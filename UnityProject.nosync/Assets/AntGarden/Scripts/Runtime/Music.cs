#if !SFX_SYNTH_ONLY
using UnityEngine;
#endif
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace AntGarden
{
#if !SFX_SYNTH_ONLY
    /// <summary>
    /// A little music under the nest's air (the user, 11-04, of the 40 s sketch: "背景音乐小样是OK的"; Tools/sound/music.py):
    /// a slow, warm pad moving through four chords of the nest's key, now and then a felt-piano note or two, rarely a
    /// glass bell, all in a large dark room. One key per nest (SoundSettings.nestKeys): the colour wheel read as the
    /// circle of fifths, so neighbours in the ring are a fifth apart. Its parts, and the beds of air that Sfx plays, are
    /// made once on a worker thread in one key (MusicSynth.RefRoot) and played higher or lower for each nest's. On a
    /// change of world the old key's music fades and the new one begins. SoundSettings.musicOn / music; -music 0.
    /// </summary>
    public static class Music
    {
        const float NoteGain = 1.45f, BellGain = 0.13f;   // against the pad, as in the sketch (calibrated in Tools/sound)
        static SoundSettings cfg;
        static Thread worker;
        static volatile bool bedsReady, partsReady;
        static MusicSynth.Parts parts;
        static AudioClip bedNest, bedTunnel;
        static AudioClip[] chords, notes, bells;
        static AudioSource[] pads, voices;
        static float[] padBase, voiceBase;
        static bool on, playing;
        static float bus, busTarget, keyPitch = 1f, pendingPitch = -1f;
        static double nextChord, nextNote, nextBell, secondAt = -1;
        static int chordStep, degree = 5, secondDegree, nextPad, nextVoice;
        static float secondVel;
        static readonly int[] order = { 0, 1, 2, 3 };
        static readonly int[] walk = { -2, -1, -1, 1, 1, 2 }, leap = { -2, 2, 3 };
        static readonly Dsp.Rand rng = new Dsp.Rand(29);

        public static void Init(SoundSettings settings)
        {
            cfg = settings;
            on = cfg.musicOn && Game.Arg("-music") != "0";
            if (worker == null)
            {
                parts = new MusicSynth.Parts();
                worker = new Thread(() =>
                {
                    MusicSynth.MakeBeds(parts);
                    bedsReady = true;
                    MusicSynth.MakeMusic(parts);
                    partsReady = true;
                }) { IsBackground = true, Name = "AntGarden music" };
                worker.Start();
            }
            if (pads == null)
            {
                var host = new GameObject("Music");
                UnityEngine.Object.DontDestroyOnLoad(host);
                pads = new AudioSource[4];
                padBase = new float[pads.Length];
                voices = new AudioSource[8];
                voiceBase = new float[voices.Length];
                for (int i = 0; i < pads.Length; i++) pads[i] = Source(host);
                for (int i = 0; i < voices.Length; i++) voices[i] = Source(host);
            }
            float p = KeyPitch();
            if (playing && Mathf.Abs(p - keyPitch) > 1e-3f) { pendingPitch = p; busTarget = 0f; }
            else if (!playing) keyPitch = p;
        }

        static AudioSource Source(GameObject host)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0;
            s.dopplerLevel = 0;
            s.priority = 32;
            return s;
        }

        /// <summary>The bed of nest air (or a tunnel's), once it is made; null before.</summary>
        public static AudioClip Bed(bool tunnel)
        {
            MakeClips();
            return tunnel ? bedTunnel : bedNest;
        }

        static void MakeClips()
        {
            if (bedNest == null && bedsReady)
            {
                bedNest = Sfx.Clip("BedNest", parts.bedNest, 2, MusicSynth.Rate);
                bedTunnel = Sfx.Clip("BedTunnel", parts.bedTunnel, 2, MusicSynth.Rate);
                parts.bedNest = parts.bedTunnel = null;
                Debug.Log($"[AntGarden] sfx: the beds of air made in {parts.bedsMs} ms (worker thread)");
            }
            if (chords == null && partsReady)
            {
                chords = new AudioClip[parts.chords.Length];
                for (int i = 0; i < chords.Length; i++) chords[i] = Sfx.Clip($"MusicChord{i}", parts.chords[i], 2, MusicSynth.Rate);
                notes = new AudioClip[parts.notes.Length];
                for (int i = 0; i < notes.Length; i++) notes[i] = Sfx.Clip($"MusicNote{MusicSynth.LowDegree + i}", parts.notes[i], 2, MusicSynth.Rate);
                bells = new AudioClip[parts.bells.Length];
                for (int i = 0; i < bells.Length; i++) bells[i] = Sfx.Clip($"MusicBell{i}", parts.bells[i], 2, MusicSynth.Rate);
                parts.chords = parts.notes = parts.bells = null;
                Debug.Log($"[AntGarden] music: made in {parts.musicMs} ms (worker thread); key pitch {keyPitch:F3}");
            }
        }

        static float KeyPitch()
        {
            NestTheme th = Game.Lab ? NestTheme.Current : null;
            int root = cfg.KeyFor(th != null ? th.id : "cyan", th != null ? th.hue : 180f);
            return Mathf.Pow(2f, (root - MusicSynth.RefRoot) / 12f);
        }

        /// <summary>Every frame (from Sfx.Tick): the next chord every MusicSynth.Period seconds, a note or two now and
        /// then, rarely a bell; the level eased in and out.</summary>
        public static void Tick(float dt)
        {
            if (cfg == null || pads == null) return;
            MakeClips();
            if (chords == null) return;
            float target = on ? busTarget : 0f;
            bus += (target - bus) * (1 - Mathf.Exp(-dt / (target > bus ? 2.5f : 0.9f)));
            double now = Time.unscaledTimeAsDouble;
            if (pendingPitch > 0f && bus < 0.03f)
            {
                foreach (AudioSource s in pads) s.Stop();
                foreach (AudioSource s in voices) s.Stop();
                keyPitch = pendingPitch;
                pendingPitch = -1f;
                playing = false;
            }
            if (on && !playing && pendingPitch < 0f)
            {
                playing = true;
                busTarget = 1f;
                chordStep = 0;
                nextChord = now + 0.2;
                nextNote = now + rng.Range(4.0, 6.0);
                nextBell = now + rng.Range(18.0, 30.0);
                secondAt = -1;
                Debug.Log($"[AntGarden] music: begins, played at pitch {keyPitch:F3} of its parts ({(Game.Lab && NestTheme.Current != null ? NestTheme.Current.id : "cyan")})");
            }
            if (playing && on && pendingPitch < 0f)
            {
                if (now >= nextChord)
                {
                    PlayOn(pads, padBase, ref nextPad, chords[order[chordStep % 4]], 1f, 0f);
                    chordStep++;
                    if (chordStep % 4 == 0 && rng.U() < 0.3) { int a = order[1]; order[1] = order[2]; order[2] = a; }   // now and then IV before vi
                    nextChord += MusicSynth.Period;
                    if (nextChord < now) nextChord = now + MusicSynth.Period;
                }
                if (now >= nextNote)
                {
                    degree = Mathf.Clamp(degree + rng.Pick(walk), MusicSynth.LowDegree, MusicSynth.HighDegree);
                    float vel = (float)rng.Range(0.18, 0.3);
                    PlayOn(voices, voiceBase, ref nextVoice, notes[degree - MusicSynth.LowDegree], NoteGain * vel / 0.24f, (float)rng.Range(-0.3, 0.3));
                    if (rng.U() < 0.3)
                    {
                        secondDegree = Mathf.Clamp(degree + rng.Pick(leap), MusicSynth.LowDegree, MusicSynth.HighDegree);
                        secondVel = (float)rng.Range(0.14, 0.22);
                        secondAt = now + rng.Range(0.35, 0.7);
                    }
                    nextNote = now + rng.Range(2.6, 5.4);
                }
                if (secondAt > 0 && now >= secondAt)
                {
                    PlayOn(voices, voiceBase, ref nextVoice, notes[secondDegree - MusicSynth.LowDegree], NoteGain * secondVel / 0.24f, (float)rng.Range(-0.3, 0.3));
                    secondAt = -1;
                }
                if (now >= nextBell)
                {
                    int b = rng.Int(bells.Length);
                    PlayOn(voices, voiceBase, ref nextVoice, bells[b], BellGain, b == 0 ? -0.25f : 0.25f);
                    nextBell = now + rng.Range(22.0, 40.0);
                }
            }
            float vol = cfg.music * cfg.master * bus;
            for (int i = 0; i < pads.Length; i++) if (pads[i].isPlaying) pads[i].volume = padBase[i] * vol;
            for (int i = 0; i < voices.Length; i++) if (voices[i].isPlaying) voices[i].volume = voiceBase[i] * vol;
        }

        /// <summary>On the source that started longest ago (with these lengths it has finished), in the nest's key.</summary>
        static void PlayOn(AudioSource[] set, float[] bases, ref int next, AudioClip clip, float gain, float pan)
        {
            int k = next;
            next = (next + 1) % set.Length;
            AudioSource s = set[k];
            s.Stop();
            s.clip = clip;
            s.pitch = keyPitch;
            s.panStereo = pan;
            bases[k] = gain;
            s.volume = gain * cfg.music * cfg.master * bus;
            s.Play();
        }

        /// <summary>Checks (-sfxdump): waits for the worker, then writes the beds, the chords, the notes and the bells.</summary>
        public static void Dump(string dir, Action<string, AudioClip> write)
        {
            worker?.Join();
            MakeClips();
            write("bed_nest", bedNest);
            write("bed_tunnel", bedTunnel);
            for (int i = 0; i < chords.Length; i++) write($"music_chord_{i}", chords[i]);
            for (int i = 0; i < notes.Length; i++) write($"music_note_{MusicSynth.LowDegree + i}", notes[i]);
            for (int i = 0; i < bells.Length; i++) write($"music_bell_{MusicSynth.BellDegrees[i]}", bells[i]);
            string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            File.WriteAllText(Path.Combine(dir, "music.json"),
                $"{{\"refRoot\": {MusicSynth.RefRoot}, \"period\": {F(MusicSynth.Period)}, \"noteGain\": {F(NoteGain)}, \"bellGain\": {F(BellGain)}, " +
                $"\"keyPitch\": {F(KeyPitch())}, \"rate\": {MusicSynth.Rate}}}");
        }
    }
