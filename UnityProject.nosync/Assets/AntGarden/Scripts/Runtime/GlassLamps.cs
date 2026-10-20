using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's glass-bulbed plants (ParticleLook.glassKinds): a soft light inside each bulb, seen through
    /// the gaps of its broken shell of splats (drawn after them, additive, so the splats in front of it hide it). Drawn
    /// with the threads' shader (Shaders/LightThread.shader) as a round knot of light facing the camera, in the plant's
    /// glow colour; each breathes slowly at its own pace, and fades out far away and right in front of the lens.
    /// </summary>
    public class GlassLamps : MonoBehaviour
    {
        public float gain = 0.9f;            // brightness (HDR): a quiet light, far below the threads'
        public float reach = 32f;            // not drawn further from the camera than this
        public float spread = 1.6f;          // the knot's size against the light's radius (its halo fills the bulb)

        struct Lamp { public Vector3 pos; public float size; public Color col; public float seed; }

        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Vector3> verts = new List<Vector3>(4096);
        readonly List<Color> cols = new List<Color>(4096);
        readonly List<Vector2> uvs = new List<Vector2>(4096), uv2s = new List<Vector2>(4096);
        readonly List<int> tris = new List<int>(6144);
        Material mat;
        Mesh mesh;
        Camera cam;

        public void Init(List<(Vector3 pos, float size, Color col)> from, Material material)
        {
            if (material == null || from == null || from.Count == 0) { enabled = false; return; }
            mat = new Material(material) { name = "GlassLamps" };   // its own: the threads set theirs
            mat.SetFloat("_Gain", gain);
            mat.SetColor("_Core", new Color(0.95f, 0.98f, 0.96f).linear);
            mat.SetColor("_Halo", Color.white);
            foreach (var (pos, size, col) in from)
                lamps.Add(new Lamp { pos = pos, size = size, col = col, seed = Mathf.Repeat(pos.x * 12.9898f + pos.z * 78.233f, 97f) });
            mesh = new Mesh { name = "GlassLamps" };
            mesh.MarkDynamic();
            Debug.Log($"[AntGarden] glass lamps: {lamps.Count}");
        }

        void LateUpdate()
        {
            if (mesh == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            verts.Clear(); cols.Clear(); uvs.Clear(); uv2s.Clear(); tris.Clear();
            Vector3 eye = cam.transform.position, fwd = cam.transform.forward, right = cam.transform.right, up = cam.transform.up;
            float t = Time.time, r2 = reach * reach;
            float pixelK = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            foreach (Lamp l in lamps)
            {
                Vector3 v = l.pos - eye;
                float d2 = v.sqrMagnitude;
                if (d2 > r2 || Vector3.Dot(v, fwd) < -l.size) continue;
                float dist = Mathf.Sqrt(d2);
                float breathe = 0.6f + 0.4f * Mathf.Sin(t * (0.35f + 0.25f * Mathf.Repeat(l.seed, 1f)) + l.seed);
                float fade = (1 - Mathf.SmoothStep(0, 1, (dist - reach * 0.6f) / (reach * 0.4f))) * Mathf.SmoothStep(0.3f, 1.2f, dist);
                float a = breathe * fade;
                if (a < 0.01f) continue;
                float s = Mathf.Max(l.size * spread, dist * pixelK * 3f);
                int i = verts.Count;
                verts.Add(l.pos + (-right - up) * s); verts.Add(l.pos + (right - up) * s);
                verts.Add(l.pos + (right + up) * s); verts.Add(l.pos + (-right + up) * s);
                var c = new Color(l.col.r, l.col.g, l.col.b, a);
                for (int k = 0; k < 4; k++) { cols.Add(c); uv2s.Add(new Vector2(1, 0)); }
                uvs.Add(new Vector2(-1, -1)); uvs.Add(new Vector2(1, -1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(-1, 1));
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            mesh.Clear();
            if (verts.Count == 0) return;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, uv2s);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(eye, Vector3.one * (reach * 2 + 10));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (mesh != null) Destroy(mesh);
        }
    }
}
