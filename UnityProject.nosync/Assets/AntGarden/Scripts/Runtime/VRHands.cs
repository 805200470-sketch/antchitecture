using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// In a headset without Meta's runtime (a computer through Quest Link; XRDevices): the two controllers where the
    /// headset tracks them, drawn as the headset's own home draws them (the user, 10-31: "参考本机…虚拟阁内效果"): a Quest 3
    /// controller's round, near-black head with its stick, two buttons, a small menu button and three green lights, over
    /// a grey handle (Resources/AntGarden/VR/VRController.shader). Made here of rounded parts, at real size in the VR
    /// origin, so they stay the size of the player's hands whatever the world's scale; each is hidden while the headset
    /// cannot see it. No hands: hands made up round the controllers were a guess (the user, 11-01: "不要自己去猜手的位置");
    /// on a Quest the headset's own hands and controllers are drawn instead (MetaDevices).
    /// </summary>
    public class VRHands : MonoBehaviour
    {
        Transform left, right;

        /// <summary>Makes both controllers in the origin; light: their brightness (1 as made).</summary>
        public void Init(Transform origin, float light)
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
