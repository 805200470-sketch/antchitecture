using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AntGarden.EditorTools
{
    /// <summary>
    /// Player builds into ~/Desktop/AntGarden/05_Builds.nosync.
    /// Batch mode: Unity -batchmode -quit -projectPath ... -executeMethod AntGarden.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        static string BuildsRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../05_Builds.nosync"));

        [MenuItem("AntGarden/Build/Windows (x64)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "AntGarden_Windows", "AntGarden.exe");

        [MenuItem("AntGarden/Build/macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "AntGarden_Mac", "AntGarden.app");

        static void Build(BuildTarget target, string folder, string exe)
        {
            ProjectSetup.Run();
            string dir = Path.Combine(BuildsRoot, folder);
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Path.Combine(dir, exe),
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            if (target == BuildTarget.StandaloneOSX && s.result == BuildResult.Succeeded) DisableAppNap(options.locationPathName);
            Debug.Log($"[AntGarden] Build {target}: {s.result}, {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:F0}s -> {options.locationPathName}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// macOS App Nap freezes a game whose window is covered by other windows (the player loop stops).
        /// LSAppNapIsDisabled in Info.plist keeps the demo running when it is not the front window.
        /// </summary>
        static void DisableAppNap(string appPath)
        {
            string plist = Path.Combine(appPath, "Contents", "Info.plist");
            if (!File.Exists(plist)) return;
            string text = File.ReadAllText(plist);
            if (text.Contains("LSAppNapIsDisabled")) return;
            int top = text.IndexOf("<dict>", StringComparison.Ordinal); // the top-level dictionary only
            if (top < 0) return;
            text = text.Insert(top + "<dict>".Length, "\n\t<key>LSAppNapIsDisabled</key>\n\t<true/>");
            File.WriteAllText(plist, text);
        }
    }
}
