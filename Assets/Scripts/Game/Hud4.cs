using UnityEngine;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>
    /// Fitting every screen and installing the game: the UI scales to the screen's shape
    /// (phones, 4:3 tablets, 16:10 laptops, ultrawide monitors), and in the browser the
    /// main menu offers INSTALL so the game gets its own home screen icon.
    /// </summary>
    public partial class Hud
    {
        GameObject installButton, installPanel;
        Text installSteps;
        Text rotateTitle, rotateSub;
        CanvasScaler scaler;
        float nextInstallCheck;
        int screenW, screenH;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int WorInstallState();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void WorInstallPrompt();
#endif

        /// <summary>0 nothing to offer, 1 browser install sheet, 2 iPhone/iPad steps, 3 Android menu steps.</summary>
        static int InstallState()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return WorInstallState(); } catch (System.Exception) { return 0; }
#else
            return 0;
#endif
        }

        // =====================================================================
        // Screen shape
        // =====================================================================

        /// <summary>
        /// The UI is laid out on a 1920x1080 board. Wide screens (16:9 and wider phones)
        /// keep it the full height; narrower ones (iPad 4:3, 16:10 tablets and laptops)
        /// fit it to the width instead, so side panels never run off the edge.
        /// </summary>
        void FitScreenShape()
        {
            int w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            if (w == screenW && h == screenH) return;
            screenW = w; screenH = h;
            if (scaler == null || w <= 0 || h <= 0) return;
            float aspect = (float)w / h;
            scaler.matchWidthOrHeight = aspect >= 16f / 9f - 0.01f ? 1f : 0f;
        }

        // =====================================================================
        // Install to home screen
        // =====================================================================

        void BuildInstall()
        {
            installButton = UiKit.Button(menuPanel.transform, "INSTALL", new Vector2(1f, 1f), new Vector2(-160f, -70f), new Vector2(260f, 84f),
                UiKit.ButtonStyle.Secondary, OnInstallPressed, 34).gameObject;
            installButton.SetActive(false);

            installPanel = Screen("Install", false);
            UiKit.Image(installPanel.transform, null, new Color(0.01f, 0.012f, 0.018f, 0.78f), true);
            var card = UiKit.Panel("Card", installPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 680f));
            UiKit.Title(Place(card, 0.5f, 0.86f, 1000f, 100f), "INSTALL THE GAME", 66, UiKit.TextBright);
            var stripe = UiKit.Rect("Stripe", card, new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(160f, 5f));
            UiKit.Image(stripe, null, UiKit.Accent);
            installSteps = UiKit.Label(Place(card, 0.5f, 0.47f, 940f, 300f), "", 34, UiKit.TextBright, TextAnchor.MiddleLeft);
            installSteps.lineSpacing = 1.15f;
            UiKit.Button(card, "GOT IT", new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(420f, 100f), UiKit.ButtonStyle.Primary,
                () => { installPanel.SetActive(false); menuPanel.SetActive(true); }, 42);
            installPanel.SetActive(false);
            rotatePanel.transform.SetAsLastSibling();

            // The portrait warning also shows on desktops with a tall, narrow window.
            if (!Application.isMobilePlatform && !ShowTouch)
            {
                rotateTitle.text = "WIDEN THE WINDOW";
                rotateSub.text = "War of Robots is played on a wide screen";
            }
        }

        void OnInstallPressed()
        {
            int state = InstallState();
            if (state == 1)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { WorInstallPrompt(); } catch (System.Exception) { }
#endif
                return;
            }
            if (state == 2)
                installSteps.text =
                    "1.  Tap the Share button (the square with an arrow).\n" +
                    "2.  Scroll down and tap Add to Home Screen.\n" +
                    "3.  Tap Add. The game now opens full screen from its own icon, even without a signal.";
            else
                installSteps.text =
                    "1.  Open the browser menu (the three dots).\n" +
                    "2.  Tap Install app or Add to Home screen.\n" +
                    "3.  Tap Install. The game now opens full screen from its own icon, even without a signal.";
            menuPanel.SetActive(false);
            installPanel.SetActive(true);
        }

        /// <summary>Shows INSTALL on the main menu only while installing is possible.</summary>
        void UpdateInstall()
        {
            FitScreenShape();
            if (installButton == null || !menuPanel.activeInHierarchy) return;
            if (Time.unscaledTime < nextInstallCheck) return;
            nextInstallCheck = Time.unscaledTime + 1f;
            bool show = InstallState() != 0;
            if (installButton.activeSelf != show) installButton.SetActive(show);
        }
    }
}
