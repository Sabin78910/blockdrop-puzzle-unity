using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BlockDrop.EditorTools
{
    /// <summary>Web (WebGL) build for the browser version, installable and playable offline (PWA).
    /// Every WebGL setting is applied here at build time, so ProjectSettings and the Android build stay untouched.
    /// Local: -executeMethod BlockDrop.EditorTools.WebBuild.Build   (output: build/WebGL/BlockDrop)
    /// CI:    game-ci/unity-builder with buildMethod BlockDrop.EditorTools.WebBuild.Build (reads -customBuildPath)</summary>
    public static class WebBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string DefaultOutput = "build/WebGL/BlockDrop";
        private const string ServiceWorker = "service-worker.js";

        [MenuItem("Block Drop/Build for Web")]
        public static void Build()
        {
            string output = Arg("-customBuildPath") ?? DefaultOutput;
            Configure();
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, output, BuildTarget.WebGL, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("Web build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            WritePrecacheList(output);
            Debug.Log($"Web build done: {output} ({report.summary.totalSize / 1024} KB)");
        }

        private static void Configure()
        {
            PlayerSettings.WebGL.template = "PROJECT:BlockDrop";
            // GitHub Pages can't send Content-Encoding headers, so the loader decompresses gzip itself.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            // Hashed names make every build file immutable, so the service worker can cache them forever.
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = false; // the service worker caches; no second copy in IndexedDB
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.runInBackground = false; // a hidden tab pauses instead of burning battery
        }

        /// <summary>Gives the service worker the exact file list and a build id, so a new deploy replaces the
        /// old cache and the first visit stores everything needed to play offline.</summary>
        private static void WritePrecacheList(string output)
        {
            string swPath = Path.Combine(output, ServiceWorker);
            if (!File.Exists(swPath)) { Debug.LogWarning("No service worker in the web template"); return; }
            var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(output, f).Replace('\\', '/'))
                .Where(f => f != ServiceWorker && !f.EndsWith(".map") && !f.StartsWith("."))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            using var sha = SHA256.Create();
            var sb = new StringBuilder();
            foreach (var f in files) sb.Append(f).Append('\n').Append(new FileInfo(Path.Combine(output, f)).Length).Append('\n');
            string id = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").Substring(0, 12).ToLowerInvariant();
            string list = "[\"./\"," + string.Join(",", files.Select(f => "\"./" + f + "\"")) + "]";
            string sw = File.ReadAllText(swPath).Replace("\"__BUILD_ID__\"", "\"" + id + "\"").Replace("[/*__PRECACHE__*/]", list);
            File.WriteAllText(swPath, sw);
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
