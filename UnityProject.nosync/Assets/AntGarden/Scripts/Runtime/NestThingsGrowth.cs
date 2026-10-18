using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The small natural things on the canyon floor, at ant scale: moss cushions, clover, mushrooms, seedlings, pebbles,
    /// snail shells, lichen and crystal grains. Each is drawn with a ThingPen as opaque splats spread evenly over its
    /// surfaces, every splat facing the way its surface faces, so the moonlight shows the form. A generator stands its
    /// thing at p on the floor (n up, ta and tb along the floor); size is the thing's main dimension in units. No splat
    /// sits lower than Lift above the floor, and no thing has more than Cap of them.
    /// </summary>
    public static class NestThingsGrowth
    {
        /// <summary>The most splats one thing may have.</summary>
        public const int Cap = 9000;

        /// <summary>How high above the floor plane the lowest splat sits.</summary>
        public const float Lift = 0.012f;

        const float Tau = 6.2831853f;

        /// <summary>
        /// A thing's place on the floor and the pen it is drawn with. Points and normals are local: x along ta, y up along
        /// n, z along tb, from p. Put keeps every splat at least Lift above the floor and stops at Cap.
        /// </summary>
        struct Spot
        {
            readonly ThingPen pen;
            readonly Vector3 p, a, n, b;
            readonly int start;

            public Spot(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb)
            {
                this.pen = pen;
                this.p = p;
                this.n = n;
                a = ta;
                b = tb;
                start = pen.into.Count;
            }

            /// <summary>The thing's next random number, in [0, 1).</summary>
            public float R() => pen.R();

            /// <summary>A random number between lo and hi.</summary>
            public float U(float lo, float hi) => lo + (hi - lo) * pen.R();

            /// <summary>A random number between -1 and 1.</summary>
            public float S() => pen.R() * 2f - 1f;

            /// <summary>A splat radius that covers a surface at the pen's density (times scale).</summary>
            public float Rad(float scale = 1f) => pen.Radius(scale);

            /// <summary>The smallest radius a fibre's splats may have (the typical Radius(0.5)).</summary>
            public float Thin => 0.31f / Mathf.Sqrt(pen.PerArea);

            /// <summary>How many splats cover this much surface (unit²), at least min.</summary>
            public int Count(float area, int min = 8) => pen.Count(area, min);

            /// <summary>How many more splats this thing may have.</summary>
            public int Left => Cap - (pen.into.Count - start);

            /// <summary>One splat at local point lp facing local normal ln: linear colour c, radius r, glow.</summary>
            public void Put(Vector3 lp, Vector3 ln, Color c, float r, float glow = 0f)
            {
                if (pen.into.Count - start >= Cap) return;
                if (lp.y < Lift) lp.y = Lift;
                pen.Put(p + a * lp.x + n * lp.y + b * lp.z, a * ln.x + n * ln.y + b * ln.z, c, r, glow);
            }

            /// <summary>Colour c varied for one splat: its brightness by about amount, each channel a little.</summary>
            public Color V(Color c, float amount = 0.1f)
            {
                float k = 1f + S() * amount;
                return new Color(c.r * k * (1f + S() * 0.04f), c.g * k * (1f + S() * 0.04f), c.b * k * (1f + S() * 0.04f), 1f);
            }
        }

        /// <summary>A palette colour as one thing has it: brightness k, warmth w (red up and blue down by w).</summary>
        static Color Tint(Color c, float k, float w) => new Color(c.r * k * (1f + w), c.g * k, c.b * k * (1f - w), 1f);

        /// <summary>Two unit vectors at right angles to unit d and to each other.</summary>
        static void Perp(Vector3 d, out Vector3 u, out Vector3 v)
        {
            u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            v = Vector3.Cross(d, u);
        }

        /// <summary>An ellipsoid's surface area from its half-axes (Thomsen's formula, within about 1 %).</summary>
        static float EllipsoidArea(float a, float b, float c)
        {
            float ap = Mathf.Pow(a, 1.6075f), bp = Mathf.Pow(b, 1.6075f), cp = Mathf.Pow(c, 1.6075f);
            return 12.566371f * Mathf.Pow((ap * bp + ap * cp + bp * cp) / 3f, 1f / 1.6075f);
        }

        /// <summary>
        /// A point spread evenly over an ellipsoid round local centre c, its half-axes the orthogonal local vectors ex,
        /// ey, ez. Gives the point, its outward normal, and q: where it lies on the unit sphere (q.x along ex, q.y along
        /// ey, q.z along ez), for colouring.
        /// </summary>
        static Vector3 OnEllipsoid(Spot s, Vector3 c, Vector3 ex, Vector3 ey, Vector3 ez, out Vector3 nrm, out Vector3 q)
        {
            float a = ex.magnitude, b = ey.magnitude, d = ez.magnitude, m = Mathf.Min(a, Mathf.Min(b, d));
            Vector3 g = Vector3.up;
            q = Vector3.up;
            for (int k = 0; k < 16; k++)
            {
                float z = s.S(), f = s.R() * Tau, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                q = new Vector3(rr * Mathf.Cos(f), z, rr * Mathf.Sin(f));
                g = new Vector3(q.x / a, q.y / b, q.z / d);
                if (s.R() < g.magnitude * m) break;   // keeps the spread even where the ellipsoid is stretched
            }
            nrm = ex * (g.x / a) + ey * (g.y / b) + ez * (g.z / d);
            return c + ex * q.x + ey * q.y + ez * q.z;
        }

        /// <summary>
        /// A tapered tube from local a0 (radius r0, colour c0) to a1 (r1, c1). A thick one is covered over its surface; a
        /// thin one (stalk, fibre) is a row of splats along its axis about a radius apart, each its own radius and facing
        /// out at its own angle round the axis. fill below 1 covers a thick one with fewer, larger splats.
        /// </summary>
        static void Tube(Spot s, Vector3 a0, Vector3 a1, float r0, float r1, Color c0, Color c1, float glow = 0f, float fill = 1f)
        {
            Vector3 d = a1 - a0;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            d /= len;
            Perp(d, out Vector3 u, out Vector3 v);
            float rm = (r0 + r1) * 0.5f, slope = (r0 - r1) / len;
            if (rm < 0.025f)
            {
                int k = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(rm, 0.004f)));
                for (int i = 0; i < k; i++)
                {
                    float t = (i + 0.5f + s.S() * 0.25f) / k, f = s.R() * Tau, r = Mathf.Lerp(r0, r1, t);
                    Vector3 o = u * Mathf.Cos(f) + v * Mathf.Sin(f);
                    s.Put(a0 + d * (len * t) + o * (r * 0.25f), o + d * slope, s.V(Color.Lerp(c0, c1, t), 0.08f), Mathf.Max(r, s.Thin), glow);
                }
                return;
            }
            int m = s.Count(Tau * rm * len * fill, 2);
            float grow = 1f / Mathf.Sqrt(fill);
            for (int i = 0; i < m; i++)
            {
                float t = s.R(), f = s.R() * Tau, r = Mathf.Lerp(r0, r1, t);
                Vector3 o = u * Mathf.Cos(f) + v * Mathf.Sin(f);
                s.Put(a0 + d * (len * t) + o * r, o + d * slope, s.V(Color.Lerp(c0, c1, t)), s.Rad() * grow, glow);
            }
        }

        // ------------------------------------------------------------------------------------------------- moss

        /// <summary>
        /// A moss cushion: a lumpy low dome made of countless tiny upright fronds, dark green deep down and bright
        /// yellow-green at their tips, with 5 to 15 thin reddish stalks standing out of it, each nodding a small capsule.
        /// size is its diameter (0.6 to 2).
        /// </summary>
        public static void MossCushion(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            float k = s.U(0.9f, 1.1f), w = s.U(-0.06f, 0.06f);
            Color deep = Tint(new Color(0.06f, 0.16f, 0.05f), k, w), tip = Tint(new Color(0.25f, 0.42f, 0.08f), k, w);
            Color seta = Tint(new Color(0.3f, 0.12f, 0.06f), k, w), capsule = Tint(new Color(0.35f, 0.25f, 0.1f), k, w);
            float rm = size * 0.5f, h = size * s.U(0.31f, 0.39f);
            float a1 = s.U(0.05f, 0.11f), a2 = s.U(0.03f, 0.07f), a3 = s.U(0.02f, 0.05f);
            float p1 = s.R() * Tau, p2 = s.R() * Tau, p3 = s.R() * Tau;
            int setae = 5 + (int)(s.R() * 11f);

            // the dome: frond tips over its surface, a quarter of them with a short fibre standing out of it; a big
            // cushion gets fewer, larger splats so it stays covered within Cap
            int want = s.Count(0.5f * EllipsoidArea(rm, rm, h) * 1.03f);
            float fill = Mathf.Clamp(((float)s.Left - setae * 90) / (want * 1.75f), 0.1f, 1f);
            int count = Mathf.RoundToInt(want * fill);
            float grow = 1f / Mathf.Sqrt(fill);
            float rmax = rm * (1f + a1 + a2 + a3), wmax = rmax * rmax / Mathf.Min(rm * (1f - a1 - a2 - a3), h);
            for (int i = 0; i < count; i++)
            {
                float qy = 0f, qr = 1f, th = 0f, rho = rm;
                for (int t = 0; t < 12; t++)
                {
                    qy = s.R();
                    qr = Mathf.Sqrt(1f - qy * qy);
                    th = s.R() * Tau;
                    rho = rm * (1f + a1 * Mathf.Sin(2f * th + p1) + a2 * Mathf.Sin(3f * th + p2) + a3 * Mathf.Sin(5f * th + p3));
                    if (s.R() * wmax < rho * rho * Mathf.Sqrt(qr * qr / (rho * rho) + qy * qy / (h * h))) break;
                }
                float drho = rm * (2f * a1 * Mathf.Cos(2f * th + p1) + 3f * a2 * Mathf.Cos(3f * th + p2) + 5f * a3 * Mathf.Cos(5f * th + p3));
                Vector3 rad = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th)), side = new Vector3(-rad.z, 0f, rad.x);
                Vector3 nrm = ((rad - side * (drho / rho)) * (qr / rho) + Vector3.up * (qy / h)).normalized;
                Vector3 pos = rad * (rho * qr) + Vector3.up * (h * qy);
                float tt = Mathf.Clamp01(0.3f + 0.5f * s.R() + 0.25f * qy) * Mathf.Clamp01(0.45f + qy * 3f);
                if (s.R() < 0.15f) tt *= 0.35f;   // dark gaps between the fronds
                Vector3 fn = (nrm + new Vector3(s.S(), s.S(), s.S()) * 0.25f).normalized;
                s.Put(pos + nrm * (s.S() * 0.008f), fn, s.V(Color.Lerp(deep, tip, tt)), s.Rad() * grow);
                if ((i & 3) == 0)
                {
                    Vector3 dir = (nrm + Vector3.up * 0.4f + new Vector3(s.S(), s.S(), s.S()) * 0.3f).normalized;
                    float len = s.U(0.03f, 0.07f);
                    for (int j = 0; j < 3; j++)
                    {
                        float f = j * 0.5f;
                        s.Put(pos + dir * (len * f - 0.01f), fn, s.V(Color.Lerp(deep, tip, 0.25f + 0.75f * f)), s.Rad(0.6f) * grow);
                    }
                }
            }

            // the sporophytes: thin reddish stalks bending a little outward, each nodding a small ovoid capsule
            for (int j = 0; j < setae; j++)
            {
                float th = s.R() * Tau, rn = Mathf.Sqrt(s.R()) * 0.7f;
                float rho = rm * (1f + a1 * Mathf.Sin(2f * th + p1) + a2 * Mathf.Sin(3f * th + p2) + a3 * Mathf.Sin(5f * th + p3));
                Vector3 rad = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                Vector3 b0 = rad * (rho * rn) + Vector3.up * (h * Mathf.Sqrt(1f - rn * rn) - 0.015f);
                Vector3 lean = rad * (0.05f + 0.25f * rn) + new Vector3(s.S(), 0f, s.S()) * 0.1f;
                float len = s.U(0.2f, 0.5f), r = s.U(0.009f, 0.013f);
                Vector3 prev = b0;
                for (int q = 1; q <= 6; q++)
                {
                    float f = q / 6f;
                    Vector3 cur = b0 + (Vector3.up * f + lean * (f * f)) * len;
                    Tube(s, prev, cur, r, r * 0.95f, Color.Lerp(seta * 0.75f, seta, f - 1f / 6f), Color.Lerp(seta * 0.75f, seta, f));
                    prev = cur;
                }
                Vector3 dir = (Vector3.up + lean * 2.5f).normalized;
                float cl = s.U(0.028f, 0.042f), cw = cl * s.U(0.5f, 0.65f);
                Perp(dir, out Vector3 u, out Vector3 v);
                Vector3 cc = prev + dir * (cl * 0.9f);
                int m = s.Count(EllipsoidArea(cw, cl, cw), 10);
                for (int i = 0; i < m; i++)
                {
                    Vector3 pos = OnEllipsoid(s, cc, u * cw, dir * cl, v * cw, out Vector3 nrm, out Vector3 q);
                    s.Put(pos, nrm, s.V(Color.Lerp(capsule * 0.8f, capsule, 0.5f + 0.5f * q.y)), s.Rad(0.6f));
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- clover

        /// <summary>
        /// A clover patch: 3 to 7 trefoils, each a thin green stalk holding three heart-shaped leaflets out nearly flat,
        /// dark green with a pale chevron band and a light midrib, their edges cupped up a little. size is the patch's
        /// radius (0.6 to 1.5).
        /// </summary>
        public static void CloverPatch(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            float k = s.U(0.9f, 1.1f), w = s.U(-0.05f, 0.05f);
            Color stem = Tint(new Color(0.1f, 0.24f, 0.08f), k, w), leaf = Tint(new Color(0.08f, 0.24f, 0.08f), k, w);
            Color band = Tint(new Color(0.3f, 0.42f, 0.28f), k, w), rib = Color.Lerp(leaf, band, 0.55f);
            int count = 3 + (int)(s.R() * 5f);
            float bandAt = s.U(0.3f, 0.42f), spin = s.R() * Tau;
            for (int i = 0; i < count; i++)
            {
                // trefoils spread over the patch on a jittered sunflower spiral, so they rarely crowd
                float ang = spin + i * 2.39996f + s.S() * 0.3f, dist = size * 0.8f * Mathf.Sqrt((i + 0.5f) / count) * s.U(0.8f, 1.1f);
                Vector3 b0 = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                float hgt = s.U(0.3f, 0.7f), lf = s.U(0.35f, 0.6f), r = s.U(0.013f, 0.018f);
                Vector3 lean = new Vector3(s.S(), 0f, s.S()) * 0.12f + b0 * (0.1f / Mathf.Max(size, 0.1f));
                Vector3 prev = b0;
                for (int q = 1; q <= 6; q++)
                {
                    float f = q / 6f;
                    Vector3 cur = b0 + (Vector3.up * f + lean * (f * f)) * hgt;
                    Tube(s, prev, cur, r, r * 0.9f, Color.Lerp(stem * 1.25f, stem, f - 1f / 6f), Color.Lerp(stem * 1.25f, stem, f));
                    prev = cur;
                }
                float rot = s.R() * Tau;
                for (int l = 0; l < 3; l++)
                {
                    float phi = rot + l * 2.0943951f + s.S() * 0.2f, tilt = s.U(-0.25f, 0.18f);
                    float len = lf * s.U(0.9f, 1.05f), cup = s.U(0.25f, 0.45f), droop = s.U(0.05f, 0.2f);
                    Vector3 m = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    Vector3 c = new Vector3(-m.z, 0f, m.x);
                    m = m * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt);
                    Vector3 up = Vector3.Cross(c, m);
                    int want = s.Count(0.52f * len * len);
                    for (int j = 0; j < want; j++)
                    {
                        float x = 0.5f, y = 0f;
                        for (int t = 0; t < 20; t++)
                        {
                            x = s.R() * 0.95f;
                            y = s.S() * 0.44f;
                            if (InLeaflet(x, y)) break;
                        }
                        // a gentle spoon: edges lifted by cup, the tip dropping by droop
                        float z = cup * y * y - droop * x * x;
                        Vector3 nrm = up + m * (2f * droop * x) - c * (2f * cup * y);
                        Vector3 pos = prev + (m * (x + 0.03f) + c * y + up * z) * len;
                        float bd = Mathf.Abs(x - (bandAt + 0.6f * Mathf.Abs(y)));
                        Color col = leaf;
                        if (bd < 0.06f && x > 0.1f) col = Color.Lerp(band, leaf, bd / 0.06f * 0.5f);
                        else if (Mathf.Abs(y) < 0.018f) col = rib;
                        s.Put(pos, nrm, s.V(col), s.Rad());
                    }
                }
            }
        }

        /// <summary>Whether (x along the midrib from the base, y across, in leaflet lengths) lies in a clover leaflet: a
        /// narrow wedge widening into two rounded lobes with a notch between them at the tip.</summary>
        static bool InLeaflet(float x, float y)
        {
            float ay = Mathf.Abs(y);
            if (x < 0f) return false;
            if (x <= 0.68f && ay <= 0.44f * x / 0.68f) return true;
            float dx = x - 0.68f, dy = ay - 0.17f;
            return dx * dx + dy * dy <= 0.0729f;
        }

        // ------------------------------------------------------------------------------------------------- mushrooms

        /// <summary>
        /// A cluster of 1 to 4 mushrooms: cream stems with bulbous feet, convex caps (tan, red-brown with small white
        /// spots, or pale grey-violet) and radial gills under them that glow softly, the only part that does; the smaller
        /// ones lean outward. size is the tallest one's height (0.5 to 1.8).
        /// </summary>
        public static void Mushrooms(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            float k = s.U(0.9f, 1.1f), w = s.U(-0.05f, 0.05f), kind = s.R();
            bool spotted = kind >= 0.4f && kind < 0.75f;
            Color cap = Tint(kind < 0.4f ? new Color(0.42f, 0.32f, 0.2f) : spotted ? new Color(0.38f, 0.12f, 0.07f) : new Color(0.35f, 0.32f, 0.42f), k, w);
            Color stem = Tint(new Color(0.45f, 0.42f, 0.36f), k, w * 0.5f), dot = new Color(0.6f, 0.58f, 0.55f);
            Color gill = new Color(0.25f, 0.45f, 0.48f);
            float glow = s.U(0.35f, 0.6f);
            int count = 1 + (int)(s.R() * 4f);

            // heights first: a big cluster gets fewer, larger splats so it stays covered within Cap
            float h1 = size, h2 = size * s.U(0.45f, 0.85f), h3 = size * s.U(0.4f, 0.8f), h4 = size * s.U(0.35f, 0.75f);
            float want = 0f;
            for (int i = 0; i < count; i++)
            {
                float h = i == 0 ? h1 : i == 1 ? h2 : i == 2 ? h3 : h4;
                want += s.Count(1.94f * h * h + 0.53f * h * h * h, 0) * 1.1f + 60f;
            }
            float fill = Mathf.Clamp((s.Left - 200f) / want, 0.1f, 1f), grow = 1f / Mathf.Sqrt(fill);

            float spin = s.R() * Tau;
            for (int i = 0; i < count; i++)
            {
                float h = i == 0 ? h1 : i == 1 ? h2 : i == 2 ? h3 : h4;
                float ang = spin + i * 2.2f + s.S() * 0.5f;
                Vector3 outw = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 b0 = outw * (size * (i == 0 ? 0.05f * s.R() : s.U(0.22f, 0.4f)));
                Vector3 lean = outw * (i == 0 ? 0.03f : 0.12f + 0.3f * (1f - h / size)) + new Vector3(s.S(), 0f, s.S()) * 0.05f;
                float rs = h * s.U(0.065f, 0.095f), rc = h * s.U(0.38f, 0.5f), hc = rc * s.U(0.45f, 0.7f), rise = hc * 0.3f;
                float ls = h - hc + rise;

                // the stem, curving with its lean, soil-stained at the foot
                Vector3 prev = b0;
                for (int q = 1; q <= 8; q++)
                {
                    float f0 = (q - 1) / 8f, f1 = q / 8f;
                    Vector3 cur = b0 + (Vector3.up * f1 + lean * (f1 * f1)) * ls;
                    Tube(s, prev, cur, StemR(rs, f0), StemR(rs, f1), Color.Lerp(stem * 0.7f, stem, f0 * 3f), Color.Lerp(stem * 0.7f, stem, f1 * 3f), 0f, fill);
                    prev = cur;
                }

                // the cap's frame: its axis follows the stem's end; cc is the centre of its rim
                Vector3 ax = (Vector3.up + lean * 2f).normalized;
                Perp(ax, out Vector3 u, out Vector3 v);
                Vector3 cc = prev - ax * rise;

                // the convex top, darker at the crown, paler at the margin
                int top = Mathf.RoundToInt(s.Count(0.5f * EllipsoidArea(rc, hc, rc), 0) * fill);
                for (int j = 0; j < top; j++)
                {
                    Vector3 pos = OnEllipsoid(s, cc, u * rc, ax * hc, v * rc, out Vector3 nrm, out Vector3 q);
                    if (q.y < 0f)
                    {
                        pos -= ax * (2f * hc * q.y);
                        nrm -= ax * (2f * q.y / hc);
                        q.y = -q.y;
                    }
                    s.Put(pos, nrm, s.V(cap * (1.12f - 0.3f * q.y * q.y)), s.Rad(1.2f) * grow);
                }

                // small white spots, raised a little over the top
                if (spotted)
                {
                    int spots = 5 + (int)(s.R() * 9f);
                    for (int j = 0; j < spots; j++)
                    {
                        float ca = 1f - s.R() * 0.75f, sa = Mathf.Sqrt(1f - ca * ca), be = s.R() * Tau, ar = s.U(0.07f, 0.15f);
                        Vector3 qs = new Vector3(sa * Mathf.Cos(be), ca, sa * Mathf.Sin(be));
                        Perp(qs, out Vector3 su, out Vector3 sv);
                        int m = Mathf.Max(3, Mathf.RoundToInt(s.Count(3.1416f * ar * ar * rc * rc, 3) * fill));
                        for (int l = 0; l < m; l++)
                        {
                            float f = s.R() * Tau, dd = ar * Mathf.Sqrt(s.R());
                            Vector3 q = (qs + (su * Mathf.Cos(f) + sv * Mathf.Sin(f)) * dd).normalized;
                            Vector3 nrm = (u * (q.x / rc) + ax * (q.y / hc) + v * (q.z / rc)).normalized;
                            s.Put(cc + u * (q.x * rc) + ax * (q.y * hc) + v * (q.z * rc) + nrm * 0.005f, nrm, s.V(dot, 0.06f), s.Rad(0.8f) * grow);
                        }
                    }
                }

                // underneath: the cap's flesh rising to the stem between the gills, glowing a little
                float r0 = rs * 0.95f, span = rc - r0;
                int under = Mathf.RoundToInt(s.Count(3.1416f * (rc * rc - r0 * r0), 0) * fill);
                for (int j = 0; j < under; j++)
                {
                    float r = Mathf.Sqrt(Mathf.Lerp(r0 * r0, rc * rc, s.R())), f = s.R() * Tau;
                    Vector3 o = u * Mathf.Cos(f) + v * Mathf.Sin(f);
                    s.Put(cc + o * r + ax * (rise * (1f - (r - r0) / span)), -ax - o * (rise / span), s.V(gill * 0.7f), s.Rad() * grow, glow * 0.5f);
                }

                // the gills: thin radial blades hanging from it, deepest by the stem, glowing softly
                int gills = Mathf.Clamp(Mathf.RoundToInt(Tau * rc / 0.07f), 10, 60);
                float g0 = s.R() * Tau, deepest = 0.7f * rise + 0.04f * rc;
                int per = Mathf.Max(3, Mathf.RoundToInt(s.Count(span * (0.35f * rise + 0.025f * rc), 0) * fill));
                for (int g = 0; g < gills; g++)
                {
                    float f = g0 + (g + s.S() * 0.2f) * Tau / gills;
                    Vector3 o = u * Mathf.Cos(f) + v * Mathf.Sin(f), side = Vector3.Cross(ax, o);
                    for (int j = 0; j < per; j++)
                    {
                        float t = 0f, depth = 0f;
                        for (int tr = 0; tr < 8; tr++)
                        {
                            t = s.R();
                            depth = 0.7f * rise * (1f - t) + 0.04f * rc * Mathf.Sin(3.1416f * t);
                            if (s.R() * deepest < depth) break;   // even over the blade, which is deeper inside
                        }
                        float down = s.R();
                        Vector3 pos = cc + o * (r0 + t * span) + ax * (rise * (1f - t) - depth * down);
                        Vector3 nrm = side * ((s.R() < 0.5f ? -1f : 1f) * (1f - 0.6f * down)) - ax * (0.8f * down);
                        s.Put(pos, nrm, s.V(gill), s.Rad(0.75f) * grow, glow);
                    }
                }
            }
        }

        /// <summary>A mushroom stem's radius at fraction f of its length: a bulb at the foot, narrowing a little upward.</summary>
        static float StemR(float rs, float f)
        {
            float b = Mathf.Max(0f, 1f - f / 0.22f);
            return rs * (1f + 0.5f * b * b) * (1f - 0.1f * f);
        }

        // ------------------------------------------------------------------------------------------------- seedling

        /// <summary>
        /// A seedling: a slender curved green stem, pale at its foot, opening two thick oval seed-leaves flat at the top,
        /// sometimes with a tiny serrated first leaf rising between them, and its seed's split husk lying beside it. size
        /// is its height (0.8 to 2.2).
        /// </summary>
        public static void Seedling(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            float k = s.U(0.9f, 1.1f), w = s.U(-0.05f, 0.05f);
            Color green = Tint(new Color(0.18f, 0.36f, 0.1f), k, w), pale = Tint(new Color(0.34f, 0.36f, 0.22f), k, w);
            Color leaf = Tint(new Color(0.2f, 0.42f, 0.1f), k, w), mid = Tint(new Color(0.3f, 0.5f, 0.18f), k, w);
            Color under = Tint(new Color(0.16f, 0.3f, 0.1f), k, w), husk = Tint(new Color(0.3f, 0.2f, 0.1f), k, w);
            float da = s.R() * Tau, bend = s.U(0.04f, 0.1f) * (s.R() < 0.5f ? -1f : 1f), lean = s.U(-0.04f, 0.08f);
            Vector3 d = new Vector3(Mathf.Cos(da), 0f, Mathf.Sin(da)), e = new Vector3(-d.z, 0f, d.x);
            float sc = Mathf.Sqrt(size / 1.5f), rs = s.U(0.024f, 0.032f) * sc, hs = size * 0.93f;

            // the stem: a gentle arc in the plane of d, pale at its foot
            Vector3 prev = Vector3.zero;
            for (int q = 1; q <= 12; q++)
            {
                float f0 = (q - 1) / 12f, f1 = q / 12f;
                Vector3 cur = (d * (bend * Mathf.Sin(3.1416f * f1) + lean * f1 * f1) + Vector3.up * f1) * hs;
                Tube(s, prev, cur, rs * (1.15f - 0.25f * f0), rs * (1.15f - 0.25f * f1), Color.Lerp(pale, green, f0 * 3.5f), Color.Lerp(pale, green, f1 * 3.5f));
                prev = cur;
            }

            // two thick oval seed-leaves opening flat to either side, glossy on top with a lighter midline
            float len = Mathf.Lerp(0.35f, 0.6f, Mathf.Clamp01((size - 0.8f) / 1.4f) * 0.6f + s.R() * 0.4f), tw = s.S() * 0.25f;
            for (int c = 0; c < 2; c++)
            {
                float sg = c == 0 ? 1f : -1f, tilt = s.U(0.08f, 0.35f);
                float l = len * s.U(0.92f, 1.05f), wd = l * s.U(0.5f, 0.62f), th = l * s.U(0.07f, 0.1f);
                Vector3 hz = (e * (sg * Mathf.Cos(tw)) + d * Mathf.Sin(tw)).normalized;
                Vector3 m = hz * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt);
                Vector3 wdir = Vector3.Cross(Vector3.up, hz).normalized, ln = Vector3.Cross(m, wdir);
                Vector3 centre = prev + m * (l * 0.5f + 0.02f * sc);
                int count = s.Count(EllipsoidArea(l * 0.5f, th * 0.5f, wd * 0.5f));
                for (int j = 0; j < count; j++)
                {
                    Vector3 pos = OnEllipsoid(s, centre, m * (l * 0.5f), ln * (th * 0.5f), wdir * (wd * 0.5f), out Vector3 nrm, out Vector3 q);
                    Color col = q.y < 0f ? under : Mathf.Abs(q.z) < 0.07f ? mid : leaf;
                    if (q.x < -0.75f) col = Color.Lerp(col, pale, 0.4f);
                    s.Put(pos, nrm, s.V(col, 0.07f), s.Rad());
                }
            }

            // sometimes the first true leaf, tiny and serrated, rising between them
            if (s.R() < 0.5f)
            {
                float lt = s.U(0.14f, 0.24f) * sc, sg = s.R() < 0.5f ? -1f : 1f, rise = s.U(0.9f, 1.2f);
                Vector3 hz = d * sg;
                Vector3 m = (hz * Mathf.Cos(rise) + Vector3.up * Mathf.Sin(rise)).normalized;
                Vector3 c = Vector3.Cross(Vector3.up, hz).normalized, up = Vector3.Cross(m, c);
                Vector3 b0 = prev + Vector3.up * (0.02f * sc);
                Tube(s, prev, b0 + m * (0.02f * sc), rs * 0.4f, rs * 0.35f, green, green);
                int count = s.Count(0.32f * lt * lt / 0.42f, 12);
                for (int j = 0; j < count; j++)
                {
                    float x = 0.5f, y = 0f;
                    for (int t = 0; t < 20; t++)
                    {
                        x = s.R();
                        y = s.S() * 0.3f;
                        if (InSerrated(x, y)) break;
                    }
                    s.Put(b0 + (m * (x + 0.1f) + c * y) * lt, up - m * (0.15f * x), s.V(Mathf.Abs(y) < 0.02f ? mid : leaf * 1.1f, 0.07f), s.Rad(0.65f));
                }
            }

            // the seed's husk, split into two halves lying open beside the foot of the stem
            float hl = s.U(0.16f, 0.24f) * sc, hw = hl * s.U(0.55f, 0.7f), hd = hl * s.U(0.3f, 0.4f), ha = s.R() * Tau;
            Vector3 hc = new Vector3(Mathf.Cos(ha), 0f, Mathf.Sin(ha)) * (rs * 2f + hl * 0.5f);
            Vector3 hx = new Vector3(-Mathf.Sin(ha + 0.6f), 0f, Mathf.Cos(ha + 0.6f)), hzz = Vector3.Cross(hx, Vector3.up);
            for (int half = 0; half < 2; half++)
            {
                float sg = half == 0 ? 1f : -1f, roll = s.U(0.5f, 1.1f) * sg;
                Vector3 cy = Vector3.up * Mathf.Cos(roll) + hzz * Mathf.Sin(roll), cz = Vector3.Cross(hx, cy);
                Vector3 c0 = hc + hzz * (sg * hw * 0.6f) + Vector3.up * (hd * 0.8f);
                int count = s.Count(0.5f * EllipsoidArea(hl * 0.5f, hd, hw * 0.5f), 10);
                for (int j = 0; j < count; j++)
                {
                    // the shell's outside below its rim, then its paler inside facing into the cup
                    Vector3 pos = OnEllipsoid(s, c0, hx * (hl * 0.5f), cy * hd, cz * (hw * 0.5f), out Vector3 nrm, out Vector3 q);
                    if (q.y > 0f)
                    {
                        pos -= cy * (2f * hd * q.y);
                        nrm -= cy * (2f * q.y / hd);
                    }
                    s.Put(pos, nrm, s.V(husk), s.Rad(0.8f));
                    if ((j & 3) != 0) s.Put(c0 + (pos - c0) * 0.84f, -nrm, s.V(husk * 1.4f), s.Rad(0.8f));
                }
            }
        }

        /// <summary>Whether (x from base to tip, y across, in leaf lengths) lies in a small serrated leaf: an ovate blade
        /// whose margin is cut into teeth pointing toward the tip.</summary>
        static bool InSerrated(float x, float y)
        {
            if (x <= 0f || x >= 1f) return false;
            float hw = 0.26f * Mathf.Pow(Mathf.Sin(3.1416f * x), 0.7f) * (1.15f - 0.3f * x);
            float tooth = x * 8f - Mathf.Floor(x * 8f);
            return Mathf.Abs(y) <= hw * (0.8f + 0.2f * tooth);
        }

        // ------------------------------------------------------------------------------------------------- pebbles

        /// <summary>
        /// A cluster of 1 to 5 smooth pebbles: flattened ellipsoids sunk a little into the floor, grey, blue-grey or sandy,
        /// with small darker and lighter speckles and now and then a pale quartz vein round them. size is the largest
        /// pebble's length (0.3 to 1).
        /// </summary>
        public static void Pebbles(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            int count = 1 + (int)(s.R() * 5f);
            float spin = s.R() * Tau;
            Color quartz = new Color(0.52f, 0.5f, 0.46f);
            for (int i = 0; i < count; i++)
            {
                float kind = s.R(), k = s.U(0.88f, 1.12f);
                Color stone = Tint(kind < 0.4f ? new Color(0.22f, 0.23f, 0.24f) : kind < 0.7f ? new Color(0.18f, 0.22f, 0.28f) : new Color(0.36f, 0.3f, 0.22f), k, s.U(-0.04f, 0.04f));
                float a = (i == 0 ? size : size * s.U(0.35f, 0.8f)) * 0.5f, c = a * s.U(0.6f, 0.9f), b = a * s.U(0.35f, 0.6f);
                float ang = spin + i * 2.4f + s.S() * 0.6f;
                Vector3 at = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ((size * 0.5f + a) * s.U(0.75f, 1.05f));

                // lying flat with a little tilt, turned any way, its lower part in the floor
                float yaw = s.R() * Tau;
                Vector3 ey = (Vector3.up + new Vector3(s.S(), 0f, s.S()) * 0.18f).normalized;
                Vector3 h0 = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 ex = (h0 - ey * Vector3.Dot(h0, ey)).normalized, ez = Vector3.Cross(ex, ey);
                Vector3 cc = at + Vector3.up * (b * s.U(0.55f, 0.8f));

                bool vein = s.R() < 0.35f;
                Vector3 vn = new Vector3(s.S(), s.S() * 0.5f, s.S()).normalized;
                float vw = s.U(0.04f, 0.08f), ph1 = s.R() * Tau, ph2 = s.R() * Tau;
                int m = s.Count(EllipsoidArea(a, b, c));
                for (int j = 0; j < m; j++)
                {
                    Vector3 pos = OnEllipsoid(s, cc, ex * a, ey * b, ez * c, out Vector3 nrm, out Vector3 q);
                    if (pos.y < Lift) continue;   // sunk in the floor
                    // soft mottling, a little darker where it meets the floor
                    float shade = (1f + 0.08f * Mathf.Sin(q.x * 3.1f + ph1) * Mathf.Sin(q.z * 2.7f + ph2)) * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(pos.y / (b * 1.2f)));
                    Color col = stone * shade;
                    float rad = s.Rad(1.25f), sp = s.R();
                    if (vein && Mathf.Abs(Vector3.Dot(q, vn)) < vw) col = quartz * shade;
                    else if (sp < 0.12f)
                    {
                        // a speckle: smaller, darker or lighter, just over the surface
                        col *= sp < 0.07f ? 0.55f : 1.45f;
                        rad *= 0.6f;
                        pos += nrm.normalized * 0.003f;
                    }
                    s.Put(pos, nrm, s.V(col, 0.06f), rad);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- snail shell

        /// <summary>
        /// An empty snail shell lying on its side: a tube coiled in a logarithmic spiral over 2.5 to 3.5 whorls, cream with
        /// brown spiral bands and fine growth lines, its opening dark inside a pale lip and turned to the side or up. size
        /// is the shell's diameter (0.6 to 1.4).
        /// </summary>
        public static void SnailShell(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            float k0 = s.U(0.9f, 1.1f), w = s.U(-0.05f, 0.05f);
            Color cream = Tint(new Color(0.48f, 0.42f, 0.3f), k0, w), brown = Tint(new Color(0.25f, 0.15f, 0.08f), k0, w);
            Color lip = Tint(new Color(0.55f, 0.5f, 0.42f), k0, w), hole = new Color(0.035f, 0.028f, 0.022f);
            // the coil: each whorl wr times the one before; the tube's radius rho times its distance from the axis; the
            // tube moves down the axis tr times that distance
            float wr = s.U(2.1f, 2.6f), k = Mathf.Log(wr) / Tau, end = s.U(2.5f, 3.5f) * Tau;
            float rho = s.U(0.6f, 0.7f), tr = s.U(0.55f, 0.85f);
            float am = size / ((1f + rho) * (1f + 1f / Mathf.Sqrt(wr)));
            int bands = 1 + (int)(s.R() * 4f);
            float b0 = s.R() * Tau, bstep = s.U(0.5f, 0.9f), bw = s.U(0.08f, 0.16f), lines = s.U(30f, 44f);

            // its frame: the axis lies along the floor, a little tilted, and the shell is turned so its opening faces
            // somewhere between sideways and up
            float yaw = s.R() * Tau, tilt = s.S() * 0.2f, face = s.U(0f, 1.2f);
            Vector3 axis = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(tilt), Mathf.Sin(tilt), Mathf.Sin(yaw) * Mathf.Cos(tilt));
            Vector3 u1 = (Vector3.up - axis * axis.y).normalized, u2 = Vector3.Cross(axis, u1);
            float sig = -face - end;
            Vector3 xs = u1 * Mathf.Cos(sig) + u2 * Mathf.Sin(sig), zs = u2 * Mathf.Cos(sig) - u1 * Mathf.Sin(sig);

            // rest it on the floor by the lowest point of its last whorl
            float low = 1e9f;
            for (int i = 0; i <= 48; i++)
            {
                float th = end - Tau * i / 48f, g = Mathf.Exp(k * (th - end));
                Vector3 nr = xs * Mathf.Cos(th) + zs * Mathf.Sin(th);
                float cy = (nr * (am * g) - axis * (tr * am * g)).y;
                low = Mathf.Min(low, cy - rho * am * g * Mathf.Sqrt(nr.y * nr.y + axis.y * axis.y));
            }
            Vector3 o = axis * (tr * am * 0.5f);
            o.y = Lift - low + rho * am * 0.09f;

            // the coiled tube, spread evenly over its surface, leaving out what lies inside the whorls next to it
            float e2 = Mathf.Exp(-2f * k * end);
            int count = s.Count(Tau * rho * am * am * (1f - e2) / (2f * k));
            for (int i = 0; i < count; i++)
            {
                float th = end + Mathf.Log(e2 + s.R() * (1f - e2)) / (2f * k), g = Mathf.Exp(k * (th - end));
                float ph = 0f;
                for (int t = 0; t < 8; t++)
                {
                    ph = s.R() * Tau;
                    if (s.R() * (1f + rho) < 1f + rho * Mathf.Cos(ph)) break;   // more round the outside of the coil
                }
                float rr = am * g, r = rho * rr, cph = Mathf.Cos(ph), sph = Mathf.Sin(ph);
                float pr = rr + r * cph, py = -tr * rr + r * sph;   // in the plane through the axis: out from it, along it
                if (th + Tau <= end && Sq(pr - rr * wr, py + tr * rr * wr) < Sq(r * wr * 0.97f)) continue;
                if (th - Tau >= 0f && Sq(pr - rr / wr, py + tr * rr / wr) < Sq(r / wr * 0.97f)) continue;
                Vector3 nr = xs * Mathf.Cos(th) + zs * Mathf.Sin(th), tg = zs * Mathf.Cos(th) - xs * Mathf.Sin(th);
                // fine growth lines: shallow ridges across the tube, darker in their grooves
                float gl = Mathf.Sin(th * lines + Mathf.Sin(th * 2.3f) * 1.5f);
                Vector3 nrm = nr * cph + axis * sph + tg * (0.12f * gl);
                Color col = cream;
                for (int b = 0; b < bands; b++)
                    if (Mathf.Abs(Mathf.Repeat(ph - b0 - b * bstep + Mathf.PI, Tau) - Mathf.PI) < bw) col = brown;
                col *= (0.9f + 0.1f * gl) * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(th / Tau));
                s.Put(o + nr * pr + axis * py, nrm, s.V(col, 0.07f), s.Rad());
            }

            // the opening: a pale lip rolled round its edge, dark inside
            Vector3 n1 = xs * Mathf.Cos(end) + zs * Mathf.Sin(end), t1 = zs * Mathf.Cos(end) - xs * Mathf.Sin(end);
            float r1 = rho * am, rl = r1 * 0.09f;
            Vector3 c1 = o + n1 * am - axis * (tr * am);
            int rim = s.Count(Tau * r1 * Tau * rl * 1.2f);
            for (int i = 0; i < rim; i++)
            {
                float f = s.R() * Tau, ps = s.R() * Tau;
                Vector3 ring = n1 * Mathf.Cos(f) + axis * Mathf.Sin(f);
                Vector3 nrm = ring * Mathf.Cos(ps) + t1 * Mathf.Sin(ps);
                s.Put(c1 + ring * r1 + nrm * rl, nrm, s.V(lip, 0.06f), s.Rad(0.8f));
            }
            int dark = s.Count(3.1416f * r1 * r1);
            for (int i = 0; i < dark; i++)
            {
                float f = s.R() * Tau, d = r1 * 0.97f * Mathf.Sqrt(s.R());
                Vector3 ring = n1 * Mathf.Cos(f) + axis * Mathf.Sin(f);
                s.Put(c1 + ring * d - t1 * (r1 * 0.12f), t1, s.V(hole, 0.1f), s.Rad());
            }
        }

        static float Sq(float x) => x * x;

        static float Sq(float x, float y) => x * x + y * y;

        // ------------------------------------------------------------------------------------------------- lichen

        /// <summary>
        /// A leafy lichen crust: flat lobed rosettes on the floor, ruffled lobes radiating from the centre with lifted,
        /// curled edges, pale mint, ochre-yellow or grey-blue, darker toward the centre where small cup-shaped fruiting
        /// bodies sit. size is the main rosette's diameter (0.5 to 1.5).
        /// </summary>
        public static void LichenCrust(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            float kind = s.R(), k = s.U(0.9f, 1.1f);
            Color body = Tint(kind < 0.4f ? new Color(0.32f, 0.45f, 0.4f) : kind < 0.7f ? new Color(0.45f, 0.36f, 0.1f) : new Color(0.28f, 0.32f, 0.4f), k, s.U(-0.04f, 0.04f));
            Color fruit = Tint(new Color(0.25f, 0.12f, 0.06f), k, 0f);
            int extra = s.R() < 0.4f ? 1 + (int)(s.R() * 2f) : 0;
            float main = size * 0.5f, at = s.R() * Tau;
            for (int i = 0; i <= extra; i++)
            {
                // the main rosette, and sometimes one or two smaller ones grown against its edge
                var ro = new Rosette();
                ro.r = i == 0 ? main : main * s.U(0.35f, 0.6f);
                float ang = at + i * 2.3f;
                ro.c = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (main * 0.8f + ro.r * 0.5f);
                ro.h0 = Lift + 0.004f + i * 0.004f;
                ro.lift = s.U(0.06f, 0.12f);
                ro.ruff = s.U(0.01f, 0.025f);
                ro.lobes = 5 + (int)(s.R() * 5f);
                ro.ruffles = 14 + (int)(s.R() * 12f);
                ro.off = s.R();
                ro.ph = s.R() * Tau;
                DrawRosette(s, ro, body, fruit);
            }
        }

        /// <summary>
        /// A lichen rosette's shape: lobes radiating from its centre c and splitting in two halfway out, each with its own
        /// length and a rounded tip; its surface low on the floor, lifting and ruffling toward the edge.
        /// </summary>
        struct Rosette
        {
            public Vector3 c;
            public float r, h0, lift, ruff, off, ph;
            public int lobes, ruffles;

            /// <summary>How far the edge is from the centre at angle th.</summary>
            public float Edge(float th)
            {
                float u = th / Tau * lobes * 2f + off, f = u - Mathf.Floor(u), idx = Mathf.Repeat(Mathf.Floor(u), lobes * 2f);
                return r * (0.86f + 0.14f * Mathf.Sin(idx * 2.71f + ph)) * (0.8f + 0.2f * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * f))));
            }

            /// <summary>The surface's height at local (x, z). seam: how near a line between two lobes (0 far, 1 on it);
            /// q: how far out toward the edge (0 centre, 1 edge).</summary>
            public float H(float x, float z, out float seam, out float q)
            {
                float dx = x - c.x, dz = z - c.z, d = Mathf.Sqrt(dx * dx + dz * dz), th = Mathf.Atan2(dz, dx);
                if (th < 0f) th += Tau;
                q = Mathf.Clamp01(d / Edge(th));
                float split = q < 0.5f ? lobes : lobes * 2f;
                float u = th / Tau * split + off * split / (lobes * 2f), fu = u - Mathf.Floor(u);
                float gap = Mathf.Min(fu, 1f - fu) * Tau / split * d;   // along the arc to the nearest line between lobes
                seam = Mathf.Exp(-gap * gap / 0.00015f) * Smooth(0.1f, 0.25f, q);
                float lifted = Smooth(0.55f, 1f, q);
                return h0 + r * (lift * lifted * lifted + ruff * Mathf.Sin(th * ruffles + ph) * q * q * q) - 0.004f * seam;
            }
        }

        /// <summary>One lichen rosette: its top spread evenly over the lobes, the curled rim round its edge, and the
        /// fruiting cups near its centre (a rim of the body's colour round a dark concave disc).</summary>
        static void DrawRosette(Spot s, Rosette ro, Color body, Color fruit)
        {
            float r = ro.r;
            int count = s.Count(2.42f * r * r);
            for (int i = 0; i < count; i++)
            {
                float x = ro.c.x, z = ro.c.z, h = ro.h0, seam = 0f, q = 0f, hx = 0f, hz = 0f;
                for (int t = 0; t < 16; t++)
                {
                    float d = r * Mathf.Sqrt(s.R()), th = s.R() * Tau;
                    if (d > ro.Edge(th)) continue;
                    x = ro.c.x + d * Mathf.Cos(th);
                    z = ro.c.z + d * Mathf.Sin(th);
                    h = ro.H(x, z, out seam, out q);
                    hx = (ro.H(x + 0.004f, z, out _, out _) - ro.H(x - 0.004f, z, out _, out _)) / 0.008f;
                    hz = (ro.H(x, z + 0.004f, out _, out _) - ro.H(x, z - 0.004f, out _, out _)) / 0.008f;
                    if (s.R() * 1.6f < Mathf.Sqrt(1f + hx * hx + hz * hz)) break;   // more where the surface is steeper
                }
                float tone = Mathf.Lerp(0.62f, 1f, Smooth(0.1f, 0.65f, q)) * (1f - 0.35f * seam) * (1f + 0.12f * Smooth(0.8f, 1f, q));
                s.Put(new Vector3(x, h, z), new Vector3(-hx, 1f, -hz), s.V(body * tone), s.Rad());
            }

            // the curled rim: the lifted edge's outer face, from the floor up to the lobe's edge, darker underneath
            float edgeH = ro.h0 + r * ro.lift;
            int rim = s.Count(6.6f * r * (edgeH - Lift), 8);
            for (int i = 0; i < rim; i++)
            {
                float th = s.R() * Tau, e = ro.Edge(th);
                float de = (ro.Edge(th + 0.002f) - ro.Edge(th - 0.002f)) / 0.004f;
                Vector3 rad = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th)), side = new Vector3(-rad.z, 0f, rad.x);
                float top = ro.H(ro.c.x + rad.x * e * 0.995f, ro.c.z + rad.z * e * 0.995f, out _, out _);
                s.Put(ro.c + rad * e + Vector3.up * Mathf.Lerp(Lift, top, s.R()), rad - side * (de / e) + Vector3.up * 0.3f, s.V(body * 0.5f), s.Rad(0.8f));
            }

            // fruiting cups near the centre
            int cups = 4 + (int)(s.R() * 10f * Mathf.Clamp01(r / 0.5f + 0.2f));
            for (int i = 0; i < cups; i++)
            {
                float th = s.R() * Tau, d = r * 0.5f * Mathf.Sqrt(s.R());
                float ra = Mathf.Max(0.028f, r * s.U(0.05f, 0.09f)), rm = ra * 0.28f, dep = ra * 0.3f;
                float x = ro.c.x + d * Mathf.Cos(th), z = ro.c.z + d * Mathf.Sin(th);
                Vector3 cc = new Vector3(x, ro.H(x, z, out _, out _) + rm * 0.6f, z);
                int m = s.Count(39.48f * ra * rm / 0.5f, 6);   // the rim: a torus, its upper three quarters
                for (int j = 0; j < m; j++)
                {
                    float f = s.R() * Tau, ps = s.U(-0.6f, Mathf.PI);
                    Vector3 rd = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
                    Vector3 nrm = rd * Mathf.Cos(ps) + Vector3.up * Mathf.Sin(ps);
                    s.Put(cc + rd * ra + nrm * rm, nrm, s.V(body * 0.85f), s.Rad(0.7f));
                }
                int md = s.Count(Mathf.PI * ra * ra / 0.5f, 6);   // the disc, dipping to its middle
                for (int j = 0; j < md; j++)
                {
                    float f = s.R() * Tau, dd = ra * Mathf.Sqrt(s.R());
                    Vector3 rd = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
                    s.Put(cc + rd * dd + Vector3.up * (rm * 0.5f - dep * (1f - dd * dd / (ra * ra))), Vector3.up - rd * (2f * dep * dd / (ra * ra)), s.V(fruit), s.Rad(0.7f));
                }
            }
        }

        /// <summary>0 below e0, 1 above e1, smooth between.</summary>
        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------------------------------------- crystals

        /// <summary>
        /// 3 to 8 quartz-like grains lying on the floor: hexagonal prisms with pointed ends, icy, each flat facet with its
        /// own normal so they catch the light differently, and a faint glow along their edges only. size is the largest
        /// grain's length (0.15 to 0.5).
        /// </summary>
        public static void Crystals(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new Spot(pen, p, n, ta, tb);
            float k = s.U(0.9f, 1.1f), glow = s.U(0.1f, 0.2f);
            Color ice = Tint(new Color(0.35f, 0.45f, 0.55f), k, s.U(-0.05f, 0.03f));
            Color edgeCol = Color.Lerp(ice, new Color(0.55f, 0.62f, 0.68f), 0.6f);
            int count = 3 + (int)(s.R() * 6f);
            float spin = s.R() * Tau;
            for (int i = 0; i < count; i++)
            {
                float len = i == 0 ? size : size * s.U(0.4f, 0.85f), rp = len * s.U(0.16f, 0.24f), tip = rp * s.U(0.9f, 1.3f);
                float half = Mathf.Max(len - 2f * tip, rp * 0.4f) * 0.5f;
                float ang = spin + i * 2.4f + s.S() * 0.5f, dist = i == 0 ? 0f : size * (0.45f + 0.6f * Mathf.Sqrt(i / (float)count)) * s.U(0.8f, 1.15f);
                Vector3 c0 = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                float yaw = s.R() * Tau, tilt = s.S() * 0.15f, roll = s.R() * Tau;
                Vector3 ax = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(tilt), Mathf.Sin(tilt), Mathf.Sin(yaw) * Mathf.Cos(tilt));
                Vector3 e1 = (Vector3.up - ax * ax.y).normalized, e2 = Vector3.Cross(ax, e1);

                // lying on its side, resting on its lowest corner
                float low = -Mathf.Abs(ax.y) * (half + tip);
                for (int v = 0; v < 6; v++)
                {
                    float a = roll + v * 1.0471976f;
                    low = Mathf.Min(low, (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)).y * rp - Mathf.Abs(ax.y) * half);
                }
                c0.y = Lift - low;

                float w = Mathf.Max(0.008f, 0.08f * rp);   // how near an edge a splat is to glow
                for (int f = 0; f < 6; f++)
                {
                    float a0 = roll + f * 1.0471976f, am = a0 + 0.5235988f;
                    Vector3 r0 = e1 * Mathf.Cos(a0) + e2 * Mathf.Sin(a0), r1 = e1 * Mathf.Cos(a0 + 1.0471976f) + e2 * Mathf.Sin(a0 + 1.0471976f);
                    Vector3 fn = e1 * Mathf.Cos(am) + e2 * Mathf.Sin(am);
                    Color face = ice * s.U(0.88f, 1.12f);

                    // the prism's side: a long flat rectangle
                    int m = s.Count(rp * 2f * half / 0.5f, 6);
                    for (int j = 0; j < m; j++)
                    {
                        float u = s.R(), t = s.R();
                        float edge = Mathf.Min(Mathf.Min(u, 1f - u) * rp, Mathf.Min(t, 1f - t) * 2f * half);
                        Facet(s, c0 + Vector3.Lerp(r0, r1, u) * rp + ax * ((t * 2f - 1f) * half), fn, face, edgeCol, edge < w, glow);
                    }

                    // its two pointed ends: a triangle from the side's end edge to each apex
                    for (int e = -1; e <= 1; e += 2)
                    {
                        Vector3 v0 = c0 + r0 * rp + ax * (e * half), v1 = c0 + r1 * rp + ax * (e * half), apex = c0 + ax * (e * (half + tip));
                        Vector3 cr = Vector3.Cross(v1 - v0, apex - v0);
                        float area = cr.magnitude * 0.5f;
                        Vector3 tn = Vector3.Dot(cr, fn + ax * e) < 0f ? -cr : cr;
                        float h0 = 2f * area / Mathf.Max(1e-5f, (v1 - apex).magnitude);
                        float h1 = 2f * area / Mathf.Max(1e-5f, (apex - v0).magnitude);
                        float h2 = 2f * area / Mathf.Max(1e-5f, (v1 - v0).magnitude);
                        int mt = s.Count(area / 0.5f, 4);
                        for (int j = 0; j < mt; j++)
                        {
                            float sa = Mathf.Sqrt(s.R()), sb = s.R(), b0 = 1f - sa, b1 = sa * (1f - sb), b2 = sa * sb;
                            float edge = Mathf.Min(b0 * h0, Mathf.Min(b1 * h1, b2 * h2));
                            Facet(s, v0 * b0 + v1 * b1 + apex * b2, tn, face * 1.05f, edgeCol, edge < w, glow);
                        }
                    }
                }
            }
        }

        /// <summary>One splat of a crystal facet, facing the facet's own way; along an edge brighter and faintly
        /// glowing.</summary>
        static void Facet(Spot s, Vector3 pos, Vector3 nrm, Color face, Color edge, bool onEdge, float glow)
        {
            if (onEdge) s.Put(pos, nrm, s.V(edge, 0.05f), s.Rad(0.6f), glow);
            else s.Put(pos, nrm, s.V(face, 0.05f), s.Rad(0.7f));
        }
    }
}
