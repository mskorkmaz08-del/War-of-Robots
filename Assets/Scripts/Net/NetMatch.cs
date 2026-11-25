using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Keeps an online fight in step. The host runs the rules, the AI and all damage, and sends
    /// every robot's position, aim, controls and health 15 times a second. Each player drives
    /// their own robot locally (so it feels instant) and sends its state to the host. Robots
    /// that someone else controls are "proxies": they get the same controls (so legs walk and
    /// guns fire) and are gently pulled to the reported position.
    /// </summary>
    public class NetMatch : MonoBehaviour
    {
        public static NetMatch Instance { get; private set; }
        public const ulong AiOwner = ulong.MaxValue;

        const string MsgSpawn = "wor.spawn", MsgDespawn = "wor.despawn", MsgSnap = "wor.snap", MsgState = "wor.state",
            MsgHud = "wor.hud", MsgAnn = "wor.ann", MsgEnd = "wor.end";
        public static readonly string[] MessageNames = { MsgSpawn, MsgDespawn, MsgSnap, MsgState, MsgHud, MsgAnn, MsgEnd };

        public struct SpawnInfo
        {
            public ushort id;
            public ulong owner;
            public int type, team, livery;
            public string name;
            public Vector3 pos;
            public float yaw;
        }

        public struct RobotState
        {
            public ushort id;
            public Vector3 pos, vel, aim;
            public Quaternion rot;
            public float throttle, steer, health;
            public bool primary, special, boost;
            public bool hasMove, hasFace;
            public Vector2 move;   // world XZ
            public float faceYaw;  // degrees
        }

        MatchManager M;
        readonly Dictionary<ushort, Damageable> byId = new Dictionary<ushort, Damageable>();
        readonly Dictionary<Damageable, ushort> idOf = new Dictionary<Damageable, ushort>();
        readonly Dictionary<Damageable, ulong> ownerOf = new Dictionary<Damageable, ulong>();
        ushort nextId = 1;
        float snapTimer, hudTimer, stateTimer;
        static readonly List<SpawnInfo> pending = new List<SpawnInfo>();
        /// <summary>Client: the map is built and robots can be spawned.</summary>
        public bool Ready { get; set; }

        void Awake()
        {
            Instance = this;
            M = GetComponent<MatchManager>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public ulong OwnerOf(Damageable d) => d != null && ownerOf.TryGetValue(d, out var o) ? o : AiOwner;

        /// <summary>The current robot of a player (alive or the latest wreck).</summary>
        public Damageable RobotOf(ulong owner)
        {
            Damageable best = null;
            foreach (var kv in ownerOf)
                if (kv.Value == owner && kv.Key != null && (best == null || !kv.Key.IsDead)) best = kv.Key;
            return best;
        }

        // =====================================================================
        // Host
        // =====================================================================

        public void Register(Damageable d, ulong owner, RobotType type, int livery)
        {
            if (!NetSession.IsHost || d == null) return;
            ushort id = nextId++;
            byId[id] = d;
            idOf[d] = id;
            ownerOf[d] = owner;
            var info = new SpawnInfo
            {
                id = id, owner = owner, type = (int)type, team = d.team, livery = livery,
                name = d.displayName, pos = d.transform.position, yaw = d.transform.eulerAngles.y,
            };
            NetSession.Send(MsgSpawn, (ref FastBufferWriter w) => WriteSpawn(ref w, info));
        }

        public void Unregister(Damageable d)
        {
            if (d == null || !idOf.TryGetValue(d, out var id)) return;
            idOf.Remove(d);
            byId.Remove(id);
            ownerOf.Remove(d);
            if (NetSession.IsHost) NetSession.Send(MsgDespawn, (ref FastBufferWriter w) => w.WriteValueSafe(id));
        }

        public static void PlayerLeft(ulong clientId)
        {
            var nm = Instance;
            if (nm == null) return;
            var gone = new List<Damageable>();
            foreach (var kv in nm.ownerOf) if (kv.Value == clientId) gone.Add(kv.Key);
            foreach (var d in gone) nm.M.RemoveWreck(d);
            nm.M.Announce(NetSession.I != null ? NetSession.I.NameOf(clientId) + " LEFT" : "PLAYER LEFT", UiKit.TextDim);
        }

        public void Announce(string text, Color color)
        {
            if (!NetSession.IsHost) return;
            NetSession.Send(MsgAnn, (ref FastBufferWriter w) =>
            {
                w.WriteValueSafe(text);
                w.WriteValueSafe(color.r); w.WriteValueSafe(color.g); w.WriteValueSafe(color.b);
            });
        }

        public void End(bool won, string title, string sub)
        {
            if (!NetSession.IsHost) return;
            NetSession.Send(MsgEnd, (ref FastBufferWriter w) =>
            {
                w.WriteValueSafe(won);
                w.WriteValueSafe(title);
                w.WriteValueSafe(sub);
            });
        }

        void Update()
        {
            if (!NetSession.Online) return;
            if (NetSession.IsHost)
            {
                if (M.Current != MatchManager.State.Fighting && M.Current != MatchManager.State.Won && M.Current != MatchManager.State.Lost) return;
                snapTimer -= Time.unscaledDeltaTime;
                if (snapTimer <= 0f)
                {
                    snapTimer = 1f / 15f;
                    SendSnapshots();
                }
                hudTimer -= Time.unscaledDeltaTime;
                if (hudTimer <= 0f)
                {
                    hudTimer = 0.4f;
                    SendHud();
                }
            }
            else if (Ready && M.Player != null && !M.Player.IsDead)
            {
                stateTimer -= Time.unscaledDeltaTime;
                if (stateTimer <= 0f && idOf.TryGetValue(M.Player, out var id))
                {
                    stateTimer = 1f / 20f;
                    var s = Capture(M.Player, id);
                    NetSession.Send(MsgState, (ref FastBufferWriter w) => WriteState(ref w, s), false);
                }
            }
        }

        static RobotState Capture(Damageable d, ushort id)
        {
            var s = new RobotState { id = id, pos = d.transform.position, rot = d.transform.rotation, health = d.IsDead ? 0f : d.Health };
            if (d.Body != null) s.vel = Compat.GetVelocity(d.Body);
            var cmd = d.GetComponent<RobotCommands>();
            if (cmd != null)
            {
                s.throttle = cmd.throttle; s.steer = cmd.steer;
                s.primary = cmd.primary; s.special = cmd.special; s.boost = cmd.boost;
                s.hasMove = cmd.hasMove; s.move = new Vector2(cmd.move.x, cmd.move.z);
                s.hasFace = cmd.hasFace && cmd.face.sqrMagnitude > 0.0001f;
                s.faceYaw = s.hasFace ? Mathf.Atan2(cmd.face.x, cmd.face.z) * Mathf.Rad2Deg : 0f;
            }
            var turret = d.GetComponent<Turret>();
            s.aim = turret != null ? turret.AimPoint : d.transform.position + d.transform.forward * 100f;
            return s;
        }

        readonly List<RobotState> snapBuffer = new List<RobotState>();

        void SendSnapshots()
        {
            snapBuffer.Clear();
            foreach (var kv in byId) if (kv.Value != null) snapBuffer.Add(Capture(kv.Value, kv.Key));
            // Unreliable messages must fit one packet: send in chunks.
            for (int start = 0; start < snapBuffer.Count; start += 12)
            {
                int n = Mathf.Min(12, snapBuffer.Count - start);
                var chunk = snapBuffer.GetRange(start, n);
                NetSession.Send(MsgSnap, (ref FastBufferWriter w) =>
                {
                    w.WriteValueSafe(chunk.Count);
                    foreach (var s in chunk) WriteState(ref w, s);
                }, false);
            }
        }

        void SendHud()
        {
            var rules = M.Rules;
            if (rules == null || NetSession.I == null) return;
            var zones = new List<CaptureZone>(CaptureZone.All);
            foreach (var p in NetSession.I.RemotePlayers())
            {
                float respawn = rules.RespawnTimeFor(RobotOf(p.id));
                string status = rules.Status, objective = respawn > 0f ? "Respawning in " + Mathf.CeilToInt(respawn) : rules.Objective;
                NetSession.Send(MsgHud, (ref FastBufferWriter w) =>
                {
                    w.WriteValueSafe(status);
                    w.WriteValueSafe(objective);
                    w.WriteValueSafe(respawn);
                    w.WriteValueSafe(zones.Count);
                    foreach (var z in zones)
                    {
                        w.WriteValueSafe(z.Owner);
                        w.WriteValueSafe(z.Progress);
                    }
                }, true, p.id);
            }
        }

        // =====================================================================
        // Receiving
        // =====================================================================

        public static void Receive(string name, ulong sender, FastBufferReader r)
        {
            var nm = Instance;
            switch (name)
            {
                case MsgSpawn:
                {
                    var info = ReadSpawn(ref r);
                    if (nm != null && nm.Ready) nm.SpawnFromNet(info);
                    else pending.Add(info);
                    break;
                }
                case MsgDespawn:
                {
                    r.ReadValueSafe(out ushort id);
                    pending.RemoveAll(p => p.id == id);
                    if (nm != null && nm.byId.TryGetValue(id, out var d))
                    {
                        nm.byId.Remove(id);
                        nm.idOf.Remove(d);
                        nm.ownerOf.Remove(d);
                        nm.M.RemoveWreck(d);
                    }
                    break;
                }
                case MsgSnap:
                {
                    if (nm == null || !nm.Ready) return;
                    r.ReadValueSafe(out int count);
                    for (int i = 0; i < count; i++) nm.ApplyFromHost(ReadState(ref r));
                    break;
                }
                case MsgState:
                {
                    if (nm == null || !NetSession.IsHost) return;
                    var s = ReadState(ref r);
                    if (nm.byId.TryGetValue(s.id, out var d) && d != null && nm.OwnerOf(d) == sender)
                    {
                        var proxy = d.GetComponent<NetProxy>();
                        if (proxy != null) proxy.Apply(s, false);
                    }
                    break;
                }
                case MsgHud:
                {
                    r.ReadValueSafe(out string status);
                    r.ReadValueSafe(out string objective);
                    r.ReadValueSafe(out float respawn);
                    r.ReadValueSafe(out int zoneCount);
                    var rules = nm != null ? nm.M.Rules : null;
                    if (rules != null)
                    {
                        rules.Status = status;
                        rules.Objective = objective;
                        rules.RespawnIn = respawn;
                    }
                    for (int i = 0; i < zoneCount; i++)
                    {
                        r.ReadValueSafe(out int owner);
                        r.ReadValueSafe(out float progress);
                        if (i < CaptureZone.All.Count) CaptureZone.All[i].NetSet(owner, progress);
                    }
                    break;
                }
                case MsgAnn:
                {
                    r.ReadValueSafe(out string text);
                    r.ReadValueSafe(out float cr); r.ReadValueSafe(out float cg); r.ReadValueSafe(out float cb);
                    if (nm != null) nm.M.ShowAnnouncement(text, new Color(cr, cg, cb));
                    break;
                }
                case MsgEnd:
                {
                    r.ReadValueSafe(out bool won);
                    r.ReadValueSafe(out string title);
                    r.ReadValueSafe(out string sub);
                    if (nm != null) nm.M.EndMatch(won, title, sub);
                    break;
                }
            }
        }

        /// <summary>Client: the map is ready; build the robots that arrived early.</summary>
        public void FlushPending()
        {
            Ready = true;
            var list = new List<SpawnInfo>(pending);
            pending.Clear();
            foreach (var info in list) SpawnFromNet(info);
        }

        public static void ClearPending() => pending.Clear();

        void SpawnFromNet(SpawnInfo info)
        {
            if (byId.ContainsKey(info.id)) return;
            var d = M.SpawnFromNet(info, info.owner == NetSession.LocalId);
            if (d == null) return;
            byId[info.id] = d;
            idOf[d] = info.id;
            ownerOf[d] = info.owner;
        }

        void ApplyFromHost(RobotState s)
        {
            if (!byId.TryGetValue(s.id, out var d) || d == null) return;
            d.NetSetHealth(s.health);
            if (OwnerOf(d) == NetSession.LocalId) return;
            var proxy = d.GetComponent<NetProxy>();
            if (proxy != null) proxy.Apply(s, true);
        }

        // =====================================================================
        // Encoding
        // =====================================================================

        static void WriteSpawn(ref FastBufferWriter w, SpawnInfo s)
        {
            w.WriteValueSafe(s.id);
            w.WriteValueSafe(s.owner);
            w.WriteValueSafe(s.type);
            w.WriteValueSafe(s.team);
            w.WriteValueSafe(s.livery);
            w.WriteValueSafe(s.name ?? "");
            w.WriteValueSafe(s.pos);
            w.WriteValueSafe(s.yaw);
        }

        static SpawnInfo ReadSpawn(ref FastBufferReader r)
        {
            var s = new SpawnInfo();
            r.ReadValueSafe(out s.id);
            r.ReadValueSafe(out s.owner);
            r.ReadValueSafe(out s.type);
            r.ReadValueSafe(out s.team);
            r.ReadValueSafe(out s.livery);
            r.ReadValueSafe(out s.name);
            r.ReadValueSafe(out s.pos);
            r.ReadValueSafe(out s.yaw);
            return s;
        }

        static void WriteState(ref FastBufferWriter w, RobotState s)
        {
            w.WriteValueSafe(s.id);
            w.WriteValueSafe(s.pos);
            w.WriteValueSafe(s.rot);
            w.WriteValueSafe(s.vel);
            w.WriteValueSafe(s.aim);
            w.WriteValueSafe((sbyte)Mathf.RoundToInt(Mathf.Clamp(s.throttle, -1f, 1f) * 100f));
            w.WriteValueSafe((sbyte)Mathf.RoundToInt(Mathf.Clamp(s.steer, -1f, 1f) * 100f));
            byte flags = (byte)((s.primary ? 1 : 0) | (s.special ? 2 : 0) | (s.boost ? 4 : 0)
                | (s.hasMove ? 8 : 0) | (s.hasFace ? 16 : 0));
            w.WriteValueSafe(flags);
            w.WriteValueSafe(s.health);
            w.WriteValueSafe((sbyte)Mathf.RoundToInt(Mathf.Clamp(s.move.x, -1f, 1f) * 100f));
            w.WriteValueSafe((sbyte)Mathf.RoundToInt(Mathf.Clamp(s.move.y, -1f, 1f) * 100f));
            w.WriteValueSafe((short)Mathf.RoundToInt(Mathf.Repeat(s.faceYaw, 360f) * 10f));
        }

        static RobotState ReadState(ref FastBufferReader r)
        {
            var s = new RobotState();
            r.ReadValueSafe(out s.id);
            r.ReadValueSafe(out s.pos);
            r.ReadValueSafe(out s.rot);
            r.ReadValueSafe(out s.vel);
            r.ReadValueSafe(out s.aim);
            r.ReadValueSafe(out sbyte th);
            r.ReadValueSafe(out sbyte st);
            r.ReadValueSafe(out byte flags);
            r.ReadValueSafe(out s.health);
            r.ReadValueSafe(out sbyte mx);
            r.ReadValueSafe(out sbyte mz);
            r.ReadValueSafe(out short fy);
            s.move = new Vector2(mx / 100f, mz / 100f);
            s.faceYaw = fy / 10f;
            s.hasMove = (flags & 8) != 0;
            s.hasFace = (flags & 16) != 0;
            s.throttle = th / 100f;
            s.steer = st / 100f;
            s.primary = (flags & 1) != 0;
            s.special = (flags & 2) != 0;
            s.boost = (flags & 4) != 0;
            return s;
        }
    }

    /// <summary>
    /// A robot controlled by someone else: replays their controls and aim, and is pulled
    /// towards the position they report.
    /// </summary>
    public class NetProxy : MonoBehaviour, IDisableOnDeath
    {
        RobotCommands commands;
        Turret turret;
        Rigidbody body;
        NetMatch.RobotState last;
        bool has;
        float receivedAt;

        void Awake()
        {
            commands = GetComponent<RobotCommands>();
            if (commands == null) commands = gameObject.AddComponent<RobotCommands>();
            turret = GetComponent<Turret>();
            if (turret != null) turret.netControlled = true;
            body = GetComponent<Rigidbody>();
        }

        public void Apply(NetMatch.RobotState s, bool fromHost)
        {
            last = s;
            receivedAt = Time.time;
            if (!has && body != null)
            {
                body.position = s.pos;
                body.rotation = s.rot;
            }
            has = true;
        }

        void Update()
        {
            if (!has) return;
            commands.throttle = last.throttle;
            commands.steer = last.steer;
            commands.primary = last.primary;
            commands.special = last.special;
            commands.boost = last.boost;
            commands.hasMove = last.hasMove;
            commands.move = new Vector3(last.move.x, 0f, last.move.y);
            commands.hasFace = last.hasFace;
            commands.face = Quaternion.Euler(0f, last.faceYaw, 0f) * Vector3.forward;
            if (turret != null) turret.netAim = last.aim;
        }

        void FixedUpdate()
        {
            if (!has || body == null) return;
            float age = Mathf.Min(Time.time - receivedAt, 0.25f);
            Vector3 target = last.pos + last.vel * age;
            Vector3 err = target - body.position;
            if (err.sqrMagnitude > 12f * 12f)
            {
                body.position = target;
                body.rotation = last.rot;
                Compat.SetVelocity(body, last.vel);
                return;
            }
            body.position += err * 0.12f;
            Compat.SetVelocity(body, Vector3.Lerp(Compat.GetVelocity(body), last.vel, 0.15f));
            body.rotation = Quaternion.Slerp(body.rotation, last.rot, 0.15f);
        }

        void OnDisable()
        {
            if (commands != null) commands.Clear();
        }
    }

    /// <summary>A joining player's view of the rules: everything comes from the host.</summary>
    public class NetClientMode : ModeController
    {
        readonly List<CaptureZone> zones = new List<CaptureZone>();
        readonly List<WorldMarker> zoneMarkers = new List<WorldMarker>();

        public override string ModeName => MatchSetup.Modes.Find(m => m.mode == MatchSetup.Mode)?.name ?? "ONLINE";

        public override IEnumerator Setup()
        {
            Status = "";
            Objective = "Waiting for the host";
            if (MatchSetup.Mode == GameMode.Domination)
            {
                string[] letters = { "A", "B", "C" };
                for (int i = 0; i < L.zones.Length && i < 3; i++)
                {
                    var z = CaptureZone.Create(L.zones[i], L.zoneRadius, letters[i]);
                    z.netDriven = true;
                    zones.Add(z);
                    var mk = new WorldMarker { label = letters[i], position = z.transform.position, height = 16f };
                    zoneMarkers.Add(mk);
                    Markers.Add(mk);
                }
            }
            yield break;
        }

        public override void Tick(float dt)
        {
            for (int i = 0; i < zones.Count; i++) zoneMarkers[i].color = CaptureZone.TeamColor(zones[i].Owner);
        }
    }
}
