using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The things only one particle nest has (NestTheme.things), so the nests differ in what grows and lies in them,
    /// not only in their colours. The amber nest (orange): gold ball-stalks lining the way, beads of resin lit from
    /// within, patches of wax comb brimming with glowing honey, clusters of amber shards. The kiln temple (red): ember
    /// cracks in the floor, clay chimneys glowing at the mouth, clay cells, pot shards; and at its heart the amber
    /// shrine with an ancestor inside. Each is drawn like the floor's other things (NestThingsGrowth): opaque splats
    /// over its surfaces, each facing the way its surface faces; what glows glows of itself (and breathes with the
    /// plants: Shaders/NestField.cginc); what is glassy (resin, amber) has a thin skin of small splats with gaps, so
    /// the light inside shows through, as the blue forest's glass bulbs do. A generator stands its thing at p on the
    /// floor (n up, ta and tb along the floor); size is the thing's main dimension in units (the ant is 1 long).
    /// </summary>
    public static class NestThingsThemed
    {
        const float Tau = 6.2831853f;

        /// <summary>A heart thing's frame: like a floor thing's, but with no cap on its splats (it is big, and seen
        /// close).</summary>
        struct Frame
        {
            readonly ThingPen pen;
            readonly Vector3 p, a, n, b;

            public Frame(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb)
            {
                this.pen = pen;
                this.p = p;
                this.n = n;
                a = ta;
                b = tb;
            }

            public float R() => pen.R();
            public float U(float lo, float hi) => lo + (hi - lo) * pen.R();
            public float S() => pen.R() * 2f - 1f;

            public void Put(Vector3 lp, Vector3 ln, Color c, float r, float glow = 0f) =>
                pen.Put(p + a * lp.x + n * lp.y + b * lp.z, a * ln.x + n * ln.y + b * ln.z, c, r, glow);

            /// <summary>Where local point lp is in the world.</summary>
            public Vector3 World(Vector3 lp) => p + a * lp.x + n * lp.y + b * lp.z;

            public ThingPen Pen => pen;
        }

        /// <summary>A random point inside the unit ball, and how far out it is (0 middle .. 1 skin).</summary>
        static Vector3 InBall(ThingPen pen, out float f)
        {
            for (int k = 0; k < 24; k++)
            {
                var d = new Vector3(pen.R() * 2 - 1, pen.R() * 2 - 1, pen.R() * 2 - 1);
                if (d.sqrMagnitude <= 1f) { f = d.magnitude; return d; }
            }
            f = 0;
            return Vector3.zero;
        }

        // ------------------------------------------------------------------- the newer nests (first versions)

        /// <summary>
        /// The leafcutters' garden: a piece of fresh leaf the ants cut, a crescent with a clean bitten edge, standing up
        /// on its straight edge like a sail; its midrib and veins paler and faintly lit, the bitten rim browner. size is
        /// its height (0.5 to 1.4).
        /// </summary>
        public static void LeafSail(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float w = size * s.U(0.9f, 1.3f), lean = s.S() * 0.25f, yaw = s.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            Color leaf = Color.Lerp(new Color(0.40f, 0.62f, 0.14f), new Color(0.56f, 0.70f, 0.20f), s.R()), vein = new Color(0.72f, 0.86f, 0.36f), bite = new Color(0.46f, 0.34f, 0.14f);
            int m = s.Count(w * size * 0.6f * 2f, 60);
            for (int j = 0; j < m; j++)
            {
                // a half disc (the leaf's cut) less a bite out of its top edge (the crescent)
                float u = s.S(), v = s.R();
                if (u * u + v * v > 1f) continue;
                float bx = u - 0.35f, by = v - 0.95f;
                if (bx * bx + by * by < 0.42f) continue;
                bool rim = bx * bx + by * by < 0.5f || u * u + v * v > 0.9f;
                float mid = Mathf.Abs(u + 0.15f * v), veins = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((v - mid * 0.6f) * 26f)), 18f);
                bool isVein = mid < 0.03f || veins > 0.5f;
                float x = u * w * 0.5f, y = v * size, side = (s.R() - 0.5f) * 0.012f;
                var lp = new Vector3(x * cs - side * sn + lean * y * sn, y, x * sn + side * cs - lean * y * cs);
                var nrm = new Vector3(-sn, 0.15f, cs) * (s.R() < 0.5f ? 1f : -1f);
                Color col = rim ? bite : isVein ? vein : leaf;
                s.Put(lp, nrm, s.V(col, 0.1f), s.Rad(0.95f), isVein ? 0.22f : 0f);
            }
        }

        /// <summary>
        /// The moss forest: a dew drop as big as the ant, resting on the moss: a clear sphere, its skin a few small
        /// glassy splats with gaps, dark but for its rim and one tight bright highlight toward the moon, the light it
        /// gathers pooled low inside it, and that light thrown onto the floor beside it (a caustic). size is its width
        /// (0.4 to 1.2).
        /// </summary>
        public static void DewDrop(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float r = size * 0.5f, flat = s.U(0.8f, 0.92f);
            var c = new Vector3(0f, r * flat * 0.92f, 0f);
            Color skin = new Color(0.62f, 0.76f, 0.74f), inner = new Color(0.60f, 0.92f, 0.80f);
            int m = s.Count(NestThingsGrowth.EllipsoidArea(r, r * flat, r) * 0.3f, 30);
            for (int j = 0; j < m; j++)
            {
                Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, Vector3.right * r, Vector3.up * (r * flat), Vector3.forward * r, out Vector3 nrm, out Vector3 q);
                bool high = q.y > 0.7f && q.x < -0.05f && q.x > -0.45f && q.z < 0.25f && q.z > -0.3f;   // the highlight, toward the moon
                float rim = Mathf.Pow(1f - Mathf.Abs(q.y), 3f);
                s.Put(pos, nrm, high ? new Color(0.96f, 0.99f, 0.97f) : s.V(skin * (0.2f + 0.5f * rim), 0.08f), s.Rad(high ? 0.5f : 0.3f), high ? 0.85f : 0.05f + 0.1f * rim);
            }
            int k = Mathf.RoundToInt(m * 0.22f);
            for (int j = 0; j < k; j++)
            {
                Vector3 d = InBall(pen, out float f);
                float low = Mathf.Clamp01(0.2f - d.y);
                s.Put(c + new Vector3(d.x * r, d.y * r * flat, d.z * r) * 0.75f, Vector3.up, s.V(inner * (0.2f + 0.5f * low), 0.08f), s.Rad(0.3f), (0.15f + 0.45f * low) * (1f - f));
            }
            // the caustic: a small soft spot of its light on the floor, away from the moon
            for (int j = 0, kc = Mathf.Max(12, k / 3); j < kc; j++)
            {
                float a = s.R() * Tau, q = Mathf.Sqrt(s.R()) * r * 0.45f;
                s.Put(new Vector3(-0.32f * r * 1.4f + Mathf.Cos(a) * q, 0.015f, -0.28f * r * 1.4f + Mathf.Sin(a) * q), Vector3.up, s.V(inner * 0.6f, 0.1f), s.Rad(0.35f), 0.5f * (1f - q / (r * 0.45f)));
            }
        }

        /// <summary>
        /// The moss forest: a moss stalk as tall as a street lamp is to us: one to three thin red-brown setae rising from
        /// the moss, each nodding a capsule at its top, olive, its pale lid glowing softly (a row of them reads as a row
        /// of thin lamps). size is the tallest one's height (0.8 to 2.4).
        /// </summary>
        public static void Sporophyte(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            int count = 1 + (int)(s.R() * 3f);
            Color seta0 = new Color(0.22f, 0.10f, 0.06f), seta1 = new Color(0.50f, 0.26f, 0.14f), caps = new Color(0.40f, 0.36f, 0.16f), lid = new Color(0.92f, 0.98f, 0.58f);
            for (int i = 0; i < count; i++)
            {
                float h = size * (i == 0 ? 1f : s.U(0.55f, 0.9f)), ang = s.R() * Tau, off = i == 0 ? 0f : s.U(0.05f, 0.2f);
                var b0 = new Vector3(Mathf.Cos(ang) * off, 0f, Mathf.Sin(ang) * off);
                var lean = new Vector3(s.S(), 0f, s.S()) * 0.12f;
                float rs = Mathf.Lerp(0.007f, 0.013f, Mathf.Clamp01(h / 2.4f));
                Vector3 prev = b0;
                for (int q = 1; q <= 8; q++)
                {
                    float f0 = (q - 1) / 8f, f1 = q / 8f;
                    Vector3 cur = b0 + Vector3.up * (h * f1) + lean * (h * f1 * f1);
                    NestThingsGrowth.Tube(s, prev, cur, rs, rs * 0.92f, Color.Lerp(seta0, seta1, f0), Color.Lerp(seta0, seta1, f1));
                    prev = cur;
                }
                // the capsule, nodding: an ellipsoid along its own axis, its lid (the far end) pale and lit
                var nod = new Vector3(Mathf.Cos(ang + 1.3f), 0f, Mathf.Sin(ang + 1.3f));
                Vector3 axis = (nod * s.U(0.5f, 0.9f) + Vector3.up * s.U(0.2f, 0.6f)).normalized;
                float rc = Mathf.Lerp(0.03f, 0.055f, Mathf.Clamp01(h / 2.4f)), lc = rc * s.U(1.6f, 2.2f);
                NestThingsGrowth.Perp(axis, out Vector3 u, out Vector3 v);
                Vector3 c = prev + axis * lc;
                int m = s.Count(NestThingsGrowth.EllipsoidArea(rc, lc, rc), 30);
                float glow = s.U(0.55f, 0.85f);
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, u * rc, axis * lc, v * rc, out Vector3 nrm, out Vector3 qq);
                    bool isLid = qq.y > 0.6f;
                    s.Put(pos, nrm, s.V(isLid ? lid : caps * (0.8f + 0.3f * qq.y), 0.08f), s.Rad(0.6f), isLid ? glow : glow * 0.2f);
                }
            }
        }

        /// <summary>
        /// The moss forest: a patch of star moss, at ant scale a stand of small pines: shoots on short dark stems, each
        /// topped with a star of narrow leaves, lighter toward their tips. size is the patch's width (0.5 to 1.4).
        /// </summary>
        public static void StarMoss(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color stem = new Color(0.08f, 0.17f, 0.06f), tip = new Color(0.46f, 0.64f, 0.24f);
            int shoots = 10 + (int)(size * 24f);
            float r0 = s.Thin;
            for (int i = 0; i < shoots; i++)
            {
                float a = s.R() * Tau, q = Mathf.Sqrt(s.R()) * size * 0.5f;
                var b = new Vector3(Mathf.Cos(a) * q, 0f, Mathf.Sin(a) * q);
                float h = s.U(0.12f, 0.32f) * (1f - 0.4f * q / (size * 0.5f));
                Vector3 top = b + new Vector3(s.S() * 0.02f, h, s.S() * 0.02f);
                for (int j = 0; j < 8; j++)
                {
                    float t = (j + 0.5f) / 8f;
                    s.Put(Vector3.Lerp(b, top, t), new Vector3(s.S(), 0.2f, s.S()), s.V(stem * (0.8f + 0.4f * t), 0.1f), r0 * 1.2f);
                }
                int leaves = 6 + (int)(s.R() * 3f);
                float spin = s.R() * Tau;
                for (int k = 0; k < leaves; k++)
                {
                    float la = spin + k * Tau / leaves;
                    Vector3 d = new Vector3(Mathf.Cos(la), s.U(0.25f, 0.6f), Mathf.Sin(la)).normalized;
                    float len = h * s.U(0.35f, 0.55f);
                    for (int j = 0; j < 6; j++)
                    {
                        float t = (j + 0.5f) / 6f;
                        s.Put(top + d * (len * t) + Vector3.down * (0.15f * len * t * t), Vector3.up + d * 0.3f, s.V(Color.Lerp(stem, tip, 0.4f + 0.6f * t), 0.1f), r0 * (1.3f - 0.5f * t));
                    }
                }
            }
        }

        /// <summary>
        /// The verdigris city: a giant's brass gear standing on its edge, half sunk in the floor: its rim and square
        /// teeth (their flanks and tops too, so they read against the light), a hub and its spokes; the brass dull, worn
        /// bright on the teeth, patched with pale green verdigris that gathers low and round the hub and glows faintly
        /// along the teeth. size is its diameter (0.8 to 2.4). Blocks the ants.
        /// </summary>
        public static void Gear(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float ro = size * 0.5f, ri = ro * s.U(0.7f, 0.78f), rh = ro * 0.2f, depth = size * s.U(0.09f, 0.13f), tooth = ro * 0.17f, spokeW = ro * 0.11f;
            int teeth = 12 + (int)(s.R() * 10f), spokes = 4 + (int)(s.R() * 3f);
            float yaw = s.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw), sink = ro * s.U(0.25f, 0.45f), seed = s.R() * 20f;
            Color brass = new Color(0.74f, 0.50f, 0.14f), worn = new Color(0.96f, 0.76f, 0.32f), green = new Color(0.30f, 0.64f, 0.50f);
            // a point of the gear in its own plane (gx across, gy up, gz through its thickness) to the floor's frame
            Vector3 At(float gx, float gy, float gz) => new Vector3(gx * cs - gz * sn, gy + ro - sink, gx * sn + gz * cs);
            Vector3 Dir(float gx, float gy, float gz) => new Vector3(gx * cs - gz * sn, gy, gx * sn + gz * cs);
            bool IsTooth(float a) { float t = Mathf.Repeat(a / Tau * teeth, 1f); return t > 0.28f && t < 0.72f; }
            bool Solid(float gx, float gy)
            {
                float r = Mathf.Sqrt(gx * gx + gy * gy), a = Mathf.Atan2(gy, gx);
                if (r <= rh || (r >= ri && r <= ro)) return true;
                if (r > ro) return r <= ro + tooth && IsTooth(a);
                float sa = Mathf.Repeat(a / Tau * spokes, 1f) - 0.5f;
                return Mathf.Abs(sa) * Tau / spokes * r < spokeW * 0.5f;
            }
            Color Col(float gx, float gy, float r)
            {
                float patina = Mathf.PerlinNoise(gx * 3f + seed, gy * 3f + seed) + 0.25f * Mathf.Clamp01(-gy / ro) + (r < rh * 1.6f ? 0.2f : 0f);
                Color c = Color.Lerp(brass, green, Mathf.SmoothStep(0.72f, 0.86f, patina));   // (mostly brass, the green in patches)
                return r > ro + tooth * 0.55f && patina < 0.6f ? Color.Lerp(c, worn, 0.7f) : c;
            }
            float faces = 2f * (3.1416f * (rh * rh + ro * ro - ri * ri) + teeth * tooth * Tau * ro / teeth * 0.44f + spokes * (ri - rh) * spokeW);
            float edges = (Tau * ro + teeth * 2f * tooth) * depth;
            float fill = Mathf.Clamp((s.Left - 200f) / Mathf.Max(1f, s.Count(faces + edges, 1)), 0.15f, 1f), grow = 1f / Mathf.Sqrt(fill);
            // its two faces
            float box = ro + tooth;
            int tries = Mathf.RoundToInt(s.Count(8f * box * box, 1) * fill);   // (both faces' square round it; most fall on the gear)
            for (int j = 0; j < tries; j++)
            {
                float gx = s.S() * box, gy = s.S() * box;
                if (!Solid(gx, gy) || gy + ro - sink < 0.01f) continue;
                float r = Mathf.Sqrt(gx * gx + gy * gy), face = s.R() < 0.5f ? 1f : -1f;
                Color c = Col(gx, gy, r);
                bool lit = r > ro && c.g > c.r;
                s.Put(At(gx, gy, face * depth * 0.5f), Dir(0, 0, face), s.V(c, 0.08f), s.Rad() * grow, lit ? 0.28f : 0f);
            }
            // its edge: the rim between the teeth, the teeth's tops and flanks
            int me = Mathf.RoundToInt(s.Count(edges, 1) * fill);
            for (int j = 0; j < me; j++)
            {
                float a = s.R() * Tau, gz = s.S() * depth * 0.5f, r;
                if (s.R() < 0.3f)
                {
                    // a flank: at a tooth's side, from the rim out to its top
                    float k = Mathf.Floor(a / Tau * teeth), at = (k + (s.R() < 0.5f ? 0.28f : 0.72f)) / teeth * Tau;
                    r = ro + s.R() * tooth;
                    a = at;
                }
                else r = IsTooth(a) ? ro + tooth : ro;
                float gx = Mathf.Cos(a) * r, gy = Mathf.Sin(a) * r;
                if (gy + ro - sink < 0.01f) continue;
                s.Put(At(gx, gy, gz), Dir(Mathf.Cos(a), Mathf.Sin(a), 0), s.V(Col(gx, gy, r), 0.08f) * 0.85f, s.Rad() * grow);
            }
        }

        /// <summary>
        /// The verdigris city: a length of the giant's copper wire lying along the floor like a rail, two strands side by
        /// side bound together here and there, bent a little, green with verdigris in patches that glow faintly. Flat:
        /// SolidThings drapes it over the floor. size is its length (1 to 3).
        /// </summary>
        public static void Wire(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float dir = s.R() * Tau, r = s.U(0.035f, 0.05f), gap = r * 2.8f, seed = s.R() * 20f;
            Color copper = new Color(0.55f, 0.29f, 0.15f), verdigris = new Color(0.34f, 0.66f, 0.52f), bind = new Color(0.16f, 0.14f, 0.12f);
            var at = new Vector3(-Mathf.Cos(dir), 0f, -Mathf.Sin(dir)) * (size * 0.5f);
            int steps = Mathf.Max(8, Mathf.RoundToInt(size / 0.02f));
            float nextBind = s.U(0.15f, 0.4f), along = 0;
            for (int i = 0; i < steps; i++)
            {
                dir += s.S() * 0.06f;
                var fwd = new Vector3(Mathf.Cos(dir), 0f, Mathf.Sin(dir));
                at += fwd * (size / steps);
                along += size / steps;
                var side = new Vector3(-fwd.z, 0f, fwd.x);
                for (int strand = -1; strand <= 1; strand += 2)
                {
                    float patina = Mathf.PerlinNoise(i * 0.08f + seed + strand * 3.3f, seed);
                    Vector3 c0 = at + side * (strand * gap * 0.5f);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = s.R() * Mathf.PI;
                        Vector3 o = side * (Mathf.Cos(a) * r) + Vector3.up * (r + Mathf.Sin(a) * r);
                        Color col = patina > 0.56f ? verdigris : copper * (0.8f + 0.5f * Mathf.Pow(Mathf.Sin(a), 6f));   // a gleam along its top
                        s.Put(c0 + o, (o - Vector3.up * r).normalized, s.V(col, 0.1f), s.Rad(0.45f), patina > 0.64f ? 0.3f : 0f);
                    }
                }
                if (along > nextBind)
                {
                    nextBind = along + s.U(0.3f, 0.6f);
                    for (int k = 0; k < 40; k++)
                    {
                        float u = s.S() * (gap * 0.5f + r * 1.2f), a = s.R() * Mathf.PI;
                        s.Put(at + side * u + fwd * (s.S() * r * 0.6f) + Vector3.up * (r * 2.2f * Mathf.Sin(a) + 0.01f), Vector3.up, s.V(bind, 0.1f), s.Rad(0.4f));
                    }
                }
            }
        }

        /// <summary>
        /// The rain cistern: a puddle on the floor, still and black, its edge irregular, a thin wet sheen round it and the
        /// earth beyond it darker; the light caught on it in a few broken streaks and points, as a reflection does, and a
        /// faint ring where a drop fell. Flat: SolidThings drapes it over the floor. size is its width (0.8 to 2.6).
        /// </summary>
        public static void Puddle(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float R = size * 0.5f, seed = s.R() * 30f, stretch = s.U(0.6f, 1f), ringAt = s.U(0.2f, 0.6f);
            Color water = new Color(0.004f, 0.006f, 0.016f), sheen = new Color(0.16f, 0.20f, 0.32f), wet = new Color(0.05f, 0.045f, 0.06f), gleam = new Color(0.62f, 0.70f, 1.0f);
            float streakDir = s.R() * Tau, sc = Mathf.Cos(streakDir), ss = Mathf.Sin(streakDir);
            int m = s.Count(3.1416f * R * R * stretch * 1.4f, 80);
            for (int j = 0; j < m; j++)
            {
                float a = s.R() * Tau, f = Mathf.Sqrt(s.R()) * 1.18f;
                float edge = 1f + 0.18f * Mathf.Sin(a * 3f + seed) + 0.1f * Mathf.Sin(a * 7f - seed);
                if (f > edge * 1.18f) continue;
                bool inWater = f < edge, atEdge = inWater && f > edge * 0.9f;
                float x = Mathf.Cos(a) * f * R, z = Mathf.Sin(a) * f * R * stretch;
                // the light caught on it: streaks across it one way (a beam's or a thread's reflection), broken
                float across = x * -ss + z * sc, alongS = x * sc + z * ss;
                float streak = Mathf.Exp(-across * across / (0.0016f * R * R)) * Mathf.Max(0f, Mathf.Sin(alongS * 9f / R + seed));
                float ring = Mathf.Exp(-Mathf.Pow((f - ringAt) / 0.03f, 2f));
                bool lit = inWater && s.R() < 0.012f + 0.5f * streak;
                Color col = lit ? gleam * (0.4f + 0.6f * s.R()) : atEdge ? s.V(sheen, 0.15f) : inWater ? s.V(Color.Lerp(water, sheen, 0.5f * ring), 0.05f) : s.V(wet, 0.15f);
                s.Put(new Vector3(x, 0.02f, z), Vector3.up, col, s.Rad(inWater ? 0.85f : 0.6f), lit ? 0.6f : 0.06f * ring);
            }
        }

        // ----------------------------------------------------------- the newer nests' hearts (first versions)

        /// <summary>A point spread evenly over an ellipsoid round local centre c (half-axes ex, ey, ez) for a heart thing,
        /// its outward normal and where it lies on the unit sphere (q), as NestThingsGrowth.OnEllipsoid.</summary>
        static Vector3 OnEllipsoid(Frame f, Vector3 c, Vector3 ex, Vector3 ey, Vector3 ez, out Vector3 nrm, out Vector3 q)
        {
            float a = ex.magnitude, b = ey.magnitude, d = ez.magnitude, m = Mathf.Min(a, Mathf.Min(b, d));
            Vector3 g = Vector3.up;
            q = Vector3.up;
            for (int k = 0; k < 16; k++)
            {
                float z = f.S(), t = f.R() * Tau, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                q = new Vector3(rr * Mathf.Cos(t), z, rr * Mathf.Sin(t));
                g = new Vector3(q.x / a, q.y / b, q.z / d);
                if (f.R() < g.magnitude * m) break;
            }
            nrm = ex * (g.x / a) + ey * (g.y / b) + ez * (g.z / d);
            return c + ex * q.x + ey * q.y + ez * q.z;
        }

        /// <summary>A tapered tube of a heart thing from local a0 to a1 (radius r0 to r1, colour c0 to c1), covered over
        /// its surface at perArea (a thin one at least a splat every radius along it).</summary>
        static void Tube(Frame f, Vector3 a0, Vector3 a1, float r0, float r1, Color c0, Color c1, float perArea, float glow = 0f)
        {
            Vector3 d = a1 - a0;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            d /= len;
            NestThingsGrowth.Perp(d, out Vector3 u, out Vector3 v);
            float rm = (r0 + r1) * 0.5f, rad = 0.62f / Mathf.Sqrt(perArea), slope = (r0 - r1) / len;
            int m = Mathf.Max(Mathf.RoundToInt(Tau * rm * len * perArea), Mathf.RoundToInt(len / Mathf.Max(0.004f, rm)), 3);
            for (int i = 0; i < m; i++)
            {
                float t = f.R(), a = f.R() * Tau, r = Mathf.Lerp(r0, r1, t);
                Vector3 o = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                f.Put(a0 + d * (len * t) + o * r, o + d * slope, Color.Lerp(c0, c1, t) * (0.85f + 0.3f * f.R()),
                    Mathf.Min(rad * (0.75f + 0.5f * f.R()), r * 1.3f + 0.003f), glow);
            }
        }

        /// <summary>Tubes through local points, the radius going from r0 at the first to r1 at the last.</summary>
        static void Path(Frame f, Vector3[] pts, float r0, float r1, Color c0, Color c1, float perArea, float glow = 0f)
        {
            for (int i = 1; i < pts.Length; i++)
            {
                float t0 = (i - 1f) / (pts.Length - 1f), t1 = i / (pts.Length - 1f);
                Tube(f, pts[i - 1], pts[i], Mathf.Lerp(r0, r1, t0), Mathf.Lerp(r0, r1, t1), Color.Lerp(c0, c1, t0), Color.Lerp(c0, c1, t1), perArea, glow);
            }
        }

        /// <summary>A clear drop round local c (radius r, flat its height against its width): its skin a few small
        /// glassy splats with gaps, a bright highlight toward the moon, its rim catching the light; inside, the light it
        /// gathers, pooled low in it (light: how much of it glows).</summary>
        static void Drop(Frame f, Vector3 c, float r, float flat, Color skin, Color inner, float perArea, float light, float skinLight = 0.18f)
        {
            float rad = 0.62f / Mathf.Sqrt(perArea);
            int ms = Mathf.Max(24, Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(r, r * flat, r) * perArea * 0.5f));
            for (int j = 0; j < ms; j++)
            {
                Vector3 pos = OnEllipsoid(f, c, Vector3.right * r, Vector3.up * (r * flat), Vector3.forward * r, out Vector3 nrm, out Vector3 q);
                bool high = q.y > 0.5f && q.x < -0.1f && q.z < 0.3f && q.z > -0.4f;
                float rim = Mathf.Pow(1f - Mathf.Abs(q.y), 2f);
                f.Put(pos, nrm, high ? new Color(0.96f, 0.99f, 0.97f) : skin * (skinLight + 0.55f * rim * rim + 0.1f * f.R()),
                    Mathf.Min(rad * (high ? 0.5f : 0.3f + 0.2f * f.R()), r * 0.3f), high ? 0.8f : (0.04f + 0.12f * rim) * light);
            }
            for (int j = 0, mi = ms / 2; j < mi; j++)
            {
                Vector3 d = InBall(f.Pen, out float fr);
                float low = Mathf.Clamp01(0.3f - d.y * 0.8f);
                f.Put(c + new Vector3(d.x * r, d.y * r * flat, d.z * r) * 0.85f, Vector3.up, inner * (0.25f + 0.55f * low) * (1.1f - 0.5f * fr),
                    Mathf.Min(rad * 0.35f, r * 0.25f), light * Mathf.Clamp01((0.25f + 0.6f * low) * (1.05f - 0.6f * fr)));
            }
        }

        /// <summary>A shoot of star moss at local b, h tall: a short dark stem and a star of narrow leaves at its top,
        /// lighter toward their tips.</summary>
        static void StarShoot(Frame f, Vector3 b, float h, float rad, Color tip)
        {
            Color stem = new Color(0.08f, 0.17f, 0.06f);
            Vector3 top = b + new Vector3(f.S() * 0.02f, h, f.S() * 0.02f);
            for (int j = 0; j < 10; j++)
            {
                float t = (j + 0.5f) / 10f;
                f.Put(Vector3.Lerp(b, top, t), new Vector3(f.S(), 0.2f, f.S()), stem * (0.8f + 0.4f * t), rad * 0.45f);
            }
            int leaves = 6 + (int)(f.R() * 3f);
            float spin = f.R() * Tau;
            for (int k = 0; k < leaves; k++)
            {
                float a = spin + k * Tau / leaves;
                Vector3 d = new Vector3(Mathf.Cos(a), f.U(0.25f, 0.6f), Mathf.Sin(a)).normalized;
                float len = h * f.U(0.35f, 0.55f);
                for (int j = 0; j < 8; j++)
                {
                    float t = (j + 0.5f) / 8f;
                    f.Put(top + d * (len * t) + Vector3.down * (0.15f * len * t * t), Vector3.up + d * 0.3f,
                        Color.Lerp(stem, tip, 0.4f + 0.6f * t) * (0.85f + 0.3f * f.R()), rad * (0.5f - 0.2f * t));
                }
            }
        }

        /// <summary>
        /// The moss forest's heart: on a broad cushion of moss (a close pile, bright tips and dark hollows, star moss
        /// standing in it) one dew drop taller than the ant, holding the light. size is the drop's width (about 1.6). The
        /// ring of moss stalks round it is placed as the floor's things are (SolidThings.Heart). Returns where its light
        /// is (the world).
        /// </summary>
        public static Vector3 HeartDew(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float rad = 0.62f / Mathf.Sqrt(perArea), cr = size * 1.05f, ch = size * 0.26f;
            Color deep = new Color(0.05f, 0.13f, 0.05f), moss = new Color(0.18f, 0.38f, 0.12f), tip = new Color(0.44f, 0.62f, 0.24f);
            int m = Mathf.RoundToInt(3.1416f * cr * cr * 1.2f * perArea);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, q = Mathf.Sqrt(f.R());
                float lump = 1f + 0.08f * Mathf.Sin(a * 5f + 1.7f) + 0.05f * Mathf.Sin(a * 11f);
                float x = Mathf.Cos(a) * q * cr * lump, z = Mathf.Sin(a) * q * cr * lump, y = ch * Mathf.Sqrt(Mathf.Max(0f, 1f - q * q));
                float pile = Mathf.Abs(Mathf.Sin(x * 41f + Mathf.Sin(z * 23f)) * Mathf.Sin(z * 37f + Mathf.Sin(x * 19f)));
                Color col = Color.Lerp(deep, Color.Lerp(moss, tip, pile * pile), 0.35f + 0.65f * pile);
                f.Put(new Vector3(x, y + 0.05f + 0.035f * pile, z), new Vector3(x / (cr * cr), y / (ch * ch) + 0.5f, z / (cr * cr)),
                    col * (0.85f + 0.3f * f.R()), rad * (0.7f + 0.5f * f.R()));
            }
            for (int i = 0; i < 44; i++)
            {
                float a = f.R() * Tau, q = Mathf.Sqrt(f.R()) * 0.95f;
                float x = Mathf.Cos(a) * q * cr, z = Mathf.Sin(a) * q * cr;
                if (x * x + z * z < size * size * 0.2f) continue;   // not under the drop
                StarShoot(f, new Vector3(x, ch * Mathf.Sqrt(Mathf.Max(0f, 1f - q * q)) + 0.05f, z), f.U(0.08f, 0.2f), rad, tip);
            }
            float r = size * 0.5f;
            var c = new Vector3(0f, ch + 0.05f + r * 0.9f * 0.95f, 0f);
            Drop(f, c, r, 0.9f, new Color(0.62f, 0.78f, 0.70f), new Color(0.55f, 0.95f, 0.70f), perArea, 1f);
            return f.World(c);
        }

        /// <summary>
        /// The rain cistern's heart: the chamber's floor a pool of still black water, rings of ripples spreading over it
        /// from under the drop and the light caught on it in broken streaks; wet dark earth and pebbles round its edge;
        /// over it a root arching out of the floor, its tip holding one big drop, fine rootlets beaded with water hanging
        /// from it. Flat parts are draped over the floor by SolidThings. size is the pool's width (about 4.4). Returns
        /// where the big drop is (the world).
        /// </summary>
        public static Vector3 HeartPool(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea, Vector3 face)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float rad = 0.62f / Mathf.Sqrt(perArea), R = size * 0.5f, seed = f.R() * 30f;
            Color water = new Color(0.004f, 0.006f, 0.016f), wet = new Color(0.05f, 0.045f, 0.055f), ring = new Color(0.08f, 0.11f, 0.24f), gleam = new Color(0.62f, 0.70f, 1.0f);
            int m = Mathf.RoundToInt(3.1416f * R * R * 1.35f * perArea * 0.7f);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, q = Mathf.Sqrt(f.R()) * 1.16f;
                float edge = 1f + 0.12f * Mathf.Sin(a * 3f + seed) + 0.07f * Mathf.Sin(a * 7f - seed);
                if (q > edge * 1.16f) continue;
                bool inWater = q < edge;
                float x = Mathf.Cos(a) * q * R, z = Mathf.Sin(a) * q * R, d = q * R;
                float rip = 0f;
                for (int k = 0; k < 3; k++)
                    rip = Mathf.Max(rip, Mathf.Exp(-Mathf.Pow((d - R * (0.22f + 0.24f * k)) / (0.025f * R), 2f)) * (1f - 0.25f * k));
                float streak = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(x * 3.1f + seed) * Mathf.Sin(z * 0.7f + x * 0.4f)), 12f);
                bool lit = inWater && f.R() < 0.012f + 0.25f * streak;
                Color col = lit ? gleam * (0.5f + 0.5f * f.R()) : inWater ? Color.Lerp(water, ring, rip * 0.8f) * (0.85f + 0.3f * f.R()) : wet * (0.8f + 0.5f * f.R());
                f.Put(new Vector3(x, 0.07f, z), Vector3.up, col, rad * (inWater ? 1.1f : 0.7f) * (0.8f + 0.4f * f.R()), lit ? 0.55f : rip * 0.12f);
            }
            // the root: out of the floor beyond the pool's edge, arching up and over it, thinning to its tip
            // (rooted beyond the pool's far side from the way in, so its drop hangs over the pool in front of the ant)
            float side = Mathf.Atan2(-face.z, -face.x) + f.S() * 0.4f, cs = Mathf.Cos(side), sn = Mathf.Sin(side), H = size * 0.5f;
            var pts = new Vector3[16];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / (pts.Length - 1f);
                float along = Mathf.Lerp(R * 1.08f, -R * 0.06f, t);
                float y = H * Mathf.Sin(Mathf.Min(t * 1.3f, 1f) * Mathf.PI * 0.5f) - Mathf.Max(0f, t - 0.75f) * H * 0.35f;
                float wob = 0.12f * Mathf.Sin(t * 5.3f + seed);
                pts[i] = new Vector3(along * cs - wob * sn, y, along * sn + wob * cs);
            }
            pts[0].y = -0.1f;
            Color bark = new Color(0.10f, 0.07f, 0.05f), barkTip = new Color(0.30f, 0.25f, 0.19f), skin = new Color(0.58f, 0.64f, 0.90f), inner = new Color(0.55f, 0.62f, 1.0f);
            Path(f, pts, 0.17f, 0.025f, bark, barkTip, perArea);
            Vector3 dropAt = pts[pts.Length - 1] + Vector3.down * 0.27f;
            Drop(f, dropAt, 0.21f, 1.2f, skin, inner, perArea * 1.5f, 1f);
            for (int k = 0; k < 6; k++)
            {
                Vector3 from = pts[3 + (int)(f.R() * 10f)];
                float len = f.U(0.25f, 1.1f);
                var hang = new[] { from, from + new Vector3(f.S() * 0.05f, -len * 0.5f, f.S() * 0.05f), from + new Vector3(f.S() * 0.08f, -len, f.S() * 0.08f) };
                Path(f, hang, 0.012f, 0.005f, bark, barkTip, perArea);
                Drop(f, hang[2] + Vector3.down * 0.035f, f.U(0.025f, 0.045f), 1.15f, skin, inner, perArea * 3f, 0.8f);
            }
            for (int k = 0; k < 14; k++)
            {
                float a = f.R() * Tau, d = R * f.U(1.0f, 1.2f), pr = f.U(0.08f, 0.2f);
                var c = new Vector3(Mathf.Cos(a) * d, pr * 0.3f, Mathf.Sin(a) * d);
                int mp = Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(pr, pr * 0.6f, pr * 0.8f) * perArea);
                Color stone = Color.Lerp(new Color(0.30f, 0.30f, 0.32f), new Color(0.50f, 0.48f, 0.44f), f.R());
                for (int j = 0; j < mp; j++)
                {
                    Vector3 pos = OnEllipsoid(f, c, new Vector3(pr, 0, 0), new Vector3(0, pr * 0.6f, 0), new Vector3(0, 0, pr * 0.8f), out Vector3 nrm, out Vector3 q);
                    f.Put(pos, nrm, stone * (0.7f + 0.5f * Mathf.Max(0f, q.y)) * (0.85f + 0.3f * f.R()), rad * (0.6f + 0.4f * f.R()));
                }
            }
            return f.World(dropAt);
        }

        /// <summary>
        /// The verdigris city's heart: a giant's pocket watch lying open, half sunk in the floor and tipped up toward the
        /// sky: its case a brass band patched with verdigris, its dial pale with twelve hour marks and stopped hands, a
        /// piece of the dial broken out over the small gears and the hairspring inside, which glow a faint verdigris; the
        /// glass cracked across; its lid standing open behind it, the crown and its ring at the top. size is its
        /// diameter (about 3.2). Returns where its light is (the world).
        /// </summary>
        public static Vector3 HeartWatch(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea, Vector3 face)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float rad = 0.62f / Mathf.Sqrt(perArea), R = size * 0.5f, depth = size * 0.1f, seed = f.R() * 20f;
            // its dial toward the way in, tipped up from it this far (radians): the ant coming in sees its face
            float yaw = Mathf.Atan2(face.z, face.x) + f.S() * 0.3f, tilt = f.U(0.6f, 0.8f);
            // the watch's own frame: wx across its face (level), wy up its face, wz out of it
            var wz = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(tilt), Mathf.Sin(tilt), Mathf.Sin(yaw) * Mathf.Cos(tilt));
            var wx = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 wy = Vector3.Cross(wz, wx).normalized;
            if (wy.y < 0) { wy = -wy; wx = -wx; }
            var c = new Vector3(0f, R * wy.y * 0.5f, 0f);   // its lower third in the floor
            Vector3 At(float u, float v, float w) => c + wx * u + wy * v + wz * w;
            Color brass = new Color(0.50f, 0.36f, 0.14f), green = new Color(0.30f, 0.58f, 0.46f), dial = new Color(0.74f, 0.71f, 0.60f), ink = new Color(0.09f, 0.08f, 0.07f), steel = new Color(0.07f, 0.08f, 0.11f);
            Color Metal(float u, float v, float w)
            {
                float pat = Mathf.PerlinNoise(u * 2.3f + seed, v * 2.3f + w * 3f + seed);
                return Color.Lerp(brass, green, Mathf.SmoothStep(0.5f, 0.72f, pat)) * (0.8f + 0.4f * f.R());
            }
            bool Broken(float a, float r) => r > 0.28f * R && Mathf.Repeat(a - 3.6f + 0.25f * Mathf.Sin(r * 9f), Tau) < 0.9f;   // the piece out of the dial
            // the case's band
            int m = Mathf.RoundToInt(Tau * R * depth * perArea);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, w = (f.R() - 0.5f) * depth;
                Vector3 o = wx * Mathf.Cos(a) + wy * Mathf.Sin(a);
                f.Put(At(Mathf.Cos(a) * R, Mathf.Sin(a) * R, w) + o * 0.01f, o, Metal(Mathf.Cos(a), Mathf.Sin(a), w), rad * (0.7f + 0.5f * f.R()));
            }
            // the bezel, the dial (less its broken piece), the marks and the hands
            m = Mathf.RoundToInt(3.1416f * R * R * perArea);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, r = Mathf.Sqrt(f.R()) * R, u = Mathf.Cos(a) * r, v = Mathf.Sin(a) * r;
                if (r > 0.9f * R)
                {
                    f.Put(At(u, v, depth * 0.5f), wz + (wx * Mathf.Cos(a) + wy * Mathf.Sin(a)) * 0.4f, Metal(u, v, 1f) * 1.1f, rad * (0.7f + 0.5f * f.R()));
                    continue;
                }
                if (Broken(a, r)) continue;
                float hour = Mathf.Repeat(a / Tau * 12f + 0.5f, 1f) - 0.5f;
                bool mark = (r > 0.7f * R && r < 0.82f * R && Mathf.Abs(hour) * Tau / 12f * r < 0.03f * R)
                    || (r > 0.84f * R && r < 0.87f * R && Mathf.Abs(Mathf.Repeat(a / Tau * 60f + 0.5f, 1f) - 0.5f) < 0.12f);
                Color col = mark ? ink : dial * (0.82f + 0.25f * f.R()) * (1f - 0.25f * Mathf.PerlinNoise(u * 3f - seed, v * 3f));
                f.Put(At(u, v, depth * 0.36f), wz, col, rad * (0.6f + 0.4f * f.R()));
            }
            float ah = f.R() * Tau, am = f.R() * Tau;
            foreach (var (ang, len, wid) in new[] { (ah, 0.45f * R, 0.04f * R), (am, 0.72f * R, 0.025f * R) })
            {
                int mh = Mathf.RoundToInt(len * wid * 2f * perArea * 2f) + 20;
                for (int j = 0; j < mh; j++)
                {
                    float t = f.R(), s = f.S() * wid * (1f - 0.7f * t);
                    float u = Mathf.Cos(ang) * t * len - Mathf.Sin(ang) * s, v = Mathf.Sin(ang) * t * len + Mathf.Cos(ang) * s;
                    f.Put(At(u, v, depth * 0.4f), wz, steel * (0.8f + 0.4f * f.R()), rad * 0.45f);
                }
            }
            // inside, under the broken piece: two small gears and the hairspring, glowing faintly
            for (int g = 0; g < 3; g++)
            {
                float ga = 3.6f + 0.45f + (g - 1) * 0.3f, gd = R * (0.45f + 0.15f * g), gr = R * (0.16f - 0.03f * g);
                float gu = Mathf.Cos(ga) * gd, gv = Mathf.Sin(ga) * gd;
                int teeth = 10 + 4 * g, mg = Mathf.RoundToInt(3.1416f * gr * gr * perArea * 1.6f) + 40;
                for (int j = 0; j < mg; j++)
                {
                    float a = f.R() * Tau, toothAt = Mathf.Repeat(a / Tau * teeth, 1f), rr = Mathf.Sqrt(f.R()) * gr * (toothAt > 0.5f ? 1.15f : 1f);
                    if (rr < gr * 0.3f && rr > gr * 0.12f && Mathf.Repeat(a / Tau * 4f, 1f) > 0.3f) continue;   // its spokes
                    float pat = Mathf.PerlinNoise(a + seed, rr * 9f);
                    f.Put(At(gu + Mathf.Cos(a) * rr, gv + Mathf.Sin(a) * rr, -depth * 0.1f + g * 0.012f), wz, Color.Lerp(brass, green, pat) * 1.1f, rad * 0.5f, pat > 0.55f ? 0.35f : 0.08f);
                }
            }
            float su = Mathf.Cos(3.6f + 0.45f) * R * 0.6f, sv = Mathf.Sin(3.6f + 0.45f) * R * 0.6f;
            for (int j = 0; j < 900; j++)
            {
                float t = f.R(), a = t * Tau * 6f, rr = R * (0.02f + 0.13f * t);
                f.Put(At(su + Mathf.Cos(a) * rr, sv + Mathf.Sin(a) * rr, depth * 0.05f), wz, new Color(0.62f, 0.86f, 0.72f), rad * 0.3f, 0.45f);
            }
            // the glass: a few faint glints over the dial, cracked across in jagged lines
            m = Mathf.RoundToInt(3.1416f * R * R * perArea * 0.08f);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, r = Mathf.Sqrt(f.R()) * 0.88f * R;
                f.Put(At(Mathf.Cos(a) * r, Mathf.Sin(a) * r, depth * 0.5f + 0.02f), wz, new Color(0.55f, 0.62f, 0.62f), rad * 0.4f, 0.05f);
            }
            Vector2 hit = new Vector2(f.S(), f.S()) * (0.3f * R);
            for (int k = 0; k < 5; k++)
            {
                float a = f.R() * Tau;
                Vector2 at = hit;
                for (int j = 0; j < 90; j++)
                {
                    a += f.S() * 0.35f;
                    at += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (R * 0.015f);
                    if (at.magnitude > 0.88f * R) break;
                    f.Put(At(at.x, at.y, depth * 0.5f + 0.025f), wz, new Color(0.80f, 0.90f, 0.88f), rad * 0.32f, 0.22f);
                }
            }
            // the lid, open behind it on its hinge (at its left), and the crown and ring at its top
            float open = f.U(1.9f, 2.2f), co = Mathf.Cos(open), so = Mathf.Sin(open);
            m = Mathf.RoundToInt(3.1416f * R * R * perArea * 0.8f);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, r = Mathf.Sqrt(f.R()) * R, u = Mathf.Cos(a) * r, v = Mathf.Sin(a) * r, du = u + R;
                Vector3 pos = c + wx * (-R + du * co) + wy * v + wz * (depth * 0.5f + du * so);
                Vector3 nrm = wz * co - wx * so;
                float engr = 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(r * 26f));
                f.Put(pos, nrm, Metal(u, v, 2f) * engr, rad * (0.7f + 0.5f * f.R()));
            }
            Vector3 top = At(0f, R * 1.02f, 0f);
            Tube(f, top, top + wy * (0.12f * R), 0.07f * R, 0.07f * R, brass, brass, perArea);
            for (int j = 0; j < 500; j++)
            {
                float a = f.R() * Tau, b = f.R() * Tau, rr = 0.14f * R, tr = 0.018f * R;
                Vector3 ringC = top + wy * (0.26f * R);
                Vector3 o = wy * Mathf.Cos(a) + wx * Mathf.Sin(a);
                f.Put(ringC + o * (rr + tr * Mathf.Cos(b)) + wz * (tr * Mathf.Sin(b)), o, Metal(a, b, 3f), rad * 0.5f);
            }
            return f.World(At(su, sv, depth * 0.1f));
        }

        /// <summary>
        /// The leafcutters' heart: their fungus garden, a big grey-white sponge of fungus filling the chamber, holed and
        /// ridged all over, dotted with clusters of the small swollen tips the ants eat, which glow a faint yellow-green;
        /// round its foot the pieces of leaf it grows on, fresh green, browning further out. size is its width (about
        /// 3.4). Returns where its light is (the world).
        /// </summary>
        public static Vector3 HeartGarden(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float rad = 0.62f / Mathf.Sqrt(perArea), R = size * 0.5f, H = size * 0.42f, seed = f.R() * 40f;
            Color sponge = new Color(0.62f, 0.62f, 0.54f), hollow = new Color(0.13f, 0.13f, 0.10f), knob = new Color(0.84f, 0.92f, 0.50f);
            Vector3 Surface(Vector3 d)
            {
                float lump = 1f + 0.16f * Mathf.Sin(d.x * 4.1f + seed) * Mathf.Sin(d.z * 3.7f - seed) + 0.08f * Mathf.Sin(d.y * 9f + d.x * 5f);
                return new Vector3(d.x * R, d.y * H, d.z * R) * lump;
            }
            int m = Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(R, H, R) * 0.6f * perArea);
            for (int j = 0; j < m; j++)
            {
                Vector3 d = InBall(pen, out _).normalized;
                if (d.y < 0f) d.y = -d.y * 0.15f;   // a dome on the floor
                Vector3 pos = Surface(d);
                float cell = Mathf.Abs(Mathf.Sin(pos.x * 23f + seed) + Mathf.Sin(pos.y * 27f - pos.z * 9f) + Mathf.Sin(pos.z * 25f + pos.x * 7f)) / 3f;
                bool hole = cell < 0.16f;
                f.Put(pos - (hole ? d * 0.025f : Vector3.zero), new Vector3(d.x / R, d.y / H, d.z / R), (hole ? hollow : sponge * (0.7f + 0.5f * cell)) * (0.85f + 0.3f * f.R()), rad * (0.65f + 0.5f * f.R()));
            }
            int clusters = 28 + (int)(f.R() * 14f);
            for (int k = 0; k < clusters; k++)
            {
                Vector3 d = InBall(pen, out _).normalized;
                d.y = Mathf.Abs(d.y) * 0.9f + 0.1f;
                Vector3 c = Surface(d.normalized);
                int balls = 5 + (int)(f.R() * 9f);
                for (int b = 0; b < balls; b++)
                {
                    float br = f.U(0.025f, 0.05f);
                    Vector3 bc = c + new Vector3(f.S(), f.S() * 0.5f + 0.3f, f.S()) * 0.08f;
                    int mb = Mathf.Max(10, Mathf.RoundToInt(12.566f * br * br * perArea * 2f));
                    for (int j = 0; j < mb; j++)
                    {
                        Vector3 o = InBall(pen, out _).normalized;
                        f.Put(bc + o * br, o, knob * (0.8f + 0.4f * f.R()), Mathf.Min(rad * 0.4f, br * 0.4f), 0.4f + 0.3f * Mathf.Max(0f, o.y));
                    }
                }
            }
            for (int k = 0; k < 44; k++)
            {
                float a = f.R() * Tau, d0 = R * f.U(0.85f, 1.3f), ls = f.U(0.18f, 0.42f), rot = f.R() * Tau;
                Color leaf = Color.Lerp(new Color(0.30f, 0.46f, 0.10f), new Color(0.40f, 0.24f, 0.10f), Mathf.Clamp01(f.R() * 0.6f + (d0 > R * 1.08f ? 0.45f : 0f)));
                int ml = Mathf.RoundToInt(ls * ls * 0.6f * perArea) + 20;
                var c = new Vector3(Mathf.Cos(a) * d0, 0.06f, Mathf.Sin(a) * d0);
                for (int j = 0; j < ml; j++)
                {
                    float u = f.S(), v = f.S();
                    if (u * u + v * v > 1f || (u - 0.5f) * (u - 0.5f) + (v - 0.6f) * (v - 0.6f) < 0.25f) continue;   // a piece with a bite out
                    float x = u * ls * 0.5f, z = v * ls * 0.4f;
                    f.Put(c + new Vector3(x * Mathf.Cos(rot) - z * Mathf.Sin(rot), 0.01f * f.R(), x * Mathf.Sin(rot) + z * Mathf.Cos(rot)), Vector3.up, leaf * (0.85f + 0.3f * f.R()), rad * 0.6f);
                }
            }
            return f.World(new Vector3(0f, H * 0.6f, 0f));
        }

        /// <summary>
        /// The honeypot granary's heart: a root arching over the chamber like a rib of its dome, and hanging from it a
        /// cluster of honeypot ants, each a small dark head and thorax gripping the root, its legs round it, and below
        /// that its abdomen swollen into a clear globe of honey that glows: a still chandelier; under them a pile of
        /// gathered seeds. size is the arch's height (about 2.6). Returns where the cluster is (the world).
        /// </summary>
        public static Vector3 HeartHoney(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea, Vector3 face)
        {
            var f = new Frame(pen, p, n, ta, tb);
            // (the arch across the way in, so the ant coming in sees the hanging cluster from its side)
            float rad = 0.62f / Mathf.Sqrt(perArea), span = size * 0.75f, yaw = Mathf.Atan2(face.z, face.x) + Mathf.PI * 0.5f, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            Vector3 Arch(float t)
            {
                float x = Mathf.Lerp(-span, span, t), y = size * Mathf.Sin(t * Mathf.PI) + 0.08f * Mathf.Sin(t * 13f);
                return new Vector3(x * cs, y - 0.1f, x * sn) + new Vector3(-sn, 0f, cs) * (0.15f * Mathf.Sin(t * 4.1f));
            }
            var pts = new Vector3[22];
            for (int i = 0; i < pts.Length; i++) pts[i] = Arch(i / (pts.Length - 1f));
            Color bark = new Color(0.14f, 0.09f, 0.05f), barkTop = new Color(0.30f, 0.20f, 0.11f);
            for (int i = 1; i < pts.Length; i++)
            {
                float t = i / (pts.Length - 1f), r = Mathf.Lerp(0.15f, 0.09f, Mathf.Sin(t * Mathf.PI));
                Tube(f, pts[i - 1], pts[i], r, r, Color.Lerp(bark, barkTop, Mathf.Sin(t * Mathf.PI)), Color.Lerp(bark, barkTop, Mathf.Sin(t * Mathf.PI)), perArea);
            }
            Color body = new Color(0.16f, 0.09f, 0.04f), skin = new Color(0.66f, 0.44f, 0.08f), honey = new Color(1.0f, 0.86f, 0.32f);
            int ants = 13 + (int)(f.R() * 5f);
            for (int k = 0; k < ants; k++)
            {
                float t = f.U(0.3f, 0.7f);
                Vector3 grip = Arch(t) + Vector3.down * 0.1f + new Vector3(f.S(), 0f, f.S()) * 0.08f;
                float ab = f.U(0.2f, 0.36f), drop = f.R() < 0.3f ? f.U(0.12f, 0.4f) : 0f;   // some hang below the others
                Vector3 thorax = grip + Vector3.down * (0.06f + drop);
                if (drop > 0) Tube(f, grip, thorax, 0.006f, 0.006f, body, body, perArea);   // holding on to one above it
                for (int j = 0; j < 420; j++)
                {
                    Vector3 o = InBall(pen, out _).normalized;
                    bool head = j < 160;
                    Vector3 cc = head ? thorax + new Vector3(0.08f, 0.03f, 0f) : thorax;
                    f.Put(cc + Vector3.Scale(o, head ? new Vector3(0.07f, 0.06f, 0.06f) : new Vector3(0.1f, 0.07f, 0.07f)), o, body * (0.8f + 0.6f * f.R()) * (0.8f + 0.4f * Mathf.Max(0f, o.y)), rad * 0.35f);
                }
                for (int leg = 0; leg < 6; leg++)
                {
                    float la = leg * Tau / 6f + f.S() * 0.3f;
                    Vector3 foot = grip + new Vector3(Mathf.Cos(la) * 0.08f, 0.04f, Mathf.Sin(la) * 0.08f);
                    Tube(f, thorax, foot, 0.006f, 0.004f, body, body, perArea);
                }
                Drop(f, thorax + Vector3.down * (0.05f + ab * 1.05f), ab, 1.08f, skin, honey, perArea * 1.4f, 1f, 0.6f);
            }
            // the seeds under them: a low pile of grains and husks, pale and brown, a few striped
            float pr = 1.1f;
            for (int k = 0; k < 520; k++)
            {
                float a = f.R() * Tau, q = Mathf.Sqrt(f.R()), x = Mathf.Cos(a) * q * pr, z = Mathf.Sin(a) * q * pr;
                float y = 0.32f * (1f - q * q) + 0.03f, sl = f.U(0.06f, 0.14f), sw = sl * f.U(0.35f, 0.55f), rot = f.R() * Tau;
                int kind = (int)(f.R() * 3f);
                Color seedCol = kind == 0 ? new Color(0.62f, 0.52f, 0.34f) : kind == 1 ? new Color(0.34f, 0.22f, 0.12f) : new Color(0.12f, 0.10f, 0.08f);
                var c = new Vector3(x, y, z);
                Vector3 ex = new Vector3(Mathf.Cos(rot), f.S() * 0.3f, Mathf.Sin(rot)).normalized * sl * 0.5f;
                Vector3 ez = Vector3.Cross(ex.normalized, Vector3.up).normalized * sw * 0.5f;
                Vector3 ey = Vector3.up * sw * 0.35f;
                int ms = Mathf.Max(12, Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(sl * 0.5f, sw * 0.35f, sw * 0.5f) * perArea));
                for (int j = 0; j < ms; j++)
                {
                    Vector3 pos = OnEllipsoid(f, c, ex, ey, ez, out Vector3 nrm, out Vector3 qq);
                    bool stripe = kind == 2 && Mathf.Abs(Mathf.Sin(qq.z * 9f)) < 0.3f;
                    f.Put(pos, nrm, (stripe ? new Color(0.70f, 0.66f, 0.56f) : seedCol) * (0.75f + 0.45f * Mathf.Max(0f, qq.y)), rad * 0.45f);
                }
            }
            return f.World(Arch(0.5f) + Vector3.down * 0.45f);
        }

        /// <summary>
        /// The giant's relics' heart: one of the giant's buttons lying in the middle of the chamber, as wide as three or
        /// four ants are long: a thick disc of pale horn, a raised rim round its dished face, its four holes with a faint
        /// azure light pooled in them, worn and speckled; and beside it one of the giant's needles stuck slantwise in the
        /// floor, steel blue, rusted at its foot, a length of faded red thread through its eye. size is the button's
        /// width (about 3.4). Returns where its light is (the world).
        /// </summary>
        public static Vector3 HeartButton(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea, Vector3 face)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float rad = 0.62f / Mathf.Sqrt(perArea), R = size * 0.5f, T = size * 0.1f, seed = f.R() * 30f, spin = f.R() * Tau;
            Color horn = new Color(0.74f, 0.72f, 0.64f), worn = new Color(0.86f, 0.84f, 0.76f), speck = new Color(0.36f, 0.34f, 0.30f), deep = new Color(0.20f, 0.22f, 0.26f), glowC = new Color(0.50f, 0.78f, 1.0f);
            // tipped up toward the way in, resting on its far edge, so the ant coming in sees its face and holes (lying
            // flat it read from there as a white ellipse)
            Quaternion tilt = Quaternion.AngleAxis(22f, Vector3.Cross(Vector3.up, face));
            Vector3 lift = Vector3.up * (R * 0.21f);
            void BPut(Vector3 lp, Vector3 ln, Color c, float r, float g = 0f) => f.Put(tilt * lp + lift, tilt * ln, c, r, g);
            var holes = new Vector2[4];
            for (int i = 0; i < 4; i++) holes[i] = new Vector2(Mathf.Cos(spin + i * Tau / 4f), Mathf.Sin(spin + i * Tau / 4f)) * (0.3f * R);
            float hr = 0.11f * R;
            float Face(float r) => r < 0.8f * R ? T - 0.05f * size * (1f - (r / (0.8f * R)) * (r / (0.8f * R))) : T + 0.035f * size * Mathf.Sin(Mathf.PI * (r - 0.8f * R) / (0.2f * R));
            int m = Mathf.RoundToInt(3.1416f * R * R * 1.1f * perArea);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, r = Mathf.Sqrt(f.R()) * R, x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                bool inHole = false;
                foreach (Vector2 h in holes) if ((new Vector2(x, z) - h).sqrMagnitude < hr * hr) inHole = true;
                if (inHole) continue;
                float y = Face(r), e = 0.002f;
                float dy = (Face(r + e) - Face(Mathf.Max(0f, r - e))) / (2f * e);
                var nrm = new Vector3(-Mathf.Cos(a) * dy, 1f, -Mathf.Sin(a) * dy);
                bool isSpeck = Mathf.PerlinNoise(x * 9f + seed, z * 9f) > 0.72f || f.R() < 0.02f;
                Color col = isSpeck ? speck : Color.Lerp(horn, worn, Mathf.Clamp01((r - 0.8f * R) / (0.2f * R)) * 0.8f + 0.2f * Mathf.PerlinNoise(x * 2f, z * 2f + seed));
                BPut(new Vector3(x, y, z), nrm, col * (0.85f + 0.3f * f.R()), rad * (0.7f + 0.5f * f.R()));
            }
            m = Mathf.RoundToInt(Tau * R * T * perArea);
            for (int j = 0; j < m; j++)
            {
                float a = f.R() * Tau, y = f.R() * T;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                BPut(o * R + Vector3.up * y, o, horn * (0.6f + 0.25f * f.R()), rad * (0.7f + 0.5f * f.R()));
            }
            foreach (Vector2 h in holes)
            {
                int mh = Mathf.RoundToInt(Tau * hr * T * perArea) + 40;
                for (int j = 0; j < mh; j++)
                {
                    float a = f.R() * Tau, y = f.R() * T;
                    var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    BPut(new Vector3(h.x, y, h.y) + o * hr, -o, deep * (0.7f + 0.5f * y / T), rad * 0.6f);
                }
                for (int j = 0; j < 70; j++)
                {
                    float a = f.R() * Tau, r = Mathf.Sqrt(f.R()) * hr * 0.9f;
                    BPut(new Vector3(h.x + Mathf.Cos(a) * r, 0.03f, h.y + Mathf.Sin(a) * r), Vector3.up, glowC * 0.5f, rad * 0.5f, 0.5f * (1f - r / hr));
                }
            }
            // the needle: stuck slantwise in the floor beside it, its eye high up, a thread through it
            float na = Mathf.Atan2(-face.z, -face.x) + f.S() * 0.6f, L = size * 1.2f, nr = 0.016f * size;   // (beyond the button, seen behind it)
            var foot = new Vector3(Mathf.Cos(na), 0f, Mathf.Sin(na)) * (R * 1.35f);
            Vector3 axis = (Vector3.up + new Vector3(Mathf.Cos(na + 2.2f), 0f, Mathf.Sin(na + 2.2f)) * 0.45f).normalized;
            NestThingsGrowth.Perp(axis, out Vector3 nu, out Vector3 nv);
            Color steelC = new Color(0.34f, 0.40f, 0.48f), rust = new Color(0.40f, 0.22f, 0.11f);
            int mn = Mathf.RoundToInt(Tau * nr * L * perArea * 1.5f);
            for (int j = 0; j < mn; j++)
            {
                float t = f.R(), a = f.R() * Tau;
                bool eye = t > 0.86f && t < 0.95f && Mathf.Abs(Mathf.Sin(a)) > 0.6f;   // the eye, through it
                if (eye) continue;
                Vector3 o = nu * Mathf.Cos(a) + nv * Mathf.Sin(a);
                float rr = nr * (t > 0.97f ? Mathf.Sqrt((1f - t) / 0.03f) : 1f);
                float rusty = Mathf.SmoothStep(0.3f, 0.05f, t) * Mathf.PerlinNoise(t * 20f + seed, a);
                f.Put(foot + axis * (t * L - 0.25f) + o * rr, o, Color.Lerp(steelC, rust, rusty * 1.6f) * (0.8f + 0.4f * f.R()), Mathf.Min(rad * 0.6f, rr * 1.2f));
            }
            Vector3 eyeAt = foot + axis * (0.905f * L - 0.25f);
            var thread = new Vector3[9];
            for (int i = 0; i < thread.Length; i++)
            {
                float t = i / (thread.Length - 1f);
                thread[i] = eyeAt + nu * (0.35f * Mathf.Sin(t * Mathf.PI)) + Vector3.down * (t * (0.9f * L - 0.2f)) + nv * (0.2f * t);
            }
            Path(f, thread, 0.012f, 0.012f, new Color(0.55f, 0.18f, 0.16f), new Color(0.48f, 0.16f, 0.15f), perArea);
            return f.World(new Vector3(0f, T * 0.6f, 0f));
        }

        // ------------------------------------------------------------------------------- the amber nest (orange)

        /// <summary>
        /// Gold ball-stalks: three to eight thin dry stalks spread from one foot, each nodding a round head of tiny
        /// packed florets that glows a dim gold, paler at its crown (the amber nest's ball-stalks lining the way).
        /// size is the tallest stalk's height (0.7 to 2.2).
        /// </summary>
        public static void GoldBalls(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            int count = 3 + (int)(s.R() * 6f);
            Color stalk0 = new Color(0.20f, 0.15f, 0.08f), stalk1 = new Color(0.44f, 0.36f, 0.20f);
            Color head = new Color(0.52f, 0.30f, 0.07f), crown = new Color(0.70f, 0.46f, 0.14f);
            float spin = s.R() * Tau;
            for (int i = 0; i < count; i++)
            {
                float h = size * (i == 0 ? 1f : s.U(0.42f, 0.92f));
                float ang = spin + i * 2.39996f + s.S() * 0.4f, rad = size * 0.3f * Mathf.Sqrt((i + 0.5f) / count);
                var outw = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 b0 = outw * rad, lean = outw * s.U(0.05f, 0.2f) + new Vector3(s.S(), 0f, s.S()) * 0.05f;
                float rs = Mathf.Lerp(0.011f, 0.024f, Mathf.Clamp01(h / 2.2f)), rh = h * s.U(0.07f, 0.1f);
                // the stalk: a gentle curve up to the head, darker at its foot
                Vector3 prev = b0;
                for (int q = 1; q <= 7; q++)
                {
                    float f0 = (q - 1) / 7f, f1 = q / 7f;
                    Vector3 cur = b0 + Vector3.up * ((h - rh) * f1) + lean * (h * f1 * f1);
                    NestThingsGrowth.Tube(s, prev, cur, rs * (1.2f - 0.35f * f0), rs * (1.2f - 0.35f * f1), Color.Lerp(stalk0, stalk1, f0), Color.Lerp(stalk0, stalk1, f1));
                    prev = cur;
                }
                // the head: a ball of florets (their bumps in its light and dark), a little flattened
                Vector3 c = prev + Vector3.up * (rh * 0.8f);
                int m = s.Count(12.566f * rh * rh * 1.15f, 30);
                float glow = s.U(0.28f, 0.5f);
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, Vector3.right * rh, Vector3.up * (rh * 0.88f), Vector3.forward * rh, out Vector3 nrm, out Vector3 q);
                    float floret = 0.82f + 0.36f * Mathf.Abs(Mathf.Sin(q.x * 23f + q.z * 19f + q.y * 17f));
                    pos += nrm.normalized * (rh * 0.05f * (floret - 1f));
                    Color col = Color.Lerp(head, crown, Mathf.Max(0f, q.y)) * floret;
                    s.Put(pos, nrm, s.V(col, 0.08f), s.Rad(0.75f), glow * (0.65f + 0.35f * Mathf.Max(0f, q.y)));
                }
            }
        }

        /// <summary>
        /// Resin drops: two to six beads of amber resin on the floor, some run together, each a little teardrop
        /// (drawn up to a point where it dripped); its skin dark and glassy, small splats with gaps, and inside a honey
        /// light (fine bright splats, brightest in the middle) with a few dark specks caught in it. size is the
        /// cluster's width (0.4 to 1.1).
        /// </summary>
        public static void ResinDrops(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size) =>
            Drops(pen, p, n, ta, tb, size, new Color(0.30f, 0.13f, 0.03f), new Color(0.98f, 0.60f, 0.20f));

        /// <summary>The honey granary's honey drops: as the amber nest's resin, pale and lemon-gold, the light inside a
        /// clear yellow.</summary>
        public static void HoneyDrops(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size) =>
            Drops(pen, p, n, ta, tb, size, new Color(0.40f, 0.32f, 0.05f), new Color(1.0f, 0.90f, 0.30f));

        static void Drops(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, Color skin, Color core)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            int count = 2 + (int)(s.R() * 5f);
            Color speck = new Color(0.05f, 0.025f, 0.01f);
            for (int i = 0; i < count; i++)
            {
                float r = size * (i == 0 ? 0.28f : s.U(0.11f, 0.22f)), ht = r * s.U(1.0f, 1.45f);
                float ang = s.R() * Tau, off = i == 0 ? 0f : size * s.U(0.16f, 0.42f);
                var c = new Vector3(Mathf.Cos(ang) * off, ht * 0.5f, Mathf.Sin(ang) * off);
                // the skin: a teardrop, its top drawn up to a point; a glint along its crown
                int m = s.Count(NestThingsGrowth.EllipsoidArea(r, ht * 0.5f, r) * 0.55f, 20);
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, Vector3.right * r, Vector3.up * (ht * 0.5f), Vector3.forward * r, out Vector3 nrm, out Vector3 q);
                    if (q.y > 0f)
                    {
                        float pinch = q.y * q.y;
                        pos.x = c.x + (pos.x - c.x) * (1f - 0.55f * pinch);
                        pos.z = c.z + (pos.z - c.z) * (1f - 0.55f * pinch);
                        pos.y += ht * 0.35f * pinch;
                    }
                    Color col = skin * (0.75f + 0.6f * q.y * q.y);
                    s.Put(pos, nrm, s.V(col, 0.12f), s.Rad(0.42f), 0.1f + 0.15f * Mathf.Max(0f, q.y));
                }
                // the light inside, and what it caught
                int k = Mathf.RoundToInt(m * 0.75f);
                for (int j = 0; j < k; j++)
                {
                    Vector3 d = InBall(pen, out float f);
                    Vector3 pos = c + new Vector3(d.x * r, d.y * ht * 0.45f, d.z * r) * 0.85f;
                    bool caught = s.R() < 0.04f;
                    s.Put(pos, Vector3.up, caught ? speck : s.V(core * (1f - 0.45f * f), 0.08f), s.Rad(caught ? 0.5f : 0.38f), caught ? 0f : 1f - 0.4f * f);
                }
            }
        }

        /// <summary>
        /// A patch of wax comb on the floor: hexagonal cells in rows, their thin walls pale ochre wax; some cells
        /// brimming with honey that glows a soft amber, some capped with a pale lid, some empty and dark, the patch's
        /// edge ragged. Flat: SolidThings drapes it over the floor. size is the patch's width (1 to 2.4).
        /// </summary>
        public static void WaxComb(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float cell = s.U(0.34f, 0.44f), wall = cell * s.U(0.34f, 0.46f), R = size * 0.5f;
            Color wax = new Color(0.34f, 0.22f, 0.09f), cap = new Color(0.66f, 0.54f, 0.34f), honey = new Color(0.80f, 0.42f, 0.08f), empty = new Color(0.07f, 0.04f, 0.02f);
            float seed = s.R() * 50f;
            // cells: a hexagonal grid (flat to flat = cell), kept inside a ragged round edge
            float dx = cell, dz = cell * 0.8660254f;
            int cols = Mathf.CeilToInt(R / dx) + 1, rows = Mathf.CeilToInt(R / dz) + 1;
            float want = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                float fill = pass == 0 ? 1f : Mathf.Clamp((s.Left - 100f) / Mathf.Max(1f, want), 0.15f, 1f), grow = 1f / Mathf.Sqrt(fill);
                for (int rz = -rows; rz <= rows; rz++)
                for (int cx = -cols; cx <= cols; cx++)
                {
                    float x = cx * dx + (rz & 1) * dx * 0.5f, z = rz * dz;
                    float edge = R * (0.8f + 0.25f * Mathf.Sin(Mathf.Atan2(z, x) * 3f + seed) + 0.1f * Mathf.Sin(Mathf.Atan2(z, x) * 7f - seed));
                    if (x * x + z * z > edge * edge) continue;
                    float hw = wall * (1f - 0.35f * Mathf.Sqrt(x * x + z * z) / Mathf.Max(R, 0.01f));   // lower toward the ragged edge
                    if (pass == 0) { want += s.Count(cell * cell * 0.87f + 6f * cell * 0.577f * hw * 2f, 0); continue; }
                    // which: honey, capped or empty (honey gathers toward the middle)
                    float pick = Mathf.Repeat(Mathf.Sin(cx * 12.9898f + rz * 78.233f + seed) * 43758.5f, 1f);
                    bool middle = x * x + z * z < R * R * 0.35f;
                    int kind = pick < (middle ? 0.55f : 0.3f) ? 0 : pick < 0.75f ? 1 : 2;
                    // the walls: six thin wax sheets round the cell (shared walls drawn from both sides)
                    float rc = cell * 0.57735f;   // centre to corner
                    for (int e = 0; e < 6; e++)
                    {
                        float a0 = e * Tau / 6f + Tau / 12f, a1 = a0 + Tau / 6f;
                        var c0 = new Vector3(x + Mathf.Cos(a0) * rc, 0f, z + Mathf.Sin(a0) * rc);
                        var c1 = new Vector3(x + Mathf.Cos(a1) * rc, 0f, z + Mathf.Sin(a1) * rc);
                        var mid = (c0 + c1) * 0.5f;
                        var outw = new Vector3(mid.x - x, 0f, mid.z - z).normalized;
                        int w = Mathf.Max(3, Mathf.RoundToInt(s.Count(rc * hw, 3) * fill * 1.1f));
                        for (int j = 0; j < w; j++)
                        {
                            float t = s.R(), up = s.R() * hw;
                            Vector3 pos = Vector3.Lerp(c0, c1, t) + Vector3.up * up;
                            Vector3 nrm = up > hw * 0.85f ? Vector3.up : (s.R() < 0.5f ? outw : -outw);
                            s.Put(pos, nrm, s.V(wax * (0.75f + 0.6f * up / hw), 0.1f), s.Rad(0.45f) * grow);
                        }
                    }
                    // the cell's floor: honey brimming (glowing), a pale cap, or the dark bottom
                    float level = kind == 0 ? hw * s.U(0.55f, 0.85f) : kind == 1 ? hw * 0.95f : 0.02f;
                    Color fillCol = kind == 0 ? honey : kind == 1 ? cap : empty;
                    int b = Mathf.Max(3, Mathf.RoundToInt(s.Count(cell * cell * 0.75f, 3) * fill));
                    for (int j = 0; j < b; j++)
                    {
                        float ang = s.R() * Tau, rr = cell * 0.36f * Mathf.Sqrt(s.R());   // inset: a dark ring between it and the walls
                        var pos = new Vector3(x + Mathf.Cos(ang) * rr, level + (kind == 1 ? 0.016f * (1f - rr / (cell * 0.36f)) : 0f), z + Mathf.Sin(ang) * rr);
                        s.Put(pos, Vector3.up, s.V(fillCol, 0.08f), s.Rad(0.8f) * grow, kind == 0 ? s.U(0.55f, 0.85f) : 0f);
                    }
                }
            }
        }

        /// <summary>
        /// Amber shards: four to nine hexagonal prisms of amber with pointed tips, leaning out from one foot sunk in the
        /// floor, a crystal cluster grown of resin; their skins dark and glassy (small splats with gaps, brighter along
        /// the edges), their insides lit honey-gold, brightest low in each prism. size is the tallest prism's height
        /// (0.8 to 3).
        /// </summary>
        public static void AmberShards(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            int count = 4 + (int)(pen.R() * 6f);
            float perArea = s.Count(1f, 1);
            // within one thing's splats: about 0.53 · count · size² · perArea at full density
            float density = Mathf.Clamp(8000f / (0.53f * count * size * size * perArea), 0.08f, 0.6f);
            Shards(pen, (lp, ln, c, r, g) => s.Put(lp, ln, c, r, g), size, count, perArea, density);
        }

        /// <summary>The prisms of an amber cluster (AmberShards, and the big one at the amber nest's heart): put
        /// draws a local splat; perArea splats per unit² (thinned: the skin keeps gaps); density thins it further.</summary>
        static void Shards(ThingPen pen, System.Action<Vector3, Vector3, Color, float, float> put, float size, int count, float perArea, float density)
        {
            Color skin = new Color(0.36f, 0.16f, 0.035f), edge = new Color(0.72f, 0.42f, 0.12f), core = new Color(0.98f, 0.62f, 0.22f);
            float spin = pen.R() * Tau, radius = 0.62f / Mathf.Sqrt(Mathf.Max(1f, perArea));
            for (int i = 0; i < count; i++)
            {
                float L = size * (i == 0 ? 1f : 0.4f + 0.55f * pen.R()), w = L * (0.11f + 0.07f * pen.R());
                float ang = spin + i * 2.39996f, tilt = i == 0 ? 0.08f + 0.12f * pen.R() : 0.25f + 0.45f * pen.R();
                var outw = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 axis = (Vector3.up + outw * tilt).normalized, foot = outw * (i == 0 ? 0f : size * 0.08f) - axis * (L * 0.12f);
                NestThingsGrowth.Perp(axis, out Vector3 u, out Vector3 v);
                float tipAt = L * (0.72f + 0.1f * pen.R()), turn = pen.R() * Tau;
                // the six faces, then the tip's six facets: a hexagon of corners at radius w
                float faceArea = w * tipAt + w * (L - tipAt) * 0.6f;
                int m = Mathf.Max(12, Mathf.RoundToInt(faceArea * perArea * density));
                for (int f = 0; f < 6; f++)
                {
                    float a0 = turn + f * Tau / 6f, a1 = a0 + Tau / 6f;
                    Vector3 k0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), k1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                    Vector3 fn = (k0 + k1).normalized;
                    for (int j = 0; j < m; j++)
                    {
                        float t = pen.R(), along = pen.R() * L;
                        float shrink = along > tipAt ? 1f - (along - tipAt) / (L - tipAt) : 1f;
                        Vector3 pos = foot + axis * along + Vector3.Lerp(k0, k1, t) * (w * shrink);
                        float nearEdge = Mathf.Min(t, 1f - t);
                        Vector3 nrm = along > tipAt ? (fn + axis * 0.8f).normalized : fn;
                        Color col = Color.Lerp(edge, skin, Mathf.Clamp01(nearEdge * 6f)) * (0.85f + 0.3f * pen.R());
                        put(pos, nrm, col, radius * (0.35f + 0.25f * pen.R()), 0.15f + 0.25f * (1f - Mathf.Clamp01(nearEdge * 6f)));
                    }
                }
                // the light inside: along the axis, brightest low down, fading toward the tip
                int k = Mathf.RoundToInt(m * 2.2f);
                for (int j = 0; j < k; j++)
                {
                    float along = Mathf.Pow(pen.R(), 1.3f) * tipAt, rr = w * 0.7f * Mathf.Sqrt(pen.R()), a = pen.R() * Tau;
                    Vector3 pos = foot + axis * along + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rr;
                    float f = along / L;
                    put(pos, axis, core * (1.05f - 0.5f * f), radius * (0.3f + 0.25f * pen.R()), 1f - 0.45f * f);
                }
            }
        }

        // ------------------------------------------------------------------------------- the kiln temple (red)

        /// <summary>
        /// Ember cracks: the floor split by a few branching cracks with live embers deep in them, as in a kiln's floor:
        /// each crack a thin line glowing ember red (widest in its middle), edged with charred clay that thins out away
        /// from it, a few pale flakes of ash beside it. Flat: SolidThings drapes it over the floor. size is the
        /// network's width (1.2 to 3).
        /// </summary>
        public static void EmberCracks(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color ember = new Color(1.0f, 0.24f, 0.10f), hot = new Color(1.0f, 0.52f, 0.30f), char0 = new Color(0.13f, 0.06f, 0.05f), ash = new Color(0.62f, 0.56f, 0.52f);
            int branches = 3 + (int)(s.R() * 3f);
            float spin = s.R() * Tau;
            for (int i = 0; i < branches; i++)
                Crack(s, Vector3.zero, spin + i * Tau / branches + s.S() * 0.5f, size * s.U(0.35f, 0.55f), 0.022f + 0.012f * s.R(), 0, ember, hot, char0, ash);
        }

        static void Crack(NestThingsGrowth.Spot s, Vector3 from, float dir, float length, float width, int depth, Color ember, Color hot, Color char0, Color ash)
        {
            const float step = 0.03f;
            int steps = Mathf.Max(3, Mathf.RoundToInt(length / step));
            Vector3 at = from;
            for (int i = 0; i < steps; i++)
            {
                float u = i / (float)steps;
                dir += s.S() * 0.35f;
                at += new Vector3(Mathf.Cos(dir), 0f, Mathf.Sin(dir)) * step;
                float w = width * Mathf.Sin(Mathf.PI * Mathf.Clamp01(0.15f + 0.85f * u)) * (0.7f + 0.6f * s.R());
                var side = new Vector3(-Mathf.Sin(dir), 0f, Mathf.Cos(dir));
                // the embers in the crack, hottest in its middle
                // (lifted clear of the floor's own points, which lie a little above the ground and would hide them)
                s.Put(at + Vector3.up * 0.018f, Vector3.up, s.V(Color.Lerp(ember, hot, s.R() * 0.6f * (1f - u)), 0.1f), Mathf.Max(w * 1.15f, s.Thin * 1.3f), s.U(0.8f, 1f));
                // charred clay either side, thinning out
                for (int k = 0; k < 3; k++)
                {
                    float off = w * (1.2f + 2.6f * s.R()) * (s.R() < 0.5f ? -1f : 1f);
                    s.Put(at + side * off + Vector3.up * 0.012f, Vector3.up, s.V(char0, 0.2f), s.Rad(0.55f));
                }
                if (s.R() < 0.06f) s.Put(at + side * (w * s.U(3f, 6f) * (s.R() < 0.5f ? -1 : 1)) + Vector3.up * 0.01f, Vector3.up, s.V(ash, 0.15f), s.Rad(0.7f));
                if (depth < 2 && s.R() < 0.035f)
                    Crack(s, at, dir + (s.R() < 0.5f ? 1f : -1f) * s.U(0.5f, 1.1f), length * (1f - u) * s.U(0.35f, 0.7f), width * 0.6f, depth + 1, ember, hot, char0, ash);
            }
        }

        /// <summary>
        /// A clay chimney, as termites raise and the temple's builders copied: a tapering tube of terracotta with a
        /// spiral ridge winding up it and a few small holes glowing faintly, its open mouth glowing with the embers
        /// inside; the clay paler where it dried at the top. size is its height (0.8 to 2.4). Blocks the ants.
        /// </summary>
        public static void ClayChimney(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float rb = size * s.U(0.2f, 0.26f), rt = rb * s.U(0.42f, 0.58f), turns = s.U(1.5f, 3f), seed = s.R() * 40f;
            Color clay = new Color(0.62f, 0.30f, 0.22f), dry = new Color(0.84f, 0.56f, 0.46f), ember = new Color(1.0f, 0.28f, 0.12f);
            var lean = new Vector3(s.S(), 0f, s.S()) * 0.06f;
            float area = Tau * (rb + rt) * 0.5f * size, fill = Mathf.Clamp((s.Left - 600f) / Mathf.Max(1f, s.Count(area, 1) * 1.1f), 0.2f, 1f), grow = 1f / Mathf.Sqrt(fill);
            int m = Mathf.RoundToInt(s.Count(area, 40) * fill);
            int holes = 2 + (int)(s.R() * 3f);
            var holeAt = new Vector2[holes];
            for (int h = 0; h < holes; h++) holeAt[h] = new Vector2(s.U(0.2f, 0.75f), s.R() * Tau);
            for (int j = 0; j < m; j++)
            {
                float t = s.R(), a = s.R() * Tau, r = Mathf.Lerp(rb, rt, t);
                // the spiral ridge, and the lumps of the clay
                float ridge = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a - t * turns * Tau)), 12f);
                float lump = 0.06f * Mathf.Sin(a * 5f + t * 13f + seed) * Mathf.Sin(t * 9f - a * 3f);
                r *= 1f + 0.16f * ridge + lump;
                bool hole = false;
                foreach (Vector2 hh in holeAt)
                {
                    float da = Mathf.DeltaAngle(a * Mathf.Rad2Deg, hh.y * Mathf.Rad2Deg) * Mathf.Deg2Rad * r, dt = (t - hh.x) * size;
                    if (da * da + dt * dt < 0.045f * 0.045f) hole = true;
                }
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 pos = o * r + Vector3.up * (t * size) + lean * (t * t * size);
                if (hole) { s.Put(pos - o * 0.02f, o, s.V(ember * 0.85f, 0.1f), s.Rad(0.55f), 0.8f); continue; }
                Color col = Color.Lerp(clay, dry, Mathf.SmoothStep(0.55f, 1f, t)) * (0.9f + 0.25f * ridge);
                s.Put(pos, o + Vector3.up * ((rb - rt) / size), s.V(col, 0.1f), s.Rad() * grow);
            }
            // the mouth: a lip, and the embers glowing down inside it
            Vector3 top = Vector3.up * size + lean * size;
            int lip = s.Count(Tau * rt * 0.05f, 20), inside = s.Count(3.1416f * rt * rt * 1.4f, 30);
            for (int j = 0; j < lip; j++)
            {
                float a = s.R() * Tau;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                s.Put(top + o * (rt * s.U(0.85f, 1.1f)) + Vector3.up * s.U(-0.01f, 0.02f), Vector3.up + o * 0.4f, s.V(dry, 0.1f), s.Rad(0.7f));
            }
            for (int j = 0; j < inside; j++)
            {
                float a = s.R() * Tau, d = s.R();
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 pos = top - Vector3.up * (d * rt * 1.6f) + o * (rt * 0.85f * (1f - 0.3f * d));
                s.Put(pos, -o + Vector3.up * 0.3f, s.V(Color.Lerp(ember, new Color(1f, 0.6f, 0.3f), 1f - d), 0.1f), s.Rad(0.6f), 0.95f - 0.5f * d);
            }
        }

        /// <summary>
        /// Clay cells: a cluster of hexagonal clay cells standing a little up from the floor, like the temple's but
        /// small, the walls tallest at the cluster's middle, rough and lumpy; deep in a few of them a faint ember glow.
        /// size is the cluster's width (0.9 to 2).
        /// </summary>
        public static void HexCells(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float cell = s.U(0.42f, 0.58f), R = size * 0.5f, tall = cell * s.U(0.55f, 0.9f), seed = s.R() * 30f;
            Color clay = new Color(0.64f, 0.32f, 0.24f), rim = new Color(0.86f, 0.56f, 0.46f), deep = new Color(0.10f, 0.045f, 0.035f), ember = new Color(1.0f, 0.26f, 0.12f);
            float dx = cell, dz = cell * 0.8660254f;
            int cols = Mathf.CeilToInt(R / dx) + 1, rows = Mathf.CeilToInt(R / dz) + 1;
            int cells = 0;
            for (int rz = -rows; rz <= rows; rz++)
            for (int cx = -cols; cx <= cols; cx++)
            {
                float x = cx * dx + (rz & 1) * dx * 0.5f, z = rz * dz;
                if (x * x + z * z <= R * R) cells++;
            }
            float per = Mathf.Max(60f, (s.Left - 200f) / Mathf.Max(1, cells));
            for (int rz = -rows; rz <= rows; rz++)
            for (int cx = -cols; cx <= cols; cx++)
            {
                float x = cx * dx + (rz & 1) * dx * 0.5f, z = rz * dz, d = Mathf.Sqrt(x * x + z * z);
                if (d > R) continue;
                float hw = tall * (1f - 0.55f * d / Mathf.Max(R, 0.01f)) * (0.85f + 0.3f * Mathf.Abs(Mathf.Sin(cx * 3.1f + rz * 1.7f + seed)));
                bool glows = Mathf.Repeat(Mathf.Sin(cx * 12.9898f + rz * 78.233f + seed) * 43758.5f, 1f) < 0.5f;
                float rc = cell * 0.57735f;
                // the walls (rough), their tops paler
                int wallSplats = Mathf.RoundToInt(per * 0.75f), floorSplats = Mathf.RoundToInt(per * 0.25f);
                for (int j = 0; j < wallSplats; j++)
                {
                    int e = (int)(s.R() * 6f) % 6;
                    float a0 = e * Tau / 6f + Tau / 12f, a1 = a0 + Tau / 6f, t = s.R(), up = s.R() * hw;
                    var c0 = new Vector3(x + Mathf.Cos(a0) * rc, 0f, z + Mathf.Sin(a0) * rc);
                    var c1 = new Vector3(x + Mathf.Cos(a1) * rc, 0f, z + Mathf.Sin(a1) * rc);
                    var mid = (c0 + c1) * 0.5f;
                    var outw = new Vector3(mid.x - x, 0f, mid.z - z).normalized;
                    bool topEdge = up > hw * 0.88f;
                    Vector3 nrm = topEdge ? Vector3.up : (s.R() < 0.5f ? outw : -outw);
                    float rough = 0.012f * Mathf.Sin(up * 40f + t * 17f + seed);
                    Color col = topEdge ? rim : Color.Lerp(deep * 2.5f, clay, Mathf.Clamp01(up / hw + 0.2f));
                    s.Put(Vector3.Lerp(c0, c1, t) + outw * rough + Vector3.up * up, nrm, s.V(col, 0.12f), s.Rad(0.9f));
                }
                for (int j = 0; j < floorSplats; j++)
                {
                    float ang = s.R() * Tau, rr = cell * 0.44f * Mathf.Sqrt(s.R());
                    var pos = new Vector3(x + Mathf.Cos(ang) * rr, 0.02f, z + Mathf.Sin(ang) * rr);
                    s.Put(pos, Vector3.up, s.V(glows ? Color.Lerp(deep, ember, 0.75f) : deep, 0.15f), s.Rad(0.8f), glows ? s.U(0.65f, 0.95f) : 0f);
                }
            }
        }

        /// <summary>
        /// Pot shards: two to five broken pieces of a fired clay pot lying on the floor, each a curved piece of its wall
        /// with a broken edge, terracotta outside with incised bands, darker inside, paler where it broke. Flat:
        /// SolidThings drapes it over the floor. size is the largest piece's width (0.5 to 1.4).
        /// </summary>
        public static void PotShards(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color outside = new Color(0.72f, 0.36f, 0.26f), inside = new Color(0.48f, 0.25f, 0.18f), broken = new Color(0.90f, 0.64f, 0.54f), band = new Color(0.34f, 0.16f, 0.10f);
            int count = 2 + (int)(s.R() * 4f);
            for (int i = 0; i < count; i++)
            {
                float w = size * (i == 0 ? 1f : s.U(0.35f, 0.75f)), curve = s.U(0.8f, 1.6f) / w, ang = s.R() * Tau, off = i == 0 ? 0f : size * s.U(0.4f, 0.9f);
                var c = new Vector3(Mathf.Cos(ang) * off, 0f, Mathf.Sin(ang) * off);
                float yaw = s.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
                bool up = s.R() < 0.6f;   // lying on its back (inside up) or on its face
                float seed = s.R() * 20f, bands = s.U(9f, 16f);
                int m = s.Count(w * w * 0.55f * 2f, 30);
                for (int j = 0; j < m; j++)
                {
                    // a piece of a cylinder's wall: u round it, v along it; the outline broken (wobbly)
                    float u = s.S() * 0.5f, v = s.S() * 0.5f;
                    float outline = 0.42f + 0.08f * Mathf.Sin(Mathf.Atan2(v, u) * 5f + seed) + 0.05f * Mathf.Sin(Mathf.Atan2(v, u) * 11f - seed);
                    float rr = Mathf.Sqrt(u * u + v * v);
                    if (rr > outline) continue;
                    float x = u * w, z = v * w, y = (1f - Mathf.Cos(x * curve)) / curve;
                    if (!up) y = 0.06f * w - y;
                    var lpos = new Vector3(x * cs - z * sn, y + 0.015f, x * sn + z * cs);
                    var nrm = new Vector3(-Mathf.Sin(x * curve) * cs, Mathf.Cos(x * curve), -Mathf.Sin(x * curve) * sn) * (up ? 1f : -1f);
                    bool edge = rr > outline - 0.035f;
                    Color col = edge ? broken : up ? inside : Color.Lerp(outside, band, Mathf.Pow(Mathf.Max(0f, Mathf.Sin(v * bands * Tau / 1f)), 18f));
                    s.Put(c + lpos, up ? nrm : -nrm, s.V(col, 0.12f), s.Rad(0.8f));
                }
            }
        }

        /// <summary>
        /// The amber shrine at the kiln temple's heart: a rough block of amber, wider at its foot, on a low ring of clay;
        /// its skin dark and glassy (small splats with gaps), lit from within, and inside it the ancestor, a winged ant
        /// (antPoints: its model's points, centred, its length along +z), wings spread, in amber shadow. size is the
        /// block's height (about 3.4). Draws with no cap (it is big and seen close).
        /// </summary>
        public static void Shrine(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, Vector3[] antPoints, float perArea)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float hx = size * 0.36f, hy = size * 0.5f, hz = size * 0.32f;
            Color skin = new Color(0.30f, 0.09f, 0.03f), edge = new Color(0.68f, 0.26f, 0.10f), core = new Color(0.98f, 0.42f, 0.18f), clay = new Color(0.40f, 0.18f, 0.13f), clayTop = new Color(0.60f, 0.32f, 0.24f);
            var c = new Vector3(0f, hy * 0.95f, 0f);
            float rad = 0.62f / Mathf.Sqrt(perArea);
            // the skin: a lumpy ellipsoid, wider low down; small splats, so the light inside shows between them
            int m = Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(hx, hy, hz) * perArea * 0.55f);
            for (int j = 0; j < m; j++)
            {
                Vector3 d = InBall(pen, out float _).normalized;
                float lump = 1f + 0.1f * Mathf.Sin(d.x * 9f + 1.3f) * Mathf.Sin(d.y * 7f - 0.4f) + 0.07f * Mathf.Sin(d.z * 11f);
                float widen = 1f + 0.22f * Mathf.Clamp01(-d.y * 1.6f);
                var pos = c + new Vector3(d.x * hx * widen, d.y * hy, d.z * hz * widen) * lump;
                var nrm = new Vector3(d.x / hx, d.y / hy, d.z / hz);
                float rimLight = Mathf.Pow(1f - Mathf.Abs(d.y), 3f);
                f.Put(pos, nrm, Color.Lerp(skin, edge, 0.35f * rimLight) * (0.8f + 0.4f * f.R()), rad * (0.35f + 0.25f * f.R()), 0.12f + 0.2f * rimLight);
            }
            // the light inside: a core low in the middle, round the ancestor, fading out well before the skin (a block
            // filled with light all through read as a ball of glow: no block, no ancestor)
            int k = Mathf.RoundToInt(m * 0.7f);
            for (int j = 0; j < k; j++)
            {
                Vector3 d = InBall(pen, out float fr);
                var pos = c + new Vector3(d.x * hx, d.y * hy * 0.8f - hy * 0.12f, d.z * hz) * 0.72f;
                float core2 = (1f - fr) * (1f - fr);
                f.Put(pos, Vector3.up, core * (0.6f + 0.5f * core2), rad * (0.3f + 0.25f * f.R()), 0.2f + 0.65f * core2);
            }
            // the ancestor: its points in amber shadow (darker than the light round it, a faint glow of its own)
            if (antPoints != null && antPoints.Length > 0)
            {
                float yaw = f.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw), scale = size * 0.5f;
                int stepAnt = Mathf.Max(1, antPoints.Length / 14000);
                for (int j = 0; j < antPoints.Length; j += stepAnt)
                {
                    Vector3 a = antPoints[j] * scale;
                    // tipped a little, as if it settled while the resin was soft
                    a = new Vector3(a.x, a.y * 0.96f + a.z * 0.28f, a.z * 0.96f - a.y * 0.28f);
                    var pos = c + new Vector3(a.x * cs - a.z * sn, a.y + hy * 0.12f, a.x * sn + a.z * cs);
                    f.Put(pos, Vector3.up, new Color(0.06f, 0.025f, 0.01f) * (0.8f + 0.5f * f.R()), rad * 0.6f, 0.04f);   // dark against the light
                }
            }
            // the ring of clay it stands on: a low wall round its foot, paler on top
            float rr = Mathf.Max(hx, hz) * 1.45f, wallH = size * 0.08f;
            int ring = Mathf.RoundToInt(Tau * rr * wallH * 2.4f * perArea);
            for (int j = 0; j < ring; j++)
            {
                float a = f.R() * Tau, up = f.R() * wallH, out0 = f.R() < 0.5f ? 1f : -1f;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                bool top = up > wallH * 0.85f;
                float wob = 1f + 0.05f * Mathf.Sin(a * 7f) + 0.03f * Mathf.Sin(a * 17f);
                f.Put(o * (rr * wob + 0.06f * out0) + Vector3.up * (up + 0.01f), top ? Vector3.up : o * out0, (top ? clayTop : clay) * (0.85f + 0.3f * f.R()), rad * 0.9f);
            }
        }

        /// <summary>The big amber cluster at the amber nest's heart (AmberShards, larger and denser, no cap).</summary>
        public static void HeartShards(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea)
        {
            var f = new Frame(pen, p, n, ta, tb);
            Shards(pen, (lp, ln, c, r, g) => f.Put(lp, ln, c, r, g), size, 7 + (int)(pen.R() * 4f), perArea, 0.7f);
        }
    }
}
