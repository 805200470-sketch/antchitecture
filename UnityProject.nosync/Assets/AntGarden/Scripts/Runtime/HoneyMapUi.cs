using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AntGarden
{
    /// <summary>
    /// The honeycomb maps' look (11-04, the user: "让它更加成熟一些，让这个UI"): one size of type in one weight, the hierarchy in
    /// its brightness only; thin lines; a quiet dark ground; each nest's hue, low in chroma; short words; small hints that
    /// name the buttons of whatever the player holds (Pad.LastUsed). Nothing for its own sake.
    /// </summary>
    public static class MapStyle
    {
        public const int Type = 18;                                                   // every word on the maps
        public static readonly Color Ground = new Color(0.035f, 0.043f, 0.047f, 1f);  // the big map's ground (opaque: the UI
                                                                                       // blends in linear light, so through
                                                                                       // 0.9 the world read as a jumble)
        public static readonly Color Ink = new Color(0.84f, 0.87f, 0.86f);
        public static readonly Color Dim = new Color(0.5f, 0.55f, 0.55f);
        public static readonly Color Here = new Color(1f, 0.925f, 0.785f);           // the ant's nest and the ant (its arrow's warm white)
        public static Color Rim(float hue) => Color.HSVToRGB(Mathf.Repeat(hue, 360f) / 360f, 0.3f, 0.7f);
        public static Color Door(float hue) => Color.HSVToRGB(Mathf.Repeat(hue, 360f) / 360f, 0.35f, 0.92f);
        public static Color Word(float hue) => Color.HSVToRGB(Mathf.Repeat(hue, 360f) / 360f, 0.25f, 0.92f);
        public static Color Jump(float hue) => Color.HSVToRGB(Mathf.Repeat(hue, 360f) / 360f, 0.22f, 0.85f);
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>A line of words in the one size, not wrapped.</summary>
        public static Text Words(Transform parent, string name, string text, Color colour, TextAnchor anchor)
        {
            Text t = UiKit.Label(parent, name, text, Type, colour, anchor);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>A thin straight line from a to b (reference pixels of the parent, from its middle).</summary>
        public static Image Line(Transform parent, Vector2 a, Vector2 b, float width, Color colour)
        {
            Vector2 d = b - a;
            var rt = UiKit.Rect(parent, "Line", 0.5f, 0.5f, (a + b) / 2f, new Vector2(d.magnitude, width));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        static Sprite ring;
        /// <summary>A thin white circle (the pad's cursor on the maps).</summary>
        public static Sprite Ring()
        {
            if (ring != null) return ring;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            tex.ignoreMipmapLimit = true;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - (n / 2f - 5f)) / 2.2f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return ring;
        }

        static Texture2D dot;
        /// <summary>A soft-edged white disc (the dots of the doors and of the jumps between the nests).</summary>
        public static Texture2D Dot()
        {
            if (dot != null) return dot;
            const int n = 64;
            dot = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            dot.ignoreMipmapLimit = true;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude;
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(n / 2f - 1.5f - d) * 255f));
            }
            dot.SetPixels32(px);
            dot.Apply(true, false);
            return dot;
        }

        /// <summary>The ant's arrow and a dark shade under it (it reads on a light floor), size reference pixels.</summary>
        public static (RectTransform arrow, RectTransform shade) Arrow(Transform parent, float size)
        {
            var shade = UiKit.Rect(parent, "PlayerShade", 0.5f, 0.5f, Vector2.zero, new Vector2(size, size) * 1.45f);
            var s = shade.gameObject.AddComponent<Image>();
            s.sprite = UiKit.Arrow();
            s.color = new Color(0f, 0.02f, 0.03f, 0.7f);
            s.raycastTarget = false;
            var arrow = UiKit.Rect(parent, "Player", 0.5f, 0.5f, Vector2.zero, new Vector2(size, size));
            var a = arrow.gameObject.AddComponent<Image>();
            a.sprite = UiKit.Arrow();
            a.color = Here;
            a.raycastTarget = false;
            return (arrow, shade);
        }
    }

    /// <summary>
    /// One nest of the honeycomb, whole (11-04): its picture (HoneyMap) turned as it lies in the ring, a thin rim in its hue
    /// (warm white for the nest the ant is in), its doors as dots in the colour of the nest each leads to (its way out, where
    /// the light leads, with a soft light round it), and the ant. Named: each door's nest written beside it, outside the
    /// rim, on a thin line (the big map's first step); else small (the minimap). Its RectTransform's middle is the nest's;
    /// r reference pixels for its circumradius.
    /// </summary>
    public class NestView
    {
        public readonly RectTransform rt;
        readonly HoneyMap map;
        readonly HoneyMap.Nest nest;
        readonly int k;
        readonly float r;
        readonly RawImage picture;
        readonly RectTransform arrow, arrowShade;
        bool sharp;

        public NestView(Transform parent, HoneyMap map, int k, float r, bool named, bool here)
        {
            this.map = map;
            this.k = k;
            this.r = r;
            nest = map.nests[k];
            rt = UiKit.Rect(parent, "Nest", 0.5f, 0.5f, Vector2.zero, new Vector2(2f * r, 1.7320508f * r));
            HoneyMap.Picture pic = named && nest.Detail != null ? nest.Detail : nest.Picture;
            sharp = pic == nest.Detail;
            var pr = UiKit.Rect(rt, "Picture", 0.5f, 0.5f, Vector2.zero, pic.Size(nest.R) * r);
            pr.localRotation = Quaternion.Euler(0f, 0f, -nest.turn);
            picture = pr.gameObject.AddComponent<RawImage>();
            picture.texture = pic.Texture;
            picture.raycastTarget = false;
            var rim = UiKit.Stretch(rt, "Rim").gameObject.AddComponent<Image>();
            rim.sprite = BigMap.Hexagon(named ? 1.5f : 1.2f, Mathf.RoundToInt(2f * r));   // (drawn at the size it is shown: a thin, smooth line)
            rim.color = new Color(nest.colour.r, nest.colour.g, nest.colour.b, 0.85f);   // (its own hue: alone, it needs no mark of the ant's)
            rim.raycastTarget = false;
            // the doors, and the soft light round the way out
            var dots = UiKit.Rect(rt, "Doors", 0.5f, 0.5f, Vector2.zero, Vector2.zero).gameObject.AddComponent<MapDots>();
            dots.Init(MapStyle.Dot());
            float doorR = named ? 4.6f : 2.8f;
            foreach (HoneyMap.Door d in nest.doors)
            {
                Vector2 p = Local(d.at);
                if (d.wayOut && here)
                {
                    Color halo = MapStyle.Door(nest.hue);
                    halo.a = 0.26f;
                    dots.Add(p, doorR * 3.2f, halo);
                }
                dots.Add(p, doorR + (named ? 1.3f : 1f), new Color(0.92f, 0.92f, 0.9f, 0.9f));
                dots.Add(p, doorR, d.toIndex >= 0 ? MapStyle.Door(map.nests[d.toIndex].hue) : Color.white);
            }
            dots.Done();
            if (named) Names();
            if (here) (arrow, arrowShade) = MapStyle.Arrow(rt, named ? 22f : 16f);
        }

        /// <summary>Each door's nest beside it, outside the rim, on a thin line from the door; the words lean away from the
        /// hexagon (left of a door on its left side, right of one on its right, centred above or below).</summary>
        void Names()
        {
            foreach (HoneyMap.Door d in nest.doors)
            {
                if (d.toIndex < 0) continue;
                Vector2 p = Local(d.at), u = p.normalized, at = p + u * 30f;
                MapStyle.Line(rt, p + u * 8f, at - u * 5f, 1f, new Color(MapStyle.Dim.r, MapStyle.Dim.g, MapStyle.Dim.b, 0.7f));
                float px = u.x > 0.3f ? 0f : u.x < -0.3f ? 1f : 0.5f;
                var box = UiKit.Rect(rt, "Door_" + d.toIndex, px, 0.5f, Vector2.zero, new Vector2(320f, 30f));
                box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);   // (placed from the nest's middle; pivot on the side it leans from)
                box.anchoredPosition = px == 0.5f ? at + new Vector2(0f, Mathf.Sign(u.y) * 8f) : at;
                MapStyle.Words(box, "Text", "→ " + Gates.WorldName(d.to), MapStyle.Word(map.nests[d.toIndex].hue),
                    px == 0f ? TextAnchor.MiddleLeft : px == 1f ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);
            }
        }

        /// <summary>A point of the nest (units of the nest) in the view (reference pixels from its middle).</summary>
        public Vector2 Local(Vector2 world) => HoneyMap.Turn(world, nest.turn) / nest.R * r;

        /// <summary>A point of the view (reference pixels from its middle) in the nest.</summary>
        public Vector2 World(Vector2 local) => HoneyMap.Turn(local / r * nest.R, -nest.turn);

        /// <summary>The point is inside the nest's hexagon.</summary>
        public bool Inside(Vector2 local) => HoneyMap.InCell(local / r, 0f);

        public Vector2 Size => new Vector2(2f * r, 1.7320508f * r);

        public void Player(Vector3 position, float heading)
        {
            if (arrow == null) return;
            Vector2 at = Local(new Vector2(position.x, position.z));
            var rot = Quaternion.Euler(0f, 0f, -(heading * Mathf.Rad2Deg + nest.turn));
            arrow.anchoredPosition = arrowShade.anchoredPosition = at;
            arrow.localRotation = arrowShade.localRotation = rot;
        }

        /// <summary>The sharper picture, once it is painted (HoneyMap.WantDetail).</summary>
        public void Sharpen()
        {
            if (sharp || nest.Detail == null) return;
            sharp = true;
            picture.texture = nest.Detail.Texture;
            picture.rectTransform.sizeDelta = nest.Detail.Size(nest.R) * r;
        }
    }

    /// <summary>
    /// The honeycomb, whole (11-04, the big map's second step): each nest's picture in its cell, turned; the cells the ant
    /// is not in a little shaded; thin rims in each nest's hue (warm white round the ant's); each nest's name on a soft
    /// shade; the jumps from each way out to the next nest's gate back, dotted, one hue fading into the next; the doors;
    /// the ant. A click on a cell (or the pad's A) chooses it; the mouse on one lights it. r reference pixels for a cell's
    /// circumradius; its RectTransform's middle is the ring's.
    /// </summary>
    public class RingView
    {
        public readonly RectTransform rt;
        readonly HoneyMap map;
        readonly int here;
        readonly float r;
        readonly Image[] rims, veils;
        public readonly Button[] buttons;
        readonly RectTransform arrow, arrowShade;

        public RingView(Transform parent, HoneyMap map, int here, float r, Action<int> hover, Action<int> choose)
        {
            this.map = map;
            this.here = here;
            this.r = r;
            int n = map.Count;
            Rect b = map.Bounds();
            rt = UiKit.Rect(parent, "Ring", 0.5f, 0.5f, Vector2.zero, b.size * r);
            rims = new Image[n];
            veils = new Image[n];
            buttons = new Button[n];
            Vector2 cell = new Vector2(2f, 1.7320508f) * r;
            // the jumps first (under the cells: where an arc meets a cell it goes under its rim)
            var jumps = UiKit.Rect(rt, "Jumps", 0.5f, 0.5f, Vector2.zero, Vector2.zero).gameObject.AddComponent<MapDots>();
            jumps.Init(MapStyle.Dot());
            foreach (HoneyMap.Jump j in map.jumps)
            {
                List<Vector2> arc = map.Arc(j, 96);
                float total = 0f;
                for (int i = 1; i < arc.Count; i++) total += (arc[i] - arc[i - 1]).magnitude * r;
                int count = Mathf.Max(2, Mathf.RoundToInt(total / 10f));
                Color a = MapStyle.Jump(map.nests[j.a].hue), c = MapStyle.Jump(map.nests[j.b].hue);
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) / count, at = t * (arc.Count - 1);
                    int i0 = Mathf.Min(arc.Count - 2, Mathf.FloorToInt(at));
                    Vector2 p = Vector2.Lerp(arc[i0], arc[i0 + 1], at - i0) * r;
                    Color col = Color.Lerp(a, c, t);
                    col.a = 0.9f;
                    jumps.Add(p, 2f, col);
                }
            }
            jumps.Done();
            for (int k = 0; k < n; k++)
            {
                HoneyMap.Nest nest = map.nests[k];
                Vector2 at = map.Centre(k) * r;
                var pr = UiKit.Rect(rt, "Picture_" + k, 0.5f, 0.5f, at, nest.Picture.Size(nest.R) * r);
                pr.localRotation = Quaternion.Euler(0f, 0f, -nest.turn);
                var pic = pr.gameObject.AddComponent<RawImage>();
                pic.texture = nest.Picture.Texture;
                pic.raycastTarget = false;
                var cellRt = UiKit.Rect(rt, "Nest_" + k, 0.5f, 0.5f, at, cell);
                // a shade over the nests the ant is not in; a light veil under the mouse or the cursor (Light)
                veils[k] = cellRt.gameObject.AddComponent<Image>();
                veils[k].sprite = BigMap.Hexagon(0f);
                veils[k].alphaHitTestMinimumThreshold = 0.5f;   // (only the hexagon takes a click, not its box's corners)
                rims[k] = UiKit.Stretch(cellRt, "Rim").gameObject.AddComponent<Image>();
                rims[k].sprite = BigMap.Hexagon(k == here ? 2f : 1.3f, Mathf.RoundToInt(cell.x));
                rims[k].raycastTarget = false;
                // its name on a soft shade
                var plate = UiKit.Rect(cellRt, "Shade", 0.5f, 0.5f, Vector2.zero, new Vector2(Mathf.Min(cell.x * 0.9f, 230f), 64f)).gameObject.AddComponent<Image>();
                plate.sprite = UiKit.Soft();
                plate.color = new Color(MapStyle.Ground.r, MapStyle.Ground.g, MapStyle.Ground.b, 0.85f);
                plate.raycastTarget = false;
                var name = UiKit.Rect(cellRt, "Name", 0.5f, 0.5f, Vector2.zero, new Vector2(cell.x - 24f, 50f));
                Text t = UiKit.Label(name, "Text", Gates.WorldName(nest.id), MapStyle.Type, k == here ? MapStyle.Here : MapStyle.Ink, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                buttons[k] = cellRt.gameObject.AddComponent<Button>();
                buttons[k].transition = Selectable.Transition.None;
                buttons[k].navigation = new Navigation { mode = Navigation.Mode.None };
                int ki = k;
                buttons[k].onClick.AddListener(() => choose(ki));
                var trigger = cellRt.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => hover(ki));
                trigger.triggers.Add(enter);
                var leave = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                leave.callback.AddListener(_ => hover(-1));
                trigger.triggers.Add(leave);
            }
            // the doors over everything
            var doors = UiKit.Rect(rt, "Doors", 0.5f, 0.5f, Vector2.zero, Vector2.zero).gameObject.AddComponent<MapDots>();
            doors.Init(MapStyle.Dot());
            for (int k = 0; k < n; k++)
                foreach (HoneyMap.Door d in map.nests[k].doors)
                {
                    Vector2 p = map.ToBoard(k, d.at) * r;
                    doors.Add(p, 3.4f, new Color(0.92f, 0.92f, 0.9f, 0.85f));
                    doors.Add(p, 2.4f, d.toIndex >= 0 ? MapStyle.Door(map.nests[d.toIndex].hue) : Color.white);
                }
            doors.Done();
            if (here >= 0) (arrow, arrowShade) = MapStyle.Arrow(rt, 14f);
            Light(-1);
        }

        /// <summary>The nest under the mouse or the pad's cursor lit (-1: none): its rim at full, a light veil over it.</summary>
        public void Light(int over)
        {
            for (int k = 0; k < rims.Length; k++)
            {
                Color c = map.nests[k].colour;
                rims[k].color = k == here ? new Color(MapStyle.Here.r, MapStyle.Here.g, MapStyle.Here.b, 0.95f) : new Color(c.r, c.g, c.b, k == over ? 1f : 0.7f);
                // (white blends strongly in linear light: 0.03 lifts a dark cell's ground by a third)
                veils[k].color = k == over ? new Color(1f, 1f, 1f, 0.03f) : k == here ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.22f);
            }
        }

        public Vector2 Centre(int k) => map.Centre(k) * r;
        public int CellAt(Vector2 local) => map.CellAt(local / r);
        public Vector2 Size => rt.sizeDelta;

        public void Player(Vector3 position, float heading)
        {
            if (arrow == null) return;
            Vector2 at = map.ToBoard(here, new Vector2(position.x, position.z)) * r;
            var rot = Quaternion.Euler(0f, 0f, -(heading * Mathf.Rad2Deg + map.nests[here].turn));
            arrow.anchoredPosition = arrowShade.anchoredPosition = at;
            arrow.localRotation = arrowShade.localRotation = rot;
        }
    }

    /// <summary>
    /// The big map in a nest of the honeycomb (11-04, the user: "单个地图要显示完全，然后再点开来才是更多的地图"), in two steps:
    /// 1, the nest the ant is in, whole and as large as the screen allows (NestView, named), a click in it (or the pad's A)
    /// teleporting the ant there; 2, one step further, the whole honeycomb (RingView), a click on a nest (or A) going there.
    /// Y on the pad goes from one to the other, Tab on the keyboard, or a click on the hint that says so; B and Esc step
    /// back one at a time (BigMap.Back), M closes. Top left what is shown, in a word or two; at the foot the hints for the
    /// device in hand. The steps cross-fade in a fifth of a second, the nest shrinking as the ring comes in (and back).
    /// Built again when the screen's size changes (every word stays the one size).
    /// </summary>
    public class HoneyMapScreen
    {
        public int Level { get; private set; } = 1;
        readonly RectTransform root;
        readonly Canvas canvas;
        readonly HoneyMap map;
        readonly int here;
        readonly Func<Vector2, string> teleport;   // a point of the nest: null when the ant went there, else why not
        readonly Action<string> travel;
        readonly Action close;
        RectTransform level1, level2, cursor;
        CanvasGroup g1, g2;
        NestView nest;
        RingView ring;
        Text title1, title2, note;
        readonly List<(RectTransform rt, Text text, Action click, string tag)> hints = new List<(RectTransform, Text, Action, string)>();
        RectTransform hintRow1, hintRow2;
        Vector2 builtFor, cursorPos;
        bool cursorShown, hintsPad;
        int hover = -1, padOver = -1;
        float blend, noteTimer;   // blend: 0 the nest, 1 the ring (as shown, easing toward Level)
        static readonly bool padHints = Game.Arg("-mappad") != null;   // (checks: the pad's hints, as a player holding one sees them)

        public HoneyMapScreen(RectTransform parent, Canvas canvas, HoneyMap map, Func<Vector2, string> teleport, Action<string> travel, Action close)
        {
            this.canvas = canvas;
            this.map = map;
            this.teleport = teleport;
            this.travel = travel;
            this.close = close;
            here = map.IndexOf(Game.Selection);
            map.WantDetail(here);
            root = UiKit.Stretch(parent, "Honeycomb");
            Build();
        }

        /// <summary>Both steps, laid out for the screen as it is now (a nest as large as fits with room for its doors'
        /// names round it; the ring as large as fits, a cell at most 170 across).</summary>
        void Build()
        {
            for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
            hints.Clear();
            Rect cr = ((RectTransform)canvas.transform).rect;
            builtFor = cr.size;
            float band = 112f, room = Loc.En ? 300f : 230f;   // (top and foot; beside the nest for its doors' names)

            level1 = UiKit.Stretch(root, "Nest");
            g1 = level1.gameObject.AddComponent<CanvasGroup>();
            float R = Mathf.Min(520f, Mathf.Min((cr.height - 2f * band) / 1.7320508f, (cr.width - 2f * room) / 2f));
            nest = new NestView(level1, map, here, R, true, true);
            var hit = nest.rt.gameObject.AddComponent<Image>();   // (clicks anywhere on the nest's box; Inside sorts them)
            hit.color = new Color(0f, 0f, 0f, 0f);
            var trigger = nest.rt.gameObject.AddComponent<EventTrigger>();
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(e =>
            {
                var pe = (PointerEventData)e;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(nest.rt, pe.position, pe.pressEventCamera, out Vector2 local)) Teleport(local);
            });
            trigger.triggers.Add(click);
            HoneyMap.Nest n = map.nests[here];
            title1 = Title(level1, $"{Gates.WorldName(n.id)}<color={MapStyle.Hex(MapStyle.Dim)}>    {Segment(n)}</color>");
            hintRow1 = HintRow(level1);

            level2 = UiKit.Stretch(root, "Ring");
            g2 = level2.gameObject.AddComponent<CanvasGroup>();
            Rect b = map.Bounds();
            float r = Mathf.Min(170f, Mathf.Min((cr.height - 2f * band) / b.height, (cr.width - 160f) / b.width));
            ring = new RingView(level2, map, here, r, Hover, Choose);
            title2 = Title(level2, "");
            hintRow2 = HintRow(level2);

            note = MapStyle.Words(UiKit.Rect(root, "Note", 1f, 1f, new Vector2(-64f, -48f), new Vector2(700f, 30f)), "Text", "", MapStyle.Dim, TextAnchor.MiddleRight);
            cursor = UiKit.Rect(root, "PadCursor", 0.5f, 0.5f, Vector2.zero, new Vector2(30f, 30f));
            var ci = cursor.gameObject.AddComponent<Image>();
            ci.sprite = MapStyle.Ring();
            ci.color = new Color(MapStyle.Here.r, MapStyle.Here.g, MapStyle.Here.b, 0.95f);
            ci.raycastTarget = false;
            cursor.gameObject.SetActive(false);
            cursorShown = false;
            Hints(true);
            Hover(-1);
            Apply();
        }

        static Text Title(RectTransform parent, string text)
        {
            var box = UiKit.Rect(parent, "Title", 0f, 1f, new Vector2(64f, -48f), new Vector2(900f, 30f));
            return MapStyle.Words(box, "Text", text, MapStyle.Ink, TextAnchor.MiddleLeft);
        }

        static string Segment(HoneyMap.Nest n)
        {
            NestTheme t = NestTheme.For(n.biome);
            return Loc.T($"{t.segmentZh} · {Mathf.RoundToInt(t.hue)}°", $"{t.segmentEn} · {Mathf.RoundToInt(t.hue)}°");
        }

        static RectTransform HintRow(RectTransform parent)
        {
            var row = UiKit.Rect(parent, "Hints", 0.5f, 0f, new Vector2(0f, 34f), new Vector2(1200f, 30f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 44f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        /// <summary>The hints at the foot of each step, for the pad or the keyboard (written again when the other is taken
        /// up): the key brighter, then what it does; the one that changes step and the one that closes take a click too.</summary>
        void Hints(bool force)
        {
            bool pad = Pad.LastUsed || padHints;
            if (!force && pad == hintsPad) return;
            hintsPad = pad;
            foreach (var h in hints) Object.Destroy(h.rt.gameObject);
            hints.Clear();
            if (pad)
            {
                Hint(hintRow1, "A", Loc.T("传送", "teleport"), null);
                Hint(hintRow1, "Y", Loc.T("全部蚁穴", "all nests"), () => Show(2, false));
                Hint(hintRow1, "B", Loc.T("关闭", "close"), close);
                Hint(hintRow2, "A", Loc.T("前往", "travel"), null);
                Hint(hintRow2, "Y", Loc.T("这个蚁穴", "this nest"), () => Show(1, false));
                Hint(hintRow2, "B", Loc.T("返回", "back"), () => Show(1, false));
            }
            else
            {
                Hint(hintRow1, Loc.T("点击", "Click"), Loc.T("传送", "teleport"), null);
                Hint(hintRow1, "Tab", Loc.T("全部蚁穴", "all nests"), () => Show(2, false));
                Hint(hintRow1, "M", Loc.T("关闭", "close"), close);
                Hint(hintRow2, Loc.T("点击", "Click"), Loc.T("前往", "travel"), null);
                Hint(hintRow2, "Tab", Loc.T("这个蚁穴", "this nest"), () => Show(1, false));
                Hint(hintRow2, "Esc", Loc.T("返回", "back"), () => Show(1, false));
            }
        }

        void Hint(RectTransform row, string key, string what, Action click)
        {
            var rt = new GameObject("Hint_" + key, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(row, false);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = UiKit.Font;
            text.fontSize = MapStyle.Type;
            text.supportRichText = true;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = MapStyle.Dim;
            text.text = $"<color={MapStyle.Hex(MapStyle.Ink)}>{key}</color>  {what}";
            text.raycastTarget = click != null;
            if (click != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.ColorTint;
                var colours = button.colors;
                colours.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);   // (the mouse over it lifts it to its full brightness)
                colours.highlightedColor = Color.white;
                colours.pressedColor = new Color(1f, 0.95f, 0.85f, 1f);
                colours.selectedColor = colours.normalColor;
                button.colors = colours;
                button.targetGraphic = text;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => click());
            }
            hints.Add((rt, text, click, key));
        }

        /// <summary>A step shown (instant: at once, as when the map is built again for the other language).</summary>
        public void Show(int level, bool instant)
        {
            Level = level == 2 ? 2 : 1;
            if (instant) blend = Level == 2 ? 1f : 0f;
            cursorShown = false;
            cursor.gameObject.SetActive(false);
            padOver = -1;
            Hover(-1);
            Apply();
        }

        /// <summary>The cross-fade as it stands: the nest shrinking a little as it goes, the ring settling as it comes.</summary>
        void Apply()
        {
            float e = blend * blend * (3f - 2f * blend);
            g1.alpha = 1f - e;
            g2.alpha = e;
            g1.blocksRaycasts = g1.interactable = Level == 1;
            g2.blocksRaycasts = g2.interactable = Level == 2;
            level1.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, e);
            level2.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, e);
        }

        void Hover(int k)
        {
            hover = k;
            ring?.Light(k);
            if (title2 == null) return;
            HoneyMap.Nest n = map.nests[here];
            string sub = k < 0 || k == here ? Loc.T($"你在 {Gates.WorldName(n.id)}", $"You are in {Gates.WorldName(n.id)}")
                : Loc.T($"前往 {Gates.WorldName(map.nests[k].id)}", $"To {Gates.WorldName(map.nests[k].id)}");
            title2.text = Loc.T("全部蚁穴", "All nests") + $"<color={MapStyle.Hex(MapStyle.Dim)}>    {sub}</color>";
        }

        void Choose(int k)
        {
            if (k < 0) return;
            if (k == here) { Say(Loc.T("蚂蚁就在这个蚁穴里", "The ant is in this nest")); return; }
            travel(map.nests[k].id);
        }

        void Teleport(Vector2 local)
        {
            if (!nest.Inside(local)) { Say(Loc.T("那里在蚁穴外面", "That is outside the nest")); return; }
            string why = teleport(nest.World(local));
            if (why != null) Say(why);
        }

        void Say(string text)
        {
            note.text = text;
            noteTimer = 3f;
        }

        public void Tick(float dt, AntController player)
        {
            if (((RectTransform)canvas.transform).rect.size != builtFor) { int l = Level; Build(); Show(l, true); }
            Hints(false);
            nest.Sharpen();
            blend = Mathf.MoveTowards(blend, Level == 2 ? 1f : 0f, dt / 0.22f);
            Apply();
            noteTimer -= dt;
            note.color = new Color(MapStyle.Dim.r, MapStyle.Dim.g, MapStyle.Dim.b, Mathf.Clamp01(noteTimer));
            if (Input.GetKeyDown(KeyCode.Tab) && !Game.Instance.demo) Show(Level == 1 ? 2 : 1, false);   // (Game leaves Tab to the map while it is open)
            nest.Player(player.Position, player.heading);
            ring.Player(player.Position, player.heading);
            if (!cursorShown) cursorPos = Level == 1 ? nest.Local(new Vector2(player.Position.x, player.Position.z)) : ring.Centre(here);   // the pad's cursor starts at the ant
        }

        /// <summary>The pad: the left stick moves a cursor (from the ant, or from its nest in the ring), A teleports there or
        /// goes to the nest under it.</summary>
        public void PadTick(Vector2 stick, bool confirm, float dt)
        {
            if (!cursorShown && stick.sqrMagnitude > 0f)
            {
                cursorShown = true;
                cursor.gameObject.SetActive(true);
            }
            if (!cursorShown) return;
            Vector2 half = (Level == 1 ? nest.Size : ring.Size) / 2f;
            cursorPos = Vector2.Max(-half, Vector2.Min(half, cursorPos + stick * 560f * dt));   // (reference pixels a second at full tilt)
            cursor.anchoredPosition = cursorPos * (Level == 1 ? level1.localScale.x : level2.localScale.x);
            if (Level == 2)
            {
                int over = ring.CellAt(cursorPos);
                if (over != padOver) Hover(over);
                padOver = over;
                if (confirm && over >= 0) Choose(over);
                return;
            }
            if (confirm) Teleport(cursorPos);
        }

        // ---- checks (HoneycombCheck, MapCheck, PadCheck): the ring's nests, the pad's cursor (reference pixels from the
        // screen's middle), a click on a nest as the mouse makes it, and what each step covers on the screen ----
        public int RingCount => map.Count;
        public string RingHere => here >= 0 ? map.nests[here].id : null;
        public string RingHover => Level == 2 && hover >= 0 ? map.nests[hover].id : null;
        public Vector2 PadCursor => cursorPos;
        public Vector2 RingCentre(string id) { int k = map.IndexOf(id); return k < 0 ? Vector2.zero : ring.Centre(k); }
        public void ClickNest(string id) { int k = map.IndexOf(id); if (k >= 0) ring.buttons[k].onClick.Invoke(); }

        public List<(string name, Rect rect)> ScreenRects()
        {
            var list = new List<(string, Rect)>();
            void Add(string name, RectTransform rt, Text words = null)
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                Rect r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                if (words != null)   // (a line of words: as wide as it is written, from its anchored side)
                {
                    float w = words.preferredWidth * canvas.scaleFactor;
                    r = words.alignment == TextAnchor.MiddleRight ? Rect.MinMaxRect(r.xMax - w, r.yMin, r.xMax, r.yMax)
                        : words.alignment == TextAnchor.MiddleLeft ? Rect.MinMaxRect(r.xMin, r.yMin, r.xMin + w, r.yMax)
                        : Rect.MinMaxRect(r.center.x - w / 2f, r.yMin, r.center.x + w / 2f, r.yMax);
                }
                list.Add((name, r));
            }
            RectTransform shown = Level == 1 ? level1 : level2;
            Add("title", (RectTransform)(Level == 1 ? title1 : title2).transform, Level == 1 ? title1 : title2);
            if (Level == 1)
            {
                Add("nest", nest.rt);
                foreach (Transform t in nest.rt)
                    if (t.name.StartsWith("Door_")) Add(t.name, (RectTransform)t.GetChild(0), t.GetComponentInChildren<Text>());
            }
            else Add("ring", ring.rt);
            foreach (var h in hints)
                if (h.rt.parent == (Level == 1 ? hintRow1 : hintRow2)) Add("hint " + h.tag, h.rt, h.text);
            return list;
        }
    }

    /// <summary>
    /// The minimap in a nest of the honeycomb (11-04, the user: the single map shown whole): the nest the ant is in, its
    /// whole hexagon top right (NestView, small: its canyons, the way its light leads, its doors in the colours of where
    /// they lead, the ant), on a soft shade so it reads over a bright scene; no panel round it. The big map goes further.
    /// </summary>
    public class HoneyMiniMap
    {
        const float R = 118f;   // reference pixels for the nest's circumradius: 236 across, 204 high
        public readonly RectTransform root;
        readonly NestView view;

        /// <summary>Hud's minimap for the nest the ant is in, when the honeycomb's map is drawn and the nest is one of its ring.</summary>
        public static HoneyMiniMap Build(Transform canvas)
        {
            HoneyMap map = HoneyMap.Shared;
            if (map == null || !map.Ready) return null;
            int here = map.IndexOf(Game.Selection);
            return here < 0 ? null : new HoneyMiniMap(canvas, map, here);
        }

        HoneyMiniMap(Transform canvas, HoneyMap map, int here)
        {
            root = UiKit.Rect(canvas, "Minimap", 1, 1, new Vector2(-28, -28), new Vector2(2f * R, 1.7320508f * R));
            var shade = UiKit.Rect(root, "Shade", 0.5f, 0.5f, Vector2.zero, new Vector2(2.7f * R, 2.5f * R)).gameObject.AddComponent<Image>();
            shade.sprite = UiKit.Soft();
            shade.color = new Color(0f, 0.01f, 0.015f, 0.55f);
            shade.raycastTarget = false;
            view = new NestView(root, map, here, R, false, true);
        }

        public void Tick(AntController player) => view.Player(player.Position, player.heading);
    }
}
