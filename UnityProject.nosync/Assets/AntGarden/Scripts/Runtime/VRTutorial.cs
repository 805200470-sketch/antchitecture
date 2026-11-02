using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// In a headset: a card in front of the eyes teaches the controllers one step at a time (crawl, turn, the view behind
    /// the ant, the antennae, a greeting, then the way on, its light picked out of a darkened nest: VRPathFocus), each
    /// step done by doing it. It is shown the first time the game is played on this headset (VRPlayer.tutorial "auto";
    /// or "always", "never"), a trigger skips it, and after that it is not shown again: the left controller's menu button
    /// brings up the instructions instead, every control, where a trigger runs the tutorial again (the user, 11-01:
    /// "新手教程可以隐藏，重复体验就不用重复打开。新手教程可以作为使用说明放在里面"). In 中文 or English, as the rest of the
    /// screen (X switches). The screen's own lines are made for a monitor: here the goal moves up to the middle and grows,
    /// the prompts and lines grow and name the controllers' A for the keyboard's E, and crawling for walking (11-01:
    /// "确认ui中文字有没有歧义，比如走进蚂蚁，但是实际上不走动"); every card is larger (11-01: "ui字体太小"); and the minimap
    /// moves in from the corner, where a headset's view would lose it (11-01: "右上角缺少小地图和实时位置显示").
    /// </summary>
    [DefaultExecutionOrder(50)]   // after VRPlayer has read the controllers this frame
    public class VRTutorial : MonoBehaviour
    {
        const string K = "<color=#e9d7a8>";
        const float MinShown = 2.5f;        // every step stays this long at least (seconds), to be read
        const float Larger = 1.35f;         // the cards, this much larger than they are made (their words with them)
        const string Seen = "AntGarden.vr.tutorialSeen";   // PlayerPrefs: done or skipped once on this headset
        // (blended in linear light, 10% of a bright line behind a card still reads a third as bright: the instructions,
        // over the goal and the region's name, shut out all of it)
        static readonly Color CardBg = new Color(0.02f, 0.07f, 0.08f, 0.95f);
        static readonly Color HelpBg = new Color(0.02f, 0.07f, 0.08f, 1f);

        // each step: its title and its words, in 中文 and in English (the player stands still: the ant crawls, the player
        // never walks)
        // (the 中文 broken by hand where a line would otherwise end mid-phrase or begin with a stop)
        static readonly string[][] Steps =
        {
            new[] { "欢迎来到 Ant Garden", "Welcome to Ant Garden",
                $"你是一只蚂蚁。站在原地玩，不用走动。\n推{K}左摇杆</color>，蚂蚁朝你看的方向爬；按下左摇杆是快爬。",
                $"You are an ant. Play standing still, no need to walk: push the {K}left stick</color> and the ant crawls the way you look; click it in to run." },
            new[] { "转身", "Turning",
                $"{K}转头</color>看向别处，停一下，蚂蚁就跟着转过去。\n低头看看，那是你的腿。",
                $"{K}Turn your head</color> and hold it there: the ant turns to look with you. Look down: your legs." },
            new[] { "换个视角", "Another view",
                $"按 {K}Y</color>：换到蚂蚁身后看（第三人称），\n推{K}右摇杆</color>绕着蚂蚁转视角；\n再按一次 {K}Y</color>，回到蚂蚁的眼睛（第一人称）。",
                $"Press {K}Y</color> to watch the ant from behind (third person), where the {K}right stick</color> turns the view round it; press it again to look through its eyes (first person)." },
            new[] { "触角", "Antennae",
                $"松开摇杆停下来，按 {K}B</color> 摆动触角。\n触角碰到发光的植物，植物会亮起来。",
                $"Let go of the stick to stop, and press {K}B</color> to wave your antennae. Glowing plants light up at their touch." },
            new[] { "打招呼", "Saying hello",
                $"用左摇杆爬到别的蚂蚁或发光的物件旁边，\n按 {K}A</color> 打招呼、仔细看看。",
                $"Crawl up to another ant or a glowing relic with the left stick, and press {K}A</color> to greet it or look closer." },
            // (the light may be beside or behind the ant: the player is sent to look for it)
            new[] { "出发", "Off you go",
                "看看脚下和四周：地上那条亮起来的光，\n沿着它爬，它带你穿过蚁穴，通向出口。\n右上角的小地图上，箭头就是你。",
                "Look down and around: the light on the ground, brighter now. Crawl along it, through the nest, to the way out. On the map top right, the arrow is you." },
        };

        // the turning step when the right stick turns instead of the head (QuestLook's turn "stick")
        static readonly string[] StickTurn =
        {
            $"向左或向右拨一下{K}右摇杆</color>，转身 30°。",
            $"Flick the {K}right stick</color> left or right to turn 30°.",
        };

        // the screen's own words that read as walking, made for a monitor: in a headset the player stands, the ant crawls
        static readonly string[][] Crawling =
        {
            new[] { "继续跟着光走", "继续沿着光爬" },
            new[] { "跟着地上的光走", "用左摇杆，沿着地上的光爬" },
            new[] { "Go on: the light leads", "Crawl on: the light leads" },
            new[] { "Follow the light on the ground", "Crawl along the light on the ground (left stick)" },
        };

        VRPlayer vr;
        AntController ant;
        RectTransform card, help;
        Text title, body, foot, helpText, prompt, goal;
        CanvasGroup line;
        int step, turnsAt;
        float shown, walked;
        bool switched, talked, greeted, helpOpen, en;
        Vector3 last;
        string promptSeen, goalSeen;

        public void Init(VRPlayer player, Canvas canvas, AntController playerAnt)
        {
            vr = player;
            ant = playerAnt;
            last = ant.Position;
            en = Loc.En;
            Transform root = canvas.transform;

            // the cards a little below straight ahead (the canvas's middle is 3° below the eyes, and about 34 of its units
            // make a degree there), on a darker ground than the screen's (through 0.62 the nest's points read as noise)
            card = UiKit.Rect(root, "VR Tutorial", 0.5f, 0.5f, new Vector2(0, -100), new Vector2(1150, 300));
            card.localScale = Vector3.one * Larger;
            UiKit.Panel(card, CardBg);
            title = Inset(UiKit.Label(card, "Title", "", 48, UiKit.Warm, TextAnchor.UpperLeft), 34, 20);
            body = Inset(UiKit.Label(card, "Body", "", 40, UiKit.Ink, TextAnchor.UpperLeft), 34, 84);
            body.rectTransform.offsetMin = new Vector2(34, 56);   // (down to just over the foot)
            foot = Inset(UiKit.Label(card, "Foot", "", 28, UiKit.Dim, TextAnchor.LowerRight), 34, 16);

            help = UiKit.Rect(root, "VR Controls", 0.5f, 0.5f, new Vector2(0, -20), new Vector2(900, 900));
            help.localScale = Vector3.one * Larger;
            UiKit.Panel(help, HelpBg);
            helpText = Inset(UiKit.Label(help, "Text", "", 34, UiKit.Ink, TextAnchor.UpperLeft), 40, 30);
            Fit(body, 30);
            Fit(helpText, 22);
            help.gameObject.SetActive(false);

            // the screen's own lines, made for a monitor: the goal to the top middle and larger; the prompt (bottom) and
            // a line said (an ant's, a relic's, a hint: where the card is, which steps aside for it) larger and narrower,
            // on the cards' darker ground; the region's name larger; the minimap up and out of the way, about 24° right
            // and 21° up (at 6° up it sat in the middle of the view: the user, 11-01, "小地图的位置更靠上，不要在视觉中心
            // 遮挡"), its arrow (the player) larger; the frame-rate bar gone
            if (Find(root, "Objective") is RectTransform g)
            {
                g.anchorMin = g.anchorMax = g.pivot = new Vector2(0.5f, 1f);
                g.anchoredPosition = new Vector2(0, -30);
                g.localScale = Vector3.one * 2.3f;
                foreach (Text t in g.GetComponentsInChildren<Text>(true)) t.alignment = TextAnchor.MiddleCenter;
                goal = g.GetComponentInChildren<Text>(true);
            }
            if (Find(root, "Prompt") is RectTransform pr)
            {
                pr.sizeDelta = new Vector2(680, 56);
                pr.localScale = Vector3.one * 2.2f;
                if (pr.GetComponent<Image>() is Image bar) bar.color = CardBg;
                prompt = pr.GetComponentInChildren<Text>(true);
            }
            if (Find(root, "Line") is RectTransform said)
            {
                said.sizeDelta = new Vector2(820, 140);
                said.anchoredPosition = new Vector2(0, 307);   // its middle where the card's is
                said.localScale = Vector3.one * 1.9f;
                if (said.GetComponent<Image>() is Image ground) ground.color = CardBg;
                line = said.GetComponent<CanvasGroup>();
            }
            if (Find(root, "Caption") is RectTransform caption) caption.localScale = Vector3.one * 1.35f;
            if (Find(root, "Region") is RectTransform region) region.localScale = Vector3.one * 1.5f;
            if (Find(root, "Minimap") is RectTransform map)
            {
                map.anchoredPosition = new Vector2(91, 498);
                map.localScale = Vector3.one * 1.4f;
                if (Find(map, "Player") is RectTransform me) me.localScale = Vector3.one * 2f;
            }
            if (Find(root, "Stats") is RectTransform stats) stats.gameObject.SetActive(false);
            if (Find(root, "Fade") is RectTransform veil) veil.localScale = Vector3.one * 6f;   // a fade covers the whole view, not a window in it

            // the tutorial the first time on this headset; after that, where to find it
            if (vr.tutorial == "never" || (vr.tutorial != "always" && PlayerPrefs.GetInt(Seen, 0) == 1))
            {
                step = Steps.Length;
                Game.Instance?.hud?.ShowLine(Loc.T("使用说明", "Instructions"),
                    Loc.T("按菜单键看操作说明；在说明里扣一下扳机，可以重看新手教程。", "Press Menu for the instructions; there, pull a trigger to see the tutorial again."));
            }
            else if (vr.tutorialFrom < 0)
            {
                // (a test: the instructions open instead)
                step = Steps.Length;
                helpOpen = true;
            }
            else if (vr.tutorialFrom > 0)
            {
                // (a test: from this step on)
                step = Mathf.Min(vr.tutorialFrom, Steps.Length - 1);
                if (step == Steps.Length - 1) Focus();
            }
            Write();
            Debug.Log($"[AntGarden] VR tutorial: {(step < Steps.Length ? "shown" : "not shown")} ({vr.tutorial}, seen before {PlayerPrefs.GetInt(Seen, 0) == 1})");
        }

        void Update()
        {
            bool teaching = step < Steps.Length;
            if (vr.Help) { helpOpen = !helpOpen; Write(); }
            else if (vr.Trigger && helpOpen) Restart();
            else if (vr.Trigger && teaching) Skip();
            Vector3 p = ant.Position;
            float d = new Vector2(p.x - last.x, p.z - last.z).magnitude;
            if (d < 1f) walked += d;   // (a longer jump is a teleport)
            last = p;
            switched |= vr.Switched;
            talked |= vr.Talk;
            greeted |= vr.Interact;
            if (en != Loc.En) { en = Loc.En; Write(); }
            if (step >= Steps.Length) return;
            if (line != null && line.alpha >= 0.01f) return;   // (the card has stepped aside for a line: the step waits)
            shown += Time.deltaTime;
            if (shown >= MinShown && Done()) Next();
        }

        void LateUpdate()
        {
            // one thing to read at a time: while a line is said the card steps aside
            bool cardOn = step < Steps.Length && !helpOpen && (line == null || line.alpha < 0.01f);
            if (card.gameObject.activeSelf != cardOn) card.gameObject.SetActive(cardOn);
            // the game's prompts name the keyboard's E: in a headset that is A; and its goal crawls rather than walks
            if (prompt != null && prompt.text != promptSeen)
            {
                promptSeen = Crawl(prompt.text.Replace("按 E", "按 A").Replace("press E", "press A").Replace("Press E", "Press A"));
                prompt.text = promptSeen;
            }
            if (goal != null && goal.text != goalSeen)
            {
                goalSeen = Crawl(goal.text);
                goal.text = goalSeen;
            }
        }

        static string Crawl(string s)
        {
            foreach (string[] w in Crawling) s = s.Replace(w[0], w[1]);
            return s;
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
            Begin();
            Voices.Chime(ant.Position + Vector3.up, 1.5f + 0.12f * step, 0.25f);
            Debug.Log($"[AntGarden] VR tutorial: step {step} of {Steps.Length}");
            if (step == Steps.Length - 1) Focus();
            if (step >= Steps.Length) Remember();
            Write();
        }

        /// <summary>A trigger while it teaches: no more of it, now or next time (the instructions have it).</summary>
        void Skip()
        {
            step = Steps.Length;
            Remember();
            Game.Instance?.hud?.ShowLine(Loc.T("新手教程已跳过", "Tutorial skipped"),
                Loc.T("按菜单键看操作说明，里面可以重看新手教程。", "Press Menu for the instructions; the tutorial is there too."));
            Debug.Log("[AntGarden] VR tutorial: skipped");
            Write();
        }

        /// <summary>A trigger in the instructions: the tutorial again, from its first step.</summary>
        void Restart()
        {
            helpOpen = false;
            step = 0;
            Begin();
            Debug.Log("[AntGarden] VR tutorial: again");
            Write();
        }

        /// <summary>The way on: its light picked out of the darkened nest (VRPathFocus).</summary>
        void Focus()
        {
            var focus = GetComponent<VRPathFocus>();
            if (focus != null) focus.Show();
        }

        void Begin()
        {
            shown = walked = 0;
            turnsAt = vr.Turns;
            switched = talked = greeted = false;
        }

        static void Remember()
        {
            PlayerPrefs.SetInt(Seen, 1);
            PlayerPrefs.Save();
        }

        /// <summary>The cards' words, in the language in use.</summary>
        void Write()
        {
            int l = Loc.En ? 1 : 0;
            bool teaching = step < Steps.Length;
            help.gameObject.SetActive(helpOpen);   // (the card: LateUpdate)
            if (teaching)
            {
                title.text = $"<b>{Steps[step][l]}</b>";
                body.text = step == 1 && !vr.gazeTurn ? StickTurn[l] : Steps[step][2 + l];
                foot.text = Loc.T(
                    $"{K}扳机</color> 跳过教程    {K}菜单键</color> 使用说明    {K}Y</color> 视角    {K}X</color> English        {step + 1} / {Steps.Length}",
                    $"{K}Trigger</color> skip    {K}Menu</color> instructions    {K}Y</color> view    {K}X</color> 中文        {step + 1} / {Steps.Length}");
            }
            // turning: through its eyes after the head (or the right stick, in steps); behind it the right stick turns the
            // view round it
            string turning = vr.gazeTurn
                ? Loc.T($"{K}转头</color>  蚂蚁跟着你看的方向转身（第一人称）\n{K}右摇杆</color>  绕着蚂蚁转视角（第三人称）",
                    $"{K}Turn your head</color>  the ant turns the way you look (first person)\n{K}Right stick</color>  turn the view round the ant (third person)")
                : Loc.T($"{K}右摇杆</color>  转身 30°（第一人称）/ 绕着蚂蚁转视角（第三人称）",
                    $"{K}Right stick</color>  turn 30° (first person) / turn the view round the ant (third person)");
            helpText.text = Loc.T(
                $"<b>使用说明</b>\n站在原地玩，不用走动。\n\n{K}左摇杆</color>  蚂蚁朝你看的方向爬（按下：快爬）\n{turning}\n{K}A</color>  打招呼 / 查看\n{K}B</color>  摆动触角（碰亮发光植物）\n" +
                $"{K}Y</color>  第一人称 / 第三人称（蚂蚁身后）\n{K}按下右摇杆</color>  真实走动 开 / 关（默认关）\n{K}X</color>  English\n{K}扳机</color>  重看新手教程\n{K}菜单键</color>  关上说明\n" +
                $"放下手柄也能玩：{K}右手</color>拇指捏食指 = A，捏中指 = B；{K}左手</color>捏住食指 = 往前爬，捏中指 = Y\n\n" +
                "<color=#9bbcbc>沿着地上的光爬，它通向出口。右上角小地图上的箭头就是你。</color>",
                $"<b>Instructions</b>\nPlay standing still: no need to walk.\n\n{K}Left stick</color>  the ant crawls the way you look (click in: run)\n{turning}\n{K}A</color>  greet / look closer\n{K}B</color>  wave the antennae (light up plants)\n" +
                $"{K}Y</color>  first person / third person (behind the ant)\n{K}Right stick click</color>  walk for real on / off (off at first)\n{K}X</color>  中文\n{K}Trigger</color>  the tutorial again\n{K}Menu</color>  close\n" +
                $"Controllers down? {K}Right hand</color>: pinch thumb and index = A, thumb and middle = B; {K}left hand</color>: hold an index pinch to crawl, middle = Y\n\n" +
                "<color=#9bbcbc>Crawl along the light on the ground: it leads to the way out. On the map top right, the arrow is you.</color>");
        }

        /// <summary>A card's words kept inside it whatever font the headset has (its lines may stand taller than a
        /// computer's, and English runs longer than 中文): as large as they were made while they fit, a little smaller
        /// (down to least) where they would not, never spilling out below the card.</summary>
        static void Fit(Text t, int least)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = least;
            t.resizeTextMaxSize = t.fontSize;
            t.verticalOverflow = VerticalWrapMode.Truncate;
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
