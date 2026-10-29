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
        /// on its straight edge like a sail, a little cupped (seen edge on it is still a leaf, not a strip); its midrib
        /// and veins pale and glowing, its bitten rim glowing brightest (its sap's light), its own edge browning. size is
        /// its height (0.5 to 1.4).
        /// </summary>
        public static void LeafSail(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (all upright side by side they read as stakes driven into the floor: some lean back, as if dropped against
            // something, turned about their foot)
            float tilt = s.R() < 0.6f ? s.S() * 0.25f : (s.R() < 0.5f ? -1f : 1f) * s.U(0.35f, 0.75f);   // (at 1, turned up to the sky, they lit lemon green)
            float w = size * s.U(1.2f, 1.6f), yaw = s.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw), ct = Mathf.Cos(tilt), st = Mathf.Sin(tilt);
            var along = new Vector3(cs, 0f, sn);
            var face = new Vector3(-sn * ct, st, cs * ct);   // its face's normal, tilted with it
            var upw = new Vector3(sn * st, ct, -cs * st);    // up its face
            // (its body darker and solid, only its veins and the cut lit: a whole sail light yellow-green was the brightest
            // thing in view, and seen close a loose one read as a heap of flakes)
            // (the thirty-third: leaning back to the sky its body read bright lemon green, its cut white, the reverse of
            // "身子暗、部位亮": its body darker, its cut's sap a deeper yellow-green)
            Color leaf = Color.Lerp(new Color(0.06f, 0.10f, 0.03f), new Color(0.085f, 0.125f, 0.035f), s.R()), vein = new Color(0.20f, 0.27f, 0.11f), bite = new Color(0.20f, 0.14f, 0.05f);   // (its veins dim: bright, straight lines read as a wireframe)
            Color sap = new Color(0.36f, 0.62f, 0.08f);   // (lit by the ant's light at the gate a saturated sap read as lemon green on the floor; paler and brighter, the thirty-third, its cut read white)
            int m = s.Count(w * size * 0.6f * 2.8f, 60);
            for (int j = 0; j < m; j++)
            {
                // a half disc (the leaf's cut) less a bite out of its top edge (the crescent)
                float u = s.S(), v = s.R();
                if (u * u + v * v > 1f) continue;
                float bx = u - 0.35f, by = v - 0.95f;
                if (bx * bx + by * by < 0.42f) continue;
                bool bitten = bx * bx + by * by < 0.47f, rim = u * u + v * v > 0.945f;   // (the cut an edge: wider, it glowed as a white blot; narrower, it was only pale) (its own edge narrower: at 0.9 a cream outline)
                // its midrib, and fine side veins from it out to its edge, slanting toward its tip (two wide ones read as
                // a big W drawn on it)
                float mid = Mathf.Abs(u + 0.15f * v), veins = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((v - mid * 0.7f) * 36f)), 20f);
                bool isVein = mid < 0.04f || veins > 0.5f;
                float x = u * w * 0.5f, y = v * size, side = (s.R() - 0.5f) * 0.008f + u * u * w * 0.08f;   // a little cupped (more, close up it read as a cone)
                var lp = along * x + face * side + upw * y;
                var nrm = (face + Vector3.up * 0.15f - along * (0.88f * u)) * (s.R() < 0.5f ? 1f : -1f);
                Color col = bitten ? sap : rim ? bite : isVein ? vein : leaf;
                s.Put(lp, nrm, s.V(col, 0.1f), s.Rad(bitten ? 0.6f : 0.95f), bitten ? 0.55f : isVein && !rim ? 0.1f : 0f);
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
        /// The moss forest: a bunch of moss stalks as tall as street lamps are to us, rising from a small dark cushion of
        /// moss: six to eleven thin setae of different heights and thickness, leaning out and curving each its own way,
        /// dark red-brown at the foot turning gold toward the top, each bent over at its top like a shepherd's crook and
        /// hanging a pear-shaped capsule from it, like a lamp's head. Its light is in its parts, as in the glow references
        /// (Docs/参考_发光): the capsule a whole warm amber-brown body lit softly from inside, most where it is widest, six
        /// fine ribs from neck to mouth a little lighter, a small core of light inside; most still have their pale lid over
        /// the mouth, many stand open, the crown of pale teeth round the mouth a ring of bright dots; and its own light rises
        /// softly along the seta, none at its foot. (First version: a capsule a twentieth of the ant, its lid lit, a dull
        /// khaki speck; then, its core filling it, a solid cream blob; then three to six straight sticks standing apart,
        /// each capsule a skin half gaps round near-white ribs: lines hung with hollow wireframe lanterns, review k12.)
        /// size is the tallest one's height (0.8 to 2.4).
        /// </summary>
        public static void Sporophyte(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            int count = 6 + (int)(s.R() * 5.99f);
            Color seta0 = new Color(0.16f, 0.06f, 0.04f), seta1 = new Color(0.44f, 0.28f, 0.10f);
            Color skin0 = new Color(0.22f, 0.14f, 0.06f), skin1 = new Color(0.46f, 0.30f, 0.11f);
            Color core = new Color(0.98f, 0.80f, 0.45f), lid = new Color(0.80f, 0.74f, 0.50f), tooth = new Color(0.98f, 0.95f, 0.80f);
            Color rib = Color.Lerp(skin1, new Color(0.95f, 0.80f, 0.50f), 0.35f);
            Color moss0 = new Color(0.03f, 0.08f, 0.03f), moss1 = new Color(0.10f, 0.20f, 0.06f);
            // the cushion it rises from: a low dark dome of moss, lighter toward its top
            float cr = size * 0.13f, ch = size * 0.06f;
            for (int j = 0, mc = s.Count(3.1416f * cr * cr * 1.3f, 60); j < mc; j++)
            {
                float a = s.R() * Tau, q = Mathf.Sqrt(s.R()), y = ch * (1f - q * q) * s.U(0.85f, 1.1f);
                var lp = new Vector3(Mathf.Cos(a) * q * cr, y, Mathf.Sin(a) * q * cr);
                s.Put(lp, new Vector3(lp.x / cr, 1f, lp.z / cr), s.V(Color.Lerp(moss0, moss1, 1f - 0.7f * q), 0.15f), s.Rad(0.6f));
            }
            for (int i = 0; i < count; i++)
            {
                float h = size * (i == 0 ? 1f : s.U(0.45f, 0.95f)), ang = s.R() * Tau, off = s.U(0f, 0.8f) * cr;
                var b0 = new Vector3(Mathf.Cos(ang) * off, ch * 0.6f, Mathf.Sin(ang) * off);
                // leaning out from the cushion's middle and curving its own way, bowed a little along its length, never two
                // parallel (leaning a little, k13: the tall ones still a row of straight parallel sticks)
                var lean = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * s.U(0.08f, 0.22f) + new Vector3(s.S(), 0f, s.S()) * 0.1f;
                var bow = new Vector3(s.S(), 0f, s.S()) * (0.06f * h);
                float rs = Mathf.Lerp(0.006f, 0.012f, Mathf.Clamp01(h / 2.4f)) * s.U(0.75f, 1.3f);
                Vector3 prev = b0;
                for (int q = 1; q <= 10; q++)
                {
                    float f0 = (q - 1) / 10f, f1 = q / 10f;
                    Vector3 cur = b0 + Vector3.up * (h * f1) + lean * (h * f1 * f1) + bow * Mathf.Sin(Mathf.PI * f1);
                    // (its own light along its growth, soft: none at its foot, a little under the capsule)
                    NestThingsGrowth.Tube(s, prev, cur, rs * (1.1f - 0.15f * f0), rs * (1.1f - 0.15f * f1), Color.Lerp(seta0, seta1, f0), Color.Lerp(seta0, seta1, f1), 0.1f * f1 * f1);
                    prev = cur;
                }
                // the crook: the seta's top bends over, and the capsule hangs from it, its axis (neck to mouth) out and down
                var nod = new Vector3(Mathf.Cos(ang + 1.3f), 0f, Mathf.Sin(ang + 1.3f));
                Vector3 axis = (nod * s.U(0.6f, 0.9f) + Vector3.down * s.U(0.35f, 0.75f)).normalized;
                // (at a quarter to a half of the ant long, it read as a gold leaf hung on a stick)
                float lc = Mathf.Lerp(0.09f, 0.17f, Mathf.Clamp01(h / 2.4f)) * s.U(0.85f, 1.15f), rc = lc * s.U(0.38f, 0.48f);
                Vector3 bend = prev + Vector3.up * (lc * 0.3f) + nod * (lc * 0.12f), neck = bend + nod * (lc * 0.3f) + Vector3.down * (lc * 0.06f);
                NestThingsGrowth.Tube(s, prev, bend, rs, rs * 1.2f, seta1, seta1, 0.1f);
                NestThingsGrowth.Tube(s, bend, neck, rs * 1.2f, rs * 1.6f, seta1, skin1, 0.12f);
                NestThingsGrowth.Perp(axis, out Vector3 u, out Vector3 v);
                Vector3 c = neck + axis * lc;
                bool open = s.R() < 0.45f;
                float light = s.U(0.55f, 0.85f), spin = s.R() * Tau;
                // its body, pear-shaped (narrow at the neck, closing a little at the mouth), as a lantern pod in the
                // references: a whole warm amber-brown skin, lit softly from inside, most where it is widest (a dark skin
                // half gaps round its ribs read as a hollow wireframe)
                float Pear(float z) => Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, (z + 1f) / 1.2f)) * (z > 0.8f ? Mathf.Lerp(1f, 0.82f, (z - 0.8f) / 0.2f) : 1f);
                int m = s.Count(NestThingsGrowth.EllipsoidArea(rc, lc, rc), 40);
                for (int j = 0; j < m; j++)
                {
                    float z = s.S(), t = s.R() * Tau, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                    if ((open && z > 0.86f) || s.R() < 0.1f) continue;
                    Vector3 radial = u * (rr * Mathf.Cos(t)) + v * (rr * Mathf.Sin(t));
                    float along = (z + 1f) * 0.5f;
                    s.Put(c + axis * (lc * z) + radial * (rc * Pear(z)), radial + axis * (z * 0.6f), s.V(Color.Lerp(skin0, skin1, along) * 1.1f, 0.1f), s.Rad(0.55f),
                        (0.06f + 0.18f * rr * Pear(z)) * light);   // (at 0.05 + 0.12, k15: filled but no light to be seen in it)
                }
                // six fine ribs from neck to mouth, a little lighter than the body, amber, drawn along their lines (picked
                // out of the skin's scattered points, they read as specks, not lines; near white, the capsules' gold
                // outlines stood out of the green as glints)
                for (int k = 0; k < 6; k++)
                {
                    float a = spin + k * Tau / 6f;
                    Vector3 o = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    int steps = Mathf.Clamp(Mathf.RoundToInt(lc * 2f / (s.Rad(0.26f) * 1.1f)), 8, 60);
                    for (int j = 0; j < steps; j++)
                    {
                        float z = Mathf.Lerp(-0.9f, open ? 0.84f : 0.9f, (j + 0.5f) / steps), rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z)), along = (z + 1f) * 0.5f;
                        s.Put(c + axis * (lc * z) + o * (rc * Pear(z) * rr * 1.02f), o + axis * (z * 0.6f), s.V(rib, 0.05f), s.Rad(0.26f), (0.15f + 0.2f * along) * light);
                    }
                }
                // the light inside: a small soft amber core, bright in its middle and darker out from it (larger and
                // filling the capsule, it made it a solid cream blob)
                for (int j = 0, k = Mathf.RoundToInt(m * 0.12f); j < k; j++)
                {
                    Vector3 d = InBall(pen, out float f);
                    float mid = 0.3f + 0.7f * (1f - f) * (1f - f);
                    s.Put(c + axis * (lc * 0.5f * d.y) + (u * d.x + v * d.z) * (rc * 0.5f), axis, s.V(core * mid, 0.06f), s.Rad(0.55f), light * mid);
                }
                Vector3 mouth = c + axis * (lc * 0.86f);
                if (open)
                {
                    // the crown of teeth round the open mouth, bent in: a ring of small bright dots
                    for (int k = 0; k < 16; k++)
                    {
                        float a = (k + 0.3f * s.S()) / 16f * Tau;
                        Vector3 o = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                        for (int j = 0; j < 3; j++)
                        {
                            float t = j / 2f;
                            s.Put(mouth + axis * (lc * 0.07f * t) + o * (rc * 0.8f * (1f - 0.45f * t)), axis + o * 0.5f, s.V(tooth, 0.05f), s.Rad(0.4f), 0.6f + 0.3f * light);
                        }
                    }
                }
                else
                {
                    // its lid: a pale low dome over the mouth with a short beak, lit a little more than the skin
                    for (int j = 0, kl = Mathf.Max(12, m / 6); j < kl; j++)
                    {
                        Vector3 d = InBall(pen, out _).normalized;
                        Vector3 o = u * d.x + v * d.z;
                        s.Put(mouth + o * (rc * 0.78f) + axis * (rc * 0.45f * Mathf.Abs(d.y)), o + axis, s.V(lid, 0.08f), s.Rad(0.45f), 0.3f * light);
                    }
                    for (int j = 0; j < 3; j++)
                        s.Put(mouth + axis * (rc * (0.5f + 0.2f * j)), axis, s.V(lid, 0.05f), s.Rad(0.3f), 0.5f * light);
                }
            }
        }

        /// <summary>
        /// The moss forest: star moss (haircap moss) as a colony of round tufts, at ant scale cushions about as tall as the
        /// ant and up to half as tall again, a big one and a few smaller ones merging into it: each a dome of shoots packed
        /// so close their leaves make one surface, deep green at its foot and where it meets the next, lighter on its
        /// shoulders, a little frayed round its foot; its top a field of stars (the shoots' crowns, touching: a dark point
        /// and narrow leaves spreading from it, yellow-green toward their tips, a few of those tips faintly lit, and only
        /// those light up when the ant walks past or touches it, SolidThings: lit whole, k19, its domes glared). (First
        /// version: ankle-high stars, a flat pale layer. Then ten-odd thin stems standing apart, hung with fine leaf dots:
        /// the user, "太单薄". Then a dense thicket of a hundred shoots with broad pale leaves: a block of pale green
        /// flakes, a wall of light when touched. Then that thicket darkened: its loose dark leaves vanished on the dark
        /// floor and only the stars at its top showed, a flat scatter of pale flecks, its male shoots' glowing coral domes
        /// cream dots among them; the user, 10-29, "看不出来是什么东西".) size is the colony's width (0.8 to 1.8).
        /// </summary>
        public static void StarMoss(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color deep = new Color(0.025f, 0.06f, 0.022f), body = new Color(0.06f, 0.16f, 0.055f), crown = new Color(0.11f, 0.24f, 0.08f), tip = new Color(0.28f, 0.42f, 0.13f);
            Color olive = new Color(0.10f, 0.14f, 0.035f);
            float rad = size * 0.5f, seed = s.R() * 40f;
            // the tufts: a big one near the middle, smaller ones round it, their domes a little taller than wide; each its
            // own shade, some olive, and mottled (one even bright green with its stars evenly over it read as pasted on,
            // review k20)
            int nt = Mathf.Clamp(Mathf.RoundToInt(size * 3.2f), 3, 6);
            var tc = new Vector3[nt];
            var tr = new float[nt];
            var th = new float[nt];
            var tk = new Color[nt];
            for (int i = 0; i < nt; i++)
            {
                tk[i] = Color.Lerp(body, olive, s.R() < 0.35f ? s.U(0.3f, 0.6f) : s.U(0f, 0.15f)) * s.U(0.62f, 0.92f);
                float rt = rad * (i == 0 ? s.U(0.48f, 0.62f) : s.U(0.26f, 0.42f));
                Vector3 c = Vector3.zero;
                for (int tries = 0; tries < 12; tries++)
                {
                    float a = s.R() * Tau, q = i == 0 ? s.R() * rad * 0.2f : s.U(0.4f, 1f) * (rad - rt * 0.5f);
                    c = new Vector3(Mathf.Cos(a) * q, 0f, Mathf.Sin(a) * q);
                    bool clear = true;
                    for (int j = 0; j < i; j++)
                        if ((c - tc[j]).magnitude < 0.8f * (rt + tr[j])) clear = false;
                    if (clear) break;
                }
                tc[i] = c;
                tr[i] = rt;
                th[i] = rt * s.U(1.1f, 1.4f);
            }
            // a dome: an ellipsoid standing a tenth of its height up, cut at the floor; how far into the nearest other
            // dome a point is (under 1: hidden in it; a little over: in the crease where they meet)
            Vector3 Centre(int i) => tc[i] + Vector3.up * (th[i] * 0.1f);
            float Others(Vector3 x, int self)
            {
                float m = 9f;
                for (int j = 0; j < nt; j++)
                {
                    if (j == self) continue;
                    Vector3 e = x - Centre(j);
                    m = Mathf.Min(m, Mathf.Sqrt((e.x * e.x + e.z * e.z) / (tr[j] * tr[j]) + e.y * e.y / (th[j] * th[j] * 0.81f)));
                }
                return m;
            }
            // their outlines lumpy, not smooth domes: pushed in and out along their normal by two crossed waves of noise
            // (round and even, k20, they read as clipped shrubs)
            float Lump(Vector3 q) => Mathf.PerlinNoise(q.x * 5f + seed, q.z * 5f - seed) + Mathf.PerlinNoise(q.y * 6f - seed, (q.x - q.z) * 4f + seed) - 1f;
            // their surfaces, about half its splats (fewer and larger on a big colony), lit by their own form: an outer
            // layer of leaves with gaps, and under it a dark core that the gaps show (so no dome is seen through); their
            // tops only a little lighter than their sides, so the stars on them show (at the stars' own light green, k19,
            // the stars' inner parts were lost in it and their tips read as pale blobs)
            float area = 0f;
            for (int i = 0; i < nt; i++) area += NestThingsGrowth.EllipsoidArea(tr[i], th[i] * 0.9f, tr[i]) * 0.56f;
            float fill = Mathf.Min(1f, s.Left * 0.45f / Mathf.Max(1, s.Count(area)));
            for (int i = 0; i < nt; i++)
            {
                float rt = tr[i], ht = th[i];
                int m = Mathf.RoundToInt(s.Count(NestThingsGrowth.EllipsoidArea(rt, ht * 0.9f, rt)) * fill);
                for (int k = 0; k < m / 5; k++)
                {
                    Vector3 x = NestThingsGrowth.OnEllipsoid(s, Centre(i), new Vector3(rt * 0.86f, 0f, 0f), new Vector3(0f, ht * 0.8f, 0f), new Vector3(0f, 0f, rt * 0.86f), out Vector3 nrm, out Vector3 q);
                    if (q.y < -0.12f || Others(x, i) < 0.97f) continue;
                    s.Put(x, nrm, s.V(deep * 0.8f, 0.1f), s.Rad(2.2f / Mathf.Sqrt(fill)));
                }
                for (int k = 0; k < m; k++)
                {
                    Vector3 x = NestThingsGrowth.OnEllipsoid(s, Centre(i), new Vector3(rt, 0f, 0f), new Vector3(0f, ht * 0.9f, 0f), new Vector3(0f, 0f, rt), out Vector3 nrm, out Vector3 q);
                    float other = Others(x, i);
                    if (q.y < -0.12f || other < 0.97f) continue;
                    nrm.Normalize();
                    float up = Mathf.Clamp01(x.y / ht);
                    x += nrm * (rt * 0.14f * Lump(x) * Mathf.SmoothStep(0f, 1f, up / 0.25f));
                    if (up < 0.3f) x += new Vector3(nrm.x, 0f, nrm.z) * (rt * 0.08f * s.R() * (1f - up / 0.3f));   // (its foot frayed)
                    float mottle = 0.72f + 0.56f * Mathf.PerlinNoise(x.x * 7f + seed, (x.z + x.y) * 7f - seed);
                    Color col = Color.Lerp(deep, tk[i] * mottle, Mathf.SmoothStep(0f, 1f, up / 0.5f));
                    col = Color.Lerp(col, crown, Mathf.SmoothStep(0f, 1f, (up - 0.5f) / 0.5f) * 0.2f * (0.5f + 0.5f * Mathf.Max(0f, nrm.y)));
                    col = Color.Lerp(deep, col, Mathf.SmoothStep(0f, 1f, (other - 0.97f) / 0.25f));
                    s.Put(x, nrm + new Vector3(s.S(), s.S(), s.S()) * 0.35f, s.V(col, 0.18f), s.Rad(1f / Mathf.Sqrt(fill)));
                }
            }
            // the stars over their upper parts, larger on a larger tuft: a dark point, five to seven narrow leaves spreading
            // from it, a little above the dome (broad and short, k19, they ran together into pale discs); unlike in size,
            // tilted each its own way, crowded in places and thin in others, a third of them young ones, small and dark
            // with no pale tips, the rest starting in the tuft's own green (evenly spread, all alike and all pale, k20,
            // they read as stickers on the dome; a third young, thinned by half, pale only at their very tips, k22, they
            // were lost in it)
            int left = s.Left;
            float caps = area * 0.35f / 0.56f;
            for (int i = 0; i < nt; i++)
            {
                float rt = tr[i], ht = th[i], sr = Mathf.Clamp(rt * 0.2f, 0.035f, 0.075f);
                float cap = NestThingsGrowth.EllipsoidArea(rt, ht * 0.9f, rt) * 0.35f;
                int ns = Mathf.Min(Mathf.RoundToInt(cap / (3.1416f * sr * sr * 1.6f)), Mathf.RoundToInt(left / 26f * cap / caps));
                Color inner = tk[i] * 1.2f;
                for (int k = 0; k < ns; k++)
                {
                    Vector3 x = NestThingsGrowth.OnEllipsoid(s, Centre(i), new Vector3(rt, 0f, 0f), new Vector3(0f, ht * 0.9f, 0f), new Vector3(0f, 0f, rt), out Vector3 nrm, out Vector3 q);
                    if (q.y < 0.3f || Others(x, i) < 1f) continue;
                    if (Mathf.PerlinNoise(x.x * 5f - seed, x.z * 5f + seed) < s.U(0.1f, 0.3f)) continue;
                    x += nrm.normalized * (rt * 0.14f * Lump(x));   // (on the lumps, as the leaves under them)
                    nrm = (nrm.normalized + new Vector3(s.S(), s.S(), s.S()) * 0.25f).normalized;
                    NestThingsGrowth.Perp(nrm, out Vector3 u, out Vector3 v);
                    bool young = s.R() < 0.2f;
                    float r1 = sr * (young ? s.U(0.4f, 0.65f) : s.U(0.85f, 1.35f)), spin = s.R() * Tau, len = r1 * s.U(1f, 1.3f);
                    bool lit = !young && s.R() < 0.3f;
                    x += nrm * 0.012f;
                    s.Put(x, nrm, s.V(deep, 0.1f), r1 * 0.3f);
                    for (int r = 0, rays = 5 + (int)(s.R() * 3f); r < rays; r++)
                    {
                        float a = spin + (r + 0.3f * s.S()) * Tau / rays;
                        Vector3 d = (u * Mathf.Cos(a) + v * Mathf.Sin(a) + nrm * 0.2f).normalized;
                        for (int j = 0; j < 4; j++)
                        {
                            float t = (j + 0.5f) / 4f;
                            Color c = young ? Color.Lerp(deep, inner, 0.4f + 0.6f * t) : Color.Lerp(inner, tip, 0.2f + 0.8f * t);
                            s.Put(x + d * (len * t) - nrm * (0.2f * len * t * t), nrm + d * 0.3f, s.V(c, 0.12f), r1 * (0.26f - 0.12f * t), lit && j == 3 ? 0.15f : 0f);
                        }
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
            // the light it holds, gathered low in it (as a lens gathers it), soft round its middle: with the drop's own
            // inside alone it read as a ghost of a bubble except close up
            // (its points as large as the cushion's: at a third of that, a few hundred of them did not show at all)
            Color held = new Color(0.86f, 1f, 0.90f);
            var focus = c + new Vector3(0.08f * r, -0.42f * r, 0.06f * r);
            for (int j = 0, k = Mathf.RoundToInt(700f * size); j < k; j++)
            {
                Vector3 d = InBall(pen, out float fr);
                f.Put(focus + new Vector3(d.x, d.y * 0.6f, d.z) * (0.3f * r), Vector3.up, held * (0.6f + 0.4f * (1f - fr)), rad * (1.2f + 1.4f * (1f - fr)), 0.95f * (1f - fr) * (1f - fr));
            }
            // and thrown onto the moss beside its foot, a soft crescent of light
            for (int j = 0, k = Mathf.RoundToInt(900f * size); j < k; j++)
            {
                float a = 3.6f + 1.4f * f.S(), q = r * (0.95f + 0.4f * f.R());
                float edge = 1f - Mathf.Abs(a - 3.6f) / 1.4f, y = ch * Mathf.Sqrt(Mathf.Max(0f, 1f - q * q / (cr * cr))) + 0.06f;
                f.Put(new Vector3(Mathf.Cos(a) * q, y, Mathf.Sin(a) * q), Vector3.up, held * 0.7f, rad * 1.8f,
                    0.5f * edge * (1f - (q - r * 0.95f) / (r * 0.4f)));
            }
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
        /// The leafcutters' fungus garden (a landmark beside their way, once the heart): a grey-white sponge of fungus in
        /// round lobes pressed one against the next, pitted all over with round dark holes, lighter on its tops and
        /// darker toward its foot, the lobes' tops dotted with clusters of the small swollen tips the ants eat, glowing a
        /// faint yellow-green; round its foot the pieces of leaf it grows on, fresh green, browning further out. size is
        /// its width (about 2 to 3). Returns where its light is (the world).
        /// </summary>
        public static Vector3 HeartGarden(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float rad = 0.62f / Mathf.Sqrt(perArea), R = size * 0.5f, seed = f.R() * 40f;
            // (one dome, its holes the dark lines where three waves cancel, read from the way as a mossy stone cracked
            // across; now lobes, round pits, and its tops lighter than its foot)
            // (the twenty-ninth's five to seven round lobes, few pits, read from the way as a heap of smooth grey stones:
            // more lobes, unlike in size and height, some on others, lumpy; more and bigger pits; twice the knobs; darker)
            // (the thirty-seventh: grey as its walls, its holes as hollows in a rock, still a stone: a warmer cream, a
            // faint light in its holes, fresh cut leaves stuck in its top)
            Color sponge = new Color(0.24f, 0.21f, 0.15f), hollow = new Color(0.045f, 0.045f, 0.035f), knob = new Color(0.62f, 0.78f, 0.26f), deepGlow = new Color(0.10f, 0.15f, 0.03f);
            int lobes = 8 + (int)(f.R() * 5f);
            var lc = new Vector3[lobes];
            var lr = new float[lobes];
            var lh = new float[lobes];
            float H = 0f;
            for (int i = 0; i < lobes; i++)
            {
                lr[i] = R * (i == 0 ? 0.55f : f.U(0.22f, 0.46f));
                lh[i] = lr[i] * f.U(0.7f, 1.3f);
                float a = f.R() * Tau, off = i == 0 ? 0f : R * f.U(0.3f, 0.72f);
                float lift = i > 0 && off < R * 0.5f && f.R() < 0.4f ? R * f.U(0.15f, 0.3f) : 0f;   // (one on the others)
                lc[i] = new Vector3(Mathf.Cos(a) * off, lh[i] * (i == 0 ? 0.55f : f.U(0.3f, 0.55f)) + lift, Mathf.Sin(a) * off);
                H = Mathf.Max(H, lc[i].y + lh[i]);
            }
            bool Inside(Vector3 q, int except)
            {
                for (int j = 0; j < lobes; j++)
                {
                    if (j == except) continue;
                    Vector3 e = q - lc[j];
                    if ((e.x * e.x + e.z * e.z) / (lr[j] * lr[j]) + e.y * e.y / (lh[j] * lh[j]) < 0.97f) return true;
                }
                return false;
            }
            // its holes, as a sponge's: many small ones close together and a few big cavities, each darker toward its
            // middle (one size of round dark spots, flat, read as a spotted stone): how far into one a point is (under
            // 1: in it), the nearer of the two
            float PitIn(Vector3 q, float cell, float share, float size, uint salt)
            {
                float gx = q.x / cell + seed, gy = q.y / cell, gz = q.z / cell - seed;
                int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy), iz = Mathf.FloorToInt(gz);
                uint h = (uint)(ix * 73856093 ^ iy * 19349663 ^ iz * 83492791) + salt;
                float H01(uint k) { k ^= k >> 15; k *= 2246822519u; k ^= k >> 13; return (k & 0xFFFF) / 65535f; }
                if (H01(h) > share) return 9f;
                float dx = gx - (ix + 0.35f + 0.3f * H01(h + 1)), dy = gy - (iy + 0.35f + 0.3f * H01(h + 2)), dz = gz - (iz + 0.35f + 0.3f * H01(h + 3));
                return Mathf.Sqrt(dx * dx + dy * dy + dz * dz) / (size + 0.12f * H01(h + 4));
            }
            float Pit(Vector3 q) => Mathf.Min(PitIn(q, 0.11f, 0.8f, 0.3f, 0u), PitIn(q, 0.42f, 0.5f, 0.34f, 101u));   // (big ones bigger: from the way the small ones blur)
            // pieces of leaf bedded in the sponge, as the ants lay them: in about a fifth of the cells of a coarser grid a
            // sliver lying along its surface, fresh green on its tops, brown lower down, darker than the sponge (grey
            // and pitted alone, close up, it read as a stone)
            const float leafCell = 0.38f;
            float Scrap(Vector3 q)
            {
                float gx = q.x / leafCell - seed, gy = q.y / leafCell, gz = q.z / leafCell + seed;
                int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy), iz = Mathf.FloorToInt(gz);
                uint h = (uint)(ix * 19349663 ^ iy * 83492791 ^ iz * 73856093) + 7u;
                float H01(uint k) { k ^= k >> 15; k *= 2246822519u; k ^= k >> 13; return (k & 0xFFFF) / 65535f; }
                if (H01(h) > 0.4f) return 9f;
                float a = H01(h + 5) * Tau, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float dx = gx - (ix + 0.5f), dy = gy - (iy + 0.5f), dz = gz - (iz + 0.5f);
                float along = dx * ca + dz * sa, across = dz * ca - dx * sa;
                return Mathf.Sqrt(along * along / 0.3f + across * across / 0.07f + dy * dy / 0.08f);
            }
            Color leafGreen = new Color(0.19f, 0.35f, 0.06f), leafBrown = new Color(0.20f, 0.12f, 0.04f);   // (darker and smaller, from the way they did not show)
            // its lobes lumpy, not smooth domes: pushed in and out along their normal by two crossed waves of noise
            float Lump(Vector3 q) => Mathf.PerlinNoise(q.x * 6f + seed, q.z * 6f - seed) + Mathf.PerlinNoise(q.y * 6f - seed, (q.x - q.z) * 4.4f + seed) - 1f;
            for (int i = 0; i < lobes; i++)
            {
                float r = lr[i], ht = lh[i];
                int m = Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(r, ht, r) * 1.5f * perArea);
                for (int j = 0; j < m; j++)
                {
                    Vector3 d = InBall(pen, out _).normalized;
                    if (d.y < -0.35f) continue;
                    Vector3 q = lc[i] + new Vector3(d.x * r, d.y * ht, d.z * r);
                    if (q.y < 0f || Inside(q, i)) continue;   // (only the outside of the lobes together, on the floor)
                    Vector3 nrm = new Vector3(d.x / r, d.y / ht, d.z / r);
                    q += nrm.normalized * (Lump(q) * 0.28f * Mathf.Min(r, ht) + (f.R() - 0.5f) * 0.024f);   // (and rough)
                    if (q.y < 0f) continue;
                    float pit = Pit(q), up = Mathf.Clamp01(q.y / H);
                    // its tops lighter, its foot darker; a hole's rim a little lighter, its inside darker toward its
                    // middle and sunk; grainy all over
                    Color body = sponge * (0.45f + 0.7f * up) * (pit < 1.3f ? 1.12f : 1f) * (0.82f + 0.36f * f.R());
                    bool hole = pit < 1f;
                    if (!hole && Scrap(q) < 1f)
                    {
                        f.Put(q + nrm.normalized * 0.01f, nrm, Color.Lerp(leafBrown, leafGreen, Mathf.SmoothStep(0.25f, 0.7f, up)) * (0.85f + 0.3f * f.R()), rad * (1f + 0.4f * f.R()));
                        continue;
                    }
                    float deep = hole ? 1f - pit * pit : 0f;
                    f.Put(q - nrm.normalized * (0.07f * deep), nrm, hole ? Color.Lerp(body * 0.45f, Color.Lerp(hollow, deepGlow, 0.6f), deep) : body, rad * (1f + 0.5f * f.R()), hole ? 0.1f * deep : 0f);
                    // its mycelium: a soft pale fuzz standing off its surface, so its edge is soft, not a stone's hard one
                    // (grey, lumpy and pitted, from the way it still read as a stone with lichen on it)
                    if (!hole && f.R() < 0.3f)
                        f.Put(q + nrm.normalized * f.U(0.015f, 0.055f) + new Vector3(f.S(), f.S(), f.S()) * 0.012f, nrm, body * f.U(1.1f, 1.35f), rad * f.U(0.5f, 0.8f));
                }
                // a dark core inside each lobe, so what gaps there are show depth, not what lies behind
                for (int j = 0, mc = Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(r, ht, r) * 0.3f * perArea); j < mc; j++)
                {
                    Vector3 d = InBall(pen, out _).normalized;
                    if (d.y < -0.35f) continue;
                    Vector3 q = lc[i] + new Vector3(d.x * r, d.y * ht, d.z * r) * 0.82f;
                    if (q.y < 0f) continue;
                    f.Put(q, new Vector3(d.x / r, d.y / ht, d.z / r), hollow * (0.7f + 0.3f * f.R()), rad * 1.6f);
                }
            }
            // its knobs: clusters of small round beads on the lobes' tops, where the ants tend them (scattered evenly, or
            // few and dim, they read as specks on a stone; bright, as balls of light)
            // (small beads, many: big pale ones read as cauliflower florets on a stone; in clusters of beads as big as
            // these, from the way, as white flowers on a stone: fine bright dots)
            int clusters = 60 + (int)(f.R() * 20f);
            for (int k = 0; k < clusters; k++)
            {
                int i = (int)(f.R() * (lobes - 0.001f));
                float a = f.R() * Tau, upw = f.U(0.7f, 1f), side = Mathf.Sqrt(1f - upw * upw);
                var qd = new Vector3(Mathf.Cos(a) * side, upw, Mathf.Sin(a) * side);
                Vector3 c = lc[i] + new Vector3(qd.x * lr[i], qd.y * lh[i], qd.z * lr[i]);
                if (Inside(c, i)) continue;
                Vector3 nn = new Vector3(qd.x / lr[i], qd.y / lh[i], qd.z / lr[i]).normalized;
                c += nn * (Lump(c) * 0.16f * Mathf.Min(lr[i], lh[i]));   // (on its lumps, as its surface is)
                int balls = 8 + (int)(f.R() * 7f);
                for (int b = 0; b < balls; b++)
                {
                    float br = f.U(0.01f, 0.018f);
                    Vector3 bc = c + new Vector3(f.S(), 0f, f.S()) * 0.07f + nn * br;
                    int mb = Mathf.Max(24, Mathf.RoundToInt(12.566f * br * br * perArea * 2f));
                    for (int j = 0; j < mb; j++)
                    {
                        // (through the bead, not only on its skin: on the skin alone, close up, each read as a ring of dots)
                        Vector3 o = InBall(pen, out float of);
                        Vector3 on = of > 1e-3f ? o / of : Vector3.up;
                        f.Put(bc + o * br, on, knob * (0.85f + 0.3f * f.R()), Mathf.Min(rad * 0.7f, br * 0.6f), 0.42f + 0.25f * Mathf.Max(0f, on.y));
                    }
                }
            }
            for (int k = 0; k < 44; k++)
            {
                float a = f.R() * Tau, d0 = R * f.U(0.95f, 1.35f), ls = f.U(0.18f, 0.42f), rot = f.R() * Tau;
                Color leaf = Color.Lerp(new Color(0.30f, 0.46f, 0.10f), new Color(0.40f, 0.24f, 0.10f), Mathf.Clamp01(f.R() * 0.6f + (d0 > R * 1.15f ? 0.45f : 0f)));
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
            // fresh pieces of leaf the ants have just brought, stuck half into its top, their cut edges lit
            for (int k = 0, nl = 2 + (int)(f.R() * 2f); k < nl; k++)
            {
                int i = k % lobes;
                Vector3 top = lc[i] + new Vector3(f.S() * lr[i] * 0.3f, lh[i] * 0.75f, f.S() * lr[i] * 0.3f);
                LeafSail(pen, f.World(top), n, ta, tb, f.U(0.35f, 0.5f));
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
        /// The honey granary's landmark beside its way (no nest has a heart now, and its honeypot ants were insects:
        /// "新内容不要昆虫"; SolidThings.Landmarks): a giant sunflower seed, as long as three ants, lying on its flat side,
        /// its broad end half toward the way and split open there along its seam, the upper half bent up at that end only;
        /// honey fills it, deep amber, and wells out of the split in a thick tongue over the floor toward the way, gold
        /// where the light comes through it, a highlight along it. Its husk near black with pale grey stripes along it, as
        /// such a seed's; its seam a pale edge; only the honey lit; a few small seeds of its kind round it. (Half a husk lying hollow up read from above as a white dish; an
        /// acorn's cap as a bowl, a pot, a hole, "像一只倒了的碗".) size is its length (about 2.1 to 2.6). Returns where
        /// its honey's light is (the world).
        /// </summary>
        public static Vector3 HeartSeedCup(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea, Vector3 face)
        {
            var f = new Frame(pen, p, n, ta, tb);
            float rad = 0.62f / Mathf.Sqrt(perArea);
            float A = size * 0.5f, W = size * 0.29f, D = size * 0.12f;   // half its length, half its width, each half's depth
            Vector3 toWay = new Vector3(face.x, 0f, face.z).normalized, side = new Vector3(toWay.z, 0f, -toWay.x);
            Vector3 ax = (toWay + side).normalized, ac = new Vector3(ax.z, 0f, -ax.x);   // along it toward its broad end (half toward the way), across it
            float h0 = D * 0.9f;   // its seam's height (its lower half sunk a little into the floor)
            // its upper half cracked at its broad end only, bent up there about 13° from a third of the way along: a split, not
            // a lid (lifted whole from near its point, at 14° it read as an open shell, "像张开的贝壳"; at 7°, a pale band of
            // honey all along its side, a filling, "瓜子里的蜜像一条平的浅黄带")
            const float sinLid = 0.18f;   // (at 0.22 from a third of the way, a wide gap full of flat gold honey, "金黄方片"; at 0.13 from near its end, no split seen)
            float cosLid = Mathf.Sqrt(1f - sinLid * sinLid), hingeX = A * 0.3f;
            float Half(float u) => Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (1f - 0.38f * u);   // teardrop: pointed at u = 1
            Vector3 World(Vector3 l) => ax * l.x + Vector3.up * l.y + ac * l.z;
            // a point of its husk's outside: u along it (-1 its broad end .. 1 its point), v across (-1..1); top: its upper
            // half, lifted; nrm its outward normal (in its own frame: x toward its broad end, y up, z across)
            Vector3 Shell(float u, float v, bool top, out Vector3 nrm)
            {
                float hw = Half(u), env = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)), bulge = D * (1f - v * v) * env;
                var l = new Vector3(-u * A, top ? h0 + bulge : h0 - bulge, v * hw * W);
                nrm = new Vector3(l.x / (A * A), (l.y - h0) / (D * D), l.z / (W * W)).normalized;
                if (!top) return l;
                float dx = l.x - hingeX, dy = l.y - h0;
                if (dx <= 0f) return l;
                nrm = new Vector3(nrm.x * cosLid - nrm.y * sinLid, nrm.x * sinLid + nrm.y * cosLid, nrm.z);
                return new Vector3(hingeX + dx * cosLid - dy * sinLid, h0 + dx * sinLid + dy * cosLid, l.z);
            }
            // (linear colours)
            Color black = new Color(0.022f, 0.02f, 0.018f), grey = new Color(0.16f, 0.155f, 0.14f), seam = new Color(0.12f, 0.11f, 0.095f), shade = new Color(0.07f, 0.06f, 0.045f);
            Color shine = new Color(0.95f, 0.92f, 0.80f), kernel = new Color(0.16f, 0.145f, 0.11f);
            float seed = f.R() * 10f, halfArea = 3.4f * A * W;
            // its husk: both halves' outsides, striped along it, its seam a pale edge; under its lifted lid, toward its
            // mouth, the lid's inside in shadow
            int m = Mathf.RoundToInt(halfArea * perArea * 1.3f);
            for (int half = 0; half < 2; half++)
                for (int j = 0; j < m; j++)
                {
                    float u = f.S(), v = f.S();
                    bool top = half == 1;
                    Vector3 l = Shell(u, v, top, out Vector3 nl);
                    if (!top && l.y < 0.005f) continue;   // (under the floor)
                    bool stripe = Mathf.Abs(Mathf.Sin(v * 7f + seed + 0.5f * u)) > 0.82f;
                    Color col = Mathf.Abs(v) > 0.94f ? seam : (stripe ? grey : black) * (0.85f + 0.3f * f.R());
                    f.Put(World(l), World(nl), col, rad * (0.8f + 0.4f * f.R()));
                    if (top && u < -0.2f)   // under its lifted husk, the kernel's pale flesh showing, a dark line where they part
                        f.Put(World(l - nl * (D * 0.12f)), -World(nl), (Mathf.Abs(v) > 0.8f ? shade : kernel) * (0.8f + 0.3f * f.R()), rad * (0.8f + 0.4f * f.R()));
                }
            // the honey filling it, level a little under its seam, drawn toward its mouth, where it shows: deep amber, darkest
            // away from its mouth, where it lies deepest; at its mouth welling up over the lip, lit through, gold (even and
            // pale, it read as yellow felt, "像黄毡")
            Color deep = new Color(0.03f, 0.01f, 0.001f), thin = new Color(0.012f, 0.004f, 0.0005f), clear = new Color(0.95f, 0.62f, 0.16f), body = new Color(0.09f, 0.035f, 0.004f);   // (a third as light as they read: the granary's night is 1.9 stops brighter)
            float level = h0 - D * 0.05f;
            int mh = Mathf.RoundToInt(halfArea * 0.5f * perArea * 1.3f);
            for (int j = 0; j < mh; j++)
            {
                float u = f.U(-1f, 0.1f), v = f.S() * 0.96f, mouth = Mathf.Clamp01(-u), well = Mathf.Pow(mouth, 6f);
                var l = new Vector3(-u * A, level + D * 0.1f * well * (1f - v * v), v * Half(u) * W);
                bool glint = f.R() < 0.04f;   // (its lustre painted: glossy, a still caught its flat honey flashing pale)
                Color col = Color.Lerp(deep, Color.Lerp(body, clear, 0.3f), 0.1f + 0.9f * well) * (0.9f + 0.15f * f.R());   // (to 0.6 of its light, a flat gold slab, "金黄方片")
                f.Put(World(l), Vector3.up, glint ? shine : col, rad * (glint ? 0.45f : 0.9f), glint ? 0.3f : 0.03f + 0.2f * well);
            }
            // out of the split, over its lower half's rim and down, a short sheet of honey, and on the floor at its broad end a
            // thick round spill, uneven at its edge, swollen toward its front: deep amber, gold only where it is thin at its
            // front edge and in the sheet falling over the rim, a few highlights; a drop hanging from the split's lip (a long
            // tongue with a lit crown and a round lobe at its end read as a spoon, "像一把勺子"; a flat straight-edged band,
            // lit, as a strip of light; amber all over and evenly lit, as a cake by itself, "一块黄饼")
            float poolR = size * 0.16f, poolX = A * 0.97f + size * 0.05f + poolR * 0.55f, sheetW = size * 0.075f;   // (the pool against the sheet: apart, a separate disc)
            for (int j = 0, ms = Mathf.RoundToInt(size * size * 0.03f * perArea); j < ms; j++)
            {
                float t = f.R(), half = sheetW * (0.6f + 0.4f * t), w = f.S() * half, edge = Mathf.Abs(w) / half;
                var l = new Vector3(A * 0.97f + size * 0.05f * t, Mathf.Max(0.012f, h0 * (1f - t)) + size * 0.012f * (1f - edge * edge), w);
                Color col = Color.Lerp(Color.Lerp(thin, body, 1f - edge), clear, 0.4f * (1f - edge * edge));
                bool glint = edge < 0.12f && f.R() < 0.35f;   // a highlight down it
                f.Put(World(l), (ax * 0.8f + Vector3.up).normalized, glint ? shine : col, rad * (glint ? 0.45f : 0.9f), glint ? 0.25f : 0.03f + 0.15f * (1f - edge));
            }
            float spillSeed = f.R() * 30f;
            for (int j = 0, mp = Mathf.RoundToInt(Mathf.PI * poolR * poolR * 2.8f * perArea); j < mp; j++)   // (at 1.7, holes in it: a sponge)
            {
                float a = f.R() * Tau, d = Mathf.Sqrt(f.R()) * poolR * 1.2f;
                float reach = poolR * (0.8f + 0.35f * Mathf.PerlinNoise(Mathf.Cos(a) * 1.7f + spillSeed, Mathf.Sin(a) * 1.7f + spillSeed));   // its edge uneven
                if (d > reach) continue;
                float dd = d / reach, front = Mathf.Clamp01(Mathf.Cos(a));   // 0 its middle .. 1 its edge; toward its front (+x)
                float thick = size * 0.045f * (1f - dd * dd) * (0.7f + 0.3f * front);
                var l = new Vector3(poolX + Mathf.Cos(a) * d, 0.012f + thick, Mathf.Sin(a) * d * 1.1f);
                float lit = (1f - dd) * (1f - dd) * (0.55f + 0.45f * front);   // lit through where it is thick (at its thin front edge, a fried egg's rim)
                bool glint = dd < 0.7f && f.R() < 0.03f;
                Vector3 nl = (Vector3.up + (ax * Mathf.Cos(a) + ac * Mathf.Sin(a)) * (dd * 0.8f)).normalized;
                Color col = Color.Lerp(Color.Lerp(body, thin, Mathf.SmoothStep(0f, 1f, (dd - 0.75f) / 0.25f)), clear, lit * 0.7f);
                f.Put(World(l), nl, glint ? shine : col, rad * (glint ? 0.45f : 0.9f), glint ? 0.3f : 0.03f + 0.3f * lit);
            }
            {
                float dr = size * 0.028f;
                Vector3 dc = World(new Vector3(A * 0.99f, h0 - dr * 0.9f, f.S() * W * 0.12f));
                for (int j = 0, md = Mathf.Max(30, Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(dr, dr * 1.3f, dr) * perArea * 1.5f)); j < md; j++)
                {
                    Vector3 pos = OnEllipsoid(f, dc, ax * dr, Vector3.up * (dr * 1.3f), ac * dr, out Vector3 nrm, out Vector3 q);
                    float below = Mathf.Max(0f, -q.y);
                    bool high = q.y > 0.15f && q.y < 0.55f && q.x < -0.3f;
                    f.Put(pos, nrm, high ? shine : Color.Lerp(body, clear, 0.2f + 0.6f * below * below), rad * (high ? 0.4f : 0.7f), high ? 0.4f : 0.05f + 0.3f * below * below);
                }
            }
            // round it, a few small seeds of its kind, whole, striped (none in its honey)
            for (int k = 0; k < 14; k++)
            {
                float a = f.R() * Tau, rr = f.U(1.1f, 1.6f);
                Vector3 c = ax * (Mathf.Cos(a) * A * rr) + ac * (Mathf.Sin(a) * W * rr * 1.6f) + Vector3.up * 0.03f;
                if (Mathf.Cos(a) > 0.75f) continue;
                float sl = size * f.U(0.06f, 0.09f), sw = sl * 0.5f, rot = f.R() * Tau;
                Vector3 ex = new Vector3(Mathf.Cos(rot), 0f, Mathf.Sin(rot)) * sl * 0.5f, ez = Vector3.Cross(ex.normalized, Vector3.up) * sw * 0.5f, ey = Vector3.up * sw * 0.3f;
                int mk = Mathf.Max(16, Mathf.RoundToInt(NestThingsGrowth.EllipsoidArea(sl * 0.5f, sw * 0.3f, sw * 0.5f) * perArea));
                for (int j = 0; j < mk; j++)
                {
                    Vector3 pos = OnEllipsoid(f, c, ex, ey, ez, out Vector3 nrm, out Vector3 q);
                    bool st = Mathf.Abs(Mathf.Sin(q.z * 8f)) > 0.75f;
                    f.Put(pos, nrm, (st ? grey : black) * (0.8f + 0.4f * Mathf.Max(0f, q.y)), rad * 0.5f);
                }
            }
            return f.World(World(new Vector3(A * 0.85f, h0 + 0.1f, 0f)));
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
                    // lit in the florets' tips only, a dot each, brighter toward the crown (lit evenly the whole head read
                    // as a flat gold disc)
                    float dot = floret > 1.1f ? 1f : 0.2f;
                    s.Put(pos, nrm, s.V(col, 0.08f), s.Rad(0.75f), glow * dot * (0.55f + 0.45f * Mathf.Max(0f, q.y)));
                }
            }
        }

        /// <summary>
        /// Resin drops: two to six beads of amber resin on the floor, some run together, each a little teardrop
        /// (drawn up to a point where it dripped); its skin dark and glassy, small splats with a few gaps, and inside a
        /// honey light (fine bright splats, brightest in the middle) with a few dark specks caught in it. size is the
        /// cluster's width (0.4 to 1.1). (With its light at full and half its skin drawn, a cluster read as one white
        /// glowing mound, no drops in it: the light quieter, the skin nearly whole, so each drop's outline shows.)
        /// </summary>
        public static void ResinDrops(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size) =>
            Drops(pen, p, n, ta, tb, size, new Color(0.34f, 0.15f, 0.035f), new Color(0.98f, 0.60f, 0.20f), 0.45f, 0.9f);

        /// <summary>The honey granary's honey drops: one to three drops of clear honey on the floor, a little apart, each
        /// whole and round (ClearDrop). (Run together they read as burnt flakes of gold, "像烤焦的金色薄片"; drawn up to a
        /// point and opaque, as gold nuggets, "像金块"; coarse and lit, as broken gold leaf, "碎金箔".) size is the
        /// cluster's width (0.3 to 0.75).</summary>
        public static void HoneyDrops(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float r0 = size * 0.3f, a0 = s.R() * Tau;
            ClearDrop(s, Vector3.zero, r0);
            for (int i = 1, count = 1 + (int)(s.R() * 2.99f); i < count; i++)
            {
                float r = size * s.U(0.13f, 0.2f), ang = a0 + i * s.U(2.2f, 2.8f), off = (r0 + r) * s.U(1.15f, 1.4f);
                ClearDrop(s, new Vector3(Mathf.Cos(ang) * off, 0f, Mathf.Sin(ang) * off), r);
            }
        }

        /// <summary>A drop of clear honey on the floor at local c (r: its radius): a low smooth dome, near black amber at its
        /// rim, where it is thin over the dark floor, deep amber, and clear gold toward its middle where the light comes
        /// through it; one small sharp highlight on top toward the moon; beyond it, on the floor on its far side from the
        /// moon, the light come through it in a gold spot. Fine even splats, their shading smooth: its skin whole. (Pale,
        /// flat and evenly lit, a drop read as a yellow coin; coarse and lit through its middle, as an orange biscuit,
        /// "像橙色饼干"; round, dark on top and gold below, as a mushroom's cap; in coarse splats catching the scene's
        /// glints, as bread, "像面包".)</summary>
        static void ClearDrop(NestThingsGrowth.Spot s, Vector3 c, float r)
        {
            // (linear colours, as dark as they are here: the granary's night 1.9 stops brighter lit a body of 0.20 bright orange)
            Color edge = new Color(0.015f, 0.005f, 0.0006f), body = new Color(0.07f, 0.025f, 0.0025f), clear = new Color(0.95f, 0.62f, 0.16f), shine = new Color(0.98f, 0.95f, 0.86f);
            float ht = r * s.U(0.32f, 0.4f);   // (spread on the floor: higher, a mushroom's cap)
            int m = s.Count(NestThingsGrowth.EllipsoidArea(r, ht, r) * 5f, 50);   // (finer and more: coarse, its splats read one by one)
            for (int j = 0; j < m; j++)
            {
                Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, Vector3.right * r, Vector3.up * ht, Vector3.forward * r, out Vector3 nrm, out Vector3 q);
                if (q.y < 0f) continue;   // (a dome on the floor)
                float outer = Mathf.Sqrt(q.x * q.x + q.z * q.z);   // 0 at its middle .. 1 at its edge
                float hx = q.x + 0.35f, hz = q.z;
                bool high = q.y > 0.55f && hx * hx + hz * hz < 0.022f;   // toward the moon
                float through = 1f - Mathf.SmoothStep(0f, 1f, outer / 0.62f);   // the light through its middle
                Color col = high ? shine : Color.Lerp(Color.Lerp(body, edge, Mathf.SmoothStep(0f, 1f, (outer - 0.72f) / 0.26f)), clear, through * through * 0.85f);
                s.Put(pos, nrm, col, s.Rad(high ? 0.35f : 0.48f), high ? 0.6f : 0.02f + 0.5f * through * through);
            }
            // the light come through it, on the floor on its far side from the moon (+x): a gold spot
            for (int j = 0, k = s.Count(r * r * 2f, 16); j < k; j++)
            {
                float a = s.S() * 0.75f, d = r * s.U(0.92f, 1.55f), fade = (1f - Mathf.Abs(a) / 0.75f) * (1f - Mathf.Abs(d / r - 1.15f) / 0.42f);
                if (s.R() > fade) continue;
                s.Put(c + new Vector3(Mathf.Cos(a) * d, 0.003f, Mathf.Sin(a) * d), Vector3.up, Color.Lerp(body, clear, 0.75f), s.Rad(0.5f), 0.1f + 0.25f * fade);
            }
        }

        /// <summary>Drops: skin and core their colours, coreGlow how strongly the light inside glows, skinFill how much of
        /// the skin is drawn (under 1: gaps, a glassy skin the light shows through; honey's is whole, an opaque gold).</summary>
        static void Drops(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, Color skin, Color core, float coreGlow, float skinFill)
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
                int m = s.Count(NestThingsGrowth.EllipsoidArea(r, ht * 0.5f, r) * skinFill, 20);
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
                    // (a whole skin, honey's, is glossy: fine splats, smooth, and a highlight toward the moon; at the size
                    // that covers it it read as sponge)
                    bool glint = skinFill >= 1f && q.y > 0.35f && q.y < 0.75f && q.x < -0.2f && q.z < 0.15f && q.z > -0.45f;
                    if (glint) col = new Color(0.98f, 0.92f, 0.74f);
                    s.Put(pos, nrm, s.V(col, skinFill >= 1f ? 0.05f : 0.12f), s.Rad(skinFill >= 1f ? 0.6f : 0.42f), glint ? 0.7f : 0.1f + 0.15f * Mathf.Max(0f, q.y));
                }
                // the light inside, and what it caught
                int k = Mathf.RoundToInt(m * 0.75f);
                for (int j = 0; j < k; j++)
                {
                    Vector3 d = InBall(pen, out float f);
                    Vector3 pos = c + new Vector3(d.x * r, d.y * ht * 0.45f, d.z * r) * 0.85f;
                    bool caught = s.R() < 0.04f;
                    s.Put(pos, Vector3.up, caught ? speck : s.V(core * (1f - 0.45f * f), 0.08f), s.Rad(caught ? 0.5f : 0.38f), caught ? 0f : (1f - 0.4f * f) * coreGlow);
                }
            }
        }

        /// <summary>
        /// The honey granary: honey set to crystals, as stored honey sets: a thin crust of coarse sugar grains spread over
        /// the floor in three to five patches run together, cream to pale amber, each grain further in or out, lighter or
        /// darker, now and then one catching the light; unlit (pale, lit and crusted with small crystals it read as a white
        /// mound glowing, "发白光的白丘"); in the seams where the patches meet and round their edge, honey that has not set,
        /// deep amber, lit a little; and beside it a drop of the same honey clear (ClearDrop), so the two read against each
        /// other. (Its first versions, gold six-sided needles, read as a white blaze and then as gems, "像宝石"; one smooth
        /// mound, as an egg, "像一颗蛋"; low lumpy heaps, as dough, "像土黄面团".) size is its width (0.5 to 1.2).
        /// </summary>
        public static void SugarCrystals(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (linear colours: at 0.70 its cream read near white; at 0.44, still pale)
            Color cream = new Color(0.19f, 0.11f, 0.035f), foot = new Color(0.06f, 0.032f, 0.009f), grain = new Color(0.36f, 0.27f, 0.14f);   // (at 0.38, the granary's night 1.9 stops brighter, a pale flat blob; at 0.16, ochre dough)
            Color amber = new Color(0.07f, 0.025f, 0.003f), core = new Color(0.55f, 0.3f, 0.06f), shine = new Color(0.95f, 0.92f, 0.80f);
            // the heaps: low lumpy domes, one big, the others crowding round it, each its own
            int heaps = 3 + (int)(s.R() * 2.99f);
            var hc = new Vector3[heaps];
            var hr = new float[heaps];
            var hh = new float[heaps];
            float lumps = s.R() * 50f;
            for (int i = 0; i < heaps; i++)
            {
                hr[i] = size * (i == 0 ? 0.26f : s.U(0.13f, 0.2f));
                hh[i] = hr[i] * s.U(0.3f, 0.42f);   // (small lumps: as heaps, smooth, an egg; lumpy and tall, dough; a flat crust, a powder like the sand's)
                float ang = i * Tau / (heaps - 1) + s.S() * 0.6f, off = i == 0 ? 0f : (hr[0] + hr[i]) * s.U(0.72f, 0.85f);   // (run together into one, they read as one smooth mound)
                hc[i] = new Vector3(Mathf.Cos(ang) * off, 0f, Mathf.Sin(ang) * off);
            }
            // how far inside heap o a point lies (under 1: inside it)
            float Inside(Vector3 q, int o)
            {
                Vector3 d = q - hc[o];
                return (d.x * d.x + d.z * d.z) / (hr[o] * hr[o]) + d.y * d.y / (hh[o] * hh[o]);
            }
            for (int i = 0; i < heaps; i++)
            {
                float r = hr[i], ht = hh[i];
                int m = s.Count(NestThingsGrowth.EllipsoidArea(r, ht, r) * 2.4f, 60);   // (fine and well covered: at 1.6 speckled; coarse, a heap of pale scraps; sparse, white flakes like snow)
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, hc[i], Vector3.right * r, Vector3.up * ht, Vector3.forward * r, out Vector3 nrm, out Vector3 q);
                    if (q.y < 0f) continue;   // (a dome: its lower half under the floor)
                    float seam = 9f;
                    for (int o = 0; o < heaps; o++) if (o != i) seam = Mathf.Min(seam, Inside(pos, o));
                    if (seam < 0.94f) continue;   // inside another heap, where they run together
                    if (seam < 1.35f)
                    {
                        // a seam: honey that has not set, deep amber, lit a little, deepest where they meet
                        float deep = 1f - (seam - 0.94f) / 0.41f;
                        bool glint = s.R() < 0.06f;
                        s.Put(pos + nrm.normalized * (r * 0.01f), nrm, glint ? shine : s.V(Color.Lerp(amber, core, 0.2f + 0.45f * deep * deep), 0.05f),
                            s.Rad(glint ? 0.4f : 0.6f), glint ? 0.35f : 0.05f + 0.2f * deep * deep);
                        continue;
                    }
                    // set honey: lumpy, and coarse grains (each splat further in or out, lighter or darker, now and then
                    // one catching the light), paler on top, shaded toward its foot; unlit
                    float lump = Mathf.PerlinNoise(pos.x * 7f / size + lumps, pos.z * 7f / size + lumps * 0.7f) - 0.5f;
                    pos += nrm.normalized * (r * (0.06f * lump + 0.05f * s.S()));
                    float g = s.R();
                    bool caught = g > 0.97f;
                    Color col = caught ? grain : Color.Lerp(foot, cream, 0.55f + 0.45f * q.y) * (0.72f + 0.5f * g);   // (darker grains read as dark spots; paler ones and coarse, as pale scraps)
                    s.Put(pos, nrm, col, s.Rad(caught ? 0.5f : 0.9f), caught ? 0.1f : 0f);
                }
            }
            // round their feet a skirt of the honey that has not set: flat, glossy, deepest (and most lit) against the
            // heaps (wide and lit, with the mound it read as a glow)
            float reach = size * 0.85f;
            int mp = s.Count(Mathf.PI * reach * reach * 0.7f, 60);
            for (int j = 0; j < mp; j++)
            {
                float a = s.R() * Tau, d = Mathf.Sqrt(s.R()) * reach;
                var lp = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float outside = 9f;
                for (int o = 0; o < heaps; o++) outside = Mathf.Min(outside, Inside(lp, o));
                outside = Mathf.Sqrt(outside);
                float edge = 1.2f + 0.2f * Mathf.PerlinNoise(Mathf.Cos(a) * 2.3f + size * 7f, Mathf.Sin(a) * 2.3f);
                if (outside < 0.97f || outside > edge) continue;
                float deep = 1f - Mathf.Clamp01((outside - 0.97f) / (edge - 0.97f));
                lp.y = size * 0.02f * deep;
                bool glint = s.R() < 0.05f;
                s.Put(lp, new Vector3(0.1f * Mathf.Cos(a), 1f, 0.1f * Mathf.Sin(a)), glint ? shine : s.V(Color.Lerp(amber, core, 0.3f + 0.5f * deep), 0.04f),
                    s.Rad(glint ? 0.5f : 1.15f), glint ? 0.3f : 0.04f + 0.16f * deep);   // (large, few: covering the skirt within the cap)
            }
            // beside it, a drop of the same honey clear
            float da = s.R() * Tau, dr = size * s.U(0.16f, 0.2f);   // (smaller, it was not seen)
            ClearDrop(s, new Vector3(Mathf.Cos(da), 0f, Mathf.Sin(da)) * (size * 0.72f + dr), dr);
        }

        /// <summary>
        /// The honey granary: what the foragers dropped on their way home, lying flat beside the way: one or two split
        /// sunflower-seed husks, a broken bit of dry grass and a honey stain with a drop standing in it ("路边要看得见种壳、
        /// 碎草、蜜渍"; more of them and paler, they read as a heap of pale scraps, "一堆浅色碎屑"). size is how wide the
        /// scatter is (1 to 1.8).
        /// </summary>
        public static void Gleanings(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float reach = size * 0.5f;
            Vector3 Somewhere(float k)
            {
                float a = pen.R() * Tau, d = Mathf.Sqrt(pen.R()) * reach * k;
                return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            }
            HoneyStain(s, Somewhere(0.4f), size * s.U(0.16f, 0.24f));   // (no broken grass: at the stain's end it read as a caterpillar with a lit head, "像毛毛虫")
            for (int i = 0, k = 1 + (int)(s.R() * 1.99f); i < k; i++) Husk(s, Somewhere(0.85f), size * s.U(0.36f, 0.48f), s.R() * Tau);
        }

        /// <summary>
        /// The honey granary's floor in patches of other ground (the tint its floor's patches were given barely showed on
        /// it: "看不出湿土、干沙的斑块"): a flat patch, irregular at its edge and thinning out there, either of damp earth a
        /// little darker than the floor, packed, a few glints of wet and seeds pressed into it, or of pale dry sand, grey-
        /// beige, fine and loose, a few small stones in it. size is its width (1.4 to 2.6).
        /// </summary>
        public static void SoilPatch(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            bool damp = s.R() < 0f;   // (dry sand alone: damp earth, however light, read at the ant's eye as a pit, "像地上的坑")
            float r = size * 0.5f, seed = s.R() * 50f, stretch = s.U(0.6f, 1f), yaw = s.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            // (damp brown a little darker than the floor: near black, and darker toward its rim, it read as a pit, "像地上的
            // 坑"; dry sand fine and grey-beige: coarse and pale, grains of rice; a pale gold near the floor's own, not seen)
            Color a = damp ? new Color(0.27f, 0.19f, 0.10f) : new Color(0.045f, 0.035f, 0.018f), b = damp ? new Color(0.34f, 0.25f, 0.14f) : new Color(0.055f, 0.042f, 0.021f);   // (lit by the moon as the floor's points are not: at 0.075, still a pale strip)   // (linear; paler, in a close view a field of pale flakes; at 0.40-0.46, white as snow; at 0.17-0.2, aslant a sheet of white paper)
            int m = s.Count(Mathf.PI * r * r * stretch * 0.65f, 80);
            for (int j = 0; j < m; j++)
            {
                float u = s.S(), v = s.S(), d = Mathf.Sqrt(u * u + v * v);
                float edge = 0.75f + 0.25f * Mathf.PerlinNoise(Mathf.Atan2(v, u) * 1.6f + seed, d * 2f);
                if (d > edge * (0.45f + 0.55f * s.R())) continue;   // its rim broken: thinning out in bits (cut evenly, a sheet of paper)
                float x = u * r, z = v * r * stretch;
                var lp = new Vector3(x * cs - z * sn, damp ? 0.004f : 0.004f + 0.006f * s.R(), x * sn + z * cs);
                bool glint = damp && s.R() < 0.03f;
                Color col = glint ? new Color(0.62f, 0.56f, 0.46f) : Color.Lerp(a, b, s.R());
                s.Put(lp, Vector3.up + new Vector3(s.S(), 0f, s.S()) * (damp ? 0.05f : 0.12f), s.V(col, 0.06f), s.Rad(1.3f), glint ? 0.25f : 0f);   // (covered by few large splats: a patch is wide)
            }
            // in it: seeds pressed into the damp earth (no stones in the sand: they read as glass domes on it, "透明圆顶")
            for (int i = 0, k = damp ? 3 + (int)(s.R() * 4.99f) : 0; i < k; i++)
            {
                float ang = s.R() * Tau, off = Mathf.Sqrt(s.R()) * r * 0.65f, rr = size * s.U(0.025f, 0.05f), deep = damp ? 0.5f : 0.9f;
                var c = new Vector3(Mathf.Cos(ang) * off, rr * 0.3f, Mathf.Sin(ang) * off);
                Color col = damp ? new Color(0.34f, 0.24f, 0.12f) : new Color(0.17f, 0.16f, 0.14f);
                for (int j = 0, mk = s.Count(NestThingsGrowth.EllipsoidArea(rr, rr * 0.6f, rr * deep), 10); j < mk; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, Vector3.right * rr, Vector3.up * (rr * 0.6f), Vector3.forward * (rr * deep), out Vector3 nrm, out Vector3 q);
                    s.Put(pos, nrm, s.V(col * (0.7f + 0.5f * Mathf.Max(0f, q.y)), 0.08f), s.Rad(0.6f));
                }
            }
        }

        /// <summary>A sunflower-seed husk lying on the floor at local c (len: its length), hollow down: a low teardrop dome,
        /// pointed at one end, near black with pale grey stripes along it, as such a seed's; now and then its other half
        /// beside it. (Rolled onto their sides with both their sides drawn, their inside and outside showed through each
        /// other, speckled black and white; flat and pale, they read as chalk marks.)</summary>
        static void Husk(NestThingsGrowth.Spot s, Vector3 c, float len, float yaw)
        {
            Color outer = new Color(0.03f, 0.027f, 0.022f), stripe = new Color(0.24f, 0.23f, 0.20f);   // (linear)
            float w = len * s.U(0.42f, 0.52f), h = w * 0.32f;
            for (int half = 0, halves = s.R() < 0.5f ? 2 : 1; half < halves; half++)
            {
                float yw = yaw + half * s.U(0.4f, 1.4f), cs = Mathf.Cos(yw), sn = Mathf.Sin(yw);
                Vector3 o = c + (half == 0 ? Vector3.zero : new Vector3(-sn, 0f, cs) * (w * s.U(1.1f, 1.6f)));
                int m = s.Count(len * w * 1.2f, 24);
                for (int j = 0; j < m; j++)
                {
                    float x = s.S(), z = s.S();
                    float hw = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x)) * (1f - 0.35f * x);   // teardrop: pointed at x = 1
                    if (Mathf.Abs(z) > hw) continue;
                    float zz = hw > 1e-3f ? z / hw : 0f, env = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x)), py = h * (1f - zz * zz) * env;
                    float px = x * len * 0.5f, pz = z * w * 0.5f;
                    var lp = o + new Vector3(px * cs - pz * sn, py, px * sn + pz * cs);
                    var nl = new Vector3(-sn * zz * 0.9f, 1f, cs * zz * 0.9f);   // its dome's slope across it
                    s.Put(lp, nl, s.V(Mathf.Abs(Mathf.Sin(zz * 5.5f + yw)) > 0.8f ? stripe : outer, 0.08f), s.Rad(0.85f));
                }
            }
        }

        /// <summary>A broken bit of dry grass stalk lying on the floor at local c (len: its length): a thin round stalk, bent
        /// a little, straw-brown, swollen and darker at its nodes, one end split. (Flat pale strips read as chalk marks.)</summary>
        static void GrassBit(NestThingsGrowth.Spot s, Vector3 c, float len, float yaw)
        {
            Color straw = new Color(0.13f, 0.09f, 0.035f), pale = new Color(0.19f, 0.14f, 0.06f), node = new Color(0.06f, 0.04f, 0.015f);   // (linear; paler, it read as a white stick)
            float rad0 = Mathf.Max(0.018f, len * s.U(0.022f, 0.032f)), bend = s.S() * 0.25f, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            int nodes = 2 + (int)(s.R() * 2.99f);
            var across = new Vector3(-sn, 0f, cs);
            for (int j = 0, m = s.Count(Tau * rad0 * len * 1.2f, 30); j < m; j++)
            {
                float t = s.R(), a = s.R() * Mathf.PI;   // round its upper half (its underside is on the floor)
                float x = (t - 0.5f) * len, z = bend * len * (t - 0.5f) * (t - 0.5f);
                float seg = t * (nodes + 1), nd = Mathf.Abs(seg - Mathf.Round(seg));
                bool atNode = nd < 0.07f && t > 0.06f && t < 0.94f;
                float r = rad0 * (atNode ? 1.3f : 1f) * (t > 0.96f ? 0.7f : 1f);   // (its split end thinner)
                Vector3 o = across * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
                var lp = c + new Vector3(x * cs - z * sn, r, x * sn + z * cs) + o * r;
                Color col = atNode ? node : Color.Lerp(straw, pale, 0.35f + 0.65f * Mathf.Sin(a));
                s.Put(lp, o, s.V(col, 0.08f), Mathf.Max(s.Thin, r * 0.9f));
            }
        }

        /// <summary>A honey stain on the floor at local c (r: its radius): a flat, irregular smear, two or three spills run
        /// together, deep amber and glossy, darker at its rim, a little lit from within where it is thickest; a drop or two
        /// standing in it.</summary>
        static void HoneyStain(NestThingsGrowth.Spot s, Vector3 c, float r)
        {
            Color amber = new Color(0.07f, 0.026f, 0.003f), core = new Color(0.62f, 0.36f, 0.08f), rim = new Color(0.02f, 0.007f, 0.001f), shine = new Color(0.95f, 0.92f, 0.80f);   // (linear; at 0.52, a heap of gold coins; at 0.22, the granary's night 1.9 stops brighter, a flat orange sticker)
            int blobs = 2 + (int)(s.R() * 1.99f);
            var bc = new Vector2[blobs];
            var br = new float[blobs];
            for (int i = 0; i < blobs; i++)
            {
                float a = s.R() * Tau, d = i == 0 ? 0f : r * s.U(0.35f, 0.6f);
                bc[i] = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
                br[i] = r * (i == 0 ? 0.65f : s.U(0.35f, 0.5f));
            }
            float seed = s.R() * 40f;
            int m = s.Count(Mathf.PI * r * r * 2.6f, 40);   // (fine and covered: sparse and coarse, it read as flakes of gold leaf, then as gold coins)
            for (int j = 0; j < m; j++)
            {
                float a = s.R() * Tau, d = Mathf.Sqrt(s.R()) * r * 1.1f;
                var q = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
                float depth = 0f;
                for (int i = 0; i < blobs; i++)
                    depth = Mathf.Max(depth, 1f - (q - bc[i]).magnitude / (br[i] * (0.85f + 0.3f * Mathf.PerlinNoise(q.x * 6f + seed, q.y * 6f))));
                if (depth <= 0f) continue;
                bool edge = depth < 0.15f, glint = !edge && s.R() < 0.05f;
                Color col = glint ? shine : edge ? rim : Color.Lerp(amber * 0.9f, core, 0.25f * depth);
                s.Put(c + new Vector3(q.x, 0.004f + 0.012f * depth, q.y), Vector3.up, s.V(col, 0.02f), s.Rad(glint ? 0.4f : 0.6f), glint ? 0.3f : edge ? 0.02f : 0.04f + 0.16f * depth * depth);
            }
            for (int i = 0, k = 1 + (int)(s.R() * 1.99f); i < k; i++)
            {
                float rd = r * s.U(0.1f, 0.16f);
                Vector2 at = bc[(int)(s.R() * blobs) % blobs] + new Vector2(s.S(), s.S()) * (r * 0.15f);
                var dc = c + new Vector3(at.x, rd * 0.7f, at.y);
                int md = s.Count(NestThingsGrowth.EllipsoidArea(rd, rd * 0.8f, rd), 14);
                for (int j = 0; j < md; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, dc, Vector3.right * rd, Vector3.up * (rd * 0.8f), Vector3.forward * rd, out Vector3 nrm, out Vector3 q);
                    if (q.y < -0.6f) continue;
                    bool high = q.y > 0.3f && q.x < -0.2f && q.z < 0.2f && q.z > -0.4f;   // a highlight toward the moon
                    s.Put(pos, nrm, high ? shine : s.V(Color.Lerp(amber, core, 0.3f + 0.4f * Mathf.Max(0f, -q.y)), 0.05f), s.Rad(high ? 0.45f : 0.7f),
                        high ? 0.6f : 0.2f + 0.25f * Mathf.Max(0f, -q.y));
                }
            }
        }

        /// <summary>
        /// The honey granary: a few pollen pellets as foragers carry them home, one to three lying together, each a small
        /// bean, packed and a little flattened as the loads on a forager's legs are, coarse, in its own pollen's colour (yellows, oranges,
        /// ochre, now and then one dark violet: the flowers they came from), matte and powdery (fine paler grains over it),
        /// dark underneath, a dark gap between each and the next; round them a dusting of loose pollen spilled off them;
        /// they do not glow. (Round and in six colours, a row of them read as sweets, "像糖豆"; round and pale in a heap,
        /// as a clutch of eggs.) size is how wide they lie (0.4 to 1).
        /// </summary>
        public static void PollenPellets(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (linear colours: at 0.86 they read pale, near cream; in six colours, olive and violet among them, sweets)
            Color[] pollen = { new Color(0.3f, 0.11f, 0.01f), new Color(0.32f, 0.21f, 0.018f), new Color(0.19f, 0.1f, 0.016f),
                new Color(0.28f, 0.16f, 0.014f), new Color(0.24f, 0.16f, 0.035f), new Color(0.24f, 0.085f, 0.01f) };   // (at 0.5-0.6, pale cream and orange)
            // and in some heaps one dark violet (phacelia's): the one colour in it not gold ("最多加一颗暗紫")
            Color violet = new Color(0.05f, 0.022f, 0.085f), powder = new Color(0.45f, 0.37f, 0.2f);
            bool oneViolet = s.R() < 0.25f;
            Color shadow = new Color(0.035f, 0.025f, 0.015f);
            int want = 1 + (int)(s.R() * 2.99f), first = (int)(s.R() * 5.99f), placed = 0;   // (one to three: four to six in a heap read as a clutch of eggs, "像一窝彩蛋")
            var bc = new Vector3[want];
            var br = new float[want];
            for (int tries = 0; placed < want && tries < 80; tries++)
            {
                float r = size * s.U(0.09f, 0.13f), ang = s.R() * Tau, off = placed == 0 ? 0f : size * s.U(0.14f, 0.32f);   // (a loose heap, not a row)
                var c = new Vector3(Mathf.Cos(ang) * off, r * 0.62f, Mathf.Sin(ang) * off);
                bool apart = true;
                for (int o = 0; o < placed && apart; o++)
                    apart = new Vector2(c.x - bc[o].x, c.z - bc[o].z).magnitude > (r + br[o]) * 1.06f;   // a dark gap between each two
                if (!apart) continue;
                bc[placed] = c;
                br[placed++] = r;
            }
            Color dust = pollen[first];
            for (int i = 0; i < placed; i++)
            {
                float r = br[i], ry = r * 0.62f, rz = r * s.U(0.66f, 0.78f), yaw = s.R() * Tau, bend = r * s.U(0.18f, 0.28f);   // (a packed bean, a little flattened: round, eggs)
                Vector3 c = bc[i], along = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)), across = new Vector3(-along.z, 0f, along.x);
                // its shadow on the floor round its foot
                for (int j = 0, k = s.Count(Mathf.PI * r * r * 1.6f, 12); j < k; j++)
                {
                    float a = s.R() * Tau, d = r * Mathf.Sqrt(s.U(0.5f, 1.6f));
                    s.Put(new Vector3(c.x + Mathf.Cos(a) * d, 0.002f, c.z + Mathf.Sin(a) * d), Vector3.up, s.V(shadow, 0.1f), s.Rad(0.8f));
                }
                Color col = (oneViolet && i == placed - 1 && placed > 1 ? violet : pollen[(first + i) % pollen.Length]) * s.U(0.9f, 1.05f);
                int m = s.Count(NestThingsGrowth.EllipsoidArea(r, ry, rz) * 3.2f, 40);   // (fine splats, more than cover it: sparse, they read as fragments, the floor between them)
                for (int j = 0; j < m; j++)
                {
                    // a pellet: round, a little flattened and bent about its middle
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, along * r, Vector3.up * ry, across * rz, out Vector3 nrm, out Vector3 q);
                    if (q.y < -0.55f) continue;   // (on the floor under it, unseen)
                    float u = Vector3.Dot(pos - c, along) / r;
                    pos += across * (bend * (u * u - 0.35f)) + nrm.normalized * (r * 0.07f * s.S());   // coarse: pollen packed (smooth, an egg)
                    float lit = 0.22f + 0.85f * Mathf.Clamp01(q.y * 0.6f + 0.45f);   // shaded, darkest round its foot
                    bool grain = s.R() < 0.22f;   // powdery: fine paler grains over it
                    s.Put(pos, nrm, s.V((grain ? Color.Lerp(col, powder, 0.35f) : col) * lit * (s.R() < 0.1f ? 0.75f : 1f), 0.05f), s.Rad(grain ? 0.4f : 0.6f));
                }
            }
            // round them, loose pollen spilled off them: a dusting of fine grains, thinning out
            for (int j = 0, k = s.Count(Mathf.PI * size * size * 0.12f, 30); j < k; j++)
            {
                float a = s.R() * Tau, d = size * (0.12f + 0.42f * Mathf.Sqrt(s.R()));
                if (s.R() > 1.2f - d / (size * 0.55f)) continue;
                s.Put(new Vector3(Mathf.Cos(a) * d, 0.003f, Mathf.Sin(a) * d), Vector3.up, s.V(dust * 0.8f, 0.1f), s.Rad(0.35f));
            }
        }

        /// <summary>
        /// The honey granary: honey dripping off a grass stalk. A small tuft of dry grass, dark straw, unlit; its longest
        /// stalk leans out of it and bends over all along it under the honey on it, its seed head and the upper part of it
        /// coated (glossy beads, lit a little from within); from its tip hangs a drop lit from within, a thin string of
        /// honey running from the drop down to a small pool on the floor that glows where it is deepest. (Its first
        /// version, threads between drops on the floor, read as two hollow balls on a line: "这个是什么，不是很清晰"; one
        /// stalk alone, pale, upright and arched over evenly, read from the front as a candle and from the side as a tap:
        /// "像蜡烛", "像水龙头".) size is the stalk's length (0.9 to 2).
        /// </summary>
        public static void HoneyDrip(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color straw = new Color(0.11f, 0.075f, 0.03f), root = new Color(0.045f, 0.03f, 0.012f), seed = new Color(0.15f, 0.10f, 0.04f);   // (dark, linear: paler, from the front it read as a candle; at 0.30, pale gold)
            // (its honey gold-amber: paler, lemon cream, "浅柠檬奶油色、不透", with its string a little tap, "像个小龙头")
            Color honey = new Color(0.2f, 0.08f, 0.008f), core = new Color(0.95f, 0.62f, 0.16f), deep = new Color(0.05f, 0.017f, 0.002f), thin = new Color(0.015f, 0.005f, 0.0006f), shine = new Color(0.98f, 0.95f, 0.85f);   // (a third as light as they read: the granary's night is 1.9 stops brighter)
            float yaw = s.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            Vector3 Turn(Vector3 v) => new Vector3(v.x * cs - v.z * sn, v.y, v.x * sn + v.z * cs);
            Vector3 Q(Vector3 x0, Vector3 x1, Vector3 x2, float t) => (1 - t) * (1 - t) * x0 + 2 * (1 - t) * t * x1 + t * t * x2;
            // the stalk: leaning out of its tuft and bending over all along it, turning a little aside as it goes, its tip
            // below its crown (no upright riser turned over at its top: a tap)
            float L = size, H = L * s.U(0.42f, 0.5f), reach = L * s.U(0.5f, 0.62f), side = L * s.S() * 0.18f;   // (its tip lower: a shorter string)
            Vector3 o0 = Turn(new Vector3(-reach * 0.45f, 0f, -side * 0.5f));   // (its root off centre, so the whole of it is about its place)
            Vector3 a0 = o0, b0 = o0 + Turn(new Vector3(reach * 0.22f, H * 0.66f, side * 0.1f)), b1 = o0 + Turn(new Vector3(reach * 0.62f, H * 1.14f, side * 0.6f)), a1 = o0 + Turn(new Vector3(reach, H * 0.6f, side));
            Vector3 At(float t)
            {
                float u = 1f - t;
                return u * u * u * a0 + 3f * u * u * t * b0 + 3f * u * t * t * b1 + t * t * t * a1;
            }
            const int segs = 16;
            float r0 = Mathf.Max(s.Thin, L * 0.016f), r1 = Mathf.Max(s.Thin, L * 0.008f);   // (a grass's: at 0.026, a thick arm on a short stump; thinner still, its lit honey was all of it)
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
                NestThingsGrowth.Tube(s, At(t0), At(t1), Mathf.Lerp(r0, r1, t0), Mathf.Lerp(r0, r1, t1), Color.Lerp(root, straw, t0 * 1.5f), Color.Lerp(root, straw, t1 * 1.5f));
            }
            // the rest of its tuft: four to six shorter stalks from the same root, fine, leaning away from it every way, unlit
            for (int b = 0, nb = 4 + (int)(s.R() * 2.99f); b < nb; b++)
            {
                float by = yaw + Mathf.PI * s.U(0.5f, 1.5f), bl = L * s.U(0.4f, 0.75f), lean = s.U(0.3f, 0.7f), bc = Mathf.Cos(by), bs = Mathf.Sin(by);
                Vector3 q0 = a0 + new Vector3(s.S(), 0f, s.S()) * (L * 0.03f), q1 = q0 + new Vector3(bc * lean, 1f, bs * lean).normalized * (bl * 0.55f);
                Vector3 q2 = q0 + new Vector3(bc * (lean + 0.5f), 0.75f, bs * (lean + 0.5f)).normalized * bl;
                float rb = r0 * s.U(0.5f, 0.8f);
                for (int i = 0; i < 8; i++)
                {
                    float t0 = i / 8f, t1 = (i + 1) / 8f;
                    NestThingsGrowth.Tube(s, Q(q0, q1, q2, t0), Q(q0, q1, q2, t1), Mathf.Lerp(rb, rb * 0.3f, t0), Mathf.Lerp(rb, rb * 0.3f, t1), Color.Lerp(root, straw, t0 * 1.3f), Color.Lerp(root, straw, t1 * 1.3f));
                }
            }
            // honey along the last third of it: beads hanging under it, glossy, lit a little from within (more, and brighter,
            // the stalk read as a lamp post: "像一盏路灯")
            for (float t = 0.66f; t < 0.93f; t += s.U(0.04f, 0.07f))
            {
                float rb = Mathf.Lerp(r0, r1, t) * s.U(1.3f, 2f);   // (larger, with its drop they read as two balls stacked)
                Vector3 c = At(t) + Vector3.down * (rb * 0.45f);
                int m = s.Count(NestThingsGrowth.EllipsoidArea(rb, rb * 1.1f, rb), 10);
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, Vector3.right * rb, Vector3.up * (rb * 1.1f), Vector3.forward * rb, out Vector3 nrm, out Vector3 q);
                    s.Put(pos, nrm, s.V(Color.Lerp(honey, core, 0.2f + 0.3f * Mathf.Max(0f, -q.y)), 0.08f), s.Rad(0.85f), 0.05f + 0.06f * Mathf.Max(0f, -q.y));
                }
            }
            // its seed head at the tip: husks along the last of it, alternate sides, a few coated
            Vector3 dir = (At(1f) - At(0.94f)).normalized;
            for (int i = 0; i < 8; i++)
            {
                float t = 0.84f + 0.02f * i, rh = L * 0.02f;
                Vector3 c = At(t) + Turn(new Vector3(0f, 0f, (i % 2 == 0 ? 1f : -1f) * rh * 0.6f));
                int m = s.Count(NestThingsGrowth.EllipsoidArea(rh, rh * 0.5f, rh * 0.5f), 10);
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, dir * rh, Vector3.up * (rh * 0.5f), Vector3.Cross(dir, Vector3.up).normalized * (rh * 0.5f), out Vector3 nrm, out _);
                    bool coated = s.R() < 0.3f;
                    s.Put(pos, nrm, s.V(coated ? Color.Lerp(honey, core, 0.3f) : seed, 0.1f), s.Rad(0.8f), coated ? 0.1f : 0f);
                }
            }
            // the drop hanging from its tip: drawn to a neck at the tip, round below; a skin lit from within, a light
            // inside, a highlight toward the moon
            Vector3 tip = At(1f);
            float rd = Mathf.Max(0.05f, L * s.U(0.04f, 0.052f)), hd = rd * 1.5f;   // longer than wide: a drop, not a ball
            Vector3 dc = tip + Vector3.down * (hd * 0.95f);
            int md = s.Count(NestThingsGrowth.EllipsoidArea(rd, hd, rd) * 1.2f, 40);
            for (int j = 0; j < md; j++)
            {
                Vector3 pos = NestThingsGrowth.OnEllipsoid(s, dc, Vector3.right * rd, Vector3.up * hd, Vector3.forward * rd, out Vector3 nrm, out Vector3 q);
                if (q.y > -0.2f)
                {
                    // drawn up to a point at the tip
                    float pinch = Mathf.Pow((q.y + 0.2f) / 1.2f, 1.5f);
                    pos.x = dc.x + (pos.x - dc.x) * (1f - 0.85f * pinch);
                    pos.z = dc.z + (pos.z - dc.z) * (1f - 0.85f * pinch);
                }
                // amber, deepest at its rim and lit below, where the light inside gathers; a highlight toward the moon
                bool high = q.y > 0.0f && q.y < 0.45f && q.x < -0.25f && q.z < 0.1f && q.z > -0.5f;
                float below = Mathf.Max(0f, -q.y);
                s.Put(pos, nrm, high ? shine : s.V(Color.Lerp(deep, Color.Lerp(honey, core, 0.5f), 0.35f + 0.65f * below), 0.06f), s.Rad(high ? 0.5f : 0.85f), high ? 0.35f : 0.05f + 0.3f * below * below);   // (brighter all over, a row of little street lamps, "像小路灯"; the stalk and its drop a candle)
            }
            for (int j = 0, k = md / 2; j < k; j++)
            {
                Vector3 d = InBall(pen, out float f);
                s.Put(dc + new Vector3(d.x * rd, d.y * hd, d.z * rd) * 0.8f, Vector3.up, s.V(Color.Lerp(core, shine, 0.3f * (1f - f)), 0.06f), s.Rad(0.38f), 0.1f - 0.06f * f);
            }
            // the string from the drop down to the floor, thicker where it leaves the drop, beaded once or twice
            Vector3 from = dc + Vector3.down * (hd * 0.92f), to = new Vector3(from.x, 0f, from.z);
            float len = from.y;
            int steps = Mathf.Max(6, Mathf.RoundToInt(len / Mathf.Max(0.004f, s.Thin)));
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps, thick = s.Thin * (0.8f + 1.2f * Mathf.Pow(1f - t, 3f)) * (0.8f + 0.35f * Mathf.Abs(Mathf.Sin(t * 11f + L * 7f)));   // (even, it read as a beam of light)
                bool bead = s.R() < 0.05f;
                s.Put(Vector3.Lerp(from, to, t), Vector3.up, s.V(Color.Lerp(honey, deep, 0.25f), 0.06f), bead ? thick * 2.2f : thick, 0.04f);   // (paler and lit, a white beam)
            }
            // the pool: a low glossy dome of honey on the floor, deep amber at its edge, glowing where it is deepest, its
            // rim catching the light
            float rp = L * s.U(0.08f, 0.11f), rz = rp * s.U(0.7f, 0.95f), hp = rp * 0.06f;   // flat (wider, with its drop it read as a lamp; at 0.11-0.15, white)
            var pc = new Vector3(to.x, 0f, to.z);
            int mp = s.Count(Mathf.PI * rp * rz * 1.3f, 40);
            for (int j = 0; j < mp; j++)
            {
                float u = s.S(), v = s.S(), d2 = u * u + v * v;
                if (d2 > 1f) continue;
                float depth = 1f - d2, dd = Mathf.Sqrt(d2);
                var lp = Turn(new Vector3(u * rp, 0f, v * rz)) + pc + Vector3.up * (hp * Mathf.Sqrt(depth));
                var nrm = new Vector3(Turn(new Vector3(u, 0f, v)).x * 0.5f, 1f, Turn(new Vector3(u, 0f, v)).z * 0.5f);
                bool rim = dd > 0.86f && s.R() < 0.2f;
                s.Put(lp, nrm, rim ? Color.Lerp(core, shine, 0.2f) : s.V(Color.Lerp(thin, Color.Lerp(honey, core, 0.35f), depth * depth), 0.08f), s.Rad(rim ? 0.5f : 0.9f), rim ? 0.08f : 0.02f + 0.16f * depth * depth * depth);
            }
            // at its root, a broken bit of its own straw and a husk or two from its seed head (the granary's way lined with
            // what falls and is dropped: "路边要看得见种壳、碎草")
            for (int i = 0, k = 2 + (int)(s.R() * 1.99f); i < k; i++)
            {
                float a = s.R() * Tau, d = L * s.U(0.08f, 0.3f);
                Vector3 at = a0 + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (i == 0) GrassBit(s, at, L * s.U(0.3f, 0.5f), s.R() * Tau);
                else Husk(s, at, L * s.U(0.12f, 0.2f), s.R() * Tau);
            }
        }

        /// <summary>
        /// The leafcutters' garden: a tuft of the ants' fungus, the garden in small. A spongy grey lump of mycelium
        /// grown over leaf scraps, its surface in soft bumps and holes, dotted on top with a few clusters of tiny pale
        /// knobs (the swollen hyphal tips the ants eat) glowing a soft yellow-green, as a fungus of another world; green
        /// and brown leaf scraps under its edge. size is its width (0.5 to 1.2).
        /// </summary>
        public static void FungusTuft(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (grey, not white: at 0.46 under the moon, its knobs' light merged with it and it read as a ball of cotton)
            // (one warm grey, its holes darker but not black: grey with black holes read as granite)
            Color sponge = new Color(0.27f, 0.26f, 0.22f), shade = new Color(0.18f, 0.17f, 0.14f), pit = new Color(0.10f, 0.12f, 0.05f), knob = new Color(0.60f, 0.78f, 0.24f);
            Color[] scraps = { new Color(0.38f, 0.56f, 0.14f), new Color(0.50f, 0.40f, 0.18f), new Color(0.34f, 0.26f, 0.14f) };
            int lumps = 2 + (int)(s.R() * 2.99f);
            var lr = new float[lumps];
            var lh = new float[lumps];
            var lc = new Vector3[lumps];
            float want = 0f;
            for (int i = 0; i < lumps; i++)
            {
                lr[i] = size * (i == 0 ? 0.32f : s.U(0.14f, 0.24f));
                lh[i] = lr[i] * s.U(0.65f, 0.95f);
                float ang = s.R() * Tau, off = i == 0 ? 0f : size * s.U(0.15f, 0.32f);
                lc[i] = new Vector3(Mathf.Cos(ang) * off, lh[i] * 0.55f, Mathf.Sin(ang) * off);
                want += s.Count(NestThingsGrowth.EllipsoidArea(lr[i], lh[i], lr[i]), 40) * 1.6f;
            }
            // its surface covered (at a fraction of a covering splat's size, a quarter of it was: a heap of flakes); fewer,
            // larger splats when the thing's cap is near
            float fill = Mathf.Clamp((s.Left - 2000f) / Mathf.Max(1f, want), 0.12f, 1f), grow = 1f / Mathf.Sqrt(fill);
            for (int i = 0; i < lumps; i++)
            {
                float r = lr[i], ht = lh[i];
                Vector3 c = lc[i];
                // (fine splats all of a size, more of them: splats of mixed sizes read as a heap of flakes or as granite)
                int m = Mathf.RoundToInt(s.Count(NestThingsGrowth.EllipsoidArea(r, ht, r), 40) * 1.6f * fill);
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, c, Vector3.right * r, Vector3.up * ht, Vector3.forward * r, out Vector3 nrm, out Vector3 q);
                    if (q.y < -0.4f) continue;
                    pos += nrm * (r * 0.06f * Mathf.Sin(pos.x * 37f + pos.z * 29f) * Mathf.Sin(pos.y * 41f));   // spongy bumps
                    // pitted as a sponge: small dark holes all over (whole and pale it read as a ball of cotton)
                    // (holes about a tenth of an ant across, big enough to read from the way)
                    float cellv = Mathf.Abs(Mathf.Sin(pos.x * 38f) + Mathf.Sin(pos.y * 42f - pos.z * 12f) + Mathf.Sin(pos.z * 37f + pos.x * 8f)) / 3f;
                    bool hole = cellv < 0.14f;
                    // (a solid body, shaded from its top to its underside: looser and paler it read as a heap of flakes)
                    Color body = Color.Lerp(shade, sponge, Mathf.Clamp01(0.25f + 0.75f * (q.y + 0.4f) / 1.4f));
                    s.Put(pos - (hole ? nrm.normalized * r * 0.04f : Vector3.zero), nrm, s.V(hole ? pit : body, 0.08f), s.Thin * 1.5f * grow, 0f);
                }
            }
            // the knobs: two or three clusters of small round ones on top of the lumps, glowing a soft yellow-green
            // (scattered all over they read as a spray of sparks; tiny, they were not seen at all)
            // (two or three on its sides read as one white light on one side: four to six, small, on its tops)
            int clusters = 4 + (int)(s.R() * 2.99f);
            for (int c = 0; c < clusters; c++)
            {
                int i = c == 0 ? 0 : (int)(s.R() * (lumps - 0.001f));
                float a = s.R() * Tau, up = s.U(0.78f, 1f), side = Mathf.Sqrt(1f - up * up);
                var qd = new Vector3(Mathf.Cos(a) * side, up, Mathf.Sin(a) * side);
                Vector3 at = lc[i] + new Vector3(qd.x * lr[i], qd.y * lh[i], qd.z * lr[i]);
                Vector3 nn = new Vector3(qd.x / lr[i], qd.y / lh[i], qd.z / lr[i]).normalized;
                // (a few beads apart, each shaded round, its top lit: packed close, glowing alike, a cluster read as one
                // flat white light)
                int k = 5 + (int)(s.R() * 4.99f);
                for (int t = 0; t < k; t++)
                {
                    float kr = size * s.U(0.02f, 0.032f);
                    Vector3 kc = at + new Vector3(s.S(), 0f, s.S()) * (size * 0.09f) + nn * kr;
                    for (int j = 0; j < 20; j++)
                    {
                        Vector3 o = new Vector3(s.S(), s.S(), s.S()).normalized;
                        float top = Mathf.Max(0f, Vector3.Dot(o, nn));
                        s.Put(kc + o * (kr * 0.8f), o, s.V(knob * (0.55f + 0.45f * top), 0.05f), kr * 0.5f, 0.12f + 0.2f * top);
                    }
                }
            }
            int scrapCount = 3 + (int)(s.R() * 4f);
            for (int i = 0; i < scrapCount; i++)
            {
                // (crescents, the bits the ants carry in, mostly fresh green: pale brown ovals read as coins)
                float ang = s.R() * Tau, off = size * s.U(0.35f, 0.6f), w = size * s.U(0.12f, 0.22f), rot = s.R() * Tau, cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                var c = new Vector3(Mathf.Cos(ang) * off, 0.01f, Mathf.Sin(ang) * off);
                Color col = scraps[s.R() < 0.7f ? 0 : 2] * 0.6f;
                int m = s.Count(w * w * 2f, 10);
                for (int j = 0; j < m; j++)
                {
                    float u = s.S(), v = s.S();
                    if (u * u + v * v > 1f || u * u + (v + 0.75f) * (v + 0.75f) < 0.64f) continue;
                    float x = u * w, z = v * w * 0.6f;
                    s.Put(c + new Vector3(x * cr - z * sr, 0.005f, x * sr + z * cr), Vector3.up, s.V(col, 0.1f), s.Rad(0.7f), 0f);
                }
            }
        }

        /// <summary>
        /// The leafcutters' garden: a fallen leaf the ants have been at, crescent bites cut cleanly out of its edges (one
        /// or two, each a half moon about an ant long); dark green, its sides curling up a little, its own edge browning
        /// and faintly lit (its outline reads), its midrib and veins faintly glowing, and the cut rims glowing brightest
        /// in yellow-green, its sap's light (so the bites read, and the leaf is not a garden's). On the floor. size is its
        /// length (1.5 to 2.8).
        /// </summary>
        public static void BittenLeaf(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            float L = size * 0.5f, W = size * s.U(0.28f, 0.36f), yaw = s.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            // (its body dark and its veins dim, the bites' rims the lightest: a light green leaf on the moss hid its bites
            // from the way, "身子暗、部位亮"; its own edge faintly lit, so its outline, and the bites in it, read on a floor of
            // leaves; its sap yellow-green, not white: near white the cut glowed as a white streak)
            Color leaf = Color.Lerp(new Color(0.055f, 0.11f, 0.025f), new Color(0.09f, 0.15f, 0.035f), s.R()), vein = new Color(0.19f, 0.26f, 0.11f), rim = new Color(0.40f, 0.32f, 0.14f);
            Color sap = new Color(0.46f, 0.70f, 0.14f);   // (darker than the floor round it: twice as light it was the leaf, not its bite, that read; paler, the thirty-third, its bite read white)
            float cup = W * 0.14f;   // its sides curl up a little (lying dead flat it was one with the floor)
            int bites = 1 + (int)(s.R() * 1.99f);   // one or two, big (three small ones did not read from the way)
            var bc = new Vector2[bites];
            var br = new float[bites];
            for (int i = 0; i < bites; i++)
            {
                float along = s.U(-0.6f, 0.6f), side = s.R() < 0.5f ? -1f : 1f;
                br[i] = L * s.U(0.36f, 0.48f);   // (bigger and deeper: smaller, they did not read from the way)
                bc[i] = new Vector2(along * L, side * (W * Mathf.Sqrt(1f - along * along) + br[i] * 0.1f));   // deep: its outline changed
            }
            // each bite's cut edge first, along its arc where the leaf is (so the cap never thins it): one unbroken light
            // line from the way, a few brighter beads of sap on it (scattered among the leaf's own splats it read as dots)
            for (int i = 0; i < bites; i++)
            {
                int k = Mathf.Max(32, Mathf.RoundToInt(Tau * br[i] * 1.1f / s.Thin));
                for (int j = 0; j < k; j++)
                {
                    float a = (j + s.R() * 0.5f) * Tau / k, rr = br[i] * s.U(1.03f, 1.13f);
                    float x = bc[i].x + Mathf.Cos(a) * rr, y = bc[i].y + Mathf.Sin(a) * rr, u = x / L, v = y / W;
                    if (u * u + v * v > 1f || Mathf.Abs(v) > (1f - Mathf.Abs(u)) * 1.6f) continue;
                    bool gone = false;
                    for (int o = 0; o < bites; o++) gone |= o != i && (new Vector2(x, y) - bc[o]).magnitude < br[o] * 1.03f;
                    if (gone) continue;
                    bool bead = s.R() < 0.08f;
                    s.Put(new Vector3(x * cs - y * sn, 0.016f + cup * v * v, x * sn + y * cs), Vector3.up, s.V(sap, 0.06f), s.Thin * (bead ? 1.5f : 1.1f), bead ? 0.8f : 0.6f);
                }
            }
            int m = s.Count(size * W * 1.6f, 60);
            for (int j = 0; j < m; j++)
            {
                float u = s.S(), v = s.S();
                if (u * u + v * v > 1f || Mathf.Abs(v) > (1f - Mathf.Abs(u)) * 1.6f) continue;   // an ellipse, pointed at its ends
                float x = u * L, y = v * W;
                bool cut = false, edge = u * u + v * v > 0.88f, bitten = false;
                for (int i = 0; i < bites && !cut; i++)
                {
                    float d = (new Vector2(x, y) - bc[i]).magnitude;
                    if (d < br[i]) cut = true;
                    else if (d < br[i] * 1.2f) bitten = true;
                }
                if (cut) continue;
                // (side veins curving out to the edge, thin: straight, they joined into zigzags and triangles, a wireframe)
                float ay = Mathf.Abs(y) / W;
                bool isVein = Mathf.Abs(v) < 0.03f || Mathf.Pow(Mathf.Max(0f, Mathf.Cos((x / L - 0.55f * ay * ay - 0.25f * ay) * 9f)), 40f) > 0.6f;
                var lp = new Vector3(x * cs - y * sn, 0.012f + cup * v * v, x * sn + y * cs);
                s.Put(lp, Vector3.up, s.V(bitten ? sap : edge ? rim : isVein ? vein : leaf, 0.1f), s.Rad(0.9f), bitten ? 0.6f : edge ? 0.05f : isVein ? 0.08f : 0f);   // (its own edge lit at 0.15 read as a near-white ring)
            }
            // its stalk, out of its base (without one, seen from above, it read as a polygon cut from a sheet, not a leaf)
            float stalk = L * s.U(0.22f, 0.32f);
            int ks = Mathf.Max(12, Mathf.RoundToInt(stalk / (s.Thin * 0.8f)));
            for (int j = 0; j < ks; j++)
            {
                float t = (j + s.R()) / ks, x = -L - stalk * t, y = W * 0.08f * Mathf.Sin(t * 2.2f);
                s.Put(new Vector3(x * cs - y * sn, 0.014f, x * sn + y * cs), Vector3.up, s.V(rim, 0.08f), s.Thin * 1.3f, 0f);
            }
        }

        /// <summary>
        /// The leafcutters' garden: a drift of the cut leaf bits the ants dropped on their way, crescents, half moons
        /// and strips (half to four fifths of an ant long: smaller, they read as specks) lying flat, a few overlapping;
        /// mostly fresh and darker green and yellow-green, some faded yellow and tan, their cut edges a little paler. Not glowing: litter, the garden's own
        /// floor (a moss carpet alone read as the moss forest's). size is the drift's length (1.2 to 2.6).
        /// </summary>
        public static void LeafBits(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (fresh green and yellow-green, a little lighter than the floor's leaves: in its own greens they were lost in
            // it, in pale yellows they read as scraps of paper)
            // (and not much lighter: much lighter than the floor they read as lichen spots)
            // (muted, as leaves are, a midrib each: brighter and plain they read as fluorescent scraps of paper)
            // (lying flat in the moonlight they read lime at 0.24 green, and at 0.21 still brighter than the floor round them,
            // confetti from the way: as dark as the floor's leaves, larger and fewer, so their shapes read)
            Color[] cols = { new Color(0.07f, 0.125f, 0.03f), new Color(0.09f, 0.14f, 0.035f), new Color(0.12f, 0.15f, 0.045f), new Color(0.15f, 0.12f, 0.06f), new Color(0.06f, 0.095f, 0.025f) };
            float[] share = { 0.32f, 0.3f, 0.18f, 0.08f, 0.12f };
            Color paleCut = new Color(0.62f, 0.62f, 0.34f);
            // a drift, longer one way (as they fall beside a trail), denser in its middle
            int bits = 3 + (int)(size * s.U(1.5f, 3f));
            float half = size * 0.5f, wide = s.U(0.45f, 0.7f), yaw = s.R() * Tau, cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            for (int i = 0; i < bits; i++)
            {
                float a = s.R() * Tau, q = Mathf.Sqrt(s.R());
                float dx = Mathf.Cos(a) * q * half, dz = Mathf.Sin(a) * q * half * wide;
                var c = new Vector2(dx * cy - dz * sy, dx * sy + dz * cy);
                float pick = s.R(), acc = share[0];
                int ci = 0;
                while (pick > acc && ci < cols.Length - 1) acc += share[++ci];
                Color col = cols[ci] * s.U(0.85f, 1.15f), edge = Color.Lerp(col, paleCut, 0.35f);
                float len = s.U(0.45f, 0.8f), wid = len * s.U(0.6f, 0.9f), rot = s.R() * Tau, cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                int shape = s.R() < 0.85f ? (int)(s.R() * 1.99f) : 2;   // a crescent (a round bite out of one side), a half moon, now and then a strip
                float lift = 0.008f + 0.006f * i / bits;   // (the later ones on top)
                int m = s.Count(0.8f * len * wid, 10);
                for (int j = 0; j < m; j++)
                {
                    float u = s.S(), v = s.S();
                    if (u * u + v * v > 1f) continue;
                    // how far inside its cut edge (below 0: cut away)
                    float cut = shape == 0 ? Mathf.Sqrt(u * u + (v + 0.75f) * (v + 0.75f)) - 0.8f : shape == 1 ? v + 0.15f : 0.32f - Mathf.Abs(v);
                    if (cut < 0f) continue;
                    float x = u * len * 0.5f, z = v * wid * 0.5f;
                    bool rib = Mathf.Abs(v - (shape == 0 ? 0.55f : shape == 1 ? 0.4f : 0f)) < 0.06f;
                    // (each a little curled, facing a little off the sky: flat up, the moon lit them all alike)
                    Vector3 nrm = new Vector3(-x * 0.9f / len, 1f, -z * 0.9f / wid);
                    s.Put(new Vector3(c.x + x * cr - z * sr, lift + 0.02f * (u * u + v * v) * len, c.y + x * sr + z * cr), new Vector3(nrm.x * cr - nrm.z * sr, 1f, nrm.x * sr + nrm.z * cr), s.V(cut < 0.12f ? edge : rib ? col * 0.7f : col, 0.08f), s.Rad(0.8f));
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
        /// the edges), a soft honey-gold light in their upper halves, their points a little lit, their feet dark. size
        /// is the tallest prism's height (0.8 to 3).
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
                        // its point a little lit, the light gathered in it (as the references' crystals: dark at the foot)
                        float lit = along > tipAt ? 0.35f * (along - tipAt) / (L - tipAt) : 0f;
                        put(pos, nrm, col, radius * (0.35f + 0.25f * pen.R()), 0.1f + 0.2f * (1f - Mathf.Clamp01(nearEdge * 6f)) + lit);
                    }
                }
                // the light inside: a soft patch in its upper half, the foot dark (at full and brightest low down, each
                // cluster read as one glowing lump, its prisms lost)
                int k = Mathf.RoundToInt(m * 1.1f);
                float patch = tipAt * (0.6f + 0.25f * pen.R());
                for (int j = 0; j < k; j++)
                {
                    float along = Mathf.Clamp(patch + (pen.R() + pen.R() - 1f) * tipAt * 0.3f, 0f, L * 0.95f), a = pen.R() * Tau;
                    float shrink = along > tipAt ? 1f - (along - tipAt) / (L - tipAt) : 1f, rr = w * 0.55f * shrink * Mathf.Sqrt(pen.R());
                    Vector3 pos = foot + axis * along + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rr;
                    float f = along / L;
                    put(pos, axis, core * (0.6f + 0.4f * f), radius * (0.3f + 0.25f * pen.R()), 0.15f + 0.45f * f);
                }
            }
        }

        /// <summary>
        /// Amber nodules: one to three lumps of old amber half sunk in the floor, as they weather out of the soil: a dull
        /// crust like a pebble's over most of each, and on one side a window where the crust has broken away: clear amber,
        /// lit from inside in patches, its broken edge catching the light. Nothing caught in it (something caught in amber
        /// is the cliché the kiln's shrine already is); a few chips of amber lying about them. (Amber is no crystal: the
        /// prisms these replace read as gold crystal, jewels on a stage.) size is the biggest lump's width (0.6 to 1.6).
        /// </summary>
        public static void AmberNodules(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            int count = 1 + (int)(s.R() * 2.99f);
            for (int i = 0; i < count; i++)
            {
                float r = size * (i == 0 ? 0.34f : s.U(0.14f, 0.24f)), h = r * s.U(0.65f, 0.85f);
                float ang = s.R() * Tau, off = i == 0 ? 0f : size * s.U(0.32f, 0.52f);
                float fa = s.R() * Tau;   // the broken face: facing out and well up (a little up, from the game's view its window was edge on, a two-tone pebble, k20)
                Nodule(pen, s.Put, a => s.Count(a, 20), s.Rad, new Vector3(Mathf.Cos(ang) * off, h * 0.15f, Mathf.Sin(ang) * off), r, h,
                    new Vector3(Mathf.Cos(fa), s.U(0.55f, 0.95f), Mathf.Sin(fa)).normalized, s.U(0.4f, 0.6f), -1);
            }
            // a few chips of amber broken off and lying about them, each lit a little from within
            for (int i = 0, chips = 3 + (int)(s.R() * 3.99f); i < chips; i++)
            {
                float rc = size * s.U(0.025f, 0.05f), ang = s.R() * Tau, off = size * s.U(0.35f, 0.7f);
                var cc = new Vector3(Mathf.Cos(ang) * off, rc * 0.4f, Mathf.Sin(ang) * off);
                for (int j = 0, mc = s.Count(12.57f * rc * rc, 8); j < mc; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, cc, Vector3.right * rc, Vector3.up * (rc * 0.6f), Vector3.forward * rc, out Vector3 nrm, out Vector3 q);
                    s.Put(pos, nrm, s.V(new Color(0.70f, 0.36f, 0.08f) * (0.7f + 0.5f * Mathf.Max(0f, q.y)), 0.08f), s.Rad(0.6f), 0.18f + 0.15f * Mathf.Max(0f, q.y));
                }
            }
        }

        /// <summary>The amber nest's featured thing, standing beside its way as a landmark (SolidThings.StandNodule): a
        /// boulder of old amber weathering out of the floor, as the nodules but far bigger, broken open toward the way (in
        /// place of the cluster of amber prisms at its heart, which read as gold crystal, a jewel on a stage); two small
        /// lumps beside it. (A fern caught in it read as a centipede or a fishbone from the way.) face: toward the way.</summary>
        public static void HeartNodule(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float perArea, Vector3 face)
        {
            var f = new Frame(pen, p, n, ta, tb);
            // (splats big enough to close its surface: at the amber prisms' size, which kept gaps on purpose, its window
            // read as gold flecks over black)
            float radius = 1.05f / Mathf.Sqrt(Mathf.Max(1f, perArea));
            int Count(float area) => Mathf.Max(20, Mathf.RoundToInt(area * perArea));
            float Rad(float k) => radius * k;
            float r = size * 0.42f, h = r * 0.8f;
            Vector3 toWay = new Vector3(Vector3.Dot(face, ta), 0f, Vector3.Dot(face, tb)).normalized;
            Nodule(pen, f.Put, Count, Rad, new Vector3(0f, h * 0.12f, 0f), r, h, (toWay * 0.75f + Vector3.up * 0.55f).normalized, 0.74f, -1);
            for (int i = 0; i < 2; i++)
            {
                float rs = size * f.U(0.1f, 0.16f), ang = Mathf.Atan2(toWay.z, toWay.x) + (i == 0 ? 1.2f : -1.5f) + f.S() * 0.3f, fa = f.R() * Tau;
                Nodule(pen, f.Put, Count, Rad, new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (r * 1.35f) + Vector3.up * (rs * 0.1f), rs, rs * 0.75f,
                    new Vector3(Mathf.Cos(fa), 0.5f, Mathf.Sin(fa)).normalized, f.U(0.6f, 0.78f), -1);
            }
        }

        /// <summary>One lump of amber (AmberNodules, HeartNodule) round local centre c, half-axes r, h, r, whatever is
        /// under the floor left out: a dark weathered crust like a stone's over most of it, and where the crust has
        /// broken away round fn (the cap of the unit lump past cut) a window of clear amber, glossy, a little sunk, its
        /// broken edge a thin line catching the light. The window shows what is inside it on its own surface, as if seen
        /// through: two or three patches of warm light, gold at their middles and deep amber toward their edges, and the
        /// dark shape of what the resin caught (0 a seed, 1 a needle, 2 a fern frond). (A face cut flat across read as a
        /// dome of gold flecks; drawn under an opaque window, the light and the fern did not show at all.) put draws a
        /// local splat, count how many cover an area, rad a splat's radius (times a scale).</summary>
        static void Nodule(ThingPen pen, System.Action<Vector3, Vector3, Color, float, float> put, System.Func<float, int> count, System.Func<float, float> rad,
            Vector3 c, float r, float h, Vector3 fn, float cut, int caughtKind)
        {
            Color crust0 = new Color(0.09f, 0.075f, 0.06f), crust1 = new Color(0.23f, 0.20f, 0.17f);   // (weathered: grey-brown, as a pebble; at 0.34 twice as light as its amber, "a grey pebble, one end painted orange")
            Color rimRed = new Color(0.50f, 0.13f, 0.025f), deep = new Color(0.66f, 0.28f, 0.05f), gold = new Color(1.0f, 0.60f, 0.14f), edgeLit = new Color(0.80f, 0.40f, 0.10f), caught = new Color(0.05f, 0.03f, 0.015f);
            Color V(Color col, float amount) => col * (1f + (pen.R() * 2f - 1f) * amount);
            // a lump, not a ball (its surface swelling and sinking a little), and its window's edge ragged, broken along
            // an uneven line (a round lump cut straight across read as a two-tone egg or an acorn)
            float sl = pen.R() * 40f, se = pen.R() * 40f;
            float Swell(Vector3 q) => 1f + 0.16f * (Mathf.PerlinNoise(q.x * 1.3f + sl, q.z * 1.3f + q.y * 1.1f) - 0.5f);
            float Edge(Vector3 q) => cut + 0.2f * (Mathf.PerlinNoise(q.x * 2.6f + se, q.z * 2.6f + q.y * 2.1f) - 0.5f);
            Vector3 Local(Vector3 q) => c + new Vector3(q.x * r, q.y * h, q.z * r) * Swell(q);
            Vector3 Normal(Vector3 q) => new Vector3(q.x / r, q.y / h, q.z / r).normalized;
            float top = Mathf.Max(0.01f, c.y + h);
            NestThingsGrowth.Perp(fn, out Vector3 e1, out Vector3 e2);
            float capR = Mathf.Sqrt(Mathf.Max(0f, 1f - cut * cut));   // the window's radius on the unit lump
            // the crust: solid, a little lumpy in colour, darker toward the floor it sits in; its broken edge round the
            // window a thin bright line
            int m = count(NestThingsGrowth.EllipsoidArea(r, h, r) * 1.05f);
            float seed = pen.R() * 40f;
            for (int j = 0; j < m; j++)
            {
                Vector3 q = InBall(pen, out float len);
                if (len < 0.05f) continue;
                q /= len;
                float d = Vector3.Dot(q, fn), ed = Edge(q);
                Vector3 pos = Local(q);
                if (pos.y < 0f || d > ed) continue;
                bool edge = d > ed - 0.035f;
                float lump = Mathf.PerlinNoise(q.x * 3.1f + seed, q.z * 3.1f + q.y * 2.3f);
                Color col = edge ? edgeLit : Color.Lerp(crust0, crust1, lump) * (0.6f + 0.4f * Mathf.Clamp01(pos.y / top));
                // (rough: its surface lifted and sunk a little by the same lumps)
                put(edge ? pos : Local(q * (1f + 0.05f * (lump - 0.5f))), Normal(q), V(col, edge ? 0.05f : 0.16f), rad(edge ? 0.55f : 1f), edge ? 0.2f : 0f);
            }
            // what is inside, in the window's own coordinates (u along e1, v along e2, both within capR): the light
            // patches, and the caught thing's strokes (a line from a to b, its half width)
            int patches = 2 + (int)(pen.R() * 1.99f);
            var spots = new Vector3[patches];   // (u, v, spread)
            for (int i = 0; i < patches; i++)
            {
                float pa = pen.R() * Tau, pr = capR * (0.1f + 0.45f * pen.R());
                spots[i] = new Vector3(Mathf.Cos(pa) * pr, Mathf.Sin(pa) * pr, capR * (0.22f + 0.14f * pen.R()));
            }
            var strokes = new System.Collections.Generic.List<(Vector2 a, Vector2 b, float w)>();
            float lay = pen.R() * Tau;
            Vector2 dirL = new Vector2(Mathf.Cos(lay), Mathf.Sin(lay)), acrossL = new Vector2(-dirL.y, dirL.x);
            Vector2 mid0 = new Vector2(pen.R() * 2f - 1f, pen.R() * 2f - 1f) * (capR * (caughtKind == 2 ? 0.06f : 0.25f));
            if (caughtKind < 0) { }
            else if (caughtKind == 2)
            {
                // a fern frond: its stem, and leaflets in pairs, angled toward its tip, shorter toward it
                float half = capR * 0.7f;
                strokes.Add((mid0 - dirL * half, mid0 + dirL * half, capR * 0.04f));
                for (int pair = 0; pair < 9; pair++)
                {
                    float t = -0.8f + 1.6f * pair / 8f, len = half * 0.44f * (1f - 0.55f * (t + 0.8f) / 1.6f);
                    Vector2 at = mid0 + dirL * (t * half);
                    for (int sideK = -1; sideK <= 1; sideK += 2)
                        strokes.Add((at, at + (acrossL * sideK + dirL * 0.45f).normalized * len, capR * 0.026f));
                }
            }
            else if (caughtKind == 1) strokes.Add((mid0 - dirL * capR * 0.55f, mid0 + dirL * capR * 0.55f, capR * 0.018f));
            else strokes.Add((mid0 - dirL * capR * 0.1f, mid0 + dirL * capR * 0.1f, capR * 0.07f));
            float Ink(Vector2 uv)
            {
                float best = 0f;
                foreach (var (a, b, w) in strokes)
                {
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(uv - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    float dist = (uv - (a + ab * t)).magnitude;
                    best = Mathf.Max(best, 1f - Mathf.SmoothStep(0f, 1f, (dist - w) / (w * 0.8f)));
                }
                return best;
            }
            // the window: clear amber on the lump's own curve, a little sunk below the crust, glossy; a deep orange-red at
            // its broken edge, amber, then gold toward its middle and over the patches, never white; dark over what it
            // caught. A third of its splats a layer further in, deep amber, so no gap between the others shows the lump's
            // dark inside (k20: "full of black seams, gold leaf or a shell with holes", its middle milky white)
            int mw = count(2f * Mathf.PI * (1f - cut) * r * Mathf.Max(r, h) * 2.2f);
            for (int j = 0; j < mw; j++)
            {
                float z = (cut - 0.1f) + (1.1f - cut) * pen.R(), phi = pen.R() * Tau;
                z = Mathf.Min(z, 1f);
                float rho = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                Vector3 qs = fn * z + (e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi)) * rho;
                if (z < Edge(qs)) continue;   // (the crust still there)
                bool under = j % 3 == 0;
                Vector3 q = qs * (under ? 0.9f : 0.965f);
                Vector3 pos = c + new Vector3(q.x * r, q.y * h, q.z * r) * Swell(qs);
                if (pos.y < 0f) continue;
                var uv = new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * rho;
                float light = 0f;
                foreach (Vector3 sp in spots)
                {
                    float d2 = ((uv - new Vector2(sp.x, sp.y)) / sp.z).sqrMagnitude;
                    light = Mathf.Max(light, Mathf.Exp(-1.6f * d2));
                }
                // smooth and glassy, one piece (each splat varied, small, it read as gold dust or crystallised honey): its
                // colour hardly varied splat to splat, larger splats (a soft gloss band across it read as a seam down it,
                // then whitened its middle: one or two glints instead, below)
                float ink = Ink(uv), ripple = 0.97f + 0.03f * Mathf.Sin(rho * 25f + phi * 0.5f);
                float inward = 1f - Mathf.Clamp01(rho / Mathf.Max(1e-3f, capR));   // 0 at its broken edge, 1 at its middle
                Color col = under ? deep : Color.Lerp(Color.Lerp(rimRed, deep, Mathf.Clamp01(inward * 2.5f)), gold, Mathf.Clamp01(0.55f * inward + 0.45f * light)) * ripple;
                col = Color.Lerp(col, caught, 0.85f * ink);
                put(pos, Normal(q), V(col, 0.02f), rad(under ? 1.5f : 1.25f), (under ? 0.1f : 0.12f + 0.16f * inward + 0.18f * light) * (1f - ink));   // (lit from within, gold at most: at 0.16 + 0.5 by its patches they bloomed white)
            }
            // one or two glints of glass on it, small, where the light catches it
            for (int g = 0, glints = 1 + (int)(pen.R() * 1.99f); g < glints; g++)
            {
                float pa = pen.R() * Tau, pr = capR * (0.2f + 0.45f * pen.R());
                Vector3 qg = (fn * Mathf.Sqrt(Mathf.Max(0f, 1f - pr * pr)) + (e1 * Mathf.Cos(pa) + e2 * Mathf.Sin(pa)) * pr).normalized;
                if (Vector3.Dot(qg, fn) < Edge(qg) + 0.04f) continue;
                Vector3 gc = c + new Vector3(qg.x * r, qg.y * h, qg.z * r) * Swell(qg);
                for (int j = 0; j < 3; j++)
                    put(gc + (e1 * (pen.R() - 0.5f) + e2 * (pen.R() - 0.5f)) * (r * 0.05f), Normal(qg), new Color(0.98f, 0.9f, 0.74f), rad(1.4f), 0.3f);
            }
        }

        /// <summary>
        /// Resin running from a wound: a short broken branch stub standing in the floor (a little tilted, its bark furrowed
        /// up and down, its broken top jagged and pale), split open near its top, and from the split two or three runs of
        /// resin flowing down its side, thin where they leave the wound and fuller as they go, each ending in a teardrop:
        /// hanging from the bark where it stops part way down, or in a small pool where it reaches the floor. The resin a
        /// glassy amber over a warm light, brightest in the drops, a glint on each; the bark dark (the references: light in
        /// parts, the body dark). (Lying on the floor, on a piece of bark, the runs read as yellow jelly fungus on a branch:
        /// resin reads as resin when it runs down.) size is the stub's height (0.6 to 1.2).
        /// </summary>
        public static void ResinRun(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color bark0 = new Color(0.08f, 0.05f, 0.03f), bark1 = new Color(0.22f, 0.14f, 0.075f), wood = new Color(0.45f, 0.32f, 0.18f);
            Color skin = new Color(0.36f, 0.14f, 0.025f), rimLit = new Color(0.82f, 0.42f, 0.10f), core = new Color(0.85f, 0.45f, 0.11f), glint = new Color(0.98f, 0.88f, 0.66f);
            float H = size * s.U(0.8f, 1f), R = size * s.U(0.13f, 0.18f), lean = s.U(0.05f, 0.2f), turn = s.R() * Tau;
            Vector3 axis = (Vector3.up + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * lean).normalized;
            NestThingsGrowth.Perp(axis, out Vector3 u, out Vector3 v);
            // a point on its bark: y up it (0..1), a round it; its foot a little wider, its broken top jagged
            float seed = s.R() * 30f, slant = s.R() * Tau;
            // (broken off aslant and raggedly, high on one side, low on the other, its edge jagged: at 0.14 it read as a post,
            // flat-topped; at 0.26 in gentle waves still "a sawn stump", k20)
            float TopAt(float a) => H * (0.7f + 0.13f * Mathf.Cos(a - slant) + 0.1f * Mathf.PerlinNoise(a * 2.8f + seed, seed) + 0.06f * Mathf.PerlinNoise(a * 9f + seed, seed * 0.5f));
            Vector3 Bark(float y, float a, float lift) => axis * y + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * (R * (1f + 0.25f * Mathf.Max(0f, 1f - y / (H * 0.2f))) + lift);
            Vector3 BarkN(float a) => u * Mathf.Cos(a) + v * Mathf.Sin(a);
            float furrows = Mathf.Round(s.U(7f, 11f));
            int m = s.Count(Tau * R * H * 1.1f, 40);
            for (int j = 0; j < m; j++)
            {
                float a = s.R() * Tau, y = s.R() * TopAt(a);
                float ridge = Mathf.Abs(Mathf.Sin(a * furrows + 1.3f * Mathf.Sin(y / H * 4f + seed)));
                Color col = Color.Lerp(bark0, bark1, ridge) * (0.65f + 0.35f * y / H);   // (darker toward its foot)
                s.Put(Bark(y, a, R * 0.08f * ridge), BarkN(a), s.V(col, 0.12f), s.Rad(0.85f), 0f);
            }
            // its broken top: pale splintered wood across it, sloping with the break
            int mt = s.Count(Mathf.PI * R * R * 1.2f, 16);
            for (int j = 0; j < mt; j++)
            {
                float a = s.R() * Tau, rr = R * Mathf.Sqrt(s.R());
                Vector3 pos = axis * Mathf.Lerp(H * 0.66f, TopAt(a), rr / R) + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rr;
                s.Put(pos, axis, s.V(wood, 0.15f), s.Rad(0.7f), 0f);
            }
            // and splinters standing up from the break, long and short, leaning out a little: pale where they split from
            // the wood, bark-dark on their outer side (a branch broken off, not sawn)
            for (int i = 0, splinters = 4 + (int)(s.R() * 3.99f); i < splinters; i++)
            {
                float a = s.R() * Tau, len = H * s.U(0.08f, 0.3f), w = R * s.U(0.14f, 0.26f);
                Vector3 outN = BarkN(a), foot = Bark(TopAt(a) - H * 0.04f, a, -R * s.U(0.08f, 0.35f));
                Vector3 dir = (axis + outN * s.U(0.05f, 0.35f) + (u * s.S() + v * s.S()) * 0.08f).normalized, across = Vector3.Cross(dir, outN).normalized;
                for (int j = 0, ms = s.Count(w * len * 1.3f, 8); j < ms; j++)
                {
                    float t = (j + s.R()) / ms, wt = w * (1f - 0.8f * t);
                    Vector3 at = foot + dir * (t * len) + across * (s.S() * wt * 0.6f);
                    s.Put(at - outN * (wt * 0.2f), -outN + dir * 0.3f, s.V(wood, 0.15f), s.Rad(0.6f) * (1f - 0.4f * t), 0f);
                    s.Put(at + outN * (wt * 0.2f), outN, s.V(bark1, 0.12f), s.Rad(0.6f) * (1f - 0.4f * t), 0f);
                }
            }
            // the wound: a short split down its side near the top, pale wood in it (below the break on its low side too);
            // the runs leave from its foot
            float aw = s.R() * Tau, yw = Mathf.Min(H * s.U(0.55f, 0.68f), TopAt(aw) - H * 0.12f);
            for (int j = 0, mw = s.Count(R * 0.5f * H * 0.18f, 10); j < mw; j++)
                s.Put(Bark(yw + s.S() * H * 0.08f, aw + s.S() * 0.12f, R * 0.06f), BarkN(aw), s.V(wood * 1.15f, 0.1f), s.Rad(0.6f), 0f);
            int runs = 2 + (int)(s.R() * 1.99f);
            for (int k = 0; k < runs; k++)
            {
                // (round the stub, so one shows from wherever it is seen: from one split alone, seen from its other side a
                // stub showed no resin at all)
                float a0 = aw + k * Tau / runs + s.S() * 0.3f, drift = s.S() * 0.35f, end = k == 0 ? 0f : s.U(0.15f, 0.45f);   // (the first reaches the floor)
                float y0 = yw - H * 0.08f, y1 = Mathf.Max(0.02f, end * yw);
                Vector3 At(float t) => Bark(Mathf.Lerp(y0, y1, t), a0 + drift * t * t, R * 0.1f + size * 0.02f * t);
                const int steps = 12;
                Vector3 prev = At(0f);
                for (int q = 1; q <= steps; q++)
                {
                    float t0 = (q - 1) / (float)steps, t1 = q / (float)steps;
                    Vector3 cur = At(t1);
                    NestThingsGrowth.Tube(s, prev, cur, size * Mathf.Lerp(0.018f, 0.05f, t0), size * Mathf.Lerp(0.018f, 0.05f, t1),
                        Color.Lerp(skin, rimLit, 0.12f + 0.2f * t0), Color.Lerp(skin, rimLit, 0.12f + 0.2f * t1), 0.05f + 0.1f * t1);
                    prev = cur;
                }
                // a light inside it, along its middle, brightening as it fills (one even colour, the run read as a stripe of
                // yellow paint: resin is dark at its edges and lit through its middle)
                for (int j = 0, mc = s.Count((y0 - y1) * size * 0.03f, 16); j < mc; j++)
                {
                    float t = s.R();
                    Vector3 outN = BarkN(a0 + drift * t * t);
                    s.Put(At(t) + outN * (size * Mathf.Lerp(0.018f, 0.05f, t) * 0.55f), outN, s.V(core, 0.06f), s.Rad(0.35f), 0.22f + 0.28f * t);
                }
                // its drop: hanging from the bark (a teardrop, its point up), or a small pool where it reaches the floor
                float rd = size * s.U(0.06f, 0.08f);
                bool pool = end <= 0f;
                Vector3 outward = BarkN(a0 + drift);
                Vector3 cd = pool ? new Vector3(At(1f).x, 0f, At(1f).z) + outward * rd * 1.2f + Vector3.up * (rd * 0.3f) : At(1f) + outward * (rd * 0.6f) - Vector3.up * (rd * 0.9f);
                // (solid: at 0.95 of its surface in splats half the usual size, and an inside at half as many, the drop read
                // as a ball of gold dots with black seams, k20; its pool wider, a little puddle of resin at the stub's foot)
                Vector3 ey = Vector3.up * (pool ? rd * 0.2f : rd * 1.25f);
                Vector3 ex = pool ? outward * rd * 2.3f : outward * rd;
                Vector3 ez = Vector3.Cross(Vector3.up, outward).normalized * (pool ? rd * 2f : rd);
                int md = s.Count(NestThingsGrowth.EllipsoidArea(ex.magnitude, ey.magnitude, ez.magnitude) * 1.8f, 32);
                for (int j = 0; j < md; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, cd, ex, ey, ez, out Vector3 nrm, out Vector3 qq);
                    if (pool && qq.y < -0.1f) continue;   // (a pool only its top: its whole shell read as an empty husk at the stub's foot)
                    if (!pool && qq.y > 0f)   // drawn up to a point where it hangs from the run
                    {
                        float pinch = qq.y * qq.y;
                        pos = cd + (pos - cd - Vector3.up * Vector3.Dot(pos - cd, Vector3.up)) * (1f - 0.65f * pinch) + Vector3.up * (Vector3.Dot(pos - cd, Vector3.up) * (1f + 0.4f * pinch));
                    }
                    bool isGlint = qq.y > 0.2f && qq.x > 0.3f && qq.z < 0.1f && qq.z > -0.5f;
                    float low = Mathf.Max(0f, -qq.y);   // (a hanging drop brightest where it gathers, at its foot)
                    Color col = isGlint ? glint : Color.Lerp(skin, rimLit, 0.35f + 0.45f * low);
                    s.Put(pos, nrm, s.V(col, 0.05f), s.Rad(isGlint ? 0.5f : 0.65f), isGlint ? 0.5f : 0.14f + 0.24f * low);
                }
                for (int j = 0, mi = Mathf.RoundToInt(md * 0.7f); j < mi; j++)
                {
                    Vector3 d = InBall(pen, out float f);
                    s.Put(cd + ex * (d.x * 0.75f) + ey * (d.y * 0.7f) + ez * (d.z * 0.75f), Vector3.up, s.V(core, 0.06f), s.Rad(0.5f), 0.3f * (1f - 0.5f * f));
                }
            }
        }

        /// <summary>
        /// Pine needles, the amber nest's (k21): two to five bundles fallen from the conifers its resin comes from, lying
        /// crossed on the floor beside the way: each bundle two needles, long, thin and straight, several ants long, a dry
        /// russet brown, paler toward their tips, held together at their foot in a short dark sheath. No light of their own.
        /// (Its ground texture's needle ends read as dark smudges from the game's view and as gold confetti close by, k20.)
        /// size is the longest needle's length.
        /// </summary>
        public static void PineNeedles(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color dark = new Color(0.07f, 0.035f, 0.014f), pale = new Color(0.13f, 0.065f, 0.025f), sheath = new Color(0.035f, 0.028f, 0.022f);   // (at 0.12-0.24 by a beam's light they lay as glowing orange streaks, k21)
            float spin = s.R() * Tau;
            for (int i = 0, bundles = 2 + (int)(s.R() * 3.99f); i < bundles; i++)
            {
                float ang = spin + i * Tau / bundles + s.S() * 1.1f, len = size * s.U(0.65f, 1f), half = 0.028f * s.U(0.85f, 1.15f);
                var foot = new Vector3(s.S(), 0f, s.S()) * (size * 0.25f);
                var along = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Color tone = Color.Lerp(dark, pale, s.R());
                for (int k = 0; k < 2; k++)
                {
                    // the pair a little apart toward their tips, one a little shorter
                    float a = ang + (k == 0 ? -1f : 1f) * s.U(0.03f, 0.12f), lk = len * (k == 0 ? 1f : s.U(0.85f, 1f));
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    int steps = Mathf.CeilToInt(lk / (half * 1.1f));
                    for (int j = 0; j < steps; j++)
                    {
                        float t = (j + s.R()) / steps;
                        Vector3 at = foot + dir * (t * lk) + Vector3.up * (0.02f + half * 0.6f);
                        Color col = Color.Lerp(tone, tone * 1.45f, Mathf.Clamp01((t - 0.6f) / 0.4f));
                        s.Put(at, Vector3.up, s.V(col, 0.08f), half * (1f - 0.5f * t * t), 0f);
                    }
                }
                // the sheath: a short dark collar round their foot, a little thicker than the pair
                for (int j = 0; j < 8; j++)
                    s.Put(foot + along * (len * s.U(0f, 0.05f)) + Vector3.up * (0.02f + half), Vector3.up, s.V(sheath, 0.1f), half * 1.5f, 0f);
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
        /// Charred twigs, long after the fire (the kiln temple, redesigned 2026-10-27): two to four burnt sticks lying across
        /// each other, their charcoal cracked into the square checks burnt wood takes, silver grey on the raised checks, a
        /// little grey ash settled on top. Deep in a few cracks along the grain toward one end, and in that end where it
        /// broke, an ember still glows: small, most of a twig is dark (the light follows the grain, as the user wants a
        /// marking to follow how a thing grew). An antenna's touch brightens it (SolidThings). size is the longest twig.
        /// </summary>
        public static void CharTwigs(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (its sheen and its ash darker and fewer than k3's, whose twigs read as pale tan sticks from above; darker again
            // in k11: lit up close, a pale grey X like glass rods, k10 content review)
            Color char0 = new Color(0.035f, 0.03f, 0.03f), sheen = new Color(0.075f, 0.071f, 0.074f), ash = new Color(0.18f, 0.172f, 0.165f);
            // a crust or two of lichen on some, so long after the fire the first thing on the charred wood: a pale warm grey,
            // in its world's colours (k11-k11c grey-green, a cool note for its family, a bright green blob at twice the chroma;
            // the user, 10-29: "在自己的环境中，所有植物应该偏向这个环境色", a green mapped into its red, not kept green)
            Color lichenDark = new Color(0.085f, 0.072f, 0.064f), lichenPale = new Color(0.17f, 0.152f, 0.136f);
            // (hot from 0.3 green: k2's embers read orange; both toward crimson since k8: bright, a red turns orange on the
            // screen, at 1, 0.09, 0.04 they read 18-21°, k7 colour review)
            Color ember = new Color(0.85f, 0.02f, 0.04f), hot = new Color(1.0f, 0.05f, 0.05f);   // (k8b: at 0.9, 0.035, 0.035, bright, 12°)
            int twigs = 2 + (int)(s.R() * 3f);
            float spin = s.R() * Tau;
            for (int t = 0; t < twigs; t++)
            {
                // (twice as thick as k1's, which lay on the floor as a thin streak of coal bits: a twig is a log to an ant)
                float len = size * (t == 0 ? 1f : s.U(0.45f, 0.85f)), rad = Mathf.Max(0.07f, len * s.U(0.045f, 0.065f));
                float dir = spin + t * s.U(0.7f, 1.5f);   // (crossing the ones under it)
                var ax = new Vector3(Mathf.Cos(dir), 0f, Mathf.Sin(dir));
                var side = new Vector3(-ax.z, 0f, ax.x);
                Vector3 mid = side * (s.S() * size * 0.16f) + ax * (s.S() * size * 0.1f);
                float bend = s.S() * 0.1f * len, warm = t == 0 ? 1f : s.U(0.15f, 0.6f);   // (one end still warm; the first twig most)
                float checkU = rad * s.U(1.6f, 2.4f);   // the checks about as long as they are wide (six round the twig)
                int cracks = 6, m = Mathf.Min(s.Left / Mathf.Max(1, twigs - t), s.Count(Tau * rad * len, 60));
                // its lichen: on about half the twigs, one to three lobed crusts on its top, along its cooler length
                int lichens = s.R() < 0f ? 1 + (int)(s.R() * 2.99f) : 0;   // (none since k13: a whole pale section read as a glass rod, k12)
                var lu = new float[lichens];
                var la = new float[lichens];
                var lr = new float[lichens];
                for (int q = 0; q < lichens; q++) { lu[q] = s.U(0.3f, 0.95f); la[q] = s.U(-0.8f, 0.8f); lr[q] = rad * s.U(1f, 2f); }
                for (int j = 0; j < m; j++)
                {
                    float u = s.R(), a = s.R() * Tau;
                    // thinner toward the far end; bowed a little; the upper twigs rest on the ones under them
                    float r = rad * (1f - 0.35f * u) * (1f + 0.05f * Mathf.Sin(a * 3f + u * 17f));
                    float rest = rad * (1f + 0.9f * t * Mathf.Max(0f, 1f - Mathf.Abs(u - 0.5f) * 2.2f));
                    var o = new Vector3(0f, Mathf.Cos(a), 0f) + side * Mathf.Sin(a);
                    Vector3 at = mid + ax * ((u - 0.5f) * len) + side * (bend * 4f * u * (1f - u)) + Vector3.up * rest + o * r;
                    bool lichen = false;
                    for (int q = 0; q < lichens && !lichen; q++)
                    {
                        float du = (u - lu[q]) * len, da = Mathf.DeltaAngle(a * Mathf.Rad2Deg, la[q] * Mathf.Rad2Deg) * Mathf.Deg2Rad * r;
                        float lobe = lr[q] * (0.82f + 0.22f * Mathf.Sin(Mathf.Atan2(da, du) * 5f + q * 1.7f));
                        lichen = du * du + da * da < lobe * lobe;
                    }
                    if (lichen)
                    {
                        s.Put(at + o * (r * 0.06f), o, s.V(Color.Lerp(lichenDark, lichenPale, s.R()), 0.08f), s.Rad(0.5f));
                        continue;
                    }
                    // where it lies in the checks: along the grain between six cracks, across at even steps
                    float along = u * len / checkU, round = a / Tau * cracks + 0.15f * Mathf.Sin(along * 1.7f);
                    float dAlong = Mathf.Abs(along - Mathf.Round(along)) * checkU, dRound = Mathf.Abs(round - Mathf.Round(round)) * (Tau * r / cracks);
                    bool crack = dRound < r * 0.09f || dAlong < r * 0.08f;
                    if (crack)
                    {
                        // an ember deep in a crack along the grain, toward the warm end, in a few of the checks
                        float cell = Mathf.Floor(along) * 7.31f + Mathf.Round(round) * 3.17f;
                        bool lit = dRound < r * 0.09f && Mathf.Repeat(Mathf.Sin(cell) * 43758.5f, 1f) < 0.5f * warm * (1f - u) * (1f - u);
                        // (dimmer than k3's 0.55-0.85, whose embers blew out orange, k3 colour review)
                        if (lit) s.Put(at - o * (r * 0.12f), o, s.V(Color.Lerp(ember, hot, s.R() * 0.4f), 0.1f), s.Rad(0.42f), s.U(0.32f, 0.5f));
                        else s.Put(at - o * (r * 0.1f), o, s.V(char0 * 0.6f, 0.2f), s.Rad(0.45f));
                        continue;
                    }
                    // the checks: dark charcoal, silver on their raised middles; ash settled on top
                    float raised = Mathf.Min(dAlong / checkU, dRound / (Tau * r / cracks)) * 2f;
                    Color col = Color.Lerp(char0, sheen, Mathf.Clamp01(raised * 1.2f) * s.U(0.4f, 1f));
                    if (o.y > 0.55f && s.R() < 0.08f) col = Color.Lerp(col, ash, s.U(0.5f, 1f));
                    s.Put(at, o, s.V(col, 0.12f), s.Rad(0.55f));
                }
                // its broken end, where the twig snapped: a ragged face, the warm one with embers in its middle
                Vector3 end = mid - ax * (0.5f * len) + Vector3.up * rad;
                int f = s.Count(3.1416f * rad * rad * 1.3f, 14);
                for (int j = 0; j < f; j++)
                {
                    float q = Mathf.Sqrt(s.R()), a = s.R() * Tau;
                    Vector3 at = end + (Vector3.up * Mathf.Cos(a) + side * Mathf.Sin(a)) * (q * rad) - ax * (s.R() * rad * 0.15f);
                    bool glowing = q < 0.6f && s.R() < 0.7f * warm;
                    // (as dim as its cracks' embers: at 0.5-0.8 its broken end blew out orange, k3 and k6 colour reviews)
                    s.Put(at, -ax, s.V(glowing ? Color.Lerp(ember, hot, (1f - q) * 0.4f) : char0, 0.12f), s.Rad(0.45f), glowing ? s.U(0.32f, 0.5f) * (1f - q * 0.5f) : 0f);
                }
            }
        }

        /// <summary>
        /// Cold ash, the kiln's (k11): a low drift of fine ash the wind left on the floor, its edge broken into lobes, a cool
        /// blue grey (ash long cold under the night sky), paler where it lies deepest, a few flecks of charcoal in it. One of
        /// its few cool notes, with its slate's edges, its twigs' lichen and its weathered wood (its colour family 9-11°
        /// without them; as patches mixed into its floor the cold ash came out a neutral grey, k10). No light of its own.
        /// size is the drift's width.
        /// </summary>
        public static void AshDrift(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (k11b: OKLCH C about 0.06, which the kiln's warm moon turns to about 0.035 on the screen: at k11's 0.036 it came out
            // a neutral grey, C 0.020)
            // (k12b: a pale warm grey, ash's own, on its now redder floor: its blue greys, k11-k12, read as blue patches there, and the
            // user's rule since 10-29, everything in its world's colours; k11b's pale blue read as snow by the ant)
            Color pale = new Color(0.142f, 0.121f, 0.11f), deep = new Color(0.073f, 0.062f, 0.056f), coal = new Color(0.03f, 0.028f, 0.03f);
            float rx = size * 0.5f, rz = rx * s.U(0.55f, 0.85f), h = size * s.U(0.04f, 0.07f), yaw = s.R() * Tau;
            float ph1 = s.R() * Tau, ph2 = s.R() * Tau;
            var ex = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var ez = new Vector3(-ex.z, 0f, ex.x);
            int m = s.Count(3.1416f * rx * rz * 1.2f);
            for (int j = 0; j < m; j++)
            {
                // a point inside its lobed outline, on a low dome thinning out toward its edge
                float ang = s.R() * Tau, rr = Mathf.Sqrt(s.R());
                float lobe = 1f + 0.18f * Mathf.Sin(3f * ang + ph1) + 0.1f * Mathf.Sin(7f * ang + ph2);
                if (rr > lobe * s.U(0.85f, 1f)) continue;
                float x = Mathf.Cos(ang) * rr * rx, z = Mathf.Sin(ang) * rr * rz, t = rr / lobe;
                Vector3 pos = ex * x + ez * z + Vector3.up * (h * (1f - t * t) + NestThingsGrowth.Lift);
                Vector3 nrm = (Vector3.up + (ex * (x / (rx * rx)) + ez * (z / (rz * rz))) * (2f * h)).normalized;
                float sp = s.R();
                Color col = sp < 0.06f ? coal : Color.Lerp(deep, pale, Mathf.Clamp01(1.2f - t) * s.U(0.7f, 1f));
                s.Put(pos, nrm, s.V(col, 0.06f), s.Rad(sp < 0.06f ? 0.6f : 0.9f));
            }
        }

        /// <summary>
        /// Stones in the ash, the kiln's (k7c): one, now and then two, small flat slabs of dark slate, a cool blue grey,
        /// their outlines angular, their tops flat, sunk half into the ash, a little lighter where they are worn on top, a
        /// few flecks of ash settled on them. Its few cool accents, scattered singly (the world's pebbles, heaps of big round
        /// stones, read as things from elsewhere: lilac in the palette's accent, k6; dark slate, still heaps of boulders at
        /// the way in, k7c; as round ellipsoids they read as pebbles, not slate, k7d). No light of their own. size is the
        /// larger stone's length.
        /// </summary>
        public static void AshStones(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            Color ash = new Color(0.2f, 0.19f, 0.18f);
            int count = s.R() < 0.3f ? 2 : 1;
            float spin = s.R() * Tau;
            for (int i = 0; i < count; i++)
            {
                float k = s.U(0.85f, 1.15f);
                // (a cool slate grey, OKLCH about 251°, C 0.028: k7d's 0.05, 0.062, 0.088 hardly read as cool; at 282° they
                // read lavender, k8; at 242° and fuller a teal-blue sponge, "blue confetti", k8b)
                // (k10: OKLCH 0.40 / 0.034 / 251°, and still "a blue thing", k10 content review. k11: its body a grey, C 0.02,
                // its cool only where light catches it, its top's straight edge and its layers, lighter and bluer, C 0.04:
                // one of the kiln's few cool notes)
                Color slate = new Color(0.0568f * k, 0.065f * k, 0.0766f * k * s.U(0.97f, 1.03f));   // (k13: a grey slate, C 0.012; at 0.02-0.036 a blue-grey lump on the red-brown floor, k12)
                Color edge = new Color(0.0853f * k, 0.1137f * k, 0.158f * k);   // (k14: OKLCH 0.48 / 0.03 / 255°, faintly cool where light catches it; 0.02 in k13, too little to count; 0.035 in k12b, 0.06 in k11c, blue)
                // (thicker and less sunk than k10's, whose small ones read as blue dots)
                float a = (i == 0 ? size : size * s.U(0.4f, 0.7f)) * 0.5f, c = a * s.U(0.6f, 0.85f), b = a * s.U(0.24f, 0.34f);
                float ph1 = s.R() * Tau;
                int sides = 4 + (int)(s.R() * 2.99f);   // its outline a few straight sides with sharp corners, as split slate
                float wedge = Tau / sides, rot = s.R() * Tau;
                Vector3 at = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(spin), 0f, Mathf.Sin(spin)) * ((size * 0.5f + a) * s.U(0.9f, 1.3f));
                // lying flat, a little tilted, turned any way, half in the ash
                float yaw = s.R() * Tau;
                Vector3 ey = (Vector3.up + new Vector3(s.S(), 0f, s.S()) * 0.15f).normalized;
                Vector3 h0 = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 ex = (h0 - ey * Vector3.Dot(h0, ey)).normalized, ez = Vector3.Cross(ex, ey);
                Vector3 cc = at + Vector3.up * (b * s.U(0.45f, 0.65f));
                int m = s.Count(NestThingsGrowth.EllipsoidArea(a, b, c) * 1.7f);   // (finer: at k10's size its straight edges blurred round)
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = NestThingsGrowth.OnEllipsoid(s, cc, ex * a, ey * b, ez * c, out Vector3 nrm, out Vector3 q);
                    // a slab: its outline a polygon (straight sides, sharp corners; sine-waved it read as round lumps,
                    // pebbles, k8), its top and bottom pressed flat
                    Vector3 l = pos - cc;
                    float lx = Vector3.Dot(l, ex), ly = Vector3.Dot(l, ey), lz = Vector3.Dot(l, ez);
                    float ang = Mathf.Atan2(lz / c, lx / a), local = Mathf.Repeat(ang - rot, wedge) - wedge * 0.5f;
                    float side = Mathf.Cos(wedge * 0.5f) / Mathf.Cos(local);
                    float flat = Mathf.Sign(ly) * b * Mathf.Sqrt(Mathf.Abs(ly) / b);
                    pos = cc + ex * (lx * side) + ey * flat + ez * (lz * side);
                    if (Mathf.Abs(ly) > 0.6f * b) nrm = Vector3.Lerp(nrm.normalized, ey * Mathf.Sign(ly), 0.7f);
                    if (pos.y < NestThingsGrowth.Lift) continue;   // sunk in the ash
                    float up = Mathf.Clamp01(nrm.normalized.y);
                    Color col = slate * Mathf.Lerp(0.8f, 1.25f, up * up);   // worn lighter on top, darker where it meets the ash
                    float rad = s.Rad(0.85f), sp = s.R(), rim = Mathf.Abs(ly) / b;
                    // round its edge, two or three paler, cooler lines: its layers; and the straight edge round its top
                    if (rim < 0.6f && Mathf.Abs(Mathf.Repeat(ly / b * 1.6f + ph1, 1f) - 0.5f) < 0.07f) col = edge * s.U(0.85f, 1.05f);
                    else if (rim > 0.62f && rim < 0.86f && ly > 0f) col = Color.Lerp(col, edge, 0.75f);
                    else if (up > 0.6f && sp < 0.1f) col = ash * s.U(0.8f, 1.1f);   // a fleck of ash on it
                    else if (sp < 0.18f) { col *= sp < 0.14f ? 0.7f : 1.3f; rad *= 0.6f; pos += nrm.normalized * 0.003f; }   // a grain in the stone
                    s.Put(pos, nrm, s.V(col, 0.05f), rad);
                }
            }
        }

        /// <summary>
        /// Charcoal balls (King Alfred's cakes, Daldinia: a fungus of burnt wood), the kiln's own living thing after the fire:
        /// a cluster of hard round knobs, black-brown with a faint silver bloom and fine pores, half sunk in the ash; one or
        /// two broken open show the inside the fungus is known by, rings of black and silver grey round its middle, its
        /// growth zones, which glow softly, the inner rings the more (its marking follows how it grew: the user's rule). The
        /// others only the faint light every living thing has (SolidThings.Living). size is the cluster's width.
        /// </summary>
        public static void CharBalls(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            // (its bloom and its rings greyer and dimmer than k2's, whose white specks and white ring discs read as frost and
            // as targets drawn on the floor)
            Color skin = new Color(0.03f, 0.022f, 0.02f), bloom = new Color(0.07f, 0.065f, 0.068f), silver = new Color(0.26f, 0.25f, 0.25f);
            int balls = 3 + (int)(s.R() * 6f), open = 0;
            for (int i = 0; i < balls; i++)
            {
                float r = size * s.U(0.11f, 0.22f) * (i == 0 ? 1.3f : 1f);   // (k1's, smaller, read as a heap of gravel)
                var c = new Vector3(s.S() * size * 0.3f, r * 0.5f, s.S() * size * 0.3f);   // (half sunk)
                bool broken = open < 2 && s.R() < (i == 0 ? 0.85f : 0.3f);
                Vector3 cut = Vector3.zero, e1 = Vector3.zero, e2 = Vector3.zero;
                if (broken)
                {
                    open++;
                    cut = new Vector3(s.S(), s.U(-0.1f, 0.45f), s.S()).normalized;   // (broken off at the side, seen from the way)
                    if (cut.sqrMagnitude < 0.5f) cut = Vector3.right;
                    e1 = Vector3.Cross(cut, Mathf.Abs(cut.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    e2 = Vector3.Cross(cut, e1);
                }
                int m = Mathf.Min(s.Left / Mathf.Max(1, balls - i), s.Count(Tau * r * r, 30));
                for (int j = 0; j < m; j++)
                {
                    Vector3 d = InBall(pen, out _);
                    if (d.sqrMagnitude < 1e-3f) continue;
                    d.Normalize();
                    if (d.y < -0.4f) continue;   // (its foot in the ash)
                    if (broken && Vector3.Dot(d, cut) > 0.3f) continue;   // (the broken part: its face below)
                    Color col = s.R() < 0.04f ? skin * 0.45f : Color.Lerp(skin, bloom, Mathf.Pow(s.R(), 2f) * Mathf.Clamp01(d.y + 0.3f));   // pores; the bloom on top
                    s.Put(c + d * r, d, s.V(col, 0.12f), s.Rad(0.75f));   // (0.45 in k3: the knobs read hollow, like glass bubbles)
                }
                if (!broken) continue;
                // the broken face: rings round its middle, black and silver, the silver ones lit, the inner ones more
                Vector3 o = c + cut * (r * 0.3f);
                float rr = r * 0.95f;
                int k = s.Count(3.1416f * rr * rr * 1.6f, 30);
                for (int j = 0; j < k; j++)
                {
                    float q = Mathf.Sqrt(s.R()), a = s.R() * Tau;
                    float ring = Mathf.Sin(q * 3.5f * Tau * 0.5f + 0.4f);
                    bool pale = ring > 0.25f;
                    float glow = pale ? Mathf.Lerp(0.4f, 0.12f, q) : 0f;
                    s.Put(o + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * (q * rr), cut, s.V(pale ? silver : skin, 0.08f), s.Rad(0.38f), glow);
                }
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
