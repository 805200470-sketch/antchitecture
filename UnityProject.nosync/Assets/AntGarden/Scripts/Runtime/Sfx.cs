#if !SFX_SYNTH_ONLY
using UnityEngine;
#endif
using System;
using System.Collections.Generic;
using System.IO;

namespace AntGarden
{
#if !SFX_SYNTH_ONLY
    /// <summary>
    /// The small sounds of the garden, all made at start-up (no audio files), in one of two directions
    /// (SoundSettings.direction, or -sound classic|natural):
    /// natural (the user's pick, 11-04; Tools/sound/directions.py, Natural): no pitch at all. The player's feet are soft
    /// pats with a tiny tick of chitin, drier in the warm nests; antennae stroke one another; a plant lights up with a
    /// slow, soft breath, and plants lit together swell one breath, never a burst (the user found the old light-up
    /// jarring); the map is a dry leaf; a gate is a gust through a tunnel; a beam of the guiding light breathes like a
    /// hollow reed when the ant reaches it; an answer (Voices.Chime) is one drop in a cave. Under it all the nest's air
    /// and a little music (Music).
    /// classic: the sounds before 11-04 (rustles, a murmur, a shimmer, a breath and paper; wind and crickets).
    /// Every natural clip is made equally loud (Dsp.Ref), so the levels in SoundSettings are plain gains. Only
    /// AudioClip.Create and AudioSource: the Quest build plays the same.
    /// </summary>
    public static class Sfx
    {
        static SoundSettings cfg;
        static bool natural;

        // natural: made once on a worker thread, kept across scene loads
        static System.Threading.Thread maker;
        static volatile bool madeReady;
        static float[][] made;
        static long madeMs;
        static AudioClip[] stepSoft, stepDry, touches, flares, replies;
        static AudioClip mapOpen, mapClose, gate, veil, guide;
        static AudioSource[] pool, glow;
        static float[] poolEnds, glowStart, glowLevel, glowTarget, glowCap;
        static AudioSource bedA, bedB;
        static bool tunnel;
        static Dsp.Rand rng;
        static int lastStep = -1, lastFlare = -1;
        static bool[] beamArmed;
        static float[] beamLast;
        static int beamCount = -1;
        static int nSteps, nTouches, nFlares, nSwells, nGuides, nReplies, nGates, nMaps;   // logged when the game quits

        public static void Init(SoundSettings settings, Camera cam)
        {
            cfg = settings;
            natural = (Game.Arg("-sound") ?? cfg.direction) != "classic";
            if (natural) InitNatural(cam);
            else Classic.Init(cfg, cam);
            Music.Init(cfg);
        }

        static void InitNatural(Camera cam)
        {
            if (maker == null)
            {
                // made off the main thread (about 0.6 s on the Mac, more on the headset), so loading takes no longer;
                // each sound waits until they are there
                rng = new Dsp.Rand(11);
                maker = new System.Threading.Thread(() =>
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var list = new List<float[]>();
                    for (int i = 0; i < 8; i++) list.Add(Dsp.ToRef(SfxSynth.Step(false, i)));
                    for (int i = 0; i < 8; i++) list.Add(Dsp.ToRef(SfxSynth.Step(true, i)));
                    for (int i = 0; i < 3; i++) list.Add(Dsp.ToRef(SfxSynth.Touch(i)));
                    for (int i = 0; i < 8; i++) list.Add(Dsp.ToRef(SfxSynth.Flare(i)));
                    for (int i = 0; i < 3; i++) list.Add(Dsp.ToRef(SfxSynth.Reply(i)));
                    list.Add(Dsp.ToRef(SfxSynth.MapOpen()));
                    list.Add(Dsp.ToRef(SfxSynth.MapClose()));
                    list.Add(Dsp.ToRef(SfxSynth.Gate(false)));
                    list.Add(Dsp.ToRef(SfxSynth.Gate(true)));
                    list.Add(Dsp.ToRef(SfxSynth.Guide()));
                    made = list.ToArray();
                    madeMs = watch.ElapsedMilliseconds;
                    madeReady = true;
                }) { IsBackground = true, Name = "AntGarden sfx" };
                maker.Start();
                Application.quitting += () => Debug.Log($"[AntGarden] sfx: played {nSteps} steps, {nTouches} touches, {nFlares} light-ups " +
                    $"(and {nSwells} more lit plants swelled one), {nGuides} beams of the guiding light, {nReplies} answers, {nGates} gates, {nMaps} maps");
            }
            if (pool == null)
            {
                var host = new GameObject("SfxVoices");
                UnityEngine.Object.DontDestroyOnLoad(host);
                pool = new AudioSource[24];
                poolEnds = new float[pool.Length];
                for (int i = 0; i < pool.Length; i++) pool[i] = Voice(host.transform, "SfxVoice");
                glow = new AudioSource[5];
                glowStart = new float[glow.Length];
                glowLevel = new float[glow.Length];
                glowTarget = new float[glow.Length];
                glowCap = new float[glow.Length];
                for (int i = 0; i < glow.Length; i++) glow[i] = Voice(host.transform, "SfxGlow");
                // the bed of air: the nest's, and a tunnel's to cross-fade to (made with the music); kept across scene
                // loads, so the air goes on through a change of world instead of stopping with the old camera
                bedA = host.AddComponent<AudioSource>();
                bedB = host.AddComponent<AudioSource>();
                foreach (AudioSource b in new[] { bedA, bedB })
                {
                    b.loop = true;
                    b.spatialBlend = 0;
                    b.volume = 0;
                    b.playOnAwake = false;
                    b.priority = 40;
                }
            }
            tunnel = false;
            beamCount = -1;
        }

        /// <summary>The natural sounds as clips, once the worker has made them (the first call after that makes them).</summary>
        static bool Ready()
        {
            if (stepSoft != null) return true;
            if (!madeReady) return false;
            int k = 0;
            AudioClip Next(string name) => Clip(name, made[k++], 1, Dsp.SR);
            stepSoft = new AudioClip[8];
            stepDry = new AudioClip[8];
            for (int i = 0; i < 8; i++) stepSoft[i] = Next($"StepSoft{i}");
            for (int i = 0; i < 8; i++) stepDry[i] = Next($"StepDry{i}");
            touches = new AudioClip[3];
            for (int i = 0; i < touches.Length; i++) touches[i] = Next($"Touch{i}");
            flares = new AudioClip[8];
            for (int i = 0; i < flares.Length; i++) flares[i] = Next($"Flare{i}");
            replies = new AudioClip[3];
            for (int i = 0; i < replies.Length; i++) replies[i] = Next($"Reply{i}");
            mapOpen = Next("MapOpen");
            mapClose = Next("MapClose");
            gate = Next("Gate");
            veil = Next("Veil");
            guide = Next("Guide");
            made = null;
            Debug.Log($"[AntGarden] sfx: the natural sounds made in {madeMs} ms (worker thread)");
            return true;
        }

        public static AudioClip Clip(string name, float[] interleaved, int channels, int rate)
        {
            var c = AudioClip.Create(name, interleaved.Length / channels, channels, rate, false);
            c.SetData(interleaved, 0);
            return c;
        }

        static AudioSource Voice(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.dopplerLevel = 0;                      // a swinging camera must not bend the pitch
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2;
            s.maxDistance = 30;
            s.spatialBlend = 0.6f;
            return s;
        }

        /// <summary>Which bed of air to hear: the nest's or a tunnel's (classic: open night or tunnel, warmer wind).</summary>
        public static void SetPlace(bool inTunnel, bool warmBiome)
        {
            if (!natural) { Classic.SetPlace(inTunnel, warmBiome); return; }
            tunnel = inTunnel;
        }

        /// <summary>Every frame: the beds cross-fade, a swelling light-up eases to its level, the guiding light is
        /// listened for, and the music moves on.</summary>
        public static void Tick(float dt)
        {
            if (cfg == null) return;
            if (!natural)
            {
                Classic.Tick(dt);
                Music.Tick(dt);
                return;
            }
            if (bedA != null)
            {
                if (bedA.clip == null && Music.Bed(false) != null)
                {
                    bedA.clip = Music.Bed(false);
                    bedB.clip = Music.Bed(true);
                    bedA.Play();
                    bedB.Play();
                }
                float k = 1 - Mathf.Exp(-1.5f * dt), amb = cfg.ambience * cfg.master;
                bedA.volume += ((tunnel ? 0f : 1f) * amb - bedA.volume) * k;
                bedB.volume += ((tunnel ? 1f : 0f) * amb - bedB.volume) * k;
            }
            if (!Ready()) { Music.Tick(dt); return; }
            float e = 1 - Mathf.Exp(-dt / 0.25f);
            for (int i = 0; i < glow.Length; i++)
            {
                if (!glow[i].isPlaying) continue;
                glowLevel[i] += (glowTarget[i] - glowLevel[i]) * e;
                glow[i].volume = glowLevel[i];
            }
            Game g = Game.Instance;
            if (Game.Lab && g != null && g.player != null && guide != null) GuideTick(g.player.Position);
            Music.Tick(dt);
        }

