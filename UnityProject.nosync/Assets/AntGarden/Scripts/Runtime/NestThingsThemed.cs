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
        public static void ResinDrops(ThingPen pen, Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size)
        {
            var s = new NestThingsGrowth.Spot(pen, p, n, ta, tb);
            int count = 2 + (int)(s.R() * 5f);
            Color skin = new Color(0.30f, 0.13f, 0.03f), core = new Color(0.98f, 0.60f, 0.20f), speck = new Color(0.05f, 0.025f, 0.01f);
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
            // the light inside, densest low and in the middle, round the ancestor
            int k = Mathf.RoundToInt(m * 1.6f);
            for (int j = 0; j < k; j++)
            {
                Vector3 d = InBall(pen, out float fr);
                var pos = c + new Vector3(d.x * hx, d.y * hy, d.z * hz) * 0.82f;
                float low = Mathf.Clamp01(0.6f - d.y * 0.5f);
                f.Put(pos, Vector3.up, core * (0.75f + 0.4f * low) * (1.05f - 0.45f * fr), rad * (0.3f + 0.25f * f.R()), Mathf.Clamp01(1.05f - 0.5f * fr));
            }
            // the ancestor: its points in amber shadow (darker than the light round it, a faint glow of its own)
            if (antPoints != null && antPoints.Length > 0)
            {
                float yaw = f.R() * Tau, cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw), scale = size * 0.5f;
                int stepAnt = Mathf.Max(1, antPoints.Length / 9000);
                for (int j = 0; j < antPoints.Length; j += stepAnt)
                {
                    Vector3 a = antPoints[j] * scale;
                    // tipped a little, as if it settled while the resin was soft
                    a = new Vector3(a.x, a.y * 0.96f + a.z * 0.28f, a.z * 0.96f - a.y * 0.28f);
                    var pos = c + new Vector3(a.x * cs - a.z * sn, a.y + hy * 0.12f, a.x * sn + a.z * cs);
                    f.Put(pos, Vector3.up, new Color(0.10f, 0.04f, 0.015f) * (0.8f + 0.5f * f.R()), rad * 0.42f, 0.15f);
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
