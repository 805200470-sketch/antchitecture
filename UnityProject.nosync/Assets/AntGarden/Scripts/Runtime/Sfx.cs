using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The small sounds of the garden, all synthesised at start-up (no audio files): the patter of the player's
    /// feet (softer on moss, crisper on dry clay), a murmur when two ants touch antennae, a shimmer when a glowing
    /// plant flares, a breath of air through a gate, paper for the map, and under everything a quiet bed of night
    /// air: wind and far crickets in the open, a low hum and the odd drip in a tunnel. Quiet on purpose: the world
    /// should sound alive, not busy. Levels in GameConfig.audio.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static SoundSettings cfg;
        static AudioClip[] stepsSoft, stepsDry;
        static AudioClip murmur, shimmer, breath, paperOpen, paperClose, veil;
        static AudioClip airOpen, airTunnel;
        static AudioSource[] voices;
        static AudioSource bedA, bedB;
        static int next;
        static float lastFlare = -1;
        static System.Random rng;

        public static void Init(SoundSettings settings, Camera cam)
        {
            cfg = settings;
            if (voices == null)
            {
                rng = new System.Random(11);
                stepsSoft = new AudioClip[6];
                stepsDry = new AudioClip[6];
                for (int i = 0; i < 6; i++)
                {
                    stepsSoft[i] = Make($"StepSoft{i}", Patter(1900, 0.6f));
                    stepsDry[i] = Make($"StepDry{i}", Patter(4600, 1f));
                }
                murmur = Make("Murmur", Murmur());
                shimmer = Make("Shimmer", Shimmer(1f));
                veil = Make("Veil", Shimmer(0.6f));
                breath = Make("Breath", Breath());
                paperOpen = Make("PaperOpen", Paper(0.16f));
                paperClose = Make("PaperClose", Paper(0.1f));
                airOpen = Make("AirOpen", OpenAir(), true);
                airTunnel = Make("AirTunnel", TunnelAir(), true);
                voices = new AudioSource[12];
                for (int i = 0; i < voices.Length; i++)
                {
                    var go = new GameObject("SfxVoice");
                    Object.DontDestroyOnLoad(go);
                    voices[i] = go.AddComponent<AudioSource>();
                    voices[i].playOnAwake = false;
                    voices[i].spatialBlend = 0.6f;
                    voices[i].rolloffMode = AudioRolloffMode.Linear;
                    voices[i].minDistance = 2;
                    voices[i].maxDistance = 30;
                }
            }
            // the bed of night air rides on the camera, two sources so open air and tunnel can cross-fade
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

        static bool tunnel, warm;

        /// <summary>Which bed of air to hear: open night or tunnel; the warm nest has a drier, higher wind.</summary>
        public static void SetPlace(bool inTunnel, bool warmBiome)
        {
            tunnel = inTunnel;
            warm = warmBiome;
            if (bedA != null) bedA.pitch = warm ? 1.12f : 1f;
        }

        /// <summary>Cross-fade the beds towards the current place; call every frame.</summary>
        public static void Tick(float dt)
        {
            if (bedA == null || cfg == null) return;
            float k = 1 - Mathf.Exp(-1.5f * dt);
            bedA.volume += ((tunnel ? 0.25f : 1f) * cfg.ambience * cfg.master - bedA.volume) * k;
            bedB.volume += ((tunnel ? 1f : 0f) * cfg.ambience * cfg.master - bedB.volume) * k;
        }

        /// <summary>The patter of one step: three feet of a tripod landing a few milliseconds apart.</summary>
        public static void Step(Vector3 at, bool dryGround, bool sprint)
        {
            if (cfg == null) return;
            AudioClip[] set = dryGround ? stepsDry : stepsSoft;
            Play(set[rng.Next(set.Length)], at, cfg.steps * (sprint ? 1.25f : 1f), 0.9f + (float)rng.NextDouble() * 0.25f);
        }

        /// <summary>Two ants touching antennae. Quieter when the player only overhears it.</summary>
        public static void Touch(Vector3 at, bool overheard = false)
        {
            if (cfg == null) return;
            if (overheard && !Near(at, 7f)) return;
            Play(murmur, at, cfg.touch * (overheard ? 0.45f : 1f), 0.85f + (float)rng.NextDouble() * 0.35f);
        }

        /// <summary>A glowing plant flaring under an antenna.</summary>
        public static void Flare(Vector3 at)
        {
            if (cfg == null || Time.time - lastFlare < 0.18f) return;
            lastFlare = Time.time;
            Play(shimmer, at, cfg.flare, 0.85f + (float)rng.NextDouble() * 0.4f);
        }

        /// <summary>Through a portal or a teleport: a breath of air.</summary>
        public static void Gate(Vector3 at) { if (cfg != null) Play(breath, at, cfg.gate, 1f, true); }

        /// <summary>Into another nest through a light gate: a soft veil.</summary>
        public static void Veil(Vector3 at) { if (cfg != null) Play(veil, at, cfg.gate * 0.6f, 0.8f, true); }

        public static void Map(bool open) { if (cfg != null) Play(open ? paperOpen : paperClose, Vector3.zero, cfg.map, open ? 1f : 1.15f, true); }

        static bool Near(Vector3 at, float range)
        {
            Camera c = Camera.main;
            return c != null && (c.transform.position - at).sqrMagnitude < range * range;
        }

        static void Play(AudioClip clip, Vector3 at, float volume, float pitch, bool flat = false)
        {
            AudioSource s = voices[next++ % voices.Length];
            s.transform.position = flat && Camera.main != null ? Camera.main.transform.position : at;
            s.spatialBlend = flat ? 0 : 0.6f;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume * cfg.master);
        }

        /// <summary>Checks: write every sound as a WAV file to listen to outside the game.</summary>
        public static void Dump(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var all = new System.Collections.Generic.List<AudioClip> { stepsSoft[0], stepsSoft[1], stepsDry[0], stepsDry[1], murmur, shimmer, veil, breath, paperOpen, paperClose, airOpen, airTunnel };
            foreach (AudioClip c in all)
            {
                var data = new float[c.samples];
                c.GetData(data, 0);
                using var w = new System.IO.BinaryWriter(System.IO.File.Create(System.IO.Path.Combine(dir, c.name + ".wav")));
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + data.Length * 2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(data.Length * 2);
                foreach (float v in data) w.Write((short)Mathf.Clamp(v * 32767f, -32768f, 32767f));
            }
        }

        // ---------- synthesis

        static AudioClip Make(string name, float[] data, bool loop = false)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise() => (float)(rng.NextDouble() * 2 - 1);

        /// <summary>RBJ biquad (band-pass, constant peak gain, or low-pass), run over a buffer in place.</summary>
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

        /// <summary>Three tiny clicks of claws on ground, 8-16 ms apart, with a faint body thump.</summary>
        static float[] Patter(float centre, float crisp)
        {
            int n = (int)(Rate * 0.07f);
            var x = new float[n];
            float t0 = 0;
            for (int k = 0; k < 3; k++)
            {
                t0 += k == 0 ? 0.002f : 0.008f + (float)rng.NextDouble() * 0.008f;
                float amp = 0.6f + (float)rng.NextDouble() * 0.4f, tau = 0.0015f + 0.0015f * (1 - crisp);
                for (int i = (int)(t0 * Rate); i < n; i++)
                {
                    float t = i / (float)Rate - t0;
                    if (t > tau * 8) break;
                    x[i] += Noise() * amp * Mathf.Exp(-t / tau) * Mathf.Min(1, t * 3000);
                }
            }
            Filter(x, centre * (0.85f + (float)rng.NextDouble() * 0.3f), 1.1f);
            Filter(x, crisp < 1 ? 3500 : 7000, 0.7f, true);   // moss swallows the highs
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                x[i] += 0.2f * Mathf.Sin(2 * Mathf.PI * 170 * t) * Mathf.Exp(-t / 0.006f);
            }
            Normalise(x, 0.8f);
            return x;
        }

        /// <summary>A small two-note murmur, like a questioning "mm-hm", breathy at the edges.</summary>
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

        /// <summary>A soft rising shimmer of a few high partials over a breath of air (bright = 1, veil = darker).</summary>
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

        /// <summary>A breath of air: noise through a band rising and falling, over a low swell.</summary>
        static float[] Breath()
        {
            int n = (int)(Rate * 1.2f);
            var x = new float[n];
            // the sweep is built from short filtered slices, each a little higher
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
                    float w = i < 400 ? i / 400f : i > len ? 1 - (i - len) / 400f : 1;   // overlap the slices
                    x[k] += seg[i] * w;
                }
            }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.2f));
                x[i] = x[i] * env * env + 0.35f * Mathf.Sin(2 * Mathf.PI * 70 * t) * env;
            }
            Filter(x, 2500, 0.7f, true);   // breath, not hiss
            Normalise(x, 0.7f);
            return x;
        }

        /// <summary>A few crackles of paper unfolding (longer) or folding away (shorter).</summary>
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

        /// <summary>
        /// 24 seconds of open night air, looping seamlessly: a soft wind that swells and settles, and crickets
        /// far off, a few short trills at a time.
        /// </summary>
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
            // crickets: trills of 3-6 pulses at about 30 a second, a little different each time
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

        /// <summary>16 seconds of tunnel air: a low hum of moving air and, now and then, a drip far off.</summary>
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
                    float f = f0 * (1 + 0.6f * Mathf.Exp(-t / 0.01f));   // a plink: the pitch drops as it rings
                    x[i] += amp * Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t / 0.07f) * Mathf.Min(1, t * 2000);
                }
            }
            return Loop(x);
        }

        /// <summary>Cross-fade the last half second into the first and drop it, so the loop has no seam.</summary>
        static float[] Loop(float[] x)
        {
            int m = Rate / 2, n = x.Length;
            for (int i = 0; i < m; i++)
            {
                float w = i / (float)m;
                x[i] = x[i] * w + x[n - m + i] * (1 - w);
            }
            System.Array.Resize(ref x, n - m);
            return x;
        }
    }
}
