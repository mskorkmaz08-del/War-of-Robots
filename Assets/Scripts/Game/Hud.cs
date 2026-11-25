using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>
    /// Every screen, built in code: main menu, hangar (robot preview), settings,
    /// in-fight HUD with radar and touch controls, pause menu and the end screen.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        MatchManager match;
        Canvas canvas;

        GameObject menuPanel, hangarPanel, fightPanel, pausePanel, settingsPanel, endPanel, deployPanel, rotatePanel;
        GameObject settingsReturn;

        // Hangar
        int hangarIndex;
        Text hName, hRole, hDesc, hPrimary, hSpecial, hMobility, hCount;
        Image[] sArmour, sSpeed, sFire, sRange;

        // Fight
        Weapon special;
        Locomotion playerLoco;
        string lastSpecialText;
        RectTransform healthFill, fuelFill, specialFill;
        Image healthImg;
        Text specialLabel, playerName, healthText;
        RectTransform targetMarker;
        Image damageFlash;
        Text fpsText;
        float fpsTimer;
        int fpsFrames;
        readonly List<(Damageable d, RectTransform fill, Text label)> enemyBars = new List<(Damageable, RectTransform, Text)>();
        RectTransform enemyBarRoot;
        float lastHealth = -1f;

        // Touch controls
        GameObject touchControls;
        VirtualJoystick joystick;
        TouchButton fireButton, specialButton, boostButton;
        Image specialCharge, boostCharge;
        LookPad lookPad;

        // End
        Text endTitle, endSub;

        public bool ShowTouch => Application.isMobilePlatform || Application.isEditor || TouchAvailable();

        static bool TouchAvailable()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.touchSupported;
#elif ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Touchscreen.current != null;
#else
            return false;
