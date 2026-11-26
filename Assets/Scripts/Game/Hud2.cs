using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>
    /// Mode, map and mission selection screens, and the team part of the fight HUD:
    /// score/status, team pips, objective, markers over points and robots, announcements.
    /// </summary>
    public partial class Hud
    {
        GameObject modePanel, mapPanel, missionPanel;
        Text modeMissionProgress;

        // =====================================================================
        // Mode select
        // =====================================================================

        static readonly Color[] ModeColors =
        {
            new Color(0.95f, 0.27f, 0.2f), UiKit.Cyan, new Color(1f, 0.64f, 0.12f), new Color(0.45f, 0.9f, 0.45f),
        };

        static readonly string[] ModeGlyph = { "3 V 3", "A  B  C", "WAVES", "6 OPS" };

        GameObject SelectScreen(string name, string title, UnityEngine.Events.UnityAction back)
        {
            var panel = Screen(name);
            var dim = UiKit.Fill("Dim", panel.transform);
            UiKit.Stretch(dim, -400f);
            UiKit.Image(dim, null, new Color(0.01f, 0.015f, 0.025f, 0.62f), true);
            UiKit.Button(panel.transform, "BACK", new Vector2(0f, 1f), new Vector2(150f, -70f), new Vector2(240f, 84f), UiKit.ButtonStyle.Secondary, back, 36);
            var head = UiKit.Rect("Head", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 80f));
            UiKit.Title(head, title, 54, UiKit.TextBright);
            var line = UiKit.Rect("Line", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(140f, 5f));
            UiKit.Image(line, null, UiKit.Orange);
            return panel;
        }

        RectTransform Card(Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UiKit.Panel("Card", parent, new Vector2(0.5f, 0.5f), pos, size);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            var c = btn.colors;
            c.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            c.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            c.fadeDuration = 0.06f;
            btn.colors = c;
            btn.onClick.AddListener(() => GameAudio.Play2D(Sfx.UiClick));
            btn.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<PressBounce>();
            return rt;
        }

        void BuildModeSelect()
        {
            modePanel = SelectScreen("Modes", "SELECT MODE", () => ShowMenu());
            var modes = MatchSetup.Modes;
            for (int i = 0; i < modes.Count; i++)
            {
                var info = modes[i];
                var color = ModeColors[i % ModeColors.Length];
                var card = Card(modePanel.transform, new Vector2((i - 1.5f) * 450f, -40f), new Vector2(420f, 640f), () => PickMode(info.mode));
                // Coloured header block with a big glyph.
                var top = UiKit.Rect("Top", card, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(370f, 170f));
                UiKit.Image(top, UiKit.Chamfer, new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.22f, 0.95f));
                UiKit.Child(top, "Edge", UiKit.ChamferOutline, new Color(color.r, color.g, color.b, 0.6f));
                UiKit.Title(top, ModeGlyph[i % ModeGlyph.Length], 58, color);
                UiKit.Title(Place(card, 0.5f, 0.6f, 400f, 70f), info.name, 44, UiKit.TextBright);
                UiKit.Label(Place(card, 0.5f, 0.52f, 400f, 40f), UiKit.Spaced(info.tagline), 22, color, TextAnchor.MiddleCenter, true);
                UiKit.Label(Place(card, 0.5f, 0.33f, 360f, 170f), info.description, 28, UiKit.TextDim);
                var sel = UiKit.Label(Place(card, 0.5f, 0.08f, 360f, 50f), "SELECT  >", 30, UiKit.Orange, TextAnchor.MiddleCenter, true);
                if (info.mode == GameMode.Mission) modeMissionProgress = UiKit.Label(Place(card, 0.5f, 0.17f, 360f, 40f), "", 24, UiKit.TextBright, TextAnchor.MiddleCenter, true);
                _ = sel;
            }
        }

        public void ShowModeSelect()
        {
            HideAll();
            if (modeMissionProgress != null)
                modeMissionProgress.text = Mathf.Min(MatchSetup.MissionsCompleted, MatchSetup.Missions.Count) + " / " + MatchSetup.Missions.Count + " COMPLETE";
            modePanel.SetActive(true);
            GameAudio.PlayMusic(0.9f);
            GameAudio.PlayAmbience(false);
        }

        void PickMode(GameMode mode)
        {
            MatchSetup.Mode = mode;
            if (mode == GameMode.Mission) ShowMissionSelect();
            else ShowMapSelect();
        }

        /// <summary>"TEAM BATTLE  /  DOCKYARD", shown in the hangar.</summary>
        static string SetupCaption()
        {
            if (MatchSetup.Mode == GameMode.Mission)
                return "MISSION " + (MatchSetup.MissionIndex + 1) + "  " + MatchSetup.Mission.name;
            string mode = MatchSetup.Modes.Find(m => m.mode == MatchSetup.Mode)?.name ?? "";
            string map = MatchSetup.Maps.Find(m => m.map == MatchSetup.Map)?.name ?? "";
            return mode + "  /  " + map;
        }

        // =====================================================================
        // Map select
        // =====================================================================

        static readonly Color[] MapTints =
        {
            new Color(0.35f, 0.42f, 0.5f), new Color(0.2f, 0.45f, 0.5f), new Color(0.72f, 0.5f, 0.28f),
        };

        Text mapModeCaption;

        void BuildMapSelect()
        {
            mapPanel = SelectScreen("Maps", "SELECT MAP", () => ShowModeSelect());
            mapModeCaption = UiKit.Label(UiKit.Rect("Mode", mapPanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(900f, 50f)),
                "", 28, UiKit.Orange, TextAnchor.MiddleCenter, true);
            var maps = MatchSetup.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var info = maps[i];
                var tint = MapTints[i % MapTints.Length];
                var card = Card(mapPanel.transform, new Vector2((i - 1f) * 590f, -60f), new Vector2(560f, 600f), () => PickMap(info.map));
                var view = UiKit.Rect("View", card, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(500f, 250f));
                UiKit.Image(view, UiKit.Chamfer, new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, 1f));
                var grad = UiKit.Child(view, "Grad", UiKit.VGrad, new Color(0f, 0f, 0f, 0.55f), 2f);
                grad.rectTransform.anchorMax = new Vector2(1f, 0.7f);
                UiKit.Child(view, "Edge", UiKit.ChamferOutline, new Color(tint.r, tint.g, tint.b, 0.8f));
                DrawMapSketch(view, info.map, tint);
                UiKit.Title(Place(card, 0.5f, 0.42f, 520f, 70f), info.name, 44, UiKit.TextBright);
                UiKit.Label(Place(card, 0.5f, 0.27f, 480f, 110f), info.description, 28, UiKit.TextDim);
                UiKit.Label(Place(card, 0.5f, 0.08f, 360f, 50f), "SELECT  >", 30, UiKit.Orange, TextAnchor.MiddleCenter, true);
            }
        }

        /// <summary>A few simple shapes that hint at the layout of each map.</summary>
        static void DrawMapSketch(RectTransform view, MapId map, Color tint)
        {
            var c = new Color(Mathf.Min(1f, tint.r * 1.6f), Mathf.Min(1f, tint.g * 1.6f), Mathf.Min(1f, tint.b * 1.6f), 0.75f);
            void Block(float x, float y, float w, float h)
            {
                var b = UiKit.Rect("B", view, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
                UiKit.Image(b, null, c);
            }
            switch (map)
            {
                case MapId.Foundry:
                    Block(0f, 0f, 56f, 56f);
                    for (int i = 0; i < 4; i++) Block((i % 2 == 0 ? -1 : 1) * 130f, (i < 2 ? -1 : 1) * 60f, 22f, 22f);
                    Block(-200f, 0f, 8f, 180f); Block(200f, 0f, 8f, 180f);
                    break;
                case MapId.Dockyard:
                    for (int i = -3; i <= 3; i++) { Block(i * 56f, 34f, 44f, 18f); Block(i * 56f, -34f, 44f, 18f); }
                    Block(-205f, 0f, 14f, 190f); Block(205f, 0f, 14f, 190f);
                    break;
                default:
                    Block(-120f, 40f, 90f, 60f); Block(110f, -30f, 110f, 70f); Block(0f, 0f, 12f, 12f);
                    Block(-40f, -70f, 50f, 30f); Block(60f, 75f, 60f, 26f);
                    break;
            }
        }

        void ShowMapSelect()
        {
            HideAll();
            string mode = MatchSetup.Modes.Find(m => m.mode == MatchSetup.Mode)?.name ?? "";
            mapModeCaption.text = UiKit.Spaced(mode);
            mapPanel.SetActive(true);
        }

        void PickMap(MapId map)
        {
            MatchSetup.Map = map;
            match.OpenHangar();
        }

        // =====================================================================
        // Mission select
        // =====================================================================

        readonly List<(Image bg, Text status)> missionRows = new List<(Image, Text)>();
        Text mName, mMap, mBrief, mGoal, mTeams;
        Button mStart;
        Text mStartText;
        int missionSel;

        void BuildMissionSelect()
        {
            missionPanel = SelectScreen("Missions", "MISSIONS", () => ShowModeSelect());
            var list = UiKit.Panel("List", missionPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-500f, -50f), new Vector2(760f, 820f));
            for (int i = 0; i < MatchSetup.Missions.Count; i++)
            {
                int idx = i;
                var row = UiKit.Button(list, "", new Vector2(0.5f, 1f), new Vector2(0f, -90f - i * 118f), new Vector2(680f, 104f), UiKit.ButtonStyle.Secondary, () => SelectMission(idx));
                UiKit.Label(Place(row.transform, 0.1f, 0.5f, 60f, 60f), (i + 1).ToString("00"), 34, UiKit.Orange, TextAnchor.MiddleCenter, true);
                UiKit.Label(Place(row.transform, 0.5f, 0.5f, 400f, 60f), MatchSetup.Missions[i].name, 34, UiKit.TextBright, TextAnchor.MiddleLeft, true);
                var st = UiKit.Label(Place(row.transform, 0.83f, 0.5f, 180f, 50f), "", 22, UiKit.TextDim, TextAnchor.MiddleRight, true);
                missionRows.Add((row.GetComponent<Image>(), st));
            }

            var info = UiKit.Panel("Brief", missionPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(420f, -50f), new Vector2(980f, 820f));
            mName = UiKit.Title(Place(info, 0.5f, 0.9f, 900f, 100f), "", 66, UiKit.TextBright);
            mMap = UiKit.Label(Place(info, 0.5f, 0.8f, 900f, 50f), "", 28, UiKit.Orange, TextAnchor.MiddleCenter, true);
            var stripe = UiKit.Rect("Stripe", info, new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(860f, 2f));
            UiKit.Image(stripe, null, new Color(1f, 1f, 1f, 0.15f));
            mBrief = UiKit.Label(Place(info, 0.5f, 0.6f, 840f, 170f), "", 34, Color.white);
            UiKit.Label(Place(info, 0.2f, 0.42f, 300f, 44f), "OBJECTIVE", 24, UiKit.TextDim, TextAnchor.MiddleLeft, true);
            mGoal = UiKit.Label(Place(info, 0.6f, 0.42f, 560f, 44f), "", 30, UiKit.TextBright, TextAnchor.MiddleLeft, true);
            UiKit.Label(Place(info, 0.2f, 0.34f, 300f, 44f), "FORCES", 24, UiKit.TextDim, TextAnchor.MiddleLeft, true);
            mTeams = UiKit.Label(Place(info, 0.6f, 0.34f, 560f, 44f), "", 28, UiKit.TextBright, TextAnchor.MiddleLeft);
            mStart = UiKit.Button(info, "CHOOSE ROBOT", new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(560f, 120f), UiKit.ButtonStyle.Primary, StartMission, 50);
            mStartText = mStart.GetComponentInChildren<Text>();
        }

        static string GoalText(MissionInfo m)
        {
            switch (m.goal)
            {
                case MissionGoal.DestroyDepots: return "Destroy 3 fuel tanks";
                case MissionGoal.DefendReactor: return "Defend the reactor for " + Mathf.RoundToInt(m.amount / 60f * 10f) / 10f + " min";
                case MissionGoal.HoldUplink: return "Hold the uplink for " + Mathf.RoundToInt(m.amount) + " s";
                case MissionGoal.KillCommander: return "Destroy the commander";
                case MissionGoal.SurviveWaves: return "Survive " + Mathf.RoundToInt(m.amount) + " waves";
                default: return "Destroy all enemies";
            }
        }

        void ShowMissionSelect()
        {
            HideAll();
            int first = Mathf.Clamp(MatchSetup.MissionsCompleted, 0, MatchSetup.Missions.Count - 1);
            SelectMission(MatchSetup.IsUnlocked(MatchSetup.MissionIndex) ? MatchSetup.MissionIndex : first);
            missionPanel.SetActive(true);
        }

        void SelectMission(int i)
        {
            missionSel = i;
            for (int k = 0; k < missionRows.Count; k++)
            {
                var (bg, st) = missionRows[k];
                bool done = MatchSetup.IsDone(k), open = MatchSetup.IsUnlocked(k);
                st.text = done ? "DONE" : open ? "NEW" : "LOCKED";
                st.color = done ? UiKit.Green : open ? UiKit.Orange : UiKit.TextDim;
                bg.color = k == i ? new Color(0.32f, 0.22f, 0.08f, 0.97f) : open ? UiKit.Steel : new Color(0.07f, 0.08f, 0.1f, 0.9f);
            }
            var m = MatchSetup.Missions[i];
            mName.text = m.name;
            string map = MatchSetup.Maps.Find(x => x.map == m.map)?.name ?? "";
            mMap.text = UiKit.Spaced("MISSION " + (i + 1) + "  /  " + map);
            mBrief.text = m.briefing;
            mGoal.text = GoalText(m);
            string enemies = m.goal == MissionGoal.SurviveWaves ? "waves of enemies" : (m.enemies.Length + (m.goal == MissionGoal.KillCommander ? 1 : 0)) + " enemy robots";
            mTeams.text = "You + " + m.allies + (m.allies == 1 ? " teammate" : " teammates") + "  vs  " + enemies;
            bool unlocked = MatchSetup.IsUnlocked(i);
            mStart.interactable = unlocked;
            mStartText.text = unlocked ? "CHOOSE ROBOT" : "LOCKED";
        }

        void StartMission()
        {
            if (!MatchSetup.IsUnlocked(missionSel)) return;
            MatchSetup.MissionIndex = missionSel;
            match.OpenHangar();
        }

        // =====================================================================
        // Fight HUD: teams, status, objective, markers, announcements
        // =====================================================================

        Text statusText, objectiveText, announceText, respawnText;
        float announceTime = -10f;
        RectTransform allyPipRoot, enemyPipRoot, markerRoot, tagRoot;
        readonly List<Image> allyPips = new List<Image>();
        readonly List<Image> enemyPips = new List<Image>();
        readonly List<(RectTransform rt, Image diamond, Text label, Text dist)> markerViews = new List<(RectTransform, Image, Text, Text)>();
        readonly List<(RectTransform rt, Text name, RectTransform fill, Image fillImg)> tagViews = new List<(RectTransform, Text, RectTransform, Image)>();
        const int MaxPips = 8;

        void BuildTeamHud(RectTransform statusCard)
        {
            // World markers and robot name tags sit under everything else.
            markerRoot = UiKit.Fill("Markers", fightPanel.transform);
            markerRoot.SetAsFirstSibling();
            tagRoot = UiKit.Fill("Tags", fightPanel.transform);
            tagRoot.SetAsFirstSibling();

            var top = UiKit.Panel("Teams", fightPanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(620f, 132f));
            statusText = UiKit.Label(Place(top, 0.5f, 0.68f, 580f, 56f), "", 38, UiKit.TextBright, TextAnchor.MiddleCenter, true);
            UiKit.Label(Place(top, 0.5f, 0.25f, 80f, 34f), "VS", 22, UiKit.TextDim, TextAnchor.MiddleCenter, true);
            allyPipRoot = UiKit.Rect("Allies", top, new Vector2(0.5f, 0.25f), new Vector2(-40f, 0f), new Vector2(10f, 24f));
            enemyPipRoot = UiKit.Rect("Enemies", top, new Vector2(0.5f, 0.25f), new Vector2(40f, 0f), new Vector2(10f, 24f));
            for (int i = 0; i < MaxPips; i++)
            {
                var a = UiKit.Rect("Pip", allyPipRoot, new Vector2(0.5f, 0.5f), new Vector2(-i * 34f - 12f, 0f), new Vector2(28f, 20f));
                allyPips.Add(UiKit.Image(a, UiKit.Chamfer, UiKit.Cyan));
                var e = UiKit.Rect("Pip", enemyPipRoot, new Vector2(0.5f, 0.5f), new Vector2(i * 34f + 12f, 0f), new Vector2(28f, 20f));
                enemyPips.Add(UiKit.Image(e, UiKit.Chamfer, UiKit.Red));
            }

            // Objective strip under the status card.
            var obj = UiKit.Panel("Objective", fightPanel.transform, new Vector2(0f, 1f), new Vector2(300f, -258f), new Vector2(560f, 92f));
            UiKit.Label(Place(obj, 0.5f, 0.74f, 500f, 30f), UiKit.Spaced("OBJECTIVE"), 18, UiKit.Orange, TextAnchor.MiddleLeft, true);
            objectiveText = UiKit.Label(Place(obj, 0.5f, 0.34f, 500f, 44f), "", 28, UiKit.TextBright, TextAnchor.MiddleLeft, true);
            objectiveText.horizontalOverflow = HorizontalWrapMode.Overflow;

            announceText = UiKit.Title(Place(fightPanel.transform, 0.5f, 0.74f, 1600f, 120f), "", 64, UiKit.Orange);
            respawnText = UiKit.Title(Place(fightPanel.transform, 0.5f, 0.6f, 1600f, 120f), "", 56, UiKit.TextBright);
        }

        void ResetTeamHud()
        {
            announceText.text = "";
            respawnText.text = "";
            foreach (var m in markerViews) m.rt.gameObject.SetActive(false);
            foreach (var t in tagViews) t.rt.gameObject.SetActive(false);
        }

        /// <summary>Big centred message that fades out ("WAVE 2", "POINT B").</summary>
        public void Announce(string text, Color color)
        {
            if (announceText == null) return;
            announceText.text = text;
            announceText.color = color;
            announceTime = Time.unscaledTime;
            GameAudio.Play2D(Sfx.Confirm, 0.6f);
        }

        void UpdateTeamHud()
        {
            var rules = match.Rules;
            if (rules != null)
            {
                if (statusText.text != rules.Status) statusText.text = rules.Status;
                if (objectiveText.text != rules.Objective) objectiveText.text = rules.Objective;
                string rs = rules.RespawnIn > 0f ? "RESPAWN IN " + Mathf.CeilToInt(rules.RespawnIn) : "";
                if (respawnText.text != rs) respawnText.text = rs;
            }

            // Announcement fade
            float age = Time.unscaledTime - announceTime;
            if (announceText.text.Length > 0)
            {
                var c = announceText.color;
                c.a = Mathf.Clamp01(2.8f - age);
                announceText.color = c;
                if (age > 2.8f) announceText.text = "";
            }

            // Team pips: your side (you + teammates), alive bright, fallen dim.
            int allyTotal = 0, allyAlive = 0;
            if (match.Player != null) { allyTotal++; if (!match.Player.IsDead) allyAlive++; }
            foreach (var a in match.Allies) if (a != null) { allyTotal++; if (!a.IsDead) allyAlive++; }
            int enemyAlive = 0;
            foreach (var e in match.Enemies) if (e != null && !e.IsDead) enemyAlive++;
            for (int i = 0; i < MaxPips; i++)
            {
                bool on = i < allyTotal;
                if (allyPips[i].gameObject.activeSelf != on) allyPips[i].gameObject.SetActive(on);
                if (on) allyPips[i].color = i < allyAlive ? UiKit.Cyan : new Color(0.3f, 0.35f, 0.4f, 0.6f);
                bool eon = i < enemyAlive;
                if (enemyPips[i].gameObject.activeSelf != eon) enemyPips[i].gameObject.SetActive(eon);
            }

            var cam = Camera.main;
            if (cam == null) return;
            UpdateMarkers(cam, rules);
            UpdateTags(cam);
        }

        bool ToLocal(RectTransform root, Camera cam, Vector3 world, bool clampToEdge, out Vector2 local, out float dist)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            dist = sp.z;
            bool behind = sp.z < 0f;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            if (behind)
            {
                sp.x = w - sp.x;
                sp.y = 0f;
            }
            bool off = behind || sp.x < 0f || sp.x > w || sp.y < 0f || sp.y > h;
            if (off && !clampToEdge)
            {
                local = Vector2.zero;
                return false;
            }
            float m = Mathf.Min(w, h) * 0.07f;
            sp.x = Mathf.Clamp(sp.x, m, w - m);
            sp.y = Mathf.Clamp(sp.y, m, h - m * 1.6f);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out local);
        }

        void UpdateMarkers(Camera cam, ModeController rules)
        {
            int n = rules != null ? rules.Markers.Count : 0;
            while (markerViews.Count < n)
            {
                var rt = UiKit.Rect("Marker", markerRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 110f));
                var d = UiKit.Rect("Diamond", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
                d.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var dImg = UiKit.Image(d, UiKit.Chamfer, Color.white);
                UiKit.Child(d, "Edge", UiKit.ChamferOutline, new Color(0f, 0f, 0f, 0.6f));
                var label = UiKit.Label(Place(rt, 0.5f, 0.86f, 240f, 34f), "", 26, Color.white, TextAnchor.MiddleCenter, true);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var dist = UiKit.Label(Place(rt, 0.5f, 0.12f, 200f, 30f), "", 22, UiKit.TextDim, TextAnchor.MiddleCenter, true);
                markerViews.Add((rt, dImg, label, dist));
            }
            Vector3 from = match.Player != null ? match.Player.transform.position : cam.transform.position;
            for (int i = 0; i < markerViews.Count; i++)
            {
                var v = markerViews[i];
                var mk = i < n ? rules.Markers[i] : null;
                bool show = mk != null && mk.Active && ToLocal(markerRoot, cam, mk.WorldPos, true, out var local, out _);
                if (v.rt.gameObject.activeSelf != show) v.rt.gameObject.SetActive(show);
                if (!show) continue;
                ToLocal(markerRoot, cam, mk.WorldPos, true, out local, out _);
                v.rt.anchoredPosition = local;
                v.diamond.color = mk.color;
                if (v.label.text != mk.label) v.label.text = mk.label;
                v.label.color = mk.color;
                string dt = Mathf.RoundToInt(Vector3.Distance(from, mk.WorldPos)) + " m";
                if (mk.target != null && mk.target.GetComponent<Locomotion>() == null)
                    dt = Mathf.CeilToInt(mk.target.HealthFraction * 100f) + "%  " + dt;
                if (v.dist.text != dt) v.dist.text = dt;
            }
        }

        void UpdateTags(Camera cam)
        {
            int used = 0;
            Vector3 from = cam.transform.position;
            foreach (var list in new[] { match.Allies, match.Enemies })
            {
                foreach (var d in list)
                {
                    if (d == null || d.IsDead) continue;
                    Vector3 head = d.Center + Vector3.up * (d.size * 3.2f + 2f);
                    if ((head - from).sqrMagnitude > 240f * 240f) continue;
                    if (!ToLocal(tagRoot, cam, head, false, out var local, out _)) continue;
                    if (used >= tagViews.Count)
                    {
                        var rt = UiKit.Rect("Tag", tagRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 50f));
                        var name = UiKit.Label(Place(rt, 0.5f, 0.7f, 260f, 30f), "", 22, Color.white, TextAnchor.MiddleCenter, true);
                        name.horizontalOverflow = HorizontalWrapMode.Overflow;
                        var fill = UiKit.Bar(rt, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(130f, 9f), UiKit.Red, out var img);
                        tagViews.Add((rt, name, fill, img));
                    }
                    var v = tagViews[used++];
                    if (!v.rt.gameObject.activeSelf) v.rt.gameObject.SetActive(true);
                    v.rt.anchoredPosition = local;
                    Color c = d.team == 0 ? UiKit.Cyan : UiKit.Red;
                    if (v.name.text != d.displayName) v.name.text = d.displayName;
                    v.name.color = d.team == 0 ? new Color(0.75f, 0.92f, 1f) : new Color(1f, 0.75f, 0.68f);
                    v.fillImg.color = c;
                    SetFill(v.fill, d.HealthFraction);
                }
            }
            for (int i = used; i < tagViews.Count; i++)
                if (tagViews[i].rt.gameObject.activeSelf) tagViews[i].rt.gameObject.SetActive(false);
        }
    }
}
