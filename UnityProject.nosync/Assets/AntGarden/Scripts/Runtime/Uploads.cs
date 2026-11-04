using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// What visitors left (README 1.2): things of their own, photographed, each with a few words, standing beside a
    /// particle nest's way as its relics do and drawn of the same points (as relics are: in their own colours, in the
    /// nest's light and grain). An antenna's touch, or E, lights one and opens its card: its name, its words, its picture.
    /// The exports are read at run time from a folder (-uploads &lt;dir&gt;, else StreamingAssets/Uploads), never kept in the
    /// project, whose repository is public. Each export is a folder as it came (manifest.json: per thing its index, its
    /// name and words in English and as written, its cut-out picture) with our placement.json beside it: which of its
    /// things stand in which nest, how each is made, how large, and where along the way. -uploadshots &lt;dir&gt; takes a
    /// still of each thing and of its card, and quits.
    /// </summary>
    public class Uploads
    {
        [System.Serializable] class Manifest { public string exported; public Item[] items; }
        [System.Serializable] class Item { public int index; public string name, text, cutout; public Original original; }
        [System.Serializable] class Original { public string name, text, language; }
        [System.Serializable] class Plan { public Nest[] nests; }
        [System.Serializable] class Nest { public string nest; public Entry[] items; }

        /// <summary>One thing in a nest (placement.json).</summary>
        [System.Serializable]
        class Entry
        {
            public int item;      // its index in the manifest
            public string make;   // "model": a Tripo model (model: its .mesh, Tools/uploads/glb_to_mesh.py); "sleeve": a thin
                                  // square board, the picture on its front (a record's sleeve); "stones": each piece of the
                                  // picture raised into a rounded stone, lying as they lay when photographed from above
            public string model;
            public float size;    // its longest side (units; an ant is about one long)
            public float at;      // where beside the way (0 the way in, 1 the way out)
            public int side;      // which side of the way it would rather stand (1, -1; 0 either)
            public float thick;   // stones: how high for how wide (0.45)
            public bool stand;    // stones: stood up, their photographed face to the way (else lying as they lay)
            public float lean;    // a sleeve leaning back this far (degrees)
            public float turn;    // turned this far from facing the way (degrees)
            public float light;   // its colours lifted (the dark ones most) until their mean is this light (0..1; 0: 0.68)
            public float glow;    // this share of its points glowing in their own colours, the lighter ones more often (0:
                                  // none; as specks they can hide its face)
            public float shine;   // how bright the line of light its tide sends up it (0: 0.5)
            public int with;      // stands beside this one (its item, listed before it) under one ring: two things that
                                  // belong together (bought together); at and side are then that one's
        }

        class Thing
        {
            public int index;
            public string zhName, enName, zhText, enText, zhWhen, enWhen;
            public Texture2D picture;
            public Vector3 pos;       // its foot
            public Vector2 from;      // the point of the way beside it
            public float reach, top, at;
            public bool touching;
            public Halo halo;          // the ring of light round its foot, made of points
            public Thing partner;      // the one it stands beside under one ring ("with"; the first of the two keeps the ring)
            public float lit, want;    // how lit its ring is after it was read, following want (1 when read, fading)
            public Vector4 sphere;     // round it as placed (xyz its middle, w how far): the solid shader finds its splats by it
            public float foot, tall, phase, light;    // its tide: where its foot is (height), how tall it is, when, how bright
        }

        /// <summary>A thing's surface before it becomes points: triangles, and the picture its colours come from.</summary>
        class Shape
        {
            public readonly List<Vector3> pos = new List<Vector3>(), nrm = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> tris = new List<int>();
            public readonly List<float> shade = new List<float>();   // per triangle: the picture's colour times this (below 0: a grey this dark)
            public Texture2D tex;
            public Color32[] px;
            public int w, h;
        }

        static readonly Color Glow = new Color(1f, 0.82f, 0.5f);   // as the nest's relics (LabNest)
        const int PointCount = 200000;                          // as a plant's (AssetBuilder)

        readonly World world;
        readonly int region;
        readonly List<Thing> things = new List<Thing>();
        // the rings round their feet: white points, as in the reference the user sent (warm, they read as the gold specks
        // that lead along the way, 11-03; the user may send the colours they want)
        static readonly Color HaloColour = new Color(1.8f, 1.8f, 1.75f);
        const float RiseHeight = 0.7f;   // how high the few points rising off a ring go before they fade (a little spray)
        /// <summary>This share of a ring's points (1; the headset's QuestLook lowers it: 28 fps beside a visitor's model with
        /// its card open, 11-03). Set before Place.</summary>
        public static float HaloShare = 1f;
        RectTransform card;
        CanvasGroup cardGroup;
        RawImage cardPicture;
        Text cardTitle, cardSub, cardBody;
        int shown = -1;
        bool shownEn;
        float cardFor;
        static readonly Vector3[] tips = new Vector3[2];

        Uploads(World world, int region)
        {
            this.world = world;
            this.region = region;
        }

        public int Count => things.Count;
        public string NameOf(int i) => Loc.T(things[i].zhName, things[i].enName);

        /// <summary>Where the exports are: -uploads &lt;dir&gt;, else StreamingAssets/Uploads, else the app's own files folder
        /// (persistentDataPath/Uploads: on a headset StreamingAssets is inside the APK, and the exports are pushed there
        /// instead, as quest.json is); null when none is there.</summary>
        public static string Root
        {
            get
            {
                string arg = Game.Arg("-uploads");
                if (!string.IsNullOrEmpty(arg)) return Directory.Exists(arg) ? arg : null;
                string own = Path.Combine(Application.streamingAssetsPath, "Uploads");
                if (Directory.Exists(own)) return own;
                string files = Path.Combine(Application.persistentDataPath, "Uploads");
                return Directory.Exists(files) ? files : null;
            }
        }

        // the solid shader's: each thing's sphere and its tide (ParticleSolid.shader, _Exhibits)
        static readonly int ExhibitsId = Shader.PropertyToID("_Exhibits"), TidesId = Shader.PropertyToID("_ExhibitTide"),
            CountId = Shader.PropertyToID("_ExhibitCount"), PeriodId = Shader.PropertyToID("_ExhibitPeriod");
        const float TidePeriod = 7f;   // one tide: risen, sunk back, risen again (seconds)

        /// <summary>The shader's things forgotten (a world without them, or before they are placed).</summary>
        static void Forget(UnityEngine.SceneManagement.Scene _ = default) => Shader.SetGlobalFloat(CountId, 0f);

        /// <summary>How high its tide stands at time t (0 low .. 1 high), as the solid shader has it.</summary>
        static float Swell(float t, float phase) => 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / TidePeriod + phase);

        /// <summary>The visitors' things that stand in this nest, beside its way (before its plants grow, so they keep clear
        /// of them); null when there are none.</summary>
        public static Uploads Place(World world, Region region, LabNest lab, RuntimeRefs refs, Transform parent)
        {
            Forget();
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= Forget;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += Forget;
            string root = Root;
            List<Vector2> track = lab?.Route?.mainTrack;
            if (root == null || track == null || track.Count < 4) return null;
            var u = new Uploads(world, region.index);
            var holder = new GameObject("VisitorThings").transform;
            holder.SetParent(parent, false);
            var folders = new List<string>();
            try
            {
                if (File.Exists(Path.Combine(root, "manifest.json"))) folders.Add(root);
                else folders.AddRange(Directory.GetDirectories(root));
            }
            catch (System.Exception e)
            {
                // (a folder adb made can be the shell's alone, which the app may not list: the headset's, 11-03; uncaught,
                // its world never finished loading)
                Debug.LogWarning($"[AntGarden] uploads: {root} not listed ({e.Message})");
            }
            folders.Sort(System.StringComparer.Ordinal);   // the same order every run
            foreach (string dir in folders)
            {
                string manifestPath = Path.Combine(dir, "manifest.json"), planPath = Path.Combine(dir, "placement.json");
                if (!File.Exists(manifestPath) || !File.Exists(planPath)) continue;
                Manifest manifest;
                Plan plan;
                try
                {
                    manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
                    plan = JsonUtility.FromJson<Plan>(File.ReadAllText(planPath));
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AntGarden] uploads: {Path.GetFileName(dir)} not read ({e.Message})");
                    continue;
                }
                if (manifest?.items == null || plan?.nests == null) continue;
                foreach (Nest nest in plan.nests)
                {
                    if (nest.nest != region.biome.id || nest.items == null) continue;
                    foreach (Entry e in nest.items)
                    {
                        Item item = System.Array.Find(manifest.items, it => it.index == e.item);
                        if (item == null) { Debug.LogWarning($"[AntGarden] uploads: no #{e.item} in {Path.GetFileName(dir)}"); continue; }
                        try { u.Add(dir, manifest, item, e, track, lab, refs, holder); }
                        catch (System.Exception ex) { Debug.LogWarning($"[AntGarden] uploads: #{e.item} not made ({ex.Message})"); }
                    }
                }
            }
            Debug.Log($"[AntGarden] uploads: {u.things.Count} visitors' things in {region.biome.id} (from {root})");
            if (u.things.Count == 0) return null;
            // a line of light rising up them and sinking back like a tide: the solid shader lights their splats (it knows them
            // by where they are)
            var spheres = new Vector4[8];
            var tides = new Vector4[8];
            for (int i = 0; i < u.things.Count && i < 8; i++)
            {
                Thing t = u.things[i];
                spheres[i] = t.sphere;
                tides[i] = new Vector4(t.foot, t.tall, t.phase, t.light);
            }
            Shader.SetGlobalVectorArray(ExhibitsId, spheres);
            Shader.SetGlobalVectorArray(TidesId, tides);
            Shader.SetGlobalFloat(PeriodId, TidePeriod);
            Shader.SetGlobalFloat(CountId, Mathf.Min(8, u.things.Count));
            string shots = Game.Arg("-uploadshots");
            if (!string.IsNullOrEmpty(shots) && Game.Instance != null) Game.Instance.StartCoroutine(u.Shots(Game.Instance, shots, track));
            string film = Game.Arg("-uploadfilm");
            if (!string.IsNullOrEmpty(film) && Game.Instance != null) Game.Instance.StartCoroutine(u.Film(Game.Instance, film, track));
            return u;
        }

        void Add(string dir, Manifest manifest, Item item, Entry e, List<Vector2> track, LabNest lab, RuntimeRefs refs, Transform holder)
        {
            Texture2D picture = Picture(Path.Combine(dir, item.cutout));
            Shape shape = e.make == "model" ? Model(Path.Combine(dir, e.model))
                : e.make == "sleeve" ? Sleeve(picture)
                : Stones(picture, e.thick > 0f ? e.thick : 0.45f, e.stand);
            if (shape == null || shape.tris.Count == 0) throw new System.Exception("nothing to make it of");
            // at height 1, its foot at the origin (as a plant's points are)
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            foreach (Vector3 p in shape.pos) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            float height = Mathf.Max(1e-5f, hi.y - lo.y);
            var foot = new Vector3((lo.x + hi.x) * 0.5f, lo.y, (lo.z + hi.z) * 0.5f);
            for (int i = 0; i < shape.pos.Count; i++) shape.pos[i] = (shape.pos[i] - foot) / height;
            Vector3 size = (hi - lo) / height;
            var plant = new PlantRef { name = "upload_" + item.index, size = size, footprintRadius = 0.5f * Mathf.Max(size.x, size.z) };
            Sample(shape, PointCount, 7919 * item.index, plant);
            Lift(plant, e.light > 0f ? e.light : 0.68f, Mathf.Max(0f, e.glow), 104729 * item.index);   // (0.62 a little dark still, 11-03)

            // beside the way, facing it
            float longest = e.size > 0f ? e.size : 2f;
            float scale = longest / Mathf.Max(1f, Mathf.Max(size.x, size.z));
            // (leaning back, it lies over more of the floor, behind its foot)
            float lean = e.lean * Mathf.Deg2Rad, behind = scale * Mathf.Sin(Mathf.Max(0f, lean));
            float r = Mathf.Max(0.3f, Mathf.Max(plant.footprintRadius * scale, 0.5f * (behind + scale * size.z * Mathf.Cos(lean))));
            // (two that belong together, "with": this one beside the other, under one ring; where there is no room there, as any)
            Thing partner = e.with > 0 ? things.Find(o => o.index == e.with) : null;
            Vector2 c = default, a = default;
            if (partner != null && !Beside(partner, r, longest, lab.Objects, out c, out a)) partner = null;
            if (partner == null && !Spot(track, e.at, e.side, r, longest, lab.Objects, out c, out a)) throw new System.Exception("no room beside the way");
            float yaw = Mathf.Atan2(a.x - c.x, a.y - c.y) * Mathf.Rad2Deg + e.turn;
            float y = world.Height(c.x, c.y);
            for (int q = 0; q < 6; q++)
            {
                float ang = q * Mathf.PI / 3;
                y = Mathf.Min(y, world.Height(c.x + Mathf.Cos(ang) * r, c.y + Mathf.Sin(ang) * r));
            }
            y -= (e.make == "stones" ? 0.1f : 0.03f) * scale;   // a little sunk, as things lying a while are
            // its foot toward the way, so what it lies over is where it was given room
            Vector2 foot2 = c + (a - c).normalized * (0.5f * behind);
            var go = new GameObject("Visitor_" + item.index);
            go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(new Vector3(foot2.x, y, foot2.y), Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-e.lean, 0f, 0f));
            go.transform.localScale = Vector3.one * scale;
            Show(go, shape, refs);
            plant.prefab = go;
            world.plantInstances.Add(new PlantInstance { plant = plant, t = go.transform, hue = 0, sat = 1, glow = Glow, scan = new Color(1f, 0.93f, 0.8f), relic = true, exhibit = true });
            world.placements.Add(new Placement { plant = plant.name, region = region, x = c.x, z = c.y, height = scale });
            world.reserved.Add(new Circle(c.x, c.y, r + 1.2f, 1f));   // (and a little room round it: a plant stood over one, 11-02)
            // nothing grown between it and the way, so it is seen whole from there (as the amber nest's cones, LabNest), nor
            // between it and where one coming along the way first sees it, six units before it (corals hid two, 11-02)
            for (float f = 0.25f; f < 1f; f += 0.25f) world.reserved.Add(new Circle(Mathf.Lerp(a.x, c.x, f), Mathf.Lerp(a.y, c.y, f), r, 1f));
            int nearest = 0;
            for (int j = 1; j < track.Count; j++) if ((track[j] - a).sqrMagnitude < (track[nearest] - a).sqrMagnitude) nearest = j;
            int seenFrom = nearest;
            for (float run = 0f; seenFrom > 0 && run < 6f; seenFrom--) run += (track[seenFrom] - track[seenFrom - 1]).magnitude;
            Vector2 seen = track[seenFrom];
            float sight = (c - seen).magnitude;
            for (float s = 1.2f; s < sight - r; s += 1.2f) world.reserved.Add(new Circle(Mathf.Lerp(seen.x, c.x, s / sight), Mathf.Lerp(seen.y, c.y, s / sight), 1.4f, 1f));
            float top = scale * Mathf.Cos(e.lean * Mathf.Deg2Rad);
            // low stones are climbed over, as the nest's coins are; the rest stand in the way
            if (top < 0.8f) world.steps.Add(new Circle(c.x, c.y, r * 1.05f, top * 0.9f));
            else world.colliders.Add(new Circle(c.x, c.y, r, scale));
            lab.Objects.Add((plant.name, c, a));

            // its words: as written and in English (a thing written in English shows its English in both)
            bool zh = item.original != null && item.original.language == "zh";
            var t = new Thing
            {
                index = item.index, picture = picture, pos = new Vector3(c.x, y, c.y), from = a, reach = r + 0.25f, top = top, at = e.at,
                enName = item.name, enText = (item.text ?? "").Trim(),
                zhName = zh && !string.IsNullOrEmpty(item.original.name) ? item.original.name : item.name,
                zhText = (zh && !string.IsNullOrEmpty(item.original.text) ? item.original.text : item.text ?? "").Trim(),
            };
            if (System.DateTime.TryParse(manifest.exported, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out System.DateTime when))
            {
                t.zhWhen = $"{when.Year} 年 {when.Month} 月";
                t.enWhen = when.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }
            // its tide (the user, 11-03: on the ground as all else, "像潮汐这样律动的发光"): a soft line of light rising up its
            // lower part and sinking back, slowly; the solid shader lights its splats, found inside its sphere (round the
            // corners of its bounds as placed)
            t.phase = partner != null ? partner.phase : Mathf.Repeat(item.index * 2.399f, 2f * Mathf.PI);   // (a pair in time)
            t.light = e.shine > 0f ? e.shine : 0.5f;
            var corners = new Vector3[8];
            Vector3 mid = Vector3.zero;
            for (int q = 0; q < 8; q++)
            {
                corners[q] = go.transform.TransformPoint(new Vector3((q & 1) == 0 ? -0.5f * size.x : 0.5f * size.x, (q & 2) == 0 ? 0f : 1f, (q & 4) == 0 ? -0.5f * size.z : 0.5f * size.z));
                mid += corners[q] / 8f;
            }
            float round = 0f;
            foreach (Vector3 corner in corners) round = Mathf.Max(round, (corner - mid).magnitude);
            t.sphere = new Vector4(mid.x, mid.y, mid.z, round * 1.05f + 0.05f);
            // its tide rises from the ground at its foot up its height
            float highest = float.MinValue;
            foreach (Vector3 corner in corners) highest = Mathf.Max(highest, corner.y);
            t.foot = world.Height(c.x, c.y);
            t.tall = Mathf.Max(0.3f, highest - t.foot);
            // and round its foot a ring of light made of points, ebbing and flowing with it: here is something to read
            if (partner == null) t.halo = MakeHalo(holder, refs, c, r + 0.35f, item.index);
            else
            {
                // (a pair: one ring round the two, kept by the first)
                var c0 = new Vector2(partner.pos.x, partner.pos.z);
                if (partner.halo != null) UnityEngine.Object.Destroy(partner.halo.ps.gameObject);
                partner.halo = MakeHalo(holder, refs, (c0 + c) * 0.5f, 0.5f * (c - c0).magnitude + Mathf.Max(partner.reach - 0.25f, r) + 0.35f, partner.index);
                if (partner.halo != null) UpdateHalo(partner.halo, Swell(NestClock.SinceLoad, partner.phase), 1f, NestClock.SinceLoad);
                partner.partner = t;
                t.partner = partner;
            }
            if (t.halo != null) UpdateHalo(t.halo, Swell(NestClock.SinceLoad, t.phase), 1f, NestClock.SinceLoad);
            things.Add(t);
            Debug.Log($"[AntGarden] uploads: #{item.index} ({e.make}) at ({c.x:F1}, {c.y:F1}), {longest:F1} long, {e.at:P0} along the way");
        }

        /// <summary>Beside a thing already placed ("with"), along the way from it, a hand's breadth between them: on fairly
        /// flat floor, clear of the nest's relics, in from the rim (as Spot).</summary>
        bool Beside(Thing p, float r, float size, List<(string id, Vector2 at, Vector2 from)> relics, out Vector2 c, out Vector2 a)
        {
            Vector2 c0 = new Vector2(p.pos.x, p.pos.z), n = (c0 - p.from).normalized, d = new Vector2(-n.y, n.x);
            float apart = (p.reach - 0.25f) + r + 0.25f;
            for (float extra = 0f; extra < 1f; extra += 0.25f)
            for (int s = 1; s >= -1; s -= 2)
            {
                c = c0 + d * (s * (apart + extra));
                a = p.from + d * (s * (apart + extra));
                if (world.HexDist(c.x, c.y) > -(r + 1.5f) || world.Dist(c.x, c.y) > 1f || world.colliders.Overlaps(c.x, c.y, r * 0.8f)) continue;
                float lo = float.MaxValue, hi = float.MinValue;
                for (int q = 0; q < 8; q++)
                {
                    float ang = q * Mathf.PI / 4;
                    float h = world.Height(c.x + Mathf.Cos(ang) * r * 0.8f, c.y + Mathf.Sin(ang) * r * 0.8f);
                    lo = Mathf.Min(lo, h);
                    hi = Mathf.Max(hi, h);
                }
                if (hi - lo > Mathf.Max(0.45f, 0.2f * size)) continue;
                bool clear = true;
                foreach (var (id, at0, _) in relics)
                {
                    if (id.StartsWith("upload_")) continue;
                    float room = 1f;
                    foreach (Circle o in world.reserved.Near(at0.x, at0.y))
                        if (Mathf.Abs(o.x - at0.x) < 0.05f && Mathf.Abs(o.z - at0.y) < 0.05f) room = Mathf.Max(room, o.r);
                    if ((at0 - c).magnitude < room + r + 2f) { clear = false; break; }
                }
                if (clear) return true;
            }
            c = a = Vector2.zero;
            return false;
        }

        /// <summary>Near a thing's foot, out of the way, on fairly flat floor (as LabNest finds its relics' places): near where
        /// it is wanted along the way, on the side it would rather stand, the nearer the way the better. Never up on the
        /// rim: they are to be come upon. Away from the nest's relics, which hid them (a large relic stood before one),
        /// and far from the other visitors' things, which ran together (the way winds back: two of them 6 apart).</summary>
        bool Spot(List<Vector2> track, float at, int side, float r, float size, List<(string id, Vector2 at, Vector2 from)> relics, out Vector2 c, out Vector2 a)
        {
            float length = 0;
            var along = new List<float> { 0 };
            for (int i = 1; i < track.Count; i++) along.Add(length += (track[i] - track[i - 1]).magnitude);
            float want = Mathf.Clamp01(at) * length;
            int k = 1;
            while (k < track.Count - 2 && along[k] < want) k++;
            c = a = track[k];
            if (side == 0) side = 1;
            bool found = false;
            float best = float.MaxValue;
            // how much room each relic needs: the ground kept for it (LabNest reserves its footprint; the teacup is large)
            var room = new float[relics.Count];
            for (int i = 0; i < relics.Count; i++)
            {
                room[i] = 1f;
                foreach (Circle o in world.reserved.Near(relics[i].at.x, relics[i].at.y))
                    if (Mathf.Abs(o.x - relics[i].at.x) < 0.05f && Mathf.Abs(o.z - relics[i].at.y) < 0.05f) room[i] = Mathf.Max(room[i], o.r);
            }
            // (first only where it keeps clear of them, 2 between its edge and a relic's and 9 from another visitor's thing,
            // near where it is wanted, then anywhere along the way; where there is no such place, the nearest to that)
            for (int pass = 0; pass < 3 && !found; pass++)
            for (int dk = pass == 1 ? -k : -40; dk <= (pass == 1 ? track.Count - k : 40); dk += 2)
            {
                int kk = Mathf.Clamp(k + dk, 1, track.Count - 2);
                Vector2 q = track[kk], qd = (track[kk + 1] - track[kk - 1]).normalized;
                for (int s = 0; s < 2; s++)
                {
                    Vector2 qn = new Vector2(-qd.y, qd.x) * (s == 0 ? side : -side);
                    for (float off = r + 0.6f; off < r + 8f; off += 0.3f)
                    {
                        Vector2 cc = q + qn * off;
                        if (world.HexDist(cc.x, cc.y) > -(r + 1.5f) || world.Dist(cc.x, cc.y) > 1f) continue;
                        if (world.reserved.Overlaps(cc.x, cc.y, r * 0.95f) || world.colliders.Overlaps(cc.x, cc.y, r * 0.8f)) continue;
                        float lo = float.MaxValue, hi = float.MinValue;
                        for (int qa = 0; qa < 8; qa++)
                        {
                            float ang = qa * Mathf.PI / 4;
                            float hh = world.Height(cc.x + Mathf.Cos(ang) * r * 0.8f, cc.y + Mathf.Sin(ang) * r * 0.8f);
                            lo = Mathf.Min(lo, hh);
                            hi = Mathf.Max(hi, hh);
                        }
                        if (hi - lo > Mathf.Max(0.45f, 0.2f * size)) continue;
                        float score = Mathf.Abs(dk) * 0.6f + off + (s == 0 ? 0 : 1.5f);
                        bool crowded = false;
                        for (int ri = 0; ri < relics.Count; ri++)
                        {
                            var (id, at0, _) = relics[ri];
                            float dr = (at0 - cc).magnitude, clear = id.StartsWith("upload_") ? 9f : room[ri] + r + 2f;
                            if (dr >= clear) continue;
                            crowded = true;
                            score += (clear - dr) * 3f;
                        }
                        if (pass < 2 && crowded) continue;
                        if (score < best) { best = score; c = cc; a = q; found = true; }
                    }
                }
            }
            return found;
        }

        // ---------------------------------------------------------------- shapes

        static Texture2D Picture(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!File.Exists(path) || !tex.LoadImage(File.ReadAllBytes(path))) throw new System.Exception("no picture " + Path.GetFileName(path));
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static Shape Pixels(Texture2D tex) => new Shape { tex = tex, px = tex.GetPixels32(), w = tex.width, h = tex.height };

        /// <summary>A model made by Tools/uploads/glb_to_mesh.py: its .mesh and its _tex picture.</summary>
        static Shape Model(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length < 12 || raw[0] != 'A' || raw[1] != 'G' || raw[2] != 'M' || raw[3] != '1') throw new System.Exception("not a model (AGM1): " + Path.GetFileName(path));
            int nv = System.BitConverter.ToInt32(raw, 4), nt = System.BitConverter.ToInt32(raw, 8);
            var f = new float[nv * 8];
            var idx = new int[nt * 3];
            System.Buffer.BlockCopy(raw, 12, f, 0, nv * 32);
            System.Buffer.BlockCopy(raw, 12 + nv * 32, idx, 0, nt * 12);
            string stem = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
            Shape s = Pixels(Picture(File.Exists(stem + "_tex.jpg") ? stem + "_tex.jpg" : stem + "_tex.png"));
            for (int i = 0; i < nv; i++)
            {
                s.pos.Add(new Vector3(f[8 * i], f[8 * i + 1], f[8 * i + 2]));
                s.nrm.Add(new Vector3(f[8 * i + 3], f[8 * i + 4], f[8 * i + 5]));
                s.uv.Add(new Vector2(f[8 * i + 6], f[8 * i + 7]));
            }
            s.tris.AddRange(idx);
            for (int i = 0; i < nt; i++) s.shade.Add(1f);
            return s;
        }

        /// <summary>A record's sleeve: a thin board as wide for its height as the picture's opaque part, standing on its
        /// lower edge; that part on its front (+z) and, darkened, on its back; its edges a dark grey.</summary>
        static Shape Sleeve(Texture2D picture)
        {
            Shape s = Pixels(picture);
            int x0 = s.w, y0 = s.h, x1 = -1, y1 = -1;
            for (int y = 0; y < s.h; y++)
                for (int x = 0; x < s.w; x++)
                    if (s.px[y * s.w + x].a > 128) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            if (x1 < 0) return null;
            float wide = (x1 - x0 + 1f) / (y1 - y0 + 1f), half = wide * 0.5f, th = 0.035f, z = th * 0.5f;
            var uv0 = new Vector2(x0 / (float)s.w, y0 / (float)s.h);
            var du = new Vector2((x1 + 1f - x0) / s.w, 0f);
            var dv = new Vector2(0f, (y1 + 1f - y0) / s.h);
            Quad(s, new Vector3(-half, 0, z), new Vector3(wide, 0, 0), Vector3.up, 40, 40, Vector3.forward, uv0, du, dv, 1f);
            // (its back the same picture, dark)
            Quad(s, new Vector3(half, 0, -z), new Vector3(-wide, 0, 0), Vector3.up, 40, 40, Vector3.back, uv0, du, dv, 0.3f);
            Quad(s, new Vector3(-half, 1, -z), new Vector3(wide, 0, 0), new Vector3(0, 0, th), 40, 1, Vector3.up, uv0, du, dv, -0.12f);
            Quad(s, new Vector3(-half, 0, -z), new Vector3(0, 1, 0), new Vector3(0, 0, th), 40, 1, Vector3.left, uv0, du, dv, -0.12f);
            Quad(s, new Vector3(half, 0, -z), new Vector3(0, 1, 0), new Vector3(0, 0, th), 40, 1, Vector3.right, uv0, du, dv, -0.12f);
            return s;
        }

        /// <summary>A grid of nu × nv quads from o along u and v, facing normal, its picture from uv0 along du and dv.</summary>
        static void Quad(Shape s, Vector3 o, Vector3 u, Vector3 v, int nu, int nv, Vector3 normal, Vector2 uv0, Vector2 du, Vector2 dv, float shade)
        {
            int start = s.pos.Count;
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                {
                    float fu = i / (float)nu, fv = j / (float)nv;
                    s.pos.Add(o + u * fu + v * fv);
                    s.nrm.Add(normal);
                    s.uv.Add(uv0 + du * fu + dv * fv);
                }
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int p00 = start + j * (nu + 1) + i, p10 = p00 + 1, p01 = p00 + nu + 1, p11 = p01 + 1;
                    Triangle(s, p00, p01, p10, normal, shade);
                    Triangle(s, p10, p01, p11, normal, shade);
                }
        }

        /// <summary>One triangle, turned so its front faces the given way (Unity's: clockwise seen from the front).</summary>
        static void Triangle(Shape s, int a, int b, int c, Vector3 front, float shade)
        {
            if (Vector3.Dot(Vector3.Cross(s.pos[b] - s.pos[a], s.pos[c] - s.pos[a]), front) < 0) (b, c) = (c, b);
            s.tris.Add(a);
            s.tris.Add(b);
            s.tris.Add(c);
            s.shade.Add(shade);
        }

        /// <summary>Stones photographed from above: each piece of the picture's opaque part raised into a rounded stone, as
        /// high in the middle as thick × its width there (a stone's section, steep at its edge), lying as they lay, or stood
        /// up on its edge with its photographed face to the front (a low camera sees little of what lies flat).</summary>
        static Shape Stones(Texture2D picture, float thick, bool stand)
        {
            Shape s = Pixels(picture);
            // a working grid of the picture, at most 300 cells across
            float step = Mathf.Max(1f, Mathf.Max(s.w, s.h) / 300f);
            int gw = Mathf.Max(2, Mathf.RoundToInt(s.w / step)), gh = Mathf.Max(2, Mathf.RoundToInt(s.h / step));
            var inside = new bool[gw * gh];
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    int px = Mathf.Min(s.w - 1, (int)((x + 0.5f) * s.w / gw)), py = Mathf.Min(s.h - 1, (int)((y + 0.5f) * s.h / gh));
                    inside[y * gw + x] = s.px[py * s.w + px].a > 128;
                }
            // its pieces (side by side counts as touching), specks left out
            var piece = new int[gw * gh];
            var sizes = new List<int> { 0 };
            var queue = new Queue<int>();
            for (int i = 0; i < inside.Length; i++)
            {
                if (!inside[i] || piece[i] != 0) continue;
                int id = sizes.Count, n = 0;
                piece[i] = id;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue(), cx = cur % gw, cy = cur / gw;
                    n++;
                    if (cx > 0 && inside[cur - 1] && piece[cur - 1] == 0) { piece[cur - 1] = id; queue.Enqueue(cur - 1); }
                    if (cx < gw - 1 && inside[cur + 1] && piece[cur + 1] == 0) { piece[cur + 1] = id; queue.Enqueue(cur + 1); }
                    if (cy > 0 && inside[cur - gw] && piece[cur - gw] == 0) { piece[cur - gw] = id; queue.Enqueue(cur - gw); }
                    if (cy < gh - 1 && inside[cur + gw] && piece[cur + gw] == 0) { piece[cur + gw] = id; queue.Enqueue(cur + gw); }
                }
                sizes.Add(n);
            }
            int largest = 0;
            foreach (int n in sizes) largest = Mathf.Max(largest, n);
            for (int i = 0; i < piece.Length; i++) if (piece[i] != 0 && sizes[piece[i]] < largest * 0.02f) piece[i] = 0;
            // how far in from its piece's edge each cell lies (chamfer 3-4, then in cells)
            var d = new float[gw * gh];
            for (int i = 0; i < d.Length; i++) d[i] = piece[i] != 0 ? 1e9f : 0f;
            float D(int x, int y) => x < 0 || y < 0 || x >= gw || y >= gh ? 0f : d[y * gw + x];
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                    if (d[y * gw + x] > 0f) d[y * gw + x] = Mathf.Min(d[y * gw + x], Mathf.Min(Mathf.Min(D(x - 1, y) + 3, D(x, y - 1) + 3), Mathf.Min(D(x - 1, y - 1) + 4, D(x + 1, y - 1) + 4)));
            for (int y = gh - 1; y >= 0; y--)
                for (int x = gw - 1; x >= 0; x--)
                    if (d[y * gw + x] > 0f) d[y * gw + x] = Mathf.Min(d[y * gw + x], Mathf.Min(Mathf.Min(D(x + 1, y) + 3, D(x, y + 1) + 3), Mathf.Min(D(x + 1, y + 1) + 4, D(x - 1, y + 1) + 4)));
            var deepest = new float[sizes.Count];
            for (int i = 0; i < d.Length; i++) { d[i] /= 3f; if (piece[i] != 0) deepest[piece[i]] = Mathf.Max(deepest[piece[i]], d[i]); }
            // one vertex per cell, raised (nothing at its edge, so its face and back meet there). Lying, the picture's up is
            // the stones' far side (+z); stood up, it is up, each piece on its own lowest edge, its face (the picture) to
            // the front (+z) and a flatter back behind it
            var low = new float[sizes.Count];
            for (int p = 0; p < low.Length; p++) low[p] = float.MaxValue;
            for (int i = 0; i < piece.Length; i++) if (piece[i] != 0) low[piece[i]] = Mathf.Min(low[piece[i]], i / gw);
            var vertex = new int[gw * gh];
            var behind = new int[gw * gh];
            for (int i = 0; i < vertex.Length; i++)
            {
                vertex[i] = behind[i] = -1;
                if (piece[i] == 0) continue;
                float dm = Mathf.Max(1f, deepest[piece[i]]), t = Mathf.Clamp01((d[i] - 1f) / Mathf.Max(1f, dm - 1f));
                float h = thick * 2f * dm * Mathf.Sqrt(1f - (1f - t) * (1f - t));
                int x = i % gw, y = i / gw;
                var uv = new Vector2((x + 0.5f) / gw, (y + 0.5f) / gh);
                vertex[i] = s.pos.Count;
                s.pos.Add(stand ? new Vector3(x + 0.5f, y - low[piece[i]] + 0.5f, h) : new Vector3(x + 0.5f, h, y + 0.5f));
                s.nrm.Add(Vector3.zero);
                s.uv.Add(uv);
                if (!stand) continue;
                behind[i] = s.pos.Count;
                s.pos.Add(new Vector3(x + 0.5f, y - low[piece[i]] + 0.5f, -0.6f * h));
                s.nrm.Add(Vector3.zero);
                s.uv.Add(uv);
            }
            Vector3 front = stand ? Vector3.forward : Vector3.up;
            for (int y = 0; y < gh - 1; y++)
                for (int x = 0; x < gw - 1; x++)
                {
                    int i = y * gw + x, p = piece[i];
                    if (p == 0 || piece[i + 1] != p || piece[i + gw] != p || piece[i + gw + 1] != p) continue;
                    Triangle(s, vertex[i], vertex[i + gw], vertex[i + 1], front, 1f);
                    Triangle(s, vertex[i + 1], vertex[i + gw], vertex[i + gw + 1], front, 1f);
                    if (!stand) continue;
                    Triangle(s, behind[i], behind[i + gw], behind[i + 1], Vector3.back, 0.7f);
                    Triangle(s, behind[i + 1], behind[i + gw], behind[i + gw + 1], Vector3.back, 0.7f);
                }
            if (stand)
            {
                // each piece turned a little its own way about its foot (side by side in one plane, stood up, they read as
                // a row of headstones, 11-02)
                var footX = new float[sizes.Count];
                var footN = new int[sizes.Count];
                for (int i = 0; i < piece.Length; i++)
                    if (piece[i] != 0 && i / gw <= low[piece[i]] + 2) { footX[piece[i]] += i % gw + 0.5f; footN[piece[i]]++; }
                var turn = new Quaternion[sizes.Count];
                var rng = new System.Random(911);
                for (int p = 1; p < sizes.Count; p++)
                    turn[p] = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 24f, (float)(rng.NextDouble() - 0.5) * 60f, (float)(rng.NextDouble() - 0.5) * 16f);
                // and gathered close, a small heap, left to right as they lay (photographed apart, they stood far from one
                // another at an ant's scale, 11-02)
                var left = new float[sizes.Count];
                var right = new float[sizes.Count];
                for (int p = 0; p < sizes.Count; p++) { left[p] = float.MaxValue; right[p] = float.MinValue; }
                for (int i = 0; i < piece.Length; i++)
                    if (piece[i] != 0) { left[piece[i]] = Mathf.Min(left[piece[i]], i % gw); right[piece[i]] = Mathf.Max(right[piece[i]], i % gw + 1f); }
                var pieces = new List<int>();
                float widths = 0f;
                for (int p = 1; p < sizes.Count; p++) if (right[p] > left[p]) { pieces.Add(p); widths += right[p] - left[p]; }
                pieces.Sort((u, v) => left[u].CompareTo(left[v]));
                var shift = new float[sizes.Count];
                float cursor = pieces.Count > 0 ? left[pieces[0]] : 0f;
                foreach (int p in pieces)
                {
                    shift[p] = cursor - left[p];
                    cursor += right[p] - left[p] + 0.2f * widths / pieces.Count;
                }
                for (int i = 0; i < piece.Length; i++)
                {
                    int p = piece[i];
                    if (p == 0) continue;
                    var move = new Vector3(shift[p], 0f, 0f);
                    var pivot = new Vector3(footX[p] / Mathf.Max(1, footN[p]), 0f, 0f) + move;
                    s.pos[vertex[i]] = pivot + turn[p] * (s.pos[vertex[i]] + move - pivot);
                    s.pos[behind[i]] = pivot + turn[p] * (s.pos[behind[i]] + move - pivot);
                }
            }
            // smooth normals from the faces round each vertex
            for (int k = 0; k < s.tris.Count; k += 3)
            {
                int a = s.tris[k], b = s.tris[k + 1], c = s.tris[k + 2];
                Vector3 face = Vector3.Cross(s.pos[b] - s.pos[a], s.pos[c] - s.pos[a]);
                s.nrm[a] += face;
                s.nrm[b] += face;
                s.nrm[c] += face;
            }
            for (int i = 0; i < s.nrm.Count; i++) s.nrm[i] = s.nrm[i].sqrMagnitude > 1e-12f ? s.nrm[i].normalized : Vector3.up;
            return s;
        }

        /// <summary>Its mesh, as it looks with the particle look off (P): its picture on a relic's material.</summary>
        static void Show(GameObject go, Shape s, RuntimeRefs refs)
        {
            PlantRef like = refs.Plant("relic_teacup");
            Renderer template = like?.prefab != null ? like.prefab.GetComponentInChildren<Renderer>() : null;
            if (template == null || template.sharedMaterial == null) return;
            var mesh = new Mesh { indexFormat = s.pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(s.pos);
            mesh.SetNormals(s.nrm);
            mesh.SetUVs(0, s.uv);
            mesh.SetTriangles(s.tris, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = new Material(template.sharedMaterial) { mainTexture = s.tex };
        }

        // ---------------------------------------------------------------- points

        /// <summary>Its points, as AssetBuilder.SurfacePoints makes a plant's: over its surface in random order, denser where
        /// its picture changes (edges, lines, spots), with their colours (none glowing), normals, and how much denser than an
        /// even spread each lies. Given to its PlantRef, which then needs no file.</summary>
        static void Sample(Shape s, int count, int seed, PlantRef into)
        {
            int n = s.tris.Count / 3;
            var areas = new float[n];
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = s.pos[s.tris[3 * i]], b = s.pos[s.tris[3 * i + 1]], c = s.pos[s.tris[3 * i + 2]];
                areas[i] = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                total += areas[i];
            }
            double mean = total / Mathf.Max(1, n), sum = 0;
            var cdf = new double[n];
            var rel = new float[n];
            for (int i = 0; i < n; i++)
            {
                float detail = 0f;
                if (s.shade[i] > 0f)
                {
                    Vector2 ua = s.uv[s.tris[3 * i]], ub = s.uv[s.tris[3 * i + 1]], uc = s.uv[s.tris[3 * i + 2]], um = (ua + ub + uc) / 3f;
                    Color c0 = Colour(s, ua), c1 = Colour(s, ub), c2 = Colour(s, uc), c3 = Colour(s, um), c4 = Colour(s, (ua + um) * 0.5f);
                    float l0 = Lum(c0), l1 = Lum(c1), l2 = Lum(c2), l3 = Lum(c3), l4 = Lum(c4), lm = (l0 + l1 + l2 + l3 + l4) / 5f;
                    float spread = Mathf.Sqrt(((l0 - lm) * (l0 - lm) + (l1 - lm) * (l1 - lm) + (l2 - lm) * (l2 - lm) + (l3 - lm) * (l3 - lm) + (l4 - lm) * (l4 - lm)) / 5f);
                    float chroma = (Chroma(c0, c3) + Chroma(c1, c3) + Chroma(c2, c3)) / 3f;
                    detail = Mathf.Clamp01(spread * 7f + chroma * 2.5f);
                }
                double w = areas[i] * (1 + 2.5 * detail) + 0.3 * mean;
                sum += w;
                cdf[i] = sum;
                rel[i] = (float)(w / Mathf.Max(1e-12f, areas[i]));
            }
            float even = (float)(sum / total);
            var rng = new System.Random(seed);
            var pts = new Vector3[count];
            var cols = new Color32[count];
            var nrms = new uint[count];
            var dens = new float[count];
            for (int k = 0; k < count; k++)
            {
                double pick = rng.NextDouble() * sum;
                int lo = 0, hi = n - 1;
                while (lo < hi) { int mid = (lo + hi) / 2; if (cdf[mid] < pick) lo = mid + 1; else hi = mid; }
                int ia = s.tris[3 * lo], ib = s.tris[3 * lo + 1], ic = s.tris[3 * lo + 2];
                float r1 = Mathf.Sqrt((float)rng.NextDouble()), r2 = (float)rng.NextDouble();
                float wa = 1 - r1, wb = r1 * (1 - r2), wc = r1 * r2;
                pts[k] = s.pos[ia] * wa + s.pos[ib] * wb + s.pos[ic] * wc;
                float shade = s.shade[lo];
                Color c = shade < 0f ? new Color(-shade, -shade, -shade) : Colour(s, s.uv[ia] * wa + s.uv[ib] * wb + s.uv[ic] * wc) * shade;
                cols[k] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 0);
                nrms[k] = ThingPen.EncodeNormal((s.nrm[ia] * wa + s.nrm[ib] * wb + s.nrm[ic] * wc).normalized);
                dens[k] = Mathf.Clamp(rel[lo] / even, 0.07f, 15f);
            }
            into.points = pts;
            into.pointColors = cols;
            into.pointNormals = nrms;
            into.pointDensity = dens;
            into.pointArea = (float)total;
        }

        /// <summary>
        /// So it reads in the nest's night before it is touched: each point's lightness lifted along one curve (the dark
        /// ones most, white kept white) until their mean is this light, its hue and saturation kept (all three channels
        /// scaled alike; one that would pass white let go a little toward grey). A relic's points are its texture's colours
        /// under the moon (ParticleLook), and things people photograph are often dark ones. Optionally a share of its
        /// points glowing in their own colours, the lighter ones more often (off: as white specks over its face they hid
        /// what it was, 11-02).
        /// </summary>
        static void Lift(PlantRef p, float light, float glowShare, int seed)
        {
            Color32[] cols = p.pointColors;
            int n = cols.Length;
            float Lum32(Color32 c) => (c.r * 0.3f + c.g * 0.55f + c.b * 0.15f) / 255f;
            double sum = 0;
            for (int k = 0; k < n; k++) sum += Lum32(cols[k]);
            float mean = Mathf.Clamp((float)(sum / Mathf.Max(1, n)), 0.02f, 0.98f);
            float gamma = mean < light ? Mathf.Clamp(Mathf.Log(light) / Mathf.Log(mean), 0.55f, 1f) : 1f;
            var weight = new float[n];
            double wsum = 0;
            for (int k = 0; k < n; k++)
            {
                Color32 c = cols[k];
                float l = Mathf.Max(1e-3f, Lum32(c)), want = Mathf.Pow(l, gamma), g = want / l;
                float r = c.r / 255f * g, gg = c.g / 255f * g, b = c.b / 255f * g, top = Mathf.Max(r, Mathf.Max(gg, b));
                if (top > 1f)
                {
                    float t = (top - 1f) / Mathf.Max(1e-3f, top - want);
                    r = Mathf.Lerp(r, want, t);
                    gg = Mathf.Lerp(gg, want, t);
                    b = Mathf.Lerp(b, want, t);
                }
                cols[k] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(gg) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f), 0);
                wsum += weight[k] = want;
            }
            if (glowShare <= 0f) return;
            float wmean = Mathf.Max(1e-3f, (float)(wsum / Mathf.Max(1, n)));
            var rng = new System.Random(seed);
            for (int k = 0; k < n; k++)
                if (rng.NextDouble() < glowShare * Mathf.Clamp(weight[k] / wmean, 0.25f, 2.5f)) cols[k].a = 255;
        }

        /// <summary>The picture's colour at uv (its rows bottom up), see-through pixels left out of the blend.</summary>
        static Color Colour(Shape s, Vector2 uv)
        {
            float x = Mathf.Clamp(uv.x * s.w - 0.5f, 0f, s.w - 1), y = Mathf.Clamp(uv.y * s.h - 0.5f, 0f, s.h - 1);
            int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, s.w - 1), y1 = Mathf.Min(y0 + 1, s.h - 1);
            float fx = x - x0, fy = y - y0;
            Color32 p00 = s.px[y0 * s.w + x0], p10 = s.px[y0 * s.w + x1], p01 = s.px[y1 * s.w + x0], p11 = s.px[y1 * s.w + x1];
            float w00 = (1 - fx) * (1 - fy) * (p00.a + 1), w10 = fx * (1 - fy) * (p10.a + 1), w01 = (1 - fx) * fy * (p01.a + 1), w11 = fx * fy * (p11.a + 1);
            float k = 1f / (255f * (w00 + w10 + w01 + w11));
            return new Color((p00.r * w00 + p10.r * w10 + p01.r * w01 + p11.r * w11) * k,
                             (p00.g * w00 + p10.g * w10 + p01.g * w01 + p11.g * w11) * k,
                             (p00.b * w00 + p10.b * w10 + p01.b * w01 + p11.b * w11) * k, 1f);
        }

        static float Lum(Color c) => c.r * 0.3f + c.g * 0.55f + c.b * 0.15f;
        static float Chroma(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // ---------------------------------------------------------------- touching and reading

        /// <summary>The thing within reach of an ant at p (the nearest), or -1.</summary>
        public int Near(Vector3 p)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < things.Count; i++)
            {
                float d = Flat(p, things[i].pos) - things[i].reach;
                if (d < 1.2f && d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>A thing answers a touch or E: its card opens, a chime, and its ring lights up (the thing itself is not
        /// flared: lit as a touched plant is, its face went white, 11-02).</summary>
        public void Read(int i, Hud hud)
        {
            Thing t = things[i];
            if (card == null) MakeCard(hud);
            Fill(i);
            shown = i;
            cardFor = 6f;
            t.want = 1f;
            Voices.Chime(t.pos + Vector3.up, 1.15f, 0.4f);
        }

        /// <summary>Antenna touches (a thing touched opens its card); the rings, breathing slowly, brighter as the ant comes
        /// near and when their thing is read; the card stays while the ant is near, then fades.</summary>
        public void Tick(float dt, AntController player, Hud hud)
        {
            tips[0] = player.AntennaTip(0);
            tips[1] = player.AntennaTip(1);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                float d = Mathf.Min(Flat(tips[0], t.pos), Flat(tips[1], t.pos));
                bool touch = d < t.reach && Mathf.Min(tips[0].y, tips[1].y) < t.pos.y + t.top + 0.3f;
                if (touch && !t.touching) Read(i, hud);
                if (d > t.reach + 0.6f) t.touching = false;
                else if (touch) t.touching = true;
                // (read: its ring brightens over a second and goes back slowly, never at once: "不是突然高亮", 11-03)
                t.want = Mathf.Max(0f, t.want - dt / 5f);
                t.lit = Mathf.MoveTowards(t.lit, t.want, dt);
                float far = Flat(player.Position, t.pos);
                if (t.halo == null || far > 50f) continue;   // (out of sight: left as it was)
                float near = 1f - Mathf.Clamp01((far - t.reach) / 6f);
                float lt = t.partner != null ? Mathf.Max(t.lit, t.partner.lit) : t.lit;   // (a pair's ring: either read)
                float lit = lt * lt * (3f - 2f * lt);
                float now = NestClock.SinceLoad;
                UpdateHalo(t.halo, Swell(now, t.phase), (1f + 0.3f * near) * (1f + 0.45f * lit), now);
            }
            if (shown >= 0)
            {
                Thing s = things[shown];
                if (Flat(player.Position, s.pos) < s.reach + 3.5f) cardFor = Mathf.Max(cardFor, 1.5f);
                cardFor -= dt;
                if (Input.GetKeyDown(KeyCode.Escape) || Pad.Back) cardFor = 0f;
                if (shownEn != Loc.En) Fill(shown);   // L switched the language while it is open
                if (cardFor <= 0f) shown = -1;
            }
            if (cardGroup != null) cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, shown >= 0 ? 1f : 0f, dt * (shown >= 0 ? 4f : 1.5f));
        }

        /// <summary>
        /// The ring of light round a thing's foot, made of points (the reference the user sent, 11-03: an uneven rim, dense and
        /// bright, a few points scattered inside and outside it; and a few rising off it like spray), its light ebbing and
        /// flowing with the thing's tide, a slow wave of light running round it, brighter as the ant comes near and when the
        /// thing is read. Points set by hand each frame (no emission), drawn as the nest's spores are (GlowUnlit, additive).
        /// </summary>
        sealed class Halo
        {
            public ParticleSystem ps;
            public ParticleSystem.Particle[] parts;
            public Vector3[] at;                         // each point's place at low tide (a rising one: where it rises from)
            public float[] ang, size, bright, rise, age; // round the ring, how big, how bright, how fast it rises (0: it stays), when
        }

        Halo MakeHalo(Transform holder, RuntimeRefs refs, Vector2 c, float radius, int seed)
        {
            if (refs.spores == null) return null;
            var go = new GameObject("VisitorHalo_" + seed);
            go.transform.SetParent(holder, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // (as many points again, larger and brighter, after the first: "可以再突出一些", 11-03)
            int rim = Mathf.RoundToInt(340f * radius * HaloShare), loose = rim * 2 / 5, rising = Mathf.Max(Mathf.RoundToInt(30f * HaloShare), rim / 10), n = rim + loose + rising;
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1e5f;
            main.startSpeed = 0f;
            main.maxParticles = n;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = new Material(refs.spores);
            pr.sharedMaterial.SetColor("_Color", HaloColour);
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.maxParticleSize = 0.025f;   // (a camera right beside one saw its near points as large white blots: the headset's, 11-03)
            var h = new Halo { ps = ps, parts = new ParticleSystem.Particle[n], at = new Vector3[n], ang = new float[n], size = new float[n], bright = new float[n], rise = new float[n], age = new float[n] };
            var rng = new System.Random(seed * 31 + 7);
            float R() => (float)rng.NextDouble();
            float Gauss() => Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(1e-6f, R()))) * Mathf.Cos(2f * Mathf.PI * R());
            // its rim uneven, as a hand-drawn circle: a few slow waves round it
            float w1 = R() * 6.2832f, w2 = R() * 6.2832f;
            float Rim(float a) => radius * (1f + 0.05f * Mathf.Sin(3f * a + w1) + 0.03f * Mathf.Sin(7f * a + w2));
            for (int i = 0; i < n; i++)
            {
                float a = R() * 6.2832f, rr, up = 0f;
                if (i < rim) { rr = Rim(a) * (1f + 0.05f * Gauss()); up = 0.06f * Mathf.Abs(Gauss()); h.size[i] = 0.05f + 0.05f * R(); h.bright[i] = 0.7f + 0.3f * R(); }
                else if (i < rim + loose) { rr = Rim(a) * (R() < 0.6f ? 1.06f + 0.35f * R() : 0.55f + 0.38f * R()); up = 0.04f * R(); h.size[i] = 0.03f + 0.03f * R(); h.bright[i] = 0.3f + 0.4f * R(); }
                else { rr = Rim(a) * (0.95f + 0.1f * R()); h.size[i] = 0.04f + 0.03f * R(); h.bright[i] = 0.6f + 0.4f * R(); h.rise[i] = 0.2f + 0.25f * R(); h.age[i] = R(); }
                float x = c.x + Mathf.Cos(a) * rr, z = c.y + Mathf.Sin(a) * rr;
                h.at[i] = new Vector3(x, world.WalkHeight(x, z) + 0.08f + up, z);   // (a little off the ground: its splats hid the lowest)
                h.ang[i] = a;
            }
            for (int i = 0; i < n; i++) { h.parts[i].startLifetime = h.parts[i].remainingLifetime = 1e5f; h.parts[i].startSize = h.size[i]; }
            ps.SetParticles(h.parts, n);
            ps.Play();
            return h;
        }

        /// <summary>A ring's points this frame: its tide (0 low .. 1 high), how much brighter (near, read), the time.</summary>
        static void UpdateHalo(Halo h, float swell, float boost, float t)
        {
            int n = h.at.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = h.at[i];
                // ebbing and flowing with the tide, a slow wave of light running round the ring
                float b = h.bright[i] * (0.5f + 0.5f * swell) * (0.75f + 0.25f * Mathf.Sin(2f * h.ang[i] - 1.2f * t)) * boost;
                if (h.rise[i] > 0f)
                {
                    // spray: rising off the rim, higher at high tide, fading in and out
                    float age = Mathf.Repeat(h.age[i] + t * h.rise[i] / RiseHeight, 1f);
                    p.y += age * RiseHeight * (0.6f + 0.4f * swell);
                    b *= Mathf.Sin(age * Mathf.PI);
                }
                else p.y += 0.05f * swell;
                h.parts[i].position = p;
                h.parts[i].startSize = h.size[i];
                h.parts[i].startColor = new Color(1f, 1f, 1f, Mathf.Clamp01(b));
                h.parts[i].remainingLifetime = 1e5f;
            }
            h.ps.SetParticles(h.parts, n);
        }

        /// <summary>The card, above the prompt bar: the thing's picture on the left, its name, who left it and its words.</summary>
        void MakeCard(Hud hud)
        {
            Transform root = hud.canvas.transform;
            Transform old = root.Find("VisitorCard");
            if (old != null) Object.Destroy(old.gameObject);
            card = UiKit.Rect(root, "VisitorCard", 0.5f, 0f, new Vector2(0, 176), new Vector2(1060, 236));
            UiKit.Panel(card, new Color(0.02f, 0.06f, 0.07f, 0.86f));
            cardPicture = UiKit.Rect(card, "Picture", 0f, 0.5f, new Vector2(24, 0), new Vector2(190, 190)).gameObject.AddComponent<RawImage>();
            cardPicture.raycastTarget = false;
            var words = UiKit.Rect(card, "Words", 0f, 1f, new Vector2(238, -20), new Vector2(1060 - 238 - 30, 236 - 40));
            cardTitle = UiKit.Label(words, "Title", "", 24, UiKit.Warm, TextAnchor.UpperLeft);
            cardSub = UiKit.Label(words, "Who", "", 16, UiKit.Dim, TextAnchor.UpperLeft);
            cardSub.rectTransform.offsetMax = new Vector2(0, -36);
            cardBody = UiKit.Label(words, "Words", "", 21, UiKit.Ink, TextAnchor.UpperLeft);
            cardBody.rectTransform.offsetMax = new Vector2(0, -66);
            cardBody.lineSpacing = 1.1f;
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
        }

        void Fill(int i)
        {
            Thing t = things[i];
            shownEn = Loc.En;
            cardTitle.text = "<b>" + Loc.T(Unbroken(t.zhName), t.enName) + "</b>";
            cardSub.text = Loc.T("一位访客留下的", "Left by a visitor") + (string.IsNullOrEmpty(t.zhWhen) ? "" : " · " + Loc.T(t.zhWhen, t.enWhen));
            cardBody.text = Loc.T(Unbroken(t.zhText), t.enText);
            if (!Loc.En) Unstopped(cardBody);
            // the picture whole in its square; the card as tall as its words need
            float pw = t.picture != null ? t.picture.width : 1f, ph = t.picture != null ? t.picture.height : 1f, k = 190f / Mathf.Max(pw, ph);
            cardPicture.texture = t.picture;
            cardPicture.rectTransform.sizeDelta = new Vector2(pw * k, ph * k);
            cardPicture.rectTransform.anchoredPosition = new Vector2(24 + (190 - pw * k) * 0.5f, 0);
            float tall = Mathf.Max(236f, 66 + 40 + cardBody.preferredHeight + 10);
            card.sizeDelta = new Vector2(1060, tall);
        }

        /// <summary>Chinese with Latin words in it, for Unity's Text, which breaks lines only at spaces (a Latin word with
        /// Chinese after it went down a line whole, the rest of the sentence with it): its spaces made non-breaking.</summary>
        static string Unbroken(string s) => s?.Replace(' ', ' ');

        // what a line of Chinese never starts with: stops, commas, closing brackets
        const string NoLineStart = "，。、：；！？）」』》〉】…,.:;!?)";

        /// <summary>Chinese words laid out in a label so that no line starts with a stop or a comma (Unity's Text breaks
        /// Chinese anywhere: lines began with "：" and "、", the headset's session found, 11-03): the character before such
        /// a line's first goes down to it, after a line break.</summary>
        static void Unstopped(Text label)
        {
            for (int pass = 0; pass < 12; pass++)
            {
                TextGenerator gen = label.cachedTextGenerator;
                gen.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
                IList<UILineInfo> lines = gen.lines;
                int moveAt = -1;
                for (int l = 1; l < lines.Count && moveAt < 0; l++)
                {
                    int at = lines[l].startCharIdx;
                    if (at >= 2 && at < label.text.Length && NoLineStart.IndexOf(label.text[at]) >= 0 && label.text[at - 2] != '\n') moveAt = at - 1;
                }
                if (moveAt < 0) return;
                label.text = label.text.Insert(moveAt, "\n");
            }
        }

        // ---------------------------------------------------------------- checks

        /// <summary>-uploadshots: each thing as one coming along the way sees it (untouched), from across the way, then its
        /// card as an antenna's touch opens it (lit); quits when done.</summary>
        IEnumerator Shots(Game game, string dir, List<Vector2> track)
        {
            Directory.CreateDirectory(dir);
            while (!game.Ready) yield return null;
            game.demo = true;
            game.hud.SetStatsVisible(false);
            game.hud.SetHelpVisible(false);
            yield return new WaitForSeconds(7f);   // shaders compile, the NPCs spread out, the chapter's card goes
            var order = new List<int>();
            for (int i = 0; i < things.Count; i++) order.Add(i);
            order.Sort((x, y) => things[x].at.CompareTo(things[y].at));
            int shot = 0;
            foreach (int i in order)
            {
                Thing t = things[i];
                var at = new Vector2(t.pos.x, t.pos.z);
                // coming along the way, six units before it, looking at it
                int near = 0;
                for (int j = 1; j < track.Count; j++) if ((track[j] - t.from).sqrMagnitude < (track[near] - t.from).sqrMagnitude) near = j;
                int back = near;
                for (float run = 0f; back > 0 && run < 6f; back--) run += (track[back] - track[back - 1]).magnitude;
                DemoMode.PlaceFacing(game, track[back], at);
                DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
                yield return new WaitForSeconds(2.5f);
                yield return SceneShots.ShellSettles();
                yield return Grab(Path.Combine(dir, $"{++shot}a_{t.index:000}_come.png"));
                // nearer, still on the way (out of its antennae's reach), facing it
                back = near;
                for (float run = 0f; back > 0 && run < 2.5f; back--) run += (track[back] - track[back - 1]).magnitude;
                DemoMode.PlaceFacing(game, track[back], at);
                DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
                yield return new WaitForSeconds(2.5f);
                yield return SceneShots.ShellSettles();
                yield return Grab(Path.Combine(dir, $"{shot}b_{t.index:000}_view.png"));
                Read(i, game.hud);
                yield return new WaitForSeconds(0.9f);
                yield return Grab(Path.Combine(dir, $"{shot}c_{t.index:000}_card.png"));
                shown = -1;
                yield return new WaitForSeconds(5f);   // its flare fades, its card goes
            }
            Debug.Log("[AntGarden] uploadshots: done");
            Application.Quit();
        }

        /// <summary>-uploadfilm: for each thing, coming along the way to it, the ant reaching out its antennae to it, its
        /// ring lit and its card open (frames at 30 a second, for Tools/frames_to_video.sh); quits when done.</summary>
        IEnumerator Film(Game game, string dir, List<Vector2> track)
        {
            Directory.CreateDirectory(dir);
            while (!game.Ready) yield return null;
            Application.targetFrameRate = -1;
            Time.captureFramerate = DemoMode.Fps;
            game.demo = true;
            game.demoKeys = null;
            game.hud.SetStatsVisible(false);
            game.hud.SetHelpVisible(false);
            yield return new WaitForSeconds(7f);   // shaders compile, the NPCs spread out, the chapter's card goes
            var end = new WaitForEndOfFrame();
            var order = new List<int>();
            for (int i = 0; i < things.Count; i++) order.Add(i);
            order.Sort((x, y) => things[x].at.CompareTo(things[y].at));
            int f = 0;
            foreach (int i in order)
            {
                Thing t = things[i];
                var at = new Vector2(t.pos.x, t.pos.z);
                int near = 0;
                for (int j = 1; j < track.Count; j++) if ((track[j] - t.from).sqrMagnitude < (track[near] - t.from).sqrMagnitude) near = j;
                int start = near;
                for (float run = 0f; start > 0 && run < 7f; start--) run += (track[start] - track[start - 1]).magnitude;
                game.hud.SetFade(1f, true);
                DemoMode.PlaceFacing(game, track[start], track[Mathf.Min(track.Count - 1, start + 3)]);
                DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
                game.demoMove = Vector2.zero;
                for (int k = 0; k < 20; k++) yield return null;   // the camera settles behind the ant
                game.hud.SetFade(0f);
                int next = start;
                float held = -1f;
                for (int k = 0; k < 14 * DemoMode.Fps; k++, f++)
                {
                    Vector2 pos = DemoMode.Pos(game);
                    while (next < near && (track[next] - pos).sqrMagnitude < 2.5f * 2.5f) next++;
                    float toThing = (at - pos).magnitude - t.reach;
                    if (shown == i && held < 0f) held = 0f;   // read: it stops, and stays on its card
                    if (held < 0f && k == 9 * DemoMode.Fps) Read(i, game.hud);   // (kept from it by something: read all the same)
                    if (held >= 0f)
                    {
                        game.demoMove = Vector2.zero;
                        held += 1f / DemoMode.Fps;
                        if (Mathf.Abs(held - 3.6f) < 0.5f / DemoMode.Fps) game.hud.SetFade(1f);
                    }
                    else
                    {
                        Vector2 d = (next < near ? track[next] : at) - pos;
                        game.demoMove = d.sqrMagnitude < 0.04f ? Vector2.zero : d.normalized * (next >= near && toThing < 1.5f ? 0.5f : 1f);
                        if (next >= near && toThing < 1.2f) game.player.talkTimer = Mathf.Max(game.player.talkTimer, 0.9f);   // its antennae reach out
                    }
                    yield return end;
                    Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, $"f{f:00000}.jpg"), tex.EncodeToJPG(88));
                    Object.Destroy(tex);
                    if (held >= 4.1f) { f++; break; }
                    yield return null;
                }
                shown = -1;
                game.demoMove = Vector2.zero;
            }
            Time.captureFramerate = 0;
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Debug.Log($"[AntGarden] uploadfilm: {f} frames");
            Application.Quit();
        }

        static IEnumerator Grab(string path)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
