using System;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Every gameplay and look tunable in one place (defaults carried over from the web prototype's config.js).
    /// Overrides: put a partial JSON copy in StreamingAssets/game_config.json; fields present there win.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public AntSettings ant = new AntSettings();
        public NpcSettings npcs = new NpcSettings();
        public CameraSettings camera = new CameraSettings();
        public SoundSettings audio = new SoundSettings();
        public bool padRumble = true;       // a short rumble of the game pad on touches, flares and gates
        public float ruinWidth = 0.55f;     // a ruin's canyons in a honeycomb, relative to the nest's own: narrow tunnels
        public float trailBrightness = 0.9f;// the glowing footprints that lead through a ruin
        public ScatterSettings scatter = new ScatterSettings();
        public AtmosphereSettings atmosphere = new AtmosphereSettings();
        public ReliefSettings relief = new ReliefSettings();
        public GlowSettings glow = new GlowSettings();
        public GateSettings gates = new GateSettings();
        public DisplaySettings display = new DisplaySettings();
        /// <summary>
        /// The world built when nothing else is chosen: three stitched nests in the blue fungal forest,
        /// plus one separate nest in a warm amber style elsewhere on the map (reached through a portal gate).
        /// Format: regions separated by '|'; each is "path-NNN" or "stitch:row|cluster:ids", with "@biome".
        /// </summary>
        // every nest stitched into one honeycomb, edge to edge, joined on foot by connector canyons across the seams
        // (a light gate on each): the blue trio in the middle with the amber nest and the two ruins round it, then
        // the nine single nests in alternating colours
        public string defaultWorld = "honeycomb:path-001@blue,path-002@blue,path-003@blue,path-010@amber,path-003@blue#relics,path-010@amber#temple," +
            "path-004@blue,path-005@amber,path-006@blue,path-007@amber,path-008@blue,path-009@amber,path-011@blue,path-012@amber,path-013@blue";
        public float regionGap = 90f;       // empty space between separate regions on the map
        public Biome[] biomes =
        {
            new Biome
            {
                id = "blue", displayName = "腐海·蓝", displayNameEn = "Blue Fungal Forest",
                groundSet = "blue", terrainHue = 0, terrainSaturation = 0.8f,
                // the moss textures are dark: tinted up a little, kept below the earlier ground for the night mood
                floorTint = new Color(0.95f, 1.15f, 1.2f), bankTint = new Color(0.95f, 1.25f, 1.17f), plateauTint = new Color(0.42f, 0.54f, 0.57f),
                speckColor = new Color(0.55f, 1f, 0.92f),
                plantHue = 0, plantSaturation = 1f, plantGlow = new Color(0.55f, 1f, 0.95f), groundColor = new Color(0.1f, 0.2f, 0.19f),
                antHue = 0, antSaturation = 1f,
                fogColor = new Color(0.09f, 0.19f, 0.25f), ambientSky = new Color(0.34f, 0.55f, 0.68f),
                ambientEquator = new Color(0.2f, 0.36f, 0.42f), ambientGround = new Color(0.06f, 0.12f, 0.13f),
                moonColor = new Color(0.72f, 0.86f, 1f), skyTop = new Color(0.015f, 0.035f, 0.075f), skyHorizon = new Color(0.1f, 0.24f, 0.3f),
                gateColor = new Color(0.45f, 1f, 0.95f),
                nativePlants = "seed_husk",
            },
            new Biome
            {
                id = "amber", displayName = "琥珀·暖", displayNameEn = "Warm Amber",
                // its own clay and soil textures (bright by day: tinted down for the night)
                groundSet = "amber", terrainHue = 0, terrainSaturation = 1f,
                mapHue = 165, mapSaturation = 1.15f,   // YIQ: +165 turns the map's teal into rust and amber
                floorTint = new Color(0.36f, 0.33f, 0.3f), bankTint = new Color(0.5f, 0.46f, 0.43f), plateauTint = new Color(0.4f, 0.37f, 0.35f),
                speckColor = new Color(1f, 0.75f, 0.35f),
                plantHue = 165, plantSaturation = 1.2f, plantGlow = new Color(1f, 0.72f, 0.35f), groundColor = new Color(0.22f, 0.13f, 0.08f),
                antHue = 165, antSaturation = 1.15f,
                fogColor = new Color(0.2f, 0.11f, 0.08f), ambientSky = new Color(0.62f, 0.44f, 0.32f),
                ambientEquator = new Color(0.42f, 0.25f, 0.17f), ambientGround = new Color(0.1f, 0.06f, 0.04f),
                moonColor = new Color(1f, 0.84f, 0.68f), skyTop = new Color(0.06f, 0.03f, 0.05f), skyHorizon = new Color(0.36f, 0.16f, 0.1f),
                rimColor = new Color(1f, 0.62f, 0.38f),
                gateColor = new Color(1f, 0.74f, 0.36f),
                nativePlants = "amber_resin,soil_spire,dry_mushroom,seed_husk,fungus_garden,dry_grass_tuft,thorn_succulent,seed_pile",
                landmarkPlant = "dry_mushroom",
                surroundings = "soil_spire,dry_mushroom,trunk_night",
            },
        };

        /// <summary>True when <paramref name="id"/> is in the comma-separated <paramref name="list"/>.</summary>
        public static bool InList(string list, string id)
        {
            if (string.IsNullOrEmpty(list)) return false;
            foreach (string s in list.Split(',')) if (s.Trim() == id) return true;
            return false;
        }

        public Biome GetBiome(string id)
        {
            foreach (var b in biomes) if (b.id == id) return b;
            return biomes[0];
        }
        public QualityPreset[] quality =
        {
            new QualityPreset { name = "low", textureLimit = 1, parallax = 0f, terrainStep = 0.8f, density = 0.55f, shadowDistance = 0f, renderScale = 0.8f, drawDistance = 120f, plantCull = 90f, bigPlantCull = 200f, bloom = true, ao = false },
            new QualityPreset { name = "medium", parallax = 0f, terrainStep = 0.5f, density = 0.8f, shadowDistance = 45f, renderScale = 1f, drawDistance = 170f, plantCull = 110f, bigPlantCull = 240f, bloom = true, ao = false },
            new QualityPreset { name = "high", parallax = 0f, terrainStep = 0.35f, density = 1f, shadowDistance = 50f, renderScale = 1f, drawDistance = 240f, plantCull = 115f, bigPlantCull = 260f, bloom = true, ao = false },
        };
        public float teleportCooldown = 0f; // seconds; > 0 later gives the teleport tool a time limit
        public float slotSpacing = 34f;     // story slots along the paths (placeholders for user objects)
        public float slotRadius = 2.4f;     // how close the ant must be to interact with a slot

        /// <summary>Defaults, then StreamingAssets/game_config.json on top when present.</summary>
        public static GameConfig Load()
        {
            var cfg = new GameConfig();
            string path = Path.Combine(Application.streamingAssetsPath, "game_config.json");
            try
            {
                if (File.Exists(path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(path), cfg);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AntGarden] game_config.json ignored: {e.Message}");
            }
            return cfg;
        }

        public QualityPreset Preset(string name)
        {
            foreach (var q in quality) if (q.name == name) return q;
            return quality[1];
        }
    }

    [Serializable]
    public class AntSettings
    {
        public float bodyLength = 1f;
        public float radius = 0.42f;        // collision radius
        public float crawlSpeed = 0.75f;    // units / second (≈ body lengths / second): the pace of the NPC walkers
        public float fastSpeed = 1.25f;     // hold Shift: only a little faster than crawling, as a real ant hurries
        public float turnRate = 4.2f;       // max radians / second when slow: turns in short arcs, never snaps round
        public float sprintTurnRate = 3f;   // max radians / second at full sprint (smooth arcs)
        public float turnGain = 6f;         // how eagerly the heading chases the input direction
        public float accel = 7f;            // speed-up rate (1/s)
        public float sprintAccel = 4.5f;    // sprint ramps up a little slower: it feels like effort
        public float decel = 10f;           // stopping is snappy
        public float idleBelow = 0.06f;     // gait thresholds (units / second)
        public float fastAbove = 1.2f;
        public float gaitFade = 0.16f;      // seconds to cross-fade between clips
        public float leanRoll = 0.02f;      // body roll into turns
        public float leanPitch = 0.03f;     // head dips when accelerating
        // Cadence. The clips' authored strides are short (0.12-0.23 body lengths per second at 1x), so matching
        // ground speed meant playing them 4-7x too fast and the whole body shook. Like a real ant, the legs now
        // step quickly (a separate leg layer at ground-matched speed, capped) while the body, head, antennae
        // and gaster keep a calm rhythm (the walk clip over a longer effective stride, capped low).
        public float strideScale = 2.4f;    // body layer: effective stride, as a multiple of the authored one
        public float minCadence = 0.7f;
        public float maxCadence = 1.5f;     // body layer cap
        public float legCadenceMax = 3.2f;  // leg layer cap for farther ants (feet slide a little above this)
        // Procedural legs for near ants (LegRig): an alternating tripod gait like a real ant's, feet planted
        // while on the ground, two-bone IK, a low sprawled posture. Lengths are in body lengths (BL).
        // 2026-10-13: off. The authored clips move more smoothly and look like the ants as modelled; the
        // procedural tripod (still here, -proclegs 1) plants its feet but steps faster and lower.
        public bool proceduralLegs = false;
        public float strideBase = 0.45f;    // stride (BL) at a standstill ...
        public float strideGain = 0.2f;     // ... plus this per BL/s of speed (strides lengthen as ants speed up)
        public float strideMax = 1.0f;
        public float strideRoom = 0.34f;    // feet are placed so a stride of at least this fits within leg reach
        public float swingFraction = 0.42f; // share of each step cycle a foot spends in the air
        public float legCadenceCap = 3f;    // steps a second that turning may push the legs to (walking alone is never capped)
        public float turnStep = 0.25f;      // foot travel per radian of turning (BL): how much turning counts as walking
        public float stepHeight = 0.1f;     // foot lift (BL)
        public float legSpread = 1.15f;     // feet further out to the sides than the clip's rest pose
        public float legReach = 1.08f;      // front feet further forward, hind feet further back
        public float bodyDrop = 0.05f;      // body lowered (BL); the knees rise above it
        public float kneeOut = 0.6f;        // knees bend up and outward
        public float ikBlend = 0.3f;        // seconds to blend between clip legs and procedural legs
        public float bodySettle = 7f;       // how quickly the body follows the ground's tilt (1/s); lower = steadier
        public float separation = 2.2f;     // how fast crowded ants ease apart (units / s)
        public float climb = 3.9f;          // how far up the banks past the canyon edge this ant may climb (the crest is
                                            // about 4.5 up; NPCs keep to the floor with 0)
        public Color rimColor = new Color(0.62f, 0.96f, 1f);
        public float rimStrength = 0.9f;
        public Color glowColor = new Color(0.18f, 0.6f, 0.63f);
        public float glowStrength = 0.25f;
        public string playerSpecies = "Ant_001";
    }

    [Serializable]
    public class NpcSettings
    {
        public int countPerNest = 100;      // each nest gets this many at high quality (medium 65 %, low 30 %)
        public int maxCount = 900;
        public int extraCountPerNest = 40;  // nests beyond the archipelago's first two regions get smaller colonies
        public float chamberShare = 0.25f;  // ants milling in the junction chambers
        public float wanderShare = 0.15f;   // ants roaming the full canyon width; the rest stream along the trails
        public float dominantBoost = 1.6f;  // a region's own colony species is this much more common than the others
        public int minPerSpecies = 4;       // every species appears at least this often in every region
        public float chamberSpread = 0.85f; // how far chamber ants roam, relative to the chamber's clearance
        public float leaveChamber = 0.04f;  // chance a chamber ant heads off along a trail after each little walk
        public float meetRadius = 1.6f;     // head-on meetings within this many body lengths stop to touch antennae
        public float stopRate = 0.05f;      // short stops per second of walking
        public Vector2 shortStop = new Vector2(0.3f, 1.2f);
        public float lodNear = 18f;         // closer than this: animated every frame, procedural legs
        public float lodMid = 32f;          // closer than this: every 2nd frame; beyond: every 4th
        public float shadowDistance = 20f;  // only ants this close cast shadows
        public float drawDistance = 60f;    // farther NPCs keep walking but are not drawn or animated
        public Vector2 walkSpeed = new Vector2(0.55f, 0.95f);
        public float fastChance = 0.15f;
        public float pauseChance = 0.35f;
        public Vector2 pause = new Vector2(0.8f, 3.5f);
        public float greetRadius = 2.6f;    // they stop and touch antennae when the player is this close
        public float talkRadius = 2.4f;     // E near an NPC greets it
        public float messengerShare = 0.2f; // ants that have something to say when antennae touch
        public SpeciesSettings[] species =
        {
            new SpeciesSettings { id = "Ant_001", bodyLength = 1f, radius = 0.42f, walkSpeed = new Vector2(0.55f, 0.95f), fastSpeed = 1.6f, rimColor = new Color(1f, 0.85f, 0.63f), glowColor = new Color(0.54f, 0.35f, 0.13f) },
            new SpeciesSettings { id = "Ant_002", hue = 35f, bodyLength = 1.15f, radius = 0.48f, walkSpeed = new Vector2(0.55f, 0.95f), fastSpeed = 1.6f, rimColor = new Color(0.56f, 0.71f, 1f), glowColor = new Color(0.17f, 0.37f, 0.6f) },
            new SpeciesSettings { id = "Ant_003", bodyLength = 1f, radius = 0.42f, walkSpeed = new Vector2(0.55f, 0.95f), fastSpeed = 1.6f, rimColor = new Color(0.82f, 0.96f, 1f), glowColor = new Color(0.23f, 0.56f, 0.69f), canFly = true, share = 0.12f },
            new SpeciesSettings { id = "Ant_004", hue = -25f, bodyLength = 1.2f, radius = 0.5f, walkSpeed = new Vector2(0.38f, 0.6f), fastSpeed = 1.1f, rimColor = new Color(0.75f, 0.96f, 0.85f), glowColor = new Color(0.18f, 0.48f, 0.35f) },
            new SpeciesSettings { id = "Ant_005", hue = 70f, bodyLength = 0.95f, radius = 0.4f, walkSpeed = new Vector2(0.85f, 1.25f), fastSpeed = 1.9f, rimColor = new Color(0.62f, 0.85f, 1f), glowColor = new Color(0.12f, 0.37f, 0.53f) },
        };
        public FlightSettings flight = new FlightSettings();
        public ColonySettings colony = new ColonySettings();
    }

    [Serializable]
    public class SpeciesSettings
    {
        public string id;
        public float hue;                    // shell colour shift (degrees) so the species read apart
        public float bodyLength = 1f;
        public float radius = 0.42f;
        public Vector2 walkSpeed = new Vector2(0.55f, 0.95f);
        public float fastSpeed = 1.6f;
        public Color rimColor = Color.white;
        public Color glowColor = Color.gray;
        public bool canFly;
        public float share = 1f;             // how common the species is (winged ants are rare)
    }

    [Serializable]
    public class FlightSettings
    {
        public float chance = 0.08f;               // at the end of an edge, how often a flyer takes off (alates are rare)
        public Vector2 altitude = new Vector2(5f, 9f);
        public float speed = 2.6f;
        public Vector2 duration = new Vector2(8f, 16f);
        public Vector2 cooldown = new Vector2(25f, 60f);
    }

    [Serializable]
    public class CameraSettings
    {
        public float distance = 3.6f;
        public float minDistance = 1.4f;
        public float maxDistance = 12f;
        public float height = 0.45f;         // look-at offset above the ant
        public float pitch = 0.2f;           // radians above horizontal
        public float minPitch = -0.55f;      // below 0 the camera dips low and tilts up to look at the canopy
        public float maxPitch = 1.2f;
        public float followDelay = 1.4f;     // seconds after a drag before the camera starts easing back behind the ant
        public float recenterRate = 0.9f;    // how quickly it eases round behind an ant running away from it (1/s)
        public float sprintFovKick = 5f;     // degrees added to the field of view at full sprint
        public float sprintDistance = 1.08f; // camera pulls back a little when sprinting
        public float wallMargin = 0.35f;     // how far inside the canyon floor the camera stays
        public float fov = 62f;
        public float mouseSensitivity = 1f;
        public float padTurnSpeed = 2.4f;    // right stick fully over: radians a second round the ant
        public float padPitchSpeed = 1.3f;   // ... and up / down
        public float slideRate = 10f;        // how quickly the camera slides along a canyon wall it meets (1/s)
        public float followSmooth = 12f;     // how closely the camera keeps up with its ideal spot (1/s)
        public float zoomInRate = 10f;       // how fast the boom shortens when something gets in the way (1/s)
        public float zoomHold = 0.35f;       // seconds it stays in before lengthening again (no pumping)
        public float zoomOutRate = 2.2f;     // how slowly it extends again once the view is clear (1/s)
        public float blockingPropHeight = 3f; // props lower than this never push the camera in
    }

    /// <summary>Levels of the synthesised sounds (Sfx), 0..1. Everything is meant to sit quietly under the scene.</summary>
    [Serializable]
    public class SoundSettings
    {
        public float master = 1f;
        public float ambience = 0.16f;   // night air: wind and far crickets, or tunnel hum and drips
        public float steps = 0.14f;      // the player's feet
        public float touch = 0.3f;       // antennae touching
        public float flare = 0.22f;      // a glowing plant lighting up
        public float gate = 0.35f;       // portals, teleports, light gates
        public float map = 0.25f;        // opening and closing the map
    }

    /// <summary>Plant placement: bands are signed distance to the canyon edge (negative = on the canyon floor).</summary>
    [Serializable]
    public class ScatterSettings
    {
        public ScatterRule[] rules =
        {
            // big things first: later rules only fill the space that is left
            new ScatterRule { plant = "trunk_night", dMin = 4.5f, dMax = 18f, spacing = 9f, height = new Vector2(7f, 13f), stack = 2, tilt = 0.08f, tintJitter = 0.12f, patchiness = 0.35f },
            new ScatterRule { plant = "trunk_night", dMin = 18f, dMax = 60f, spacing = 15f, height = new Vector2(10f, 18f), stack = 3, tilt = 0.05f, tintJitter = 0.1f, dim = 0.55f, patchiness = 0.35f },
            new ScatterRule { plant = "fan_tree", dMin = 3f, dMax = 14f, spacing = 16f, height = new Vector2(6f, 10f), tilt = 0.1f, tintJitter = 0.1f, patchiness = 0.3f },
            new ScatterRule { plant = "dry_mushroom", biomes = "amber", dMin = 3.5f, dMax = 18f, spacing = 15f, height = new Vector2(6f, 11f), tilt = 0.06f, tintJitter = 0.08f, patchiness = 0.35f },
            new ScatterRule { plant = "soil_spire", biomes = "amber", dMin = 2.5f, dMax = 16f, spacing = 11f, height = new Vector2(4f, 9f), tilt = 0.08f, tintJitter = 0.1f, patchiness = 0.4f },
            new ScatterRule { plant = "bracket_shelf", biomes = "blue", dMin = 0.3f, dMax = 6f, spacing = 8f, height = new Vector2(1.4f, 2.8f), tilt = 0.35f, tintJitter = 0.1f },
            new ScatterRule { plant = "lantern_pods", biomes = "blue", dMin = 0.5f, dMax = 6f, spacing = 8f, height = new Vector2(2.6f, 4.6f), tilt = 0.12f, tintJitter = 0.08f },
            new ScatterRule { plant = "sponge_mound", dMin = 0.2f, dMax = 6f, spacing = 3.6f, height = new Vector2(1.2f, 2.6f), tilt = 0.7f, tintJitter = 0.15f },
            new ScatterRule { plant = "seed_husk", dMin = -0.4f, dMax = 3f, spacing = 20f, height = new Vector2(1.4f, 2.4f), tilt = 0.6f, tintJitter = 0.06f },
            new ScatterRule { plant = "curl_glow", dMin = -0.6f, dMax = 4f, spacing = 5.2f, height = new Vector2(1.6f, 3.4f), tilt = 0.25f, tintJitter = 0.1f, cluster = new Vector2Int(1, 3) },
            new ScatterRule { plant = "coral_fungus", biomes = "blue", dMin = -1.2f, dMax = 3.5f, spacing = 5.2f, height = new Vector2(0.8f, 1.8f), tilt = 0.3f, tintJitter = 0.1f, cluster = new Vector2Int(1, 3) },
            new ScatterRule { plant = "jelly_fungus", biomes = "blue", dMin = -0.6f, dMax = 4.5f, spacing = 6.5f, height = new Vector2(0.7f, 1.4f), tilt = 0.5f, tintJitter = 0.08f },
            new ScatterRule { plant = "amber_resin", biomes = "amber", dMin = -1.2f, dMax = 4f, spacing = 4.6f, height = new Vector2(0.9f, 2.4f), tilt = 0.3f, tintJitter = 0.08f, cluster = new Vector2Int(1, 3) },
            new ScatterRule { plant = "spore_stalk", dMin = -1.8f, dMax = 3f, spacing = 3.6f, height = new Vector2(1.0f, 2.6f), tilt = 0.3f, tintJitter = 0.08f, cluster = new Vector2Int(2, 6) },
            new ScatterRule { plant = "sponge_mound", dMin = -6f, dMax = -0.6f, spacing = 2.8f, height = new Vector2(0.25f, 0.55f), tilt = 0.9f, tintJitter = 0.12f },
            // world detail pass (v5): big pieces first, then the ground detail that fills the gaps
            new ScatterRule { plant = "boulder_moss", dMin = 1.5f, dMax = 22f, spacing = 10f, height = new Vector2(1.6f, 4.2f), tilt = 0.5f, tintJitter = 0.1f },
            new ScatterRule { plant = "glass_sponge", biomes = "blue", dMin = 1f, dMax = 9f, spacing = 11f, height = new Vector2(2.6f, 5f), tilt = 0.15f, tintJitter = 0.08f },
            new ScatterRule { plant = "thorn_succulent", biomes = "amber", dMin = 0.5f, dMax = 9f, spacing = 6.5f, height = new Vector2(1f, 2.2f), tilt = 0.3f, tintJitter = 0.1f, cluster = new Vector2Int(1, 2) },
            new ScatterRule { plant = "root_arch", dMin = -0.5f, dMax = 5f, spacing = 8f, height = new Vector2(0.8f, 1.8f), tilt = 0.7f, tintJitter = 0.1f },
            new ScatterRule { plant = "fungus_garden", biomes = "amber", dMin = -2.5f, dMax = 1f, spacing = 9f, height = new Vector2(0.8f, 1.6f), tilt = 0.6f, tintJitter = 0.06f },
            new ScatterRule { plant = "anemone_fungus", biomes = "blue", dMin = -1.5f, dMax = 3f, spacing = 6.5f, height = new Vector2(0.8f, 1.6f), tilt = 0.4f, tintJitter = 0.08f, cluster = new Vector2Int(1, 2) },
            new ScatterRule { plant = "cup_fungus", biomes = "blue", dMin = -0.8f, dMax = 4f, spacing = 5.5f, height = new Vector2(0.6f, 1.4f), tilt = 0.4f, tintJitter = 0.1f, cluster = new Vector2Int(1, 2) },
            new ScatterRule { plant = "seed_pile", biomes = "amber", dMin = -2.5f, dMax = 1.5f, spacing = 11f, height = new Vector2(0.35f, 0.7f), tilt = 0.9f, tintJitter = 0.05f },
            new ScatterRule { plant = "dry_grass_tuft", biomes = "amber", dMin = -0.5f, dMax = 8f, spacing = 3f, height = new Vector2(0.6f, 1.4f), tilt = 0.5f, tintJitter = 0.12f, cluster = new Vector2Int(1, 3), noShadow = true },
            new ScatterRule { plant = "tiny_mushrooms", biomes = "blue", dMin = -2f, dMax = 5f, spacing = 3.6f, height = new Vector2(0.3f, 0.8f), tilt = 0.7f, tintJitter = 0.1f, cluster = new Vector2Int(1, 2), noShadow = true },
            new ScatterRule { plant = "pebble_cluster", dMin = -6f, dMax = 10f, spacing = 2.9f, height = new Vector2(0.22f, 0.65f), tilt = 0.9f, tintJitter = 0.12f, noShadow = true },
            new ScatterRule { plant = "fallen_leaf", dMin = -6f, dMax = 3f, spacing = 3.6f, height = new Vector2(0.12f, 0.3f), tilt = 1f, tintJitter = 0.12f, noShadow = true },
        };
        public LandmarkRule landmark = new LandmarkRule();
        public float surroundingsReach = 70f; // dark forest silhouettes outside the hexagon
        public int surroundingsCount = 40;
        public float sink = 0.04f;            // fraction of plant height pushed into the ground
        public float patchSize = 3.5f;        // a plant's own patches are about this many spacings across
        public float stepUp = 0.45f;          // things lower than this are walked over (a dome), higher ones block ...
        public float stepUpBroad = 1f;        // ... unless broad (no taller than 0.8 of their radius) and below this
    }

    [Serializable]
    public class ScatterRule
    {
        public string plant;
        public float dMin, dMax, spacing;
        public Vector2 height = new Vector2(1, 2);
        public int stack = 1;                 // >1: trunk segments stacked into a taller column
        public float tilt = 0.2f;             // 0 = upright, 1 = fully follows the ground slope
        public float tintJitter = 0.1f;
        public float dim = 1f;                // < 1 darkens far plants
        public Vector2Int cluster = new Vector2Int(1, 1);
        public string biomes = "";            // comma-separated biome ids this rule grows in; empty = every biome
        public bool noShadow;                 // small ground detail: no shadow casting (cheaper, and it reads as texture)
        public float patchiness = 0.85f;      // 0 = spread evenly; 1 = only in patches (with a few strays between)
    }

    [Serializable]
    public class LandmarkRule
    {
        public string plant = "fan_tree";
        public Vector2 height = new Vector2(9f, 13f);
        public float minClearing = 2.6f;
    }

    [Serializable]
    public class AtmosphereSettings
    {
        public Color fogColor = new Color(0.09f, 0.19f, 0.25f);
        public float fogDensity = 0.018f;
        public Color ambientSky = new Color(0.34f, 0.55f, 0.68f);
        public Color ambientEquator = new Color(0.2f, 0.36f, 0.42f);
        public Color ambientGround = new Color(0.06f, 0.12f, 0.13f);
        public Color moonColor = new Color(0.72f, 0.86f, 1f);
        public float moonIntensity = 1.05f;
        public Vector3 moonEuler = new Vector3(48f, 35f, 0f);
        public Color rimLightColor = new Color(0.3f, 0.85f, 0.95f);
        public float rimLightIntensity = 0.35f;
        public float bloomIntensity = 2.2f;
        public float bloomThreshold = 0.85f;
        public float exposure = 0.75f;
        public int sporeCount = 900;
    }

    [Serializable]
    public class QualityPreset
    {
        public float plantCull = 125f;      // plants are not drawn beyond this (the night haze has swallowed them)
        public float bigPlantCull = 280f;   // tall trunks and trees stay a little longer, as silhouettes
        public string name;
        public float terrainStep = 0.7f;   // terrain vertex spacing (units)
        public float density = 1f;         // plant density multiplier
        public float shadowDistance = 45f;
        public float renderScale = 1f;
        public float drawDistance = 170f;
        public bool bloom = true;
        public bool ao;
        public int textureLimit;           // 1 = every texture at half resolution (saves video memory)
        public float parallax = 0f;        // parallax relief depth (units) near the camera; off: at the low ant-eye
                                           // camera angle it streaked into spikes (2026-10-11), the relief comes from
                                           // the soft normal maps, crevice shading, terrain mounds and 3D props instead
    }
}

