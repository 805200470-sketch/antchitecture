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
    /// (For); the shapes and the motion are the same in every one.
    /// </summary>
    public sealed class NestTheme
    {
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
        public Color bloomTint = new Color(0.85f, 1f, 0.97f);   // what the glow's bloom is tinted (the profile's mint turns gold green)
        public string[] labPlants;              // which of the particle nest's own plants grow here (Game.LabPlants)
        public string[] glassKinds;             // plants whose glowing head is glass: a broken shell round a light of its own
        public string[] headGlowKinds;          // and plants whose head alone glows, solid (with how far up it starts)
        public float[] headGlowFrom;

        /// <summary>The theme of the particle nest now being played (set by Game before its parts start).</summary>
        public static NestTheme Current;

        /// <summary>The theme for a region's biome (the blue forest's for any it does not know).</summary>
        public static NestTheme For(string biome) => biome == "amber" ? Orange : Cyan;

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
            labPlants = new[] { "curl_a_crozier", "curl_b_double", "curl_c_hook", "curl_d_cluster", "puff_b_lantern", "coral_b_branching", "frond_a_feather" },
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
                new Color(0.88f, 0.78f, 0.62f), new Color(0.52f, 0.32f, 0.24f), new Color(0.62f, 0.62f, 0.44f), new Color(0.84f, 0.70f, 0.44f),
            },
            glow = new[]
            {
                new Color(1.00f, 0.72f, 0.32f), new Color(1.00f, 0.82f, 0.48f), new Color(1.00f, 0.58f, 0.28f), new Color(0.92f, 0.62f, 0.58f),
                new Color(0.55f, 0.80f, 0.78f), new Color(0.95f, 0.86f, 0.55f), new Color(1.00f, 0.88f, 0.66f), new Color(0.95f, 0.66f, 0.52f),
            },
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
            // the ball-stalks line the way as solid gold heads (02_Concepts/Particle_Styles_v1/amber_*_v2.png), not glass
            glassKinds = new string[0], headGlowKinds = new[] { "spore_stalk" }, headGlowFrom = new[] { 0.6f },
            // ferns, the hooked bell and the paper lanterns suit the clay; the blue coral and the twisted ribbons do not
            labPlants = new[] { "curl_a_crozier", "curl_c_hook", "curl_d_cluster", "puff_b_lantern", "frond_a_feather" },
        };
    }
}