        /// <summary>A few of the player's feet on the ground, never the same twice running, nor quite as loud.</summary>
        public static void Step(Vector3 at, bool dryGround, bool sprint)
        {
            if (cfg == null) return;
            if (!natural) { Classic.Step(at, dryGround, sprint); return; }
            if (!Ready()) return;
            bool dry = Game.Lab && NestTheme.Current != null ? cfg.IsDry(NestTheme.Current.id) : dryGround;
            AudioClip[] set = dry ? stepDry : stepSoft;
            int v = Pick(set.Length, ref lastStep);
            float loud = (0.55f + 0.45f * (float)rng.U()) * (sprint ? 1.2f : 1f);
            Play(set[v], at, cfg.steps * cfg.master * loud, 0.94f + 0.12f * (float)rng.U(), 0.6f);
            nSteps++;
        }

        /// <summary>Two ants touching antennae. Quieter when the player only overhears it.</summary>
        public static void Touch(Vector3 at, bool overheard = false)
        {
            if (cfg == null) return;
            if (!natural) { Classic.Touch(at, overheard); return; }
            if (!Ready() || overheard && !Near(at, 7f)) return;
            Play(touches[rng.Int(touches.Length)], at, cfg.touch * cfg.master * (overheard ? 0.45f : 1f), 0.94f + 0.12f * (float)rng.U(), 0.6f);
            nTouches++;
        }

        /// <summary>
        /// A glowing plant flaring under an antenna: a soft breath of light. A plant lit within flareMerge seconds of the
        /// last swells that breath a little (to +4 dB at most) instead of starting another, so a cluster lit at once is
        /// one swell, never a burst; and a breath still sounding is never cut for a new one.
        /// </summary>
        public static void Flare(Vector3 at)
        {
            if (cfg == null) return;
            if (!natural) { Classic.Flare(at); return; }
            if (!Ready()) return;
            float now = Time.unscaledTime;
            int newest = -1, free = -1;
            for (int i = 0; i < glow.Length; i++)
            {
                if (!glow[i].isPlaying) { if (free < 0) free = i; continue; }
                if (newest < 0 || glowStart[i] > glowStart[newest]) newest = i;
            }
            if (newest >= 0 && (now - glowStart[newest] < cfg.flareMerge || free < 0))
            {
                glowTarget[newest] = Mathf.Min(glowTarget[newest] * 1.2f, glowCap[newest]);
                nSwells++;
                return;
            }
            AudioSource s = glow[free];
            s.clip = flares[Pick(flares.Length, ref lastFlare)];
            s.transform.position = at;
            s.pitch = 0.96f + 0.08f * (float)rng.U();
            glowLevel[free] = glowTarget[free] = cfg.flare * cfg.master * (0.85f + 0.15f * (float)rng.U());
            glowCap[free] = glowLevel[free] * 1.6f;
            s.volume = glowLevel[free];
            s.Play();
            glowStart[free] = now;
            nFlares++;
        }

        /// <summary>Through a portal or a teleport, or into another world: a gust through a tunnel.</summary>
        public static void Gate(Vector3 at)
        {
            if (cfg == null) return;
            if (!natural) { Classic.Gate(at); return; }
            if (Ready()) { Play(gate, at, cfg.gate * cfg.master, 1f, 0f); nGates++; }
        }

        /// <summary>Into another nest of a stitched world: a softer breath of that air.</summary>
        public static void Veil(Vector3 at)
        {
            if (cfg == null) return;
            if (!natural) { Classic.Veil(at); return; }
            if (Ready()) Play(veil, at, cfg.veil * cfg.master, 1f, 0f);
        }

        public static void Map(bool open)
        {
            if (cfg == null) return;
            if (!natural) { Classic.Map(open); return; }
            if (Ready()) { Play(open ? mapOpen : mapClose, Vector3.zero, cfg.map * cfg.master, 1f, 0f); nMaps++; }
        }

        /// <summary>An answer (Voices.Chime: an ant's line, a relic, a story, a visitor's thing, a tutorial step, the end
        /// of a chapter): one drop in a cave, a little higher or lower with the speaker's pitch. False in the classic
        /// direction, where Voices.Chime rings its bell.</summary>
        public static bool Reply(Vector3 at, float pitch, float volume)
        {
            if (cfg == null || !natural) return false;
            if (!Ready()) return true;
            float p = Mathf.Clamp(Mathf.Pow(Mathf.Max(0.3f, pitch), 0.35f), 0.7f, 1.3f);
            Play(replies[rng.Int(replies.Length)], at, cfg.reply * cfg.master * volume / 0.4f, p, 0.65f, 2f, 40f);
            nReplies++;
            return true;
        }

        /// <summary>The guiding light: a beam breathes as the ant reaches its foot (once, until the ant has been away).</summary>
        static void GuideTick(Vector3 ant)
        {
            List<Vector2> feet = GuideThreads.BeamFeet;
            if (feet.Count != beamCount)
            {
                beamCount = feet.Count;
                beamArmed = new bool[beamCount];
                beamLast = new float[beamCount];
                for (int i = 0; i < beamCount; i++) { beamArmed[i] = true; beamLast[i] = -999f; }
            }
            float now = Time.unscaledTime, reach = cfg.guideReach;
            for (int i = 0; i < beamCount; i++)
            {
                Vector2 f = feet[i];
                float d = new Vector2(ant.x - f.x, ant.z - f.y).magnitude;
                if (beamArmed[i] && d < reach)
                {
                    beamArmed[i] = false;
                    beamLast[i] = now;
                    Play(guide, new Vector3(f.x, ant.y, f.y), cfg.guide * cfg.master, 0.97f + 0.06f * (float)rng.U(), 0.8f, 4f, 45f);
                    nGuides++;
                }
                else if (!beamArmed[i] && d > reach * 2.5f && now - beamLast[i] > 20f) beamArmed[i] = true;
            }
        }

        static int Pick(int n, ref int last)
        {
            int v = last < 0 ? rng.Int(n) : rng.Int(n - 1);
            if (last >= 0 && v >= last) v++;
            last = v;
            return v;
        }

        static bool Near(Vector3 at, float range)
        {
            Camera c = Camera.main;
            return c != null && (c.transform.position - at).sqrMagnitude < range * range;
        }

