using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RobotArena
{
    /// <summary>Online screens: host or join a room, and the room (lobby) before the fight.</summary>
    public partial class Hud
    {
        GameObject onlinePanel, lobbyPanel;
        InputField codeField;
        Text onlineStatus;
        Text lobbyCode, lobbyRobot, lobbyMode, lobbyMap, lobbyWait, lobbyStatus;
        readonly List<(Text name, Text robot, Image bg)> lobbyRows = new List<(Text, Text, Image)>();
        readonly List<GameObject> hostOnly = new List<GameObject>();
        GameObject lobbyStart;
        NetSession subscribed;

        static readonly GameMode[] OnlineModes = { GameMode.TeamBattle, GameMode.Domination, GameMode.Survival };

        // =====================================================================
        // Host / join
        // =====================================================================

        void BuildOnline()
        {
            onlinePanel = SelectScreen("Online", "PLAY ONLINE", () => ShowMenu());

            var host = UiKit.Panel("Host", onlinePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-430f, -40f), new Vector2(760f, 600f));
            UiKit.Title(Place(host, 0.5f, 0.86f, 700f, 80f), "HOST A GAME", 50, UiKit.TextBright);
            UiKit.Label(Place(host, 0.5f, 0.58f, 640f, 180f),
                "Create a room and send the code to your friends. Up to 4 players fight on the same team against the robots.", 30, UiKit.TextDim);
            UiKit.Button(host, "CREATE ROOM", new Vector2(0.5f, 0.16f), Vector2.zero, new Vector2(520f, 120f), UiKit.ButtonStyle.Primary,
                () => match.Online().Host(), 46);

            var join = UiKit.Panel("Join", onlinePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(430f, -40f), new Vector2(760f, 600f));
            UiKit.Title(Place(join, 0.5f, 0.86f, 700f, 80f), "JOIN A FRIEND", 50, UiKit.TextBright);
            UiKit.Label(Place(join, 0.5f, 0.69f, 640f, 60f), "Type the room code your friend sees.", 30, UiKit.TextDim);
            codeField = CodeInput(join, new Vector2(0.5f, 0.45f), new Vector2(520f, 120f));
            UiKit.Button(join, "JOIN", new Vector2(0.5f, 0.16f), Vector2.zero, new Vector2(520f, 120f), UiKit.ButtonStyle.Primary,
                () => match.Online().Join(codeField.text), 46);

            onlineStatus = UiKit.Label(UiKit.Rect("Status", onlinePanel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1600f, 60f)),
                "", 32, UiKit.Orange, TextAnchor.MiddleCenter, true);
        }

        static InputField CodeInput(Transform parent, Vector2 anchor, Vector2 size)
        {
            var rt = UiKit.Rect("CodeInput", parent, anchor, Vector2.zero, size);
            var bg = UiKit.Image(rt, UiKit.Chamfer, new Color(0.03f, 0.035f, 0.045f, 0.95f), true);
            UiKit.Child(rt, "Edge", UiKit.ChamferOutline, new Color(UiKit.Orange.r, UiKit.Orange.g, UiKit.Orange.b, 0.7f));
            var placeholder = UiKit.Label(rt, "ROOM CODE", 40, new Color(1f, 1f, 1f, 0.22f), TextAnchor.MiddleCenter, true);
            var text = UiKit.Label(rt, "", 54, UiKit.TextBright, TextAnchor.MiddleCenter, true);
            text.supportRichText = false;
            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = 12;
            field.contentType = InputField.ContentType.Alphanumeric;
            field.onValueChanged.AddListener(v =>
            {
                string up = v.ToUpperInvariant();
                if (up != v) field.text = up;
            });
            return field;
        }

        void Subscribe()
        {
            var s = NetSession.I;
            if (s == null || s == subscribed) return;
            if (subscribed != null) subscribed.Changed -= OnNetChanged;
            subscribed = s;
            s.Changed += OnNetChanged;
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.Changed -= OnNetChanged;
        }

        void OnNetChanged()
        {
            if (this == null) return;
            Subscribe();
            if (NetSession.Online && onlinePanel.activeSelf)
            {
                ShowLobby();
                return;
            }
            if (onlinePanel.activeSelf) onlineStatus.text = NetSession.I != null ? NetSession.I.Status : "";
            if (lobbyPanel.activeSelf)
            {
                if (!NetSession.Online)
                {
                    ShowOnline();
                    return;
                }
                RefreshLobby();
            }
        }

        public void ShowOnline()
        {
            HideAll();
            match.Online();
            Subscribe();
            onlineStatus.text = NetSession.I != null ? NetSession.I.Status : "";
            onlinePanel.SetActive(true);
            GameAudio.PlayMusic(0.9f);
            GameAudio.PlayAmbience(false);
        }

        // =====================================================================
        // The room
        // =====================================================================

        void BuildLobby()
        {
            lobbyPanel = SelectScreen("Lobby", "ROOM", () => { if (NetSession.I != null) NetSession.I.Leave(); ShowOnline(); });

            var codeCard = UiKit.Panel("Code", lobbyPanel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(900f, 200f));
            UiKit.Label(Place(codeCard, 0.5f, 0.8f, 800f, 40f), UiKit.Spaced("ROOM CODE"), 24, UiKit.TextDim, TextAnchor.MiddleCenter, true);
            lobbyCode = UiKit.Title(Place(codeCard, 0.5f, 0.45f, 860f, 100f), "", 84, UiKit.Orange);
            UiKit.Label(Place(codeCard, 0.5f, 0.12f, 860f, 40f), "Friends tap PLAY ONLINE and type this code", 24, UiKit.TextDim);

            var players = UiKit.Panel("Players", lobbyPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-480f, -170f), new Vector2(760f, 540f));
            UiKit.Label(Place(players, 0.5f, 0.9f, 680f, 44f), UiKit.Spaced("YOUR TEAM"), 24, UiKit.Cyan, TextAnchor.MiddleCenter, true);
            for (int i = 0; i < NetSession.MaxPlayers; i++)
            {
                var row = UiKit.Rect("Row", players, new Vector2(0.5f, 1f), new Vector2(0f, -120f - i * 100f), new Vector2(680f, 84f));
                var bg = UiKit.Image(row, UiKit.Chamfer, UiKit.Steel);
                var n = UiKit.Label(Place(row, 0.32f, 0.5f, 380f, 60f), "", 32, UiKit.TextBright, TextAnchor.MiddleLeft, true);
                var r = UiKit.Label(Place(row, 0.78f, 0.5f, 260f, 60f), "", 28, UiKit.Orange, TextAnchor.MiddleRight, true);
                lobbyRows.Add((n, r, bg));
            }

            var setup = UiKit.Panel("Setup", lobbyPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(420f, -170f), new Vector2(980f, 540f));
            lobbyRobot = CycleRow(setup, 0.84f, "YOUR ROBOT", d => StepLobbyRobot(d), false);
            lobbyMode = CycleRow(setup, 0.64f, "MODE", d => StepLobbyMode(d), true);
            lobbyMap = CycleRow(setup, 0.44f, "MAP", d => StepLobbyMap(d), true);
            lobbyStart = UiKit.Button(setup, "START", new Vector2(0.5f, 0.14f), Vector2.zero, new Vector2(520f, 120f), UiKit.ButtonStyle.Primary,
                () => { if (NetSession.IsHost) NetSession.I.StartMatch(); }, 54).gameObject;
            lobbyWait = UiKit.Label(Place(setup, 0.5f, 0.14f, 800f, 60f), "Waiting for the host to start", 32, UiKit.TextDim, TextAnchor.MiddleCenter, true);
            lobbyStatus = UiKit.Label(UiKit.Rect("Status", lobbyPanel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(1600f, 50f)),
                "", 28, UiKit.Orange, TextAnchor.MiddleCenter, true);
        }

        Text CycleRow(Transform card, float y, string label, System.Action<int> step, bool hostOnlyArrows)
        {
            UiKit.Label(Place(card, 0.17f, y, 260f, 50f), label, 28, UiKit.TextDim, TextAnchor.MiddleLeft, true);
            var value = UiKit.Label(Place(card, 0.63f, y, 420f, 70f), "", 38, UiKit.TextBright, TextAnchor.MiddleCenter, true);
            var left = UiKit.IconButton(card, null, "<", new Vector2(0.36f, y), Vector2.zero, 84f, () => step(-1)).gameObject;
            var right = UiKit.IconButton(card, null, ">", new Vector2(0.9f, y), Vector2.zero, 84f, () => step(1)).gameObject;
            if (hostOnlyArrows)
            {
                hostOnly.Add(left);
                hostOnly.Add(right);
            }
            return value;
        }

        void StepLobbyRobot(int d)
        {
            var s = NetSession.I;
            if (s == null) return;
            int n = RobotCatalog.All.Count;
            int i = RobotCatalog.All.IndexOf(RobotCatalog.Get(s.LocalType));
            i = ((i + d) % n + n) % n;
            s.Pick(RobotCatalog.All[i].type);
            GameAudio.Play2D(Sfx.Servo, 0.5f);
            RefreshLobby();
        }

        void StepLobbyMode(int d)
        {
            var s = NetSession.I;
            if (s == null || !NetSession.IsHost) return;
            int i = System.Array.IndexOf(OnlineModes, s.Mode);
            i = ((i + d) % OnlineModes.Length + OnlineModes.Length) % OnlineModes.Length;
            s.SetModeMap(OnlineModes[i], s.Map);
        }

        void StepLobbyMap(int d)
        {
            var s = NetSession.I;
            if (s == null || !NetSession.IsHost) return;
            int n = MatchSetup.Maps.Count;
            int i = MatchSetup.Maps.FindIndex(m => m.map == s.Map);
            i = ((i + d) % n + n) % n;
            s.SetModeMap(s.Mode, MatchSetup.Maps[i].map);
        }

        public void ShowLobby()
        {
            HideAll();
            Subscribe();
            lobbyPanel.SetActive(true);
            RefreshLobby();
            GameAudio.PlayMusic(0.9f);
            GameAudio.PlayAmbience(false);
        }

        void RefreshLobby()
        {
            var s = NetSession.I;
            if (s == null) return;
            bool host = NetSession.IsHost;
            lobbyCode.text = string.IsNullOrEmpty(s.Code) ? "-" : s.Code;
            lobbyRobot.text = RobotCatalog.Get(s.LocalType).name;
            lobbyMode.text = MatchSetup.Modes.Find(m => m.mode == s.Mode)?.name ?? "";
            lobbyMap.text = MatchSetup.Maps.Find(m => m.map == s.Map)?.name ?? "";
            foreach (var go in hostOnly) go.SetActive(host);
            lobbyStart.SetActive(host);
            lobbyWait.gameObject.SetActive(!host);
            lobbyStatus.text = s.Status;
            for (int i = 0; i < lobbyRows.Count; i++)
            {
                var (n, r, bg) = lobbyRows[i];
                if (i < s.Players.Count)
                {
                    var p = s.Players[i];
                    bool me = p.id == NetSession.LocalId;
                    n.text = p.name + (me ? "  (YOU)" : "");
                    n.color = me ? UiKit.Cyan : UiKit.TextBright;
                    r.text = RobotCatalog.Get(p.type).name;
                    bg.color = UiKit.Steel;
                }
                else
                {
                    n.text = "Waiting for a friend";
                    n.color = UiKit.TextDim;
                    r.text = "";
                    bg.color = new Color(0.07f, 0.08f, 0.1f, 0.7f);
                }
            }
        }
    }
}
