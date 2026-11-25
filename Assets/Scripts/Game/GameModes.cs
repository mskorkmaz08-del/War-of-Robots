using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The rules of one match: who spawns where, what the objective is, and when it's won or lost.
    /// Ticked by the MatchManager while the fight is on.
    /// </summary>
    public abstract class ModeController
    {
        protected MatchManager M;
        protected MapLayout L;

        /// <summary>Short line at the top centre of the HUD (score, wave, timer).</summary>
        public string Status = "";
        /// <summary>Objective text in the panel under the status card.</summary>
        public string Objective = "";
        public readonly List<WorldMarker> Markers = new List<WorldMarker>();
        /// <summary>Seconds until the player's robot comes back, or below zero when alive.</summary>
        public float RespawnIn = -1f;
        public bool Over { get; private set; }

        public virtual bool IsMission => false;
        public virtual string ModeName => "BATTLE";

        public void Init(MatchManager m, MapLayout l)
        {
            M = m;
            L = l;
        }

        /// <summary>Spawns the robots and buildings, yielding between heavy steps.</summary>
        public abstract IEnumerator Setup();
        public virtual void Tick(float dt) { }
        public virtual void RobotDied(Damageable d) { }
        /// <summary>Seconds until this robot's owner is back (respawn modes), or below zero.</summary>
        public virtual float RespawnTimeFor(Damageable d) => -1f;

        protected void Win(string title, string sub)
        {
            if (Over) return;
            Over = true;
            OnWin();
            M.EndMatch(true, title, sub);
        }

        protected void Lose(string title, string sub)
        {
            if (Over) return;
            Over = true;
            M.EndMatch(false, title, sub);
        }

        protected virtual void OnWin() { }

        // ---- Helpers ----

        protected static readonly string[] AllyNames = { "ATLAS", "RANGER", "BULWARK", "AEGIS", "NOMAD", "SENTRY" };
        protected static readonly string[] EnemyNames = { "GOLIATH", "VIPER", "HAVOC", "MAULER", "WRAITH", "KRAKEN", "BRUTUS", "STINGER", "OGRE", "VENOM", "RAZOR", "DREAD" };
        int enemyNameIndex = Random.Range(0, 100);
        int allyNameIndex;

        protected string NextEnemyName() => EnemyNames[enemyNameIndex++ % EnemyNames.Length];

        protected static RobotType RandomType(params RobotType[] exclude)
        {
            var pool = new List<RobotInfo>();
            foreach (var r in RobotCatalog.All) if (System.Array.IndexOf(exclude, r.type) < 0) pool.Add(r);
            if (pool.Count == 0) pool.AddRange(RobotCatalog.All);
            return pool[Random.Range(0, pool.Count)].type;
        }

        /// <summary>n robot types, all different where possible.</summary>
        protected static RobotType[] RandomTeam(int n)
        {
            var pool = new List<RobotInfo>(RobotCatalog.All);
            var result = new RobotType[n];
            for (int i = 0; i < n; i++)
            {
                if (pool.Count == 0) pool.AddRange(RobotCatalog.All);
                var pick = pool[Random.Range(0, pool.Count)];
                pool.Remove(pick);
                result[i] = pick.type;
            }
            return result;
        }

        protected IEnumerator SpawnPlayerAndAllies(int allies, bool follow, Vector3? playerPos = null)
        {
            var player = M.SpawnPlayer(playerPos ?? L.allySpawns[0]);
            yield return null;
            int slot = 1;
            if (NetSession.IsHost)
            {
                foreach (var p in NetSession.I.RemotePlayers())
                {
                    M.SpawnRemotePlayer(p.id, p.type, L.allySpawns[slot++ % L.allySpawns.Length], p.name);
                    yield return null;
                }
                allies = Mathf.Max(0, allies - (slot - 1));
            }
            var types = RandomTeam(allies + 1);
            for (int i = 0; i < allies; i++)
            {
                var type = types[i] == M.PlayerType ? types[allies] : types[i];
                var a = M.SpawnRobot(type, 0, L.allySpawns[(i + slot) % L.allySpawns.Length], AllyNames[allyNameIndex++ % AllyNames.Length]);
                if (follow) Brain(a).follow = player.transform;
                yield return null;
            }
        }

        protected static AIBrain Brain(Damageable d) => d != null ? d.GetComponent<AIBrain>() : null;

        protected static int Alive(List<Damageable> list)
        {
            int n = 0;
            foreach (var d in list) if (d != null && !d.IsDead) n++;
            return n;
        }

        /// <summary>An enemy spawn point with nobody standing on it.</summary>
        protected Vector3 FreeSpawn(Vector3[] points)
        {
            int start = Random.Range(0, points.Length);
            for (int k = 0; k < points.Length; k++)
            {
                var p = points[(start + k) % points.Length];
                bool clear = true;
                foreach (var d in Damageable.All)
                    if (d != null && !d.IsDead && (d.transform.position - p).sqrMagnitude < 18f * 18f) { clear = false; break; }
                if (clear) return p;
            }
            return points[start] + new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-6f, 6f));
        }

        protected static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>A human player's robot died: the match is lost once every human is down.</summary>
        protected bool HumanDown(Damageable d)
        {
            if (!M.IsHuman(d)) return false;
            if (M.AllHumansDown()) PlayerLost();
            return true;
        }

        protected void PlayerLost()
        {
            Lose(IsMission ? "MISSION FAILED" : "DESTROYED", "Your machine has fallen. Rebuild and return.");
        }
    }

    // =========================================================================
    // Team battle and the objective missions
    // =========================================================================

    /// <summary>Team battle (3 vs 3) and the missions with one fixed objective.</summary>
    public class ObjectiveMode : ModeController
    {
        readonly MissionGoal goal;
        readonly int allies;
        readonly RobotType[] enemyTypes;
        readonly float amount;
        readonly int missionIndex;

        readonly List<Damageable> depots = new List<Damageable>();
        Damageable reactor;
        Damageable commander;
        CaptureZone uplink;
        WorldMarker uplinkMarker;
        float timer;
        float held;
        int reinforceIndex;
        int maxAlive;
        readonly List<float> reinforceQueue = new List<float>();

        public override bool IsMission => missionIndex >= 0;
        public override string ModeName => IsMission ? MatchSetup.Missions[missionIndex].name : "TEAM BATTLE";

        public ObjectiveMode(MissionGoal goal, int allies, RobotType[] enemies, float amount, int missionIndex)
        {
            this.goal = goal;
            this.allies = allies;
            enemyTypes = enemies;
            this.amount = amount;
            this.missionIndex = missionIndex;
        }

        public static ObjectiveMode TeamBattle()
        {
            // Four humans online: one more enemy to keep it a fight.
            int humans = NetSession.IsHost ? NetSession.I.Players.Count : 1;
            return new ObjectiveMode(MissionGoal.EliminateAll, 2, RandomTeam(3 + Mathf.Max(0, humans - 3)), 0f, -1);
        }

        public override IEnumerator Setup()
        {
            bool defend = goal == MissionGoal.DefendReactor || goal == MissionGoal.HoldUplink;
            yield return SpawnPlayerAndAllies(allies, !defend);

            switch (goal)
            {
                case MissionGoal.DestroyDepots:
                    for (int i = 0; i < L.depots.Length; i++)
                    {
                        var dep = Structures.FuelDepot(L.depots[i], i * 35f - 35f);
                        depots.Add(dep);
                        Markers.Add(new WorldMarker { label = "DEPOT", color = UiKit.Red, target = dep, height = 10f });
                        yield return null;
                    }
                    break;
                case MissionGoal.DefendReactor:
                    reactor = Structures.Reactor(L.reactor);
                    reactor.Died += _ => Lose("MISSION FAILED", "The reactor was destroyed.");
                    Markers.Add(new WorldMarker { label = "DEFEND", color = UiKit.Cyan, target = reactor, height = 14f });
                    timer = amount;
                    break;
                case MissionGoal.HoldUplink:
                    uplink = CaptureZone.Create(L.zones[1], 22f, "UPLINK");
                    uplink.captureTime = 6f;
                    uplinkMarker = new WorldMarker { label = "UPLINK", position = uplink.transform.position, height = 18f };
                    Markers.Add(uplinkMarker);
                    break;
            }

            // Defending teammates guard the objective instead of following you.
            if (defend)
            {
                Vector3 guard = goal == MissionGoal.DefendReactor ? L.reactor : L.zones[1];
                foreach (var a in M.Allies)
                {
                    var b = Brain(a);
                    b.goal = guard + new Vector3(Random.Range(-18f, 18f), 0f, Random.Range(-14f, 10f));
                    b.engageRange = 110f;
                }
            }

            if (goal == MissionGoal.KillCommander)
            {
                commander = M.SpawnRobot(RobotType.Juggernaut, 1, L.enemySpawns[0], "WARLORD", true);
                commander.maxHealth *= 2f;
                commander.Heal(commander.maxHealth);
                Brain(commander).goal = L.zones[1];
                Brain(commander).engageRange = 140f;
                Markers.Add(new WorldMarker { label = "WARLORD", color = UiKit.Red, target = commander, height = 9f });
                yield return null;
            }

            // Timed missions keep the pressure on with reinforcements instead of one big wave.
            bool timed = goal == MissionGoal.DefendReactor || goal == MissionGoal.HoldUplink;
            maxAlive = timed ? Mathf.Min(enemyTypes.Length, 3) : enemyTypes.Length;
            for (int i = 0; i < enemyTypes.Length; i++)
            {
                if (i < maxAlive)
                {
                    SpawnEnemy(enemyTypes[i], i);
                    yield return null;
                }
                else reinforceQueue.Add(10f + (i - maxAlive) * 8f);
            }
            reinforceIndex = maxAlive;
            UpdateText();
        }

        void SpawnEnemy(RobotType type, int i)
        {
            Vector3 pos = L.enemySpawns[i % L.enemySpawns.Length];
            if (goal == MissionGoal.DestroyDepots && depots.Count > 0)
            {
                // Guards stand in front of their depot.
                var dep = depots[i % depots.Count].transform.position;
                pos = dep + (Vector3.zero - dep).normalized * 26f;
            }
            else if (i >= L.enemySpawns.Length || M.Enemies.Count > 0) pos = FreeSpawn(L.enemySpawns);

            var e = M.SpawnRobot(type, 1, pos, NextEnemyName());
            var b = Brain(e);
            switch (goal)
            {
                case MissionGoal.DestroyDepots:
                    b.goal = pos;
                    b.engageRange = 120f;
                    break;
                case MissionGoal.DefendReactor:
                    b.goal = L.reactor;
                    b.engageRange = 75f;
                    if (i % 2 == 0) b.priorityTarget = reactor;
                    break;
                case MissionGoal.HoldUplink:
                    b.goal = L.zones[1] + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                    b.engageRange = 90f;
                    break;
                case MissionGoal.KillCommander:
                    if (commander != null)
                    {
                        b.follow = commander.transform;
                        b.engageRange = 130f;
                    }
                    break;
            }
        }

        public override void Tick(float dt)
        {
            if (Over) return;
            // Reinforcements
            for (int i = reinforceQueue.Count - 1; i >= 0; i--)
            {
                reinforceQueue[i] -= dt;
                if (reinforceQueue[i] <= 0f && Alive(M.Enemies) < maxAlive)
                {
                    reinforceQueue.RemoveAt(i);
                    SpawnEnemy(enemyTypes[reinforceIndex++ % enemyTypes.Length], 99);
                }
            }

            if (goal == MissionGoal.DefendReactor)
            {
                timer -= dt;
                if (timer <= 0f) Win("MISSION COMPLETE", "Reinforcements have arrived. The reactor is safe.");
            }
            else if (goal == MissionGoal.HoldUplink && uplink != null)
            {
                if (uplink.Owner == 0 && uplink.Count1 == 0) held += dt;
                uplinkMarker.color = CaptureZone.TeamColor(uplink.Owner);
                if (held >= amount) Win("MISSION COMPLETE", "Uplink secured. The data is through.");
            }
            UpdateText();
        }

        void UpdateText()
        {
            int alive = Alive(M.Enemies);
            switch (goal)
            {
                case MissionGoal.DestroyDepots:
                    Objective = "Destroy the fuel depots  " + (depots.Count - Alive(depots)) + " / " + depots.Count;
                    Status = "ENEMIES " + alive;
                    break;
                case MissionGoal.DefendReactor:
                    Objective = "Defend the reactor until help arrives";
                    Status = "REACTOR " + Mathf.CeilToInt(reactor != null ? reactor.HealthFraction * 100f : 0f) + "%   " + Clock(timer);
                    break;
                case MissionGoal.HoldUplink:
                    string state = uplink == null ? "" : uplink.Contested ? "  CONTESTED" : uplink.Owner == 0 ? "" : uplink.Count0 > 0 ? "  CAPTURING" : "  CAPTURE IT";
                    Objective = "Capture and hold the uplink" + state;
                    Status = "UPLINK " + Mathf.FloorToInt(held) + " / " + Mathf.RoundToInt(amount);
                    break;
                case MissionGoal.KillCommander:
                    Objective = commander != null && !commander.IsDead ? "Destroy the commander WARLORD" : "WARLORD destroyed";
                    Status = "ENEMIES " + alive;
                    break;
                default:
                    Objective = "Destroy the enemy team";
                    Status = "ENEMIES LEFT " + alive;
                    break;
            }
        }

        public override void RobotDied(Damageable d)
        {
            if (Over) return;
            if (HumanDown(d)) return;
            if (d.team == 1 && (goal == MissionGoal.DefendReactor || goal == MissionGoal.HoldUplink))
                reinforceQueue.Add(goal == MissionGoal.DefendReactor ? 7f : 9f);

            switch (goal)
            {
                case MissionGoal.DestroyDepots:
                    if (depots.Contains(d))
                    {
                        M.Announce("DEPOT DESTROYED", UiKit.Orange);
                        if (Alive(depots) == 0) Win("MISSION COMPLETE", "The enemy fuel supply is burning.");
                    }
                    break;
                case MissionGoal.KillCommander:
                    if (d == commander) Win("MISSION COMPLETE", "WARLORD is scrap metal. Their army is leaderless.");
                    break;
                case MissionGoal.EliminateAll:
                    if (d.team == 1 && Alive(M.Enemies) == 0)
                        Win(IsMission ? "MISSION COMPLETE" : "VICTORY", "Every enemy machine is scrap metal.");
                    break;
            }
            UpdateText();
        }

        protected override void OnWin()
        {
            if (IsMission) MatchSetup.CompleteMission(missionIndex);
        }
    }

    // =========================================================================
    // Survival
    // =========================================================================

    /// <summary>Waves of enemies that grow each round. Survive them all.</summary>
    public class SurvivalMode : ModeController
    {
        readonly int waves;
        readonly int allies;
        readonly int missionIndex;
        int wave;
        float breakTimer = 5f;
        float spawnTimer;
        readonly List<RobotType> queue = new List<RobotType>();
        bool inWave;

        public override bool IsMission => missionIndex >= 0;
        public override string ModeName => IsMission ? MatchSetup.Missions[missionIndex].name : "SURVIVAL";

        public SurvivalMode(int waves, int allies, int missionIndex = -1)
        {
            this.waves = waves;
            this.allies = allies;
            this.missionIndex = missionIndex;
        }

        public override IEnumerator Setup()
        {
            yield return SpawnPlayerAndAllies(allies, true);
            UpdateText();
        }

        static readonly RobotType[] Light = { RobotType.Hornet, RobotType.Raptor, RobotType.Arachne };
        static readonly RobotType[] Medium = { RobotType.Titan, RobotType.Lancer, RobotType.Raptor, RobotType.Arachne, RobotType.Hornet };
        static readonly RobotType[] Heavy = { RobotType.Juggernaut, RobotType.Bastion, RobotType.Titan };

        void StartWave()
        {
            wave++;
            inWave = true;
            queue.Clear();
            int count = Mathf.Min(wave + 1, 7);
            for (int i = 0; i < count; i++)
            {
                RobotType[] pool = wave <= 1 ? Light : (wave <= 3 || i % 3 != 0) ? Medium : Heavy;
                queue.Add(pool[Random.Range(0, pool.Length)]);
            }
            if (wave == waves) queue[0] = RobotType.Juggernaut;
            spawnTimer = 0f;
            M.Announce(wave == waves ? "FINAL WAVE" : "WAVE " + wave, UiKit.Red);
        }

        public override void Tick(float dt)
        {
            if (Over) return;
            if (!inWave)
            {
                breakTimer -= dt;
                if (breakTimer <= 0f) StartWave();
            }
            else
            {
                spawnTimer -= dt;
                if (queue.Count > 0 && spawnTimer <= 0f && Alive(M.Enemies) < 4)
                {
                    var e = M.SpawnRobot(queue[0], 1, FreeSpawn(L.enemySpawns), NextEnemyName());
                    Brain(e).startDelay = 1.5f;
                    queue.RemoveAt(0);
                    spawnTimer = 2.5f;
                }
                if (queue.Count == 0 && Alive(M.Enemies) == 0)
                {
                    inWave = false;
                    if (wave >= waves)
                    {
                        Win(IsMission ? "MISSION COMPLETE" : "SURVIVED", "All " + waves + " waves destroyed.");
                        return;
                    }
                    breakTimer = 10f;
                    M.Announce("WAVE CLEARED", UiKit.Green);
                    // Patch up the team and bring back fallen teammates.
                    if (M.Player != null) M.Player.Heal(M.Player.maxHealth * 0.4f);
                    foreach (var a in M.Allies) if (a != null && !a.IsDead) a.Heal(a.maxHealth * 0.4f);
                    M.ReviveAllies();
                }
            }
            UpdateText();
        }

        void UpdateText()
        {
            Status = "WAVE " + Mathf.Max(1, wave) + " / " + waves;
            if (!inWave) Objective = wave == 0 ? "Get ready. First wave in " + Mathf.CeilToInt(breakTimer) : "Next wave in " + Mathf.CeilToInt(breakTimer) + "  -  repairs done";
            else Objective = "Destroy the wave  " + (Alive(M.Enemies) + queue.Count) + " left";
        }

        public override void RobotDied(Damageable d)
        {
            HumanDown(d);
        }

        protected override void OnWin()
        {
            if (IsMission) MatchSetup.CompleteMission(missionIndex);
        }
    }

    // =========================================================================
    // Domination
    // =========================================================================

    /// <summary>Three capture points; owned points score. Robots respawn. First to 100.</summary>
    public class DominationMode : ModeController
    {
        const float ScoreToWin = 100f;
        const float PointsPerZonePerSecond = 0.6f;
        const float RespawnDelay = 7f;
        const float TimeLimit = 420f;

        readonly List<CaptureZone> zones = new List<CaptureZone>();
        readonly List<WorldMarker> zoneMarkers = new List<WorldMarker>();
        readonly float[] score = new float[2];
        float clock = TimeLimit;
        float assignTimer;
        readonly List<(Damageable d, float t)> respawns = new List<(Damageable, float)>();

        public override string ModeName => "DOMINATION";

        public override IEnumerator Setup()
        {
            yield return SpawnPlayerAndAllies(2, false);
            string[] letters = { "A", "B", "C" };
            for (int i = 0; i < L.zones.Length && i < 3; i++)
            {
                var z = CaptureZone.Create(L.zones[i], L.zoneRadius, letters[i]);
                z.Captured += (zone, team) => M.Announce((team == 0 ? "POINT " : "ENEMY TOOK ") + zone.label, team == 0 ? UiKit.Cyan : UiKit.Red);
                zones.Add(z);
                var mk = new WorldMarker { label = letters[i], position = z.transform.position, height = 16f };
                zoneMarkers.Add(mk);
                Markers.Add(mk);
            }
            yield return null;
            var types = RandomTeam(3);
            for (int i = 0; i < 3; i++)
            {
                M.SpawnRobot(types[i], 1, L.enemySpawns[i], NextEnemyName());
                yield return null;
            }
            AssignGoals();
            UpdateText();
        }

        void AssignGoals()
        {
            var taken = new Dictionary<CaptureZone, int>();
            foreach (var list in new[] { M.Allies, M.Enemies })
            {
                taken.Clear();
                foreach (var d in list)
                {
                    var b = Brain(d);
                    if (b == null || d.IsDead) continue;
                    CaptureZone best = null;
                    float bestScore = float.MaxValue;
                    foreach (var z in zones)
                    {
                        float s = Vector3.Distance(d.transform.position, z.transform.position);
                        if (z.Owner == d.team && !z.Contested) s += 160f;
                        if (taken.TryGetValue(z, out int n)) s += 70f * n;
                        if (s < bestScore)
                        {
                            bestScore = s;
                            best = z;
                        }
                    }
                    if (best == null) continue;
                    taken[best] = taken.TryGetValue(best, out int c) ? c + 1 : 1;
                    b.goal = best.transform.position + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                    b.goalRadius = best.radius * 0.5f;
                    b.engageRange = 85f;
                }
            }
        }

        public override void Tick(float dt)
        {
            if (Over) return;
            foreach (var z in zones) if (z.Owner >= 0) score[z.Owner] += PointsPerZonePerSecond * dt;
            for (int i = 0; i < zones.Count; i++) zoneMarkers[i].color = CaptureZone.TeamColor(zones[i].Owner);

            assignTimer -= dt;
            if (assignTimer <= 0f)
            {
                assignTimer = 4f;
                AssignGoals();
            }

            for (int i = respawns.Count - 1; i >= 0; i--)
            {
                var (d, t) = respawns[i];
                t -= dt;
                if (t <= 0f)
                {
                    respawns.RemoveAt(i);
                    Respawn(d);
                }
                else respawns[i] = (d, t);
            }
            RespawnIn = -1f;
            foreach (var r in respawns) if (r.d == M.Player) RespawnIn = r.t;

            clock -= dt;
            if (score[0] >= ScoreToWin) Win("VICTORY", "Your team controlled the battlefield.");
            else if (score[1] >= ScoreToWin) Lose("DEFEAT", "The enemy took the battlefield.");
            else if (clock <= 0f)
            {
                if (score[0] >= score[1]) Win("VICTORY", "Time is up. Your team held more ground.");
                else Lose("DEFEAT", "Time is up. The enemy held more ground.");
            }
            UpdateText();
        }

        void UpdateText()
        {
            Status = Mathf.FloorToInt(score[0]) + "   VS   " + Mathf.FloorToInt(score[1]) + "      " + Clock(clock);
            int own = 0;
            foreach (var z in zones) if (z.Owner == 0) own++;
            Objective = RespawnIn > 0f ? "Respawning in " + Mathf.CeilToInt(RespawnIn) : "Capture and hold points  " + own + " / " + zones.Count + " held";
        }

        public override void RobotDied(Damageable d)
        {
            if (Over) return;
            respawns.Add((d, RespawnDelay));
            if (d == M.Player) RespawnIn = RespawnDelay;
        }

        public override float RespawnTimeFor(Damageable d)
        {
            foreach (var r in respawns) if (r.d == d) return r.t;
            return -1f;
        }

        void Respawn(Damageable old)
        {
            if (old == null) return;
            bool wasPlayer = old == M.Player;
            int team = old.team;
            var points = team == 0 ? L.allySpawns : L.enemySpawns;
            Vector3 pos = FreeSpawn(points);
            if (wasPlayer)
            {
                M.RemoveWreck(old);
                M.SpawnPlayer(pos);
                M.ShowAnnouncement("BACK IN THE FIGHT", UiKit.Cyan);
                return;
            }
            if (M.IsRemoteHuman(old, out ulong owner))
            {
                var rtype = M.TypeOf(old);
                string rname = old.displayName;
                M.RemoveWreck(old);
                M.SpawnRemotePlayer(owner, rtype, pos, rname);
                return;
            }
            var type = M.TypeOf(old);
            string name = old.displayName;
            M.RemoveWreck(old);
            var d = M.SpawnRobot(type, team, pos, name);
            Brain(d).startDelay = 0.5f;
            AssignGoals();
        }
    }
}
