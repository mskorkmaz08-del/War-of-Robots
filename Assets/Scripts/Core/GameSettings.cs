using UnityEngine;

namespace RobotArena
{
    /// <summary>Player settings, saved on the device (PlayerPrefs works in the browser too).</summary>
    public static class GameSettings
    {
        public enum Quality { Low, Medium, High }

        static bool loaded;
        static float volume = 0.9f;
        static float lookSensitivity = 1f;
        static bool cameraShake = true;
        static Quality quality = Quality.Medium;
        static bool showFps;

        public static event System.Action Changed;

        public static float Volume { get { Load(); return volume; } set { volume = Mathf.Clamp01(value); Save(); } }
        public static float LookSensitivity { get { Load(); return lookSensitivity; } set { lookSensitivity = Mathf.Clamp(value, 0.2f, 3f); Save(); } }
        public static bool CameraShake { get { Load(); return cameraShake; } set { cameraShake = value; Save(); } }
        public static Quality GraphicsQuality { get { Load(); return quality; } set { quality = value; Save(); } }
        public static bool ShowFps { get { Load(); return showFps; } set { showFps = value; Save(); } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                volume = PlayerPrefs.GetFloat("volume", 0.9f);
                lookSensitivity = PlayerPrefs.GetFloat("look", 1f);
                cameraShake = PlayerPrefs.GetInt("shake", 1) == 1;
                showFps = PlayerPrefs.GetInt("fps", 0) == 1;
                int q = PlayerPrefs.GetInt("quality", -1);
                quality = q < 0 ? (Application.isMobilePlatform ? Quality.Medium : Quality.High) : (Quality)Mathf.Clamp(q, 0, 2);
            }
            catch { }
        }

        static void Save()
        {
            try
            {
                PlayerPrefs.SetFloat("volume", volume);
                PlayerPrefs.SetFloat("look", lookSensitivity);
                PlayerPrefs.SetInt("shake", cameraShake ? 1 : 0);
                PlayerPrefs.SetInt("fps", showFps ? 1 : 0);
                PlayerPrefs.SetInt("quality", (int)quality);
                PlayerPrefs.Save();
            }
            catch { }
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Pushes the settings into the engine.</summary>
        public static void Apply()
        {
            Load();
            AudioListener.volume = volume;
            Performance.ApplyQuality(quality);
        }
    }
}
