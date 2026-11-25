using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RobotArena.EditorTools
{
    /// <summary>
    /// Entry point for the cloud builds on GitHub Actions (GameCI).
    /// Generates the test scene, applies player settings and builds for
    /// whatever platform the build machine was started with (WebGL or iOS).
    /// </summary>
    public static class CIBuild
    {
        public const string ProductName = "War of Robots";
        public const string CompanyName = "Mustafa";
        public const string BundleId = "com.mustafa.warofrobots";
        /// <summary>Unity Gaming Services project used for online play (Relay rooms). Not a secret.</summary>
        public const string CloudProjectId = "";

        public static void Build()
        {
            string outputPath = GetArg("-customBuildPath");
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            Debug.Log($"[CIBuild] Target: {target}, output: {outputPath}");

            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogError("[CIBuild] Missing -customBuildPath argument.");
                EditorApplication.Exit(1);
                return;
            }

            ArenaBuilder.BuildScene();
            ApplyPlayerSettings(target);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ArenaBuilder.ScenePath },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[CIBuild] Result: {summary.result}, size: {summary.totalSize} bytes, errors: {summary.totalErrors}");

            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void ApplyPlayerSettings(BuildTarget target)
        {
            // Use both input backends: keyboard/touch code uses the classic one,
            // UI works with either. Best effort; ignored if the setting moves.
            var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (all != null && all.Length > 0)
            {
                var so = new SerializedObject(all[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null) handler.intValue = 2;
                // Link the build to the online services project so rooms can be created.
                var cloudId = so.FindProperty("cloudProjectId");
                if (cloudId != null && !string.IsNullOrEmpty(CloudProjectId)) cloudId.stringValue = CloudProjectId;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[CIBuild] Cloud project id: " + (string.IsNullOrEmpty(CloudProjectId) ? "(none)" : CloudProjectId));
            }

            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            if (target == BuildTarget.WebGL)
            {
                // GitHub Pages can't serve pre-compressed files with the right headers,
                // so ship uncompressed files. Simple and works in Safari on iPhone.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.runInBackground = false;
                // Our page: renders phones at normal pixel density and fills the screen.
                PlayerSettings.WebGL.template = "PROJECT:WarOfRobots";
                PlayerSettings.WebGL.dataCaching = true;
                // Best effort: these settings moved between Unity versions, so set them by name.
                SetStaticByName("UnityEditor.PlayerSettings+WebGL", "powerPreference", "HighPerformance");
                SetStaticByName("UnityEditor.WebGL.UserBuildSettings", "codeOptimization", "RuntimeSpeed");
            }
            else if (target == BuildTarget.iOS)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
                PlayerSettings.iOS.appleEnableAutomaticSigning = false;
                PlayerSettings.iOS.targetOSVersionString = "15.0";
            }
        }

        static void SetStaticByName(string typeName, string property, string enumValue)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetType(typeName, false);
                    if (type == null) continue;
                    var prop = type.GetProperty(property, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop == null || !prop.CanWrite) continue;
                    object value = prop.PropertyType.IsEnum ? Enum.Parse(prop.PropertyType, enumValue) : (object)enumValue;
                    prop.SetValue(null, value);
                    Debug.Log($"[CIBuild] {typeName}.{property} = {enumValue}");
                    return;
                }
                Debug.Log($"[CIBuild] {typeName}.{property} not found, skipped.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CIBuild] Could not set {typeName}.{property}: {e.Message}");
            }
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