namespace AntGarden
{
    /// <summary>A visual style for a region: ground, plants, glow, fog, sky and ant colours.</summary>
    [System.Serializable]
    public class Biome
    {
        public string id, displayName, displayNameEn;
        public string Name => Loc.T(displayName, string.IsNullOrEmpty(displayNameEn) ? displayName : displayNameEn);
        public string groundSet = "";                        // Art/Ground/<id> textures; empty = recolour the default ones
        public float terrainHue, terrainSaturation = 1f;      // hue rotation (degrees) applied to the ground textures
        public float mapHue, mapSaturation = 1f;              // the same for the map (its colours come from the blue palette)
        public Color floorTint = Color.white, bankTint = Color.white, plateauTint = Color.white, speckColor = Color.white;
        public float plantHue, plantSaturation = 1f;          // hue rotation for every plant in the region
        public Color plantGlow = Color.white, groundColor = Color.black;
        public float antHue, antSaturation = 1f;              // added to each species' own hue
        public Color fogColor, ambientSky, ambientEquator, ambientGround, moonColor, skyTop, skyHorizon;
        public Color rimColor = new Color(0.3f, 0.85f, 0.95f);   // the camera-following silhouette light
        public Color gateColor = Color.white;
        public string nativePlants = "";      // plants painted in this biome's own colours: never hue-shifted here
        public string landmarkPlant = "";     // junction-chamber landmark; empty = scatter.landmark.plant
        public string surroundings = "";      // silhouettes beyond the rim; empty = trunks and fan trees
    }

