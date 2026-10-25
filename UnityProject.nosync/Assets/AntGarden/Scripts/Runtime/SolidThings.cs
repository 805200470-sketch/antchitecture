using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's floor at ant scale: the things lying and growing on it, each drawn as opaque lit splats
    /// (Shaders/ParticleSolid.shader) so it reads as what it is: fallen leaves, twigs, petals, seeds, fern fronds,
    /// feathers and the odd fallen blossom where litter gathers (NestThingsLitter); moss cushions, clover, mushrooms,
    /// seedlings and lichen where it is damp (NestThingsGrowth); pebbles, crystal grains and snail shells where it is
    /// stony; and roots creeping out from the foot of the walls. At ant scale they are big: a leaf is several ants
    /// long, a twig a log. They gather along the ways the ant is led, never on them (the glowing way stays clear),
    /// each kind in its own patches; flat things drape over the floor; tall ones block the ants. Made once, when the
    /// nest is built; far away the shader thins them out. Every thing takes one of the nest's tints over its own
    /// colours, so the floor is of a piece with the plants. When the particle look draws its plants solid, their
    /// coarse splats join these in the same buffer, and round the ant a fine set of them is drawn instead (the shell).
    /// Each nest also has things of its own (NestTheme.things, NestThingsThemed), in its own colours; those that glow
    /// can be lit by an antenna like a glowing plant, and the glassy ones (resin, amber) have a light inside that
    /// breathes and brightens as the ant walks past (GlassLamps), as the blue forest's glass bulbs do. At the nest's
    /// heart stands its heart thing (NestTheme.heart).
    /// </summary>
    public class SolidThings : MonoBehaviour
    {
        public int maxThings = 460;
        public float nearDensity = 0.3f;    // things per unit² of floor along the ways
        public float farDensity = 0.07f;    // and far from them
        public int roots = 44;
        public float perArea = 2200f;       // splats per unit² of a thing's surface
        public float gain = 0.62f;          // night: the things are seen by moonlight, darker than the glowing plants
        public Vector4 lod = new Vector4(7f, 30f, 0.28f, 0);    // all splats within x units of the camera, thinned to z of them by y

        enum Kind
        {
            Leaf, Twig, Petal, Seed, Fern, Feather, Blossom, Moss, Clover, Mushrooms, Seedling, Lichen, Pebbles, Crystals, Shell,
            // a nest's own (NestTheme.things)
            GoldBalls, ResinDrops, WaxComb, AmberShards, EmberCracks, ClayChimney, HexCells, PotShards,
        }

        static bool Own(Kind k) => k >= Kind.GoldBalls;
        /// <summary>Checks (SceneShots): where this nest's own things stand, what each is and how tall ("Heart": the
        /// thing at its heart).</summary>
        public static readonly List<(string kind, Vector2 pos, float height)> Owned = new List<(string, Vector2, float)>();
        /// <summary>Checks (StutterCheck): how many fine shells have been made, and swapped in.</summary>
        public static int ShellsMade, ShellsShown;
        NestTheme theme;
        LabNest nest;
        readonly List<(Vector3 pos, float size, Color col)> ownLamps = new List<(Vector3, float, Color)>();
        int touchables;

        // the fine splats of the plants round the ant (ParticleLook.SolidShell), made again on a worker thread as it
        // walks, uploaded a chunk a frame into the buffer not being drawn, then swapped in (they are the same splats
        // where the old and new overlap, so nothing changes but at the far edge, where the coarse set takes over)
        public float shellReach = 12.5f, shellMove = 2.5f;
        public Vector2 nearFade = new Vector2(7f, 10.5f);   // fine splats out to y round the ant, giving way to coarse from x
        Transform ant;
        readonly ComputeBuffer[] shellBuffers = new ComputeBuffer[2];
        readonly int[] shellCounts = new int[2];
        int shellFront, uploading = -1, uploadedSoFar;
        bool shellPending;
        Vector2 shellAt;
        System.Threading.Tasks.Task shellTask;
        readonly List<SolidPoint> shellWork = new List<SolidPoint>(1 << 21);
        readonly MaterialPropertyBlock shellBlock = new MaterialPropertyBlock();
        const int UploadChunk = 262144;

        World world;
        ParticleLook look;
        Material mat;
        ComputeBuffer buffer;
        int count;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        uint rs = 0x9E3779B9u;

        public int Count => count;

        public void Init(World w, ParticleLook particles, Material material, Transform player, LabNest lab = null)
        {
            world = w;
            ant = player;
            look = particles;
            mat = material;
            nest = lab;
            theme = NestTheme.Current ?? NestTheme.Cyan;
            Owned.Clear();
            if (mat == null || world == null || look == null || !look.HasMaps) { enabled = false; return; }
            var splats = new List<SolidPoint>(1 << 21);
            var pen = new ThingPen(splats) { PerArea = perArea, Keep = 0.3f };   // the palette over each thing's own colours
            float t0 = Time.realtimeSinceStartup;
            int heart = Heart(pen);
            int things = Place(pen), rootsMade = Roots(pen), thingSplats = splats.Count;
            // the plants too, as solid splats, when the particle look draws them so
            if (look.solidPlants) look.BuildSolidPlants(splats);
            splats.AddRange(look.extraSolids);   // the vines round the guiding beams
            look.glassLamps.AddRange(ownLamps);  // and the lights inside this nest's glassy things
            GlowingPlants();
            if (theme.things.Length > 0 || heart > 0)
                Debug.Log($"[AntGarden] nest's own things ({theme.id}): {own} of {things}, heart {heart} splats, {ownLamps.Count} lamps, {touchables} an antenna can light");
            count = splats.Count;
            if (count == 0) { enabled = false; return; }
            buffer = new ComputeBuffer(count, 24);
            buffer.SetData(splats);
            if (look.solidPlants && ant != null) ShellNow();
            Debug.Log($"[AntGarden] solid things: {things} things and {rootsMade} roots, {thingSplats} splats; {count} with the plants ({Time.realtimeSinceStartup - t0:F2}s)");
        }

        float R()
        {
            rs ^= rs << 13;
            rs ^= rs >> 17;
            rs ^= rs << 5;
            return (rs & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Places the things: patches of litter, of damp growth and of stones along the ways, never on them.</summary>
        int Place(ThingPen pen)
        {
            var occupied = new CircleGrid(2f);
            int made = 0;
            // one thing at (x, z) if there is room: gap clear of the way, clear of the plants and props and of each other,
            // and on fairly flat floor
            int[] why = new int[5];   // the way pass: why the spots tried were refused (way, reserved, colliders, others, slope)
            // (lane: it may lie in the way's lane, which is kept clear of plants: a flat mark in the floor the ant walks over)
            bool Put(Kind kind, float x, float z, float size, float gap, bool lane = false)
            {
                float r = Footprint(kind, size);
                if (look.WayDistance(x, z) < gap) { why[0]++; return false; }
                if (!lane && world.reserved.Overlaps(x, z, r * 0.8f)) { why[1]++; return false; }
                if (world.colliders.Overlaps(x, z, r * 0.6f)) { why[2]++; return false; }
                if (occupied.Overlaps(x, z, r * 0.85f)) { why[3]++; return false; }
                float h0 = look.HeightAt(x, z), lo = h0, hi = h0;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.0472f, hh = look.HeightAt(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r);
                    lo = Mathf.Min(lo, hh);
                    hi = Mathf.Max(hi, hh);
                }
                if (hi - lo > Mathf.Max(0.12f, r * 0.45f)) { why[4]++; return false; }
                occupied.Add(new Circle(x, z, r, 1f));
                Make(pen, kind, x, z, size, (uint)made * 2654435761u ^ 0x5bd1e995u);
                Block(kind, x, z, size);
                if (Own(kind)) { own++; Lit(kind, x, z, size, r); }
                made++;
                return true;
            }
            bool Floor(float x, float z) => world.HexDist(x, z) <= -1.5f && world.Dist(x, z) <= -0.15f && look.LayerAt(x, z).r >= 0.55f
                && float.IsInfinity(world.Ceiling(x, z));   // on the canyon floor, not under a tunnel roof
            // this nest's own thing that also lines the way (the kiln's embers in the floor's cracks): one every so often,
            // on alternate sides, just off the way, where the ant passes close enough to touch it
            if (nest?.Track != null && !string.IsNullOrEmpty(theme.wayThing) && System.Enum.TryParse(theme.wayThing, out Kind wk))
            {
                List<Vector2> t = nest.Track;
                float along = 0, next = theme.wayEvery * 0.6f;
                int side = 1, spots = 0, offFloor = 0, before = made;
                for (int i = 1; i < t.Count; i++)
                {
                    float seg = (t[i] - t[i - 1]).magnitude;
                    along += seg;
                    if (along < next || seg < 1e-4f) continue;
                    next = along + theme.wayEvery * (0.75f + 0.5f * R());
                    side = -side;
                    Vector2 dir = (t[i] - t[i - 1]) / seg, nrm = new Vector2(-dir.y, dir.x) * side;
                    float size = Size(wk) * 0.5f;
                    spots++;
                    bool done = false;
                    for (int tryside = 0; tryside < 2 && !done; tryside++, nrm = -nrm)   // this side, else the other
                        for (float off = 1.0f; off < 2.6f && !done; off += 0.2f)
                        {
                            Vector2 c = t[i] + nrm * off;
                            if (!Floor(c.x, c.y)) { offFloor++; continue; }
                            done = Put(wk, c.x, c.y, size, 0.6f, true);
                        }
                }
                Debug.Log($"[AntGarden] {theme.wayThing} along the way: {made - before} of {spots} (refused: off the floor {offFloor}, the way {why[0]}, its lane {why[1]}, props {why[2]}, each other {why[3]}, slope {why[4]})");
            }
            for (int tries = 0; tries < maxThings * 120 && made < maxThings; tries++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, R()), z = Mathf.Lerp(world.minZ, world.maxZ, R());
                if (!Floor(x, z)) continue;
                float focus = look.Focus(x, z);
                if (R() > Mathf.Lerp(farDensity, nearDensity, focus) / nearDensity) continue;
                // which patch: litter, damp growth or stones (each in its own broad patches)
                float litter = Noise(x * 0.12f + 3.1f, z * 0.12f - 7.7f), damp = Noise(x * 0.1f - 11.3f, z * 0.1f + 4.9f), stony = Noise(x * 0.14f + 21.7f, z * 0.14f + 13.1f);
                float wl = litter * litter * 1.3f + 0.12f, wd = (damp * damp * 1.2f + 0.1f) * theme.dampScale, ws = stony * stony * stony * 1.1f + 0.05f;
                float pick = R() * (wl + wd + ws);
                Kind kind = pick < wl ? Pick(KLitter, WLitter) : pick < wl + wd ? Pick(KDamp, WDamp) : Pick(KStony, WStony);
                if (theme.things.Length > 0 && R() < theme.ownShare) kind = PickOwn();   // one of this nest's own
                else if (theme.noKinds.Length > 0 && System.Array.IndexOf(theme.noKinds, kind.ToString()) >= 0) kind = R() < 0.6f ? Kind.Leaf : Kind.Seed;
                float size = Size(kind);
                Put(kind, x, z, size, Footprint(kind, size) + 0.7f);
            }
            return made;
        }

        static readonly Kind[] KLitter = { Kind.Leaf, Kind.Twig, Kind.Petal, Kind.Seed, Kind.Fern, Kind.Feather, Kind.Blossom, Kind.Pebbles };
        static readonly float[] WLitter = { 40, 16, 8, 12, 10, 4, 3, 5 };
        static readonly Kind[] KDamp = { Kind.Moss, Kind.Clover, Kind.Mushrooms, Kind.Seedling, Kind.Lichen, Kind.Pebbles, Kind.Shell, Kind.Leaf };
        static readonly float[] WDamp = { 30, 18, 14, 12, 10, 5, 5, 8 };
        static readonly Kind[] KStony = { Kind.Pebbles, Kind.Crystals, Kind.Lichen, Kind.Shell, Kind.Seed, Kind.Twig };
        static readonly float[] WStony = { 34, 20, 20, 10, 10, 6 };

        int own;

        /// <summary>One of the nest's own kinds of thing, by their weights.</summary>
        Kind PickOwn()
        {
            float sum = 0;
            foreach (NestTheme.Thing t in theme.things) sum += t.weight;
            float r = R() * sum;
            foreach (NestTheme.Thing t in theme.things)
                if ((r -= t.weight) < 0) return (Kind)System.Enum.Parse(typeof(Kind), t.kind);
            return (Kind)System.Enum.Parse(typeof(Kind), theme.things[theme.things.Length - 1].kind);
        }

        Kind Pick(Kind[] kinds, float[] weights)
        {
            float sum = 0;
            foreach (float w in weights) sum += w;
            float r = R() * sum;
            for (int i = 0; i < kinds.Length; i++) if ((r -= weights[i]) < 0) return kinds[i];
            return kinds[kinds.Length - 1];
        }

        /// <summary>A thing's size at ant scale (the ant is 1 long): its length, diameter or height.</summary>
        float Size(Kind k)
        {
            float u = R();
            switch (k)
            {
                case Kind.Leaf: return 1.6f + 2.2f * u;
                case Kind.Twig: return 2f + 3f * u;
                case Kind.Petal: return 0.8f + 1.2f * u;
                case Kind.Seed: return 0.45f + 0.45f * u;
                case Kind.Fern: return 2f + 2f * u;
                case Kind.Feather: return 1.6f + 1.8f * u;
                case Kind.Blossom: return 1.1f + 1.1f * u;
                case Kind.Moss: return 0.7f + 1.3f * u;
                case Kind.Clover: return 0.6f + 0.8f * u;
                case Kind.Mushrooms: return 0.55f + 1.2f * u;
                case Kind.Seedling: return 0.9f + 1.2f * u;
                case Kind.Lichen: return 0.6f + 0.9f * u;
                case Kind.Pebbles: return 0.3f + 0.65f * u;
                case Kind.Crystals: return 0.18f + 0.3f * u;
                case Kind.GoldBalls: return 0.8f + 1.4f * u;
                case Kind.ResinDrops: return 0.4f + 0.6f * u;
                case Kind.WaxComb: return 1.0f + 1.3f * u;
                case Kind.AmberShards: return 0.7f + 1.2f * u;
                case Kind.EmberCracks: return 1.2f + 1.8f * u;
                case Kind.ClayChimney: return 0.8f + 1.5f * u;
                case Kind.HexCells: return 0.9f + 1.0f * u;
                case Kind.PotShards: return 0.5f + 0.8f * u;
                default: return 0.6f + 0.7f * u;   // shell
            }
        }

        static float Footprint(Kind k, float size)
        {
            switch (k)
            {
                case Kind.Clover: return size * 1.05f;
                case Kind.Mushrooms: return size * 0.45f;
                case Kind.Seedling: return size * 0.38f;
                case Kind.Pebbles: return size * 0.9f;
                case Kind.Crystals: return size * 1.3f;
                case Kind.GoldBalls: return size * 0.4f;
                case Kind.ResinDrops: return size * 0.6f;
                case Kind.WaxComb: return size * 0.55f;
                case Kind.AmberShards: return size * 0.45f;
                case Kind.EmberCracks: return size * 0.55f;
                case Kind.ClayChimney: return size * 0.3f;
                case Kind.HexCells: return size * 0.55f;
                case Kind.PotShards: return size * 0.9f;
                default: return size * 0.52f;
            }
        }

        /// <summary>Tall things block the ants (the flat ones are walked over).</summary>
        void Block(Kind k, float x, float z, float size)
        {
            float r = 0, h = 0;
            switch (k)
            {
                case Kind.Mushrooms: r = size * 0.22f; h = size; break;
                case Kind.Seedling: r = size * 0.1f; h = size; break;
                case Kind.Shell: r = size * 0.42f; h = size * 0.8f; break;
                case Kind.Pebbles: if (size > 0.5f) { r = size * 0.45f; h = size * 0.5f; } break;
                case Kind.Moss: if (size > 1.3f) { r = size * 0.35f; h = size * 0.35f; } break;
                case Kind.GoldBalls: r = size * 0.12f; h = size; break;
                case Kind.AmberShards: r = size * 0.3f; h = size; break;
                case Kind.ClayChimney: r = size * 0.24f; h = size; break;
                case Kind.HexCells: r = size * 0.45f; h = size * 0.4f; break;
                case Kind.ResinDrops: if (size > 0.7f) { r = size * 0.3f; h = size * 0.4f; } break;
            }
            if (r > 0) world.colliders.Add(new Circle(x, z, r, h));
        }

        /// <summary>The nest's tints (ParticleLook.bodyPalette) each kind of thing takes; a few things take a warm one.</summary>
        static readonly int[][] Tints =
        {
            new[] { 7, 1, 0, 5 },   // leaf: straw, moss, sage, deep sage-teal
            new[] { 5, 0, 3 },      // twig
            new[] { 4, 7 },         // petal: bone, straw
            new[] { 7, 4 },         // seed
            new[] { 1, 0, 6 },      // fern
            new[] { 4, 3 },         // feather
            new[] { 4, 6 },         // blossom
            new[] { 1, 6 },         // moss
            new[] { 1, 2 },         // clover
            new[] { 4, 0 },         // mushrooms
            new[] { 6, 1 },         // seedling
            new[] { 3, 4, 2 },      // lichen
            new[] { 3, 5, 4 },      // pebbles
            new[] { 3, 4 },         // crystals
            new[] { 4, 7 },         // shell
        };

        Color TintFor(int[] choices)
        {
            Color[] pal = look.bodyPalette;
            if (pal == null || pal.Length < 8) return Color.clear;
            int i = choices[(int)(R() * choices.Length) % choices.Length];
            return pal[i].linear;
        }

        void Make(ThingPen pen, Kind k, float x, float z, float size, uint seed)
        {
            pen.Tint = Own(k) ? Color.clear : TintFor(Tints[(int)k]);   // a nest's own things keep their own colours
            Vector3 n = world.Normal(x, z), ta = Vector3.Cross(n, Vector3.forward).normalized;
            if (ta.sqrMagnitude < 0.5f) ta = Vector3.Cross(n, Vector3.right).normalized;
            Vector3 tb = Vector3.Cross(ta, n);
            var p = new Vector3(x, look.HeightAt(x, z), z);
            int from = pen.into.Count;
            pen.Begin(seed);
            switch (k)
            {
                case Kind.Leaf: NestThingsLitter.FallenLeaf(pen, p, n, ta, tb, size); break;
                case Kind.Twig: NestThingsLitter.Twig(pen, p, n, ta, tb, size); break;
                case Kind.Petal: NestThingsLitter.Petal(pen, p, n, ta, tb, size); break;
                case Kind.Seed: NestThingsLitter.Seed(pen, p, n, ta, tb, size); break;
                case Kind.Fern: NestThingsLitter.FernFrond(pen, p, n, ta, tb, size); break;
                case Kind.Feather: NestThingsLitter.Feather(pen, p, n, ta, tb, size); break;
                case Kind.Blossom: NestThingsLitter.FallenBlossom(pen, p, n, ta, tb, size); break;
                case Kind.Moss: NestThingsGrowth.MossCushion(pen, p, n, ta, tb, size); break;
                case Kind.Clover: NestThingsGrowth.CloverPatch(pen, p, n, ta, tb, size); break;
                case Kind.Mushrooms: NestThingsGrowth.Mushrooms(pen, p, n, ta, tb, size); break;
                case Kind.Seedling: NestThingsGrowth.Seedling(pen, p, n, ta, tb, size); break;
                case Kind.Lichen: NestThingsGrowth.LichenCrust(pen, p, n, ta, tb, size); break;
                case Kind.Pebbles: NestThingsGrowth.Pebbles(pen, p, n, ta, tb, size); break;
                case Kind.Crystals: NestThingsGrowth.Crystals(pen, p, n, ta, tb, size); break;
                case Kind.GoldBalls: NestThingsThemed.GoldBalls(pen, p, Vector3.up, Vector3.right, Vector3.forward, size); break;   // upright
                case Kind.ResinDrops: NestThingsThemed.ResinDrops(pen, p, n, ta, tb, size); break;
                case Kind.WaxComb: NestThingsThemed.WaxComb(pen, p, n, ta, tb, size); break;
                case Kind.AmberShards: NestThingsThemed.AmberShards(pen, p, n, ta, tb, size); break;
                case Kind.EmberCracks: NestThingsThemed.EmberCracks(pen, p, n, ta, tb, size); break;
                case Kind.ClayChimney: NestThingsThemed.ClayChimney(pen, p, Vector3.up, Vector3.right, Vector3.forward, size); break;
                case Kind.HexCells: NestThingsThemed.HexCells(pen, p, n, ta, tb, size); break;
                case Kind.PotShards: NestThingsThemed.PotShards(pen, p, n, ta, tb, size); break;
                default: NestThingsGrowth.SnailShell(pen, p, n, ta, tb, size); break;
            }
            // the kiln's clay buildings light only where they glow (their holes, mouths, cells), not whole
            if (theme.buildingsGlowInParts && (k == Kind.ClayChimney || k == Kind.HexCells))
                for (int i = from; i < pen.into.Count; i++) pen.into[i] = ThingPen.Building(pen.into[i]);
            // dew: a few beads on what holds it (leaves, moss, clover, lichen, petals, fronds), on parts that face up
            if (k == Kind.Leaf || k == Kind.Moss || k == Kind.Clover || k == Kind.Lichen || k == Kind.Petal || k == Kind.Fern || k == Kind.Seedling)
            {
                int beads = (int)(R() * 4.5f), end = pen.into.Count;
                for (int b = 0, tries = 0; b < beads && tries < 30 && end > from; tries++)
                {
                    SolidPoint sp = pen.into[from + (int)(R() * (end - from)) % (end - from)];
                    Vector3 nn = ThingPen.DecodeNormal(sp.nrm);
                    if (nn.y < 0.6f) continue;
                    float r = 0.018f + 0.022f * R();
                    pen.into.Add(ThingPen.Splat(sp.pos + nn * (Mathf.Abs(sp.radius) * 0.5f + r * 0.6f), Vector3.up, (NestTheme.Current ?? NestTheme.Cyan).dew, r, 0.4f));
                    b++;
                }
            }
            // flat things drape over the floor: each splat keeps its height above the plane through p, over the floor
            // under it (so a big leaf follows the ground's rise and fall instead of floating or sinking)
            if (k == Kind.Leaf || k == Kind.Petal || k == Kind.Fern || k == Kind.Feather || k == Kind.Blossom || k == Kind.Lichen
                || k == Kind.WaxComb || k == Kind.EmberCracks || k == Kind.PotShards)
                for (int i = from; i < pen.into.Count; i++)
                {
                    SolidPoint sp = pen.into[i];
                    Vector3 d = sp.pos - p;
                    float above = Vector3.Dot(d, n);
                    sp.pos.y = look.HeightAt(sp.pos.x, sp.pos.z) + above;
                    pen.into[i] = sp;
                }
        }

        /// <summary>A nest's own thing that glows: an antenna can light it (a touchable glow with no mesh of its own),
        /// and a glassy one has a light inside it (GlassLamps).</summary>
        void Lit(Kind k, float x, float z, float size, float footprint)
        {
            float height = k == Kind.WaxComb || k == Kind.EmberCracks || k == Kind.PotShards ? 0.25f : k == Kind.HexCells ? size * 0.4f : k == Kind.ResinDrops ? size * 0.6f : size;
            Owned.Add((k.ToString(), new Vector2(x, z), height));
            if (world.glow == null || k == Kind.PotShards) return;
            float y = look.HeightAt(x, z);
            world.glow.AddTouchable(new Vector3(x, y, z), footprint, height, theme.glow[0]);
            touchables++;
            if (k == Kind.ResinDrops) ownLamps.Add((new Vector3(x, y + size * 0.18f, z), size * 0.32f, theme.glow[0]));
            if (k == Kind.AmberShards) ownLamps.Add((new Vector3(x, y + size * 0.3f, z), size * 0.28f, theme.glow[0]));
            if (k == Kind.ClayChimney) ownLamps.Add((new Vector3(x, y + size * 0.97f, z), size * 0.1f, theme.glow[2]));
        }

        /// <summary>The thing at the nest's heart (NestTheme.heart): the amber nest's big cluster of amber shards, the
        /// temple's amber shrine with the ancestor inside. It stands in the middle of the heart, blocks the ants, can be
        /// lit by an antenna and has its light inside. Returns its splats.</summary>
        int Heart(ThingPen pen)
        {
            if (nest == null || string.IsNullOrEmpty(theme.heart)) return 0;
            Vector2 h = nest.Heart;
            float y = look.HeightAt(h.x, h.y);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                y = Mathf.Min(y, look.HeightAt(h.x + Mathf.Cos(a) * 1.1f, h.y + Mathf.Sin(a) * 1.1f));
            }
            var p = new Vector3(h.x, y - 0.05f, h.y);
            int from = pen.into.Count;
            pen.Tint = Color.clear;
            pen.Begin(0x9e3779b9u);
            float size;
            if (theme.heart == "shrine")
            {
                size = 3.4f;
                NestThingsThemed.Shrine(pen, p, Vector3.up, Vector3.right, Vector3.forward, size, AntPoints("Ant_003"), 1700f);
                world.colliders.Add(new Circle(h.x, h.y, size * 0.45f, size));
                ownLamps.Add((p + Vector3.up * (size * 0.42f), size * 0.14f, theme.glow[0]));   // a small core: the block, not a ball of light
            }
            else
            {
                size = 2.8f;
                NestThingsThemed.HeartShards(pen, p, Vector3.up, Vector3.right, Vector3.forward, size, 1600f);
                world.colliders.Add(new Circle(h.x, h.y, size * 0.32f, size));
                ownLamps.Add((p + Vector3.up * (size * 0.3f), size * 0.3f, theme.glow[0]));
            }
            world.glow?.AddTouchable(p, size * 0.4f, size, theme.glow[0]);
            Owned.Add(("Heart", h, size));
            touchables++;
            world.reserved.Add(new Circle(h.x, h.y, size * 0.6f, size));
            return pen.into.Count - from;
        }

        /// <summary>An ant model's points (its particle file, AGA2), centred, its body length 1 along +z.</summary>
        static Vector3[] AntPoints(string id)
        {
            var asset = Resources.Load<TextAsset>("AntGarden/Points/ant_" + id);
            if (asset == null) return null;
            using var r = new System.IO.BinaryReader(new System.IO.MemoryStream(asset.bytes));
            if (r.ReadInt32() != 0x32414741) return null;   // "AGA2"
            int bones = r.ReadInt32();
            for (int i = 0; i < bones; i++) { r.ReadString(); for (int j = 0; j < 16; j++) r.ReadSingle(); }
            int n = r.ReadInt32();
            var pts = new Vector3[n];
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int i = 0; i < n; i++)
            {
                pts[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                r.ReadSingle(); r.ReadSingle(); r.ReadSingle();   // normal
                r.ReadBytes(4); r.ReadUInt16(); r.ReadUInt16(); r.ReadSingle();   // colour, bones, weight
                lo = Vector3.Min(lo, pts[i]);
                hi = Vector3.Max(hi, pts[i]);
            }
            Vector3 c = (lo + hi) * 0.5f, ext = hi - lo;
            float len = Mathf.Max(1e-4f, Mathf.Max(ext.x, ext.z));
            bool alongX = ext.x > ext.z;   // the body's length along x: turn it to run along z
            for (int i = 0; i < n; i++)
            {
                Vector3 d = (pts[i] - c) / len;
                pts[i] = alongX ? new Vector3(-d.z, d.y, d.x) : d;
            }
            return pts;
        }

        /// <summary>The plants that glow a little only in this nest (NestTheme.glowParts) can be lit by an antenna too.</summary>
        void GlowingPlants()
        {
            if (world.glow == null || theme.glowParts.Length == 0) return;
            foreach (PlantInstance pi in world.plantInstances)
            {
                if (pi.t == null || pi.plant == null || pi.far) continue;
                foreach (NestTheme.GlowPart part in theme.glowParts)
                {
                    if (part.plant != pi.plant.name || System.Array.IndexOf(theme.touchParts, part.plant) < 0) continue;
                    float sc = pi.t.lossyScale.y;
                    world.glow.AddTouchable(pi.t.position, pi.plant.footprintRadius * sc, sc, theme.glow[0]);
                    touchables++;
                    break;
                }
            }
        }

        /// <summary>
        /// Roots creeping out from the foot of the walls across the floor: each a tapering tube wandering over the
        /// ground (its upper half drawn), bark brown-grey, with a few fine rootlets and pale tips; they keep off the way.
        /// </summary>
        int Roots(ThingPen pen)
        {
            int made = 0;
            for (int tries = 0; tries < roots * 200 && made < roots; tries++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, R()), z = Mathf.Lerp(world.minZ, world.maxZ, R());
                if (world.HexDist(x, z) > -1.5f || !float.IsInfinity(world.Ceiling(x, z))) continue;
                Color lw = look.LayerAt(x, z);
                if (lw.g < 0.45f || lw.r < 0.15f) continue;   // at the foot of a wall
                if (R() > 0.15f + 0.85f * look.Focus(x, z) || look.WayDistance(x, z) < 1.2f) continue;
                pen.Begin((uint)made * 2246822519u ^ 0x27d4eb2fu);
                pen.Tint = TintFor(RootTints);
                // into the canyon: down the slope of the distance field
                float e = 0.3f, gx = world.Dist(x + e, z) - world.Dist(x - e, z), gz = world.Dist(x, z + e) - world.Dist(x, z - e);
                float dir = Mathf.Atan2(-gz, -gx) + (pen.R() - 0.5f) * 1.4f;
                Root(pen, x, z, dir, 2.2f + 3.8f * pen.R(), 0.07f + 0.08f * pen.R(), 0);
                made++;
            }
            return made;
        }

        static readonly int[] RootTints = { 5, 0 };

        void Root(ThingPen pen, float x, float z, float dir, float length, float radius, int depth)
        {
            float step = Mathf.Max(0.012f, radius * 0.5f);
            int steps = Mathf.Max(4, Mathf.RoundToInt(length / step));
            float curl = (pen.R() - 0.5f) * 0.05f;
            Color bark = Color.Lerp(new Color(0.14f, 0.1f, 0.07f), new Color(0.11f, 0.12f, 0.13f), pen.R()), tip = new Color(0.42f, 0.36f, 0.27f);
            for (int i = 0; i < steps; i++)
            {
                float u = i / (float)steps;
                dir += curl + (pen.R() - 0.5f) * 0.12f;
                float cs = Mathf.Cos(dir), sn = Mathf.Sin(dir);
                float nx = x + cs * step, nz = z + sn * step;
                if (look.WayDistance(nx, nz) < 0.7f || world.HexDist(nx, nz) > -0.8f) break;
                x = nx;
                z = nz;
                float r = radius * (1 - 0.85f * u) + 0.008f;
                float y = look.HeightAt(x, z) + r * 0.8f;
                var along = new Vector3(cs, 0, sn);
                var side = new Vector3(-sn, 0, cs);
                Color c = Color.Lerp(bark, tip, Mathf.Clamp01((u - 0.82f) / 0.18f));
                // its upper half, ridged along its length
                int around = 3 + Mathf.RoundToInt(r * 60f);
                for (int k = 0; k < around; k++)
                {
                    float a = (k + pen.R()) / around * Mathf.PI;
                    Vector3 nrm = side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
                    float ridge = 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(a * 7f + i * 0.35f));
                    pen.Put(new Vector3(x, y, z) + nrm * r + along * ((pen.R() - 0.5f) * step), nrm, c * ridge, Mathf.Max(pen.Radius(0.7f), r * 0.55f));
                }
                if (depth < 2 && pen.R() < 0.025f)
                    Root(pen, x, z, dir + (pen.R() < 0.5f ? 1f : -1f) * (0.6f + 0.5f * pen.R()), length * (0.2f + 0.3f * pen.R()) * (1 - u), r * 0.55f, depth + 1);
            }
        }

        readonly Vector4[] flarePos = new Vector4[16], flareInfo = new Vector4[16];

        /// <summary>The fine shell made at once round the ant (at the start, and when it has jumped far).</summary>
        void ShellNow()
        {
            shellAt = new Vector2(ant.position.x, ant.position.z);
            look.SolidShell(shellAt, shellReach, shellWork);
            int back = 1 - shellFront;
            Ensure(back, shellWork.Count);
            if (shellWork.Count > 0) shellBuffers[back].SetData(shellWork, 0, 0, shellWork.Count);
            shellCounts[back] = shellWork.Count;
            shellFront = back;
            ShellsMade++;
            ShellsShown++;
            Debug.Log($"[AntGarden] solid shell: {shellWork.Count} fine plant splats round ({shellAt.x:F0}, {shellAt.y:F0})");
        }

        void Ensure(int which, int n)
        {
            if (shellBuffers[which] != null && shellBuffers[which].count >= n) return;
            shellBuffers[which]?.Release();
            shellBuffers[which] = new ComputeBuffer(Mathf.Max(1024, Mathf.NextPowerOfTwo(n)), 24);
        }

        /// <summary>Once the ant has walked shellMove from where the shell was made, it is made again on a worker thread,
        /// then uploaded a chunk a frame into the buffer not being drawn and swapped in. A jump (a cut) makes it at once.</summary>
        void UpdateShell()
        {
            var at = new Vector2(ant.position.x, ant.position.z);
            if (shellTask == null && !shellPending && uploading < 0 && (at - shellAt).sqrMagnitude > 64f) { ShellNow(); return; }
            if (shellTask != null && shellTask.IsCompleted)
            {
                if (shellTask.IsFaulted) Debug.LogException(shellTask.Exception);
                else shellPending = true;
                shellTask = null;
            }
            if (shellPending && uploading < 0)
            {
                shellPending = false;
                uploading = 1 - shellFront;
                uploadedSoFar = 0;
                Ensure(uploading, shellWork.Count);
            }
            if (uploading >= 0)
            {
                int n = Mathf.Min(UploadChunk, shellWork.Count - uploadedSoFar);
                if (n > 0) shellBuffers[uploading].SetData(shellWork, uploadedSoFar, uploadedSoFar, n);
                uploadedSoFar += n;
                if (uploadedSoFar < shellWork.Count) return;
                shellCounts[uploading] = shellWork.Count;
                shellFront = uploading;
                uploading = -1;
                ShellsShown++;
                return;
            }
            if (shellTask != null || (at - shellAt).sqrMagnitude < shellMove * shellMove) return;
            shellAt = at;
            shellTask = System.Threading.Tasks.Task.Run(() => look.SolidShell(at, shellReach, shellWork));
            ShellsMade++;
        }

        void Update()
        {
            if (count == 0 || buffer == null) return;
            if (look.solidPlants && ant != null) UpdateShell();
            // the plants an antenna has lit flare among the splats
            int nf = world.glow != null ? world.glow.Flares(flarePos, flareInfo) : 0;
            block.SetFloat("_FlareCount", nf);
            block.SetVectorArray("_Flares", flarePos);
            block.SetVectorArray("_FlareInfo", flareInfo);
            block.SetBuffer("_Solid", buffer);
            block.SetFloat("_Gain", gain);
            block.SetFloat("_GlowGain", 2f);
            NestTheme theme = NestTheme.Current ?? NestTheme.Cyan;
            block.SetVector("_RimColor", theme.rim);
            block.SetVector("_GlintColor", theme.glint);
            block.SetVector("_Lod", lod);
            block.SetVector("_MoonDir", new Vector4(0.32f, 0.9f, 0.28f, 0).normalized);
            block.SetVector("_MoonColor", theme.moon);
            block.SetVector("_FillColor", theme.fill);
            block.SetVector("_NearFade", look.solidPlants ? (Vector4)nearFade : Vector4.zero);
            block.SetFloat("_Shell", 0f);
            var bounds = new Bounds(new Vector3((world.minX + world.maxX) / 2, 0, (world.minZ + world.maxZ) / 2),
                new Vector3(world.maxX - world.minX + 40, 200, world.maxZ - world.minZ + 40));
            Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, count * 3, 1, null, block, ShadowCastingMode.Off, false, 0);
            if (shellCounts[shellFront] > 0)
            {
                shellBlock.SetFloat("_FlareCount", nf);
                shellBlock.SetVectorArray("_Flares", flarePos);
                shellBlock.SetVectorArray("_FlareInfo", flareInfo);
                shellBlock.SetBuffer("_Solid", shellBuffers[shellFront]);
                shellBlock.SetFloat("_Gain", gain);
                shellBlock.SetFloat("_GlowGain", 2f);
                shellBlock.SetVector("_RimColor", theme.rim);
                shellBlock.SetVector("_GlintColor", theme.glint);
                shellBlock.SetVector("_Lod", lod);
                shellBlock.SetVector("_MoonDir", new Vector4(0.32f, 0.9f, 0.28f, 0).normalized);
                shellBlock.SetVector("_MoonColor", theme.moon);
                shellBlock.SetVector("_FillColor", theme.fill);
                shellBlock.SetVector("_NearFade", nearFade);
                shellBlock.SetFloat("_Shell", 1f);
                Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, shellCounts[shellFront] * 3, 1, null, shellBlock, ShadowCastingMode.Off, false, 0);
            }
        }

        void OnDestroy()
        {
            buffer?.Release();
            foreach (ComputeBuffer b in shellBuffers) b?.Release();
        }

        static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
