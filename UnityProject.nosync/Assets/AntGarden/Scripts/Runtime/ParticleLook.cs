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
        public float carpetStep = 0.013f;   // spacing of its points (units)
        public Vector2 carpetFade = new Vector2(3.5f, 6f);   // drawn out to y round the ant, thinning out from x
        public float carpetGain = 0.1f;
        public float carpetSize = 0.0055f;

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
                        Emit(g, g.area * g.kind * shellDensity, ref rng, groundBrightness * shellBrightness, 0f, shellWork);
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
                bool splat = splatRoll < splats;
                into.Add(new Point { pos = p + n * 0.035f, col = PackLinear(col * (bright * (splat ? 0.16f : 1f)), Ground, splat ? 40u : 10u) });
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
            Color pale = new Color(0.62f, 0.75f, 1f).linear, gold = new Color(1f, 0.74f, 0.4f).linear, rose = new Color(0.95f, 0.66f, 1f).linear;
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
                    points.Add(new Point { pos = p, col = PackLinear(stem * (0.42f * lit), Ground, 8u) });
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
                points.Add(new Point { pos = p, col = PackLinear(colour * (0.3f * rib * (0.7f + 0.6f * rng.Next())), Ground, 8u) });
            }
        }

        Color LayerAt(float x, float z)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((x - mapX0) / MapCell), 0, mapW - 1), j = Mathf.Clamp(Mathf.FloorToInt((z - mapZ0) / MapCell), 0, mapH - 1);
            return layerArr[j * mapW + i];
        }

        float HeightAt(float x, float z)
        {
            float fx = (x - mapX0) / MapCell - 0.5f, fz = (z - mapZ0) / MapCell - 0.5f;
            int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, mapW - 2), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, mapH - 2);
            float tx = Mathf.Clamp01(fx - i), tz = Mathf.Clamp01(fz - j);
            float a = heightArr[j * mapW + i], b = heightArr[j * mapW + i + 1], c = heightArr[(j + 1) * mapW + i], d = heightArr[(j + 1) * mapW + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>How far (x, z) is from the nearest glowing way (units; far: large).</summary>
        float WayDistance(float x, float z)
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
        float Focus(float x, float z)
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
