using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// Full-screen module map (M): click anywhere to teleport the ant there (snapped to the nearest
    /// walkable floor), or jump to a chamber / story slot / exit. The bottom row switches nest or stitched world.
    /// In a nest of the honeycomb (11-03; redone 11-04, the user: "单个地图要显示完全，然后再点开来才是更多的地图 … 让它更加
    /// 成熟一些") the map is HoneyMapScreen's, in two steps: the nest the ant is in, whole, then one step further the whole
    /// honeycomb (its nests' own canyons, the journey round the ring), a nest chosen there going there. Y on the pad goes from
    /// one step to the other (M on the keyboard opens and closes; Tab or a click on the hint changes step there), B and Esc
    /// step back one at a time (Back). It fades in and out in a fifth of a second. Elsewhere (and in a particle nest while the
    /// honeycomb's map is still being drawn) the painter's map as before, fitted to the screen (11-04: at 1728×1117 full screen
    /// its left border was missing): scaled down to fit between the title and the buttons, a row too wide likewise.
    /// </summary>
    public class BigMap
    {
        readonly Canvas canvas;
        readonly RectTransform root;
        readonly CanvasGroup group;
        readonly MapPainter map;
        readonly World world;
        readonly GameConfig cfg;
        readonly List<Gate> gates;
        readonly Action<Vector2> teleport;
        readonly Action<Gate> toGate;
        readonly Action<string> switchWorld;
        readonly IList<string> modules;
        readonly bool honeycomb;
        readonly List<string> labs = new List<string>();
        float cooldown;
        bool open;
        float shown;   // the fade in and out, 0..1
        public bool IsOpen => open;

        // a nest of the honeycomb: its two-step map (HoneyMapScreen), once the honeycomb's map is drawn
        HoneyMapScreen screen;
        bool honeyLate;   // the honeycomb's map was still being drawn when this was built: built again once it is
        static int carryLevel = 1, carryFrame = -1;   // (the map built again for the other language keeps its step)

        // the painter's map
        RectTransform middle, mapRect, marker, cursor;
        Text head, hint;
        Vector2 cursorUv, natural, fittedTo;
        bool cursorShown;
        float bottomBand;
        readonly List<RectTransform> rows = new List<RectTransform>();

        internal static readonly Color HereColour = new Color(1f, 0.92f, 0.7f);   // the player's arrow's warm white

        public BigMap(Canvas canvas, MapPainter map, World world, GameConfig cfg, List<Gate> gates, Action<Vector2> teleport, Action<Gate> toGate, Action<string> switchWorld, IList<string> modules)
        {
            this.canvas = canvas;
            this.map = map;
            this.world = world;
            this.cfg = cfg;
            this.gates = gates;
            this.teleport = teleport;
            this.toGate = toGate;
            this.switchWorld = switchWorld;
            this.modules = modules;
            foreach (string id in modules) if (id.StartsWith("lab:")) labs.Add(id);
            honeycomb = Game.Lab && labs.Count > 1;
            root = UiKit.Stretch(canvas.transform, "BigMap");
            // (opaque: the UI blends in linear light, so through the old 0.9 the world, the goal, the minimap, the nest's
            // banner and the prompt read as a jumble behind the map)
            UiKit.Panel(root, honeycomb ? MapStyle.Ground : new Color(0.01f, 0.04f, 0.05f, 1f));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Build();
            root.gameObject.SetActive(false);
        }

        void Build()
        {
            honeyLate = false;
            if (honeycomb && HoneyMap.ReadyHere)
            {
                screen = new HoneyMapScreen(root, canvas, HoneyMap.Shared, TryTeleport, switchWorld, () => Toggle(false));
                return;
            }
            honeyLate = honeycomb && HoneyMap.WantedHere;
            BuildPainter();
        }

        // ---- opening, closing, the steps ----------------------------------------------------------------------------

        /// <summary>
        /// M on the keyboard, Y on the pad (Game; no argument): closed, open on the nest; open, Y goes to the other step and
        /// M closes (Pad.LastUsed tells the two apart). true: open on the nest (or, built again for the other language this
        /// very frame, on the step it was on, at once). false: close (a teleport, a check).
        /// </summary>
        public void Toggle(bool? want = null)
        {
            if (want == null)
            {
                if (!open) Open(1, false);
                else if (screen != null && Pad.LastUsed) screen.Show(screen.Level == 1 ? 2 : 1, false);
                else Close();
                return;
            }
            if (!want.Value) { Close(); return; }
            bool carried = carryFrame == Time.frameCount;
            Open(carried ? carryLevel : 1, carried);
        }

        /// <summary>B on the pad, Esc (Game): one step back: from the ring to the nest, from the nest closed.</summary>
        public void Back()
        {
            if (!open) return;
            if (screen != null && screen.Level == 2) { screen.Show(1, false); return; }
            Close();
        }

        /// <summary>The honeycomb's whole ring shown (opening the map if it is closed; checks).</summary>
        public void ShowNests()
        {
            if (screen == null) return;
            if (!open) Open(2, false);
            else screen.Show(2, false);
        }

        /// <summary>The step shown: 0 closed, 1 the nest (or the painter's map), 2 the whole honeycomb.</summary>
        public int Level => !open ? 0 : screen != null ? screen.Level : 1;

        void Open(int level, bool instant)
        {
            open = true;
            root.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            if (instant) shown = 1f;
            Fade(0f);
            cursorShown = false;
            if (cursor != null) cursor.gameObject.SetActive(false);
            if (hint != null) hint.text = "";
            if (screen != null) screen.Show(level, true);
            else if (((RectTransform)canvas.transform).rect.size != fittedTo) Fit();
        }

        void Close()
        {
            open = false;
            group.blocksRaycasts = false;
        }

        /// <summary>The fade (a fifth of a second in, a little less out), the map settling from a touch smaller as it comes.</summary>
        void Fade(float dt)
        {
            shown = Mathf.MoveTowards(shown, open ? 1f : 0f, dt / (open ? 0.18f : 0.14f));
            float e = shown * shown * (3f - 2f * shown);
            group.alpha = e;
            root.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, e);
            if (!open && shown <= 0f && root.gameObject.activeSelf) root.gameObject.SetActive(false);
        }

        /// <summary>Remove the map UI (it is rebuilt when the language changes: the step it was on is kept).</summary>
        public void Destroy()
        {
            carryLevel = screen != null ? screen.Level : 1;
            carryFrame = Time.frameCount;
            UnityEngine.Object.Destroy(root.gameObject);
        }

        /// <summary>The pad on the map: the left stick moves a cursor (from the ant), A teleports there or chooses the nest.</summary>
        public void PadTick(Vector2 stick, bool confirm, float dt)
        {
            if (!open) return;
            if (screen != null) { screen.PadTick(stick, confirm, dt); return; }
            if (!cursorShown && stick.sqrMagnitude > 0)
            {
                cursorShown = true;
                cursor.gameObject.SetActive(true);
            }
            if (!cursorShown) return;
            float aspect = mapRect.rect.width / Mathf.Max(1, mapRect.rect.height);
            cursorUv += new Vector2(stick.x, stick.y * aspect) * 0.32f * dt;
            cursorUv = new Vector2(Mathf.Clamp01(cursorUv.x), Mathf.Clamp01(cursorUv.y));
            cursor.anchoredPosition = new Vector2(cursorUv.x * mapRect.rect.width, cursorUv.y * mapRect.rect.height);
            if (confirm) TeleportTo(cursorUv);
        }

        public void Tick(float dt, AntController player)
        {
            cooldown = Mathf.Max(0, cooldown - dt);
            // the honeycomb's map drawn since this was built: built again with it (the minimap likewise: Hud)
            if (honeyLate && HoneyMap.ReadyHere)
            {
                for (int i = root.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
                rows.Clear();
                cursor = marker = null;
                Build();
                if (open && screen != null) screen.Show(1, true);
            }
            if (root.gameObject.activeSelf) Fade(dt);
            if (!open) return;
            if (screen != null) { screen.Tick(dt, player); return; }
            if (((RectTransform)canvas.transform).rect.size != fittedTo) Fit();   // (the window resized, or full screen)
            Vector2 uv = map.ToUv(player.Position.x, player.Position.z);
            marker.anchoredPosition = new Vector2(uv.x * mapRect.rect.width, uv.y * mapRect.rect.height);
            if (!cursorShown) cursorUv = uv;   // the pad's cursor starts at the ant
            marker.localRotation = Quaternion.Euler(0, 0, -player.heading * Mathf.Rad2Deg);
        }

        /// <summary>A point of this world: the ant goes there (snapped to the nearest floor); null when it went, else why not.</summary>
        string TryTeleport(Vector2 wp)
        {
            if (cooldown > 0) return Loc.T($"传送冷却中：{cooldown:F0} 秒", $"Teleport cooling down: {cooldown:F0} s");
            if (world.HexDist(wp.x, wp.y) > 0) return Loc.T("那里在蚁穴外面", "That is outside the nest");
            if (!world.SnapToWalkable(wp.x, wp.y, 0.9f, out Vector2 snapped)) return Loc.T("附近没有可以站的地方", "Nowhere to stand near there");
            cooldown = cfg.teleportCooldown;
            teleport(snapped);
            return null;
        }

        // ---- the painter's map (outside the honeycomb) ----------------------------------------------------------------

        void BuildPainter()
        {
            head = UiKit.Label(root, "Head", Loc.T("<b>模块地图</b>  <size=16><color=#9bbcbc>点地图上任意位置，蚂蚁会传送过去 · M 或 Esc 关闭 · 手柄：左摇杆移动光标，A 传送，B 关闭</color></size>",
                "<b>Nest map</b>  <size=16><color=#9bbcbc>Click anywhere on the map to teleport the ant there · M or Esc to close · Pad: left stick moves the cursor, A teleports, B closes</color></size>"), 24, UiKit.Ink, TextAnchor.UpperLeft);
            head.rectTransform.offsetMin = new Vector2(32, 0);
            head.rectTransform.offsetMax = new Vector2(-32, -24);
            hint = UiKit.Label(root, "Hint", "", 18, UiKit.Warm, TextAnchor.UpperRight);
            hint.rectTransform.offsetMax = new Vector2(-32, -28);

            // the map between the title and the buttons: scaled to fit the screen (Fit)
            middle = UiKit.Rect(root, "Middle", 0.5f, 0.5f, Vector2.zero, Vector2.zero);
            float w = Mathf.Min(1500, 760 * map.Aspect), h = w / map.Aspect;
            natural = new Vector2(w, h);
            mapRect = UiKit.Rect(middle, "Map", 0.5f, 0.5f, Vector2.zero, natural);
            var img = mapRect.gameObject.AddComponent<RawImage>();
            img.texture = map.texture;
            marker = UiKit.Rect(mapRect, "Player", 0, 0, Vector2.zero, new Vector2(26, 26));
            var arrow = marker.gameObject.AddComponent<Image>();
            arrow.sprite = UiKit.Arrow();
            arrow.color = HereColour;
            arrow.raycastTarget = false;
            var trigger = mapRect.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(e =>
            {
                var pe = (PointerEventData)e;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, pe.position, pe.pressEventCamera, out Vector2 local)) return;
                TeleportTo(new Vector2(local.x / mapRect.rect.width + 0.5f, local.y / mapRect.rect.height + 0.5f));
            });
            trigger.triggers.Add(entry);

            // the pad's cursor (shown once the left stick moves on the map)
            cursor = UiKit.Rect(mapRect, "PadCursor", 0, 0, Vector2.zero, new Vector2(42, 42));
            var dot = cursor.gameObject.AddComponent<Image>();
            dot.sprite = UiKit.Dot();
            dot.color = new Color(0.55f, 1f, 0.95f, 0.85f);
            dot.raycastTarget = false;
            cursor.gameObject.SetActive(false);

            // waypoints: chambers, story slots, exits
            var row = Row(root, "Waypoints", 92);
            int chamber = 0;
            foreach (WNode n in world.nodes)
            {
                if (n.degree < 3 || !n.inside) continue;
                var p = new Vector2(n.x, n.z);
                UiKit.Button(row, Loc.T("洞室 ", "Chamber ") + ++chamber, new Vector2(Loc.En ? 118 : 96, 36), () => Jump(world, p, teleport), 16);
            }
            for (int i = 0; i < world.slots.Count; i++)
            {
                var s = world.slots[i];
                var p = new Vector2(s.position.x, s.position.z);
                UiKit.Button(row, Loc.T("物件位 ", "Slot ") + (i + 1), new Vector2(Loc.En ? 84 : 104, 36), () => Jump(world, p, teleport), 16);
            }
            foreach (Gate g in gates)
            {
                if (!g.portal) continue;
                Gate target = g;
                string name = world.regions[g.region].biome.Name;
                UiKit.Button(row, Loc.T("光门·", "Gate · ") + name, new Vector2(Loc.En ? 230 : 130, 36), () => toGate(target), 16);
            }
            for (int i = 0; i < world.exits.Count; i++)
            {
                var ex = world.exits[i];
                var p = new Vector2(ex.x, ex.z);
                UiKit.Button(row, Loc.T("出口 ", "Exit ") + (i + 1), new Vector2(90, 36), () => Jump(world, p, teleport), 16);
            }
            var mods = Row(root, "Modules", 40);
            // the particle nests, one per colour of the room's lights, in a row of their own (short labels: there are many)
            var nests = Row(root, "Nests", 144);
            foreach (string id in modules)
            {
                if (!id.StartsWith("lab:")) continue;
                string label = LabShort(id), target = id;
                UiKit.Button(nests, label, new Vector2(Loc.En ? 128 : 156, 36), () => switchWorld(target), 15);
            }
            foreach (string id in modules)
            {
                if (id.StartsWith("lab:") || honeycomb) continue;   // (in a particle nest only its nests: no archipelago, ruins or path worlds)
                string label = id.StartsWith("honeycomb:") ? Loc.T("蚁穴群岛", "Archipelago")
                    : id.Contains("#") ? RuinScene.Title(id.Substring(id.LastIndexOf('#') + 1))
                    : id.Contains("|") ? Loc.T("蚁穴群岛", "Archipelago")
                    : id.StartsWith("stitch:") ? Loc.T("拼接 ", "Stitch ") + id.Substring(id.LastIndexOf(':') + 1).Replace("path-", "").Replace(",", "+")
                    : id.Replace("path-", "");
                string target = id;
                UiKit.Button(mods, label, new Vector2(label.Length > 3 ? 200 : 64, 36), () => switchWorld(target), 16);
            }
            // the map keeps clear of the rows that have buttons
            bottomBand = 24f;
            foreach (RectTransform r in rows)
                if (r.childCount > 0) bottomBand = Mathf.Max(bottomBand, r.anchoredPosition.y + r.sizeDelta.y + 8f);   // (a row's pivot is its foot)
            Fit();
        }

        /// <summary>A row of buttons along the bottom (Fit scales it down when it is wider than the screen).</summary>
        RectTransform Row(RectTransform parent, string name, float y)
        {
            var row = UiKit.Rect(parent, name, 0.5f, 0, new Vector2(0, y), new Vector2(1800, 44));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            rows.Add(row);
            return row;
        }

        static void Jump(World world, Vector2 p, Action<Vector2> teleport)
        {
            if (world.SnapToWalkable(p.x, p.y, 0.9f, out Vector2 snapped)) teleport(snapped);
        }

        /// <summary>
        /// Fits the painter's map to the screen as it is now (the canvas's size in reference pixels: 1920 wide at 16:9, 1791
        /// at 1728×1117, narrower toward 4:3): the map between the title (its height as it wraps) and the buttons scaled
        /// down to fit, never up, 32 pixels clear of each side; a row of buttons wider than the screen likewise.
        /// </summary>
        void Fit()
        {
            Rect cr = ((RectTransform)canvas.transform).rect;
            fittedTo = cr.size;
            float top = 24f + Mathf.Max(32f, head.preferredHeight) + 16f;
            float s = Mathf.Min(1f, Mathf.Min((cr.width - 64f) / natural.x, (cr.height - top - bottomBand) / natural.y));
            middle.localScale = new Vector3(s, s, 1f);
            middle.anchoredPosition = new Vector2(0f, (bottomBand - top) / 2f);
            foreach (RectTransform row in rows)
            {
                float wide = 0f;
                int count = 0;
                foreach (RectTransform b in row)
                {
                    var le = b.GetComponent<LayoutElement>();
                    wide += le != null ? le.preferredWidth : b.sizeDelta.x;
                    count++;
                }
                wide += Mathf.Max(0, count - 1) * 8f;
                float rs = count == 0 ? 1f : Mathf.Min(1f, (cr.width - 48f) / wide);
                // (its own width: a HorizontalLayoutGroup only centres what fits in it, and spilled a long row off the right;
                // the archipelago's dozens of chambers, slots and exits would be specks: left out, a click still goes anywhere)
                row.sizeDelta = new Vector2(Mathf.Max(1f, wide), row.sizeDelta.y);
                row.gameObject.SetActive(count > 0 && (rs >= 0.7f || row.name != "Waypoints"));
                row.localScale = new Vector3(rs, rs, 1f);
            }
        }

        /// <summary>Teleport to a point of the painter's map (0..1 across and up), snapped to the nearest floor.</summary>
        void TeleportTo(Vector2 uv)
        {
            string why = TryTeleport(map.ToWorld(uv.x, uv.y));
            if (why != null) hint.text = why;
        }

        /// <summary>A particle nest's button: its colour segment and its name.</summary>
        static string LabShort(string id)
        {
            int at = id.LastIndexOf('@');
            NestTheme t = NestTheme.For(at >= 0 ? id.Substring(at + 1) : "blue");
            return Loc.T($"{t.segmentZh} · {t.nameZh}", t.segmentEn);
        }

        // ---- the honeycomb's layout (Game.RingGates, HoneyMap) ---------------------------------------------------------

        /// <summary>
        /// Where the k-th of n nests sits in the honeycomb's ring: the first at the top, the others on clockwise (the user,
        /// 11-03: 0° top, 30° upper right, 60° lower right, 90° bottom, 120° lower left, 180° upper left), two neighbours 1
        /// apart; six make the hexagons round an empty middle, flat side up. Game.RingGates opens a nest's exits on its
        /// two neighbours by these directions, so the gates lead the way the map shows.
        /// </summary>
        public static Vector2 RingAt(int k, int n)
        {
            if (n < 2) return Vector2.zero;
            k = (k % n + n) % n;
            float a = 2f * Mathf.PI * k / n;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (0.5f / Mathf.Sin(Mathf.PI / n));
        }

        static readonly Dictionary<(float, int), Sprite> hexagons = new Dictionary<(float, int), Sprite>();

        /// <summary>A white hexagon, flat side up, filling its box (2 wide to √3 high), soft-edged; rim &gt; 0: only a band
        /// that wide (texture pixels) inside its edge. width: the texture's (11-04: a rim drawn at the size it is shown,
        /// so a thin line stays smooth; a 256 sprite magnified four times drew the big map's rim in steps).</summary>
        internal static Sprite Hexagon(float rim, int width = 256)
        {
            rim = Mathf.Round(rim * 20f) / 20f;
            width = Mathf.Clamp(width, 32, 2048);
            if (hexagons.TryGetValue((rim, width), out Sprite made) && made != null) return made;
            int w = width, h = Mathf.RoundToInt(width * 0.8660254f);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            float apothem = 0.8660254f * (w / 2f - 1f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - w / 2f), dy = Mathf.Abs(y + 0.5f - h / 2f);
                float d = Mathf.Max(dy, 0.8660254f * dx + 0.5f * dy) - apothem;   // (negative inside)
                float a = Mathf.Clamp01(0.5f - d);
                if (rim > 0f) a *= Mathf.Clamp01(d + rim + 0.5f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            made = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            hexagons[(rim, width)] = made;
            return made;
        }

        // ---- checks (HoneycombCheck, MapCheck, PadCheck) ---------------------------------------------------------------

        /// <summary>The ring's nests, the one marked as the ant's, the pad's cursor and the nest under it, a cell's middle
        /// (reference pixels from the screen's middle), and a click on a cell as the mouse makes it (the honeycomb's map;
        /// none on the painter's).</summary>
        public int RingCount => screen?.RingCount ?? 0;
        public string RingHere => screen?.RingHere;
        public string RingHover => screen?.RingHover;
        public Vector2 PadCursor => screen?.PadCursor ?? Vector2.zero;
        public Vector2 RingCentre(string id) => screen?.RingCentre(id) ?? Vector2.zero;
        public void ClickNest(string id) => screen?.ClickNest(id);
        public bool HoneyShown => screen != null;

        /// <summary>What the map covers on the screen (screen pixels): the honeycomb's step shown, or the painter's title as
        /// written, its map and each row of buttons.</summary>
        public List<(string name, Rect rect)> ScreenRects()
        {
            if (screen != null) return screen.ScreenRects();
            var list = new List<(string, Rect)>();
            var hc = new Vector3[4];
            head.rectTransform.GetWorldCorners(hc);
            float k = canvas.scaleFactor;
            list.Add(("title", new Rect(hc[1].x, hc[1].y - head.preferredHeight * k, Mathf.Min(head.preferredWidth, head.rectTransform.rect.width) * k, head.preferredHeight * k)));
            var c = new Vector3[4];
            mapRect.GetWorldCorners(c);
            list.Add(("map", Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y)));
            foreach (RectTransform row in rows)
            {
                if (row.childCount == 0 || !row.gameObject.activeSelf) continue;
                Rect r = Rect.zero;
                bool any = false;
                foreach (RectTransform b in row)
                {
                    b.GetWorldCorners(c);
                    var br = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                    r = any ? Rect.MinMaxRect(Mathf.Min(r.xMin, br.xMin), Mathf.Min(r.yMin, br.yMin), Mathf.Max(r.xMax, br.xMax), Mathf.Max(r.yMax, br.yMax)) : br;
                    any = true;
                }
                list.Add((row.name, r));
            }
            return list;
        }
    }
}
