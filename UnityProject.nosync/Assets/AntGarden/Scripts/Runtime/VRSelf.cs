using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// In a headset: the player's own ant seen through its eyes, drawn here instead of by ParticleAnts. The user, 11-01,
    /// after a try: "要变得更透明，可以直接调整透明度，能看出来蚂蚁造型的边界即可", and as it crawled it still "会遮挡，而且会
    /// 有一些位移，感觉和摄像头的移动不同步". So it is see-through (its outline the most of it, nothing of it close in front of
    /// the eyes: Resources/AntGarden/VR/VRGhostAnt.shader), and it is carried with the view: its skeleton as it moves (legs
    /// stepping, antennae swinging), but level, at the view's own eased height, without the slope's tilt or the dip and
    /// bank the ant leans into as it sets off and turns (a desktop camera's nicety; through its eyes it swung the body
    /// across a view that stays level). Behind the ant ParticleAnts draws it again, as every other ant.
    /// ParticleAnts is shared with the desktop: its lists are read here by reflection and the file is left as it is.
    /// </summary>
    [DefaultExecutionOrder(1001)]   // after ParticleAnts (1000): the ants moved and posed
    public class VRSelf : MonoBehaviour
    {
        ParticleAnts ants;
        AntController ant;
        Transform lean;
        Transform[] bones;
        Matrix4x4[] poses, skinData;
        ComputeBuffer points, skin;
        float area, alpha, size, fade;
        int count;
        Material mat;
        MaterialPropertyBlock block;
        Vector3 at;
        float heading;

        /// <summary>Through the ant's eyes (VRPlayer, every frame): its body drawn here, carried at feet (the ant's
        /// place, the view's eased floor) facing heading (radians).</summary>
        public bool Shown { get; set; }

        public void Carry(Vector3 feet, float facing)
        {
            at = feet;
            heading = facing;
        }

        /// <summary>The player's ant, with share of all its points (each smaller the more there are), each size times as
        /// large as would just cover it (smaller: a finer line along its outline), at opacity alpha along its outline.
        /// Null where the ant is not drawn as points (a ruin) or ParticleAnts has changed.</summary>
        public static VRSelf Make(ParticleAnts ants, AntController player, float share, float size, float alpha)
        {
            var shader = Resources.Load<Shader>("AntGarden/VR/VRGhostAnt");
            if (ants == null || player == null || shader == null) return null;
            const BindingFlags inside = BindingFlags.NonPublic | BindingFlags.Instance;
            var kindType = typeof(ParticleAnts).GetNestedType("Kind", BindingFlags.NonPublic);
            var shownType = typeof(ParticleAnts).GetNestedType("Shown", BindingFlags.NonPublic);
            FieldInfo pointsOf = kindType?.GetField("points"), posesOf = kindType?.GetField("poses"), areaOf = kindType?.GetField("area");
            FieldInfo kindOf = shownType?.GetField("kind"), antOf = shownType?.GetField("ant"), bonesOf = shownType?.GetField("bones");
            if (pointsOf == null || posesOf == null || areaOf == null || kindOf == null || antOf == null || bonesOf == null
                || !(typeof(ParticleAnts).GetField("ants", inside)?.GetValue(ants) is IList shown))
            {
                Debug.LogWarning("[AntGarden] VR: the ant's own body is drawn as the others (ParticleAnts has changed)");
                return null;
            }
            foreach (object s in shown)
            {
                if (!ReferenceEquals(antOf.GetValue(s), player)) continue;
                object kind = kindOf.GetValue(s);
                if (!(pointsOf.GetValue(kind) is ComputeBuffer buffer) || !(bonesOf.GetValue(s) is Transform[] b)
                    || !(posesOf.GetValue(kind) is Matrix4x4[] p) || b.Length == 0) return null;
                var self = ants.gameObject.AddComponent<VRSelf>();
                self.ants = ants;
                self.ant = player;
                self.lean = player.Model != null && player.Model.parent != null ? player.Model.parent : player.transform;
                self.bones = b;
                self.poses = p;
                self.points = buffer;
                self.area = (float)areaOf.GetValue(kind);
                self.count = Mathf.Clamp(Mathf.RoundToInt(buffer.count * Mathf.Clamp01(share)), 64, buffer.count);
                self.alpha = Mathf.Clamp01(alpha);
                self.size = Mathf.Max(0.05f, size);
                self.skinData = new Matrix4x4[b.Length];
                self.skin = new ComputeBuffer(b.Length, 64);
                self.mat = new Material(shader);
                self.block = new MaterialPropertyBlock();
                Debug.Log($"[AntGarden] VR: the ant's own body through its eyes, {self.count} points ×{self.size:F2}, see-through ({self.alpha:F2})");
                return self;
            }
            return null;
        }

        void LateUpdate()
        {
            fade = Mathf.MoveTowards(fade, Shown ? 1f : 0f, Time.deltaTime / 0.3f);
            if (fade <= 0f || ant == null || !ant.Visible) return;
            // its bones as they stand over the lean (the ant's own frame, its slope and its lean taken out), carried to
            // the view's frame: level, at the view's floor, the ant's way
            Matrix4x4 carry = Matrix4x4.TRS(at, Quaternion.Euler(0f, heading * Mathf.Rad2Deg, 0f), ant.transform.lossyScale) * lean.worldToLocalMatrix;
            for (int i = 0; i < bones.Length; i++) skinData[i] = carry * bones[i].localToWorldMatrix * poses[i];
            skin.SetData(skinData);
            float len = ant.cfg.bodyLength;
            float bindToWorld = ((Vector3)skinData[0].GetColumn(0)).magnitude;
            float r = (area > 0f ? ants.cover * Mathf.Sqrt(area / count) * bindToWorld : ants.size) * size;
            block.SetBuffer("_AntPoints", points);
            block.SetBuffer("_Skin", skin);
            block.SetFloat("_Size", r);
            block.SetFloat("_Gain", ants.gain * NestTheme.LightComp(ants.exposureComp));
            block.SetFloat("_Pale", ants.pale);
            block.SetFloat("_Contrast", ants.contrast);
            block.SetFloat("_Hue", 0f);
            block.SetFloat("_Sat", ant.Saturation);
            block.SetFloat("_Alpha", alpha * fade);
            block.SetFloat("_Near", 0.05f * len);
            block.SetFloat("_Whole", 0.15f * len);
            Graphics.DrawProcedural(mat, new Bounds(at, Vector3.one * 5f * len), MeshTopology.Triangles, count * 3, 1, null, block, ShadowCastingMode.Off, false, 0);
        }

        void OnDestroy()
        {
            skin?.Release();   // (the points are ParticleAnts' own: it frees them)
            if (mat != null) Destroy(mat);
        }
    }
}
