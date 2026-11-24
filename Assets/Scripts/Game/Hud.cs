using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>
    /// All on-screen UI, built in code: robot select, health and boost bars,
    /// enemy status, target marker, touch controls and the end screen.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        MatchManager match;
        Canvas canvas;
        Font font;
        Sprite circle;

        GameObject selectPanel;
        GameObject fightPanel;
        GameObject endPanel;
        GameObject touchControls;
        Text endTitle;
        Text endSub;

        RectTransform healthFill;
        RectTransform fuelFill;
        RectTransform specialFill;
        Text specialLabel;
        Text playerName;
        RectTransform targetMarker;
        Image damageFlash;

        VirtualJoystick joystick;
        TouchButton fireButton;
        TouchButton specialButton;
        TouchButton boostButton;

        readonly List<(Damageable d, RectTransform fill, Text label)> enemyBars = new List<(Damageable, RectTransform, Text)>();
        float lastHealth = -1f;

        static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f, 0.82f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
        static readonly Color Cyan = new Color(0.3f, 0.85f, 1f);

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
            font = LoadFont();
            circle = GameAssets.Get() != null ? GameAssets.Get().CircleSprite() : null;

            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildSelect();
            BuildFight();
            BuildEnd();
        }

        // ---------- Robot select ----------

        void BuildSelect()
        {
            selectPanel = MakeRect("Select", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var bg = selectPanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            var title = Label(selectPanel.transform, "WAR OF ROBOTS", 110, Orange, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.85f), new Vector2(1400f, 160f));
            title.fontStyle = FontStyle.Bold;
            var sub = Label(selectPanel.transform, "CHOOSE YOUR MACHINE", 40, Color.white, TextAnchor.MiddleCenter);
            Place(sub.rectTransform, new Vector2(0.5f, 0.74f), new Vector2(1200f, 60f));

            Card(selectPanel.transform, new Vector2(0.3f, 0.4f), "TITAN",
                "15-metre walking giant\n\nHeavy arm cannons\nHoming missile swarm\nJump jets + shockwave landing",
                () => match.StartMatch(RobotType.Titan));
            Card(selectPanel.transform, new Vector2(0.7f, 0.4f), "RAPTOR",
                "Armoured six-wheel destroyer\n\nRapid pulse lasers\nPlasma railgun super weapon\nULTRA THRUST ramming booster",
                () => match.StartMatch(RobotType.Raptor));
        }

        void Card(Transform parent, Vector2 anchor, string name, string body, UnityEngine.Events.UnityAction onClick)
        {
            var rt = MakeRect(name, parent, anchor, anchor, Vector2.zero, new Vector2(620f, 560f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Panel;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.3f, 0.4f, 1f);
            colors.pressedColor = new Color(0.4f, 0.3f, 0.1f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var stripe = MakeRect("Stripe", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 12f));
            stripe.gameObject.AddComponent<Image>().color = Orange;

            var title = Label(rt, name, 90, Color.white, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(560f, 120f));
            var text = Label(rt, body, 34, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperCenter);
            Place(text.rectTransform, new Vector2(0.5f, 0.4f), new Vector2(560f, 300f));
            var go = Label(rt, "TAP TO DEPLOY", 30, Orange, TextAnchor.MiddleCenter);
            Place(go.rectTransform, new Vector2(0.5f, 0.08f), new Vector2(560f, 50f));
        }

        public void ShowSelect()
        {
            selectPanel.SetActive(true);
            fightPanel.SetActive(false);
            endPanel.SetActive(false);
        }

        // ---------- In-fight HUD ----------

        void BuildFight()
        {
            fightPanel = MakeRect("Fight", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

            // Damage flash
            var flash = MakeRect("DamageFlash", fightPanel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            damageFlash = flash.gameObject.AddComponent<Image>();
            damageFlash.color = new Color(1f, 0.1f, 0f, 0f);
            damageFlash.raycastTarget = false;

            // Player status, top left
            var status = MakeRect("Status", fightPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f, -110f), new Vector2(600f, 180f));
            var sbg = status.gameObject.AddComponent<Image>();
            sbg.color = Panel;
            sbg.raycastTarget = false;
            playerName = Label(status, "", 36, Orange, TextAnchor.UpperLeft);
            playerName.fontStyle = FontStyle.Bold;
            Place(playerName.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(560f, 50f));
            healthFill = Bar(status, new Vector2(0.5f, 0.55f), new Vector2(560f, 30f), new Color(0.25f, 0.95f, 0.4f), "ARMOUR");
            fuelFill = Bar(status, new Vector2(0.5f, 0.33f), new Vector2(560f, 18f), Cyan, "BOOST");
            specialFill = Bar(status, new Vector2(0.5f, 0.14f), new Vector2(560f, 18f), Orange, "");
            specialLabel = Label(status, "", 22, Color.white, TextAnchor.MiddleLeft);
            Place(specialLabel.rectTransform, new Vector2(0.5f, 0.14f), new Vector2(540f, 30f));

            // Target marker
            targetMarker = MakeRect("Target", fightPanel.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(140f, 140f));
            var ring = targetMarker.gameObject.AddComponent<Image>();
            ring.sprite = circle;
            ring.color = new Color(1f, 0.25f, 0.15f, 0.35f);
            ring.raycastTarget = false;
            var dot = MakeRect("Dot", targetMarker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            var dimg = dot.gameObject.AddComponent<Image>();
            dimg.sprite = circle;
            dimg.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            dimg.raycastTarget = false;

            // Controls hint for desktop
            if (!ShowTouch)
            {
                var hint = Label(fightPanel.transform, "W A S D  move    SPACE / LEFT CLICK  fire    E / RIGHT CLICK  special    SHIFT  boost", 26, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
                Place(hint.rectTransform, new Vector2(0.5f, 0.04f), new Vector2(1800f, 40f));
            }

            BuildTouchControls();
        }

        void BuildTouchControls()
        {
            touchControls = MakeRect("TouchControls", fightPanel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

            var stickBg = Circle("Joystick", touchControls.transform, new Vector2(0f, 0f), new Vector2(270f, 270f), 330f, new Color(1f, 1f, 1f, 0.16f));
            var handle = Circle("Handle", stickBg, new Vector2(0.5f, 0.5f), Vector2.zero, 150f, new Color(1f, 1f, 1f, 0.5f));
            handle.GetComponent<Image>().raycastTarget = false;
            joystick = stickBg.gameObject.AddComponent<VirtualJoystick>();
            joystick.background = stickBg;
            joystick.handle = handle;
            joystick.radius = 130f;

            fireButton = TouchBtn("FIRE", new Vector2(-230f, 230f), 260f, new Color(1f, 0.3f, 0.15f, 0.5f));
            specialButton = TouchBtn("SPECIAL", new Vector2(-470f, 170f), 170f, new Color(1f, 0.6f, 0.1f, 0.45f));
            boostButton = TouchBtn("BOOST", new Vector2(-200f, 470f), 170f, new Color(0.3f, 0.75f, 1f, 0.45f));

            touchControls.SetActive(ShowTouch);
        }

        TouchButton TouchBtn(string label, Vector2 pos, float size, Color color)
        {
            var rt = Circle(label, touchControls.transform, new Vector2(1f, 0f), pos, size, color);
            var t = Label(rt, label, Mathf.RoundToInt(size * 0.17f), Color.white, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            return rt.gameObject.AddComponent<TouchButton>();
        }

        public void ConnectControls(RobotInput input)
        {
            input.joystick = joystick;
            input.fireButton = fireButton;
            input.specialButton = specialButton;
            input.boostButton = boostButton;
        }

        public void ShowFight()
        {
            selectPanel.SetActive(false);
            endPanel.SetActive(false);
            fightPanel.SetActive(true);

            foreach (var e in enemyBars) if (e.fill != null) Destroy(e.fill.parent.parent.gameObject);
            enemyBars.Clear();
            for (int i = 0; i < match.Enemies.Count; i++)
            {
                var d = match.Enemies[i];
                var holder = MakeRect("Enemy", fightPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((i - (match.Enemies.Count - 1) * 0.5f) * 440f, -60f), new Vector2(400f, 70f));
                var img = holder.gameObject.AddComponent<Image>();
                img.color = Panel;
                img.raycastTarget = false;
                var label = Label(holder, d.displayName, 26, new Color(1f, 0.5f, 0.4f), TextAnchor.UpperLeft);
                Place(label.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(370f, 34f));
                var fill = Bar(holder, new Vector2(0.5f, 0.3f), new Vector2(370f, 16f), new Color(1f, 0.25f, 0.2f), "");
                enemyBars.Add((d, fill, label));
            }

            if (match.Player != null) playerName.text = match.Player.displayName;
        }

        void Update()
        {
            if (fightPanel == null || !fightPanel.activeSelf) return;
            var p = match.Player;
            if (p != null)
            {
                SetFill(healthFill, p.HealthFraction);
                var loco = p.GetComponent<Locomotion>();
                if (loco != null) SetFill(fuelFill, loco.Fuel01);

                Weapon special = null;
                foreach (var w in p.GetComponents<Weapon>()) if (w.slot == Weapon.Slot.Special) special = w;
                if (special != null)
                {
                    SetFill(specialFill, special.Ready01);
                    var rail = special as PlasmaRailgun;
                    string state = rail != null && rail.IsCharging ? "CHARGING..." : (special.IsReady ? "READY" : "RELOADING");
                    specialLabel.text = "  " + special.displayName.ToUpper() + "  -  " + state;
                }

                if (lastHealth >= 0f && p.HealthFraction < lastHealth)
                    damageFlash.color = new Color(1f, 0.1f, 0f, Mathf.Min(0.35f, damageFlash.color.a + (lastHealth - p.HealthFraction) * 4f));
                lastHealth = p.HealthFraction;
            }
            var fc = damageFlash.color;
            fc.a = Mathf.MoveTowards(fc.a, 0f, Time.unscaledDeltaTime * 0.8f);
            damageFlash.color = fc;

            foreach (var e in enemyBars)
            {
                if (e.d == null) continue;
                SetFill(e.fill, e.d.HealthFraction);
                if (e.d.IsDead) e.label.text = e.d.displayName + "  -  DESTROYED";
            }

            UpdateTargetMarker(p);
        }

        void UpdateTargetMarker(Damageable p)
        {
            var turret = p != null && !p.IsDead ? p.GetComponent<Turret>() : null;
            var cam = Camera.main;
            if (turret == null || turret.Target == null || cam == null)
            {
                targetMarker.gameObject.SetActive(false);
                return;
            }
            Vector3 sp = cam.WorldToScreenPoint(turret.Target.Center);
            if (sp.z < 0f)
            {
                targetMarker.gameObject.SetActive(false);
                return;
            }
            targetMarker.gameObject.SetActive(true);
            float scale = canvas.transform.localScale.x;
            targetMarker.anchoredPosition = new Vector2(sp.x, sp.y) / Mathf.Max(0.0001f, scale);
            targetMarker.localRotation = Quaternion.Euler(0f, 0f, Time.time * 60f);
        }

        // ---------- End screen ----------

        void BuildEnd()
        {
            endPanel = MakeRect("End", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            endPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            endTitle = Label(endPanel.transform, "", 150, Orange, TextAnchor.MiddleCenter);
            endTitle.fontStyle = FontStyle.Bold;
            Place(endTitle.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(1600f, 220f));
            endSub = Label(endPanel.transform, "", 40, Color.white, TextAnchor.MiddleCenter);
            Place(endSub.rectTransform, new Vector2(0.5f, 0.48f), new Vector2(1400f, 80f));

            var btnRt = MakeRect("Again", endPanel.transform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(520f, 130f));
            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = Orange;
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => match.Restart());
            var t = Label(btnRt, "FIGHT AGAIN", 52, Color.black, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            Stretch(t.rectTransform);
        }

        public void ShowEnd(bool won)
        {
            fightPanel.SetActive(false);
            endPanel.SetActive(true);
            endTitle.text = won ? "VICTORY" : "DESTROYED";
            endTitle.color = won ? Orange : new Color(1f, 0.25f, 0.2f);
            endSub.text = won ? "Every enemy machine is scrap metal." : "Your machine has fallen. Rebuild and return.";
        }

        // ---------- UI helpers ----------

        static Font LoadFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return f;
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        RectTransform Bar(Transform parent, Vector2 anchor, Vector2 size, Color color, string label)
        {
            var bg = MakeRect("Bar", parent, anchor, anchor, Vector2.zero, size);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.12f);
            bgImg.raycastTarget = false;
            var fill = MakeRect("Fill", bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var img = fill.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(bg, label, Mathf.RoundToInt(size.y * 0.7f), new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft);
                Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(10f, 0f);
            }
            return fill;
        }

        static void SetFill(RectTransform fill, float fraction)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            fill.offsetMax = Vector2.zero;
        }

        RectTransform Circle(string name, Transform parent, Vector2 anchor, Vector2 pos, float size, Color color)
        {
            var rt = MakeRect(name, parent, anchor, anchor, pos, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = circle;
            img.color = color;
            return rt;
        }
    }
}
