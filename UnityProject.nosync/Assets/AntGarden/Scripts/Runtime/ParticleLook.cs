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
        public float solidFine = 1500f;     // and in the fine one, round the ant
        public int solidFineCap = 42000;    // at most this many for one plant
        public float solidAlbedo = 0.5f;    // how bright a plant's splats are lit (its dots' colours were for glowing)
        // plants whose glowing part is glass (a mesh can only make it an opaque lump): there a thin, broken shell of
        // splats round a light of its own inside, as a capture of something translucent looks
        public string[] glassKinds = { "spore_stalk" };
        // plants whose glowing part is only their head (the curl of a lollipop-like curl): glowing from this far up
        public readonly Dictionary<string, float> glowFrom = new Dictionary<string, float>
        {
            { "curl_glow", 0.5f }, { "curl_a_crozier", 0.6f }, { "curl_b_double", 0.62f }, { "curl_c_hook", 0.5f }, { "curl_d_cluster", 0.55f },
        };
        public int glassCore = 1400;        // splats in that light (all in the fine set, a sixth in the coarse one)

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
            public Vector2 bendDir;
            public float bend, twist, sy, sxz;
            public bool rigid;              // a thing (relic): neither bent nor recoloured
            public bool glass;              // its glowing part is glass (glassKinds)
            public float glowFrom;          // its glowing part only from this far up (0..1 of its height)
            public Color glowDeep;          // its glow colour, deeper (the palette's glow colours are pale: as light they read white)
            public float ownHue, ownSat = 1;   // its region's turn of the texture's own colour (a blue plant in the amber nest)
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
                uint pal = replace ? (pi.relic ? 63u : Palette(pi, ord++)) << 24 : 0;   // 63: its own colours
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
            int start = into.Count;
            var picks = new List<(PlantInstance pi, float area, int n)>();
            var census = new Dictionary<string, int>();
            long wanted = 0;
            foreach (PlantInstance pi in world.plantInstances)
            {
                if (pi.t == null || pi.plant == null || !pi.plant.LoadPoints()) continue;
                float sc = pi.t.lossyScale.y;
                float area = (pi.plant.pointArea > 0 ? pi.plant.pointArea : 2.5f) * sc * sc;
                Vector3 p0 = pi.t.position;
                float density = solidCoarse * (pi.relic ? 2f : pi.far ? 0.15f : Mathf.Lerp(farShare, 1f, Focus(p0.x, p0.z)));
                int n = Mathf.Clamp(Mathf.RoundToInt(area * density), 120, pi.far ? 8000 : pi.plant.points.Length);
                picks.Add((pi, area, n));
                wanted += n;
                census[pi.plant.name] = census.TryGetValue(pi.plant.name, out int c) ? c + 1 : 1;
            }
            float share = wanted > solidCoarseBudget ? (float)solidCoarseBudget / wanted : 1f;
            int ord = 0;
            foreach (var (pi, area, n0) in picks)
            {
                int n = Mathf.Max(100, Mathf.RoundToInt(n0 * share));
                SolidSlot slot = Slot(pi, area, ord++);
                float radius = Mathf.Min(pi.far ? 0.6f : 0.055f, 0.62f * Mathf.Sqrt(area / n));
                for (int k = 0; k < n && k < slot.pts.Length; k++)
                    into.Add(SolidSplat(slot, k, -radius));   // negative: coarse (left out round the ant)
                if (slot.core != null)
                {
                    for (int j = 0, m = Mathf.Min(slot.core.Length, n / 6); j < m; j++) into.Add(CoreSplat(slot, j, -radius));
                    Vector4 bulb = glassBulbs[pi.plant];
                    Vector3 mid = bulb, up = Vector3.up;
                    Shape(slot, ref mid, ref up);
                    glassLamps.Add((slot.m.MultiplyPoint3x4(mid), bulb.w * pi.t.lossyScale.y * slot.sxz, slot.glow));
                }
                if (!pi.far) solidSlots.Add(slot);
            }
            var top = new List<KeyValuePair<string, int>>(census);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var line = new System.Text.StringBuilder();
            foreach (var kv in top) line.Append($" {kv.Key} {kv.Value},");
            Debug.Log($"[AntGarden] solid plants: {into.Count - start} coarse splats from {picks.Count} plants" + (share < 1 ? $" (each at {share:P0})" : "") + $"; kinds:{line}");
            return into.Count - start;
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
            uint pal = pi.relic ? 63u : Palette(pi, ord);
            Color b0 = body != null && pal != 63 ? body[pal & 7] : Color.white;
            // the tip a neighbour of the base tint in the palette (or the same, lighter), each jittered
            Color b1 = body != null && pal != 63 ? body[(int)((pal & 7) + 1 + (int)(R() * 2.99f)) % 8] : Color.white;
            if (R() < 0.35f) b1 = Color.Lerp(b0, Color.white, 0.25f);
            Color Jitter(Color c, float hueDeg, float v) => (HueShift(c.linear, hueDeg, 0.85f + 0.4f * R()) * v);
            float hue = (R() - 0.5f) * 26f;
            var slot = new SolidSlot
            {
                pts = plant.points, raw = plant.pointColors, nrms = plant.pointNormals, density = plant.pointDensity, open = plant.pointOpen,
                m = pi.t.localToWorldMatrix, pos = p0, reach = pi.t.lossyScale.y * 0.8f, area = area, top = st.top, meanLum = st.mean,
                rigid = pi.relic,
                baseTint = Jitter(b0, hue, 0.8f + 0.3f * R()),
                tipTint = Jitter(b1, hue + (R() - 0.5f) * 20f, 0.9f + 0.35f * R()),
                glow = glowPal != null && pal != 63 ? HueShift(glowPal[(pal >> 3) & 7].linear, (R() - 0.5f) * 30f, 1f) : Color.white,
                keepOwn = pi.relic ? 0.8f : 0.25f + 0.35f * R(),
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
            if (glowFrom.TryGetValue(plant.name, out float from)) slot.glowFrom = from;
            // a plant of its region's own kinds keeps its colours (the backdrop's are turned whatever they are)
            bool native = GameConfig.InList(world.RegionAt(p0.x, p0.z).biome.nativePlants, plant.name);
            slot.ownHue = native ? 0 : pi.hue;
            slot.ownSat = native ? 1 : pi.sat;
            // as light, the palette's pale glow colours read white: deeper, and toward the nest's teal
            slot.glowDeep = Color.Lerp(HueShift(slot.glow, 0f, 1.9f), (NestTheme.Current ?? NestTheme.Cyan).glowDeep, slot.glowFrom > 0 ? 0.55f : 0.3f);
            if (!pi.relic && glassKinds != null && System.Array.IndexOf(glassKinds, plant.name) >= 0)
            {
                slot.glass = true;
                slot.core = GlassCore(plant);
            }
            return slot;
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
        public void SolidShell(Vector2 at, float reach, List<SolidPoint> into)
        {
            into.Clear();
            foreach (SolidSlot slot in solidSlots)
            {
                float dx = slot.pos.x - at.x, dz = slot.pos.z - at.y, r = reach + slot.reach;
                if (dx * dx + dz * dz > r * r) continue;
                int n = Mathf.Min(Mathf.Min(solidFineCap, slot.pts.Length), Mathf.RoundToInt(slot.area * solidFine));
                float radius = Mathf.Max(0.004f, 0.62f * Mathf.Sqrt(slot.area / Mathf.Max(1, n)));
                for (int k = 0; k < n; k++) into.Add(SolidSplat(slot, k, radius));
                if (slot.core != null)
                    for (int j = 0, m = Mathf.Min(slot.core.Length, n / 3); j < m; j++) into.Add(CoreSplat(slot, j, radius));
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
            bool glows = c.a > 127 && p.y >= s.glowFrom * s.top;
            Vector3 n = s.nrms != null ? ThingPen.DecodeNormal(s.nrms[k]) : Vector3.up;
            float y01 = Mathf.Clamp01(p.y / s.top);
            Shape(s, ref p, ref n);
            float open = s.open != null ? s.open[k] / 255f : 1f;
            float shade = 0.3f + 0.7f * Mathf.Pow(open, 1.1f);
            // the texture's light and dark against the plant's own mean (a little exaggerated), some of its own colour,
            // the rest this plant's tints from base to tip; its glowing parts lean to its glow colour but keep the
            // pattern, and glow most where the texture is lightest (veins, rims)
            float lum = Mathf.Max(1e-4f, Lum(lin));
            float rel = Mathf.Clamp(Mathf.Pow(lum / s.meanLum, s.contrast), 0.05f, 3.2f);
            Color own = lin * (1f / lum);                                   // the texture's own hue, at unit brightness
            if (s.ownHue != 0 || s.ownSat != 1) own = HueShift(own, s.ownHue, s.ownSat);   // turned as its region turns its plants
            Color tint = Color.Lerp(s.baseTint, s.tipTint, Mathf.SmoothStep(0.15f, 1f, y01));
            Color col = s.rigid ? lin * 1.6f : Color.Lerp(tint, own * 0.75f, s.keepOwn) * (s.meanLum * rel * 2.2f);
            if (glows && !s.rigid) col = Color.Lerp(col, s.glowDeep * Mathf.Clamp(s.meanLum * rel * 2.4f, 0.22f, 0.55f), 0.8f);
            else if (s.glowFrom > 0 && !s.rigid) col *= 0.7f;   // a lollipop's stick: darker, so its head reads as a light
            // mottling in this plant's own pattern, and now and then faint bands along its height
            float mot = Noise(p.x * s.mottleScale + s.mottleSeed, p.z * s.mottleScale + p.y * s.mottleScale * 0.7f);
            col *= 1f + s.mottle * (mot * 2f - 1f) + (s.band > 0 ? s.band * Mathf.Sin(p.y * s.bandFreq + s.mottleSeed) : 0f);
            Color albedo = col * (solidAlbedo * shade);
            float glow = glows ? (s.glowFrom > 0 ? 1f : Mathf.Clamp01(s.glowGain * (0.55f + 0.4f * Mathf.Clamp01(rel * 0.8f))))
                : s.faint * Mathf.SmoothStep(0f, 1f, (rel - 1.05f) / 1.2f);   // its own faint light, in its lightest parts
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
            return ThingPen.Splat(s.m.MultiplyPoint3x4(p), s.m.MultiplyVector(n), albedo, radius * vary, glow);
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
        uint Palette(PlantInstance pi, int index)
        {
            if (bodyPalette == null || glowPalette == null) return 0;
            uint kind = 2166136261u;
            foreach (char ch in pi.plant.name) kind = (kind ^ ch) * 16777619u;
            uint own = (uint)index * 2654435761u ^ kind;
            own ^= own >> 15; own *= 2246822519u; own ^= own >> 13;
            float r1 = (own & 0xFFFF) / 65535f, r2 = ((own >> 16) & 0xFFFF) / 65535f;
            uint body = r1 < bodyAccent ? 6u + ((own >> 3) & 1u) : kind % 6u;
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
                    col = Color.Lerp(LayerTint(lw) * lum, own, groundTexMix);
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
                into.Add(new Point { pos = p + n * 0.035f, col = PackLinear(col * (bright * (large ? 0.14f : middling ? 0.4f : 1f)), Ground, large ? 40u : middling ? 20u : 10u) });
            }
        }

        /// <summary>The pale tint of a ground point by layer: floor, bank, plateau, and the bank's top edge (where it meets
        /// the plateau, the outline of the passage) leaning to the edge colour.</summary>
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
                Color col = golden ? gold * (0.5f + 0.9f * (float)rng.NextDouble())
                          : rosy ? rose * (0.4f + 0.7f * (float)rng.NextDouble())
                          : pale * (0.25f + 0.6f * (float)rng.NextDouble());
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
