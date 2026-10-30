using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// Playing in a standalone headset: a Meta Quest running the game itself, as an Android app (Editor/VR/VRSetup.cs,
    /// BuildQuest), no computer. Its chip draws about a tenth of what a desktop's card does, and must draw every frame
    /// twice (an eye each) 72 times a second, so the particle nest is made with far fewer splats: each set at its own
    /// share of the desktop's (Settings), all of them made once as the nest loads but for two small shells round the ant;
    /// no bloom, shadows or anti-aliasing passes. Only on Android, or on a desktop with -quest 1 (or -quest a/quest.json)
    /// to see it there; the shared defaults are never changed. On the headset its values can be changed without building
    /// again: a quest.json in the app's files folder is read over them as it starts
    /// (adb push quest.json /sdcard/Android/data/com.ucldfpi.antgarden/files/quest.json).
    /// </summary>
    public static class QuestLook
    {
        [Serializable]
        public class Settings
        {
            public string world = "";           // the nest to start in (empty: game_config.json's defaultWorld)
            public float all = 0.6f;            // every share below, times this (the first thing to turn on the headset; 1: 20-28 fps on a Quest 3)
            public float plants = 0.2f;         // the plants' coarse splats, of the desktop's
            public float far = 0.05f;           // the giants beyond the walls: their fine splats
            public int giants = 8;              // and at most this many of them (cfg.scatter.surroundingsCount; their dark bodies are as many as their size wants)
            public float things = 0.35f;        // the floor's things (splats per unit² of their surface)
            public int fine = 2;                // each thing's splat drawn as this many up close (SolidThings.Fine; 4 on a desktop)
            public float ground = 0.15f;        // the ground's glowing points
            public float dust = 0.3f;           // the motes in the air
            public float carpet = 3f;           // the carpet's spacing, times the desktop's (a third as many each way)
            public float shell = 0.12f;         // the plants' fine splats round the ant (the budget moved from far to near: no coarse discs up close)
            public float walls = 0.08f;         // the walls' splats round the ant
            public float groundShell = 0.2f;    // the ground's points round the ant
            public float vines = 0.5f;
            public int shellRoom = 1 << 20;     // room made for the fine shell up front, splats (8 million on a desktop)
            public string post = "grade";       // "none", "grade" (the colour grading alone), "bloom" (and the bloom, fast) or "full" (as on a desktop)
            public bool shadows = false;
            public float resolution = 0.9f;     // the eyes' textures, of the size the headset asks for (the particles fill the frame: pixels cost most)
            public float fpsLog = 5f;           // seconds between frame-rate lines in the log (0: none)
            public float wave = 1.6f;           // the plants' sway in the wind, times the desktop's (larger in a headset)
            public float gusts = 1.25f;         // how fast the gusts run, times the desktop's
            public float ripples = 1.6f;        // the rings that spread over the floor, times their strength
            public float volume = 1.6f;         // every sound, times the desktop's
            public string view = "eye";         // where the headset starts: "eye" (the ant's own eyes) or "behind" (Y switches)
            public float behindScale = 0.5f;    // behind the ant: a real metre is this many units (the ant's eyes: 0.25)
            public float behindBack = 2f;       // the head this far behind the ant (units)
            public float behindUp = 1.1f;       // and this far above its feet
        }

        /// <summary>The game runs in a standalone headset, or a desktop shows how it would there.</summary>
        public static bool On => Application.platform == RuntimePlatform.Android || Game.Arg("-quest") != null;

        static Settings current;

        /// <summary>The values in use: the defaults, then quest.json over them (the app's files folder; on a desktop the
        /// file -quest names).</summary>
        public static Settings Current
        {
            get
            {
                if (current != null) return current;
                current = new Settings();
                string file = Path.Combine(Application.persistentDataPath, "quest.json");
                string arg = Game.Arg("-quest");
                if (!string.IsNullOrEmpty(arg) && File.Exists(arg)) file = arg;
                try
                {
                    if (File.Exists(file)) JsonUtility.FromJsonOverwrite(File.ReadAllText(file), current);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AntGarden] quest.json ignored: {e.Message}");
                }
                Debug.Log($"[AntGarden] quest: {JsonUtility.ToJson(current)}");
                return current;
            }
        }

        static float Share(float s) => Mathf.Max(0f, s * Current.all);
        static int Scaled(int n, float s) => Mathf.Max(1, Mathf.RoundToInt(n * Share(s)));

        /// <summary>The game's settings as it starts: the nest to start in, fewer giants beyond the walls.</summary>
        public static void Tune(GameConfig cfg)
        {
            Settings q = Current;
            if (!string.IsNullOrEmpty(q.world)) cfg.defaultWorld = q.world;
            cfg.scatter.surroundingsCount = Mathf.Min(cfg.scatter.surroundingsCount, q.giants);
            cfg.audio.master *= Mathf.Max(0f, q.volume);
        }

        /// <summary>The headset's view: where it starts (the ant's eyes, or behind it) and how it stands behind the ant.</summary>
        public static void Tune(VRPlayer vr)
        {
            Settings q = Current;
            vr.view = q.view == "behind" ? VRPlayer.View.Behind : VRPlayer.View.Eye;
            vr.behindScale = Mathf.Max(0.05f, q.behindScale);
            vr.behindBack = q.behindBack;
            vr.behindUp = q.behindUp;
        }

        /// <summary>The particle nest, before it is made (and before SolidThings and the vines are: their shares are
        /// set here too).</summary>
        public static void Tune(ParticleLook look)
        {
            Settings q = Current;
            look.solidCoarse *= Share(q.plants);
            look.solidCoarseBudget = Scaled(look.solidCoarseBudget, q.plants);
            look.farBudget = Scaled(look.farBudget, q.far);
            look.groundNear *= Share(q.ground);
            look.groundFar *= Share(q.ground);
            look.groundBudget = Scaled(look.groundBudget, q.ground);
            look.dust *= Share(q.dust);
            look.carpetStep *= q.carpet;
            look.carpetSize *= q.carpet;
            look.solidFine *= Share(q.shell);
            look.solidNear *= Share(q.shell);
            look.solidFineCap = Scaled(look.solidFineCap, q.shell);
            look.solidNearCap = Scaled(look.solidNearCap, q.shell);
            look.wallDensity *= Share(q.walls);
            look.shellDensity *= Share(q.groundShell);
            SolidThings.Fine = Mathf.Max(1, q.fine);
            SolidThings.ShellRoom = Mathf.Max(1 << 16, q.shellRoom);
        }

        /// <summary>The floor's things, before they are made.</summary>
        public static void Tune(SolidThings things) => things.perArea *= Share(Current.things);

        /// <summary>The vines round the guiding beams, before they are made.</summary>
        public static void Tune(GuideThreads guide) => guide.vineDensity *= Share(Current.vines);

        /// <summary>The camera and its passes, once the night is set up: the colour grading alone (or nothing, or all of
        /// it), no shadows; the eyes' size; the frame rate in the log.</summary>
        public static void TuneView(Camera cam)
        {
            Settings q = Current;
            if (!q.shadows) QualitySettings.shadows = ShadowQuality.Disable;
            var layer = cam.GetComponent<PostProcessLayer>();
            var volume = UnityEngine.Object.FindObjectOfType<PostProcessVolume>();
            if (q.post == "none")
            {
                if (layer != null) layer.enabled = false;
                cam.allowHDR = false;
            }
            else if (q.post != "full")
            {
                if (layer != null) layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                if (volume != null && volume.profile != null)
                    foreach (PostProcessEffectSettings fx in volume.profile.settings)
                    {
                        if (fx is ColorGrading) continue;
                        if (fx is Bloom bloom && q.post == "bloom") { bloom.fastMode.Override(true); continue; }
                        fx.enabled.Override(false);
                    }
            }
            if (XRSettings.enabled && q.resolution > 0f) XRSettings.eyeTextureResolutionScale = q.resolution;
            if (q.fpsLog > 0f) cam.gameObject.AddComponent<QuestFps>().every = q.fpsLog;
            var field = cam.gameObject.AddComponent<QuestField>();
            field.wave = q.wave;
            field.gusts = q.gusts;
            field.ripples = q.ripples;
        }
    }

    /// <summary>The nest's living field a little stronger in a headset, for a stronger sense of being there: the plants
    /// sway further in the wind, the gusts run faster, the rings on the floor rise higher. The nest's own values
    /// (NestField.cginc's _NestWind and _Ripples, as LabNest sets them) are scaled as they come, each once.</summary>
    public class QuestField : MonoBehaviour
    {
        public float wave = 1f, gusts = 1f, ripples = 1f;
        Vector4 windSet;
        Vector4[] ringsSet;
        readonly System.Collections.Generic.List<Vector4> rings = new System.Collections.Generic.List<Vector4>(8);

        void LateUpdate()
        {
            Vector4 w = Shader.GetGlobalVector("_NestWind");
            if (w != Vector4.zero && w != windSet)
            {
                windSet = new Vector4(w.x * wave, w.y * gusts, w.z, w.w);
                Shader.SetGlobalVector("_NestWind", windSet);
            }
            if (ripples == 1f) return;
            Shader.GetGlobalVectorArray("_Ripples", rings);
            if (rings.Count == 0) return;
            if (ringsSet == null || ringsSet.Length != rings.Count) ringsSet = new Vector4[rings.Count];
            bool changed = false;
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] == ringsSet[i]) continue;   // still as scaled here
                Vector4 r = rings[i];
                r.w *= ripples;
                ringsSet[i] = r;
                changed = true;
            }
            if (changed) Shader.SetGlobalVectorArray("_Ripples", ringsSet);
        }
    }

    /// <summary>The frame rate in the log every few seconds (QuestLook.Settings.fpsLog), to read with adb logcat: frames
    /// a second, how many missed the headset's refresh, the slowest, the memory in use.</summary>
    public class QuestFps : MonoBehaviour
    {
        public float every = 5f;
        float t, worst;
        int frames, missed;

        void Update()
        {
            float dt = Time.unscaledDeltaTime, hz = XRSettings.enabled && XRDevice.refreshRate > 1f ? XRDevice.refreshRate : 72f;
            t += dt;
            frames++;
            if (dt > 1.5f / hz) missed++;
            worst = Mathf.Max(worst, dt);
            if (t < every) return;
            Debug.Log($"[AntGarden] quest: {frames / t:F1} fps at {hz:F0} Hz, {missed} of {frames} frames missed, slowest {worst * 1000f:F0} ms, "
                + $"{UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB in use");
            t = worst = 0f;
            frames = missed = 0;
        }
    }
}
