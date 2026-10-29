using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The particle nests' colours from one recipe: the blue fungal forest's (v15, NestTheme.Cyan, the palette the user
    /// approved), turned to another segment of the colour wheel. v15's colours fall into four layers:
    ///   1 base: the floor, walls, sky and far distance. Most of the picture, dark and low in chroma, but leaning clearly
    ///     to the world's hue (it tells the segment at a glance).
    ///   2 middle: the plants, rocks and fungi. A family of hues from the world's toward its neighbours (v15: cyan, teal,
    ///     teal-green to sage and moss), middling chroma, light and dark, some near grey-white.
    ///   3 highlights: the threads, beams, pools, glows and motes. Near white, with a little of the world's hue.
    ///   4 accents: a few colours far from the world's (v15: coral, straw, violet). Small in area.
    /// Each of v15's colours is turned in OKLCH (perceptually even: a colour keeps its lightness, and its chroma means
    /// about the same vividness at any hue) from cyan's hue to the segment's. Its lightness is kept; its chroma is
    /// corrected for how vivid the new hue looks against cyan (yellow, green and magenta glare at a chroma blue is calm
    /// at). The middle family spreads toward foliage hues, as v15's does (from cyan toward moss; from yellow toward
    /// olive, never toward pink). Accents turn with the rest, so they stay opposite the world's hue. Segment 6 (cyan,
    /// 180°) gives v15's colours back. Every knob is in Recipe, for tuning world by world.
    /// </summary>
    public static class NestPalette
    {
        public sealed class Recipe
        {
            public float hueBlend = 0.75f;          // a segment's hue: 0 its light's own (the wheel's colours crowd: yellow to spring green in 40°, azure and blue 8° apart), 1 evenly 30° apart
            public float hueNudge;                  // and turned this many degrees more
            public float spread = 1f;               // how far the colours spread from the world's hue (1: v15's: its plants 80° from cyan toward sage and moss)
            public float towardHue = 80f;           // they spread toward this OKLCH hue (gold-olive, dry grass and old leaves), as v15's from cyan
            public float spreadSide;                // or toward this side: +1 higher hues, -1 lower (0: toward towardHue)
            public float baseChroma = 1f, midChroma = 1f, highChroma = 1f, accentChroma = 1f;   // each layer's chroma, of v15's after the hue correction
            public float groundLift = 0.05f;        // lightness added to the floor and walls in a warm world (yellow to green; v15's ground is darker than a warm world's should be)
            public float groundGain = 0.25f;        // and the ground's points brighter by up to this share
            public float accentShare = 1f;          // how often a plant, thing or glow takes an accent colour, of v15's
            public float jitter = 1f;               // how far a plant's own tint wanders from its palette colour (hue, chroma, lightness), of v15's
            public float warmth = 4f;               // the most the white balance may lean warm (v15 leans 8 cool)
            public float exposure;                  // stops added to the nest's night
            public float skyGain = 1f;              // its sky this much lighter than the other nests' (Game: a deep night in its colour)

            /// <summary>A copy, with "knob=value,knob=value" (the fields above, by name) set on it: trying a nest's colours
            /// without a build (-recipe).</summary>
            public Recipe With(string overrides)
            {
                var r = (Recipe)MemberwiseClone();
                if (string.IsNullOrEmpty(overrides)) return r;
                foreach (string pair in overrides.Split(','))
                {
                    string[] kv = pair.Split('=');
                    var f = kv.Length == 2 ? typeof(Recipe).GetField(kv[0].Trim()) : null;
                    if (f != null && f.FieldType == typeof(float))
                        f.SetValue(r, float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
                    else Debug.LogWarning($"[AntGarden] -recipe: no knob '{pair}'");
                }
                return r;
            }
            // how vivid a hue looks against cyan at the same OKLCH chroma (OKLCH hue, factor): a colour turned to a hue
            // that looks more vivid keeps less chroma (never more than it had: a sage turned to cyan would turn neon)
            public Vector2[] vividness =
            {
                new Vector2(25, 1.05f), new Vector2(55, 1.1f), new Vector2(95, 1.3f), new Vector2(112, 1.3f), new Vector2(135, 1.2f),
                new Vector2(150, 1.15f), new Vector2(170, 1.05f), new Vector2(195, 1f), new Vector2(260, 1f), new Vector2(295, 1.05f),
                new Vector2(330, 1.2f),
            };
        }

        /// <summary>One colour of a palette: what it colours (a NestTheme field, or an array's element: body3, glow0,
        /// micro5), its layer (1 base, 2 middle, 3 highlight, 4 accent) and its colour (sRGB; above 1 for a light).</summary>
        public struct Swatch { public string role; public int layer; public Color colour; }

        public sealed class Palette
        {
            public int segment;
            public float hue;                       // the world's OKLCH hue
            public float warm;                      // how warm a world it is (0..1: its ground lighter)
            public readonly List<Swatch> swatches = new List<Swatch>();
            public float temperature, saturation, exposure, groundGain, accentShare, jitter, skyGain = 1f;
        }

        // the terrain textures' tints in v15 (the blue biome's), turned with the rest
        static readonly Color V15FloorTex = new Color(0.95f, 1.15f, 1.2f), V15BankTex = new Color(0.95f, 1.25f, 1.17f), V15PlateauTex = new Color(0.42f, 0.54f, 0.57f);
        // and how v15's ants look: their own teal (no tint: it is the world's); elsewhere they lean to this, turned
        static readonly Color V15AntLook = new Color(0.46f, 0.70f, 0.72f);
        // and its floating spores' colours (Atmosphere's: a pale cyan, a pale warm)
        static readonly Color V15SporeA = new Color(0.75f, 0.95f, 1f), V15SporeB = new Color(1f, 0.9f, 0.65f);

        /// <summary>Every role a palette fills, base layer first.</summary>
        public static IEnumerable<string> Roles { get { foreach (string s in Base) yield return s; foreach (string s in Middle) yield return s; foreach (string s in High) yield return s; } }

        static readonly string[] Base = { "ground", "floor", "bank", "plateau", "edge", "fog", "skyTop", "skyHorizon", "fill", "rim", "antBody", "vineDark", "floorTex", "bankTex", "plateauTex" };
        static readonly string[] Middle = { "body0", "body1", "body2", "body3", "body4", "body5", "body6", "body7", "scan", "moon", "glint", "dew", "vineLeaf", "antPale",
            "micro0", "micro1", "micro2", "micro3", "micro4", "micro5", "micro6", "micro7", "antLook" };
        static readonly string[] High = { "glow0", "glow1", "glow2", "glow3", "glow4", "glow5", "glow6", "glow7", "threadCore", "threadHalo", "beacon", "grainGlow", "lampCore",
            "ring", "trail", "motes", "glowDeep", "antRim", "bloomTint", "sporeA", "sporeB" };

        /// <summary>A segment's hue in OKLCH: between its light's own hue and 30° steps from cyan's, turned by the
        /// recipe's nudge (cyan's own, v15's, is never nudged).</summary>
        public static float SegmentHue(int segment, Recipe r)
        {
            float cyan = WheelHue(180f), even = cyan + (segment - 6) * 30f;
            float own = even + Wrap(WheelHue(segment * 30f) - even);
            return Mod(Mathf.Lerp(own, even, r.hueBlend) + (segment == 6 ? 0f : r.hueNudge), 360f);
        }

        /// <summary>The four-layer palette for a segment of the wheel (0..11; 6 is cyan, v15's).</summary>
        public static Palette For(int segment, Recipe r = null)
        {
            r ??= new Recipe();
            var p = new Palette { segment = segment };
            float hc = WheelHue(180f), ht = SegmentHue(segment, r);
            p.hue = ht;
            p.warm = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 80f, Mathf.Abs(Wrap(ht - 115f))));
            // the side the colours spread to: toward towardHue from wherever the world is (v15 spreads from cyan toward
            // lower hues: sage and moss)
            float sideT = r.spreadSide != 0 ? Mathf.Sign(r.spreadSide) : Mathf.Sign(Wrap(r.towardHue - ht));
            float flip = sideT * Mathf.Sign(Wrap(r.towardHue - hc));
            NestTheme v15 = NestTheme.Cyan;
            void Add(string role, int layer)
            {
                Color c = Get(v15, role);
                float scale = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                Vector3 lch = Oklch(c / scale);
                float off = Wrap(lch.z - hc);
                bool accent = layer != 1 && Mathf.Abs(off) > 75f && lch.y > 0.025f;
                float h = accent ? lch.z + (ht - hc) : ht + off * r.spread * flip;
                float chroma = lch.y * Mathf.Min(1f, Vividness(lch.z, r) / Vividness(h, r))
                    * (accent ? r.accentChroma : layer == 1 ? r.baseChroma : layer == 2 ? r.midChroma : r.highChroma);
                float lift = layer == 1 && role != "fog" && role != "skyTop" && role != "skyHorizon" && role != "fill" && role != "rim" && role != "antBody" && role != "vineDark"
                    ? r.groundLift * p.warm : 0f;
                Color o = FromOklch(new Vector3(Mathf.Clamp01(lch.x + lift), chroma, Mod(h, 360f))) * scale;
                o.a = 1;
                p.swatches.Add(new Swatch { role = role, layer = accent ? 4 : layer, colour = o });
            }
            foreach (string s in Base) Add(s, 1);
            foreach (string s in Middle) Add(s, 2);
            foreach (string s in High) Add(s, 3);
            // the white balance leans the way v15's does, toward the world's hue (along blue..yellow), warm at most warmth
            float cool = Mathf.Cos((hc - 250f) * Mathf.Deg2Rad);
            p.temperature = Mathf.Clamp(-8f * Mathf.Cos((ht - 250f) * Mathf.Deg2Rad) / cool, -8f, r.warmth);
            p.saturation = 6f;
            p.exposure = r.exposure;
            p.skyGain = r.skyGain;
            p.groundGain = 1f + r.groundGain * p.warm;
            p.accentShare = r.accentShare;
            p.jitter = r.jitter;
            return p;
        }

        /// <summary>A theme's colours from a palette, and v15's way of using them (how much of their own colour plants,
        /// things and the ground keep, how bright what is near the ant is, how often accents come). Its content (plants,
        /// things, heart) stays its own.</summary>
        public static NestTheme Apply(NestTheme t, Palette p)
        {
            NestTheme v15 = NestTheme.Cyan;
            t.body = new Color[8];
            t.glow = new Color[8];
            t.microLife = new Color[8];
            t.segment = p.segment;
            foreach (Swatch s in p.swatches) Set(t, s.role, s.colour);
            t.temperature = p.temperature;
            t.saturation = p.saturation;
            t.exposureLift = p.exposure;
            t.skyGain = p.skyGain;
            t.groundGain = p.groundGain;
            t.bodyAccent = v15.bodyAccent * p.accentShare;
            t.glowAccent = v15.glowAccent * p.accentShare;
            t.tintJitter = p.jitter;
            t.vineGlow = v15.vineGlow;
            t.vineDarken = v15.vineDarken;
            t.farDim = v15.farDim;
            t.farFog = p.segment == 6 ? 0f : 0.7f;   // (v15's are its own blue; turned elsewhere, their own colours stay v15's)
            t.gateGain = v15.gateGain;
            t.nearLift = v15.nearLift;
            t.walkGain = v15.walkGain;
            t.thingKeep = v15.thingKeep;
            t.plantKeep = v15.plantKeep;
            t.groundTexMix = v15.groundTexMix;
            t.floorVary = v15.floorVary;
            t.floorPatchA = t.floorPatchB = Color.clear;
            t.noKinds = new string[0];
            return t;
        }

        /// <summary>A theme's colour for a palette role (v15's tints for the terrain textures).</summary>
        public static Color Get(NestTheme t, string role)
        {
            if (role.StartsWith("body")) return t.body[role[4] - '0'];
            if (role.StartsWith("glow") && role.Length == 5) return t.glow[role[4] - '0'];
            if (role.StartsWith("micro")) return t.microLife[role[5] - '0'];
            switch (role)
            {
                case "ground": return t.ground;
                case "floor": return t.floor;
                case "bank": return t.bank;
                case "plateau": return t.plateau;
                case "edge": return t.edge;
                case "fog": return t.fog;
                case "skyTop": return t.skyTop;
                case "skyHorizon": return t.skyHorizon;
                case "fill": return t.fill;
                case "rim": return t.rim;
                case "antBody": return t.antBody;
                case "vineDark": return t.vineDark;
                case "floorTex": return V15FloorTex;
                case "bankTex": return V15BankTex;
                case "plateauTex": return V15PlateauTex;
                case "scan": return t.scan;
                case "moon": return t.moon;
                case "glint": return t.glint;
                case "dew": return t.dew;
                case "vineLeaf": return t.vineLeaf;
                case "antPale": return t.antPale;
                case "antLook": return V15AntLook;
                case "sporeA": return V15SporeA;
                case "sporeB": return V15SporeB;
                case "threadCore": return t.threadCore;
                case "threadHalo": return t.threadHalo;
                case "beacon": return t.beacon;
                case "grainGlow": return t.grainGlow;
                case "lampCore": return t.lampCore;
                case "ring": return t.ring;
                case "trail": return t.trail;
                case "motes": return t.motes;
                case "glowDeep": return t.glowDeep;
                case "antRim": return t.antRim;
                case "bloomTint": return t.bloomTint;
            }
            throw new System.ArgumentException("no palette role " + role);
        }

        static void Set(NestTheme t, string role, Color c)
        {
            if (role.StartsWith("body")) { t.body[role[4] - '0'] = c; return; }
            if (role.StartsWith("glow") && role.Length == 5) { t.glow[role[4] - '0'] = c; return; }
            if (role.StartsWith("micro")) { t.microLife[role[5] - '0'] = c; return; }
            switch (role)
            {
                case "ground": t.ground = c; break;
                case "floor": t.floor = c; break;
                case "bank": t.bank = c; break;
                case "plateau": t.plateau = c; break;
                case "edge": t.edge = c; break;
                case "fog": t.fog = c; break;
                case "skyTop": t.skyTop = c; break;
                case "skyHorizon": t.skyHorizon = c; break;
                case "fill": t.fill = c; break;
                case "rim": t.rim = c; break;
                case "antBody": t.antBody = c; break;
                case "vineDark": t.vineDark = c; break;
                case "floorTex": t.floorTexTint = c; break;
                case "bankTex": t.bankTexTint = c; break;
                case "plateauTex": t.plateauTexTint = c; break;
                case "scan": t.scan = c; break;
                case "moon": t.moon = c; break;
                case "glint": t.glint = c; break;
                case "dew": t.dew = c; break;
                case "vineLeaf": t.vineLeaf = c; break;
                case "antPale": t.antPale = c; break;
                // (v15's ants keep their own teal, the world's own colour; elsewhere they lean to it turned)
                case "antLook": t.antTint = t.segment == 6 ? Color.clear : new Color(c.r, c.g, c.b, 0.6f); break;
                case "sporeA": t.sporeA = c; break;
                case "sporeB": t.sporeB = c; break;
                case "threadCore": t.threadCore = c; break;
                case "threadHalo": t.threadHalo = c; break;
                case "beacon": t.beacon = c; break;
                case "grainGlow": t.grainGlow = c; break;
                case "lampCore": t.lampCore = c; break;
                case "ring": t.ring = c; break;
                case "trail": t.trail = c; break;
                case "motes": t.motes = c; break;
                case "glowDeep": t.glowDeep = c; break;
                case "antRim": t.antRim = c; break;
                case "bloomTint": t.bloomTint = c; break;
                default: throw new System.ArgumentException("no palette role " + role);
            }
        }

        /// <summary>How vivid a hue looks against cyan (Recipe.vividness, sorted by hue; between two, smoothly; round the
        /// circle from the last to the first).</summary>
        static float Vividness(float hue, Recipe r)
        {
            Vector2[] v = r.vividness;
            for (int i = 0; i < v.Length; i++)
            {
                Vector2 a = v[i], b = v[(i + 1) % v.Length];
                float span = Mod(b.x - a.x, 360f), t = Mod(hue - a.x, 360f);
                if (t <= span) return Mathf.Lerp(a.y, b.y, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(1e-3f, span)));
            }
            return 1f;
        }

        // ---------------------------------------------------------------- OKLCH (Björn Ottosson's OKLab, polar)

        /// <summary>The OKLCH hue of the colour wheel's colour at this angle (full saturation and value).</summary>
        public static float WheelHue(float deg) => Oklch(Color.HSVToRGB(Mod(deg, 360f) / 360f, 1f, 1f)).z;

        static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float Gam(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        static float Cbrt(float x) => x < 0 ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);
        static float Wrap(float d) => Mod(d + 180f, 360f) - 180f;
        static float Mod(float x, float m) => x - m * Mathf.Floor(x / m);

        /// <summary>An sRGB colour as OKLCH: lightness 0..1, chroma, hue in degrees.</summary>
        public static Vector3 Oklch(Color c)
        {
            float r = Lin(c.r), g = Lin(c.g), b = Lin(c.b);
            float l = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
            float m = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
            float s = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);
            float L = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
            float A = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
            float B = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;
            return new Vector3(L, Mathf.Sqrt(A * A + B * B), Mod(Mathf.Atan2(B, A) * Mathf.Rad2Deg, 360f));
        }

        static Vector3 LinearFromOklch(Vector3 lch)
        {
            float a = lch.y * Mathf.Cos(lch.z * Mathf.Deg2Rad), b = lch.y * Mathf.Sin(lch.z * Mathf.Deg2Rad);
            float l = lch.x + 0.3963377774f * a + 0.2158037573f * b, m = lch.x - 0.1055613458f * a - 0.0638541728f * b, s = lch.x - 0.0894841775f * a - 1.2914855480f * b;
            l *= l * l; m *= m * m; s *= s * s;
            return new Vector3(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
                -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
                -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
        }

        /// <summary>An OKLCH colour as sRGB; outside sRGB, its chroma lowered (lightness and hue kept) until it fits.</summary>
        public static Color FromOklch(Vector3 lch)
        {
            bool Fits(Vector3 v) => v.x >= -1e-4f && v.y >= -1e-4f && v.z >= -1e-4f && v.x <= 1.0001f && v.y <= 1.0001f && v.z <= 1.0001f;
            Vector3 rgb = LinearFromOklch(lch);
            if (!Fits(rgb))
            {
                float lo = 0f, hi = lch.y;
                for (int i = 0; i < 18; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(LinearFromOklch(new Vector3(lch.x, mid, lch.z)))) lo = mid; else hi = mid;
                }
                rgb = LinearFromOklch(new Vector3(lch.x, lo, lch.z));
            }
            return new Color(Gam(Mathf.Clamp01(rgb.x)), Gam(Mathf.Clamp01(rgb.y)), Gam(Mathf.Clamp01(rgb.z)), 1f);
        }
    }
}
