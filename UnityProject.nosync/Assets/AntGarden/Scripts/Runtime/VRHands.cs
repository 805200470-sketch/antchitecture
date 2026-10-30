using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// In a headset: the two controllers in the player's hands, where the headset tracks them, drawn as the headset's own
    /// home draws them (the system draws them there; in a game the game must; the user, 10-31: "参考本机…虚拟阁内效果"):
    /// a Quest 3 controller's round, near-black head with its stick, two buttons, a small menu button and three green
    /// lights, over a grey handle; and round each a ghost of the hand holding it, dark and see-through with a pale line
    /// along its outline (Resources/AntGarden/VR: VRController.shader, VRGhostHand.shader). Made here of rounded parts,
    /// at real size in the VR origin, so they stay the size of the player's hands whatever the world's scale; each is
    /// hidden while the headset cannot see it.
    /// </summary>
    public class VRHands : MonoBehaviour
    {
        Transform left, right;

        /// <summary>Makes both controllers in the origin, and the hands round them if asked; light: their brightness (1 as
        /// made).</summary>
        public void Init(Transform origin, float light, bool ghostHands)
        {
            var shader = Resources.Load<Shader>("AntGarden/VR/VRController");
            if (shader == null)
            {
                Debug.LogWarning("[AntGarden] VR: the controllers' shader is missing; no controllers drawn");
                return;
            }
            var mat = new Material(shader);
            mat.SetFloat("_Light", light);
            left = Make("VR Controller L", Build(true), mat, origin);
            right = Make("VR Controller R", Build(false), mat, origin);
            var ghost = ghostHands ? Resources.Load<Shader>("AntGarden/VR/VRGhostHand") : null;
            if (ghost != null)
            {
                var hand = new Material(ghost);
                hand.SetFloat("_Light", light);
                Make("VR Hand L", Hand(true), hand, left).gameObject.SetActive(true);
                Make("VR Hand R", Hand(false), hand, right).gameObject.SetActive(true);
            }
            Application.onBeforeRender += Pose;
        }

        void OnDestroy() => Application.onBeforeRender -= Pose;

        void Update() => Pose();

        /// <summary>Each controller where the headset sees it, as late as possible before a frame is drawn.</summary>
        void Pose()
        {
            Place(left, XRNode.LeftHand);
            Place(right, XRNode.RightHand);
        }

        static void Place(Transform t, XRNode node)
        {
            if (t == null) return;
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            bool seen = d.isValid && (!d.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || tracked);
            if (seen && d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p) && d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q))
            {
                t.localPosition = p;
                t.localRotation = q;
            }
            else seen = false;
            if (t.gameObject.activeSelf != seen) t.gameObject.SetActive(seen);
        }

        static Transform Make(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        // the grip's space: metres, +z the way the pointing finger points, +y up out of the top of the fist, +x to the
        // right; the handle runs through the fist with its top leaning forward
        static readonly Quaternion Lean = Quaternion.Euler(35f, 0f, 0f);

        /// <summary>One controller (the right hand's, or mirrored for the left) in the grip's space.</summary>
        static Mesh Build(bool leftHand)
        {
            var m = new Parts();
            var handle = new Color(0.12f, 0.12f, 0.125f, 0f);   // mid grey
            var head = new Color(0.022f, 0.022f, 0.025f, 0f);   // near black
            var face = new Color(0.012f, 0.012f, 0.014f, 0f);
            var dark = new Color(0.03f, 0.03f, 0.033f, 0.05f);  // the stick and the buttons (alpha: how much a part glows)
            var pale = new Color(0.35f, 0.36f, 0.38f, 0.2f);    // the menu button (Meta's on the right hand)
            var led = new Color(0.08f, 0.7f, 0.25f, 1f);        // three green lights
            m.Ellipsoid(new Vector3(0f, -0.012f, -0.008f), Lean, new Vector3(0.018f, 0.056f, 0.016f), handle);
            // the round head over it, tipped a little back toward the player
            Quaternion tip = Quaternion.Euler(-15f, 0f, 0f);
            Vector3 centre = new Vector3(-0.002f, 0.042f, 0.03f), top = centre + tip * new Vector3(0f, 0.011f, 0f);
            m.Ellipsoid(centre, tip, new Vector3(0.031f, 0.011f, 0.031f), head);
            m.Ellipsoid(centre + tip * new Vector3(0f, 0.007f, 0f), tip, new Vector3(0.027f, 0.0055f, 0.027f), face);
            // the stick by the thumb, the two buttons beyond it (A and B on the right hand, X and Y on the left), the menu
            // button nearer the player
            m.Cylinder(top + tip * new Vector3(-0.009f, 0.003f, -0.006f), tip, 0.0042f, 0.01f, dark);
            m.Cylinder(top + tip * new Vector3(-0.009f, 0.009f, -0.006f), tip, 0.0105f, 0.004f, dark);
            m.Cylinder(top + tip * new Vector3(0.010f, 0.0005f, 0.010f), tip, 0.0062f, 0.004f, dark);
            m.Cylinder(top + tip * new Vector3(0.015f, 0.0005f, -0.006f), tip, 0.0062f, 0.004f, dark);
            m.Cylinder(top + tip * new Vector3(0.004f, 0f, -0.019f), tip, 0.0035f, 0.003f, pale);
            // the green lights down the head's inner edge
            for (int i = -1; i <= 1; i++) m.Ellipsoid(centre + tip * new Vector3(-0.0305f, 0.002f, 0.0045f * i), tip, Vector3.one * 0.0013f, led, 4, 6);
            // the trigger under the head's front, where the index finger rests
            m.Ellipsoid(new Vector3(-0.002f, 0.02f, 0.058f), Quaternion.Euler(40f, 0f, 0f), new Vector3(0.008f, 0.014f, 0.009f), handle);
            return m.ToMesh(leftHand ? "VR Controller L" : "VR Controller R", leftHand);
        }

        /// <summary>The hand round one controller, in the same space: the palm on the handle's outer side, the fingers
        /// wrapped round its front (the index over the trigger), the thumb over the head to the stick, the wrist fading
        /// toward the arm.</summary>
        static Mesh Hand(bool leftHand)
        {
            var m = new Parts();
            Color skin = Color.white;   // (the shader's own colours; the alpha fades)
            Vector3 x = Vector3.right, up = Lean * Vector3.up, front = Lean * Vector3.forward;
            // a point round the handle: along it, at an angle from its outer side toward its front, this far from its middle
            Vector3 At(float along, float angle, float r) => up * along + (x * Mathf.Cos(angle * Mathf.Deg2Rad) + front * Mathf.Sin(angle * Mathf.Deg2Rad)) * r;
            Vector3 outward = At(0f, -15f, 1f);
            m.Ellipsoid(At(0f, -15f, 0.03f), Quaternion.LookRotation(Vector3.Cross(outward, up), up), new Vector3(0.013f, 0.045f, 0.036f), skin);
            float[] along = { 0.045f, 0.013f, -0.012f, -0.034f }, thick = { 0.0085f, 0.0088f, 0.0082f, 0.0072f }, reach = { 0.04f, 0.028f, 0.027f, 0.026f };
            for (int f = 0; f < 4; f++)
            {
                Vector3 knuckle = At(along[f], 35f, reach[f]), p1 = At(along[f], 95f, reach[f]), p2 = At(along[f] - 0.004f, 155f, reach[f]);
                Vector3 end = At(along[f] - 0.008f, 205f, reach[f] * 0.9f);
                m.Capsule(knuckle, p1, thick[f], skin);
                m.Capsule(p1, p2, thick[f], skin);
                m.Capsule(p2, end, thick[f] * 0.92f, skin);
            }
            Vector3 t0 = new Vector3(0.023f, 0.038f, 0.004f), t1 = new Vector3(0.006f, 0.061f, 0.012f), t2 = new Vector3(-0.01f, 0.067f, 0.02f);
            m.Capsule(t0, t1, 0.0098f, skin);
            m.Capsule(t1, t2, 0.0088f, skin);
            Vector3 wrist = At(-0.035f, -30f, 0.025f);
            m.Capsule(wrist, wrist + new Vector3(0.15f, -0.35f, -0.92f).normalized * 0.07f, 0.024f, skin, true);
            return m.ToMesh(leftHand ? "VR Hand L" : "VR Hand R", leftHand);
        }

        /// <summary>Rounded parts gathered into one mesh, each part its own colour (Unity's winding: clockwise seen
        /// from outside).</summary>
        class Parts
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<Color> c = new List<Color>();
            readonly List<int> t = new List<int>();

            public void Ellipsoid(Vector3 at, Quaternion rot, Vector3 radii, Color col, int rings = 10, int segs = 18)
            {
                int start = v.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float phi = Mathf.PI * i / rings;
                    for (int j = 0; j <= segs; j++)
                    {
                        float th = 2f * Mathf.PI * j / segs;
                        var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        v.Add(at + rot * Vector3.Scale(unit, radii));
                        n.Add(rot * new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized);
                        c.Add(col);
                    }
                }
                Rows(start, rings + 1, segs);
            }

            /// <summary>A capsule from a to b; fade: its colour's alpha falling from a to nothing at b.</summary>
            public void Capsule(Vector3 a, Vector3 b, float radius, Color col, bool fade = false, int segs = 12, int cap = 4)
            {
                Vector3 axis = b - a;
                float h = axis.magnitude;
                Vector3 dir = h > 1e-6f ? axis / h : Vector3.up, mid = (a + b) * 0.5f;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir);
                int start = v.Count;
                for (int i = 0; i <= 2 * cap + 1; i++)
                {
                    // the end toward b, then the end toward a; the rows between the two halves make the side
                    bool toB = i <= cap;
                    float phi = Mathf.PI * 0.5f * (toB ? i : i - 1) / cap;
                    float y = Mathf.Cos(phi) * radius + (toB ? 0.5f : -0.5f) * h, ring = Mathf.Sin(phi) * radius;
                    for (int j = 0; j <= segs; j++)
                    {
                        float th = 2f * Mathf.PI * j / segs;
                        v.Add(mid + rot * new Vector3(ring * Mathf.Cos(th), y, ring * Mathf.Sin(th)));
                        n.Add(rot * new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)));
                        Color k = col;
                        if (fade) k.a *= Mathf.Clamp01(1f - Vector3.Dot(v[v.Count - 1] - a, dir) / Mathf.Max(h, 1e-6f));
                        c.Add(k);
                    }
                }
                Rows(start, 2 * cap + 2, segs);
            }

            /// <summary>A short cylinder along rot's up, centred at at, with flat ends.</summary>
            public void Cylinder(Vector3 at, Quaternion rot, float radius, float height, Color col, int segs = 16)
            {
                int start = v.Count;
                for (int j = 0; j <= segs; j++)
                {
                    float th = 2f * Mathf.PI * j / segs;
                    var round = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                    for (int k = 0; k < 2; k++)
                    {
                        v.Add(at + rot * (round * radius + Vector3.up * (k == 0 ? 0.5f : -0.5f) * height));
                        n.Add(rot * round);
                        c.Add(col);
                    }
                }
                for (int j = 0; j < segs; j++)
                {
                    int a = start + j * 2;   // a: its top, a + 1: its bottom; a + 2 and a + 3 the next pair round
                    t.Add(a); t.Add(a + 2); t.Add(a + 1);
                    t.Add(a + 2); t.Add(a + 3); t.Add(a + 1);
                }
                for (int k = 0; k < 2; k++)
                {
                    float y = (k == 0 ? 0.5f : -0.5f) * height;
                    Vector3 nrm = rot * (k == 0 ? Vector3.up : Vector3.down);
                    int centre = v.Count;
                    v.Add(at + rot * (Vector3.up * y));
                    n.Add(nrm);
                    c.Add(col);
                    for (int j = 0; j <= segs; j++)
                    {
                        float th = 2f * Mathf.PI * j / segs;
                        v.Add(at + rot * (new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th)) * radius + Vector3.up * y));
                        n.Add(nrm);
                        c.Add(col);
                    }
                    for (int j = 0; j < segs; j++)
                    {
                        if (k == 0) { t.Add(centre); t.Add(centre + j + 2); t.Add(centre + j + 1); }
                        else { t.Add(centre); t.Add(centre + j + 1); t.Add(centre + j + 2); }
                    }
                }
            }

            /// <summary>The triangles between rows of segs + 1 vertices each, the rows running down from the top.</summary>
            void Rows(int start, int rows, int segs)
            {
                for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < segs; j++)
                    {
                        int a = start + i * (segs + 1) + j, b = a + segs + 1;
                        t.Add(a); t.Add(a + 1); t.Add(b);
                        t.Add(a + 1); t.Add(b + 1); t.Add(b);
                    }
            }

            /// <summary>The mesh, mirrored left to right if asked (the left hand's).</summary>
            public Mesh ToMesh(string name, bool mirror)
            {
                if (mirror)
                {
                    for (int i = 0; i < v.Count; i++)
                    {
                        v[i] = new Vector3(-v[i].x, v[i].y, v[i].z);
                        n[i] = new Vector3(-n[i].x, n[i].y, n[i].z);
                    }
                    for (int i = 0; i < t.Count; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                }
                var mesh = new Mesh { name = name };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetColors(c);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
