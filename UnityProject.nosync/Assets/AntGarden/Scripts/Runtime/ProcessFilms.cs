using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Films of how the game was made, for the team's presentation (11-04). Film runs only: nothing here runs in play,
    /// and no nest looks any different for it.
    /// <para>-turntablefilm &lt;dir&gt;: a plant, or the player's ant, alone on a dark ground with the camera turning slowly
    /// round it, as the game draws it. The plants (-ttplants a,b,c; default the nest's first finely made kind) are grown
    /// far above the nest with its own plants (Stage, from Game.Build after the scatter and before the particle look
    /// gathers the plants), so each is drawn exactly as the nest's plants are: its splats bent and tinted its own way,
    /// in the nest's palette and light, with its markings and motes. The ant (-ttant 1) is held in its standing pose up
    /// there, filmed first as its mesh in its own materials, then as the points the game draws it with. Frames at
    /// 1920x1080, 30 a second (&lt;dir&gt;/&lt;subject&gt;/f00000.jpg); film.json beside them has every frame's camera and what
    /// the game did to the subject (a plant's bend, twist and stretch; the ant's skin), so its model and its stored points
    /// can be drawn outside the game from the same eye (Tools/process_film) and faded into these frames.
    /// -ttseconds 14, -ttturn 0.55 (of a turn), -ttfov 40, -ttpitch 12, -ttyaw 215, -ttheight (else its rule's middle).</para>
    /// <para>-nestpointsfilm &lt;dir&gt;: the nest from high above, the camera circling it slowly from SceneShots.Aerial's view,
    /// as the game draws it; and every splat of it written out first (solid.bin: the floor's things, the plants' coarse
    /// splats, the giants, the vines, SolidPoint as it is; ground.bin: the ground's points, position and packed colour),
    /// so its raw points can be drawn outside the game from the same eye and the film can go from the points to the
    /// finished nest. -npseconds 18, -npturn 0.2, -nppitch 55, -npyaw 200.</para>
    /// </summary>
    public static class ProcessFilms
    {
        const int W = 1920, H = 1080;
        // the studio: this far above the nest's middle (inside the 200 units high bounds the nest's splats are drawn with, or
        // the whole draw is culled; above its walls, beams and giants, which the far plane leaves out); its subjects this far apart
        const float StudioUp = 75f, StudioGap = 60f;
        // and each plant this far across from where the ant stands below it: close enough for its fine splats (drawn out to
        // 10 round the ant), too far for it to part round the ant (1.3) or brighten for it (4.5, the splats' near lift)
        const float StudioSide = 7.5f;

        struct Subject { public string kind; public PlantRef plant; public Transform t; public float height; public Vector2 floor; }
        static readonly List<Subject> subjects = new List<Subject>();
        static Vector3 studio;
        static RenderTexture rt;
        static Texture2D grab;

        static float Num(string arg, float v) =>
            Game.Arg(arg) is string a && float.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ? x : v;

        static string F(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>Game.Build, after the scatter: the plants to film, each grown far above the nest (StudioUp over its
        /// middle), above a spot of its way a little to one side (where the ant will stand while it is filmed), before the
        /// particle look gathers the nest's plants. Nothing without -turntablefilm.</summary>
        public static void Stage(Game game, Transform root)
        {
            subjects.Clear();
            if (string.IsNullOrEmpty(Game.Arg("-turntablefilm")) || !Game.Lab || game.world == null || game.world.regions.Count == 0) return;
            World world = game.world;
            Region reg = world.regions[0];
            float cx = (reg.minX + reg.maxX) / 2, cz = (reg.minZ + reg.maxZ) / 2;
            studio = new Vector3(cx, world.Height(cx, cz) + StudioUp, cz);
            string list = Game.Arg("-ttplants") ?? (NestTheme.Current?.labPlants != null && NestTheme.Current.labPlants.Length > 0 ? NestTheme.Current.labPlants[0] : "");
            var kinds = new List<string>();
            foreach (string part in list.Split(',')) if (part.Trim().Length > 0) kinds.Add(part.Trim());
            List<Vector2> track = game.LabNest?.Track;
            for (int i = 0; i < kinds.Count; i++)
            {
                string kind = kinds[i];
                PlantRef plant = game.refs.Plant(kind);
                if (plant == null || plant.prefab == null) { Debug.LogWarning($"[AntGarden] turntable: no plant {kind}"); continue; }
                float h = Tall(game.cfg.scatter.rules, plant);
                // the ant's spot on the way (spread along it, never within half a StudioGap of another's: they would see each
                // other's plants), the plant up above, StudioSide across from it. (Shifted off the way, 11-04, the ant stood in
                // a wall, was pushed out of it, and the plant lost its fine splats.)
                Vector2 floor = new Vector2(cx + i * StudioGap, cz);
                if (track != null && track.Count > 8)
                {
                    float want = 0.2f + 0.6f * i / Mathf.Max(1, kinds.Count - 1);
                    for (int k = 0; k < 40; k++)
                    {
                        float along = Mathf.Repeat(want + (k % 2 == 0 ? 1 : -1) * 0.025f * ((k + 1) / 2), 1f);
                        floor = track[Mathf.Clamp(Mathf.RoundToInt((track.Count - 1) * along), 0, track.Count - 1)];
                        bool clear = true;
                        foreach (Subject o in subjects) if ((o.floor - floor).sqrMagnitude < StudioGap * StudioGap * 0.25f) clear = false;
                        if (clear) break;
                    }
                }
                GameObject go = Object.Instantiate(plant.prefab, new Vector3(floor.x + StudioSide, studio.y, floor.y), Quaternion.identity, root);
                go.name = "Turntable_" + kind;
                go.transform.localScale = Vector3.one * h;
                // as Scatter.Place grows a plant of this region
                bool native = GameConfig.InList(reg.biome.nativePlants, kind);
                world.plantInstances.Add(new PlantInstance { plant = plant, t = go.transform, hue = native ? 0 : reg.biome.plantHue,
                    sat = native ? 1 : reg.biome.plantSaturation, glow = reg.biome.plantGlow, scan = Color.Lerp(reg.biome.rimColor, Color.white, 0.55f) });
                subjects.Add(new Subject { kind = kind, plant = plant, t = go.transform, height = h, floor = floor });
                Debug.Log($"[AntGarden] turntable: {kind}, {h:F2} tall, at {go.transform.position}");
            }
        }

        /// <summary>How tall to grow it: -ttheight, or the middle of the height its nest grows it at; never so large that the
        /// game would split its stored points up close (ParticleLook.SolidShell: 16000 splats a unit² of its surface, at most
        /// 200000), so its grain stays the same all round the turn.</summary>
        static float Tall(ScatterRule[] rules, PlantRef plant)
        {
            float h = 2.6f;
            if (Game.Arg("-ttheight") != null) return Num("-ttheight", h);
            if (rules != null)
                foreach (ScatterRule r in rules)
                    if (r.plant == plant.name) { h = (r.height.x + r.height.y) / 2f; break; }
            if (plant.LoadPoints() && plant.pointArea > 0f) h = Mathf.Min(h, Mathf.Sqrt(190000f / (16000f * plant.pointArea)));
            return h;
        }

        // ------------------------------------------------------------------ the turntable

        public static IEnumerator Turntable(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            Begin(game);
            // the studio: a dark ground, nothing beyond the subject (the nest lies far below, past the far plane)
            Camera cam = game.cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.006f, 0.007f, 0.008f, 1f);
            var spores = GameObject.Find("Spores");
            if (spores != null && spores.GetComponent<ParticleSystemRenderer>() is ParticleSystemRenderer sr) sr.enabled = false;
            for (int i = 0; i < 90; i++) yield return null;   // shaders compile, the plants' buffers are made
            var json = new StringBuilder("{\n\"width\": 1920, \"height\": 1080, \"fps\": 30,\n\"subjects\": [\n");
            bool first = true;
            foreach (Subject s in subjects)
            {
                yield return PlantFilm(game, dir, s, json, first);
                first = false;
            }
            if (Game.Arg("-ttant") != null && Game.Arg("-ttant") != "0") yield return AntFilm(game, dir, json, first);
            json.Append("\n]\n}\n");
            File.WriteAllText(Path.Combine(dir, "film.json"), json.ToString());
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Debug.Log($"[AntGarden] turntable film done: {subjects.Count} plants");
            Application.Quit();
        }

        static IEnumerator PlantFilm(Game game, string dir, Subject s, StringBuilder json, bool first)
        {
            ParticleLook.SolidSlot slot = SlotOf(game.Look, s.t);
            if (slot == null) { Debug.LogWarning($"[AntGarden] turntable: {s.kind} has no splats"); yield break; }
            // where its splats are, as the game bends it (a sample of its stored points)
            Vector3[] pts = s.plant.points;
            var b = new Bounds(slot.m.MultiplyPoint3x4(Shaped(slot, pts[0])), Vector3.zero);
            for (int k = 0; k < pts.Length; k += 17) b.Encapsulate(slot.m.MultiplyPoint3x4(Shaped(slot, pts[k])));
            Vector3 c = b.center;
            float r = 0f;
            for (int k = 0; k < pts.Length; k += 17)
            {
                Vector3 p = slot.m.MultiplyPoint3x4(Shaped(slot, pts[k]));
                r = Mathf.Max(r, new Vector2(p.x - c.x, p.z - c.z).magnitude);
            }
            float fov = Num("-ttfov", 40f), pitch = Num("-ttpitch", 12f), yaw0 = Num("-ttyaw", 215f);
            float d = Frame(fov, b.size.y, r);
            Camera cam = game.cam;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = d + r + 4f;
            // the ant on its way below, StudioSide across from the plant
            DemoMode.PlaceFacing(game, s.floor, s.floor + new Vector2(0f, 1f));
            Place(cam, c, d, pitch, yaw0);
            for (int i = 0; i < 20; i++) yield return null;
            yield return SceneShots.ShellSettles();
            // and the fine splats' zone round the ant grown back: after a jump SolidThings shrinks it by how far its shell
            // lagged behind (fadeLag), then lets it grow back 4 units a second; until then the plant had its coarse splats
            int waited = 0;
            while (waited < 2400 && SolidThings.Instance != null && Field(SolidThings.Instance, "fadeLag") is float lag && lag > 0.02f) { waited++; yield return null; }
            for (int i = 0; i < 10; i++) yield return null;
            Debug.Log($"[AntGarden] turntable {s.kind}: the fine splats' zone back after {waited} frames");
            int n = Mathf.RoundToInt(Num("-ttseconds", 14f) * DemoMode.Fps);
            var cams = new StringBuilder();
            yield return Orbit(game, Path.Combine(dir, s.kind), c, d, pitch, yaw0, Num("-ttturn", 0.55f), n, cams);
            Matrix4x4 m = slot.m;
            json.Append(first ? "" : ",\n").Append("{ \"name\": \"").Append(s.kind).Append("\", \"kind\": \"plant\", \"dir\": \"").Append(s.kind)
                .Append("\", \"frames\": ").Append(n).Append(", \"height\": ").Append(F(s.height))
                .Append(", \"m\": ").Append(Rows(m))
                .Append(", \"bend\": ").Append(F(slot.bend)).Append(", \"bendDir\": [").Append(F(slot.bendDir.x)).Append(", ").Append(F(slot.bendDir.y)).Append("]")
                .Append(", \"twist\": ").Append(F(slot.twist)).Append(", \"sy\": ").Append(F(slot.sy)).Append(", \"sxz\": ").Append(F(slot.sxz))
                .Append(", \"sunk\": ").Append(F(slot.sunk)).Append(", \"top\": ").Append(F(slot.top)).Append(", \"rigid\": ").Append(slot.rigid ? "true" : "false")
                .Append(", \"centre\": [").Append(F(c.x)).Append(", ").Append(F(c.y)).Append(", ").Append(F(c.z)).Append("], \"distance\": ").Append(F(d))
                .Append(", \"pitch\": ").Append(F(pitch)).Append(", \"fov\": ").Append(F(fov)).Append(", \"near\": ").Append(F(cam.nearClipPlane)).Append(", \"far\": ").Append(F(cam.farClipPlane))
                .Append(",\n  \"cams\": [").Append(cams).Append("] }");
            Debug.Log($"[AntGarden] turntable {s.kind}: {n} frames, {d:F2} away, bend {slot.bend:F2} twist {slot.twist:F2} sy {slot.sy:F2} sxz {slot.sxz:F2}");
        }

        /// <summary>The player's ant held in its standing pose in the studio (the world's clock all but stopped, its place
        /// set again after the game has moved it each frame): its mesh in its own materials, then its points.</summary>
        static IEnumerator AntFilm(Game game, string dir, StringBuilder json, bool first)
        {
            AntController a = game.player;
            if (a == null || a.Model == null || a.Ref == null) yield break;
            var (paths, poses) = AntBones(a.Ref.id);
            if (paths == null) { Debug.LogWarning("[AntGarden] turntable: no points for the ant"); yield break; }
            Vector3 spot = studio + new Vector3(-StudioGap, 0, 0);
            float keepScale = Time.timeScale;
            Time.timeScale = 1e-4f;   // its pose held (its clips and its lean barely move), the rest of the world too
            var bones = new Transform[paths.Length];
            for (int i = 0; i < paths.Length; i++) bones[i] = string.IsNullOrEmpty(paths[i]) ? a.Model : a.Model.Find(paths[i]) ?? a.Model;
            a.Place(spot.x, spot.z, 0f);   // (its own idea of where it is: the floor straight below, facing +z)
            game.StartCoroutine(Hold(a, spot));
            for (int i = 0; i < 10; i++) yield return null;
            // where it is: its bones' places (the points follow them); set here too, the game may have moved it this frame
            a.transform.SetPositionAndRotation(spot, Quaternion.identity);
            var b = new Bounds(bones[0].position, Vector3.zero);
            foreach (Transform t in bones) b.Encapsulate(t.position);
            Vector3 c = b.center;
            float r = Mathf.Max(0.35f, Mathf.Max(b.extents.x, b.extents.z));
            float fov = Num("-ttfov", 40f), pitch = Num("-ttantpitch", 18f), yaw0 = Num("-ttantyaw", 215f);
            float d = Frame(fov, Mathf.Max(b.size.y, 0.45f), r * 1.15f);
            Camera cam = game.cam;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = d + r + 4f;
            int n = Mathf.RoundToInt(Num("-ttseconds", 14f) * DemoMode.Fps);
            float turn = Num("-ttturn", 0.55f);
            // its mesh in its own materials (the game draws it dark under its points), no points
            var pa = game.GetComponent<ParticleAnts>();
            Renderer[] mine = a.Model.GetComponentsInChildren<Renderer>(true), own = a.Ref.prefab.GetComponentsInChildren<Renderer>(true);
            var kept = new Material[mine.Length][];
            var shown = new bool[mine.Length];
            for (int i = 0; i < mine.Length; i++)
            {
                kept[i] = mine[i].sharedMaterials;
                shown[i] = mine[i].enabled;
                if (i < own.Length) mine[i].sharedMaterials = own[i].sharedMaterials;
                mine[i].enabled = true;
            }
            if (pa != null) pa.enabled = false;
            Place(cam, c, d, pitch, yaw0);
            for (int i = 0; i < 5; i++) yield return null;
            var meshCams = new StringBuilder();
            yield return Orbit(game, Path.Combine(dir, "ant_mesh"), c, d, pitch, yaw0, turn, n, meshCams);
            for (int i = 0; i < mine.Length; i++) { mine[i].sharedMaterials = kept[i]; mine[i].enabled = shown[i]; }
            if (pa != null) pa.enabled = true;
            // its skin as ParticleAnts makes it (each bone times its bind pose): its points outside the game
            var skin = new StringBuilder();
            a.transform.SetPositionAndRotation(spot, Quaternion.identity);
            for (int i = 0; i < bones.Length; i++) skin.Append(i == 0 ? "" : ",\n    ").Append(Rows(bones[i].localToWorldMatrix * poses[i]));
            Place(cam, c, d, pitch, yaw0);
            for (int i = 0; i < 5; i++) yield return null;
            var cams = new StringBuilder();
            yield return Orbit(game, Path.Combine(dir, "ant"), c, d, pitch, yaw0, turn, n, cams);
            holding = false;
            Time.timeScale = keepScale;
            json.Append(first ? "" : ",\n").Append("{ \"name\": \"").Append(a.Ref.id).Append("\", \"kind\": \"ant\", \"dir\": \"ant\", \"meshDir\": \"ant_mesh\", \"frames\": ").Append(n)
                .Append(", \"centre\": [").Append(F(c.x)).Append(", ").Append(F(c.y)).Append(", ").Append(F(c.z)).Append("], \"distance\": ").Append(F(d))
                .Append(", \"pitch\": ").Append(F(pitch)).Append(", \"fov\": ").Append(F(fov)).Append(", \"near\": ").Append(F(cam.nearClipPlane)).Append(", \"far\": ").Append(F(cam.farClipPlane))
                .Append(",\n  \"skin\": [").Append(skin).Append("],\n  \"cams\": [").Append(cams).Append("],\n  \"meshCams\": [").Append(meshCams).Append("] }");
            Debug.Log($"[AntGarden] turntable ant {a.Ref.id}: {n} frames twice, {d:F2} away, {bones.Length} bones");
        }

        static bool holding;

        /// <summary>Each frame, after the game has moved the ant (Game.Update ticks it onto the floor below), back in the
        /// studio, upright and facing +z.</summary>
        static IEnumerator Hold(AntController a, Vector3 spot)
        {
            holding = true;
            while (holding && a != null)
            {
                a.transform.SetPositionAndRotation(spot, Quaternion.identity);
                yield return null;
            }
        }

        /// <summary>The ant's bones as its points file names them, and their bind poses (as ParticleAnts reads them).</summary>
        static (string[] paths, Matrix4x4[] poses) AntBones(string id)
        {
            var asset = Resources.Load<TextAsset>("AntGarden/Points/ant_" + id);
            if (asset == null) return (null, null);
            using (var r = new BinaryReader(new MemoryStream(asset.bytes)))
            {
                int magic = r.ReadInt32();
                if (magic != 0x32414741 && magic != 0x33414741) return (null, null);
                int ns = r.ReadInt32();
                var paths = new string[ns];
                var poses = new Matrix4x4[ns];
                for (int i = 0; i < ns; i++)
                {
                    paths[i] = r.ReadString();
                    var m = new Matrix4x4();
                    for (int j = 0; j < 16; j++) m[j] = r.ReadSingle();
                    poses[i] = m;
                }
                return (paths, poses);
            }
        }

        /// <summary>How far away to stand for something this tall and this wide (round its middle) to fill the frame at
        /// this lens, all the way round.</summary>
        static float Frame(float fov, float tall, float wide)
        {
            float half = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(tall * 0.5f * 1.25f / half, wide * 1.35f / (half * W / H)) + wide * 0.6f;
        }

        /// <summary>A stored point of the plant where the game's splat of it is, in the plant's own space: ParticleLook.Shape
        /// (stretched, twisted up its height, bent) and its sunk lump.</summary>
        static Vector3 Shaped(ParticleLook.SolidSlot s, Vector3 p)
        {
            float y01 = Mathf.Clamp01(p.y / Mathf.Max(1e-4f, s.top));
            if (!s.rigid && !(s.bend == 0 && s.twist == 0 && s.sy == 1 && s.sxz == 1))
            {
                float y = Mathf.Max(0, p.y);
                p.x *= s.sxz; p.z *= s.sxz; p.y *= s.sy;
                float a = s.twist * y, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                p = new Vector3(p.x * ca - p.z * sa, p.y, p.x * sa + p.z * ca);
                float yy = y * s.sy;
                p.x += s.bendDir.x * s.bend * yy * yy;
                p.z += s.bendDir.y * s.bend * yy * yy;
            }
            if (s.sunk > 0f) p.y -= (y01 < s.sunk + 0.03f ? 1f + s.sunk : s.sunk) * s.top * s.sy;
            return p;
        }

        static ParticleLook.SolidSlot SlotOf(ParticleLook look, Transform t)
        {
            if (look == null || !(Field(look, "solidSlots") is List<ParticleLook.SolidSlot> slots)) return null;
            foreach (ParticleLook.SolidSlot s in slots)
                if ((s.pos - t.position).sqrMagnitude < 1e-4f) return s;
            return null;
        }

        // ------------------------------------------------------------------ the nest from its points

        public static IEnumerator NestPoints(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            Begin(game);
            for (int i = 0; i < 90; i++) yield return null;   // shaders compile, the NPCs spread out
            // its splats as the game holds them
            int solid = DumpSolid(Path.Combine(dir, "solid.bin"));
            int ground = DumpGround(game.Look, Path.Combine(dir, "ground.bin"));
            var map = new MapPainter(game.world, game.gates, 1600);
            File.WriteAllBytes(Path.Combine(dir, "map.png"), map.texture.EncodeToPNG());
            // SceneShots.Aerial's view to begin with: from 55° up, 200° round, 0.95 of the nest's size away, a 50° lens;
            // the night lifted 0.8 of a stop and the haze a quarter, so the nest's shape reads from up there
            Region reg = game.world.regions[0];
            var c = new Vector3((reg.minX + reg.maxX) / 2, 0, (reg.minZ + reg.maxZ) / 2);
            c.y = game.world.Height(c.x, c.z);
            float size = Mathf.Max(reg.maxX - reg.minX, reg.maxZ - reg.minZ), dist = size * Num("-npdist", 0.95f);
            Camera cam = game.cam;
            cam.farClipPlane = dist * 2.5f;
            cam.fieldOfView = 50f;
            RenderSettings.fogDensity *= 0.25f;
            QualitySettings.shadowDistance = dist * 2f;
            Exposure(0.8f);
            float pitch = Num("-nppitch", 55f), yaw0 = Num("-npyaw", 200f);
            Place(cam, c, dist, pitch, yaw0);
            for (int i = 0; i < 30; i++) yield return null;
            yield return SceneShots.ShellSettles();
            int n = Mathf.RoundToInt(Num("-npseconds", 18f) * DemoMode.Fps);
            var cams = new StringBuilder();
            yield return Orbit(game, Path.Combine(dir, "nest"), c, dist, pitch, yaw0, Num("-npturn", 0.2f), n, cams);
            string json = "{\n\"width\": 1920, \"height\": 1080, \"fps\": 30, \"world\": \"" + Game.Selection + "\", \"frames\": " + n
                + ", \"solid\": " + solid + ", \"ground\": " + ground
                + ", \"centre\": [" + F(c.x) + ", " + F(c.y) + ", " + F(c.z) + "], \"distance\": " + F(dist) + ", \"pitch\": " + F(pitch)
                + ", \"fov\": 50, \"near\": " + F(cam.nearClipPlane) + ", \"far\": " + F(cam.farClipPlane)
                + ", \"bounds\": [" + F(reg.minX) + ", " + F(reg.maxX) + ", " + F(reg.minZ) + ", " + F(reg.maxZ) + "]"
                + ",\n\"cams\": [" + cams + "]\n}\n";
            File.WriteAllText(Path.Combine(dir, "film.json"), json);
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Debug.Log($"[AntGarden] nest points film done: {n} frames, {solid} solid splats, {ground} ground points");
            Application.Quit();
        }

        /// <summary>SolidThings' splats (the floor's things, the plants' coarse splats, the giants, the vines), read back
        /// from the GPU as SolidPoint (24 bytes each).</summary>
        static int DumpSolid(string path)
        {
            SolidThings st = SolidThings.Instance;
            if (st == null || st.Count == 0 || !(Field(st, "buffer") is ComputeBuffer buf)) { Debug.LogWarning("[AntGarden] nest points: no solid splats"); return 0; }
            var arr = new SolidPoint[st.Count];
            buf.GetData(arr, 0, 0, st.Count);
            WriteRaw(path, arr, st.Count, 24);
            return st.Count;
        }

        /// <summary>The particle look's own points (the ground's, sparse over the nest, and the dust): position and packed
        /// colour, 16 bytes each.</summary>
        static int DumpGround(ParticleLook look, string path)
        {
            if (look == null || !(Field(look, "uploaded") is System.Array arr) || !(Field(look, "count") is int count) || count == 0) return 0;
            WriteRaw(path, arr, count, 16);
            return count;
        }

        static object Field(object o, string name) =>
            o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(o);

        static void WriteRaw(string path, System.Array arr, int count, int stride)
        {
            var bytes = new byte[count * stride];
            GCHandle h = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try { Marshal.Copy(h.AddrOfPinnedObject(), bytes, 0, bytes.Length); }
            finally { h.Free(); }
            File.WriteAllBytes(path, bytes);
        }

        static void Exposure(float stops)
        {
            var volume = Object.FindObjectOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            if (volume != null && volume.profile != null && volume.profile.TryGetSettings(out UnityEngine.Rendering.PostProcessing.ColorGrading grading))
                grading.postExposure.Override(grading.postExposure.value + stops);
        }

        // ------------------------------------------------------------------ shared

        /// <summary>A fixed 30 fps clock, no UI, the camera placed by hand and drawing into a 1920x1080 texture (the
        /// window may be cut by the screen; the UI is not drawn into it).</summary>
        static void Begin(Game game)
        {
            Application.targetFrameRate = -1;
            Time.captureFramerate = DemoMode.Fps;
            game.demo = true;
            game.demoKeys = null;
            game.demoMove = Vector2.zero;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            game.hud.canvas.enabled = false;
            game.rig.manual = true;
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            game.cam.targetTexture = rt;
            grab = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        /// <summary>Round c at distance d, pitch degrees up, from yaw0 (degrees round from +z) on through turn of a whole
        /// turn over n frames: each frame saved into dir, its camera (position, rotation) into cams.</summary>
        static IEnumerator Orbit(Game game, string dir, Vector3 c, float d, float pitch, float yaw0, float turn, int n, StringBuilder cams)
        {
            Directory.CreateDirectory(dir);
            var end = new WaitForEndOfFrame();
            Camera cam = game.cam;
            for (int f = 0; f < n; f++)
            {
                Place(cam, c, d, pitch, yaw0 + 360f * turn * f / n);
                yield return end;
                Save(Path.Combine(dir, $"f{f:00000}.jpg"));
                Vector3 p = cam.transform.position;
                Quaternion q = cam.transform.rotation;
                cams.Append(f == 0 ? "" : ",").Append(f % 6 == 0 ? "\n    " : "").Append('[').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z))
                    .Append(',').Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w)).Append(']');
                yield return null;
            }
        }

        static void Place(Camera cam, Vector3 c, float d, float pitch, float yaw)
        {
            float p = pitch * Mathf.Deg2Rad, y = yaw * Mathf.Deg2Rad;
            Vector3 eye = c + new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p)) * d;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(c - eye));
        }

        static void Save(string path)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = rt;
            grab.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
            RenderTexture.active = active;
            File.WriteAllBytes(path, grab.EncodeToJPG(94));
        }

        /// <summary>A matrix as four rows of four, for film.json.</summary>
        static string Rows(Matrix4x4 m)
        {
            var sb = new StringBuilder("[");
            for (int r = 0; r < 4; r++)
            {
                sb.Append(r == 0 ? "[" : ", [");
                for (int k = 0; k < 4; k++) sb.Append(k == 0 ? "" : ", ").Append(F(m[r, k]));
                sb.Append(']');
            }
            return sb.Append(']').ToString();
        }
    }
}
