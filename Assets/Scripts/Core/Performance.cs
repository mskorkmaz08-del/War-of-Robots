using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Frame pacing and graphics quality, tuned per device.
    /// Phones (app or browser) get shorter, simpler shadows and fewer per-pixel lights.
    /// </summary>
    public static class Performance
    {
        static bool applied;

        public static bool IsPhone => Application.isMobilePlatform;

        public static void Apply()
        {
            // Never let one slow frame turn into a long physics catch-up (that reads as a freeze).
            Time.maximumDeltaTime = 0.1f;

#if UNITY_WEBGL && !UNITY_EDITOR
            // In the browser, a frame-rate cap makes Unity use timers instead of the
            // browser's own frame callback, which stutters. Let the browser pace frames.
            Application.targetFrameRate = -1;
#else
            Application.targetFrameRate = 60;
#endif
            QualitySettings.vSyncCount = 0;

            if (applied) return;
            applied = true;
            GameSettings.Apply();
        }

        /// <summary>Graphics quality from the settings menu.</summary>
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void WorSetPixelRatio(float ratio);
#endif

        /// <summary>Render resolution on the web page: sharper text and edges on higher settings.</summary>
        static void ApplyPixelRatio(GameSettings.Quality q)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            float r = q == GameSettings.Quality.Low ? 1f : q == GameSettings.Quality.Medium ? 1.5f : 2f;
            try { WorSetPixelRatio(r); } catch (System.Exception) { }
#elif UNITY_ANDROID && !UNITY_EDITOR
            ApplyNativeResolution(q);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static int nativeW, nativeH;

        /// <summary>
        /// The Android app renders at the same sharpness as the browser version: 1x, 1.5x
        /// or 2x of the screen's "normal" pixel size (160 dpi), instead of the panel's full
        /// 3x-4x density, which costs many times the work for detail nobody can see.
        /// </summary>
        static void ApplyNativeResolution(GameSettings.Quality q)
        {
            if (nativeW == 0)
            {
                nativeW = Display.main.systemWidth > 0 ? Display.main.systemWidth : UnityEngine.Screen.width;
                nativeH = Display.main.systemHeight > 0 ? Display.main.systemHeight : UnityEngine.Screen.height;
                // The game only runs sideways.
                if (nativeW < nativeH) { int t = nativeW; nativeW = nativeH; nativeH = t; }
            }
            if (nativeW <= 0 || nativeH <= 0) return;
            float ratio = q == GameSettings.Quality.Low ? 1f : q == GameSettings.Quality.Medium ? 1.5f : 2f;
            float density = UnityEngine.Screen.dpi > 0f ? UnityEngine.Screen.dpi / 160f : 2.5f;
            float scale = Mathf.Clamp(ratio / Mathf.Max(1f, density), 0.35f, 1f);
            // Never go below 720 lines on the short side, so text stays crisp on small phones.
            float shortSide = Mathf.Min(nativeW, nativeH);
            scale = Mathf.Max(scale, Mathf.Min(1f, 720f / shortSide));
            int w = Mathf.RoundToInt(nativeW * scale), h = Mathf.RoundToInt(nativeH * scale);
            if (w == UnityEngine.Screen.width && h == UnityEngine.Screen.height) return;
            UnityEngine.Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        }
#endif

        public static void ApplyQuality(GameSettings.Quality q)
        {
            ApplyPixelRatio(q);
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.softParticles = false;
            switch (q)
            {
                case GameSettings.Quality.Low:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.lodBias = 0.5f;
                    break;
                case GameSettings.Quality.Medium:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowCascades = 1;
                    QualitySettings.shadowDistance = 90f;
                    QualitySettings.pixelLightCount = 1;
                    QualitySettings.lodBias = 0.8f;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = IsPhone ? ShadowResolution.Medium : ShadowResolution.High;
                    QualitySettings.shadowCascades = IsPhone ? 1 : 2;
                    QualitySettings.shadowDistance = IsPhone ? 130f : 170f;
                    QualitySettings.pixelLightCount = IsPhone ? 2 : 3;
                    QualitySettings.lodBias = 1f;
                    break;
            }
        }

        /// <summary>
        /// A reflection probe rendered once when it appears, so metal armour reflects
        /// the sky and the arena instead of looking flat and dark.
        /// </summary>
        public static ReflectionProbe AddReflections(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Reflections");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.size = size;
            probe.hdr = false;
            probe.shadowDistance = 0f;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            probe.importance = 1;
            return probe;
        }

        /// <summary>Camera settings: the fog hides everything past ~650 m anyway.</summary>
        public static void TuneCamera(Camera cam)
        {
            if (cam == null) return;
            cam.farClipPlane = Mathf.Min(cam.farClipPlane, 700f);
            cam.allowMSAA = false;
            cam.allowHDR = false;
        }
    }
}
