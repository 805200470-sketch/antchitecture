using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The loose things on the canyon floor at ant scale, drawn with a ThingPen as opaque splats over their surfaces:
    /// fallen leaves, twigs, petals, feathers, fern fronds, seeds and fallen blossoms. Each generator lays one thing on
    /// the floor at p (normal n, tangents ta and tb), turned at random, sized by its main dimension in units (the ant is
    /// 1 unit long). Everything random comes from the pen's own sequence, so a thing is the same each time it is made.
    /// No splat sits lower than 0.012 above the floor plane, and no thing uses more than 9000 splats.
    /// </summary>
    public static class NestThingsLitter
    {
        const int Cap = 9000;        // splats one thing may use
        const float Lift = 0.012f;   // lowest a splat's centre may sit above the floor plane
        const float Tau = 6.2831853f;

        /// <summary>The floor under one thing: x and y along the floor (ta, tb turned at random), z up along n.</summary>
        struct Frame
        {
            public Vector3 o, a, b, n;

            public Frame(ThingPen pen, Vector3 p, Vector3 up, Vector3 ta, Vector3 tb)
            {
                float t = pen.R() * Tau, c = Mathf.Cos(t), s = Mathf.Sin(t);
                o = p;
                n = up;
                a = ta * c + tb * s;
                b = tb * c - ta * s;
            }

            /// <summary>A point x, y along the floor and z above it (never lower than Lift).</summary>
            public Vector3 P(float x, float y, float z) => o + a * x + b * y + n * Mathf.Max(z, Lift);
            public Vector3 P(Vector3 v) => P(v.x, v.y, v.z);
            /// <summary>A direction given in the floor's x, y, z.</summary>
            public Vector3 D(float x, float y, float z) => a * x + b * y + n * z;
            public Vector3 D(Vector3 v) => a * v.x + b * v.y + n * v.z;
        }

        /// <summary>A thin sheet lying along x from 0 to len: half-width W·Bell(t) about a midline bent sideways by bend,
        /// its edges lifted by curl of its width (a little more on one side, asym), its length arched up away from
        /// t = mid, its edge gently waved.</summary>
        struct Blade
        {
            public float len, W, p1, p2, bend, curl, asym, arch, mid, wave, wf, ph;

            public float Half(float t) => W * Bell(t, p1, p2);
            public float Mid(float x) { float t = x / len; return bend * len * t * t; }

            /// <summary>Height at x along the sheet and u off its midline; nl is the surface normal there (floor's x, y, z).</summary>
            public float Z(float x, float u, out Vector3 nl)
            {
                float t = x / len, a = u / W, k = curl * 2f * W * (u > 0f ? 1f + asym : 1f - asym);
                float K = k + wave * (0.5f + 0.5f * Mathf.Sin(wf * t + ph)), d = t - mid;
                float dzdu = 2f * a / W * K;
                float dzdx = 8f * arch * d + wave * 0.5f * Mathf.Cos(wf * t + ph) * wf * a * a / len - dzdu * 2f * bend * t;
                nl = new Vector3(-dzdx, -dzdu, 1f);
                return Lift + 0.003f + K * a * a + 4f * arch * len * d * d;
            }
        }

        static void Dot(ThingPen pen, int end, Vector3 pos, Vector3 nrm, Color c, float r, float glow = 0f)
        {
            if (pen.into.Count < end) pen.Put(pos, nrm, c, r, glow);
        }

        /// <summary>A splat's middle radius at the pen's density.</summary>
        static float R0(ThingPen pen) => 0.62f / Mathf.Sqrt(pen.PerArea);

        /// <summary>The spacing that covers area (unit²) at the pen's density, wider if that would take more than budget splats.</summary>
        static float Spacing(ThingPen pen, float area, int budget)
        {
            float s = 1f / Mathf.Sqrt(pen.PerArea), k = pen.Count(area) / (float)budget;
            return k > 1f ? s * Mathf.Sqrt(k) : s;
        }

        /// <summary>A radius for fine lines (veins, barbs): varied like Radius, without its rare large splats.</summary>
        static float Line(ThingPen pen, float scale) => Mathf.Min(pen.Radius(scale), R0(pen) * scale * 1.4f);

        /// <summary>A colour varied per splat by about ±amt in brightness and a little in hue.</summary>
        static Color V(ThingPen pen, Color c, float amt = 0.1f)
        {
            float k = 1f + (pen.R() * 2f - 1f) * amt;
            float r = k * (0.97f + 0.06f * pen.R()), g = k * (0.97f + 0.06f * pen.R()), b = k * (0.97f + 0.06f * pen.R());
            return new Color(c.r * r, c.g * g, c.b * b, 1f);
        }

        /// <summary>A palette colour varied per thing, each channel by up to ±10 %.</summary>
        static Color Tint(ThingPen pen, Color c)
        {
            float r = 0.9f + 0.2f * pen.R(), g = 0.9f + 0.2f * pen.R(), b = 0.9f + 0.2f * pen.R();
            return new Color(c.r * r, c.g * g, c.b * b, 1f);
        }

        static Color Mul(Color c, float k) => new Color(Mathf.Min(c.r * k, 0.65f), Mathf.Min(c.g * k, 0.65f), Mathf.Min(c.b * k, 0.65f), 1f);

        /// <summary>Smoothstep from e0 to e1 (either way round).</summary>
        static float Sstep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>t^p1·(1−t)^p2 scaled to peak at 1: an outline's half-width along its length (p1 shapes the base, p2 the tip).</summary>
        static float Bell(float t, float p1, float p2)
        {
            if (t <= 0f || t >= 1f) return 0f;
            float m = p1 / (p1 + p2);
            return Mathf.Pow(t, p1) * Mathf.Pow(1f - t, p2) / (Mathf.Pow(m, p1) * Mathf.Pow(1f - m, p2));
        }

        /// <summary>How far (x, u) is from a hole's centre (h.x, h.y), in hole radii (h.z); large when there is no hole.</summary>
        static float Hole(float x, float u, Vector3 h)
        {
            if (h.z <= 0f) return 99f;
            float dx = x - h.x, du = u - h.y;
            return Mathf.Sqrt(dx * dx + du * du) / h.z;
        }

        // ------------------------------------------------------------------------------------------------ leaf

        static Color LeafColour(int i)
        {
            // (a nest's own where it has them: the honey granary's dry straw and brown)
            Color[] own = NestTheme.Current?.leafColours;
            if (own != null && own.Length > 0) return own[i % own.Length];
            switch (i)
            {
                case 0: return new Color(0.35f, 0.12f, 0.06f);   // rust
                case 1: return new Color(0.40f, 0.28f, 0.08f);   // ochre
                case 2: return new Color(0.18f, 0.24f, 0.08f);   // olive
                case 3: return new Color(0.22f, 0.08f, 0.16f);   // plum
                default: return new Color(0.14f, 0.20f, 0.22f);  // faded teal-grey
            }
        }

        /// <summary>A fallen leaf, size 1.5–4 long: an ovate-to-elliptic blade with a pointed tip on a short stalk, a
        /// lighter midrib and 5–8 pairs of curving side veins, a finely toothed edge, gently curled and wavy. About one
        /// leaf in four is worn: one to three small insect holes and dried, darker brown edges.</summary>
        public static void FallenLeaf(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var f = new Frame(pen, p, n, ta, tb);
            int end = pen.into.Count + pen.Cap(Cap);
            float L = Mathf.Max(size, 0.3f), Lp = 0.12f * L, r0 = R0(pen), rMin = 0.5f * r0;
            var b = new Blade
            {
                len = L - Lp, p1 = 0.55f + 0.3f * pen.R(), p2 = 1f + 0.35f * pen.R(),
                bend = (pen.R() * 2f - 1f) * 0.07f, curl = 0.04f + 0.06f * pen.R(), asym = (pen.R() * 2f - 1f) * 0.3f,
                arch = 0.012f + 0.02f * pen.R(), mid = 0.45f + 0.1f * pen.R(), wf = 9f + 8f * pen.R(), ph = pen.R() * Tau
            };
            b.W = b.len * (0.2f + 0.1f * pen.R());
            b.wave = b.W * (0.02f + 0.03f * pen.R());
            float xo = 0.5f * (Lp - b.len);   // centres the leaf, stalk included, on p
            int teeth = 26 + (int)(pen.R() * 16f);

            bool worn = pen.R() < 0.25f;
            Vector3 h0 = Vector3.zero, h1 = Vector3.zero, h2 = Vector3.zero;
            int holes = worn ? 1 + (int)(pen.R() * 2.999f) : 0;
            for (int k = 0; k < holes; k++)
            {
                float ht = 0.25f + 0.55f * pen.R();
                var h = new Vector3(ht * b.len, (pen.R() * 2f - 1f) * 0.55f * b.Half(ht), b.W * (0.06f + 0.09f * pen.R()));
                if (k == 0) h0 = h; else if (k == 1) h1 = h; else h2 = h;
            }
            Color body = Tint(pen, LeafColour((int)(pen.R() * 4.999f)));
            Color vein = Mul(body, 1.4f), rim = Mul(body, 0.78f), dry = new Color(0.11f, 0.065f, 0.035f);

            // the blade: an even grid of splats over the toothed outline, each on the curled surface
            float s = Spacing(pen, 2f * b.W * b.len * 0.66f, 7000), rs = s * Mathf.Sqrt(pen.PerArea);
            float ym = b.W + Mathf.Abs(b.bend) * b.len;
            int nx = Mathf.CeilToInt(b.len / s), ny = Mathf.CeilToInt(2f * ym / s);
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = (i + 0.15f + 0.7f * pen.R()) * s, y = -ym + (j + 0.15f + 0.7f * pen.R()) * s;
                    float t = x / b.len, u = y - b.Mid(x), au = Mathf.Abs(u);
                    float w = b.Half(t) * (1f - 0.06f * ((t * teeth) % 1f) * Sstep(0.03f, 0.15f, t) * Sstep(0.97f, 0.85f, t));
                    if (au >= w) continue;
                    float hd = Mathf.Min(Hole(x, u, h0), Mathf.Min(Hole(x, u, h1), Hole(x, u, h2)));
                    if (hd < 1f) continue;
                    float z = b.Z(x, u, out Vector3 nl), e = 1f - au / w;
                    Color c = Mul(body, 1f + 0.08f * Mathf.Sin(x * 7.3f + b.ph) * Mathf.Sin(u * 9.1f - b.ph));
                    c = Color.Lerp(c, vein, 0.2f * Sstep(0.8f, 1f, e) + 0.25f * Sstep(0.1f, 0f, t));
                    if (worn)
                    {
                        float band = 0.17f + 0.08f * Mathf.Sin(t * 23f + 2f * b.ph) + 0.06f * Mathf.Sin(t * 61f + b.ph);
                        float d = Mathf.Max(Sstep(band, 0.3f * band, e), Mathf.Max(Sstep(1.5f, 1f, hd), 0.7f * Sstep(0.75f, 1f, t)));
                        c = Color.Lerp(c, dry, 0.85f * d);
                    }
                    else c = Color.Lerp(c, rim, Sstep(0.1f, 0f, e));
                    Dot(pen, end, f.P(x + xo, y, z), f.D(nl), V(pen, c), pen.Radius(1.3f * rs));
                }

            // the midrib: a raised, lighter line down the middle, thinning to the tip
            for (float x = 0f; x < 0.97f * b.len;)
            {
                float r = Mathf.Max(rMin, 0.011f * L * (1f - 0.8f * x / b.len));
                float z = b.Z(x, 0f, out Vector3 nl) + 0.4f * r;
                Dot(pen, end, f.P(x + xo, b.Mid(x), z), f.D(nl), V(pen, vein, 0.06f), r);
                x += 0.9f * r;
            }

            // side veins: out from the midrib and forward, bending toward the tip near the edge
            int pairs = 5 + (int)(pen.R() * 3.999f);
            float rv = 0.7f * r0;
            for (int k = 0; k < pairs; k++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float t0 = 0.07f + 0.76f * (k + 0.35f + 0.3f * pen.R()) / pairs + 0.012f * side;
                    float dt = 0.13f + 0.07f * pen.R(), reach = 0.82f + 0.1f * pen.R(), wm = b.Half(t0 + 0.5f * dt);
                    int steps = Mathf.CeilToInt(1.15f * Mathf.Sqrt(dt * b.len * dt * b.len + wm * wm) / rv);
                    for (int q = 1; q <= steps; q++)
                    {
                        float sq = q / (float)steps, t = t0 + dt * sq;
                        if (t >= 0.96f) break;
                        float x = t * b.len, hw = b.Half(t), u = side * reach * hw * Mathf.Sin(1.5707963f * sq);
                        if (Mathf.Min(Hole(x, u, h0), Mathf.Min(Hole(x, u, h1), Hole(x, u, h2))) < 1.2f) continue;
                        float z = b.Z(x, u, out Vector3 nl) + 0.003f;
                        Color c = worn ? Color.Lerp(vein, dry, 0.7f * Sstep(0.22f, 0.05f, 1f - Mathf.Abs(u) / hw)) : vein;
                        Dot(pen, end, f.P(x + xo, u + b.Mid(x), z), f.D(nl), V(pen, c, 0.06f), Mathf.Max(rMin, Line(pen, 0.7f) * (1f - 0.35f * sq)));
                    }
                }

            // the stalk: from the floor up into the blade's base
            float zb = b.Z(0f, 0f, out _), rp = Mathf.Max(rMin, 0.013f * L), pc = (pen.R() * 2f - 1f) * 0.25f;
            for (float x = -Lp; x < 0.03f * b.len;)
            {
                float t = Mathf.Clamp01((x + Lp) / Lp);
                Dot(pen, end, f.P(x + xo, pc * Lp * (1f - t) * (1f - t), Mathf.Lerp(Lift + rp, zb + 0.4f * rp, t * t)),
                    f.n, V(pen, Mul(vein, 0.9f), 0.06f), rp);
                x += 0.9f * rp;
            }
        }

        // ------------------------------------------------------------------------------------------------ twig

        /// <summary>A twig, size 2–5 long and 0.06–0.18 thick: a slightly bent, tapering stick lying on the floor, bark
        /// ridges along it (lighter ridges, darker grooves), a short side-branch stub, both ends cut to pale wood with a
        /// darker ring, and on about a third of them a small moss patch on top.</summary>
        public static void Twig(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var f = new Frame(pen, p, n, ta, tb);
            int end = pen.into.Count + pen.Cap(Cap);
            float L = Mathf.Max(size, 0.3f), R = 0.06f + 0.12f * pen.R(), taper = 0.2f + 0.25f * pen.R();
            float bend = (pen.R() * 2f - 1f) * 0.06f, wig = 0.012f * pen.R(), ph = pen.R() * Tau;
            Color bark = Tint(pen, Color.Lerp(new Color(0.16f, 0.12f, 0.09f), new Color(0.12f, 0.13f, 0.14f), pen.R()));
            Color wood = Tint(pen, new Color(0.45f, 0.38f, 0.28f)), moss = Tint(pen, new Color(0.12f, 0.28f, 0.08f));
            int ridges = Mathf.Clamp(Mathf.RoundToInt(Tau * R / 0.085f), 6, 14);
            bool mossy = pen.R() < 0.3f;
            float mt = 0.2f + 0.6f * pen.R(), ml = 0.06f + 0.08f * pen.R(), ma = 0.6f + 0.4f * pen.R();
            float s = Spacing(pen, Tau * R * (1f - 0.5f * taper) * L * 0.67f, 6000), rs = s * Mathf.Sqrt(pen.PerArea);

            // the bark: rings along the stick, each over the upper two thirds of its girth (the rest lies on the floor)
            int rings = Mathf.CeilToInt(L / s);
            for (int i = 0; i < rings; i++)
            {
                float t = (i + 0.2f + 0.6f * pen.R()) / rings;
                TwigAxis(t, L, R, taper, bend, wig, ph, out Vector3 c, out Vector3 side, out float r);
                int around = Mathf.Max(3, Mathf.CeilToInt(r * 4.18879f / s));
                for (int k = 0; k < around; k++)
                {
                    float phi = -2.0943951f + (k + 0.2f + 0.6f * pen.R()) * 4.18879f / around, sp = Mathf.Sin(phi), cp = Mathf.Cos(phi);
                    Vector3 dir = side * sp + new Vector3(0f, 0f, cp), tan = side * cp - new Vector3(0f, 0f, sp);
                    float arg = phi * ridges + 1.3f * Mathf.Sin(t * L * 2.3f + ph) + 0.4f * Mathf.Sin(t * L * 7.1f + 2f * ph);
                    float g = 0.5f + 0.5f * Mathf.Sin(arg), rr = r * (1f + 0.06f * (g - 0.5f));
                    float dm = mossy ? (t - mt) * L / (ml * L) : 9f;
                    float mr = ma * Mathf.Sqrt(Mathf.Max(0f, 1f - dm * dm)) * (1f + 0.25f * Mathf.Sin(t * L * 31f + phi * 5f));
                    if (Mathf.Abs(phi) < mr)
                    {
                        // moss: a soft green cushion a little proud of the bark
                        Vector3 m = new Vector3(pen.R() - 0.5f, pen.R() - 0.5f, pen.R() - 0.5f) * 0.8f;
                        Dot(pen, end, f.P(c + dir * (rr + 0.01f + 0.015f * pen.R())), f.D(dir + m), V(pen, moss, 0.18f), pen.Radius(0.9f * rs));
                        if (pen.R() < 0.6f)
                            Dot(pen, end, f.P(c + dir * (rr + 0.025f + 0.015f * pen.R())), f.D(dir - m), V(pen, Mul(moss, 1.2f), 0.18f), pen.Radius(0.7f * rs));
                        continue;
                    }
                    Color col = Mul(bark, Mathf.Lerp(0.6f, 1.3f, g * g) * (1f + 0.08f * cp));
                    Dot(pen, end, f.P(c + dir * rr), f.D(dir - tan * (0.03f * ridges * Mathf.Cos(arg))), V(pen, col), pen.Radius(1.15f * rs));
                }
            }

            // the cut ends
            for (int e = 0; e < 2; e++)
            {
                TwigAxis(e, L, R, taper, bend, wig, ph, out Vector3 c, out Vector3 side, out float r);
                Vector3 ax = Vector3.Cross(side, new Vector3(0f, 0f, 1f)) * (e == 0 ? -1f : 1f);
                CutEnd(pen, end, f, c + ax * 0.004f, ax, side, new Vector3(0f, 0f, 1f), r, wood, bark, rs);
            }

            // a short side-branch stub, rising from one flank
            {
                float t = 0.3f + 0.4f * pen.R(), sg = pen.R() < 0.5f ? -1f : 1f, el = 0.4f + 0.6f * pen.R(), fw = 0.6f + 0.3f * pen.R();
                TwigAxis(t, L, R, taper, bend, wig, ph, out Vector3 c, out Vector3 side, out float r);
                Vector3 tan = Vector3.Cross(side, new Vector3(0f, 0f, 1f));
                Vector3 outw = side * (sg * Mathf.Cos(el)) + new Vector3(0f, 0f, Mathf.Sin(el));
                Vector3 d = (tan * Mathf.Cos(fw) + outw * Mathf.Sin(fw)).normalized;
                Vector3 w1 = Vector3.Cross(d, new Vector3(0f, 0f, 1f)).normalized, w2 = Vector3.Cross(w1, d);
                float rb = 0.55f * r, lb = rb * (2.5f + 2f * pen.R());
                Vector3 b0 = c + outw * (0.6f * r);
                int nl = Mathf.Max(2, Mathf.CeilToInt(lb / s)), na = Mathf.Max(5, Mathf.CeilToInt(Tau * rb / s));
                for (int i = 0; i < nl; i++)
                {
                    float l = (i + 0.2f + 0.6f * pen.R()) / nl, rl = rb * (1f - 0.15f * l);
                    for (int k = 0; k < na; k++)
                    {
                        float a = (k + pen.R()) * Tau / na, g = 0.5f + 0.5f * Mathf.Sin(a * 5f + l * 3f + ph);
                        Vector3 dir = w1 * Mathf.Cos(a) + w2 * Mathf.Sin(a);
                        Dot(pen, end, f.P(b0 + d * (l * lb) + dir * rl), f.D(dir), V(pen, Mul(bark, Mathf.Lerp(0.7f, 1.25f, g))), pen.Radius(1.1f * rs));
                    }
                }
                CutEnd(pen, end, f, b0 + d * (lb + 0.004f), d, w1, w2, 0.85f * rb, wood, bark, rs);
            }
        }

        /// <summary>The twig's axis at t (0..1): centre c (floor's x, y, z), the unit vector across it along the floor, its radius.</summary>
        static void TwigAxis(float t, float L, float R, float taper, float bend, float wig, float ph, out Vector3 c, out Vector3 side, out float r)
        {
            r = R * (1f - taper * t);
            float y = bend * L * 4f * t * (1f - t) + wig * L * Mathf.Sin(7f * t + ph);
            float dy = bend * 4f * (1f - 2f * t) + wig * 7f * Mathf.Cos(7f * t + ph);
            c = new Vector3((t - 0.5f) * L, y, r + Lift);
            side = new Vector3(-dy, 1f, 0f).normalized;
        }

        /// <summary>A cut end of a stick: a disc of pale wood (centre c, facing ax, spanned by e1 and e2) with a darker
        /// ring, faint growth rings, a dark pith and a thin rim of bark.</summary>
        static void CutEnd(ThingPen pen, int end, Frame f, Vector3 c, Vector3 ax, Vector3 e1, Vector3 e2, float r, Color wood, Color bark, float rs)
        {
            int count = pen.Count(3.1415927f * r * r);
            float turn = pen.R() * Tau, ring = 0.5f + 0.2f * pen.R(), growth = 3f + 3f * pen.R();
            for (int i = 0; i < count; i++)
            {
                float q = Mathf.Sqrt((i + 0.5f) / count), a = i * 2.3999632f + turn, wob = q + 0.03f * Mathf.Sin(a * 3f + turn);
                Color col = Mul(wood, 1f - 0.12f * Sstep(0.6f, 1f, Mathf.Sin(wob * growth * Tau)));
                col = Color.Lerp(col, Mul(wood, 0.55f), Sstep(0.07f, 0.02f, Mathf.Abs(wob - ring)));
                col = Color.Lerp(col, Mul(wood, 0.5f), Sstep(0.12f, 0.05f, q));
                if (q > 0.88f) col = bark;
                Dot(pen, end, f.P(c + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * (q * r)), f.D(ax), V(pen, col, 0.06f), 0.85f * pen.Radius(1.2f * rs));
            }
        }

        // ------------------------------------------------------------------------------------------------ petal, blossom

        static Color PetalColour(float pick) =>
            pick < 0.4f ? new Color(0.5f, 0.28f, 0.34f) : pick < 0.7f ? new Color(0.55f, 0.5f, 0.4f) : new Color(0.6f, 0.6f, 0.58f);

        /// <summary>True inside the notch cut into a petal's tip (notch deep, as a fraction of its length; nw half-wide).</summary>
        static bool PetalNotch(float t, float au, float notch, float nw) => notch > 0f && au < nw && t > 1f - notch * (1f - au / nw);

        /// <summary>A single fallen petal, size 0.8–2 long: rounded and widest toward its end, cupped upward, sometimes
        /// notched at the tip, fine veins fanning from its base; pink, cream or white, darker and yellower at the base.</summary>
        public static void Petal(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var f = new Frame(pen, p, n, ta, tb);
            int end = pen.into.Count + pen.Cap(Cap);
            float L = Mathf.Max(size, 0.2f), r0 = R0(pen);
            var b = new Blade
            {
                len = L, p1 = 1.1f + 0.5f * pen.R(), p2 = 0.35f + 0.15f * pen.R(), bend = (pen.R() * 2f - 1f) * 0.05f,
                curl = 0.1f + 0.05f * pen.R(), asym = (pen.R() * 2f - 1f) * 0.2f, arch = 0.03f + 0.03f * pen.R(),
                mid = 0.35f, wf = 6f + 4f * pen.R(), ph = pen.R() * Tau
            };
            b.W = L * (0.3f + 0.12f * pen.R());
            b.wave = 0.03f * b.W;
            float xo = -0.5f * L, notch = pen.R() < 0.4f ? 0.06f + 0.08f * pen.R() : 0f, nw = 0.3f * b.W;
            Color body = Tint(pen, PetalColour(pen.R()));
            Color root = Color.Lerp(Mul(body, 0.7f), new Color(0.45f, 0.38f, 0.1f), 0.35f), vein = Color.Lerp(Mul(body, 0.82f), root, 0.2f);

            float s = Spacing(pen, 2f * b.W * L * 0.62f, 7000), rs = s * Mathf.Sqrt(pen.PerArea);
            float ym = b.W + Mathf.Abs(b.bend) * L;
            int nx = Mathf.CeilToInt(L / s), ny = Mathf.CeilToInt(2f * ym / s);
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = (i + 0.15f + 0.7f * pen.R()) * s, y = -ym + (j + 0.15f + 0.7f * pen.R()) * s;
                    float t = x / L, u = y - b.Mid(x), au = Mathf.Abs(u), w = b.Half(t);
                    if (au >= w || PetalNotch(t, au, notch, nw)) continue;
                    float z = b.Z(x, u, out Vector3 nl);
                    float streak = 1f + 0.05f * Mathf.Sin(Mathf.Atan2(u, x + 0.05f * L) * 30f);
                    Color c = Mul(Color.Lerp(root, body, Sstep(0.02f, 0.32f, t)), streak * (1f + 0.06f * Sstep(0.12f, 0f, 1f - au / w)));
                    Dot(pen, end, f.P(x + xo, y, z), f.D(nl), V(pen, c, 0.08f), pen.Radius(1.3f * rs));
                }

            // fine veins fanning from the base toward the rim
            int veins = 7 + (int)(pen.R() * 3.999f);
            float rv = 0.6f * r0;
            for (int k = 0; k < veins; k++)
            {
                float kf = -0.85f + 1.7f * (k + 0.5f) / veins + (pen.R() - 0.5f) * 0.05f;
                int steps = Mathf.CeilToInt(0.86f * L * (1f + 0.2f * Mathf.Abs(kf)) / rv);
                for (int q = 0; q < steps; q++)
                {
                    float t = 0.04f + 0.86f * q / steps, x = t * L, u = kf * 0.9f * b.Half(t);
                    if (PetalNotch(t, Mathf.Abs(u), notch, nw)) continue;
                    float z = b.Z(x, u, out Vector3 nl) + 0.002f;
                    Dot(pen, end, f.P(x + xo, u + b.Mid(x), z), f.D(nl), V(pen, vein, 0.06f), Line(pen, 0.6f));
                }
            }
        }

        /// <summary>A small fallen wildflower, size 1–2.2 across (rare): five rounded, slightly cupped petals, now and then
        /// one or two of them missing, around a dense yellow-orange centre of small bumps; white, pale yellow or lilac.</summary>
        public static void FallenBlossom(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var f = new Frame(pen, p, n, ta, tb);
            int end = pen.into.Count + pen.Cap(Cap);
            float D = Mathf.Max(size, 0.3f), rc = D * (0.13f + 0.04f * pen.R()), pick = pen.R();
            Color body = Tint(pen, pick < 0.4f ? new Color(0.6f, 0.6f, 0.58f) : pick < 0.7f ? new Color(0.6f, 0.55f, 0.2f) : new Color(0.4f, 0.3f, 0.55f));
            Color root = Color.Lerp(Mul(body, 0.8f), new Color(0.5f, 0.4f, 0.08f), 0.3f), heart = Tint(pen, new Color(0.55f, 0.35f, 0.05f));
            int miss0 = -1, miss1 = -1;
            if (pen.R() < 0.35f)
            {
                miss0 = (int)(pen.R() * 4.999f);
                if (pen.R() < 0.3f) miss1 = (miss0 + 2 + (int)(pen.R() * 1.999f)) % 5;
            }
            float turn = pen.R() * Tau, rin = 0.55f * rc, lp = 0.5f * D - rin;
            float s = Spacing(pen, 5f * 2f * 0.42f * lp * lp * 0.6f, 6500), rs = s * Mathf.Sqrt(pen.PerArea);

            // the petals, each a cupped blade out from the centre
            for (int k = 0; k < 5; k++)
            {
                if (k == miss0 || k == miss1) continue;
                float th = turn + k * 1.2566371f + (pen.R() - 0.5f) * 0.12f, ct = Mathf.Cos(th), st = Mathf.Sin(th);
                var b = new Blade
                {
                    len = lp * (0.92f + 0.16f * pen.R()), p1 = 0.9f + 0.3f * pen.R(), p2 = 0.4f + 0.15f * pen.R(),
                    bend = (pen.R() * 2f - 1f) * 0.05f, curl = 0.1f + 0.05f * pen.R(), asym = (pen.R() * 2f - 1f) * 0.2f,
                    arch = 0.02f + 0.02f * pen.R(), mid = 0.3f, wf = 5f + 4f * pen.R(), ph = pen.R() * Tau
                };
                b.W = b.len * (0.38f + 0.07f * pen.R());
                b.wave = 0.03f * b.W;
                float zk = 0.002f * (k % 2) + 0.0012f * k, ym = b.W + Mathf.Abs(b.bend) * b.len;
                int nx = Mathf.CeilToInt(b.len / s), ny = Mathf.CeilToInt(2f * ym / s);
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        float x = (i + 0.15f + 0.7f * pen.R()) * s, y = -ym + (j + 0.15f + 0.7f * pen.R()) * s;
                        float t = x / b.len, u = y - b.Mid(x), au = Mathf.Abs(u), w = b.Half(t);
                        if (au >= w) continue;
                        float z = b.Z(x, u, out Vector3 nl) + zk, X = rin + x;
                        Color c = Color.Lerp(root, body, Sstep(0.02f, 0.35f, t));
                        c = Mul(c, (1f + 0.05f * Mathf.Sin(u / w * 9f)) * (1f - 0.1f * Sstep(0.1f, 0f, 1f - au / w)));
                        Dot(pen, end, f.P(X * ct - y * st, X * st + y * ct, z), f.D(nl.x * ct - nl.y * st, nl.x * st + nl.y * ct, nl.z),
                            V(pen, c, 0.08f), pen.Radius(1.3f * rs));
                    }
            }

            // the centre: a low dome of tiny florets, each a bump of a few splats
            float sf = Mathf.Max(0.035f, 0.16f * rc), hc = 0.4f * rc, rf = 0.55f * sf;
            int florets = Mathf.Max(8, Mathf.CeilToInt(3.1415927f * rc * rc / (sf * sf)));
            for (int i = 0; i < florets; i++)
            {
                float q = Mathf.Sqrt((i + 0.5f) / florets), a = i * 2.3999632f + turn;
                float px = q * rc * Mathf.Cos(a), py = q * rc * Mathf.Sin(a), z = Lift + 0.012f + hc * (1f - q * q);
                Vector3 dn = new Vector3(2f * hc * px / (rc * rc), 2f * hc * py / (rc * rc), 1f);
                Color c = Color.Lerp(heart, new Color(0.5f, 0.45f, 0.08f), 0.4f * (1f - q));
                Dot(pen, end, f.P(px, py, z + 0.35f * rf), f.D(dn), V(pen, Mul(c, 1.15f), 0.08f), 0.75f * rf);
                for (int m = 0; m < 3; m++)
                {
                    float o = a + m * 2.0943951f + pen.R(), ox = Mathf.Cos(o), oy = Mathf.Sin(o);
                    Dot(pen, end, f.P(px + ox * 0.55f * rf, py + oy * 0.55f * rf, z), f.D(dn + new Vector3(ox, oy, 0f) * 0.9f),
                        V(pen, Mul(c, 0.8f), 0.08f), 0.6f * rf);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ feather

        /// <summary>A fragment of a down feather, size 1.5–3.5 long, lying flat: a pale tapering shaft, soft vanes on both
        /// sides of fine barbs angled toward the tip, a few frayed gaps, fluffy down near the quill; grey-white with faint
        /// darker bars.</summary>
        public static void Feather(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var f = new Frame(pen, p, n, ta, tb);
            int end = pen.into.Count + pen.Cap(Cap);
            float L = Mathf.Max(size, 0.3f), rMin = 0.5f * R0(pen), xo = -0.5f * L;
            float bend = (pen.R() * 2f - 1f) * 0.06f, Wv = L * (0.2f + 0.1f * pen.R()), q0 = 0.1f + 0.04f * pen.R();
            float curl = 0.05f + 0.07f * pen.R(), bars = 4f + 3f * pen.R(), ph = pen.R() * Tau;
            Color shaft = Tint(pen, new Color(0.5f, 0.48f, 0.42f)), vane = Tint(pen, new Color(0.42f, 0.44f, 0.46f));

            // the shaft: the bare quill, then the rachis tapering to the tip
            for (float x = 0f; x < L;)
            {
                float t = x / L, r = Mathf.Max(rMin, 0.016f * L * (1f - 0.75f * t));
                Dot(pen, end, f.P(x + xo, bend * L * t * t, Lift + r), f.n, V(pen, t < q0 ? Mul(shaft, 1.1f) : shaft, 0.05f), r);
                x += 0.9f * r;
            }

            // the vanes: barb after barb down each side, each a line of splats out and toward the tip; rb is the barb
            // splats' radius and spacing along a barb, dp the gap between neighbouring barbs, so they cover at PerArea
            float s = Spacing(pen, 2f * Wv * 0.62f * L * (1f - q0), 6500), rs = s * Mathf.Sqrt(pen.PerArea), rb = 0.68f * s, dp = s * s / rb;
            for (int side = -1; side <= 1; side += 2)
            {
                float th0 = 0.75f + 0.2f * pen.R(), gA = 0.3f + 0.25f * pen.R(), gB = side > 0 ? 0.6f + 0.3f * pen.R() : -9f;
                for (float xb = q0 * L; xb < L;)
                {
                    float t0 = xb / L, fd = Sstep(q0 + 0.25f, q0, t0);   // 1 near the quill: fluffy down
                    float g = Mathf.Abs(t0 - gA) < Mathf.Abs(t0 - gB) ? gA : gB;
                    float th = th0 + 0.45f * fd, dx = dp / Mathf.Sin(th), gap = Mathf.Abs(t0 - g) * L / dx;   // gap: in barbs
                    th -= Mathf.Sign(t0 - g) * 0.18f * Sstep(3f, 0.7f, gap);   // barbs beside a split lean away from it
                    bool split = gap < 0.7f;
                    xb += dx * (0.85f + 0.3f * pen.R());
                    float rr = Mathf.Max(rMin, 0.016f * L * (1f - 0.75f * t0)), rag = (0.88f + 0.2f * pen.R()) * (1f + 0.35f * fd);
                    float px = t0 * L, py = bend * L * t0 * t0 + side * rr;
                    for (int q = 0; q < 400; q++)
                    {
                        float tc = px / L;
                        if (tc >= 1f) break;
                        float v = Mathf.Clamp01((tc - q0) / (1f - q0));
                        float lim = Wv * Mathf.Pow(Mathf.Sin(3.1415927f * Mathf.Pow(v, 0.65f)), 0.6f) * rag;
                        float d = Mathf.Abs(py - bend * L * tc * tc);
                        if (d >= lim) break;
                        float sq = d / lim, ang = th - 0.35f * sq;
                        if (!split || sq < 0.25f)
                        {
                            float dzdd = 4f * curl * d / Wv - (d < 4f * rr ? 0.2f : 0f);
                            float z = Lift + 0.003f + 0.8f * rr * Mathf.Max(0f, 1f - d / (4f * rr)) + 2f * curl * d * d / Wv + 0.012f * fd * pen.R();
                            float bar = Sstep(0.55f, 0.95f, Mathf.Sin((tc + 0.12f * d / L) * bars * Tau + ph));
                            Color c = Mul(vane, (1f - 0.13f * bar) * (1f + 0.1f * fd) * (1f + 0.05f * sq));
                            float jy = (pen.R() - 0.5f) * 1.5f * rb * fd;
                            Dot(pen, end, f.P(px + xo, py + jy, z), f.D(0f, -side * dzdd, 1f), V(pen, c, 0.08f), Line(pen, 1.1f * rs) * (1f + 0.2f * fd));
                        }
                        px += rb * Mathf.Cos(ang);
                        py += side * rb * Mathf.Sin(ang);
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ fern

        /// <summary>A dried fern frond, size 2–4 long, lying flat: a curving stalk and midrib with alternating lobed
        /// leaflets that shrink toward the tip; sage green, browning toward the tips.</summary>
        public static void FernFrond(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var f = new Frame(pen, p, n, ta, tb);
            int end = pen.into.Count + pen.Cap(Cap);
            float L = Mathf.Max(size, 0.3f), rMin = 0.5f * R0(pen), xo = -0.5f * L;
            float bend = (pen.R() * 2f - 1f) * 0.12f, st = 0.16f + 0.06f * pen.R(), Lmax = L * (0.26f + 0.06f * pen.R());
            int per = 9 + (int)(pen.R() * 4.999f);
            float browning = 0.5f + 0.5f * pen.R(), th0 = 1f + 0.15f * pen.R();
            Color green = Tint(pen, new Color(0.16f, 0.24f, 0.12f)), brown = Tint(pen, new Color(0.28f, 0.18f, 0.08f));

            // the stalk and midrib
            for (float x = 0f; x < L;)
            {
                float t = x / L, r = Mathf.Max(rMin, 0.011f * L * (1f - 0.7f * t));
                Dot(pen, end, f.P(x + xo, bend * L * t * t, Lift + r), f.n, V(pen, Mul(Color.Lerp(green, brown, 0.3f + 0.5f * t), 0.85f), 0.06f), r);
                x += 0.9f * r;
            }

            // the leaflets, left and right in turn, each a lobed blade leaving the midrib at an angle and curving forward
            float s = Spacing(pen, 2f * per * Lmax * Lmax * 0.55f * 0.18f, 7000), rs = s * Mathf.Sqrt(pen.PerArea);
            for (int k = 0; k < 2 * per; k++)
            {
                float side = (k & 1) == 0 ? 1f : -1f;
                float tk = st + (0.97f - st) * (k + 0.5f + 0.2f * (pen.R() - 0.5f)) / (2 * per), u = (tk - st) / (1f - st);
                float lp = Lmax * Mathf.Pow(Mathf.Sin(3.1415927f * Mathf.Pow(u, 0.6f)), 0.8f) * (0.9f + 0.2f * pen.R());
                if (lp < 0.04f) continue;
                float wp = lp * (0.12f + 0.03f * pen.R()), lobes = 3f + Mathf.Round(3f * lp / Lmax), curv = 0.12f;
                float curlP = 0.08f + 0.07f * pen.R(), tipLift = 0.03f + 0.03f * pen.R(), th = th0 + 0.1f * (pen.R() - 0.5f);
                float rr = Mathf.Max(rMin, 0.011f * L * (1f - 0.7f * tk)), zk = 0.0015f * (k % 3);
                // where it leaves the midrib, the way it points (u) and the way it curves (v)
                float bx = tk * L, by = bend * L * tk * tk, tx = 1f, ty = 2f * bend * tk, tl = Mathf.Sqrt(tx * tx + ty * ty);
                tx /= tl;
                ty /= tl;
                float ct = Mathf.Cos(th), sn = Mathf.Sin(th);
                float ux = tx * ct - side * ty * sn, uy = ty * ct + side * tx * sn, vx = tx * sn + side * ty * ct, vy = ty * sn - side * tx * ct;
                int na = Mathf.CeilToInt(lp / s), nq = Mathf.CeilToInt(2f * wp / s);
                for (int j = 0; j < nq; j++)
                    for (int i = 0; i < na; i++)
                    {
                        float a = (i + 0.15f + 0.7f * pen.R()) * s, q = -wp + (j + 0.15f + 0.7f * pen.R()) * s, sa = a / lp;
                        float hw = wp * Bell(sa, 0.35f, 0.7f) * (0.55f + 0.45f * Mathf.Pow(Mathf.Abs(Mathf.Sin(3.1415927f * sa * lobes)), 0.7f));
                        if (Mathf.Abs(q) >= hw) continue;
                        float ax = ux + vx * 2f * curv * sa, ay = uy + vy * 2f * curv * sa, al = Mathf.Sqrt(ax * ax + ay * ay);
                        ax /= al;
                        ay /= al;
                        float px = bx + ux * a + vx * curv * lp * sa * sa - ay * q, py = by + uy * a + vy * curv * lp * sa * sa + ax * q;
                        float z = Lift + 0.003f + zk + 0.7f * rr * Mathf.Pow(1f - sa, 4f) + 2f * curlP * q * q / wp + tipLift * lp * sa * sa;
                        float dzdq = 4f * curlP * q / wp, dzda = (2f * tipLift * lp * sa - 2.8f * rr * Mathf.Pow(1f - sa, 3f)) / lp;
                        float br = Mathf.Clamp01(browning * Mathf.Pow(u, 1.6f) + 0.55f * sa * sa + 0.15f * pen.R()), e = Mathf.Abs(q) / hw;
                        Color c = Mul(Color.Lerp(green, brown, br), (1f + 0.1f * Sstep(0.25f, 0f, e)) * (1f - 0.12f * Sstep(0.8f, 1f, e)));
                        Dot(pen, end, f.P(px + xo, py, z), f.D(-ax * dzda + ay * dzdq, -ay * dzda - ax * dzdq, 1f), V(pen, c), pen.Radius(1.25f * rs));
                    }
            }
        }

        // ------------------------------------------------------------------------------------------------ seed

        /// <summary>A seed, size 0.4–0.9 long, lying on its side: either a striped sunflower-like seed (a flat teardrop
        /// with dark and light stripes along it) or an acorn-like nut (an egg-shaped brown body under a scaly cap).</summary>
        public static void Seed(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var f = new Frame(pen, p, n, ta, tb);
            int end = pen.into.Count + pen.Cap(Cap);
            float L = Mathf.Max(size, 0.1f);
            if (pen.R() < 0.55f) StripedSeed(pen, f, end, L);
            else Acorn(pen, f, end, L);
        }

        /// <summary>A flat teardrop seed, pointed at one end, with dark and light stripes along it and pale edges.</summary>
        static void StripedSeed(ThingPen pen, Frame f, int end, float L)
        {
            float A = L * (0.24f + 0.05f * pen.R()), k = 0.5f + 0.12f * pen.R(), p1 = 0.75f + 0.2f * pen.R(), p2 = 0.4f + 0.1f * pen.R();
            float stripes = 4f + Mathf.Round(2f * pen.R()), ph = pen.R() * Tau, zc = Lift + k * A, xo = -0.5f * L, s = 1f / Mathf.Sqrt(pen.PerArea);
            Color dark = Tint(pen, new Color(0.06f, 0.06f, 0.07f)), light = Tint(pen, new Color(0.4f, 0.4f, 0.38f));
            int rings = Mathf.CeilToInt(1.3f * L / s);
            for (int i = 0; i < rings; i++)
            {
                // rings closer together toward both ends, so the point and the blunt end close up
                float t = 0.5f - 0.5f * Mathf.Cos(3.1415927f * (i + 0.2f + 0.6f * pen.R()) / rings), a = A * Bell(t, p1, p2);
                float da = A * (Bell(Mathf.Min(t + 0.01f, 0.9999f), p1, p2) - Bell(Mathf.Max(t - 0.01f, 0.0001f), p1, p2)) / (0.02f * L);
                int around = Mathf.Max(4, Mathf.CeilToInt(Tau * a / s));
                for (int j = 0; j < around; j++)
                {
                    float phi = (j + pen.R()) * Tau / around, cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
                    float g = Mathf.Sin(cp * stripes * 3.1415927f + 0.25f * Mathf.Sin(t * 7f + ph));
                    Color c = Color.Lerp(dark, light, Sstep(0.3f, 0.75f, g));
                    c = Color.Lerp(c, light, 0.7f * Sstep(0.88f, 0.98f, Mathf.Abs(cp)) + 0.4f * Sstep(0.1f, 0f, t));
                    Dot(pen, end, f.P(t * L + xo, a * cp, zc + k * a * sp), f.D(-da, cp, sp / k), V(pen, c, 0.08f), pen.Radius(1.2f));
                }
            }
        }

        /// <summary>The acorn body's radius at t (0 at the capped end, 1 at the tip).</summary>
        static float AcornR(float t, float R)
        {
            float u = 2f * t - 1f;
            return R * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (1f - 0.15f * u);
        }

        /// <summary>An acorn-like nut: an egg-shaped brown body, its wide end in a scaly cup with a stub of stalk, a pale
        /// band where the cup meets it and a small dark point at the tip.</summary>
        static void Acorn(ThingPen pen, Frame f, int end, float L)
        {
            float R = L * (0.3f + 0.05f * pen.R()), capF = 0.3f + 0.1f * pen.R(), thick = 0.12f * R, xo = -0.5f * L;
            float rowsN = 4f + Mathf.Round(2f * pen.R()), aroundN = 10f + Mathf.Round(4f * pen.R()), ph = pen.R() * Tau;
            Color nut = Tint(pen, new Color(0.3f, 0.18f, 0.08f)), cup = Tint(pen, new Color(0.2f, 0.15f, 0.1f));
            float top = 0f;   // its widest, so it rests on the floor there
            for (int i = 0; i <= 24; i++) top = Mathf.Max(top, AcornR(i / 24f, R) + (i / 24f <= capF ? 1.35f * thick : 0f));
            float zc = Lift + top, s = 1f / Mathf.Sqrt(pen.PerArea);

            // the body, from just under the cup to the tip (rings closer toward the tip)
            float t0 = capF - 0.05f;
            int rings = Mathf.CeilToInt(1.3f * (1f - t0) * L / s);
            for (int i = 0; i < rings; i++)
            {
                float t = t0 + (1f - t0) * Mathf.Sin(1.5707963f * (i + 0.2f + 0.6f * pen.R()) / rings);
                float r = AcornR(t, R), dr = (AcornR(Mathf.Min(t + 0.01f, 1f), R) - AcornR(t - 0.01f, R)) / (0.02f * L);
                int around = Mathf.Max(4, Mathf.CeilToInt(Tau * r / s));
                for (int j = 0; j < around; j++)
                {
                    float phi = (j + pen.R()) * Tau / around, cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
                    float shade = (1f + 0.07f * Mathf.Sin(phi * 9f + ph)) * (1f + 0.3f * Sstep(capF + 0.07f, capF, t)) * (1f - 0.25f * Sstep(0.85f, 1f, t));
                    Dot(pen, end, f.P(t * L + xo, r * cp, zc + r * sp), f.D(-dr, cp, sp), V(pen, Mul(nut, shade), 0.08f), pen.Radius(1.2f));
                }
            }
            for (int i = 0; i < 5; i++)   // the small dark point at the tip
                Dot(pen, end, f.P(L + xo - 0.01f * L + 0.015f * R * i, 0f, zc), f.D(1f, 0f, 0.3f), V(pen, Mul(nut, 0.5f), 0.06f), Mathf.Max(0.5f * R0(pen), 0.07f * R * (1f - 0.12f * i)));

            // the cup: overlapping scales in staggered rows, their tips lighter and raised
            int crings = Mathf.CeilToInt(1.4f * capF * L / s);
            for (int i = 0; i < crings; i++)
            {
                float t = capF * (i + 0.2f + 0.6f * pen.R()) / crings;
                float rb = AcornR(t, R) + thick, drb = (AcornR(t + 0.01f, R) - AcornR(Mathf.Max(t - 0.01f, 0f), R)) / (0.02f * L);
                int around = Mathf.Max(5, Mathf.CeilToInt(Tau * rb / s));
                for (int j = 0; j < around; j++)
                {
                    float phi = (j + pen.R()) * Tau / around, cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
                    float rv = t / capF * rowsN, row = Mathf.Floor(rv), fr = rv - row, cv = phi / Tau * aroundN + 0.5f * row, fc = cv - Mathf.Floor(cv);
                    float lit = Sstep(0.15f, 0.9f, fr) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Abs(fc - 0.5f) * 2f)), rr = rb + 0.35f * thick * lit;
                    Dot(pen, end, f.P(t * L + xo, rr * cp, zc + rr * sp), f.D(-drb - 0.3f * lit, cp, sp), V(pen, Mul(cup, Mathf.Lerp(0.6f, 1.35f, lit)), 0.08f), pen.Radius(1.2f));
                }
            }
            // the cup's rim, facing the tip
            float rl = AcornR(capF, R);
            int lip = Mathf.Max(8, Mathf.CeilToInt(Tau * (rl + thick) / s));
            for (int m = 0; m < 2; m++)
                for (int j = 0; j < lip; j++)
                {
                    float phi = (j + pen.R()) * Tau / lip, rr = rl + thick * (0.35f + 0.7f * m), cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
                    Dot(pen, end, f.P(capF * L + xo + 0.004f, rr * cp, zc + rr * sp), f.D(1f, 0.4f * cp, 0.4f * sp), V(pen, Mul(cup, 1.2f), 0.08f), pen.Radius(1f));
                }
            // a stub of stalk at the cup's pole
            float rs0 = Mathf.Max(0.5f * R0(pen), 0.1f * R);
            for (float d = -0.02f * L; d > -0.02f * L - 0.25f * R; d -= 0.9f * rs0)
                Dot(pen, end, f.P(xo + d, 0f, zc), f.D(-0.3f, 0f, 1f), V(pen, Mul(cup, 0.8f), 0.06f), rs0);
        }
    }
}
