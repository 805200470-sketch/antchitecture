using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace AntGarden.EditorTools
{
    /// <summary>
    /// The headset set-up, for PC VR: a Windows PC with a Meta Quest connected through Meta Quest Link (cable or Air
    /// Link). XR Plug-in Management starts OpenXR as the game starts; with no headset (or no runtime) it gives up and
    /// the game plays on the screen as before. Multi-pass stereo: each eye is drawn as a camera of its own, so the
    /// particle shaders work unchanged. The Quest's controllers (Oculus Touch, and the Quest 3's Touch Plus).
    /// Batch mode: Unity -batchmode -quit -projectPath ... -executeMethod AntGarden.EditorTools.VRSetup.BuildWindowsVR
    /// (the build goes to $ANTGARDEN_BUILDS, or 05_Builds.nosync next to the project, as AntGarden_Windows_VR).
    /// </summary>
    public static class VRSetup
    {
        const string Dir = "Assets/XR";

        [MenuItem("AntGarden/VR/Configure OpenXR")]
        public static void Configure()
        {
            BuildTargetGroup g = BuildTargetGroup.Standalone;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, Dir + "/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(g)) perTarget.CreateDefaultSettingsForBuildTarget(g);
            if (!perTarget.HasManagerSettingsForBuildTarget(g)) perTarget.CreateDefaultManagerSettingsForBuildTarget(g);
            XRGeneralSettings settings = perTarget.SettingsForBuildTarget(g);
            settings.InitManagerOnStart = true;
            bool assigned = XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", g);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(perTarget);

            FeatureHelpers.RefreshFeatures(g);
            OpenXRSettings oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
            oxr.renderMode = OpenXRSettings.RenderMode.MultiPass;
            // the Quest's controllers (by name: the Touch Plus profile is not in every version of the plug-in)
            var on = new System.Collections.Generic.List<string>();
            foreach (OpenXRFeature f in oxr.GetFeatures<OpenXRFeature>())
            {
                string n = f.GetType().Name;
                if (n == "OculusTouchControllerProfile" || n == "MetaQuestTouchPlusControllerProfile" || n == "MetaQuestTouchProControllerProfile")
                {
                    f.enabled = true;
                    on.Add(n);
                }
            }
            EditorUtility.SetDirty(oxr);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AntGarden] VR: OpenXR loader {(assigned ? "assigned" : "already there or not found")}, start on launch, multi-pass; controllers: {string.Join(", ", on)}");
        }

        [MenuItem("AntGarden/Build/Windows VR (x64, OpenXR)")]
        public static void BuildWindowsVR()
        {
            Configure();
            ProjectSetup.Run();
            string root = System.Environment.GetEnvironmentVariable("ANTGARDEN_BUILDS");
            if (string.IsNullOrEmpty(root)) root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../05_Builds.nosync"));
            string dir = Path.Combine(root, "AntGarden_Windows_VR");
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Path.Combine(dir, "AntGarden.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[AntGarden] Build {options.target} (VR): {s.result}, {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:F0}s -> {options.locationPathName}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
