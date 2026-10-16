using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>Glowing rings on the ground: flat ones (story slots), and the player's marker draped over the surface.</summary>
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

        /// <summary>
        /// A ring that lies on the ground rather than floating flat: each frame its corners are set on the surface
        /// under them (over pebbles and leaves too), round a centre that moves.
        /// </summary>
        public sealed class Draped
        {
            public readonly GameObject go;
            readonly Mesh mesh;
            readonly Vector3[] shape, verts;

            public Draped(string name, float r0, float r1, Material mat, Color color)
            {
                go = Make(name, r0, r1, mat, color);
                mesh = go.GetComponent<MeshFilter>().sharedMesh;   // its own mesh: Ring makes a new one each time
                mesh.MarkDynamic();
                shape = mesh.vertices;
                verts = new Vector3[shape.Length];
            }

            public void Drape(World w, Vector3 centre, float lift = 0.05f)
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // the corners are in world space
                for (int i = 0; i < shape.Length; i++)
                {
                    float x = centre.x + shape[i].x, z = centre.z + shape[i].z;
                    verts[i] = new Vector3(x, w.WalkHeight(x, z) + lift, z);
                }
                mesh.vertices = verts;
                mesh.RecalculateBounds();
            }
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