#endif

    /// <summary>
    /// The music's parts and the beds of air (Tools/sound/music.py, line for line), made in one key (RefRoot), kept at
    /// half rate (nothing in them above 9 kHz). No Unity: made on a worker thread, and compiled outside Unity for checks.
    /// </summary>
    public static class MusicSynth
    {
        public const int Rate = 22050;
        public const int RefRoot = 61;                   // C#4: every nest's key is within five semitones of it
        public const double Period = 8.5, Attack = 3.0, Release = 4.0;
        public const int LowDegree = 2, HighDegree = 9;  // the notes wander over these steps of the pentatonic
        public static readonly int[] BellDegrees = { 9, 10 };
        static readonly int[] Penta = { 0, 2, 4, 7, 9 };
        static readonly int[][] Chords =
        {
            new[] { -12, -5, 2, 4 },      // I add9
            new[] { -15, -8, 0, 2 },      // vi add11
            new[] { -19, -12, -3, 4 },    // IV maj7
            new[] { -17, -10, -3, 0 },    // V sus
        };
        const double TwoPi = 2 * Math.PI;
        const int SR = Dsp.SR;

        public sealed class Parts
        {
            public float[] bedNest, bedTunnel;
            public float[][] chords, notes, bells;
            public long bedsMs, musicMs;
        }

        public static void MakeBeds(Parts p)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            p.bedNest = Half(BedLoop(false), true);
            p.bedTunnel = Half(BedLoop(true), true);
            p.bedsMs = watch.ElapsedMilliseconds;
        }

        public static void MakeMusic(Parts p)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            p.chords = new float[Chords.Length][];
            for (int i = 0; i < Chords.Length; i++) p.chords[i] = Half(ChordClip(i), true);
            p.notes = new float[HighDegree - LowDegree + 1][];
            for (int d = LowDegree; d <= HighDegree; d++) p.notes[d - LowDegree] = Half(NoteClip(d, false), false);
            p.bells = new float[BellDegrees.Length][];
            for (int i = 0; i < BellDegrees.Length; i++) p.bells[i] = Half(NoteClip(BellDegrees[i], true), false);
            p.musicMs = watch.ElapsedMilliseconds;
        }

        /// <summary>Made Ref loud (integrated, or at the loudest moment), low-passed, every other sample kept, interleaved.</summary>
        public static float[] Half(float[][] st, bool integrated)
        {
            Dsp.ToRef(st, integrated);
            for (int c = 0; c < st.Length; c++) st[c] = Dsp.Lp(Dsp.Lp(st[c], 9000, 0.7), 9000, 0.7);
            int n = st[0].Length / 2;
            var o = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                o[2 * i] = st[0][2 * i];
                o[2 * i + 1] = st[1][2 * i];
            }
            return o;
        }

        public static double MidiHz(double m) => 440.0 * Math.Pow(2, (m - 69) / 12.0);

        public static int PentaNote(int root, int degree)
        {
            int octave = (int)Math.Floor(degree / 5.0), d = degree - 5 * octave;
            return root + 12 * octave + Penta[d];
        }

        /// <summary>Slow random wandering in [-1, 1]: points every 1/rate s, joined by cosine easing.</summary>
        public static double[] ValueNoise(int n, double rateHz, int seed)
        {
            var r = new Dsp.Rand(seed);
            int step = Math.Max(1, (int)(SR / rateHz));
            var pts = new double[n / step + 3];
            for (int j = 0; j < pts.Length; j++) pts[j] = r.Range(-1, 1);
            var ease = new double[step];
            for (int i = 0; i < step; i++) ease[i] = 0.5 - 0.5 * Math.Cos(Math.PI * i / step);
            var o = new double[n];
            for (int i = 0; i < n; i++)
            {
                int k = i / step;
                o[i] = pts[k] + (pts[k + 1] - pts[k]) * ease[i - k * step];
            }
            return o;
        }

        /// <summary>A warm sustained note: harmonics wandering slowly in level, three copies a few cents apart, and a
        /// breath of air at its pitch.</summary>
        public static float[] PadVoice(int m, int n, int seed, double level)
        {
            double f = MidiHz(m);
            var y = new double[n];
            double[] harm = { 1.0, 0.42, 0.2, 0.11, 0.05, 0.025 };
            double[][] copies = { new[] { 0.0, 0.5, 0.3 }, new[] { 2.6, 0.3, 1.7 }, new[] { -3.4, 0.22, 2.9 } };
            for (int h = 1; h <= harm.Length; h++)
            {
                if (f * h > 4000) break;
                double[] drift = ValueNoise(n, 0.12, seed * 31 + h);
                foreach (double[] cp in copies)
                {
                    double fh = f * h * Math.Pow(2, cp[0] / 1200), g = cp[1] * harm[h - 1], w = TwoPi * fh / SR;
                    double cw = Math.Cos(w), sw = Math.Sin(w), c = Math.Cos(cp[2] * h), s = Math.Sin(cp[2] * h);
                    for (int i = 0; i < n; i++)
                    {
                        y[i] += g * (1 + 0.3 * drift[i]) * s;
                        double c2 = c * cw - s * sw;
                        s = s * cw + c * sw;
                        c = c2;
                    }
                }
            }
            float[] br = Dsp.Svf(Dsp.Pink(n, seed * 7 + 3), f, 40.0, 'b');
            double ry = 0;
            foreach (double v in y) ry += v * v;
            ry = Math.Sqrt(ry / n + 1e-20);
            double kb = 0.25 / (Dsp.Rms(br) + 1e-9) * ry * 0.5;
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = (float)((y[i] + kb * br[i]) * level);
            return o;
        }

        /// <summary>A felt piano note: stretched partials, two strings beating, a two-stage decay, the hammer's soft thump.</summary>
        public static float[] FeltPiano(int m, double vel, double dur, int seed, double bright = 1.0, double thump = 1.0)
        {
            double f0 = MidiHz(m);
            var r = new Dsp.Rand(seed + 101);
            double B = 1.0e-4 * Math.Pow(2, (m - 60) / 18.0);
            double tau1 = Math.Min(7.0, Math.Max(0.5, 3.0 * Math.Pow(261.6 / f0, 0.65)));
            int n = Dsp.Secs(dur);
            double fc = (450 + 2000 * vel) * bright;
            var y = new double[n];
            for (int k = 1; k < 18; k++)
            {
                double fk = k * f0 * Math.Sqrt(1 + B * k * k);
                if (fk > 7500) break;
                double a = Math.Abs(Math.Sin(Math.PI * k * 0.12)) / k / (1 + (fk / fc) * (fk / fc));
                double tau = tau1 / Math.Pow(1 + 0.5 * (k - 1), 0.85) / (1 + (fk / 3500.0) * (fk / 3500.0));
                double cents = 0.7 * r.Range(0.5, 1.5);
                double fa = fk * Math.Pow(2, cents / 1200), fb = fk * Math.Pow(2, -cents / 1200);
                double ph = r.Range(0, TwoPi);
                double wa = TwoPi * fa / SR, wb = TwoPi * fb / SR;
                double cwa = Math.Cos(wa), swa = Math.Sin(wa), cwb = Math.Cos(wb), swb = Math.Sin(wb);
                double ca = Math.Cos(ph), sa = Math.Sin(ph), cb = ca, sb = sa;
                double d1 = Math.Exp(-1.0 / (0.22 * tau * SR)), d2 = Math.Exp(-1.0 / (tau * SR)), e1 = 0.55, e2 = 0.45;
                for (int i = 0; i < n; i++)
                {
                    y[i] += a * (e1 + e2) * 0.5 * (sa + sb);
                    double t2 = ca * cwa - sa * swa;
                    sa = sa * cwa + ca * swa;
                    ca = t2;
                    t2 = cb * cwb - sb * swb;
                    sb = sb * cwb + cb * swb;
                    cb = t2;
                    e1 *= d1;
                    e2 *= d2;
                }
            }
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = (float)y[i];
            Dsp.Onset(o, 3.0 + 3.0 * (1 - vel));
            // the felt hammer and the key: a soft low thump and a faint wooden knock
            int nt = Dsp.Secs(0.06), nk = Dsp.Secs(0.02);
            float[] th = Dsp.Noise(nt, seed + 7);
            for (int i = 0; i < nt; i++) th[i] = (float)(th[i] * Math.Exp(-(i / (double)SR) / 0.009));
            th = Dsp.Lp(th, 240, 0.7);
            Dsp.Onset(th, 1.5);
            for (int i = 0; i < nt; i++) th[i] = (float)(th[i] * (0.5 + 0.8 * vel));
            float[] kn = Dsp.Noise(nk, seed + 8);
            for (int i = 0; i < nk; i++) kn[i] = (float)(kn[i] * Math.Exp(-(i / (double)SR) / 0.0025));
            kn = Dsp.Bp(kn, 1150, 1.4);
            Dsp.Onset(kn, 0.5);
            for (int i = 0; i < nk; i++) kn[i] *= 0.12f;
            Dsp.Place(o, th, 0, 0.25 * thump * MaxAbs(o) / (MaxAbs(th) + 1e-9));
            Dsp.Place(o, kn, 0, MaxAbs(o) / (MaxAbs(kn) + 1e-9) * 0.08);
            o = Dsp.Lp(o, 5200, 0.6);
            Dsp.FadeOut(o, Math.Max(30.0, dur * 150.0));
            return o;
        }

        static double MaxAbs(float[] x)
        {
            double m = 0;
            foreach (float v in x) m = Math.Max(m, Math.Abs(v));
            return m;
        }

        /// <summary>Decaying sine modes struck by a soft mallet (contact ms), soft onset, the end faded.</summary>
        static float[] Modes(double dur, double[] freqs, double[] amps, double[] taus, double contactMs, int phaseSeed)
        {
            int n = Dsp.Secs(dur);
            var y = new double[n];
            var r = new Dsp.Rand(phaseSeed);
            double mfc = 0.6 / (contactMs / 1000.0);
            for (int j = 0; j < freqs.Length; j++)
            {
                double f = freqs[j], a = amps[j];
                if (f >= SR * 0.45 || a == 0) continue;
                double ph = r.Range(0, TwoPi), g = a / Math.Sqrt(1.0 + Math.Pow(f / mfc, 4));
                double w = TwoPi * f / SR, cw = Math.Cos(w), sw = Math.Sin(w), c = Math.Cos(ph), s = Math.Sin(ph);
                double d = Math.Exp(-1.0 / (taus[j] * SR)), e = 1;
                for (int i = 0; i < n; i++)
                {
                    y[i] += g * e * s;
                    double c2 = c * cw - s * sw;
                    s = s * cw + c * sw;
                    c = c2;
                    e *= d;
                }
            }
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = (float)y[i];
            Dsp.Onset(o, contactMs);
            Dsp.FadeOut(o, Math.Max(30.0, dur * 150.0));
            return o;
        }

        /// <summary>A small glass bell: a clear fundamental shimmering slowly, a soft octave, the bar's modes dying fast.</summary>
        public static float[] GlassBell(int m, double vel, double dur, int seed, double contactMs = 2.4)
        {
            double f0 = MidiHz(m), sc = Math.Pow(523.25 / f0, 0.4);
            var r = new Dsp.Rand(seed + 202);
            double beat = r.Range(0.5, 1.1), v6 = 0.6 + vel;
            double[] freqs = { f0 - beat / 2, f0 + beat / 2, 2.0 * f0, 2.76 * f0, 4.07 * f0, 5.40 * f0 };
            double[] amps = { 0.84, 0.16, 0.14, 0.09 * v6, 0.04 * v6, 0.025 * v6 };
            double[] taus = { 1.7 * sc, 1.7 * sc, 0.75 * sc, 0.4 * sc, 0.22 * sc, 0.14 * sc };
            float[] y = Modes(dur, freqs, amps, taus, contactMs, seed + 3);
            int nt = Dsp.Secs(0.004);
            float[] tk = Dsp.Noise(nt, seed + 9);
            for (int i = 0; i < nt; i++) tk[i] = (float)(tk[i] * Math.Exp(-(i / (double)SR) / 0.0008));
            tk = Dsp.Hp(tk, 3000);
            Dsp.Place(y, tk, 0, 0.04 * vel);
            return Dsp.Lp(y, 6500, 0.6);
        }

        static double Rms2(float[][] st)
        {
            double s = 0;
            int n = 0;
            foreach (float[] c in st) { foreach (float v in c) s += (double)v * v; n += c.Length; }
            return Math.Sqrt(s / Math.Max(1, n) + 1e-20);
        }

        /// <summary>One chord of the pad (stereo): rising over Attack, held until the next starts, released over Release,
        /// its large dark room baked in.</summary>
        public static float[][] ChordClip(int ci, int root = RefRoot, int seed = 1)
        {
            int n = Dsp.Secs(Period + Release);
            var env = new double[n];
            for (int i = 0; i < n; i++) env[i] = 1;
            int k = Dsp.Secs(Attack), k2 = Dsp.Secs(Release);
            for (int i = 0; i < k; i++) env[i] = 0.5 - 0.5 * Math.Cos(Math.PI * i / k);
            for (int i = 0; i < k2; i++) env[n - k2 + i] *= 0.5 + 0.5 * Math.Cos(Math.PI * i / k2);
            var pad = new[] { new float[n], new float[n] };
            int[] chord = Chords[ci];
            double[] pans = { -0.05, -0.35, 0.3, 0.45 };
            for (int vi = 0; vi < chord.Length; vi++)
            {
                double level = vi == 0 ? 0.55 : vi < chord.Length - 1 ? 0.8 : 0.65;
                float[] v = PadVoice(root + chord[vi], n, seed * 100 + ci * 10 + vi, level);
                for (int i = 0; i < n; i++) v[i] = (float)(v[i] * env[i]);
                float[][] pp = Dsp.Pan(v, pans[vi % 4]);
                for (int c = 0; c < 2; c++) for (int i = 0; i < n; i++) pad[c][i] += pp[c][i];
            }
            for (int c = 0; c < 2; c++) pad[c] = Dsp.Hp(Dsp.Lp(pad[c], 2400, 0.6), 40, 0.7);
            var mono = new float[n];
            for (int i = 0; i < n; i++) mono[i] = pad[0][i] + pad[1][i];
            float[][] rp = Dsp.Fdn(mono, 4.5, 1.6, 0.45, 20, true, 4.5);
            double gp = 1.0 / Rms2(pad), gr = 0.35 / Rms2(rp);
            int len = rp[0].Length;
            var o = new[] { new float[len], new float[len] };
            for (int c = 0; c < 2; c++)
            {
                for (int i = 0; i < len; i++) o[c][i] = (float)((i < n ? pad[c][i] * gp : 0) + gr * rp[c][i]);
                o[c] = Dsp.Hp(o[c], 35, 0.7);
                Dsp.FadeOut(o[c], 300);
            }
            return o;
        }

        /// <summary>A felt-piano note of the pentatonic (or a glass bell), centred, with the same room baked in (stereo).</summary>
        public static float[][] NoteClip(int degree, bool bell, int root = RefRoot)
        {
            int m = PentaNote(root, degree), seed = degree;
            float[] v = bell ? GlassBell(m, 0.3, 3.0, seed) : FeltPiano(m, 0.24, 4.0, seed);
            if (bell) for (int i = 0; i < v.Length; i++) v[i] *= 0.35f;
            float[][] rm = Dsp.Fdn(v, 4.5, 1.6, 0.45, 20, true, 4.5);
            double gv = 0.7071 / Dsp.Rms(v), gr = 0.9 / Rms2(rm);
            int len = rm[0].Length;
            var o = new[] { new float[len], new float[len] };
            for (int c = 0; c < 2; c++)
            {
                for (int i = 0; i < len; i++) o[c][i] = (float)((i < v.Length ? v[i] * gv : 0) + gr * rm[c][i]);
                o[c] = Dsp.Hp(o[c], 35, 0.7);
                Dsp.FadeOut(o[c], 300);
            }
            return o;
        }

        /// <summary>Night air in the nest (stereo): a dark, slowly breathing room tone, a little air, the far patter of the
        /// colony (thousands of tiny feet, like the finest rain), now and then a drip far off. tunnel: darker, no patter,
        /// drips more often.</summary>
        public static float[][] NestBed(double dur, int seed, bool tunnel)
        {
            int n = Dsp.Secs(dur);
            var o = new[] { new float[n], new float[n] };
            for (int c = 0; c < 2; c++)
            {
                float[] room = Dsp.Lp(Dsp.Brown(n, seed + c, 0.997), tunnel ? 120 : 160, 0.5);
                double[] vr = ValueNoise(n, 0.08, seed + 10 + c);
                for (int i = 0; i < n; i++) room[i] = (float)(room[i] * (0.65 + 0.35 * vr[i]));
                float[] air = Dsp.Bp(Dsp.Pink(n, seed + 20 + c), 330, 0.8);
                double[] va = ValueNoise(n, 0.11, seed + 60 + c);
                for (int i = 0; i < n; i++) air[i] = (float)(air[i] * (0.7 + 0.3 * va[i]));
                float[] high = Dsp.Hp(Dsp.Pink(n, seed + 30 + c), 3500, 0.7);
                room = Dsp.Hp(Dsp.Hp(room, 40, 0.7), 40, 0.7);
                double gr = 1.0 / Dsp.Rms(room), ga = 0.07 / Dsp.Rms(air), gh = 0.006 / Dsp.Rms(high);
                for (int i = 0; i < n; i++) o[c][i] = (float)(room[i] * gr + air[i] * ga + high[i] * gh);
            }
            if (!tunnel)
            {
                var patter = new float[2][];
                for (int c = 0; c < 2; c++) patter[c] = Dsp.Lp(Dsp.Crackle(dur, s => 55.0, seed + 40 + c, 1800, 4200, 0.3, 0.8, 1.8), 3000, 0.7);
                var sum = new float[n];
                for (int i = 0; i < n; i++) sum[i] = patter[0][i] + patter[1][i];
                float[][] pr = Dsp.Fdn(sum, 1.6, 1.1, 0.5, 10, true, 0);
                for (int c = 0; c < 2; c++) for (int i = 0; i < n; i++) patter[c][i] = (float)(0.6 * patter[c][i] + 0.8 * pr[c][i]);
                double gp = 0.035 / (Dsp.Rms(patter[0]) + 1e-9);
                for (int c = 0; c < 2; c++) for (int i = 0; i < n; i++) o[c][i] += (float)(patter[c][i] * gp);
            }
            var r = new Dsp.Rand(seed + 50);
            var dr = new[] { new float[n], new float[n] };
            double t = r.Range(1.5, 3.0);
            while (t < dur - 3.5)
            {
                double f0 = r.Range(750, 1250), rise = r.Range(1.1, 1.7);
                float[] d = Dsp.Plink(f0, rise, 0.014, 11, 0.1);
                double g = r.Range(0.4, 1.0);
                for (int i = 0; i < d.Length; i++) d[i] = (float)(d[i] * g);
                float[][] pp = Dsp.Pan(d, r.Range(-0.8, 0.8));
                for (int c = 0; c < 2; c++) Dsp.Place(dr[c], pp[c], Dsp.Secs(t));
                t += tunnel ? r.Range(2.0, 4.5) : r.Range(3.0, 7.0);
            }
            var ds = new float[n];
            for (int i = 0; i < n; i++) ds[i] = dr[0][i] + dr[1][i];
            float[][] rv = Dsp.Fdn(ds, 2.6, 1.3, 0.55, 20, true, 0);
            for (int c = 0; c < 2; c++)
            {
                for (int i = 0; i < n; i++) o[c][i] += (float)(0.12 * (0.3 * dr[c][i] + rv[c][i]));
                o[c] = Dsp.Hp(o[c], 35, 0.7);
            }
            return o;
        }

        /// <summary>The bed as a seamless 24 s loop: its last half second cross-faded into its first.</summary>
        public static float[][] BedLoop(bool tunnel, double dur = 24.0, int seed = 5)
        {
            int n = Dsp.Secs(dur), m = Dsp.Secs(0.5);
            float[][] x = NestBed(dur + 0.5, seed + (tunnel ? 100 : 0), tunnel);
            var o = new[] { new float[n], new float[n] };
            for (int c = 0; c < 2; c++)
            {
                Array.Copy(x[c], o[c], n);
                for (int i = 0; i < m; i++)
                {
                    double w = i / (double)m;
                    o[c][i] = (float)(x[c][i] * w + x[c][n + i] * (1 - w));
                }
            }
            return o;
        }
    }
}
