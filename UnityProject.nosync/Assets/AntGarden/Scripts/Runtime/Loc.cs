using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// UI language, 中文 or English. Every on-screen string goes through T(zh, en). L switches the language
    /// in game and the choice is remembered; "-lang en" / "-lang zh" on the command line wins for that run
    /// (the English demo recording uses it) and is not saved.
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
    }
}