    /// <summary>Window and frame pacing. Frames are paced by the game, not the display (no vsync stalls).</summary>
    [Serializable]
    public class DisplaySettings
    {
        public int targetFps = 60;          // cap while the window is in front (120 Hz screens would otherwise run hot)
        public int backgroundFps = 15;      // cap while another app is in front
        public int windowWidth = 1280;      // the game opens in a window of this size; F toggles full screen
        public int windowHeight = 720;
    }

    /// <summary>Rolling relief added to the generated ground (world units).</summary>
    [System.Serializable]
    public class ReliefSettings
    {
        public float floor = 0.7f;       // swell amplitude on the canyon floor
        public float bank = 1.3f;        // swell amplitude on banks and plateau
        public float scale = 0.045f;     // swell frequency (1 / units)
        public float mid = 0.24f;        // mounds and hollows a few body lengths across
        public float midScale = 0.14f;
        public float bumps = 0.2f;       // small bump amplitude everywhere
        public float bumpScale = 0.3f;
        public float strata = 1f;        // banks gathered into soil ledges (0 = smooth slope, 1 = full terraces)
        public float strataPeriod = 1.4f;    // height of one soil layer (units)
    }

    /// <summary>Light gates: arches where stitched nests meet, and portals between separate regions.</summary>
    [System.Serializable]
    public class GateSettings
    {
        public float archScale = 1.15f;  // arch radius relative to the canyon half-width
        public float tube = 0.22f;       // arch thickness
        public float glow = 1.0f;        // HDR brightness of the arch (bloom does the rest)
        public float curtain = 0.25f;    // brightness of the light veil inside the arch
        public float lightIntensity = 0.9f;
        public float lightRange = 14f;
        public float portalRadius = 3.6f; // how close the ant must be to use a portal
    }
}

