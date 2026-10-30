using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace AntGarden.EditorTools
{
    /// <summary>
    /// The headset set-ups. PC VR: a Windows PC with a Meta Quest connected through Meta Quest Link (cable or Air Link).
    /// Standalone: the game installed on the Quest itself, an Android app (QuestLook: far fewer splats there). Either way
    /// XR Plug-in Management starts OpenXR as the game starts; with no headset (or no runtime) it gives up and the game
    /// plays on the screen as before. Multi-pass stereo: each eye is drawn as a camera of its own, so the particle
    /// shaders work unchanged. The Quest's controllers (Oculus Touch, and the Quest 3's Touch Plus).
    /// Batch mode: Unity -batchmode -quit -projectPath ... -executeMethod AntGarden.EditorTools.VRSetup.BuildWindowsVR
    /// (or -buildTarget Android ... VRSetup.BuildQuest); the builds go to $ANTGARDEN_BUILDS, or 05_Builds.nosync next to
    /// the project, as AntGarden_Windows_VR and AntGarden_Quest/AntGarden.apk.
    /// </summary>
    public static class VRSetup
    {
        const string Dir = "Assets/XR";
        /// <summary>The standalone app's id on the headset (adb, its files folder: QuestLook).</summary>
        public const string QuestPackage = "com.ucldfpi.antgarden";
        // the Quest's controllers (by name: the Touch Plus profile is not in every version of the plug-in)
        static readonly string[] Controllers = { "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "MetaQuestTouchProControllerProfile" };

        [MenuItem("AntGarden/VR/Configure OpenXR")]
        public static void Configure() => Configure(BuildTargetGroup.Standalone, Controllers);

        /// <summary>A target's OpenXR: its loader started as the game starts, multi-pass, these features on.</summary>
        static OpenXRSettings Configure(BuildTargetGroup g, string[] features)
        {
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
            var on = new List<string>();
            foreach (OpenXRFeature f in oxr.GetFeatures<OpenXRFeature>())
            {
                string n = f.GetType().Name;
                if (System.Array.IndexOf(features, n) < 0) continue;
                f.enabled = true;
                EditorUtility.SetDirty(f);
                on.Add(n);
            }
            EditorUtility.SetDirty(oxr);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AntGarden] VR ({g}): OpenXR loader {(assigned ? "assigned" : "already there or not found")}, start on launch, multi-pass; on: {string.Join(", ", on)}");
            return oxr;
        }

        /// <summary>The standalone headset: OpenXR for Android with Meta Quest support, and the player as a Quest wants it
        /// (64-bit ARM through IL2CPP, Vulkan, Android 12L and later, ASTC textures, landscape).</summary>
        [MenuItem("AntGarden/VR/Configure Quest (Android)")]
        public static void ConfigureQuest()
        {
            var features = new List<string>(Controllers) { "MetaQuestFeature" };
            OpenXRSettings oxr = Configure(BuildTargetGroup.Android, features.ToArray());
            // the Quest 3 among the devices the app says it was made for: this version of the plug-in lists the Quest,
            // Quest 2 and Quest Pro, and a Quest 3 runs an app made for them as a Quest 2 would (its chip held back).
            // (Added for this session of the editor only, as the plug-in keeps it: so here, just before each build.)
            foreach (OpenXRFeature f in oxr.GetFeatures<OpenXRFeature>())
            {
                if (f.GetType().Name != "MetaQuestFeature") continue;
                var add = f.GetType().GetMethod("AddTargetDevice");
                add?.Invoke(f, new object[] { "eureka", "Quest 3", true });
                EditorUtility.SetDirty(f);
                Debug.Log($"[AntGarden] VR (Android): Quest 3 {(add != null ? "added to the target devices" : "NOT added (no AddTargetDevice)")}");
            }
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, QuestPackage);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
            PlayerSettings.Android.optimizedFramePacing = false;   // the headset paces the frames
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;
            AssetDatabase.SaveAssets();
        }

        static string BuildsRoot()
        {
            string root = System.Environment.GetEnvironmentVariable("ANTGARDEN_BUILDS");
            return string.IsNullOrEmpty(root) ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../05_Builds.nosync")) : root;
        }

        static void Build(BuildTarget target, string folder, string file)
        {
            string dir = Path.Combine(BuildsRoot(), folder);
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Path.Combine(dir, file),
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[AntGarden] Build {options.target} (VR): {s.result}, {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:F0}s -> {options.locationPathName}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("AntGarden/Build/Windows VR (x64, OpenXR)")]
        public static void BuildWindowsVR()
        {
            Configure();
            ProjectSetup.Run();
            Build(BuildTarget.StandaloneWindows64, "AntGarden_Windows_VR", "AntGarden.exe");
        }

        [MenuItem("AntGarden/Build/Quest (Android, OpenXR)")]
        public static void BuildQuest()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            ProjectSetup.Run();
            ConfigureQuest();
            // a second copy that installs beside the first, to compare presets in the headset: ANTGARDEN_VARIANT=smooth
            // makes com.ucldfpi.antgarden.smooth, named "Ant Garden" and ANTGARDEN_VARIANT_NAME (its own files folder, so
            // its own quest.json); the project's own id and name are put back afterwards
            string variant = System.Environment.GetEnvironmentVariable("ANTGARDEN_VARIANT");
            if (string.IsNullOrEmpty(variant))
            {
                Build(BuildTarget.Android, "AntGarden_Quest", "AntGarden.apk");
                return;
            }
            string name = PlayerSettings.productName, label = System.Environment.GetEnvironmentVariable("ANTGARDEN_VARIANT_NAME");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, QuestPackage + "." + variant);
            PlayerSettings.productName = name + " " + (string.IsNullOrEmpty(label) ? variant : label);
            try
            {
                Build(BuildTarget.Android, "AntGarden_Quest", $"AntGarden_{variant}.apk");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, QuestPackage);
                PlayerSettings.productName = name;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
