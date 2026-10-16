using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>Flat glowing rings lying on the ground (story slots, the player's marker).</summary>
    public static class Rings
    {

        /// <summary>A ring between radii r0 and r1 using the additive glow material, tinted per instance.</summary>
        public static GameObject Make(string name, float r0, float r1, Material mat, Color color)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = Ring(r0, r1);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            mr.SetPropertyBlock(block);
            return go;
        }

        static Mesh Ring(float r0, float r1)
        {
            const int seg = 64;
            var mesh = new Mesh { name = $"Ring_{r0:F2}_{r1:F2}" };
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var t = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = d * r0;
                v[i * 2 + 1] = d * r1;
                uv[i * 2] = new Vector2((float)i / seg, 0);
                uv[i * 2 + 1] = new Vector2((float)i / seg, 1);
            }
            for (int i = 0; i < seg; i++)
            {
                int k = i * 6, b = i * 2;
                t[k] = b; t[k + 1] = b + 1; t[k + 2] = b + 2;
                t[k + 3] = b + 1; t[k + 4] = b + 3; t[k + 5] = b + 2;
            }
            var colors = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) colors[i] = new Color(1, 1, 1, i % 2 == 0 ? 0.25f : 1f);
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
