using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlockDrop.EditorTools
{
    /// <summary>Batch-mode entry points for CI: -executeMethod BlockDrop.EditorTools.BuildScript.BuildAndroid</summary>
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Block Drop/Create Main Scene")]
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.orthographic = true;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Google Play requirements: app id, API 36, 64-bit IL2CPP, icon, version.
        /// Run once: -executeMethod BlockDrop.EditorTools.BuildScript.ConfigureForPlay</summary>
        [MenuItem("Block Drop/Configure for Google Play")]
        public static void ConfigureForPlay()
        {
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.companyName = "Sabin Khanal";
            PlayerSettings.productName = "Block Drop Puzzle";
            PlayerSettings.SetApplicationIdentifier(android, "com.sabin.blockdrop");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true; // online leaderboards (optional at runtime)
            AssetDatabase.ImportAsset("Assets/Art/AppIcon.png");
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("ConfigureForPlay done: icon=" + (icon != null));
        }

        /// <summary>Unity Build Automation "Pre-export method": output an Android App Bundle (.aab)
        /// for Google Play instead of an APK. Set on the "Play Release" build target only.</summary>
        public static void PreExportAppBundle()
        {
            EditorUserBuildSettings.buildAppBundle = true;
            Debug.Log("PreExportAppBundle: building .aab for Google Play");
        }

        public static void BuildAndroid()
        {
            EnsureScene();
            EditorUserBuildSettings.buildAppBundle = false; // local/CI test build: installable APK
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.sabin.blockdrop");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "build/Android/BlockDrop.apk", BuildTarget.Android, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
