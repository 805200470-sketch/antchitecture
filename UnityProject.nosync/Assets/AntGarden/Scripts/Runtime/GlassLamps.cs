using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's glass-bulbed plants (ParticleLook.glassKinds): a soft light inside each bulb, seen through
    /// the gaps of its broken shell of splats (drawn after them, additive, so the splats in front of it hide it). Drawn
    /// with the threads' shader (Shaders/LightThread.shader) as a round knot of light facing the camera, in the plant's
    /// glow colour; it breathes slowly with the plants' glowing parts (NestBreath), and fades out far away and right in
    /// front of the lens.
    /// </summary>
    public class GlassLamps : MonoBehaviour
    {
        public float gain = 1.38f;           // brightness (HDR): a quiet light, far below the threads' (1.15 until 10-29, when the fade before the lens below topped out at 1.2: Mathf.SmoothStep(0.3, 1.2, d) lerps from 0.3 to 1.2, it is not HLSL's smoothstep)
        public float reach = 32f;            // not drawn further from the camera than this
        public float spread = 1.6f;          // the knot's size against the light's radius (its halo fills the bulb)

        struct Lamp { public Vector3 pos; public float size; public Color col; }

        readonly List<Lamp> lamps = new List<Lamp>();
        struct Orbit { public Vector3 centre; public float radius; public Vector2 height; public float speed, phase, size; public Color col; }
        readonly List<Orbit> orbits = new List<Orbit>();
        public float orbitGain = 0.4f;       // the motes circling the finely made plants: quieter than the bulbs (one passing the lens was the brightest thing in view)
        readonly List<Vector3> verts = new List<Vector3>(4096);
        readonly List<Color> cols = new List<Color>(4096);
        readonly List<Vector2> uvs = new List<Vector2>(4096), uv2s = new List<Vector2>(4096);
        readonly List<int> tris = new List<int>(6144);
        Material mat;
        Mesh mesh;
        Camera cam;

        public void Init(List<(Vector3 pos, float size, Color col)> from, Material material,
            List<(Vector3 centre, float radius, Vector2 height, float speed, float phase, float size, Color col)> circling = null)
        {
            if (material == null || ((from == null || from.Count == 0) && (circling == null || circling.Count == 0))) { enabled = false; return; }
            if (circling != null)
                foreach (var (centre, radius, height, speed, phase, size, col) in circling)
                    orbits.Add(new Orbit { centre = centre, radius = radius, height = height, speed = speed, phase = phase, size = size, col = col });
            mat = new Material(material) { name = "GlassLamps" };   // its own: the threads set theirs
            mat.SetFloat("_Gain", gain);
            mat.SetColor("_Core", (NestTheme.Current ?? NestTheme.Cyan).lampCore.linear);
            mat.SetColor("_Halo", Color.white);
            if (from != null)
                foreach (var (pos, size, col) in from)
                    lamps.Add(new Lamp { pos = pos, size = size, col = col });
            mesh = new Mesh { name = "GlassLamps" };
            mesh.MarkDynamic();
            Debug.Log($"[AntGarden] glass lamps: {lamps.Count}, motes circling plants: {orbits.Count}");
        }

        void LateUpdate()
        {
            if (mesh == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            verts.Clear(); cols.Clear(); uvs.Clear(); uv2s.Clear(); tris.Clear();
            Vector3 eye = cam.transform.position, fwd = cam.transform.forward, right = cam.transform.right, up = cam.transform.up;
            float r2 = reach * reach;
            float pixelK = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            foreach (Lamp l in lamps)
            {
                Vector3 v = l.pos - eye;
                float d2 = v.sqrMagnitude;
                if (d2 > r2 || Vector3.Dot(v, fwd) < -l.size) continue;
                float dist = Mathf.Sqrt(d2);
                float b = WalkLight.Breath(l.pos, NestClock.SinceLoad);
                float breathe = 0.45f + 0.55f * b * b;
                float fade = (1 - Mathf.SmoothStep(0, 1, (dist - reach * 0.6f) / (reach * 0.4f))) * Mathf.SmoothStep(0, 1, (dist - 0.3f) / 0.9f);
                float a = breathe * fade * (1f + 2f * (WalkLight.Instance != null ? WalkLight.Instance.LitAt(l.pos) : 0f));
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
            // the motes circling the finely made plants: round the stem, rising and sinking slowly (NestClock)
            float now = NestClock.SinceLoad;
            foreach (Orbit o in orbits)
            {
                float ang = o.phase + o.speed * now;
                float y = Mathf.Lerp(o.height.x, o.height.y, 0.5f + 0.5f * Mathf.Sin(now * Mathf.Abs(o.speed) * 0.37f + o.phase * 1.7f));
                Vector3 pos = o.centre + new Vector3(Mathf.Cos(ang) * o.radius, y, Mathf.Sin(ang) * o.radius);
                Vector3 v = pos - eye;
                float d2 = v.sqrMagnitude;
                if (d2 > r2 || Vector3.Dot(v, fwd) < 0) continue;
                float dist = Mathf.Sqrt(d2);
                float twinkle = 0.6f + 0.4f * Mathf.Sin(now * (1.3f + 0.7f * Mathf.Abs(o.speed)) + o.phase * 3.1f);
                float fade = (1 - Mathf.SmoothStep(0, 1, (dist - reach * 0.5f) / (reach * 0.5f))) * Mathf.SmoothStep(0, 1, (dist - 0.3f) / 0.9f);
                float a = orbitGain * twinkle * fade;
                if (a < 0.01f) continue;
                float s = Mathf.Max(o.size, dist * pixelK * 2.5f);
                int i = verts.Count;
                verts.Add(pos + (-right - up) * s); verts.Add(pos + (right - up) * s);
                verts.Add(pos + (right + up) * s); verts.Add(pos + (-right + up) * s);
                var c = new Color(o.col.r, o.col.g, o.col.b, a);
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
