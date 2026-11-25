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
        public static void ApplyQuality(GameSettings.Quality q)
        {
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
