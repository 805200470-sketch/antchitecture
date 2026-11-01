using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// In a headset, at the tutorial's last step (the way on): the way's light picked out of the nest for a few seconds
    /// (the user, 11-01: "在光路引导那一步要将光路差异化突出，可以暂时压暗环境，突出光路"). A dark veil over the whole view, the
    /// first thing on the screen's canvas, so it is drawn over the nest and under the screen's words; the way's light is
    /// drawn after it, and brighter and further ahead meanwhile: the floor's specks (GuideTrail, their own copy of their
    /// material just after the canvas), the threads over the way (GuideThreads, after it already); the glass lamps go
    /// under it (GlassLamps, by reflection). Then all as it was. Everything here is shared with the desktop and is only
    /// changed while it runs, never in its files.
    /// </summary>
    public class VRPathFocus : MonoBehaviour
    {
        public float darkness = 0.65f;      // how dark the nest grows (0 to 1)
        public float seconds = 10f;         // for this long: darkening over the first second, clearing over the last 1.5
        public float boost = 1.6f;          // meanwhile the way's light this much brighter, and reaching this much further

        class Speck { public Renderer r; public Material was; public float gain, mote, ahead, pulse; }

        Image veil;
        float t = -1f;                      // seconds into it (below 0: not running)
        readonly List<Speck> specks = new List<Speck>();
        GuideThreads threads;
        Material threadMat, lampMat;
        float reachWas, threadGainWas;
        int lampQueueWas;

        MaterialPropertyBlock block;

        /// <summary>The veil on the screen's canvas (hidden until Show).</summary>
        public void Init(Canvas canvas, float dark, float time)
        {
            block = new MaterialPropertyBlock();
            darkness = Mathf.Clamp01(dark);
            seconds = Mathf.Max(3f, time);
            var rt = UiKit.Rect(canvas.transform, "VR Path Focus", 0.5f, 0.5f, Vector2.zero, new Vector2(1920, 1080));
            rt.SetAsFirstSibling();
            rt.localScale = Vector3.one * 8f;   // the whole view, not a window in it
            veil = rt.gameObject.AddComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0f);
            veil.raycastTarget = false;
            rt.gameObject.SetActive(false);
        }

        /// <summary>The way's light picked out (again from the start if it is already).</summary>
        public void Show()
        {
            if (veil == null) return;
            if (t >= 0f) { t = Mathf.Min(t, 1f); return; }
            Game game = Game.Instance;
            GameObject world = GameObject.Find("World");
            if (world != null)
                foreach (Transform c in world.transform)
                {
                    var r = c.name.StartsWith("GuideTrail") ? c.GetComponent<Renderer>() : null;
                    if (r == null || r.sharedMaterial == null) continue;
                    r.GetPropertyBlock(block);
                    var s = new Speck { r = r, was = r.sharedMaterial };
                    s.gain = Get(r, "_Gain");
                    s.mote = Get(r, "_MoteGain");
                    s.ahead = Get(r, "_AheadReach");
                    s.pulse = Get(r, "_PulseReach");
                    r.sharedMaterial = new Material(r.sharedMaterial) { renderQueue = 3001 };
                    specks.Add(s);
                }
            threads = game != null ? game.GetComponent<GuideThreads>() : null;
            if (threads != null) reachWas = threads.reach;
            threadMat = game != null && game.refs != null ? game.refs.lightThread : null;
            if (threadMat != null && threadMat.HasProperty("_Gain")) threadGainWas = threadMat.GetFloat("_Gain");
            else threadMat = null;
            var lamps = game != null ? game.GetComponent<GlassLamps>() : null;
            lampMat = lamps != null ? typeof(GlassLamps).GetField("mat", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(lamps) as Material : null;
            if (lampMat != null)
            {
                lampQueueWas = lampMat.renderQueue;
                lampMat.renderQueue = 2999;   // under the veil
            }
            veil.gameObject.SetActive(true);
            t = 0f;
            Debug.Log($"[AntGarden] VR path focus: {specks.Count} trails, threads {(threads != null ? "yes" : "no")}, lamps {(lampMat != null ? "yes" : "no")}, {seconds:F0} s");
        }

        void LateUpdate()
        {
            if (t < 0f) return;
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1f) * Mathf.Clamp01((seconds - t) / 1.5f));
            veil.color = new Color(0f, 0f, 0f, darkness * k);
            float up = Mathf.Lerp(1f, boost, k);
            foreach (Speck s in specks)
            {
                if (s.r == null) continue;
                s.r.GetPropertyBlock(block);   // (LabNest sets where the ant is on it each frame, in the same block)
                block.SetFloat("_Gain", s.gain * up);
                block.SetFloat("_MoteGain", s.mote * up);
                block.SetFloat("_AheadReach", s.ahead * up);
                block.SetFloat("_PulseReach", s.pulse * up);
                s.r.SetPropertyBlock(block);
            }
            if (threads != null) threads.reach = reachWas * up;
            if (threadMat != null) threadMat.SetFloat("_Gain", threadGainWas * up);
            if (t >= seconds) End();
        }

        /// <summary>All as it was.</summary>
        void End()
        {
            foreach (Speck s in specks)
            {
                if (s.r == null) continue;
                Material copy = s.r.sharedMaterial;
                s.r.sharedMaterial = s.was;
                s.r.GetPropertyBlock(block);
                block.SetFloat("_Gain", s.gain);
                block.SetFloat("_MoteGain", s.mote);
                block.SetFloat("_AheadReach", s.ahead);
                block.SetFloat("_PulseReach", s.pulse);
                s.r.SetPropertyBlock(block);
                if (copy != s.was) Destroy(copy);
            }
            specks.Clear();
            if (threads != null) threads.reach = reachWas;
            if (threadMat != null) threadMat.SetFloat("_Gain", threadGainWas);
            if (lampMat != null) lampMat.renderQueue = lampQueueWas;
            threads = null;
            threadMat = lampMat = null;
            if (veil != null) veil.gameObject.SetActive(false);
            t = -1f;
        }

        void OnDestroy()
        {
            if (t >= 0f) End();
        }

        /// <summary>A trail's value: its block's (as LabNest sets them), else its material's.</summary>
        float Get(Renderer r, string name)
        {
            if (block.HasFloat(name)) return block.GetFloat(name);
            return r.sharedMaterial.HasProperty(name) ? r.sharedMaterial.GetFloat(name) : 1f;
        }
    }
}
