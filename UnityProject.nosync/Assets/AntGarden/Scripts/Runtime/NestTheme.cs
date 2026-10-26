using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's colours for one segment of the installation's colour wheel. The lights in the room are
    /// set on a hue wheel cut into twelve 30° segments, and each segment has one nest of its own, in its own tones
    /// (02_Concepts/Particle_Styles_v1/mapping.md): cyan (180°) is the blue fungal forest, orange (30°) the warm amber
    /// nest, red (0°) the ancient temple, azure (210°) the giant's relics. Everything the particle look draws takes
    /// its colours from here: the plants' bodies and glows, the floor and walls, the mist and sky, the guiding light,
    /// the vines, the small life, the dark under the ants. A nest is drawn in the theme of its region's biome
    /// (For); the shapes, the motion and every interaction are the same in every one. What grows and lies in it is
    /// its own as well: which of the world's plants grow there (scatterPlants), which of the particle nest's own
    /// (labPlants), the things only it has (things: NestThingsThemed), which plants glow a little there (glowParts),
    /// what stands beside its way (objects) and what is at its heart (heart).
    /// </summary>
    public sealed class NestTheme
    {
        /// <summary>A part of a plant that glows a little in this nest only (the plant's own material does not glow):
        /// from..to of its height (0..1), where its texture is at least lumMin of its plant's mean brightness.</summary>
        public struct GlowPart { public string plant; public float from, to, lumMin, amount; }

        /// <summary>One of the nest's own things, and how often it is picked among them.</summary>
        public struct Thing { public string kind; public float weight; }

        public string id;
        public float hue;                       // the segment's centre on the wheel (degrees)
        public string segmentZh, segmentEn;     // its name on the wheel
        public string nameZh, nameEn;           // the nest's name (chapter cards, the map)

        // plants: their bodies in one family of near colours (0-5) with two muted accents (6, 7); their glows mostly
        // in the segment's own light (0, 1, 2, 6), now and then an accent (3, 4, 5, 7)
        public Color[] body, glow;
        public float bodyAccent = 0.05f, glowAccent = 0.06f;
        public Color scan;                      // one pale colour every plant starts from

        // the ground (floor, walls, plateau, the walls' top edge) and the air
        public Color ground, floor, bank, plateau, edge;
        public Color fog, skyTop, skyHorizon, motes;

        // light: the guiding threads and beams, their pools, the floor's glowing grains, the glass bulbs' light, the
        // player's ring, the specks on the floor
        public Color threadCore, threadHalo, beacon, grainGlow, lampCore, ring, trail;

        // the solid splats' lighting (moon, fill, the thin rim, glints), dew, the vines' dark and their leaves
        public Color moon, fill, rim, glint, dew, vineDark, vineLeaf;

        public Color glowDeep;                  // what a plant's glowing part leans toward (the light, deep and saturated)
        public Color[] microLife;               // the small life's colours (8)
        public Color antBody;                   // the dark under the ants' points
        public Color antTint;                   // every ant's colours lean to this (a: how far), keeping their light and dark
        public Color antRim, antPale;           // the ants' rim light, and the pale their points lean to
        public float exposureLift;              // stops added to the particle nest's darker night (a darker palette needs more)
        public float temperature = -8;          // the colour grading's white balance (the profile's -8 cools; a warm nest wants it neutral)
        public float saturation = 6;            // and its saturation (the profile's +6; a vivid palette went neon with it)
        public Color bloomTint = new Color(0.85f, 1f, 0.97f);   // what the glow's bloom is tinted (the profile's mint turns gold green)
        public string[] labPlants;              // which of the particle nest's own plants grow here (Game.LabPlants)
        public string[] glassKinds;             // plants whose glowing head is glass: a broken shell round a light of its own
        public string[] headGlowKinds;          // and plants whose head alone glows, solid (with how far up it starts)
        public float[] headGlowFrom;
        public string[] scatterPlants;          // the world's plants that grow in this nest (null: all of them)
        public GlowPart[] glowParts = new GlowPart[0];
        public Thing[] things = new Thing[0];   // the things only this nest has (SolidThings, NestThingsThemed)
        public float ownShare;                  // how many of the floor's things are its own
        public float dampScale = 1f;            // how much of the damp green growth (moss, clover...) a dry nest keeps
        public string objects = "relics";       // what stands beside the way (LabNest.PlaceObjects): relics, temple, none
        public string heart;                    // what is at the heart (SolidThings.Heart): null, shards, shrine, dew, pool, watch, garden, honeypots, button
        public string[] noKinds = new string[0];   // the floor's things it does not have (pink petals in a warm nest)
        public float vineGlow = 1f;             // how much the vines' veins, pores and tips glow (a saturated light needs less)
        public float vineDarken = 0.5f;         // how far the vines' stems go from the plants' tints toward vineDark (at least)
        public float farDim = 1f;               // the huge trees beyond the walls (Scatter.Surroundings): how bright, muted (1: as the plants)
        public float gateGain = 1f;             // the light gates to the other nests (a saturated light made them glare behind a heart)
        // how much brighter what is right round the ant is, and what it walks past lights up (a vivid palette glared
        // with the blue forest's: the vines beside the ant went neon)
        public float nearLift = 0.9f, walkGain = 1f;
        public float rigidGain = 1f, rigidTint; // the props' and relics' own colours: how bright, how far toward body[0]
        public string[] touchParts = new string[0];   // which of glowParts' plants an antenna can light (not every grass tuft)
        public Vector2 ownBand;                  // the plants' own colours kept within this hue band (centre, half-width; 0: any)
        public bool buildingsGlowInParts;       // the props (and own clay buildings) light only in their glowing parts, not whole
        public string wayThing;                 // one of things that also lines the way, close enough to touch (SolidThings)
        public float wayEvery = 5f;             // about how far apart along the way

        /// <summary>The theme of the particle nest now being played (set by Game before its parts start).</summary>
        public static NestTheme Current;

        /// <summary>The theme for a region's biome (the blue forest's for any it does not know).</summary>
        public static NestTheme For(string biome) => biome == "amber" ? Orange : biome == "kiln" ? Red
            : biome == "honey" ? Yellow : biome == "leafcut" ? Chartreuse : biome == "moss" ? Green : biome == "verdigris" ? SpringGreen
            : biome == "azure" ? Azure : biome == "cistern" ? Blue : Cyan;

        /// <summary>
        /// A nest built from its effect images (02_Concepts/Particle_Styles_v1, v2): its plant and glow palettes, floor
        /// and fog taken from them (the k-means of their lit surfaces and glows), with two accent colours in each palette
        /// as the blue nest has (a scene leans to its segment's hue; it is not all one colour); the plants of our own whose
        /// colours fall in it (labPlants), and those of the world's that suit it (scatter). Its other colours are worked
        /// out from those, as the built themes relate theirs.
        /// </summary>
        static NestTheme Basic(string id, float hue, string seg, string segEn, string zh, string en, Color[] body6, Color[] bodyAccents,
            Color[] glow4, Color[] glowAccents, Color floor, Color fog, string[] lab, string[] scatter, Thing[] things = null, float ownShare = 0.3f,
            string objects = "none", float exposureLift = 0.3f, float vineGlow = 1f, float fillGain = 1.45f, string wayThing = null, string heart = null)
        {
            Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k);
            Color g = glow4[0];
            var body = new Color[8];
            for (int i = 0; i < 6; i++) body[i] = Color.Lerp(Grey(body6[i]), body6[i], 0.7f);   // (muted: as tints the effect images' vivid colours made whole canopies glare)
            body[6] = bodyAccents[0];
            body[7] = bodyAccents[1];
            var glow = new Color[8];
            for (int i = 0; i < 4; i++) glow[i] = glow4[i];
            glow[4] = glowAccents[0];
            glow[5] = Color.Lerp(glow4[1], Color.white, 0.35f);
            glow[6] = Color.Lerp(glow4[2], Color.white, 0.35f);
            glow[7] = glowAccents[1];
            var micro = new Color[8];
            for (int i = 0; i < 8; i++) micro[i] = Mul(body[i], 0.85f);
            Color.RGBToHSV(g, out float gh, out _, out _);
            return new NestTheme
            {
                id = id, hue = hue, segmentZh = seg, segmentEn = segEn, nameZh = zh, nameEn = en,
                body = body, glow = glow, scan = Color.Lerp(body[5], Color.white, 0.5f), bodyAccent = 0.1f, glowAccent = 0.16f,
                ground = floor, floor = floor, bank = Color.Lerp(floor, body[3], 0.3f), plateau = Mul(floor, 0.8f), edge = Color.Lerp(floor, Color.white, 0.6f),
                fog = fog, skyTop = Mul(fog, 0.18f), skyHorizon = Mul(fog, 0.85f),
                motes = Color.Lerp(new Color(1.6f, 1.6f, 1.6f), Mul(g, 1.7f), 0.4f),
                threadCore = Color.Lerp(g, Color.white, 0.75f), threadHalo = g, beacon = Color.Lerp(g, Color.white, 0.15f),
                // (the ring at the heart and the trail: as bright as the blue nest's cyan reads, however light this hue is)
                grainGlow = Mul(g, 0.9f), lampCore = Color.Lerp(g, Color.white, 0.85f), ring = Mul(g, 0.62f / Mathf.Max(0.62f, Lum(g))),
                trail = Mul(Color.Lerp(g, Color.white, 0.3f), 0.7f / Mathf.Max(0.7f, Lum(g))),
                moon = Color.Lerp(new Color(0.76f, 0.76f, 0.74f), g, 0.12f), fill = Mul(fog, fillGain), rim = Mul(fog, 0.5f),
                glint = Mul(body[4], 0.75f), dew = Mul(Color.Lerp(body[4], Color.white, 0.2f), 0.85f),
                // (the vines' stems dark and muted, as the blue forest's are: a saturated palette would make them bright)
                vineDark = Mul(Color.Lerp(Grey(body[1]), body[1], 0.5f), 0.2f), vineLeaf = Mul(Color.Lerp(body[2], body[6], 0.3f), 0.6f),
                microLife = micro,
                antBody = Mul(fog, 0.3f), antTint = new Color(g.r, g.g, g.b, 0.7f), antRim = Color.Lerp(g, Color.white, 0.3f),
                antPale = Color.Lerp(body[4], Color.white, 0.4f), glowDeep = Color.HSVToRGB(gh, 0.82f, 0.85f),
                temperature = 0, saturation = -12, bloomTint = Color.Lerp(Color.white, g, 0.12f), exposureLift = exposureLift,
                glassKinds = new string[0], headGlowKinds = new string[0], headGlowFrom = new float[0],
                labPlants = lab, scatterPlants = scatter, objects = objects, things = things ?? new Thing[0], ownShare = ownShare,
                // (the huge trees beyond the walls in a vivid palette glared green through the hearts: dark shapes in the fog)
                noKinds = new[] { "Petal", "Blossom" }, vineGlow = vineGlow, vineDarken = 0.7f, farDim = 0.3f, gateGain = 0.28f, nearLift = 0.45f, walkGain = 0.55f, wayThing = wayThing, wayEvery = 4f, heart = heart,
            };
        }

        static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        static Color Grey(Color c) { float l = Lum(c); return new Color(l, l, l); }

        // first versions of the scenes that had only their effect images: palettes from those images; the new plants
        // (Tripo v3.1) placed by the hue of their bodies (curl_d 47°, curl_a 51°, puff_a 62°; curl_e 78°, frond 84°;
        // curl_b 107°, curl_c 112°; coral_b 134°; puff_b 28° is the amber nest's)
        public static readonly NestTheme Yellow = Basic("yellow", 60, "黄", "Yellow", "蜜罐粮仓", "The Honeypot Granary",
            new[] { new Color(0.62f, 0.42f, 0.12f), new Color(0.58f, 0.42f, 0.16f), new Color(0.66f, 0.50f, 0.14f), new Color(0.62f, 0.48f, 0.26f), new Color(0.74f, 0.60f, 0.16f), new Color(0.84f, 0.70f, 0.30f) },
            new[] { new Color(0.52f, 0.60f, 0.44f), new Color(0.66f, 0.46f, 0.40f) },   // a dusty sage, a muted rose-brown
            new[] { new Color(1.00f, 0.92f, 0.20f), new Color(1.00f, 0.95f, 0.30f), new Color(1.00f, 0.90f, 0.42f), new Color(0.98f, 0.96f, 0.55f) },
            new[] { new Color(0.78f, 0.95f, 0.80f), new Color(0.95f, 0.72f, 0.60f) },   // a pale mint, a soft coral
            new Color(0.62f, 0.42f, 0.20f), new Color(0.15f, 0.10f, 0.04f),
            new[] { "curl_d_cluster", "curl_a_crozier", "puff_a_stalked" },
            new[] { "seed_pile", "seed_husk", "dry_grass_tuft", "pebble_cluster", "fallen_leaf", "fungus_garden", "tiny_mushrooms" },
            new[] { new Thing { kind = "HoneyDrops", weight = 30 }, new Thing { kind = "GoldBalls", weight = 14 } }, 0.3f, "none", 0.22f, 0.6f, heart: "honeypots");
        public static readonly NestTheme Chartreuse = Basic("chartreuse", 90, "黄绿", "Chartreuse", "切叶蚁菌圃", "The Leafcutters' Garden",
            new[] { new Color(0.52f, 0.66f, 0.20f), new Color(0.58f, 0.62f, 0.42f), new Color(0.46f, 0.66f, 0.14f), new Color(0.60f, 0.64f, 0.46f), new Color(0.56f, 0.68f, 0.32f), new Color(0.78f, 0.88f, 0.56f) },
            new[] { new Color(0.82f, 0.80f, 0.74f), new Color(0.58f, 0.38f, 0.24f) },   // the fungus garden's pale grey-white, the spent leaves' rust
            new[] { new Color(0.66f, 1.00f, 0.22f), new Color(0.78f, 1.00f, 0.40f), new Color(0.86f, 1.00f, 0.52f), new Color(0.72f, 1.00f, 0.30f) },
            new[] { new Color(0.96f, 0.95f, 0.82f), new Color(0.95f, 0.76f, 0.46f) },   // a pale cream, a muted amber
            new Color(0.56f, 0.52f, 0.24f), new Color(0.10f, 0.11f, 0.04f),
            new[] { "frond_a_feather", "curl_e_corkscrew" },
            new[] { "fungus_garden", "tiny_mushrooms", "sponge_mound", "fallen_leaf", "pebble_cluster", "boulder_moss", "coral_fungus" },
            new[] { new Thing { kind = "LeafSail", weight = 34 } }, 0.3f, "none", 0.4f, 0.32f, heart: "garden");
        public static readonly NestTheme Green = Basic("green", 120, "绿", "Green", "苔藓森林", "The Moss Forest",
            new[] { new Color(0.28f, 0.68f, 0.30f), new Color(0.44f, 0.62f, 0.36f), new Color(0.36f, 0.68f, 0.44f), new Color(0.26f, 0.64f, 0.32f), new Color(0.40f, 0.70f, 0.46f), new Color(0.56f, 0.86f, 0.62f) },
            new[] { new Color(0.58f, 0.36f, 0.26f), new Color(0.74f, 0.80f, 0.80f) },   // the moss stalks' red-brown, the dew's clear grey
            new[] { new Color(0.32f, 1.00f, 0.46f), new Color(0.46f, 1.00f, 0.58f), new Color(0.58f, 1.00f, 0.68f), new Color(0.40f, 1.00f, 0.54f) },
            new[] { new Color(0.70f, 0.95f, 0.92f), new Color(0.96f, 0.88f, 0.56f) },   // a pale water, a soft gold
            new Color(0.42f, 0.54f, 0.22f), new Color(0.05f, 0.11f, 0.05f),
            new string[0],   // (no curling plants: they are the blue forest's; its own are the moss stalks and star moss)
            new[] { "boulder_moss", "tiny_mushrooms", "fallen_leaf", "pebble_cluster", "cup_fungus", "sponge_mound" },
            new[] { new Thing { kind = "StarMoss", weight = 30 }, new Thing { kind = "Sporophyte", weight = 26 }, new Thing { kind = "DewDrop", weight = 18 } },
            0.36f, "none", 0.4f, 0.42f, heart: "dew");
        public static readonly NestTheme SpringGreen = Basic("springgreen", 150, "青绿", "Spring green", "铜绿机城", "The Verdigris City",
            new[] { new Color(0.34f, 0.66f, 0.52f), new Color(0.56f, 0.66f, 0.46f), new Color(0.32f, 0.66f, 0.54f), new Color(0.40f, 0.66f, 0.52f), new Color(0.56f, 0.66f, 0.50f), new Color(0.64f, 0.86f, 0.74f) },
            new[] { new Color(0.62f, 0.52f, 0.30f), new Color(0.64f, 0.42f, 0.30f) },   // dull brass, copper
            new[] { new Color(0.58f, 1.00f, 0.80f), new Color(0.56f, 1.00f, 0.70f), new Color(0.50f, 1.00f, 0.84f), new Color(0.60f, 1.00f, 0.90f) },
            new[] { new Color(0.96f, 0.88f, 0.56f), new Color(0.80f, 0.92f, 1.00f) },   // a pale gold, a blue-white
            new Color(0.30f, 0.56f, 0.48f), new Color(0.05f, 0.12f, 0.11f),
            new[] { "coral_b_branching" },
            new[] { "pebble_cluster", "boulder_moss", "fallen_leaf", "glass_sponge", "anemone_fungus", "jelly_fungus", "lantern_pods" },
            new[] { new Thing { kind = "Gear", weight = 26 }, new Thing { kind = "Wire", weight = 20 } }, 0.3f, "machine", 0.22f, 0.42f, 1.45f, "Wire", "watch");
        static readonly NestTheme AzureBasic = Basic("azure", 210, "天蓝", "Azure", "巨人遗物", "The Giant's Relics",
            // (turned 10-13° from the effect image's, toward 210°: the scene measured 197°, at the cyan nest's edge)
            new[] { new Color(0.20f, 0.40f, 0.78f), new Color(0.40f, 0.60f, 0.74f), new Color(0.22f, 0.44f, 0.80f), new Color(0.46f, 0.62f, 0.72f), new Color(0.30f, 0.54f, 0.80f), new Color(0.62f, 0.78f, 0.88f) },
            new[] { new Color(0.90f, 0.86f, 0.76f), new Color(0.64f, 0.42f, 0.30f) },   // porcelain cream, rust
            new[] { new Color(0.36f, 0.64f, 1.00f), new Color(0.48f, 0.75f, 1.00f), new Color(0.56f, 0.80f, 1.00f), new Color(0.64f, 0.84f, 1.00f) },
            new[] { new Color(0.96f, 0.92f, 0.78f), new Color(0.96f, 0.62f, 0.56f) },   // a warm cream, a faded thread red
            new Color(0.20f, 0.33f, 0.62f), new Color(0.030f, 0.062f, 0.140f),
            new string[0],
            new[] { "bracket_shelf", "trunk_night", "spore_stalk", "coral_fungus", "jelly_fungus", "cup_fungus", "pebble_cluster", "fallen_leaf" },
            new[] { new Thing { kind = "DewDrop", weight = 20 } }, 0.15f, "giant", 0.95f, 0.85f, 1.45f, "DewDrop", "button");   // (the dew drops: the giant's glass marbles)
        // (its relics dimmer: the white porcelain blew out once its exposure was lifted)
        public static readonly NestTheme Azure = AzureTuned();
        static NestTheme AzureTuned() { NestTheme t = AzureBasic; t.rigidGain = 0.55f; return t; }

        public static readonly NestTheme Blue = Basic("cobalt", 240, "蓝", "Blue", "雨水潭", "The Rain Cistern",
            new[] { new Color(0.28f, 0.34f, 0.80f), new Color(0.40f, 0.44f, 0.78f), new Color(0.30f, 0.36f, 0.84f), new Color(0.46f, 0.50f, 0.80f), new Color(0.36f, 0.42f, 0.86f), new Color(0.58f, 0.62f, 0.90f) },
            new[] { new Color(0.74f, 0.72f, 0.66f), new Color(0.44f, 0.34f, 0.28f) },   // pale pebbles, root brown
            new[] { new Color(0.40f, 0.46f, 1.00f), new Color(0.52f, 0.60f, 1.00f), new Color(0.62f, 0.68f, 1.00f), new Color(0.36f, 0.42f, 1.00f) },
            new[] { new Color(0.86f, 0.90f, 1.00f), new Color(0.74f, 0.66f, 0.96f) },   // a pale silver, a faint violet
            new Color(0.24f, 0.28f, 0.62f), new Color(0.03f, 0.04f, 0.12f),
            new string[0],
            new[] { "root_arch", "pebble_cluster", "trunk_night", "boulder_moss", "fallen_leaf", "glass_sponge", "jelly_fungus" },
            new[] { new Thing { kind = "Puddle", weight = 30 } }, 0.3f, "none", 1.5f, 0.9f, 3.4f, "Puddle", "pool");

        /// <summary>Cyan, 180°: the blue fungal forest (the first particle nest, v15).</summary>
        public static readonly NestTheme Cyan = new NestTheme
        {
            id = "cyan", hue = 180, segmentZh = "青", segmentEn = "Cyan", nameZh = "蓝色真菌林", nameEn = "Blue Fungal Forest",
            // the colour card (07_Outputs/particle_nest/palette_card.jpg): sage, moss, teal-green, silver-blue and bone
            // for the bodies; glows mostly cyan breath and bone, a few muted coral, violet or straw
            body = new[]
            {
                new Color(0.72f, 0.80f, 0.64f), new Color(0.66f, 0.78f, 0.50f), new Color(0.56f, 0.78f, 0.72f), new Color(0.64f, 0.76f, 0.78f),
                new Color(0.76f, 0.80f, 0.72f), new Color(0.54f, 0.70f, 0.64f), new Color(0.78f, 0.88f, 0.56f), new Color(0.84f, 0.78f, 0.60f),
            },
            glow = new[]
            {
                new Color(0.55f, 0.95f, 0.98f), new Color(0.70f, 0.96f, 0.90f), new Color(0.78f, 0.95f, 0.70f), new Color(0.88f, 0.72f, 0.74f),
                new Color(0.78f, 0.74f, 0.90f), new Color(0.88f, 0.84f, 0.68f), new Color(0.85f, 0.92f, 0.92f), new Color(0.88f, 0.76f, 0.74f),
            },
            scan = new Color(0.90f, 0.94f, 0.88f),
            ground = new Color(0.5f, 0.66f, 0.62f), floor = new Color(0.45f, 0.68f, 0.62f), bank = new Color(0.55f, 0.68f, 0.62f),
            plateau = new Color(0.40f, 0.55f, 0.55f), edge = new Color(0.80f, 0.86f, 0.82f),
            fog = new Color(0.075f, 0.13f, 0.13f), skyTop = new Color(0.012f, 0.025f, 0.027f), skyHorizon = new Color(0.06f, 0.11f, 0.11f),
            motes = new Color(1.5f, 1.65f, 1.5f),
            threadCore = new Color(0.92f, 0.95f, 0.9f), threadHalo = new Color(0.55f, 0.9f, 0.95f), beacon = new Color(0.62f, 0.86f, 0.84f),
            grainGlow = new Color(0.42f, 0.86f, 0.84f), lampCore = new Color(0.95f, 0.98f, 0.96f), ring = new Color(0.5f, 0.9f, 0.95f),
            trail = new Color(0.72f, 0.92f, 0.88f),
            moon = new Color(0.66f, 0.72f, 0.72f), fill = new Color(0.13f, 0.18f, 0.18f), rim = new Color(0.05f, 0.065f, 0.065f),
            glint = new Color(0.55f, 0.62f, 0.6f), dew = new Color(0.62f, 0.7f, 0.7f), vineDark = new Color(0.1f, 0.16f, 0.15f),
            vineLeaf = new Color(0.3f, 0.42f, 0.26f),
            microLife = new[]
            {
                // silver-blue, bone, sage, dusty violet, ochre straw, teal-green, moss, coral
                new Color(0.62f, 0.70f, 0.72f), new Color(0.80f, 0.80f, 0.74f), new Color(0.48f, 0.53f, 0.42f), new Color(0.49f, 0.43f, 0.59f),
                new Color(0.71f, 0.60f, 0.35f), new Color(0.30f, 0.45f, 0.42f), new Color(0.45f, 0.51f, 0.31f), new Color(0.70f, 0.46f, 0.48f),
            },
            antBody = new Color(0.02f, 0.05f, 0.06f), antTint = Color.clear, antRim = new Color(0.6f, 0.85f, 1.0f), antPale = new Color(0.80f, 0.90f, 0.86f),
            glowDeep = new Color(0.22f, 0.78f, 0.82f),
            glassKinds = new[] { "spore_stalk" }, headGlowKinds = new string[0], headGlowFrom = new float[0],
            labPlants = new[] { "curl_a_crozier", "curl_b_double", "curl_c_hook", "curl_d_cluster", "puff_b_lantern", "coral_b_branching", "frond_a_feather", "curl_e_corkscrew", "puff_a_stalked" },
        };

        /// <summary>
        /// Orange, 30°: the warm amber nest (02_Concepts/Particle_Styles_v1/amber_*_v2.png). Red-brown clay walls and
        /// floor; ochre, terracotta, umber and bone bodies with a muted sage now and then; amber-gold, honey and ember
        /// glows (a dusty teal the rare contrast); a warm brown night, gold light.
        /// </summary>
        public static readonly NestTheme Orange = new NestTheme
        {
            id = "orange", hue = 30, segmentZh = "橙", segmentEn = "Orange", nameZh = "暖琥珀蚁穴", nameEn = "The Amber Nest",
            body = new[]
            {
                new Color(0.80f, 0.60f, 0.40f), new Color(0.58f, 0.52f, 0.30f), new Color(0.74f, 0.44f, 0.32f), new Color(0.66f, 0.54f, 0.48f),
                new Color(0.88f, 0.78f, 0.62f), new Color(0.52f, 0.32f, 0.24f), new Color(0.54f, 0.62f, 0.50f), new Color(0.84f, 0.70f, 0.44f),
            },
            // (accents, as the blue forest has its coral and violet: a pale teal, a soft violet and a sage among the golds;
            // the scene measured all one colour without them)
            glow = new[]
            {
                new Color(1.00f, 0.72f, 0.32f), new Color(1.00f, 0.82f, 0.48f), new Color(1.00f, 0.58f, 0.28f), new Color(0.78f, 0.70f, 0.94f),
                new Color(0.55f, 0.86f, 0.80f), new Color(0.95f, 0.86f, 0.55f), new Color(1.00f, 0.88f, 0.66f), new Color(0.66f, 0.88f, 0.70f),
            },
            bodyAccent = 0.12f, glowAccent = 0.26f,
            scan = new Color(0.95f, 0.88f, 0.78f),
            ground = new Color(0.78f, 0.54f, 0.42f), floor = new Color(0.80f, 0.54f, 0.40f), bank = new Color(0.78f, 0.56f, 0.44f),
            plateau = new Color(0.62f, 0.45f, 0.38f), edge = new Color(0.92f, 0.80f, 0.66f),
            fog = new Color(0.16f, 0.095f, 0.075f), skyTop = new Color(0.03f, 0.016f, 0.013f), skyHorizon = new Color(0.13f, 0.075f, 0.055f),
            motes = new Color(1.7f, 1.4f, 1.05f),
            threadCore = new Color(1.0f, 0.94f, 0.82f), threadHalo = new Color(1.0f, 0.70f, 0.38f), beacon = new Color(0.95f, 0.72f, 0.42f),
            grainGlow = new Color(0.95f, 0.62f, 0.30f), lampCore = new Color(1.0f, 0.94f, 0.84f), ring = new Color(1.0f, 0.74f, 0.42f),
            trail = new Color(0.98f, 0.78f, 0.50f),
            moon = new Color(0.80f, 0.70f, 0.60f), fill = new Color(0.20f, 0.14f, 0.11f), rim = new Color(0.07f, 0.05f, 0.04f),
            glint = new Color(0.66f, 0.56f, 0.46f), dew = new Color(0.80f, 0.70f, 0.58f), vineDark = new Color(0.16f, 0.10f, 0.07f),
            vineLeaf = new Color(0.42f, 0.40f, 0.24f),
            microLife = new[]
            {
                new Color(0.86f, 0.76f, 0.60f), new Color(0.84f, 0.80f, 0.70f), new Color(0.62f, 0.52f, 0.36f), new Color(0.66f, 0.46f, 0.42f),
                new Color(0.80f, 0.62f, 0.36f), new Color(0.56f, 0.44f, 0.32f), new Color(0.58f, 0.54f, 0.34f), new Color(0.76f, 0.50f, 0.44f),
            },
            // the ants in amber and honey (a turn of hue would make the teal-green ones dusty pink)
            antBody = new Color(0.06f, 0.035f, 0.03f), antTint = new Color(0.95f, 0.55f, 0.22f, 0.9f), exposureLift = 0.2f, temperature = 2,
            bloomTint = new Color(1f, 0.93f, 0.84f),
            antRim = new Color(1.0f, 0.78f, 0.52f), antPale = new Color(0.95f, 0.86f, 0.72f),
            glowDeep = new Color(0.98f, 0.56f, 0.16f),
            glassKinds = new string[0], headGlowKinds = new string[0], headGlowFrom = new float[0],
            // its own: the clay nest's plants (resin, dry mushrooms, soil spires, thorny succulents, dry grass, seeds)
            // and the paper lanterns; none of the blue forest's glass bulbs, lollipop curls, croziers or corals
            scatterPlants = new[] { "amber_resin", "dry_mushroom", "soil_spire", "thorn_succulent", "fungus_garden", "dry_grass_tuft", "seed_pile",
                "seed_husk", "pebble_cluster", "fallen_leaf", "trunk_night", "root_arch" },
            labPlants = new[] { "puff_b_lantern" },
            // a faint gold in the dry mushrooms' gills, the succulents' spines and the grass's seed heads
            glowParts = new[]
            {
                new GlowPart { plant = "dry_mushroom", from = 0.62f, to = 0.84f, lumMin = 1.0f, amount = 0.35f },
                new GlowPart { plant = "thorn_succulent", from = 0.55f, to = 1.05f, lumMin = 1.1f, amount = 0.45f },
                new GlowPart { plant = "dry_grass_tuft", from = 0.65f, to = 1.05f, lumMin = 1.0f, amount = 0.4f },
            },
            // only here: gold ball-stalks lining the way, beads of resin lit from within, wax comb brimming with honey,
            // amber shards; and a big cluster of them at the heart. Little damp green growth on the dry clay
            things = new[]
            {
                new Thing { kind = "GoldBalls", weight = 34 }, new Thing { kind = "ResinDrops", weight = 22 },
                new Thing { kind = "WaxComb", weight = 16 }, new Thing { kind = "AmberShards", weight = 12 },
            },
            ownShare = 0.42f, dampScale = 0.3f, objects = "none", heart = "shards",
            noKinds = new[] { "Petal", "Blossom", "Clover" }, touchParts = new[] { "dry_mushroom", "thorn_succulent" }, ownBand = new Vector2(30, 40),
        };

        /// <summary>
        /// Red, 0°: the ancient temple, its kiln (02_Concepts/Particle_Styles_v1/temple_*_v2.png). Red clay; terracotta,
        /// umber, ash and charcoal bodies; ember-red glows (a dusty violet the rare contrast); a dark maroon night. Spiral
        /// clay spires and carved arches beside the way, embers in the floor, the amber shrine at its heart.
        /// </summary>
        public static readonly NestTheme Red = new NestTheme
        {
            id = "red", hue = 0, segmentZh = "红", segmentEn = "Red", nameZh = "古蚁圣殿", nameEn = "The Ancient Temple",
            body = new[]
            {
                new Color(0.84f, 0.48f, 0.40f), new Color(0.74f, 0.40f, 0.34f), new Color(0.62f, 0.36f, 0.31f), new Color(0.88f, 0.74f, 0.68f),
                new Color(0.54f, 0.34f, 0.31f), new Color(0.80f, 0.44f, 0.34f), new Color(0.66f, 0.56f, 0.50f), new Color(0.86f, 0.60f, 0.48f),
            },
            glow = new[]
            {
                new Color(1.00f, 0.40f, 0.26f), new Color(1.00f, 0.46f, 0.32f), new Color(1.00f, 0.32f, 0.22f), new Color(1.00f, 0.50f, 0.38f),
                new Color(0.94f, 0.58f, 0.44f), new Color(1.00f, 0.62f, 0.44f), new Color(1.00f, 0.50f, 0.30f), new Color(0.98f, 0.48f, 0.34f),
            },
            scan = new Color(0.96f, 0.86f, 0.80f),
            ground = new Color(0.76f, 0.42f, 0.36f), floor = new Color(0.78f, 0.42f, 0.35f), bank = new Color(0.74f, 0.44f, 0.38f),
            plateau = new Color(0.60f, 0.40f, 0.36f), edge = new Color(0.92f, 0.76f, 0.66f),
            fog = new Color(0.16f, 0.07f, 0.06f), skyTop = new Color(0.03f, 0.012f, 0.012f), skyHorizon = new Color(0.13f, 0.055f, 0.048f),
            motes = new Color(1.8f, 1.05f, 0.9f),
            threadCore = new Color(1.0f, 0.90f, 0.84f), threadHalo = new Color(1.0f, 0.40f, 0.28f), beacon = new Color(0.98f, 0.44f, 0.34f),
            grainGlow = new Color(0.98f, 0.36f, 0.22f), lampCore = new Color(1.0f, 0.90f, 0.80f), ring = new Color(1.0f, 0.48f, 0.34f),
            trail = new Color(0.98f, 0.52f, 0.42f),
            moon = new Color(0.96f, 0.76f, 0.70f), fill = new Color(0.30f, 0.16f, 0.14f), rim = new Color(0.09f, 0.06f, 0.05f),
            glint = new Color(0.68f, 0.52f, 0.46f), dew = new Color(0.82f, 0.64f, 0.56f), vineDark = new Color(0.22f, 0.11f, 0.09f),
            vineLeaf = new Color(0.42f, 0.34f, 0.24f),
            microLife = new[]
            {
                new Color(0.84f, 0.72f, 0.64f), new Color(0.82f, 0.78f, 0.72f), new Color(0.60f, 0.46f, 0.38f), new Color(0.62f, 0.46f, 0.52f),
                new Color(0.80f, 0.56f, 0.38f), new Color(0.52f, 0.38f, 0.34f), new Color(0.56f, 0.50f, 0.36f), new Color(0.78f, 0.46f, 0.42f),
            },
            antBody = new Color(0.06f, 0.03f, 0.025f), antTint = new Color(0.92f, 0.40f, 0.24f, 0.85f), exposureLift = 0.42f, temperature = -4,
            bloomTint = new Color(1f, 0.84f, 0.82f),
            antRim = new Color(1.0f, 0.62f, 0.50f), antPale = new Color(0.96f, 0.80f, 0.72f),
            glowDeep = new Color(0.98f, 0.30f, 0.14f),
            glassKinds = new string[0], headGlowKinds = new string[0], headGlowFrom = new float[0],
            // a sparse kiln: soil spires, thorny succulents, dry grass and seeds; the temple's spires and arches stand
            // beside the way (objects), the amber shrine at the heart
            scatterPlants = new[] { "soil_spire", "thorn_succulent", "dry_grass_tuft", "seed_pile", "seed_husk", "pebble_cluster", "fallen_leaf", "trunk_night", "root_arch" },
            labPlants = new string[0],
            glowParts = new[]
            {
                new GlowPart { plant = "thorn_succulent", from = 0.55f, to = 1.05f, lumMin = 1.1f, amount = 0.45f },
                new GlowPart { plant = "dry_grass_tuft", from = 0.65f, to = 1.05f, lumMin = 1.0f, amount = 0.35f },
                // the temple's buildings: only the pale clay inlaid along their spiral paths and relief bands glows
                // (the lightest of their texture), and a few points (ParticleLook, building)
                new GlowPart { plant = "temple_spire_v2", from = 0f, to = 1.05f, lumMin = 1.7f, amount = 0.8f },
                new GlowPart { plant = "temple_arch_v2", from = 0f, to = 1.05f, lumMin = 1.7f, amount = 0.8f },
                new GlowPart { plant = "temple_cells_v2", from = 0f, to = 1.05f, lumMin = 1.5f, amount = 0.8f },
                // (the ruin's simpler models, where the finer ones are not built: their palest ridges)
                new GlowPart { plant = "temple_spire", from = 0f, to = 1.05f, lumMin = 1.7f, amount = 0.7f },
                new GlowPart { plant = "temple_arch", from = 0f, to = 1.05f, lumMin = 1.7f, amount = 0.7f },
                new GlowPart { plant = "temple_cells", from = 0f, to = 1.05f, lumMin = 1.7f, amount = 0.7f },
            },
            // only here: embers in the floor's cracks, pot shards (the temple's buildings, finer models, stand beside the
            // way: LabNest's temple plan; the procedural clay chimneys and cells were too plain beside them)
            things = new[]
            {
                new Thing { kind = "EmberCracks", weight = 30 },   // (the pot shards read as pale flat splotches: left out)
            },
            ownShare = 0.35f, dampScale = 0.12f, objects = "temple", heart = "shrine",
            noKinds = new[] { "Petal", "Blossom", "Clover" }, touchParts = new[] { "thorn_succulent", "dry_grass_tuft" },
            vineGlow = 0.72f, rigidGain = 1.1f, rigidTint = 0.35f,
            // (the plants' own colours kept within 16 ± 20°: a wider band let some fall to pink, which stood out as blots)
            wayThing = "EmberCracks", wayEvery = 3.6f, ownBand = new Vector2(16, 20), buildingsGlowInParts = true,
        };
    }
}