#endif
        }

        public void Build(MatchManager m)
        {
            match = m;
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var flash = UiKit.Fill("DamageFlash", canvas.transform);
            damageFlash = UiKit.Image(flash, null, new Color(1f, 0.1f, 0f, 0f));

            BuildMenu();
            BuildHangar();
            BuildFight();
            BuildPause();
            BuildEnd();
            BuildDeploying();
            BuildSettings();
            BuildRotate();
            HideAll();
        }

        GameObject Screen(string name, bool safe = true)
        {
            var rt = UiKit.Fill(name, canvas.transform);
            if (safe) rt.gameObject.AddComponent<SafeArea>();
            return rt.gameObject;
        }

        void HideAll()
        {
            foreach (var p in new[] { menuPanel, hangarPanel, fightPanel, pausePanel, settingsPanel, endPanel, deployPanel })
                p.SetActive(false);
        }

        // =====================================================================
        // Main menu
        // =====================================================================

        void BuildMenu()
        {
            menuPanel = Screen("Menu");
            var dim = UiKit.Fill("Dim", menuPanel.transform);
            UiKit.Stretch(dim, -400f);
            UiKit.Image(dim, null, new Color(0.01f, 0.03f, 0.08f, 0.35f));

            var top = UiKit.Rect("Over", menuPanel.transform, new Vector2(0.5f, 0.86f), Vector2.zero, new Vector2(1200f, 80f));
            UiKit.Label(top, "GIANT ROBOT BATTLES", 44, UiKit.Cyan, TextAnchor.MiddleCenter, true);
            var title = UiKit.Rect("Title", menuPanel.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1600f, 220f));
            UiKit.Title(title, "WAR OF ROBOTS", 170, UiKit.Orange);
            var line = UiKit.Rect("Line", menuPanel.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(700f, 8f));
            UiKit.Image(line, UiKit.Rounded, UiKit.Cyan);

            UiKit.Button(menuPanel.transform, "PLAY", new Vector2(0.5f, 0.43f), Vector2.zero, new Vector2(560f, 150f), UiKit.ButtonStyle.Primary, () => match.OpenHangar(), 72);
            UiKit.Button(menuPanel.transform, "SETTINGS", new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(460f, 110f), UiKit.ButtonStyle.Secondary, () => OpenSettings(menuPanel), 44);

            var foot = UiKit.Rect("Footer", menuPanel.transform, new Vector2(0.5f, 0.06f), Vector2.zero, new Vector2(1600f, 50f));
            UiKit.Label(foot, "Pick a machine, enter the arena, destroy every enemy robot.", 30, UiKit.TextDim);
        }

        public void ShowMenu()
        {
            HideAll();
            menuPanel.SetActive(true);
        }

        // =====================================================================
        // Hangar: robot preview and choice
        // =====================================================================

        void BuildHangar()
        {
            hangarPanel = Screen("Hangar");

            // Drag anywhere on the left to spin the robot.
            var spin = UiKit.Rect("Spin", hangarPanel.transform, Vector2.zero, new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
            UiKit.Image(spin, null, new Color(0f, 0f, 0f, 0f), true);
            spin.gameObject.AddComponent<DragSpin>().onDrag = dx => match.SpinPreview(dx * 0.3f);

            UiKit.Button(hangarPanel.transform, "< BACK", new Vector2(0f, 1f), new Vector2(150f, -70f), new Vector2(240f, 90f), UiKit.ButtonStyle.Secondary, () => match.BackToMenu(), 36);
            var head = UiKit.Rect("Head", hangarPanel.transform, new Vector2(0.3f, 1f), new Vector2(0f, -70f), new Vector2(800f, 80f));
            UiKit.Title(head, "CHOOSE YOUR MACHINE", 52, Color.white);

            // Switch robots
            UiKit.IconButton(hangarPanel.transform, null, "<", new Vector2(0.06f, 0.5f), Vector2.zero, 130f, () => StepRobot(-1));
            UiKit.IconButton(hangarPanel.transform, null, ">", new Vector2(0.54f, 0.5f), Vector2.zero, 130f, () => StepRobot(1));
            var countRt = UiKit.Rect("Count", hangarPanel.transform, new Vector2(0.3f, 0.07f), Vector2.zero, new Vector2(400f, 60f));
            hCount = UiKit.Label(countRt, "", 34, UiKit.TextDim, TextAnchor.MiddleCenter, true);

            // Info card on the right
            var card = UiKit.Panel("Info", hangarPanel.transform, new Vector2(0.79f, 0.5f), new Vector2(-10f, 20f), new Vector2(700f, 900f));
            var stripe = UiKit.Rect("Stripe", card, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(640f, 10f));
            UiKit.Image(stripe, UiKit.Rounded, UiKit.Orange);

            hName = UiKit.Title(Place(card, 0.5f, 0.91f, 640f, 110f), "", 96, UiKit.Orange);
            hRole = UiKit.Label(Place(card, 0.5f, 0.825f, 640f, 50f), "", 34, UiKit.Cyan, TextAnchor.MiddleCenter, true);
            hDesc = UiKit.Label(Place(card, 0.5f, 0.73f, 600f, 110f), "", 30, Color.white);

            float y = 0.6f;
            sArmour = StatRow(card, "ARMOUR", y, UiKit.Green);
            sSpeed = StatRow(card, "SPEED", y - 0.055f, UiKit.Cyan);
            sFire = StatRow(card, "FIREPOWER", y - 0.11f, UiKit.Orange);
            sRange = StatRow(card, "RANGE", y - 0.165f, UiKit.Yellow);

            hPrimary = WeaponRow(card, "MAIN", 0.34f);
            hSpecial = WeaponRow(card, "SPECIAL", 0.28f);
            hMobility = WeaponRow(card, "MOVE", 0.22f);

            UiKit.Button(card, "DEPLOY", new Vector2(0.5f, 0.09f), Vector2.zero, new Vector2(560f, 120f), UiKit.ButtonStyle.Primary,
                () => match.StartMatch(RobotCatalog.All[hangarIndex].type), 60);
        }

        static RectTransform Place(Transform parent, float ax, float ay, float w, float h)
        {
            return UiKit.Rect("Slot", parent, new Vector2(ax, ay), Vector2.zero, new Vector2(w, h));
        }

        Image[] StatRow(Transform card, string label, float y, Color color)
        {
            UiKit.Label(Place(card, 0.24f, y, 220f, 40f), label, 26, UiKit.TextDim, TextAnchor.MiddleLeft, true);
            return UiKit.SegmentBar(card, new Vector2(0.67f, y), Vector2.zero, new Vector2(400f, 24f), color);
        }

        Text WeaponRow(Transform card, string label, float y)
        {
            UiKit.Label(Place(card, 0.2f, y, 180f, 40f), label, 26, UiKit.Orange, TextAnchor.MiddleLeft, true);
            return UiKit.Label(Place(card, 0.62f, y, 460f, 40f), "", 28, Color.white, TextAnchor.MiddleLeft);
        }

        void StepRobot(int dir)
        {
            int n = RobotCatalog.All.Count;
            hangarIndex = ((hangarIndex + dir) % n + n) % n;
            var info = RobotCatalog.All[hangarIndex];
            FillHangar(info);
            match.PreviewRobot(info);
        }

        void FillHangar(RobotInfo info)
        {
            hangarIndex = RobotCatalog.All.IndexOf(info);
            hName.text = info.name;
            hRole.text = info.role;
            hDesc.text = info.description;
            hPrimary.text = info.primary;
            hSpecial.text = info.special;
            hMobility.text = info.mobility;
            hCount.text = (hangarIndex + 1) + " / " + RobotCatalog.All.Count;
            UiKit.SetSegments(sArmour, info.armour, UiKit.Green);
            UiKit.SetSegments(sSpeed, info.speed, UiKit.Cyan);
            UiKit.SetSegments(sFire, info.firepower, UiKit.Orange);
            UiKit.SetSegments(sRange, info.range, UiKit.Yellow);
        }

        public void ShowHangar(RobotInfo info)
        {
            HideAll();
            FillHangar(info);
            hangarPanel.SetActive(true);
        }

        // =====================================================================
        // Fight HUD
        // =====================================================================

        void BuildFight()
        {
            fightPanel = Screen("Fight");

            // Status card, top left
            var status = UiKit.Panel("Status", fightPanel.transform, new Vector2(0f, 1f), new Vector2(300f, -105f), new Vector2(560f, 190f));
            playerName = UiKit.Title(Place(status, 0.3f, 0.82f, 300f, 50f), "", 40, UiKit.Orange);
            playerName.alignment = TextAnchor.MiddleLeft;
            healthText = UiKit.Label(Place(status, 0.78f, 0.82f, 200f, 50f), "", 30, Color.white, TextAnchor.MiddleRight, true);
            healthFill = UiKit.Bar(status, new Vector2(0.5f, 0.57f), Vector2.zero, new Vector2(510f, 40f), UiKit.Green, out healthImg);
            UiKit.Label(Place(status, 0.14f, 0.32f, 120f, 30f), "BOOST", 22, UiKit.TextDim, TextAnchor.MiddleLeft, true);
            fuelFill = UiKit.Bar(status, new Vector2(0.6f, 0.32f), Vector2.zero, new Vector2(370f, 22f), UiKit.Cyan, out _);
            specialLabel = UiKit.Label(Place(status, 0.14f, 0.12f, 120f, 30f), "", 22, UiKit.TextDim, TextAnchor.MiddleLeft, true);
            specialLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            specialFill = UiKit.Bar(status, new Vector2(0.6f, 0.12f), Vector2.zero, new Vector2(370f, 22f), UiKit.Orange, out _);

            // Enemy bars, top centre
            enemyBarRoot = UiKit.Rect("Enemies", fightPanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 80f));

            // Radar and pause, top right
            Minimap.Create(fightPanel.transform, new Vector2(1f, 1f), new Vector2(-175f, -175f), 300f);
            UiKit.IconButton(fightPanel.transform, UiKit.PauseIcon, "II", new Vector2(1f, 1f), new Vector2(-395f, -70f), 100f, () => Pause(true));

            // Crosshair
            var cross = UiKit.Rect("Crosshair", fightPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            UiKit.Image(cross, UiKit.Ring, new Color(1f, 1f, 1f, 0.55f));
            var dot = UiKit.Rect("Dot", cross, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            UiKit.Image(dot, UiKit.Circle, UiKit.Orange);

            // Lock-on marker
            targetMarker = UiKit.Rect("Target", fightPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            UiKit.Image(targetMarker, UiKit.Ring, new Color(1f, 0.3f, 0.2f, 0.85f));
            foreach (var p in new[] { new Vector2(0f, 75f), new Vector2(0f, -75f), new Vector2(75f, 0f), new Vector2(-75f, 0f) })
            {
                var tick = UiKit.Rect("Tick", targetMarker, new Vector2(0.5f, 0.5f), p, p.x == 0f ? new Vector2(6f, 26f) : new Vector2(26f, 6f));
                UiKit.Image(tick, UiKit.Rounded, UiKit.Red);
            }

            // FPS counter (optional)
            fpsText = UiKit.Label(Place(fightPanel.transform, 0.5f, 0.02f, 300f, 40f), "", 24, UiKit.TextDim);

            if (!ShowTouch)
            {
                var hint = UiKit.Rect("Hint", fightPanel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1800f, 40f));
                UiKit.Label(hint, "CLICK to aim with mouse   WASD move   LEFT CLICK fire   RIGHT CLICK special   SHIFT boost   ESC pause", 26, UiKit.TextDim);
            }

            BuildTouchControls();
        }

        void BuildTouchControls()
        {
            touchControls = UiKit.Fill("TouchControls", fightPanel.transform).gameObject;

            var pad = UiKit.Fill("LookPad", touchControls.transform);
            UiKit.Image(pad, null, new Color(0f, 0f, 0f, 0f), true);
            lookPad = pad.gameObject.AddComponent<LookPad>();

            // Joystick: dark base with a glowing ring and a chunky knob.
            var stickBg = UiKit.Rect("Joystick", touchControls.transform, Vector2.zero, new Vector2(260f, 250f), new Vector2(320f, 320f));
            var area = UiKit.Rect("StickArea", stickBg, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 560f));
            UiKit.Image(area, null, new Color(0f, 0f, 0f, 0f), true);
            UiKit.Image(stickBg, UiKit.Circle, new Color(0.04f, 0.08f, 0.16f, 0.5f), true);
            UiKit.Child(stickBg, "Ring", UiKit.Ring, new Color(0.3f, 0.85f, 1f, 0.7f));
            var handle = UiKit.Rect("Knob", stickBg, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            UiKit.Image(handle, UiKit.Circle, new Color(0.25f, 0.75f, 1f, 0.9f));
            UiKit.Child(handle, "Shine", UiKit.Circle, new Color(1f, 1f, 1f, 0.25f), 30f);
            UiKit.Child(handle, "Ring", UiKit.Ring, Color.white);
            joystick = stickBg.gameObject.AddComponent<VirtualJoystick>();
            joystick.background = stickBg;
            joystick.handle = handle;
            joystick.radius = 120f;

            fireButton = ActionButton("FIRE", new Vector2(-220f, 230f), 250f, UiKit.Red, out _);
            specialButton = ActionButton("SPECIAL", new Vector2(-460f, 150f), 170f, UiKit.Orange, out specialCharge);
            boostButton = ActionButton("BOOST", new Vector2(-190f, 470f), 170f, UiKit.Cyan, out boostCharge);

            var hint = UiKit.Rect("LookHint", touchControls.transform, new Vector2(0.55f, 0f), new Vector2(0f, 60f), new Vector2(400f, 40f));
            UiKit.Label(hint, "DRAG TO LOOK", 24, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, true);

            touchControls.SetActive(ShowTouch);
        }

        TouchButton ActionButton(string label, Vector2 pos, float size, Color color, out Image charge)
        {
            var rt = UiKit.Rect(label, touchControls.transform, new Vector2(1f, 0f), pos, new Vector2(size, size));
            UiKit.Image(rt, UiKit.Circle, new Color(0.04f, 0.07f, 0.14f, 0.6f), true);
            // Charge ring fills up as the weapon reloads.
            var chargeImg = UiKit.Child(rt, "Charge", UiKit.Ring, color);
            chargeImg.type = Image.Type.Filled;
            chargeImg.fillMethod = Image.FillMethod.Radial360;
            chargeImg.fillOrigin = (int)Image.Origin360.Top;
            charge = chargeImg;
            UiKit.Child(rt, "Inner", UiKit.Circle, new Color(color.r, color.g, color.b, 0.28f), size * 0.12f);
            UiKit.Child(rt, "Shine", UiKit.Sheen, new Color(1f, 1f, 1f, 0.6f), size * 0.12f);
            UiKit.Label(rt, label, Mathf.RoundToInt(size * 0.16f), Color.white, TextAnchor.MiddleCenter, true);
            rt.gameObject.AddComponent<PressBounce>();
            return rt.gameObject.AddComponent<TouchButton>();
        }

        public void ConnectControls(RobotInput input)
        {
            input.joystick = joystick;
            input.fireButton = fireButton;
            input.specialButton = specialButton;
            input.boostButton = boostButton;
            input.lookPad = lookPad;
        }

        public void ShowFight()
        {
            HideAll();
            fightPanel.SetActive(true);
            special = null;
            playerLoco = null;
            lastSpecialText = null;
            lastHealth = -1f;
            var p = match.Player;
            if (p != null)
            {
                playerLoco = p.GetComponent<Locomotion>();
                foreach (var w in p.GetComponents<Weapon>()) if (w.slot == Weapon.Slot.Special) special = w;
                playerName.text = p.displayName;
            }

            foreach (Transform c in enemyBarRoot) Destroy(c.gameObject);
            enemyBars.Clear();
            for (int i = 0; i < match.Enemies.Count; i++)
            {
                var d = match.Enemies[i];
                var holder = UiKit.Rect("Enemy", enemyBarRoot, new Vector2(0.5f, 0.5f),
                    new Vector2((i - (match.Enemies.Count - 1) * 0.5f) * 420f, 0f), new Vector2(390f, 80f));
                var label = UiKit.Label(Place(holder, 0.5f, 0.75f, 380f, 36f), d.displayName, 28, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter, true);
                var fill = UiKit.Bar(holder, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(380f, 22f), UiKit.Red, out _);
                enemyBars.Add((d, fill, label));
            }
        }

        void Update()
        {
            // Landscape only: ask the player to turn the phone if it's upright.
            bool portrait = UnityEngine.Screen.height > UnityEngine.Screen.width;
            if (rotatePanel.activeSelf != portrait) rotatePanel.SetActive(portrait);

            var fc = damageFlash.color;
            if (fc.a > 0f)
            {
                fc.a = Mathf.MoveTowards(fc.a, 0f, Time.unscaledDeltaTime * 0.8f);
                damageFlash.color = fc;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (fightPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Pause(true);
            else if (pausePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Pause(false);
#endif

            if (!fightPanel.activeSelf) return;
            var p = match.Player;
            if (p != null)
            {
                float hp = p.HealthFraction;
                SetFill(healthFill, hp);
                healthImg.color = Color.Lerp(UiKit.Red, UiKit.Green, Mathf.InverseLerp(0.2f, 0.7f, hp));
                string hpText = Mathf.CeilToInt(hp * 100f) + "%";
                if (healthText.text != hpText) healthText.text = hpText;
                if (playerLoco != null)
                {
                    SetFill(fuelFill, playerLoco.Fuel01);
                    if (boostCharge != null) boostCharge.fillAmount = playerLoco.Fuel01;
                }

                if (special != null)
                {
                    SetFill(specialFill, special.Ready01);
                    if (specialCharge != null) specialCharge.fillAmount = special.Ready01;
                    var rail = special as PlasmaRailgun;
                    string state = rail != null && rail.IsCharging ? "CHARGE" : (special.IsReady ? "READY" : "RELOAD");
                    if (!ReferenceEquals(state, lastSpecialText))
                    {
                        lastSpecialText = state;
                        specialLabel.text = state;
                        specialLabel.color = special.IsReady ? UiKit.Orange : UiKit.TextDim;
                    }
                }

                if (lastHealth >= 0f && hp < lastHealth)
                    damageFlash.color = new Color(1f, 0.1f, 0f, Mathf.Min(0.35f, damageFlash.color.a + (lastHealth - hp) * 4f));
                lastHealth = hp;
            }

            foreach (var e in enemyBars)
            {
                if (e.d == null) continue;
                SetFill(e.fill, e.d.HealthFraction);
                if (e.d.IsDead && e.label.color.a > 0.99f)
                {
                    e.label.text = e.d.displayName + " - DESTROYED";
                    e.label.color = new Color(0.6f, 0.6f, 0.6f, 0.98f);
                }
            }

            UpdateTargetMarker(p);
            UpdateFps();
        }

        void UpdateFps()
        {
            if (!GameSettings.ShowFps)
            {
                if (fpsText.text.Length > 0) fpsText.text = "";
                return;
            }
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fpsText.text = Mathf.RoundToInt(fpsFrames / fpsTimer) + " FPS";
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }

        void UpdateTargetMarker(Damageable p)
        {
            var turret = p != null && !p.IsDead ? p.GetComponent<Turret>() : null;
            var cam = Camera.main;
            if (turret == null || turret.Target == null || cam == null)
            {
                if (targetMarker.gameObject.activeSelf) targetMarker.gameObject.SetActive(false);
                return;
            }
            Vector3 sp = cam.WorldToScreenPoint(turret.Target.Center);
            if (sp.z < 0f)
            {
                targetMarker.gameObject.SetActive(false);
                return;
            }
            targetMarker.gameObject.SetActive(true);
            var parentRt = (RectTransform)targetMarker.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, sp, null, out Vector2 local))
                targetMarker.anchoredPosition = local;
            targetMarker.localRotation = Quaternion.Euler(0f, 0f, Time.time * 60f);
        }

        static void SetFill(RectTransform fill, float fraction)
        {
            if (fill == null) return;
            float f = Mathf.Round(Mathf.Clamp01(fraction) * 200f) / 200f;
            if (Mathf.Approximately(fill.anchorMax.x, f)) return;
            fill.anchorMax = new Vector2(f, 1f);
        }

        // =====================================================================
        // Pause
        // =====================================================================

        void BuildPause()
        {
            pausePanel = Screen("Pause", false);
            UiKit.Image(pausePanel.transform, null, new Color(0.01f, 0.02f, 0.06f, 0.7f), true);
            var card = UiKit.Panel("Card", pausePanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 760f));
            UiKit.Title(Place(card, 0.5f, 0.88f, 600f, 110f), "PAUSED", 90, Color.white);
            UiKit.Button(card, "RESUME", new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(480f, 110f), UiKit.ButtonStyle.Primary, () => Pause(false), 48);
            UiKit.Button(card, "SETTINGS", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 100f), UiKit.ButtonStyle.Secondary, () => OpenSettings(pausePanel), 40);
            UiKit.Button(card, "RESTART", new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(480f, 100f), UiKit.ButtonStyle.Secondary, () => { ShowLoading(); match.Restart(); }, 40);
            UiKit.Button(card, "QUIT TO MENU", new Vector2(0.5f, 0.15f), Vector2.zero, new Vector2(480f, 100f), UiKit.ButtonStyle.Danger, () => { ShowLoading(); match.QuitToMenu(); }, 40);
        }

        void Pause(bool on)
        {
            if (on && match.Current != MatchManager.State.Fighting) return;
            match.SetPaused(on);
            pausePanel.SetActive(on);
            fightPanel.SetActive(!on);
        }

        // =====================================================================
        // Settings
        // =====================================================================

        Button[] qualityButtons;
        Text shakeText, fpsToggleText;

        void BuildSettings()
        {
            settingsPanel = Screen("Settings", false);
            UiKit.Image(settingsPanel.transform, null, new Color(0.01f, 0.02f, 0.06f, 0.75f), true);
            var card = UiKit.Panel("Card", settingsPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 900f));
            UiKit.Title(Place(card, 0.5f, 0.91f, 900f, 110f), "SETTINGS", 84, Color.white);

            SettingLabel(card, "SOUND", 0.76f);
            UiSlider.Create(card, new Vector2(0.64f, 0.76f), Vector2.zero, 620f, 0f, 1f, GameSettings.Volume, v => GameSettings.Volume = v);

            SettingLabel(card, "LOOK SPEED", 0.62f);
            UiSlider.Create(card, new Vector2(0.64f, 0.62f), Vector2.zero, 620f, 0.3f, 2.5f, GameSettings.LookSensitivity, v => GameSettings.LookSensitivity = v);

            SettingLabel(card, "GRAPHICS", 0.48f);
            qualityButtons = new Button[3];
            string[] names = { "LOW", "MEDIUM", "HIGH" };
            for (int i = 0; i < 3; i++)
            {
                int q = i;
                qualityButtons[i] = UiKit.Button(card, names[i], new Vector2(0.64f, 0.48f), new Vector2((i - 1) * 210f, 0f), new Vector2(195f, 90f),
                    UiKit.ButtonStyle.Secondary, () => { GameSettings.GraphicsQuality = (GameSettings.Quality)q; RefreshSettings(); }, 32);
            }

            SettingLabel(card, "CAMERA SHAKE", 0.34f);
            var shakeBtn = UiKit.Button(card, "ON", new Vector2(0.64f, 0.34f), Vector2.zero, new Vector2(300f, 90f), UiKit.ButtonStyle.Secondary,
                () => { GameSettings.CameraShake = !GameSettings.CameraShake; RefreshSettings(); }, 34);
            shakeText = shakeBtn.GetComponentInChildren<Text>();

            SettingLabel(card, "SHOW FPS", 0.21f);
            var fpsBtn = UiKit.Button(card, "OFF", new Vector2(0.64f, 0.21f), Vector2.zero, new Vector2(300f, 90f), UiKit.ButtonStyle.Secondary,
                () => { GameSettings.ShowFps = !GameSettings.ShowFps; RefreshSettings(); }, 34);
            fpsToggleText = fpsBtn.GetComponentInChildren<Text>();

            UiKit.Button(card, "DONE", new Vector2(0.5f, 0.07f), Vector2.zero, new Vector2(420f, 100f), UiKit.ButtonStyle.Primary, CloseSettings, 46);
        }

        static void SettingLabel(Transform card, string text, float y)
        {
            UiKit.Label(Place(card, 0.2f, y, 320f, 60f), text, 34, UiKit.TextDim, TextAnchor.MiddleLeft, true);
        }

        void RefreshSettings()
        {
            for (int i = 0; i < qualityButtons.Length; i++)
            {
                bool on = (int)GameSettings.GraphicsQuality == i;
                var img = qualityButtons[i].GetComponent<Image>();
                img.color = on ? UiKit.Orange : new Color(0.1f, 0.2f, 0.36f, 0.95f);
                qualityButtons[i].GetComponentInChildren<Text>().color = on ? new Color(0.12f, 0.06f, 0f) : Color.white;
            }
            shakeText.text = GameSettings.CameraShake ? "ON" : "OFF";
            fpsToggleText.text = GameSettings.ShowFps ? "ON" : "OFF";
        }

        void OpenSettings(GameObject from)
        {
            settingsReturn = from;
            from.SetActive(false);
            RefreshSettings();
            settingsPanel.SetActive(true);
        }

        void CloseSettings()
        {
            settingsPanel.SetActive(false);
            if (settingsReturn != null) settingsReturn.SetActive(true);
        }

        // =====================================================================
        // End of match
        // =====================================================================

        void BuildEnd()
        {
            endPanel = Screen("End");
            var dim = UiKit.Fill("Dim", endPanel.transform);
            UiKit.Stretch(dim, -400f);
            UiKit.Image(dim, null, new Color(0f, 0f, 0.03f, 0.55f), true);
            endTitle = UiKit.Title(Place(endPanel.transform, 0.5f, 0.68f, 1600f, 240f), "", 170, UiKit.Orange);
            endSub = UiKit.Label(Place(endPanel.transform, 0.5f, 0.52f, 1400f, 80f), "", 40, Color.white);
            UiKit.Button(endPanel.transform, "FIGHT AGAIN", new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(560f, 130f), UiKit.ButtonStyle.Primary,
                () => { ShowLoading(); match.Restart(); }, 54);
            UiKit.Button(endPanel.transform, "CHANGE ROBOT", new Vector2(0.5f, 0.34f), new Vector2(-560f, 0f), new Vector2(420f, 110f), UiKit.ButtonStyle.Secondary,
                () => { ShowLoading(); match.ChangeRobot(); }, 38);
            UiKit.Button(endPanel.transform, "MENU", new Vector2(0.5f, 0.34f), new Vector2(520f, 0f), new Vector2(320f, 110f), UiKit.ButtonStyle.Secondary,
                () => { ShowLoading(); match.QuitToMenu(); }, 38);
        }

        public void ShowEnd(bool won)
        {
            HideAll();
            endPanel.SetActive(true);
            endTitle.text = won ? "VICTORY!" : "DESTROYED";
            endTitle.color = won ? UiKit.Yellow : UiKit.Red;
            endSub.text = won ? "Every enemy machine is scrap metal." : "Your machine has fallen. Rebuild and return.";
        }

        // =====================================================================
        // Loading / deploying, rotate-your-phone
        // =====================================================================

        Text deployText;

        void BuildDeploying()
        {
            deployPanel = Screen("Deploying", false);
            UiKit.Image(deployPanel.transform, null, new Color(0.01f, 0.02f, 0.06f, 0.75f), true);
            deployText = UiKit.Title(Place(deployPanel.transform, 0.5f, 0.5f, 1600f, 180f), "DEPLOYING...", 100, UiKit.Orange);
        }

        public void ShowDeploying()
        {
            HideAll();
            deployText.text = "DEPLOYING...";
            deployPanel.SetActive(true);
        }

        void ShowLoading()
        {
            HideAll();
            deployText.text = "LOADING...";
            deployPanel.SetActive(true);
        }

        void BuildRotate()
        {
            rotatePanel = Screen("Rotate", false);
            rotatePanel.transform.SetAsLastSibling();
            UiKit.Image(rotatePanel.transform, null, new Color(0.02f, 0.03f, 0.07f, 1f), true);
            UiKit.Title(Place(rotatePanel.transform, 0.5f, 0.55f, 1800f, 200f), "TURN YOUR PHONE", 110, UiKit.Orange);
            UiKit.Label(Place(rotatePanel.transform, 0.5f, 0.42f, 1800f, 80f), "War of Robots is played sideways", 50, Color.white);
            rotatePanel.SetActive(false);
        }
    }

    /// <summary>Reports horizontal drags (used to spin the robot in the hangar).</summary>
    public class DragSpin : MonoBehaviour, IDragHandler
    {
        public System.Action<float> onDrag;
        public void OnDrag(PointerEventData e) => onDrag?.Invoke(e.delta.x * 1080f / Mathf.Max(1f, UnityEngine.Screen.height));
    }
}
