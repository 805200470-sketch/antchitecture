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
    /// points walk, step and touch antennae with them. The ant is made of its points: lit flakes of different sizes
    /// and colours, as many and as large as cover its surface, over its own mesh drawn dark (nothing behind shows
    /// through; between the flakes only dark, never a smooth body). Far ants get fewer points.
    /// Colours (11-04): an NPC ant takes its nest's, the player's ant has a quiet blue grey body in every nest (see baseChroma).
    /// </summary>
    [DefaultExecutionOrder(1000)]   // after the ants have moved and been posed
    public class ParticleAnts : MonoBehaviour
    {
        public float size = 0.003f;     // a point's radius (units) for an ant whose points file has no area (AGA2)
        public float cover = 0.7f;      // otherwise: its radius, of the spacing of its points (0.62 just covers the surface)
        // (1.15 made every nest's ant nearly white, the brightest thing in view with the bloom round it: the user,
        // 10-27, "蚂蚁亮度太亮了". The grading squeezes its highlights, so halving it moved its flakes' median luma only
        // 0.85 → 0.78 in the honey nest, 0.81 → 0.68 in the leafcut garden)
        public float gain = 0.4f;
        // and of the stops a nest's night is lifted above v15's, how many its ants do not take (NestTheme.LightComp): at
        // 0.4 alone they were still white in the nests lifted two stops (brightest tenth 0.70-0.79 against v15's 0.32;
        // the user, 10-29, "有些蚂蚁都太发光、太白了…有点过曝了")
        public float exposureComp = 0.8f;
        public float pale = 0.06f;      // how far colours go from the ant's own toward a pale glow
        public float contrast = 1.2f;   // how much the texture's light and dark are exaggerated
        public Color bodyColour = new Color(0.02f, 0.05f, 0.06f);   // the ant's own mesh, dark, under its points
        public float bodyTexture = 0f;  // and its own texture on it (0.5 showed a smooth model between the points)
        public float fullUntil = 2.5f;  // all its points this close to the camera (the player's ant, one beside it)
        public float fewBeyond = 7f;    // a third of them from here on (with 160000 points each, more left frames running late)
        // colours (11-04). The user's taste for every nest's living things: they lean to its own hue, their bodies dark and
        // quiet in it, nothing off its palette. So an NPC ant's own colours are folded into its nest's own ant colour (Local;
        // a nest without one, the hue of its floor), each kind keeping speciesSpread of how its hue differs from the others'
        // and hueKeep of its own colours' spread, made quiet but clearly local (baseChroma: how strong its colour is, of its
        // lightness), dark (bodyGain), its shell's pits a little more vivid (npcPitSat); it does not glow. The player's ant
        // has one quiet blue grey body in every nest (playerBody), and only its markings, its texture's own light lines and
        // dots (the rims of its gaster's segments, rings at its joints: Tools/ants/ant_marks.py), and faintly its pits,
        // glow, in the nest's light (markGain, pitGlow): the one ant that glows, found anywhere, and still of the nest it
        // is in (the user, 11-04, over five rounds of stills). Before, every ant, the player's too, was only leaned toward
        // its nest's antTint, lit as before: a muddy khaki in the granary, its own teal in the moss forest.
        // (first round: turned toward antTint, the moss forest's ants came out a blue grey in its greens; turned whole,
        // Ant_001's two colours, teal and yellow green, an olive in the kiln and the granary; at body 0.55 and markings 2.4
        // a little light for "dark", by the granary's three trials)
        // (11-04, the user, on the second round: "其他房间的这些蚂蚁不要是灰色的，你可以带有它本地的…这个场景的一些特色": at 0.22
        // the NPCs read grey. Now each nest's own ant colour, Local, at baseChroma 0.45, a little lighter, and the kinds
        // closer to it; and the colour in its shell's pits too, not only in its bands: pitGlow, "不是说只要一圈就行了")
        public float baseChroma = 0.45f;
        public float bodyGain = 0.5f;
        // (and its markings a quiet glow, not a shine: the user, "蚂蚁身上的根据环境发光不要太亮", on bright green stripes on
        // the player's ant in the moss forest at 2.7, round 4; at 1.6, under the bloom's threshold everywhere)
        public float markGain = 1.6f;
        public float speciesSpread = 0.3f;
        public float hueKeep = 0.3f;
        public float pitGlow = 0.45f;   // how brightly its pits glow, of its markings (at most: they are strongest beside one;
                                        // at 0.35 they did not show at markings 2.7, round 3; fainter than the markings)
        // (11-04, the user's decision: "别的蚂蚁根据环境颜色上颜色，不要花纹发光，只保留玩家的蚂蚁有这个特征". Only the player's ant
        // glows, its markings and faintly its pits, in the nest's light: the one ant that glows, found anywhere. An NPC
        // has no glowing markings at all; its colour is its nest's, in its body and, a little more vivid, in its pits)
        public float npcPitSat = 0.8f;   // how much more vivid an NPC's pits are than the rest of its shell (of its own strength)
        public float minChroma = 0.5f;   // and none of its shell less vivid than this share of its strength (baseChroma's, or the
                                         // player's playerChroma): its near white streaks stayed grey, Ant_002's most, round 5
        public float minMarkSat = 0.45f;   // its markings' light at least this vivid (the moss forest's near white read as white)
        // (and never blown out: "NPC的蚂蚁也不要太白…之前那个版本会有很过曝的这种现象". No flake of an ant is brighter than this,
        // in the scene's light before the grading, in a nest lit as v15 (less in a nest lifted more: NestTheme.LightComp):
        // below the bloom's threshold, 0.85, so no ant adds to the bloom; 0.95 in round 4, a little glow round the markings)
        public float maxLight = 0.75f;
        // the player's ant (11-04): it kept its own body, its texture's teal, in every nest, and the user: "可以把整个蚂蚁的
        // 不用这么蓝，放在别的场景太突兀", then "还是可以有一点蓝的，但不要这么蓝，饱和度不用这么高" (a little blue, far less
        // vivid, so it no longer jumps out of the warm nests). Now a dark, quiet blue grey, the same in every nest
        // (playerBody's hue, playerChroma: a third of its texture's teal, playerGain), its markings in the nest's light
        public Color playerBody = new Color(0.30f, 0.36f, 0.42f);
        public float playerChroma = 0.14f;
        public float playerGain = 0.62f;   // (0.55 in round 3: a near black slate, not a blue grey)

        // (look, 11-04: how much it is one of the ant's markings, its low byte, and how much one of its pits, the next)
        struct AntPoint { public Vector3 pos; public Vector3 nrm; public uint col; public uint slots; public float w; public uint look; }

        class Kind
        {
            public ComputeBuffer points;
            public int count;
            public float area;          // its surface in bind space (0: not known)
            public string[] paths;
            public Matrix4x4[] poses;
            public float hueAngle, chroma;   // its shell's own colour: its hue (degrees round the shader's YIQ), how strong (of its lightness)
            public bool marked;              // its markings found (ant_<id>_marks)
        }

        class Shown
        {
            public AntController ant;
            public Kind kind;
            public Transform[] bones;
            public int skinBase;
            public MaterialPropertyBlock block;
            public bool own;                 // the player's ant
            public float hue, sat, body, keep, minC;   // its colours turned, made quiet, darkened, their spread kept, the least vivid (Colour)
            public Vector4 mark;             // its markings' light (linear) and how bright (w)
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
            NestTheme theme = NestTheme.Current ?? NestTheme.Cyan;
            // (11-04) the dark under the points, the rim light and the pale the ants lean to, all from the nest's own colours:
            // the dark in its floor's hue (the moss forest's antBody was a blue green), the rim and the pale from its markings'
            // light (no ant's rim or wing of another nest's colour). The dark's copies are made again for each nest too
            // (AntController.BodyFor: the first nest's dark teal stayed under the ants of every nest after it)
            if (NestTheme.Current != null) bodyColour = Dark(theme.floor, theme.antBody);
            minMarkSatNow = minMarkSat;
            Color light = Mark(theme, 0);
            Shader.SetGlobalColor("_AntTint", theme.antTint);
            Shader.SetGlobalColor("_AntRimColor", Color.Lerp(light, Color.white, 0.35f));
            Shader.SetGlobalColor("_AntPaleColor", Color.Lerp(light, Color.white, 0.6f));
            if (refs.antOccluder != null)
            {
                refs.antOccluder.SetColor("_Color", bodyColour);
                refs.antOccluder.SetFloat("_TexLight", bodyTexture);
            }
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
                ants.Add(new Shown { ant = a, kind = k, bones = bones, skinBase = slots, block = new MaterialPropertyBlock(), own = a == player });
                slots += bones.Length;
            }
            if (slots == 0) return;
            skin = new ComputeBuffer(slots, 64);
            skinData = new Matrix4x4[slots];
            Colour(theme);
            Debug.Log($"[AntGarden] particle ants: {ants.Count} ants of {kinds.Count} kinds, {slots} bones");
        }

        /// <summary>Each ant's colours in this nest (11-04): see baseChroma. Trials without a build: -antchroma, -antbody,
        /// -antmark, -antspread, -antkeep, -antpits, -antmaxlight, -antmarksat, -playerchroma, -playergain, -antbase.</summary>
        void Colour(NestTheme theme)
        {
            float Tried(string arg, float v) => Game.Arg(arg) is string a && float.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) ? x : v;
            baseChroma = Tried("-antchroma", baseChroma);
            bodyGain = Tried("-antbody", bodyGain);
            markGain = Tried("-antmark", markGain);
            speciesSpread = Tried("-antspread", speciesSpread);
            hueKeep = Tried("-antkeep", hueKeep);
            playerChroma = Tried("-playerchroma", playerChroma);
            playerGain = Tried("-playergain", playerGain);
            pitGlow = Tried("-antpits", pitGlow);
            npcPitSat = Tried("-npcpits", npcPitSat);
            minChroma = Tried("-antminchroma", minChroma);
            maxLight = Tried("-antmaxlight", maxLight);
            minMarkSatNow = minMarkSat = Tried("-antmarksat", minMarkSat);
            // the kinds' middle hue, round which each keeps its own difference
            float cx = 0, cy = 0;
            foreach (Kind k in kinds.Values)
                if (k != null) { cx += Mathf.Cos(k.hueAngle * Mathf.Deg2Rad); cy += Mathf.Sin(k.hueAngle * Mathf.Deg2Rad); }
            float middle = Mathf.Atan2(cy, cx) * Mathf.Rad2Deg;
            // the hue they lean to: the nest's own ant colour (Local), or where it has none its floor's (-antbase tint, body,
            // fog, ground or floor: the others, for trials)
            string from = Game.Arg("-antbase") ?? (Local(theme.id).a > 0f ? "local" : "floor");
            Color baseColour = from == "local" ? Local(theme.id) : from == "tint" ? theme.antTint : from == "body" ? theme.antBody : from == "fog" ? theme.fog : from == "ground" ? theme.ground : theme.floor;
            // (a colour too grey to have a hue of its own leaves it to the ants' tint, or their own middle)
            float nest = from == "tint" && theme.antTint.a <= 0f ? middle : Angle(baseColour.linear);
            if (Chroma(baseColour.linear) < 0.04f) nest = theme.antTint.a > 0f ? Angle(theme.antTint.linear) : middle;
            Debug.Log($"[AntGarden] particle ants' hue in {theme.id} ({from}): {nest:F0}; floor {Angle(theme.floor.linear):F0}, ground {Angle(theme.ground.linear):F0}, fog {Angle(theme.fog.linear):F0}, antBody {Angle(theme.antBody.linear):F0}, antTint {(theme.antTint.a > 0f ? Angle(theme.antTint.linear).ToString("F0") : "none")}, kinds' middle {middle:F0}");
            var log = new System.Text.StringBuilder();
            foreach (Shown s in ants)
            {
                Color m = Mark(theme, s.own ? 0 : LightOf(s.ant.Ref.id));
                s.mark = new Vector4(m.linear.r, m.linear.g, m.linear.b, s.own ? markGain : 0f);   // (an NPC's: no glow; its light still tints its wings)
                if (s.own)
                {
                    s.hue = Mathf.DeltaAngle(s.kind.hueAngle, Angle(playerBody.linear));
                    s.sat = Mathf.Clamp(playerChroma / Mathf.Max(0.02f, s.kind.chroma), 0.05f, 1.5f);
                    s.minC = playerChroma * minChroma;
                    s.body = playerGain;
                    s.keep = hueKeep;
                }
                else
                {
                    s.hue = Mathf.DeltaAngle(s.kind.hueAngle, nest + Mathf.DeltaAngle(middle, s.kind.hueAngle) * speciesSpread);
                    s.sat = Mathf.Clamp(baseChroma * (Local(theme.id).a > 0f ? Local(theme.id).a : 1f) / Mathf.Max(0.02f, s.kind.chroma), 0.05f, 1.5f);
                    s.minC = baseChroma * (Local(theme.id).a > 0f ? Local(theme.id).a : 1f) * minChroma;
                    s.body = bodyGain;
                    s.keep = hueKeep;
                }
                log.Append($" {s.ant.Ref.id}{(s.own ? "(player)" : "")} turn {s.hue:F0} sat {s.sat:F2} {(s.own ? "glowing markings #" + ColorUtility.ToHtmlStringRGB(m) : "no glow")}{(s.kind.marked ? "" : " (no markings)")};");
            }
            Debug.Log($"[AntGarden] particle ants' colours in {theme.id}: hue {nest:F0}, chroma {baseChroma:F2}, body {bodyGain:F2}, spread kept {hueKeep:F2}, the player's markings x{markGain:F2}, NPC pits +{npcPitSat:F2}:{log}");
        }

        /// <summary>A nest's own ant colour (11-04, the user's: each nest's ants carry its character, quiet but clearly its own,
        /// not grey): amber browns, the kiln's ember red, the granary's honey gold, the garden's leaf yellow green, moss green,
        /// the blue forest's soft blue. Its hue counts, and its a: how much of baseChroma (greens and blues read far more vivid
        /// at the same strength: round 3's garden ant a grass green, the blue forest's a royal blue, the kiln's a quiet red
        /// brown; and the granary's gold turned 20° from the amber nest's brown, round 3 two of a kind). Clear: none.</summary>
        static Color Local(string id) => id == "orange" ? new Color(0.70f, 0.40f, 0.20f, 1f) : id == "red" ? new Color(0.70f, 0.26f, 0.18f, 1f)
            : id == "yellow" ? new Color(0.84f, 0.68f, 0.24f, 0.95f) : id == "chartreuse" ? new Color(0.56f, 0.72f, 0.24f, 0.6f)
            : id == "green" ? new Color(0.32f, 0.56f, 0.28f, 0.68f) : id == "cyan" ? new Color(0.42f, 0.60f, 0.80f, 0.72f) : Color.clear;

        /// <summary>Which of its nest's own lights (NestTheme.glow 0, 1, 2, 6) a kind's markings glow in.</summary>
        static int LightOf(string id) => id == "Ant_002" || id == "Ant_005" ? 1 : id == "Ant_004" ? 2 : id == "Ant_003" ? 6 : 0;

        static float minMarkSatNow = 0.45f;   // (minMarkSat, for the static Mark)

        /// <summary>One of the nest's own lights as a marking's: toward its deep glow, as its plants' markings are
        /// (ParticleLook), or its markings' own colour where it has one (a little turned for the other kinds), at least
        /// minMarkSat vivid, as bright as it can be in its hue (no channel above 1).</summary>
        static Color Mark(NestTheme t, int light)
        {
            Color c;
            if (t.markColor.a > 0f)
            {
                c = t.markColor;
                Color.RGBToHSV(c, out float h, out float sv, out float v);
                if (light == 1) c = Color.HSVToRGB(Mathf.Repeat(h + 8f / 360f, 1f), sv, v);
                else if (light == 2) c = Color.HSVToRGB(Mathf.Repeat(h - 8f / 360f, 1f), sv, v);
                else if (light == 6) c = Color.Lerp(c, Color.white, 0.3f);
            }
            else c = Color.Lerp(t.glow != null && t.glow.Length > light ? t.glow[light] : t.antRim, t.glowDeep, t.markDeep);
            // (at least minMarkSat vivid: its nest's colour, not a white)
            Color.RGBToHSV(c, out float hh, out float ss, out float vv);
            if (ss < minMarkSatNow) c = Color.HSVToRGB(hh, minMarkSatNow, vv);
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return mx > 1e-3f ? new Color(c.r / mx, c.g / mx, c.b / mx, 1f) : Color.white;
        }

        /// <summary>The dark under an ant's points in this nest: its floor's hue, quiet, nearly black (as dark as its own antBody,
        /// within 0.04-0.08 of white).</summary>
        static Color Dark(Color floor, Color own)
        {
            Color.RGBToHSV(floor, out float h, out float s, out _);
            Color.RGBToHSV(own, out _, out _, out float v);
            return Color.HSVToRGB(h, Mathf.Min(s, 0.5f), Mathf.Clamp(v, 0.04f, 0.08f));
        }

        /// <summary>How strong a colour is, of its lightness (YIQ).</summary>
        static float Chroma(Color lin)
        {
            float y = 0.299f * lin.r + 0.587f * lin.g + 0.114f * lin.b;
            float i = 0.596f * lin.r - 0.274f * lin.g - 0.322f * lin.b, q = 0.211f * lin.r - 0.523f * lin.g + 0.312f * lin.b;
            return Mathf.Sqrt(i * i + q * q) / Mathf.Max(1e-4f, y);
        }

        /// <summary>A colour's hue as the shader turns it (AG_HueShift: degrees round the YIQ plane).</summary>
        static float Angle(Color lin)
        {
            float i = 0.596f * lin.r - 0.274f * lin.g - 0.322f * lin.b, q = 0.211f * lin.r - 0.523f * lin.g + 0.312f * lin.b;
            return Mathf.Atan2(q, i) * Mathf.Rad2Deg;
        }

        Kind Load(string id)
        {
            if (kinds.TryGetValue(id, out Kind k)) return k;
            kinds[id] = null;
            var asset = Resources.Load<TextAsset>("AntGarden/Points/ant_" + id);
            if (asset == null) return null;
            using (var r = new BinaryReader(new MemoryStream(asset.bytes)))
            {
                int magic = r.ReadInt32();
                if (magic != 0x32414741 && magic != 0x33414741) return null;   // "AGA2", "AGA3" (with its area)
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
                float area = magic == 0x33414741 ? r.ReadSingle() : 0f;
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
                k = new Kind { count = n, area = area, paths = paths, poses = poses, points = new ComputeBuffer(n, 40) };
                // its shell's own colour (linear, as the shader turns it): its hue and how strong, of its lightness
                double si = 0, sq = 0, sc = 0, sy = 0;
                for (int i = 0; i < n; i++)
                {
                    if (((pts[i].col >> 24) & 255) != 0) continue;
                    float rr = Mathf.GammaToLinearSpace((pts[i].col & 255) / 255f), gg = Mathf.GammaToLinearSpace(((pts[i].col >> 8) & 255) / 255f), bb = Mathf.GammaToLinearSpace(((pts[i].col >> 16) & 255) / 255f);
                    float ii = 0.596f * rr - 0.274f * gg - 0.322f * bb, qq = 0.211f * rr - 0.523f * gg + 0.312f * bb;
                    si += ii; sq += qq; sc += Mathf.Sqrt(ii * ii + qq * qq); sy += 0.299f * rr + 0.587f * gg + 0.114f * bb;
                }
                k.hueAngle = Mathf.Atan2((float)sq, (float)si) * Mathf.Rad2Deg;
                k.chroma = sy > 0 ? (float)(sc / sy) : 0f;
                k.marked = LoadMarks(id, asset.bytes, pts);
                k.points.SetData(pts);
            }
            kinds[id] = k;
            return k;
        }

        /// <summary>
        /// The ant's markings and pits (Tools/ants/ant_marks.py: Points/ant_&lt;id&gt;_marks.bytes, "AGM2", n, the points
        /// file's mark, then two bytes a point, how much it is a marking and how much a pit), into each point's look, where
        /// the shader reads them. Made for other points, they are left out (make them again after unity_build.sh ants).
        /// </summary>
        static bool LoadMarks(string id, byte[] pointsFile, AntPoint[] pts)
        {
            var asset = Resources.Load<TextAsset>("AntGarden/Points/ant_" + id + "_marks");
            if (asset == null) { Debug.LogWarning($"[AntGarden] {id}: no markings (Tools/ants/ant_marks.py)"); return false; }
            uint mark = 2166136261u;
            for (int i = 0; i < pointsFile.Length; i += 997) mark = (mark ^ pointsFile[i]) * 16777619u;
            byte[] b = asset.bytes;
            if (b.Length < 12 + 2 * pts.Length || System.BitConverter.ToInt32(b, 0) != 0x324D4741 || System.BitConverter.ToInt32(b, 4) != pts.Length || System.BitConverter.ToUInt32(b, 8) != mark)
            {
                Debug.LogWarning($"[AntGarden] {id}: its markings were made for other points, left out (make them again: Tools/ants/ant_marks.py)");
                return false;
            }
            for (int i = 0; i < pts.Length; i++) pts[i].look = b[12 + 2 * i] | ((uint)b[13 + 2 * i] << 8);
            return true;
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
                // its colours in this nest (Colour): an NPC's folded into the nest's hue, quiet and dark, its pits a little more
                // vivid, no glow; the player's ant's into its quiet blue grey, its markings, and faintly its pits, glowing in
                // the nest's light; no flake above maxLight
                s.block.SetFloat("_Hue", s.hue);
                s.block.SetFloat("_Sat", s.sat);
                s.block.SetFloat("_MeanHue", s.kind.hueAngle);
                s.block.SetFloat("_Keep", s.keep);
                s.block.SetFloat("_BodyGain", s.body);
                s.block.SetVector("_MarkColor", s.mark);
                s.block.SetFloat("_PitGlow", s.own ? pitGlow : 0f);
                s.block.SetFloat("_PitSat", s.own ? 0f : npcPitSat);
                s.block.SetFloat("_MinChroma", s.minC);
                s.block.SetFloat("_MaxLight", maxLight * NestTheme.LightComp(1f));
                // just large enough to cover the ant (its points' spacing in bind space, scaled as its skin scales it)
                float bindToWorld = ((Vector3)skinData[s.skinBase].GetColumn(0)).magnitude;
                float r = s.kind.area > 0 ? cover * Mathf.Sqrt(s.kind.area / s.kind.count) * bindToWorld : size;
                s.block.SetFloat("_Size", r);
                s.block.SetFloat("_Gain", gain * NestTheme.LightComp(exposureComp));
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
