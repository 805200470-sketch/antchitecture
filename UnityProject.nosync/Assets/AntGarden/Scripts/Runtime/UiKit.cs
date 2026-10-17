using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// Code-built UGUI helpers. Chinese text uses an OS font (PingFang on macOS, Microsoft YaHei on
    /// Windows), so the build needs no bundled CJK font.
    /// </summary>
    public static class UiKit
    {
        static Font font;
        public static readonly Color Ink = new Color(0.86f, 0.95f, 0.93f, 1f);
        public static readonly Color Dim = new Color(0.6f, 0.74f, 0.74f, 1f);
        public static readonly Color Warm = new Color(1f, 0.89f, 0.66f, 1f);
        public static readonly Color PanelBg = new Color(0.02f, 0.07f, 0.08f, 0.62f);

        public static Font Font
        {
            get
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "PingFang SC", "Microsoft YaHei UI", "Microsoft YaHei", "DengXian", "SimHei",
                        "Noto Sans CJK SC", "Source Han Sans SC", "Hiragino Sans GB", "Arial Unicode MS", "Arial",
                    }, 16);
                return font;
            }
        }

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return canvas;
        }

        /// <summary>A RectTransform child anchored at (ax, ay) with pivot (ax, ay), offset and size in reference pixels.</summary>
        public static RectTransform Rect(Transform parent, string name, float ax, float ay, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align)
        {
            var rt = Stretch(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string text, Vector2 size, UnityAction onClick, int fontSize = 18)
        {
            var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            go.GetComponent<LayoutElement>().preferredWidth = size.x;
            go.GetComponent<LayoutElement>().preferredHeight = size.y;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.07f, 0.2f, 0.21f, 0.85f);
            var b = go.GetComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(0.55f, 0.95f, 0.88f, 1f);
            colors.pressedColor = new Color(1f, 0.9f, 0.6f, 1f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            Label(rt, "Text", text, fontSize, Ink, TextAnchor.MiddleCenter);
            return b;
        }

        /// <summary>A white isosceles triangle pointing up, for map markers.</summary>
        public static Sprite Arrow()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float half = (n - 1 - y) * 0.42f;
                bool inside = Mathf.Abs(x - (n - 1) / 2f) <= half && y > 2;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A soft white oval fading out to its edge, for shade behind a title.</summary>
        public static Sprite Soft()
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - (w - 1) / 2f) / ((w - 1) / 2f), dy = (y - (h - 1) / 2f) / ((h - 1) / 2f);
                float a = Mathf.Clamp01(1 - (dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A soft white disc, for dots on the map.</summary>
        public static Sprite Dot()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1 - d) * 4)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
