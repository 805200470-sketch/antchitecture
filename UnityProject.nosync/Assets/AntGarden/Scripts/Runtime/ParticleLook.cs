using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// Toward the look of point clouds and Gaussian splats (Marshmallow Laser Feast's "Dream"): plants, ruin props
    /// and ground drawn as glowing points sampled from their surfaces (AssetBuilder.SurfacePoints), drawn from
    /// buffers with DrawProcedural (Shaders/ParticleLook.shader). Two ways:
    /// - Overlay (P toggles; -particlelook [soft]): round the ant, a layer of points over the meshes, which dim.
    /// - Replace (the particle nest, chapter one): the plant meshes are hidden and the whole nest is made of dense
    ///   points, pale like a scan but keeping their textures' light and dark, colour and grain: lighter parts of a
    ///   texture get more points and brighter ones, like a halftone. The walls and floor of the canyons become
    ///   points too, sampled from the terrain's own triangles and coloured from its own textures (read back from
    ///   the GPU once), the terrain itself sinking nearly black so it only hides what lies behind it; dust floats in
    ///   the canyons' air. Sparse over the whole nest (densest along the ways the ant is led), dense round the ant:
    ///   the shell, made again on a worker thread as it walks, adds ground and plant points near it, each fading in
    ///   at its own distance. Colliders, touches and walking are unchanged.
    /// </summary>
    public class ParticleLook : MonoBehaviour
    {
        public enum Mode { Overlay, Replace }
        public Mode mode = Mode.Overlay;
        public float radius = 60f;          // overlay: plants and props this close to the ant become points
        public float groundRadius = 40f;    // overlay: and the ground this close
        public float regatherAfter = 20f;   // overlay: gather again once the ant has walked this far
        public float perUnit = 220f;        // overlay: points for a plant one unit tall (× height^1.6)
        public float perArea = 700f;        // replace: points for a plant one unit tall (× height², its surface)
        public int budget = 4000000;        // replace: at most this many plant points in all (fewer for every plant)
        public float groundStep = 0.5f;     // overlay: spacing of the ground's points
        public float pointSize = 0.02f;     // a point's radius (units)
        public float brightness = 1.2f;
        public float glowBrightness = 3f;
        public float shimmer = 0f;          // how far each point trembles (units)
        public float plantDim = 0.82f;      // overlay: how dark the plant meshes go behind their points
        public float groundDim = 0.5f;      // and the ground
        public float scanMix = 0.65f;       // how far a point's colour goes from its texture toward the pale scan colour
        public Color scanOverride = Color.clear;   // replace: one pale colour for every plant (alpha 0: each region's own)
        public float grain = 0f;            // replace: how much a texture's light and dark thin and dim its points (0..1)

        // replace: plants as solid lit splats (drawn by SolidThings with the floor's things) instead of glowing dots: a
        // coarse set for every plant, and a fine one for the plants round the ant (the solid shell)
        public bool solidPlants;
        public float solidCoarse = 240f;    // splats per unit² of a plant's surface in the coarse set (along the ways)
        public int solidCoarseBudget = 1500000;
        // the giants beyond the walls (Scatter.Surroundings: trunks, fan trees, spires 26-48 tall): their splats as big as
        // their size wants, splatFrac of their height, never smaller than splatMin nor larger than splatMax, as many as
        // cover them, not thinned further off (FarSplats). (They had 8000 each, as large as needed to cover them: up to
        // 0.6 across, twice that from 30 units off, so the far fan trees read as blobby mushrooms; the user, 10-27:
        // "背景远处大蘑菇素材的粒子太大了，完全没有细节，可以根据物件大小调整粒子大小，但是最好设定一个最大和最小值").
        // At 0.006 and at most 0.2 (10-27) still "这个点有点太大，粒子太大了" (10-29): half that now. As many as would cover
        // them at that size are four times the splats, so they cover only part of it, over a body of few large dark
        // splats a little inside it, in its own colour darkened (farBody): fine grain on a solid dark shape, as the walls'
        public float splatFrac = 0.003f;
        public float splatMin = 0.04f;      // (the nest's own plants have 0.04 along the ways, 0.055 off them, as before)
        public float splatMax = 0.1f;
        public int farBudget = 1200000;     // at most this many splats for all of them (their body besides)
        public float farBody = 3f;          // the body's splats, this many times the giant's own size (at most 0.3)
        public float farBodyDark = 0.75f;   // and their colour, the giant's mean darkened by this (at 0.45 the fine ones lay on it as bright flakes on black)
        public float solidFine = 1500f;     // and in the fine one, round the ant (as far from the camera as it thins to)
        public int solidFineCap = 42000;    // at most this many for one plant (the old fine set; see solidNear)
        public float solidNear = 16000f;    // the fine set right in front of the camera: splats per unit² (detail, form)
        public float nearDist = 2.8f;       // within this of the camera a plant has all of them; further, fewer (1/d²)
        public int solidNearCap = 200000;   // at most this many for one plant (all of a plant's stored points)
        public int splitMax = 4;            // a big plant's stored points too few for solidNear: each near the camera drawn as up to this many
        public float solidAlbedo = 0.5f;    // how bright a plant's splats are lit (its dots' colours were for glowing)
        // plants whose glowing part is glass (a mesh can only make it an opaque lump): there a thin, broken shell of
        // splats round a light of its own inside, as a capture of something translucent looks
        public string[] glassKinds = { "spore_stalk" };
        // crystals (Tripo, 02_Concepts/Crystals_v1): solid and faceted (CrystalSplat). As glass, a thin broken shell round
        // a light, they read as an outline (the user: "只有一个外形，没有那种质感，也没有一些棱棱角角…只有一个剪影形状")
        public string[] crystalKinds = { "crystal_a_spray", "crystal_b_fan", "crystal_c_mixed" };
        public float crystalAlbedo = 0.35f;     // how light their faces are (as albedo), before each face's light and dark (0.3 smoky quartz;
                                                // 0.21-0.24 with a strong sheen, metal; 0.45-0.6 plaster)
        public float crystalOwn = 0.35f;        // how much of their texture's own colour their faces keep (its patches of colour more)
        public float crystalGlow = 0.4f;        // a light of their own from inside, more toward their points
        public float crystalCloud = 0.5f;       // and in soft clouds inside them, one or two to a prism, more toward their points
        public float crystalCore = 0.3f;        // and at the cores of their faces, away from their ridges (its growth map's s: depth)
        public float crystalInner = 2.4f;       // and more in the patches of colour inside them (lavender, green)
        public float crystalLine = 0.7f;        // how much their ridges and points glow
        public float crystalDensity = 4f;       // their fine splats per unit², of a plant's (fewer, larger ones rounded the thin prisms into tubes)
        public float crystalWarm = 0.55f;       // their upper inside taking the nest's light colour ("冷灰色，里面不透光")
        public float crystalLit = 0.35f;        // and glowing this much more there
        public float crystalRidgeShare = 2.5f;  // their ridges' points drawn this many times as often as their faces' (CrystalOrder)
        public float emberShare = 2f;           // a relic's embers' points drawn this many times as often as the rest (EmberOrder)
        // (the night's fifteen rounds, 2026-10-27: v10, these, was the reviews' best in the game; a sheen (Glossy) and darker
        // faces read as metal, coloured patches as painted on, brighter glow as plaster. The next step is translucency, a
        // pass of its own: the user to decide, Docs/报告/2026-10-28_场景报告/共用改动.md)
        public Vector3 crystalKey = new Vector3(0.55f, 0.62f, -0.56f);   // their faces lit from this side (toward the light),
        public Vector3 crystalFill = new Vector3(-0.7f, -0.05f, 0.7f);   // and dimly from this one
        // plants whose glowing part is only their head (the curl of a lollipop-like curl): glowing from this far up (under
        // 0, set by a nest for a kind: no part of it glows, its texture's glow flags ignored, its light its markings alone)
        public readonly Dictionary<string, float> glowFrom = new Dictionary<string, float>
        {
            { "curl_glow", 0.5f }, { "curl_a_crozier", 0.6f }, { "curl_b_double", 0.62f }, { "curl_c_hook", 0.5f }, { "curl_d_cluster", 0.55f },
        };
        public int glassCore = 1400;        // splats in that light (all in the fine set, a sixth in the coarse one)
        // the particle nest's finely made plants (Tripo v3.1 and their splat captures). Drawn with the same grain and
        // the same share of their own colour as the nest's other plants (finer points stood apart from the scene); lit
        // so their form reads: dark at the foot, a light of their own growing toward the top (a flat light of their own
        // made them read flat); their glowing parts (a curl's head, a lantern) glow in the nest's light only a little,
        // their pattern kept. They grow in tufts (Game.LabPlants). (Thickening them, their points spread through a
        // layer round the surface, blurred the curls' detail; drawing all their points made them finer than the rest.)
        public string[] finePlants = { "curl_a_crozier", "curl_b_double", "curl_c_hook", "curl_d_cluster", "curl_e_corkscrew",
            "frond_a_feather", "puff_a_stalked", "puff_b_lantern", "coral_b_branching",
            "liverwort_parasols", "pixie_cup_lichen", "moss_capsules",   // (these three the moss forest's own)
            "tear_tree", "gumweed" };   // (and these two the amber nest's)
        public float fineOwnLight = 0.26f;  // its light of its own at the top (its lighter parts more), none at its foot (at 0.32, over bioFaint, pale young leaves lit whole)
        public float fineFoot = 0.72f;      // how dark its foot is, of its upper part's colour (at 0.5 the change read as a band)
        public float fineGlow = 1f;         // how strongly its glowing parts glow (a curl's head, a lantern), of what the others' do
        // and a light of its own in places, as on a dandelion: a puffball's middle strongest and the small balls round
        // it a little less ("中心可以强一些，四周的小球可以稍微弱一些"), a crozier's coil, the tips of curls and fronds
        // (SpotTable), in its markings' colour; never taking the scene over
        public float fineSpotGlow = 1f;
        // plants whose middle has a light in it too (GlassLamps), seen through what surrounds it: a puffball's, among its rays
        public string[] heartLamps = { "puff_a_stalked" };
        public int fineMotes = 3;           // and motes of light slowly circling each (GlassLamps), as fireflies round a plant
        // a light of their own, as on another world rather than in a garden (the user: "素材都可以有一些微光，然后同时有
        // 一些纹理发一些强光，参考阿凡达的世界观，我们要和普通真实的植物做出区分"): every plant faintly lit of itself, more
        // toward its top (bioFaint), and most kinds marked with a pattern that glows strongly (BioTable: spots, rings up
        // it, rows of dots, its texture's palest veins and rims, its tips), the same on every plant of a kind as a
        // species' markings are, a little stronger or weaker plant by plant; in its light's colour (now and then an
        // accent's, as the palette gives it) and small in area, so it marks the plant without lighting the scene
        public float bioFaint = 0.15f;   // (at 0.2, over the scene's lifted light, the bodies glowed as much as their parts: "身子暗、部位亮")
        public float bioStrong = 1f;
        public float bioAlbedo = 0.62f;     // how light the markings' colour is (as albedo: they glow by it; near white read as frost)
        public float accentArea = 6f;       // a plant with more surface than this (units²) never takes an accent colour
        /// <summary>Replace, solid plants: the motes circling the finely made plants (GlassLamps): the plant's foot, how
        /// far round, between which heights, how fast (radians a second; the sign the way round), where they start,
        /// how big, their colour.</summary>
        public readonly List<(Vector3 centre, float radius, Vector2 height, float speed, float phase, float size, Color col)> orbitLamps =
            new List<(Vector3, float, Vector2, float, float, float, Color)>();
        readonly Dictionary<PlantRef, float[]> spotCache = new Dictionary<PlantRef, float[]>();
        readonly Dictionary<PlantRef, Vector4> spotHearts = new Dictionary<PlantRef, Vector4>();   // a glowing core's middle (xyz) and its part's mean radius (w)

        // where each finely made plant lights a little of its own (kind 0: a soft core round the middle of what is above
        // `from`, 1: that part's rim, 2: its growing tips; size: the core's radius or where the rim starts, of that
        // part's mean radius; amount)
        static readonly Dictionary<string, Vector4[]> SpotTable = new Dictionary<string, Vector4[]>
        {
            { "puff_a_stalked", new[] { new Vector4(0, 0.62f, 0.5f, 1f), new Vector4(1, 0.62f, 1.02f, 0.3f) } },   // its hub; its balls, a little
            { "curl_a_crozier", new[] { new Vector4(0, 0.68f, 0.4f, 0.7f) } },
            { "curl_d_cluster", new[] { new Vector4(2, 0.82f, 0f, 0.2f) } },
            { "curl_b_double", new[] { new Vector4(2, 0.85f, 0f, 0.4f) } },
            { "curl_e_corkscrew", new[] { new Vector4(2, 0.86f, 0f, 0.45f) } },
            { "frond_a_feather", new[] { new Vector4(2, 0.78f, 0f, 0.4f) } },
        };

        // each kind's markings (bioFaint), following how it grows (its growth map, Tools/plants/growth_map.py; the user,
        // on bands cut level through a corkscrew's stem and leaves alike: "要顺着植物本身的生长逻辑去生长 … 要贴合它的形
        // 体"): pattern, size, width, strength. 1 spots on its surface (size: the cell of the grid they sit in, of its
        // height; width: a spot's radius, of the cell), 2 nodes: rings round its stems, square to them wherever they turn
        // (size: how many from its foot to its farthest point; width: half a ring, of their spacing), 4 its texture's
        // palest parts, its own veins, rims and flecks (size: their share of its points: its texture's mean says
        // nothing, some kinds are pale all over), 5 its growing tips, the ends of its shoots, leaflets, branches and
        // leaves (size: how far back from a tip they glow, of its height), 6 growth zones: rings out from its foot over
        // all of it, as on a cup fungus (size, width: as 2), 7 a crozier's: thin rings up it at even steps of its growth,
        // brighter toward its coil, and its coil's tip (size, width: as 2), 8 its growing tips alone, softly (as 5 without
        // its specks); 0 none (stone, soil, what
        // glows already). 2, 5, 6, 7 and 8 need its growth map
        // (without one: none). A kind not listed gets one by its name; a nest may give a kind its own (NestTheme.bio).
        static readonly Dictionary<string, Vector4> BioTable = new Dictionary<string, Vector4>
        {
            { "curl_a_crozier", new Vector4(5, 0.045f, 0, 1f) }, { "curl_b_double", new Vector4(5, 0.03f, 0, 1f) },
            { "curl_c_hook", new Vector4(5, 0.045f, 0, 1f) }, { "curl_d_cluster", new Vector4(1, 0.09f, 0.34f, 1f) },
            { "curl_e_corkscrew", new Vector4(5, 0.028f, 0, 1f) }, { "frond_a_feather", new Vector4(5, 0.03f, 0, 1f) },
            { "puff_a_stalked", Vector4.zero }, { "puff_b_lantern", new Vector4(4, 0.07f, 0, 1f) },   // (a puffball's light: SpotTable)
            { "coral_b_branching", new Vector4(5, 0.035f, 0, 1f) },
            { "tiny_mushrooms", new Vector4(1, 0.12f, 0.34f, 0.8f) }, { "sponge_mound", new Vector4(1, 0.1f, 0.32f, 0.8f) },
            { "coral_fungus", new Vector4(5, 0.045f, 0, 0.9f) }, { "fallen_leaf", new Vector4(4, 0.05f, 0, 0.8f) },
            { "dry_grass_tuft", new Vector4(5, 0.035f, 0, 0.55f) }, { "trunk_night", new Vector4(1, 0.06f, 0.3f, 0.7f) },
            { "fan_tree", new Vector4(4, 0.05f, 0, 0.8f) }, { "fungus_garden", new Vector4(1, 0.08f, 0.34f, 0.9f) },
            { "anemone_fungus", new Vector4(5, 0.045f, 0, 1f) }, { "cup_fungus", new Vector4(6, 6, 0.05f, 0.9f) },
            { "jelly_fungus", new Vector4(4, 0.06f, 0, 0.9f) }, { "bracket_shelf", new Vector4(1, 0.08f, 0.3f, 0.8f) },
            { "thorn_succulent", new Vector4(5, 0.035f, 0, 0.9f) }, { "lantern_pods", new Vector4(4, 0.06f, 0, 0.9f) },
            { "glass_sponge", new Vector4(4, 0.06f, 0, 0.8f) }, { "dry_mushroom", new Vector4(1, 0.12f, 0.34f, 0.8f) },
            { "root_arch", new Vector4(1, 0.07f, 0.3f, 0.7f) }, { "boulder_moss", new Vector4(1, 0.08f, 0.3f, 0.7f) },
            { "pebble_cluster", Vector4.zero }, { "soil_spire", Vector4.zero }, { "seed_pile", Vector4.zero },
            { "spore_stalk", Vector4.zero }, { "amber_resin", Vector4.zero }, { "seed_husk", Vector4.zero },
            { "crystal_a_spray", Vector4.zero }, { "crystal_b_fan", Vector4.zero }, { "crystal_c_mixed", Vector4.zero },   // (their light: CrystalSplat)
        };
        // and kinds whose growing tips glow as well (how far back from a tip, of its height)
        static readonly Dictionary<string, float> BioTips = new Dictionary<string, float>();

        /// <summary>0 below a, 1 above b, smooth between (as a shader's smoothstep; Mathf.SmoothStep is another thing).</summary>
        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static uint KindHash(string name)
        {
            uint h = 2166136261u;
            foreach (char ch in name) h = (h ^ ch) * 16777619u;
            return h;
        }

        readonly Dictionary<PlantRef, Vector2> paleCuts = new Dictionary<PlantRef, Vector2>();

        /// <summary>Where a kind's palest parts begin and are full (against its mean lightness): about share of its points
        /// lie above the middle of the two. The same for every plant of a kind.</summary>
        Vector2 PaleCut(PlantRef plant, float meanLum, float share)
        {
            if (paleCuts.TryGetValue(plant, out Vector2 cut)) return cut;
            var pale = new List<float>(plant.points.Length / 5 + 1);
            for (int k = 0; k < plant.points.Length; k += 5)
            {
                Color32 c = plant.pointColors[k];
                pale.Add(Lum(new Color(Linear[c.r], Linear[c.g], Linear[c.b])) / meanLum);
            }
            pale.Sort();
            float At(float q) => pale[Mathf.Clamp(Mathf.RoundToInt(q * (pale.Count - 1)), 0, pale.Count - 1)];
            return paleCuts[plant] = new Vector2(At(1f - share * 1.6f), At(1f - share * 0.45f));
        }

        /// <summary>A kind's markings (BioTable; for a kind not listed, one picked by its name).</summary>
        static Vector4 Bio(string name)
        {
            if (BioTable.TryGetValue(name, out Vector4 b)) return b;
            switch (KindHash(name) % 5)
            {
                case 1: return new Vector4(1, 0.1f, 0.32f, 0.8f);
                case 2: return new Vector4(2, 8, 0.065f, 0.8f);
                case 3: return new Vector4(4, 0.06f, 0, 0.8f);
                case 4: return new Vector4(5, 0.06f, 0, 0.8f);
                default: return Vector4.zero;
            }
        }

        /// <summary>How much stored point k (at p) of a plant lies in its kind's markings (0..1); never at its foot. pale:
        /// how pale its texture is there, against the plant's mean. Plain arithmetic only.</summary>
        static float BioMark(SolidSlot s, int k, Vector3 p, float y01, float pale)
        {
            if (y01 < 0.1f) return 0f;
            float mark = 0f;
            switch (s.bio)
            {
                case 1:
                {
                    // a spot in about half the cells of a grid through it, each well inside its cell
                    float c = s.bioSize * s.top, gx = p.x / c + 17.3f * s.bioSeed, gy = p.y / c, gz = p.z / c - 11.1f * s.bioSeed;
                    int ix = (int)System.Math.Floor(gx), iy = (int)System.Math.Floor(gy), iz = (int)System.Math.Floor(gz);
                    int cell = ix * 73856093 ^ iy * 19349663 ^ iz * 83492791;
                    if (PointHash(cell) > 0.5f) break;
                    float r = s.bioWidth, span = 1f - 2f * r;
                    float dx = gx - (ix + r + span * PointHash(cell + 1)), dy = gy - (iy + r + span * PointHash(cell + 2)), dz = gz - (iz + r + span * PointHash(cell + 3));
                    mark = 1f - Smooth(0.55f, 1f, (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz) / r);
                    break;
                }
                case 2:
                case 6:
                {
                    // rings at even steps of its growth from its foot: square to a stem wherever it turns (2: on its stems
                    // only), round a cup as its growth zones (6)
                    if (s.growth == null) break;
                    float t = s.growth[k] * s.bioSize + s.bioSeed;
                    mark = 1f - Smooth(0.5f, 1f, Mathf.Abs(t - Mathf.Round(t)) / s.bioWidth);
                    if (s.bio == 2) mark *= Smooth(0.35f, 0.7f, s.stem[k] / 255f);
                    break;
                }
                case 4: mark = Smooth(s.bioLo, s.bioHi, pale); break;
                case 5:
                    if (s.tip == null) break;
                    // its tips: bright at the very end, the light fading back along it over twice as far, softly; and a few
                    // brighter specks scattered just behind them
                    mark = (1f - Smooth(0f, 2.2f * s.bioSize, s.tip[k])) * (0.55f + 0.45f * (1f - Smooth(0f, 0.6f * s.bioSize, s.tip[k])));
                    if (s.tip[k] < 3f * s.bioSize && PointHash(k * 31 + 7) < 0.035f) mark = 1f;
                    break;
                case 7:
                {
                    // nodes up its stem, rings square to it wherever it turns, brighter toward its coil, and its coil's
                    // tip (a crozier; its texture's pale line up the stem, lit with dots, read as frost, coarse as specks)
                    if (s.growth == null) break;
                    float t = s.growth[k] * s.bioSize + s.bioSeed;
                    mark = (1f - Smooth(0.5f, 1f, Mathf.Abs(t - Mathf.Round(t)) / s.bioWidth)) * Smooth(0.25f, 0.85f, s.growth[k]);
                    if (s.tip != null) mark = Mathf.Max(mark, (1f - Smooth(0f, 0.09f, s.tip[k])) * Smooth(0.55f, 0.8f, s.growth[k]));   // (the coil's, not the leaflets' at its foot)
                    break;
                }
                case 8:
                    // its growing tips alone, soft, with no specks round them (5's specks read as frost on a cluster's heads)
                    if (s.tip == null) break;
                    mark = (1f - Smooth(0f, 2.2f * s.bioSize, s.tip[k])) * (0.55f + 0.45f * (1f - Smooth(0f, 0.6f * s.bioSize, s.tip[k])));
                    break;
            }
            if (s.tipsToo > 0 && s.tip != null) mark = Mathf.Max(mark, 1f - Smooth(0f, 2.2f * s.tipsToo, s.tip[k]));
            return Smooth(0.1f, 0.25f, y01) * mark;
        }

        /// <summary>How much each of a finely made plant's stored points lights of its own (SpotTable; null: none). The
        /// same for every plant of a kind.</summary>
        float[] Spots(PlantRef plant, float top)
        {
            if (spotCache.TryGetValue(plant, out float[] w)) return w;
            Vector4[] spots = null;   // (a nest's own for a kind first: NestTheme.spots)
            if (NestTheme.Current?.spots == null || !NestTheme.Current.spots.TryGetValue(plant.name, out spots))
                if (!SpotTable.TryGetValue(plant.name, out spots)) return spotCache[plant] = null;
            Vector3[] pts = plant.points;
            w = new float[pts.Length];
            foreach (Vector4 sp in spots)
            {
                int kind = (int)sp.x;
                float from = sp.y;
                if (kind == 2)
                {
                    // its tips: its growing tips as its growth map has them (back half as far as `from` is from its top),
                    // or without one, its top (a band cut level through it)
                    float back = (1f - from) * 0.5f;
                    for (int k = 0; k < pts.Length; k++)
                        w[k] = Mathf.Max(w[k], (plant.pointTip != null ? 1f - Smooth(0.45f * back, back, plant.pointTip[k])
                            : Mathf.SmoothStep(0f, 1f, (pts[k].y / top - from) / (1f - from))) * sp.w);
                    continue;
                }
                // the part above `from`: its middle and its mean radius
                Vector3 c = Vector3.zero;
                int n = 0;
                for (int k = 0; k < pts.Length; k++) if (pts[k].y / top >= from) { c += pts[k]; n++; }
                if (n < 20) continue;
                c /= n;
                float mean = 0;
                for (int k = 0; k < pts.Length; k++) if (pts[k].y / top >= from) mean += (pts[k] - c).magnitude;
                mean = Mathf.Max(1e-4f, mean / n);
                if (kind == 0) spotHearts[plant] = new Vector4(c.x, c.y, c.z, mean);
                for (int k = 0; k < pts.Length; k++)
                {
                    if (pts[k].y / top < from - 0.05f) continue;
                    float d = (pts[k] - c).magnitude / mean;
                    float v = kind == 0 ? 1f - Mathf.SmoothStep(0f, 1f, d / sp.z) : Mathf.SmoothStep(0f, 1f, (d - sp.z) / Mathf.Max(0.05f, 1.25f - sp.z));
                    w[k] = Mathf.Max(w[k], v * sp.w);
                }
            }
            return spotCache[plant] = w;
        }
        public Vector2 fineLean = new Vector2(0.3f, 0.15f);   // how far a head's glow (x) and its texture's glowing parts (y) lean to the nest's light

        /// <summary>A plant as the solid shell sees it (copied on the main thread: workers cannot touch transforms).</summary>
        public sealed class SolidSlot
        {
            public Vector3[] pts;
            public Color32[] raw;           // the texture's colour at each point (alpha 255: a glowing part)
            public uint[] nrms;
            public float[] density;         // how much denser than an even spread each point lies (null: even)
            public byte[] open;             // how open each point is (null: all open)
            public Matrix4x4 m;
            public Vector3 pos;
            public float reach, area, top, meanLum;
            // this plant's own look, so no two of a kind are alike: its tints (base to tip), how much of the texture's
            // own colour it keeps, its mottling, how strongly its glowing parts glow, and how it is bent, twisted and
            // stretched
            public Color baseTint, tipTint, glow;
            public float keepOwn, contrast, mottle, mottleScale, mottleSeed, band, bandFreq, glowGain;
            public float faint;             // a faint light of its own in its lightest parts (veins, rims, pale spots)
            public bool fine;               // a finely made plant (finePlants)
            public float[] spots;           // and how much each of its points lights of its own (Spots; null: none)
            public int bio;                 // its kind's markings (BioTable; 0 none): their size, width, strength on this
            public float bioSize, bioWidth, bioAmt, bioSeed;   // plant, where its spots' grid and its rings start,
            public float bioLo, bioHi;      // how pale its palest parts are (from: none of them, to: all; pattern 4)
            public float tipsToo;           // its growing tips glow as well, this far back (BioTips; 0: not)
            public float[] growth, tip;     // its kind's growth map (PlantRef.pointGrowth, pointTip, pointStem; null: none)
            public byte[] stem;
            public Color bioCol;            // and their colour (linear, as albedo: they glow by it)
            public Vector2 bendDir;
            public float bend, twist, sy, sxz;
            public bool rigid;              // a thing (relic): neither bent nor recoloured
            public bool glass;              // its glowing part is glass (glassKinds)
            public bool crystal;            // a crystal (crystalKinds): CrystalSplat where it is crystal, still in the wind
            public byte[] ridge;            // and how much each point lies on a ridge (PlantRef.pointRidge)
            public int[] order;             // its points in the order its splats take them (CrystalOrder; null: as stored)
            public Color crystalTint, crystalEdge, crystalLight;   // its faces' colour (the nest's, pale), its ridges', the light inside it
            public float crystalLum;        // its crystal's mean lightness in its texture
            public Vector2 crystalChroma;   // how coloured its texture is where its patches of colour begin, and where they are full
            public float glowFrom;          // its glowing part only from this far up (0..1 of its height)
            public float sunk;              // lowered this far (of its height) so the lump of soil it stands on is under the floor (NestTheme.sunkKinds)
            public float foot;              // how dark its foot is, if its nest wants it darker than fineFoot (NestTheme.kindFoot; 0: fineFoot)
            public float gain = 1f;         // how light it is, if its nest wants a kind darker (NestTheme.kindGain)
            public Color glowDeep;         // its glow colour, deeper (the palette's glow colours are pale: as light they read white)
            public float ownHue, ownSat = 1;   // its region's turn of the texture's own colour (a blue plant in the amber nest)
            public bool building;              // a prop that lights only in its glowing parts (NestTheme.buildingsGlowInParts)
            public bool far;                   // one of the huge trees beyond the walls (Scatter.Surroundings)
            public string name;                // its plant's (checks)
            public float partFrom, partTo, partRel, partAmount, partRed, partHot;   // a part that glows a little in this nest only (NestTheme.glowParts)
            public Vector4[] core;          // and the light inside it: where (xyz), how far out (w, 0 middle .. 1 edge)
        }
        readonly List<SolidSlot> solidSlots = new List<SolidSlot>();
        readonly Dictionary<PlantRef, (float mean, float top)> kindStats = new Dictionary<PlantRef, (float, float)>();
        readonly Dictionary<PlantRef, Vector4[]> glassCores = new Dictionary<PlantRef, Vector4[]>();
        readonly Dictionary<PlantRef, Vector4> glassBulbs = new Dictionary<PlantRef, Vector4>();   // its middle (xyz), its light's radius (w)
        /// <summary>Replace, solid plants: every glass plant's light (where in the world, how big, its colour), for
        /// GlassLamps; filled by BuildSolidPlants.</summary>
        public readonly List<(Vector3 pos, float size, Color col)> glassLamps = new List<(Vector3, float, Color)>();
        /// <summary>Replace: more solid splats for SolidThings to draw with the floor's things (the vines round the
        /// guiding beams, GuideThreads.VineSplats); filled before SolidThings starts.</summary>
        public readonly List<SolidPoint> extraSolids = new List<SolidPoint>();

        // replace: the ground as points
        public readonly List<MeshFilter> ground = new List<MeshFilter>();   // the terrain's meshes
        public float groundNear = 260f;     // ground points per unit² along the ways (walls a little more, floor less)
        public float groundFar = 18f;       // and far from them
        public int groundBudget = 1300000;
        public float splatShare = 0.06f;    // this share of them are big soft splats
        public Color groundColour = Color.clear;   // their colour (alpha 0: the scan colour)
        public float groundBrightness = 1f;
        public float groundTexMix = 0f;     // how much of the terrain textures' own colour the points keep (0..1)
        public float dust = 0f;             // dust motes per unit³ of the canyons' air along the ways
        public float dustHeight = 3.2f;     // how high above the floor it floats

        // replace: the dense points round the ant, made again on a worker thread as it walks (the shell)
        public float shellDensity = 0f;     // ground points per unit² round the ant (0: no shell)
        public float shellPlantArea = 0f;   // plant points round the ant for a plant one unit tall (× height²)
        public float shellReach = 15f;      // made this far round it
        public Vector2 shellFade = new Vector2(7f, 12f);   // drawn out to y, thinning out from x
        public float shellBrightness = 0.5f;               // each of its ground points, against a sparse one
        public float shellMove = 3f;        // made again once the ant is this far from where they were made
        public float nearPlantGain = 1f;    // a plant point's brightness where the shell is densest (it is dimmer there)

        // replace: the carpet, the floor round the ant as a dense grid of points made on the GPU
        // (Shaders/ParticleCarpet.shader) from a height map and a layer map of the terrain baked here
        public Material carpet;             // its material (null: no carpet)
        public float carpetStep = 0.013f;   // spacing of its splats (units)
        public Vector2 carpetFade = new Vector2(3.5f, 6f);   // drawn out to y round the ant, thinning out from x
        public float carpetGain = 0.22f;
        public float carpetSize = 0.0075f;

        // replace, solid plants: the walls round the ant as opaque splats, drawn with the plants' fine ones (SolidShell,
        // WallShell): the carpet lies on the floor only, and up close the walls' glowing points were a snowfall over the
        // smooth terrain (without them, 10-27, the bare terrain showed there)
        public float wallDensity = 0f;      // splats per unit² of wall right in front of the camera (0: none; the walls'
                                            // glowing points then give way round the ant as the floor's do)
        public float wallReach = 9f;        // made this far round the ant
        public float wallGain = 0.5f;       // their albedo against a ground point's colour (the solid shader lights them)
        public Vector2 wallFade = new Vector2(3.5f, 6f);   // the walls' glowing points give way to them out to y, from x

        // replace: ground cover, the small things on the floor at ant scale (moss tufts, lichen rosettes, fallen
        // leaves, trefoils, grit and pebbles, now and then a sprout with a bead), in patches; the trodden way clearer
        public float cover = 0f;            // things per unit² of floor over the whole nest (along the ways; fewer away)
        public float coverNear = 0f;        // and round the ant (the shell): many more
        public float coverBrightness = 1f;
        public int threads = 0;             // fungal threads, fine and pale, lying over the floor

        // replace: vines and roots creeping over the floor, as points (between the ways, not on them)
        public int vines = 0;
        public Color vineColour = new Color(0.35f, 0.6f, 0.78f), leafColour = new Color(0.55f, 0.95f, 0.85f), vineAccent = new Color(0.85f, 0.55f, 0.6f);
        public float shellCrossfade = 0.6f; // seconds a newly made shell takes to fade in over the old one

        // replace: colour. Plants take a pale tint by kind from bodyPalette (one family of near colours, a rare
        // warm one), their glowing parts one from glowPalette (now and then a warm accent); the ground a tint by
        // layer: floor, bank, plateau, and the bank's top edge (alpha 0: groundColour)
        public Color[] bodyPalette, glowPalette;   // 8 each (null: the scan colour only)
        public float bodyAccent = 0.06f;    // share of plants with a warm body
        public float glowAccent = 0.22f;    // share of plants with warm glowing parts
        public Color floorColour = Color.clear, bankColour = Color.clear, plateauColour = Color.clear, edgeColour = Color.clear;

        // replace: where the ant is led (the glowing tracks): plants and ground are densest near them
        public List<List<Vector2>> focus;
        public float focusReach = 7f;       // how fast the density falls off away from them (units)
        public float farShare = 0.3f;       // plants far from them keep this share of their points

        struct Point { public Vector3 pos; public uint col; }
        struct GroundTri { public int mesh, k; public float area, kind, x, z; }

        /// <summary>A plant as the shell sees it (copied on the main thread: workers cannot touch transforms).</summary>
        class PlantSlot
        {
            public Vector3[] pts;
            public uint[] cols;
            public uint palette;    // its tints (fourth colour byte, low six bits: glow << 3 | body)
            public bool relic;
            public float[] keep;
            public Matrix4x4 m;
            public Vector3 pos;
            public float height;
            public int baseEnd;     // where the sparse points stopped (the shell goes on from here)
            public int baseCount;
        }

        /// <summary>A terrain material's textures, read back from the GPU, with how the terrain shader maps them.</summary>
        class GroundLook
        {
            public Color32[] floor, bank, plateau;
            public Texture floorTex;
            public int fw, fh, bw, bh, pw, ph;
            public float floorScale, bankScale, plateauScale, hue, sat = 1, meanLum = 0.2f;
            public Color floorTint = Color.white, bankTint = Color.white, plateauTint = Color.white;
        }

        /// <summary>A small random sequence (xorshift), the same for the same seed: a triangle's points never change.</summary>
        struct Rng
        {
            uint s;
            public Rng(uint seed) { s = seed * 747796405u + 2891336453u; if (s == 0) s = 1; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }
        }

        // what a point is (the colour's fourth byte, top two bits; the low six: its size × 1/8, 0 = as it is)
        const uint Plant = 0, Dust = 1, Ground = 2, Glow = 3;
        const int UploadChunk = 262144;     // shell points uploaded per frame

        World world;
        Transform follow;
        Material mat;
        ComputeBuffer buffer;
        Point[] uploaded = new Point[0];
        int count;
        Vector3 gatheredAt;
        bool on;
        readonly List<Point> points = new List<Point>(1 << 20);
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly Dictionary<(PlantRef, float, float, Color, Color), uint[]> colours = new Dictionary<(PlantRef, float, float, Color, Color), uint[]>();
        readonly Dictionary<PlantRef, float[]> keeps = new Dictionary<PlantRef, float[]>();
        readonly List<PlantSlot> plants = new List<PlantSlot>();
        // the terrain's triangles in world space (replace), binned on a coarse grid for the shell
        readonly List<(Vector3[] v, Vector3[] n, Color[] c, int[] t, GroundLook look)> meshes = new List<(Vector3[], Vector3[], Color[], int[], GroundLook)>();
        GroundTri[] tris;
        readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
        readonly Dictionary<Material, GroundLook> looks = new Dictionary<Material, GroundLook>();
        const float CellSize = 4f;
        Color groundTint, floorTintL, bankTintL, plateauTintL, edgeTintL;
        // the carpet's maps and how it is drawn
        Texture2D heightMap, layerMap;
        Vector4 heightRect;
        MaterialPropertyBlock carpetBlock;
        int carpetN;
        // the shell: made on a worker thread, uploaded a chunk a frame into the back buffer, then faded in over the old
        readonly ComputeBuffer[] shellBuffers = new ComputeBuffer[2];
        readonly int[] shellCounts = new int[2];
        int shellFront, uploading = -1, uploadedSoFar, shellPrev = -1;
        float shellBlend = 1f;
        bool shellPending;
        readonly MaterialPropertyBlock prevBlock = new MaterialPropertyBlock();
        // the carpet's maps on the CPU too (vines grow over them)
        float[] heightArr;
        Color32[] layerArr;
        int mapW, mapH;
        float mapX0, mapZ0;
        const float MapCell = 0.125f;
        // flares: plants an antenna has lit, brighter among the points
        readonly Vector4[] flarePos = new Vector4[16], flareInfo = new Vector4[16];
        Vector2 shellAt;
        bool shellStarted;
        System.Threading.Tasks.Task shellTask;
        readonly List<Point> shellWork = new List<Point>(1 << 20);
        readonly MaterialPropertyBlock mainBlock = new MaterialPropertyBlock(), shellBlock = new MaterialPropertyBlock();
        float[] focusD2;   // squared distance to the nearest way, on a grid
        int fw, fh;
        float fx0, fz0;
        const float FocusCell = 1f;
        static readonly float[] Linear = MakeLinear();

        public bool On => on;
        public int Count => count;
        /// <summary>Replace: the carpet's maps are baked (HeightAt and LayerAt can be read).</summary>
        public bool HasMaps => heightArr != null;

        public void Init(World w, Transform ant, Material m)
        {
            world = w;
            follow = ant;
            mat = m;
            SetOn(false);
        }

        public void SetOn(bool value)
        {
            on = value && mat != null;
            if (mat != null)
            {
                mat.SetFloat("_Size", pointSize);
                mat.SetFloat("_Gain", brightness);
                mat.SetFloat("_GlowGain", glowBrightness);
                mat.SetFloat("_Shimmer", shimmer);
                mat.SetFloat("_MinPixels", mode == Mode.Replace ? 0.9f : 1.2f);
                mat.SetVector("_NearDim", new Vector4(shellFade.x, shellFade.y, mode == Mode.Replace && shellPlantArea > 0 ? nearPlantGain : 1f, 0));
                mat.SetVector("_CarpetFade", mode == Mode.Replace && carpet != null ? new Vector4(carpetFade.x, carpetFade.y, 0, 0) : Vector4.zero);
                Vector2 wf = wallDensity > 0 && solidPlants ? wallFade : carpetFade;
                mat.SetVector("_WallFade", mode == Mode.Replace && carpet != null ? new Vector4(wf.x, wf.y, 0, 0) : Vector4.zero);
                mat.SetFloat("_CoverGain", coverBrightness);
                bool pal = mode == Mode.Replace && bodyPalette != null && glowPalette != null && bodyPalette.Length >= 8 && glowPalette.Length >= 8;
                mat.SetFloat("_UsePalette", pal ? 1f : 0f);
                if (pal)
                {
                    var b = new Vector4[8];
                    var g = new Vector4[8];
                    for (int i = 0; i < 8; i++) { b[i] = bodyPalette[i].linear; g[i] = glowPalette[i].linear; }
                    mat.SetVectorArray("_BodyPal", b);
                    mat.SetVectorArray("_GlowPal", g);
                }
            }
            Shader.SetGlobalFloat("_PlantDim", on && mode == Mode.Overlay ? plantDim : 0f);
            Shader.SetGlobalFloat("_GroundDim", on ? groundDim : 0f);
            // replace: the meshes go; only their points are seen
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (on && mode == Mode.Replace)
                foreach (PlantInstance pi in world.plantInstances)
                    if (pi.t != null)
                        foreach (Renderer r in pi.t.GetComponentsInChildren<Renderer>())
                            if (r.enabled) { r.enabled = false; hidden.Add(r); }
            if (on) Gather();
        }

        void Update()
        {
            if (!on || world == null || follow == null) return;
            if (mode == Mode.Overlay && (follow.position - gatheredAt).sqrMagnitude > regatherAfter * regatherAfter) Gather();
            if (mode == Mode.Replace)
            {
                UpdateShell();
                // the plants an antenna has lit stand out among the points
                int nf = world.glow != null ? world.glow.Flares(flarePos, flareInfo) : 0;
                mat.SetFloat("_FlareCount", nf);
                if (nf > 0) { mat.SetVectorArray("_Flares", flarePos); mat.SetVectorArray("_FlareInfo", flareInfo); }
            }
            if (count == 0) return;
            var bounds = new Bounds(new Vector3((world.minX + world.maxX) / 2, 0, (world.minZ + world.maxZ) / 2),
                new Vector3(world.maxX - world.minX + 400, 200, world.maxZ - world.minZ + 400));
            mainBlock.SetBuffer("_Points", buffer);
            mainBlock.SetFloat("_Shell", 0f);
            Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, count * 3, 1, null, mainBlock, ShadowCastingMode.Off, false, 0);
            if (carpetN > 0)
            {
                Vector3 a = follow.position;
                Graphics.DrawProcedural(carpet, new Bounds(a, new Vector3(carpetFade.y * 2 + 4, 60, carpetFade.y * 2 + 4)), MeshTopology.Triangles,
                    carpetN * carpetN * 3, 1, null, carpetBlock, ShadowCastingMode.Off, false, 0);
            }
            if (shellPrev >= 0)
            {
                // a newly made shell fades in over the old one, so nothing pops
                shellBlend = Mathf.Min(1f, shellBlend + Time.deltaTime / Mathf.Max(0.05f, shellCrossfade));
                if (shellCounts[shellPrev] > 0) DrawShell(shellPrev, prevBlock, 1f - shellBlend, bounds);
                if (shellBlend >= 1f) shellPrev = -1;
            }
            if (shellCounts[shellFront] > 0) DrawShell(shellFront, shellBlock, shellPrev >= 0 ? shellBlend : 1f, bounds);
        }

        void DrawShell(int which, MaterialPropertyBlock block, float gain, Bounds bounds)
        {
            block.SetBuffer("_Points", shellBuffers[which]);
            block.SetFloat("_Shell", 1f);
            block.SetFloat("_ShellGain", gain);
            block.SetVector("_ShellFade", shellFade);
            Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, shellCounts[which] * 3, 1, null, block, ShadowCastingMode.Off, false, 0);
        }

        /// <summary>The shell: once the ant has walked shellMove from where it was made, it is made again round the
        /// ant on a worker thread; then uploaded a chunk a frame into the buffer not being drawn, and faded in over the
        /// old one (shellCrossfade); a new one waits until that is done.</summary>
        void UpdateShell()
        {
            if ((shellDensity <= 0 && shellPlantArea <= 0) || tris == null) return;
            if (shellTask != null && shellTask.IsCompleted)
            {
                if (shellTask.IsFaulted) Debug.LogException(shellTask.Exception);
                else shellPending = shellWork.Count > 0;
                shellTask = null;
            }
            if (shellPending && uploading < 0 && shellPrev < 0)
            {
                shellPending = false;
                uploading = 1 - shellFront;
                uploadedSoFar = 0;
                if (shellBuffers[uploading] == null || shellBuffers[uploading].count < shellWork.Count)
                {
                    shellBuffers[uploading]?.Release();
                    shellBuffers[uploading] = new ComputeBuffer(Mathf.NextPowerOfTwo(shellWork.Count), 16);
                }
            }
            if (uploading >= 0)
            {
                int n = Mathf.Min(UploadChunk, shellWork.Count - uploadedSoFar);
                if (n > 0) shellBuffers[uploading].SetData(shellWork, uploadedSoFar, uploadedSoFar, n);
                uploadedSoFar += n;
                if (uploadedSoFar < shellWork.Count) return;
                shellCounts[uploading] = shellWork.Count;
                shellPrev = shellFront;
                shellFront = uploading;
                shellBlend = 0f;
                uploading = -1;
                return;
            }
            if (shellTask != null || shellPending || shellPrev >= 0) return;
            var at = new Vector2(follow.position.x, follow.position.z);
            if (shellStarted && (at - shellAt).sqrMagnitude < shellMove * shellMove) return;
            shellStarted = true;
            shellAt = at;
            shellTask = System.Threading.Tasks.Task.Run(() => MakeShell(at));
        }

        /// <summary>The first shell, made at once (while the nest loads), so it is there from the first frame.</summary>
        void FirstShell()
        {
            if ((shellDensity <= 0 && shellPlantArea <= 0) || tris == null) return;
            var at = new Vector2(follow.position.x, follow.position.z);
            MakeShell(at);
            if (shellWork.Count == 0) return;
            shellBuffers[0]?.Release();
            shellBuffers[0] = new ComputeBuffer(Mathf.NextPowerOfTwo(shellWork.Count), 16);
            shellBuffers[0].SetData(shellWork, 0, 0, shellWork.Count);
            shellCounts[0] = shellWork.Count;
            shellFront = 0;
            shellPrev = -1;
            shellStarted = true;
            shellAt = at;
        }

        void OnDestroy()
        {
            buffer?.Release();
            foreach (ComputeBuffer b in shellBuffers) b?.Release();
            if (heightMap != null) Destroy(heightMap);
            Shader.SetGlobalVector("_FloorHeightRect", Vector4.zero);
            if (layerMap != null) Destroy(layerMap);
            Shader.SetGlobalFloat("_PlantDim", 0f);
            Shader.SetGlobalFloat("_GroundDim", 0f);
        }

        void Gather()
        {
            Vector3 c = follow.position;
            gatheredAt = c;
            points.Clear();
            plants.Clear();
            bool replace = mode == Mode.Replace;
            if (replace && focusD2 == null) BuildFocus();
            float r2 = replace ? float.MaxValue : radius * radius;
            // how many points each plant gets; in replace mode all of them share the budget
            var picks = new List<(PlantInstance pi, int n)>();
            long wanted = 0;
            foreach (PlantInstance pi in world.plantInstances)
            {
                if (replace && solidPlants) break;   // the plants are drawn solid (BuildSolidPlants)
                if (pi.t == null || pi.plant == null || !pi.plant.LoadPoints()) continue;
                if (!replace && pi.far) continue;
                Vector3 p0 = pi.t.position;
                float dx = p0.x - c.x, dz = p0.z - c.z;
                if (dx * dx + dz * dz > r2) continue;
                float h = pi.t.lossyScale.y;
                int n = replace ? Mathf.Clamp(Mathf.RoundToInt(perArea * h * h * (pi.relic ? 8f : Mathf.Lerp(farShare, 1f, Focus(p0.x, p0.z)))), pi.relic ? 20000 : 150, pi.far ? 12000 : pi.plant.points.Length)
                                : Mathf.Clamp(Mathf.RoundToInt(perUnit * Mathf.Pow(h, 1.6f)), 40, pi.plant.points.Length);
                picks.Add((pi, n));
                wanted += n;
            }
            float share = replace && wanted > budget ? (float)budget / wanted : 1f;
            int ord = 0;
            foreach (var (pi, n0) in picks)
            {
                int n = Mathf.Max(60, Mathf.RoundToInt(n0 * share));
                uint[] cols = Colours(pi);
                float[] keep = replace ? Keep(pi.plant) : null;
                Matrix4x4 m = pi.t.localToWorldMatrix;
                Vector3[] src = pi.plant.points;
                uint pal = replace ? (pi.relic ? 63u : Palette(pi, ord++, 0f)) << 24 : 0;   // 63: its own colours
                int taken = 0, k = 0;
                for (; k < src.Length && k < cols.Length && taken < n; k++)
                {
                    if (keep != null && PointHash(k) > keep[k]) continue;   // the texture's darker parts get fewer points
                    points.Add(new Point { pos = m.MultiplyPoint3x4(src[k]), col = cols[k] | pal });
                    taken++;
                }
                if (replace && !pi.far)
                    plants.Add(new PlantSlot { pts = src, cols = cols, palette = pal, relic = pi.relic, keep = keep, m = m, pos = pi.t.position, height = pi.t.lossyScale.y, baseEnd = k, baseCount = taken });
            }
            int plantPoints = points.Count;
            var rng = new System.Random(7);
            if (replace)
            {
                SampleGround();
                int groundPoints = points.Count - plantPoints;
                GrowVines();
                int vinePoints = points.Count - plantPoints - groundPoints;
                groundPoints += vinePoints;
                FloatDust(rng);
                Debug.Log($"[AntGarden] particle look (Replace): {plantPoints} plant points from {picks.Count} plants" + (share < 1 ? $" (each at {share:P0})" : "")
                    + $", {groundPoints} ground ({vinePoints} in vines), {points.Count - plantPoints - groundPoints} dust");
            }
            else
            {
                // the ground: a fine, slightly jittered grid of points over the floor, banks and plateau round the ant
                float step = groundStep, g2 = groundRadius * groundRadius;
                for (float x = c.x - groundRadius; x <= c.x + groundRadius; x += step)
                for (float z = c.z - groundRadius; z <= c.z + groundRadius; z += step)
                {
                    if ((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z) > g2) continue;
                    float px = x + (float)(rng.NextDouble() - 0.5) * step, pz = z + (float)(rng.NextDouble() - 0.5) * step;
                    if (world.HexDist(px, pz) > -0.3f) continue;
                    Region r = world.RegionAt(px, pz);
                    bool floor = world.Dist(px, pz) < 0;
                    Color g = Color.Lerp(r.biome.groundColor, r.biome.rimColor, 0.35f) * (floor ? 1.3f : 0.9f) * (0.6f + 0.8f * (float)rng.NextDouble());
                    points.Add(new Point { pos = new Vector3(px, world.WalkHeight(px, pz) + 0.03f, pz), col = Pack(g, Ground) });
                }
                Debug.Log($"[AntGarden] particle look (Overlay): {points.Count} points from {picks.Count} plants round ({c.x:F0}, {c.z:F0})");
            }
            count = points.Count;
            if (count == 0) return;
            if (buffer == null || buffer.count < count)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(Mathf.NextPowerOfTwo(count), 16);
            }
            if (uploaded.Length < count) uploaded = new Point[Mathf.NextPowerOfTwo(count)];
            points.CopyTo(uploaded);
            buffer.SetData(uploaded, 0, 0, count);
            if (replace) FirstShell();
        }

        /// <summary>
        /// Replace, solid plants: every plant's stored points as opaque lit splats (Shaders/ParticleSolid.shader, drawn by
        /// SolidThings with the floor's things), so the plants have the same grain as everything else: here the coarse
        /// set, a few splats per unit² of surface (more along the ways), flagged (negative radius) so the shader leaves
        /// them out round the ant, where the fine set (SolidShell) is drawn instead. Every plant is its own: bent,
        /// twisted and stretched a little its own way, in its own tints from the palette, keeping more or less of its
        /// texture's own colour, mottled in its own pattern; its crevices dark (the baked openness), its texture's
        /// light and dark kept and a little exaggerated; splats smaller where the detail is (the sampling was denser
        /// there). Returns how many it added.
        /// </summary>
        public int BuildSolidPlants(List<SolidPoint> into)
        {
            if (focusD2 == null) BuildFocus();
            solidSlots.Clear();
            glassLamps.Clear();
            orbitLamps.Clear();
            int start = into.Count;
            var picks = new List<(PlantInstance pi, float area, int n)>();
            var census = new Dictionary<string, int>();
            long wanted = 0, farWanted = 0;
            foreach (PlantInstance pi in world.plantInstances)
            {
                if (pi.t == null || pi.plant == null || !pi.plant.LoadPoints()) continue;
                float sc = pi.t.lossyScale.y;
                float area = (pi.plant.pointArea > 0 ? pi.plant.pointArea : 2.5f) * sc * sc;
                Vector3 p0 = pi.t.position;
                census[pi.plant.name] = census.TryGetValue(pi.plant.name, out int c) ? c + 1 : 1;
                if (pi.far)
                {
                    // a giant beyond the walls: as many as cover it at the size its height wants (FarSplats)
                    float fr = FarRadius(sc);
                    int nf = Mathf.Min(pi.plant.points.Length, Mathf.RoundToInt(0.385f * area / (fr * fr)));
                    picks.Add((pi, area, nf));
                    farWanted += nf;
                    continue;
                }
                float density = solidCoarse * (pi.relic ? 2f : Mathf.Lerp(farShare, 1f, Focus(p0.x, p0.z)));
                int n = Mathf.Clamp(Mathf.RoundToInt(area * density), 120, pi.plant.points.Length);
                picks.Add((pi, area, n));
                wanted += n;
            }
            float share = wanted > solidCoarseBudget ? (float)solidCoarseBudget / wanted : 1f;
            float farCut = farWanted > farBudget ? (float)farBudget / farWanted : 1f;
            var farJobs = new List<(SolidSlot slot, int n, float radius)>();
            int ord = 0;
            foreach (var (pi, area, n0) in picks)
            {
                SolidSlot slot = Slot(pi, area, ord++);
                if (pi.far) { farJobs.Add((slot, Mathf.Max(100, Mathf.RoundToInt(n0 * farCut)), FarRadius(pi.t.lossyScale.y))); continue; }
                int n = Mathf.Max(100, Mathf.RoundToInt(n0 * share));
                float radius = Mathf.Min(0.055f, 0.62f * Mathf.Sqrt(area / n));
                for (int k = 0; k < n && k < slot.pts.Length; k++)
                    into.Add(SolidSplat(slot, slot.order != null ? slot.order[k] : k, -radius));   // negative: coarse (left out round the ant)
                if (slot.core != null)
                {
                    for (int j = 0, m = Mathf.Min(slot.core.Length, n / 6); j < m; j++) into.Add(CoreSplat(slot, j, -radius));
                    Vector4 bulb = glassBulbs[pi.plant];
                    Vector3 mid = bulb, up = Vector3.up;
                    Shape(slot, ref mid, ref up);
                    glassLamps.Add((slot.m.MultiplyPoint3x4(mid), bulb.w * pi.t.lossyScale.y * slot.sxz, slot.glow));
                }
                if (!pi.far) solidSlots.Add(slot);
                // a light in the middle of a plant whose middle glows (a puffball's hub), seen through its rays and balls
                if (slot.fine && !pi.far && System.Array.IndexOf(heartLamps, pi.plant.name) >= 0 && spotHearts.TryGetValue(pi.plant, out Vector4 heart))
                {
                    Vector3 mid = heart, up = Vector3.up;
                    Shape(slot, ref mid, ref up);
                    // (wide and bright: inside the ball of rays most of it is hidden, it shows through the gaps)
                    float lampGain = NestTheme.Current?.heartLampGain ?? 1f;   // (as its nest wants it: NestTheme.heartLampGain)
                    glassLamps.Add((slot.m.MultiplyPoint3x4(mid), heart.w * 0.9f * pi.t.lossyScale.y * slot.sxz * (0.5f + 0.5f * lampGain), slot.glow * (1.5f * lampGain)));
                }
                // motes slowly circling a finely made plant, round its stem between a third and most of its height
                if (slot.fine && !pi.far && fineMotes > 0)
                {
                    float h = pi.t.lossyScale.y * slot.top * slot.sy;
                    var mr = new Rng((uint)(ord * 7919 + 17));
                    Vector3 foot = slot.m.MultiplyPoint3x4(Vector3.zero);
                    float moteSize = NestTheme.Current?.moteSize ?? 1f, moteGain = NestTheme.Current?.moteGain ?? 1f;   // (its nest's: NestTheme)
                    for (int q = 0; q < fineMotes; q++)
                        orbitLamps.Add((foot, h * (0.18f + 0.2f * mr.Next()), new Vector2(h * (0.3f + 0.2f * mr.Next()), h * (0.7f + 0.25f * mr.Next())),
                            (0.22f + 0.25f * mr.Next()) * (mr.Next() < 0.5f ? -1f : 1f), mr.Next() * 6.2832f, Mathf.Max(0.02f, h * 0.012f) * moteSize, slot.glow * moteGain));
                }
            }
            int nearMade = into.Count - start;
            if (farJobs.Count > 0) FarSplats(farJobs, into, farCut);
            var top = new List<KeyValuePair<string, int>>(census);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var line = new System.Text.StringBuilder();
            foreach (var kv in top) line.Append($" {kv.Key} {kv.Value},");
            Debug.Log($"[AntGarden] solid plants: {nearMade} coarse splats from {picks.Count - farJobs.Count} plants" + (share < 1 ? $" (each at {share:P0})" : "")
                + $", {into.Count - start - nearMade} for the {farJobs.Count} beyond the walls; kinds:{line}");
            // (checks) how much of each kind its markings cover: the share of its stored points well inside them
            var marks = new System.Text.StringBuilder();
            var seen = new HashSet<string>();
            foreach (SolidSlot sl in solidSlots)
            {
                if ((sl.bio == 0 && sl.tipsToo <= 0) || !seen.Add(sl.name)) continue;
                int on = 0, all = 0;
                for (int k = 0; k < sl.pts.Length; k += 11, all++)
                {
                    Color32 c = sl.raw[k];
                    float pale = Lum(new Color(Linear[c.r], Linear[c.g], Linear[c.b])) / sl.meanLum;
                    if (BioMark(sl, k, sl.pts[k], Mathf.Clamp01(sl.pts[k].y / sl.top), pale) > 0.5f) on++;
                }
                marks.Append($" {sl.name} {(float)on / Mathf.Max(1, all):P0}" + (sl.growth == null && (sl.bio == 2 || sl.bio == 5 || sl.bio == 6) ? " (no growth map: none)" : "") + ",");
            }
            Debug.Log($"[AntGarden] plant markings (of each kind's points):{marks}");
            return into.Count - start;
        }

        /// <summary>The splats' size for a giant beyond the walls h tall: splatFrac of its height, within splatMin..splatMax.</summary>
        float FarRadius(float h) => Mathf.Clamp(splatFrac * h, splatMin, splatMax);

        /// <summary>
        /// The giants beyond the walls (BuildSolidPlants' far ones), each made on a core of its own: splats of the size its
        /// height wants, each larger or smaller as the detail there is (as a plant's are) but within splatMin..splatMax, and
        /// as many more as cover what that took off; those on its sides that can never be seen from inside the nest (facing
        /// away from wherever the camera can be: Eyes) one in four, twice as large, so that from above it keeps its body.
        /// Ranked 127 with a negative radius: the shader neither thins them further off (they are as small as their
        /// distance wants already) nor leaves them out round the ant (they have no fine set).
        /// </summary>
        void FarSplats(List<(SolidSlot slot, int n, float radius)> jobs, List<SolidPoint> into, float cut)
        {
            Vector3[] eyes = Eyes();
            var made = new List<SolidPoint>[jobs.Count];
            int unseen = 0, bodyCount = 0;
            var cores = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Mathf.Max(1, System.Environment.ProcessorCount - 2) };
            System.Threading.Tasks.Parallel.For(0, jobs.Count, cores, j =>
            {
                var (slot, n0, r) = jobs[j];
                // (SolidSplat's own spread of sizes, capped: the cover it would have had over the cover it has)
                double was = 0, now = 0;
                for (int k = 0, probe = Mathf.Min(1024, slot.pts.Length); k < probe; k++)
                {
                    float v = (0.72f + 0.56f * PointHash(k)) / Mathf.Sqrt(Mathf.Clamp(slot.density != null ? slot.density[k] : 1f, 0.25f, 9f));
                    float vc = Mathf.Clamp(v * r, splatMin, splatMax) / r;
                    was += v * v;
                    now += vc * vc;
                }
                int n = Mathf.Min(slot.pts.Length, Mathf.RoundToInt(n0 * (float)(was / System.Math.Max(1e-6, now))));
                bool Seen(Vector3 at, Vector3 nn)
                {
                    if (eyes.Length == 0) return true;
                    foreach (Vector3 e in eyes)
                    {
                        Vector3 d = e - at;
                        if (Vector3.Dot(nn, d) > -0.2f * d.magnitude) return true;
                    }
                    return false;
                }
                var list = new List<SolidPoint>(n);
                int hid = 0;
                for (int k = 0; k < n; k++)
                {
                    SolidPoint sp = SolidSplat(slot, k, -r);
                    float rad = Mathf.Clamp(-sp.radius, splatMin, splatMax);
                    if (!Seen(sp.pos, ThingPen.UnpackNormal(sp.nrm)))
                    {
                        if (PointHash(k + 7717) >= 0.25f) continue;
                        rad *= 2f;
                        hid++;
                    }
                    sp.radius = -rad;
                    list.Add(ThingPen.WithRank(sp, 127));
                }
                // its body: few large splats a little inside its surface, all of one colour (its splats' mean, darkened), so
                // that between the fine ones it reads as a solid dark shape and not as blotches; on its seen sides only (the
                // others keep their one in four, twice as large)
                float rb = Mathf.Min(0.3f, farBody * r);
                int nb = Mathf.Min(slot.pts.Length, Mathf.RoundToInt(n / Mathf.Max(cut, 1e-3f) * (r * r) / (rb * rb)));
                Color mean = Color.black;
                int sampled = Mathf.Min(512, list.Count);
                for (int q = 0; q < sampled; q++)
                {
                    uint c = list[q].col;
                    mean += new Color(Linear[c & 255], Linear[(c >> 8) & 255], Linear[(c >> 16) & 255]);
                }
                Color body = sampled > 0 ? mean / sampled * farBodyDark : Color.black;
                int bodies = 0;
                for (int q = 0; q < nb; q++)
                {
                    SolidPoint sp = SolidSplat(slot, slot.pts.Length - 1 - q, -rb);   // (from the other end of its points: they are in no order)
                    Vector3 nn = ThingPen.UnpackNormal(sp.nrm);
                    if (!Seen(sp.pos, nn)) continue;
                    list.Add(ThingPen.WithRank(ThingPen.Splat(sp.pos - nn * (0.6f * rb), nn, body, -rb, 0f), 127));
                    bodies++;
                }
                made[j] = list;
                System.Threading.Interlocked.Add(ref unseen, hid);
                System.Threading.Interlocked.Add(ref bodyCount, bodies);
            });
            int count = 0;
            float rMin = float.MaxValue, rMax = 0f;
            foreach (var (_, _, r) in jobs) { rMin = Mathf.Min(rMin, r); rMax = Mathf.Max(rMax, r); }
            foreach (List<SolidPoint> list in made) { into.AddRange(list); count += list.Count; }
            Debug.Log($"[AntGarden] beyond the walls: {jobs.Count} giants, {count} splats of {rMin:F3}-{rMax:F3} (height × {splatFrac}, within {splatMin}-{splatMax})"
                + (cut < 1f ? $", each at {cut:P0} of its cover" : "") + $"; {unseen} of them on sides never seen from the nest (one in four, twice as large), {eyes.Length} eyes; "
                + $"{bodyCount} of them their dark body ({farBody}× as large, at most 0.3)");
        }

        /// <summary>Where the camera can be in the nest, for which sides of the giants beyond its walls can ever be seen:
        /// just inside its rim all round, half way in and in its middle, low and high over the floor. None if its middle is
        /// not in it (all their sides are then kept).</summary>
        Vector3[] Eyes()
        {
            var eyes = new List<Vector3>();
            float cx = (world.minX + world.maxX) / 2, cz = (world.minZ + world.maxZ) / 2;
            if (world.HexDist(cx, cz) >= -1f) return eyes.ToArray();
            float reach = Mathf.Max(world.maxX - world.minX, world.maxZ - world.minZ);
            void Eye(float x, float z)
            {
                float y = world.Height(x, z);
                eyes.Add(new Vector3(x, y + 0.3f, z));
                eyes.Add(new Vector3(x, y + 5f, z));
            }
            Eye(cx, cz);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12f, c = Mathf.Cos(a), s = Mathf.Sin(a), lo = 0f, hi = reach;
                for (int it = 0; it < 16; it++)
                {
                    float mid = (lo + hi) / 2;
                    if (world.HexDist(cx + c * mid, cz + s * mid) < -1f) lo = mid; else hi = mid;
                }
                Eye(cx + c * lo, cz + s * lo);
                Eye(cx + c * lo * 0.5f, cz + s * lo * 0.5f);
            }
            return eyes.ToArray();
        }

        /// <summary>A plant's own look and shape (from its place, so it is the same every time the nest is made).</summary>
        SolidSlot Slot(PlantInstance pi, float area, int ord)
        {
            PlantRef plant = pi.plant;
            if (!kindStats.TryGetValue(plant, out var st))
            {
                double sum = 0;
                float topY = 0.01f;
                int count = 0;
                for (int k = 0; k < plant.points.Length; k += 7)
                {
                    Color32 c = plant.pointColors[k];
                    if (c.a <= 127) { sum += Linear[c.r] * 0.3f + Linear[c.g] * 0.55f + Linear[c.b] * 0.15f; count++; }
                    topY = Mathf.Max(topY, plant.points[k].y);
                }
                st = (count > 0 ? Mathf.Max(1e-3f, (float)(sum / count)) : 0.2f, topY);
                kindStats[plant] = st;
            }
            Vector3 p0 = pi.t.position;
            var rng = new Rng((uint)Mathf.RoundToInt(p0.x * 73.1f) * 73856093u ^ (uint)Mathf.RoundToInt(p0.z * 91.7f) * 19349663u ^ (uint)ord);
            float R() => rng.Next();
            Color[] body = bodyPalette, glowPal = glowPalette;
            Vector2 keepRange = NestTheme.Current != null ? NestTheme.Current.plantKeep : new Vector2(0.5f, 0.8f);
            uint pal = pi.relic ? 63u : Palette(pi, ord, area);
            Color b0 = body != null && pal != 63 ? body[pal & 7] : Color.white;
            // the tip a neighbour of the base tint in the palette (or the same, lighter), each jittered
            Color b1 = body != null && pal != 63 ? body[(int)((pal & 7) + 1 + (int)(R() * 2.99f)) % 8] : Color.white;
            if (R() < 0.35f) b1 = Color.Lerp(b0, Color.white, 0.25f);
            if (NestTheme.Current != null && System.Array.IndexOf(NestTheme.Current.plainTips, plant.name) >= 0) b1 = b0;   // (NestTheme.plainTips)
            // (each plant its own: hue, saturation and value a little off its palette colour, as far as the theme says)
            float jit = NestTheme.Current != null ? NestTheme.Current.tintJitter : 1f;
            Color Jitter(Color c, float hueDeg, float v) => (HueShift(c.linear, hueDeg, 1f + (0.4f * R() - 0.15f) * jit) * (1f + (v - 1f) * jit));
            float hue = (R() - 0.5f) * 26f * jit;
            var slot = new SolidSlot
            {
                pts = plant.points, raw = plant.pointColors, nrms = plant.pointNormals, density = plant.pointDensity, open = plant.pointOpen,
                m = pi.t.localToWorldMatrix, pos = p0, reach = pi.t.lossyScale.y * 0.8f, area = area, top = st.top, meanLum = st.mean,
                rigid = pi.relic, name = plant.name, far = pi.far,
                building = pi.relic && NestTheme.Current != null && NestTheme.Current.buildingsGlowInParts,
                baseTint = Jitter(b0, hue, 0.8f + 0.3f * R()),
                tipTint = Jitter(b1, hue + (R() - 0.5f) * 20f, 0.9f + 0.35f * R()),
                glow = glowPal != null && pal != 63 ? HueShift(glowPal[(pal >> 3) & 7].linear, (R() - 0.5f) * 30f, 1f) : Color.white,
                keepOwn = pi.relic ? 0.8f : Mathf.Lerp(keepRange.x, keepRange.y, R()),   // mostly its own colours (their detail), the nest's palette over them
                contrast = 1.1f + 0.25f * R(),
                mottle = 0.06f + 0.16f * R(), mottleScale = 2.5f + 7f * R(), mottleSeed = R() * 97f,
                band = R() < 0.3f ? 0.1f + 0.15f * R() : 0f, bandFreq = 8f + 22f * R(),
                glowGain = 0.75f + 0.5f * R(),
                faint = pi.relic ? 0f : 0.1f + 0.14f * R(),
                bendDir = new Vector2(Mathf.Cos(R() * 6.283f), 0), bend = 0, twist = 0, sy = 1, sxz = 1,
            };
            if (!pi.relic && !pi.far)
            {
                float a = R() * 6.283f;
                slot.bendDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                slot.bend = 0.06f + 0.32f * R() * R();
                slot.twist = (R() - 0.5f) * 1.6f;
                slot.sy = 0.82f + 0.4f * R();
                slot.sxz = 0.85f + 0.3f * R();
            }
            if (glowFrom.TryGetValue(plant.name, out float from))
            {
                slot.glowFrom = from;
                slot.band = 0;   // no bands across a glowing head: it grows out of its stick in one sweep
            }
            int sunkAt = NestTheme.Current != null && !pi.relic ? System.Array.IndexOf(NestTheme.Current.sunkKinds, plant.name) : -1;
            if (sunkAt >= 0) slot.sunk = NestTheme.Current.sunkBy[sunkAt];
            if (!pi.relic && System.Array.IndexOf(finePlants, plant.name) >= 0)
            {
                slot.fine = true;
                slot.band = 0;   // (its own pattern is finer than any band)
                slot.spots = Spots(plant, st.top);
                if (NestTheme.Current?.kindFoot != null && NestTheme.Current.kindFoot.TryGetValue(plant.name, out float foot)) slot.foot = foot;
            }
            if ((!pi.relic || slot.building) && NestTheme.Current != null)
                foreach (NestTheme.GlowPart part in NestTheme.Current.glowParts)
                    if (part.plant == plant.name) { slot.partFrom = part.from; slot.partTo = part.to; slot.partRel = part.lumMin; slot.partAmount = part.amount; slot.partRed = part.red; slot.partHot = part.hot; }
            // (a relic glowing where it is red, the kiln's: its thin cracks' points first as it thins, EmberOrder)
            if (slot.building && slot.partRed > 0f) slot.order = EmberOrder(plant, slot.partRed);
            // a plant of its region's own kinds keeps its colours (the backdrop's are turned whatever they are)
            bool native = GameConfig.InList(world.RegionAt(p0.x, p0.z).biome.nativePlants, plant.name);
            slot.ownHue = native ? 0 : pi.hue;
            slot.ownSat = native ? 1 : pi.sat;
            // (a kind whose own colour a nest mutes: NestTheme.kindSat)
            if (NestTheme.Current?.kindSat != null && NestTheme.Current.kindSat.TryGetValue(plant.name, out float kindSat)) slot.ownSat *= kindSat;
            if (NestTheme.Current?.kindGain != null && NestTheme.Current.kindGain.TryGetValue(plant.name, out float kindGain)) slot.gain = kindGain;   // (and darkens: NestTheme.kindGain)
            // as light, the palette's pale glow colours read white: deeper, and toward the nest's teal
            slot.glowDeep = Color.Lerp(HueShift(slot.glow, 0f, 1.9f), (NestTheme.Current ?? NestTheme.Cyan).glowDeep, slot.glowFrom > 0 ? 0.55f : 0.3f);
            if (pi.relic && NestTheme.Current != null && NestTheme.Current.relicGlow.a > 0f) slot.glow = slot.glowDeep = NestTheme.Current.relicGlow;   // (a nest's own: NestTheme.relicGlow)
            // its markings: its kind's, a little stronger or weaker on this plant (now and then hardly lit), in its light's
            // colour a little deepened, as light as bioAlbedo
            if (!pi.relic && !pi.far)
            {
                Vector4 b = NestTheme.Current?.bio != null && NestTheme.Current.bio.TryGetValue(plant.name, out Vector4 own) ? own : Bio(plant.name);   // (its nest's own: NestTheme.bio)
                uint kh = KindHash(plant.name);
                slot.bio = (int)b.x;
                slot.bioSize = b.y;
                slot.bioWidth = b.z;
                slot.bioAmt = b.w * (R() < 0.15f ? 0.3f : 0.75f + 0.25f * R());
                slot.bioSeed = (kh & 0xFF) / 255f;
                slot.faint *= NestTheme.Current?.faintGain ?? 1f;   // (its nest's: a pale head lit whole by it reads as one blob)
                slot.growth = plant.pointGrowth;
                slot.tip = plant.pointTip;
                slot.stem = plant.pointStem;
                slot.tipsToo = BioTips.TryGetValue(plant.name, out float tt) ? tt : 0f;
                if (slot.bio == 4)
                {
                    Vector2 cut = PaleCut(plant, st.mean, b.y);
                    slot.bioLo = cut.x;
                    slot.bioHi = Mathf.Max(cut.y, cut.x + 0.05f);
                }
                Color bc = Color.Lerp(slot.glow, slot.glowDeep, NestTheme.Current?.markDeep ?? 0.55f);   // (as deep as its nest wants: NestTheme)
                if (NestTheme.Current != null && NestTheme.Current.markColor.a > 0f) bc = NestTheme.Current.markColor.linear;   // (or its own colour)
                if (NestTheme.Current?.kindMark != null && NestTheme.Current.kindMark.TryGetValue(plant.name, out Color km)) bc = km;   // (or its kind's: NestTheme.kindMark)
                bc *= bioAlbedo / Mathf.Max(0.05f, Lum(bc));
                float mx = Mathf.Max(bc.r, Mathf.Max(bc.g, bc.b));
                slot.bioCol = mx > 1f ? bc * (1f / mx) : bc;
            }
            if (!pi.relic && glassKinds != null && System.Array.IndexOf(glassKinds, plant.name) >= 0)
            {
                slot.glass = true;
                slot.core = GlassCore(plant);
            }
            if (!pi.relic && crystalKinds != null && System.Array.IndexOf(crystalKinds, plant.name) >= 0)
            {
                // a crystal: straight (neither bent nor twisted: its faces stay flat), pale in the nest's colour, its ridges
                // near white leaning to its light; never glass, though a nest's theme names it so (no shell, no light inside)
                slot.crystal = true;
                slot.glass = false;
                slot.core = null;
                slot.bend = slot.twist = 0f;
                slot.sy = slot.sxz = 1f;
                slot.ridge = plant.pointRidge;
                slot.order = CrystalOrder(plant);
                slot.crystalTint = Color.Lerp(slot.tipTint, Color.white, 0.5f);
                slot.crystalEdge = Color.Lerp(Color.white, slot.glow, 0.3f) * 0.95f;
                slot.crystalLight = Color.Lerp(slot.glow, slot.glowDeep, 0.3f);
                Vector3 st2 = CrystalStats(plant);
                slot.crystalLum = st2.x;
                slot.crystalChroma = new Vector2(st2.y, st2.z);
            }
            return slot;
        }

        readonly Dictionary<PlantRef, Vector3> crystalStats = new Dictionary<PlantRef, Vector3>();
        readonly Dictionary<PlantRef, int[]> crystalOrders = new Dictionary<PlantRef, int[]>();

        /// <summary>A crystal's points in the order its splats take them (a plant draws its first so many, as many as its
        /// distance wants): its ridges' and points' points crystalRidgeShare times as often as its faces', so however it
        /// is thinned its ridges stay lines (in their stored order, which is random, thinned they broke into dots). The
        /// same for every crystal of a kind; null without a ridge map.</summary>
        int[] CrystalOrder(PlantRef plant)
        {
            if (crystalOrders.TryGetValue(plant, out int[] order)) return order;
            if (plant.pointRidge == null) return crystalOrders[plant] = null;
            int n = plant.points.Length;
            var ridge = new List<int>();
            var face = new List<int>();
            for (int k = 0; k < n; k++)
                (plant.pointRidge[k] > 128 ? ridge : face).Add(k);
            // after each face's point, the ridges' at crystalRidgeShare times their share, till they run out
            order = new int[n];
            float per = crystalRidgeShare * ridge.Count / Mathf.Max(1, face.Count), owe = 0f;
            int w = 0, ri = 0;
            foreach (int f in face)
            {
                order[w++] = f;
                for (owe += per; owe >= 1f && ri < ridge.Count; owe -= 1f) order[w++] = ridge[ri++];
            }
            while (ri < ridge.Count) order[w++] = ridge[ri++];
            return crystalOrders[plant] = order;
        }

        readonly Dictionary<PlantRef, int[]> emberOrders = new Dictionary<PlantRef, int[]>();

        /// <summary>A relic's points in the order its splats take them, as CrystalOrder's: those where it is red (a glow
        /// part's red, NestTheme.GlowPart: the embers in the kiln's charred log's and temple's cracks) emberShare times as
        /// often as the rest, so thinned further off its thin cracks still glow (in their stored order they thinned away
        /// with the charcoal round them: from the way, a tenth of its points drawn, none red, k6). The same for every
        /// relic of a kind.</summary>
        int[] EmberOrder(PlantRef plant, float red)
        {
            if (emberOrders.TryGetValue(plant, out int[] order)) return order;
            int n = plant.points.Length;
            var ember = new List<int>();
            var rest = new List<int>();
            for (int k = 0; k < n; k++)
            {
                Color32 c = plant.pointColors[k];
                var lin = new Color(Linear[c.r], Linear[c.g], Linear[c.b]);
                bool hot = Lum(lin) > 0.03f && (lin.r - Mathf.Max(lin.g, lin.b)) / Mathf.Max(1e-3f, lin.r) > red;
                (hot ? ember : rest).Add(k);
            }
            // after each of the rest's points, the embers' at emberShare times their share, till they run out
            order = new int[n];
            float per = emberShare * ember.Count / Mathf.Max(1, rest.Count), owe = 0f;
            int w = 0, ei = 0;
            foreach (int f in rest)
            {
                order[w++] = f;
                for (owe += per; owe >= 1f && ei < ember.Count; owe -= 1f) order[w++] = ember[ei++];
            }
            while (ei < ember.Count) order[w++] = ember[ei++];
            Debug.Log($"[AntGarden] {plant.name}: {ember.Count} of {n} points embers ({100f * ember.Count / Mathf.Max(1, n):F1} %), drawn {emberShare}× as often as it thins");
            return emberOrders[plant] = order;
        }

        /// <summary>A crystal's texture where it is all crystal (above the rock it grows from, a third of its height up): its
        /// mean lightness, and how coloured it is at the 80th and the 97th hundredth of its points (its patches of colour:
        /// lavender, green).</summary>
        Vector3 CrystalStats(PlantRef plant)
        {
            if (crystalStats.TryGetValue(plant, out Vector3 st)) return st;
            var chroma = new List<float>();
            double sum = 0;
            float top = 0.01f;
            for (int k = 0; k < plant.points.Length; k += 5) top = Mathf.Max(top, plant.points[k].y);
            for (int k = 0; k < plant.points.Length; k += 5)
            {
                Color32 c = plant.pointColors[k];
                if (plant.points[k].y < 0.33f * top) continue;
                var lin = new Color(Linear[c.r], Linear[c.g], Linear[c.b]);
                float lum = Mathf.Max(1e-4f, Lum(lin));
                sum += lum;
                Color own = lin * (1f / lum);
                chroma.Add(Mathf.Max(own.r, Mathf.Max(own.g, own.b)) - Mathf.Min(own.r, Mathf.Min(own.g, own.b)));
            }
            if (chroma.Count < 20) return crystalStats[plant] = new Vector3(0.4f, 0.2f, 0.4f);
            chroma.Sort();
            float lo = chroma[(int)(chroma.Count * 0.5f)], hi = chroma[(int)(chroma.Count * 0.99f)];   // (80th to 97th: hard-edged blocks of colour; from the 65th, the green hardly showed)
            return crystalStats[plant] = new Vector3((float)(sum / chroma.Count), lo, Mathf.Max(hi, lo + 0.02f));
        }

        /// <summary>
        /// A glass plant's light: splats inside its glowing part, packed towards the middle of it (the glowing points'
        /// centre, out to about half their mean distance from it). The same for every plant of a kind.
        /// </summary>
        Vector4[] GlassCore(PlantRef plant)
        {
            if (glassCores.TryGetValue(plant, out var core)) return core;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int k = 0; k < plant.points.Length; k += 3)
                if (plant.pointColors[k].a > 127) { sum += plant.points[k]; n++; }
            if (n < 20 || glassCore <= 0) return glassCores[plant] = null;
            Vector3 c = sum / n;
            float rad = 0;
            for (int k = 0; k < plant.points.Length; k += 3)
                if (plant.pointColors[k].a > 127) rad += (plant.points[k] - c).magnitude;
            rad = 0.55f * rad / n;
            glassBulbs[plant] = new Vector4(c.x, c.y, c.z, rad);
            core = new Vector4[glassCore];
            var rng = new Rng(4099u);
            for (int j = 0; j < core.Length;)
            {
                var d = new Vector3(rng.Next() * 2 - 1, rng.Next() * 2 - 1, rng.Next() * 2 - 1);
                float d2 = d.sqrMagnitude;
                if (d2 > 1 || d2 < 1e-4f) continue;
                float f = Mathf.Pow(rng.Next(), 0.9f);   // evenly out from the middle: denser there
                Vector3 q = c + d / Mathf.Sqrt(d2) * (rad * f);
                core[j++] = new Vector4(q.x, q.y, q.z, f);
            }
            return glassCores[plant] = core;
        }

        /// <summary>
        /// Replace, solid plants: the fine set for the plants round (x, z) (as far as reach and their own size), many
        /// splats per unit², small; into is cleared first. Plain arithmetic on arrays only: a worker thread may run it.
        /// </summary>
        /// <summary>Checks: the plant nearest p as the fine splats draw it seen from cam (how big it is, how many splats it
        /// has there, how large each is on the screen).</summary>
        public string DescribeNear(Vector3 p, Vector3 cam, float fovDeg, int screenH)
        {
            SolidSlot best = null;
            float bd = 2.5f * 2.5f;
            foreach (SolidSlot s in solidSlots)
            {
                float d = (new Vector2(s.pos.x - p.x, s.pos.z - p.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null) return "no plant near";
            float height = best.reach / 0.8f, dist = Vector3.Distance(cam, best.pos + Vector3.up * height * 0.5f);
            int full = ShellFull(best, out float r);
            float want = Mathf.Clamp(nearDist * nearDist / (dist * dist), Mathf.Clamp01(solidFine / solidNear), 1f);
            if (best.building && NestTheme.Current != null) want = Mathf.Max(want, NestTheme.Current.relicKeep);   // (NestTheme.relicKeep)
            float px = 2f * dist * Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad) / screenH;
            int split = want > 0.2f && dist < nearDist * 1.6f ? Mathf.Clamp(Mathf.CeilToInt(best.area * solidNear / full - 0.25f), 1, splitMax) : 1;
            if (split > 1) r *= 1.15f / Mathf.Sqrt(split);
            return $"{best.name}: height {height:F2}, surface {best.area:F1} u², {best.pts.Length} points, {full} splats at full" + (split > 1 ? $" (each split in {split} up close)" : "")
                + $", {want:P0} kept at {dist:F1} from the camera, dab {2 * r / Mathf.Sqrt(want) / px:F1} px across" + (best.fine ? ", fine" : "");
        }

        /// <summary>How many fine splats a plant has right in front of the camera (its share of them further off goes by
        /// rank), and how big each is: as many as its surface wants, at most all its stored points.</summary>
        int ShellFull(SolidSlot s, out float radius)
        {
            int full = Mathf.Max(1, Mathf.Min(Mathf.Min(solidNearCap, s.pts.Length), Mathf.RoundToInt(s.area * solidNear * (s.crystal ? crystalDensity : 1f))));
            radius = Mathf.Max(0.0018f, 0.62f * Mathf.Sqrt(s.area / full));
            return full;
        }

        /// <summary>The fine splats of the plants round at (into, grown as needed; returns how many), made plant by plant
        /// on all the cores.</summary>
        public int SolidShell(Vector2 at, float reach, ref SolidPoint[] into, Vector3 cam, float slack)
        {
            float floor = Mathf.Clamp01(solidFine / solidNear);
            var jobs = new List<(SolidSlot slot, int full, int n, float radius, int coreFull, int m, int at, int split)>();
            int total = 0;
            foreach (SolidSlot slot in solidSlots)
            {
                float dx = slot.pos.x - at.x, dz = slot.pos.z - at.y, r = reach + slot.reach;
                if (dx * dx + dz * dz > r * r) continue;
                // as many splats as the nearest the camera may come to it before the shell is made again wants; each
                // frame the shader keeps those its distance then wants (by their rank), the rest a little larger, so
                // near the camera a plant shows its texture's detail and its form, further off it thins smoothly
                float cx = slot.pos.x - cam.x, cz = slot.pos.z - cam.z;
                float dc = Mathf.Max(0.4f, Mathf.Sqrt(cx * cx + cz * cz) - slot.reach * 0.5f - slack);
                float want = Mathf.Clamp(nearDist * nearDist / (dc * dc), floor, 1f);
                int full = ShellFull(slot, out float radius);
                // a nest's relics kept denser further off (NestTheme.relicKeep: the kiln's stump temple, 6 high and seen
                // from 9, drawn with a tenth of its points, showed neither its embers' cracks nor its carved galleries,
                // k6-k7): as many made, and the shader keeps that share of a building's however far (_NearDensity.z)
                if (slot.building && NestTheme.Current != null) want = Mathf.Max(want, NestTheme.Current.relicKeep);
                int n = Mathf.Min(full, Mathf.CeilToInt(full * want));
                // a big plant whose stored points are too few for that (a giant mushroom, a boulder): near the camera
                // each of them is drawn as several smaller ones, so its grain is as fine as what stands beside it
                int split = want > 0.2f ? Mathf.Clamp(Mathf.CeilToInt(slot.area * solidNear / full - 0.25f), 1, splitMax) : 1;
                // the light inside a glass plant: ranked as its shell is
                int coreFull = slot.core != null ? Mathf.Min(slot.core.Length, Mathf.Max(1, full / 3)) : 0;
                int m = slot.core != null ? Mathf.Min(coreFull, Mathf.CeilToInt(coreFull * want)) : 0;
                jobs.Add((slot, full, n, radius, coreFull, m, total, split));
                total += n * split + m;   // (the most it may make: only those near the camera are split)
            }
            if (into == null || into.Length < total) into = new SolidPoint[Mathf.NextPowerOfTwo(Mathf.Max(1024, total))];
            SolidPoint[] arr = into;
            var made = new int[jobs.Count];
            float splitReach = nearDist * 1.6f + slack;
            // (on all but four cores: the main and render threads keep theirs, or frames run late while a big shell is made)
            var cores = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Mathf.Max(1, System.Environment.ProcessorCount - 4) };
            System.Threading.Tasks.Parallel.For(0, jobs.Count, cores, j =>
            {
                var (slot, full, n, radius, coreFull, m, o, split) = jobs[j];
                int w = o;
                float near2 = split > 1 ? splitReach * splitReach : -1f, shrink = 1.15f / Mathf.Sqrt(split);
                for (int k = 0; k < n; k++)
                {
                    int rank = k * 128 / full;
                    SolidPoint sp = SolidSplat(slot, slot.order != null ? slot.order[k] : k, radius);
                    if ((sp.pos - cam).sqrMagnitude >= near2) { arr[w++] = ThingPen.WithRank(sp, rank); continue; }
                    Vector3 nn = ThingPen.UnpackNormal(sp.nrm);
                    NestThingsGrowth.Perp(nn, out Vector3 u, out Vector3 v);
                    uint flag = sp.nrm & (1u << 31);
                    for (int c = 0; c < split; c++)
                    {
                        float h1 = PointHash(k * 7 + c * 131 + 17), h2 = PointHash(k * 7 + c * 131 + 29), h3 = PointHash(k * 7 + c * 131 + 41);
                        float a = h1 * 6.2831853f, d = sp.radius * (c == 0 ? 0.25f * h2 : 0.9f * Mathf.Sqrt(h2));
                        SolidPoint q = sp;
                        q.pos = sp.pos + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * d;
                        q.nrm = ThingPen.PackNormal(nn + (u * (h3 * 2f - 1f) + v * (h1 * 2f - 1f)) * 0.12f) | flag;
                        q.radius = sp.radius * shrink * (0.85f + 0.3f * h3);
                        arr[w++] = ThingPen.WithRank(q, rank);
                    }
                }
                for (int c = 0; c < m; c++) arr[w++] = ThingPen.WithRank(CoreSplat(slot, c, radius), c * 128 / coreFull);
                made[j] = w - o;
            });
            // close up the room left for splits that were not needed
            int end = 0;
            for (int j = 0; j < jobs.Count; j++)
            {
                if (jobs[j].at != end) System.Array.Copy(arr, jobs[j].at, arr, end, made[j]);
                end += made[j];
            }
            if (wallDensity > 0 && tris != null) end = WallShell(at, cam, slack, ref into, end);
            return end;
        }

        readonly List<int> wallTris = new List<int>(1 << 14);
        readonly List<SolidPoint>[] wallParts = new List<SolidPoint>[12];

        /// <summary>The walls round at as opaque splats, and anything steep (the carpet lies on the floor only, and thins
        /// on a slope; Emit's points there give way to these round the ant), appended to into from end; returns the new
        /// end. Made on all but four cores, a share of the triangles each (into lists kept from one shell to the next:
        /// nothing allocated, no collection mid-walk).</summary>
        int WallShell(Vector2 at, Vector3 cam, float slack, ref SolidPoint[] into, int end)
        {
            wallTris.Clear();
            float reach2 = wallReach * wallReach;
            int r = Mathf.CeilToInt(wallReach / CellSize);
            int cx = Mathf.FloorToInt(at.x / CellSize), cz = Mathf.FloorToInt(at.y / CellSize);
            for (int z = cz - r; z <= cz + r; z++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!cells.TryGetValue(CellKey(x, z), out var bin)) continue;
                foreach (int i in bin)
                {
                    float dx = tris[i].x - at.x, dz = tris[i].z - at.y;
                    if (dx * dx + dz * dz > reach2) continue;
                    var (_, nr, c, t, _) = meshes[tris[i].mesh];
                    int ia = t[tris[i].k], ib = t[tris[i].k + 1], ic = t[tris[i].k + 2];
                    // all floor and flat: the carpet's
                    if (Mathf.Min(c[ia].r, Mathf.Min(c[ib].r, c[ic].r)) > 0.75f && Mathf.Min(nr[ia].y, Mathf.Min(nr[ib].y, nr[ic].y)) > 0.7f) continue;
                    wallTris.Add(i);
                }
            }
            for (int j = 0; j < wallParts.Length; j++) wallParts[j] ??= new List<SolidPoint>(1 << 15);
            var cores = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Mathf.Max(1, System.Environment.ProcessorCount - 4) };
            System.Threading.Tasks.Parallel.For(0, wallParts.Length, cores, j =>
            {
                List<SolidPoint> part = wallParts[j];
                part.Clear();
                for (int q = j; q < wallTris.Count; q += wallParts.Length) WallTri(wallTris[q], at, cam, slack, part);
            });
            int total = end;
            foreach (List<SolidPoint> part in wallParts) total += part.Count;
            if (into.Length < total)
            {
                var bigger = new SolidPoint[Mathf.NextPowerOfTwo(total)];
                System.Array.Copy(into, bigger, end);
                into = bigger;
            }
            foreach (List<SolidPoint> part in wallParts)
            {
                part.CopyTo(into, end);
                end += part.Count;
            }
            return end;
        }

        /// <summary>A wall triangle's opaque splats: as many as the nearest the camera may come wants (a share of the
        /// slack only: the walls are wide), ranked as a plant's fine splats are, so the shader keeps those their distance
        /// wants; thinning out over the last two units of wallReach; coloured as the ground's points are (wallGain), the
        /// texture's darkest parts left as gaps (fewer than the carpet's); the ground's own (they stand still, no rim, no
        /// glints: ThingPen.Terrain).</summary>
        void WallTri(int i, Vector2 at, Vector3 cam, float slack, List<SolidPoint> into)
        {
            GroundTri g = tris[i];
            var (v, nr, c, t, look) = meshes[g.mesh];
            int ia = t[g.k], ib = t[g.k + 1], ic = t[g.k + 2];
            // (a little larger than covering: the dark terrain showed through as black holes)
            float floor = Mathf.Clamp01(solidFine / solidNear), radius = 0.78f / Mathf.Sqrt(wallDensity);
            float dc = Mathf.Max(0.4f, Vector3.Distance((v[ia] + v[ib] + v[ic]) / 3f, cam) - Mathf.Sqrt(g.area) - slack * 0.5f);
            float full = g.area * wallDensity, many = full * Mathf.Clamp(nearDist * nearDist / (dc * dc), floor, 1f);
            var rng = new Rng((uint)i * 3266489917u ^ 0x68e31da4u);
            int n = (int)many;
            if (rng.Next() < many - n) n++;
            bool textured = look != null && look.floor != null;
            for (int k = 0; k < n; k++)
            {
                float u = rng.Next(), w = rng.Next();
                if (u + w > 1) { u = 1 - u; w = 1 - w; }
                float s = 1 - u - w;
                float roll = rng.Next(), gap = rng.Next(), jitter = rng.Next(), edge = rng.Next();
                float gN = (rng.Next() + rng.Next() + rng.Next() - 1.5f) * 1.6f;
                Vector3 p = v[ia] * s + v[ib] * u + v[ic] * w;
                float ex = p.x - at.x, ez = p.z - at.y;
                if (ex * ex + ez * ez > (wallReach - 2f * edge) * (wallReach - 2f * edge)) continue;
                Vector3 nn = (nr[ia] * s + nr[ib] * u + nr[ic] * w).normalized;
                Color lw = c[ia] * s + c[ib] * u + c[ic] * w;   // floor, bank, plateau, open
                if (roll < Smooth(0.25f, 0.75f, lw.r) * Smooth(0.45f, 0.7f, nn.y)) continue;   // the carpet's
                float lum = (0.3f * lw.r + 0.75f * lw.g + 0.32f * lw.b + 1.4f * lw.g * lw.b) * (0.55f + 0.45f * lw.a);
                Color col;
                if (textured)
                {
                    Color albedo = Albedo(look, p, nn, lw);
                    float al = Lum(albedo), rel = al / look.meanLum;
                    if (gap > Mathf.Lerp(1f, Mathf.Clamp(0.6f + 0.4f * rel * Mathf.Sqrt(rel), 0.05f, 1f), grain)) continue;
                    lum *= (0.85f + 0.3f * jitter) * Mathf.Lerp(1f, Mathf.Clamp(0.08f + 0.92f * rel * rel, 0.04f, 3.2f), grain);
                    // (as much of the texture's own colour as the ground's points: more, and leafcut's walls turned from
                    // its 119° to 85-102°, the textures' own khaki and brown)
                    col = Color.Lerp(FloorPatches(LayerTint(lw), p, lw.r) * lum, albedo * (lum / Mathf.Max(1e-4f, al)), groundTexMix);
                }
                else col = LayerTint(lw) * (lum * (0.6f + 0.8f * jitter));
                float rad = radius * Mathf.Exp(gN * 0.42f);
                into.Add(ThingPen.WithRank(ThingPen.Terrain(ThingPen.Splat(p + nn * 0.012f, nn, col * wallGain, rad, 0f)), (int)(k * 128 / Mathf.Max(1f, full))));
            }
        }

        /// <summary>
        /// Splat k of a plant: where its point is (bent, twisted and stretched as this plant is), which way it faces, its
        /// colour (the texture's light and dark against the plant's own mean, a little exaggerated; some of its own colour,
        /// the rest this plant's tints from base to tip; mottled; dark in its crevices) and its radius (smaller where the
        /// sampling was denser; a little varied). Plain arithmetic only.
        /// </summary>
        SolidPoint SolidSplat(SolidSlot s, int k, float radius)
        {
            Color32 c = s.raw[k];
            var lin = new Color(Linear[c.r], Linear[c.g], Linear[c.b]);
            Vector3 p = s.pts[k];
            Vector3 n = s.nrms != null ? ThingPen.DecodeNormal(s.nrms[k]) : Vector3.up;
            float y01 = Mathf.Clamp01(p.y / s.top);
            // how far along its growth (its growth map: along its surface from its foot), for its gradients from foot to
            // tip, soft and following its form as the references' do (by height they cut it in bands); its height without one
            float g01 = s.growth != null ? s.growth[k] : y01;
            Shape(s, ref p, ref n);
            // a plant on a lump of its own soil (the tiny mushrooms' pot, a hard-edged saucer on the floor): the lump
            // under the floor, which hides it, the rest lowered to rise out of it
            if (s.sunk > 0f) p.y -= (y01 < s.sunk + 0.03f ? 1f + s.sunk : s.sunk) * s.top * s.sy;
            float open = s.open != null ? s.open[k] / 255f : 1f;
            float shade = 0.3f + 0.7f * Mathf.Pow(open, 1.1f);
            // the texture's light and dark against the plant's own mean (a little exaggerated), some of its own colour,
            // the rest this plant's tints from base to tip; its glowing parts lean to its glow colour but keep the
            // pattern, and glow most where the texture is lightest (veins, rims)
            float lum = Mathf.Max(1e-4f, Lum(lin));
            if (s.crystal) return CrystalSplat(s, k, p, n, lin, lum, g01, y01, shade, radius);
            // (a plant whose head glows: its texture's light and dark softened, so its pale head and dark stalk do not
            // read as two parts)
            float rel = Mathf.Clamp(Mathf.Pow(lum / s.meanLum, s.glowFrom > 0 ? s.contrast * 0.8f : s.contrast), 0.05f, 3.2f);
            // how much of the glowing part this point is: its texture's glowing parts; a plant whose head alone glows
            // grows into it over a soft band round where the head begins, not at a line; and a part glowing only in
            // this nest (a mushroom's gills), where its texture is light
            // its head: over a wide soft band from well down the stalk, not at the line where the texture's glow flags
            // start (those switch on at one height; a plant with a head glows by the band alone)
            float head = s.glowFrom > 0 ? Smooth(s.glowFrom - 0.4f, s.glowFrom + 0.3f, g01) : 1f;
            float gw = s.glowFrom > 0 ? head : c.a > 127 && !s.building && s.glowFrom == 0 ? 1f : 0f;   // (a building's texture flags are ignored: its glow parts say; glowFrom under 0, its markings)
            float ember = 0f, paleWood = 0f;
            if (s.partAmount > 0 || s.partRed > 0)   // (a part glowing where red may leave its pale parts unlit: amount 0)
            {
                float band = Mathf.SmoothStep(0f, 1f, (y01 - s.partFrom) / 0.08f) * (1f - Mathf.SmoothStep(0f, 1f, (y01 - s.partTo) / 0.08f));
                if (s.partRed > 0f)
                {
                    // (or where it is red, the redder the more: the kiln's log and temple, the embers in their cracks bright,
                    // at partHot, not their red-brown charcoal, nor its darkest crumbs, however red; and their carved pale
                    // wood, where it is warm, a little: not the charcoal's grey sheen, however light)
                    float red = (lin.r - Mathf.Max(lin.g, lin.b)) / Mathf.Max(1e-3f, lin.r);
                    ember = band * Smooth(s.partRed, s.partRed + 0.18f, red) * Smooth(0.025f, 0.05f, lum);
                    gw = Mathf.Max(gw, ember * s.partHot);
                    paleWood = band * Mathf.SmoothStep(0f, 1f, (rel - s.partRel) / 0.4f) * Smooth(0.25f, 0.38f, red);
                    gw = Mathf.Max(gw, paleWood * s.partAmount);
                }
                else gw = Mathf.Max(gw, band * Mathf.SmoothStep(0f, 1f, (rel - s.partRel) / 0.4f) * s.partAmount);
            }
            bool glows = gw > 0.5f;
            Color own = lin * (1f / lum);                                   // the texture's own hue, at unit brightness
            if (s.ownHue != 0 || s.ownSat != 1) own = HueShift(own, s.ownHue, s.ownSat);   // turned as its region turns its plants
            NestTheme th = NestTheme.Current;
            if (th != null && th.ownBand.y > 0) own = Fold(own, th.ownBand.x, th.ownBand.y);
            Color tint = Color.Lerp(s.baseTint, s.tipTint, Smooth(0.05f, 1f, g01));
            Color col = s.rigid ? (th == null ? lin * 1.6f : Color.Lerp(lin, th.body[0].linear * (lum / Mathf.Max(1e-3f, Lum(th.body[0].linear))), th.rigidTint) * (1.6f * th.rigidGain))
                : Color.Lerp(tint, own * 0.75f, s.keepOwn) * (s.meanLum * rel * 2.2f);
            // (a relic's pale carved wood weathered toward a cool silver grey, as far as its nest has it: the kiln's,
            // NestTheme.relicPaleGrey; in its own tan it read gold, the amber nest's colour, k7-k8)
            if (paleWood > 0f && th != null && th.relicPaleGrey > 0f)
            {
                float lg = Lum(col);
                col = Color.Lerp(col, new Color(lg * 0.9f, lg * 0.98f, lg * 1.14f), paleWood * th.relicPaleGrey);   // (a little bluer since k11: one of its cool notes)
            }
            if (!s.rigid)
            {
                // a lollipop's stick is darker, brightening up into its head
                if (s.glowFrom > 0) col *= Mathf.Lerp(0.86f, 1f, head);
                if (gw > 0)
                {
                    // the glowing part leans to the glow colour, paler toward a head's tip, its pattern kept
                    float tipward = s.glowFrom > 0 ? Mathf.Clamp01((g01 - s.glowFrom) / Mathf.Max(0.05f, 1f - s.glowFrom)) : 0f;
                    Color light = Color.Lerp(s.glowDeep, s.glow, 0.4f * tipward);
                    // (a finely made plant keeps its own colours and pattern under the glow: a curl's scales, a
                    // lantern's paper, a coral's pale tips)
                    float lean = s.fine ? (s.glowFrom > 0 ? fineLean.x : fineLean.y) : s.glowFrom > 0 ? 0.45f : 0.8f;
                    col = Color.Lerp(col, light * Mathf.Clamp(s.meanLum * rel * 2.4f, 0.22f, 0.55f), lean * Mathf.Min(1f, gw));
                }
            }
            // mottling in this plant's own pattern, and now and then faint bands along its height
            float mot = Noise(p.x * s.mottleScale + s.mottleSeed, p.z * s.mottleScale + p.y * s.mottleScale * 0.7f);
            col *= 1f + s.mottle * (mot * 2f - 1f) + (s.band > 0 ? s.band * Mathf.Sin(p.y * s.bandFreq + s.mottleSeed) : 0f);
            // a finely made plant: dark at its foot, lighter up its body (it stands in the floor's shadow; its form reads)
            if (s.fine) shade *= Mathf.Lerp(s.foot > 0f ? s.foot : fineFoot, 1f, Smooth(0f, 0.6f, g01));
            Color albedo = col * (solidAlbedo * shade * s.gain);
            float bright = s.glowFrom > 0 ? 0.7f + 0.3f * Mathf.Clamp01(rel) : Mathf.Clamp01(s.glowGain * (0.55f + 0.4f * Mathf.Clamp01(rel * 0.8f)));
            if (s.fine) bright *= fineGlow;
            float glow = Mathf.Max(gw * bright, s.building ? 0f : s.faint * Mathf.SmoothStep(0f, 1f, (rel - 1.05f) / 1.2f));   // and its own faint light, in its lightest parts
            if (s.fine) glow = Mathf.Max(glow, (th != null && th.fineLight >= 0f ? th.fineLight : fineOwnLight) * (0.55f + 0.45f * Mathf.Clamp01(rel)) * Smooth(0f, 1f, g01));
            if (s.spots != null && s.spots[k] > 0.01f)
            {
                // a spot of its own light (a puffball's middle and its balls, a coil, a tip): toward its markings' colour,
                // glowing
                float lit = s.spots[k];
                albedo = Color.Lerp(albedo, s.bioCol, Mathf.Min(1f, 0.8f * lit));
                glow = Mathf.Max(glow, fineSpotGlow * lit * (0.75f + 0.25f * Mathf.Clamp01(rel)));
            }
            if (s.bio > 0)
            {
                // its kind's markings: in their colour, glowing strongly
                float mark = BioMark(s, k, s.pts[k], y01, lum / s.meanLum) * s.bioAmt;
                if (mark > 0.01f)
                {
                    albedo = Color.Lerp(albedo, s.bioCol, Mathf.Min(1f, mark * 1.25f));
                    glow = Mathf.Max(glow, bioStrong * (th != null ? th.markGain : 1f) * mark);
                }
            }
            // and every plant a faint light of its own, more toward its top
            if (!s.rigid && !s.building) glow = Mathf.Max(glow, bioFaint * (0.45f + 0.55f * Smooth(0f, 0.9f, g01)));
            if (s.building)
            {
                // a building glows only along its paths (its texture's glowing parts) and at a few points, embers in its
                // pores; those lean to its glow colour
                if (PointHash(k + 911) < 0.012f) glow = Mathf.Max(glow, 0.55f + 0.3f * PointHash(k + 913));
                if (glow > 0) albedo = Color.Lerp(albedo, s.glowDeep * 0.55f, 0.6f * Mathf.Min(1f, glow));
                // (the kiln's embers in their cracks: bright, in their own red, however deep in the crack; leaned as above
                // they stayed a dark rust, k7)
                if (ember > 0f) albedo = Color.Lerp(albedo, s.glowDeep * 0.9f, ember);   // (at 1.4 they bloomed salmon pink, k7b)
            }
            if (s.far && th != null && (th.farDim < 1f || th.farFog > 0f))
            {
                // a tree beyond the walls: a dark, muted shape in the fog; in a nest whose palette is turned from v15's, in
                // the fog's colour (its texture's own, kept, put a navy column at the end of the granary's way)
                float l = Lum(albedo);
                Color fog = th.fog.linear;
                Color muted = th.farFog > 0f ? Color.Lerp(albedo, fog * (l / Mathf.Max(1e-3f, Lum(fog))), th.farFog) : Color.Lerp(new Color(l, l, l), albedo, 0.5f);
                albedo = muted * th.farDim;
                glow *= th.farDim * 0.5f;
            }
            if (s.glass && glows)
            {
                // glass: a thin, broken shell, darker, lit only faintly by the light inside (its outline still
                // catches the silvery rim light)
                albedo *= 0.7f;
                glow *= 0.15f;
                radius *= 0.42f;
            }
            float dens = s.density != null ? s.density[k] : 1f;
            float vary = (0.72f + 0.56f * PointHash(k)) / Mathf.Sqrt(Mathf.Clamp(dens, 0.25f, 9f));
            SolidPoint sp = ThingPen.Splat(s.m.MultiplyPoint3x4(p), s.m.MultiplyVector(n), albedo, radius * vary, glow);
            return s.building ? ThingPen.Building(sp) : sp;
        }

        /// <summary>Splat j of the light inside a glass plant: fine, in its glow colour, paler and brightest in the
        /// middle; all facing up, so the moonlight lights them evenly and the light reads as one glow, not as grains.
        /// Plain arithmetic only.</summary>
        SolidPoint CoreSplat(SolidSlot s, int j, float radius)
        {
            Vector4 c = s.core[j];
            Vector3 p = c, n = Vector3.up;
            Shape(s, ref p, ref n);
            float f = c.w;
            Color col = Color.Lerp(s.glow, Color.white, 0.45f * (1 - f)) * (1f - 0.45f * f);
            float glow = Mathf.Clamp01(1.25f - 0.6f * f);
            return ThingPen.Splat(s.m.MultiplyPoint3x4(p), s.m.MultiplyVector(n), col, radius * (0.35f + 0.3f * PointHash(j + 77)), glow);
        }

        /// <summary>
        /// Splat k of a crystal (crystalKinds): where it is crystal, solid, its points covering it; pale, in the nest's
        /// colour, keeping some of its texture's own, more of it in the patches of colour inside it; each face its own
        /// light and dark, lit from one side as by a lamp, so a prism's faces read apart; its ridges and points catching
        /// the light as thin pale lines (its ridge map, Tools/plants/growth_map.py; the references' crystals: "棱和边缘
        /// 接住光，底部暗，越往上越亮"); dark at its foot, lighter toward its point; a light of its own from inside, more
        /// in its patches of colour, least at its ridges (they shine, they do not glow). Where it is the rock it grows
        /// from, dark and rough, as a plant's foot is; the one going over into the other at the crystals' feet by the
        /// texture's lightness and the height together (its texture marks no glowing part: by height alone it would
        /// cut the crystals level). A building to the shader: it stands still in the wind. Plain arithmetic only.
        /// </summary>
        SolidPoint CrystalSplat(SolidSlot s, int k, Vector3 p, Vector3 n, Color lin, float lum, float g01, float y01, float shade, float radius)
        {
            Vector3 wn = s.m.MultiplyVector(n).normalized;
            Color own = lin * (1f / lum);
            // how much it is crystal: everything a third of the way up; below, its pale parts (the rock is dark)
            float crystal = Mathf.Max(Smooth(0.22f, 0.36f, y01), Smooth(0.06f, 0.16f, lum) * Smooth(0.02f, 0.1f, y01));
            // the rock: dark, its own colour mostly, the nest's base tint over it, faintly lit of itself as every plant is
            Color rock = Color.Lerp(s.baseTint, own * 0.75f, s.keepOwn) * (Mathf.Clamp(lum, 0.015f, 0.15f) * 3f * solidAlbedo * shade);
            float rockGlow = bioFaint * 0.45f;
            // the crystal
            float ridge = (s.ridge != null ? s.ridge[k] / 255f : 0f) * crystal;
            float point = (s.tip != null ? 1f - Smooth(0f, 0.06f, s.tip[k]) : 0f) * crystal;
            // (its ridges held to their middles: their soft edges lit the thin blades' points as scattered dots; its points
            // only where ridges meet: the growth map's tips also fall on its flat faces, where they lit white blots)
            float line = Mathf.Max(Smooth(0.5f, 0.8f, ridge), Mathf.Min(1f, 1.3f * point * Smooth(0.15f, 0.4f, ridge)));
            float key = Mathf.Max(0f, Vector3.Dot(wn, crystalKey.normalized)), fill = Mathf.Max(0f, Vector3.Dot(wn, crystalFill.normalized));
            // (and a third, faint, from below the other side: the faces turned from the key light were one grey)
            float under = Mathf.Max(0f, Vector3.Dot(wn, new Vector3(0.35f, -0.45f, 0.82f)));
            float face = 0.42f + 0.95f * key * key + 0.25f * fill + 0.2f * under;
            float chroma = Mathf.Max(own.r, Mathf.Max(own.g, own.b)) - Mathf.Min(own.r, Mathf.Min(own.g, own.b));
            float patch = Smooth(s.crystalChroma.x, s.crystalChroma.y, chroma);
            float up = Smooth(0f, 1f, g01);
            float light = Mathf.Clamp(Mathf.Sqrt(lum / s.crystalLum), 0.95f, 1.05f);   // its texture's cloudiness, nearly gone (wider, grey blotches on its faces: granite, frost)
            // (its patches of colour a little more vivid than the texture's: as a light inside, its own pale tints read grey)
            Color vivid = Color.LerpUnclamped(Color.white, own, 1.8f);
            vivid = new Color(Mathf.Max(0f, vivid.r), Mathf.Max(0f, vivid.g), Mathf.Max(0f, vivid.b));
            Color body = Color.Lerp(s.crystalTint, Color.Lerp(own, vivid, patch), Mathf.Lerp(crystalOwn, 0.5f, patch));   // (at 0.9 its colour sat on its surface: its light shows it)
            Color albedo = body * (crystalAlbedo * face * light * Mathf.Lerp(0.45f, 1f, up) * Mathf.Lerp(0.85f, 1f, shade));
            // its depth: a darker band along its ridges' inner side, its faces' cores lighter (a crystal's growth map's s:
            // how far from its nearest ridge, 1 at a face's middle); as one even grey it read as frosted stone
            float core = s.stem != null ? Smooth(0f, 0.85f, s.stem[k] / 255f) : 0.5f;
            albedo *= Mathf.Lerp(0.72f, 1.06f, core);
            albedo = Color.Lerp(albedo, s.crystalEdge, 0.85f * line);
            // the light inside, in its nest's light colour (a warm gold in the granary): up each prism, away from its
            // ridges (they stay pale), brightest toward its point (cold grey, it read as plain quartz, not lit within)
            float inside = (1f - line) * up * up;
            Color lightCol = s.crystalLight * (Lum(albedo) / Mathf.Max(1e-3f, Lum(s.crystalLight)));
            albedo = Color.Lerp(albedo, lightCol, crystalWarm * inside);
            // (lit from within, more toward its point; least at its ridges, which shine rather than glow) and in soft clouds
            // of light inside it, large, more toward its point, more at its faces' cores
            float cloud = Smooth(0.4f, 0.85f, Noise(s.pts[k].x * 3.2f + s.mottleSeed, s.pts[k].y * 2.4f + s.pts[k].z * 3.2f));
            float glow = (crystalGlow + crystalInner * patch) * (0.2f + 0.8f * up * up) * (1f - 0.8f * line) + crystalLine * line + crystalLit * inside
                + (crystalCloud * cloud * Mathf.Lerp(0.3f, 1.2f, core) + crystalCore * core) * up * (1f - line);
            albedo = Color.Lerp(rock, albedo, crystal);
            glow = Mathf.Lerp(rockGlow, glow, crystal);
            float dens = s.density != null ? s.density[k] : 1f;
            // its faces' splats larger and alike (smaller and varied, the dark behind showed through: frost, sugar), its
            // ridges' smaller (a fine line; at 0.6 a dashed one)
            float vary = (0.9f + 0.2f * PointHash(k)) / Mathf.Sqrt(Mathf.Clamp(dens, 0.25f, 9f));
            return ThingPen.Building(ThingPen.Splat(s.m.MultiplyPoint3x4(p), wn, albedo, radius * vary * Mathf.Lerp(1.3f, 0.85f, line), glow));
        }

        /// <summary>A point of a plant (and its normal) stretched, twisted about its stem and bent over (the higher, the
        /// more) as this plant is.</summary>
        static void Shape(SolidSlot s, ref Vector3 p, ref Vector3 n)
        {
            if (s.rigid || (s.bend == 0 && s.twist == 0 && s.sy == 1 && s.sxz == 1)) return;
            float y = Mathf.Max(0, p.y);
            p.x *= s.sxz; p.z *= s.sxz; p.y *= s.sy;
            n.x /= s.sxz; n.z /= s.sxz; n.y /= s.sy;
            float a = s.twist * y, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            p = new Vector3(p.x * ca - p.z * sa, p.y, p.x * sa + p.z * ca);
            n = new Vector3(n.x * ca - n.z * sa, n.y, n.x * sa + n.z * ca);
            float yy = y * s.sy, b = s.bend;
            p.x += s.bendDir.x * b * yy * yy;
            p.z += s.bendDir.y * b * yy * yy;
            n.y -= 2 * b * yy * (s.bendDir.x * n.x + s.bendDir.y * n.z);
        }

        /// <summary>A stored point's own random number (0..1), the same for every plant of a kind.</summary>
        static float PointHash(int k)
        {
            uint h = (uint)k * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777216f;
        }

        /// <summary>A plant's tints: its kind picks one of the near colours for its body and one for its glow; now and
        /// then (by the plant itself) a warm one instead. Glow index in bits 3-5, body in 0-2.</summary>
        uint Palette(PlantInstance pi, int index, float area)
        {
            if (bodyPalette == null || glowPalette == null) return 0;
            uint kind = 2166136261u;
            foreach (char ch in pi.plant.name) kind = (kind ^ ch) * 16777619u;
            uint own = (uint)index * 2654435761u ^ kind;
            own ^= own >> 15; own *= 2246822519u; own ^= own >> 13;
            float r1 = (own & 0xFFFF) / 65535f, r2 = ((own >> 16) & 0xFFFF) / 65535f;
            // (an accent only on a small plant: a trunk in one made a large patch of it, a navy column in the granary)
            uint body = r1 < bodyAccent && area < accentArea ? 6u + ((own >> 3) & 1u) : kind % 6u;
            uint[] coolGlow = { 0, 1, 2, 6 }, warmGlow = { 3, 4, 5, 7 };
            uint glow = r2 < glowAccent ? warmGlow[(own >> 7) % 4] : coolGlow[(kind >> 4) % 4];
            return (glow << 3) | body;
        }

        /// <summary>
        /// Replace: how likely each of a plant's points is kept, from its texture's light and dark (against the
        /// plant's own mean), so the lighter parts of the texture are drawn with more points, like a halftone.
        /// Glowing points are always kept.
        /// </summary>
        float[] Keep(PlantRef plant)
        {
            if (keeps.TryGetValue(plant, out float[] cached)) return cached;
            Color32[] src = plant.pointColors;
            var lum = new float[src.Length];
            double sum = 0;
            int n = 0;
            for (int k = 0; k < src.Length; k++)
            {
                lum[k] = Linear[src[k].r] * 0.299f + Linear[src[k].g] * 0.587f + Linear[src[k].b] * 0.114f;
                if (src[k].a <= 127) { sum += lum[k]; n++; }
            }
            float mean = n > 0 ? Mathf.Max(1e-3f, (float)(sum / n)) : 0.2f;
            var keep = new float[src.Length];
            for (int k = 0; k < src.Length; k++)
                keep[k] = src[k].a > 127 ? 1f : Mathf.Lerp(1f, Mathf.Clamp(0.06f + 0.94f * Mathf.Pow(lum[k] / mean, 2f), 0.06f, 1f), grain);
            keeps[plant] = keep;
            return keep;
        }

        /// <summary>
        /// Replace: the terrain's triangles in world space, the ones inside the hexagon listed with their area and
        /// what they are (their vertex colours say floor, bank, plateau and how open), binned on a coarse grid; and
        /// each terrain material's textures read back from the GPU.
        /// </summary>
        void PrepareGround()
        {
            meshes.Clear();
            cells.Clear();
            var list = new List<GroundTri>();
            foreach (MeshFilter mf in ground)
            {
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                GroundLook look = groundTexMix > 0 || grain > 0 ? Look(mr != null ? mr.sharedMaterial : null) : null;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Vector3[] v = mesh.vertices, n = mesh.normals;
                for (int i = 0; i < v.Length; i++) { v[i] = m.MultiplyPoint3x4(v[i]); n[i] = m.MultiplyVector(n[i]).normalized; }
                Color[] c = mesh.colors;
                if (c.Length != v.Length) { c = new Color[v.Length]; for (int i = 0; i < c.Length; i++) c[i] = new Color(0, 1, 0, 1); }
                int[] t = mesh.triangles;
                meshes.Add((v, n, c, t, look));
                int mi = meshes.Count - 1;
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector3 a = v[t[k]], b = v[t[k + 1]], d = v[t[k + 2]];
                    Vector3 mid = (a + b + d) / 3f;
                    if (world.HexDist(mid.x, mid.z) > -0.2f) continue;   // the rim of the hexagon
                    Color lw = (c[t[k]] + c[t[k + 1]] + c[t[k + 2]]) / 3f;
                    list.Add(new GroundTri { mesh = mi, k = k, area = 0.5f * Vector3.Cross(b - a, d - a).magnitude,
                        kind = lw.r * 0.55f + lw.g * 1.25f + lw.b * 0.45f, x = mid.x, z = mid.z });
                }
            }
            tris = list.ToArray();
            for (int i = 0; i < tris.Length; i++)
            {
                long key = CellKey(Mathf.FloorToInt(tris[i].x / CellSize), Mathf.FloorToInt(tris[i].z / CellSize));
                if (!cells.TryGetValue(key, out var bin)) cells[key] = bin = new List<int>();
                bin.Add(i);
            }
            groundTint = (groundColour.a > 0 ? groundColour : scanOverride.a > 0 ? scanOverride : new Color(0.7f, 0.8f, 1f)).linear;
            floorTintL = floorColour.a > 0 ? floorColour.linear : groundTint;
            bankTintL = bankColour.a > 0 ? bankColour.linear : groundTint;
            plateauTintL = plateauColour.a > 0 ? plateauColour.linear : groundTint;
            edgeTintL = edgeColour.a > 0 ? edgeColour.linear : bankTintL;
            // each look's mean brightness, so its light and dark are read against it
            var rng = new Rng(99);
            foreach (GroundLook look in looks.Values)
            {
                double sum = 0;
                int n = 0;
                for (int s = 0; s < 4000 && tris.Length > 0; s++)
                {
                    GroundTri g = tris[(int)(rng.Next() * (tris.Length - 1))];
                    var (v, nr, c, t, l) = meshes[g.mesh];
                    if (l != look) continue;
                    int ia = t[g.k];
                    sum += Lum(Albedo(look, v[ia], nr[ia], c[ia]));
                    n++;
                }
                if (n > 0) look.meanLum = Mathf.Max(1e-3f, (float)(sum / n));
            }
            if (carpet != null) BakeCarpet();
        }

        /// <summary>
        /// The carpet's maps: the terrain's top surface height and its layer weights (floor, bank, plateau, open) on
        /// a grid of 1/8 unit over the nest, rasterised from its triangles; and the material settings for drawing it.
        /// </summary>
        void BakeCarpet()
        {
            const float cell = MapCell;
            float x0 = world.minX - 1, z0 = world.minZ - 1;
            int w = Mathf.Min(2048, Mathf.CeilToInt((world.maxX - world.minX + 2) / cell));
            int h = Mathf.Min(2048, Mathf.CeilToInt((world.maxZ - world.minZ + 2) / cell));
            var height = new float[w * h];
            var layers = new Color32[w * h];
            for (int i = 0; i < height.Length; i++) height[i] = float.MinValue;
            foreach (var (v, _, c, t, _) in meshes)
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector3 a = v[t[k]], b = v[t[k + 1]], d = v[t[k + 2]];
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, d.x)) - x0) / cell));
                    int i1 = Mathf.Min(w - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, d.x)) - x0) / cell));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, d.z)) - z0) / cell));
                    int j1 = Mathf.Min(h - 1, Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(b.z, d.z)) - z0) / cell));
                    float den = (b.z - d.z) * (a.x - d.x) + (d.x - b.x) * (a.z - d.z);
                    if (Mathf.Abs(den) < 1e-9f) continue;
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float px = x0 + (i + 0.5f) * cell, pz = z0 + (j + 0.5f) * cell;
                        float wa = ((b.z - d.z) * (px - d.x) + (d.x - b.x) * (pz - d.z)) / den;
                        float wb = ((d.z - a.z) * (px - d.x) + (a.x - d.x) * (pz - d.z)) / den;
                        float wc = 1 - wa - wb;
                        if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                        float y = a.y * wa + b.y * wb + d.y * wc;
                        int idx = j * w + i;
                        if (y <= height[idx]) continue;
                        height[idx] = y;
                        layers[idx] = c[t[k]] * wa + c[t[k + 1]] * wb + c[t[k + 2]] * wc;
                    }
                }
            for (int i = 0; i < height.Length; i++) if (height[i] == float.MinValue) height[i] = -100f;
            heightArr = height;
            layerArr = layers;
            mapW = w;
            mapH = h;
            mapX0 = x0;
            mapZ0 = z0;
            heightMap = new Texture2D(w, h, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            heightMap.SetPixelData(height, 0);
            heightMap.Apply(false, true);
            layerMap = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            layerMap.SetPixels32(layers);
            layerMap.Apply(false, true);
            heightRect = new Vector4(x0, z0, w * cell, h * cell);
            // the floor's height for every point shader of the nest (NestField.cginc: sway, parting, ripples)
            Shader.SetGlobalTexture("_FloorHeightMap", heightMap);
            Shader.SetGlobalVector("_FloorHeightRect", heightRect);
            GroundLook look = null;
            foreach (GroundLook l in looks.Values) { look = l; break; }
            carpetN = Mathf.CeilToInt(carpetFade.y * 2 / carpetStep) + 2;
            carpetBlock = new MaterialPropertyBlock();
            carpetBlock.SetTexture("_HeightMap", heightMap);
            carpetBlock.SetTexture("_LayerMap", layerMap);
            carpetBlock.SetVector("_HeightRect", heightRect);
            carpetBlock.SetVector("_Fade", carpetFade);
            carpetBlock.SetFloat("_Step", carpetStep);
            carpetBlock.SetFloat("_N", carpetN);
            carpetBlock.SetFloat("_Size", carpetSize);
            carpetBlock.SetFloat("_Gain", carpetGain);
            carpetBlock.SetFloat("_MinPixels", 0.8f);
            carpetBlock.SetFloat("_Grain", grain);
            carpetBlock.SetFloat("_TexMix", groundTexMix);
            carpetBlock.SetFloat("_Vary", NestTheme.Current != null ? NestTheme.Current.floorVary : 22f);
            carpetBlock.SetVector("_PatchA", NestTheme.Current != null ? (Vector4)NestTheme.Current.floorPatchA.linear : Vector4.zero);
            carpetBlock.SetVector("_PatchB", NestTheme.Current != null ? (Vector4)NestTheme.Current.floorPatchB.linear : Vector4.zero);
            carpetBlock.SetVector("_TintFloor", floorTintL);
            carpetBlock.SetVector("_TintBank", bankTintL);
            if (look != null && look.floorTex != null)
            {
                carpetBlock.SetTexture("_FloorTex", look.floorTex);
                carpetBlock.SetFloat("_FloorScale", look.floorScale);
                carpetBlock.SetVector("_FloorTint", look.floorTint);
                carpetBlock.SetFloat("_HueShift", look.hue);
                carpetBlock.SetFloat("_Saturation", look.sat);
                carpetBlock.SetFloat("_MeanLum", look.meanLum);
            }
            else
            {
                carpetBlock.SetTexture("_FloorTex", Texture2D.grayTexture);
                carpetBlock.SetFloat("_FloorScale", 1f);
                carpetBlock.SetVector("_FloorTint", Color.white);
                carpetBlock.SetFloat("_Saturation", 1f);
                carpetBlock.SetFloat("_MeanLum", 0.2f);
                carpetBlock.SetFloat("_Grain", 0f);
            }
            Debug.Log($"[AntGarden] particle carpet: {carpetN}² points round the ant, maps {w}×{h}");
        }

        static long CellKey(int x, int z) => ((long)x << 32) | (uint)z;

        /// <summary>A terrain material's textures, read back from the GPU (the assets are not readable on the CPU).</summary>
        GroundLook Look(Material m)
        {
            if (m == null) return null;
            if (looks.TryGetValue(m, out GroundLook look)) return look;
            look = new GroundLook
            {
                floorTex = m.GetTexture("_FloorTex"),
                floor = Readback(m.GetTexture("_FloorTex"), out int fw0, out int fh0),
                bank = Readback(m.GetTexture("_BankTex"), out int bw0, out int bh0),
                plateau = Readback(m.GetTexture("_PlateauTex"), out int pw0, out int ph0),
                floorScale = m.HasProperty("_FloorScale") ? m.GetFloat("_FloorScale") : 0.3f,
                bankScale = m.HasProperty("_BankScale") ? m.GetFloat("_BankScale") : 0.3f,
                plateauScale = m.HasProperty("_PlateauScale") ? m.GetFloat("_PlateauScale") : 0.3f,
                floorTint = m.HasProperty("_FloorTint") ? m.GetColor("_FloorTint").linear : Color.white,
                bankTint = m.HasProperty("_BankTint") ? m.GetColor("_BankTint").linear : Color.white,
                plateauTint = m.HasProperty("_PlateauTint") ? m.GetColor("_PlateauTint").linear : Color.white,
                hue = m.HasProperty("_HueShift") ? m.GetFloat("_HueShift") : 0,
                sat = m.HasProperty("_Saturation") ? m.GetFloat("_Saturation") : 1,
            };
            look.fw = fw0; look.fh = fh0; look.bw = bw0; look.bh = bh0; look.pw = pw0; look.ph = ph0;
            looks[m] = look;
            return look;
        }

        /// <summary>A texture's pixels (gamma bytes, at most max wide), halved on the GPU step by step (each halving
        /// averages four texels), then read back.</summary>
        static Color32[] Readback(Texture tex, out int w, out int h, int max = 512)
        {
            w = h = 0;
            if (tex == null) return null;
            int sw = tex.width, sh = tex.height;
            RenderTexture prev = RenderTexture.active, rt = null;
            Texture src = tex;
            do
            {
                if (sw > max || sh > max) { sw = Mathf.Max(1, sw / 2); sh = Mathf.Max(1, sh / 2); }
                var next = RenderTexture.GetTemporary(sw, sh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, next);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                rt = next;
                src = next;
            } while (sw > max || sh > max);
            RenderTexture.active = rt;
            var t2 = new Texture2D(sw, sh, TextureFormat.RGBA32, false, false);
            t2.ReadPixels(new Rect(0, 0, sw, sh), 0, 0, false);
            t2.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] px = t2.GetPixels32();
            Destroy(t2);
            w = sw;
            h = sh;
            return px;
        }

        /// <summary>
        /// Replace: the walls and floor as points over the whole nest, sparse, densest along the ways (focus), lifted a
        /// little off the surface so the dark terrain does not hide them. Most are fine points; a few are big soft
        /// splats, a faint haze on the surfaces.
        /// </summary>
        void SampleGround()
        {
            if (ground.Count == 0) return;
            if (tris == null) PrepareGround();
            double wanted = 0;
            foreach (GroundTri g in tris) wanted += g.area * g.kind * Mathf.Lerp(groundFar, groundNear, Focus(g.x, g.z));
            float scale = wanted > groundBudget ? (float)(groundBudget / wanted) : 1f;
            for (int i = 0; i < tris.Length; i++)
            {
                var rng = new Rng((uint)i * 2654435761u ^ 0x5bd1e995u);
                Emit(tris[i], tris[i].area * tris[i].kind * Mathf.Lerp(groundFar, groundNear, Focus(tris[i].x, tris[i].z)) * scale,
                    ref rng, groundBrightness, splatShare, points);
            }
            if (cover > 0)
            {
                var pen = new CoverPen(this, points);
                for (int i = 0; i < tris.Length; i++)
                    Cover(tris[i], i, 0, CoverCount(tris[i], i, cover * Mathf.Lerp(0.15f, 1f, Focus(tris[i].x, tris[i].z))), points, pen);
            }
        }

        /// <summary>The shell's points round (x, z): the ground's and the plants' (on a worker thread: plain
        /// arithmetic on arrays only).</summary>
        void MakeShell(Vector2 at)
        {
            shellWork.Clear();
            float reach2 = shellReach * shellReach;
            if (shellDensity > 0)
            {
                int r = Mathf.CeilToInt(shellReach / CellSize);
                int cx = Mathf.FloorToInt(at.x / CellSize), cz = Mathf.FloorToInt(at.y / CellSize);
                for (int z = cz - r; z <= cz + r; z++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!cells.TryGetValue(CellKey(x, z), out var bin)) continue;
                    foreach (int i in bin)
                    {
                        GroundTri g = tris[i];
                        float dx = g.x - at.x, dz = g.z - at.y;
                        if (dx * dx + dz * dz > reach2) continue;
                        var rng = new Rng((uint)i * 2246822519u ^ 0x27d4eb2fu);
                        Emit(g, g.area * g.kind * shellDensity, ref rng, groundBrightness * shellBrightness, 0.12f, shellWork);
                    }
                }
            }
            if (coverNear > 0)
            {
                var pen = new CoverPen(this, shellWork);
                int r = Mathf.CeilToInt(shellReach / CellSize);
                int cx = Mathf.FloorToInt(at.x / CellSize), cz = Mathf.FloorToInt(at.y / CellSize);
                for (int z = cz - r; z <= cz + r; z++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!cells.TryGetValue(CellKey(x, z), out var bin)) continue;
                    foreach (int i in bin)
                    {
                        GroundTri g = tris[i];
                        float dx = g.x - at.x, dz = g.z - at.y;
                        if (dx * dx + dz * dz > reach2) continue;
                        // the same things as far away, then many more: the near field is a superset
                        int from = CoverCount(g, i, cover * Mathf.Lerp(0.15f, 1f, Focus(g.x, g.z)));
                        Cover(g, i, from, Mathf.Max(from, CoverCount(g, i, coverNear)), shellWork, pen);
                    }
                }
            }
            if (shellPlantArea > 0)
                foreach (PlantSlot p in plants)
                {
                    float dx = p.pos.x - at.x, dz = p.pos.z - at.y, reach = shellReach + p.height;
                    if (dx * dx + dz * dz > reach * reach) continue;
                    int want = Mathf.RoundToInt(shellPlantArea * p.height * p.height * (p.relic ? 3f : 1f)) - p.baseCount;
                    for (int k = p.baseEnd, taken = 0; k < p.pts.Length && k < p.cols.Length && taken < want; k++)
                    {
                        if (p.keep != null && PointHash(k) > p.keep[k]) continue;
                        shellWork.Add(new Point { pos = p.m.MultiplyPoint3x4(p.pts[k]), col = p.cols[k] | p.palette });
                        taken++;
                    }
                }
        }

        /// <summary>A triangle's ground points (want of them, on average), coloured like the surface they sit on: the
        /// floor darker than the walls (the glowing way on it must stand out), the plateau dimmer, the bank's top
        /// edge (where it meets the plateau) brightest, the outline of the passage; creases darker. With the terrain's
        /// textures read back, its light and dark set how bright the points are and how many (like a halftone), and
        /// some of its own colour shows through; else smooth noise stands in. Plain arithmetic only, so a worker
        /// thread can run it.</summary>
        void Emit(GroundTri g, float want, ref Rng rng, float bright, float splats, List<Point> into)
        {
            var (v, nr, c, t, look) = meshes[g.mesh];
            int ia = t[g.k], ib = t[g.k + 1], ic = t[g.k + 2];
            bool textured = look != null && look.floor != null;
            if (textured) want *= 1 + 0.7f * grain;    // the halftone thins them: start with more
            int many = (int)want;
            if (rng.Next() < want - many) many++;
            for (int j = 0; j < many; j++)
            {
                float u = rng.Next(), w = rng.Next();
                if (u + w > 1) { u = 1 - u; w = 1 - w; }
                float s = 1 - u - w;
                Vector3 n = (nr[ia] * s + nr[ib] * u + nr[ic] * w).normalized;
                Vector3 p = v[ia] * s + v[ib] * u + v[ic] * w;
                Color lw = c[ia] * s + c[ib] * u + c[ic] * w;   // floor, bank, plateau, open
                float lum = 0.3f * lw.r + 0.75f * lw.g + 0.32f * lw.b + 1.4f * lw.g * lw.b;
                lum *= 0.55f + 0.45f * lw.a;
                float jitter = rng.Next(), roll = rng.Next(), splatRoll = rng.Next();
                Color col;
                if (textured)
                {
                    Color albedo = Albedo(look, p, n, lw);
                    float al = Lum(albedo), rel = al / look.meanLum;
                    if (roll > Mathf.Lerp(1f, Mathf.Clamp(0.05f + 0.95f * Mathf.Pow(rel, 2f), 0.05f, 1f), grain)) continue;
                    lum *= (0.9f + 0.2f * jitter) * Mathf.Lerp(1f, Mathf.Clamp(0.06f + 0.94f * Mathf.Pow(rel, 2.4f), 0.03f, 3.5f), grain);
                    Color own = albedo * (lum / Mathf.Max(1e-4f, al));   // its own colour, as bright as the pale one
                    col = Color.Lerp(FloorPatches(LayerTint(lw), p, lw.r) * lum, own, groundTexMix);
                }
                else
                {
                    float patch = Noise(p.x * 0.31f + 17.3f, p.z * 0.31f + p.y * 0.45f + 5.1f);
                    float layers = 0.68f + 0.32f * (float)System.Math.Sin(p.y * 6.5f + Noise(p.x * 0.12f, p.z * 0.12f) * 6f);
                    lum *= (0.4f + 1.2f * jitter) * (0.3f + 1.2f * patch) * (1 + (layers - 1) * lw.g);
                    col = LayerTint(lw) * lum;
                }
                // like a splat capture: most points fine, some soft middling splats, a few large faint ones under them
                bool large = splatRoll < splats * 0.2f, middling = !large && splatRoll < splats;
                uint size = large ? 40u : middling ? 20u : 10u;
                // round the ant these sizes give way to the carpet (ParticleLook.shader), which lies on the floor only and
                // thins out on a slope: on the walls and anything steep they are one size up and stay (the smooth terrain
                // showed there, close by, behind the plants)
                if (Hash(Mathf.FloorToInt(p.x * 9973f), Mathf.FloorToInt(p.z * 9967f + p.y * 997f)) >= Smooth(0.25f, 0.75f, lw.r) * Smooth(0.45f, 0.7f, n.y)) size++;
                into.Add(new Point { pos = p + n * 0.035f, col = PackLinear(col * (bright * (large ? 0.14f : middling ? 0.4f : 1f)), Ground, size) });
            }
        }

        /// <summary>The pale tint of a ground point by layer: floor, bank, plateau, and the bank's top edge (where it meets
        /// the plateau, the outline of the passage) leaning to the edge colour.</summary>
        /// <summary>A nest's floor in patches of other materials (NestTheme.floorPatchA, B: dark earth, pale straw...),
        /// in broad soft-edged patches, on the floor only (floor, its weight in the layer map), as the carpet's are.</summary>
        static Color FloorPatches(Color tint, Vector3 p, float floor)
        {
            NestTheme th = NestTheme.Current;
            if (th == null || floor <= 0) return tint;
            if (th.floorPatchA.a > 0)
                tint = Color.Lerp(tint, th.floorPatchA.linear, th.floorPatchA.a * floor * Mathf.SmoothStep(0f, 1f, (Noise(p.x / 3.7f + 63.1f, p.z / 3.7f + 63.1f) - 0.42f) / 0.25f));
            if (th.floorPatchB.a > 0)
                tint = Color.Lerp(tint, th.floorPatchB.linear, th.floorPatchB.a * floor * Mathf.SmoothStep(0f, 1f, (Noise(p.x / 2.9f - 27.7f, p.z / 2.9f + 11.3f) - 0.5f) / 0.25f));
            return tint;
        }

        Color LayerTint(Color lw)
        {
            float total = Mathf.Max(1e-3f, lw.r + lw.g + lw.b);
            Color t = (floorTintL * lw.r + bankTintL * lw.g + plateauTintL * lw.b) * (1f / total);
            float edge = lw.g * lw.b * 4f;
            if (edge > 1) edge = 1;
            return Color.Lerp(t, edgeTintL, edge * 0.7f);
        }

        /// <summary>The terrain's colour at p as its shader paints it: the floor, bank and plateau textures projected
        /// three ways (by the normal), tinted, blended by the vertex weights, then turned by the region's hue (linear).</summary>
        static Color Albedo(GroundLook l, Vector3 p, Vector3 n, Color lw)
        {
            float ax = n.x * n.x, ay = n.y * n.y, az = n.z * n.z;
            ax *= ax; ay *= ay; az *= az;
            float sw = Mathf.Max(1e-5f, ax + ay + az);
            ax /= sw; ay /= sw; az /= sw;
            float total = Mathf.Max(1e-3f, lw.r + lw.g + lw.b);
            float wf = lw.r / total, wb = lw.g / total, wp = lw.b / total;
            Color a = Color.black;
            if (wf > 0.02f) a += Tri(l.floor, l.fw, l.fh, p, l.floorScale, ax, ay, az) * l.floorTint * wf;
            if (wb > 0.02f) a += Tri(l.bank ?? l.floor, l.bank != null ? l.bw : l.fw, l.bank != null ? l.bh : l.fh, p, l.bankScale, ax, ay, az) * l.bankTint * wb;
            if (wp > 0.02f) a += Tri(l.plateau ?? l.floor, l.plateau != null ? l.pw : l.fw, l.plateau != null ? l.ph : l.fh, p, l.plateauScale, ax, ay, az) * l.plateauTint * wp;
            return HueShift(a, l.hue, l.sat);
        }

        static Color Tri(Color32[] px, int w, int h, Vector3 p, float scale, float ax, float ay, float az)
        {
            Color c = Color.black;
            if (ax > 0.02f) c += Texel(px, w, h, p.z * scale, p.y * scale) * ax;
            if (ay > 0.02f) c += Texel(px, w, h, p.x * scale, p.z * scale) * ay;
            if (az > 0.02f) c += Texel(px, w, h, p.x * scale, p.y * scale) * az;
            return c;
        }

        static Color Texel(Color32[] px, int w, int h, float u, float v)
        {
            int x = (int)((u - System.Math.Floor(u)) * w), y = (int)((v - System.Math.Floor(v)) * h);
            if (x >= w) x = w - 1;
            if (y >= h) y = h - 1;
            Color32 c = px[y * w + x];
            return new Color(Linear[c.r], Linear[c.g], Linear[c.b], 1);
        }

        static float Lum(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        /// <summary>The hue rotation in YIQ, as AG_HueShift (degrees).</summary>
        /// <summary>A colour whose hue lies outside the band (centre and half-width, degrees) turned to the band's nearer
        /// edge and half as saturated: a nest keeps to its own palette what its turn of the plants' colours threw out of it
        /// (a green moss turned pink in the red nest becomes a dull crimson-brown).</summary>
        static Color Fold(Color c, float centre, float half)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float d = Mathf.DeltaAngle(centre, h * 360f);
            if (Mathf.Abs(d) <= half || s < 0.08f) return c;
            return Color.HSVToRGB(Mathf.Repeat((centre + Mathf.Sign(d) * half) / 360f, 1f), s * 0.5f, v, true);
        }

        static Color HueShift(Color c, float deg, float sat)
        {
            if (deg == 0 && sat == 1) return c;
            float a = deg * Mathf.Deg2Rad, cs = (float)System.Math.Cos(a), sn = (float)System.Math.Sin(a);
            float y = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            float i = c.r * 0.596f - c.g * 0.274f - c.b * 0.322f;
            float q = c.r * 0.211f - c.g * 0.523f + c.b * 0.312f;
            float i2 = (i * cs - q * sn) * sat, q2 = (i * sn + q * cs) * sat;
            return new Color(System.Math.Max(0, y + 0.956f * i2 + 0.621f * q2), System.Math.Max(0, y - 0.272f * i2 - 0.647f * q2), System.Math.Max(0, y - 1.106f * i2 + 1.703f * q2));
        }

        static float[] MakeLinear()
        {
            var t = new float[256];
            for (int i = 0; i < 256; i++)
            {
                float x = i / 255f;
                t[i] = x <= 0.04045f ? x / 12.92f : (float)System.Math.Pow((x + 0.055) / 1.055, 2.4);
            }
            return t;
        }

        /// <summary>Smooth value noise, 0..1 (managed: safe on a worker thread).</summary>
        static float Noise(float x, float y)
        {
            int ix = (int)System.Math.Floor(x), iy = (int)System.Math.Floor(y);
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

        /// <summary>Replace: motes floating in the canyons' air, most of them along the ways; a few golden.</summary>
        void FloatDust(System.Random rng)
        {
            if (dust <= 0) return;
            float area = (world.maxX - world.minX) * (world.maxZ - world.minZ);
            int tries = Mathf.RoundToInt(area * dustHeight * dust);
            Color pale = new Color(0.62f, 0.74f, 0.74f).linear, gold = new Color(0.86f, 0.88f, 0.80f).linear, rose = new Color(0.55f, 0.85f, 0.88f).linear;   // silver mist, bone, cyan breath
            // as bright on screen as in v15 whatever the nest's exposure (a white glitter in the air of the lifted nests)
            float dustComp = NestTheme.LightComp(1f);
            for (int i = 0; i < tries; i++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(world.minZ, world.maxZ, (float)rng.NextDouble());
                if (rng.NextDouble() > 0.12f + 0.88f * Focus(x, z)) continue;
                if (world.HexDist(x, z) > -0.5f || world.Dist(x, z) > 0.5f) continue;   // in the canyons, not over the plateau
                float floor = world.WalkHeight(x, z);
                float y = floor + 0.12f + Mathf.Pow((float)rng.NextDouble(), 1.6f) * dustHeight;
                if (y > world.Ceiling(x, z) - 0.2f) continue;
                double kind = rng.NextDouble();
                bool golden = kind < 0.14, rosy = !golden && kind < 0.2;
                Color col = (golden ? gold * (0.5f + 0.9f * (float)rng.NextDouble())
                          : rosy ? rose * (0.4f + 0.7f * (float)rng.NextDouble())
                          : pale * (0.25f + 0.6f * (float)rng.NextDouble())) * dustComp;
                // (a nest's own dust colour, as light: NestTheme.dustTint)
                Color tint = NestTheme.Current != null ? NestTheme.Current.dustTint : Color.clear;
                if (tint.a > 0f) col = Color.Lerp(col, tint.linear * (Lum(col) / Mathf.Max(1e-3f, Lum(tint.linear))), tint.a);
                points.Add(new Point { pos = new Vector3(x, y, z), col = Pack(col.gamma, Dust, golden ? 10u : 0u) });
            }
        }

        /// <summary>What the cover's shapes (GroundCoverShapes) draw with: a random sequence they can call, and a callback
        /// adding to one list. Made once per pass, so nothing is allocated per thing.</summary>
        sealed class CoverPen
        {
            public Rng rng;
            public readonly System.Func<float> rnd;
            public readonly System.Action<Vector3, Color, uint> add;
            public CoverPen(ParticleLook look, List<Point> into)
            {
                rnd = () => rng.Next();
                add = (q, c, size) => look.Add(into, q, c, size);
            }
        }

        /// <summary>How many of the ground cover's things a triangle holds at this density (its area, mostly floor).</summary>
        int CoverCount(GroundTri g, int i, float density)
        {
            float want = g.area * density * Mathf.Clamp01(g.kind);
            int n = (int)want;
            var rng = new Rng((uint)i * 1597334677u ^ 0x68e31da4u);
            if (rng.Next() < want - n) n++;
            return n;
        }

        /// <summary>
        /// The ground cover's things from..to of a triangle, each from its own random sequence (so the far field's are
        /// the first of the near field's, and a thing is the same every time it is made). In patches (smooth noise
        /// picks what grows where): moss tufts, lichen rosettes, trefoils, fallen leaves, grit and pebbles, now and
        /// then a sprout with a bead; on the trodden way mostly grit. Plain arithmetic only (a worker thread runs it).
        /// </summary>
        void Cover(GroundTri g, int ti, int from, int to, List<Point> into, CoverPen pen)
        {
            if (to <= from) return;
            var (v, nr, c, t, _) = meshes[g.mesh];
            int ia = t[g.k], ib = t[g.k + 1], ic = t[g.k + 2];
            for (int e = from; e < to; e++)
            {
                var rng = new Rng((uint)ti * 2246822519u ^ (uint)e * 3266489917u ^ 0x165667b1u);
                float u = rng.Next(), w = rng.Next();
                if (u + w > 1) { u = 1 - u; w = 1 - w; }
                float s = 1 - u - w;
                Vector3 n = (nr[ia] * s + nr[ib] * u + nr[ic] * w).normalized;
                Vector3 p = v[ia] * s + v[ib] * u + v[ic] * w;
                Color lw = c[ia] * s + c[ib] * u + c[ic] * w;   // floor, bank, plateau, open
                if (lw.r + 0.35f * lw.g < rng.Next() * 0.9f) continue;   // mostly on the floor, a few up the walls
                // in drifts: some ground thick with them, some nearly bare
                float drift = Noise(p.x * 0.33f + 5.7f, p.z * 0.33f - 9.1f);
                if (rng.Next() > 0.12f + 1.3f * drift * drift) continue;
                Vector3 ta = Vector3.Cross(n, Mathf.Abs(n.z) < 0.9f ? Vector3.forward : Vector3.right).normalized, tb = Vector3.Cross(n, ta);
                float dark = 0.55f + 0.45f * lw.a;   // creases darker
                float way = WayDistance(p.x, p.z);
                float roll = rng.Next();
                pen.rng = rng;
                if (way < 0.45f)
                {
                    // the trodden way: grit, a pebble now and then, a leaf, a twig or a grain of crystal blown onto it
                    if (roll < 0.62f) Grit(p, ta, tb, 0.03f + 0.05f * rng.Next(), dark, ref rng, into);
                    else if (roll < 0.8f) Pebble(p, n, ta, tb, 0.018f + 0.03f * rng.Next(), dark, ref rng, into);
                    else if (roll < 0.9f) LeafOn(p, n, ta, tb, rng.Next() * 6.283f, 0.05f + 0.07f * rng.Next(), LitterColour(ref rng), dark, ref rng, into);
                    else if (roll < 0.96f) GroundCoverShapes.Twig(p, n, ta, tb, 0.1f + 0.2f * rng.Next(), dark, pen.rnd, pen.add);
                    else GroundCoverShapes.Crystals(p, n, ta, tb, 0.05f + 0.06f * rng.Next(), dark, pen.rnd, pen.add);
                    continue;
                }
                // what grows here: patches of moss (spore caps among it), of lichen, of trefoils and ferns, with leaves,
                // twigs, petals, seed pods, roots, crystals, shells, a feather and a rare sprout among them
                float moss = Noise(p.x * 0.55f + 3.1f, p.z * 0.55f - 1.7f), lichen = Noise(p.x * 0.4f - 7.3f, p.z * 0.4f + 2.2f), clover = Noise(p.x * 0.7f + 11.9f, p.z * 0.7f + 5.3f);
                float litter = Noise(p.x * 0.25f - 21.3f, p.z * 0.25f + 13.7f);
                float wMoss = moss * moss * 2.2f, wLichen = lichen * lichen * 1.6f, wClover = clover * clover * clover * 1.6f, wLeaf = 0.2f + 0.4f * litter, wGrit = 0.18f, wSprout = 0.07f;
                float wTwig = 0.06f + 0.22f * litter, wFern = 0.12f * clover + 0.04f, wPetal = 0.06f, wSeed = 0.05f + 0.06f * litter, wSpore = 0.03f + 0.12f * moss;
                float wCrystal = 0.05f, wRoot = 0.06f, wShell = 0.03f, wFeather = 0.025f;
                float sum = wMoss + wLichen + wClover + wLeaf + wGrit + wSprout + wTwig + wFern + wPetal + wSeed + wSpore + wCrystal + wRoot + wShell + wFeather, r = roll * sum;
                float size = rng.Next();
                if ((r -= wMoss) < 0) Moss(p, n, ta, tb, 0.04f + 0.14f * size * rng.Next() + 0.1f * moss, dark, ref rng, into);
                else if ((r -= wLichen) < 0) Lichen(p, n, ta, tb, 0.05f + 0.16f * size * rng.Next() + 0.06f * lichen, dark, ref rng, into);
                else if ((r -= wClover) < 0) Trefoil(p, n, ta, tb, 0.04f + 0.05f * size, rng.Next() * 6.283f, dark, ref rng, into);
                else if ((r -= wLeaf) < 0) LeafOn(p, n, ta, tb, rng.Next() * 6.283f, 0.05f + 0.09f * size, LitterColour(ref rng), dark, ref rng, into);
                else if ((r -= wGrit) < 0) Grit(p, ta, tb, 0.03f + 0.06f * size, dark, ref rng, into);
                else if ((r -= wSprout) < 0) Sprout(p, n, ta, tb, 0.04f + 0.08f * size, dark, ref rng, into);
                else if ((r -= wTwig) < 0) GroundCoverShapes.Twig(p, n, ta, tb, 0.12f + 0.33f * size, dark, pen.rnd, pen.add);
                else if ((r -= wFern) < 0) GroundCoverShapes.FernFrond(p, n, ta, tb, 0.15f + 0.25f * size, dark, pen.rnd, pen.add);
                else if ((r -= wPetal) < 0) GroundCoverShapes.Petal(p, n, ta, tb, 0.06f + 0.08f * size, dark, pen.rnd, pen.add);
                else if ((r -= wSeed) < 0) GroundCoverShapes.SeedPod(p, n, ta, tb, 0.08f + 0.1f * size, dark, pen.rnd, pen.add);
                else if ((r -= wSpore) < 0) GroundCoverShapes.SporeCaps(p, n, ta, tb, 0.06f + 0.1f * size, dark, pen.rnd, pen.add);
                else if ((r -= wCrystal) < 0) GroundCoverShapes.Crystals(p, n, ta, tb, 0.05f + 0.07f * size, dark, pen.rnd, pen.add);
                else if ((r -= wRoot) < 0) GroundCoverShapes.RootNodules(p, n, ta, tb, 0.15f + 0.2f * size, dark, pen.rnd, pen.add);
                else if ((r -= wShell) < 0) GroundCoverShapes.TinyShell(p, n, ta, tb, 0.05f + 0.06f * size, dark, pen.rnd, pen.add);
                else GroundCoverShapes.Feather(p, n, ta, tb, 0.12f + 0.18f * size, dark, pen.rnd, pen.add);
            }
        }

        /// <summary>One of the ground cover's points. Its size must not be 10, 20 or 40: those mark the plain ground
        /// points that give way to the carpet round the ant (the shader), and the cover must stay on it.</summary>
        void Add(List<Point> into, Vector3 p, Color linear, uint size)
        {
            if (size == 10 || size == 20 || size == 40) size--;
            into.Add(new Point { pos = p, col = PackLinear(linear, Ground, size) });   // brightened in the shader (_CoverGain)
        }

        /// <summary>A moss tuft: a soft dome of fine points, its tips lighter.</summary>
        void Moss(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, ref Rng rng, List<Point> into)
        {
            int count = 14 + (int)(size * 520);
            Color baseC = rng.Next() < 0.8f ? new Color(0.16f, 0.42f, 0.32f) : new Color(0.2f, 0.36f, 0.42f), tip = new Color(0.42f, 0.78f, 0.6f);
            for (int k = 0; k < count; k++)
            {
                float a = rng.Next() * 6.283f, up = Mathf.Sqrt(rng.Next()), side = Mathf.Sqrt(1 - up * up) * (0.6f + 0.4f * rng.Next());
                Vector3 q = p + (ta * Mathf.Cos(a) + tb * Mathf.Sin(a)) * (side * size) + n * (up * size * 0.55f + 0.02f);
                Add(into, q, Color.Lerp(baseC, tip, up * up) * (dark * (0.7f + 0.6f * rng.Next())), 6u + (uint)(rng.Next() * 3));
            }
        }

        /// <summary>A lichen rosette lying flat: lobed, its rim lighter than its middle; mostly pale mint, some
        /// blue-grey, a few ochre.</summary>
        void Lichen(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, ref Rng rng, List<Point> into)
        {
            float pick = rng.Next();
            Color col = pick < 0.6f ? new Color(0.5f, 0.78f, 0.72f) : pick < 0.85f ? new Color(0.45f, 0.55f, 0.75f) : new Color(0.8f, 0.66f, 0.32f);
            int lobes = 5 + (int)(rng.Next() * 3), count = 12 + (int)(size * 260);
            float turn = rng.Next() * 6.283f;
            for (int k = 0; k < count; k++)
            {
                float a = rng.Next() * 6.283f, rr = Mathf.Sqrt(rng.Next());
                float edge = 0.72f + 0.28f * Mathf.Cos(lobes * a + turn);
                Vector3 q = p + (ta * Mathf.Cos(a) + tb * Mathf.Sin(a)) * (rr * edge * size) + n * 0.014f;
                Add(into, q, col * (dark * (0.45f + 0.75f * rr) * (0.75f + 0.5f * rng.Next())), 6u + (uint)(rng.Next() * 2));
            }
        }

        /// <summary>A trefoil: three round lobes lying flat on a short stalk, green-teal.</summary>
        void Trefoil(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float turn, float dark, ref Rng rng, List<Point> into)
        {
            Color col = rng.Next() < 0.75f ? new Color(0.25f, 0.6f, 0.45f) : new Color(0.3f, 0.5f, 0.62f);
            Vector3 top = p + n * (0.02f + size * 0.25f);
            for (int lobe = 0; lobe < 3; lobe++)
            {
                float a = turn + lobe * 2.094f;
                Vector3 centre = top + (ta * Mathf.Cos(a) + tb * Mathf.Sin(a)) * (size * 0.42f);
                int count = 6 + (int)(size * 160);
                for (int k = 0; k < count; k++)
                {
                    float b = rng.Next() * 6.283f, rr = Mathf.Sqrt(rng.Next()) * size * 0.38f;
                    Add(into, centre + (ta * Mathf.Cos(b) + tb * Mathf.Sin(b)) * rr, col * (dark * (0.65f + 0.6f * rng.Next())), 6u);
                }
            }
        }

        /// <summary>A fallen leaf lying flat: almond-shaped, its midrib lighter.</summary>
        void LeafOn(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float dir, float size, Color col, float dark, ref Rng rng, List<Point> into)
        {
            Vector3 along = ta * Mathf.Cos(dir) + tb * Mathf.Sin(dir), across = Vector3.Cross(n, along);
            int count = 12 + (int)(size * 300);
            for (int k = 0; k < count; k++)
            {
                float t = rng.Next(), wv = (rng.Next() - 0.5f) * 2f * Mathf.Sin(t * Mathf.PI) * 0.42f;
                float rib = 1 + 0.7f * Mathf.Exp(-wv * wv * 70f);
                Vector3 q = p + along * ((t - 0.5f) * size) + across * (wv * size) + n * (0.012f + 0.01f * Mathf.Sin(t * Mathf.PI));
                Add(into, q, col * (dark * rib * (0.6f + 0.5f * rng.Next())), 6u + (uint)(rng.Next() * 2));
            }
        }

        Color LitterColour(ref Rng rng)
        {
            float pick = rng.Next();
            return pick < 0.3f ? new Color(0.62f, 0.32f, 0.22f)    // rust
                 : pick < 0.5f ? new Color(0.7f, 0.5f, 0.24f)      // amber
                 : pick < 0.7f ? new Color(0.45f, 0.3f, 0.55f)     // plum
                 : pick < 0.88f ? new Color(0.38f, 0.48f, 0.26f)   // olive
                 : new Color(0.22f, 0.45f, 0.48f);                 // dark teal
        }

        /// <summary>Grit: a few fine grains scattered flat, sand or blue-grey.</summary>
        void Grit(Vector3 p, Vector3 ta, Vector3 tb, float size, float dark, ref Rng rng, List<Point> into)
        {
            Color col = rng.Next() < 0.5f ? new Color(0.48f, 0.45f, 0.4f) : new Color(0.36f, 0.42f, 0.55f);
            int count = 3 + (int)(rng.Next() * 6);
            for (int k = 0; k < count; k++)
            {
                float a = rng.Next() * 6.283f, rr = Mathf.Sqrt(rng.Next()) * size;
                Add(into, p + (ta * Mathf.Cos(a) + tb * Mathf.Sin(a)) * rr + new Vector3(0, 0.012f, 0), col * (dark * (0.5f + 0.7f * rng.Next())), 5u + (uint)(rng.Next() * 3));
            }
        }

        /// <summary>A pebble: an ovoid of points, lighter on top.</summary>
        void Pebble(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float size, float dark, ref Rng rng, List<Point> into)
        {
            Color col = rng.Next() < 0.5f ? new Color(0.5f, 0.48f, 0.44f) : new Color(0.4f, 0.47f, 0.6f);
            float stretch = 1f + 0.6f * rng.Next(), turn = rng.Next() * 6.283f;
            Vector3 a1 = ta * Mathf.Cos(turn) + tb * Mathf.Sin(turn), a2 = Vector3.Cross(n, a1);
            int count = 8 + (int)(size * 500);
            for (int k = 0; k < count; k++)
            {
                float a = rng.Next() * 6.283f, up = Mathf.Sqrt(rng.Next()), side = Mathf.Sqrt(1 - up * up);
                Vector3 q = p + a1 * (Mathf.Cos(a) * side * size * stretch) + a2 * (Mathf.Sin(a) * side * size) + n * (up * size * 0.7f + 0.01f);
                Add(into, q, col * (dark * (0.4f + 0.8f * up)), 7u + (uint)(rng.Next() * 3));
            }
        }

        /// <summary>A sprout: one to three fine stems rising and leaning, each with a bead at its tip (coral, pale gold,
        /// lilac or white): the rare bright touch on the floor.</summary>
        void Sprout(Vector3 p, Vector3 n, Vector3 ta, Vector3 tb, float height, float dark, ref Rng rng, List<Point> into)
        {
            float pick = rng.Next();
            Color bead = pick < 0.3f ? new Color(0.95f, 0.5f, 0.5f) : pick < 0.55f ? new Color(0.95f, 0.82f, 0.5f) : pick < 0.8f ? new Color(0.72f, 0.6f, 0.95f) : new Color(0.85f, 0.92f, 0.95f);
            Color stem = new Color(0.3f, 0.55f, 0.45f);
            int stems = 1 + (int)(rng.Next() * 3);
            for (int sIdx = 0; sIdx < stems; sIdx++)
            {
                float a = rng.Next() * 6.283f, lean = 0.15f + 0.35f * rng.Next(), hh = height * (0.6f + 0.4f * rng.Next());
                Vector3 leanDir = ta * Mathf.Cos(a) + tb * Mathf.Sin(a);
                Vector3 baseP = p + leanDir * (0.01f * sIdx);
                int segs = 6 + (int)(hh * 70);
                for (int k = 0; k <= segs; k++)
                {
                    float f = k / (float)segs;
                    Add(into, baseP + n * (f * hh) + leanDir * (f * f * hh * lean), stem * (dark * (0.5f + 0.4f * f)), 5u);
                }
                Vector3 tip = baseP + n * hh + leanDir * (hh * lean);
                for (int k = 0; k < 7; k++)
                {
                    Vector3 off = new Vector3(rng.Next() - 0.5f, rng.Next() - 0.5f, rng.Next() - 0.5f) * 0.012f;
                    Add(into, tip + off, bead * (1.2f + 0.6f * rng.Next()), 7u);
                }
            }
        }

        /// <summary>
        /// Replace: vines and roots creeping over the canyon floor, as points: each a stem wandering from a seed with a
        /// steady curl, tapering, now and then a side shoot and small leaves lying on the ground; most along the ways
        /// but never on them (the glowing way stays clear). Stems are points round their upper half, lit from above,
        /// brighter at their nodes; a few are rose instead of the family's blue-green.
        /// </summary>
        void GrowVines()
        {
            if (vines <= 0 || heightArr == null) return;
            var rng = new Rng(4242);
            int made = 0;
            for (int tries = 0; made < vines && tries < vines * 60; tries++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, rng.Next()), z = Mathf.Lerp(world.minZ, world.maxZ, rng.Next());
                Color lw0 = LayerAt(x, z);
                if (lw0.r + lw0.g < 0.6f || world.HexDist(x, z) > -0.5f) continue;   // the floor and the walls
                float f = Focus(x, z);
                if (rng.Next() > 0.1f + 0.9f * f || WayDistance(x, z) < 0.6f) continue;
                bool rose = rng.Next() < 0.08f;
                Vine(x, z, rng.Next() * Mathf.PI * 2, 1.2f + 3.5f * rng.Next(), 0.014f + 0.012f * rng.Next(), rose, ref rng, 0);
                made++;
            }
            // fungal threads: fine, pale, branching often, lying right on the floor
            made = 0;
            for (int tries = 0; made < threads && tries < threads * 60; tries++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, rng.Next()), z = Mathf.Lerp(world.minZ, world.maxZ, rng.Next());
                Color lw0 = LayerAt(x, z);
                if (lw0.r < 0.5f || world.HexDist(x, z) > -0.5f) continue;
                if (rng.Next() > 0.1f + 0.9f * Focus(x, z) || WayDistance(x, z) < 0.5f) continue;
                Thread(x, z, rng.Next() * Mathf.PI * 2, 0.5f + 1.8f * rng.Next(), ref rng, 0);
                made++;
            }
        }

        void Thread(float x, float z, float dir, float length, ref Rng rng, int depth)
        {
            const float step = 0.025f;
            int steps = Mathf.Max(2, Mathf.RoundToInt(length / step));
            Color pale = new Color(0.55f, 0.66f, 0.72f);
            for (int i = 0; i < steps; i++)
            {
                dir += (rng.Next() - 0.5f) * 0.5f;
                x += Mathf.Cos(dir) * step;
                z += Mathf.Sin(dir) * step;
                if (LayerAt(x, z).r < 0.3f || world.HexDist(x, z) > -0.3f || WayDistance(x, z) < 0.4f) break;
                points.Add(new Point { pos = new Vector3(x, HeightAt(x, z) + 0.01f, z), col = PackLinear(pale * (0.16f * (0.6f + 0.8f * rng.Next()) / coverBrightness), Ground, 4u) });
                if (depth < 3 && rng.Next() < 0.05f) Thread(x, z, dir + (rng.Next() < 0.5f ? 0.8f : -0.8f), length * 0.4f, ref rng, depth + 1);
            }
        }

        void Vine(float x, float z, float dir, float length, float radius, bool rose, ref Rng rng, int depth)
        {
            const float step = 0.04f;
            int steps = Mathf.Max(2, Mathf.RoundToInt(length / step));
            float curl = (rng.Next() - 0.5f) * 0.1f;
            Color stem = (rose ? vineAccent : vineColour).linear * 2.2f, leaf = (rose ? vineAccent : leafColour).linear * 2.2f;
            for (int i = 0; i < steps; i++)
            {
                float u = i / (float)steps;
                dir += curl + (rng.Next() - 0.5f) * 0.2f;
                float cs = Mathf.Cos(dir), sn = Mathf.Sin(dir);
                x += cs * step;
                z += sn * step;
                Color lw = LayerAt(x, z);
                if (lw.r + lw.g < 0.35f || world.HexDist(x, z) > -0.3f || WayDistance(x, z) < 0.45f) break;
                float y = HeightAt(x, z), r = radius * (1 - 0.7f * u);
                var along = new Vector3(cs, 0, sn);
                var side = new Vector3(-sn, 0, cs);
                float node = 1 + 0.7f * Mathf.Exp(-Mathf.Pow(i % 9 - 4f, 2) * 0.5f);   // brighter at its nodes
                int around = 4 + Mathf.RoundToInt(r * 450);
                for (int k = 0; k < around; k++)
                {
                    float a = rng.Next() * Mathf.PI;   // its upper half
                    Vector3 p = new Vector3(x, y + r + 0.012f, z) + side * (Mathf.Cos(a) * r) + Vector3.up * (Mathf.Sin(a) * r)
                              + along * ((rng.Next() - 0.5f) * step);
                    float lit = (0.35f + 0.65f * Mathf.Sin(a)) * node * (0.7f + 0.6f * rng.Next()) * (0.6f + 0.4f * lw.a);
                    points.Add(new Point { pos = p, col = PackLinear(stem * (0.42f * lit / coverBrightness), Ground, 8u) });
                }
                // small leaves lying on the ground, now on one side, now on the other
                if (depth < 2 && rng.Next() < 0.07f)
                {
                    float la = dir + (rng.Next() < 0.5f ? 1f : -1f) * (0.6f + 0.5f * rng.Next());
                    Leaf(new Vector3(x, y + 0.01f, z), la, 0.05f + 0.06f * rng.Next(), leaf, ref rng);
                }
                // a side shoot
                if (depth < 1 && rng.Next() < 0.03f)
                    Vine(x, z, dir + (rng.Next() < 0.5f ? 0.9f : -0.9f), length * (0.25f + 0.3f * rng.Next()), r * 0.7f, rose, ref rng, depth + 1);
            }
        }

        void Leaf(Vector3 at, float dir, float size, Color colour, ref Rng rng)
        {
            float cs = Mathf.Cos(dir), sn = Mathf.Sin(dir);
            int n = 18 + Mathf.RoundToInt(size * 500);
            for (int k = 0; k < n; k++)
            {
                // an almond shape: along its length, narrower toward both ends; the midrib brighter
                float t = rng.Next(), w = (rng.Next() - 0.5f) * 2f * Mathf.Sin(t * Mathf.PI) * 0.45f;
                float lx = t * size, lz = w * size;
                var p = new Vector3(at.x + cs * lx - sn * lz, 0, at.z + sn * lx + cs * lz);
                p.y = HeightAt(p.x, p.z) + 0.014f;
                float rib = 1 + 0.8f * Mathf.Exp(-w * w * 60f);
                points.Add(new Point { pos = p, col = PackLinear(colour * (0.3f * rib * (0.7f + 0.6f * rng.Next()) / coverBrightness), Ground, 8u) });
            }
        }

        /// <summary>The terrain's layer weights at (x, z): floor, bank, plateau, open (from the carpet's map).</summary>
        public Color LayerAt(float x, float z)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((x - mapX0) / MapCell), 0, mapW - 1), j = Mathf.Clamp(Mathf.FloorToInt((z - mapZ0) / MapCell), 0, mapH - 1);
            return layerArr[j * mapW + i];
        }

        /// <summary>The terrain's top surface height at (x, z) (from the carpet's map; bilinear).</summary>
        public float HeightAt(float x, float z)
        {
            float fx = (x - mapX0) / MapCell - 0.5f, fz = (z - mapZ0) / MapCell - 0.5f;
            int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, mapW - 2), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, mapH - 2);
            float tx = Mathf.Clamp01(fx - i), tz = Mathf.Clamp01(fz - j);
            float a = heightArr[j * mapW + i], b = heightArr[j * mapW + i + 1], c = heightArr[(j + 1) * mapW + i], d = heightArr[(j + 1) * mapW + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>How far (x, z) is from the nearest glowing way (units; far: large).</summary>
        public float WayDistance(float x, float z)
        {
            if (focus == null || focus.Count == 0 || focusD2 == null) return 99f;
            int i = Mathf.Clamp(Mathf.RoundToInt((x - fx0) / FocusCell), 0, fw - 1);
            int j = Mathf.Clamp(Mathf.RoundToInt((z - fz0) / FocusCell), 0, fh - 1);
            float d2 = focusD2[j * fw + i];
            return d2 == float.MaxValue ? 99f : Mathf.Sqrt(d2);
        }

        /// <summary>The squared distance to the nearest way, on a coarse grid (for Focus).</summary>
        void BuildFocus()
        {
            fx0 = world.minX - 2;
            fz0 = world.minZ - 2;
            fw = Mathf.CeilToInt((world.maxX - world.minX + 4) / FocusCell) + 1;
            fh = Mathf.CeilToInt((world.maxZ - world.minZ + 4) / FocusCell) + 1;
            focusD2 = new float[fw * fh];
            for (int i = 0; i < focusD2.Length; i++) focusD2[i] = float.MaxValue;
            if (focus == null) return;
            int rc = Mathf.CeilToInt(focusReach * 3 / FocusCell);
            foreach (List<Vector2> track in focus)
            {
                if (track == null) continue;
                for (int k = 0; k < track.Count; k++)
                {
                    Vector2 a = track[k], b = k + 1 < track.Count ? track[k + 1] : a;
                    int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.7f));
                    for (int s = 0; s < steps; s++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                        int ci = Mathf.RoundToInt((p.x - fx0) / FocusCell), cj = Mathf.RoundToInt((p.y - fz0) / FocusCell);
                        for (int j = Mathf.Max(0, cj - rc); j <= Mathf.Min(fh - 1, cj + rc); j++)
                        for (int i = Mathf.Max(0, ci - rc); i <= Mathf.Min(fw - 1, ci + rc); i++)
                        {
                            float dx = fx0 + i * FocusCell - p.x, dz = fz0 + j * FocusCell - p.y;
                            float d2 = dx * dx + dz * dz;
                            if (d2 < focusD2[j * fw + i]) focusD2[j * fw + i] = d2;
                        }
                    }
                }
            }
        }

        /// <summary>1 on the ways the ant is led, falling toward 0 away from them (1 everywhere if there are none).</summary>
        public float Focus(float x, float z)
        {
            if (focus == null || focus.Count == 0 || focusD2 == null) return 1f;
            int i = Mathf.Clamp(Mathf.RoundToInt((x - fx0) / FocusCell), 0, fw - 1);
            int j = Mathf.Clamp(Mathf.RoundToInt((z - fz0) / FocusCell), 0, fh - 1);
            float d2 = focusD2[j * fw + i];
            return d2 == float.MaxValue ? 0f : Mathf.Exp(-d2 / (focusReach * focusReach));
        }

        /// <summary>A plant's point colours as its region shows it (the Plant shader's hue rotation), packed. Like a
        /// scan: pale, but its texture's light and dark kept (against the plant's own mean, a little exaggerated), and
        /// some of its own colour.</summary>
        uint[] Colours(PlantInstance pi)
        {
            Color scanColour = scanOverride.a > 0 ? scanOverride * (pi.far ? 0.6f : 1f) : pi.scan;
            if (pi.relic) scanColour.a = 0.5f;   // (a different key) a thing keeps more of its own colour
            var key = (pi.plant, pi.hue, pi.sat, pi.glow, scanColour);
            if (colours.TryGetValue(key, out uint[] cached)) return cached;
            Color32[] src = pi.plant.pointColors;
            var cols = new uint[src.Length];
            float a = pi.hue * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            // the plant's mean brightness (its glowing parts aside)
            double sum = 0;
            int count0 = 0;
            for (int k = 0; k < src.Length; k++)
                if (src[k].a <= 127) { sum += Linear[src[k].r] * 0.3f + Linear[src[k].g] * 0.55f + Linear[src[k].b] * 0.15f; count0++; }
            float mean = count0 > 0 ? Mathf.Max(1e-3f, (float)(sum / count0)) : 0.2f;
            for (int k = 0; k < src.Length; k++)
            {
                Color lin = ((Color)src[k]).linear;
                // the hue rotation in YIQ, as AG_HueShift
                float y = lin.r * 0.299f + lin.g * 0.587f + lin.b * 0.114f;
                float i = lin.r * 0.596f - lin.g * 0.274f - lin.b * 0.322f;
                float q = lin.r * 0.211f - lin.g * 0.523f + lin.b * 0.312f;
                float i2 = (i * cs - q * sn) * pi.sat, q2 = (i * sn + q * cs) * pi.sat;
                var shifted = new Color(Mathf.Max(0, y + 0.956f * i2 + 0.621f * q2), Mathf.Max(0, y - 0.272f * i2 - 0.647f * q2), Mathf.Max(0, y - 1.106f * i2 + 1.703f * q2));
                bool glows = src[k].a > 127;
                float lum = shifted.r * 0.3f + shifted.g * 0.55f + shifted.b * 0.15f;
                // flat scan (grain 0): brighter where the surface is lighter, compressed; with grain, the texture's
                // light and dark against the plant's mean, a little exaggerated
                float flat = 0.18f + 1.3f * Mathf.Sqrt(lum);
                float rel = Mathf.Clamp(0.05f + 0.95f * Mathf.Pow(lum / mean, 2.2f), 0.02f, 3.6f);
                Color scan = scanColour.linear * Mathf.Lerp(flat, rel, grain);
                bool tinted = mode == Mode.Replace && glowPalette != null;
                Color c = glows ? (tinted ? Color.Lerp(shifted, Color.white * (0.55f + 0.9f * Mathf.Sqrt(lum)), 0.85f)
                                          : Color.Lerp(Color.Lerp(shifted, pi.glow.linear, 0.7f), scanColour.linear, mode == Mode.Replace ? 0.5f : 0f))
                                : Color.Lerp(shifted * 1.6f, scan, pi.relic ? scanMix * 0.4f : scanMix);
                cols[k] = Pack(c.gamma, glows ? Glow : Plant);
            }
            colours[key] = cols;
            return cols;
        }

        /// <summary>Pack from linear colour (the sRGB curve in managed code: safe on a worker thread).</summary>
        static uint PackLinear(Color c, uint kind, uint size = 0) => Pack(new Color(ToGamma(c.r), ToGamma(c.g), ToGamma(c.b)), kind, size);

        static float ToGamma(float x) => x <= 0.0031308f ? 12.92f * x : 1.055f * (float)System.Math.Pow(x, 1 / 2.4) - 0.055f;

        /// <summary>Gamma-space rgb in three bytes; the fourth says what the point is (top two bits) and how big
        /// (low six: × size/8; 0 = its own size).</summary>
        static uint Pack(Color c, uint kind, uint size = 0)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 255), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 255), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 255), 0, 255);
            return r | (g << 8) | (b << 16) | (((kind << 6) | (size & 63)) << 24);
        }
    }
}