namespace AntGarden
{
    /// <summary>Glowing plants: how differently bright they are, and how they flare when an ant touches them.</summary>
    [System.Serializable]
    public class GlowSettings
    {
        public float dormantShare = 0.3f;    // plants that barely glow until touched
        public float memoryShare = 0.07f;    // memory plants: they keep a story left by a visitor
        public float dormantBoost = 0.1f;
        public float brightShare = 0.1f;     // plants clearly brighter than the rest
        public Vector2 brightBoost = new Vector2(1.4f, 2f);
        public Vector2 normalBoost = new Vector2(0.3f, 0.9f);
        public float hueJitter = 20f;        // degrees of glow-colour difference between plants
        public float touchReach = 0.15f;     // an antenna tip this close to a plant's footprint touches it
        public float talkReach = 0.25f;      // reaching out with Space (the antenna-touch clip) adds this much
        public float rise = 0.25f;           // seconds to flare up
        public float hold = 1.2f;            // seconds at full brightness
        public float fade = 3.5f;            // seconds to fade back
        public float ripple = 4f;            // neighbours within this distance flare too ...
        public float rippleAmount = 0.5f;    // ... this strongly ...
        public float rippleSpeed = 5f;       // ... the wave travelling this many units per second
        public int lights = 3;               // point lights lent to the brightest flares
        public float lightRange = 5f;
        public float lightIntensity = 2.2f;
    }
}

