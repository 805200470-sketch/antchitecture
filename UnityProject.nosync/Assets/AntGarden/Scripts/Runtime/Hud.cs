using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// On-screen UI: loading screen, help (H), stats, minimap, interaction prompt, region banner
    /// (entering another nest of a stitched world) and the story-slot panel.
    /// </summary>
    public class Hud
    {
        public readonly Canvas canvas;
        readonly RectTransform loading, loadingBar, help, slotPanel, stats;
        readonly Text loadingLabel, titleLabel, statsLabel, promptLabel, regionTitle, regionSub, slotText, captionLabel;
        readonly CanvasGroup promptGroup, regionGroup, captionGroup, lineGroup;
        readonly Text lineSpeaker, lineText;
        float lineTimer;
        readonly Image fade;
        float captionTarget, fadeTarget;
        bool helpWanted = true, mapOpen;
        RectTransform minimap, playerMarker;
        RectTransform objective;
        Text objectiveLabel;
        MapPainter map;
        float fps = 60, statTimer, regionTimer, regionDuration = 2.8f;

        public Hud()
        {
            canvas = UiKit.MakeCanvas("HUD", 10);
            Transform root = canvas.transform;

            help = UiKit.Rect(root, "Help", 0, 1, new Vector2(24, -24), new Vector2(560, 330));
            UiKit.Panel(help, UiKit.PanelBg);
            titleLabel = UiKit.Label(help, "Title", "", 22, UiKit.Ink, TextAnchor.UpperLeft);
            Pad(titleLabel.rectTransform, 18, 14);
            titleLabel.text = Loc.T("<b>Ant Garden · 蚁穴峡谷</b>", "<b>Ant Garden · Nest Canyon</b>");

            // the chapter's current goal, one quiet line top left (chapters only)
            objective = UiKit.Rect(root, "Objective", 0, 1, new Vector2(28, -28), new Vector2(760, 40));
            objectiveLabel = UiKit.Label(objective, "ObjectiveText", "", 21, UiKit.Warm, TextAnchor.MiddleLeft);
            objective.gameObject.SetActive(false);

            stats = UiKit.Rect(root, "Stats", 0, 0, new Vector2(24, 24), new Vector2(520, 64));
            UiKit.Panel(stats, UiKit.PanelBg);
            statsLabel = UiKit.Label(stats, "StatsText", "", 17, UiKit.Dim, TextAnchor.MiddleLeft);
            Pad(statsLabel.rectTransform, 14, 6);

            var prompt = UiKit.Rect(root, "Prompt", 0.5f, 0, new Vector2(0, 110), new Vector2(980, 52));
            UiKit.Panel(prompt, UiKit.PanelBg);
            promptLabel = UiKit.Label(prompt, "PromptText", "", 20, UiKit.Warm, TextAnchor.MiddleCenter);
            promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0;

            var region = UiKit.Rect(root, "Region", 0.5f, 1, new Vector2(0, -150), new Vector2(900, 110));
            // a soft dark shade behind the words, so they read over bright plants
            var shade = UiKit.Panel(UiKit.Rect(region, "Shade", 0.5f, 0.5f, Vector2.zero, new Vector2(1250, 230)), new Color(0f, 0.02f, 0.04f, 0.6f));
            shade.sprite = UiKit.Soft();
            shade.raycastTarget = false;
            regionTitle = UiKit.Label(region, "RegionTitle", "", 40, new Color(0.91f, 0.98f, 0.96f), TextAnchor.UpperCenter);
            regionSub = UiKit.Label(region, "RegionSub", "", 18, UiKit.Dim, TextAnchor.LowerCenter);
            regionGroup = region.gameObject.AddComponent<CanvasGroup>();
            regionGroup.alpha = 0;

            // demo captions (bottom centre) and a black veil for cuts between demo shots
            var caption = UiKit.Rect(root, "Caption", 0.5f, 0, new Vector2(0, 40), new Vector2(1500, 96));
            UiKit.Panel(caption, new Color(0.01f, 0.04f, 0.05f, 0.72f));
            captionLabel = UiKit.Label(caption, "CaptionText", "", 30, UiKit.Ink, TextAnchor.MiddleCenter);
            Pad(captionLabel.rectTransform, 24, 8);
            captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
            captionGroup.alpha = 0;
            // a line spoken by an ant, a relic or a memory plant (above the prompt bar)
            var line = UiKit.Rect(root, "Line", 0.5f, 0, new Vector2(0, 180), new Vector2(980, 118));
            UiKit.Panel(line, new Color(0.02f, 0.06f, 0.07f, 0.82f));
            lineSpeaker = UiKit.Label(line, "Speaker", "", 17, UiKit.Warm, TextAnchor.UpperLeft);
            Pad(lineSpeaker.rectTransform, 26, 12);
            lineText = UiKit.Label(line, "LineText", "", 26, UiKit.Ink, TextAnchor.MiddleLeft);
            Pad(lineText.rectTransform, 26, 34);
            lineText.rectTransform.offsetMax = new Vector2(-26, -36);
            lineGroup = line.gameObject.AddComponent<CanvasGroup>();
            lineGroup.alpha = 0;
            lineGroup.blocksRaycasts = false;
            var veil = UiKit.Stretch(root, "Fade");
            fade = UiKit.Panel(veil, new Color(0, 0, 0, 0));
            fade.raycastTarget = false;

            slotPanel = UiKit.Rect(root, "SlotPanel", 0.5f, 0.5f, Vector2.zero, new Vector2(620, 260));
            UiKit.Panel(slotPanel, new Color(0.02f, 0.07f, 0.08f, 0.88f));
            slotText = UiKit.Label(slotPanel, "SlotText", "", 20, UiKit.Ink, TextAnchor.UpperLeft);
            Pad(slotText.rectTransform, 24, 20);
            slotPanel.gameObject.SetActive(false);

            loading = UiKit.Stretch(root, "Loading");
            UiKit.Panel(loading, new Color(0.016f, 0.063f, 0.07f, 1f));
            var box = UiKit.Rect(loading, "Box", 0.5f, 0.5f, Vector2.zero, new Vector2(620, 140));
            var title = UiKit.Label(box, "LoadingTitle", Loc.T("正在生长峡谷", "Growing the canyon"), 32, UiKit.Ink, TextAnchor.UpperCenter);
            var track = UiKit.Rect(box, "Track", 0.5f, 0.5f, new Vector2(0, -6), new Vector2(560, 6));
            UiKit.Panel(track, new Color(0.05f, 0.16f, 0.18f));
            loadingBar = UiKit.Rect(track, "Bar", 0, 0.5f, Vector2.zero, new Vector2(0, 6));
            UiKit.Panel(loadingBar, new Color(0.31f, 0.71f, 0.67f));
            loadingLabel = UiKit.Label(box, "LoadingLabel", "", 18, UiKit.Dim, TextAnchor.LowerCenter);
        }

        static void Pad(RectTransform rt, float x, float y)
        {
            rt.offsetMin = new Vector2(x, y);
            rt.offsetMax = new Vector2(-x, -y);
        }

        public void SetProgress(string label, float frac)
        {
            loadingLabel.text = label;
            loadingBar.sizeDelta = new Vector2(560 * Mathf.Clamp01(frac), 6);
        }

        public void HideLoading() => loading.gameObject.SetActive(false);

        public void ShowError(string message)
        {
            loading.gameObject.SetActive(true);
            loadingLabel.text = message;
            loadingLabel.color = new Color(1f, 0.6f, 0.5f);
        }

        /// <summary>Help text: world label plus the controls.</summary>
        public void SetHelp(string worldLabel)
        {
            titleLabel.text = Loc.T("<b>Ant Garden · 蚁穴峡谷</b>\n", "<b>Ant Garden · Nest Canyon</b>\n") +
                $"<size=16><color=#9bbcbc>{worldLabel}</color></size>\n\n<size=18>" +
                Loc.T("WASD 爬行 · Shift 快爬 · 左键拖动转镜头 · 滚轮远近\n" +
                      "空格 触角动作（触角能碰亮发光植物）· E 打招呼 / 查看物件位\n" +
                      "M 地图与传送（点地图任意处即传送）\n" +
                      "Tab 切换画质 · F 全屏/窗口 · L English · H 隐藏说明\n" +
                      "<color=#9bbcbc>手柄：左摇杆 爬行 · 右摇杆 转镜头 · RT 快爬 · A 交互 · B 触角 / 返回 · Y 地图 · LB/RB 远近 · ☰ 说明</color>",
                      "WASD crawl · Shift run · drag to look · scroll to zoom\n" +
                      "Space antenna touch (it lights up glowing plants) · E greet / open a story slot\n" +
                      "M map & teleport (click anywhere on it)\n" +
                      "Tab quality · F full screen · L 中文 · H hide help\n" +
                      "<color=#9bbcbc>Pad: left stick crawl · right stick look · RT run · A interact · B antennae / back · Y map · LB/RB zoom · ☰ help</color>") + "</size>";
        }

        public void ToggleHelp() => SetHelpVisible(!helpWanted);
        public void SetHelpVisible(bool on)
        {
            helpWanted = on;
            help.gameObject.SetActive(helpWanted && !mapOpen);
        }

        /// <summary>The big map covers the screen: the help panel steps aside so the two do not overlap.</summary>
        public void SetMapOpen(bool open)
        {
            if (open == mapOpen) return;
            mapOpen = open;
            help.gameObject.SetActive(helpWanted && !mapOpen);
        }
        public void SetStatsVisible(bool on) => stats.gameObject.SetActive(on);

        /// <summary>Demo caption at the bottom of the screen (rich text); empty fades it out.</summary>
        public void SetCaption(string text)
        {
            // above the big map (built later in the same canvas); the black veil stays on top of both
            captionGroup.transform.SetAsLastSibling();
            fade.rectTransform.SetAsLastSibling();
            if (!string.IsNullOrEmpty(text)) captionLabel.text = text;
            captionTarget = string.IsNullOrEmpty(text) ? 0 : 1;
        }

        /// <summary>Fade the whole screen to black (1) or back (0); used for cuts in the demo.</summary>
        public void SetFade(float target, bool instant = false)
        {
            fade.rectTransform.SetAsLastSibling(); // above the minimap, which is built after the HUD
            fadeTarget = target;
            if (instant) fade.color = new Color(0, 0, 0, target);
        }

        public void BuildMinimap(MapPainter painter)
        {
            map = painter;
            float size = 260;
            float wpx = painter.Aspect >= 1 ? size : size * painter.Aspect;
            float hpx = painter.Aspect >= 1 ? size / painter.Aspect : size;
            minimap = UiKit.Rect(canvas.transform, "Minimap", 1, 1, new Vector2(-24, -24), new Vector2(wpx + 20, hpx + 20));
            UiKit.Panel(minimap, UiKit.PanelBg);
            var img = UiKit.Rect(minimap, "Map", 0.5f, 0.5f, Vector2.zero, new Vector2(wpx, hpx)).gameObject.AddComponent<RawImage>();
            img.texture = painter.texture;
            img.raycastTarget = false;
            playerMarker = UiKit.Rect(img.rectTransform, "Player", 0, 0, Vector2.zero, new Vector2(16, 16));
            var arrow = playerMarker.gameObject.AddComponent<Image>();
            arrow.sprite = UiKit.Arrow();
            arrow.color = new Color(1f, 0.92f, 0.7f);
        }

        public void SetPrompt(string text)
        {
            promptLabel.text = text ?? "";
            promptGroup.alpha = string.IsNullOrEmpty(text) ? 0 : 1;
        }

        public void ShowRegion(string title, string sub)
        {
            regionTitle.text = title;
            regionSub.text = sub;
            regionTimer = regionDuration = 2.8f;
        }

        /// <summary>A chapter's title card (the region banner, held longer).</summary>
        public void ShowChapter(string title, string sub)
        {
            regionTitle.text = title;
            regionSub.text = sub;
            regionTimer = regionDuration = 5f;
        }

        /// <summary>The chapter's current goal top left; empty hides it.</summary>
        public void SetObjective(string text)
        {
            objectiveLabel.text = string.IsNullOrEmpty(text) ? "" : "✦  " + text;
            objective.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void ShowSlot(string html)
        {
            slotText.text = html;
            slotPanel.gameObject.SetActive(true);
        }

        public void HideSlot() => slotPanel.gameObject.SetActive(false);
        public bool SlotOpen => slotPanel.gameObject.activeSelf;

        /// <summary>
        /// Show one spoken line: who speaks (an ant, a relic, a memory plant) and what it says. It stays a few
        /// seconds, longer for longer lines, then fades.
        /// </summary>
        public void ShowLine(string speaker, string text)
        {
            lineSpeaker.text = speaker;
            lineText.text = text;
            lineTimer = Mathf.Clamp(2.5f + text.Length * 0.12f, 4f, 9f);
        }

        public bool LineShowing => lineTimer > 0;

        public void Tick(float dt, AntController player, int npcs, string extra)
        {
            lineTimer -= dt;
            lineGroup.alpha = Mathf.MoveTowards(lineGroup.alpha, lineTimer > 0 ? 1 : 0, dt * (lineTimer > 0 ? 5f : 1.5f));
            captionGroup.alpha = Mathf.MoveTowards(captionGroup.alpha, captionTarget, dt * 3f);
            fade.color = new Color(0, 0, 0, Mathf.MoveTowards(fade.color.a, fadeTarget, dt * 3f));
            fps += (1f / Mathf.Max(dt, 1e-4f) - fps) * 0.05f;
            statTimer -= dt;
            if (statTimer <= 0)
            {
                statTimer = 0.4f;
                statsLabel.text = $"<b>{fps:F0}</b> FPS · {npcs} " + Loc.T("只蚂蚁", "ants") + $" · {extra}";
            }
            regionTimer -= dt;
            regionGroup.alpha = Mathf.Clamp01(Mathf.Min((regionDuration - regionTimer) / 0.6f, regionTimer / 0.6f));
            if (map != null && playerMarker != null)
            {
                Vector2 uv = map.ToUv(player.Position.x, player.Position.z);
                var parent = (RectTransform)playerMarker.parent;
                playerMarker.anchoredPosition = new Vector2(uv.x * parent.rect.width, uv.y * parent.rect.height);
                // heading 0 = +Z = up on the map; UI rotation is counter-clockwise
                playerMarker.localRotation = Quaternion.Euler(0, 0, -player.heading * Mathf.Rad2Deg);
            }
        }
    }
}
