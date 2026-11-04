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
        /// from..to of its height (0..1), where its texture is at least lumMin of its plant's mean brightness; or, red
        /// above 0, where its texture is that red (its red above its green and blue, as a share of its red), the redder
        /// the more, at hot: the embers in a charred log's cracks (the kiln's; 0 elsewhere); with red, lumMin and amount
        /// light only its warm pale parts (the log's carved wood, not its charcoal's grey sheen).</summary>
        public struct GlowPart { public string plant; public float from, to, lumMin, amount, red, hot, grey, burnt; }   // (grey: with red, how far its warm pale parts weather toward a cool silver grey; burnt: how far they char to a dark warm grey-brown)

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
        public string[] sunkKinds = new string[0];   // plants standing on a lump of soil of their own (a saucer at ant scale),
        public float[] sunkBy = new float[0];        // sunk this far (of their height) so it is under the floor (ParticleLook)
        public string[] sparserKinds = new string[0];   // world plants it has fewer of: their spacing (Game) times these
        public float[] sparserBy = new float[0];
        // its own plants grown otherwise than in the other nests (Game.LabPlants): plant -> more in each tuft (x), how far
        // from the way at least (y) and at most (z), their height times w (y, z, w 0: as the rule has it)
        public System.Collections.Generic.Dictionary<string, Vector4> labTweaks;
        public System.Collections.Generic.Dictionary<string, float> kindSat;   // plants whose own colour it mutes: their saturation times this (ParticleLook)
        public System.Collections.Generic.Dictionary<string, float> kindFoot;  // finely made plants darker at their foot: its shade, of their upper part's (ParticleLook.fineFoot otherwise)
        public System.Collections.Generic.Dictionary<string, float> kindGain;  // plants it has darker: their lightness times this (ParticleLook; their glow as it was)
        public System.Collections.Generic.Dictionary<string, int[]> thingTints;   // the palette's tints for a kind of floor thing, or "Root", not SolidThings' table's
        public Color rootColour;                // its roots' tint, linear, instead of the palette's (SolidThings.Roots; clear: the palette's)
        public System.Collections.Generic.Dictionary<string, Color> thingColours;   // a kind of floor thing's tint, linear, instead of the palette's (SolidThings.Make)
        public bool noJunctionTree;             // no fan tree in its junction chamber (Scatter.Landmarks): its own landmarks stand beside its way
        public float heartLampGain = 1f;        // the lights in its puffballs' hearts (ParticleLook.heartLamps): how bright, and a little how wide
        public float crystalPale = 0.5f;        // how far its crystals' faces go from its plants' tints toward white (ParticleLook)
        public float microGain = 1f;            // how bright its small life is, the seed fluff and midges (MicroLife)
        public Color midgeTint = new Color(0.5f, 0.85f, 0.85f, 0.5f);   // its midges' bodies: the palette colour drawn toward this, by a (MicroLife.shader; the default is v15's cyan, which was fixed in the shader for every nest until 11-04, so warm nests had pale blue-white midges)
        public Color midgeWing = new Color(0.4f, 0.46f, 0.46f);           // its midges' faint wings (the same; set warm in warm nests)
        public Vector4 groundSatByDistance;     // its terrain's saturation near the camera (x) and far off (y), of its biome's, from z to w units away (y 0: the same everywhere; TerrainCore.cginc)
        public string junctionTree;             // or one of its own plants there instead of its biome's (null: its biome's)
        public Vector2 junctionTreeHeight;      // and how tall (zero: the rule's, 9 to 13)
        public float rootWidth = 1f;            // and how thick they are, of the rule's (0: none)
        public string[] scatterPlants;         // the world's plants that grow in this nest (null: all of them)
        public ScatterRule[] extraRules;       // and rules of its own for them, after the world's (Game; null: none): a kind at another scale or further up its banks (11-04)
        public GlowPart[] glowParts = new GlowPart[0];
        public Thing[] things = new Thing[0];   // the things only this nest has (SolidThings, NestThingsThemed)
        public float ownShare;                  // how many of the floor's things are its own
        public float dampScale = 1f;            // how much of the damp green growth (moss, clover...) a dry nest keeps
        public string objects = "relics";       // what stands beside the way (LabNest.PlaceObjects): relics, temple, none
        public string heart;                    // what is at the heart (SolidThings.Heart): null, shards, shrine, dew, pool, watch, garden, honeypots, button (none in any nest now: Game clears it, "不要用心脏的概念了"; -heart <kind> for checks)
        public string[] noKinds = new string[0];   // the floor's things it does not have (pink petals in a warm nest)
        public Color[] leafColours;             // its fallen leaves' colours, linear (NestThingsLitter.LeafColour; null: the litter's own five)
        public float vineGlow = 1f;             // how much the vines' veins, pores and tips glow (a saturated light needs less)
        public Color vineLight;                 // and in what colour, where a nest sets one (a > 0; else its palette's cool glows)
        public float vineDarken = 0.5f;         // how far the vines' stems go from the plants' tints toward vineDark (at least)
        public float vineCurl = 1f;             // how often the vines' tips curl into hooks (GuideThreads; of 1)
        public float farDim = 1f;               // the huge trees beyond the walls (Scatter.Surroundings): how bright, muted (1: as the plants)
        public float farFog;                    // and how much of the fog's colour they take instead of their own (0: none)
        public Vector4 farDark;                 // its far off darker, as v15's falls into the night: from x to y units from the camera down to z of itself (w 1: on; Game: _NestFar, the points', solid splats' and terrain's shaders)
        public Vector4 farSat;                  // and calmer: its glowing points' saturation from x to y units from the camera down to z of itself (w 1: on; Game: _NestFarSat, ParticleLook.shader; 11-04, the granary's "中景的粒子有点太艳")
        public string surroundings;            // and which kinds they are, where its biome's are shared with the world outside the lab (null: its biome's)
        public float surroundingsTall = 1f;     // and how tall, of the usual 26-48 (Scatter.Surroundings; 1: as usual)
        public bool clearExitView;              // no giant where the way's end looks out through a gate (Scatter.InExitView; off: as before, 11-04)
        public float skyGain = 1f;              // its sky this much lighter than the other nests' (NestPalette.Recipe.skyGain)
        public float gateGain = 1f;             // the light gates to the other nests (a saturated light made them glare behind a heart)
        // how much brighter what is right round the ant is, and what it walks past lights up (a vivid palette glared
        // with the blue forest's: the vines beside the ant went neon)
        public float nearLift = 0.9f, walkGain = 1f;
        public float touchGain = 1f;            // and how strongly an antenna's touch lights a plant, of the usual (GlowPlants.Flares; 1: as everywhere)
        public float rigidGain = 1f, rigidTint; // the props' and relics' own colours: how bright, how far toward body[0]
        public System.Collections.Generic.Dictionary<string, float> kindRigidTint;   // a relic kind's own rigidTint (null or not listed: rigidTint): the kiln's standing dead trunks, a model made for the blue forest, toward its palette (ParticleLook)
        public bool kindsSpareFar;              // kindSat, kindGain and kindRigidTint leave the giants beyond the walls as they are (the kiln's trunks, of their kind, darkened in its canyon, its far giants black, k19b-k19d)
        public string[] touchParts = new string[0];   // which of glowParts' plants an antenna can light (not every grass tuft)
        public Vector2 ownBand;                  // the plants' own colours kept within this hue band (centre, half-width; 0: any)
        public Vector2 tintBand;                 // and their tints from its palette (as ownBand; 0: any)
        public bool ownBandKeepsSat;             // its plants' own colours turned into ownBand as vivid as they were (false: half as vivid)
        public bool buildingsGlowInParts;       // the props (and own clay buildings) light only in their glowing parts, not whole
        public Color relicGlow;                 // its relics' glow colour, linear (ParticleLook; clear: white, a little toward glowDeep)
        public Color relicCore;                 // and their embers' hottest middles, where the texture is lightest (a: how far; clear: none)
        public float relicKeep;                 // its relics' share of their fine splats kept however far off (ParticleLook.SolidShell, the solid shader; 0: the plants' floor)
        public string wayThing;                 // one of things that also lines the way, close enough to touch (SolidThings)
        public float wayEvery = 5f;             // about how far apart along the way
        public float wayNear = 1f;              // and how close to the way's line they may stand (units; nearer where the floor beside it is narrow)
        // natural colours (a nest's tone from its light, its things in their own colours): how much of their own
        // colours the floor's things and the plants keep under the palette, how much of the ground textures' own
        // colour the ground keeps, and how far the floor's patches swing in hue
        public float thingKeep = 0.3f;
        public Vector2 plantKeep = new Vector2(0.5f, 0.8f);   // (at random between)
        public float groundTexMix = 0.3f;
        public float floorVary = 22f;           // degrees
        public Color floorPatchA, floorPatchB;   // patches of other materials on its floor (a: how much; clear: none)
        public Color floorWash, wallWash;       // brighter patches over its floor and its walls: the whole of a point's colour, its texture's and its tint's, times rgb (linear; a: how much; clear: none)
        public string groundSet;                // its own ground textures (Art/Ground/<id>) where its biome's are shared with the world outside the lab (null: its biome's)
        public float groundHue, groundSat = -1f;   // and turned this many degrees more, and as saturated, in the lab (Game: the terrain's _HueShift and _Saturation, which its points and carpet read; -1: its biome's)
        // made by NestPalette (the four-layer recipe from v15): which segment, the terrain textures' tints (clear: its
        // biome's), how much brighter its ground's points are, how far a plant's tint wanders from its palette colour
        public int segment = -1;
        public Color floorTexTint, bankTexTint, plateauTexTint;
        public float groundGain = 1f, tintJitter = 1f;
        public float wallGain = 1f;             // the walls round the ant (opaque splats, ParticleLook.WallShell): how light, of the usual
        public Color sporeA, sporeB;            // the floating spores' colours, between these (clear: the atmosphere's own)
        // its own plants' markings where they are not the other nests' (ParticleLook: a kind's BioTable pattern, its
        // SpotTable spots); and the motes circling its finely made plants, how big and how bright (of the usual)
        public System.Collections.Generic.Dictionary<string, Vector4> bio;
        public System.Collections.Generic.Dictionary<string, Vector4[]> spots;
        public float moteSize = 1f, moteGain = 1f;
        // its plants' markings: how strongly they glow (of ParticleLook.bioStrong), how deep their colour (toward the glow's
        // deep colour; the others' 0.55), and its finely made plants' own light (under 0: ParticleLook.fineOwnLight)
        public float markGain = 1f, markDeep = 0.55f, fineLight = -1f;
        public Color markColor;                  // and their colour (a over 0: this, not the glow's; the palette's glows are pale)
        public System.Collections.Generic.Dictionary<string, Color> kindMark;   // a kind's own markings' colour, linear (null or not listed: as above)
        public float faintGain = 1f;             // its plants' faint light in their palest parts (of ParticleLook's)
        // its featured things standing beside its way as landmarks, now that no nest has a heart (SolidThings.Landmarks):
        // which ("seedcup"), and where along the way (shares of its length)
        public string landmark;
        public float[] landmarkAt;
        // the dust floating in its air: how much of the usual, and the colour it leans to (a: how far; clear: v15's pale
        // mist); and how strongly the floor's few glowing grains glow, of the usual
        public float dustGain = 1f;
        public Color dustTint;
        public float grainGain = 1f;
        public float groundGrain = 1f;           // how much its ground textures' light and dark dim and thin its points (ParticleLook.grain; 1: the halftone, pale grains up to 3x)
        // its ground lit as one, by the scene's moon (off in every other nest): at least this share of its ground points'
        // light given to the surface behind them (Game; 0: the shared rule's), its ground points on the sides turned from
        // the moon as dim as the surface there (ParticleLook.pointShade, of 1), and the walls' opaque splats round the ant
        // lit from the moon's side as the terrain behind them is, not from above (SolidThings, the solid shader)
        public float toSurface, pointShade;
        public bool groundMoon;
        // and its walls' own light where they are turned from the moon, of its biome's (off in every other nest, 1): the
        // atmosphere's light from the sides on the terrain (AtmosphereController.equatorGain) and the walls' opaque splats'
        // fill round the ant (the solid shader); and round the ant, where its walls are those splats, the terrain behind
        // them sunk to this of itself, so between them it reads as their shadow, not as a lighter wall (TerrainCore)
        public float wallAmbient = 1f, groundNearKeep = 1f;
        public float wallAmbientGrey;           // and how far that light from the sides turns to a grey as light (0: the biome's colour)
        // the moss forest's (off in every other nest): its moss cushions dark in body with their pale yellow-green at the
        // frond tips only, their sporophytes' capsules lit at the mouth, their markings small gold specks
        // (NestThingsGrowth.MossCushion, SolidThings.Bio); and plants whose tips keep their body's palette colour rather
        // than a paler neighbour's (ParticleLook: a mushroom's cap is its tip, and a pale one hid its spots)
        public bool darkCushions;
        public string[] plainTips = new string[0];

        /// <summary>The theme of the particle nest now being played (set by Game before its parts start).</summary>
        public static NestTheme Current;

        /// <summary>How much of its light a self-lit thing (an ant, a mote, a speck of the guiding trail) keeps in this
        /// nest. Game lifts a particle nest's night by its exposureLift, and that brightens everything on screen; but the
        /// lights were made for v15's lift (0.3), so in a nest lifted two stops more they burnt white (the user, 10-29:
        /// "蚂蚁…太发光、太白了…有点过曝了", "白光…太亮了，已经分不清楚引导的光线了"). share 1: as bright on screen as in v15;
        /// 0: lifted with the rest. Outside the particle nests, 1.</summary>
        public static float LightComp(float share) => !Game.Lab || Current == null ? 1f : Mathf.Pow(2f, -share * Mathf.Max(0f, Current.exposureLift - 0.3f));

        /// <summary>The theme for a region's biome (the blue forest's for any it does not know).</summary>
        public static NestTheme For(string biome)
        {
            bool before = Game.Arg("-palette") == "before";   // (checks: the colours before the palette recipe)
            // (trying the granary's or the garden's colours without a build: -recipe "hueNudge=18,exposure=1")
            if (!before && (biome == "honey" || biome == "leafcut") && Game.Arg("-recipe") is string tried)
                return biome == "honey"   // (each its own: one shared cache handed the granary's theme to the garden whenever a gate, the map or a
                                          // name asked for the other first, 11-04, a leafcut trial drawn in the granary's colours)
                    ? trial ??= Granary(NestPalette.Apply(YellowBasic(), NestPalette.For(2, HoneyRecipe.With(tried))))
                    : leafTrial ??= Garden(NestPalette.Apply(ChartreuseBasic(), NestPalette.For(3, LeafRecipe.With(tried))));
            // (the moss forest's the same way)
            if (!before && biome == "moss" && Game.Arg("-recipe") is string mossTried)
                return mossTrial ??= Mossy(NestPalette.Apply(GreenBasic(), NestPalette.For(4, MossRecipe.With(mossTried))));
            // (and the kiln temple's)
            if (!before && biome == "kiln" && Game.Arg("-recipe") is string kilnTried)
                return kilnTrial ??= KilnTuned(NestPalette.Apply(RedBasic(), NestPalette.For(0, KilnRecipe.With(kilnTried))));
            // (and the amber nest's)
            if (!before && biome == "amber" && Game.Arg("-recipe") is string amberTried)
                return amberTrial ??= Amber(AmberRecipe.With(amberTried));
            return biome == "amber" ? (before ? OrangeBefore : Orange) : biome == "kiln" ? (before ? RedBefore : Red)
                : biome == "honey" ? (before ? YellowBefore : Yellow) : biome == "leafcut" ? (before ? ChartreuseBefore : Chartreuse)
                : biome == "moss" ? (before ? GreenBefore : Green) : biome == "verdigris" ? SpringGreen : biome == "azure" ? Azure : biome == "cistern" ? Blue : BlueForest;
        }

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

        /// <summary>The blue fungal forest as it is played: v15 with its plants' greens turned toward blue. Cyan itself
        /// stays v15's, the palette every other nest is turned from (NestPalette).</summary>
        public static readonly NestTheme BlueForest = Bluer(Cyan);

        static NestTheme Bluer(NestTheme v15)
        {
            var t = (NestTheme)v15.MemberwiseClone();
            // the user, 10-29: "蓝色那版场景中的植物有点太绿了，可以调一下它们的颜色，稍微偏向蓝一点": its sage and yellow-greens
            // turned 60° toward teal and a little less vivid, its teals 10° toward blue; its straw and bone, and its glows'
            // coral and violet, as they were
            t.body = new[]
            {
                new Color(0.68f, 0.80f, 0.74f), new Color(0.56f, 0.78f, 0.66f), new Color(0.56f, 0.78f, 0.76f), new Color(0.64f, 0.74f, 0.78f),
                new Color(0.74f, 0.80f, 0.77f), new Color(0.54f, 0.70f, 0.67f), new Color(0.63f, 0.88f, 0.71f), new Color(0.84f, 0.78f, 0.60f),
            };
            t.glow = new[]
            {
                new Color(0.55f, 0.88f, 0.98f), new Color(0.70f, 0.96f, 0.94f), new Color(0.76f, 0.95f, 0.89f), new Color(0.88f, 0.72f, 0.74f),
                new Color(0.78f, 0.74f, 0.90f), new Color(0.88f, 0.84f, 0.68f), new Color(0.85f, 0.91f, 0.92f), new Color(0.88f, 0.76f, 0.74f),
            };
            return t;
        }

        // first versions of the scenes that had only their effect images: palettes from those images; the new plants
        // (Tripo v3.1) placed by the hue of their bodies (curl_d 47°, curl_a 51°, puff_a 62°; curl_e 78°, frond 84°;
        // curl_b 107°, curl_c 112°; coral_b 134°; puff_b 28° is the amber nest's)
        // the honey granary and the leafcut garden: v15's palette turned to their segments (NestPalette), tuned here
        // (spread 0.65: v15's 80° spans one and a half segments round cyan, more where yellow to spring green crowd;
        // both spread toward green: gold, yellow and olive in the granary, yellow-green to teal-green in the garden,
        // not toward salmon and orange). The garden a third of a stop brighter than the granary, its ground lighter (at
        // 0.35 stops it read too dark overall: "绿色的场景整体太暗")
        // (2026-10-27 review: the granary still too dark, its gold light pools and threads too vivid and its family too
        // narrow; the garden's family narrow and its base a little vivid)
        // (second review: still dark, its gold light pools vivid, its walls and sky too near the amber nest's orange)
        // (11th round: the way now led to its way out, its views measured 0.083, near the 0.076 the user found too dark in the
        // garden: 0.15 stops more)
        // (12th round: the amber nest, its colours turned toward yellow, came within 16° of it, its ground and walls nearly the
        // same, its walls caramel orange: 6° more toward yellow, its base less vivid, its family wider; -recipe hueNudge=8 and
        // =16 captured beside it for the user to pick)
        // (the fifteenth: its floor the largest and brightest surface in it, a middling khaki (L 0.41), "土、暗沉", and its
        // lights near white (C 0.025): its ground lifted and dyed less, back toward a dark surface, the whole a third of a
        // stop brighter so the light lands on its walls, plants and lights; its base a little less vivid; its lights honey-gold)
        // (the sixteenth: a third of a stop brighter its floor rose with the rest, L 0.45: its ground's points and carpet
        // dimmed instead, the whole brighter again so its walls, plants and lights carry the light)
        // (the eighteenth: its floor down to L 0.38 but the whole too dark, 0.088, and one gold from walls to plants, its family
        // 12°: brighter again with its floor and walls lifted no more, its plants' tints wandering further and more accents;
        // its lights a little honey)
        public static readonly NestPalette.Recipe HoneyRecipe = new NestPalette.Recipe { spread = 1.15f, spreadSide = 1f, exposure = 1.85f, highChroma = 0.85f, hueNudge = 12f, groundLift = 0f, groundGain = -0.5f, baseChroma = 0.25f, jitter = 2f, midChroma = 1.05f, accentShare = 2.8f, accentChroma = 1.3f, skyGain = 0.5f };   // (11-04, the second trial's blind colour review, 9 % of it loud, its family 9°: its family wider (0.8 to 0.95, and 1.15 after the fourth's 8°), its base and middle a quarter calmer (0.32 to 0.25, 1.35 to 1.05); its lights whiter (highChroma 1.1 to 0.85: its beams pale gold over its gold walls, the fourth)   // (11-04, the user: "黄色这个场景也是可以稍微压暗一些，如果光束看不出来的话", "如果光束不够显眼，压低场景的亮度，根据v15蓝色的效果为基准": 2.2 to 2.0, then 1.85, its stills 0.113 -> 0.095 at 2.0 and its beams still lost against its glittering walls, 0.088 at 1.9 with its walls calmer, 0.079 at 1.75, its glowing things kept at their light, NestTheme.LightComp, so its beams read; -recipe exposure=2.2, as it was)   // (11-03, the user: "整体亮度可以稍微往下降一点点": exposure 2.35 to 2.2)   // (10-30: its floor's points at -0.4 with its new floor's own colours at 0.7, the 51st, 0.098 and 4.3 % over the line: -0.5)   // (the 40th: its plants grey sage, its family 10°: its middle more vivid; its floor still dark and dull, its stills 0.076: its floor's points and carpet a little less dark, the 41st's -0.4 trial 0.089)   // (the 21st: its sky a khaki haze, L 0.23, one with its walls: darker; its base less vivid)   // (10-29, the user: "有点过亮，稍微暗一点点，纯度稍微高一点点": a little darker, its floor's and walls' textures purer, no more exposure for light: its own lights take the exposure back off)   // (the 36th: all else passing, its family 9°: its plants' tints wandering further, its middle a fifth more vivid, more accents)   // (the 34th: still one gold, 9°: its family shorter, 0.7, so its plants reach olive and straw, not mint; more accents; its floor's texture darker, a little more light)   // (the 31st: one gold all over, its family 9°, its lights near white: more accents, its lights a little honey; its family wider, 1.3, and its plants' tints wandering further turned its plants teal and mint, its palette's family running on past olive)   // (the 24th: its base still a little gold, C 0.029: a fifth less; the 25th, its far trees and pale stones darker, 0.079: a quarter stop brighter, its floor held; the 26th, still 0.083, its sky black: its sky a dark warm brown)   // (gain 0.4: its floor 5 % bright and dyed)
        // (the garden at 0.65 stops, its brightness 0.11, still read too dark: "绿色这个还是太暗了")
        // (at 1.2 stops with its ground gain 0.7 its floor glowed, 17 % of it bright and dyed)
        // (its hue 15° toward leaf green, the user's pick of four tried: at 113° it read olive and murky, "太暗沉")
        // (the eleventh review: its base still a little vivid (C 0.029), half its way too dark, the light before its way
        // out yellow-green on the floor: base chroma down, 0.15 stops brighter with its ground gain down a little, its
        // lights paler; its family spread wider: without the lilac sponge mounds it spread 15°)
        // (the fourteenth: its floor lit all over, L 0.47 against v15's 0.26, read as a lawn, not a dark surface; lemon
        // green at the ant's feet and before the gate: its ground lifted less and the whole a little brighter, so its
        // light moves from the floor to its plants and lights; base, middle and light chroma down a little more)
        // (the fifteenth: its floor at its limit (L 0.47) but the whole only 8 % over the 0.11 still "too dark", half its
        // way 0.06: a third of a stop brighter with its ground's gain cut to match, so the light lands on its walls,
        // plants and lights, not its floor; its lights near white, lemon green on the floor at the gate. The sixteenth:
        // still a little dark, 0.14, half its way 0.07: its walls lifted (Garden: bank), its lit floor held where it was.
        // The twentieth: its base still a little vivid (C 0.031), the floor by the light pools a deep green: base and light
        // chroma down again)
        // (the twenty-ninth: its stills' hue 114°, the user's pick, tried at +15°, 123°: with its base chroma down its
        // floor texture's own olive (105°) showed through, its walls and leaves had warmed, its teal lights gone: 18°, and
        // those cooled in Garden, back toward the pick)
        // (the forty-third: its floor the user's pick A4, dark humus with patches of cut leaves: the whole 0.127, its hue
        // 116°, its colour calmer (M 8.1): a fifth of a stop brighter, 4° more toward leaf green, its middle as vivid
        // as the palette's)
        public static readonly NestPalette.Recipe LeafRecipe = new NestPalette.Recipe { spread = 1.4f, spreadSide = 1f, exposure = 1.6f, groundLift = 0.07f, groundGain = 0f, baseChroma = 0.35f, midChroma = 1.15f, highChroma = 0.4f, skyGain = 0.85f, hueNudge = 26f, accentShare = 2f };   // (spread 0.95 to 1.1, then 1.4, midChroma 1 to 1.15, accents twice: 11-04, its family 15° then 11°, grey, the second and fourth trials' blind colour reviews)   // (11-04, the user: "如果光束不够显眼，压低场景的亮度，根据v15蓝色的效果为基准": 1.95 to 1.8, then 1.6 (its stills 0.134, four times v15's, its beams lost against its bright olive slopes; 0.093 at 1.65, 0.082 at 1.5), its glowing things kept at their light; -recipe exposure=1.95, as it was)   // (its floor at gain 0.45: 7 % bright and dyed)
        /// <summary>The recipe a segment is made with (its own where it has one; v15's spread round cyan, two thirds of
        /// it elsewhere).</summary>
        public static NestPalette.Recipe RecipeFor(int segment) => segment == 0 ? KilnRecipe : segment == 1 ? AmberRecipe : segment == 2 ? HoneyRecipe : segment == 3 ? LeafRecipe : segment == 4 ? MossRecipe
            : new NestPalette.Recipe { spread = segment == 6 ? 1f : 0.65f };
        static NestTheme YellowBasic() => Basic("yellow", 60, "黄", "Yellow", "蜜罐粮仓", "The Honeypot Granary",
            new[] { new Color(0.62f, 0.42f, 0.12f), new Color(0.58f, 0.42f, 0.16f), new Color(0.66f, 0.50f, 0.14f), new Color(0.62f, 0.48f, 0.26f), new Color(0.74f, 0.60f, 0.16f), new Color(0.84f, 0.70f, 0.30f) },
            new[] { new Color(0.40f, 0.24f, 0.62f), new Color(0.86f, 0.80f, 0.62f) },   // stored berries' deep violet, pearl husks
            new[] { new Color(1.00f, 0.92f, 0.20f), new Color(1.00f, 0.95f, 0.30f), new Color(1.00f, 0.90f, 0.42f), new Color(0.98f, 0.96f, 0.55f) },
            new[] { new Color(0.78f, 0.95f, 0.80f), new Color(0.95f, 0.72f, 0.60f) },   // a pale mint, a soft coral
            new Color(0.62f, 0.42f, 0.20f), new Color(0.15f, 0.10f, 0.04f),
            new[] { "curl_d_cluster", "curl_a_crozier", "puff_a_stalked", "crystal_b_fan" },   // (no mixed crystals either: two thin prisms, in the game view two white leaves)   // (no spray crystals: their green patches, in its palette, a big teal tuft by its way in, "最跳的一大片饱和色")
            new string[0],   // (no seed piles, the palette's sage turning them into a big teal tuft by its way in; no mint mushrooms or green leaves: at the ant's eye they were all it saw, not its honey; no big husks, tinted mint, a great leaf by its way: its own husks are its things; no pebble clusters, their big pale stone by its way an ant's egg, "像蚁卵")
            // (honey in its natural forms: drops, strings, crystallised lumps, pollen pellets; no insects, comb or pots; and
            // what the foragers drop beside the way, husks, bits of grass, honey stains)
            // (the gold ball-stalks are the amber nest's own, not the granary's)
            new[] { new Thing { kind = "HoneyDrops", weight = 13 }, new Thing { kind = "SugarCrystals", weight = 10 },   // (weights as they were; more honey, 11-04: Granary)
                new Thing { kind = "PollenPellets", weight = 30 }, new Thing { kind = "HoneyDrip", weight = 14 },   // (pollen 14 to the 50th; the drips 22 to the 51st, and none the 52nd, left in this comment by a slip: lining the way, with pollen now, they are fewer here)   // (its gleanings left out: by the way they read as one yellow stick, "读不出来"; husks lie round its landmarks and its drips' roots)
                new Thing { kind = "Gleanings", weight = 4 } },   // (8 to the 41st: its honey stains' dark ovals here and there, "好几处深黑椭圆")   // (no sand patches: pale under its moonlight, "白纸", "和结晶蜜分不开")   // (patches of dry sand: the floor's own patches did not show; and a few of the husks, grass and honey stains the foragers drop, "路边留少量种壳、碎草、蜜渍")
            0.5f, "none", 0.22f, 0.6f, wayThing: "HoneyDrip", heart: "honeypots");   // (more of its honey: at 0.3 its ferns and puffballs were the scene; at 0.62 the whole set of them all along its way, "像陈列")
        public static readonly NestTheme Yellow = Granary(NestPalette.Apply(YellowBasic(), NestPalette.For(2, HoneyRecipe)));

        /// <summary>The granary's own touches over its palette (after NestPalette.Apply, which resets some of them).</summary>
        static NestTheme Granary(NestTheme t)
        {
            // its floor in patches of dark damp earth and pale dry sand among the brown (one brown all over read as one
            // colour; at 0.8 and 0.6 of browns near its own the patches were not seen: "看不出湿土、干沙的斑块")
            t.floorPatchA = new Color(0.8f, 0.42f, 0.17f, 0.7f);   // (10-29, the user, of its near floor pale beige and its far walls gold: "近处可以饱和度高，远处低，现在反了": its patches purer, an ochre rust and an olive straw; at 0.70, 0.52, 0.34 and 0.84, 0.80, 0.66, tan and near white)   // (near black, it read as pits; at 0.20 and 0.9, and at 0.30 and 0.6, still "深色斑块像坑": now ochre earth as light as the floor, its hue the change)
            t.floorPatchB = new Color(0.9f, 0.66f, 0.24f, 0.8f);   // (11-03, the user: "太绿了…更像蜂蜜": a light honey; an olive straw to the 53rd)   // (the 46th: 0.78, 0.47, 0.2 and 0.70, 0.64, 0.30, its family still 9°: further apart, rust and olive)   // (at 0.74 and 0.85, a pale gold near its floor's own, not seen: paler and greyer)
            // (trials: -patchspread N turns the two patches N° apart, the rust redder and the olive greener; -patchchroma N,
            // -patchalpha N their chroma and share times N)
            float pSpread = Check("-patchspread", 0f), pChroma = Check("-patchchroma", 1f), pAlpha = Check("-patchalpha", 1f);
            if (pSpread != 0f || pChroma != 1f || pAlpha != 1f)
            {
                Color TurnPatch(Color c, float deg)
                {
                    Vector3 lch = NestPalette.Oklch(c);
                    Color o = NestPalette.FromOklch(new Vector3(lch.x, lch.y * pChroma, lch.z + deg));
                    return new Color(Mathf.Clamp01(o.r), Mathf.Clamp01(o.g), Mathf.Clamp01(o.b), Mathf.Clamp01(c.a * pAlpha));
                }
                t.floorPatchA = TurnPatch(t.floorPatchA, -pSpread);
                t.floorPatchB = TurnPatch(t.floorPatchB, pSpread);
            }
            // (11-03, the user: "颜色有点太绿了，偏向更黄一些，更像蜂蜜那种质感，更加干净、有活力…地面…墙上…除了深棕色以外，也可以
            // 有些比较明亮的颜色…地面和墙面的差距不要做这么大，过渡可以自然一些": its ground textures no longer turned toward yellow
            // green (GameConfig's -13°, made for its first loess, turned its new floor's grey olive clods olive green), a little
            // purer; brighter honey patches over its floor and walls; its walls round the ant lighter. Checks: -groundturn N,
            // -groundsat N, -floorwash N, -wallwash N (how much), -washr N, -washg N, -washb N (the wash), -wallgain N)
            t.groundHue = Check("-groundturn", 3f);   // (10, the 54th's first: floor and walls an orange amber, toward the amber nest's)
            t.groundSat = Check("-groundsat", 0.58f);   // (11-04, the user: "黄色的饱和度有点太高了，稍微降一些，或者减少一些这些很黄色的部分": 0.85 to 0.78, 0.72, then 0.58 after the second trial's blind review, a fifth of its picture still very yellow)   // (0.8, the 54th's second: its near floor, under the guide's light, a grey tan beside its walls)
            float washR = Check("-washr", 2.2f), washG = Check("-washg", 2.0f), washB = Check("-washb", 0.8f);   // (2.4, 1.95, 0.75: orange)
            t.floorWash = new Color(washR, washG, washB, Check("-floorwash", 0.3f));   // (11-04, as groundSat: 0.6 to 0.3, its walls' 0.75 to 0.33: its very yellow patches fewer and quieter)
            t.wallWash = new Color(washR, washG * 0.98f, washB * 0.95f, Check("-wallwash", 0.33f));
            t.wallGain = Check("-wallgain", 1.0f);   // (11-04: 1.4 to 1.0, its walls round the ant behind its beams)   // (1.25, the 54th's second: at the way's end its walls still a dark brown beside its lit floor; 1.5 with chroma 0.95, its walls the most vivid thing by its way in)
            // its plants' own markings (the third review): the crozier's pale line up its stem a row of small bright dots
            // (alone it read as a grey line, "发灰、不亮"); the cluster's heads lit at their tips, small and clear, high and
            // low alike (its big spots read as white blots, and its heads' light, rising with their height, lit the high
            // ones and left the low ones dark: "像按高度分")
            t.bio = new System.Collections.Generic.Dictionary<string, Vector4>
                { { "curl_a_crozier", new Vector4(7, 4f, 0.04f, 0.8f) }, { "curl_d_cluster", new Vector4(7, 4f, 0.03f, 1f) } };   // (the crozier's at 1, the 52nd: its tallest coil's top near a white blob)   // (the 50th, its tips alone: pale gold flakes over it, "像斑驳的漆": as the crozier's, a row of small bright rings up its coils and their tips)   // (the 46th: 0.08 wide, messy bright flecks at its neck; the cluster's tips at 0.045, cream blotches, "像泼的漆")   // (the 41st: six rings thinner than its points, broken into scattered flecks, "像溅上去的光": four, wider; the cluster's tallest tip alone lit, its growth past 0.55: every tip, softly)   // (the 40th: its coil's tip alone, a blurred glint, nothing up its stem, "像普通蕨芽带反光": six thin rings, brighter toward its coil)   // (the 36th: its rings at one height on its stems still, "横切": its coils' tips alone, pattern 7's, which leaves the leaflets at the foot dark; pattern 8, every tip, lit its foot gold, the 37th)   // (the 34th: 5 rings, two at one height on its stems: one or two under its coil; the cluster's tips smaller)   // (the 24th: 9 rings 0.22 wide, bright, read as stripes, "像两盏灯"; the 26th, 12 at 0.05, scattered specks; the 31st, 10 at 0.1, level bands across the tuft, "一刀一刀切开": fewer, near its coil)   // (the 18th: 16 thin rings not seen, the cluster's tips flat yellow patches, "像地衣")   // (the cluster: its tips alone, soft: pattern 5's specks round them read as frost)
            // (the 11th round: still "像霜", pale specks on pale coils, ordinary fern buds: the markings stronger and a deeper
            // gold, fewer and larger dots, the crozier's coil lit at its tip, its fine plants' bodies a little darker)
            t.markGain = 2.2f;   // (at 1.6, a pale gold, the 15th: "不发光", like reflections)
            t.markDeep = 0.85f;
            t.fineLight = 0.08f;   // (at 0.16 the crozier's coil and top lit cream all over, "像糖霜")
            // (the 12th: its markings still read white, as frost: the palette's glows are pale; a true gold)
            t.markColor = new Color(1.0f, 0.80f, 0.42f, 1f);   // (a light gold: saturated, it lit the crozier's foot as a gold blob; paler, it did not show)
            t.faintGain = 0.3f;   // (the cluster's pale heads lit whole by their faint light: "身子暗、部位亮")
            // honey along all of the way (the 13th: half way along there was hardly any): its drip stalks closer
            t.wayEvery = 5f;   // (6 to the 52nd, its drips alone; at 3 a row of them read as street lamps; at 4 still three to a view, "像小路灯")
            // and its pollen heaps taking turns with them along it (the 52nd: its lively colours at its way in alone, 0.03 %
            // of its half way's view and 0.01 % of its end's, "中途和尽头几乎没有")
            t.wayNear = Check("-waynear", 1f);   // (a trial: by its last stretch the floor beside the way is all slope, nothing stood there)
            t.wayThing = "HoneyDrip,PollenPellets*1.6,PollenPellets*1.6";   // (a drip and a heap by turns, the 53rd: the view of its way's end without; heap, drip, heap, the 53rd's last trial: still none there, the floor by its last stretch all slope, and none half way)
            t.spots = new System.Collections.Generic.Dictionary<string, Vector4[]> { { "curl_d_cluster", new Vector4[0] },   // (the cluster lit at its coils' tips alone, its markings: its tips' spots read as gold patches here and there, "东一块西一块")
                { "puff_a_stalked", new[] { new Vector4(0, 0.62f, 0.5f, Check("-puffhub", 1.3f)), new Vector4(1, 0.62f, 1.02f, 0.1f) } },   // (its hub brighter, its balls a little: as bright, the 31st, "小球和中心差不多亮")
                { "curl_a_crozier", new[] { new Vector4(0, 0.68f, 0.25f, 0f) } } };   // (none, the 46th: its rings and coil its light)   // (the 40th: its coil's soft light a blur, "亮斑偏糊": less, its rings back)   // (the 37th: its coil not lit: twice the light)   // (its coil's light smaller and softer than the table's: the 18th, "像糖霜")
            t.headGlowKinds = new[] { "curl_d_cluster", "curl_a_crozier" };   // (the 21st: the crozier's head and the top of its stem lit near white from 0.6 of its height, "上白下绿两段": its light its rings and coil tip alone)
            t.headGlowFrom = new[] { -1f, -1f };
            // its croziers lower and a little back from the way (the 52nd: in their own game view their coils over its top
            // edge, two stalks and a few bright flecks, "看不出是卷头"): 1.8-3.8 high, now 1.3-2.7 (1.4-3.0, the 53rd: its tops still cut)
            t.labTweaks = new System.Collections.Generic.Dictionary<string, Vector4> { { "curl_a_crozier", new Vector4(0f, 0f, 0f, 0.7f) } };   // (from 0.5 off the way too, the 53rd's trial: behind the vines from the way, not seen)
            // the motes circling its plants smaller and softer (they read as hard white discs), the motes in its air
            // dimmer and in its own gold ("太多、太白")
            t.moteSize = 0.55f;
            t.moteGain = 0.6f;
            t.motes = Color.Lerp(t.motes, t.glow[1] * 1.3f, 0.7f) * 0.45f;   // (at 0.7 of white, still "多、白")
            // the scene's glints, on its honey's dense splats, a third as bright (white flakes all over its drops, "像面包");
            // the lamps in its puffballs' hearts gold and dimmer (near white, great white balls of light, "几团很大的白光")
            t.glint = t.glint * 0.35f;
            t.lampCore = Color.Lerp(new Color(1.0f, 0.74f, 0.36f), t.lampCore, 0.3f) * 0.8f;
            t.heartLampGain = 0.3f;   // (its core gold, their halo still a great white glow, the 33rd; at 0.5, the 34th, still)
            // the floor's crystal grains left out: the crystals growing by its way are its crystals
            t.noKinds = new[] { "Crystals", "Mushrooms", "Clover", "Fern", "Moss", "Seedling", "Feather", "Lichen", "Petal", "Blossom", "Seed", "Pebbles", "Shell" };   // (no pebbles or shells either: in its palette pale grey-green round lumps, eggs again, the 34th)   // (and none of the floor's own seeds: the palette's pearl and lilac made its nuts white and pink eggs, "像蚁卵"; its own husks and seeds lie round its landmarks and drips)
            // its vines' stems dark brown, their leaves dry olive (its palette's turn of v15's made them teal and mint, the
            // brightest things by its way, "灰绿藤"); their veins and tips still lit
            t.vineDark = new Color(0.045f, 0.032f, 0.014f);   // (linear, as dark as they read here: at 0.10, its night 1.9 stops brighter, pale grey-tan tubes)
            t.vineLeaf = new Color(0.12f, 0.095f, 0.045f);
            t.vineDarken = 1f;   // (0.9 to the 53rd: the tenth of its palette's yellow green left in them lit a vine olive green by its way in, "薄荷绿大颗粒卷环")
            // their veins, pores and tips in its honey's gold, softer (the 52nd: its palette's cool glows lit them mint, a
            // coarse mint loop by the cluster, "薄荷绿大颗粒卷环", and rows of white dots on dark stems, "像铁丝网")
            t.vineLight = new Color(0.8f, 0.66f, 0.38f, 1f);   // (at 0.85, 0.62, 0.28 and 0.7, the 53rd's trial: orange dashes, a warning tape)
            t.vineGlow = 0.55f;
            // its plants take more of its palette's family (keeping 0.5-0.8 of their own gold textures, the family never
            // reached them: walls, floor and plants all one gold, its family 12°; the 24th, at 0.38-0.68, still mostly gold;
            // the 31st, at 0.3-0.55, its family 9°)
            t.plantKeep = new Vector2(0.3f, 0.6f);   // (0.2-0.45 to the 46th; their own colours and tints folded into its band since the 41st and 47th)   // (at 0.15-0.4, its family 1.3 long, its croziers mint, its cluster and puffballs teal; its family 0.7 long now)
            // its crozier a little lighter (dark grey with its rings, close to, it read as a caterpillar, "像毛虫"), the
            // cluster's foot darker (a gold patch at it)
            t.kindGain = new System.Collections.Generic.Dictionary<string, float> { { "puff_a_stalked", Check("-puffgain", 0.85f) }, { "curl_a_crozier", 0.5f }, { "crystal_b_fan", 0.4f } };   // (its crystals darker too, the 40th: a neutral white, foot and point alike, among the brightest things in view)   // (its puffballs' balls a little darker, their hub as lit; the crozier's body darker under its rings: lighter, 1.15, and at 1, it read cream all over)
            t.kindFoot = new System.Collections.Generic.Dictionary<string, float> { { "curl_d_cluster", 0.55f }, { "curl_a_crozier", 0.5f } };
            // its croziers' own green muted (one in three kept enough of its texture to read teal, the 90° nest's colour,
            // the 40th), its crystals' faces more of its gold (near white, "整块中性白"), its seed fluff and midges dimmer
            // (a tuft of fluff by a puffball's foot whiter than its balls)
            t.kindSat = new System.Collections.Generic.Dictionary<string, float> { { "curl_a_crozier", Check("-croziersat", 0.6f) } };
            // and its plants' own colours folded into straw, olive and yellow-green (the 41st: one crozier still teal, the
            // cluster a vivid mid green, the puffballs' stems teal-grey, "跳出了蜜色"). Checks: -ownband N, its half-width
            t.ownBand = new Vector2(Check("-bandcentre", 58f), Check("-ownband", 30f));   // (24 to the fourth trial of 11-04, its family 8°, one gold)   // (65 ± 30 to the 48th)
            t.ownBandKeepsSat = true;   // (10-30, the user: "环境中增加一点有活力的颜色": its croziers and cluster grey sage, turned half as vivid: a fresh yellow green)
            t.tintBand = new Vector2(Check("-bandcentre", 58f), Check("-tintband", 30f));   // (the 46th: the same croziers and cluster still teal: their tints, its palette's family running on past olive)
            t.crystalPale = 0.1f;   // (0.25 and its gain 0.6, the 46th: an opaque grey-white, plaster)
            t.microGain = 0.55f;
            // the roots at its walls' feet dark brown and thinner (its palette's, greyed as its vines are, lay on its gold
            // floor as pale grey-blue lengths, "像一条灰色的虫")
            t.rootColour = new Color(0.12f, 0.075f, 0.025f);
            t.rootWidth = 0f;   // (none: dark brown and thinner, the 31st and the 34th, still "一节一节的深灰长条", a worm)
            // its fallen leaves and twigs dry straw and brown (the palette's sage and teal tinted a fallen leaf, several ants
            // long, into the biggest, most vivid thing by its way in, "入口的青绿尖叶丛")
            t.thingColours = new System.Collections.Generic.Dictionary<string, Color>
                { { "Leaf", new Color(0.17f, 0.11f, 0.04f) }, { "Twig", new Color(0.13f, 0.085f, 0.035f) } };
            // its floor's texture a little darker (the 19th: a third of a stop brighter, its floor rose to L 0.41 with the
            // rest; the recipe's ground gain reaches its points and carpet, not the terrain under them); no grass tufts, which
            // its palette turned teal by its way (its drips have their own)
            // its own floor texture (10-30, the user chose 甲 of three: clods of ochre, rust, grey olive and gold with chaff and
            // striped husks; the loess's dark violet seeds read as pits, "黑椭圆像洞", and its one gold held its family to 9°),
            // more of its own colours near the ant. Checks: -floortex N, its tint; -texmix N, its share in the floor's points
            t.groundSet = "honey2";
            t.groundTexMix = Check("-texmix", 0.7f);   // (0.5, the 50th: its clods' rust and gold not seen as colours of their own)
            float floorTex = Check("-floortex", 0.9f);
            t.floorTexTint = new Color(t.floorTexTint.r * floorTex, t.floorTexTint.g * floorTex, t.floorTexTint.b * floorTex, t.floorTexTint.a);   // (10-29, the user: its floor "太暗沉": less dark, purer; at 0.66; at 0.8, the 40th, still a dark olive ochre, its stills 0.076)   // (at 0.85, the 34th, L 0.41, the brightest flat thing in it; at 0.6 its clods' cracks deep and even, "像铺路石"; at 0.72, L 0.41 again)
            // (checks: -floorturn N turns its floor N degrees toward olive, away from its amber walls, without a build)
            float floorTurn = Game.Arg("-floorturn") is string ft && float.TryParse(ft, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ftv) ? ftv : 0f;   // (-5, toward gold, the 48th to the 50th: its new floor's rust and gold clods one colour, the 50th)
            if (floorTurn != 0f)
            {
                Color Turned(Color c)
                {
                    float sc = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                    Vector3 lch = NestPalette.Oklch(c / sc);
                    Color o = NestPalette.FromOklch(new Vector3(lch.x, lch.y, lch.z + floorTurn)) * sc;
                    return new Color(o.r, o.g, o.b, c.a);
                }
                t.floorTexTint = Turned(t.floorTexTint);
                t.floor = Turned(t.floor);
            }
            // its walls a warmer brown than its floor and a little darker (the 21st: sky, walls and floor one dark khaki, "土";
            // turned past about 75° they would meet the amber nest's walls, 43-50°), its floor varying more in hue
            // (the 24th: on screen its walls still the floor's gold, both about 90°, "几乎全是同一种金黄", its family 12°:
            // its walls and their tops turned further toward amber brown, as light and no more vivid, its floor left gold.
            // Checks: -wallwarm N turns them N degrees, without a build)
            float turn = Game.Arg("-wallwarm") is string ww && float.TryParse(ww, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float wv) ? wv : 0f;   // (8, the 54th's first: with its textures turned back, an orange brown)   // (22 to the 53rd: a dark brown against its gold floor, "地面和墙面的差距不要做这么大"; its textures turned back toward their own ochre, 11-03, they need less)
            Color Warm(Color c)
            {
                float scale = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                Vector3 lch = NestPalette.Oklch(new Color(c.r * 0.8f, c.g * 0.7f, c.b * 0.55f) / scale);
                Color o = NestPalette.FromOklch(new Vector3(Mathf.Min(1f, lch.x * 1.2f), lch.y, lch.z - turn)) * scale;   // (and lighter: the near walls, solid now, lost their gold glints; at 1.1, its stills still 0.083)
                return new Color(o.r, o.g, o.b, c.a);
            }
            t.bankTexTint = Warm(t.bankTexTint);
            t.plateauTexTint = Warm(t.plateauTexTint);
            t.bank = Warm(t.bank);
            t.edge = Warm(t.edge);
            t.plateau = Warm(t.plateau);
            // its far plateaus less vivid (its floor lifted, the 41st's trial: lit gold far off, the most vivid things in its
            // views, 3-10 % of their middles over the line). Checks: -plateaucalm N, -bankcalm N: their chroma times N
            Color Calm(Color c, float k)
            {
                float sc = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                Vector3 lch = NestPalette.Oklch(c / sc);
                Color o = NestPalette.FromOklch(new Vector3(lch.x, lch.y * k, lch.z)) * sc;
                return new Color(Mathf.Max(0f, o.r), Mathf.Max(0f, o.g), Mathf.Max(0f, o.b), c.a);
            }
            float plateauCalm = Check("-plateaucalm", 0.5f), bankCalm = Check("-bankcalm", 0.52f);   // (11-04, the user's "很黄色的部分": its walls 0.85 to 0.52)   // (0.4 and 0.5 to the 53rd: its walls a dull dark brown, 11-03)   // (its walls at 0.4, the 53rd's trial: near hardly more vivid than far off, 1.13 to 1.11, its family 10° to 8°)   // (0.5 and 0.65, the 50th: far off as vivid as near, 0.054 to 0.062)   // (at 0.8 and 1, its far walls the most vivid gold in view, "远处低")
            t.plateauTexTint = Calm(t.plateauTexTint, plateauCalm);
            t.plateau = Calm(t.plateau, plateauCalm);
            t.bankTexTint = Calm(t.bankTexTint, bankCalm);
            t.bank = Calm(t.bank, bankCalm);
            // (11-03, the user: "远处的山、远处的景太亮了，需要压暗。可以参考一下V15…我只要前景这部分比较能看清就好了。现在整体亮度可以
            // 稍微往下降一点点，但不用降很多": far off falling into the night as v15's does, near the ant as it was: everything
            // further than 10 units darker, to 0.35 of itself by 40 (its fog would, but the build has none: thicker and darker,
            // the 55th, its far hills not a point darker), its plateaus darker. Checks: -farfrom N, -farto N, -farkeep N, -plateaudark N)
            t.farDark = new Vector4(Check("-farfrom", 7f), Check("-farto", 30f), Check("-farkeep", 0.28f), 1f);   // (11-04, the user: "远景的也可以弱化，饱和度和亮度…如果光束不够显眼，压低场景的亮度，根据v15蓝色的效果为基准": from 7, not 10, to 0.3 by 32, its middle distance darker behind its beams; 10, 40, 0.35 to now)
            float plateauDark = Check("-plateaudark", 0.7f);
            t.plateau = new Color(t.plateau.r * plateauDark, t.plateau.g * plateauDark, t.plateau.b * plateauDark, t.plateau.a);
            t.plateauTexTint = new Color(t.plateauTexTint.r * plateauDark, t.plateauTexTint.g * plateauDark, t.plateauTexTint.b * plateauDark, t.plateauTexTint.a);
            // its floor near the ant purer than further off (10-29, the user: "近处可以饱和度高，远处低，现在反了": near the
            // ant its floor is the carpet, seven parts its palette's floor colour, a third as vivid as v15's, pale beige;
            // further off its terrain in its own textures' colours, its most vivid gold). Checks: -floorchroma N, its floor
            // colour's chroma times N (and -terrainsat N, its textures' saturation, GameConfig)
            t.floor = Calm(t.floor, Check("-floorchroma", 2.0f));   // (11-04, the user: "黄色的饱和度有点太高了，稍微降一些": 2.7 to 2.4, then 2.0, its floor bright and dyed in 6.7 % of the second trial)   // (2.3 to the 53rd, 11-03: its near floor greyer than its walls)   // (2.4, the 46th: its near floor still khaki, "偏橄榄卡其、发闷")
            // (its floor near the ant is mostly its terrain too, the carpet only a few steps round it: its textures purer
            // near the camera and calmer far off. Checks: -satnear N, -satfar N)
            t.groundSatByDistance = new Vector4(Check("-satnear", 1.2f), Check("-satfar", 0.35f), 5f, 15f);   // (11-04, as floorchroma: its near 1.5 to 1.2)   // (0.6 by 26 units, the 46th: its far way and its plateaus' feet still its most vivid gold in three views of four)
            t.floorVary = Check("-floorvary", 32f);   // (44 to the 53rd: half its patches swung into olive green, "太绿了", 11-03)   // (its floor's hue drifting further: 34 to the 46th, its family 9°)
            // its moonlight whiter, its fill light turned from gold (the 29th: lit by a gold moon and a gold fill, everything
            // lit read as one gold, its family 9°, "几乎全是同一种金黄"; its plants', roots' and crystals' own colours under it).
            // Checks: -moonwhite N moves its moonlight N of the way to white, -fillturn N turns its fill N degrees, without a build
            float Check(string arg, float otherwise) => Game.Arg(arg) is string a && float.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : otherwise;
            float white = Check("-moonwhite", 0.5f), fillTurn = Check("-fillturn", 0f);   // (the moon half way: it hardly moved the stills, 9°, but its lights read white)
            float moonMax = Mathf.Max(t.moon.r, Mathf.Max(t.moon.g, t.moon.b));
            t.moon = Color.Lerp(t.moon, new Color(moonMax, moonMax, moonMax, t.moon.a), white);
            if (fillTurn != 0f)
            {
                Vector3 fl = NestPalette.Oklch(t.fill);
                Color f2 = NestPalette.FromOklch(new Vector3(fl.x, Mathf.Max(fl.y, 0.02f), fl.z + fillTurn));
                t.fill = new Color(f2.r, f2.g, f2.b, t.fill.a);
            }
            // (and no damp green growth, its leaves dry straw and brown: mint mushrooms, teal and olive leaves and green
            // moss were what the ant saw near it, not its honey, "近处是薄荷色小蘑菇、青绿落叶")
            t.dampScale = 0.2f;
            t.leafColours = new[] { new Color(0.26f, 0.16f, 0.06f), new Color(0.32f, 0.21f, 0.08f), new Color(0.22f, 0.13f, 0.05f), new Color(0.30f, 0.20f, 0.09f), new Color(0.36f, 0.24f, 0.09f) };   // (dry ochre and tan; darker, flat on the floor they read as pits, "深色斑块像坑")
            // its landmarks beside the way (no nest has a heart now: "不要用心脏的概念了"; its own things beside the way):
            // its store, a seed husk split open and brimming with honey, a third and two thirds of the way along
            // the huge trees beyond its walls darker (its night 1.9 stops brighter than v15's lit their crowns as a big white
            // cloud over its walls, the brightest thing in it, "远景的大白团")
            // and no fan tree, the blue forest's, in its junction (a pale mint crown overhead and fronds by its way in, the
            // most vivid things in it: "大白团", "入口的青绿尖叶丛"); its landmarks are its seeds
            t.noJunctionTree = true;
            t.farDim = Check("-fardim", 0.24f);   // (darker with the rest far off, farDark)   // (at 0.1 black, its stills a fifth darker, 0.079; at 0.18, dark grey discs)
            t.landmark = "seedcup";
            t.landmarkAt = new[] { 0.33f, 0.62f };
            // (11-04, the user: "黄色这个场景现在有点太艳了，特别是可能近处还好，但是随着他走的这些发光的远，稍微中景的这样的粒子有点
            // 太艳了，可以稍微降一点点饱和度": near the ant as it was, its glowing points calmer from a few steps off, to three quarters
            // of their saturation by 13 units (groundSatByDistance calms the terrain's surface behind them, not the points; the
            // shared calm of the ground's points, ParticleLook.Calm, 11-04, works from 10 units on, its middle distance hardly).
            // Checks: -farsatfrom N, -farsatto N, -farsatkeep N, 1: off)
            float farSatKeep = Check("-farsatkeep", 0.5f);   // (0.7 from 5 to 13 in the second trial: its middle distance's walls still its most vivid gold, the blind review)
            if (farSatKeep < 1f) t.farSat = new Vector4(Check("-farsatfrom", 4f), Check("-farsatto", 11f), farSatKeep, 1f);
            // (and its walls less of a glitter: more of its ground points' light given to the surface behind them, as the kiln's
            // (0.6), and the points on the sides turned from the moon half as dim as the surface there; at the shared share,
            // about 0.5 at its exposure, its walls round its beams a sheet of bright gold flecks the beams were lost in, 11-04.
            // Checks: -tosurface N, -pointshade N)
            t.toSurface = Check("-tosurface", 0.7f);   // (0.6 in the third and fourth trials: its walls still flecked gold behind its beams)
            t.pointShade = Check("-pointshade", 0.5f);
            // (11-04, the user: "这几个场景的丰富度，我觉得可能还没有V15那个高，可以稍微再增加一些细节", "除了蓝色以外，这几个场景还是
            // 有些太空了。可以增加一些大的素材也好、小的素材也好": 171 plants grew inside its walls against the blue forest's 1526,
            // none up its banks or on its plateau, and its floor held its own things and a few leaves. Now the world's plants of
            // a dry granary, their own colours and tints folded into its band (ownBand, tintBand: the teal, mint and sage that took
            // them out between the 13th and the 34th rounds were its palette's tints before the band), darker and calmer than its
            // honey: dry grass in small tufts along its floor's edge and up its banks, and in tall clumps up them (silhouettes
            // in its middle distance, as v15's crowns and stalks), seed husks there too, heaps of seed by its floor's edge, a few
            // roots, sandstone boulders and gravel, dry leaves, dark porous trunks on its plateau; and its floor's own seeds and
            // pebbles back, brown and dark ("像蚁卵" was the palette's pearl and lilac on them). Checks: -honeyrich 0, as it was;
            // -honeyno a,b leaves those kinds out)
            if (Check("-honeyrich", 1f) > 0f)
            {
                var no = new System.Collections.Generic.HashSet<string>((Game.Arg("-honeyno") ?? "").Split(','));
                // (no pebbles, husks or heaps of seed: in the first trial, 11-04, the pebbles' and the heaps' pale ovals read as
                // eggs again, "像蚁卵", and the husk, a ribbed sage clam, as a great green leaf, its own texture's colour)
                // (nor porous trunks on its plateau: in the first trial's aerial a thin even scatter of dark sticks, hardly seen from
                // the ant's eye, the blind review)
                // (nor curled fallen leaves: pale sage-grey cups on its floor, broken eggshell, the second trial; its own leaves
                // are SolidThings', in dry straw and brown)
                t.scatterPlants = System.Array.FindAll(new[] { "dry_grass_tuft", "root_arch", "boulder_moss" }, k => !no.Contains(k));
                // (and more of its own finely made plants by its way: its croziers, their clusters and its puffballs)
                t.sparserKinds = new[] { "dry_grass_tuft", "root_arch", "fallen_leaf", "boulder_moss", "curl_d_cluster", "curl_a_crozier", "puff_a_stalked" };   // (fallen_leaf: none now)
                t.sparserBy = new[] { 1.6f, 1.1f, 1.1f, 1.3f, 0.8f, 0.85f, 0.9f };   // (its small grass tufts fewer: their few curled blades read as spiders by its way, the fourth trial; its tall clumps below)   // (its grass, roots and leaves as many as the blue forest's, its trunks fewer; 1.25, 1.3, 1.4 in the first trial, with the pebbles, husks and heaps)
                t.extraRules = System.Array.FindAll(new[]
                {
                    // tall clumps of dry grass along the tops of its banks, three to five ants tall, several blades' tufts from one
                    // crown leaning out over the way, their tips lit gold (a thin plant alone is flat: "做成一丛一丛"; up to 7 from
                    // the floor's edge and 2.4-4.4 tall in the first trial, the blind review still saw no forms in its middle distance)
                    new ScatterRule { plant = "dry_grass_tuft", dMin = -0.6f, dMax = 6f, spacing = 4.5f, height = new Vector2(3.4f, 6f), tilt = 0.15f, tintJitter = 0.1f, patchiness = 0.45f,   // (from the floor's edge: up the banks only, the second trial's review saw nothing taller than the ant by its way)
                        tuft = new Vector2Int(3, 5), tuftLean = 24f, tuftScale = new Vector2(0.6f, 1f) },
                }, r => !no.Contains(r.plant));
                t.noKinds = System.Array.FindAll(t.noKinds, k => k != "Seed");   // (its pebbles still out: a pale stone mottled grey, "camouflage", as the amber nest's 24th)
                t.thingColours["Seed"] = new Color(0.16f, 0.10f, 0.04f);   // (linear, as its leaves: its striped seeds and acorns brown)
                // (and its floor's things nearer the colours it gives them: at the usual 0.3 of their own, a striped seed's pale
                // stripes kept it a grey mottled oval, "像鹌鹑蛋", the first and second trials; its leaves and twigs are its
                // straw and brown already)
                t.thingKeep = 0.12f;
                foreach (var (k, g) in new[] { ("boulder_moss", 0.5f), ("root_arch", 0.6f), ("trunk_night", 0.45f), ("dry_grass_tuft", 0.62f), ("fallen_leaf", 0.7f) })   // (its grass at 0.85, a cream-white clump the brightest thing by the ant, the second trial)
                    t.kindGain[k] = g;
                // (its boulders' moss near grey: at 0.5 a pale mint lettuce up its banks, the band folding a sage that pale only
                // to a grey yellow-green)
                foreach (var (k, s) in new[] { ("boulder_moss", 0.15f), ("root_arch", 0.45f), ("trunk_night", 0.4f), ("fallen_leaf", 0.7f) })
                    t.kindSat[k] = s;
                // (its dry leaves with no light of their own: lit by their texture's pale parts, in the garden the brightest
                // white thing in view, its 33rd)
                t.headGlowKinds = new[] { "curl_d_cluster", "curl_a_crozier", "fallen_leaf" };
                t.headGlowFrom = new[] { -1f, -1f, -1f };
                t.bio["fallen_leaf"] = Vector4.zero;
                // its small life in its own gold, not v15's pale cyan (a midge's body was half that cyan in every nest until
                // 11-04: blue-white specks over its honey, a cool colour on a living thing), and a little more of it to see
                // (and more of its honey along the way, fewer of its crystallised flakes: in the first trial the blind review saw
                // pollen heaps by its way and hardly any honey, and a heap of pale gold flakes, "像一把金币")
                // (and its honey drops lining the way too, larger, between its drips and pollen: in the second trial the review
                // found one small drop of honey by its way in, "蜜只有入口一小滴")
                t.wayThing = "HoneyDrip*1.3,HoneyDrops*1.6,PollenPellets*1.4,HoneyDrip*1.3,PollenPellets*1.4";   // (its drops at 2.4 brown domes the size of its acorns, nuts more than honey, the fifth trial)   // (one drip in four, the fourth trial: its honey still seen once, by its way in)
                t.wayEvery = 4f;
                t.things = new[] { new Thing { kind = "HoneyDrops", weight = 18 }, new Thing { kind = "SugarCrystals", weight = 6 },
                    new Thing { kind = "PollenPellets", weight = 24 }, new Thing { kind = "HoneyDrip", weight = 18 }, new Thing { kind = "Gleanings", weight = 7 } };
                t.midgeTint = new Color(0.30f, 0.20f, 0.05f, 0.5f);   // (raw, linear, as the shader's: v15's 0.5, 0.85, 0.85)
                t.midgeWing = new Color(0.16f, 0.12f, 0.05f);
                t.microGain = 0.7f;   // (0.55 to now: its fluff and midges dim specks)
            }
            // its ants in its honey, as the amber nest's are in its amber (the 52nd: grey-blue, its one cool colour on a
            // living thing; leaning to the palette's turned colour, the ants' own teal still showed through)
            t.antTint = new Color(0.92f, 0.68f, 0.30f, 0.9f);   // (at 0.88, 0.72, 0.40 and 0.85, the 53rd's trial: a neutral grey)
            t.antRim = new Color(1.0f, 0.85f, 0.58f);
            return t;
        }
        static NestTheme ChartreuseBasic() => Basic("chartreuse", 90, "黄绿", "Chartreuse", "切叶蚁菌圃", "The Leafcutters' Garden",
            new[] { new Color(0.52f, 0.66f, 0.20f), new Color(0.58f, 0.62f, 0.42f), new Color(0.46f, 0.66f, 0.14f), new Color(0.60f, 0.64f, 0.46f), new Color(0.56f, 0.68f, 0.32f), new Color(0.78f, 0.88f, 0.56f) },
            new[] { new Color(0.76f, 0.28f, 0.18f), new Color(0.94f, 0.58f, 0.20f) },   // a red-veined leaf, orange petal pieces
            new[] { new Color(0.66f, 1.00f, 0.22f), new Color(0.78f, 1.00f, 0.40f), new Color(0.86f, 1.00f, 0.52f), new Color(0.72f, 1.00f, 0.30f) },
            new[] { new Color(0.96f, 0.95f, 0.82f), new Color(0.95f, 0.76f, 0.46f) },   // a pale cream, a muted amber
            new Color(0.56f, 0.52f, 0.24f), new Color(0.10f, 0.11f, 0.04f),
            new[] { "frond_a_feather", "curl_e_corkscrew", "crystal_a_spray", "crystal_b_fan", "crystal_c_mixed" },
            // (no lilac sponge mounds, on its slopes they read as nothing in particular; no fungus_garden, a brown-capped
            // white-stemmed mushroom that read as an ordinary one: its fungus is its own garden; no speckled pebbles,
            // pink-brown, read as eggs, and grey, as its fungus tufts)
            new[] { "tiny_mushrooms", "fallen_leaf", "boulder_moss", "coral_fungus" },
            // (the leafcutters' own: cut pieces standing like sails and lining the way, leaves with crescent bites, small
            // tufts of their fungus, drifts of the cut bits they dropped: its floor read as moss, the moss forest's)
            // (its drifts of dropped bits, LeafBits, left out: from the way they read as confetti, and its floor's own
            // texture is a litter of leaves)
            new[] { new Thing { kind = "LeafSail", weight = 22 }, new Thing { kind = "BittenLeaf", weight = 34 }, new Thing { kind = "FungusTuft", weight = 30 } },
            0.4f, "none", 0.4f, 0.32f, wayThing: "LeafSail", heart: "garden");
        public static readonly NestTheme Chartreuse = Garden(NestPalette.Apply(ChartreuseBasic(), NestPalette.For(3, LeafRecipe)));

        /// <summary>The garden's own touches over its palette (after NestPalette.Apply, which resets some of them).</summary>
        static NestTheme Garden(NestTheme t)
        {
            // no snail shells, no crystal grains (the crystals growing by its way are its crystals), no big brown-capped
            // mushrooms on its floor (they read as an ordinary mushroom: its fungus is its garden)
            t.noKinds = new[] { "Shell", "Crystals", "Mushrooms", "Pebbles", "Seed", "Petal", "Blossom" };   // (round pebbles and seeds by its way read as eggs, its pale petals as coins)
            // its corals' pale tips lit over a long soft band up their growth, not from where their texture's glow starts
            // (that read as two halves, "上下两截"), the band starting higher and their tips' markings softer (at 0.62 the
            // upper half went near white with a halo); its tiny mushrooms' caps lit only toward their tops, and their lump
            // of soil under the floor (a hard-edged saucer): glowing whole they took the scene from its leaves and garden
            // (the eighteenth, at its brighter exposure: the corals' heads lit white over four tenths of a branch, two
            // halves again; now no head of light at all, only their branches' tips glowing, the texture's own pale tops
            // left as they are)
            // (the thirty-third: its curled fallen leaves, lit by their texture's glow flags and pale parts, the brightest
            // white thing in view: no light of their own)
            t.headGlowKinds = new[] { "coral_fungus", "tiny_mushrooms", "fallen_leaf" };
            t.headGlowFrom = new[] { -1f, -1f, -1f };
            // its tiny mushrooms fewer and only their spots lit (they filled the room the dropped plants left, 264 of them,
            // their mint caps glowing whole: from the way they read as crystals and took the scene from its leaves)
            // (its corals fewer too: blue among its leaves, they stood out more than the cut pieces)
            // (corals at 1.5: still the biggest, brightest thing in view; its crystals fewer, smaller and off the way: a cluster
            // three ants tall stood in the way in front of a cut leaf, more striking than the leaf)
            t.sparserKinds = new[] { "tiny_mushrooms", "coral_fungus", "crystal_a_spray", "crystal_b_fan", "crystal_c_mixed" };
            t.sparserBy = new[] { 1.8f, 2.8f, 1.15f, 1.15f, 1.15f };   // (corals at 2.1: a cyan clump still took the eye)   // (crystals at 1.5 and 0.7 off the way: one spray left of five, none)
            // its fern clumps fuller (three fronds alike read as thin), its corkscrews nearer the way (at a wall's foot, in
            // its dark, they read as hanging in the air)
            t.labTweaks = new System.Collections.Generic.Dictionary<string, Vector4>
            {
                { "frond_a_feather", new Vector4(2, 0, 0, 0) }, { "curl_e_corkscrew", new Vector4(0, 0, 2.2f, 0) },
                { "crystal_a_spray", new Vector4(0, 0.4f, 0, 0.65f) }, { "crystal_b_fan", new Vector4(0, 0.4f, 0, 0.8f) }, { "crystal_c_mixed", new Vector4(0, 0.4f, 0, 0.7f) },
            };
            // its fern's and corkscrew's tips stronger and a little smaller (the fourteenth review: in the right places,
            // following their growth, but so faint they read as frosted tips, not light); the corkscrew's smaller still and
            // brighter, its tips' spots too (the fifteenth: the back half of its leaves pale all over, soft, as frost)
            t.bio = new System.Collections.Generic.Dictionary<string, Vector4>
            {
                // (the thirty-third: the specks behind its tips (5) read as white flakes on it: its tips alone, as the fern's)
                { "coral_fungus", new Vector4(8, 0.05f, 0, 1f) },
                // (the twenty-ninth: the specks behind its tips (5) read as frost on its leaflets: its tips alone, a little
                // smaller; the thirty-third: still mint-white blobs, smaller again and in its own light colour, markColor;
                // the thirty-fifth: lime tips alone read as a real fern's new leaves, no pattern of its own: as its
                // corkscrew, which passed, a string of beads up its stems, its tips lit by its spots)
                // (beads at 14 and 0.05, at 18 and 0.035, read as scattered blotches or spots of light, not its own pattern:
                // its stems lit whole, its rachis and its leaflets' stalks a lit skeleton, as the reference's ferns' veins)
                { "frond_a_feather", new Vector4(2, 18, 1f, 1.1f) },
                { "fallen_leaf", Vector4.zero },
                // (the eighteenth: its tips alone, soft, read as an ordinary variegated vine: narrow rings up its stems only,
                // square to them as they twist, a beaded line following its spiral; its tips still lit by its spots below)
                // (the twenty-ninth: twenty wide rings read as broken white flecks, as snow: fewer and narrower, a string of
                // beads; its leaves' tips less, they went pale pink whole)
                { "curl_e_corkscrew", new Vector4(2, 11, 0.032f, 1.3f) },   // (narrower, on its stems: at 0.045 blotches)
            };
            t.spots = new System.Collections.Generic.Dictionary<string, Vector4[]> { { "curl_e_corkscrew", new[] { new Vector4(2, 0.86f, 0f, 0.4f) } }, { "frond_a_feather", new[] { new Vector4(2, 0.9f, 0f, 0.5f) } } };
            // its fern clumps darker at their foot (their lowest leaflets grey-white, standing on the floor, not in it)
            t.kindFoot = new System.Collections.Generic.Dictionary<string, float> { { "frond_a_feather", 0.58f } };
            t.sunkKinds = new[] { "tiny_mushrooms" };
            t.sunkBy = new[] { 0.28f };
            // the motes circling its plants smaller and softer, the motes in its air fewer to the eye, dimmer and in its
            // own yellow-green (the third and eleventh reviews: "太多、太白"); the huge trees beyond its walls a dim shape
            // in its night, neither a black hole in it nor a pale cloud over the way (1.4, and still at 1.15)
            t.moteSize = 0.4f;
            t.moteGain = 0.35f;   // (the fifteenth review: near the lens still white balls, not soft halos)
            t.motes = Color.Lerp(t.motes, t.glow[1], 0.7f) * 0.5f;
            t.farDim = 0.35f;   // (at 1, a big pale crown over its way; at 0.8, at 0.65 still a grey cloud above its walls: a dark shape)
            // its corals' own blue muted (saturated, they were the most striking thing in its greens and browns)
            t.kindSat = new System.Collections.Generic.Dictionary<string, float> { { "coral_fungus", 0.2f }, { "tiny_mushrooms", 0.35f }, { "trunk_night", 0.3f }, { "boulder_moss", 0.55f } };   // (and a fallen trunk's blue-teal scales across the way in, a cyan mossy boulder: the thirty-seventh, the most striking things in view)   // (coral 0.45, 0.32: still an icy blue among its olive and brown; the tiny mushrooms' blue caps, the thirty-seventh: brought from the blue forest)
            // its twigs and the roots at its walls' feet bark grey and olive, not the palette's mint (a big blue-teal
            // branch across the way was the most striking thing in view)
            // (and its fallen fern fronds sage and olive, not the palette's mintiest tints)
            t.thingTints = new System.Collections.Generic.Dictionary<string, int[]> { { "Twig", new[] { 3, 5 } }, { "Root", new[] { 3, 5 } }, { "Fern", new[] { 2, 5 } } };
            // (its vines olive, the forty-first: the big blue-teal scaly thing across the way in was not one of them, and
            // its family narrowed to 15°, its floor bright and dyed 3.3 %: the palette's)
            // its plants' markings in its own pale leaf-lime, stronger, their palest parts' faint light less: near white,
            // its fern's tips and its corkscrew's beads read as frost, one with the pale specks of its walls (the
            // twenty-ninth and thirty-third reviews)
            // (the thirty-seventh: in leaf-lime at 1.4 its markings read as moss spots, the colour of its floor, not as
            // light: paler and twice as strong, as the granary's gold went from "不发光" at 1.6 to light at 2.2)
            t.markColor = new Color(0.80f, 0.95f, 0.55f, 1f);
            // its corals and curled fallen leaves darker: pale and cold, the brightest plants on its green floor (the
            // thirty-third and thirty-fifth reviews)
            t.kindGain = new System.Collections.Generic.Dictionary<string, float> { { "coral_fungus", 0.5f }, { "fallen_leaf", 0.7f }, { "frond_a_feather", 0.85f }, { "curl_e_corkscrew", 0.9f } };   // (coral at 0.65, the fortieth: still half as light again as its floor)   // (its fern and corkscrew a little darker, so their markings read as light on them)
            t.markGain = 2.2f;
            t.faintGain = 0.5f;
            // its fallen leaves green, olive, tan and brown, no plum or rust (on its floor they read as painted patches)
            // (a tan one warmed its floor toward olive: a fresh green instead)
            t.leafColours = new[] { new Color(0.10f, 0.16f, 0.04f), new Color(0.16f, 0.18f, 0.05f), new Color(0.12f, 0.20f, 0.05f), new Color(0.14f, 0.10f, 0.05f), new Color(0.08f, 0.13f, 0.035f) };
            // its walls lighter than the palette's (and so its far floor's dark surface stays as it was): lifting the
            // ground's lightness for them lit its floor past its limit (L 0.54), and the walls still read dark and drab
            // (lifted more in red than blue they read olive, khaki halfway: as much, cooler; and its floor texture's own
            // olive, which shows through its pale tint, cooled a little)
            t.bank = new Color(t.bank.r * 1.22f, t.bank.g * 1.32f, t.bank.b * 1.3f);
            t.floorTexTint = new Color(t.floorTexTint.r * 0.88f, t.floorTexTint.g, t.floorTexTint.b * 1.04f, t.floorTexTint.a);   // (its floor A4, brown humus, 116°: a little olive in it)
            t.bankTexTint = new Color(t.bankTexTint.r * 0.94f, t.bankTexTint.g, t.bankTexTint.b * 1.05f, t.bankTexTint.a);   // (the thirtieth: halfway its walls still khaki)
            // the dust in its air and the glowing grains on its floor fewer and in its own light (pale specks everywhere
            // read as frost over its leaves)
            // (the twenty-ninth: in close-ups still dense pale specks, with the plants' markings read as snow: half again,
            // and more of its own colour)
            t.dustGain = 0.15f;
            t.dustTint = new Color(t.glow[1].r * 0.7f, t.glow[1].g * 0.7f, t.glow[1].b * 0.7f, 0.85f);
            t.grainGain = 0.12f;   // (at half, close up, still "like snow" over its leaves; at 0.3 the glowing grains on its walls, behind a close-up, still read as snow in the air)
            // its landmarks beside the way (no nest has a heart now: "不要用心脏的概念了"): its fungus garden, the heart
            // it had, at two places along the way (crowded with plants, at first one of them found no room; it now looks a
            // little before and after, and grows over what is low round it)
            t.landmark = "garden";
            t.landmarkAt = new[] { 0.28f, 0.66f };   // (three read as a repeated prop, not a landmark)
            // (11-04, the user: "这几个场景的丰富度，我觉得可能还没有V15那个高，可以稍微再增加一些细节", "除了蓝色以外，这几个场景还是
            // 有些太空了。可以增加一些大的素材也好、小的素材也好": 491 plants inside its walls against the blue forest's 1526, its
            // banks bare past its ferns. Now more of a garden's: roots through its chambers, shelf fungi up its walls, small cup
            // and jelly fungi by its floor's edge, more of its own ferns and corkscrews by its way, and its moss boulders, small
            // mushrooms and fallen leaves a little denser; the fungi near grey and lit only at their markings, as its small
            // mushrooms are (their whole caps lit read as crystals). Checks: -leafrich 0, as it was; -leafno a,b leaves those
            // kinds out)
            // (11-04, the user, of the granary's and the moss forest's yellow, for this nest too: "黄色的饱和度有点太高了，稍微降
            // 一些，或者减少一些这些很黄色的部分，然后像这种远景的也可以弱化，饱和度和亮度，然后自查如果光束不够显眼，压低场景的亮度，
            // 根据v15蓝色的效果为基准": its ground textures a little less saturated (its bright yellow-olive slopes), calmer and
            // darker far off as v15 falls into the night, its glowing points calmer from a few steps off, and the whole a little
            // darker (LeafRecipe) so its beams read. Checks: -leafgroundsat N (-1: as it was), -leafsatfar N (0: the same
            // everywhere), -leaffarfrom N, -leaffarto N, -leaffarkeep N (1: not darker), -leaffarsat N (1: off))
            float LeafTry(string arg, float otherwise) => Game.Arg(arg) is string la && float.TryParse(la, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lv) ? lv : otherwise;
            t.groundSat = LeafTry("-leafgroundsat", 0.8f);
            float leafSatFar = LeafTry("-leafsatfar", 0.55f);
            if (leafSatFar > 0f) t.groundSatByDistance = new Vector4(LeafTry("-leafsatnear", 1.3f), leafSatFar, 6f, 20f);   // (its near at 1, the second trial: greyish, 43 % less colourful than v15)
            float leafFarKeep = LeafTry("-leaffarkeep", 0.25f);   // (from 8 at 0.4 in the second trial, from 6 at 0.3 in the fourth: its middle distance's olive walls as bright as its near floor)
            if (leafFarKeep < 1f) t.farDark = new Vector4(LeafTry("-leaffarfrom", 4f), LeafTry("-leaffarto", 28f), leafFarKeep, 1f);
            // (and its walls round the ant a little darker, less of a glitter of white lines: as the granary's)
            t.wallGain = LeafTry("-leafwallgain", 0.85f);
            t.toSurface = LeafTry("-leaftosurface", 0.6f);
            float leafFarSat = LeafTry("-leaffarsat", 0.75f);
            if (leafFarSat < 1f) t.farSat = new Vector4(5f, 14f, leafFarSat, 1f);
            if (Game.Arg("-leafrich") != "0")
            {
                var no = new System.Collections.Generic.HashSet<string>((Game.Arg("-leafno") ?? "").Split(','));
                // (its living things in its own band, as the granary's are: in the first trial the blind review found cyan on
                // living things, its corkscrews' and vines' pale teal, its fungi's slate blue, "活物上有青色"; their own colours and
                // tints folded into yellow-green to green, half as vivid where they are turned)
                t.ownBand = new Vector2(LeafTry("-leafbandcentre", 85f), LeafTry("-leafband", 25f));   // (95 ± 35 in the second trial: its shelf fungi still blue-grey, the band's green edge a cool green)
                t.tintBand = new Vector2(LeafTry("-leafbandcentre", 85f), LeafTry("-leafband", 25f));
                // (and its floor's own small growth, moss cushions with their stalks, seedlings, clover and lichen, in its leaf
                // colours, not the palette's tints: a cyan cushion with stalks by its way's end and cyan two-leaved seedlings by
                // its way in were the most vivid things in the second trial; no feathers, pale flakes)
                t.thingColours = new System.Collections.Generic.Dictionary<string, Color>
                    { { "Moss", new Color(0.08f, 0.09f, 0.03f) }, { "Seedling", new Color(0.12f, 0.17f, 0.04f) }, { "Clover", new Color(0.10f, 0.15f, 0.035f) }, { "Lichen", new Color(0.12f, 0.13f, 0.06f) } };   // (its moss olive brown: at 0.09, 0.13, 0.03 a bright green cushion, the moss forest's, the fourth trial)
                t.noKinds = new System.Collections.Generic.List<string>(t.noKinds) { "Feather", "Moss" }.ToArray();   // (and no moss cushions: green, with their stalks, the moss forest's, the fourth and fifth trials; its own floor's moss texture is enough, "青苔少一点")
                // (no spray crystals: a cyan sponge tuft by its way's end, the most vivid thing there, as in the granary)
                t.labPlants = System.Array.FindAll(t.labPlants, k => k != "crystal_a_spray");
                // (its vines dark olive stems, their veins and tips its pale lime, fewer hooks: pale teal-white tubes arched over
                // its way, "青白螺旋藤"; and no roots at its walls' feet: a big blue-grey scaly tube by its way in, "像蛇，又像根",
                // as the granary's and the moss forest's read as worms and beads)
                t.vineDark = new Color(0.035f, 0.042f, 0.015f);
                t.vineLeaf = new Color(0.08f, 0.11f, 0.03f);
                t.vineDarken = 0.95f;
                t.vineLight = new Color(0.72f, 0.9f, 0.45f, 1f);
                t.vineGlow = 0.6f;
                t.vineCurl = 0.4f;
                t.rootWidth = 0f;
                // (and no fan tree, the blue forest's, at its junction: its trunk the big scaly tube by its way in that every
                // review since the 37th found the most striking thing there, blue-grey, sage under its band; as in the granary,
                // its landmarks are its own, its fungus gardens)
                t.noJunctionTree = true;
                t.scatterPlants = System.Array.FindAll(new[] { "tiny_mushrooms", "fallen_leaf", "boulder_moss", "coral_fungus", "root_arch", "bracket_shelf", "cup_fungus", "jelly_fungus" }, k => !no.Contains(k));   // (no dry grass either: tan and orange tufts in its greens, the first trial)   // (no porous trunks: in a trial, 11-04, a big blue-teal scaly trunk by its way, its own texture's colour, the most striking thing in view, as the 37th review found one by its way in)
                // (and more of its own ferns and corkscrews by its way)
                t.sparserKinds = new[] { "tiny_mushrooms", "coral_fungus", "crystal_a_spray", "crystal_b_fan", "crystal_c_mixed", "boulder_moss", "fallen_leaf", "root_arch", "cup_fungus", "jelly_fungus", "frond_a_feather", "curl_e_corkscrew" };
                t.sparserBy = new[] { 1.8f, 2.8f, 1.15f, 1.3f, 1.3f, 1f, 0.8f, 1.3f, 1.4f, 1.6f, 0.8f, 0.8f };   // (its small mushrooms 1.3 in the first trial, white round caps in a heap, "有点像卵"; its boulders an even dot pattern from above at 0.8)
                // (its shelf and cup fungi near grey: at 0.5 and 0.35 the shelves a pale blue-teal stack, the cups slate blue, their
                // own textures' cool on living things, the first trial)
                foreach (var (k, s) in new[] { ("root_arch", 0.6f), ("bracket_shelf", 0.15f), ("cup_fungus", 0.15f), ("jelly_fungus", 0.2f) })
                    t.kindSat[k] = s;
                foreach (var (k, g) in new[] { ("root_arch", 0.7f), ("bracket_shelf", 0.6f), ("cup_fungus", 0.6f), ("jelly_fungus", 0.7f), ("crystal_b_fan", 0.6f), ("crystal_c_mixed", 0.6f), ("tiny_mushrooms", 0.75f) })
                    t.kindGain[k] = g;
                // big fallen leaves lying up its banks, a few ants long: the leaves its ants cut from, its own large forms
                t.extraRules = System.Array.FindAll(new[]
                {
                    new ScatterRule { plant = "fallen_leaf", dMin = 0.3f, dMax = 6f, spacing = 4.5f, height = new Vector2(0.6f, 1.2f), tilt = 1f, tintJitter = 0.12f, patchiness = 0.5f, noShadow = true },   // (6 apart up to 5 in the second trial: its gate's sides and half way's left bank still bare)
                }, r => !no.Contains(r.plant));
                t.headGlowKinds = new[] { "coral_fungus", "tiny_mushrooms", "fallen_leaf", "cup_fungus", "jelly_fungus" };
                t.headGlowFrom = new[] { -1f, -1f, -1f, -1f, -1f };
                // its small life in its own leaf colours, not v15's pale cyan (fixed in the shader for every nest until 11-04)
                t.midgeTint = new Color(0.17f, 0.24f, 0.05f, 0.5f);
                t.midgeWing = new Color(0.10f, 0.13f, 0.05f);
            }
            return t;
        }

        static NestTheme trial, leafTrial;

        /// <summary>A nest without these of the floor's things (after NestPalette.Apply, which clears the list).</summary>
        static NestTheme Without(NestTheme t, params string[] kinds)
        {
            t.noKinds = kinds;
            return t;
        }

        static NestTheme Patched(NestTheme t, Color patchA, Color patchB)
        {
            t.floorPatchA = patchA;
            t.floorPatchB = patchB;
            return t;
        }

        /// <summary>The honey granary and the leafcut garden as they were before the palette recipe (2026-10-26: the
        /// effect images' colours, their things and plants keeping more of their own, the ground brighter, patches of other
        /// materials on it), for checks comparing the two at the same moment (-palette before).</summary>
        static NestTheme Before(NestTheme t, Color patchA, Color patchB)
        {
            t.floorPatchA = patchA;
            t.floorPatchB = patchB;
            t.thingKeep = 0.7f;
            t.plantKeep = new Vector2(0.78f, 0.92f);
            t.groundTexMix = 0.6f;
            t.floorVary = 40f;
            t.saturation = 0f;
            t.exposureLift += 0.45f;
            t.noKinds = new string[0];
            return t;
        }
        static readonly NestTheme YellowBefore = Before(YellowBasic(), new Color(0.30f, 0.20f, 0.11f, 0.85f), new Color(0.80f, 0.70f, 0.45f, 0.7f));
        static readonly NestTheme ChartreuseBefore = Before(ChartreuseBasic(), new Color(0.38f, 0.25f, 0.12f, 0.85f), new Color(0.72f, 0.74f, 0.66f, 0.6f));
        // the moss forest: v15's palette turned to segment 4 (NestPalette). Its hue turned toward blue-green and its
        // family spread toward the yellow-green of moss tips, away from the leafcut garden next to it (its world 128°,
        // its plants spread toward teal): an emerald world, not a leaf-green one. As bright as the garden ("颜色要够亮,
        // 不要暗沉、土"). (Variants tried with -recipe; see Docs/报告/2026-10-27_苔藓森林/: at full chroma, 11 % of the
        // picture loud, its floor the loudest; its family spread wider, 14° read as one green)
        // (review k2: at 1 stop 0.094 bright, too near the garden's "太暗" 0.076, and its family 13° wide)
        // (review k4: brighter, its floor round the ant dyed green: base chroma 0.6 -> 0.5; its family's width waits for
        // its own ground textures and its lichen and liverwort models, more spread toward gold brought the gold back)
        // (review k5: the floor round the ant still a bright green pool: ground gain 0.2 -> 0.17)
        // (review k7: at 1.35 stops 0.113, the 0.11 the garden was "还是太暗" at: 1.5, its floor's green taken down in its
        // ground textures' saturation instead, GameConfig)
        // (review k12: its own floor a lit green lawn, L 0.50: 0.2 stops more, the light on its plants and things rather
        // than its floor, as the leafcut garden's, its floor's gain down; base chroma 0.5 -> 0.45. The gain is 1 + this in
        // a warm world, and green counts warm: at 0.09 with the brighter exposure the floor stayed at L 0.49, k13; at -0.4
        // its floor about half as bright, as the granary's at -0.5)
        // (review k14: its floor L 0.44, its lower third still half as bright again as the rest, v15's level: -0.6, and a
        // tenth of a stop more for the rest)
        // (review k15: its lower third still 1.4 times the rest: -0.7, and the light near the ant halved, Mossy: nearLift;
        // the picture then 0.095, under the garden's "还是太暗" 0.11: 0.15 stops more, the guiding light's pool on its floor
        // dimmer instead, Mossy: beacon; at 1.95 stops still 0.091, its pools of light gone: 2.3 stops, as the granary's,
        // the floor held down apart, -0.85)
        // (review k23, after the game kept glowing things at v15's light however far a nest lifts its exposure: 0.082,
        // its walls near black under a lighter grey-green sky, 12 degrees greener than the garden: its walls and floor
        // lifted, not the exposure (Mossy: wallGain), its sky darker, 5 degrees greener)
        // (review k26, its floor 1.4 times the rest: its walls lifted more; k28, the walls' own colours lifted, its floor then
        // darker than the rest, 0.88, the whole still 0.080: its floor up again, -0.6)
        // (review k29 passed its colours; both it and the content review found it close to the leafcut garden, 13 degrees:
        // 4 degrees greener)
        // (11-03, the user, of k33 against its first version: "往第零版的颜色靠一靠，但是不要这么饱和，现在有点太不饱和了",
        // "亮度上也可以向第零版靠一靠，但是不要这么暗，现在有点太亮了，缺少一些神秘感": halfway to the first version's
        // darkness and saturation, 0.097 -> 0.068 against its 0.041 on the same four stills. 2.3 stops and a lift of 0.09
        // were a grey veil over its walls and floor: 1.85 and 0.04; its chroma up, the lights' only a little (at 1.8 the
        // ring and the gate turned turquoise, k34 trials; its floor and walls are their textures' colours, Mossy: groundSat)
        // (11-04, the user: "自查如果光束不够显眼，压低场景的亮度，根据v15 蓝色的效果为基准": round its guiding beams the
        // scene was half as light again as v15's, its beams 3.3 times their background against v15's 4.2; at 1.6 stops,
        // k35 and k36, 0.054, the colour review: "又回到了用户说的'这么暗'", its beams already standing further above
        // what is behind them than v15's, by lightness, 0.52 against 0.38. So 1.85 again, and its far off much darker,
        // Mossy: the light moved from its far walls to its near floor. Its hue family wider, 1.3: at 16 degrees half its
        // middle colours one green, k36. A tenth of a stop more with its slopes darker from 3 units, k38 (0.061 at k37,
        // the review's floor 0.058). -mossrich 0: as k34)
        public static readonly NestPalette.Recipe MossRecipe = new NestPalette.Recipe { spread = Game.Arg("-mossrich") != "0" ? 1.3f : 1.1f, spreadSide = -1f, exposure = Game.Arg("-mossrich") != "0" ? 1.95f : 1.85f, groundLift = 0.04f, groundGain = -0.6f,
            baseChroma = 0.8f, midChroma = 1.15f, highChroma = 1.1f, hueNudge = 25f, jitter = 1.3f, skyGain = 0.6f };   // (sky 0.7, k26: the walls still darker than it)
        static NestTheme mossTrial;
        public static readonly NestTheme Green = Mossy(NestPalette.Apply(GreenBasic(), NestPalette.For(4, MossRecipe)));
        /// <summary>The moss forest after its palette: no petals, blossoms or white feathers on its floor (a moss forest;
        /// a feather at the way's end read as a sheet of white paper), its light gates as dim as its first version's (at
        /// v15's, the gate behind its heart stood as a bright disc beside the drop). Its boulders and cushions of moss take
        /// the family's calm green, the gold yellow left to small things (each plant kind takes one palette colour by its
        /// name, each floor thing one of a few: the boulders took the most vivid gold, and half the moss cushions v15's
        /// pale yellow-green accent turned to gold, then, paled, still twice as bright as the floor: now a darker green).
        /// None of the leafcut garden's sponge mounds, the blue forest's cup fungi or the pebble heaps (five of its six
        /// floor plants were the garden's; moss covers stones). Its walls dark surfaces, not a speckle of pale grey
        /// points, and its air less snowy; its small mushrooms with clear spots of their own (their pale bodies hid the
        /// shared pattern). Its palest middle colours darker: after its exposure was lifted, the moss cushions,
        /// mushrooms, stones and leaves that took them were the brightest patches, 1.6 to 2 times the floor, a scatter of
        /// pale confetti; its mushrooms' slot an olive brown, so their spots show; its plants keep less of their own pale
        /// textures; the huge trees beyond its walls dark shapes (a pale grey-green canopy stood out). No dew drops until
        /// it has its own (they are the azure nest's marbles too: their lamps were the brightest white balls in it).</summary>
        static NestTheme Mossy(NestTheme t)
        {
            Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
            // (no snail shells, seeds, pebbles, crystals or clover either: in its palette's colours they were spotted
            // brown and pink balls and blue-green leaf clumps nobody could name, review k20; nor the floor's lichen
            // crusts and fallen leaves, pale purple wrinkled clumps in the palette's lilac: keeping a third of their own
            // pale colours, under a grey-green or a dead leaf's brown of their own the crusts still read as white crumpled
            // paper, k22 and k23, and the leaves, which the kinds left out turn into, as large pale tan sheets, k24; its
            // moss, mushrooms, seedlings, ferns and twigs stay)
            // (nor the floor's twigs, seedlings and mushrooms: a grey-pink worm, a pale seedling taller than the parasols and a
            // spotted pink-brown dome before the lens, k26 content review; the scatter's small mushrooms stay)
            Without(t, "Petal", "Blossom", "Feather", "Shell", "Seed", "Pebbles", "Crystals", "Clover", "Lichen", "Leaf", "Twig", "Seedling", "Mushrooms");
            t.gateGain = 0.28f;
            (t.body[1], t.body[2]) = (t.body[2], t.body[1]);
            t.body[6] = Dim(Color.Lerp(t.body[1], t.body[5], 0.5f), 0.61f);
            t.body[1] = Dim(t.body[1], 0.61f);
            t.body[0] = Dim(t.body[0], 0.75f);
            t.body[4] = Dim(t.body[4], 0.75f);
            t.body[5] = Dim(Color.Lerp(t.body[5], t.body[7], 0.4f), 0.65f);
            t.plantKeep = new Vector2(0.3f, 0.55f);
            // the huge trees beyond its walls dark shapes in its fog's grey-green (a fan tree kept the blue forest's pale blue
            // cap over the capsule grove: the user, 10-27, "苔藓里压暗转灰绿")
            t.farDim = 0.3f;
            t.farFog = 0.85f;
            // its floor not one green: wider swings of hue, and patches of pale grey-green lichen (no brown: earthy, and
            // the leafcut garden's; darker than its floor colour, at half strength, they did not show; at 0.8 over its lit
            // floor, review k12: no lichen to be seen) (and wider, 42, with 11-04's changes: its hue family 15 degrees, half
            // its middle colours one green, colour reviews of k36 and k37; -mossrich 0: 34)
            t.floorVary = Game.Arg("-mossrich") != "0" ? 42f : 34f;
            t.floorPatchB = new Color(0.78f, 0.82f, 0.72f, 1f);
            // (checks: -mosstexmix N tries how much of its floor texture's own colour its floor keeps, with -groundtrial
            // for the lichen painted into it: at the palette's 0.3 the painted lichen, as light as the moss crowns, did
            // not show, k19)
            if (Game.Arg("-mosstexmix") is string tm && float.TryParse(tm, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tmv))
                t.groundTexMix = tmv;
            // (nor the fallen leaves of the scatter: a cream curl beside the cushions read as eggshell, k26)
            t.scatterPlants = System.Array.FindAll(t.scatterPlants, k => k != "sponge_mound" && k != "cup_fungus" && k != "pebble_cluster" && k != "fallen_leaf");
            // and fuller (the user, 11-04, of the nests but the blue one: "还是有些太空了。可以增加一些大的素材也好、小的素材也好";
            // the richness review against v15, k34: its large things nearly all capsule groves, in seven stills of nine, its
            // way's end and the left of its way in empty, its far off a few dark crowns): the dark trunks of the trees it
            // grows under, between and beyond its walls, a few (darker and calmer below); its capsule groves fewer, about 60
            // of 106 (LabNest: its own plants' landmarks where it was empty). (Not the green-textured arching roots: green on
            // its green floor, a blurred green pipe; nor its small mushrooms or boulders evenly denser: 263 mushrooms and
            // none read, review k34.) Checks: -mossrich 0 (all of 11-04's changes off, as k34: these, its far off, its
            // yellows, its exposure, LabNest's landmarks)
            bool rich = Game.Arg("-mossrich") != "0";
            if (rich)
            {
                t.scatterPlants = new System.Collections.Generic.List<string>(t.scatterPlants) { "trunk_night" }.ToArray();
                t.sparserKinds = new[] { "moss_capsules", "trunk_night" };
                t.sparserBy = new[] { 1.35f, 3f };   // (trunks at 2, k35: 85 of them, 27 before)
            }
            // its walls a little lighter than the palette's, and the faces of its far walls lighter still (at 0.78, its walls
            // dark surfaces, not a speckle of pale points, k3; since the game keeps glowing things at v15's light, 0.08 overall
            // with its walls near black, reviews k23 and k26: lifted in their own colour, not by the exposure)
            t.bank = Dim(t.bank, 1.3f);
            // (1.05 and 1.4, k28: 0.080 still, its largest black 9% -> 4%; at 1.9 even, k29 to k32, its walls a pale grey stone,
            // the pale lichen lost against them, k32 reviews: as light, greener, moss-covered)
            // (with 11-04's changes a fifth darker and less green, 1.4, 1.6, 1.3: the slope beside its gate, terrain 3 to 8 units
            // off where the carpet ends and its walls' splats do not reach, lit lime green, the loudest of its pictures,
            // colour reviews of k37 to k39)
            t.bankTexTint = rich ? new Color(t.bankTexTint.r * 1.4f, t.bankTexTint.g * 1.6f, t.bankTexTint.b * 1.3f, t.bankTexTint.a)
                : new Color(t.bankTexTint.r * 1.7f, t.bankTexTint.g * 2.05f, t.bankTexTint.b * 1.55f, t.bankTexTint.a);
            t.edge = Dim(t.edge, 0.95f);
            // its ground textures, which its walls and floor take, as saturated as drawn (GameConfig's 0.75 dates from the
            // blue forest's borrowed textures, k7; with its own, the grey walls and floor of k33, "有点太不饱和", 11-03)
            t.groundSat = 1f;   // (1.2 in the k34 trials: as vivid as its first version, 6.7% loud)
            // but only close by: its far off calmer and a little darker (the user, 11-04: "远景还是有点太艳了。近的东西可以这样，
            // 但远的东西还是要稍微弱化一点", and a point vivid far off turned pale as the ant walked up to it, "这个逻辑反而是反的":
            // at 1 all the way, its far walls, the terrain's own colours, were the most saturated part of its pictures, the
            // points of its near walls and floor a palette grey-green; and of the far off in a screenshot, 02:15, "像这种远景的
            // 也可以弱化，饱和度和亮度": calmer and darker than first planned, 0.42 and 0.8; and darker still, behind its guiding
            // beams, which stand off down the way: "自查如果光束不够显眼，压低场景的亮度，根据v15 蓝色的效果为基准": at 0.65
            // from 10 to 36, k35, its beams 3.7 times their background, v15's 4.2, its brightness 0.055 against the 0.050
            // of its first version, "不要这么暗", 11-03: the far off darker, not the whole; and the colour review of k36: its
            // walls beyond the ant's splats still 3.6 times as light as its near floor, 21% of them over C 0.08 against 1.4%:
            // from 6 units on, by 16 calm, by 20 at 0.4; its near ground fuller, 0.85; and its review of k37: its truly far
            // walls quiet now, but the slopes 6 to 12 units off along the way and at its end the most vivid of it, 51-59% of
            // the way's end over C 0.08, two thirds of all its loud pixels: from 3 units on, 0.75 near, its far 0.35 of
            // itself; its terrain from 2 units 0.6, k40: what shows of it close by is the slopes beyond the carpet, its near
            // floor the carpet's). Checks: -mosssatnear, -mosssatfar (0: the same everywhere), -mosssatfrom, -mosssatto,
            // -mossfarkeep (1: not darker), -mossfarfrom, -mossfarto
            float Try(string arg, float otherwise) => Game.Arg(arg) is string s && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val) ? val : otherwise;
            if (rich) t.groundSatByDistance = new Vector4(Try("-mosssatnear", 0.6f), Try("-mosssatfar", 0.2f), Try("-mosssatfrom", 2f), Try("-mosssatto", 10f));   // (to 16 in k38 to k40: the slope at its gate, 6 to 8 units off, still 22-27% over C 0.08, review k40)
            float farKeep = rich ? Try("-mossfarkeep", 0.35f) : 1f;
            if (farKeep < 1f) t.farDark = new Vector4(Try("-mossfarfrom", 3f), Try("-mossfarto", 20f), farKeep, 1f);
            // its motes fewer, smaller and in its own light, not white balls before the lens (review k14), as the garden's
            t.motes = Color.Lerp(t.motes, t.glow[1], 0.6f) * 0.5f;
            t.moteSize = 0.4f;
            t.moteGain = 0.25f;   // (at 0.5 and 0.4, k15: still a white ball before the lens in a close-up)
            // what is near the ant and what it walks past lit less (at v15's, the floor round the ant was the brightest third
            // of the picture, and the pale lichen, the parasols and the star moss flared into white pillars and walls of
            // light as it walked up, reviews k14 and k15; as the kiln's, the user's: subtle)
            // (0.45 both until k26: the cup lichen beside the ant still lit whole a pale green-white, and the floor round the
            // ant still half as bright again as the rest of the picture)
            t.nearLift = 0.24f;
            t.walkGain = 0.24f;   // (0.32 in k27 to k29: the cups beside the ant still white, k29 plant review)
            // an antenna's touch as subtle (at the usual, a parasol colony touched lit up whole in green and white, four times
            // its own light, k30, once its landmark was large enough for the ant to reach; at 0.35, k31, its marks still a
            // string of lights)
            t.touchGain = 0.2f;
            // the pool of the guiding light on its floor dimmer (halfway, the floor before the ant three times as bright as
            // the rest of the picture, k16)
            t.beacon = Dim(t.beacon, 0.6f);
            // its ring round the ant, its guiding threads and its light gates (their halo) a little greener: the palette's
            // pale turquoise stood 20 degrees off its first version's green (the user, 11-03: its colours toward the first
            // version's; about halfway). Checks: -mossturn N turns them N degrees round the HSV wheel instead
            float turn = Game.Arg("-mossturn") is string mt && float.TryParse(mt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float mtv) ? mtv : -12f;
            t.ring = HueTurn(t.ring, turn);
            t.threadCore = HueTurn(t.threadCore, turn);
            t.threadHalo = HueTurn(t.threadHalo, turn);
            // the vines across its way dark brown-green stems (pale, they read as bright ropes over it, review k14); its
            // roots down the walls too (in the vines' muted grey, a pale beaded root stood before the capsule grove, k20)
            t.vineDarken = 0.95f;   // (0.85 until k32: pale grey forked twigs and arches, k32 content review)
            t.rootColour = new Color(0.06f, 0.07f, 0.035f);
            t.rootWidth = 0f;   // (none: dark or not, they read as a row of beads, a string of stones, k26; as in the granary and the kiln)
            // its vines' tips not curled into hooks and rings, their glints fewer (grey-white curls filled the game's view
            // and hid the capsule groves, review k23); its walls lighter (near black, k23)
            t.vineCurl = 0f;
            t.vineGlow = 0.5f;
            t.wallGain = 2.3f;   // (1.35 in k24 to k26: still 0.082 overall, its floor 1.4 times the rest, review k26; 2 in k27 to k29, colour review k29 passed it at 0.094, its brightness at the lower bound)
            // (with 11-04's changes, 1.8: its slopes within 9 units of the ant, its walls' splats, which no distance reaches,
            // were left the most vivid of its pictures once its far off was calm, 44-53% of its way's end over C 0.08, colour
            // review of k38; high in the frame, they read as its far view, as in the user's screenshot of 02:15)
            if (rich) t.wallGain = 1.8f;
            // its small mushrooms' spots smaller and crisper (soft and large, review k14)
            t.bio = new System.Collections.Generic.Dictionary<string, Vector4> { { "tiny_mushrooms", new Vector4(1, 0.08f, 0.3f, 1.2f) } };
            t.things = System.Array.FindAll(t.things, th => th.kind != "DewDrop");
            // its moss cushions dark with lit tips, and its small mushrooms' caps their body's olive brown (the user's OK,
            // 10-27, for switches that act in this nest only); less dust in its air, a little green ("像下雪")
            t.darkCushions = true;
            t.plainTips = new[] { "tiny_mushrooms" };
            t.dustGain = 0.6f;
            t.dustTint = new Color(0.62f, 0.80f, 0.62f, 0.5f);
            // its own Tripo plants (the user's pick, 10-27: 02_Concepts/Moss_v1, liverwort b, lichen b, capsules a), along
            // the way and as its landmarks beside it (LabNest, objects "moss"); their markings at their growing ends,
            // following their growth maps: the parasols' ray tips and the thallus's notches, the cups' toothed rims and
            // the scarlet caps, the capsules at their mouths. Fewer of the drawn moss stalks: the capsule groves take over
            t.labPlants = new[] { "liverwort_parasols", "pixie_cup_lichen", "moss_capsules" };
            t.objects = "moss";
            // (review k12: the parasols ringed in saturated green, the cups a white pillar: their markings smaller and
            // weaker, near white with its green, not the palette's deep glow; the capsules unlit: their mouths' rings wider)
            t.bio["liverwort_parasols"] = new Vector4(5, 0.04f, 0, 1.3f);   // (at 0.03 and 0.7, review k14: no marks to be seen; at 0.045 and 0.9, k20 and k23: faint, an ordinary plant)
            t.bio["pixie_cup_lichen"] = new Vector4(5, 0.026f, 0, 0.9f);   // (at 0.75, k20 and k23: no lit ring at the cups' rims; at 1.15, k26 and k27: a pale green halo beside the ant)
            t.bio["moss_capsules"] = new Vector4(5, 0.05f, 0, 0.6f);   // (at 0.08, k13: whole white heads; at 0.06 and 0.8, k32: white patches halfway up their stalks)
            t.markColor = new Color(0.84f, 1f, 0.86f, 1f);
            // their own light over their bodies less (the lichen's pale cups lit whole by it still read as a white pillar,
            // k13), as the granary's and the garden's
            t.fineLight = 0.12f;
            t.faintGain = 0.5f;
            t.things = System.Array.ConvertAll(t.things, th => th.kind == "Sporophyte" ? new Thing { kind = th.kind, weight = 9 } : th);   // (12 until k29: its bunches the loudest colour of its pictures and in front of the parasols, k29 content review)
            // the fan trees at its walls dark as well, their caps grey-green (they keep part of the blue forest's colour:
            // one at a wall stood bright pale blue over the capsule grove); the lichen's pale cups and the parasols darker,
            // so an antenna's light or the ant walking past does not turn them into a white pillar and a green-ringed
            // glare (an antenna brightens a thing by its own lightness); the lichen's own colours fuller, its caps scarlet
            // (at the palette's tint they read pale pink)
            t.kindSat = new System.Collections.Generic.Dictionary<string, float> { { "fan_tree", 0.15f }, { "pixie_cup_lichen", 1.8f } };
            t.kindGain = new System.Collections.Generic.Dictionary<string, float> { { "fan_tree", 0.45f }, { "pixie_cup_lichen", 0.2f }, { "liverwort_parasols", 0.45f }, { "boulder_moss", 0.75f }, { "moss_capsules", 0.55f } };   // (the cups 0.35, the parasols 0.55: their bodies lit whole beside the ant, k23; the cups 0.27 still the brightest thing beside the ant, and one capsule grove pale all over, k29; at 0.75 still a pale column, k30)
            // the lichen's foot dark in the floor's shade (its foot lit pale, review k14), and its scarlet caps glowing in
            // their own red, the cups not (darkened with the cups, they were dark brick dots): its red parts its glow part
            t.kindFoot = new System.Collections.Generic.Dictionary<string, float> { { "pixie_cup_lichen", 0.45f } };
            t.glowParts = new[] { new GlowPart { plant = "pixie_cup_lichen", from = 0f, to = 1.1f, lumMin = 9f, amount = 2.2f, red = 0.35f } };   // (1.1 with its body at 0.27, k26: dark red caps, unlit; 1.8, k29: pink; its glow follows its body's darkening: 1.8 with 0.2, k32, dark red dots)
            // (its trunks dark shapes: -mossrich above; and its capsule groves' ochre calmer, the user, 11-04: "黄色的饱和度有点
            // 太高了，稍微降一些", as the capsule stalks', NestThingsThemed, of which fewer)
            if (rich)
            {
                t.kindSat["trunk_night"] = 0.3f;
                t.kindGain["trunk_night"] = 0.4f;   // (0.5 and 0.35, k35)
                t.kindSat["moss_capsules"] = 0.7f;
                t.things = System.Array.ConvertAll(t.things, th => th.kind == "Sporophyte" ? new Thing { kind = th.kind, weight = 6 } : th);
            }
            return t;
        }
        static readonly NestTheme GreenBefore = GreenBasic();   // (checks: its first version's colours, -palette before)
        static NestTheme GreenBasic() => Basic("green", 120, "绿", "Green", "苔藓森林", "The Moss Forest",
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



        /// <summary>
        /// Orange, 30°: the warm amber nest (02_Concepts/Particle_Styles_v1/amber_*_v2.png). Red-brown clay walls and
        /// floor; ochre, terracotta, umber and bone bodies with a muted sage now and then; amber-gold, honey and ember
        /// glows (a dusty teal the rare contrast); a warm brown night, gold light. Its colours as they were hand tuned,
        /// before the palette recipe (OrangeBefore, -palette before); its content (plants, things, heart) is the recipe
        /// nest's too (Amber).
        /// </summary>
        static NestTheme OrangeHand() => new NestTheme
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
        // the amber nest: v15's palette turned to its segment (NestPalette), tuned here (2026-10-27). Its hue 12° toward
        // amber: the kiln's red moved 12° toward orange (KilnRecipe), so at 0 the two were 16° apart and the honey granary
        // 43°; at 12 it sits halfway between them. Its family spreads toward gold (orange, amber, gold; toward red it
        // would meet the kiln), a little over half as far as v15's (further, its sage plants turned the granary's gold).
        // Its ground lifted by the warm-world knobs, which count its hue 0.4 warm: lift 0.06, its points 1.2 as bright
        // (first review: a family of 10°, "like an old sepia photo": spread a little wider, half as many accents again,
        // the cool ones v15's coral, straw and violet turn into: blue, teal, violet. Its hue kept: the kiln since moved
        // to 47° measured, the granary 80°, the amber nest 64° between them)
        // (still 10-11° after that and its floor's patches: its plants' tints wander further in hue, and twice v15's
        // accents; they keep a little less of their own colours below, the palette's family more)
        // (fifth review: half way between the kiln and the granary by hue, it looked much nearer the granary, the kiln
        // standing apart by its darkness: 4° back toward red; its base a little less vivid (its rust walls); more of its
        // cool accents; and no further toward gold, the granary's way)
        public static readonly NestPalette.Recipe AmberRecipe = new NestPalette.Recipe { spread = 1.4f, spreadSide = 1f, exposure = 1.3f, midChroma = 1.25f, highChroma = 1.6f, hueNudge = 24f, groundLift = 0.07f, groundGain = 0.38f, accentShare = 1f, jitter = 2f, baseChroma = 0.16f, accentChroma = 1.2f, warmth = 2f, skyGain = 0.75f };   // (baseChroma 0.19 to k25: of three oranges still the most earthen, its sand and cliffs too brown, k25 colour review)   // (accents at 2.5: whole lanterns glowing a cold cyan-white, a glow's accent taking any plant however big; seventh review: its base still vivid, its accents grey; ninth: the granary moved on to 91°, and at +8 the nest read as the kiln's, its floor and walls the same rust clay: back to +12, its base calmer, its family a little wider; its exposure up a fifth of a stop, from 0.078 back clear of 0.076, the leafcut garden's "too dark"; thirteenth, on its own ground: still earthy, an old sepia photo, its highlights near white, its floor on the kiln's hue in the game view: +16, highlights more vivid, its family wider, its base calmer; sixteenth: still flat, its three layers alike in chroma, its floor by the way in still the kiln's: its highlights 1.6 times as vivid, its base calmer and lower so its light reads as light, a tenth of a stop up (a fifth: its floor bright and dyed in 2.3% of it), its family wider again; twentieth: still a brown old photo, its middle layer as dull as its base, a fifth of its plants near grey, no cool note at all, its floor by the way in near the kiln's: its white balance half as warm (the kiln's, its own and the granary's all leaned warm to the cap alike), its middle layer a quarter more vivid, half as many accents again, its family wider, 4° on toward gold; its highlight chroma and ground lift left as they were: at orange the gamut clips a pale colour's chroma, and the lift is times warm, 0.12 here; and a fifth of a stop up: its white balance cooler, its picture fell from 0.100 to 0.080, k21; its ground's points a little less bright: at 0.5 with that stop 2.7% of its floor bright and dyed, k22; twenty-third: still a brown photo, its base too vivid (C 0.032) for its middle to rise off it, no cool note, its sky a grey-brown fog: its sky darker, its base calmer, its family wider, 4° on toward gold; and its accents back to v15's, which the palette deals to plants as well, its cool notes on stones alone, dark and small (the user, 10-29: every plant leans to its world's colour, cool accents only on what is not alive); its sky at 0.75, not the review's 0.6: at 0.6 its picture fell to 0.077, its walls' gain and textures did not lift it, k24c-k24e; twenty-fourth: its base still 0.033, its near sand's: its base a quarter calmer again)
        static NestTheme amberTrial;
        static readonly NestTheme OrangeBefore = OrangeHand();
        public static readonly NestTheme Orange = Amber(AmberRecipe);

        /// <summary>The amber nest in a recipe's colours: its own plants and things (OrangeHand), no pink petals or
        /// clover, its light gates half as bright (one stands right behind its heart and outshone it).</summary>
        /// <summary>A colour turned this many degrees round the HSV wheel, its alpha kept.</summary>
        static Color HueTurn(Color c, float degrees)
        {
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            Color o = Color.HSVToRGB(Mathf.Repeat(h + degrees / 360f, 1f), sat, v, true);
            o.a = c.a;
            return o;
        }

        static NestTheme Amber(NestPalette.Recipe r)
        {
            NestTheme t = Without(NestPalette.Apply(OrangeHand(), NestPalette.For(1, r)), "Petal", "Blossom", "Clover");
            t.gateGain = 0.5f;
            // none of its plants glowing in an accent: v15's accent glows turned with it, its violet to a teal green, and
            // the six in a hundred plants that drew one glowed bright green where they glow, a dry grass tuft's seed heads, a
            // leaf (k27b-k27d: 1000 plants in it, not 200, the k27b reviews' "four bright green clusters"; tints are held in its
            // band, glows were not). Its cool notes are on what is not alive (the user, 10-29)
            t.glowAccent = 0f;
            // its own things in amber's and resin's natural forms (the first review: the gold ball-stalks read as flowers
            // and were everywhere; the amber prisms as gold crystal, jewels; the resin drops as a white glowing mound; the
            // wax comb, bees' comb brimming with honey, is the granary's world): resin running from a wound in fallen bark,
            // old amber weathering out of the floor, broken open, lit from within. Its featured thing a boulder of that
            // amber beside the way (no heart: "不要用心脏的概念了"). (Fifth review: the amber read at once, the resin runs
            // less, as yellow jelly fungus on a branch, so fewer of them; the ball-stalks, few as they were, still read as
            // flowers: left out)
            // (and the conifers' needles, in pairs, lying by its way: its ground texture's needle ends read as dark smudges
            // from the game's view, as gold confetti close by, k20; more of its floor's things its own, so its amber and
            // resin keep their share)
            t.things = new[]
            {
                new Thing { kind = "AmberNodules", weight = 30 }, new Thing { kind = "ResinRun", weight = 14 }, new Thing { kind = "PineNeedles", weight = 30 },
                // (and slabs of slate in its sand, a cool blue grey: its few cool notes, on stone, dark and small, to open its
                // family toward 18° (the user, 11-02: "hold 18°"; at 12° with none to be measured, k24; the kiln's slabs, dark
                // for its red floor, a neutral grey here, k25)
                // (k26: three to five in the whole nest, none on its way, dark and grey: at 40 they were "blue tiles,
                // cushions", the most striking thing in every view, k25 content review; dark, they are never counted in its
                // family, which counts only the lighter half of a view: its family is its plants')
                new Thing { kind = "SlateSlabs", weight = 2 },   // (18 in k25f: 0.3% of its views cool, a quarter of what 18° needs; 40, 0.5%, 16°, k25g; at 55 and 75 no more of them fit its floor, its nodules and needles fewer, k25h-k25i)
            };
            t.ownShare = 0.68f;   // (k27: a little more of the floor's own litter between its nodules and needles, its twigs, leaves, seeds and lichen; 0.75 to k26)   // (0.6 to k25: with its slate slabs among them, their share of the floor's things capped by its floor, k25h-k25i)
            t.heart = null;
            // amber nodules lining its way too, close enough to touch (seventh review: from the way in and half way there
            // was hardly any amber to see)
            t.wayThing = "AmberNodules,PineNeedles*1.6";   // (k26: its needles by the way's lights, where they can be read from the game's view, k25 content review, at their floor size (a way thing is drawn at half); to k25 slate slabs by turns, two to a nodule: scattered at random they fell in its views by chance, its family 13-17°, k25f-k25l; one to a nodule, 15°, k25m)
            t.wayEvery = 3.2f;   // (at 7 the way in and half way still showed hardly any; 5 to k25m; 4 to k27g: the foreground floor in all three of its game views still bare sand, k27g content review)
            // its landmarks now its own Tripo models (the user's pick, 10-27: 02_Concepts/Amber_v1_候选 A, B, C): a fallen
            // pine cone beside the way, giant to an ant, beads and tears of resin on its scales (LabNest.PlaceObjects; its
            // props lit only where they glow); little frankincense trees in its junction chambers and along the way, tears of
            // resin in rows down their bark; gumweed in clumps by the way, a bead of milky resin in each bud, in place of the
            // paper lanterns. (The amber boulder they replace read as a copper bowl, an egg, in the thirteenth review; the
            // junction's giant dry mushroom, its stalk a cream-white glow, in every review.)
            t.objects = "amber";
            // (its cones in their own colours, lit a little more: lit as buildings, only where they glow, a building's few
            // random embers flecked them white, "a shrub with snow on it")
            t.rigidGain = 1.5f;   // (at 1, in the night, its brown read as a dark mound of earth)
            t.junctionTree = "tear_tree";
            t.junctionTreeHeight = new Vector2(4.2f, 5.2f);   // (at the rule's 9-13 its points were coarse, a dark mass: no trunk, no tears; at 5-6.5 the game's view saw only its trunk)
            // and beyond its walls only the dark trunks of the conifers its resin comes from (its biome's giants, shared with
            // the world outside the lab, were a third dry mushrooms: grey-blue caps by the way in, one filling the view half
            // way there, sixteenth and twentieth reviews; the user's word, 10-29)
            t.surroundings = "trunk_night";
            t.surroundingsTall = 1.7f;   // (at 26-48, from the way their flat hollow tops showed, "chimneys, stumps", twenty-third review)
            // its markings in amber, as the granary's are in gold (in the palette's pale glows they read white: the gumweed's
            // buds as white puffs, cotton; and the gumweed keeping more of its own yellow)
            t.markColor = new Color(1.0f, 0.70f, 0.34f, 1f);
            // its plants' faint light in their palest parts weaker: the thorny succulents' pale blotches lit gold all over
            // them, however muted and darkened, k24-k24h (their markings and glowing parts as they were)
            t.faintGain = 0.5f;
            // (and the green seedling muted: its own green stood out of the amber, sixteenth review; the fallen leaves, muted
            // too, still flat pale green cards on the floor in the twentieth: left out, below)
            // (k26: its little trees and gumweed in their own colours now, no longer turned as a blue plant is (GameConfig
            // nativePlants): the trees' orange bark a little muted, the gumweed as it is)
            t.kindSat = new System.Collections.Generic.Dictionary<string, float> { { "seed_husk", 0.4f }, { "thorn_succulent", 0.1f }, { "tear_tree", 0.85f } };   // (k24: its little trees' bark a pinkish grey, grey-purple on its amber floor; the gumweed's heads near white, lilac against its walls; a green arching root by a junction)   // (the succulents' gold specks their texture's own: with no glow part they stayed, k24; at 0.55 still gold blotches, k24c)
            // its plants' tints from the palette folded into its own warm band, as their own colours are (ownBand): the
            // gumweed's smaller tuft members took a lilac (the user, 10-29: every plant leans to its world's colour)
            // (k26: and both kept between amber and bronze: native, in its own colours, the gumweed read an olive green
            // against its walls, OKLCH 85-115, the little trees' leaf tufts a dark olive. Its own colours folded at 28-36
            // changed little; its tints gave it, its palette's family spread on toward olive and kept to 66 (38 ± 28 to
            // k26). Tried with -ambertint and -amberband)
            float ArgOr(string arg, float otherwise) => Game.Arg(arg) is string a && float.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : otherwise;
            float tintTop = ArgOr("-ambertint", 34f), bandTop = ArgOr("-amberband", 32f);
            t.tintBand = new Vector2((10f + tintTop) * 0.5f, (tintTop - 10f) * 0.5f);
            t.ownBand = new Vector2((6f + bandTop) * 0.5f, (bandTop - 6f) * 0.5f);
            // (k27e: and its glows held in that band as its tints are: v15's glows turned with it spread on past olive into a
            // green, as its tints had, and a little tree's leaf canopy near the camera, glowing faintly in its palest parts,
            // read lilac grey in an accent's blue, a bright green parasol in its own kind's glow, k27c-k27e. Its plants
            // glow amber to gold; its threads and lamps take theirs from their own colours)
            for (int i = 0; i < t.glow.Length; i++)
            {
                Color.RGBToHSV(t.glow[i].linear, out float gh, out float gs, out float gv);
                float d = Mathf.DeltaAngle(t.tintBand.x, gh * 360f);
                if (Mathf.Abs(d) <= t.tintBand.y || gs < 0.08f) continue;
                Color folded = Color.HSVToRGB(Mathf.Repeat((t.tintBand.x + Mathf.Sign(d) * t.tintBand.y) / 360f, 1f), gs, gv, true).gamma;
                folded.a = t.glow[i].a;
                t.glow[i] = folded;
            }
            // (its thorny succulents a little darker, and its pebble clusters darker and fewer: one took the palette's cool
            // accent pale, a lilac striped stone, "an Easter egg", the heaps of them clutter by its way in, twenty-third review)
            t.kindGain = new System.Collections.Generic.Dictionary<string, float> { { "thorn_succulent", 0.6f }, { "pebble_cluster", 0.6f }, { "gumweed", 0.7f }, { "tear_tree", 0.7f } };   // (k26: its trees darker than its floor, so their tears show, k25 plant review)   // (the gumweed's pale heads the brightest thing in view, k24)
            t.sparserKinds = new[] { "pebble_cluster", "gumweed", "tear_tree", "thorn_succulent", "dry_grass_tuft", "fallen_leaf" };   // (k27: its own plants and succulents denser, its grass, pebbles and leaves back: below. In k26 it grew 218 plants inside its walls, the blue forest about 1350; 804 in k27, its floor by the way still bare sand: there its pebbles denser, k27b, 969; its way in and its way's end still bare close by, k27b reviews: its gumweed and trees denser again, k27c, 1099; 1063 in k27g, its way in, half way's right and its gate still bare close by, k27g reviews: grass and gumweed denser, fallen leaves on its floor)
            t.sparserBy = new[] { 0.9f, 0.5f, 0.55f, 0.8f, 0.6f, 1f };
            t.plainTips = new[] { "tear_tree", "gumweed" };   // (their tips taking the palette's next colour, the cool accent: lavender gumweed, a violet-grey tree)
            t.labPlants = new[] { "tear_tree", "gumweed" };
            // the amber resin plant left out to k26: under this palette every plant starts from one pale colour, and its lumps
            // read as pale pink blobs, not resin (the resin running from bark and the amber nodules are its resin now; back,
            // few, in k27: lemon-gold fingers, a yellow coral, the brightest thing half way; out again, k27b)
            // and the sponge fungus garden left out too: it is the leafcutters' (fifth review); and the snail shells, which
            // lying mouth up read as clay pots
            t.scatterPlants = System.Array.FindAll(t.scatterPlants, k => k != "amber_resin" && k != "fungus_garden" && k != "dry_mushroom" && k != "seed_pile" && k != "root_arch" && k != "seed_husk");   // (k26: and the arching root, still green whatever its saturation, k25)   // (and the dry mushrooms: its little trees in their place; the seedlings, still a lime clump by its way in, twenty-third; the seed heaps, pale lumps by it, "popcorn", k24; the dry grass, a few curled blades, no body, and the pebble heaps, a pale stone spotted black, "camouflage", twenty-fourth review)
            // (k27, more in it (the user, 11-04, through the hub: "除了蓝色以外，这几个场景还是有些太空了。可以增加一些大的素材也好、
            // 小的素材也好", "丰富度…还没有V15那个高"): the floor beside its way bare sand where v15's is lined with plants of
            // twenty kinds; it had kept three of the world's. Back, dark and muted: its resin plant, seed husks, dry grass, pebble
            // clusters and arching roots, and the world's mossy boulders, big stones off its way. Their tints now held in its
            // warm band (tintBand: the arching root's green, k25, was a tint the band let through to 66); the root's and the
            // boulders' moss, turned as a blue plant's is, toward pink, muted near grey so its tint colours them)
            // (k27b: the roots at 0.15 and 0.7 pale grey-beige curls, bleached; the pebbles at 0.6 a pale grey heap by the way
            // in: both darker. Its fallen leaves back too, dark: pale blue-white in their own texture, "flat pale green
            // cards" in the twentieth review, when its tints reached 66)
            // (k27c, the k27b reviews: the arching root still a bright green tube, its bark turned as a blue plant's is (its
            // green pixels 25 times k26's), and dark a "fire salamander", gold specks all over: out again. The boulders a
            // black sphere and the pebbles smooth ovals, gold-spotted, "eggs", "leopard print": half sunk, no markings, a
            // little warmer; the succulents charcoal at 0.1: warmer; the gumweed olive-mustard in shade: lighter)
            // (k27d: its green pixels as many in k27c without the root, bright green blades and curls by its way: its fallen
            // leaves out again, flat cards as in the twentieth review. The green was its glows', k27e-k27f: above, where they
            // are held in its band now)
            // (k27g: its seed husks out: one by its way's end a parasol glowing whole in its pale inside, lilac grey, then
            // green, then gold as its glows were changed, k27c-k27f; a dead husk does not glow, and four in the nest)
            // (k27h, the k27g reviews: its fallen leaves back on its bare floor, dark, no markings, now its glows are its
            // own; its boulders and pebbles a little lighter, unseen dark lumps; its little trees a cream lump, L 0.70: darker)
            t.scatterPlants = new System.Collections.Generic.List<string>(t.scatterPlants) { "boulder_moss" }.ToArray();
            foreach (var (kind, sat, gain) in new[] { ("boulder_moss", 0.45f, 0.65f), ("pebble_cluster", 0.3f, 0.45f), ("thorn_succulent", 0.35f, 0.6f),
                ("dry_grass_tuft", 0.6f, 0.75f), ("fallen_leaf", 0.3f, 0.35f) })
            {
                t.kindSat[kind] = sat;
                t.kindGain[kind] = gain;
            }
            t.kindGain["gumweed"] = 0.8f;
            t.kindGain["tear_tree"] = 0.55f;
            t.sunkKinds = new[] { "boulder_moss", "pebble_cluster" };
            t.sunkBy = new[] { 0.35f, 0.4f };
            // (no moss cushions either: by its way out one lay lemon green, twice as light as the floor, "from another nest",
            // twentieth review)
            t.noKinds = new[] { "Petal", "Blossom", "Clover", "Shell", "Moss", "Seedling", "Mushrooms", "Crystals", "Feather" };   // (and crystal grit: pale crumbs by its way in, k24c; amber is no crystal; and feathers, a pale cyan-white flake by it, k24h)   // (the floor's seedlings too: a stem and one flat leaf, no body, twenty-third plant review; and its little mushrooms, bone white with mint spots, one nearly two ants tall by its way in, k24)
            // its fallen twigs, leaves and fern fronds dry browns, its few seedlings a dark olive, its lichen a muted grey-green
            // (the palette's greens made its seedlings lemon green, twentieth review, and lay lime patches on its banks,
            // twenty-first and second)
            t.thingColours = new System.Collections.Generic.Dictionary<string, Color>
                { { "Twig", new Color(0.066f, 0.133f, 0.233f) },   // (old fallen wood weathered a silver blue grey, its cool notes with its stones, OKLCH 0.50/0.06/250: at 0.026 chroma the nest's warm light left them grey, k25)
                   { "Leaf", new Color(0.13f, 0.07f, 0.026f) }, { "Fern", new Color(0.15f, 0.075f, 0.028f) }, { "Seedling", new Color(0.07f, 0.075f, 0.02f) },
                  { "Lichen", new Color(0.09f, 0.075f, 0.035f) },   // (a dark warm olive: grey-green, it read pale cyan by its way in, k23-k24)
                  // and its floor's stones a dark slate blue, its few cool notes (in the palette's bone, pale lumps by its way
                  // in, "popcorn", twenty-third review), its seeds a dry tan
                  // (lighter and bluer since, OKLCH 0.54/0.075/245: at 0.045-0.075, under the middle layer's lightness, k24; at
                  // 0.12-0.19, chroma 0.027, grey on the screen, no cool pixel, k25)
                  // (k26: dark and grey, as its slate slabs: OKLCH 0.30/0.024/245, never counted in its family)
                  { "Pebbles", new Color(0.0175f, 0.0287f, 0.0412f) }, { "Seed", new Color(0.14f, 0.09f, 0.045f) } };
            // and no roots at its walls' feet: in the palette's grey-green they lay across its floor jointed and thick, "a
            // caterpillar" (twentieth review, which took them for twigs); dark brown and thinner, the granary's still read as
            // a worm
            t.rootWidth = 0f;
            // the few glowing grains in its floor fainter (gold flecks all over it, "confetti", twentieth review)
            t.grainGain = 0.4f;
            // the motes in its air fewer to the eye, smaller, dimmer and in its own gold ("like snow" in the fifth review)
            // (the dew beads on its leaves and fronds a dim resin: cream and lit, two or three white dots in a cluster by its
            // way in, k24)
            t.dew = new Color(0.18f, 0.08f, 0.025f);
            // (its drifting dust a little cool: light, not alive, one of its cool notes)
            t.dustTint = new Color(0.6f, 0.68f, 0.82f, 0.6f);
            t.moteSize = 0.3f;   // (and smaller and dimmer again: in threes and fours over its walls, "white paper discs", twenty-third review; at 0.45 and 0.35 white balls in threes and fours by its way in, "pearls, popcorn", a mote for each of a gumweed tuft's members, k25)
            t.moteGain = 0f;   // (k26: none: at 0.15 and 0.3 still white balls in threes by its way in and over its walls, k26p)
            t.microGain = 0.35f;   // (k26: its midges' swarms and seed fluff, in the palette's pale colours, were the white balls in threes and fours by its way in, "pearls, popcorn", and snow in the ant's eye; the motes above did not take them)   // (k27: at 0.5 pale blue-white balls in threes again: a midge's body was half v15's cyan in MicroLife.shader whatever its palette; its own since 11-04, midgeTint below. At 0.5 with it its seed fluff big and white, k27b reviews: back to 0.35)
            // (k27: and its small life in its own warm darks, not the palette's pale colours: dark gold, bronze, rust, resin;
            // the user, 11-04, "可以增加…小的素材", small life among them; its midges dark amber specks, their wings bronze)
            t.microLife = new[]
            {
                new Color(0.62f, 0.40f, 0.20f), new Color(0.55f, 0.36f, 0.20f), new Color(0.70f, 0.48f, 0.24f), new Color(0.48f, 0.30f, 0.16f),
                new Color(0.66f, 0.44f, 0.18f), new Color(0.52f, 0.34f, 0.22f), new Color(0.58f, 0.42f, 0.26f), new Color(0.64f, 0.38f, 0.22f),
            };
            t.midgeTint = new Color(0.30f, 0.14f, 0.04f, 0.5f);   // (raw, linear, as the shader's: v15's 0.5, 0.85, 0.85)
            t.midgeWing = new Color(0.16f, 0.10f, 0.05f);
            t.motes = Color.Lerp(t.motes, t.glow[1] * 1.3f, 0.5f) * 0.7f;
            // what is right round the ant lit less, and what it walks past (with v15's lift the pale stones and seeds by
            // the ant read as white blots in this lighter, warmer nest)
            t.nearLift = 0.45f;
            t.walkGain = 0.6f;
            // its creeping vines dark brown with brown leaves, their pores lit less (in the palette's grey-green, one lying
            // on its floor with white dots along it read as "a green caterpillar", twenty-third review)
            // (k26: wholly its dark brown, its pores and vein in amber: at 0.85 a sixth of the palette's green stem was left,
            // its veins and pores in the palette's cool glows, "a green vine" by the gate, k25)
            t.vineDarken = 1f;
            t.vineLight = new Color(1.0f, 0.62f, 0.26f, 1f);
            t.vineDark = new Color(0.035f, 0.022f, 0.01f);
            t.vineLeaf = new Color(0.13f, 0.085f, 0.035f);
            t.vineGlow = 0.3f;   // (0.4 to k25)
            // its plants' own markings (the glow references: light in parts, the bodies dark): the dry mushrooms lit only
            // in their gills (glowParts; their spots, the other nests', speckled cap and stem alike); the lanterns lit
            // through their paper, the ribs dark (their pale panels: at a sixth of them they stayed cold grey-white, at a
            // few specks they read as nothing)
            t.bio = new System.Collections.Generic.Dictionary<string, Vector4>
                { { "dry_mushroom", Vector4.zero }, { "tear_tree", new Vector4(4, 0.014f, 0f, 0.7f) }, { "gumweed", new Vector4(4, 0.015f, 0f, 1f) },   // (k27h: 0.85, still bright gold leaves on dark crowns near the camera, 223 small bright specks on dark bodies against v15's 165, k27g colour review)   // (k26: at 0.03 big beads under the leaf tufts; the gumweed's whole head a yellow cap from above, k25 plant review)   // (k27c: the trees' tears at 0.02, more trees now and nearer, big gold discs before the camera, "a heap of coins", k27b reviews)
                  { "boulder_moss", Vector4.zero }, { "pebble_cluster", Vector4.zero }, { "seed_husk", Vector4.zero }, { "fallen_leaf", Vector4.zero },   // (k27c: stones and dead husks with no markings: the kinds' own gold spots made its boulders and pebbles "leopard print", "eggs")
                  { "thorn_succulent", new Vector4(8, 0.012f, 0f, 0.3f) },   // (k27d: at 1, three times as many of them now, "gold specks all over", k27b reviews; at 0.6 a dark disc with gold rings, "leopard print", k27g)   // (0.025 to k25: still gold blotches, k25 plant review) { "dry_grass_tuft", new Vector4(8, 0.05f, 0f, 1f) },   // (the succulents' spines are tips all over its leaves: at 0.06 gold blotches across them, k24)
                  { "resin_cone", new Vector4(4, 0.015f, 0f, 1.2f) } };
            // (its cones' beads of resin, their palest 1.5%, lit as its little trees' tears are, the cone otherwise in its own
            // brown, rigid (ParticleLook: a relic its nest marks): "no bead of resin to be seen on it, no light of its own",
            // twenty-third review; its dry grass marked at its seed heads' tips, which had none of its own)
            // (its little trees' tears marked in amber in their palest 3%, as the gumweed's beads are: its texture's tears are
            // cream, as pale as its peeling bark, no orange in them, so no glow part could pick them out, and a glowing part
            // keeps a finely made plant's own colour, its pale bark lit pinkish white, k22; its leaf tufts' faint tips gone)
            // (the lanterns since left out: its little trees' leaf tufts at their tips, faintly; the gumweed in its palest,
            // the beads of resin in its buds; the thorny succulents' spine tips, softly, without the specks behind them that
            // read as a few white dots, twentieth review: its gold flecks, fewer, no longer read at all)
            // its floor not one orange (a family of 10°, "like an old sepia photo"): its patches swinging further in hue,
            // and patches of other materials, dark earth soaked with resin and a cool blue-grey (grey-green lichen leaned
            // to the granary's olive)
            t.floorVary = 28f;   // (at 34 a grey rose-brown by the way out, the kiln's side, twenty-third colour review; "dark pink" in the game's view, content)
            t.floorPatchA = new Color(0.14f, 0.10f, 0.06f, 0.9f);   // (deep amber-brown: rust read as the kiln's)
            t.floorPatchB = Color.clear;   // (none: its cool blue-grey, mixed with its sand's orange, came out a mauve, a pinkish floor by the way, 1-3.5% of its game views, and never a cool; bluer, a pink floor toward the kiln's hues, k25b-k25f)
            t.plantKeep = new Vector2(0.38f, 0.68f);   // (v15's 0.5-0.8: its plants' own textures all orange-brown)
            // its own ground textures (v12, amber2: packed sandy earth under resinous conifers; the walls in bands of tan,
            // ochre and umber, needle ends and roots in them): on its biome's set, the kiln's clay too, it read as the kiln
            // (ninth review). On its own, the ground keeps v15's share of their colour again (on the kiln's clay, half of it:
            // a third of each ground point was that rust)
            t.groundSet = "amber2";
            // its floor by the ant keeps more of its own sandy ground (its picture, rid of the pale mushroom, seed heaps and
            // crystal grit that had lit it, fell to 0.077, k24c-k24f; its walls' gain, textures and sky did not lift it)
            t.groundTexMix = 0.37f;   // (at 0.45, 0.087 but 6% of its floor bright and dyed, k24g)
            // its far walls and plateaus calmer than its floor by the ant (the user, 10-29, on the granary: "近处可以饱和度高，
            // 远处低"): its brown photo look and its base's chroma were most its terrain's own browns, which its palette's
            // base does not reach, k23-k24 colour reviews
            t.groundSatByDistance = new Vector4(1f, 0.6f, 6f, 22f);
            // and its walls and plateaus a little lighter in their textures (the environment lit, not the exposure, shared rule
            // 10-29): with its sky darker and its far terrain calmer its picture fell to 0.077, k24c
            t.bankTexTint = new Color(t.bankTexTint.r * 1.2f, t.bankTexTint.g * 1.2f, t.bankTexTint.b * 1.2f, t.bankTexTint.a);
            t.wallGain = 1.2f;   // (and the walls round the ant: the textures' tint alone left it at 0.077, k24d)
            // its far cliffs' tint 6° on toward amber: the kiln's same layered cliffs, as light, only 15-20° from its own, k24
            t.bankTexTint = HueTurn(t.bankTexTint, 6f);
            t.plateauTexTint = HueTurn(t.plateauTexTint, 6f);
            t.plateauTexTint = new Color(t.plateauTexTint.r * 1.2f, t.plateauTexTint.g * 1.2f, t.plateauTexTint.b * 1.2f, t.plateauTexTint.a);
            t.glowParts = new[]
            {
                new GlowPart { plant = "dry_mushroom", from = 0.62f, to = 0.84f, lumMin = 1.0f, amount = 0.5f },
                // (no glow part for the thorny succulents: at 1.5 and 0.25 its leaves still all gold specks, twenty-third review,
                // at 1.1 and 0.45 sequins; its spine tips marked instead, bio)
                new GlowPart { plant = "dry_grass_tuft", from = 0.65f, to = 1.05f, lumMin = 1.0f, amount = 0.25f },   // (k27: back in it, its seed heads lit less; 0.4 when it was last here, k24)
                // its own: the beads in the gumweed's buds, by how much lighter they are than the rest. (The tears of resin on
                // the little trees are marked instead, bio: by how orange they were, at lumMin 9, over the 3.2 a texel can
                // reach, they never lit; lit by hot, its brown bark, as red as tears in linear light, glowed pale pink all
                // over, k21; by its pale warm parts, its bark's pale ridges too, pinkish white, k22)
                new GlowPart { plant = "gumweed", from = 0.55f, to = 1.05f, lumMin = 2.2f, amount = 0.3f },   // (1.7 to k25)   // (at 1.35 and 0.5, and its markings 8% of it, its whole head lit, no bead to be read, k24)
            };
            // the ants in amber and honey, as v15's are in its own teal (leaning to the palette's turned colour, the
            // ants' own teal still showed through: grey-blue ants in a warm nest)
            t.antTint = new Color(0.95f, 0.62f, 0.30f, 0.85f);
            return t;
        }

        /// <summary>
        /// Red, 0°: the ancient temple, its kiln, redesigned 2026-10-27 (the user's pick, "C 火后古木"): a forest floor long
        /// after an old fire, its temple a cluster of charred stumps, burnt logs, cup fungi and morels (below).
        /// Its colours are v15's palette turned to segment 0 (NestPalette), as the granary's and the garden's: its family
        /// spreads only a little toward the fired clay's ochre (further, its bodies took the amber nest's gold), its light
        /// nudged a little from the segment's crimson toward pure red (the user's ~0-8°). (k1: at hueNudge 12 with v15's
        /// highlight chroma its light read pale pink and its picture dull, colourfulness 8.7 against v15's 14, its glows
        /// orange, from its things' own colours, the cups' outsides and the embers: highlights as vivid as v15's measure;
        /// the hue kept, the amber nest's is set halfway between this and the granary's.) Its first, hand-made colours
        /// below are RedBefore's (-palette before).
        /// </summary>
        // (its accents at v15's chroma: at 1.3 its pebbles' lilac read as things from elsewhere, k1 content review; a few
        // more of them, small and cool, to open its family: 8° in k1. Its hue 6, not 12: k1 measured its three layers at
        // 9-12°, 14-16° and 18-19° on the wheel, all on the orange side of the user's 0-8°, k1 colour review. Its family
        // spread 0.75: at 0.55 it measured 8-11° in k3, one colour; toward rust and dull ochre, the burnt wood's and clay's.
        // k3 colour review, of its three hues 6 reads most as red, 0 raspberry, 12 vermilion; its highlights back toward
        // white, 1.3 (its red now in the deepened lights, KilnTuned), its base a little calmer, more of its small accents.
        // k6: its family 12°, still short of 18°: wider, 1.0, toward rust, grey ochre and charcoal; its walls' dark C 0.030,
        // calmer, 0.55. k8: its vines charred near black, its picture a little darker than v15's, 0.030: a tenth of a stop
        // up, 0.7)
        public static readonly NestPalette.Recipe KilnRecipe = new NestPalette.Recipe { spread = 1f, exposure = 0.7f, hueNudge = 6f, highChroma = 1.3f, baseChroma = 0.55f, accentShare = 1.8f };   // (0.8: its dust halved in k9, 0.031; 1.0: its floor redder and darker in k12, 0.028; k15: 0.7, its ground brighter instead (KilnTuned's groundGain; the recipe's groundGain and groundLift act on warm worlds only, yellow to green, nothing here, k15b): the user, 10-29, "我要提亮环境，是指整个环境提亮，而不是把远处的点提亮")
        static NestTheme kilnTrial;
        // (no fresh green on burnt ground, no petals, no snail shells, no pink toadstools, feathers or crystal grit (k2: the
        // floor's small mushrooms took the palette's pale pink), no fallen leaves or seeds (k1 content review: big pink-
        // violet leaves and striped seeds in front read as things from another nest), no lichen (k3: a lilac-grey patch at
        // the way in), no pebbles (heaps of big round stones: lilac-blue and a pale pink one in k6, dark slate boulders in
        // k7c; its own small slate stones instead, AshStones); what it leaves out is left bare (SolidThings); before Red,
        // which reads it)
        static readonly string[] KilnNoKinds = { "Petal", "Blossom", "Clover", "Moss", "Fern", "Seedling", "Shell", "Mushrooms", "Feather", "Crystals", "Leaf", "Seed", "Lichen", "Pebbles" };
        public static readonly NestTheme Red = KilnTuned(NestPalette.Apply(RedBasic(), NestPalette.For(0, KilnRecipe)));

        /// <summary>The kiln after its palette (NestPalette.Apply sets v15's ways): its own plants keep their own colours
        /// (k1: at v15's 0.5-0.8 the red cup fungi turned clay-brown bowls), its vines charred dark (at v15's 0.5 they read
        /// pale pink and took the foreground), its way out's gate seen but not glaring (a pale pink arc filled the way's end),
        /// its charcoal not frosted with white (rigidGain: lit at 1, the charcoal's silver sheen read as white specks). And
        /// its light an ember red: v15's highlights turned to red are pale, and a pale red is pink (k1, k2: "淡粉", its
        /// highlights at chroma 0.035-0.043 against v15's 0.066; the sRGB gamut keeps a light red from going deeper), so the
        /// thread's halo, the beams, the floor's glowing grains, the specks and what glowing parts lean to are made deeper
        /// and fuller, as the references' small glowing parts are; the thread's core and the lamps stay near white.</summary>
        static NestTheme KilnTuned(NestTheme t)
        {
            t.noKinds = KilnNoKinds;
            t.plantKeep = new Vector2(0.75f, 0.92f);
            // its vines charred roots, not living pink tubes (k3 content review): darker, their veins and tips lit less
            // (Apply had given them v15's 1), their leaves charred too; their own dark charred near black (darkened toward
            // the palette's, a pinkish brown, they stayed one, k6-k7: 124/92/79), their tips seldom curled into hooks
            // (k7 content review: loops at the way in and at the way out)
            t.vineDarken = 0.92f;
            t.vineGlow = 0.08f;   // (0.4 to k15: white dots along its charred vines, k12-k15 content reviews; 0.15 in k16, pale pink-white dots still on those before the log's end, k16)
            t.vineDark = new Color(0.04f, 0.03f, 0.027f);
            t.vineLeaf = new Color(0.07f, 0.055f, 0.045f);
            t.vineCurl = 0f;   // (0.15 in k8: one still curled whole against the sky, k8 content review)
            // its plants' own colours kept within 20 ± 28° (16 ± 20° folded the morels' brown, 52°, to a pink, 28°, k3 plant
            // review; the band was for the old kiln's plants, some of which fell to pink)
            t.ownBand = new Vector2(20f, 28f);
            t.gateGain = 0.15f;   // (0.25 in k3, a deep pink-red arc filling the way's end)
            t.rigidGain = 0.7f;
            // its plants' markings toward the deep ember red, not a pale red (k3: the morels' marked caps pink)
            t.markDeep = 0.8f;
            // its ground keeps more of its own textures' colours, the grey of its ash and the black of its charcoal (at
            // v15's 0.3 the palette dyed them one dark red-brown, k1 content review); the motes round its plants and in
            // its air smaller, dimmer, in its light (k1 plant review: white balls near the lens)
            // (0.6 in k3: its ash still a little pink, k3 content review; 0.75 in k6: its walls' own rust red back, C 0.030;
            // 0.65 to k11. k12, the user, 10-29: "这个地面包括环境可以稍微再偏红一些些，现在这个地面有点太灰了…有一点分不清楚":
            // more of its palette's red-brown, its grey ash and what lies on it told apart)
            t.groundTexMix = 0.3f;   // (k14: 0.45 in k12-k13, its floor still a mauve brown, C 0.03 at 17-21°: more of its now redder palette floor)
            // the roots at its walls' feet charred (in the palette's they read pale pink, k6 content review), as dark as its
            // vines and thinner (k8: lighter than the charred vines, unburnt; a thick one across the foreground, twice)
            t.rootColour = new Color(0.035f, 0.026f, 0.022f);
            t.rootWidth = 0f;   // (none: at 0.6 still a thick one across the log's foreground, k9; its charred vines are its roots)
            // its floor in patches of what follows a fire, its family wider (9-10° since k7, the colour reviews' one point
            // left): fire moss, the first thing to come back, a dark wine red, and cold ash, a bluish grey, both about as
            // light as its floor (dark patches read as pits elsewhere); its floor's patches swinging further in hue, as
            // the amber nest's, 34°
            // (vivid as patch colours go: its floor keeps 0.65 of its textures' own, so at C 0.06 and 0.02, k9, they were
            // not seen, its family 10°; mixed in, a wine red and a blue grey)
            t.floorPatchA = new Color(0.43f, 0.2f, 0.19f, 0f);   // (off since k16: red leopard spots on the slopes, k15; its family no wider for them)   // (k14: a wine red, about 3° on the wheel; 0.47, 0.16, 0.23 leaned plum, 346°, the nursery's, k10 colour review)
            t.floorPatchB = new Color(0.25f, 0.405f, 0.56f, 0f);   // (off since k12: its floor to lean red, the user's; its cold ash came out grey anyway, k10)
            t.floorVary = 16f;   // (k16: at 34 its floor's patches swung to 4-17°, a lilac pink under the ant, the nursery's, k13-k15 colour review)
            // its dust a little and grey, not gold (golden-orange specks over its walls, k6-k8: the glowing grains were
            // only a tenth of them)
            t.dustGain = 0.5f;
            t.dustTint = new Color(0.5f, 0.48f, 0.47f, 0.7f);
            t.moteSize = 0.45f;
            t.moteGain = 0.4f;
            // its floor's few glowing grains, on its walls too, dimmer (golden-orange specks over the walls, k6-k7 colour)
            t.grainGain = 0.45f;
            // its ground's halftone softer, 0.6: at 1 its walls' grey-white ash layer was a glitter of pale points over a dark wall,
            // each pale grain three times as bright; now a lit wall with grain on it (the user, 10-29: "再往远处看，这些点太亮了…我要
            // 提亮环境，是指整个环境提亮，而不是把远处的点提亮"; its ground brighter instead, KilnRecipe.groundGain)
            t.groundGrain = 0.35f;   // (0.6 in k15: its lit walls a lit wall, its shaded walls still a glitter, 16-19% pale points)
            // and its ground itself brighter, its surface and its points, instead of its exposure (0.7 from 1.0): the recipe's
            // groundGain lifts warm worlds only, so its own (at 1, with the softer grain, 0.020 against v15's 0.033, k15-k15b)
            t.groundGain = 2f;   // (1.7 to k16)
            // and lit as one (k17): its shaded walls were still a glitter, 18% pale points, and its walls no lighter, the
            // third time (k14-k16 colour and content reviews), as its ground points are unlit, as bright on a wall turned
            // from the moon as on a lit one, and the surface behind them sinks most there (its ambient light by the dim
            // twice: albedo and occlusion); and round the ant its walls' opaque splats, lit from above, were near black on
            // a slope the moon lit, the lit terrain between them flat orange-red patches, a leopard's (k15-k16)
            t.toSurface = 0.6f;
            t.pointShade = 1f;
            t.groundMoon = true;
            // (k17b: and its shaded walls' own light five times: with their points dimmed, k17, they read as dark masses,
            // their surface at OKLab L 0.10-0.12 against the user's screenshot's 0.25; and round the ant the terrain behind
            // its walls' splats sunk, between them still lighter orange patches on a slope, k17)
            t.wallAmbient = 4.5f;   // (k18: 3.5 in k17c-d, its shaded walls' surface still OKLab L 0.18-0.21 against the user's screenshot's 0.24-0.29, k17 content review)
            t.groundNearKeep = 0.6f;
            // (k17c: 3.5 and toward grey: at 5 in its biome's rust its lit walls blew out a loud red, OKLCH C 0.11-0.13,
            // 17% of the frame over 0.10, and the whole 0.048 against v15's 0.033, k17b; and its far terrain calmer, the
            // user's 10-29 "近处可以饱和度高，远处低")
            t.wallAmbientGrey = 0.85f;   // (0.75 in k17c-d)
            t.groundSatByDistance = new Vector4(0.72f, 0.43f, 5f, 24f);   // (k17d: at 0.7 from 8 to 30 its lit walls still 8.7% over C 0.10, against k16's 3.1%, k17c; k18b: its terrain's own red, the textures', not their tint, k18: at 0.78 near, a trial 4.2% for 7.5%, 4.8% in k18b; 0.72 in k18c; its floor near the ant the carpet's)
            // (at an ember's own hue, about 5° on the wheel, the user's 0-8°: at the palette's, which v15's light carries 8°
            // to the crimson side of the world's hue, the gate's arc and the thread read raspberry pink, k3-k4)
            const float EmberHue = 27f;   // (OKLCH)
            Color Ember(Color c, float l, float chroma)
            {
                float scale = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                Color o = NestPalette.FromOklch(new Vector3(l, chroma, EmberHue)) * scale;
                o.a = c.a;
                return o;
            }
            t.threadHalo = Ember(t.threadHalo, 0.72f, 0.15f);
            t.beacon = Ember(t.beacon, 0.74f, 0.13f);
            t.grainGlow = Ember(t.grainGlow, 0.68f, 0.15f);
            t.trail = Ember(t.trail, 0.64f, 0.15f);   // (0.76, 0.11 in k3: the glow round the ant's feet still pale pink)
            t.glowDeep = Ember(t.glowDeep, 0.62f, 0.18f);
            // its temple's and logs' embers, and their carved wood's warm light, in an ember red (a relic's glow was white,
            // a little toward glowDeep: the embers came out milky, its temple's split side a white speckled column, k6), at
            // the ember hue and deep: bright, a red turns on the screen (at 31°, L 0.58 they read orange, 18-21° on the
            // wheel, their brightest the amber nest's 34°, k7 colour review; at 22° a rose, -8°, toward the nursery's pink,
            // k8; at 27°, L 0.62 made bright, a salmon pink, k7b)
            // (k18: deeper and a little toward orange, a charcoal fire's red-orange, with a hotter, lighter orange in their
            // lightest middles: at 0.5, 27° the wide red patches on its temple's main column read a flat coral pink, k15-k17)
            t.relicGlow = NestPalette.FromOklch(new Vector3(0.44f, 0.17f, 29f));
            t.relicGlow.a = 1f;
            t.relicCore = NestPalette.FromOklch(new Vector3(0.58f, 0.15f, 40f));
            t.relicCore.a = 1f;
            // and its relics kept denser further off, two fifths of their splats (at the plants' tenth its temple's cracks
            // and galleries could not show, k6-k7 plant reviews)
            t.relicKeep = 0.4f;
            // the thin light along every form's outline a cool moonlit silver, against its warm embers: its cool notes
            // spread over everything, small and dark, not one blue thing (the user, 10-29: keep its family at 18°; the
            // palette's rim, its fog's dark red, gave none; cool things bright enough to count read as snow and moss, k11b)
            t.rim = new Color(0.023f, 0.036f, 0.057f);   // (k14: 0.035, 0.055, 0.088, between k12's cool 0.03, 0.055, 0.1, the log's bark violet up close, and k13's near neutral; k17 two thirds: its thin twigs near the ant, all rim, read pale grey-blue glass tubes, k16 content review)
            // its floor, and a little its walls, redder (the user, 10-29: "这个地面包括环境可以稍微再偏红一些些，现在这个地面有点太灰了"):
            // more of its palette in its ground did little, k12, its palette's floor itself a near-grey brown; so its floor's
            // chroma about doubled, toward rust, its lightness kept; its walls a little, staying darker than its floor
            Vector3 fl = NestPalette.Oklch(t.floor);
            t.floor = NestPalette.FromOklch(new Vector3(fl.x + 0.06f, fl.y * 2f + 0.03f, 32f));   // (k14; 1.8, +0.02, 30° in k13; k14b: a little lighter, its floor darker with more of its palette, 0.029)
            // and its moon a little warmer, toward orange, from its palette's pinkish grey: its floor read a mauve brown under it,
            // 17-21° on the screen whatever its own colour (k13-k14)
            t.moon = Color.Lerp(t.moon, new Color(1f, 0.8f, 0.58f) * Mathf.Max(t.moon.r, Mathf.Max(t.moon.g, t.moon.b)), 0.15f);
            Vector3 bk = NestPalette.Oklch(t.bank);
            t.bank = NestPalette.FromOklch(new Vector3(bk.x + 0.05f, bk.y * 0.8f, Mathf.LerpAngle(bk.z, 30f, 0.5f)));   // (k15c: a little lighter, its walls lit as a whole; k15d: calmer, at 1.2x their lit faces a vivid rust, 3.7% of its pixels over C 0.10; k18: 0.8 from 0.9)
            // and its walls' terrain calmer, its lightness kept (k18: its lit walls brighter since k17, their surface 27% over
            // C 0.10, 7.1% of the frame, k17 colour review; its floor near the ant is the carpet's, not this); its walls' pale
            // top edge toward its walls (a narrow strip of pale points along its ridges, k17 content review)
            Color Calmer(Color c, float k)
            {
                float scale = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                Vector3 o = NestPalette.Oklch(c * (1f / scale));
                Color r = NestPalette.FromOklch(new Vector3(o.x, o.y * k, o.z)) * scale;
                r.a = c.a;
                return r;
            }
            t.bankTexTint = Calmer(t.bankTexTint, 0.8f);
            t.edge = Color.Lerp(t.edge, t.bank, 0.4f);
            // and its fire morels' tips a warm near white, softly lit, as its cups' rims (in the palette's pale glow their
            // tips and ridges read as frost, grey-pink, k6-k7; in glowDeep, bright, a salmon pink, k7b)
            t.kindMark = new System.Collections.Generic.Dictionary<string, Color> { { "fire_morel", new Color(1f, 0.55f, 0.3f) } };   // (k19g: an ember orange; at 1, 0.8, 0.6 with their brighter tips of k19, white-hot, the k19f plant review)
            t.motes = Color.Lerp(t.motes, t.threadHalo, 0.3f) * 0.55f;
            // its midges, living things, in its own warm light: a speck of ember orange drifting like a spark, faint warm
            // grey wings (v15's cyan, fixed in the shader for every nest until 11-04, made them pale blue-white here)
            t.midgeTint = new Color(0.8f, 0.27f, 0.09f, 0.6f);
            t.midgeWing = new Color(0.16f, 0.13f, 0.11f);   // (0.42, 0.36, 0.32 in k19-k19g: their two wings paired cream dots by the floor, "eyes in the dark", the k19f plant review)
            // k19, fuller (the user, 11-04, through the hub: "这几个场景的丰富度…还没有V15那个高，可以稍微再增加一些细节" and
            // "除了蓝色以外，这几个场景还是有些太空了。可以增加一些大的素材也好、小的素材也好"): it grew 77 plants of 4 kinds against
            // v15's 1534 of 38, its floor bare between its twigs and stones. The world's plants that belong after a fire grow
            // here too, charred: dead trunks standing up its banks, burnt-out roots arching from its walls' feet, cinnabar shelf
            // fungi, small fire mushrooms, burnt grass and curled black leaves on its floor; their own colours folded to its
            // band as its own plants' are (ownBand), the burnt ones darker (kindGain). Its morels in clusters, about 40 (5 at
            // its walls' feet), a little larger and their tips a little more lit; its cups a fifth more; its charred twigs
            // about twice as many (its floor's own share), and a few ember cracks in its floor, dim and broken. (LabNest's
            // "ember" plan: dead trunks in groups and more log pieces along the way.) From the self-check's content and plant
            // reviews against v15 (07_Outputs/scene_reviews/2026-10-27_kiln/k19): no jelly fungi (resin, honey), corals,
            // stinkhorns (flowers), mossy boulders (stone eggs, or a sponge), pebble heaps. Checks: -kilnrich 0
            if (Game.Arg("-kilnrich") != "0")
            {
                t.scatterPlants = new[] { "trunk_night", "root_arch", "dry_grass_tuft" };   // (no fallen leaves since k19f: gold leaves by its way whatever their saturation or lightness, k19e, lit as the shelves and mushrooms were)   // (and no shelf fungi since k19e: gold blobs on its trunks and at their feet whatever their saturation, glowing as the mushrooms did, k19d)   // (no small mushrooms since k19d: their glowing caps a mustard gold whatever their own colour's saturation or lightness, k19b-k19c, which leave a glow as it is)
                // (k19b: k19 grew 186 grass tufts, 92 trunks up its banks, two behind its gate's arch from the way out, and 19
                // roots; 22 morels: fewer of those, more of these)
                t.sparserKinds = new[] { "trunk_night", "root_arch", "bracket_shelf", "tiny_mushrooms", "dry_grass_tuft", "fallen_leaf", "fire_morel", "ember_cups" };
                t.sparserBy = new[] { 3.2f, 1.8f, 1.3f, 1.55f, 2.6f, 1.3f, 0.2f, 0.65f };   // (k19g: fewer trunks spread on its plateau, more cups, the k19f content review)   // (the morels 0.27 to k19e: 32 at 1.6 times their size)
                t.kindGain = t.kindGain != null ? new System.Collections.Generic.Dictionary<string, float>(t.kindGain) : new System.Collections.Generic.Dictionary<string, float>();
                t.kindGain["trunk_night"] = 1.5f;      // (k19g: at 1 near-black columns in its canyon, OKLab L 0.15-0.18 against a dark rust surface's 0.22-0.26, the k19f colour review; k19e: 0.7 in k19d still black towers against the night; relics are darker here anyway, rigidGain; charcoal, as its log's 0.65 and its temple's 0.7; at 0.5 in k19b flat black cut-outs, holes in its slopes, the k19b plant and colour reviews)
                t.kindGain["root_arch"] = 0.4f;        // (in its own colours an orange sponge by the temple, k2: burnt out, near black)
                t.kindGain["dry_grass_tuft"] = 0.35f;  // (burnt stubble; 0.45 to k19f with its colour at 0.2: a silver-grey grass, the whitest plant by its way out, the k19f plant review)
                t.kindGain["fallen_leaf"] = 0.35f;     // (curled, charred; its palette's pale leaves read as another nest's, k1)
                t.kindGain["bracket_shelf"] = 0.85f;
                t.kindGain["tiny_mushrooms"] = 0.5f;   // (0.6 and their colour at 0.3 in k19b: still mustard yellow, a cluster by the way at halfway)
                // and their own colours muted: folded to its band's warm end their yellows came out lemon and gold, the honey
                // granary's (the small mushrooms a bright yellow cluster in front of its way out; the shelves and grass olive-gold
                // on its slopes, k19), the trunks' teal a navy grey; toward ochre, tan and charcoal
                t.kindSat = new System.Collections.Generic.Dictionary<string, float> { { "tiny_mushrooms", 0.12f }, { "dry_grass_tuft", 0.28f }, { "fallen_leaf", 0.4f }, { "bracket_shelf", 0.35f }, { "root_arch", 0.5f }, { "trunk_night", 0.25f } };   // (k19d: the shelves at 0.6 and the grass at 0.35 still khaki and olive gold by its way out, the k19b colour review)
                // the dead trunks standing along its way are relics, their model's own colours kept (the blue forest's dark teal: navy
                // columns, k19): toward its palette's base, as its log's and temple's own charcoal is not
                t.kindsSpareFar = true;   // (its far giants are trunks too: left as they were, k19e; black masses behind its way out in k19b-k19d)
                t.clearExitView = true;   // (and none standing where its way's end looks out through a gate: one filled its way out's arch, k19b; the hub, 11-04)
                t.kindRigidTint = new System.Collections.Generic.Dictionary<string, float> { { "trunk_night", 1f } };   // (0.85 in k19b: neutral charcoal; all the way, its base's rust brown, a dark side and not a hole)
                t.labTweaks = new System.Collections.Generic.Dictionary<string, Vector4> { { "fire_morel", new Vector4(1f, 0f, 0f, 1.6f) } };
                t.bio = t.bio != null ? new System.Collections.Generic.Dictionary<string, Vector4>(t.bio) : new System.Collections.Generic.Dictionary<string, Vector4>();
                t.bio["fire_morel"] = new Vector4(8, 0.03f, 0, 0.9f);   // (k19g: 0.036 and 1.4 in k19-k19f, their tips white-hot and a cold white light at their feet, 2.2% of the ant's view near white, the k19f plant review; and their mark an ember orange, kindMark)   // (a dark brown morel with no light of its own, the self-check's plant review: between k10's whole white caps, 0.045 and 1.5, and k11's 0.03 and 1.3)
                t.ownShare = 0.55f;
                t.things = new[] { new Thing { kind = "CharTwigs", weight = 40 }, new Thing { kind = "AshStones", weight = 16 }, new Thing { kind = "EmberCracks", weight = 3 } };   // (its stones about as many as before: its cool notes, the family; a crack in about 20 things)
            }
            return t;
        }
        static readonly NestTheme RedBefore = RedBasic();
        static NestTheme RedBasic() => new NestTheme
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
            // (2026-10-27, redesigned: the user's pick of three directions, 02_Concepts/Kiln_Redesign_v1, "C 火后古木") a
            // forest floor long after an old fire: ash, burnt earth and charcoal (its own ground set, v13). Beside the way
            // the burnt log the ancestors carved into their temple, ring by ring (objects: LabNest's "ember" plan; a
            // landmark, not the goal: no heart); round it the life that follows a fire, bonfire cups on the ash and fire
            // morels (Tripo); charred twigs with embers deep in their cracks and charcoal balls (NestThingsThemed). None of
            // the amber nest's plants (its succulents, dry grass and seeds were the kiln's before), no amber
            // (no root arches: keeping their own colours they stood as an orange sponge by the temple, k2; no pebble heaps:
            // big pale boulders, the brightest things on its floor, k5)
            scatterPlants = new string[0],
            labPlants = new[] { "ember_cups", "fire_morel" },
            glowParts = new[]
            {
                // the log lights only where it glows (buildingsGlowInParts): by how red its texture is (red), the embers
                // deep in its cracks; the pale wood of its carved galleries just a little (from 0.35, k2: its open end a
                // white disc); its charcoal not (by lightness, its silver sheen would have lit too: lumMin out of reach)
                // (amount from 0.85: k1's embers blew out to thin white lines, in the relics' pale amber glow; in its own
                // ember red since k5, 0.8: at 0.65 its embers were gone, k3 plant review)
                // (red from 0.42: its cracks are thin, and from the way, where few of its points are drawn, they were lost:
                // their red-brown edges glow a little too, k5)
                // (k7: at red 0.32 half its texture glowed, its red-brown charcoal and its tan wood too, in the relics' white:
                // milky, k6. Now its bright red cracks alone, 0.62, at hot, kept as it thins, ParticleLook.EmberOrder; its
                // carved wood, pale and warm, a soft light of its own, lumMin and amount)
                // (its carved wood's light less, 0.2: at 0.35 its rings out-shone its embers and read gold, k7; none since k11:
                // weathered silver grey, lit by its own red, its rings read a salmon pink, k10)
                // (its embers back to k8's light, 0.72: since exposure 1.0, k12, its end face's rings and embers one red)
                // (k14: its end face's rings full of embers, one red target: only its reddest cracks, dimmer, red 0.7, hot 0.6)
                // (k17: its tan wood, the end face's rings, charred a dark warm grey-brown from lumMin 1.3, its embers kept in
                // the cracks: in its own tan, folded to the world's red, a glowing copper spiral, half its face vivid, k16)
                new GlowPart { plant = "ember_log", from = 0f, to = 1.05f, lumMin = 1.3f, amount = 0f, red = 0.7f, hot = 0.6f, burnt = 0.85f },
                // and its stump temple: the embers in its long cracks, and the pale wood of its split side's galleries warm
                // from within, as the concept has them (red from 0.42, 0.36: in k3 and k4 its galleries stayed dark)
                // (its split side's less, 0.25: at 0.45 gold-orange streaks, an even orange-brown, k7; none since k11: its
                // weathered grey lit red read coral, k10)
                // (its embers' light 0.65 since k17: at 0.95 its main column's wide red patches glowed a coral pink, not its
                // cracks' deep red, k15-k16 content reviews)
                new GlowPart { plant = "char_stump_temple", from = 0f, to = 1.05f, lumMin = 2.4f, amount = 0f, red = 0.62f, hot = 0.65f, grey = 0.7f },   // (grey: its split side's pale wood weathered toward a cool silver grey; in its own tan it read gold, the amber nest's, k7-k8; not the log's, whose silver rings under its red read pink, k10-k15)
            },
            // its markings: the cups' beaded rims, the palest of their texture (as their growing ends, 5, the crumbs of their
            // ash were tips too, 1341 of them: dots all over the cups, k1 plant review); the morels' caps softly from their
            // tips down (8: at 5 and 0.06 they stayed three dark cones; at 0.12 the whole cap lit, k3). The cups' share smaller
            // than k3's 0.035, which marked the pale grains on their outsides too. The morels' stronger (at 0.9 their tips a
            // soft grey-pink, like dust, k6 plant review; at 1.3, in that grey-pink, frost over its tip and ridges, k7: 1.0, in
            // a warm near white, KilnTuned; nearer the very tips, 0.035: at 0.05 the two small caps lit whole, k7b)
            // (k8: its tips now its caps' tops, Tools/plants/growth_map.py TIPS_BY_HEIGHT: their ridges' bumps had been tips
            // too, white dots all over; brighter, 1.5, a soft light: at 1.0 a matte white top, k7 plant review)
            // (k11: a smaller light, 0.03 and 1.3: at 0.045 and 1.5 a whole white cap on each, k10 content review)
            bio = new System.Collections.Generic.Dictionary<string, Vector4> { { "ember_cups", new Vector4(4, 0.02f, 0, 1f) }, { "fire_morel", new Vector4(8, 0.03f, 0, 1.3f) } },
            // (the charcoal balls left out: three rounds, k1-k5, and still read as stone eggs and glass bubbles, k3 content
            // review: "下一轮还读不出，就拿掉"; NestThingsThemed.CharBalls kept)
            // (and small slate stones scattered singly in the ash, its cool accent, k7c: without them its family closed to 8°)
            // (the stones: 14 in k7d, hardly seen in its main views; 25 in k8, a dozen on one slope, blue confetti. k11: and cold
            // ash drifts, its cool notes spread over several small things, the user's choice to keep its family at 18°)
            // (its cold-ash drifts, AshDrift, k11-k12, left out: blue, then warm grey, they read as snow, then as pale plates,
            // never as ash, k12 content review)
            things = new[] { new Thing { kind = "CharTwigs", weight = 30 }, new Thing { kind = "AshStones", weight = 24 } },   // (k14: 18 in k13, its cool faces too few to count)
            // the burnt wood a little darker, so its embers and its carved galleries' warm light show against it (k5: the
            // temple's split side a white speckled column)
            kindGain = new System.Collections.Generic.Dictionary<string, float> { { "char_stump_temple", 0.7f }, { "ember_log", 0.65f } },   // (the log 0.85 to k12: its rings as light as its embers)
            // (its temple is the landmark where the way forks: no junction tree there; the biome's, an amber-nest clay
            // spire, stood beside the temple as an orange sponge, k3)
            ownShare = 0.3f, dampScale = 0f, objects = "ember", heart = null, noJunctionTree = true,
            noKinds = KilnNoKinds, touchParts = new[] { "ember_cups", "fire_morel" },
            vineGlow = 0.72f, rigidGain = 1f, rigidTint = 0.12f,   // (the log's charcoal kept dark grey: tinted toward body0 it turned clay)
            // (the plants' own colours kept within 16 ± 20°: a wider band let some fall to pink, which stood out as blots)
            wayThing = "CharTwigs", wayEvery = 4.5f, ownBand = new Vector2(16, 20), buildingsGlowInParts = true,
        };
    }
}
