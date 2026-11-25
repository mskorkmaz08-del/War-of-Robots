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

            QualitySettings.antiAliasing = 0;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            if (IsPhone)
            {
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.shadowCascades = 1;
                QualitySettings.shadowDistance = 90f;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.softParticles = false;
                QualitySettings.lodBias = 0.7f;
            }
            else
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.shadowCascades = 2;
                QualitySettings.shadowDistance = 170f;
                QualitySettings.pixelLightCount = 2;
            }
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
