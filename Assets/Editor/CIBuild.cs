using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace RobotArena.EditorTools
{
    /// <summary>
    /// Entry point for the cloud builds on GitHub Actions (GameCI).
    /// Generates the test scene, applies player settings and builds for
    /// whatever platform the build machine was started with (WebGL, iOS or Android).
    /// </summary>
    public static class CIBuild
    {
        public const string ProductName = "War of Robots";
        public const string CompanyName = "Mustafa";
        public const string BundleId = "com.mustafa.warofrobots";
        /// <summary>Unity Gaming Services project used for online play (Relay rooms). Not a secret.</summary>
        public const string CloudProjectId = "2538ba18-7cb3-4e0d-8e70-71e1170fdf33";

        /// <summary>
        /// Signing key for the Android .apk that is installed by hand (sideloaded). Android
        /// only installs an update over the old app when both are signed with the same key,
        /// so every cloud build uses this one. It is in the repository on purpose; for a
        /// Play Store release, use a private key passed in by the build instead.
        /// </summary>
        const string SideloadKeystore = "Tools/android/sideload.keystore";
        const string SideloadAlias = "warofrobots";
        const string SideloadPassword = "warofrobots-sideload";

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

            // The game logo as the app icon (Unity makes every size from it).
            try
            {
                AssetDatabase.ImportAsset("Assets/Art/AppIcon.png", ImportAssetOptions.ForceUpdate);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon.png");
                if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                Debug.Log("[CIBuild] App icon: " + (icon != null ? "set" : "missing"));
            }
            catch (Exception e) { Debug.LogWarning("[CIBuild] App icon skipped: " + e.Message); }

            if (target == BuildTarget.WebGL)
            {
                // GitHub Pages can't serve pre-compressed files with the right headers,
                // so ship uncompressed files. Simple and works in Safari on iPhone.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.runInBackground = false;
                // Our page: renders phones at normal pixel density and fills the screen.
                PlayerSettings.WebGL.template = "PROJECT:WarOfRobots";
                // The page's own offline cache (sw.js) keeps the game files, so Unity's
                // separate copy of the data file would only take the space twice.
                PlayerSettings.WebGL.dataCaching = false;
                // Best effort: these settings moved between Unity versions, so set them by name.
                SetStaticByName("UnityEditor.PlayerSettings+WebGL", "powerPreference", "HighPerformance");
                SetStaticByName("UnityEditor.WebGL.UserBuildSettings", "codeOptimization", "RuntimeSpeed");
                // The online packages make the WebAssembly link big; smaller generated
                // code keeps the linker within the build machine's memory.
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            }
            else if (target == BuildTarget.iOS)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
                PlayerSettings.iOS.appleEnableAutomaticSigning = false;
                PlayerSettings.iOS.targetOSVersionString = "15.0";
            }
            else if (target == BuildTarget.Android)
            {
                ApplyAndroidSettings();
            }
        }

        static void ApplyAndroidSettings()
        {
#if UNITY_ANDROID
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            // Android 7 and newer; 64-bit and older 32-bit phones.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.startInFullscreen = true;
            EditorUserBuildSettings.buildAppBundle = false;

            // Version: the build number from GitHub, so each new .apk installs over the last.
            if (int.TryParse(GetArg("-androidVersionCode"), out int code) && code > 0)
                PlayerSettings.Android.bundleVersionCode = code;

            // Signing: a private key if the build passes one, else the shared sideload key.
            string keystore = GetArg("-androidKeystoreName");
            string storePass = GetArg("-androidKeystorePass");
            string alias = GetArg("-androidKeyaliasName");
            string aliasPass = GetArg("-androidKeyaliasPass");
            if (string.IsNullOrEmpty(keystore) || !System.IO.File.Exists(keystore) || string.IsNullOrEmpty(storePass))
            {
                keystore = SideloadKeystore; storePass = SideloadPassword; alias = SideloadAlias; aliasPass = SideloadPassword;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = System.IO.Path.GetFullPath(keystore);
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(aliasPass) ? storePass : aliasPass;
            Debug.Log("[CIBuild] Android signing key: " + (keystore == SideloadKeystore ? "shared sideload key" : "private key"));

            // Launcher icons: adaptive (Android 8+, cut to the phone's icon shape), round and square.
            try
            {
                foreach (var path in new[] { "Assets/Art/AppIconAdaptiveBack.png", "Assets/Art/AppIconAdaptiveFront.png", "Assets/Art/AppIconRound.png" })
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var back = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIconAdaptiveBack.png");
                var front = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIconAdaptiveFront.png");
                var round = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIconRound.png");
                var square = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon.png");
                SetAndroidIcons(AndroidPlatformIconKind.Adaptive, back, front);
                SetAndroidIcons(AndroidPlatformIconKind.Round, round);
                SetAndroidIcons(AndroidPlatformIconKind.Legacy, square);
                Debug.Log("[CIBuild] Android icons set");
            }
            catch (Exception e) { Debug.LogWarning("[CIBuild] Android icons skipped: " + e.Message); }
#endif
        }

#if UNITY_ANDROID
        static void SetAndroidIcons(PlatformIconKind kind, params Texture2D[] layers)
        {
            foreach (var layer in layers) if (layer == null) return;
#pragma warning disable 618 // the BuildTargetGroup form exists in every Unity version
            var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
#pragma warning restore 618
        }
#endif

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