namespace AntGarden
{
    /// <summary>
    /// The colony's pheromone trails (ColonyTrails) and the NPCs' steering. Trail ants search for food along
    /// the "to food" pheromone, carry it home along "to home", and mark the way as they go; the busiest routes
    /// become highways. NPCs turn more gently than the player, so nobody spins on the spot.
    /// </summary>
    [System.Serializable]
    public class ColonySettings
    {
        public bool enabled = true;
        public float alpha = 1.6f;            // how strongly ants follow pheromone at a junction
        public float recruitedAlpha = 2.8f;   // ... after touching antennae with an ant that carries food
        public float recruitTime = 25f;
        public float straightness = 1.6f;     // preference for carrying straight on over sharp turns
        public float tau0 = 0.3f;             // how inviting an unmarked path is
        public float deposit = 1f;            // pheromone laid per walked path
        public float maxLevel = 60f;
        public float evaporation = 0.006f;    // per second: a trail nobody walks fades in a few minutes
        public float explore = 0.06f;         // chance to ignore the trail at a junction
        public int prewarmAnts = 60;          // load-time simulation that lays the first trails
        public int prewarmSteps = 400;
        public float prewarmStepSeconds = 20f;
        public Vector2 pickupPause = new Vector2(1.2f, 2.6f);  // seconds spent at food or at home
        public float turnRate = 3.2f;         // NPC steering (radians / second); the player turns quicker
        public float sprintTurnRate = 2.4f;
        public float turnGain = 4.5f;
        public float millCone = 75f;          // chamber ants pick their next spot within this angle ahead (degrees)
    }
}
