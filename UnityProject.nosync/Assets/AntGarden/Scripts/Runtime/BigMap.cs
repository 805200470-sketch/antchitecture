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
    /// </summary>
    public class BigMap
    {
        readonly RectTransform root, mapRect;
        readonly MapPainter map;
        readonly Text hint;
        readonly RectTransform marker, cursor;
        readonly List<RectTransform> npcDots = new List<RectTransform>();
        readonly World world;
        readonly GameConfig cfg;
        readonly Action<Vector2> teleport;
        float cooldown;
        Vector2 cursorUv;
        bool cursorShown;
        public bool IsOpen => root.gameObject.activeSelf;

        public BigMap(Canvas canvas, MapPainter map, World world, GameConfig cfg, List<Gate> gates, Action<Vector2> teleport, Action<Gate> toGate, Action<string> switchWorld, IList<string> modules)
        {
            this.map = map;
            this.world = world;
            this.cfg = cfg;
            this.teleport = teleport;
            root = UiKit.Stretch(canvas.transform, "BigMap");
            UiKit.Panel(root, new Color(0.01f, 0.04f, 0.05f, 0.9f));
            var head = UiKit.Label(root, "Head", Loc.T("<b>模块地图</b>  <size=16><color=#9bbcbc>点地图上任意位置，蚂蚁会传送过去 · M 或 Esc 关闭 · 手柄：左摇杆移动光标，A 传送，B 关闭</color></size>",
                "<b>Nest map</b>  <size=16><color=#9bbcbc>Click anywhere on the map to teleport the ant there · M or Esc to close · Pad: left stick moves the cursor, A teleports, B closes</color></size>"), 24, UiKit.Ink, TextAnchor.UpperLeft);
            head.rectTransform.offsetMin = new Vector2(32, 0);
            head.rectTransform.offsetMax = new Vector2(-32, -24);
            hint = UiKit.Label(root, "Hint", "", 18, UiKit.Warm, TextAnchor.UpperRight);
            hint.rectTransform.offsetMax = new Vector2(-32, -28);

            float maxW = 1500, maxH = 760;
            float w = Mathf.Min(maxW, maxH * map.Aspect), h = w / map.Aspect;
            mapRect = UiKit.Rect(root, "Map", 0.5f, 0.5f, new Vector2(0, 40), new Vector2(w, h));
            var img = mapRect.gameObject.AddComponent<RawImage>();
            img.texture = map.texture;
            var trigger = mapRect.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(e =>
            {
                var pe = (PointerEventData)e;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, pe.position, pe.pressEventCamera, out Vector2 local)) return;
                TeleportTo(new Vector2((local.x + mapRect.rect.width / 2) / mapRect.rect.width, (local.y + mapRect.rect.height / 2) / mapRect.rect.height));
            });
            trigger.triggers.Add(entry);

            // the pad's cursor (shown once the left stick moves on the map)
            cursor = UiKit.Rect(mapRect, "PadCursor", 0, 0, Vector2.zero, new Vector2(42, 42));
            var dot = cursor.gameObject.AddComponent<Image>();
            dot.sprite = UiKit.Dot();
            dot.color = new Color(0.55f, 1f, 0.95f, 0.85f);
            dot.raycastTarget = false;
            cursor.gameObject.SetActive(false);

            marker = UiKit.Rect(mapRect, "Player", 0, 0, Vector2.zero, new Vector2(26, 26));
            var arrow = marker.gameObject.AddComponent<Image>();
            arrow.sprite = UiKit.Arrow();
            arrow.color = new Color(1f, 0.92f, 0.7f);
            arrow.raycastTarget = false;

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
            foreach (string id in modules)
            {
                string label = id.StartsWith("honeycomb:") ? Loc.T("蚁穴群岛", "Archipelago")
                    : id.StartsWith("lab:") ? Loc.T("实验 · 粒子蚁穴", "Lab · Particle Nest")
                    : id.Contains("#") ? RuinScene.Title(id.Substring(id.LastIndexOf('#') + 1))
                    : id.Contains("|") ? Loc.T("蚁穴群岛", "Archipelago")
                    : id.StartsWith("stitch:") ? Loc.T("拼接 ", "Stitch ") + id.Substring(id.LastIndexOf(':') + 1).Replace("path-", "").Replace(",", "+")
                    : id.Replace("path-", "");
                string target = id;
                UiKit.Button(mods, label, new Vector2(label.Length > 3 ? 200 : 64, 36), () => switchWorld(target), 16);
            }
            root.gameObject.SetActive(false);
        }

        static RectTransform Row(RectTransform parent, string name, float y)
        {
            var row = UiKit.Rect(parent, name, 0.5f, 0, new Vector2(0, y), new Vector2(1800, 44));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        static void Jump(World world, Vector2 p, Action<Vector2> teleport)
        {
            if (world.SnapToWalkable(p.x, p.y, 0.9f, out Vector2 snapped)) teleport(snapped);
        }

        /// <summary>Remove the map UI (it is rebuilt when the language changes).</summary>
        public void Destroy() => UnityEngine.Object.Destroy(root.gameObject);

        public void Toggle(bool? open = null)
        {
            bool o = open ?? !IsOpen;
            root.gameObject.SetActive(o);
            if (o) hint.text = "";
            cursorShown = false;
            cursor.gameObject.SetActive(false);
        }

        /// <summary>Teleport to a point of the map (0..1 across and up), snapped to the nearest floor.</summary>
        void TeleportTo(Vector2 uv)
        {
            if (cooldown > 0) { hint.text = Loc.T($"传送冷却中：{cooldown:F0} 秒", $"Teleport cooling down: {cooldown:F0} s"); return; }
            Vector2 wp = map.ToWorld(uv.x, uv.y);
            if (world.HexDist(wp.x, wp.y) > 0) { hint.text = Loc.T("那里在模块外面", "That is outside the nests"); return; }
            if (!world.SnapToWalkable(wp.x, wp.y, 0.9f, out Vector2 snapped)) { hint.text = Loc.T("附近没有可以站的地方", "Nowhere to stand near there"); return; }
            cooldown = cfg.teleportCooldown;
            teleport(snapped);
        }

        /// <summary>The pad on the map: the left stick moves a cursor (from the ant), A teleports there.</summary>
        public void PadTick(Vector2 stick, bool confirm, float dt)
        {
            if (!IsOpen) return;
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
            if (!IsOpen) return;
            Vector2 uv = map.ToUv(player.Position.x, player.Position.z);
            marker.anchoredPosition = new Vector2(uv.x * mapRect.rect.width, uv.y * mapRect.rect.height);
            if (!cursorShown) cursorUv = uv;   // the pad's cursor starts at the ant
            marker.localRotation = Quaternion.Euler(0, 0, -player.heading * Mathf.Rad2Deg);
        }
    }
}
