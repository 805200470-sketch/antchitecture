using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// More kinds of small things for the particle nest's floor (the ground cover at ant scale), each drawn the way a
    /// Gaussian-splat capture covers a surface: a few large dim splats for the body (Body), medium ones for the form
    /// and fine ones on top for the detail. Every generator takes the spot on the floor (p), its unit normal (n) and
    /// two unit tangents (ta, tb), the thing's size in units (the ant is 1 long), a crevice darkening (dark, 0.5..1),
    /// its own random sequence (rnd, its only randomness, so a thing is the same every time it is made) and a callback
    /// taking each point: world position, linear colour and size code (eighths of the base radius). Plain arithmetic
    /// only and nothing allocated: a worker thread runs these.
    /// </summary>
    public static class GroundCoverShapes
    {
        const float Lift = 0.01f;          // no point lower than this above the floor (the dark terrain would hide it)
        const float BaseRadius = 0.007f;   // a splat of size code 8 is about this big (units)
        const float TwoPi = 6.2831853f;
        const int MaxPoints = 180;

        /// <summary>What one thing is drawn with: the floor under it, its darkening, its random sequence and the
        /// callback taking its points.</summary>
        readonly struct Pen
        {
            readonly Vector3 floor, up;
            readonly float dark;
            readonly System.Func<float> rnd;
            readonly System.Action<Vector3, Color, uint> add;

            public Pen(Vector3 p, Vector3 n, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
            {
                floor = p; up = n; this.dark = dark; this.rnd = rnd; this.add = add;
            }

            /// <summary>The next number of the thing's random sequence, in [0, 1).</summary>
            public float R() => rnd();

            /// <summary>A size code from lo to hi.</summary>
            public uint Code(uint lo, uint hi) => lo + (uint)(rnd() * (hi - lo + 1));

            /// <summary>One point's colour: c × light, its brightness varied ±15 % and its warmth a little.</summary>
            public Color Tone(Color c, float light)
            {
                float k = light * (0.85f + 0.3f * rnd()), w = 0.1f * rnd() - 0.05f;
                return new Color(c.r * k * (1f + w), c.g * k, c.b * k * (1f - w));
            }

            /// <summary>Emits one point: darkened for its crevice, its brightest channel kept to 1 (the hue unchanged)
            /// and lifted to at least Lift above the floor.</summary>
            public void Put(Vector3 q, Color c, uint code)
            {
                float h = Vector3.Dot(q - floor, up), k = dark / Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                if (h < Lift) q += up * (Lift - h);
                add(q, new Color(c.r * k, c.g * k, c.b * k), code);
            }
        }

        /// <summary>
        /// The body pass every generator uses: a few large, dim splats spread through an ellipsoid (centre c, half-axes
        /// a, b, h) under a thing's fine points, so its form reads as a solid surface rather than sparse dots. splat is
        /// their radius in units (size codes 10..40); bigger ones are fainter, since they add up over more pixels.
        /// </summary>
        static void Body(in Pen pen, Vector3 c, Vector3 a, Vector3 b, Vector3 h, int count, float splat, Color col)
        {
            for (int k = 0; k < count; k++)
            {
                float th = pen.R() * TwoPi, z = pen.R() * 2f - 1f, s = Mathf.Sqrt(1f - z * z), r = 0.7f * Mathf.Sqrt(pen.R());
                uint code = (uint)Mathf.Clamp(Mathf.RoundToInt(splat * (0.8f + 0.4f * pen.R()) / BaseRadius * 8f), 10, 40);
                Vector3 q = c + a * (r * s * Mathf.Cos(th)) + b * (r * s * Mathf.Sin(th)) + h * (r * z);
                pen.Put(q, pen.Tone(col, Mathf.Lerp(0.6f, 0.3f, (code - 10f) / 30f)), code);
            }
        }

        /// <summary>Points for a thing this big: 12 + size × perUnit, at most MaxPoints.</summary>
        static int Budget(float size, float perUnit) => Mathf.Min(MaxPoints, 12 + (int)(size * perUnit));

        /// <summary>The direction at this angle in the floor's plane.</summary>
        static Vector3 Dir(Vector3 ta, Vector3 tb, float angle) => ta * Mathf.Cos(angle) + tb * Mathf.Sin(angle);

        /// <summary>A broken twig lying on the floor: a slightly bent stick with bark ridges along it (lighter ridges,
        /// darker grooves), one end snapped into pale splinters; brown-grey with a cool tint.</summary>
        public static void Twig(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color bark = Color.Lerp(new Color(0.42f, 0.34f, 0.28f), new Color(0.30f, 0.36f, 0.42f), 0.25f + 0.3f * rnd());
            Color wood = new Color(0.64f, 0.62f, 0.55f);   // the pale wood showing at the break
            Vector3 along = Dir(ta, tb, rnd() * TwoPi), side = Vector3.Cross(n, along);
            float rad = 0.004f + size * 0.06f, bend = (rnd() - 0.5f) * 0.24f, twist = rnd() * TwoPi;
            int ridges = 6 + (int)(rnd() * 3), splinters = 2 + (int)(rnd() * 3), segs = 2 + (int)(size * 12f);
            int skin = Mathf.Max(10, Budget(size, 600) - splinters * 3 - 4 - segs * 2);
            float Rad(float t) => rad * (1.05f - 0.15f * t);   // tapering toward the broken end
            Vector3 Axis(float t) => p + along * ((t - 0.5f) * size) + side * (bend * size * Mathf.Sin(t * Mathf.PI)) + n * (Lift + Rad(t));
            for (int k = 0; k < skin; k++)
            {
                // a ridge, or a groove between two, over the upper part of the stick (its underside lies on the floor)
                float t = rnd(), a = rnd() * 4.3f - 0.6f;
                bool groove = rnd() < 0.3f;
                float slot = Mathf.Round((a - twist) * ridges / TwoPi) + (groove ? 0.5f : 0f);
                a = twist + slot * TwoPi / ridges + 0.12f * Mathf.Sin(t * 11f + slot) + (rnd() - 0.5f) * 0.1f;
                float up = Mathf.Sin(a), light = (0.55f + 0.45f * Mathf.Max(0f, up)) * (groove ? 0.55f : 1f);
                Vector3 q = Axis(t) + (side * Mathf.Cos(a) + n * up) * (Rad(t) * (groove ? 0.9f : 1.05f));
                pen.Put(q, pen.Tone(bark, light), groove ? pen.Code(6, 7) : pen.Code(5, 6));
            }
            // the broken end: its torn face, and splinters of pale wood standing out of it
            Vector3 end = Axis(1f);
            for (int k = 0; k < 4; k++)
            {
                float a = rnd() * TwoPi;
                pen.Put(end + (side * Mathf.Cos(a) + n * Mathf.Sin(a)) * (Rad(1f) * Mathf.Sqrt(rnd())), pen.Tone(wood, 0.8f), 5u);
            }
            for (int s = 0; s < splinters; s++)
            {
                float a = rnd() * 3.4f - 0.15f, reach = size * (0.05f + 0.12f * rnd()), dev = (rnd() - 0.5f) * 0.6f;
                Vector3 at = end + (side * Mathf.Cos(a) + n * Mathf.Sin(a)) * (Rad(1f) * (0.3f + 0.6f * rnd()));
                Vector3 dir = along * Mathf.Cos(dev) + side * Mathf.Sin(dev) + n * ((rnd() - 0.4f) * 0.3f);
                for (int j = 1; j <= 3; j++)
                    pen.Put(at + dir * (reach * j / 3f), pen.Tone(Color.Lerp(wood, bark, 0.15f * j), 1.05f - 0.1f * j), j == 1 ? 5u : 4u);
            }
            // the body: large dim splats along the stick's core
            for (int m = 0; m < segs; m++)
            {
                float t = (m + 0.5f) / segs;
                Body(pen, Axis(t) - n * (Rad(t) * 0.2f), along * (0.5f * size / segs), side * (Rad(t) * 0.6f), n * (Rad(t) * 0.5f), 2, Rad(t) * 1.3f, bark * 0.8f);
            }
        }

        /// <summary>A split seed pod: two husk halves curved like little boats, hinged at the stalk end and opened into
        /// a V, with two to five pale round seeds lying in them; the husk muted ochre or rust, paler inside.</summary>
        public static void SeedPod(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color husk = rnd() < 0.55f ? new Color(0.50f, 0.40f, 0.25f) : new Color(0.48f, 0.30f, 0.24f);
            Color lining = new Color(0.62f, 0.56f, 0.46f), seed = rnd() < 0.6f ? new Color(0.72f, 0.70f, 0.62f) : new Color(0.62f, 0.72f, 0.66f);
            Vector3 along = Dir(ta, tb, rnd() * TwoPi), side = Vector3.Cross(n, along), hinge = p - along * (size * 0.45f);
            float splay = 0.22f + 0.22f * rnd(), half = size * 0.17f, deep = size * 0.15f, sr = 0.002f + size * 0.06f;
            int seeds = 2 + (int)(rnd() * 4), total = Budget(size, 560), hull = Mathf.Max(8, (total - seeds * 7 - 7) / 2);
            for (int j = 1; j <= 3; j++)
                pen.Put(hinge - along * (size * 0.04f * j) + n * Lift, pen.Tone(husk, 0.6f), 5u);   // the stalk
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 ax = along * Mathf.Cos(splay) + side * (s * Mathf.Sin(splay)), across = Vector3.Cross(n, ax);
                for (int k = 0; k < hull; k++)
                {
                    // from rim to rim through the keel; every fourth point on a rim, drawing the boat's outline
                    float u = rnd(), th = (rnd() * 2f - 1f) * 1.5708f, prof = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.6f);
                    if (k % 4 == 0) th = th < 0f ? -1.5708f : 1.5708f;
                    float edge = Mathf.Abs(th) / 1.5708f, flare = 1f + 0.25f * edge * edge * edge;
                    Vector3 q = hinge + ax * (u * size) + across * (Mathf.Sin(th) * half * prof * flare) + n * (Lift + (1f - Mathf.Cos(th)) * deep * prof);
                    pen.Put(q, pen.Tone(Color.Lerp(lining, husk, 0.35f + 0.65f * edge), 0.5f + 0.5f * edge), k % 4 == 0 ? pen.Code(4, 5) : pen.Code(6, 8));
                }
                Body(pen, hinge + ax * (size * 0.5f) + n * (Lift + deep * 0.35f), ax * (size * 0.3f), across * (half * 0.6f), n * (deep * 0.3f), 1 + total / 40, half * 0.9f, husk * 0.8f);
            }
            // the seeds along the keels; a fourth and fifth have rolled out of the opening
            for (int i = 0; i < seeds; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                Vector3 c = i < 3 ? hinge + (along * Mathf.Cos(splay) + side * (s * Mathf.Sin(splay))) * (size * (0.3f + 0.45f * rnd()))
                                  : p + along * (size * (0.6f + 0.12f * rnd())) + side * ((rnd() - 0.5f) * size * 0.25f);
                c += n * (Lift + sr * 0.9f);
                Body(pen, c, along * (sr * 0.3f), side * (sr * 0.3f), n * (sr * 0.3f), 1, sr * 1.3f, seed * 0.8f);
                pen.Put(c + n * sr, pen.Tone(seed, 1.05f), 4u);
                for (int k = 0; k < 5; k++)
                {
                    float a = rnd() * TwoPi, z = rnd(), rr = Mathf.Sqrt(1f - z * z);
                    pen.Put(c + (Dir(ta, tb, a) * rr + n * z) * (sr * 0.9f), pen.Tone(seed, 0.6f + 0.4f * z), pen.Code(4, 5));
                }
            }
        }

        /// <summary>A cluster of two to six tiny mushrooms: thin pale stems under domed caps, each cap lighter at its top,
        /// with a faint glowing rim (cool cyan or pale lilac) and faintly glowing gills; a few spores drift above.</summary>
        public static void SporeCaps(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            float pick = rnd();
            Color cap = pick < 0.4f ? new Color(0.66f, 0.66f, 0.62f) : pick < 0.75f ? new Color(0.50f, 0.60f, 0.62f) : new Color(0.58f, 0.54f, 0.64f);
            Color glow = rnd() < 0.6f ? new Color(0.50f, 0.80f, 0.84f) : new Color(0.68f, 0.62f, 0.86f), stem = new Color(0.52f, 0.56f, 0.54f);
            Color crown = Color.Lerp(cap, new Color(0.8f, 0.8f, 0.8f), 0.35f);
            int caps = 2 + (int)(rnd() * 5), total = Budget(size, 600);
            for (int i = 0; i < caps; i++)
            {
                // the first stands tallest, near the middle; the rest round it, smaller, leaning outward
                float a = rnd() * TwoPi, d = size * 0.3f * Mathf.Sqrt(rnd()) * (i == 0 ? 0.25f : 1f), tall = size * (i == 0 ? 0.7f : 0.3f + 0.35f * rnd());
                float capR = tall * (0.32f + 0.14f * rnd()), capH = capR * (0.45f + 0.25f * rnd());
                Vector3 foot = p + Dir(ta, tb, a) * d + n * Lift, axis = (n + Dir(ta, tb, a) * ((0.1f + 0.25f * rnd()) * (i == 0 ? 0.3f : 1f))).normalized;
                Vector3 r1 = Vector3.Cross(axis, ta).normalized, r2 = Vector3.Cross(axis, r1), top = foot + axis * tall;
                int share = Mathf.Max(10, (int)(total * tall / (size * caps * 0.52f)));
                int stemPts = 2 + share / 6, rimPts = 2 + share / 6, gills = 1 + share / 14, dome = Mathf.Max(4, share - stemPts - rimPts - gills - 1);
                for (int k = 0; k < stemPts; k++)
                {
                    float f = (k + rnd()) / stemPts;
                    pen.Put(foot + axis * (f * tall) + r1 * (Mathf.Sin(f * Mathf.PI) * tall * 0.04f), pen.Tone(stem, 0.75f - 0.25f * f), pen.Code(4, 5));
                }
                for (int k = 0; k < dome; k++)
                {
                    // even over the dome (z: how far up it, 1 at the top); the rim droops a little
                    float b = rnd() * TwoPi, z = rnd(), sz = Mathf.Sqrt(1f - z * z);
                    Vector3 q = top + axis * (capH * z - capR * 0.12f * sz * sz * sz * sz) + (r1 * Mathf.Cos(b) + r2 * Mathf.Sin(b)) * (capR * sz);
                    pen.Put(q, pen.Tone(Color.Lerp(cap * 0.8f, crown, z), 0.6f + 0.45f * z), pen.Code(5, 7));
                }
                for (int k = 0; k < rimPts; k++)
                {
                    float b = (k + 0.6f * rnd()) / rimPts * TwoPi;
                    pen.Put(top + (r1 * Mathf.Cos(b) + r2 * Mathf.Sin(b)) * (capR * 1.02f) - axis * (capR * 0.12f), pen.Tone(glow, 0.85f), pen.Code(4, 5));
                }
                for (int k = 0; k < gills; k++)
                {
                    float b = rnd() * TwoPi;
                    pen.Put(top + (r1 * Mathf.Cos(b) + r2 * Mathf.Sin(b)) * (capR * (0.35f + 0.55f * rnd())) - axis * (capR * 0.08f), pen.Tone(glow, 0.35f), 5u);
                }
                Body(pen, top + axis * (capH * 0.4f), r1 * (capR * 0.7f), r2 * (capR * 0.7f), axis * (capH * 0.35f), 1, capR * 1.1f, cap * 0.8f);
            }
            // a few spores drifting just above the cluster
            for (int k = 2 + (int)(rnd() * 3); k > 0; k--)
                pen.Put(p + Dir(ta, tb, rnd() * TwoPi) * (size * 0.4f * rnd()) + n * (size * (0.8f + 0.6f * rnd())), pen.Tone(glow, 0.55f), 4u);
        }

        /// <summary>A small feather fragment lying flat: a fine shaft with barbs angled toward the tip on both sides (a
        /// few split away, the edges frayed) and a wisp of down at the quill; bone white to pale grey-blue.</summary>
        public static void Feather(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color vane = Color.Lerp(new Color(0.74f, 0.73f, 0.68f), new Color(0.56f, 0.62f, 0.72f), rnd()), shaft = new Color(0.80f, 0.78f, 0.72f);
            Vector3 along = Dir(ta, tb, rnd() * TwoPi), side = Vector3.Cross(n, along), start = p - along * (size * 0.5f);
            float bend = (rnd() - 0.5f) * 0.3f, wideA = size * (0.2f + 0.06f * rnd()), wideB = size * (0.12f + 0.06f * rnd());
            int total = Budget(size, 620), shaftPts = 5 + total / 7, down = 3 + total / 20, barbs = 4 + (int)(size * 30f);
            int perBarb = Mathf.Max(2, (total - shaftPts - down - 4) / (2 * barbs));
            // the shaft at t (0 at the quill, 1 at the tip), curving gently to one side
            Vector3 Shaft(float t) => start + along * (t * size) + side * (bend * size * t * t) + n * (Lift + 0.003f);
            for (int k = 0; k < shaftPts; k++)
            {
                float t = (k + rnd()) / shaftPts;
                pen.Put(Shaft(t), pen.Tone(shaft, 1f - 0.25f * t), t < 0.3f ? 6u : 5u);
            }
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float wide = sgn < 0 ? wideA : wideB;
                for (int b = 0; b < barbs; b++)
                {
                    // most barbs lie zipped together at about 45° toward the tip; a few have split off, shorter, at other angles
                    float t = 0.12f + 0.86f * (b + 0.3f + 0.4f * rnd()) / barbs, w = wide * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.1f) / 0.92f))));
                    bool split = rnd() < 0.25f;
                    float ang = 0.8f + (rnd() - 0.5f) * (split ? 0.6f : 0.08f), reach = w * 1.4f * (split ? 0.65f + 0.3f * rnd() : 1f);
                    Vector3 d = (along + side * (2f * bend * t)).normalized, dir = d * Mathf.Cos(ang) + Vector3.Cross(n, d) * (sgn * Mathf.Sin(ang));
                    for (int j = 0; j < perBarb; j++)
                    {
                        float f = (j + 0.5f + 0.4f * (rnd() - 0.5f)) / perBarb;
                        Vector3 q = Shaft(t) + dir * (f * reach) + n * (0.003f * Mathf.Sin(f * Mathf.PI) - 0.003f);
                        if (f > 0.75f) q += Dir(ta, tb, rnd() * TwoPi) * (w * 0.1f);   // the frayed edge
                        pen.Put(q, pen.Tone(vane, (0.95f - 0.2f * f) * (0.92f + 0.08f * Mathf.Sin(t * 24f))), pen.Code(4, 5));
                    }
                }
                Body(pen, Shaft(0.55f) + side * (sgn * wide * 0.45f) - n * 0.003f, along * (size * 0.3f), side * (wide * 0.45f), n * 0.001f, 1 + total / 40, wide * 0.6f, vane * 0.7f);
            }
            // down at the quill: a loose wisp of fine points
            for (int k = 0; k < down; k++)
            {
                Vector3 q = Shaft(0.04f + 0.18f * rnd()) + side * ((rnd() * 2f - 1f) * size * 0.12f) + along * (rnd() * size * 0.04f) + n * (0.006f * rnd());
                pen.Put(q, pen.Tone(Color.Lerp(vane, new Color(0.8f, 0.8f, 0.8f), 0.3f), 0.55f), pen.Code(4, 5));
            }
        }

        /// <summary>Three to seven small faceted grains, like quartz sand at ant scale: six-sided prisms with pointed ends
        /// lying on the floor, each facet evenly lit so the faces read; icy blue to white, a few edges sparkling.</summary>
        public static void Crystals(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color ice = rnd() < 0.6f ? new Color(0.55f, 0.68f, 0.80f) : new Color(0.60f, 0.72f, 0.74f);
            Color white = new Color(0.80f, 0.84f, 0.88f), spark = new Color(0.76f, 0.80f, 0.84f);
            Vector3 sun = (n * 0.8f + ta * 0.45f + tb * 0.3f).normalized;   // where the facets' light comes from
            int grains = 3 + (int)(rnd() * 5), total = Budget(size, 560);
            for (int g = 0; g < grains; g++)
            {
                float big = g == 0 ? 1f : 0.45f + 0.45f * rnd(), rad = size * 0.13f * big, half = rad * (1f + 1.2f * rnd());
                float tilt = 0.08f + 0.3f * rnd(), roll = rnd() * TwoPi, st = Mathf.Sin(tilt);
                Vector3 ax = Dir(ta, tb, rnd() * TwoPi) * Mathf.Cos(tilt) + n * st, u1 = Vector3.Cross(n, ax).normalized, u2 = Vector3.Cross(ax, u1);
                float rest = Lift + 0.9f * Mathf.Max(half * st, 0.6f * half * st + rad * Mathf.Cos(tilt));   // its lowest edge just on the floor
                Vector3 c = p + Dir(ta, tb, rnd() * TwoPi) * (size * 0.4f * Mathf.Sqrt(rnd()) * (g == 0 ? 0.2f : 1f)) + n * rest;
                int pts = Mathf.Max(6, (int)(total * big / (grains * 0.74f)) - 3);
                for (int k = 0; k < pts; k++)
                {
                    // a point on one facet: six sides round the prism, the ends drawn in to points
                    int f = (int)(rnd() * 6f);
                    float e = rnd(), u = rnd() * 2f - 1f, au = Mathf.Abs(u), sc = au < 0.6f ? 1f : (1f - au) / 0.4f;
                    float a0 = roll + f * 1.0472f, am = a0 + 0.5236f;
                    Vector3 v0 = u1 * Mathf.Cos(a0) + u2 * Mathf.Sin(a0), v1 = u1 * Mathf.Cos(a0 + 1.0472f) + u2 * Mathf.Sin(a0 + 1.0472f);
                    Vector3 face = u1 * Mathf.Cos(am) + u2 * Mathf.Sin(am);
                    if (au >= 0.6f) face = (face * 0.55f + ax * (u > 0f ? 0.85f : -0.85f)).normalized;
                    float light = 0.3f + 0.65f * Mathf.Max(0f, Vector3.Dot(face, sun));
                    bool edge = e < 0.1f || e > 0.9f || Mathf.Abs(au - 0.6f) < 0.04f;
                    pen.Put(c + ax * (u * half) + (v0 + (v1 - v0) * e) * (rad * sc), pen.Tone(Color.Lerp(ice, white, 0.4f * light), edge ? light + 0.12f : light), edge ? 4u : pen.Code(5, 6));
                }
                // a few of its edges and points catch the light
                for (int k = 1 + (int)(rnd() * 2.5f); k > 0; k--)
                {
                    bool tip = rnd() < 0.3f;
                    float a = roll + (int)(rnd() * 6f) * 1.0472f, u = tip ? (rnd() < 0.5f ? -1f : 1f) : (rnd() * 2f - 1f) * 0.6f;
                    Vector3 q = c + ax * (u * half) + (tip ? Vector3.zero : (u1 * Mathf.Cos(a) + u2 * Mathf.Sin(a)) * rad);
                    pen.Put(q, pen.Tone(spark, 1.2f), 4u);
                }
                Body(pen, c, ax * (half * 0.6f), u1 * (rad * 0.6f), u2 * (rad * 0.6f), g == 0 ? 2 : 1, rad * 1.2f, ice * 0.7f);
            }
        }

        /// <summary>A single fallen petal, cupped, its edges curling up and its tip lifting; dusty rose or cream, with
        /// slightly darker veins fanning from the base. Mostly medium splats, for a soft body.</summary>
        public static void Petal(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color tint = rnd() < 0.55f ? new Color(0.62f, 0.42f, 0.46f) : new Color(0.72f, 0.67f, 0.56f);
            Vector3 along = Dir(ta, tb, rnd() * TwoPi), side = Vector3.Cross(n, along), start = p - along * (size * 0.45f);
            float wide = size * (0.3f + 0.1f * rnd()), cup = wide * (0.2f + 0.2f * rnd()), rise = size * (0.05f + 0.1f * rnd());
            bool notch = rnd() < 0.4f;
            int total = Budget(size, 520), body = 2 + total / 12, rim = 4 + total / 7, skin = Mathf.Max(8, total - body - rim);
            for (int k = 0; k < skin + rim; k++)
            {
                // x runs from base to tip, v from edge to edge (the last points trace the outline)
                bool edge = k >= skin, vein = !edge && rnd() < 0.3f;
                float x = Mathf.Sqrt(rnd()), v = edge ? (rnd() < 0.5f ? -1f : 1f) : rnd() * 2f - 1f;
                if (vein) v = Mathf.Round(v * 2.5f) / 2.5f + (rnd() - 0.5f) * 0.04f;   // on a vein fanning out from the base
                if (notch && x > 0.9f && Mathf.Abs(v) < 0.3f) x -= 0.1f * (1f - Mathf.Abs(v) / 0.3f);
                float w = wide * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(x, 1.4f)))), h = cup * v * v * (0.4f + 0.6f * x) + rise * x * x * x;
                Vector3 q = start + along * (x * size) + side * (v * w * (1f - 0.12f * v * v)) + n * (Lift + h);
                float light = (0.62f + 0.38f * h / (cup + rise)) * (vein ? 0.78f : 1f) * (x < 0.15f ? 0.8f : 1f) * (edge ? 1.08f : 1f);
                pen.Put(q, pen.Tone(tint, light), edge ? pen.Code(5, 6) : pen.Code(7, 9));
            }
            Body(pen, start + along * (size * 0.58f) + n * (Lift + cup * 0.25f), along * (size * 0.38f), side * (wide * 0.65f), n * (cup * 0.2f), body, wide * 0.55f, tint * 0.75f);
        }

        /// <summary>A short root lying on the floor, wavering and tapering, with bead-like nodules along it and a few fine
        /// rootlets; tan to teal-grey, the nodules lighter.</summary>
        public static void RootNodules(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color root = Color.Lerp(new Color(0.48f, 0.42f, 0.33f), new Color(0.36f, 0.46f, 0.46f), rnd());
            Color bead = Color.Lerp(root, new Color(0.76f, 0.76f, 0.68f), 0.55f);
            Vector3 along = Dir(ta, tb, rnd() * TwoPi), side = Vector3.Cross(n, along);
            float rad = 0.003f + size * 0.035f, wave = size * (0.04f + 0.06f * rnd()), phase = rnd() * TwoPi;
            int nodules = 3 + (int)(rnd() * 3) + (int)(size * 6f), rootlets = 1 + (int)(rnd() * 3), segs = 2 + (int)(size * 10f);
            int total = Budget(size, 600), perNodule = 3 + total / 20, skin = Mathf.Max(10, total - nodules * (perNodule + 2) - rootlets * 4 - segs);
            float Rad(float t) => rad * (1.15f - 0.4f * t);
            Vector3 Axis(float t) => p + along * ((t - 0.5f) * size) + side * (wave * Mathf.Sin(t * 5f + phase)) + n * (Lift + Rad(t));
            for (int k = 0; k < skin; k++)
            {
                float t = rnd(), a = rnd() * 4.1f - 0.5f, up = Mathf.Sin(a);
                float light = (0.55f + 0.45f * Mathf.Max(0f, up)) * (0.9f + 0.1f * Mathf.Sin(a * 5f + t * 30f));
                pen.Put(Axis(t) + (side * Mathf.Cos(a) + n * up) * Rad(t), pen.Tone(root, light), pen.Code(5, 6));
            }
            for (int i = 0; i < nodules; i++)
            {
                // a bead swelling out of the root, a little to one side, its top catching the light
                float t = Mathf.Clamp((i + 0.5f + (rnd() - 0.5f) * 0.7f) / nodules, 0.06f, 0.94f), nr = Rad(t) * (1.5f + 1.1f * rnd());
                Vector3 c = Axis(t) + side * ((rnd() - 0.5f) * Rad(t) * 1.2f) + n * ((nr - Rad(t)) * 0.85f);
                Body(pen, c, along * (nr * 0.5f), side * (nr * 0.5f), n * (nr * 0.5f), 1, nr * 1.3f, bead * 0.8f);
                pen.Put(c + n * nr, pen.Tone(bead, 1.1f), 4u);
                for (int k = 0; k < perNodule; k++)
                {
                    float a = rnd() * TwoPi, z = rnd() * 1.2f - 0.2f, rr = Mathf.Sqrt(1f - z * z);
                    pen.Put(c + (Dir(ta, tb, a) * rr + n * z) * nr, pen.Tone(bead, 0.6f + 0.45f * Mathf.Max(0f, z)), pen.Code(5, 6));
                }
            }
            for (int i = 0; i < rootlets; i++)
            {
                // a fine rootlet branching off and running down onto the floor
                float t = 0.15f + 0.7f * rnd(), sgn = rnd() < 0.5f ? -1f : 1f, ang = (rnd() - 0.5f) * 1.2f, reach = size * (0.12f + 0.18f * rnd());
                Vector3 dir = side * (sgn * Mathf.Cos(ang)) + along * Mathf.Sin(ang), at = Axis(t);
                for (int j = 1; j <= 4; j++)
                {
                    float f = j / 4f;
                    pen.Put(at + dir * (f * reach) + along * (f * f * reach * 0.2f) - n * (Rad(t) * f), pen.Tone(root, 0.6f - 0.2f * f), 4u);
                }
            }
            for (int m = 0; m < segs; m++)
            {
                float t = (m + 0.5f) / segs;
                Body(pen, Axis(t), along * (0.5f * size / segs), side * Rad(t), n * (Rad(t) * 0.6f), 1, Rad(t) * 2f, root * 0.75f);
            }
        }

        /// <summary>A tiny snail shell lying on its side: a tube coiling out along a logarithmic spiral for two to three
        /// whorls, pale sand with a faint spiral band or two and fine growth lines, a pale lip round its dark opening.</summary>
        public static void TinyShell(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color sand = rnd() < 0.6f ? new Color(0.66f, 0.60f, 0.50f) : new Color(0.62f, 0.62f, 0.58f);
            Color band = rnd() < 0.5f ? new Color(0.46f, 0.38f, 0.30f) : new Color(0.42f, 0.46f, 0.52f), lip = Color.Lerp(sand, new Color(0.8f, 0.8f, 0.8f), 0.3f);
            // the coil: its whorls, its growth per whorl, the opening against the coil's radius, how far the spire stands out
            float turns = TwoPi * (2f + rnd()), k = Mathf.Log(2f + 0.8f * rnd()) / TwoPi, ap = 0.42f + 0.12f * rnd(), spire = 0.35f + 0.6f * rnd();
            float r0 = size / ((1f + ap) * (1f + Mathf.Exp(-k * Mathf.PI)) * Mathf.Exp(k * turns));
            float band1 = (rnd() - 0.5f) * 1.6f, band2 = rnd() < 0.5f ? band1 + 0.7f : 9f;   // where bands run round the tube (9: none)
            // its coil axis near level and tipped up a little, the spiral turned at random in its own plane
            Vector3 h1 = Dir(ta, tb, rnd() * TwoPi);
            float tip = 0.1f + 0.4f * rnd(), spin = rnd() * TwoPi;
            Vector3 zw = h1 * Mathf.Cos(tip) + n * Mathf.Sin(tip), e1 = Vector3.Cross(n, h1), e2 = Vector3.Cross(zw, e1);
            Vector3 xw = e1 * Mathf.Cos(spin) + e2 * Mathf.Sin(spin), yw = e2 * Mathf.Cos(spin) - e1 * Mathf.Sin(spin);
            // the tube at th along the coil and psi round the tube (f: how far out from its middle), from the coil's centre
            Vector3 At(float th, float psi, float f)
            {
                float r = r0 * Mathf.Exp(k * th), a = ap * r * f, rr = r + a * Mathf.Cos(psi);
                return xw * (rr * Mathf.Cos(th)) + yw * (rr * Mathf.Sin(th)) + zw * (a * Mathf.Sin(psi) - spire * r);
            }
            // set it down: its last whorl reaches lowest, and rests just on the floor
            float low = 0f;
            for (int i = 0; i < 16; i++)
                for (int j = 0; j < 8; j++)
                    low = Mathf.Min(low, Vector3.Dot(At(turns - i * 0.3927f, j * 0.7854f, 1f), n));
            Vector3 o = p + n * (Lift - 0.96f * low);
            int total = Budget(size, 600), lipPts = 4 + total / 12, inside = 2 + total / 30, body = 2 + total / 40, skin = Mathf.Max(10, total - lipPts - inside - body);
            float grow = Mathf.Exp(2f * k * turns) - 1f;
            for (int i = 0; i < skin; i++)
            {
                // more points where the tube is wider (its area grows as r²), over the outer three quarters of it
                float th = Mathf.Log(1f + rnd() * grow) / (2f * k), psi = (rnd() * 2f - 1f) * 2.36f;
                Vector3 nrm = (xw * Mathf.Cos(th) + yw * Mathf.Sin(th)) * Mathf.Cos(psi) + zw * Mathf.Sin(psi);
                float light = (0.55f + 0.45f * Mathf.Max(0f, Vector3.Dot(nrm, n))) * (0.93f + 0.07f * Mathf.Sin(th * 14f));
                bool banded = Mathf.Abs(psi - band1) < 0.22f || Mathf.Abs(psi - band2) < 0.22f;
                pen.Put(o + At(th, psi, 1f), pen.Tone(banded ? Color.Lerp(sand, band, 0.5f) : sand, light), pen.Code(5, 7));
            }
            for (int i = 0; i < lipPts; i++)
                pen.Put(o + At(turns, (i + 0.5f * rnd()) / lipPts * TwoPi, 1.03f), pen.Tone(lip, 1f), pen.Code(4, 5));
            for (int i = 0; i < inside; i++)
                pen.Put(o + At(turns - 0.2f, rnd() * TwoPi, 0.5f), pen.Tone(sand, 0.3f), 6u);
            float rb = ap * r0 * Mathf.Exp(k * (turns - 2.4f));
            Body(pen, o + At(turns - 2.4f, 0f, 0f), xw * rb, yw * rb, zw * rb, body, rb * 1.1f, sand * 0.7f);
        }

        /// <summary>A small dried fern frond lying flat: a curving midrib with leaflets (pinnae) alternating along it,
        /// smaller toward the tip, their edges lobed; sage to olive-teal, browning toward the tips.</summary>
        public static void FernFrond(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, System.Func<float> rnd, System.Action<Vector3, Color, uint> add)
        {
            var pen = new Pen(p, n, dark, rnd, add);
            Color leaf = Color.Lerp(new Color(0.42f, 0.52f, 0.42f), new Color(0.32f, 0.48f, 0.44f), rnd());
            Color brown = new Color(0.46f, 0.34f, 0.24f), rib = new Color(0.40f, 0.40f, 0.32f);
            Vector3 along = Dir(ta, tb, rnd() * TwoPi), side = Vector3.Cross(n, along), start = p - along * (size * 0.5f);
            float bend = (rnd() - 0.5f) * 0.5f, dry = 0.25f + 0.5f * rnd();
            int pairs = 5 + (int)(rnd() * 3) + (int)(size * 12f), total = Budget(size, 640), ribPts = 4 + total / 7, body = 2 + total / 20;
            Vector3 Rib(float t) => start + along * (t * size) + side * (bend * size * t * t) + n * Lift;
            float Slot(int i) => 0.15f + 0.8f * (i + 0.5f) / (2 * pairs);               // where leaflet i sits, sides alternating
            float Reach(float t) => size * (0.03f + 0.27f * Mathf.Pow(1f - t, 0.9f));   // a leaflet's length there
            float sum = 0f;
            for (int i = 0; i < 2 * pairs; i++) sum += Reach(Slot(i)) * Reach(Slot(i));
            int leafPts = Mathf.Max(4 * pairs, total - ribPts - body);
            for (int k = 0; k < ribPts; k++)
            {
                float t = (k + rnd()) / ribPts;
                pen.Put(Rib(t) + n * 0.002f, pen.Tone(Color.Lerp(rib, brown, t < 0.15f ? 0.6f : 0.2f), 0.85f), t < 0.15f ? 6u : 5u);
            }
            for (int i = 0; i < 2 * pairs; i++)
            {
                float t = Slot(i), len = Reach(t), wid = len * 0.26f, ang = 1.1f - 0.4f * t + (rnd() - 0.5f) * 0.12f;
                Vector3 d = (along + side * (2f * bend * t)).normalized, dir = d * Mathf.Cos(ang) + Vector3.Cross(n, d) * ((i % 2 == 0 ? -1f : 1f) * Mathf.Sin(ang));
                Vector3 across = Vector3.Cross(n, dir), at = Rib(t);
                int pts = Mathf.Max(2, Mathf.RoundToInt(leafPts * len * len / sum));
                for (int j = 0; j < pts; j++)
                {
                    // a leaflet: lance-shaped with lobed edges, bending toward the tip, its edges and end curling up a little
                    float u = rnd(), edge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * (0.12f + 0.88f * u))), 0.7f) * (0.75f + 0.25f * Mathf.Abs(Mathf.Sin(u * 15.7f)));
                    float v = (rnd() * 2f - 1f) * edge, brownness = dry * Mathf.Clamp01(u * u * u + (t - 0.7f) * 2f);
                    Vector3 q = at + dir * (u * len) + d * (u * u * len * 0.15f) + across * (v * wid) + n * (0.004f * u + 0.003f * v * v);
                    pen.Put(q, pen.Tone(Color.Lerp(leaf, brown, brownness), 0.75f + 0.25f * (1f - Mathf.Abs(v))), pen.Code(5, 7));
                }
            }
            Body(pen, Rib(0.45f), along * (size * 0.34f), side * (size * 0.2f), n * 0.002f, body, size * 0.09f, leaf * 0.65f);
        }
    }
}
