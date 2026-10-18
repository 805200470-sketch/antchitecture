using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's NPC ants as points (Shaders/ParticleAnt.shader). Each kind's points
    /// (AssetBuilder.SaveAntPoints: spread over its surface, each with its normal and the two bones that move it
    /// most) sit in one buffer; every frame each shown ant's bone × bindpose matrices go into another, and the GPU skins the points
    /// with them. The ants' meshes are not drawn, but their animation and procedural legs run as before, so the
    /// points walk, step and touch antennae with them. The points are lit opaque beads of different sizes and
    /// colours, over the ant's own mesh drawn dark (nothing behind shows through), so the body's detail reads,
    /// not just its outline. Far ants get fewer points.
    /// </summary>
    [DefaultExecutionOrder(1000)]   // after the ants have moved and been posed
    public class ParticleAnts : MonoBehaviour
    {
        public float size = 0.0052f;    // a point's radius (units)
        public float gain = 1.15f;
        public float pale = 0.12f;      // how far colours go from the ant's own toward a pale glow
        public float contrast = 1.5f;   // how much the texture's light and dark are exaggerated
        public Color bodyColour = new Color(0.02f, 0.05f, 0.06f);   // the ant's own mesh, dark, under its points
        public float fullUntil = 6f;    // all its points this close to the camera
        public float fewBeyond = 18f;   // a third of them from here on

        struct AntPoint { public Vector3 pos; public Vector3 nrm; public uint col; public uint slots; public float w; }

        class Kind
        {
            public ComputeBuffer points;
            public int count;
            public string[] paths;
            public Matrix4x4[] poses;
        }

        class Shown
        {
            public AntController ant;
            public Kind kind;
            public Transform[] bones;
            public int skinBase;
            public MaterialPropertyBlock block;
        }

        readonly Dictionary<string, Kind> kinds = new Dictionary<string, Kind>();
        readonly List<Shown> ants = new List<Shown>();
        Material mat;
        ComputeBuffer skin;
        Matrix4x4[] skinData;

        public int Count => ants.Count;

        /// <summary>Takes over drawing these ants, the player's own among them (their meshes go dark under the points).</summary>
        public void Init(List<NpcAnt> npcs, AntController player, RuntimeRefs refs)
        {
            mat = refs.particleAnt;
            if (mat == null) return;
            if (refs.antOccluder != null) refs.antOccluder.SetColor("_Color", bodyColour);
            int slots = 0;
            var all = new List<AntController>();
            if (player != null) all.Add(player);
            foreach (NpcAnt n in npcs) all.Add(n.ant);
            foreach (AntController a in all)
            {
                if (a == null || a.Ref == null || a.Model == null) continue;
                Kind k = Load(a.Ref.id);
                if (k == null) continue;
                var bones = new Transform[k.paths.Length];
                for (int i = 0; i < bones.Length; i++)
                    bones[i] = string.IsNullOrEmpty(k.paths[i]) ? a.Model : a.Model.Find(k.paths[i]) ?? a.Model;
                a.DrawAsPoints(refs.antOccluder);
                ants.Add(new Shown { ant = a, kind = k, bones = bones, skinBase = slots, block = new MaterialPropertyBlock() });
                slots += bones.Length;
            }
            if (slots == 0) return;
            skin = new ComputeBuffer(slots, 64);
            skinData = new Matrix4x4[slots];
            Debug.Log($"[AntGarden] particle ants: {ants.Count} ants of {kinds.Count} kinds, {slots} bones");
        }

        Kind Load(string id)
        {
            if (kinds.TryGetValue(id, out Kind k)) return k;
            kinds[id] = null;
            var asset = Resources.Load<TextAsset>("AntGarden/Points/ant_" + id);
            if (asset == null) return null;
            using (var r = new BinaryReader(new MemoryStream(asset.bytes)))
            {
                if (r.ReadInt32() != 0x32414741) return null;   // "AGA2"
                int ns = r.ReadInt32();
                var paths = new string[ns];
                var poses = new Matrix4x4[ns];
                for (int i = 0; i < ns; i++)
                {
                    paths[i] = r.ReadString();
                    var m = new Matrix4x4();
                    for (int j = 0; j < 16; j++) m[j] = r.ReadSingle();
                    poses[i] = m;
                }
                int n = r.ReadInt32();
                var pts = new AntPoint[n];
                for (int i = 0; i < n; i++)
                {
                    var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    var nrm = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    uint cr = r.ReadByte(), cg = r.ReadByte(), cb = r.ReadByte(), ck = r.ReadByte();
                    uint s0 = r.ReadUInt16(), s1 = r.ReadUInt16();
                    pts[i] = new AntPoint { pos = pos, nrm = nrm, col = cr | (cg << 8) | (cb << 16) | (ck << 24), slots = s0 | (s1 << 16), w = r.ReadSingle() };
                }
                if (n == 0) return null;
                k = new Kind { count = n, paths = paths, poses = poses, points = new ComputeBuffer(n, 36) };
                k.points.SetData(pts);
            }
            kinds[id] = k;
            return k;
        }

        void LateUpdate()
        {
            if (skin == null) return;
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            foreach (Shown s in ants)
            {
                if (!s.ant.Visible || s.ant.PointFade <= 0.01f) continue;
                for (int i = 0; i < s.bones.Length; i++) skinData[s.skinBase + i] = s.bones[i].localToWorldMatrix * s.kind.poses[i];
            }
            skin.SetData(skinData);
            foreach (Shown s in ants)
            {
                if (!s.ant.Visible || s.ant.PointFade <= 0.01f) continue;
                Vector3 p = s.ant.Position;
                float far = Mathf.InverseLerp(fullUntil, fewBeyond, Vector3.Distance(p, eye));
                // far ants: fewer points; the last ones shrink away rather than vanish (see the shader's _Count)
                float shown = s.kind.count * Mathf.Lerp(1f, 0.33f, far);
                int n = Mathf.Min(s.kind.count, Mathf.CeilToInt(shown * 1.15f));
                s.block.SetFloat("_Count", shown);
                s.block.SetFloat("_Fade", s.ant.PointFade);
                s.block.SetBuffer("_AntPoints", s.kind.points);
                s.block.SetBuffer("_Skin", skin);
                s.block.SetFloat("_SkinBase", s.skinBase);
                s.block.SetFloat("_Hue", s.ant.Hue);
                s.block.SetFloat("_Sat", s.ant.Saturation);
                s.block.SetFloat("_Size", size);
                s.block.SetFloat("_Gain", gain);
                s.block.SetFloat("_Pale", pale);
                s.block.SetFloat("_Contrast", contrast);
                Graphics.DrawProcedural(mat, new Bounds(p, Vector3.one * 5f), MeshTopology.Triangles, n * 3, 1, null, s.block, ShadowCastingMode.Off, false, 0);
            }
        }

        void OnDestroy()
        {
            skin?.Release();
            foreach (Kind k in kinds.Values) k?.points?.Release();
        }
    }
}
