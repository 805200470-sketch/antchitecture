using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AntGarden.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AntGarden
{
    /// <summary>
    /// Bootstrap and main loop. Builds the whole world at runtime from recognized path networks: one or
    /// more separate regions (each a single nest or a stitched group of nests, each in its own visual
    /// style), joined by portal gates. Canyon terrain, plants, gates, the player's ant, NPC ants, camera,
    /// atmosphere and UI are all generated here.
    /// Command line: -world "stitch:cluster:path-001,path-002,path-003@blue|path-010@amber"   (see GameConfig.defaultWorld)
    ///               -quality low|medium|high   -capture &lt;dir&gt; (automated screenshots + FPS report, then quit)
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static string Selection;
        /// <summary>The particle nest experiment ("lab:path-012@blue"): chapter one, see LabNest.</summary>
        public static bool Lab => Selection != null && Selection.StartsWith("lab:");
        LabNest lab;
        public LabNest LabNest => lab;
        Transform worldRoot;
        public static string QualityName;
        /// <summary>The ruin scenes, each a single nest closed over into a tunnel (see RuinScene).</summary>
        public static readonly string[] RuinWorlds = { "path-003@blue#relics", "path-010@amber#temple" };

        public static readonly string[] StitchPresets =
        {
            "stitch:row:path-010,path-013",
            "stitch:row:path-006,path-010,path-013",
            "stitch:cluster:path-001,path-002,path-003",
        };

        public GameConfig cfg;
        public World world;
        public AntController player;
        public readonly List<NpcAnt> npcs = new List<NpcAnt>();
        public List<Gate> gates = new List<Gate>();
        public CameraRig rig;
        public Camera cam;
        public Hud hud;
        public BigMap bigmap;
        // demo recording: the script steers the ant instead of the keyboard
        public bool demo;
        public Vector2 demoMove;
        public bool demoFast;
        public Vector2? demoKeys;          // checks: W/A/S/D held, read against the camera like real keys
        public bool Ready { get; private set; }
        public QualityPreset Preset { get; private set; }

        public RuntimeRefs refs;
        ParticleSystem spores, burst;
        AtmosphereController atmosphere;
        Transform ring;
        Rings.Draped playerRing;
        ParticleLook particleLook;
        public ParticleLook Look => particleLook;   // checks
        int currentNest = -1, currentRegion = -1;
        float stepRun;              // ground walked since the last footstep sound
        float stepNext = 0.2f;      // and how far until the next one (uneven)
        Vector3 lastStep;
        bool dryGround;             // the warm nest's clay sounds crisper underfoot than the moss
        string lastPrompt;
        NpcAnt nearNpc;
        StorySlot nearSlot;
        RuinScene nearRuin;
        int nearRelic = -1;
        readonly List<RuinScene> ruins = new List<RuinScene>();
        public IReadOnlyList<RuinScene> Ruins => ruins;
        readonly List<Material> groundMats = new List<Material>();
        WExit nearExit;
        Gate nearPortal, nearWorldGate;
        bool focusNpc;              // E goes to the ant in front (not to a slot or exit that is further away)
        List<string> worlds;
        MapPainter painter;
        AntGrid antGrid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Debug.Log("[AntGarden] boot");
            Ensure();
            // switching world (an exit, the map's world buttons) or quality reloads the scene, which takes the Game
            // with it: build a new one in the fresh scene, or the screen stays empty
            SceneManager.sceneLoaded += (scene, mode) => Ensure();
        }

        static void Ensure()
        {
            if (FindObjectOfType<Game>() == null) new GameObject("Game").AddComponent<Game>();
        }

        public static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        /// <summary>The game now running (checks reach it from their coroutines).</summary>
        public static Game Instance { get; private set; }

        /// <summary>Checks (NestClock fixed): the floating spores as they would be at this moment, the same in every run
        /// (their own random seed, simulated from their start); ResumeSpores lets them play on.</summary>
        public void SettleSpores(float t)
        {
            if (spores == null) return;
            spores.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            spores.useAutoRandomSeed = false;
            spores.randomSeed = 20261027;
            spores.Simulate(t, true, true, true);
        }

        public void ResumeSpores()
        {
            if (spores != null) spores.Play(true);
        }

        void Start()
        {
            Instance = this;
            cfg = GameConfig.Load();
            // test overrides: the player's species, and procedural legs on or off
            cfg.ant.playerSpecies = Arg("-player") ?? cfg.ant.playerSpecies;
            if (Arg("-colony") != null) cfg.npcs.colony.enabled = Arg("-colony") != "0";
            if (Arg("-proclegs") != null) cfg.ant.proceduralLegs = Arg("-proclegs") != "0";
            if (Arg("-giveway") != null) NpcAnt.GiveWayOn = Arg("-giveway") != "0";   // test: NPCs that do not give way
            Loc.Init(Arg("-lang"));
            // frames are paced by the game, never by the display: a sleeping or covered screen cannot stall
            // the loop, and a 120 Hz laptop screen does not drive the GPU flat out (which overheated a
            // MacBook on battery within minutes)
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = cfg.display.targetFps;
            if (int.TryParse(Arg("-vsync"), out int vsync)) QualitySettings.vSyncCount = vsync;   // checks: paced by the display instead
            if (Arg("-uncapped") != null) Application.targetFrameRate = -1;   // checks: how fast it could run (the headroom)
            if (Arg("-mute") != null) AudioListener.volume = 0f;               // checks run at night: no sound at all
            // open in a window unless full screen was asked for on the command line (Unity would otherwise
            // reopen in whatever mode the last session ended in)
            if (Arg("-screen-fullscreen") != "1" && Screen.fullScreenMode != FullScreenMode.Windowed && !windowChecked)
                Screen.SetResolution(cfg.display.windowWidth, cfg.display.windowHeight, FullScreenMode.Windowed);
            windowChecked = true;
            Selection ??= Arg("-world") ?? cfg.defaultWorld;
            QualityName ??= Arg("-quality") ?? PlayerPrefs.GetString("antgarden.quality", "medium");
            Preset = cfg.Preset(QualityName);
            QualitySettings.globalTextureMipmapLimit = Preset.textureLimit;
            Debug.Log($"[AntGarden] start: world={Selection} quality={Preset.name}");
            StartCoroutine(Build());
            string capture = Arg("-capture") ?? Arg("-demo") ?? Arg("-photos") ?? Arg("-animcheck") ?? Arg("-lookcheck") ?? Arg("-npccheck") ?? Arg("-camcheck") ?? Arg("-sceneshots") ?? Arg("-legcheck") ?? Arg("-padcheck") ?? Arg("-trailfilm") ?? Arg("-interactshots") ?? Arg("-stuttercheck");
            if (!string.IsNullOrEmpty(capture)) StartCoroutine(Watchdog(capture));
        }

        /// <summary>Capture runs must always end: if the world is not ready in time, report why and quit.</summary>
        IEnumerator Watchdog(string dir)
        {
            float t = 0;
            while (!Ready && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
            if (Ready) yield break;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "capture_report.json"), $"{{ \"error\": \"not ready after {t:F0}s\", \"stage\": \"{lastStage}\" }}\n");
            Debug.LogError($"[AntGarden] not ready after {t:F0}s, stuck at: {lastStage}");
            Application.Quit(2);
        }

        string lastStage = "";

        void Stage(string label, float frac)
        {
            lastStage = label;
            hud?.SetProgress(label, frac);
            Debug.Log($"[AntGarden] {label} ({frac:P0}) t={Time.realtimeSinceStartup:F1}s");
        }

        void Fail(string message)
        {
            lastStage = "ERROR: " + message;
            Debug.LogError("[AntGarden] " + message);
            hud?.ShowError(message);
        }

        /// <summary>One region of a world selection: "path-NNN" or "stitch:layout:ids", with its biome.</summary>
        struct RegionSpec { public string spec, biome, scene; public List<(string id, string biome, string scene)> cells; }

        static List<RegionSpec> ParseWorld(string selection)
        {
            var list = new List<RegionSpec>();
            if (selection.StartsWith("lab:")) selection = selection.Substring(4);   // the particle nest: one nest
            // "honeycomb:path-001@blue,path-010@amber#temple,…": every module one hexagon of one stitched field
            if (selection.StartsWith("honeycomb:"))
            {
                var cells = new List<(string id, string biome, string scene)>();
                foreach (string m in selection.Substring(10).Split(','))
                {
                    string p = m.Trim(), biome = "blue", scene = null;
                    int hash = p.LastIndexOf('#');
                    if (hash >= 0) { scene = p.Substring(hash + 1); p = p.Substring(0, hash); }
                    int at = p.LastIndexOf('@');
                    if (at >= 0) { biome = p.Substring(at + 1); p = p.Substring(0, at); }
                    cells.Add((p, biome, scene));
                }
                list.Add(new RegionSpec { spec = "stitch:honeycomb:" + string.Join(",", cells.ConvertAll(c => c.id)), cells = cells });
                return list;
            }
            foreach (string part in selection.Split('|'))
            {
                string p = part.Trim();
                if (p.StartsWith("world:")) p = p.Substring(6);
                string biome = "blue", scene = null;
                int hash = p.LastIndexOf('#');
                if (hash >= 0) { scene = p.Substring(hash + 1); p = p.Substring(0, hash); }
                int at = p.LastIndexOf('@');
                if (at >= 0) { biome = p.Substring(at + 1); p = p.Substring(0, at); }
                if (p.Length > 0) list.Add(new RegionSpec { spec = p, biome = biome, scene = scene });
            }
            return list;
        }

        IEnumerator Build()
        {
            Shader.SetGlobalFloat("_ChevronOn", 0f);   // the particle nest's guiding pulses: only there
            Shader.SetGlobalFloat("_TrailPlayerD", -1f);
            hud = new Hud();
            Stage(Loc.T("读取路径数据…", "Reading the path data…"), 0.02f);
            yield return null;
            refs = RuntimeRefs.Load();
            if (refs == null) { Fail(Loc.T("缺少素材清单（RuntimeRefs），请在编辑器里运行 AntGarden/Build Assets", "Asset list (RuntimeRefs) missing: run AntGarden/Build Assets in the editor")); yield break; }
            worlds = ListWorlds(cfg.defaultWorld, cfg.labWorld);

            // 1 · path fields, one per region (main thread: Unity file APIs, and each takes well under a second)
            Stage(Loc.T("由路径计算峡谷…", "Carving the canyon from the paths…"), 0.05f);
            var specs = ParseWorld(Selection);
            world = new World(cfg.relief, cfg.relief != null ? 7 : 0);
            groundMats.Clear();
            foreach (var rs in specs)
            {
                yield return null;
                PathField field;
                int connectors, paths;
                string[] ids;
                try
                {
                    var texts = ReadNetworks(rs.spec);
                    // a ruin in a honeycomb is a tunnel: its canyons narrower than a nest's open halls
                    List<double> widths = rs.cells?.ConvertAll(c => c.scene != null ? (double)cfg.ruinWidth : 1.0)
                        ?? (rs.scene != null ? new List<double> { cfg.ruinWidth } : null)
                        ?? (Lab ? new List<double> { Arg("-labwidth") is string lw ? double.Parse(lw, System.Globalization.CultureInfo.InvariantCulture) : cfg.labWidth } : null);   // the particle nest: passages
                    // its passages swell and narrow, never wide (checks: -passages before keeps them as they were)
                    bool passages = Lab && rs.cells == null && rs.scene == null && Arg("-passages") != "before";
                    (field, connectors, paths, ids) = MakeField(rs.spec, texts, widths, passages ? LabPassages(cfg) : null);
                }
                catch (Exception e) { Fail(Loc.T($"路径数据出错（{rs.spec}）：", $"Bad path data ({rs.spec}): ") + e.GetBaseException().Message); yield break; }
                if (rs.cells != null)
                {
                    // one region per hexagon, all sharing the stitched field
                    var cells = rs.cells.ConvertAll(c => (c.id, cfg.GetBiome(c.biome), c.scene));
                    foreach (Region c in world.AddCells(field, cells))
                    {
                        c.connectors = connectors;
                        c.paths = paths;
                    }
                    continue;
                }
                Biome biome = cfg.GetBiome(rs.biome);
                Region added = world.AddRegion(field, ids, "", biome, cfg.regionGap);
                added.connectors = connectors;
                added.scene = rs.scene;
                added.paths = paths;
            }
            world.label = WorldLabel();
            var root = new GameObject("World").transform;
            worldRoot = root;
            world.glow = new GlowPlants(cfg.glow, root, 4242);

            // 2 · ground, one terrain material per region (its biome's palette)
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < world.regions.Count; i++)
            {
                Region r = world.regions[i];
                Stage(Loc.T("铺设峡谷地面", "Laying the canyon floor") + $" {i + 1}/{world.regions.Count}", 0.08f + 0.3f * i / world.regions.Count);
                var ground = new Material(refs.terrain);
                groundMats.Add(ground);
                // (checks: -palette before puts the honey granary and the leafcut garden back on the textures and turn they had)
                bool before = Lab && Arg("-palette") == "before" && (r.biome.id == "honey" || r.biome.id == "leafcut");
                // (a particle nest with textures of its own where its biome's are shared with the world outside the lab,
                // NestTheme.groundSet: the amber nest's, its biome's set the kiln's clay too)
                string setId = Lab && NestTheme.For(r.biome.id).groundSet is string ownSet ? ownSet : r.biome.groundSet;
                GroundSet set = refs.Ground(before ? (r.biome.id == "honey" ? "amber" : "blue") : setId);
                if (set != null)
                {
                    ground.SetTexture("_FloorTex", set.floor);
                    // (checks: a nest's new floor texture tried in the game before it goes in, -groundtrial <png> as
                    // Tools/make_ground_textures.py makes it, its height in alpha: the particle nest's points and carpet
                    // take only this one of the floors)
                    if (Lab && Arg("-groundtrial") is string trialFloor && File.Exists(trialFloor))
                    {
                        var tried = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
                        tried.LoadImage(File.ReadAllBytes(trialFloor));
                        ground.SetTexture("_FloorTex", tried);
                    }
                    ground.SetTexture("_BankTex", set.bank);
                    ground.SetTexture("_PlateauTex", set.plateau);
                    if (set.floorNormal != null) ground.SetTexture("_FloorNormal", set.floorNormal);
                    if (set.bankNormal != null) ground.SetTexture("_BankNormal", set.bankNormal);
                    if (set.plateauNormal != null) ground.SetTexture("_PlateauNormal", set.plateauNormal);
                    ground.SetFloat("_FloorScale", 0.14f);
                    ground.SetFloat("_BankScale", 0.1f);
                    ground.SetFloat("_PlateauScale", 0.07f);
                    // soil and stone: matte, relief from the height map (parallax near the camera), little
                    // extra fine detail so the ground reads as calm structure, not a field of tiny dots
                    ground.SetFloat("_FloorSmooth", 0.12f);
                    ground.SetFloat("_BankSmooth", 0.08f);
                    ground.SetFloat("_NormalStrength", 1.15f);
                    ground.SetFloat("_DetailMix", 0.1f);
                    ground.SetFloat("_ParallaxDepth", Preset.parallax);
                    ground.SetFloat("_GlowSpecks", 0f);
                    ground.SetFloat("_Cavity", 0.2f);      // gentle: strong crevice shading made the moss read as pits
                    // sister floor textures (if the set has them), mixed in large soft patches
                    bool variety = set.floor2 != null;
                    if (variety)
                    {
                        ground.SetTexture("_FloorTex2", set.floor2);
                        ground.SetTexture("_FloorTex3", set.floor3 != null ? set.floor3 : set.floor2);
                        if (set.floor2Normal != null) ground.SetTexture("_FloorNormal2", set.floor2Normal);
                        ground.SetTexture("_FloorNormal3", set.floor3Normal != null ? set.floor3Normal : set.floor2Normal);
                    }
                    ground.SetFloat("_FloorVariety", variety ? 1f : 0f);
                    ground.SetFloat("_VarietyScale", 0.06f);   // patches about 15 units across: two or three kinds in view
                }
                // (a particle nest made by NestPalette tints its textures itself: v15's tints turned to its hue)
                NestTheme tinted = Lab ? NestTheme.For(r.biome.id) : null;
                bool own = tinted != null && tinted.floorTexTint.a > 0;
                ground.SetColor("_FloorTint", own ? tinted.floorTexTint : r.biome.floorTint);
                ground.SetColor("_BankTint", own ? tinted.bankTexTint : r.biome.bankTint);
                ground.SetColor("_PlateauTint", own ? tinted.plateauTexTint : r.biome.plateauTint);
                ground.SetColor("_GlowColor", r.biome.speckColor);
                // (checks: -groundhue N turns a particle nest's ground N degrees more, to try its ground's hue without a build)
                float groundTried = Lab && Arg("-groundhue") is string gh && float.TryParse(gh, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ghv) ? ghv : 0f;
                ground.SetFloat("_HueShift", before ? (r.biome.id == "honey" ? -25f : 90f) : r.biome.terrainHue + groundTried);
                ground.SetFloat("_Saturation", r.biome.terrainSaturation);
                int ri = i;
                if (r.cell) continue;   // a honeycomb's cells are built together below
                yield return TerrainBuilder.Build(world, r, ground, refs.soilWall, root, Preset.terrainStep, 7f,
                    p => hud.SetProgress(Loc.T("铺设峡谷地面", "Laying the canyon floor"), 0.08f + 0.3f * (ri + p) / world.regions.Count));
            }
            var honeycomb = world.regions.FindAll(r => r.cell);
            if (honeycomb.Count > 0)
                yield return TerrainBuilder.BuildCells(world, honeycomb, groundMats, refs.soilWall, root, Preset.terrainStep, 7f,
                    p => hud.SetProgress(Loc.T("铺设峡谷地面", "Laying the canyon floor"), 0.08f + 0.3f * p),
                    cfg.seamBlend ? refs.terrainSeam : null);
            Debug.Log($"[AntGarden] terrain {Time.realtimeSinceStartup - t0:F2}s");

            // 3 · story slots, ruin scenes (their ground is kept free), plants, gates
            PlaceSlots(root);
            ruins.Clear();
            foreach (Region r in world.regions)
            {
                if (string.IsNullOrEmpty(r.scene) || Lab) continue;   // (a particle nest's props are its theme's: LabNest)
                var ruin = new RuinScene(world, r, refs, r.scene, 4321 + r.index);
                ruin.Plan();
                world.roofs.Add(ruin.roof);
                ruins.Add(ruin);
            }
            lab = null;
            NestTheme.Current = null;
            if (Lab)
            {
                // the particle nest is drawn in its colour segment's theme: its region's biome picks it
                NestTheme.Current = NestTheme.For(world.regions[0].biome.id);
                // no nest has a heart now (the user, 2026-10-27: "不要用心脏的概念了"): nothing stands at the way's end,
                // which is its way out (LabNest.Plan); a nest's landmarks stand beside the way. (-heart <kind> still
                // shows one, for checks)
                if (Arg("-heart") == null) NestTheme.Current.heart = null;
                lab = new LabNest(world, world.regions[0], refs);
                var sp = world.SpawnPoint(0);
                lab.Plan(new Vector2(sp.x, sp.z));
                lab.PlaceObjects(root);
            }
            // the particle nest grows five more kinds of plant (from the amber nest), so its forest is more varied
            if (Lab)
                foreach (ScatterRule rule in cfg.scatter.rules)
                    if (GameConfig.InList(cfg.labExtraPlants, rule.plant) && !string.IsNullOrEmpty(rule.biomes) && !GameConfig.InList(rule.biomes, "blue"))
                        rule.biomes += ",blue";
            // and its own plants, from high-precision models (2026-10-20): four more curled kinds beside the one it had,
            // lantern pods, a branching coral and feather fronds; fewer of that one curled kind and of the common
            // glass-bulbed stalk, so no kind repeats all along the way
            if (Lab)
            {
                foreach (ScatterRule rule in cfg.scatter.rules)
                {
                    if (rule.plant == "curl_glow") rule.spacing *= 1.8f;
                    if (rule.plant == "spore_stalk") rule.spacing *= 1.5f;
                }
                // the world's plants that grow in this nest (its theme's: the amber nest has no glass bulbs or lollipop
                // curls, the temple only a few dry things): the others are kept out of it, those it has grow in its biome
                NestTheme theme = NestTheme.Current;
                string biomeId = world.regions[0].biome.id;
                if (theme.scatterPlants != null)
                    foreach (ScatterRule rule in cfg.scatter.rules)
                    {
                        if (System.Array.IndexOf(theme.scatterPlants, rule.plant) < 0) rule.biomes = "none";
                        else if (!string.IsNullOrEmpty(rule.biomes) && !GameConfig.InList(rule.biomes, biomeId)) rule.biomes += "," + biomeId;
                        // (and as sparse as the nest wants them: NestTheme.sparserKinds)
                        int sparse = System.Array.IndexOf(theme.sparserKinds, rule.plant);
                        if (sparse >= 0) rule.spacing *= theme.sparserBy[sparse];
                    }
                // the nest's finely made plants first, so they find room round the way before the rest fill it (last,
                // they found only one to four spots each); where they grow, the old smooth lollipop curls give way
                // to the fine croziers
                // (a nest with many kinds of them, the blue forest's nine, has each kind sparser: about as many in all)
                var rules = new List<ScatterRule>();
                float sparser = Mathf.Pow(Mathf.Max(1f, theme.labPlants.Length / 3f), 0.75f);
                foreach (ScatterRule rule in LabPlants())
                    if (System.Array.IndexOf(theme.labPlants, rule.plant) >= 0)
                    {
                        rule.spacing *= sparser;
                        // (as the nest wants them: NestTheme.labTweaks, and its sparserKinds as for the world's plants)
                        if (theme.labTweaks != null && theme.labTweaks.TryGetValue(rule.plant, out Vector4 tw))
                        {
                            rule.tuft = new Vector2Int(rule.tuft.x + (int)tw.x, rule.tuft.y + (int)tw.x);
                            if (tw.y > 0f) rule.dMin = Mathf.Max(rule.dMin, tw.y);
                            if (tw.z > 0f) rule.dMax = Mathf.Min(rule.dMax, tw.z);
                            if (tw.w > 0f) rule.height *= tw.w;
                        }
                        int sparse = System.Array.IndexOf(theme.sparserKinds, rule.plant);
                        if (sparse >= 0) rule.spacing *= theme.sparserBy[sparse];
                        rules.Add(rule);
                    }
                bool fineCurls = System.Array.IndexOf(theme.labPlants, "curl_a_crozier") >= 0;
                foreach (ScatterRule rule in cfg.scatter.rules)
                {
                    if (fineCurls && rule.plant == "curl_glow") rule.biomes = "none";
                    rules.Add(rule);
                }
                cfg.scatter.rules = rules.ToArray();
            }
            int placed = 0;
            for (int i = 0; i < world.regions.Count; i++)
            {
                Region r = world.regions[i];
                Stage(Loc.T("让植物长出来", "Growing the plants") + $" {i + 1}/{world.regions.Count}", 0.4f + 0.35f * i / world.regions.Count);
                float density = Preset.density * (r.ids.Length >= 3 ? 0.85f : 1f);
                var scatter = new Scatter(world, r, refs, cfg.scatter, density, 1234 + 97 * i, root);
                int ri = i;
                yield return scatter.Run(p => hud.SetProgress(Loc.T("让植物长出来", "Growing the plants"), 0.4f + 0.35f * (ri + p) / world.regions.Count));
                placed += scatter.Placed;
            }
            gates = Gates.Build(world, refs, cfg.gates, root);
            BuildWorldGates(root);
            foreach (RuinScene ruin in ruins) ruin.Build(groundMats[ruin.region.index], root, Preset.terrainStep * 1.6f, 99 + ruin.region.index, cfg.trailBrightness, cfg.trailDensity);
            // the seams between the nests of a honeycomb: motes rising off them
            if (honeycomb.Count > 1) SeamMotes.Build(world, honeycomb, refs.guideTrail, cfg.seamMotes, cfg.seamMoteDensity, root, 777);
            lab?.Build(root, cfg.trailBrightness, cfg.trailDensity);
            // memory plants tell their story when an antenna lights them
            if (world.glow != null)
            {
                world.glow.onFlare = at => { Sfx.Flare(at); Pad.Rumble(0.05f, 0.12f, 0.07f); lab?.OnPlantLit(hud); lab?.RippleAt(at); };
                world.glow.onStory = (k, at) =>
                {
                    hud.ShowLine(Loc.T("留在这里的话 · 一位访客", "Left here · by a visitor"), Voices.T(Voices.Stories[k % Voices.Stories.Length]));
                    Voices.Chime(at + Vector3.up, 1.9f, 0.35f);
                };
            }
            Debug.Log($"[AntGarden] plants {placed}, colliders {world.colliders.Count}, gates {gates.Count}");

            // 4 · camera, atmosphere
            Stage(Loc.T("点亮夜色…", "Lighting the night…"), 0.8f);
            yield return null;
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            rig = cam.gameObject.AddComponent<CameraRig>();
            // the particle nest: a longer lens, further back (62° stretched the near ground and the edges of the frame)
            if (Lab) { cfg.camera.fov = 52f; cfg.camera.distance = 4.2f; }
            rig.Init(cam, world, cfg.camera);
            rig.classic = Arg("-camstyle") == "classic";   // checks: the previous camera, for comparison
            Sfx.Init(cfg.audio, cam);
            Pad.rumble = cfg.padRumble;
            if (Arg("-sfxdump") != null) { Sfx.Dump(Arg("-sfxdump")); Application.Quit(); }
            if (Lab) { cfg.atmosphere.exposure -= 0.28f - NestTheme.Current.exposureLift; cfg.atmosphere.fogDensity *= 1.35f; }   // the particle nest: a darker night, still lit
            spores = Atmosphere.Apply(cam, refs, cfg.atmosphere, Preset);
            if (Lab)
            {
                // the particle nest's white balance from its theme (a cool grade turns a warm nest's gold green)
                var volume = FindObjectOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
                if (volume != null && volume.profile != null && volume.profile.TryGetSettings(out UnityEngine.Rendering.PostProcessing.ColorGrading grading))
                {
                    grading.temperature.Override(NestTheme.Current.temperature);
                    grading.saturation.Override(NestTheme.Current.saturation);
                }
                if (volume != null && volume.profile != null && volume.profile.TryGetSettings(out UnityEngine.Rendering.PostProcessing.Bloom bloom))
                    bloom.color.Override(NestTheme.Current.bloomTint);
            }
            if (Lab && spores != null)
            {
                // its motes in the nest's own colour
                var sr = spores.GetComponent<ParticleSystemRenderer>();
                sr.material = new Material(sr.sharedMaterial);
                // pale, in the nest's own light; as bright on screen as v15's whatever the nest's exposure (lifted two stops,
                // they were a white snow over everything: NestTheme.LightComp)
                Color motes = NestTheme.Current.motes;
                float motesComp = NestTheme.LightComp(1f);
                sr.material.SetColor("_Color", new Color(motes.r * motesComp, motes.g * motesComp, motes.b * motesComp, motes.a));
                // (a nest made by NestPalette: the spores' own range of colours turned to it too)
                if (NestTheme.Current.sporeA.a > 0)
                {
                    var main = spores.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(NestTheme.Current.sporeA.r, NestTheme.Current.sporeA.g, NestTheme.Current.sporeA.b, 0.6f),
                        new Color(NestTheme.Current.sporeB.r, NestTheme.Current.sporeB.g, NestTheme.Current.sporeB.b, 0.6f));
                }
            }
            // plants beyond the haze are not drawn at all (a honeycomb puts many nests in view at once)
            var cull = new float[32];
            cull[Scatter.PlantLayer] = Preset.plantCull;
            cull[Scatter.BigPlantLayer] = Preset.bigPlantCull;
            cam.layerCullDistances = cull;
            cam.layerCullSpherical = true;
            atmosphere = cam.GetComponent<AtmosphereController>();
            if (Lab && atmosphere != null)
            {
                // the particle nest: a deep mist in its theme's colour, so depth reads as haze, not as nothing
                atmosphere.fogTint = NestTheme.Current.fog;
                // and its sky a deep night in that colour, not black (the zenith toward the horizon's colour; a big black
                // sky over the walls read as a hole: "不要出现这样的一大片黑")
                Color top = NestTheme.Current.skyTop, horizon = NestTheme.Current.skyHorizon;
                // (lifted further it read as a khaki haze: "天空可以黑一些")
                atmosphere.skyTopTint = Color.Lerp(top, horizon, 0.6f) * (1.6f * NestTheme.Current.skyGain);
                atmosphere.skyHorizonTint = horizon * (1.4f * NestTheme.Current.skyGain);
            }
            atmosphere?.SetBiome(world.regions[0].biome, true);

            // 5 · ants
            Stage(Loc.T("放入蚂蚁…", "Releasing the ants…"), 0.86f);
            yield return null;
            SpawnPlayer();
            SpawnNpcs();
            if (Lab) gameObject.AddComponent<ParticleAnts>().Init(npcs, player, refs);   // the ants as points too, the player's own
            burst = MakeBurst();

            // 6 · UI
            Stage(Loc.T("绘制地图…", "Drawing the map…"), 0.94f);
            painter = new MapPainter(world, gates);
            hud.BuildMinimap(painter);
            bigmap = new BigMap(hud.canvas, painter, world, cfg, gates, Teleport, TeleportToGate, SwitchWorld, worlds);
            hud.SetHelp(world.label);
            if (Lab) hud.SetHelpVisible(false);   // chapter one: one goal at a time instead
            if (Arg("-controls") != null) hud.SetControlsVisible(true);   // showing the game to people: the pad's controls top left
            hud.HideLoading();
            Ready = true;
            Debug.Log($"[AntGarden] ready: {world.label}");
            string capture = Arg("-capture");
            if (!string.IsNullOrEmpty(capture)) StartCoroutine(CaptureMode.Run(this, capture));
            string demoDir = Arg("-demo");
            if (!string.IsNullOrEmpty(demoDir)) StartCoroutine(DemoMode.Run(this, demoDir));
            string photoDir = Arg("-photos");
            if (!string.IsNullOrEmpty(photoDir)) StartCoroutine(PhotoMode.Run(this, photoDir));
            string animDir = Arg("-animcheck");
            if (!string.IsNullOrEmpty(animDir)) StartCoroutine(AnimCheck.Run(this, animDir));
            string lookDir = Arg("-lookcheck");
            if (!string.IsNullOrEmpty(lookDir)) StartCoroutine(LookCheck.Run(this, lookDir));
            string npcDir = Arg("-npccheck");
            if (!string.IsNullOrEmpty(npcDir)) StartCoroutine(NpcCheck.Run(this, npcDir));
            string camDir = Arg("-camcheck");
            if (!string.IsNullOrEmpty(camDir)) StartCoroutine(CamCheck.Run(this, camDir));
            string shotDir = Arg("-sceneshots");
            if (!string.IsNullOrEmpty(shotDir)) StartCoroutine(SceneShots.Run(this, shotDir));
            string filmDir = Arg("-trailfilm");
            if (!string.IsNullOrEmpty(filmDir)) StartCoroutine(TrailFilm.Run(this, filmDir));
            string interactDir = Arg("-interactshots");
            if (!string.IsNullOrEmpty(interactDir)) StartCoroutine(InteractShots.Run(this, interactDir));
            string stutterDir = Arg("-stuttercheck");
            if (!string.IsNullOrEmpty(stutterDir)) StartCoroutine(StutterCheck.Run(this, stutterDir));
            string legDir = Arg("-legcheck");
            if (!string.IsNullOrEmpty(legDir)) StartCoroutine(LegCheck.Run(this, legDir));
            // check: -switchto <world> leaves for that world once ready (as the map's buttons do), and quits when
            // the new world is ready too, so a log shows whether a scene reload builds the next world
            string switchTo = Arg("-switchto");
            if (!string.IsNullOrEmpty(switchTo))
            {
                if (Selection == switchTo) { Debug.Log("[AntGarden] switchcheck ok: " + switchTo); Application.Quit(); }
                else StartCoroutine(SwitchLater(switchTo));
            }
            string padDir = Arg("-padcheck");
            if (!string.IsNullOrEmpty(padDir)) StartCoroutine(PadCheck.Run(this, padDir));
        }

        static string[] SelectionIds(string spec) => spec.StartsWith("stitch:") ? spec.Split(':')[2].Split(',') : new[] { spec };

        static Dictionary<string, string> ReadNetworks(string spec)
        {
            var texts = new Dictionary<string, string>();
            foreach (string id in SelectionIds(spec)) texts[id] = File.ReadAllText(PathFile(id));
            return texts;
        }

        /// <summary>The world's name in the current language: each region's biome, nests and path counts.</summary>
        string WorldLabel()
        {
            var parts = new List<string>();
            foreach (Region r in world.regions)
            {
                string ids = string.Join(" + ", Array.ConvertAll(r.ids, s => s.Replace("path-", "")));
                r.label = !string.IsNullOrEmpty(r.scene) ? RuinScene.Title(r.scene) + Loc.T($" · 蚁穴 {ids}", $" · nest {ids}")
                    : r.ids.Length > 1
                    ? Loc.T($"{r.biome.Name} · 拼接 {ids} · {r.connectors} 条接口通道", $"{r.biome.Name} · stitched {ids} · {r.connectors} connectors")
                    : Loc.T($"{r.biome.Name} · 蚁穴 {ids} · {r.paths} 条路径", $"{r.biome.Name} · nest {ids} · {r.paths} paths");
                parts.Add(r.label);
            }
            if (world.regions.Count > 4)
            {
                int nests = 0, ruinCount = 0;
                foreach (Region r in world.regions) { if (!string.IsNullOrEmpty(r.scene)) ruinCount++; else nests += r.ids.Length; }
                return Loc.T($"蚁穴群岛 · {nests} 个蚁穴 · {ruinCount} 处遗迹", $"The Archipelago · {nests} nests · {ruinCount} ruins");
            }
            return string.Join("  |  ", parts);
        }

        /// <summary>Pure computation: no Unity API calls in here. Returns the field, connector and path counts, ids.
        /// A single nest's config is handed to <paramref name="passages"/> last, after widthScale (the particle nest's passages).</summary>
        static (PathField, int, int, string[]) MakeField(string spec, Dictionary<string, string> texts, IList<double> widthScale = null,
            Action<PathConfig> passages = null)
        {
            var pc = PathConfig.Default();
            if (spec.StartsWith("stitch:"))
            {
                string[] parts = spec.Split(':');
                string layout = parts[1];
                string[] ids = parts[2].Split(',');
                var nets = new List<NetworkData>();
                foreach (string id in ids) nets.Add(Turned(NetworkData.Parse(texts[id]), id));
                var (field, report) = Stitcher.Stitch(nets, ids, pc, layout, widthScale);
                return (field, report.Connectors, field.Edges.Count, ids);
            }
            var net = Turned(NetworkData.Parse(texts[spec]), spec);
            if (widthScale != null && widthScale[0] != 1)
            {
                // a ruin on its own: the same narrow tunnels and small rooms as in the honeycomb
                double k = widthScale[0];
                pc.FixedWidth *= k; pc.MinHalfWidth *= k; pc.MaxHalfWidth *= k;
                pc.MinWidth *= k; pc.MaxWidth *= k; pc.ChamberMin *= k; pc.ChamberMax *= k;
                pc.JunctionClearing *= k; pc.EndpointClearing *= k;
            }
            passages?.Invoke(pc);
            var f = new PathField(net, pc);
            return (f, 0, f.Edges.Count, new[] { spec });
        }

        /// <summary>
        /// The particle nest's passages (GameConfig.labPassage*): not one width all along and never wide. Each passage
        /// its own width, swelling into bays and narrowing into squeezes along its length (the core's width noise,
        /// stronger and a little more often than elsewhere, so it reads), and no wider than labPassageMax across. The
        /// narrowest squeeze (MinHalfWidth) and the chambers at the forks and dead ends (ChamberMin) keep what labWidth
        /// gives them. Measured without Unity: floor 3.5 to 7.9 units across, 5.4 on average (it was 3.5 to 9.0, 5.9).
        /// </summary>
        static Action<PathConfig> LabPassages(GameConfig cfg) => pc =>
        {
            pc.EdgeWidthRange = new double[] { cfg.labPassageSpread.x, cfg.labPassageSpread.y };
            pc.WidthVariation = cfg.labPassageSwell;
            pc.WidthFrequency = cfg.labPassageWave;
            pc.MaxHalfWidth = Math.Max(pc.MinHalfWidth, Math.Min(pc.MaxHalfWidth, cfg.labPassageMax / 2.0));
            pc.TailWidths = true;   // the straight runs out to the frame swell and narrow too
        };

        static string PathFile(string id) => Path.Combine(Application.streamingAssetsPath, "Paths", Drawing(id) + ".json");

        /// <summary>A path id's drawing: "path-011~m" is path-011 mirrored left to right, "~v" upside down, "~r" turned half
        /// round (so nests whose drawings look alike, two loops or two forks, lie differently).</summary>
        static string Drawing(string id)
        {
            int t = id.IndexOf('~');
            return t < 0 ? id : id.Substring(0, t);
        }

        /// <summary>The network as its id turns it (Drawing): its points mirrored within the image, so its tails still run
        /// off the image's edges.</summary>
        static NetworkData Turned(NetworkData net, string id)
        {
            int t = id.IndexOf('~');
            if (t < 0) return net;
            string how = id.Substring(t + 1);
            bool fx = how.Contains("m") || how.Contains("r"), fy = how.Contains("v") || how.Contains("r");
            double w = net.ImageWidth, h = net.ImageHeight;
            if (net.Nodes != null)
                foreach (NetworkNode n in net.Nodes)
                {
                    if (fx) n.X = w - n.X;
                    if (fy) n.Y = h - n.Y;
                }
            if (net.Edges != null)
                foreach (NetworkEdge e in net.Edges)
                    if (e.Points != null)
                        foreach (double[] q in e.Points)
                        {
                            if (fx) q[0] = w - q[0];
                            if (fy) q[1] = h - q[1];
                        }
            return net;
        }

        static List<string> ListWorlds(string defaultWorld, string labWorlds)
        {
            var list = new List<string> { defaultWorld };
            list.AddRange(RuinWorlds);
            if (!string.IsNullOrEmpty(labWorlds))
                foreach (string w in labWorlds.Split(','))
                    if (!string.IsNullOrWhiteSpace(w)) list.Add(w.Trim());
            string dir = Path.Combine(Application.streamingAssetsPath, "Paths");
            var singles = new List<string>();
            if (Directory.Exists(dir))
                foreach (string f in Directory.GetFiles(dir, "path-*.json"))
                    singles.Add(Path.GetFileNameWithoutExtension(f));
            singles.Sort(StringComparer.Ordinal);
            list.AddRange(singles);
            list.AddRange(StitchPresets);
            return list;
        }

        void SpawnPlayer()
        {
            AntRef antRef = refs.Ant(cfg.ant.playerSpecies) ?? (refs.ants.Count > 0 ? refs.ants[0] : null);
            player = new GameObject("Player").AddComponent<AntController>();
            if (antRef != null && antRef.prefab != null) player.Init(world, cfg.ant, antRef, cfg.ant.rimColor, cfg.ant.glowColor);
            else player.InitPlaceholder(world, cfg.ant, refs.slotRing);
            var sp = world.SpawnPoint(0);
            player.Place(sp.x, sp.z, sp.heading);
            rig.SnapBehind(player.heading);
            // coming through a gate from another world: stand just inside the gate that leads back there
            Gate back = ArriveFrom != null ? gates.Find(g => g.world == ArriveFrom) : null;
            if (back != null) TeleportToGate(back, false);
            // a soft ring under the player's ant tells it apart from the NPC ants
            playerRing = new Rings.Draped("PlayerRing", 0.58f, 0.66f, refs.slotRing, (NestTheme.Current != null ? NestTheme.Current.ring : new Color(0.5f, 0.9f, 0.95f)) * 0.8f);
            ring = playerRing.go.transform;
            // experiment: the particle look (P toggles)
            particleLook = gameObject.AddComponent<ParticleLook>();
            particleLook.Init(world, player.transform, refs.particleLook);
            // -particlelook soft: the compromise (meshes keep most of their colour, a layer of points on them)
            if (Arg("-particlelook") == "soft") { particleLook.plantDim = 0.45f; particleLook.groundDim = 0.15f; particleLook.scanMix = 0.45f; }
            if (Lab)
            {
                // the particle nest: no meshes, only dense points, as in Dream, keeping their textures' grain and
                // some of their colour; one family of near colours with a few muted accents, from its theme
                NestTheme theme = NestTheme.Current;
                particleLook.mode = ParticleLook.Mode.Replace;
                particleLook.pointSize = 0.011f;
                particleLook.brightness = 0.34f;   // a fifth darker than before: the guides and lit plants stand out
                particleLook.glowBrightness = 0.55f;   // and breathing: dim most of the time, bright when touched
                particleLook.shimmer = 0.006f;
                particleLook.scanOverride = new Color(theme.scan.r, theme.scan.g, theme.scan.b, 1f);
                particleLook.scanMix = 0.75f;   // the plants' own colours mostly under the one palette
                particleLook.grain = 1f;
                particleLook.bodyPalette = theme.body;
                particleLook.glowPalette = theme.glow;
                particleLook.bodyAccent = theme.bodyAccent;
                particleLook.glowAccent = theme.glowAccent;   // a muted accent now and then, never a bright one
                // plants: dense along the ways, fewer away from them
                particleLook.farShare = 0.2f;
                // the ground: sparse points over the nest, dense round the ant, and underfoot the carpet, made on the GPU;
                // by layer the floor, the walls, the plateau, a pale edge along the walls' tops. The terrain behind the
                // points a dark surface in the nest's base colour, not a void (sunk to 1.5 % it read as large black holes
                // beyond the carpet: "不要出现这样的一大片黑"); a warm world's lighter
                // the environment lit as a whole, not its far points (the user, 10-29: "远处这些点都太亮了。我要提亮环境，是指
                // 整个环境提亮，而不是把远处的点提亮"): a nest whose night is lifted above v15's sends a share of its ground
                // points' light to the surface behind them, so a far wall reads as a lit wall with grain on it rather than a
                // glitter of points on a dark one (none in v15; about 0.6 in a nest lifted two stops more)
                // (at least 0.4 in a nest made by NestPalette: the kiln temple, lifted only half a stop, kept its walls' pale glitter)
                float toSurface = Mathf.Max(1f - NestTheme.LightComp(0.6f), theme.segment >= 0 ? 0.4f : 0f);
                particleLook.groundDim = 1f - 0.1f * theme.groundGain * (1f + 2f * toSurface);
                particleLook.groundBrightness = 2.6f * theme.groundGain * (1f - toSurface);
                Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
                particleLook.groundColour = Opaque(theme.ground);
                particleLook.floorColour = Opaque(theme.floor);
                particleLook.bankColour = Opaque(theme.bank);
                particleLook.plateauColour = Opaque(theme.plateau);
                particleLook.edgeColour = Opaque(theme.edge);
                particleLook.groundTexMix = theme.groundTexMix;
                particleLook.groundNear = 160f;
                particleLook.groundBudget = 1000000;
                particleLook.shellDensity = 1000f;
                particleLook.shellFade = new Vector2(5f, 12f);
                particleLook.carpet = refs.particleCarpet;
                particleLook.carpetGain = 0.085f * theme.groundGain;   // opaque splats: the floor's own colour, dark (the things on it stand out)
                // and the walls round the ant as opaque splats too, in their own colour (the carpet lies on the floor only:
                // up close the walls were the bare terrain, then a snowfall of their glowing points), about as light as the
                // walls further off: a wall's ground colour is about four times a floor's, and the solid shader lights a
                // wall with its fill and the moon at a slant (at half the carpet's gain over that they were a pale plaster,
                // eight times the floor; at 0.2 a little lighter than the walls further off, OKLab L 0.36-0.39 for 0.26-0.33)
                particleLook.wallDensity = 4500f;
                float wallLight = (theme.fill.grayscale + 0.3f * theme.moon.grayscale) * 0.62f;   // (SolidThings.gain)
                particleLook.wallGain = 0.13f * theme.wallGain * particleLook.carpetGain / Mathf.Max(0.05f, wallLight);
                Debug.Log($"[AntGarden] walls round the ant: {particleLook.wallDensity:F0} splats per unit², gain {particleLook.wallGain:F3} (light {wallLight:F3})");
                particleLook.dust = 18f * theme.dustGain;
                // the floor's things are solid (SolidThings): leaves, twigs, moss, mushrooms... at ant scale; and the
                // plants, drawn the same way, with the same grain
                particleLook.solidPlants = true;
                particleLook.glassKinds = theme.glassKinds;
                for (int i = 0; i < theme.headGlowKinds.Length; i++) particleLook.glowFrom[theme.headGlowKinds[i]] = theme.headGlowFrom[i];
                if (Arg("-glass") == "0") particleLook.glassKinds = null;   // test: glass-bulbed plants drawn as plain ones
                particleLook.shellPlantArea = 0f;
                particleLook.splatShare = 0.24f;   // like a splat capture: soft larger splats under the fine points
                // the walls and floor become points too, densest along the glowing ways
                if (lab != null) particleLook.focus = lab.Tracks;
                foreach (Transform t in worldRoot)
                    if (t.name.StartsWith("Terrain_"))
                        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>())
                            if (mf.name != "SoilWalls") particleLook.ground.Add(mf);
            }
            if (Lab || cfg.particleLook || Arg("-particlelook") != null) particleLook.SetOn(true);
            // the particle nest: the things on the floor, solid, at ant scale; and the pheromones, a sprite of arrows
            // of light leading the ant
            // the guiding light first: its vines are drawn with the floor's things
            GuideThreads guide = Lab && lab != null ? gameObject.AddComponent<GuideThreads>() : null;
            guide?.Init(lab, player.transform, refs.lightThread);
            if (Lab && particleLook.On)
            {
                guide?.VineSplats(particleLook.extraSolids, particleLook.bodyPalette, particleLook.glowPalette);
                // its gates as points too (Gates.ArchSplats), in its own light
                foreach (Gate g in gates)
                    if (g.world != null) Gates.ArchSplats(g, cfg.gates.tube, NestTheme.Current.threadHalo.linear * 0.75f, particleLook.extraSolids, gates.IndexOf(g) * 7919 + 13);
                gameObject.AddComponent<SolidThings>().Init(world, particleLook, refs.particleSolid, player.transform, lab);
                gameObject.AddComponent<WalkLight>().Init(player.transform);   // what the ant walks past lights up
                // a light inside every glass bulb, through its broken shell; motes circling the finely made plants
                if (particleLook.glassLamps.Count > 0 || particleLook.orbitLamps.Count > 0)
                    gameObject.AddComponent<GlassLamps>().Init(particleLook.glassLamps, refs.lightThread, particleLook.orbitLamps);
                gameObject.AddComponent<MicroLife>().Init(world, particleLook, refs.microLife);   // the small life
            }
            if (Lab && lab != null) lab.StartField();
        }

        /// <summary>The particle nest's own plants: where they grow from the way, how far apart, how tall. Spread at
        /// random round the way, close to it (not in patches), so they are what the ant walks past. The thin ones grow
        /// in tufts (a fern's fronds from one crown, croziers, corkscrews, puffballs and hooks in small groups): one alone
        /// is flat, a tuft has a body. (tuftFace -90: the crozier and hook lean toward their model's -x, turned to lean out.)</summary>
        static ScatterRule[] LabPlants() => new[]
        {
            // (about 1.4 times the size they first had: giant plants to an ant, big enough on the screen for their detail)
            new ScatterRule { plant = "curl_a_crozier", dMin = -0.3f, dMax = 3.6f, spacing = 8f, height = new Vector2(1.8f, 3.8f), tilt = 0.2f, tintJitter = 0.08f, patchiness = 0.25f,
                tuft = new Vector2Int(3, 5), tuftLean = 12f, tuftFace = -90f, tuftScale = new Vector2(0.55f, 1f) },
            new ScatterRule { plant = "curl_b_double", dMin = -0.2f, dMax = 3.8f, spacing = 7f, height = new Vector2(2.2f, 4.4f), tilt = 0.2f, tintJitter = 0.08f, patchiness = 0.25f },
            new ScatterRule { plant = "curl_c_hook", dMin = -0.3f, dMax = 3.6f, spacing = 8f, height = new Vector2(2.0f, 3.9f), tilt = 0.2f, tintJitter = 0.08f, patchiness = 0.25f,
                tuft = new Vector2Int(2, 3), tuftLean = 10f, tuftFace = -90f, tuftScale = new Vector2(0.7f, 1f) },
            new ScatterRule { plant = "curl_d_cluster", dMin = -0.5f, dMax = 3.4f, spacing = 7f, height = new Vector2(1.6f, 3.1f), tilt = 0.35f, tintJitter = 0.1f, patchiness = 0.25f },
            new ScatterRule { plant = "puff_b_lantern", dMin = -0.2f, dMax = 3.8f, spacing = 7.5f, height = new Vector2(2.2f, 4.2f), tilt = 0.15f, tintJitter = 0.08f, patchiness = 0.25f },
            new ScatterRule { plant = "coral_b_branching", dMin = -0.6f, dMax = 3.4f, spacing = 6.5f, height = new Vector2(1.3f, 2.5f), tilt = 0.4f, tintJitter = 0.1f, cluster = new Vector2Int(1, 2), patchiness = 0.25f },
            // (the frond's blade faces its model's +z, its leaflets along x: turned to face out, so the leaflets spread round
            // the clump and the blades open upward as they lean, as a real fern's; not edge-on, leaflets pointing in and out)
            new ScatterRule { plant = "frond_a_feather", dMin = -0.2f, dMax = 3.8f, spacing = 9f, height = new Vector2(2.0f, 3.8f), tilt = 0.25f, tintJitter = 0.1f, patchiness = 0.25f,
                tuft = new Vector2Int(5, 8), tuftLean = 22f, tuftFace = 0f, tuftFlip = true, tuftScale = new Vector2(0.4f, 1f) },   // (leaning 34° the outer blades read as growing sideways)
            new ScatterRule { plant = "curl_e_corkscrew", dMin = -0.3f, dMax = 3.6f, spacing = 8.5f, height = new Vector2(2.0f, 3.8f), tilt = 0.2f, tintJitter = 0.08f, patchiness = 0.25f,
                tuft = new Vector2Int(2, 4), tuftLean = 8f, tuftScale = new Vector2(0.6f, 1f) },
            new ScatterRule { plant = "puff_a_stalked", dMin = -0.3f, dMax = 3.6f, spacing = 8f, height = new Vector2(1.8f, 3.4f), tilt = 0.15f, tintJitter = 0.08f, patchiness = 0.25f,
                tuft = new Vector2Int(3, 5), tuftLean = 12f, tuftScale = new Vector2(0.55f, 1f) },
            // the crystals (Tripo, 02_Concepts/Crystals_v1): small clusters close beside the way, a crystal about an ant tall,
            // faceted, their ridges catching the light, lit from within in patches (ParticleLook.CrystalSplat), as the
            // references' crystals by a path
            new ScatterRule { plant = "crystal_a_spray", dMin = -0.4f, dMax = 2.6f, spacing = 11f, height = new Vector2(1.2f, 2.4f), tilt = 0.12f, tintJitter = 0.06f, cluster = new Vector2Int(1, 2), patchiness = 0.3f },
            new ScatterRule { plant = "crystal_b_fan", dMin = -0.4f, dMax = 2.6f, spacing = 10f, height = new Vector2(0.7f, 1.4f), tilt = 0.12f, tintJitter = 0.06f, cluster = new Vector2Int(1, 3), patchiness = 0.3f },
            new ScatterRule { plant = "crystal_c_mixed", dMin = -0.4f, dMax = 2.6f, spacing = 11f, height = new Vector2(1.0f, 2.0f), tilt = 0.12f, tintJitter = 0.06f, cluster = new Vector2Int(1, 2), patchiness = 0.3f },
            // the moss forest's own (Tripo, 02_Concepts/Moss_v1): liverwort colonies with their parasols, pixie-cup lichens
            // with scarlet caps, groves of moss capsules hanging like lamps; smaller than its landmarks (LabNest), near the way
            new ScatterRule { plant = "liverwort_parasols", dMin = -0.4f, dMax = 3.0f, spacing = 10f, height = new Vector2(1.4f, 2.6f), tilt = 0.12f, tintJitter = 0.08f, patchiness = 0.3f },
            new ScatterRule { plant = "pixie_cup_lichen", dMin = -0.4f, dMax = 3.0f, spacing = 9f, height = new Vector2(1.0f, 2.0f), tilt = 0.15f, tintJitter = 0.08f, cluster = new Vector2Int(1, 2), patchiness = 0.3f },
            // (the capsules three or four from one spot, a grove whose heads touch, leaning out a little: one alone, its
            // seven stalks spread from a small cushion, read thin, review k12; two or three a few ants apart still stood
            // apart, k14; three or four still thin, k15: four to six)
            new ScatterRule { plant = "moss_capsules", dMin = -0.4f, dMax = 3.2f, spacing = 10f, height = new Vector2(1.8f, 3.2f), tilt = 0.1f, tintJitter = 0.08f, patchiness = 0.3f,
                tuft = new Vector2Int(4, 6), tuftLean = 8f, tuftScale = new Vector2(0.6f, 1f) },
            // the kiln temple's own (Tripo, 02_Concepts/Kiln_Redesign_v1/fronts), the life that follows a fire: bonfire cups
            // low on the ash close beside the way, their rims beaded (half the height they had in k1: there a cup was as big
            // as the ant, the cluster a heap of clay bowls); fire morels in ones and twos, a few ants tall
            new ScatterRule { plant = "ember_cups", dMin = -0.4f, dMax = 2.8f, spacing = 6f, height = new Vector2(0.38f, 0.65f), tilt = 0.1f, tintJitter = 0.06f, cluster = new Vector2Int(1, 3), patchiness = 0.3f },
            // (the morels back from the way's very edge, dMin from -0.3: there their stems were lost, k3 plant review)
            // (the kiln's morels on the floor, clear of the banks' foot: up the slopes their stems and feet were never seen,
            // k6-k8 plant reviews; 0.5-2.4 from the floor's edge was still slope; at -0.3-0.5 their model's wide ash pile
            // still lay half on the slope and sank them to its lowest ground, their stems under it, k10)
            // (faced to the way: its model's caps stand side by side from the front; turned any way, from the way the small cap
            // stood before the big one, k12)
            new ScatterRule { plant = "fire_morel", dMin = -1.1f, dMax = -0.4f, spacing = 8.5f, height = new Vector2(1.4f, 2.6f), tilt = 0.05f, tintJitter = 0.06f, cluster = new Vector2Int(1, 2), patchiness = 0.3f, faceWay = true },
            // the amber nest's own (Tripo, 02_Concepts/Amber_v1_候选): little frankincense trees along the way, a few ants
            // tall, tears of resin in rows down their peeling bark; gumweed in clumps low beside it, a bead of resin in each bud
            // (a gumweed tall enough that its surface is over ParticleLook.accentArea: smaller, some took a cool accent whole, lilac)
            new ScatterRule { plant = "tear_tree", dMin = -0.2f, dMax = 3.4f, spacing = 11f, height = new Vector2(2.6f, 4.2f), tilt = 0.08f, tintJitter = 0.06f, patchiness = 0.3f },
            new ScatterRule { plant = "gumweed", dMin = -0.4f, dMax = 3.0f, spacing = 7f, height = new Vector2(1.9f, 2.6f), tilt = 0.12f, tintJitter = 0.08f, cluster = new Vector2Int(1, 2), patchiness = 0.3f },
        };

        /// <summary>Animation check: the player's ant becomes another species, in place (camera and ring stay).</summary>
        public void SwapPlayerSpecies(string species)
        {
            AntRef antRef = refs.Ant(species);
            if (antRef == null || antRef.prefab == null) { Debug.LogWarning($"[AntGarden] no ant {species}"); return; }
            cfg.ant.playerSpecies = species;
            Vector3 p = player.Position;
            float h = player.heading;
            Destroy(player.gameObject);
            player = new GameObject("Player").AddComponent<AntController>();
            player.Init(world, cfg.ant, antRef, cfg.ant.rimColor, cfg.ant.glowColor);
            player.grid = antGrid;
            player.Place(p.x, p.z, h);
        }

        void SpawnNpcs()
        {
            var all = new List<AntController> { player };
            var pool = new List<(SpeciesSettings s, AntRef a)>();
            foreach (var s in cfg.npcs.species)
            {
                AntRef a = refs.Ant(s.id);
                if (a != null && a.prefab != null) pool.Add((s, a));
            }
            if (pool.Count == 0) return;
            float qualityFactor = Preset.name == "low" ? 0.3f : Preset.name == "medium" ? 0.65f : 1f;
            var rng = new System.Random(4242);
            antGrid = new AntGrid(2f);
            player.grid = antGrid;
            // the colony's pheromone trails on the path graph, laid by a short simulation before anyone looks
            world.trails = cfg.npcs.colony.enabled ? new ColonyTrails(world, cfg.npcs.colony, 777) : null;
            if (world.trails != null) Debug.Log($"[AntGarden] colony trails: {world.trails.Summary()}");
            int n = 0;
            var nonFlyers = pool.FindAll(p => !p.s.canFly);
            foreach (Region r in world.regions)
            {
                // each region is one colony of a walking species, with the other kinds mixed in; winged ants are rare
                var dominant = nonFlyers.Count > 0 ? nonFlyers[r.index % nonFlyers.Count] : pool[0];
                var chambers = world.nodes.FindAll(nd => nd.region == r.index && nd.degree >= 3 && nd.inside && nd.clear >= 1.8f);
                // the first two regions (the archipelago's heart) keep full colonies; further nests smaller ones
                int perNest = r.index < (r.cell ? 4 : 2) ? cfg.npcs.countPerNest : cfg.npcs.extraCountPerNest;
                if (!string.IsNullOrEmpty(r.scene)) perNest = cfg.npcs.extraCountPerNest / 2;   // a narrow tunnel holds fewer
                if (Lab) perNest = 6;    // the particle nest: few ants, each drawn as points
                int count = Mathf.Min(Mathf.RoundToInt(perNest * r.ids.Length * qualityFactor), cfg.npcs.maxCount - n);
                var kinds = SpeciesMix(pool, dominant.s, count, rng);
                var census = new Dictionary<string, int>();
                for (int i = 0; i < kinds.Count; i++, n++)
                {
                    var (sp, ar) = kinds[i];
                    census[sp.id] = census.TryGetValue(sp.id, out int c) ? c + 1 : 1;
                    var settings = JsonUtility.FromJson<AntSettings>(JsonUtility.ToJson(cfg.ant));
                    settings.bodyLength = sp.bodyLength;
                    settings.radius = sp.radius;
                    settings.crawlSpeed = Mathf.Lerp(sp.walkSpeed.x, sp.walkSpeed.y, (float)rng.NextDouble());
                    settings.fastSpeed = sp.fastSpeed;
                    settings.climb = 0;   // NPCs keep to the canyon floor and its trails
                    if (cfg.npcs.colony.enabled)
                    {
                        // NPCs steer gently: no snapping round on the spot
                        settings.turnRate = cfg.npcs.colony.turnRate;
                        settings.sprintTurnRate = cfg.npcs.colony.sprintTurnRate;
                        settings.turnGain = cfg.npcs.colony.turnGain;
                    }
                    // the species' own colour shift plus the region's palette, so ants differ by kind and by place
                    float hue = sp.hue + r.biome.antHue;
                    Color rim = r.biome.antHue == 0 ? sp.rimColor : Color.Lerp(sp.rimColor, r.biome.plantGlow, 0.7f);
                    Color glow = r.biome.antHue == 0 ? sp.glowColor : Color.Lerp(sp.glowColor, r.biome.plantGlow * 0.5f, 0.7f);
                    var go = new GameObject($"NPC_{n:000}_{sp.id}_r{r.index}");
                    var ant = go.AddComponent<AntController>();
                    ant.Init(world, settings, ar, rim, glow, hue, r.biome.antSaturation);
                    ant.grid = antGrid;
                    var npc = new NpcAnt(ant, sp, world, cfg.npcs, 1000 + n, ar.takeoffForward, ar.takeoffRise, ar.landForward, r.index);
                    if (rng.NextDouble() < cfg.npcs.messengerShare) npc.lineIndex = rng.Next(Voices.AntLines.Length);
                    double roll = rng.NextDouble();
                    NpcAnt.Role role = roll < cfg.npcs.chamberShare && chambers.Count > 0 ? NpcAnt.Role.Chamber
                        : roll < cfg.npcs.chamberShare + cfg.npcs.wanderShare ? NpcAnt.Role.Wander : NpcAnt.Role.Trail;
                    npc.Spawn(player.Position, role, chambers.Count > 0 ? chambers[i % chambers.Count] : null);
                    npcs.Add(npc);
                    all.Add(ant);
                }
                var parts = new List<string>();
                foreach (var kv in census) parts.Add($"{kv.Key} {kv.Value}");
                parts.Sort();
                Debug.Log($"[AntGarden] ants r{r.index} ({r.biome.id}, colony {dominant.s.id}): {count} = {string.Join(", ", parts)}");
            }
            foreach (var a in all) a.others = all;
        }

        /// <summary>
        /// Which species each of <paramref name="count"/> ants gets: shares weighted by SpeciesSettings.share, the
        /// colony's own species boosted, at least minPerSpecies of every kind (so all five always appear), shuffled.
        /// </summary>
        List<(SpeciesSettings s, AntRef a)> SpeciesMix(List<(SpeciesSettings s, AntRef a)> pool, SpeciesSettings colony, int count, System.Random rng)
        {
            var weights = new float[pool.Count];
            float sum = 0;
            for (int k = 0; k < pool.Count; k++)
            {
                weights[k] = Mathf.Max(0, pool[k].s.share) * (pool[k].s == colony ? cfg.npcs.dominantBoost : 1f);
                sum += weights[k];
            }
            var counts = new int[pool.Count];
            int floor = count >= pool.Count * cfg.npcs.minPerSpecies ? cfg.npcs.minPerSpecies : count / Mathf.Max(1, pool.Count);
            int assigned = 0, colonyIndex = 0;
            for (int k = 0; k < pool.Count; k++)
            {
                counts[k] = Mathf.Max(floor, Mathf.RoundToInt(count * weights[k] / Mathf.Max(1e-4f, sum)));
                assigned += counts[k];
                if (pool[k].s == colony) colonyIndex = k;
            }
            counts[colonyIndex] = Mathf.Max(0, counts[colonyIndex] + count - assigned); // rounding goes to the colony
            var list = new List<(SpeciesSettings, AntRef)>(count);
            for (int k = 0; k < pool.Count; k++)
                for (int c = 0; c < counts[k]; c++) list.Add(pool[k]);
            for (int k = list.Count - 1; k > 0; k--)
            {
                int j = rng.Next(k + 1);
                (list[k], list[j]) = (list[j], list[k]);
            }
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        void PlaceSlots(Transform root)
        {
            int n = 0;
            foreach (WEdge e in world.edges)
            {
                if (e.connector) continue;
                float acc = 0, next = cfg.slotSpacing * 0.5f;
                for (int i = 1; i < e.pts.Count; i++)
                {
                    float x0 = e.pts[i - 1].x, z0 = e.pts[i - 1].y, x1 = e.pts[i].x, z1 = e.pts[i].y;
                    acc += Mathf.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
                    if (acc < next) continue;
                    next += cfg.slotSpacing;
                    if (world.HexDist(x1, z1) > -7) continue;
                    bool nearChamber = false;
                    foreach (WNode nd in world.nodes)
                        if (nd.degree >= 3 && Vector2.Distance(new Vector2(nd.x, nd.z), new Vector2(x1, z1)) < nd.clear + 4) nearChamber = true;
                    if (nearChamber) continue;
                    float len = Mathf.Max(1e-4f, Mathf.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)));
                    float side = n % 2 == 0 ? 1 : -1;
                    float off = Mathf.Max(0, e.hws[i] - 1.6f) * side;
                    AddSlot(root, $"{e.id}·{++n}", x1 - (z1 - z0) / len * off, z1 + (x1 - x0) / len * off);
                }
            }
            foreach (WNode nd in world.nodes)
                if (nd.degree == 1 && nd.inside) AddSlot(root, $"{nd.id}·尽头", nd.x, nd.z);
        }

        void AddSlot(Transform root, string id, float x, float z)
        {
            // (in a particle nest quieter: at full its centre blew out white)
            var marker = Rings.Make("Slot_" + id, 0.9f, 1.15f, refs.slotRing, new Color(1f, 0.85f, 0.55f) * (Lab ? 0.9f : 2f));
            marker.transform.SetParent(root, false);
            marker.transform.position = new Vector3(x, world.Height(x, z) + 0.05f, z);
            world.slots.Add(new StorySlot { id = id, position = marker.transform.position, marker = marker });
        }

        ParticleSystem MakeBurst()
        {
            var go = new GameObject("TeleportBurst");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new Color(0.75f, 1f, 0.95f);
            main.gravityModifier = -0.15f;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 160) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            if (refs.spores != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = refs.spores;
            return ps;
        }

        void Teleport(Vector2 p)
        {
            Sfx.Gate(player.Position);
            Pad.Rumble(0.35f, 0.15f, 0.28f);
            player.Place(p.x, p.y, player.heading);
            rig.SnapBehind(player.heading);
            bigmap.Toggle(false);
            PlayBurst();
        }

        /// <summary>Arrive just inside a gate, facing into its region.</summary>
        public void TeleportToGate(Gate g) => TeleportToGate(g, true);

        void TeleportToGate(Gate g, bool effects)
        {
            Vector2 inward = -g.dir;
            // far enough in that the camera boom ends up in front of the gate, not inside its veil
            Vector2 p = new Vector2(g.position.x, g.position.z) + inward * 6.5f;
            if (!world.SnapToWalkable(p.x, p.y, 0.9f, out Vector2 snapped)) snapped = p;
            player.Place(snapped.x, snapped.y, Mathf.Atan2(inward.x, inward.y));
            rig.SnapBehind(player.heading);
            if (!effects) return;
            Sfx.Gate(player.Position);
            Pad.Rumble(0.35f, 0.15f, 0.28f);
            bigmap.Toggle(false);
            PlayBurst();
        }

        void PlayBurst()
        {
            if (burst == null) return;
            burst.transform.position = player.Position + Vector3.up * 0.3f;
            burst.Play();
        }

        IEnumerator SwitchLater(string id)
        {
            yield return new WaitForSeconds(2f);
            SwitchWorld(id);
        }

        void SwitchWorld(string id)
        {
            ArriveFrom = Selection;   // the new world puts the ant at its gate back here, if it has one
            Selection = id;
            Sfx.Gate(player != null ? player.Position : Vector3.zero);
            SceneManager.LoadScene(0);
        }

        /// <summary>The world the ant just came from (to arrive at the gate that leads back there).</summary>
        static string ArriveFrom;

        /// <summary>
        /// The canyon exits that lead to other worlds, each with a light gate naming where it goes: from the
        /// archipelago to the two ruins first (a ruin by an exit of its own colour), then the single nests; from a
        /// ruin back to the archipelago; from a single nest back to the archipelago and on to the next nest.
        /// </summary>
        void BuildWorldGates(Transform parent)
        {
            if (worlds == null) return;
            var free = new List<WExit>();
            foreach (WExit ex in world.exits)
            {
                bool taken = false;
                foreach (Gate g in gates)
                    if (Vector2.Distance(new Vector2(g.position.x, g.position.z), new Vector2(ex.x, ex.z)) < 6f) taken = true;
                if (!taken) free.Add(ex);
            }
            string home = cfg.defaultWorld;
            var dests = new List<string>();
            string nextLab = null;
            if (Selection == home)
            {
                foreach (string w in RuinWorlds) if (!home.Contains(w)) dests.Add(w);
                foreach (string w in worlds)
                    if (w.StartsWith("path-") && !w.Contains("#") && !home.Contains(w)) dests.Add(w);
            }
            else
            {
                // a particle nest: its way out, where its light leads (LabNest.Heart), opens on the next nest of the list
                // (after the last, the first): the way in, through the nest, out, and on to the next (the user, 10-26 and
                // 10-27: no heart, "不要用心脏的概念了"); its other exits lead home
                if (Lab)
                {
                    var labs = worlds.FindAll(w => w.StartsWith("lab:"));
                    int li = labs.IndexOf(Selection);
                    if (labs.Count > 1 && li >= 0) dests.Add(nextLab = labs[(li + 1) % labs.Count]);
                }
                dests.Add(home);
                int i = worlds.IndexOf(Selection);
                if (!Selection.Contains("#") && i >= 0 && i + 1 < worlds.Count && worlds[i + 1].StartsWith("path-")) dests.Add(worlds[i + 1]);
            }
            int made = 0;
            foreach (string dest in dests)
            {
                if (free.Count == 0) break;
                // a ruin opens from an exit of its own colour when there is one; a particle nest's next from its way out
                string biome = dest.Contains("@") ? dest.Substring(dest.IndexOf('@') + 1).Split('#')[0] : null;
                WExit pick = free.Find(ex => biome != null && world.regions[ex.region].biome.id == biome) ?? free[0];
                if (dest == nextLab && lab != null)
                    foreach (WExit ex in free)
                        if ((new Vector2(ex.x, ex.z) - lab.Heart).sqrMagnitude < (new Vector2(pick.x, pick.z) - lab.Heart).sqrMagnitude) pick = ex;
                free.Remove(pick);
                Color c = dest.Contains("#") ? new Color(1f, 0.78f, 0.42f) : dest == home ? new Color(0.72f, 1f, 0.85f) : new Color(0.7f, 0.88f, 1f);
                // a particle nest: its gates in its own light, soft (its way out is where its light leads: at the theme's own
                // gain alone the gate's arc was a white blaze at the end of the way)
                if (NestTheme.Current != null) c = NestTheme.Current.threadHalo * (NestTheme.Current.gateGain * 0.5f);
                gates.Add(Gates.WorldGate(world, refs, cfg.gates, parent, pick, dest, c));
                made++;
            }
            // left-over exits of a ruin or nest lead home too
            if (Selection != home)
                foreach (WExit ex in free)
                {
                    gates.Add(Gates.WorldGate(world, refs, cfg.gates, parent, ex, home, NestTheme.Current != null ? NestTheme.Current.threadHalo * (NestTheme.Current.gateGain * 0.5f) : new Color(0.72f, 1f, 0.85f)));
                    made++;
                }
            Debug.Log($"[AntGarden] world gates {made} (exits {world.exits.Count}): {string.Join(", ", gates.FindAll(g => g.world != null).ConvertAll(g => g.world))}");
        }

        float heartbeat;
        static bool windowChecked;  // only on the first load: later scene reloads keep the player's choice

        /// <summary>Another app in front: drop to a low frame rate (recording and capture runs excepted).</summary>
        void OnApplicationFocus(bool focused)
        {
            if (cfg == null || Arg("-capture") != null || Arg("-demo") != null || Arg("-photos") != null || Arg("-animcheck") != null || Arg("-lookcheck") != null || Arg("-npccheck") != null || Arg("-camcheck") != null || Arg("-sceneshots") != null || Arg("-legcheck") != null || Arg("-padcheck") != null || Arg("-trailfilm") != null || Arg("-interactshots") != null || Arg("-stuttercheck") != null) return;
            Application.targetFrameRate = focused ? cfg.display.targetFps : cfg.display.backgroundFps;
        }

        /// <summary>L: switch the UI between 中文 and English without rebuilding the world.</summary>
        public void ToggleLanguage()
        {
            Loc.Toggle();
            world.label = WorldLabel();
            hud.SetHelp(world.label);
            lastPrompt = null;   // the prompt and the nest banner are written again in the new language
            currentNest = -1;
            bool open = bigmap.IsOpen;
            bigmap.Destroy();
            bigmap = new BigMap(hud.canvas, painter, world, cfg, gates, Teleport, TeleportToGate, SwitchWorld, worlds);
            bigmap.Toggle(open);
        }

        void ToggleFullScreen()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                Resolution r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }
            else Screen.SetResolution(cfg.display.windowWidth, cfg.display.windowHeight, FullScreenMode.Windowed);
        }

        void Update()
        {
            heartbeat += Time.unscaledDeltaTime;
            if (heartbeat > 2f) { heartbeat = 0; Debug.Log($"[AntGarden] heartbeat frame={Time.frameCount} t={Time.realtimeSinceStartup:F1}s stage={lastStage}"); }
            if (!Ready) return;
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            bool mapOpen = bigmap.IsOpen;

            Pad.Tick();
            if (!demo)
            {
                // keyboard, or the pad: Y map, B back (or the antennae), ☰ help, A = E
                if (Input.GetKeyDown(KeyCode.M) || Pad.Map) { bigmap.Toggle(); Sfx.Map(bigmap.IsOpen); }
                bool back = Input.GetKeyDown(KeyCode.Escape) || (Pad.Back && (bigmap.IsOpen || hud.SlotOpen));
                if (back) { if (bigmap.IsOpen) Sfx.Map(false); bigmap.Toggle(false); hud.HideSlot(); }
                if (Input.GetKeyDown(KeyCode.H) || Pad.Help) hud.ToggleHelp();
                if (Input.GetKeyDown(KeyCode.Tab)) CycleQuality();
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.F11)) ToggleFullScreen();
                if (Input.GetKeyDown(KeyCode.L)) ToggleLanguage();
                if (Input.GetKeyDown(KeyCode.P) && particleLook != null)
                {
                    particleLook.SetOn(!particleLook.On);
                    hud.ShowLine(Loc.T("粒子化（实验）", "Particle look (experiment)"), particleLook.On ? Loc.T("开", "on") : Loc.T("关", "off"));
                }
                if (!mapOpen && (Input.GetKeyDown(KeyCode.Space) || (Pad.Back && !back)) && player.speed < 0.1f)
                {
                    // reaching out with the antennae is also how you talk: to the ant or relic in front of you
                    if ((nearRuin != null || focusNpc) && nearPortal == null && nearWorldGate == null) Interact();
                    else player.PlayTalk();
                }
                if (!mapOpen && (Input.GetKeyDown(KeyCode.E) || Pad.Interact)) Interact();
                // on the map the left stick moves a cursor and A teleports there
                if (mapOpen) bigmap.PadTick(Pad.Move, Pad.Interact, dt);
            }

            var (fwd, right) = rig.Basis();
            float ix = (Key(KeyCode.D) || Key(KeyCode.RightArrow) ? 1 : 0) - (Key(KeyCode.A) || Key(KeyCode.LeftArrow) ? 1 : 0);
            float iz = (Key(KeyCode.W) || Key(KeyCode.UpArrow) ? 1 : 0) - (Key(KeyCode.S) || Key(KeyCode.DownArrow) ? 1 : 0);
            Vector2 stick = demo || mapOpen ? Vector2.zero : Pad.Move;   // a gentle tilt walks slowly
            Vector2 move = demo ? (demoKeys.HasValue ? fwd * demoKeys.Value.y + right * demoKeys.Value.x : demoMove)
                : mapOpen ? Vector2.zero : fwd * (iz + stick.y) + right * (ix + stick.x);
            bool fast = demo ? demoFast : !mapOpen && (Key(KeyCode.LeftShift) || Key(KeyCode.RightShift) || Pad.Run);
            if (antGrid != null)
            {
                antGrid.Clear();
                antGrid.Add(player);
                foreach (var n in npcs) antGrid.Add(n.ant);
            }
            player.Tick(dt, move, fast);
            // footsteps: a soft patter for every short stretch walked (a jump of more than a unit is a teleport)
            float stepped = new Vector2(player.Position.x - lastStep.x, player.Position.z - lastStep.z).magnitude;
            lastStep = player.Position;
            stepRun = stepped > 1f ? 0 : stepRun + stepped;
            if (stepRun > stepNext && player.speed > 0.1f)
            {
                // at uneven gaps, and now and then not at all: six legs make no beat
                stepRun = 0;
                stepNext = (player.sprinting ? 0.26f : 0.2f) * (0.7f + 0.7f * UnityEngine.Random.value);
                if (UnityEngine.Random.value > 0.15f) Sfx.Step(player.Position, dryGround, player.sprinting);
            }
            Sfx.Tick(dt);
            world.glow?.Tick(dt, player);
            world.trails?.Evaporate(dt);
            if (ruins.Count > 0 || lab != null) GuideTrail.Follow(player.Position, dt);   // the glowing specks brighten round the ant
            lab?.Tick(dt, player.Position, hud);
            foreach (RuinScene ruin in ruins) ruin.Tick(dt, player, hud);
            // ants in far nests wait unseen; the nest the player is in and its close neighbours live
            UpdateActiveRegions();
            NpcAnt.CameraAt = cam != null ? cam.transform.position : (Vector3?)null;
            foreach (var n in npcs)
            {
                if (regionActive[n.region]) n.Tick(dt, player);
                else if (regionWasActive[n.region]) n.ant.SetVisible(false);
            }
            rig.Tick(dt, player, !mapOpen && !demo);
            Atmosphere.Follow(spores, cam);
            playerRing.Drape(world, player.Position);   // it lies on the ground under the ant, following every bump

            hud.SetMapOpen(bigmap.IsOpen);
            UpdateRegion();
            UpdatePrompt();
            hud.Tick(dt, player, npcs.Count, Preset.name);
            bigmap.Tick(dt, player);
        }

        static bool Key(KeyCode k) => Input.GetKey(k);

        bool[] regionActive, regionWasActive;

        /// <summary>A region is lived in while the player is in it or within 40 units of its frame.</summary>
        void UpdateActiveRegions()
        {
            int n = world.regions.Count;
            if (regionActive == null || regionActive.Length != n) { regionActive = new bool[n]; regionWasActive = new bool[n]; }
            Vector3 p = player.Position;
            for (int i = 0; i < n; i++)
            {
                Region r = world.regions[i];
                regionWasActive[i] = regionActive[i];
                if (r.cell)
                {
                    // a honeycomb: the player's own cell, and a neighbour when the player is near their seam
                    regionActive[i] = r.CellDist(p.x, p.z) < 12f;
                    continue;
                }
                float dx = Mathf.Max(0, Mathf.Max(r.minX - p.x, p.x - r.maxX)), dz = Mathf.Max(0, Mathf.Max(r.minZ - p.z, p.z - r.maxZ));
                regionActive[i] = n <= 4 || dx * dx + dz * dz < 40f * 40f;
            }
        }

        void CycleQuality()
        {
            string[] names = { "low", "medium", "high" };
            int i = Array.IndexOf(names, Preset.name);
            QualityName = names[(i + 1) % names.Length];
            PlayerPrefs.SetString("antgarden.quality", QualityName);
            SceneManager.LoadScene(0);
        }

        /// <summary>Region (biome) and nest changes: blend the atmosphere and show the nest's title.</summary>
        void UpdateRegion()
        {
            // under a tunnel roof it is darker
            atmosphere?.SetShade(float.IsInfinity(world.Ceiling(player.Position.x, player.Position.z)) ? 0 : 1);
            Region r = world.RegionAt(player.Position.x, player.Position.z);
            if (r != null && r.index != currentRegion)
            {
                bool first = currentRegion < 0;
                currentRegion = r.index;
                atmosphere?.SetBiome(r.biome, first);
                dryGround = r.biome.id == "amber";
                Sfx.SetPlace(r.scene != null, dryGround);
            }
            var mods = world.modules;
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < mods.Count; i++)
            {
                float d = Vector2.SqrMagnitude(new Vector2(mods[i].x - player.Position.x, mods[i].z - player.Position.z));
                if (d < bd) { bd = d; best = i; }
            }
            if (best == currentNest) return;
            if (currentNest >= 0) Sfx.Veil(player.Position);
            currentNest = best;
            Region mr = world.regions[mods[best].region];
            int inRegion = 0, idx = 0;
            for (int i = 0; i < mods.Count; i++)
                if (mods[i].region == mr.index) { if (i == best) idx = inRegion; inRegion++; }
            string sub = inRegion > 1 ? Loc.T($"{mr.biome.Name} · 第 {idx + 1} / {inRegion} 个蚁穴", $"{mr.biome.Name} · nest {idx + 1} of {inRegion}") : mr.biome.Name;
            string nest = mods[best].id.Replace("path-", "");
            if (!string.IsNullOrEmpty(mr.scene)) hud.ShowRegion(RuinScene.Title(mr.scene), Loc.T($"地下隧道 · 蚁穴 {nest}", $"Underground tunnel · nest {nest}"));
            else hud.ShowRegion(Loc.T("蚁穴 ", "Nest ") + nest, sub);
        }

        void UpdatePrompt()
        {
            Vector2 pp = new Vector2(player.Position.x, player.Position.z);
            nearPortal = null;
            foreach (Gate g in gates)
                if (g.portal && Vector2.Distance(new Vector2(g.position.x, g.position.z), pp) < Mathf.Max(cfg.gates.portalRadius, g.radius * 0.8f)) nearPortal = g;
            // the ant to talk to: the one ahead of the player and closest, kept while it stays in reach (with hundreds
            // of ants about, the first one in the list was often behind the player or off screen)
            Vector2 facing = new Vector2(Mathf.Sin(player.heading), Mathf.Cos(player.heading));
            NpcAnt pick = null;
            float pickScore = float.MaxValue, npcDist = float.MaxValue;
            foreach (var n in npcs)
            {
                if (n.ant.altitude > 0.3f || (regionActive != null && !regionActive[n.region])) continue;
                Vector2 d = new Vector2(n.ant.Position.x - pp.x, n.ant.Position.z - pp.y);
                float dist = d.magnitude;
                if (dist > cfg.npcs.talkRadius * (n == nearNpc ? 1.15f : 1f)) continue;
                float ahead = dist > 1e-3f ? Vector2.Dot(d / dist, facing) : 1;
                if (ahead < -0.3f) continue;   // behind the player
                float score = dist / (0.6f + ahead) * (n == nearNpc ? 0.8f : 1f);
                if (score < pickScore) { pickScore = score; pick = n; npcDist = dist; }
            }
            nearNpc = pick;
            nearWorldGate = null;
            foreach (Gate g in gates)
                if (g.world != null && Vector2.Distance(new Vector2(g.position.x, g.position.z), pp) < Mathf.Max(cfg.gates.portalRadius, g.radius * 0.8f)) nearWorldGate = g;
            nearRuin = null;
            nearRelic = -1;
            foreach (RuinScene ruin in ruins)
            {
                int i = ruin.Near(player.Position);
                if (i >= 0) { nearRuin = ruin; nearRelic = i; break; }
            }
            nearSlot = null;
            float best = cfg.slotRadius;
            foreach (var s in world.slots)
            {
                float d = Vector2.Distance(new Vector2(s.position.x, s.position.z), pp);
                if (d < best) { best = d; nearSlot = s; }
            }
            nearExit = null;
            foreach (WExit ex in world.exits)
            {
                if (world.regions[ex.region].cell) continue;   // the honeycomb's rim: there is nowhere else to go
                if (Vector2.Distance(new Vector2(ex.x, ex.z), pp) >= 3.2f) continue;
                bool isPortal = false;
                foreach (Gate g in gates) if (g.portal && Vector2.Distance(new Vector2(g.position.x, g.position.z), new Vector2(ex.x, ex.z)) < 6f) isPortal = true;
                if (!isPortal) nearExit = ex;
            }
            if (nearSlot == null && hud.SlotOpen) hud.HideSlot();
            // of an ant, a story slot and an exit, the closest is what E does (gates and relics come first)
            float slotDist = nearSlot != null ? Vector2.Distance(new Vector2(nearSlot.position.x, nearSlot.position.z), pp) : float.MaxValue;
            float exitDist = nearExit != null ? Vector2.Distance(new Vector2(nearExit.x, nearExit.z), pp) : float.MaxValue;
            focusNpc = nearNpc != null && npcDist <= Mathf.Min(slotDist, exitDist);
            bool focusSlot = !focusNpc && nearSlot != null && slotDist <= exitDist;
            string text = nearPortal != null ? Loc.T($"光门 · 按 E 前往「{nearPortal.TargetLabel}」", $"Light gate · press E to travel to {nearPortal.TargetLabel}")
                : nearWorldGate != null ? Loc.T($"光门 · 按 E 前往「{nearWorldGate.TargetLabel}」", $"Light gate · press E to travel to {nearWorldGate.TargetLabel}")
                : nearRuin != null ? Loc.T($"{nearRuin.NameOf(nearRelic)} · 按 E 或用触角碰碰它", $"{nearRuin.NameOf(nearRelic)} · press E or touch it with your antennae")
                : focusNpc ? Loc.T("按 E 和它碰碰触角", "Press E to touch antennae")
                : focusSlot ? Loc.T("按 E 查看这个故事物件位", "Press E to look at this story slot")
                : nearExit != null ? Loc.T("出口 · 按 E 前往下一个模块", "Exit · press E for the next nest")
                : "";
            text = Pad.Buttons(text);
            if (text != lastPrompt) { lastPrompt = text; hud.SetPrompt(text); }
        }

        /// <summary>The glowing ring under the player (hidden for clean photos).</summary>
        public void SetRingVisible(bool on) => ring.gameObject.SetActive(on);

        /// <summary>Show a story slot's panel.</summary>
        public void OpenSlot(StorySlot slot)
        {
            // a readable name: its number and the nest it sits in
            int number = world.slots.IndexOf(slot) + 1;
            WModule nest = null;
            float best = float.MaxValue;
            foreach (WModule m in world.modules)
            {
                float d = (new Vector2(m.x - slot.position.x, m.z - slot.position.z)).sqrMagnitude;
                if (d < best) { best = d; nest = m; }
            }
            string where = nest != null ? Loc.T(" · 蚁穴 ", " · Nest ") + nest.id.Replace("path-", "") : "";
            hud.ShowSlot(Loc.T("<b><color=#ffe2a8>故事物件位</color></b>", "<b><color=#ffe2a8>Story slot</color></b>")
                + $"\n<size=16><color=#9bbcbc>{Loc.T("物件位 ", "Slot ")}{number}{where}</color></size>\n\n"
                + Loc.T("以后用户上传的照片和故事会变成物件，放在这里。", "Photos and stories uploaded by visitors will become objects placed here."));
        }

        /// <summary>The E key: use a portal, greet an ant, open a story slot or take an exit.</summary>
        public void Interact()
        {
            if (nearPortal != null && nearPortal.pair != null) TeleportToGate(nearPortal.pair);
            else if (nearWorldGate != null) SwitchWorld(nearWorldGate.world);
            else if (nearRuin != null)
            {
                player.PlayTalk();
                nearRuin.Speak(nearRelic, hud);
                Pad.Rumble(0.15f, 0.25f, 0.15f);
            }
            else if (focusNpc && nearNpc != null)
            {
                nearNpc.Greet(player);
                player.PlayTalk();
                Sfx.Touch(nearNpc.ant.Position);
                Pad.Rumble(0.12f, 0.22f, 0.12f);
                // some ants have something to say; the rest just touch antennae back
                string line = nearNpc.NextLine();
                if (line != null)
                {
                    hud.ShowLine(Loc.T("一只蚂蚁 · ", "An ant · ") + nearNpc.species.id.Replace("_", " "), line);
                    Voices.Chime(nearNpc.ant.Position + Vector3.up * 0.5f, 1.6f, 0.3f);
                }
            }
            else if (nearSlot != null && (nearExit == null || Vector2.Distance(new Vector2(nearSlot.position.x, nearSlot.position.z), new Vector2(player.Position.x, player.Position.z)) <= Vector2.Distance(new Vector2(nearExit.x, nearExit.z), new Vector2(player.Position.x, player.Position.z)))) OpenSlot(nearSlot);
            else if (nearExit != null && worlds.Count > 1)
            {
                // other exits lead to the next single nest in the list
                string current = world.regions[0].ids[0];
                int i = worlds.IndexOf(current);
                string next = i >= 0 && i + 1 < worlds.Count && !worlds[i + 1].StartsWith("stitch:") ? worlds[i + 1] : worlds[1];
                SwitchWorld(next);
            }
        }
    }
}
