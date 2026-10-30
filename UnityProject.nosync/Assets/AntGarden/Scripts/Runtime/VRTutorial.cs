using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// In a headset: a card in front of the eyes teaches the controllers one step at a time (crawl, turn, the view behind
    /// the ant, the antennae, a greeting, then the way on), each step done by doing it; then it steps aside, and the left
    /// controller's menu button brings up a card with every control. In 中文 or English, as the rest of the screen (X
    /// switches). The screen's own lines are made for a monitor: here the goal moves up to the middle and grows, the
    /// prompts and lines grow and name the controllers' A for the keyboard's E, and the minimap (a monitor's corner) is
    /// left out.
    /// </summary>
    [DefaultExecutionOrder(50)]   // after VRPlayer has read the controllers this frame
    public class VRTutorial : MonoBehaviour
    {
        const string K = "<color=#e9d7a8>";
        const float MinShown = 2.5f;        // every step stays this long at least (seconds), to be read

        // each step: its title and its words, in 中文 and in English
        static readonly string[][] Steps =
        {
            new[] { "欢迎来到 Ant Garden", "Welcome to Ant Garden",
                $"你是一只蚂蚁。推动{K}左摇杆</color>，朝你看的方向爬；按下左摇杆可以快爬。",
                $"You are an ant. Push the {K}left stick</color> to crawl the way you look; click it in to run." },
            new[] { "转身", "Turning",
                $"向左或向右拨一下{K}右摇杆</color>，转身 30°。",
                $"Flick the {K}right stick</color> left or right to turn 30°." },
            new[] { "换个视角", "Another view",
                $"按 {K}Y</color>：走出蚂蚁的身体，从它身后看；再按一次，回到它的眼睛。",
                $"Press {K}Y</color> to step out behind the ant; press it again to look through its eyes." },
            new[] { "触角", "Antennae",
                $"停下来，按 {K}B</color> 摆动触角。触角碰到发光的植物，它会亮起来。",
                $"Stand still and press {K}B</color> to wave your antennae. Glowing plants light up at their touch." },
            new[] { "打招呼", "Saying hello",
                $"走近别的蚂蚁或发光的物件，按 {K}A</color> 打招呼、仔细看看。",
                $"Walk up to another ant or a glowing relic and press {K}A</color> to greet it or look closer." },
            new[] { "出发", "Off you go",
                "跟着地上的光走，穿过蚁穴，找到它的心脏。",
                "Follow the light on the ground through the nest to its heart." },
        };

        VRPlayer vr;
        AntController ant;
        RectTransform card, help;
        Text title, body, foot, helpText, prompt;
        int step, turnsAt;
        float shown, walked;
        bool switched, talked, greeted, helpOpen, en;
        Vector3 last;
        string promptSeen;

        public void Init(VRPlayer player, Canvas canvas, AntController playerAnt)
        {
            vr = player;
            ant = playerAnt;
            last = ant.Position;
            en = Loc.En;
            Transform root = canvas.transform;

            card = UiKit.Rect(root, "VR Tutorial", 0.5f, 0.5f, new Vector2(0, -40), new Vector2(1120, 230));
            UiKit.Panel(card, UiKit.PanelBg);
            title = Inset(UiKit.Label(card, "Title", "", 40, UiKit.Warm, TextAnchor.UpperLeft), 34, 20);
            body = Inset(UiKit.Label(card, "Body", "", 32, UiKit.Ink, TextAnchor.UpperLeft), 34, 76);
            foot = Inset(UiKit.Label(card, "Foot", "", 24, UiKit.Dim, TextAnchor.LowerRight), 34, 16);

            help = UiKit.Rect(root, "VR Controls", 0.5f, 0.5f, new Vector2(0, 20), new Vector2(780, 560));
            UiKit.Panel(help, UiKit.PanelBg);
            helpText = Inset(UiKit.Label(help, "Text", "", 32, UiKit.Ink, TextAnchor.UpperLeft), 40, 30);
            help.gameObject.SetActive(false);

            // the screen's own lines, made for a monitor: the goal to the middle and larger, the prompts larger, no minimap
            if (Find(root, "Objective") is RectTransform goal)
            {
                goal.anchorMin = goal.anchorMax = goal.pivot = new Vector2(0.5f, 1f);
                goal.anchoredPosition = new Vector2(0, -30);
                goal.localScale = Vector3.one * 1.5f;
                foreach (Text t in goal.GetComponentsInChildren<Text>(true)) t.alignment = TextAnchor.MiddleCenter;
            }
            foreach (string name in new[] { "Prompt", "Line", "Caption" })
                if (Find(root, name) is RectTransform rt) rt.localScale = Vector3.one * 1.35f;
            if (Find(root, "Minimap") is RectTransform map) map.gameObject.SetActive(false);
            if (Find(root, "Fade") is RectTransform veil) veil.localScale = Vector3.one * 6f;   // a fade covers the whole view, not a window in it
            if (Find(root, "Prompt") is RectTransform pr) prompt = pr.GetComponentInChildren<Text>(true);
            Write();
        }

        void Update()
        {
            if (vr.Help) { helpOpen = !helpOpen; Write(); }
            Vector3 p = ant.Position;
            float d = new Vector2(p.x - last.x, p.z - last.z).magnitude;
            if (d < 1f) walked += d;   // (a longer jump is a teleport)
            last = p;
            switched |= vr.Switched;
            talked |= vr.Talk;
            greeted |= vr.Interact;
            if (en != Loc.En) { en = Loc.En; Write(); }
            if (step >= Steps.Length) return;
            shown += Time.deltaTime;
            if (shown >= MinShown && Done()) Next();
        }

        void LateUpdate()
        {
            // the game's prompts name the keyboard's E: in a headset that is A
            if (prompt == null || prompt.text == promptSeen) return;
            promptSeen = prompt.text.Replace("按 E", "按 A").Replace("press E", "press A").Replace("Press E", "Press A");
            prompt.text = promptSeen;
        }

        /// <summary>The step in hand has been done (the last two pass by themselves).</summary>
        bool Done()
        {
            switch (step)
            {
                case 0: return walked > 3f;
                case 1: return vr.Turns > turnsAt;
                case 2: return switched;
                case 3: return talked;
                case 4: return greeted || shown > 15f;
                default: return shown > 9f;
            }
        }

        void Next()
        {
            step++;
            shown = walked = 0;
            turnsAt = vr.Turns;
            switched = talked = greeted = false;
            Voices.Chime(ant.Position + Vector3.up, 1.5f + 0.12f * step, 0.25f);
            Debug.Log($"[AntGarden] VR tutorial: step {step} of {Steps.Length}");
            Write();
        }

        /// <summary>The cards' words, in the language in use.</summary>
        void Write()
        {
            int l = Loc.En ? 1 : 0;
            bool teaching = step < Steps.Length;
            card.gameObject.SetActive(teaching && !helpOpen);
            help.gameObject.SetActive(helpOpen);
            if (teaching)
            {
                title.text = $"<b>{Steps[step][l]}</b>";
                body.text = Steps[step][2 + l];
                foot.text = Loc.T(
                    $"{K}Y</color> 视角    {K}X</color> English    {K}菜单键</color> 全部操作        {step + 1} / {Steps.Length}",
                    $"{K}Y</color> view    {K}X</color> 中文    {K}Menu</color> all controls        {step + 1} / {Steps.Length}");
            }
            helpText.text = Loc.T(
                $"<b>手柄操作</b>\n\n{K}左摇杆</color>  爬行（按下：快爬）\n{K}右摇杆</color>  转身\n{K}A</color>  打招呼 / 查看\n{K}B</color>  触角（碰亮发光植物）\n" +
                $"{K}Y</color>  蚂蚁的眼睛 / 身后\n{K}X</color>  English\n{K}菜单键</color>  关上这张卡片\n\n<color=#9bbcbc>跟着地上的光走，找到蚁穴的心脏。</color>",
                $"<b>Controls</b>\n\n{K}Left stick</color>  crawl (click in: run)\n{K}Right stick</color>  turn\n{K}A</color>  greet / look closer\n{K}B</color>  antennae (light up plants)\n" +
                $"{K}Y</color>  the ant's eyes / behind it\n{K}X</color>  中文\n{K}Menu</color>  close this card\n\n<color=#9bbcbc>Follow the light on the ground to the heart of the nest.</color>");
        }

        /// <summary>A label kept this far in from its card's sides (left and right alike) and from its top and bottom.</summary>
        static Text Inset(Text t, float side, float edge)
        {
            t.rectTransform.offsetMin = new Vector2(side, edge);
            t.rectTransform.offsetMax = new Vector2(-side, -edge);
            return t;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
