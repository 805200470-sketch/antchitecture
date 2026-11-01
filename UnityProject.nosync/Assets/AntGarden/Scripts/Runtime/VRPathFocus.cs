using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// In a headset, at the tutorial's last step (the way on): the way's light picked out of the nest for a few seconds
    /// (the user, 11-01: "在光路引导那一步要将光路差异化突出，可以暂时压暗环境，突出光路"). A dark veil round the eyes (the
    /// comfort vignette's sphere and shader, even all over), drawn after the nest's ground, plants and ants and before
    /// what glows (the floor's specks, GuideTrail; the motes; the threads over the way, GuideThreads) and the screen's
    /// words; the way's light brighter and reaching further ahead meanwhile; the glass lamps go under the veil
    /// (GlassLamps, by reflection). Then all as it was. Everything here is shared with the desktop and is only changed
    /// while it runs, never in its files.
    /// </summary>
    public class VRPathFocus : MonoBehaviour
    {
        const int VeilQueue = 2999;         // after the solid nest (up to 2040), before what glows and the screen (3000 on)

        public float darkness = 0.65f;      // how dark the nest grows (0 to 1)
        public float seconds = 10f;         // for this long: darkening over the first second, clearing over the last 1.5
        public float boost = 2.5f;          // meanwhile the way's light this much brighter, and reaching this much further

        class Speck { public Renderer r; public float gain, mote, ahead, pulse; }

        Material veil;
        Renderer sphere;
        float t = -1f;                      // seconds into it (below 0: not running)
        readonly List<Speck> specks = new List<Speck>();
        GuideThreads threads;
        Material threadMat, lampMat;
        float reachWas, threadGainWas;
        int lampQueueWas;

        MaterialPropertyBlock block;

        /// <summary>The veil round the eyes (hidden until Show).</summary>
        public void Init(Camera cam, float dark, float time)
        {
            block = new MaterialPropertyBlock();
            darkness = Mathf.Clamp01(dark);
            seconds = Mathf.Max(3f, time);
            var shader = Resources.Load<Shader>("AntGarden/VR/VRVignette");
            if (shader == null)
            {
                Debug.LogWarning("[AntGarden] VR: the vignette shader is missing; the way's light is not picked out");
                return;
            }
            veil = new Material(shader) { renderQueue = VeilQueue };
            veil.SetFloat("_Dark", 2f);    // (dark beyond an angle no ray has: even all over)
            veil.SetFloat("_Clear", 3f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VR Path Focus";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(cam.transform, false);
            go.transform.localScale = Vector3.one * 3f;
            sphere = go.GetComponent<Renderer>();
            sphere.sharedMaterial = veil;
            sphere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sphere.receiveShadows = false;
            sphere.enabled = false;
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
                    specks.Add(new Speck { r = r, gain = Get(r, "_Gain"), mote = Get(r, "_MoteGain"), ahead = Get(r, "_AheadReach"), pulse = Get(r, "_PulseReach") });
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
                lampMat.renderQueue = VeilQueue - 1;   // under the veil
            }
            sphere.enabled = true;
            t = 0f;
            Debug.Log($"[AntGarden] VR path focus: {specks.Count} trails, threads {(threads != null ? "yes" : "no")}, lamps {(lampMat != null ? "yes" : "no")}, {seconds:F0} s");
        }

        void LateUpdate()
        {
            if (t < 0f) return;
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1f) * Mathf.Clamp01((seconds - t) / 1.5f));
            veil.SetFloat("_Strength", darkness * k);
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
                s.r.GetPropertyBlock(block);
                block.SetFloat("_Gain", s.gain);
                block.SetFloat("_MoteGain", s.mote);
                block.SetFloat("_AheadReach", s.ahead);
                block.SetFloat("_PulseReach", s.pulse);
                s.r.SetPropertyBlock(block);
            }
            specks.Clear();
            if (threads != null) threads.reach = reachWas;
            if (threadMat != null) threadMat.SetFloat("_Gain", threadGainWas);
            if (lampMat != null) lampMat.renderQueue = lampQueueWas;
            threads = null;
            threadMat = lampMat = null;
            if (sphere != null) sphere.enabled = false;
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
