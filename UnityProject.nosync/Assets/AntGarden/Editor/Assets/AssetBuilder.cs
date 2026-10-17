using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace AntGarden.EditorTools
{
    /// <summary>
    /// Turns the converted art (Art/Plants/&lt;name&gt;/, Art/Ants/Ant_00N/ + plants.json / ants.json) into
    /// materials, prefabs with tuned LOD groups, configured animation clips, a post-processing profile, and
    /// the RuntimeRefs manifest the game loads. Safe to re-run whenever new Tripo plants or ants arrive.
    /// Batch mode: Unity -batchmode -quit -projectPath ... -executeMethod AntGarden.EditorTools.AssetBuilder.Run
    /// </summary>
    public static class AssetBuilder
    {
        const string Root = "Assets/AntGarden";
        const string Gen = Root + "/Generated";

        [MenuItem("AntGarden/Build Assets")]
        public static void Run()
        {
            foreach (string d in new[] { Gen, Gen + "/Materials", Gen + "/Plants", Gen + "/Ants", Gen + "/Textures", Root + "/Resources/AntGarden" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            string refsPath = Root + "/Resources/AntGarden/RuntimeRefs.asset";
            var refs = AssetDatabase.LoadAssetAtPath<RuntimeRefs>(refsPath);
            if (refs == null)
            {
                refs = ScriptableObject.CreateInstance<RuntimeRefs>();
                AssetDatabase.CreateAsset(refs, refsPath);
            }
            refs.terrain = TerrainMaterial();
            refs.soilWall = SimpleMaterial("SoilWall", "AntGarden/SoilWall");
            refs.sky = SimpleMaterial("NightSky", "AntGarden/NightSky");
            refs.slotRing = SimpleMaterial("GlowRing", "AntGarden/GlowUnlit");
            refs.slotRing.enableInstancing = true;
            refs.gateCurtain = SimpleMaterial("GateCurtain", "AntGarden/GateCurtain");
            refs.spores = SimpleMaterial("Spores", "AntGarden/GlowUnlit");
            refs.spores.SetTexture("_MainTex", DotTexture());
            refs.spores.SetColor("_Color", new Color(1.6f, 1.9f, 1.9f, 1f));
            refs.postResources = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            refs.postProfile = PostProfile();
            refs.plants = BuildPlants();
            refs.ants = BuildAnts();
            refs.roots = SimpleMaterial("Roots", "AntGarden/Roots");
            refs.amber = SimpleMaterial("Amber", "AntGarden/Amber");
            refs.guideTrail = SimpleMaterial("GuideTrail", "AntGarden/GuideTrail");
            refs.terrainSeam = SimpleMaterial("TerrainSeam", "AntGarden/TerrainSeam");
            refs.particleLook = SimpleMaterial("ParticleLook", "AntGarden/ParticleLook");
            refs.groundSets = BuildGroundSets();
            EditorUtility.SetDirty(refs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AntGarden] assets: {refs.plants.Count} plants ({string.Join(", ", refs.plants.Select(p => p.name))}), {refs.ants.Count} ants ({string.Join(", ", refs.ants.Select(a => a.id))})");
        }

        // ------------------------------------------------------------------ materials

        static Material MaterialAsset(string name, string shader)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (sh == null) throw new System.Exception($"shader {shader} not found");
            if (m == null)
            {
                m = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            return m;
        }

        static Material SimpleMaterial(string name, string shader)
        {
            var m = MaterialAsset(name, shader);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material TerrainMaterial()
        {
            var m = MaterialAsset("Terrain", "AntGarden/Terrain");
            var floor = Texture($"{Root}/Art/Ground/ground_floor.png", false, true);
            var bank = Texture($"{Root}/Art/Ground/ground_bank.png", false, true);
            m.SetTexture("_FloorTex", floor);
            m.SetTexture("_BankTex", bank);
            m.SetTexture("_PlateauTex", bank);
            m.SetTexture("_FloorNormal", NormalFromAlbedo($"{Root}/Art/Ground/ground_floor.png", 6f));
            m.SetTexture("_BankNormal", NormalFromAlbedo($"{Root}/Art/Ground/ground_bank.png", 8f));
            // set explicitly: an existing material keeps old values when the shader defaults change
            m.SetColor("_FloorTint", new Color(0.85f, 1.05f, 1.08f));
            m.SetColor("_BankTint", new Color(0.75f, 0.98f, 0.92f));
            m.SetColor("_PlateauTint", new Color(0.55f, 0.72f, 0.76f));
            m.SetFloat("_GlowSpecks", 0.6f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Per-style ground textures made by Tools/make_ground_textures.py: Art/Ground/&lt;id&gt;/floor.png, bank.png,
        /// plateau.png with *_normal.png beside each. A region whose biome names a set uses it instead of
        /// recolouring the default textures.
        /// </summary>
        static List<GroundSet> BuildGroundSets()
        {
            var list = new List<GroundSet>();
            string dir = $"{Root}/Art/Ground";
            if (!Directory.Exists(dir)) return list;
            foreach (string sub in Directory.GetDirectories(dir).OrderBy(s => s))
            {
                string id = Path.GetFileName(sub);
                string p = $"{dir}/{id}";
                if (!File.Exists($"{p}/floor.png")) continue;
                var set = new GroundSet
                {
                    id = id,
                    floor = Texture($"{p}/floor.png", false, true, 2048, true),
                    bank = Texture($"{p}/bank.png", false, true, 2048, true),
                    plateau = Texture($"{p}/plateau.png", false, true, 2048, true),
                    floorNormal = Texture($"{p}/floor_normal.png", true, false),
                    bankNormal = Texture($"{p}/bank_normal.png", true, false),
                    plateauNormal = Texture($"{p}/plateau_normal.png", true, false),
                };
                if (File.Exists($"{p}/floor2.png"))
                {
                    set.floor2 = Texture($"{p}/floor2.png", false, true, 2048, true);
                    set.floor2Normal = Texture($"{p}/floor2_normal.png", true, false);
                }
                if (File.Exists($"{p}/floor3.png"))
                {
                    set.floor3 = Texture($"{p}/floor3.png", false, true, 2048, true);
                    set.floor3Normal = Texture($"{p}/floor3_normal.png", true, false);
                }
                if (set.bank == null) set.bank = set.floor;
                if (set.plateau == null) set.plateau = set.bank;
                list.Add(set);
            }
            return list;
        }

        /// <summary>Width of a PNG from its header (0 when missing).</summary>
        static int PngWidth(string path)
        {
            if (!File.Exists(path)) return 0;
            using (var f = File.OpenRead(path))
            {
                var b = new byte[24];
                if (f.Read(b, 0, 24) < 24) return 0;
                return (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            }
        }

        /// <summary>Import settings for a texture; returns the loaded asset (null when the file is missing).</summary>
        static Texture2D Texture(string path, bool normal, bool srgb, int maxSize = 2048, bool alphaIsData = false)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            bool changed = false;
            if (alphaIsData && ti.alphaIsTransparency) { ti.alphaIsTransparency = false; changed = true; }
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != type) { ti.textureType = type; changed = true; }
            if (!normal && ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; changed = true; }
            if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; changed = true; }
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (changed) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// A tangent-space normal map derived from a texture's brightness (bright = raised), so painted
        /// ground textures get surface relief. Written once to Generated/Textures and imported as a normal map.
        /// </summary>
        static Texture2D NormalFromAlbedo(string albedoPath, float strength)
        {
            string name = Path.GetFileNameWithoutExtension(albedoPath);
            string path = $"{Gen}/Textures/{name}_normal.png";
            if (!File.Exists(path) && File.Exists(albedoPath))
            {
                var src = new Texture2D(2, 2);
                src.LoadImage(File.ReadAllBytes(albedoPath));
                int w = src.width, h = src.height;
                Color[] px = src.GetPixels();
                var height = new float[w * h];
                for (int i = 0; i < px.Length; i++) height[i] = px[i].r * 0.3f + px[i].g * 0.55f + px[i].b * 0.15f;
                // one box blur pass so single noisy pixels do not become spikes
                var blurred = new float[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        sum += height[((y + dy + h) % h) * w + (x + dx + w) % w];
                    blurred[y * w + x] = sum / 9f;
                }
                var outPx = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float H(int ox, int oy) => blurred[((y + oy + h) % h) * w + (x + ox + w) % w];
                    float gx = (H(1, -1) + 2 * H(1, 0) + H(1, 1)) - (H(-1, -1) + 2 * H(-1, 0) + H(-1, 1));
                    float gy = (H(-1, 1) + 2 * H(0, 1) + H(1, 1)) - (H(-1, -1) + 2 * H(0, -1) + H(1, -1));
                    var n = new Vector3(-gx * strength, -gy * strength, 1f).normalized;
                    outPx[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                tex.SetPixels(outPx);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return Texture(path, true, false);
        }

        static Texture2D DotTexture()
        {
            string path = $"{Gen}/Textures/dot.png";
            if (!File.Exists(path))
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                    float a = Mathf.Clamp01(1 - d);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static PostProcessProfile PostProfile()
        {
            string path = $"{Gen}/PostProfile.asset";
            var p = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(p, path);
            var bloom = p.AddSettings<Bloom>();
            bloom.intensity.Override(2.2f);
            bloom.threshold.Override(0.85f);
            bloom.softKnee.Override(0.6f);
            bloom.diffusion.Override(7.5f);
            bloom.color.Override(new Color(0.85f, 1f, 0.97f));
            var grading = p.AddSettings<ColorGrading>();
            grading.tonemapper.Override(Tonemapper.ACES);
            grading.postExposure.Override(0.35f);
            grading.temperature.Override(-8f);
            grading.saturation.Override(6f);
            grading.contrast.Override(8f);
            var vignette = p.AddSettings<Vignette>();
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            var ao = p.AddSettings<AmbientOcclusion>();
            ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
            ao.intensity.Override(0.55f);
            foreach (var s in p.settings) AssetDatabase.AddObjectToAsset(s, p);
            EditorUtility.SetDirty(p);
            return p;
        }

        // ------------------------------------------------------------------ plants

        static Dictionary<string, object> Manifest(string path)
        {
            if (!File.Exists(path)) return null;
            return MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
        }

        /// <summary>The manifest entry for a name: either a top-level key or an item in a list.</summary>
        static object Entry(Dictionary<string, object> manifest, string name, string listKey)
        {
            if (manifest == null) return null;
            if (manifest.TryGetValue(name, out var direct)) return direct;
            foreach (string key in new[] { listKey, "items", "assets" })
            {
                if (manifest.TryGetValue(key, out var v) && v is List<object> list)
                    foreach (var item in list)
                        if (MiniJson.Txt(item, "name") == name || MiniJson.Txt(item, "id") == name) return item;
                if (manifest.TryGetValue(key, out var dv) && dv is Dictionary<string, object> dict && dict.TryGetValue(name, out var e)) return e;
            }
            return null;
        }

        static List<PlantRef> BuildPlants()
        {
            var list = new List<PlantRef>();
            string dir = $"{Root}/Art/Plants";
            var manifest = Manifest($"{dir}/plants.json");
            if (!Directory.Exists(dir) || manifest == null) return list; // the converter writes plants.json last
            string looksPath = $"{dir}/plant_looks.json";
            var looks = File.Exists(looksPath) ? MiniJson.Parse(File.ReadAllText(looksPath)) as Dictionary<string, object> : null;
            foreach (string sub in Directory.GetDirectories(dir).OrderBy(s => s))
            {
                string name = Path.GetFileName(sub);
                string fbx = $"{dir}/{name}/{name}.fbx";
                if (!File.Exists(fbx) || Entry(manifest, name, "plants") == null) continue;
                var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (mi == null) continue;
                bool dirty = false;
                if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
                if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                if (mi.isReadable) { mi.isReadable = false; dirty = true; }
                if (mi.importCameras || mi.importLights) { mi.importCameras = false; mi.importLights = false; dirty = true; }
                if (dirty) mi.SaveAndReimport();

                var e = Entry(manifest, name, "plants");
                var mat = MaterialAsset("Plant_" + name, "AntGarden/Plant");
                mat.enableInstancing = true;
                // big plants are converted at 4096 (they are seen up close): import them at full size
                string baseColor = $"{dir}/{name}/{name}_BaseColor.png";
                int full = Mathf.Clamp(PngWidth(baseColor), 512, 4096);
                mat.SetTexture("_MainTex", Texture(baseColor, false, true, full));
                mat.SetTexture("_BumpMap", Texture($"{dir}/{name}/{name}_Normal.png", true, false, full));
                mat.SetTexture("_ORM", Texture($"{dir}/{name}/{name}_ORM.png", false, false));
                PlantLook(mat, name, e, MiniJson.Obj(looks, name));
                EditorUtility.SetDirty(mat);

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var go = (GameObject)Object.Instantiate(model);
                go.name = name;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                    r.shadowCastingMode = r.name.EndsWith("_LOD2") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                TuneLods(go, new[] { 0.2f, 0.06f, 0.01f }); // full detail until a plant is small on screen
                string prefabPath = $"{Gen}/Plants/{name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                SurfacePoints(go, baseColor, mat, 40000, name.GetHashCode(), out Vector3[] points, out Color32[] pointColors);
                SavePoints(name, points, pointColors);
                Object.DestroyImmediate(go);

                var pr = new PlantRef { name = name, prefab = prefab };
                var size = MiniJson.Arr(e, "size");
                if (size != null && size.Count >= 3) pr.size = new Vector3((float)(double)size[0], (float)(double)size[1], (float)(double)size[2]);
                pr.footprintRadius = (float)FirstNum(e, 0.25, "footRadius", "footprintRadius", "footprint_radius", "baseRadius");
                var circles = MiniJson.Arr(e, "circles") ?? MiniJson.Arr(e, "footprint") ?? MiniJson.Arr(MiniJson.Obj(e, "footprint"), "circles");
                if (circles != null)
                    pr.circles = circles.OfType<List<object>>().Where(c => c.Count >= 3)
                        .Select(c => new Vector3((float)(double)c[0], (float)(double)c[1], (float)(double)c[2])).ToArray();
                list.Add(pr);
            }
            return list;
        }

        /// <summary>
        /// Points spread evenly over a plant's surface (its most detailed LOD), in the plant's own space, in random
        /// order, with their colours from the base colour texture; alpha 255 marks points in the glowing part (the
        /// material's glow bands, bright enough in the texture). For the particle look.
        /// </summary>
        static void SurfacePoints(GameObject go, string texPath, Material mat, int count, int seed, out Vector3[] points, out Color32[] colors)
        {
            points = new Vector3[0];
            colors = new Color32[0];
            var group = go.GetComponentInChildren<LODGroup>();
            Renderer[] renderers = group != null && group.GetLODs().Length > 0 ? group.GetLODs()[0].renderers : go.GetComponentsInChildren<MeshRenderer>();
            var tris = new List<(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)>();
            Matrix4x4 toRoot = go.transform.worldToLocalMatrix;
            foreach (Renderer r in renderers)
            {
                var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) continue;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                using (var arr = Mesh.AcquireReadOnlyMeshData(mf.sharedMesh))
                {
                    var data = arr[0];
                    var pos = new Unity.Collections.NativeArray<Vector3>(data.vertexCount, Unity.Collections.Allocator.Temp);
                    var uv = new Unity.Collections.NativeArray<Vector2>(data.vertexCount, Unity.Collections.Allocator.Temp);
                    data.GetVertices(pos);
                    bool hasUv = data.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0);
                    if (hasUv) data.GetUVs(0, uv);
                    for (int sm = 0; sm < data.subMeshCount; sm++)
                    {
                        var desc = data.GetSubMesh(sm);
                        if (desc.topology != MeshTopology.Triangles) continue;
                        var idx = new Unity.Collections.NativeArray<int>(desc.indexCount, Unity.Collections.Allocator.Temp);
                        data.GetIndices(idx, sm);
                        for (int i = 0; i + 2 < idx.Length; i += 3)
                            tris.Add((m.MultiplyPoint3x4(pos[idx[i]]), m.MultiplyPoint3x4(pos[idx[i + 1]]), m.MultiplyPoint3x4(pos[idx[i + 2]]),
                                      hasUv ? uv[idx[i]] : Vector2.zero, hasUv ? uv[idx[i + 1]] : Vector2.zero, hasUv ? uv[idx[i + 2]] : Vector2.zero));
                        idx.Dispose();
                    }
                    pos.Dispose();
                    uv.Dispose();
                }
            }
            if (tris.Count == 0) return;
            // pick triangles by area
            var cdf = new double[tris.Count];
            double total = 0;
            for (int i = 0; i < tris.Count; i++) { total += Vector3.Cross(tris[i].b - tris[i].a, tris[i].c - tris[i].a).magnitude * 0.5; cdf[i] = total; }
            Texture2D tex = null;
            if (File.Exists(texPath)) { tex = new Texture2D(2, 2, TextureFormat.RGBA32, false); tex.LoadImage(File.ReadAllBytes(texPath)); }
            float g1 = mat.GetFloat("_GlowMinY"), g2 = mat.GetFloat("_GlowMaxY"), h1 = mat.GetFloat("_Glow2MinY"), h2 = mat.GetFloat("_Glow2MaxY");
            float strength = mat.GetFloat("_GlowStrength"), lumMin = mat.GetFloat("_GlowLumMin");
            var rng = new System.Random(seed);
            points = new Vector3[count];
            colors = new Color32[count];
            for (int k = 0; k < count; k++)
            {
                double pick = rng.NextDouble() * total;
                int lo = 0, hi = cdf.Length - 1;
                while (lo < hi) { int mid = (lo + hi) / 2; if (cdf[mid] < pick) lo = mid + 1; else hi = mid; }
                var t = tris[lo];
                float r1 = Mathf.Sqrt((float)rng.NextDouble()), r2 = (float)rng.NextDouble();
                float wa = 1 - r1, wb = r1 * (1 - r2), wc = r1 * r2;
                Vector3 p = t.a * wa + t.b * wb + t.c * wc;
                Vector2 u = t.ua * wa + t.ub * wb + t.uc * wc;
                Color c = tex != null ? tex.GetPixelBilinear(u.x, u.y) : Color.gray;
                float lum = c.r * 0.3f + c.g * 0.55f + c.b * 0.15f;
                bool glows = strength > 0 && ((p.y >= g1 && p.y <= g2) || (p.y >= h1 && p.y <= h2)) && (lumMin <= 0 || lum >= lumMin);
                points[k] = p;
                colors[k] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(glows ? 255 : 0));
            }
            if (tex != null) Object.DestroyImmediate(tex);
        }

        /// <summary>A plant's surface points as Resources/AntGarden/Points/&lt;name&gt;.bytes: per point x, y, z (floats) and rgba bytes.</summary>
        static void SavePoints(string name, Vector3[] points, Color32[] colors)
        {
            string dir = $"{Root}/Resources/AntGarden/Points";
            Directory.CreateDirectory(dir);
            using (var w = new BinaryWriter(File.Create($"{dir}/{name}.bytes")))
            {
                w.Write(points.Length);
                for (int i = 0; i < points.Length; i++)
                {
                    w.Write(points[i].x); w.Write(points[i].y); w.Write(points[i].z);
                    w.Write(colors[i].r); w.Write(colors[i].g); w.Write(colors[i].b); w.Write(colors[i].a);
                }
            }
            AssetDatabase.ImportAsset($"{dir}/{name}.bytes");
        }

        static double FirstNum(object e, double def, params string[] keys)
        {
            foreach (string k in keys) if (MiniJson.Has(e, k) && MiniJson.Num(e, k, double.NaN) is double v && !double.IsNaN(v)) return v;
            var fp = MiniJson.Obj(e, "footprint");
            if (fp != null) foreach (string k in new[] { "radius", "r" }) if (MiniJson.Has(fp, k)) return MiniJson.Num(fp, k);
            return def;
        }

        /// <summary>
        /// Per-plant look: which height band glows and how much, sway, base blend. Measured glow ranges come
        /// from plants.json; everything else (and the fallback band) from Art/Plants/plant_looks.json.
        /// </summary>
        static void PlantLook(Material m, string name, object entry, Dictionary<string, object> look)
        {
            Vector2 glow = new Vector2(1f, 1.1f), glow2 = new Vector2(1f, 1.1f);
            var bands = new List<Vector2>();
            var gd = MiniJson.Obj(entry, "glow");
            if (gd != null)
                foreach (var v in gd.Values)
                    if (v is List<object> l && l.Count >= 2 && l[0] is double b0 && l[1] is double b1) bands.Add(new Vector2((float)b0, (float)b1));
            if (bands.Count > 1) glow2 = bands[1];
            bool doubleSided = MiniJson.Has(entry, "double_sided") && ((Dictionary<string, object>)entry)["double_sided"] is bool ds && ds;
            m.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.SetFloat("_Glow2MinY", glow2.x);
            m.SetFloat("_Glow2MaxY", glow2.y);
            var g = MiniJson.Obj(entry, "glow") ?? MiniJson.Obj(entry, "glow_ranges") ?? MiniJson.Obj(entry, "glowRanges");
            List<object> range = null;
            if (g != null) foreach (var v in g.Values) { if (v is List<object> l && l.Count >= 2 && l[0] is double) { range = l; break; } if (v is List<object> ll && ll.Count > 0 && ll[0] is List<object> inner) { range = inner; break; } }
            if (range == null) range = MiniJson.Arr(entry, "glow");
            if (range == null) range = MiniJson.Arr(look, "glow");
            if (range != null && range.Count >= 2 && range[0] is double a0 && range[1] is double a1) glow = new Vector2((float)a0, (float)a1);
            if (look == null) Debug.LogWarning($"[AntGarden] {name}: no entry in plant_looks.json, using defaults (no glow)");
            m.SetFloat("_GlowMinY", glow.x);
            m.SetFloat("_GlowMaxY", glow.y);
            m.SetFloat("_GlowStrength", (float)MiniJson.Num(look, "strength", 0));
            m.SetFloat("_GlowLumMin", (float)MiniJson.Num(look, "lumMin", 0));
            m.SetFloat("_GlowSoft", (float)MiniJson.Num(look, "soft", 0.06));
            m.SetFloat("_GlowUnder", (float)MiniJson.Num(look, "under", 0));
            m.SetFloat("_Sway", (float)MiniJson.Num(look, "sway", 0.02));
            m.SetFloat("_GroundBlend", (float)MiniJson.Num(look, "ground", 0.08));
            m.SetFloat("_RoughnessMin", (float)MiniJson.Num(look, "roughMin", 0.45));
        }

        static void TuneLods(GameObject go, float[] heights)
        {
            var group = go.GetComponentInChildren<LODGroup>();
            if (group == null) return;
            LOD[] lods = group.GetLODs();
            for (int i = 0; i < lods.Length && i < heights.Length; i++) lods[i].screenRelativeTransitionHeight = heights[i];
            group.SetLODs(lods);
            group.fadeMode = LODFadeMode.None;
            group.RecalculateBounds();
        }

        // ------------------------------------------------------------------ ants

        static List<AntRef> BuildAnts()
        {
            var list = new List<AntRef>();
            string dir = $"{Root}/Art/Ants";
            var manifest = Manifest($"{dir}/ants.json");
            if (!Directory.Exists(dir) || manifest == null) return list; // the exporter writes ants.json last
            foreach (string sub in Directory.GetDirectories(dir).OrderBy(s => s))
            {
                string id = Path.GetFileName(sub);
                string fbx = $"{dir}/{id}/{id}.fbx";
                var e = Entry(manifest, id, "ants");
                if (!File.Exists(fbx) || e == null) continue;

                var body = MaterialAsset($"{id}_Body", "AntGarden/AntShell");
                body.SetTexture("_MainTex", Texture($"{dir}/{id}/{id}_BaseColor.png", false, true));
                body.SetTexture("_BumpMap", Texture($"{dir}/{id}/{id}_Normal.png", true, false));
                body.SetTexture("_RoughMap", Texture($"{dir}/{id}/{id}_Roughness.png", false, false));
                var eye = MaterialAsset($"{id}_Eye", "AntGarden/AntShell");
                eye.SetColor("_Tint", new Color(0.05f, 0.08f, 0.14f));
                eye.SetTexture("_RoughMap", Texture2D.blackTexture);
                eye.SetFloat("_RimStrength", 0.4f);
                var wing = MaterialAsset($"{id}_Wing", "AntGarden/AntWing");
                wing.SetTexture("_MainTex", body.GetTexture("_MainTex"));
                wing.SetFloat("_Alpha", 0.34f);
                wing.renderQueue = 3000;
                var vein = MaterialAsset($"{id}_WingVein", "AntGarden/AntWing");
                vein.SetTexture("_MainTex", body.GetTexture("_MainTex"));
                vein.SetFloat("_Alpha", 0.9f);
                vein.renderQueue = 3001; // veins draw after the membrane
                foreach (var m in new[] { body, eye, wing, vein }) EditorUtility.SetDirty(m);

                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.importCameras = false;
                mi.importLights = false;
                mi.isReadable = false;
                mi.animationCompression = ModelImporterAnimationCompression.Off; // keyframe reduction makes feet slide
                var clips = mi.defaultClipAnimations;
                foreach (var c in clips)
                {
                    c.name = CleanClipName(c.takeName);
                    string n = c.name.ToLowerInvariant();
                    c.loopTime = !(n.Contains("takeoff") || n == "land" || n.EndsWith("_land"));
                    c.lockRootRotation = false;
                    c.lockRootHeightY = false;
                    c.lockRootPositionXZ = false;
                }
                mi.clipAnimations = clips;
                mi.SaveAndReimport();
                // remap the FBX's materials onto ours by name
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>())
                {
                    string n = m.name.ToLowerInvariant();
                    Material target = n.Contains("eye") ? eye : n.Contains("vein") ? vein : (n.Contains("membrane") || n.Contains("wing")) ? wing : body;
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
                }
                mi.SaveAndReimport();

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var go = (GameObject)Object.Instantiate(model);
                go.name = id;
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    r.shadowCastingMode = r.name.EndsWith("_LOD0") ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.skinnedMotionVectors = false;
                    // generous bounds: legs reach out while walking and wings spread wide in flight
                    var bnd = r.localBounds;
                    bnd.Expand(bnd.size.magnitude * 1.2f);
                    r.localBounds = bnd;
                }
                TuneLods(go, new[] { 0.22f, 0.05f, 0.006f });
                var animator = go.GetComponentInChildren<Animator>();
                if (animator != null) animator.runtimeAnimatorController = null;
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Gen}/Ants/{id}.prefab");
                Object.DestroyImmediate(go);

                var all = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
                AnimationClip Find(params string[] names)
                {
                    foreach (string nm in names) { var c = all.FirstOrDefault(x => x.name == nm); if (c != null) return c; }
                    foreach (string nm in names) { var c = all.FirstOrDefault(x => x.name.EndsWith(nm)); if (c != null) return c; }
                    return null;
                }
                var ar = new AntRef
                {
                    id = id,
                    prefab = prefab,
                    idle = Find("Idle"),
                    crawl = Find("Crawl_InPlace", "Crawl"),
                    fast = Find("FastCrawl_InPlace", "FastCrawl"),
                    talk = Find("AntennaTalk"),
                    takeoff = Find("TakeOff"),
                    fly = Find("Fly_InPlace", "Fly"),
                    land = Find("Land"),
                    modelBodyLength = (float)FirstOf(e, 1, "bodyLength", "body_length"),
                    modelCenterZ = (float)FirstOf(e, 0, "bodyCenterZ", "centerZ", "body_center_z", "centreOffset", "centerOffset"),
                    crawlCycleSpeed = (float)FirstOf(e, 0.15, "crawlCycleSpeed", "crawl_cycle_speed"),
                    fastCycleSpeed = (float)FirstOf(e, 0.45, "fastCycleSpeed", "fast_cycle_speed"),
                };
                var flight = MiniJson.Obj(e, "flight");
                if (flight != null)
                {
                    ar.takeoffForward = (float)FirstOf(flight, ar.takeoffForward, "takeoffForward", "takeoff_forward");
                    ar.takeoffRise = (float)FirstOf(flight, ar.takeoffRise, "takeoffRise", "rootRise", "takeoff_rise", "rise");
                    ar.landForward = (float)FirstOf(flight, ar.landForward, "landForward", "land_forward");
                }
                list.Add(ar);
            }
            return list;
        }

        static double FirstOf(object e, double def, params string[] keys)
        {
            foreach (string k in keys) if (MiniJson.Has(e, k) && MiniJson.Num(e, k, double.NaN) is double v && !double.IsNaN(v)) return v;
            return def;
        }

        static string CleanClipName(string take)
        {
            string n = take;
            int bar = n.LastIndexOf('|');
            if (bar >= 0) n = n.Substring(bar + 1);
            foreach (string prefix in new[] { "Ant_001_", "Ant_002_", "Ant_003_", "Ant_004_", "Ant_005_" })
                if (n.StartsWith(prefix)) n = n.Substring(prefix.Length);
            return n;
        }
    }
}
