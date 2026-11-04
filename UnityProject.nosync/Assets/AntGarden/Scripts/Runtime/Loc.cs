using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// UI language, 中文 or English. Every on-screen string goes through T(zh, en). L switches the language
    /// in game and the choice is remembered; "-lang en" / "-lang zh" on the command line wins for that run
    /// (the English demo recording uses it) and is not saved. Most strings are written where they are shown; the
    /// controls panel's are here (11-04). What the ants say is data: Resources/AntGarden/Text/npc_lines.json (NpcLines).
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "antgarden.lang";

        public static bool En { get; private set; }

        public static string T(string zh, string en) => En ? en : zh;

        public static void Init(string commandLine)
        {
            En = commandLine != null ? commandLine == "en" : PlayerPrefs.GetString(PrefKey, "zh") == "en";
        }

        public static void Toggle()
        {
            En = !En;
            PlayerPrefs.SetString(PrefKey, En ? "en" : "zh");
        }

        // ---- the controls panel top left (Hud.SetControlsVisible; 11-04) ----
        // It names whatever the player holds, as the tutorial and the prompts do (Pad.LastUsed): the pad's sticks and buttons,
        // or the keys. Its last line says how to put it away (⧉, the pad's View button, or C; its × too), and the note left
        // in its place for a few seconds after says how to bring it back.

        static string K(string key) => "<color=#e9d7a8>" + key + "</color>";   // a button's name, as the tutorial names them

        /// <summary>The pad's View button, ⧉, a third larger than the words round it (size: theirs × 1.35): its sign comes
        /// from a symbol font (UiKit.Font), drawn smaller and lower than ☰ beside it.</summary>
        public static string View(int size) => $"<size={size}>⧉</size>";

        /// <summary>The controls panel's words: the pad's controls, or the keyboard's.</summary>
        public static string Controls(bool pad) => pad
            ? T($"<b>手柄操作</b>\n{K("左摇杆")}  爬行\n{K("右摇杆")}  转镜头\n{K("RT")}  快爬\n{K("A")}  互动\n{K("B")}  触角 / 返回\n{K("Y")}  地图\n{K("LB / RB")}  拉远 / 拉近\n{K("X")}  中文 / English\n{K("☰")}  完整说明\n{K(View(26))}  隐藏",
                $"<b>Pad controls</b>\n{K("Left stick")}  crawl\n{K("Right stick")}  look\n{K("RT")}  run\n{K("A")}  interact\n{K("B")}  antennae / back\n{K("Y")}  map\n{K("LB / RB")}  zoom\n{K("X")}  English / 中文\n{K("☰")}  all controls\n{K(View(26))}  hide")
            : T($"<b>键盘操作</b>\n{K("W A S D")}  爬行\n{K("左键拖动")}  转镜头\n{K("Shift")}  快爬\n{K("E")}  互动\n{K("空格")}  触角\n{K("M")}  地图\n{K("滚轮")}  拉远 / 拉近\n{K("L")}  中文 / English\n{K("H")}  完整说明\n{K("C")}  隐藏",
                $"<b>Keyboard</b>\n{K("W A S D")}  crawl\n{K("Drag")}  look\n{K("Shift")}  run\n{K("E")}  interact\n{K("Space")}  antennae\n{K("M")}  map\n{K("Scroll")}  zoom\n{K("L")}  English / 中文\n{K("H")}  all controls\n{K("C")}  hide");

        /// <summary>Where the controls panel was, for a few seconds after it is put away: how to bring it back.</summary>
        public static string ControlsBack(bool pad) => T($"{K(pad ? View(23) : "C")}  显示操作", $"{K(pad ? View(23) : "C")}  show controls");
    }
}