        /// <summary>A sound on a voice of its own (its pitch and place never moved under another's tail): a free voice,
        /// or, with all 24 busy, the one ending soonest.</summary>
        static void Play(AudioClip clip, Vector3 at, float volume, float pitch, float blend, float minD = 2f, float maxD = 30f)
        {
            if (clip == null || volume <= 0f) return;
            int k = -1;
            float soonest = float.MaxValue;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].isPlaying) { k = i; break; }
                if (poolEnds[i] < soonest) { soonest = poolEnds[i]; k = i; }
            }
            AudioSource s = pool[k];
            s.Stop();
            s.clip = clip;
            s.transform.position = blend > 0f || Camera.main == null ? at : Camera.main.transform.position;
            s.spatialBlend = blend;
            s.minDistance = minD;
            s.maxDistance = maxD;
            s.pitch = pitch;
            s.volume = Mathf.Clamp01(volume);
            s.Play();
            poolEnds[k] = Time.unscaledTime + clip.length / Mathf.Max(0.05f, pitch);
        }

        /// <summary>Checks (-sfxdump): every clip as a WAV file, and the levels, to listen to outside the game.</summary>
        public static void Dump(string dir)
        {
            Directory.CreateDirectory(dir);
            if (!natural) { Classic.Dump(dir); return; }
            maker?.Join();
            Ready();
            void W(string name, AudioClip c)
            {
                if (c == null) return;
                var data = new float[c.samples * c.channels];
                c.GetData(data, 0);
                Dsp.WriteWav(Path.Combine(dir, name + ".wav"), data, c.channels, c.frequency);
            }
            for (int i = 0; i < 8; i++) { W($"step_soft_{i}", stepSoft[i]); W($"step_dry_{i}", stepDry[i]); }
            for (int i = 0; i < touches.Length; i++) W($"touch_{i}", touches[i]);
            for (int i = 0; i < flares.Length; i++) W($"flare_{i}", flares[i]);
            for (int i = 0; i < replies.Length; i++) W($"reply_{i}", replies[i]);
            W("map_open", mapOpen);
            W("map_close", mapClose);
            W("gate", gate);
            W("veil", veil);
            W("guide", guide);
            Music.Dump(dir, W);
            File.WriteAllText(Path.Combine(dir, "levels.json"), JsonUtility.ToJson(cfg, true));
        }

        /// <summary>The sounds before 11-04, kept as they were (SoundSettings.direction = "classic").</summary>
        static class Classic
        {
            const int Rate = 44100;
            // their levels then (SoundSettings' defaults before 11-04)
            const float Ambience = 0.16f, Steps = 0.14f, TouchLevel = 0.3f, FlareLevel = 0.22f, GateLevel = 0.35f, MapLevel = 0.25f;
            static SoundSettings cfg;
            static AudioClip[] stepsSoft, stepsDry;
            static AudioClip murmur, shimmer, breath, paperOpen, paperClose, veil;
            static AudioClip airOpen, airTunnel;
            static AudioSource[] voices;
            static AudioSource bedA, bedB;
            static int next;
            static float lastFlare = -1;
            static System.Random rng;
            static bool tunnel, warm;

            public static void Init(SoundSettings settings, Camera cam)
            {
                cfg = settings;
                if (voices == null)
                {
                    rng = new System.Random(11);
                    stepsSoft = new AudioClip[8];
                    stepsDry = new AudioClip[8];
                    for (int i = 0; i < 8; i++)
                    {
                        stepsSoft[i] = Make($"StepSoft{i}", Rustle(1100, 2600));
                        stepsDry[i] = Make($"StepDry{i}", Rustle(1900, 4200));
                    }
                    murmur = Make("Murmur", Murmur());
                    shimmer = Make("Shimmer", Shimmer(1f));
                    veil = Make("Veil", Shimmer(0.6f));
                    breath = Make("Breath", Breath());
                    paperOpen = Make("PaperOpen", Paper(0.16f));
                    paperClose = Make("PaperClose", Paper(0.1f));
                    airOpen = Make("AirOpen", OpenAir());
                    airTunnel = Make("AirTunnel", TunnelAir());
                    voices = new AudioSource[12];
                    for (int i = 0; i < voices.Length; i++)
                    {
                        var go = new GameObject("SfxVoice");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        voices[i] = go.AddComponent<AudioSource>();
                        voices[i].playOnAwake = false;
                        voices[i].spatialBlend = 0.6f;
                        voices[i].rolloffMode = AudioRolloffMode.Linear;
                        voices[i].minDistance = 2;
                        voices[i].maxDistance = 30;
                    }
                }
                bedA = cam.gameObject.AddComponent<AudioSource>();
                bedB = cam.gameObject.AddComponent<AudioSource>();
                foreach (AudioSource b in new[] { bedA, bedB })
                {
                    b.loop = true;
                    b.spatialBlend = 0;
                    b.volume = 0;
                    b.playOnAwake = false;
                }
                bedA.clip = airOpen;
                bedB.clip = airTunnel;
                bedA.Play();
                bedB.Play();
                tunnel = false;
            }

            public static void SetPlace(bool inTunnel, bool warmBiome)
            {
                tunnel = inTunnel;
                warm = warmBiome;
                if (bedA != null) bedA.pitch = warm ? 1.12f : 1f;
            }

            public static void Tick(float dt)
            {
                if (bedA == null || cfg == null) return;
                float k = 1 - Mathf.Exp(-1.5f * dt);
                bedA.volume += ((tunnel ? 0.25f : 1f) * Ambience * cfg.master - bedA.volume) * k;
                bedB.volume += ((tunnel ? 1f : 0f) * Ambience * cfg.master - bedB.volume) * k;
            }

            public static void Step(Vector3 at, bool dryGround, bool sprint)
            {
                AudioClip[] set = dryGround ? stepsDry : stepsSoft;
                float loud = (0.55f + 0.45f * (float)rng.NextDouble()) * (sprint ? 1.2f : 1f);
                Play(set[rng.Next(set.Length)], at, Steps * loud, 0.92f + (float)rng.NextDouble() * 0.16f);
            }

            public static void Touch(Vector3 at, bool overheard)
            {
                if (overheard && !Near(at, 7f)) return;
                Play(murmur, at, TouchLevel * (overheard ? 0.45f : 1f), 0.85f + (float)rng.NextDouble() * 0.35f);
            }

            public static void Flare(Vector3 at)
            {
                if (Time.time - lastFlare < 0.18f) return;
                lastFlare = Time.time;
                Play(shimmer, at, FlareLevel, 0.85f + (float)rng.NextDouble() * 0.4f);
            }

            public static void Gate(Vector3 at) => Play(breath, at, GateLevel, 1f, true);

            public static void Veil(Vector3 at) => Play(veil, at, GateLevel * 0.6f, 0.8f, true);

            public static void Map(bool open) => Play(open ? paperOpen : paperClose, Vector3.zero, MapLevel, open ? 1f : 1.15f, true);

            static void Play(AudioClip clip, Vector3 at, float volume, float pitch, bool flat = false)
            {
                AudioSource s = voices[next++ % voices.Length];
                s.transform.position = flat && Camera.main != null ? Camera.main.transform.position : at;
                s.spatialBlend = flat ? 0 : 0.6f;
                s.pitch = pitch;
                s.PlayOneShot(clip, volume * cfg.master);
            }

            public static void Dump(string dir)
            {
                var all = new List<AudioClip> { stepsSoft[0], stepsSoft[1], stepsDry[0], stepsDry[1], murmur, shimmer, veil, breath, paperOpen, paperClose, airOpen, airTunnel };
                foreach (AudioClip c in all)
                {
                    var data = new float[c.samples];
                    c.GetData(data, 0);
                    Dsp.WriteWav(Path.Combine(dir, c.name + ".wav"), data, 1, Rate);
                }
            }

            static AudioClip Make(string name, float[] data)
            {
                var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }

            static float Noise() => (float)(rng.NextDouble() * 2 - 1);

            static void Filter(float[] x, float freq, float q, bool lowPass = false)
            {
                float w = 2 * Mathf.PI * freq / Rate, cw = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2 * q);
                float b0, b1, b2, a0 = 1 + alpha, a1 = -2 * cw, a2 = 1 - alpha;
                if (lowPass) { b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; }
                else { b0 = alpha; b1 = 0; b2 = -alpha; }
                float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    float y = (b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                    x2 = x1; x1 = x[i]; y2 = y1; y1 = y;
                    x[i] = y;
                }
            }

            static void Normalise(float[] x, float peak)
            {
                float m = 1e-6f;
                foreach (float v in x) m = Mathf.Max(m, Mathf.Abs(v));
                for (int i = 0; i < x.Length; i++) x[i] *= peak / m;
            }

            static float[] Rustle(float centre, float lowPass)
            {
                int n = (int)(Rate * 0.16f);
                var x = new float[n];
                float t0 = 0.004f;
                int grains = 2 + rng.Next(3);
                for (int k = 0; k < grains; k++)
                {
                    float len = 0.012f + (float)rng.NextDouble() * 0.03f, amp = 0.35f + (float)rng.NextDouble() * 0.65f;
                    int a = (int)(t0 * Rate), m = (int)(len * Rate);
                    for (int i = 0; i < m && a + i < n; i++)
                    {
                        float u = i / (float)m;
                        float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.5f) * (1 - 0.4f * u);
                        x[a + i] += Noise() * amp * env;
                    }
                    t0 += len * (0.4f + (float)rng.NextDouble() * 0.9f) + 0.004f + (float)rng.NextDouble() * 0.02f;
                }
                Filter(x, centre * (0.8f + (float)rng.NextDouble() * 0.4f), 0.55f);
                Filter(x, lowPass, 0.6f, true);
                Filter(x, lowPass * 0.8f, 0.6f, true);
                Normalise(x, 0.5f);
                return x;
            }

            static float[] Murmur()
            {
                int n = (int)(Rate * 0.3f);
                var x = new float[n];
                float phase = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float f = t < 0.13f ? Mathf.Lerp(420, 520, t / 0.13f) : Mathf.Lerp(500, 450, (t - 0.13f) / 0.17f);
                    f *= 1 + 0.01f * Mathf.Sin(2 * Mathf.PI * 7 * t);
                    phase += 2 * Mathf.PI * f / Rate;
                    float env = t < 0.13f ? Mathf.Sin(Mathf.PI * t / 0.13f) : 0.85f * Mathf.Sin(Mathf.PI * (t - 0.13f) / 0.17f);
                    x[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(2 * phase) + 0.08f * Noise()) * env;
                }
                Filter(x, 2400, 0.7f, true);
                Normalise(x, 0.7f);
                return x;
            }

            static float[] Shimmer(float bright)
            {
                int n = (int)(Rate * 1.6f);
                var x = new float[n];
                float[] f = { 1318.5f, 1975.5f, 2637f, 3135.9f };
                for (int j = 0; j < f.Length; j++)
                {
                    float fj = f[j] * (0.75f + 0.25f * bright) * (1 + 0.004f * j), ph = (float)rng.NextDouble() * 6.28f;
                    float amp = 1f / (1 + j * 0.8f), start = j * 0.035f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate - start;
                        if (t < 0) continue;
                        float env = Mathf.Min(1, t / 0.09f) * Mathf.Exp(-t / (0.5f + 0.15f * j));
                        x[i] += amp * env * Mathf.Sin(2 * Mathf.PI * fj * t + ph);
                    }
                }
                var air = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    air[i] = Noise() * Mathf.Min(1, t / 0.12f) * Mathf.Exp(-t / 0.35f);
                }
                Filter(air, 2200 * bright + 600, 0.8f);
                for (int i = 0; i < n; i++) x[i] += 0.5f * air[i];
                Filter(x, 5000, 0.7f, true);
                Normalise(x, 0.6f);
                return x;
            }

            static float[] Breath()
            {
                int n = (int)(Rate * 1.2f);
                var x = new float[n];
                const int slices = 24;
                int len = n / slices;
                for (int s = 0; s < slices; s++)
                {
                    var seg = new float[len + 400];
                    for (int i = 0; i < seg.Length; i++) seg[i] = Noise();
                    float u = s / (float)(slices - 1);
                    Filter(seg, Mathf.Lerp(300, 2200, Mathf.Sin(u * Mathf.PI * 0.85f)), 1.4f);
                    for (int i = 0; i < seg.Length; i++)
                    {
                        int k = s * len + i;
                        if (k >= n) break;
                        float w = i < 400 ? i / 400f : i > len ? 1 - (i - len) / 400f : 1;
                        x[k] += seg[i] * w;
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.2f));
                    x[i] = x[i] * env * env + 0.35f * Mathf.Sin(2 * Mathf.PI * 70 * t) * env;
                }
                Filter(x, 2500, 0.7f, true);
                Normalise(x, 0.7f);
                return x;
            }

            static float[] Paper(float length)
            {
                int n = (int)(Rate * (length + 0.05f));
                var x = new float[n];
                int crackles = length > 0.12f ? 9 : 6;
                for (int k = 0; k < crackles; k++)
                {
                    float t0 = (float)rng.NextDouble() * length, amp = 0.3f + (float)rng.NextDouble() * 0.7f;
                    for (int i = (int)(t0 * Rate); i < n; i++)
                    {
                        float t = i / (float)Rate - t0;
                        if (t > 0.02f) break;
                        x[i] += Noise() * amp * Mathf.Exp(-t / 0.004f);
                    }
                }
                for (int i = 0; i < n; i++) x[i] += 0.15f * Noise() * Mathf.Sin(Mathf.PI * i / n);
                Filter(x, 2400, 0.9f);
                Normalise(x, 0.6f);
                return x;
            }

            static float[] OpenAir()
            {
                const float secs = 24f;
                int n = (int)(Rate * secs);
                var wind = new float[n];
                float brown = 0;
                for (int i = 0; i < n; i++) { brown = brown * 0.995f + Noise() * 0.05f; wind[i] = brown; }
                Filter(wind, 420, 0.6f, true);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    wind[i] *= 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * t / secs * 2 + 1.3f) * Mathf.Sin(2 * Mathf.PI * t / secs * 3);
                }
                Normalise(wind, 0.5f);
                var x = wind;
                for (float t0 = 0.4f; t0 < secs - 0.6f; t0 += 0.5f + (float)rng.NextDouble() * 1.4f)
                {
                    float f = 4100 + (float)rng.NextDouble() * 500, amp = 0.025f + (float)rng.NextDouble() * 0.03f;
                    int pulses = 3 + rng.Next(4);
                    for (int p = 0; p < pulses; p++)
                    {
                        float ts = t0 + p * 0.033f;
                        for (int i = (int)(ts * Rate); i < n && i < (int)((ts + 0.014f) * Rate); i++)
                        {
                            float t = i / (float)Rate - ts;
                            x[i] += amp * Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Sin(Mathf.PI * t / 0.014f);
                        }
                    }
                }
                return Loop(x);
            }

            static float[] TunnelAir()
            {
                const float secs = 16f;
                int n = (int)(Rate * secs);
                var x = new float[n];
                float brown = 0;
                for (int i = 0; i < n; i++) { brown = brown * 0.997f + Noise() * 0.04f; x[i] = brown; }
                Filter(x, 160, 0.7f, true);
                Normalise(x, 0.45f);
                for (int i = 0; i < n; i++) x[i] += 0.05f * Mathf.Sin(2 * Mathf.PI * 55 * i / (float)Rate);
                for (float t0 = 1.5f; t0 < secs - 1f; t0 += 2.5f + (float)rng.NextDouble() * 3.5f)
                {
                    float f0 = 1300 + (float)rng.NextDouble() * 700, amp = 0.08f + (float)rng.NextDouble() * 0.06f;
                    for (int i = (int)(t0 * Rate); i < n && i < (int)((t0 + 0.35f) * Rate); i++)
                    {
                        float t = i / (float)Rate - t0;
                        float f = f0 * (1 + 0.6f * Mathf.Exp(-t / 0.01f));
                        x[i] += amp * Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t / 0.07f) * Mathf.Min(1, t * 2000);
                    }
                }
                return Loop(x);
            }

            static float[] Loop(float[] x)
            {
                int m = Rate / 2, n = x.Length;
                for (int i = 0; i < m; i++)
                {
                    float w = i / (float)m;
                    x[i] = x[i] * w + x[n - m + i] * (1 - w);
                }
                Array.Resize(ref x, n - m);
                return x;
            }
        }
    }
