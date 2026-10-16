using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// Closes a region's canyon over into an underground tunnel: an earthen ceiling that arches over the path
    /// (highest above the middle of the canyon) and comes down to meet the ground at the top of the banks, plus
    /// roots hanging from it, some with a faint glowing tip. The ceiling height is also a query, so the camera
    /// can stay under it.
    /// </summary>
    public class TunnelRoof
    {
        /// <summary>The roof and its roots live on their own layer, so the tunnel's fill light reaches only them.</summary>
        public const int Layer = 9;

        readonly World world;
        readonly Region region;
        readonly float clearance;   // height of the arch above the canyon floor

        public TunnelRoof(World world, Region region, float clearance)
        {
            this.world = world;
            this.region = region;
            this.clearance = clearance;
        }

        /// <summary>Ceiling height at (x, z), or +infinity where there is no ceiling (beyond the banks).</summary>
        public float Height(float x, float z)
        {
            if (world.regions.Count > 1 && world.RegionAt(x, z) != region) return float.PositiveInfinity;   // other nests are open to the sky
            float d = world.PathDist(x, z);
            if (d > world.bankWidth + 2.5f) return float.PositiveInfinity;
            float t = Smooth(-1.5f, world.bankWidth + 2f, d);
            // from a high arch over the middle of the canyon down to just under the ground past the banks
            float clear = Mathf.Lerp(clearance, -0.4f, t * t);
            float wobble = Mathf.PerlinNoise(x * 0.11f + 7.3f, z * 0.11f - 2.1f) - 0.5f;
            return world.Height(x, z) + clear + wobble * 1.2f * (1 - t);
        }

        /// <summary>A smooth lower bound of the ceiling (its bumps left out), so the camera glides under it.</summary>
        public float LowHeight(float x, float z)
        {
            if (world.regions.Count > 1 && world.RegionAt(x, z) != region) return float.PositiveInfinity;
            float d = world.PathDist(x, z);
            if (d > world.bankWidth + 2.5f) return float.PositiveInfinity;
            float t = Smooth(-1.5f, world.bankWidth + 2f, d);
            return world.Height(x, z) + Mathf.Lerp(clearance, -0.4f, t * t) - 0.6f * (1 - t);
        }

        static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        /// <summary>The ceiling mesh (bank colouring, facing down) and the hanging roots.</summary>
        public void Build(Material ground, Material roots, Transform parent, float step, int seed)
        {
            int nx = Mathf.CeilToInt((region.maxX - region.minX) / step) + 1;
            int nz = Mathf.CeilToInt((region.maxZ - region.minZ) / step) + 1;
            var index = new int[nx * nz];
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = region.minX + i * step, z = region.minZ + j * step;
                float y = world.RegionAt(x, z) == region ? Height(x, z) : float.PositiveInfinity;
                if (float.IsInfinity(y)) { index[j * nx + i] = -1; continue; }
                index[j * nx + i] = verts.Count;
                verts.Add(new Vector3(x, y, z));
                colors.Add(new Color(0, 0.85f, 0.15f, 0.8f));    // bank soil, a little plateau
            }
            var tris = new List<int>();
            for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = index[j * nx + i], b = index[j * nx + i + 1], c = index[(j + 1) * nx + i], d = index[(j + 1) * nx + i + 1];
                // wound to face down (seen from inside the tunnel)
                if (a >= 0 && b >= 0 && c >= 0) { tris.Add(a); tris.Add(b); tris.Add(c); }
                if (b >= 0 && d >= 0 && c >= 0) { tris.Add(b); tris.Add(d); tris.Add(c); }
            }
            if (tris.Count == 0) return;
            var mesh = new Mesh { name = $"TunnelRoof_r{region.index}", indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // tangents along +X in each vertex's plane (the terrain shader's normal mapping needs them; the
            // roof has no UVs to derive them from)
            var normals = mesh.normals;
            var tangents = new Vector4[normals.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 t = Vector3.right - normals[i] * Vector3.Dot(normals[i], Vector3.right);
                if (t.sqrMagnitude < 1e-6f) t = Vector3.forward;
                t.Normalize();
                tangents[i] = new Vector4(t.x, t.y, t.z, 1);
            }
            mesh.tangents = tangents;
            mesh.RecalculateBounds();
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ground;
            mr.shadowCastingMode = ShadowCastingMode.TwoSided;   // the moon does not shine into a tunnel
            go.isStatic = true;
            go.layer = Layer;
            HangRoots(roots, go.transform, seed);
        }

        /// <summary>Roots hanging from the ceiling over the canyon: thin tapered strands, a few glowing at the tip.</summary>
        void HangRoots(Material mat, Transform parent, int seed)
        {
            if (mat == null) return;
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            int strands = Mathf.RoundToInt((region.maxX - region.minX) * (region.maxZ - region.minZ) / 45f);
            for (int s = 0, tries = 0; s < strands && tries < strands * 20; tries++)
            {
                float x = R(region.minX, region.maxX), z = R(region.minZ, region.maxZ);
                if (world.RegionAt(x, z) != region || world.PathDist(x, z) > 1.5f) continue;
                float top = Height(x, z);
                if (float.IsInfinity(top)) continue;
                float len = R(0.5f, 2.2f), r0 = R(0.08f, 0.16f);
                float floor = world.Height(x, z);
                len = Mathf.Min(len, top - floor - 1.6f);   // never down to where the ants walk
                if (len < 0.4f) continue;
                Vector3 sway = new Vector3(R(-0.25f, 0.25f), 0, R(-0.25f, 0.25f));
                bool glows = rng.NextDouble() < 0.18;
                const int Seg = 6, Sides = 4;
                int baseIndex = verts.Count;
                for (int k = 0; k <= Seg; k++)
                {
                    float t = k / (float)Seg;
                    Vector3 c = new Vector3(x, top + 0.1f, z) + Vector3.down * len * t + sway * t * t;
                    float r = r0 * (1 - 0.8f * t);
                    Color col = glows && k >= Seg - 1 ? new Color(0.55f, 1f, 0.95f, 1f) : new Color(0.07f, 0.065f, 0.055f, 0f);
                    for (int q = 0; q < Sides; q++)
                    {
                        float a = q * Mathf.PI * 2 / Sides;
                        verts.Add(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r);
                        colors.Add(col);
                    }
                }
                for (int k = 0; k < Seg; k++)
                for (int q = 0; q < Sides; q++)
                {
                    int i0 = baseIndex + k * Sides + q, i1 = baseIndex + k * Sides + (q + 1) % Sides;
                    int j0 = i0 + Sides, j1 = i1 + Sides;
                    tris.Add(i0); tris.Add(j0); tris.Add(i1);
                    tris.Add(i1); tris.Add(j0); tris.Add(j1);
                }
                s++;
            }
            if (tris.Count == 0) return;
            var mesh = new Mesh { name = "HangingRoots", indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("HangingRoots");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
            go.layer = Layer;
        }
    }
}
