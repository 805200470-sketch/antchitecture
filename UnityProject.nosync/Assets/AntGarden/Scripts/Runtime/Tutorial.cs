using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// The tutorial at the start (教程指引, 11-03): one short instruction at a time, top centre, naming what the player holds
    /// (the pad's sticks and buttons when the pad was used last, the keys otherwise), in both languages and written again the
    /// moment either changes. Each step moves on when the player does it: crawl (left stick), look round (right stick), run
    /// (RT), reach out with the antennae (B), open the map (Y) and close it (B), switch 中文 / English (X; skipped when it was
    /// switched already); then a last line hands over to the nest's chapter, whose card and goal wait until then
    /// (LabNest.Hold): follow the light, which leads on to the next nest. Once a session: not again in the next nest. Holding
    /// ☰ (H on the keyboard) for a second skips it; a short press still opens or closes the help. Off in every capture, check
    /// and demo run (their stills stay as they were) unless -tutorial 1; -tutorial 0 turns it off. When it ends, done or
    /// skipped, the controls top left (-controls) fold away by themselves; ⧉ or C brings them back (Hud.FoldControls, 11-04:
    /// the user, they should not be on screen all the time). It runs the same with them put away: it never reads them.
    /// </summary>
    [DefaultExecutionOrder(50)]   // after Game.Update: this frame's input has been acted on
    public class Tutorial : MonoBehaviour
    {
        enum Step { Move, Look, Run, Antennae, OpenMap, CloseMap, Language, Follow }
        enum Phase { Wait, In, Show, Done, Out }

        const int Taught = 7;              // the steps that teach a control (Follow only hands over)
        const float StartDelay = 3f;       // the nest's banner first (gone at 2.8 s)
        const float FadeIn = 0.35f, Ticked = 0.9f, FadeOut = 0.3f, SkipHold = 1f, FollowFor = 5f;
        const string KeyColour = "<color=#e9d7a8>";   // as the pad's controls name their buttons (Hud.SetControlsVisible)

        static bool seen;   // once a session: a scene reload (the next nest, a quality switch) does not start it again

        /// <summary>While it runs: ☰ / H are its own (held, they skip it; Game leaves the help to it).</summary>
        public static bool Active { get; private set; }
        /// <summary>The tutorial of this world, for checks (PadCheck): null when there is none.</summary>
        public static Tutorial Instance { get; private set; }

        Game game;
        Step step;
        Phase phase;
        float phaseT, stepT, held;
        bool skipped;
        // what the player has done in the step shown
        float walked, turned, ran;
        Vector3 lastPos;
        float lastYaw, lastPitch;
        bool lastTalking, lastEn;
        int toggles, togglesAtStep;
        string shownKey;   // what the labels show (step, tick, language, device): written again only when that changes

        RectTransform panel;
        Image shade, bar;
        Text line, sub;
        CanvasGroup group;

        /// <summary>Checks: the step on screen (0 crawl … 6 language, 7 the hand-over), fully in view or ticked off, its words.</summary>
        public int StepShown => (int)step;
        public bool Showing => phase == Phase.Show;
        public bool Done => phase == Phase.Done;
        public bool Finished { get; private set; }
        public bool Skipped => skipped;
        public string Words => line != null ? line.text : "";

        /// <summary>The flag that makes this a capture, check or demo run (-capture, -demo, -photos, -switchto, -sfxdump, any
        /// -…check, -…shots or -…film), or null in play: such a run has no tutorial, and its prompts stay as they were (Pad).</summary>
        public static readonly string CheckFlag = FindCheckFlag();
        public static bool CheckRun => CheckFlag != null;

        static string FindCheckFlag()
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (a.Length < 2 || a[0] != '-') continue;
                string n = a.Substring(1);
                if (n == "capture" || n == "demo" || n == "photos" || n == "switchto" || n == "sfxdump"
                    || n.EndsWith("check") || n.EndsWith("shots") || n.EndsWith("film"))
                    return a;
            }
            return null;
        }

        /// <summary>Whether this run starts with the tutorial: not once it has been shown this session; not in a capture,
        /// check or demo run unless -tutorial 1; never with -tutorial 0.</summary>
        public static bool Wanted()
        {
            string flag = Game.Arg("-tutorial");
            if (flag == "0" || seen) return false;
            if (flag == "1") return true;
            if (CheckRun) Debug.Log($"[AntGarden] tutorial: off ({CheckFlag})");
            return !CheckRun;
        }

        public void Init(Game g)
        {
            game = g;
            seen = true;
            Active = true;
            Instance = this;
            if (g.LabNest != null) g.LabNest.Hold = true;   // the chapter's card and goal wait for the hand-over
            MakeUi(g.hud);
            lastEn = Loc.En;
            phase = Phase.Wait;
            Debug.Log("[AntGarden] tutorial: on");
        }

        void MakeUi(Hud hud)
        {
            // one quiet line over a soft shade, as the nest's banner below it; the count and how to skip under it, small
            panel = UiKit.Rect(hud.canvas.transform, "Tutorial", 0.5f, 1f, new Vector2(0f, -56f), new Vector2(1200f, 90f));
            shade = UiKit.Panel(UiKit.Rect(panel, "Shade", 0.5f, 0.5f, Vector2.zero, new Vector2(900f, 176f)), new Color(0f, 0.02f, 0.04f, 0.66f));
            shade.sprite = UiKit.Soft();
            shade.raycastTarget = false;
            line = UiKit.Label(panel, "Line", "", 25, UiKit.Ink, TextAnchor.MiddleCenter);
            line.rectTransform.offsetMin = new Vector2(0f, 34f);
            sub = UiKit.Label(panel, "Sub", "", 15, UiKit.Dim, TextAnchor.MiddleCenter);
            sub.rectTransform.offsetMax = new Vector2(0f, -58f);
            // held ☰: a thin line fills under the words, and at its end the tutorial goes
            bar = UiKit.Panel(UiKit.Rect(panel, "Hold", 0.5f, 0f, new Vector2(0f, 2f), new Vector2(0f, 2f)), UiKit.Warm);
            bar.raycastTarget = false;
            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;   // clicks go through to the map under it
            group.interactable = false;
        }

        void Update()
        {
            if (game == null || !game.Ready || game.player == null || panel == null) return;
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            if (Loc.En != lastEn) { lastEn = Loc.En; toggles++; }
            // ☰ / H held a second: skipped; let go sooner, the help opens or closes as it always does
            if (Pad.HelpHeld || Input.GetKey(KeyCode.H))
            {
                held += dt;
                if (held >= SkipHold && !skipped)
                {
                    skipped = true;
                    phase = Phase.Out;
                    phaseT = 0f;
                    Debug.Log($"[AntGarden] tutorial: skipped at step {(int)step + 1}");
                }
            }
            else
            {
                if (held > 0f && held < SkipHold) game.hud.ToggleHelp();
                held = 0f;
            }
            phaseT += dt;
            switch (phase)
            {
                case Phase.Wait:
                    group.alpha = 0f;
                    if (phaseT >= StartDelay) Begin(Step.Move);
                    break;
                case Phase.In:
                case Phase.Show:
                    group.alpha = phase == Phase.In ? Mathf.Clamp01(phaseT / FadeIn) : 1f;
                    if (phase == Phase.In && phaseT >= FadeIn) phase = Phase.Show;
                    if (Did(dt))
                    {
                        Debug.Log($"[AntGarden] tutorial: step {(int)step + 1} {step} done after {stepT:F1}s");
                        phaseT = 0f;
                        if (step == Step.Follow) phase = Phase.Out;
                        else
                        {
                            phase = Phase.Done;
                            Voices.Chime(game.player.Position + Vector3.up, 1.25f + 0.07f * (int)step, 0.12f);   // softly
                        }
                    }
                    break;
                case Phase.Done:
                    group.alpha = 1f;
                    if (phaseT >= Ticked) { phase = Phase.Out; phaseT = 0f; }
                    break;
                case Phase.Out:
                    float outFor = skipped ? 0.25f : FadeOut;
                    group.alpha = Mathf.Min(group.alpha, 1f - Mathf.Clamp01(phaseT / outFor));
                    if (phaseT >= outFor) { Next(); if (panel == null) return; }
                    break;
            }
            Render();
            // above the map, which is built again (last in the canvas) when the language switches
            if (panel.GetSiblingIndex() != panel.parent.childCount - 1) panel.SetAsLastSibling();
        }

        void Begin(Step s)
        {
            step = s;
            phase = Phase.In;
            phaseT = stepT = 0f;
            walked = turned = ran = 0f;
            lastPos = game.player.Position;
            lastYaw = game.rig.yaw;
            lastPitch = game.rig.pitch;
            lastTalking = game.player.Talking;
            togglesAtStep = toggles;
            Debug.Log($"[AntGarden] tutorial: step {(int)s + 1} {s}");
        }

        void Next()
        {
            if (skipped || step == Step.Follow) { Finish(); return; }
            Step next = step + 1;
            if (next == Step.Language && toggles > 0) next = Step.Follow;   // switched once already: it is known
            Begin(next);
        }

        void Finish()
        {
            Active = false;
            Finished = true;
            if (game.LabNest != null) game.LabNest.Hold = false;   // now the chapter's card and its goal (LabNest)
            game.hud.FoldControls();   // the controls are known now: they fold away (⧉ or C brings them back; 11-04)
            Debug.Log($"[AntGarden] tutorial: {(skipped ? "skipped" : "done")}");
            if (panel != null) Destroy(panel.gameObject);
            panel = null;
            enabled = false;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Active = false;   // (left mid-way: the next world has none, it was shown)
        }

        /// <summary>Whether the player has done what the step shown asks.</summary>
        bool Did(float dt)
        {
            stepT += dt;
            AntController ant = game.player;
            Vector3 p = ant.Position;
            float moved = new Vector2(p.x - lastPos.x, p.z - lastPos.z).magnitude;
            lastPos = p;
            if (moved < 1f && ant.speed > 0.05f) walked += moved;   // (a longer jump is a teleport)
            // the camera turned by the right stick or a drag (not by its own follow)
            if (Pad.Look.sqrMagnitude > 0.01f || Input.GetMouseButton(0))
                turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw * Mathf.Rad2Deg, game.rig.yaw * Mathf.Rad2Deg)) + Mathf.Abs(game.rig.pitch - lastPitch) * Mathf.Rad2Deg;
            lastYaw = game.rig.yaw;
            lastPitch = game.rig.pitch;
            if (ant.sprinting) ran += dt;
            bool reached = ant.Talking && !lastTalking;
            lastTalking = ant.Talking;
            switch (step)
            {
                case Step.Move: return walked >= 1.8f;
                case Step.Look: return turned >= 80f;
                case Step.Run: return ran >= 0.8f;
                case Step.Antennae: return reached;
                case Step.OpenMap: return game.bigmap.IsOpen;
                case Step.CloseMap: return !game.bigmap.IsOpen;
                case Step.Language: return toggles > togglesAtStep;
                default: return stepT >= FollowFor || walked >= 6f;
            }
        }

        static string K(string key) => KeyColour + key + "</color>";

        /// <summary>The step's instruction in the language shown, naming the pad's controls or the keys.</summary>
        string Instruction(Step s, bool pad)
        {
            switch (s)
            {
                case Step.Move:
                    return pad ? Loc.T($"推 {K("左摇杆")}，往前爬", $"Push the {K("left stick")} to crawl")
                               : Loc.T($"按 {K("W A S D")}，往前爬", $"Use {K("W A S D")} to crawl");
                case Step.Look:
                    return pad ? Loc.T($"推 {K("右摇杆")}，看看四周", $"Push the {K("right stick")} to look around")
                               : Loc.T($"按住 {K("鼠标左键")} 拖动，看看四周", $"Drag with the {K("left mouse button")} to look around");
                case Step.Run:
                    return pad ? Loc.T($"边爬边按住 {K("RT")}，爬得更快", $"Hold {K("RT")} as you crawl to go faster")
                               : Loc.T($"边爬边按住 {K("Shift")}，爬得更快", $"Hold {K("Shift")} as you crawl to go faster");
                case Step.Antennae:
                    return pad ? Loc.T($"停下来，按 {K("B")} 伸出触角", $"Stop, then press {K("B")} to reach out with your antennae")
                               : Loc.T($"停下来，按 {K("空格")} 伸出触角", $"Stop, then press {K("Space")} to reach out with your antennae");
                case Step.OpenMap:
                    return pad ? Loc.T($"按 {K("Y")} 打开地图", $"Press {K("Y")} to open the map")
                               : Loc.T($"按 {K("M")} 打开地图", $"Press {K("M")} to open the map");
                case Step.CloseMap:
                    return pad ? Loc.T($"按 {K("B")} 关上地图", $"Press {K("B")} to close the map")
                               : Loc.T($"按 {K("M")} 关上地图", $"Press {K("M")} to close the map");
                case Step.Language:
                    return pad ? Loc.T($"按 {K("X")} 切换 中文 / English", $"Press {K("X")} to switch English / 中文")
                               : Loc.T($"按 {K("L")} 切换 中文 / English", $"Press {K("L")} to switch English / 中文");
                default:
                    // (a particle nest's light leads to its way out, which opens on the next nest)
                    return game.LabNest != null ? Loc.T("跟着光走，它通向下一个蚁穴", "Follow the light: it leads to the next nest")
                                                : Loc.T("就这些，去走走吧", "That's all. Off you go");
            }
        }

        /// <summary>Under the instruction: which step of how many, how to skip, and the key for the other language.</summary>
        string Sub(bool pad)
        {
            if (step == Step.Follow) return "";
            string count = $"{(int)step + 1} / {Taught}";
            string skip = pad ? Loc.T($"按住 {K("☰")} 跳过", $"Hold {K("☰")} to skip") : Loc.T($"按住 {K("H")} 跳过", $"Hold {K("H")} to skip");
            if (step == Step.Language) return $"{count}   ·   {skip}";
            return $"{count}   ·   {skip}   ·   {K(pad ? "X" : "L")} {Loc.T("English", "中文")}";
        }

        /// <summary>The labels, written again only when the step, its tick, the language or the device in hand changes.</summary>
        void Render()
        {
            bool pad = Pad.LastUsed;
            bool tick = phase == Phase.Done || (phase == Phase.Out && !skipped && step != Step.Follow);
            string key = $"{step}|{tick}|{Loc.En}|{pad}";
            if (key != shownKey)
            {
                shownKey = key;
                string words = Instruction(step, pad);
                line.text = tick ? $"{KeyColour}✓</color>  {words}" : words;
                line.color = tick ? UiKit.Dim : UiKit.Ink;
                sub.text = Sub(pad);
                shade.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(line.preferredWidth + 360f, 640f, 1400f), 176f);
            }
            bar.rectTransform.sizeDelta = new Vector2(held > 0.12f && !skipped ? Mathf.Clamp01(held / SkipHold) * 420f : 0f, 2f);
        }
    }
}
