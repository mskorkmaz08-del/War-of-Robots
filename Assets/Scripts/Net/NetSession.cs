using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace RobotArena
{
    public class NetPlayer
    {
        public ulong id;
        public string name;
        public RobotType type;
    }

    /// <summary>
    /// Online play with friends: rooms with a short code through Unity Relay (secure WebSockets,
    /// so iPhone Safari works), the lobby (who's in, which robot, mode and map), and the message
    /// pipe used by NetMatch during a fight. The host runs the match; friends join as teammates.
    /// Lives across scene reloads.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public static NetSession I { get; private set; }

        public static bool Online => I != null && I.session != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public static bool IsHost => Online && NetworkManager.Singleton.IsServer;
        public static bool IsClient => Online && !NetworkManager.Singleton.IsServer;
        public static ulong LocalId => NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;

        ISession session;
        bool busy;
        bool handlersRegistered;

        public string Code => session != null ? session.Code : "";
        public bool Busy => busy;
        public readonly List<NetPlayer> Players = new List<NetPlayer>();
        public string Status = "";
        public GameMode Mode = GameMode.TeamBattle;
        public MapId Map = MapId.Foundry;
        public RobotType LocalType = RobotType.Titan;

        /// <summary>Lobby changed (players, picks, mode, status).</summary>
        public event Action Changed;
        /// <summary>The host said go: everyone deploys.</summary>
        public event Action StartRequested;
        /// <summary>The host went back to the lobby after a match.</summary>
        public event Action BackToLobby;
        /// <summary>We lost the connection or left (reason for the player).</summary>
        public event Action<string> Disconnected;

        // ---------------------------------------------------------------------
        // Setup
        // ---------------------------------------------------------------------

        public static NetSession Ensure()
        {
            if (I != null) return I;
            var go = new GameObject("Online");
            go.SetActive(false);
            DontDestroyOnLoad(go);
            I = go.AddComponent<NetSession>();
            var transport = go.AddComponent<UnityTransport>();
            transport.UseWebSockets = true;
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = false,
                TickRate = 30,
            };
            go.SetActive(true);
            return I;
        }

        void SetStatus(string s)
        {
            Status = s;
            Changed?.Invoke();
        }

        async Task<bool> InitServices()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Online service unavailable. Check your connection.");
                return false;
            }
        }

        public async void Host()
        {
            if (busy || session != null) return;
            busy = true;
            SetStatus("CREATING ROOM");
            if (await InitServices())
            {
                try
                {
                    var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }
                        .WithRelayNetwork(new RelayNetworkOptions(RelayProtocol.WSS));
                    session = await MultiplayerService.Instance.CreateSessionAsync(options);
                    Connected();
                    Players.Clear();
                    Players.Add(new NetPlayer { id = LocalId, name = "HOST", type = LocalType });
                    Status = "";
                    BroadcastLobby();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    session = null;
                    Status = "Could not create a room. Try again.";
                }
            }
            busy = false;
            Changed?.Invoke();
        }

        public async void Join(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (busy || session != null) return;
            if (code.Length < 4)
            {
                SetStatus("Type the room code first.");
                return;
            }
            busy = true;
            SetStatus("JOINING " + code);
            if (await InitServices())
            {
                try
                {
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                    Connected();
                    Status = "";
                    SendPick();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    session = null;
                    Status = "No room with that code, or it is full.";
                }
            }
            busy = false;
            Changed?.Invoke();
        }

        public async void Leave(string reason = null)
        {
            var s = session;
            session = null;
            Players.Clear();
            Status = reason ?? "";
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            handlersRegistered = false;
            if (s != null)
            {
                try { await s.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning("Leave failed: " + e.Message); }
            }
            Changed?.Invoke();
        }

        void Connected()
        {
            var nm = NetworkManager.Singleton;
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            RegisterHandlers();
        }

        void RegisterHandlers()
        {
            if (handlersRegistered) return;
            var cmm = NetworkManager.Singleton.CustomMessagingManager;
            if (cmm == null) return;
            handlersRegistered = true;
            cmm.RegisterNamedMessageHandler(MsgPick, OnPick);
            cmm.RegisterNamedMessageHandler(MsgLobby, OnLobby);
            cmm.RegisterNamedMessageHandler(MsgStart, OnStart);
            cmm.RegisterNamedMessageHandler(MsgBack, OnBack);
            foreach (var name in NetMatch.MessageNames)
            {
                string n = name;
                cmm.RegisterNamedMessageHandler(n, (sender, reader) => NetMatch.Receive(n, sender, reader));
            }
        }

        void OnClientConnected(ulong id)
        {
            RegisterHandlers();
            if (IsHost)
            {
                if (id != LocalId && Players.Find(p => p.id == id) == null)
                    Players.Add(new NetPlayer { id = id, name = "PLAYER " + (Players.Count + 1), type = RobotType.Titan });
                BroadcastLobby();
                Changed?.Invoke();
            }
            else if (id == LocalId) SendPick();
        }

        void OnClientDisconnected(ulong id)
        {
            if (IsHost && id != LocalId)
            {
                Players.RemoveAll(p => p.id == id);
                NetMatch.PlayerLeft(id);
                BroadcastLobby();
                Changed?.Invoke();
                return;
            }
            // We are a client and lost the host, or our own connection dropped.
            if (session != null)
            {
                Leave("Disconnected from the room.");
                Disconnected?.Invoke("Disconnected from the room.");
            }
        }

        // ---------------------------------------------------------------------
        // Messages
        // ---------------------------------------------------------------------

        const string MsgPick = "wor.pick", MsgLobby = "wor.lobby", MsgStart = "wor.start", MsgBack = "wor.back";

        public delegate void Writer(ref FastBufferWriter w);

        /// <summary>Host: to every other player. Client: to the host.</summary>
        public static void Send(string name, Writer write, bool reliable = true, ulong? only = null)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null) return;
            var w = new FastBufferWriter(512, Allocator.Temp, 60000);
            try
            {
                write(ref w);
                var delivery = reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;
                if (!nm.IsServer) nm.CustomMessagingManager.SendNamedMessage(name, NetworkManager.ServerClientId, w, delivery);
                else if (only.HasValue) nm.CustomMessagingManager.SendNamedMessage(name, only.Value, w, delivery);
                else
                {
                    var targets = new List<ulong>();
                    foreach (var id in nm.ConnectedClientsIds) if (id != nm.LocalClientId) targets.Add(id);
                    if (targets.Count > 0) nm.CustomMessagingManager.SendNamedMessage(name, targets, w, delivery);
                }
            }
            catch (Exception e) { Debug.LogWarning("Send " + name + " failed: " + e.Message); }
            finally { w.Dispose(); }
        }

        /// <summary>Pick a robot in the lobby.</summary>
        public void Pick(RobotType type)
        {
            LocalType = type;
            if (IsHost)
            {
                var me = Players.Find(p => p.id == LocalId);
                if (me != null) me.type = type;
                BroadcastLobby();
                Changed?.Invoke();
            }
            else SendPick();
        }

        void SendPick()
        {
            if (!Online) return;
            int t = (int)LocalType;
            Send(MsgPick, (ref FastBufferWriter w) => w.WriteValueSafe(t));
        }

        void OnPick(ulong sender, FastBufferReader r)
        {
            if (!IsHost) return;
            r.ReadValueSafe(out int t);
            var p = Players.Find(x => x.id == sender);
            if (p == null)
            {
                p = new NetPlayer { id = sender, name = "PLAYER " + (Players.Count + 1) };
                Players.Add(p);
            }
            p.type = (RobotType)Mathf.Clamp(t, 0, RobotCatalog.All.Count - 1);
            BroadcastLobby();
            Changed?.Invoke();
        }

        public void SetModeMap(GameMode mode, MapId map)
        {
            if (!IsHost) return;
            Mode = mode;
            Map = map;
            BroadcastLobby();
            Changed?.Invoke();
        }

        public void BroadcastLobby()
        {
            if (!IsHost) return;
            int mode = (int)Mode, map = (int)Map;
            var list = new List<NetPlayer>(Players);
            Send(MsgLobby, (ref FastBufferWriter w) =>
            {
                w.WriteValueSafe(mode);
                w.WriteValueSafe(map);
                w.WriteValueSafe(list.Count);
                foreach (var p in list)
                {
                    w.WriteValueSafe(p.id);
                    w.WriteValueSafe(p.name);
                    w.WriteValueSafe((int)p.type);
                }
            });
        }

        void OnLobby(ulong sender, FastBufferReader r)
        {
            if (IsHost) return;
            r.ReadValueSafe(out int mode);
            r.ReadValueSafe(out int map);
            r.ReadValueSafe(out int count);
            Mode = (GameMode)mode;
            Map = (MapId)map;
            Players.Clear();
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out ulong id);
                r.ReadValueSafe(out string name);
                r.ReadValueSafe(out int t);
                Players.Add(new NetPlayer { id = id, name = name, type = (RobotType)t });
            }
            Changed?.Invoke();
        }

        /// <summary>Host: everyone into the fight.</summary>
        public void StartMatch()
        {
            if (!IsHost) return;
            int mode = (int)Mode, map = (int)Map;
            Send(MsgStart, (ref FastBufferWriter w) =>
            {
                w.WriteValueSafe(mode);
                w.WriteValueSafe(map);
            });
            StartRequested?.Invoke();
        }

        void OnStart(ulong sender, FastBufferReader r)
        {
            if (IsHost) return;
            r.ReadValueSafe(out int mode);
            r.ReadValueSafe(out int map);
            Mode = (GameMode)mode;
            Map = (MapId)map;
            StartRequested?.Invoke();
        }

        /// <summary>Host: after a match, everyone returns to the lobby.</summary>
        public void ReturnToLobby()
        {
            if (!IsHost) return;
            Send(MsgBack, (ref FastBufferWriter w) => w.WriteValueSafe(0));
            BackToLobby?.Invoke();
        }

        void OnBack(ulong sender, FastBufferReader r)
        {
            if (IsHost) return;
            BackToLobby?.Invoke();
        }

        public string NameOf(ulong id)
        {
            var p = Players.Find(x => x.id == id);
            return p != null ? p.name : "PLAYER";
        }

        /// <summary>Everyone except the host's own player.</summary>
        public List<NetPlayer> RemotePlayers()
        {
            var list = new List<NetPlayer>();
            foreach (var p in Players) if (p.id != LocalId) list.Add(p);
            return list;
        }
    }
}
