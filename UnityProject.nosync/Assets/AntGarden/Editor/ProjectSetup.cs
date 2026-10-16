using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AntGarden.EditorTools
{
    /// <summary>
    /// One-shot project configuration: player settings, the (empty) main scene and the build list.
    /// The game builds its whole world at runtime from the path data, so the scene only needs to exist.
    /// Batch mode: Unity -batchmode -quit -projectPath ... -executeMethod AntGarden.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/AntGarden/Scenes/Main.unity";

        [MenuItem("AntGarden/Setup Project")]
        public static void Run()
        {
            PlayerSettings.companyName = "UCL DFPI";
            PlayerSettings.productName = "Ant Garden";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SplashScreen.show = false; // allowed by the Pro / Student licence
            PlayerSettings.SplashScreen.showUnityLogo = false;
            // a resizable window by default (F toggles full screen in game). Retina off: on a Mac the
            // window renders at its point size, not 2x, which keeps the GPU (and the laptop) cool.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.macRetinaSupport = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.gcIncremental = true; // spreads garbage collection over frames: fewer hitches
            PlayerSettings.gpuSkinning = true;   // hundreds of skinned ants: deform them on the GPU
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            // Windows: DX11 first (most compatible), Vulkan as fallback; macOS: Metal.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Vulkan });

            EnsureScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[AntGarden] Project setup done.");
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // The Game bootstrap (AntGarden.Runtime) creates camera, lights and world at runtime.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.skybox = null;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
