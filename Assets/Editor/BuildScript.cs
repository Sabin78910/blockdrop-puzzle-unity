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
            PlayerSettings.productName = "Block Drop"; // short launcher label; the store title carries the keywords
            PlayerSettings.SetApplicationIdentifier(android, "com.sabin.blockdrop");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true; // online leaderboards (optional at runtime)
            bool icons = ConfigureIcons();
            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("ConfigureForPlay done: icons=" + icons);
        }

        /// <summary>Store/legacy icon plus Android adaptive layers (background + foreground),
        /// so modern launchers show a full-bleed icon instead of shrinking it into a white circle.</summary>
        private static bool ConfigureIcons()
        {
            const string dir = "Assets/Art/";
            foreach (var f in new[] { "AppIcon.png", "AppIconBackground.png", "AppIconForeground.png" }) AssetDatabase.ImportAsset(dir + f);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "AppIcon.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "AppIconBackground.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "AppIconForeground.png");
            if (icon == null || bg == null || fg == null) return false;
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                var platformIcons = PlayerSettings.GetPlatformIcons(android, kind);
                bool adaptive = kind.ToString().Contains("Adaptive");
                foreach (var pi in platformIcons) pi.SetTextures(adaptive ? new[] { bg, fg } : new[] { icon });
                PlayerSettings.SetPlatformIcons(android, kind, platformIcons);
            }
            return true;
        }

        /// <summary>Unity Build Automation "Pre-export method": output an Android App Bundle (.aab)
        /// for Google Play instead of an APK. Set on the "Play Release" build target only.</summary>
        public static void PreExportAppBundle()
        {
            EditorUserBuildSettings.buildAppBundle = true;
            IncludeSymbolsInBundle();
            Debug.Log("PreExportAppBundle: building .aab for Google Play");
        }

        /// <summary>Unity Build Automation "Pre-export method" for the Preview target: an installable .apk with its own
        /// app id, name and version code, so it installs next to the Play version without touching it. Never set this
        /// on the Play Release target.</summary>
        public static void PreExportPreview()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.sabin.blockdrop.preview");
            PlayerSettings.productName = "Block Drop Preview";
            PlayerSettings.bundleVersion = "preview";
            // Unity Build Automation numbers each build; reuse it so a newer preview always installs over an older one.
            PlayerSettings.Android.bundleVersionCode = int.TryParse(System.Environment.GetEnvironmentVariable("UCB_BUILD_NUMBER"), out int n) && n > 0 ? n : 1;
            Debug.Log("PreExportPreview: building com.sabin.blockdrop.preview .apk");
        }

        /// <summary>Puts IL2CPP symbol tables inside the .aab so Play Console's Android vitals shows readable
        /// crash stacks. Set by reflection: the API lives in the Android build-support assembly, which a
        /// machine without Android support doesn't have, and a missing type must not break editor scripts.</summary>
        private static void IncludeSymbolsInBundle()
        {
            try
            {
                var settings = System.Type.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols, UnityEditor.Android.Extensions");
                var level = System.Type.GetType("Unity.Android.Types.DebugSymbolLevel, Unity.Android.Types");
                var format = System.Type.GetType("Unity.Android.Types.DebugSymbolFormat, Unity.Android.Types");
                if (settings == null || level == null || format == null) { Debug.LogWarning("Debug symbol settings not found; AAB has no symbols"); return; }
                settings.GetProperty("level").SetValue(null, System.Enum.Parse(level, "SymbolTable"));
                settings.GetProperty("format").SetValue(null, System.Enum.Parse(format, "IncludeInBundle"));
            }
            catch (System.Exception e) { Debug.LogWarning("Could not set debug symbols: " + e.Message); }
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