#endif

    /// <summary>The garden's sound (GameConfig.audio). The natural direction's clips are all made equally loud (Dsp.Ref),
    /// so its levels are plain gains: 0.5 is 6 dB under Ref, whatever the sound.</summary>
    [Serializable]
    public class SoundSettings
    {
        public string direction = "natural"; // "natural" (the user's pick, 11-04) or "classic" (the sounds before)
        public float master = 1f;
        public float steps = 0.45f;           // the player's feet: heard now (before 11-04 they sat 20 dB under the rest; their clips peak at 0.98, about 4 dB under Ref)
        public float touch = 0.5f;            // antennae touching (once the loudest thing in the game, now under the gate)
        public float flare = 0.3f;            // a plant lighting up: a soft breath, a little under the rest (the user, 11-04)
        public float flareMerge = 0.7f;       // plants lit within this many seconds swell one breath instead of a burst
        public float map = 0.42f;             // opening and closing the map
        public float gate = 0.79f;            // through a light gate: the loudest, a change of world
        public float veil = 0.45f;            // between two nests of a stitched world
        public float guide = 0.45f;           // a beam of the guiding light as the ant reaches it
        public float guideReach = 3f;         // how near its foot that is (units)
        public float reply = 0.45f;           // an answer, at Voices.Chime's volume 0.4 (its callers' 0.12-0.5 scale it)
        public float ambience = 0.126f;       // the bed of nest air
        public string dryNests = "red,orange,yellow";   // the nests whose ground is dry underfoot (NestTheme ids: kiln, amber, honey)
        public bool musicOn = true;           // a little music under the air (-music 0 turns it off for a run)
        public float music = 0.17f;
        // each nest's key (NestTheme id and note); a nest not named here takes its hue: a fifth up for every 30°, from A-flat at 0°
        public string nestKeys = "red Ab, orange Eb, yellow Bb, chartreuse F, green C, cyan D";

        public bool IsDry(string nest) => !string.IsNullOrEmpty(nest) && Array.IndexOf(dryNests.Replace(" ", "").Split(','), nest) >= 0;

        /// <summary>The tonic of a nest's key as a MIDI note between F#3 and F4.</summary>
        public int KeyFor(string nest, float hue)
        {
            string[] names = { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };
            int pc = -1;
            foreach (string pair in nestKeys.Split(','))
            {
                string[] kv = pair.Trim().Split(' ');
                if (kv.Length == 2 && kv[0] == nest) pc = Array.IndexOf(names, kv[1].Replace("Db", "C#").Replace("D#", "Eb").Replace("Gb", "F#").Replace("G#", "Ab").Replace("A#", "Bb"));
            }
            if (pc < 0) pc = ((8 + 7 * (int)Math.Round(hue / 30f)) % 12 + 12) % 12;
            int m = 48 + pc;
            while (m < 54) m += 12;
            while (m > 65) m -= 12;
            return m;
        }
    }

    /// <summary>
    /// The natural direction's sounds (Tools/sound/directions.py, Natural, line for line, with the same random draws in
    /// the same order). No Unity: compiled outside it (SFX_SYNTH_ONLY) to compare with the Python renders.
    /// </summary>
    public static class SfxSynth
    {
        static int Secs(double v) => Dsp.Secs(v);
        static int Ms(double v) => Dsp.Ms(v);

        /// <summary>A few feet on the ground (three of six, nearly together): soft pats of soil, each with a tiny tick of
        /// chitin; on dry ground crisper, with a grain or two of litter after.</summary>
        public static float[] Step(bool dry, int v)
        {
            var r = new Dsp.Rand(1000 + v * 7 + (dry ? 500 : 0));
            var y = new float[Secs(0.17)];
            int feet = r.U() < 0.7 ? 3 : 2;
            double t = 0.003;
            for (int k = 0; k < feet; k++)
            {
                int at = Secs(t);
                double bd = r.Range(6, 10);
                int bs = r.Int(1 << 30);
                double bf = dry ? r.Range(700, 1300) : r.Range(420, 820);
                double btau = r.Range(2.0, 3.2);
                float[] pat = Dsp.Burst(bd, bs, 'b', bf, 0.9, dry ? 1.0 : 1.4, btau);
                Dsp.Place(y, Dsp.Unit(pat), at, r.Range(0.6, 1.0) * (dry ? 0.7 : 1.0));
                double f = dry ? r.Range(3000, 5000) : r.Range(2400, 3600);
                double ftau = r.Range(0.45, 0.9) / 1000 * (dry ? 1.0 : 1.3);
                double f2 = r.Range(1.5, 1.9);
                float[] ft = Dsp.Tick(f, ftau, 1.0, f2);
                Dsp.Place(y, Dsp.Unit(ft), at + Ms(0.4), r.Range(0.6, 1.0) * (dry ? 0.5 : 0.16));
                if (dry)
                {
                    int count = r.Int(4);
                    for (int c = 0; c < count; c++)
                    {
                        double cf = r.Range(2500, 6500), ctau = r.Range(0.3, 0.7) / 1000, c2 = r.Range(1.4, 2.0);
                        float[] ck = Dsp.Tick(cf, ctau, 1.0, c2);
                        double ca = 0.25 * Math.Pow(r.U(), 2);
                        Dsp.Place(y, Dsp.Unit(ck), at + Ms(r.Range(4, 45)), ca);
                    }
                }
                t += r.Range(0.006, 0.02);
            }
            y = Dsp.Lp(y, dry ? 7500 : 3000, 0.7);
            y = Dsp.Hp(y, 180, 0.7);
            return Finish(y, 8);
        }

        /// <summary>Antenna stroking antenna: a few soft brushes, a hint of chitin under each.</summary>
        public static float[] Touch(int v)
        {
            var r = new Dsp.Rand(2000 + v * 13);
            var y = new float[Secs(0.55)];
            int strokes = 4 + r.Int(3);
            double t = 0.015;
            for (int i = 0; i < strokes; i++)
            {
                double a = r.Range(0.6, 1.0) * (1 - 0.4 * i / strokes);
                double bd = r.Range(16, 26);
                int bs = r.Int(1 << 30);
                double bf = r.Range(1700, 2600), ba = r.Range(4, 7), bt = r.Range(5, 8);
                float[] br = Dsp.Burst(bd, bs, 'b', bf, 1.4, ba, bt);
                Dsp.Place(y, Dsp.Unit(br), Secs(t), a * 0.5);
                float[] tk = Dsp.Tick(r.Range(2600, 3600), 0.0006, 1.0, 1.8, 0.5);
                Dsp.Place(y, Dsp.Unit(tk), Secs(t) + Ms(r.Range(2, 6)), a * 0.12);
                t += r.Range(0.045, 0.08);
            }
            y = Dsp.Hp(Dsp.Lp(y, 4500, 0.7), 400, 0.7);
            y = Dsp.WithReverb(y, 0.1, 0.5, 0.45, 0.5, 3);
            return Finish(y, 20);
        }

        /// <summary>A plant lighting up: a slow, soft breath of light, never a hit. It rises over 0.12-0.2 s with no
        /// transient, a faint air rides on it, and it dies away into a small room, long enough never to be cut.</summary>
        public static float[] Flare(int v)
        {
            var r = new Dsp.Rand(3000 + v * 17);
            double dur = 2.0;
            int n = Secs(dur);
            float[] src = Dsp.Pink(n, r.Int(1 << 30));
            double f0 = r.Range(540, 760), rise = r.Range(1.3, 1.65), attack = r.Range(0.12, 0.2), tau = r.Range(0.3, 0.45), q = r.Range(2.2, 3.0);
            var fc = new double[n];
            var fc2 = new double[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Dsp.SR;
                fc[i] = f0 * (1 + (rise - 1) * (1 - Math.Exp(-t / 0.2))) * (1 - 0.15 * (1 - Math.Exp(-t / 0.7)));
                fc2[i] = fc[i] * 2.02;
            }
            float[] b1 = Dsp.Svf(src, fc, q, 'b'), b2 = Dsp.Svf(src, fc2, q * 1.3, 'b');
            double[] env = Dsp.RiseFall(n, attack, tau);
            var bloom = new float[n];
            for (int i = 0; i < n; i++) bloom[i] = (float)((b1[i] + 0.28 * b2[i]) * env[i]);
            float[] air = Dsp.Hp(Dsp.Pink(n, r.Int(1 << 30)), 3800, 0.7);
            for (int i = 0; i < n; i++) air[i] = (float)(air[i] * env[i]);
            float[] y = Dsp.Unit(Dsp.Lp(bloom, 3600, 0.7));
            float[] ua = Dsp.Unit(air);
            for (int i = 0; i < n; i++) y[i] += (float)(0.04 * ua[i]);
            Dsp.FadeOut(y, 600);
            y = Dsp.WithReverb(y, 0.22, 1.3, 0.8, 0.55, 8);
            return Finish(y, 300);
        }

        /// <summary>A dry leaf unfolding (the map opening) or folding (closing): clusters of crackle over a soft sweep.</summary>
        static float[] Leaf(double dur, int clusters, double span0, double span1, int seed, double sweep0, double sweep1, double attack, double release)
        {
            var r = new Dsp.Rand(seed);
            int n = Secs(dur);
            var y = new float[n];
            for (int c = 0; c < clusters; c++)
            {
                double c0 = r.Range(span0, span1);
                int count = 2 + r.Int(4);
                for (int k = 0; k < count; k++)
                {
                    c0 += r.Range(0.002, 0.009);
                    double a = Math.Min(0.55, 0.15 * Math.Pow(1 - r.U(), -1 / 2.2));
                    double bd = r.Range(0.8, 2.4);
                    int bs = r.Int(1 << 30);
                    double bf = r.Range(1400, 5000), bq = r.Range(1, 3);
                    float[] cr = Dsp.Burst(bd, bs, 'b', bf, bq, 0.25, 0.6);
                    Dsp.Place(y, Dsp.Unit(cr), Secs(c0), a * r.Sign());
                    if (r.U() < 0.15) Dsp.Place(y, Dsp.Unit(Dsp.Tick(r.Range(700, 1200), 0.004, 1.0, 0)), Secs(c0), a * 0.3);
                }
            }
            float[] pk = Dsp.Pink(n, r.Int(1 << 30));
            var fc = new double[n];
            for (int i = 0; i < n; i++) fc[i] = sweep0 + (sweep1 - sweep0) * (i / (double)Dsp.SR) / dur;
            float[] sw = Dsp.Svf(pk, fc, 0.7, 'l');
            double[] e = Dsp.Swell(n, attack, release, 1.0);
            for (int i = 0; i < n; i++) sw[i] = (float)(sw[i] * e[i]);
            float[] uy = Dsp.Unit(y), us = Dsp.Unit(sw);
            for (int i = 0; i < n; i++) uy[i] += (float)(0.55 * us[i]);
            float[] o = Dsp.Lp(Dsp.Hp(uy, 250, 0.7), 6500, 0.7);
            return Dsp.WithReverb(o, 0.08, 0.4, 0.4, 0.5, 2);
        }

        public static float[] MapOpen() => Finish(Leaf(0.5, 7, 0.03, 0.4, 4001, 600, 2600, 0.22, 0.26), 30);

        public static float[] MapClose() => Finish(Leaf(0.32, 4, 0.02, 0.2, 4002, 2200, 700, 0.06, 0.22), 30);

        /// <summary>Through a light gate: a gust of air through a tunnel. soft: the veil between two nests of a stitched
        /// world, shorter and darker.</summary>
        public static float[] Gate(bool soft)
        {
            var r = new Dsp.Rand(soft ? 5002 : 5001);
            double dur = soft ? 1.8 : 2.8;
            int n = Secs(dur);
            double[] env = soft ? Dsp.Swell(n, 0.5, 1.2, 1.2) : Dsp.Swell(n, 0.8, 1.9, 1.2);
            float[] src = Dsp.Pink(n, r.Int(1 << 30));
            var gfc = new double[n];
            var t1 = new double[n];
            var t2 = new double[n];
            for (int i = 0; i < n; i++)
            {
                gfc[i] = soft ? 300 + 1300 * Math.Pow(env[i], 1.5) : 350 + 2000 * Math.Pow(env[i], 1.5);
                double bend = 1 - 0.05 * (i / (double)Dsp.SR) / dur;
                t1[i] = 170 * bend;
                t2[i] = 345 * bend;
            }
            float[] gust = Dsp.Svf(src, gfc, 0.6, 'l');
            float[] tu1 = Dsp.Svf(src, t1, 5, 'b'), tu2 = Dsp.Svf(src, t2, 5, 'b');
            var tube = new float[n];
            for (int i = 0; i < n; i++) tube[i] = (float)(tu1[i] + 0.6 * tu2[i]);
            float[] rumble = Dsp.Hp(Dsp.Hp(Dsp.Lp(Dsp.Brown(n, r.Int(1 << 30), 0.996), 110, 0.7), 40, 0.7), 40, 0.7);
            float[] ug = Dsp.Unit(gust), ut = Dsp.Unit(tube), ur = Dsp.Unit(rumble);
            var y = new float[n];
            for (int i = 0; i < n; i++) y[i] = (float)((ug[i] + (soft ? 0.35 : 0.5) * ut[i] + (soft ? 0.0 : 0.4) * ur[i]) * env[i]);
            y = Dsp.Hp(y, 45, 0.7);
            y = Dsp.WithReverb(y, soft ? 0.25 : 0.3, 2.2, 1.2, 0.45, 12);
            return Finish(y, 200);
        }

        /// <summary>A beam of the guiding light: breath across a hollow reed, far off.</summary>
        public static float[] Guide()
        {
            double dur = 2.4;
            int n = Secs(dur);
            float[] y = Dsp.AirTone(dur, new double[] { 640, 1290 }, new double[] { 70, 50 }, new double[] { 1.0, 0.25 }, 6001,
                Dsp.Swell(n, 0.55, 1.5, 1.0), 0.06, 1100, 0.003);
            y = Dsp.Lp(y, 4000, 0.7);
            y = Dsp.WithReverb(y, 0.5, 2.4, 1.2, 0.5, 20);
            return Finish(y, 200);
        }

        /// <summary>An answer: one drop in a cave, soft at the start (no click), low, left to ring in the dark.</summary>
        public static float[] Reply(int v)
        {
            var r = new Dsp.Rand(7000 + v * 11);
            double f0 = r.Range(560, 680), rise = r.Range(0.9, 1.2);
            float[] y = Dsp.Plink(f0, rise, 0.022, 14, 0.15, 5.0);
            y = Dsp.Lp(y, 2600, 0.7);
            y = Dsp.WithReverb(y, 0.55, 2.2, 1.1, 0.55, 15);
            return Finish(y, 250);
        }

        /// <summary>Every finished sound: a 35 Hz high-pass (no DC, no rumble nobody hears), the end faded.</summary>
        static float[] Finish(float[] y, double fadeMs)
        {
            float[] o = Dsp.Hp(y, 35, 0.7);
            Dsp.FadeOut(o, fadeMs);
            return o;
        }
    }

    /// <summary>
    /// The building blocks of the made sounds (Tools/sound/dsp.py, line for line): a counter-hash noise and an xorshift
    /// random (the same numbers there and here), RBJ biquads, Simper's state-variable filter, a two-pole resonator, an
    /// 8-line feedback-delay-network reverb, and the loudness of ITU-R BS.1770 so every clip is made equally loud.
    /// No Unity, so it runs on a worker thread and compiles outside Unity for checks.
    /// </summary>
    public static class Dsp
    {
        public const int SR = 44100;
        public const double Ref = -20.0;     // every clip is made this loud at its loudest moment (LUFS)
        const double TwoPi = 2 * Math.PI;

        public static int Ms(double v) => (int)Math.Round(v * SR / 1000.0);   // half to even, as Python's round
        public static int Secs(double v) => (int)Math.Round(v * SR);

        /// <summary>xorshift32 (dsp.Rand).</summary>
        public sealed class Rand
        {
            uint s;

            public Rand(long seed)
            {
                s = unchecked((uint)((ulong)(seed * 2654435761L + 0x6D2B79F5L) & 0xFFFFFFFFUL));
                if (s == 0) s = 1;
                for (int i = 0; i < 4; i++) NextU32();
            }

            public uint NextU32()
            {
                unchecked
                {
                    s ^= s << 13;
                    s ^= s >> 17;
                    s ^= s << 5;
                }
                return s;
            }

            public double U() => (NextU32() >> 8) / 16777216.0;
            public double Range(double a, double b) => a + (b - a) * U();
            public int Int(int n) => Math.Min(n - 1, (int)(U() * n));
            public double Sign() => U() < 0.5 ? 1.0 : -1.0;
            public T Pick<T>(T[] a) => a[Int(a.Length)];
        }

        /// <summary>White noise in [-1, 1): sample i of stream seed (lowbias32 hash).</summary>
        public static float[] Noise(int n, int seed, int offset = 0)
        {
            var y = new float[n];
            unchecked
            {
                uint sd = (uint)seed * 0x85EBCA77u;
                for (int k = 0; k < n; k++)
                {
                    uint h = (uint)(offset + k) * 0x9E3779B1u + sd;
                    h ^= h >> 16;
                    h *= 0x7FEB352Du;
                    h ^= h >> 15;
                    h *= 0x846CA68Bu;
                    h ^= h >> 16;
                    y[k] = (float)(h / 2147483648.0 - 1.0);
                }
            }
            return y;
        }

        /// <summary>Paul Kellet's economy pink filter over the hash noise.</summary>
        public static float[] Pink(int n, int seed)
        {
            float[] w = Noise(n, seed);
            var y = new float[n];
            double s0 = 0, s1 = 0, s2 = 0;
            for (int i = 0; i < n; i++)
            {
                double v = w[i];
                s0 = 0.99765 * s0 + 0.0990460 * v;
                s1 = 0.96300 * s1 + 0.2965164 * v;
                s2 = 0.57000 * s2 + 1.0526913 * v;
                y[i] = (float)((s0 + s1 + s2 + 0.1848 * v) * 0.22);
            }
            return y;
        }

        /// <summary>Brown noise: a leaky integrator of white noise.</summary>
        public static float[] Brown(int n, int seed, double leak)
        {
            float[] w = Noise(n, seed);
            var y = new float[n];
            double s = 0;
            for (int i = 0; i < n; i++)
            {
                s = leak * s + 0.06 * w[i];
                y[i] = (float)s;
            }
            return y;
        }

        public struct Bq { public double b0, b1, b2, a1, a2; }

        /// <summary>RBJ cookbook coefficients. kind: lp, hp, bp (0 dB at the centre), highshelf.</summary>
        public static Bq Coefs(string kind, double f, double q, double gainDb = 0)
        {
            f = Math.Min(f, SR * 0.49);
            double w = TwoPi * f / SR, cw = Math.Cos(w), sw = Math.Sin(w), alpha = sw / (2 * q), A = Math.Pow(10, gainDb / 40);
            double b0, b1, b2, a0, a1, a2;
            switch (kind)
            {
                case "lp": b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha; break;
                case "hp": b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2; a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha; break;
                case "bp": b0 = alpha; b1 = 0; b2 = -alpha; a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha; break;
                case "highshelf":
                    double s = 2 * Math.Sqrt(A) * alpha;
                    b0 = A * ((A + 1) + (A - 1) * cw + s);
                    b1 = -2 * A * ((A - 1) + (A + 1) * cw);
                    b2 = A * ((A + 1) + (A - 1) * cw - s);
                    a0 = (A + 1) - (A - 1) * cw + s;
                    a1 = 2 * ((A - 1) - (A + 1) * cw);
                    a2 = (A + 1) - (A - 1) * cw - s;
                    break;
                default: throw new ArgumentException(kind);
            }
            return new Bq { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = a1 / a0, a2 = a2 / a0 };
        }

        public static float[] Filter(float[] x, Bq c)
        {
            var y = new float[x.Length];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double v = x[i], o = c.b0 * v + c.b1 * x1 + c.b2 * x2 - c.a1 * y1 - c.a2 * y2;
                x2 = x1; x1 = v; y2 = y1; y1 = o;
                y[i] = (float)o;
            }
            return y;
        }

        public static float[] Lp(float[] x, double f, double q = 0.7071) => Filter(x, Coefs("lp", f, q));
        public static float[] Hp(float[] x, double f, double q = 0.7071) => Filter(x, Coefs("hp", f, q));
        public static float[] Bp(float[] x, double f, double q = 1.0) => Filter(x, Coefs("bp", f, q));

        /// <summary>Simper's TPT state-variable filter, its cutoff per sample (stable when swept).
        /// mode: 'l' low-pass, 'b' band-pass (unity at the centre), 'h' high-pass.</summary>
        public static float[] Svf(float[] x, double[] fc, double q, char mode)
        {
            int n = x.Length;
            var y = new float[n];
            double k = 1.0 / q, ic1 = 0, ic2 = 0;
            for (int i = 0; i < n; i++)
            {
                double g = Math.Tan(Math.PI * Math.Min(fc[i], SR * 0.45) / SR);
                double a1 = 1.0 / (1.0 + g * (g + k)), a2 = g * a1, a3 = g * a2;
                double v3 = x[i] - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2 * v1 - ic1;
                ic2 = 2 * v2 - ic2;
                y[i] = (float)(mode == 'l' ? v2 : mode == 'b' ? k * v1 : x[i] - k * v1 - v2);
            }
            return y;
        }

        public static float[] Svf(float[] x, double fc, double q, char mode)
        {
            int n = x.Length;
            var y = new float[n];
            double k = 1.0 / q, ic1 = 0, ic2 = 0;
            double g = Math.Tan(Math.PI * Math.Min(fc, SR * 0.45) / SR);
            double a1 = 1.0 / (1.0 + g * (g + k)), a2 = g * a1, a3 = g * a2;
            for (int i = 0; i < n; i++)
            {
                double v3 = x[i] - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2 * v1 - ic1;
                ic2 = 2 * v2 - ic2;
                y[i] = (float)(mode == 'l' ? v2 : mode == 'b' ? k * v1 : x[i] - k * v1 - v2);
            }
            return y;
        }

        // ---------------------------------------------------------------- envelopes and helpers

        /// <summary>Raised-cosine fade-in over ms (in place): a soft contact, no click.</summary>
        public static void Onset(float[] x, double msLen)
        {
            int k = Ms(msLen);
            if (k <= 1) return;
            k = Math.Min(k, x.Length);
            for (int i = 0; i < k; i++) x[i] = (float)(x[i] * (0.5 - 0.5 * Math.Cos(Math.PI * i / k)));
        }

        public static void FadeOut(float[] x, double msLen)
        {
            int k = Ms(msLen);
            if (k <= 1) return;
            k = Math.Min(k, x.Length);
            int s = x.Length - k;
            for (int i = 0; i < k; i++) x[s + i] = (float)(x[s + i] * (0.5 + 0.5 * Math.Cos(Math.PI * i / k)));
        }

        /// <summary>A smooth rise over attack s, then a fall over release s (raised-cosine segments).</summary>
        public static double[] Swell(int n, double attack, double release, double curve)
        {
            int a = Secs(attack), rl = Secs(release);
            var e = new double[n];
            int k = Math.Min(a, n);
            for (int i = 0; i < k; i++) e[i] = Math.Pow(0.5 - 0.5 * Math.Cos(Math.PI * i / Math.Max(1, a)), curve);
            int m = Math.Min(rl, n - k);
            for (int i = 0; i < m; i++) e[k + i] = Math.Pow(0.5 + 0.5 * Math.Cos(Math.PI * i / Math.Max(1, rl)), curve);
            return e;
        }

        /// <summary>A raised-cosine rise over attack s, then an exponential fall (time constant tau).</summary>
        public static double[] RiseFall(int n, double attack, double tau)
        {
            var e = new double[n];
            int a = Secs(attack), k = Math.Min(n, a);
            for (int i = 0; i < n; i++) e[i] = Math.Exp(-Math.Max(i / (double)SR - attack, 0.0) / tau);
            for (int i = 0; i < k; i++) e[i] = 0.5 - 0.5 * Math.Cos(Math.PI * i / Math.Max(1, a));
            return e;
        }

        public static void Place(float[] dst, float[] src, int at, double gain = 1.0)
        {
            if (at >= dst.Length || gain == 0) return;
            int m = Math.Min(src.Length, dst.Length - at);
            for (int i = 0; i < m; i++) dst[at + i] += (float)(gain * src[i]);
        }

        public static double Peak(float[] x)
        {
            double m = 0;
            foreach (float v in x) m = Math.Max(m, Math.Abs(v));
            return m + 1e-20;
        }

        public static double Rms(float[] x)
        {
            double s = 0;
            foreach (float v in x) s += (double)v * v;
            return Math.Sqrt(s / Math.Max(1, x.Length) + 1e-20);
        }

        public static float[] Unit(float[] x)
        {
            double p = Peak(x);
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = (float)(x[i] / p);
            return y;
        }

        /// <summary>A resonant click: one or two very short modes (chitin, a twig, an antenna).</summary>
        public static float[] Tick(double f, double tau, double amp, double f2Ratio, double onsetMs = 0.25)
        {
            int n = Secs(tau * 7 + 0.002);
            var y = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR, v = Math.Exp(-t / tau) * Math.Sin(TwoPi * f * t);
                if (f2Ratio > 0) v += 0.45 * Math.Exp(-t / (tau * 0.7)) * Math.Sin(TwoPi * f * f2Ratio * t + 1.1);
                y[i] = (float)(v * amp);
            }
            Onset(y, onsetMs);
            return y;
        }

        /// <summary>A short burst of noise with a soft attack and an exponential fall, filtered ('b', 'l' or 'h').</summary>
        public static float[] Burst(double durMs, int seed, char kind, double f, double q, double attackMs, double tauMs)
        {
            int n = Ms(durMs) + 1;
            double tau = (tauMs > 0 ? tauMs : durMs / 3.0) / 1000.0;
            float[] x = Noise(n, seed);
            for (int i = 0; i < n; i++) x[i] = (float)(x[i] * Math.Exp(-(i / (double)SR) / tau));
            Onset(x, attackMs);
            x = kind == 'b' ? Bp(x, f, q) : kind == 'l' ? Lp(x, f, q) : Hp(x, f, q);
            FadeOut(x, Math.Min(durMs * 0.3, 3.0));
            return x;
        }

        /// <summary>A cloud of ticks at Poisson times (rate per second, thinned), with power-law loudness.</summary>
        public static float[] Crackle(double dur, Func<double, double> rate, int seed, double fLo, double fHi, double tauLoMs, double tauHiMs, double alpha = 1.6, double amp = 1.0)
        {
            var r = new Rand(seed);
            int n = Secs(dur);
            var y = new float[n];
            double peakRate = 0, t = 0;
            for (int x = 0; x <= 50; x++) peakRate = Math.Max(peakRate, rate(x * dur / 50.0));
            peakRate += 1e-6;
            while (true)
            {
                t += -Math.Log(1.0 - r.U() * 0.999999) / peakRate;
                if (t >= dur) break;
                if (r.U() > rate(t) / peakRate) continue;
                double a = Math.Min(1.0, 0.08 * Math.Pow(1.0 - r.U(), -1.0 / alpha));
                double f = r.Range(fLo, fHi), tau = r.Range(tauLoMs, tauHiMs) / 1000.0;
                double sa = a * r.Sign(), f2 = r.Range(1.4, 2.1);
                Place(y, Tick(f, tau, sa, f2), Secs(t));
            }
            if (amp != 1.0) for (int i = 0; i < n; i++) y[i] = (float)(y[i] * amp);
            return y;
        }

        /// <summary>A water drop: the bubble's note rises as it shrinks and rings out (after van den Doel).</summary>
        public static float[] Plink(double f0, double rise, double tau, double chirpMs, double dur, double onsetMs = 0.25)
        {
            int n = Secs(dur);
            var y = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                ph += TwoPi * f0 * (1 + rise * (1 - Math.Exp(-t / (chirpMs / 1000.0)))) / SR;
                y[i] = (float)(Math.Sin(ph) * Math.Exp(-t / tau));
            }
            Onset(y, onsetMs);
            return y;
        }

        /// <summary>Breath through a hollow: pink noise through narrow resonances, each drifting a little, over a broad
        /// breath, under an envelope.</summary>
        public static float[] AirTone(double dur, double[] freqs, double[] qs, double[] gains, int seed, double[] env, double breathGain, double breathF, double wobble)
        {
            int n = Secs(dur);
            float[] src = Pink(n, seed);
            var y = new double[n];
            var r = new Rand(seed + 77);
            for (int j = 0; j < freqs.Length; j++)
            {
                double rate = r.Range(0.15, 0.35), ph = r.Range(0, TwoPi);
                var fc = new double[n];
                for (int i = 0; i < n; i++) fc[i] = freqs[j] * (1 + wobble * Math.Sin(TwoPi * rate * (i / (double)SR) + ph));
                float[] b = Svf(src, fc, qs[j], 'b');
                double g = gains[j] * Math.Sqrt(qs[j]);
                for (int i = 0; i < n; i++) y[i] += g * b[i];
            }
            float[] br = Bp(src, breathF, 0.6);
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = (float)((y[i] + breathGain * br[i]) * env[i]);
            return o;
        }

        /// <summary>Equal-power pan of a mono signal (p in [-1, 1]).</summary>
        public static float[][] Pan(float[] x, double p)
        {
            double a = (p + 1) * Math.PI / 4, cl = Math.Cos(a), cr = Math.Sin(a);
            var l = new float[x.Length];
            var r = new float[x.Length];
            for (int i = 0; i < x.Length; i++) { l[i] = (float)(x[i] * cl); r[i] = (float)(x[i] * cr); }
            return new[] { l, r };
        }

        // ---------------------------------------------------------------- reverb

        static readonly Dictionary<string, double> revNorm = new Dictionary<string, double>();

        /// <summary>The wet signal of an 8-line feedback delay network (Hadamard mixing, a one-pole low-pass in each line
        /// for a darker high decay, two input all-passes), its impulse response scaled to unit energy, so dry + wet *
        /// this has a direct-to-reverberant ratio of 1/wet². Longer than x by tail (default rt60 * 1.1). [0] = left
        /// (or mono), [1] = right.</summary>
        public static float[][] Fdn(float[] x, double rt60, double size, double damp, double predelayMs, bool stereo, double tail = -1)
        {
            if (tail < 0) tail = rt60 * 1.1;
            string key = $"{rt60}|{size}|{damp}|{predelayMs}|{stereo}";
            double norm;
            bool known;
            lock (revNorm) known = revNorm.TryGetValue(key, out norm);
            if (!known)
            {
                var imp = new float[Secs(rt60 * 1.6 + 0.2)];
                imp[0] = 1f;
                float[][] ir = FdnCore(imp, rt60, size, damp, predelayMs, stereo);
                double e = 0;
                foreach (float[] c in ir) foreach (float v in c) e += (double)v * v;
                norm = 1.0 / Math.Sqrt(e / ir.Length + 1e-20);
                lock (revNorm) revNorm[key] = norm;
            }
            var xx = new float[x.Length + Secs(tail)];
            Array.Copy(x, xx, x.Length);
            float[][] o = FdnCore(xx, rt60, size, damp, predelayMs, stereo);
            foreach (float[] c in o) for (int i = 0; i < c.Length; i++) c[i] = (float)(c[i] * norm);
            return o;
        }

        static float[][] FdnCore(float[] x, double rt60, double size, double damp, double predelayMs, bool stereo)
        {
            int[] baseD = { 1153, 1327, 1559, 1801, 2063, 2357, 2671, 2999 };
            var d = new int[8];
            var gains = new double[8];
            for (int i = 0; i < 8; i++)
            {
                d[i] = Math.Max(97, (int)(baseD[i] * size));
                gains[i] = Math.Pow(10, -3.0 * d[i] / (SR * rt60));
            }
            int n = x.Length, pre = Ms(predelayMs);
            var u = new double[n];
            for (int i = pre; i < n; i++) u[i] = x[i - pre];
            AllPass(u, (int)(142 * size) + 1, 0.6);
            AllPass(u, (int)(379 * size) + 1, 0.55);
            var lines = new double[8][];
            var pos = new int[8];
            for (int i = 0; i < 8; i++) lines[i] = new double[d[i]];
            var lpz = new double[8];
            var f = new double[8];
            double k = 1 - damp, s8 = 1 / Math.Sqrt(8);
            double[] inj = { 0.5, -0.5, 0.5, -0.5, 0.5, -0.5, 0.5, -0.5 };
            double[] cl = { 1, 1, -1, -1, 1, -1, 1, -1 }, cr = { 1, -1, -1, 1, -1, 1, 1, -1 };
            var outL = new float[n];
            var outR = stereo ? new float[n] : null;
            for (int t = 0; t < n; t++)
            {
                double l = 0, r = 0;
                for (int i = 0; i < 8; i++)
                {
                    lpz[i] = k * lines[i][pos[i]] + damp * lpz[i];
                    f[i] = lpz[i] * gains[i];
                    l += cl[i] * f[i];
                    r += cr[i] * f[i];
                }
                outL[t] = (float)(l * s8);
                if (stereo) outR[t] = (float)(r * s8);
                // Hadamard (Sylvester order) by the fast Walsh-Hadamard transform, normalised
                for (int len = 1; len < 8; len <<= 1)
                    for (int i = 0; i < 8; i += len << 1)
                        for (int j = i; j < i + len; j++)
                        {
                            double a = f[j], b = f[j + len];
                            f[j] = a + b;
                            f[j + len] = a - b;
                        }
                for (int i = 0; i < 8; i++)
                {
                    lines[i][pos[i]] = f[i] * s8 + inj[i] * u[t];
                    if (++pos[i] == d[i]) pos[i] = 0;
                }
            }
            return stereo ? new[] { outL, outR } : new[] { outL };
        }

        static void AllPass(double[] u, int D, double g)
        {
            var y = new double[u.Length];
            for (int i = 0; i < u.Length; i++)
            {
                double v = -g * u[i];
                if (i >= D) v += u[i - D] + g * y[i - D];
                y[i] = v;
            }
            Array.Copy(y, u, u.Length);
        }

        /// <summary>dry + wet * reverb (mono), longer by the tail.</summary>
        public static float[] WithReverb(float[] x, double wet, double rt60, double size, double damp, double predelayMs)
        {
            float[] r = Fdn(x, rt60, size, damp, predelayMs, false)[0];
            var y = new float[r.Length];
            for (int i = 0; i < r.Length; i++) y[i] = (float)((i < x.Length ? x[i] : 0) + wet * r[i]);
            return y;
        }

        // ---------------------------------------------------------------- loudness (ITU-R BS.1770), to make clips equally loud

        static double[] KWeight(float[] x)
        {
            Bq a = Coefs("highshelf", 1500, 1 / Math.Sqrt(2), 4.0), b = Coefs("hp", 38, 0.5);
            var y = new double[x.Length];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0, z1 = 0, z2 = 0, w1 = 0, w2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double v = x[i], o = a.b0 * v + a.b1 * x1 + a.b2 * x2 - a.a1 * y1 - a.a2 * y2;
                x2 = x1; x1 = v; y2 = y1; y1 = o;
                double p = b.b0 * o + b.b1 * z1 + b.b2 * z2 - b.a1 * w1 - b.a2 * w2;
                z2 = z1; z1 = o; w2 = w1; w1 = p;
                y[i] = p;
            }
            return y;
        }

        /// <summary>Momentary loudness (400 ms windows, hop ms apart) in LUFS; channels summed.</summary>
        public static double[] Momentary(float[][] ch, double hopMs)
        {
            int n = ch[0].Length;
            var p = new double[n];
            foreach (float[] c in ch)
            {
                double[] k = KWeight(c);
                for (int i = 0; i < n; i++) p[i] += k[i] * k[i];
            }
            int w = Secs(0.4);
            if (n < w) { Array.Resize(ref p, w); n = w; }
            var cs = new double[n + 1];
            for (int i = 0; i < n; i++) cs[i + 1] = cs[i] + p[i];
            int hop = Ms(hopMs), count = (n - w) / hop + 1;
            var m = new double[count];
            for (int j = 0; j < count; j++)
            {
                int s = j * hop;
                m[j] = -0.691 + 10 * Math.Log10((cs[s + w] - cs[s]) / w + 1e-20);
            }
            return m;
        }

        public static double MomentaryMax(float[][] ch)
        {
            double best = -200;
            foreach (double v in Momentary(ch, 25)) best = Math.Max(best, v);
            return best;
        }

        public static double Integrated(float[][] ch)
        {
            var kept = new List<double>();
            foreach (double v in Momentary(ch, 100)) if (v > -70) kept.Add(v);
            if (kept.Count == 0) return -70;
            double mean = 0;
            foreach (double v in kept) mean += Math.Pow(10, v / 10);
            double rel = 10 * Math.Log10(mean / kept.Count) - 10, sum = 0;
            int c = 0;
            foreach (double v in kept) if (v > rel) { sum += Math.Pow(10, v / 10); c++; }
            return 10 * Math.Log10(sum / Math.Max(1, c));
        }

        /// <summary>A mono clip made Ref loud at its loudest moment (or, for a short spiky one, just under full scale:
        /// a few footsteps at Ref would peak over 1).</summary>
        public static float[] ToRef(float[] x)
        {
            double g = Math.Min(Math.Pow(10, (Ref - MomentaryMax(new[] { x })) / 20), 0.98 / Peak(x));
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = (float)(x[i] * g);
            return y;
        }

        /// <summary>Channels scaled so they are Ref loud: integrated (a bed, a chord) or at the loudest moment (a note);
        /// never over 0.98 at the peak.</summary>
        public static void ToRef(float[][] ch, bool integrated)
        {
            double g = Math.Pow(10, (Ref - (integrated ? Integrated(ch) : MomentaryMax(ch))) / 20), p = 0;
            foreach (float[] c in ch) p = Math.Max(p, Peak(c));
            g = Math.Min(g, 0.98 / p);
            foreach (float[] c in ch) for (int i = 0; i < c.Length; i++) c[i] = (float)(c[i] * g);
        }

        // ---------------------------------------------------------------- WAV

        /// <summary>16-bit PCM WAV from interleaved samples.</summary>
        public static void WriteWav(string path, float[] interleaved, int channels, int rate)
        {
            using var w = new BinaryWriter(File.Create(path));
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + interleaved.Length * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(interleaved.Length * 2);
            foreach (float v in interleaved) w.Write((short)Math.Max(-32768, Math.Min(32767, Math.Round(v * 32767.0))));
        }
    }
}
