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
        /// <summary>Each of a thing's splats is drawn as this many finer ones right in front of the camera (Densify), and
        /// the vines are made this much denser (GuideThreads.VineSplats): there their grain is as fine as the plants'
        /// (ParticleLook.solidNear), from a few units off it is as before.</summary>
        public static int Fine = 4;
        public float gain = 0.62f;          // night: the things are seen by moonlight, darker than the glowing plants
        public Vector4 lod = new Vector4(7f, 30f, 0.28f, 0);    // all splats within x units of the camera, thinned to z of them by y

        enum Kind
        {
            Leaf, Twig, Petal, Seed, Fern, Feather, Blossom, Moss, Clover, Mushrooms, Seedling, Lichen, Pebbles, Crystals, Shell,
            // a nest's own (NestTheme.things)
            GoldBalls, ResinDrops, WaxComb, AmberShards, EmberCracks, ClayChimney, HexCells, PotShards,
            HoneyDrops, LeafSail, DewDrop, Gear, Puddle, Wire, Sporophyte, StarMoss,
            SugarCrystals, PollenPellets, HoneyDrip, FungusTuft, BittenLeaf, LeafBits, Gleanings, SoilPatch,
            AmberNodules, ResinRun,
            CharTwigs, CharBalls, AshStones, AshDrift,   // the kiln's (after the fire)
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
        public float shellReach = 12.5f, shellMove = 2f;
        Vector2 shellCam;   // where the camera was when the shell was made (it is remade when either has moved shellMove)
        public Vector2 nearFade = new Vector2(7f, 10.5f);   // fine splats out to y round the ant, giving way to coarse from x
        Transform ant;
        readonly ComputeBuffer[] shellBuffers = new ComputeBuffer[2];
        readonly int[] shellCounts = new int[2];
        int shellFront, uploading = -1, uploadedSoFar;
        bool shellPending;
        Vector2 shellAt;
        System.Threading.Tasks.Task shellTask;
        // (room for the largest shell up front: growing it mid-walk, on the GPU and here, cost a frame of 75-100 ms; a
        // big nest's shell reaches about 4.7 million splats with its big plants split near the camera)
        const int ShellRoom = 1 << 23;
        SolidPoint[] shellWork = new SolidPoint[ShellRoom];
        int shellWorkCount;
        long shellMs;
        /// <summary>The nest's solid things and plants (one per nest), for the checks.</summary>
        public static SolidThings Instance { get; private set; }
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
            Instance = this;
            look = particles;
            mat = material;
            nest = lab;
            theme = NestTheme.Current ?? NestTheme.Cyan;
            Owned.Clear();
            if (mat == null || world == null || look == null || !look.HasMaps) { enabled = false; return; }
            var made = new List<SolidPoint>(1 << 20);
            var pen = new ThingPen(made) { PerArea = perArea, Keep = (NestTheme.Current ?? NestTheme.Cyan).thingKeep };   // the palette over each thing's own colours
            float t0 = Time.realtimeSinceStartup;
            int heart = Heart(pen) + Landmarks(pen);
            int things = Place(pen), rootsMade = Roots(pen), thingSplats = made.Count * Fine;
            var splats = new List<SolidPoint>(thingSplats + (1 << 21));
            Densify(made, splats);
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
            if (look.solidPlants && ant != null)
            {
                Ensure(0, ShellRoom);
                Ensure(1, ShellRoom);
                ShellNow();
            }
            Debug.Log($"[AntGarden] solid things: {things} things and {rootsMade} roots, {thingSplats} splats; {count} with the plants ({Time.realtimeSinceStartup - t0:F2}s)");
        }

        float R()
        {
            rs ^= rs << 13;
            rs ^= rs >> 17;
            rs ^= rs << 5;
            return (rs & 0xFFFFFF) / 16777216f;
        }

        /// <summary>
        /// The things up close: each of their splats as Fine smaller ones spread over its disc (in its surface's plane),
        /// each a little varied in tone and facing, the first near its middle. Ranked (ThingPen.Ranked) so that from a
        /// few units off the shader keeps the first alone, as large as the splat was, and right in front of the camera
        /// all of them: the things' colours and forms are as they were made, their grain as fine there as the plants'.
        /// </summary>
        static void Densify(List<SolidPoint> src, List<SolidPoint> dst)
        {
            uint h = 0x2545F491u;
            float H()
            {
                h ^= h << 13;
                h ^= h >> 17;
                h ^= h << 5;
                return (h & 0xFFFFFF) / 16777216f;
            }
            float shrink = 1.15f / Mathf.Sqrt(Fine);
            foreach (SolidPoint sp in src)
            {
                Vector3 n = ThingPen.UnpackNormal(sp.nrm);
                NestThingsGrowth.Perp(n, out Vector3 u, out Vector3 v);
                uint flag = sp.nrm & (1u << 31);   // a building's
                for (int j = 0; j < Fine; j++)
                {
                    float a = H() * 6.2831853f, d = sp.radius * (j == 0 ? 0.25f * H() : 0.9f * Mathf.Sqrt(H()));
                    Vector3 nj = n + (u * (H() * 2f - 1f) + v * (H() * 2f - 1f)) * 0.15f;
                    var q = new SolidPoint
                    {
                        pos = sp.pos + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * d,
                        nrm = ThingPen.PackNormal(nj) | flag,
                        col = Tone(sp.col, 0.96f + 0.08f * H()),
                        radius = sp.radius * shrink * (0.85f + 0.3f * H()),
                    };
                    dst.Add(ThingPen.Ranked(q, j, Fine));
                }
            }
        }

        /// <summary>A packed colour (gamma bytes) a little lighter or darker, its glow byte kept.</summary>
        static uint Tone(uint col, float k)
        {
            uint Ch(int shift) => (uint)Mathf.Min(255, Mathf.RoundToInt(((col >> shift) & 255u) * k)) << shift;
            return Ch(0) | Ch(8) | Ch(16) | (col & 0xFF000000u);
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
                // (the leafcutters' bitten leaves out of the guiding beams' pools of light: in one, the green leaf itself read
                // as light and its bites were lost)
                if (kind == Kind.BittenLeaf)
                    foreach (Vector2 b in GuideThreads.BeamFeet)
                        if ((b - new Vector2(x, z)).sqrMagnitude < 9f) { why[1]++; return false; }
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
                else if (theme.noKinds.Length > 0 && System.Array.IndexOf(theme.noKinds, kind.ToString()) >= 0)
                {
                    kind = R() < 0.6f ? Kind.Leaf : Kind.Seed;
                    if (kind == Kind.Seed && System.Array.IndexOf(theme.noKinds, "Seed") >= 0) kind = Kind.Leaf;   // (a nest without seeds: a leaf)
                    // (and one without leaves either, the kiln's burnt floor: nothing there, its ash bare; before, every
                    // thing it left out came back as a leaf, pale leaves in front of its temple, k3 content review; as
                    // stones, k5, pale pebbles all over)
                    if (kind == Kind.Leaf && System.Array.IndexOf(theme.noKinds, "Leaf") >= 0) continue;
                }
                float size = Size(kind);
                // (the leafcutters' dropped bits may lie on the way itself, as on their trails; everything else clear of it)
                Put(kind, x, z, size, kind == Kind.LeafBits ? 0.3f : Footprint(kind, size) + 0.7f, kind == Kind.LeafBits);
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
                case Kind.HoneyDrops: return 0.3f + 0.45f * u;
                case Kind.LeafSail: return 0.5f + 0.9f * u;
                case Kind.DewDrop: return 0.4f + 0.8f * u;
                case Kind.Gear: return 0.8f + 1.6f * u;
                case Kind.Puddle: return 0.8f + 1.8f * u;
                case Kind.Wire: return 1.0f + 2.0f * u;
                case Kind.Sporophyte: return 0.8f + 1.6f * u;
                case Kind.StarMoss: return 0.5f + 0.9f * u;
                case Kind.SugarCrystals: return 0.5f + 0.7f * u;
                case Kind.PollenPellets: return 0.4f + 0.6f * u;
                case Kind.HoneyDrip: return 1.5f + 1.1f * u;   // (taller than the ant: smaller, in the game's view it read as a little candle)
                case Kind.FungusTuft: return 0.4f + 0.5f * u;   // (smaller: beside the garden landmark it read as the same thing)
                case Kind.BittenLeaf: return 1.5f + 1.3f * u;   // (from 1.2: the small ones did not read from the way)
                case Kind.LeafBits: return 1.2f + 1.4f * u;
                case Kind.Gleanings: return 1.0f + 0.8f * u;
                case Kind.SoilPatch: return 1.6f + 1.2f * u;
                case Kind.AmberNodules: return 0.6f + 1.0f * u;
                case Kind.ResinRun: return 0.6f + 0.6f * u;   // (from 0.45: the small ones' resin did not read from the way)
                case Kind.CharTwigs: return 1.3f + 1.5f * u;   // (the longest twig of the few)
                case Kind.CharBalls: return 0.8f + 0.8f * u;   // (the cluster's width)
                case Kind.AshStones: return 0.16f + 0.18f * u;   // (small: a third of the ant at most)
                case Kind.AshDrift: return 0.8f + 0.9f * u;   // (its width; 0.5-1.1 in k11, hardly seen in the main views)
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
                case Kind.HoneyDrops: return size * 0.5f;
                case Kind.LeafSail: return size * 0.55f;
                case Kind.DewDrop: return size * 0.5f;
                case Kind.Gear: return size * 0.5f;
                case Kind.Puddle: return size * 0.6f;
                case Kind.Wire: return size * 0.45f;
                case Kind.Sporophyte: return size * 0.12f;
                case Kind.StarMoss: return size * 0.5f;
                case Kind.SugarCrystals: return size * 0.6f;
                case Kind.PollenPellets: return size * 0.5f;
                case Kind.HoneyDrip: return size * 0.45f;
                case Kind.FungusTuft: return size * 0.55f;
                case Kind.BittenLeaf: return size * 0.38f;   // (its mean radius, long and narrow: at its half length a big one found no room near the way)
                case Kind.LeafBits: return size * 0.4f;
                case Kind.Gleanings: return size * 0.5f;
                case Kind.SoilPatch: return size * 0.5f;
                case Kind.AmberNodules: return size * 0.6f;
                case Kind.ResinRun: return size * 0.45f;   // (a stub standing, its runs down its side, a pool at its foot)
                case Kind.CharTwigs: return size * 0.42f;
                case Kind.CharBalls: return size * 0.5f;
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
                case Kind.AmberNodules: if (size > 0.8f) { r = size * 0.32f; h = size * 0.3f; } break;
                case Kind.ResinRun: r = size * 0.2f; h = size * 0.9f; break;
                case Kind.ClayChimney: r = size * 0.24f; h = size; break;
                case Kind.HexCells: r = size * 0.45f; h = size * 0.4f; break;
                case Kind.ResinDrops: if (size > 0.7f) { r = size * 0.3f; h = size * 0.4f; } break;
                case Kind.HoneyDrops: if (size > 0.7f) { r = size * 0.3f; h = size * 0.4f; } break;
                case Kind.LeafSail: r = size * 0.12f; h = size; break;
                case Kind.DewDrop: if (size > 0.6f) { r = size * 0.4f; h = size * 0.8f; } break;
                case Kind.Gear: r = size * 0.2f; h = size * 0.7f; break;
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

        /// <summary>The palette's tints a nest wants for a kind of thing, or for its roots ("Root"), not the table's
        /// (NestTheme.thingTints; null: the table's).</summary>
        static int[] OwnTints(string kind) => NestTheme.Current?.thingTints != null && NestTheme.Current.thingTints.TryGetValue(kind, out int[] own) ? own : null;

        Color TintFor(int[] choices)
        {
            Color[] pal = look.bodyPalette;
            if (pal == null || pal.Length < 8) return Color.clear;
            int i = choices[(int)(R() * choices.Length) % choices.Length];
            return pal[i].linear;
        }

        void Make(ThingPen pen, Kind k, float x, float z, float size, uint seed)
        {
            pen.Tint = Own(k) ? Color.clear : TintFor(OwnTints(k.ToString()) ?? Tints[(int)k]);   // a nest's own things keep their own colours
            if (!Own(k) && theme.thingColours != null && theme.thingColours.TryGetValue(k.ToString(), out Color tc)) pen.Tint = tc;   // or the colour its nest gives the kind (NestTheme.thingColours)
            // a nest's own things twice as dense: they are what it is about, seen close (at the floor's density a small
            // one, a cut leaf, read as a few flecks of colour up close)
            pen.PerArea = Own(k) ? perArea * 2f : perArea;
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
                case Kind.AmberNodules: NestThingsThemed.AmberNodules(pen, p, n, ta, tb, size); break;
                case Kind.ResinRun: NestThingsThemed.ResinRun(pen, p, n, ta, tb, size); break;
                case Kind.CharTwigs: NestThingsThemed.CharTwigs(pen, p, n, ta, tb, size); break;
                case Kind.CharBalls: NestThingsThemed.CharBalls(pen, p, n, ta, tb, size); break;
                case Kind.AshStones: NestThingsThemed.AshStones(pen, p, n, ta, tb, size); break;
                case Kind.AshDrift: NestThingsThemed.AshDrift(pen, p, n, ta, tb, size); break;
                case Kind.EmberCracks: NestThingsThemed.EmberCracks(pen, p, n, ta, tb, size); break;
                case Kind.ClayChimney: NestThingsThemed.ClayChimney(pen, p, Vector3.up, Vector3.right, Vector3.forward, size); break;
                case Kind.HexCells: NestThingsThemed.HexCells(pen, p, n, ta, tb, size); break;
                case Kind.PotShards: NestThingsThemed.PotShards(pen, p, n, ta, tb, size); break;
                case Kind.HoneyDrops: NestThingsThemed.HoneyDrops(pen, p, n, ta, tb, size); break;
                case Kind.LeafSail: NestThingsThemed.LeafSail(pen, p, Vector3.up, Vector3.right, Vector3.forward, size); break;   // upright
                case Kind.DewDrop: NestThingsThemed.DewDrop(pen, p, n, ta, tb, size); break;
                case Kind.Gear: NestThingsThemed.Gear(pen, p, Vector3.up, Vector3.right, Vector3.forward, size); break;   // upright
                case Kind.Puddle: NestThingsThemed.Puddle(pen, p, n, ta, tb, size); break;
                case Kind.Wire: NestThingsThemed.Wire(pen, p, n, ta, tb, size); break;
                case Kind.Sporophyte: NestThingsThemed.Sporophyte(pen, p, Vector3.up, Vector3.right, Vector3.forward, size); break;   // upright
                case Kind.StarMoss: NestThingsThemed.StarMoss(pen, p, n, ta, tb, size); break;
                case Kind.SugarCrystals: NestThingsThemed.SugarCrystals(pen, p, n, ta, tb, size); break;
                case Kind.PollenPellets: NestThingsThemed.PollenPellets(pen, p, n, ta, tb, size); break;
                case Kind.HoneyDrip: NestThingsThemed.HoneyDrip(pen, p, n, ta, tb, size); break;
                case Kind.FungusTuft: NestThingsThemed.FungusTuft(pen, p, n, ta, tb, size); break;
                case Kind.BittenLeaf: NestThingsThemed.BittenLeaf(pen, p, n, ta, tb, size); break;
                case Kind.LeafBits: NestThingsThemed.LeafBits(pen, p, n, ta, tb, size); break;
                case Kind.Gleanings: NestThingsThemed.Gleanings(pen, p, n, ta, tb, size); break;
                case Kind.SoilPatch: NestThingsThemed.SoilPatch(pen, p, n, ta, tb, size); break;
                default: NestThingsGrowth.SnailShell(pen, p, n, ta, tb, size); break;
            }
            // the kiln's clay buildings light only where they glow (their holes, mouths, cells), not whole; and the amber
            // nest's stones and bark, which stay still in the wind too
            if ((theme.buildingsGlowInParts && (k == Kind.ClayChimney || k == Kind.HexCells)) || k == Kind.AmberNodules || k == Kind.ResinRun)
                for (int i = from; i < pen.into.Count; i++)
                {
                    SolidPoint sp = ThingPen.Building(pen.into[i]);
                    // (and the amber and the resin glossy, their stone and bark matt: the lit splats are the amber's; matt
                    // all over, a window of amber read as gold dust)
                    if ((k == Kind.AmberNodules || k == Kind.ResinRun) && ThingPen.GlowOf(sp) > 0.04f) sp = ThingPen.Glossy(sp);
                    pen.into[i] = sp;
                }
            Bio(pen, k, from, size);
            // dew: a few beads on what holds it (leaves, moss, clover, lichen, petals, fronds), on parts that face up
            if (k == Kind.Leaf || k == Kind.Moss || k == Kind.Clover || k == Kind.Lichen || k == Kind.Petal || k == Kind.Fern || k == Kind.Seedling)
            {
                int beads = (int)(R() * 4.5f), end = pen.into.Count;
                for (int b = 0, tries = 0; b < beads && tries < 30 && end > from; tries++)
                {
                    SolidPoint sp = pen.into[from + (int)(R() * (end - from)) % (end - from)];
                    Vector3 nn = ThingPen.UnpackNormal(sp.nrm);
                    if (nn.y < 0.6f) continue;
                    float r = 0.018f + 0.022f * R();
                    pen.into.Add(ThingPen.Splat(sp.pos + nn * (Mathf.Abs(sp.radius) * 0.5f + r * 0.6f), Vector3.up, (NestTheme.Current ?? NestTheme.Cyan).dew, r, 0.4f));
                    b++;
                }
            }
            // flat things drape over the floor: each splat keeps its height above the plane through p, over the floor
            // under it (so a big leaf follows the ground's rise and fall instead of floating or sinking)
            if (k == Kind.Leaf || k == Kind.Petal || k == Kind.Fern || k == Kind.Feather || k == Kind.Blossom || k == Kind.Lichen
                || k == Kind.WaxComb || k == Kind.EmberCracks || k == Kind.PotShards || k == Kind.Puddle || k == Kind.Wire
                || k == Kind.BittenLeaf || k == Kind.HoneyDrip || k == Kind.LeafBits || k == Kind.Gleanings || k == Kind.SoilPatch)
                for (int i = from; i < pen.into.Count; i++)
                {
                    SolidPoint sp = pen.into[i];
                    Vector3 d = sp.pos - p;
                    float above = Vector3.Dot(d, n);
                    sp.pos.y = look.HeightAt(sp.pos.x, sp.pos.z) + above;
                    pen.into[i] = sp;
                }
        }

        // a living thing's light of its own, as the plants' (ParticleLook.bioFaint): faint all over (bioFaint); and on
        // some of a kind (BioSpots: their share), spots that glow strongly in the nest's own light. (The nest's own
        // things that glow in a pattern of their own draw it themselves: NestThingsThemed.)
        public float bioFaint = 0.09f;
        static bool Living(Kind k) => k <= Kind.Lichen && k != Kind.Twig && k != Kind.Feather
            || k == Kind.Sporophyte || k == Kind.StarMoss || k == Kind.SugarCrystals
            || k == Kind.PollenPellets || k == Kind.FungusTuft || k == Kind.CharBalls;   // (a fallen, bitten leaf and a cut piece are dead: lit only at their cuts and veins; the granary's drip stalk and litter too: lit, the stalk read as a candle)
        static float BioSpots(Kind k) => k == Kind.Mushrooms ? 0.7f : k == Kind.Lichen ? 0.6f
            : k == Kind.Moss || k == Kind.Fern || k == Kind.Seedling || k == Kind.Blossom ? 0.45f
            : k == Kind.Leaf || k == Kind.Clover ? 0.35f : k == Kind.Petal ? 0.25f : 0f;

        /// <summary>A living thing's own light (from: its first splat; size: its size): faint all over, and perhaps its
        /// spots (one in about half the cells of a grid through it, each well inside its cell). Uses the pen's random
        /// numbers, so the nest's layout is as it was.</summary>
        void Bio(ThingPen pen, Kind k, int from, float size)
        {
            if (!Living(k)) return;
            bool spotted = pen.R() < BioSpots(k);
            float cell = Mathf.Max(0.04f, size * 0.16f), sx = pen.R() * 50f, sz = pen.R() * 50f;
            Color c = theme.glow[(int)(pen.R() * 2.99f)].linear;   // the nest's own light
            // (the moss forest's moss cushions: small gold specks, not broad blurred patches of light, on every one of them,
            // review k12: some read as plain green: NestTheme.darkCushions)
            if (theme.darkCushions && k == Kind.Moss) { cell = Mathf.Max(0.03f, size * 0.06f); c = new Color(0.95f, 0.84f, 0.45f).linear; spotted = true; }
            c *= look.bioAlbedo / Mathf.Max(0.05f, c.r * 0.299f + c.g * 0.587f + c.b * 0.114f);
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (mx > 1f) c *= 1f / mx;
            for (int i = from; i < pen.into.Count; i++)
            {
                SolidPoint sp = pen.into[i];
                float glow = Mathf.Max(ThingPen.GlowOf(sp), bioFaint), mark = 0f;
                if (spotted)
                {
                    float gx = sp.pos.x / cell + sx, gy = sp.pos.y / cell, gz = sp.pos.z / cell + sz;
                    int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy), iz = Mathf.FloorToInt(gz);
                    int id = ix * 73856093 ^ iy * 19349663 ^ iz * 83492791;
                    if (Hash01(id) < 0.5f)
                    {
                        const float r = 0.3f;
                        float dx = gx - (ix + r + 0.4f * Hash01(id + 1)), dy = gy - (iy + r + 0.4f * Hash01(id + 2)), dz = gz - (iz + r + 0.4f * Hash01(id + 3));
                        float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy + dz * dz) / r - 0.55f) / 0.45f);
                        mark = 1f - t * t * (3f - 2f * t);
                    }
                }
                pen.into[i] = mark > 0.01f ? ThingPen.Glowing(sp, c, Mathf.Min(1f, mark * 1.25f), Mathf.Max(glow, look.bioStrong * mark)) : ThingPen.WithGlow(sp, glow);
            }
        }

        static float Hash01(int k)
        {
            uint h = (uint)k * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777216f;
        }

        /// <summary>A nest's own thing that glows: an antenna can light it (a touchable glow with no mesh of its own),
        /// and a glassy one has a light inside it (GlassLamps).</summary>
        void Lit(Kind k, float x, float z, float size, float footprint)
        {
            // (the leafcutters' bitten leaf as if half its size tall: its close-ups stand back far enough to see it whole on
            // the floor; at 0.25 the ant stood on the leaf's end. Their drifts of bits flat: further back, plants hid them)
            // (the moss forest's star moss, its shoots now a third to two thirds of the ant tall, as half its patch's width;
            // an antenna lights it)
            float height = k == Kind.BittenLeaf ? size * 0.6f : k == Kind.StarMoss ? size * 0.5f
                : k == Kind.WaxComb || k == Kind.EmberCracks || k == Kind.PotShards || k == Kind.Puddle || k == Kind.Wire
                || k == Kind.Gleanings || k == Kind.SoilPatch || k == Kind.LeafBits ? 0.25f : k == Kind.HexCells ? size * 0.4f : k == Kind.HoneyDrip ? size * 0.6f
                : k == Kind.SugarCrystals || k == Kind.PollenPellets || k == Kind.FungusTuft ? size * 0.5f
                : k == Kind.ResinDrops || k == Kind.HoneyDrops ? size * 0.6f : k == Kind.DewDrop ? size * 0.9f
                : k == Kind.AmberNodules ? size * 0.45f : k == Kind.ResinRun ? size * 0.9f
                : k == Kind.CharTwigs ? 0.3f : k == Kind.CharBalls ? size * 0.4f : k == Kind.AshStones ? size * 0.3f : k == Kind.AshDrift ? 0.25f : size;
            Owned.Add((k.ToString(), new Vector2(x, z), height));
            if (world.glow == null || k == Kind.PotShards || k == Kind.PollenPellets || k == Kind.BittenLeaf || k == Kind.LeafBits || k == Kind.Gleanings || k == Kind.SoilPatch || k == Kind.AshStones || k == Kind.AshDrift) return;
            float y = look.HeightAt(x, z);
            world.glow.AddTouchable(new Vector3(x, y, z), footprint, height, theme.glow[0]);
            touchables++;
            if (k == Kind.ResinDrops) ownLamps.Add((new Vector3(x, y + size * 0.18f, z), size * 0.32f, theme.glow[0]));
            // (the granary's honey drops no lamp of their own: with it, their light inside spread as a halo over them)
            if (k == Kind.AmberShards) ownLamps.Add((new Vector3(x, y + size * 0.3f, z), size * 0.28f, theme.glow[0]));
            if (k == Kind.DewDrop) ownLamps.Add((new Vector3(x, y + size * 0.3f, z), size * 0.06f, theme.glow[1]));   // a glint deep in it, that an antenna's touch brightens
            if (k == Kind.ClayChimney) ownLamps.Add((new Vector3(x, y + size * 0.97f, z), size * 0.1f, theme.glow[2]));
        }

        /// <summary>The thing at the nest's heart (NestTheme.heart): the amber nest's big cluster of amber shards, the
        /// temple's amber shrine with the ancestor inside, and the newer nests' (NestThingsThemed.Heart*): the moss
        /// forest's dew drop on its cushion in a ring of moss stalks, the cistern's pool under a root holding a drop, the
        /// verdigris city's pocket watch, the leafcutters' fungus garden, the granary's honeypot ants, the giant's
        /// button and needle. It stands in the middle of the heart, mostly blocks the ants, can be lit by an antenna and
        /// most have a light inside. Returns its splats.</summary>
        /// <summary>A nest's featured things standing beside its way as landmarks, now that no nest has a heart (the user,
        /// 2026-10-27: "不要用心脏的概念了", its own things beside the way): NestTheme.landmark at each of landmarkAt (shares
        /// of the way's length), just off the way on alternate sides, its front toward the way, where the floor is flat and
        /// clear of plants and props (looked for a little before and after the spot), as finely made as a heart was.
        /// Returns their splats.</summary>
        int Landmarks(ThingPen pen)
        {
            List<Vector2> t = nest?.Track;
            if (t == null || t.Count < 4 || (theme.landmark != "seedcup" && theme.landmark != "nodule" && theme.landmark != "garden") || theme.landmarkAt == null) return 0;
            var along = new float[t.Count];
            for (int i = 1; i < t.Count; i++) along[i] = along[i - 1] + (t[i] - t[i - 1]).magnitude;
            int from = pen.into.Count, side = 1, n = 0;
            foreach (float share in theme.landmarkAt)
            {
                side = -side;
                float size = (n++ % 2 == 0 ? 2.9f : 2.4f) * (theme.landmark == "seedcup" ? 1.05f : theme.landmark == "nodule" ? 0.85f : theme.landmark == "garden" ? 0.86f : 1f), half = size * 0.5f, wide = size * (theme.landmark == "nodule" || theme.landmark == "garden" ? 0.45f : 0.5f);   // its length; half of it, half its width (the amber boulder and the fungus garden round)
                int k0 = 1;
                while (k0 < t.Count - 2 && along[k0] < share * along[t.Count - 1]) k0++;
                bool done = false;
                for (int step = 0; step < (theme.landmark == "seedcup" || theme.landmark == "nodule" || theme.landmark == "garden" ? 80 : 40) && !done; step++)   // (the granary's cup, the amber boulder and the leafcutters' garden look further)
                {
                    int k = k0 + (step % 2 == 0 ? step / 2 : -(step + 1) / 2);   // there, then a little after and before
                    if (k < 1 || k >= t.Count - 1) continue;
                    Vector2 dir = (t[k + 1] - t[k - 1]).normalized, across = new Vector2(-dir.y, dir.x) * side;
                    for (int s = 0; s < 2 && !done; s++, across = -across)
                        for (float off = wide + 0.7f; off < wide + 2.6f && !done; off += 0.2f)
                        {
                            Vector2 c = t[k] + across * off;
                            if (!LandmarkFits(c, dir, half, wide)) continue;
                            if (theme.landmark == "nodule") StandNodule(pen, c, -across, size);
                            else if (theme.landmark == "garden") StandGarden(pen, c, size);
                            else StandSeedCup(pen, c, -across, size);
                            done = true;
                        }
                }
                if (!done) Debug.Log($"[AntGarden] landmark {theme.landmark} at {share:P0} of the way: no room");
            }
            return pen.into.Count - from;
        }

        /// <summary>Whether a landmark lying along dir (half its length, half its width) fits at c: on the canyon floor,
        /// fairly flat, off the way, clear of props and of the plants.</summary>
        bool LandmarkFits(Vector2 c, Vector2 dir, float half, float wide)
        {
            var across = new Vector2(-dir.y, dir.x);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.7853982f;
                Vector2 q = i == 8 ? c : c + dir * (Mathf.Cos(a) * half) + across * (Mathf.Sin(a) * wide);
                if (world.HexDist(q.x, q.y) > -1.5f || world.Dist(q.x, q.y) > -0.15f || look.LayerAt(q.x, q.y).r < 0.55f
                    || !float.IsInfinity(world.Ceiling(q.x, q.y))) return false;
                float h = look.HeightAt(q.x, q.y);
                lo = Mathf.Min(lo, h);
                hi = Mathf.Max(hi, h);
            }
            float reach = half + 0.4f;
            if (hi - lo > 0.35f || look.WayDistance(c.x, c.y) < wide + 0.5f) return false;
            // (the leafcutters' fungus garden grows over what is low round it: only its middle need be clear; at a clear
            // reach it found no room in their crowded garden, twice)
            float clear = theme.landmark == "garden" ? half * 0.6f : reach;
            if (world.colliders.Overlaps(c.x, c.y, clear) || world.reserved.Overlaps(c.x, c.y, clear)) return false;
            // (the garden clear of the guiding beams and the vines round their feet: one stood through its middle; and
            // nothing tall between it and the way: a coral in front of it hid it from the ant)
            if (theme.landmark == "garden")
            {
                foreach (Vector2 b in GuideThreads.BeamFeet)
                    if ((b - c).sqrMagnitude < (half + 2.2f) * (half + 2.2f)) return false;
                float wayD = look.WayDistance(c.x, c.y);
                foreach (Placement pl in world.placements)
                {
                    if (pl.height < 0.8f) continue;
                    var q = new Vector2(pl.x, pl.z);
                    float dq = (q - c).magnitude, wq = look.WayDistance(pl.x, pl.z);
                    // a tall plant nearer the way than the garden is, and within the garden's own reach of it, stands in front
                    if (dq < half + 1.6f && wq < wayD - 0.3f) return false;
                }
            }
            foreach (Placement pl in world.placements)
            {
                float d2 = (pl.x - c.x) * (pl.x - c.x) + (pl.z - c.y) * (pl.z - c.y);
                // (round the granary's cup the floor's low tufts and pebbles may stand, not in it)
                if (d2 < reach * reach && ((theme.landmark != "seedcup" && theme.landmark != "garden") || pl.height >= 0.8f
                    || d2 < (theme.landmark == "garden" ? clear * clear : half * half))) return false;
            }
            return true;
        }

        /// <summary>The granary's landmark at c (NestThingsThemed.HeartSeedCup), its front (the broken lip, the spill and
        /// its pool) toward face, the way.</summary>
        void StandSeedCup(ThingPen pen, Vector2 c, Vector2 face, float size)
        {
            float y = look.HeightAt(c.x, c.y);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                y = Mathf.Min(y, look.HeightAt(c.x + Mathf.Cos(a) * size * 0.3f, c.y + Mathf.Sin(a) * size * 0.3f));
            }
            var p = new Vector3(c.x, y - 0.03f, c.y);
            int from = pen.into.Count;
            pen.Tint = Color.clear;
            pen.Begin(0x5eedc0u ^ (uint)Mathf.RoundToInt(c.x * 31f + c.y * 17f));
            NestThingsThemed.HeartSeedCup(pen, p, Vector3.up, Vector3.right, Vector3.forward, size, 2600f, new Vector3(face.x, 0f, face.y));
            // (no lamp in it: one read as a white blob over the honey)
            // its low parts follow the floor (the pool, the spill's foot)
            for (int i = from; i < pen.into.Count; i++)
            {
                SolidPoint sp = pen.into[i];
                float above = sp.pos.y - p.y;
                if (above >= 0.15f) continue;
                sp.pos.y = look.HeightAt(sp.pos.x, sp.pos.z) + Mathf.Max(0.015f, above - 0.05f);
                pen.into[i] = sp;
            }
            // the cup blocks the ants; an antenna lights it
            world.colliders.Add(new Circle(c.x, c.y, size * 0.48f, size * 0.35f));
            world.reserved.Add(new Circle(c.x, c.y, size * 0.6f, size * 0.3f));
            world.glow?.AddTouchable(p, size * 0.4f, size * 0.3f, theme.glow[0]);
            touchables++;
            Owned.Add(("HoneyStore", c, size * 0.5f));
            Debug.Log($"[AntGarden] landmark seedcup {size:F1} at ({c.x:F1}, {c.y:F1})");
        }

        /// <summary>The amber nest's landmark at c (NestThingsThemed.HeartNodule): a boulder of old amber weathering out
        /// of the floor, broken open toward face, the way, a fern caught in it (no lamp: its light is the amber's own).</summary>
        void StandNodule(ThingPen pen, Vector2 c, Vector2 face, float size)
        {
            float y = look.HeightAt(c.x, c.y);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                y = Mathf.Min(y, look.HeightAt(c.x + Mathf.Cos(a) * size * 0.35f, c.y + Mathf.Sin(a) * size * 0.35f));
            }
            var p = new Vector3(c.x, y - 0.03f, c.y);
            int from = pen.into.Count;
            pen.Tint = Color.clear;
            pen.Begin(0xa3be4u ^ (uint)Mathf.RoundToInt(c.x * 31f + c.y * 17f));
            NestThingsThemed.HeartNodule(pen, p, Vector3.up, Vector3.right, Vector3.forward, size, 2600f, new Vector3(face.x, 0f, face.y));
            for (int i = from; i < pen.into.Count; i++)   // (still in the wind; touched, only its amber lights; its amber glossy)
            {
                SolidPoint sp = ThingPen.Building(pen.into[i]);
                pen.into[i] = ThingPen.GlowOf(sp) > 0.04f ? ThingPen.Glossy(sp) : sp;
            }
            world.colliders.Add(new Circle(c.x, c.y, size * 0.38f, size * 0.45f));
            world.reserved.Add(new Circle(c.x, c.y, size * 0.6f, size * 0.45f));
            world.glow?.AddTouchable(p, size * 0.4f, size * 0.4f, theme.glow[0]);
            touchables++;
            Owned.Add(("AmberBoulder", c, size * 0.5f));
            Debug.Log($"[AntGarden] landmark nodule {size:F1} at ({c.x:F1}, {c.y:F1})");
        }

        /// <summary>The leafcutters' landmark at c (NestThingsThemed.HeartGarden, the heart it had): their fungus garden,
        /// a grey sponge holed and ridged, dotted with the small swollen tips the ants eat, cut leaf pieces round its foot.</summary>
        void StandGarden(ThingPen pen, Vector2 c, float size)
        {
            float y = look.HeightAt(c.x, c.y);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                y = Mathf.Min(y, look.HeightAt(c.x + Mathf.Cos(a) * size * 0.35f, c.y + Mathf.Sin(a) * size * 0.35f));
            }
            var p = new Vector3(c.x, y - 0.05f, c.y);
            int from = pen.into.Count;
            pen.Tint = Color.clear;
            pen.Begin(0x9a4de2u ^ (uint)Mathf.RoundToInt(c.x * 31f + c.y * 17f));
            NestThingsThemed.HeartGarden(pen, p, Vector3.up, Vector3.right, Vector3.forward, size, 2600f);
            // and the fresh cut pieces brought to it, standing round its foot, leaning on it (alone it read as a bigger
            // fungus tuft, not the garden the leaves are carried to)
            int pieces = 5 + (int)(R() * 3f);
            for (int k = 0; k < pieces; k++)
            {
                float a = (k + 0.35f * R()) / pieces * 6.2831853f, d = size * (0.5f + 0.06f * R());   // (at its lobes' outer edge)
                var at = new Vector3(c.x + Mathf.Cos(a) * d, 0f, c.y + Mathf.Sin(a) * d);
                at.y = look.HeightAt(at.x, at.z) - 0.02f;
                // (each leaning in on it, its face turned at random)
                var lean = Quaternion.AngleAxis(-18f, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
                pen.Begin(0x5a17u ^ (uint)(k * 7919) ^ (uint)Mathf.RoundToInt(c.x * 13f));
                NestThingsThemed.LeafSail(pen, at, lean * Vector3.up, lean * Vector3.right, lean * Vector3.forward, size * (0.24f + 0.1f * R()));
            }
            // its low parts follow the floor (its foot, the leaf pieces round it)
            for (int i = from; i < pen.into.Count; i++)
            {
                SolidPoint sp = pen.into[i];
                float above = sp.pos.y - p.y;
                if (above >= 0.15f) continue;
                sp.pos.y = look.HeightAt(sp.pos.x, sp.pos.z) + Mathf.Max(0.015f, above - 0.05f);
                pen.into[i] = sp;
            }
            world.colliders.Add(new Circle(c.x, c.y, size * 0.52f, size * 0.42f));
            world.reserved.Add(new Circle(c.x, c.y, size * 0.62f, size * 0.42f));
            // an antenna lights its top, where its knobs are (lit whole it was one white blaze beside the ant)
            world.glow?.AddTouchable(p + Vector3.up * (size * 0.36f), size * 0.16f, size * 0.12f, theme.glow[0]);
            touchables++;
            Owned.Add(("FungusGarden", c, size * 0.5f));
            Debug.Log($"[AntGarden] landmark garden {size:F1} at ({c.x:F1}, {c.y:F1})");
        }

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
            const float dense = 2600f;   // the newer hearts' splats per unit² (seen close, they are the chamber's focus)
            Vector3 up = Vector3.up, ax = Vector3.right, az = Vector3.forward;
            // toward the way in (where the main track ends at the heart): a heart with a front shows it to the ant coming in
            Vector3 face = Vector3.right;
            if (nest.Track != null && nest.Track.Count > 1)
            {
                List<Vector2> t = nest.Track;
                int hi = 0;
                for (int i = 1; i < t.Count; i++) if ((t[i] - h).sqrMagnitude < (t[hi] - h).sqrMagnitude) hi = i;
                Vector2 d = t[hi] - h;
                if (d.sqrMagnitude > 1e-4f) face = new Vector3(d.x, 0f, d.y).normalized;
            }
            bool drape = true;   // its low parts follow the floor (a pool, a cushion, leaves); a rigid thing's do not
            switch (theme.heart)
            {
                case "shrine":
                    size = 3.4f;
                    NestThingsThemed.Shrine(pen, p, up, ax, az, size, AntPoints("Ant_003"), 1700f);
                    world.colliders.Add(new Circle(h.x, h.y, size * 0.45f, size));
                    ownLamps.Add((p + Vector3.up * (size * 0.42f), size * 0.14f, theme.glow[0]));   // a small core: the block, not a ball of light
                    drape = false;
                    break;
                case "dew":
                    size = 1.6f;
                    NestThingsThemed.HeartDew(pen, p, up, ax, az, size, dense);   // (no lamp in it: water, not a bulb)
                    world.colliders.Add(new Circle(h.x, h.y, size * 0.6f, size));
                    break;
                case "pool":
                    size = 4.4f;
                    ownLamps.Add((NestThingsThemed.HeartPool(pen, p, up, ax, az, size, dense, face), 0.1f, theme.glow[1]));
                    break;
                case "watch":
                    size = 3.2f;
                    ownLamps.Add((NestThingsThemed.HeartWatch(pen, p, up, ax, az, size, dense, face), size * 0.1f, theme.glow[0]));
                    world.colliders.Add(new Circle(h.x, h.y, size * 0.42f, size * 0.5f));
                    drape = false;
                    break;
                case "garden":
                    size = 3.4f;
                    NestThingsThemed.HeartGarden(pen, p, up, ax, az, size, dense);
                    world.colliders.Add(new Circle(h.x, h.y, size * 0.45f, size * 0.8f));
                    break;
                case "honeypots":
                    size = 2.6f;
                    Vector3 cluster = NestThingsThemed.HeartHoney(pen, p, up, ax, az, size, dense, face);
                    for (int k = 0; k < 3; k++)
                        ownLamps.Add((cluster + new Vector3((k - 1) * 0.3f, (k % 2) * 0.2f - 0.1f, 0.1f * k), 0.22f, theme.glow[k % 2]));
                    world.colliders.Add(new Circle(h.x, h.y, 0.75f, 0.45f));
                    break;
                case "seedcup":
                    size = 3.2f;
                    ownLamps.Add((NestThingsThemed.HeartSeedCup(pen, p, up, ax, az, size, dense, face), 0.16f, theme.glow[1]));
                    world.colliders.Add(new Circle(h.x, h.y, size * 0.4f, size * 0.3f));
                    break;
                case "button":
                    size = 3.4f;
                    NestThingsThemed.HeartButton(pen, p, up, ax, az, size, dense, face);
                    world.colliders.Add(new Circle(h.x, h.y, size * 0.48f, size * 0.12f));
                    drape = false;
                    break;
                default:
                    size = 2.8f;
                    NestThingsThemed.HeartShards(pen, p, up, ax, az, size, 1600f);
                    world.colliders.Add(new Circle(h.x, h.y, size * 0.32f, size));
                    ownLamps.Add((p + Vector3.up * (size * 0.3f), size * 0.3f, theme.glow[0]));
                    drape = false;
                    break;
            }
            if (drape)
                for (int i = from; i < pen.into.Count; i++)
                {
                    SolidPoint sp = pen.into[i];
                    float above = sp.pos.y - p.y;
                    if (above >= 0.15f) continue;
                    sp.pos.y = look.HeightAt(sp.pos.x, sp.pos.z) + Mathf.Max(0.015f, above - 0.05f);
                    pen.into[i] = sp;
                }
            // the moss forest's clearing: a ring of tall moss stalks round the drop, like lamps round a square
            if (theme.heart == "dew")
                for (int i = 0, ring = 13; i < ring; i++)
                {
                    float a = (i + 0.4f * (R() - 0.5f)) / ring * 6.2831853f, d = 2.1f + 0.6f * R();
                    float x = h.x + Mathf.Cos(a) * d, z = h.y + Mathf.Sin(a) * d;
                    if (world.Dist(x, z) > -0.15f || look.WayDistance(x, z) < 0.5f) continue;
                    float tall = 1.3f + 1.1f * R();
                    Make(pen, Kind.Sporophyte, x, z, tall, (uint)i * 2654435761u ^ 0x68e31da4u);
                    Lit(Kind.Sporophyte, x, z, tall, Footprint(Kind.Sporophyte, tall));
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
            if (theme.rootWidth <= 0f) return 0;   // a nest without them (NestTheme.rootWidth)
            for (int tries = 0; tries < roots * 200 && made < roots; tries++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, R()), z = Mathf.Lerp(world.minZ, world.maxZ, R());
                if (world.HexDist(x, z) > -1.5f || !float.IsInfinity(world.Ceiling(x, z))) continue;
                Color lw = look.LayerAt(x, z);
                if (lw.g < 0.45f || lw.r < 0.15f) continue;   // at the foot of a wall
                if (R() > 0.15f + 0.85f * look.Focus(x, z) || look.WayDistance(x, z) < 1.2f) continue;
                // none round a heart that has its thing: they climbed its walls in the beam's light and hid it
                if (nest != null && !string.IsNullOrEmpty(theme.heart) && (new Vector2(x, z) - nest.Heart).sqrMagnitude < 4.5f * 4.5f) continue;
                pen.Begin((uint)made * 2246822519u ^ 0x27d4eb2fu);
                Color rt = TintFor(OwnTints("Root") ?? RootTints);
                pen.Tint = theme.vineDarken > 0.6f && rt.a > 0 ? Color.Lerp(new Color(0.3f, 0.28f, 0.26f), rt, 0.45f) : rt;   // (a vivid palette's roots dark and muted, as the vines are)
                if (theme.rootColour.a > 0) pen.Tint = theme.rootColour;   // a nest's own bark (NestTheme.rootColour)
                // into the canyon: down the slope of the distance field
                float e = 0.3f, gx = world.Dist(x + e, z) - world.Dist(x - e, z), gz = world.Dist(x, z + e) - world.Dist(x, z - e);
                float dir = Mathf.Atan2(-gz, -gx) + (pen.R() - 0.5f) * 1.4f;
                Root(pen, x, z, dir, 2.2f + 3.8f * pen.R(), (0.07f + 0.08f * pen.R()) * theme.rootWidth, 0);
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
            Vector3 cam = CamPos();
            shellCam = new Vector2(cam.x, cam.z);
            shellWorkCount = look.SolidShell(shellAt, shellReach, ref shellWork, cam, shellMove + 0.6f);
            int back = 1 - shellFront;
            Ensure(back, shellWorkCount);
            if (shellWorkCount > 0) shellBuffers[back].SetData(shellWork, 0, 0, shellWorkCount);
            shellCounts[back] = shellWorkCount;
            shellFront = back;
            ShellsMade++;
            ShellsShown++;
            Debug.Log($"[AntGarden] solid shell: {shellWorkCount} fine plant splats round ({shellAt.x:F0}, {shellAt.y:F0})");
        }

        static Vector3 CamPos() => Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        /// <summary>Checks: the fine shell drawn is the one made for where the ant and the camera are now (nothing is being
        /// made or uploaded, and neither has moved far since), so a still shows every plant round the ant.</summary>
        public bool ShellReady()
        {
            if (!look.solidPlants || ant == null) return true;
            Vector3 cam = CamPos();
            return shellTask == null && !shellPending && uploading < 0
                && (new Vector2(ant.position.x, ant.position.z) - shellAt).sqrMagnitude < 1f && (new Vector2(cam.x, cam.z) - shellCam).sqrMagnitude < 1f;
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
            // (12: a walking ant can get 8 ahead of a shell still being made, and making it at once stalled a second)
            if (shellTask == null && !shellPending && uploading < 0 && (at - shellAt).sqrMagnitude > 144f) { ShellNow(); return; }
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
                Ensure(uploading, shellWorkCount);
            }
            if (uploading >= 0)
            {
                int n = Mathf.Min(UploadChunk, shellWorkCount - uploadedSoFar);
                if (n > 0) shellBuffers[uploading].SetData(shellWork, uploadedSoFar, uploadedSoFar, n);
                uploadedSoFar += n;
                if (uploadedSoFar < shellWorkCount) return;
                shellCounts[uploading] = shellWorkCount;
                shellFront = uploading;
                uploading = -1;
                ShellsShown++;
                if (ShellsShown % 4 == 1) Debug.Log($"[AntGarden] solid shell: {shellWorkCount} fine splats, made in {shellMs} ms");
                return;
            }
            Vector3 cam = CamPos();
            var camAt = new Vector2(cam.x, cam.z);
            if (shellTask != null || ((at - shellAt).sqrMagnitude < shellMove * shellMove && (camAt - shellCam).sqrMagnitude < shellMove * shellMove)) return;
            shellAt = at;
            shellCam = camAt;
            float slack = shellMove + 0.6f;
            shellTask = System.Threading.Tasks.Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                shellWorkCount = look.SolidShell(at, shellReach, ref shellWork, cam, slack);
                shellMs = sw.ElapsedMilliseconds;
            });
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
            block.SetVector("_ThingNear", new Vector4(look.nearDist, 1f / Fine, 0, 0));
            block.SetFloat("_NearLift", theme.nearLift);
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
                shellBlock.SetVector("_NearDensity", new Vector4(look.nearDist, Mathf.Clamp01(look.solidFine / look.solidNear), theme.relicKeep, 0));   // (z: NestTheme.relicKeep)
                shellBlock.SetFloat("_NearLift", theme.nearLift);
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
