using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// Turns one region of the world into renderable ground: chunked terrain meshes (heights include the
    /// world's relief) whose outline follows the hexagon rim exactly, plus soil cross-section walls under
    /// the outer rim. A chunk per frame on the main thread, so the loading bar keeps moving.
    /// </summary>
    public static class TerrainBuilder
    {
        const float ChunkSize = 32f;

        sealed class ChunkData
        {
            public Vector3[] verts;
            public Vector3[] normals;
            public Vector4[] tangents;
            public Color[] colors;
            public int[] tris;
        }

        /// <summary>Build the ground of <paramref name="r"/> under <paramref name="parent"/>; reports progress 0..1.</summary>
        public static IEnumerator Build(World w, Region r, Material ground, Material wall, Transform parent, float step, float sideDepth, Action<float> progress)
        {
            var rim = new List<WFrameEdge>();
            foreach (WFrameEdge e in w.frameEdges) if (e.region == r.index && !e.shared) rim.Add(e);

            int nx = Mathf.CeilToInt((r.maxX - r.minX) / ChunkSize);
            int nz = Mathf.CeilToInt((r.maxZ - r.minZ) / ChunkSize);
            var chunks = new List<(int cx, int cz, float x0, float z0)>();
            for (int cz = 0; cz < nz; cz++)
            for (int cx = 0; cx < nx; cx++)
            {
                float x0 = r.minX + cx * ChunkSize;
                float z0 = r.minZ + cz * ChunkSize;
                if (ChunkTouchesFrame(r, x0, z0)) chunks.Add((cx, cz, x0, z0));
            }

            var root = new GameObject($"Terrain_r{r.index}").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                ChunkData d = BuildChunk(w, r, rim, c.x0, c.z0, step);
                if (d.tris.Length > 0) MakeMeshObject($"Chunk_{c.cx}_{c.cz}", root, ground, d, true);
                progress?.Invoke((i + 1f) / (chunks.Count + 1f));
                yield return null;
            }

            ChunkData walls = BuildWalls(w, rim, step * 1.5f, sideDepth);
            MakeMeshObject("SoilWalls", root, wall, walls, false);
            progress?.Invoke(1f);
        }

        /// <summary>
        /// The ground of a honeycomb: one continuous grid over all its cells, so nothing cracks at the seams; a
        /// triangle that crosses from one cell into another is cut along the hexagon's edge and each piece takes
        /// its own cell's ground material, so the colours change along a clean straight line.
        /// </summary>
        /// <param name="seamShader">a material with AntGarden/TerrainSeam: where neighbouring cells have different
        /// ground, the two interleave in patches across the seam instead of meeting along a line (null: a line)</param>
        public static IEnumerator BuildCells(World w, List<Region> cells, List<Material> groundByRegion, Material wall, Transform parent, float step, float sideDepth, Action<float> progress, Material seamShader = null)
        {
            var seams = seamShader != null ? SeamEdges(cells) : new Dictionary<int, List<SeamEdge>>();
            var seamMats = new Dictionary<int, Material>();
            Material SeamMat(int region)
            {
                if (!seamMats.TryGetValue(region, out Material m))
                {
                    m = new Material(groundByRegion[region]) { name = groundByRegion[region].name + "_seam", shader = seamShader.shader, renderQueue = -1 };
                    seamMats[region] = m;
                }
                return m;
            }
            var rim = new List<WFrameEdge>();
            foreach (WFrameEdge e in w.frameEdges) if (!e.shared && w.regions[e.region].cell) rim.Add(e);
            Region r0 = cells[0];
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Region c in cells) { minX = Mathf.Min(minX, c.minX); maxX = Mathf.Max(maxX, c.maxX); minZ = Mathf.Min(minZ, c.minZ); maxZ = Mathf.Max(maxZ, c.maxZ); }
            // one lattice for the whole honeycomb (chunks start on it), so neighbouring chunks share their edges
            minX = Mathf.Floor(minX / ChunkSize) * ChunkSize;
            minZ = Mathf.Floor(minZ / ChunkSize) * ChunkSize;
            int nx = Mathf.CeilToInt((maxX - minX) / ChunkSize), nz = Mathf.CeilToInt((maxZ - minZ) / ChunkSize);
            var chunks = new List<(int cx, int cz, float x0, float z0)>();
            for (int cz = 0; cz < nz; cz++)
            for (int cx = 0; cx < nx; cx++)
            {
                float x0 = minX + cx * ChunkSize, z0 = minZ + cz * ChunkSize;
                if (ChunkTouchesFrame(r0, x0, z0)) chunks.Add((cx, cz, x0, z0));
            }
            var root = new GameObject("Terrain_honeycomb").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                ChunkData d = BuildChunk(w, r0, rim, c.x0, c.z0, step);
                var parts = SplitByCell(w, d);
                if (parts.Count > 0)
                {
                    var mats = new List<Material>();
                    var lists = new List<int[]>();
                    foreach (var kv in parts) { mats.Add(groundByRegion[kv.Key]); lists.Add(kv.Value.ToArray()); }
                    MakeMultiMeshObject($"Chunk_{c.cx}_{c.cz}", root, mats, d, lists);
                    if (seams.Count > 0) SeamOverlay($"Seam_{c.cx}_{c.cz}", root, d, parts, seams, SeamMat);
                }
                progress?.Invoke((i + 1f) / (chunks.Count + 1f));
                yield return null;
            }
            MakeMeshObject("SoilWalls", root, wall, BuildWalls(w, rim, step * 1.5f, sideDepth), false);
            progress?.Invoke(1f);
        }

        /// <summary>
        /// Sort a chunk's triangles by cell; a triangle that straddles cells is clipped to each cell's hexagon,
        /// the new corners interpolated from the triangle (so both sides meet exactly). Appends to d's vertices.
        /// </summary>
        static Dictionary<int, List<int>> SplitByCell(World w, ChunkData d)
        {
            var verts = new List<Vector3>(d.verts);
            var normals = new List<Vector3>(d.normals);
            var tangents = new List<Vector4>(d.tangents);
            var colors = new List<Color>(d.colors);
            var cellOf = new int[d.verts.Length];
            for (int k = 0; k < d.verts.Length; k++) cellOf[k] = w.RegionAt(d.verts[k].x, d.verts[k].z).index;
            var parts = new Dictionary<int, List<int>>();
            List<int> Part(int cell) { if (!parts.TryGetValue(cell, out var l)) parts[cell] = l = new List<int>(); return l; }
            for (int t = 0; t < d.tris.Length; t += 3)
            {
                int a = d.tris[t], b = d.tris[t + 1], c = d.tris[t + 2];
                if (cellOf[a] == cellOf[b] && cellOf[b] == cellOf[c]) { var l = Part(cellOf[a]); l.Add(a); l.Add(b); l.Add(c); continue; }
                // the cells of the corners, plus any whose corner point lies inside the triangle (where three meet)
                var touched = new HashSet<int> { cellOf[a], cellOf[b], cellOf[c] };
                Vector3 mid = (d.verts[a] + d.verts[b] + d.verts[c]) / 3;
                touched.Add(w.RegionAt(mid.x, mid.z).index);
                touched.Add(w.RegionAt((d.verts[a].x + d.verts[b].x) / 2, (d.verts[a].z + d.verts[b].z) / 2).index);
                touched.Add(w.RegionAt((d.verts[b].x + d.verts[c].x) / 2, (d.verts[b].z + d.verts[c].z) / 2).index);
                touched.Add(w.RegionAt((d.verts[c].x + d.verts[a].x) / 2, (d.verts[c].z + d.verts[a].z) / 2).index);
                foreach (int cell in touched)
                {
                    Region rc = w.regions[cell];
                    // the polygon as barycentric weights over (a, b, c), clipped by the hexagon's six half-planes
                    var poly = new List<Vector3> { new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1) };
                    for (int e = 0; e < 6 && poly.Count >= 3; e++)
                    {
                        float ang = e * Mathf.PI / 3;
                        float ex = Mathf.Cos(ang), ez = Mathf.Sin(ang), lim = rc.cellR * 0.8660254f;
                        float Side(Vector3 bc)
                        {
                            Vector3 p = d.verts[a] * bc.x + d.verts[b] * bc.y + d.verts[c] * bc.z;
                            return (p.x - rc.cellX) * ex + (p.z - rc.cellZ) * ez - lim;   // <= 0 inside
                        }
                        var next = new List<Vector3>();
                        for (int k = 0; k < poly.Count; k++)
                        {
                            Vector3 p = poly[k], q = poly[(k + 1) % poly.Count];
                            float sp = Side(p), sq = Side(q);
                            if (sp <= 0) next.Add(p);
                            if ((sp <= 0) != (sq <= 0)) next.Add(Vector3.Lerp(p, q, sp / (sp - sq)));
                        }
                        poly = next;
                    }
                    if (poly.Count < 3) continue;
                    var idx = new int[poly.Count];
                    for (int k = 0; k < poly.Count; k++)
                    {
                        Vector3 bc = poly[k];
                        idx[k] = verts.Count;
                        verts.Add(d.verts[a] * bc.x + d.verts[b] * bc.y + d.verts[c] * bc.z);
                        normals.Add((d.normals[a] * bc.x + d.normals[b] * bc.y + d.normals[c] * bc.z).normalized);
                        tangents.Add(d.tangents[a] * bc.x + d.tangents[b] * bc.y + d.tangents[c] * bc.z);
                        colors.Add(d.colors[a] * bc.x + d.colors[b] * bc.y + d.colors[c] * bc.z);
                    }
                    var list = Part(cell);
                    for (int k = 1; k + 1 < poly.Count; k++) { list.Add(idx[0]); list.Add(idx[k]); list.Add(idx[k + 1]); }
                }
            }
            d.verts = verts.ToArray();
            d.normals = normals.ToArray();
            d.tangents = tangents.ToArray();
            d.colors = colors.ToArray();
            return parts;
        }

        /// <summary>A seam between this cell and a neighbour with different ground.</summary>
        sealed class SeamEdge
        {
            public int other;
            public float mx, mz;   // the middle of the shared edge
            public float nx, nz;   // unit normal from this cell into the other
            public float tx, tz;   // along the edge
            public float half;     // half the edge's length
            public bool second;    // this cell has the higher index of the pair: its side of the pattern is inverted
        }

        const float SeamBand = 4.5f;   // how far into each cell the two grounds interleave

        static Dictionary<int, List<SeamEdge>> SeamEdges(List<Region> cells)
        {
            var map = new Dictionary<int, List<SeamEdge>>();
            void Add(Region a, Region b, Vector2 mid, Vector2 n)
            {
                if (!map.TryGetValue(a.index, out var list)) map[a.index] = list = new List<SeamEdge>();
                list.Add(new SeamEdge { other = b.index, mx = mid.x, mz = mid.y, nx = n.x, nz = n.y, tx = -n.y, tz = n.x, half = a.cellR / 2, second = a.index > b.index });
            }
            for (int i = 0; i < cells.Count; i++)
            for (int j = i + 1; j < cells.Count; j++)
            {
                Region a = cells[i], b = cells[j];
                if (a.biome.id == b.biome.id) continue;   // the same ground: the seam does not show
                var ca = new Vector2(a.cellX, a.cellZ);
                var cb = new Vector2(b.cellX, b.cellZ);
                float apothem = a.cellR * 0.8660254f, dist = (cb - ca).magnitude;
                if (Mathf.Abs(dist - 2 * apothem) > apothem * 0.1f) continue;   // not neighbours
                Vector2 mid = (ca + cb) / 2, n = (cb - ca) / dist;
                Add(a, b, mid, n);
                Add(b, a, mid, -n);
            }
            return map;
        }

        /// <summary>
        /// The other ground's share at a point of this cell near a seam: 0.5 on the seam, falling to 0 a band's
        /// width in, the band's reach wavering with a slow noise. The same field from both sides (the second cell
        /// of the pair reads the complement), so the patches meet across the seam.
        /// </summary>
        static float SeamShare(SeamEdge e, float x, float z)
        {
            float s = (x - e.mx) * e.nx + (z - e.mz) * e.nz;          // < 0 inside this cell
            float along = Mathf.Abs((x - e.mx) * e.tx + (z - e.mz) * e.tz);
            float waver = 0.3f * (Mathf.PerlinNoise(x * 0.05f + 11.3f, z * 0.05f - 4.1f) - 0.5f) * (1 - Mathf.Clamp01(Mathf.Abs(s) / SeamBand));
            float share = 0.5f + 0.5f * s / SeamBand + (e.second ? -waver : waver);
            // where three cells meet the pattern gives way to the plain seam
            share *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(e.half - 1.5f, e.half + 1f, along));
            return Mathf.Clamp01(share);
        }

        /// <summary>The seam band of a chunk: its triangles again, in the neighbouring cell's ground, holed by noise.</summary>
        static void SeamOverlay(string name, Transform parent, ChunkData d, Dictionary<int, List<int>> parts, Dictionary<int, List<SeamEdge>> seams, Func<int, Material> seamMat)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var subs = new Dictionary<int, List<int>>();
            foreach (var kv in parts)
            {
                if (!seams.TryGetValue(kv.Key, out var edges)) continue;
                List<int> tri = kv.Value;
                for (int t = 0; t < tri.Count; t += 3)
                {
                    Vector3 c = (d.verts[tri[t]] + d.verts[tri[t + 1]] + d.verts[tri[t + 2]]) / 3;
                    // the nearest seam within the band (a triangle joins one overlay only)
                    SeamEdge best = null;
                    float bestS = -SeamBand - 0.6f;
                    foreach (SeamEdge e in edges)
                    {
                        float s = (c.x - e.mx) * e.nx + (c.z - e.mz) * e.nz;
                        float along = Mathf.Abs((c.x - e.mx) * e.tx + (c.z - e.mz) * e.tz);
                        if (s > bestS && along < e.half + 1f) { bestS = s; best = e; }
                    }
                    if (best == null) continue;
                    float w0 = SeamShare(best, d.verts[tri[t]].x, d.verts[tri[t]].z);
                    float w1 = SeamShare(best, d.verts[tri[t + 1]].x, d.verts[tri[t + 1]].z);
                    float w2 = SeamShare(best, d.verts[tri[t + 2]].x, d.verts[tri[t + 2]].z);
                    if (w0 < 0.02f && w1 < 0.02f && w2 < 0.02f) continue;
                    if (!subs.TryGetValue(best.other, out var list)) subs[best.other] = list = new List<int>();
                    float flip = best.second ? 1 : 0;
                    for (int k = 0; k < 3; k++)
                    {
                        int vi = tri[t + k];
                        list.Add(verts.Count);
                        verts.Add(d.verts[vi]);
                        normals.Add(d.normals[vi]);
                        tangents.Add(d.tangents[vi]);
                        colors.Add(d.colors[vi]);
                        uv.Add(new Vector2(k == 0 ? w0 : k == 1 ? w1 : w2, flip));
                    }
                }
            }
            if (subs.Count == 0) return;
            var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs.Count;
            var mats = new List<Material>();
            int si = 0;
            foreach (var kv in subs) { mesh.SetTriangles(kv.Value, si++); mats.Add(seamMat(kv.Key)); }
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats.ToArray();
            mr.receiveShadows = true;
            mr.shadowCastingMode = ShadowCastingMode.Off;   // the terrain below casts them
            go.isStatic = true;
        }

        static void MakeMultiMeshObject(string name, Transform parent, List<Material> mats, ChunkData d, List<int[]> submeshes)
        {
            var mesh = new Mesh { name = name };
            if (d.verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(d.verts);
            mesh.SetNormals(d.normals);
            mesh.SetTangents(d.tangents);
            mesh.SetColors(d.colors);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats.ToArray();
            mr.receiveShadows = true;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = true;
        }

        static double LocalHexDist(Region r, double x, double z) => r.field.HexDist(x - r.offset.x, -(z - r.offset.y));

        static bool ChunkTouchesFrame(Region r, float x0, float z0)
        {
            for (int j = 0; j <= 4; j++)
            for (int i = 0; i <= 4; i++)
                if (LocalHexDist(r, x0 + i * ChunkSize / 4, z0 + j * ChunkSize / 4) < ChunkSize * 0.3) return true;
            return false;
        }

        static ChunkData BuildChunk(World w, Region r, List<WFrameEdge> rim, float x0, float z0, float step)
        {
            int n = Mathf.CeilToInt(ChunkSize / step);
            float s = ChunkSize / n;
            int g = n + 3; // heights on a grid with a one-sample border, for smooth normals across chunks
            var hgrid = new double[g * g];
            for (int j = 0; j < g; j++)
            for (int i = 0; i < g; i++)
                hgrid[j * g + i] = w.HeightD(x0 + (i - 1) * s, z0 + (j - 1) * s);

            int vw = n + 1;
            var verts = new Vector3[vw * vw];
            var normals = new Vector3[vw * vw];
            var tangents = new Vector4[vw * vw];
            var colors = new Color[vw * vw];
            var outside = new bool[vw * vw];
            for (int j = 0; j < vw; j++)
            for (int i = 0; i < vw; i++)
            {
                double x = x0 + i * s, z = z0 + j * s;
                int k = j * vw + i;
                int gi = i + 1, gj = j + 1;
                double hl = hgrid[gj * g + gi - 1], hr = hgrid[gj * g + gi + 1];
                double hd = hgrid[(gj - 1) * g + gi], hu = hgrid[(gj + 1) * g + gi];
                var nrm = new Vector3((float)(hl - hr), 2 * s, (float)(hd - hu)).normalized;
                double h = hgrid[gj * g + gi];
                if (LocalHexDist(r, x, z) > 0)
                {
                    // pull the vertex onto the rim so the ground ends exactly at the hexagon outline
                    (x, z) = ClosestOnRim(rim, x, z);
                    h = w.HeightD(x, z);
                    outside[k] = true;
                }
                verts[k] = new Vector3((float)x, (float)h, (float)z);
                normals[k] = nrm;
                Vector3 t = new Vector3(2 * s, (float)(hr - hl), 0).normalized;
                tangents[k] = new Vector4(t.x, t.y, t.z, 1);
                colors[k] = LayerWeights(w, x, z, nrm);
            }
            var tris = new List<int>(n * n * 6);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * vw + i, bq = a + 1, c = a + vw, d = c + 1;
                AddTri(tris, verts, outside, a, c, bq);
                AddTri(tris, verts, outside, bq, c, d);
            }
            return new ChunkData { verts = verts, normals = normals, tangents = tangents, colors = colors, tris = tris.ToArray() };
        }

        static void AddTri(List<int> tris, Vector3[] v, bool[] outside, int a, int b, int c)
        {
            if (outside[a] && outside[b] && outside[c]) return;
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (n.sqrMagnitude < 1e-8f) return; // collapsed onto the rim
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        static (double x, double z) ClosestOnRim(List<WFrameEdge> rim, double x, double z)
        {
            double best = double.MaxValue, bx = x, bz = z;
            foreach (WFrameEdge e in rim)
            {
                double ex = e.x1 - e.x0, ez = e.z1 - e.z0;
                double t = Math.Max(0, Math.Min(1, ((x - e.x0) * ex + (z - e.z0) * ez) / (ex * ex + ez * ez)));
                double px = e.x0 + ex * t, pz = e.z0 + ez * t;
                double d = (px - x) * (px - x) + (pz - z) * (pz - z);
                if (d < best) { best = d; bx = px; bz = pz; }
            }
            return (bx, bz);
        }

        /// <summary>r = canyon floor, g = mossy bank, b = plateau, a = cavity (1 open .. 0 occluded).</summary>
        static Color LayerWeights(World f, double x, double z, Vector3 normal)
        {
            float d = (float)f.PathDistD(x, z);
            float floor = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.6f, 0.6f, d));
            float plateau = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(f.bankWidth * 0.8f, f.bankWidth * 1.6f, d));
            float steep = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 0.6f, 1 - normal.y));
            float bank = Mathf.Max(0, 1 - floor - plateau);
            bank = Mathf.Max(bank, steep * (1 - floor));
            float sum = Mathf.Max(1e-3f, floor + bank + plateau);
            // darker crease where the floor meets the bank
            float crease = Mathf.Exp(-(d - 0.4f) * (d - 0.4f) / 0.9f);
            return new Color(floor / sum, bank / sum, plateau / sum, 1 - 0.5f * crease);
        }

        static ChunkData BuildWalls(World f, List<WFrameEdge> rim, float step, float sideDepth)
        {
            double minTop = double.MaxValue;
            foreach (WFrameEdge e in rim)
                for (int i = 0; i <= 20; i++)
                {
                    double t = i / 20.0;
                    minTop = Math.Min(minTop, f.HeightD(e.x0 + (e.x1 - e.x0) * t, e.z0 + (e.z1 - e.z0) * t));
                }
            float bottom = (float)minTop - sideDepth;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            foreach (WFrameEdge e in rim)
            {
                double ex = e.x1 - e.x0, ez = e.z1 - e.z0;
                double len = Math.Sqrt(ex * ex + ez * ez);
                int n = Math.Max(2, (int)Math.Ceiling(len / step));
                // outward normal: away from this edge's hexagon centre
                double mx = (e.x0 + e.x1) / 2 - e.cx, mz = (e.z0 + e.z1) / 2 - e.cz;
                var nrm = new Vector3((float)mx, 0, (float)mz).normalized;
                // the front face must point outward whichever way the edge runs (z is mirrored from the core)
                var faceN = Vector3.Cross(new Vector3((float)ex, 0, (float)ez), Vector3.down);
                bool flip = Vector3.Dot(faceN, nrm) < 0;
                int baseIndex = verts.Count;
                for (int i = 0; i <= n; i++)
                {
                    double t = (double)i / n;
                    double x = e.x0 + ex * t, z = e.z0 + ez * t;
                    float top = (float)f.HeightD(x, z);
                    verts.Add(new Vector3((float)x, top, (float)z));
                    verts.Add(new Vector3((float)x, bottom, (float)z));
                    normals.Add(nrm);
                    normals.Add(nrm);
                    colors.Add(new Color(0, 0, 0, 1));
                    colors.Add(new Color(1, 0, 0, 1));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = baseIndex + i * 2, b = a + 1, c = a + 2, d = a + 3;
                    // Unity front face normal = Cross(v1 - v0, v2 - v0): (a, c, b) faces along edge × down
                    if (!flip) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                }
            }
            return new ChunkData { verts = verts.ToArray(), normals = normals.ToArray(), colors = colors.ToArray(), tris = tris.ToArray() };
        }

        static void MakeMeshObject(string name, Transform parent, Material mat, ChunkData d, bool receiveShadows)
        {
            var mesh = new Mesh { name = name };
            if (d.verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(d.verts);
            mesh.SetNormals(d.normals);
            if (d.tangents != null) mesh.SetTangents(d.tangents);
            mesh.SetColors(d.colors);
            mesh.SetTriangles(d.tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.receiveShadows = receiveShadows;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = true;
        }
    }
}
