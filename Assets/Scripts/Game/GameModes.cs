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
                var ab = Brain(a);
                if (follow) ab.Escort(player.transform, 200f);
                else ab.Hunt();
                ab.flankOffset = (i % 2 == 0 ? -1f : 1f) * 70f;
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

        /// <summary>
        /// Where a robot of this team comes back. The main base while it is safe; when
        /// enemies are in or near it, whichever base slot or reserve base is furthest
        /// from every enemy, so nobody gets shot the moment they appear.
        /// </summary>
        protected Vector3 SafeSpawn(int team)
        {
            var main = L.Spawns(team);
            var reserve = L.Reserve(team);
            const float Safe = 170f;
            float best = -1f;
            Vector3 bestP = main[0];
            bool mainSafe = NearestEnemy(main[0], team) > Safe;
            for (int pass = 0; pass < 2; pass++)
            {
                var pts = pass == 0 ? main : reserve;
                if (pts == null) continue;
                foreach (var p in pts)
                {
                    if (Occupied(p)) continue;
                    float d = NearestEnemy(p, team);
                    // Prefer the main base when it is safe; otherwise go where enemies are furthest.
                    float score = d + (pass == 0 && mainSafe ? 1000f : 0f);
                    if (score > best) { best = score; bestP = p; }
                }
            }
            if (best < 0f) bestP = main[Random.Range(0, main.Length)] + new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-10f, 10f));
            return bestP;
        }

        static float NearestEnemy(Vector3 p, int team)
        {
            float best = float.MaxValue;
            foreach (var d in Damageable.All)
            {
                if (d == null || d.IsDead || d.team == team) continue;
                float dist = Vector3.Distance(Flat(d.transform.position), Flat(p));
                if (dist < best) best = dist;
            }
            return best;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static bool Occupied(Vector3 p)
        {
            foreach (var d in Damageable.All)
                if (d != null && !d.IsDead && (Flat(d.transform.position) - Flat(p)).sqrMagnitude < 22f * 22f) return true;
            return false;
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
        readonly List<Damageable> cores = new List<Damageable>();
        Vector3 extraction;
        Damageable ace;
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
            // A duel starts close: no hour-long walk to find each other.
            Vector3? start = goal == MissionGoal.Duel ? NavGrid.OpenSpot(L.zones[0] + new Vector3(0f, 0f, -120f), 2) : (Vector3?)null;
            yield return SpawnPlayerAndAllies(allies, !defend, start);

            switch (goal)
            {
                case MissionGoal.DestroyDepots:
                    for (int i = 0; i < L.depots.Length; i++)
                    {
                        var dep = Structures.FuelDepot(L.depots[i], i * 35f - 35f);
                        depots.Add(dep);
                        // Depots aren't robots, so listen for their destruction directly.
                        dep.Died += DepotDestroyed;
                        var depRef = dep;
                        dep.Damaged += (amt, at) => AlertGuards(depRef);
                        Markers.Add(new WorldMarker { label = "FUEL TANK", color = UiKit.Red, target = dep, height = 19f });
                        yield return null;
                    }
                    break;
                case MissionGoal.DefendReactor:
                    reactor = Structures.Reactor(L.reactor);
                    reactor.Died += _ => Lose("MISSION FAILED", "The reactor was destroyed.");
                    Markers.Add(new WorldMarker { label = "DEFEND", color = UiKit.Cyan, target = reactor, height = 14f });
                    timer = amount;
                    break;
                case MissionGoal.Sabotage:
                {
                    Vector3 eb = L.Base(1);
                    Vector3 fwd = new Vector3(-eb.x, 0f, -eb.z).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, fwd);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var core = Structures.PowerCore(NavGrid.OpenSpot(eb + fwd * 110f + side * s * 85f, 2), 1, Quaternion.LookRotation(fwd).eulerAngles.y);
                        cores.Add(core);
                        var coreRef = core;
                        core.Died += _ => CoreDestroyed();
                        core.Damaged += (amt, at) => AlertGuards(coreRef);
                        Markers.Add(new WorldMarker { label = "POWER CORE", color = UiKit.Red, target = core, height = 22f });
                        yield return null;
                    }
                    break;
                }
                case MissionGoal.Extraction:
                    extraction = NavGrid.OpenSpot(L.enemyReserve.Length > 1 ? L.enemyReserve[1] * 0.85f : L.Base(1) * 0.7f, 2);
                    Markers.Add(new WorldMarker { label = "EXTRACTION", color = UiKit.Green, position = extraction, height = 20f });
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
                Vector3 watch = (L.Base(1) - guard);
                watch.y = 0f;
                int k = 0;
                foreach (var a in M.Allies)
                {
                    var b = Brain(a);
                    if (b == null) continue;
                    // Spread the guards round the front of the objective.
                    Vector3 post = guard + Quaternion.Euler(0f, (k++ % 2 == 0 ? -35f : 35f), 0f) * watch.normalized * 30f;
                    if (goal == MissionGoal.HoldUplink) b.Capture(L.zones[1], 30f);
                    else b.Defend(post, 18f, 110f, watch.normalized);
                }
            }

            if (goal == MissionGoal.KillCommander || (IsMission && MatchSetup.Missions[missionIndex].boss))
            {
                commander = M.SpawnRobot(RobotType.Juggernaut, 1, L.enemySpawns[0], "WARLORD", true);
                commander.maxHealth *= 2f;
                commander.Heal(commander.maxHealth);
                var cb = Brain(commander);
                Vector3 post = goal == MissionGoal.Sabotage && cores.Count > 0 ? (cores[0].transform.position + cores[cores.Count - 1].transform.position) * 0.5f : L.zones[1];
                Vector3 watchC = L.Base(0) - post;
                watchC.y = 0f;
                cb.Defend(post, 30f, 140f, watchC.normalized);
                cb.aggression = 0.8f;
                Markers.Add(new WorldMarker { label = "WARLORD", color = UiKit.Red, target = commander, height = 9f });
                yield return null;
            }

            // Timed missions keep the pressure on with reinforcements instead of one big wave.
            bool timed = goal == MissionGoal.DefendReactor || goal == MissionGoal.HoldUplink || goal == MissionGoal.Extraction || goal == MissionGoal.Sabotage;
            maxAlive = timed ? Mathf.Min(enemyTypes.Length, goal == MissionGoal.Sabotage ? 4 : 3) : enemyTypes.Length;
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
            else if (i >= L.enemySpawns.Length || M.Enemies.Count > 0) pos = SafeSpawn(1);

            if (goal == MissionGoal.Duel) pos = NavGrid.OpenSpot(L.zones[2] + new Vector3(0f, 0f, 120f), 2);
            var e = M.SpawnRobot(type, 1, pos, NextEnemyName());
            var b = Brain(e);
            Vector3 towardAllies = L.Base(0) - pos;
            towardAllies.y = 0f;
            switch (goal)
            {
                case MissionGoal.DestroyDepots:
                    // Guards walk a beat round their tank, and run to help when any tank is hit.
                    b.Defend(pos, 26f, 150f, towardAllies.normalized);
                    break;
                case MissionGoal.DefendReactor:
                    // Attackers march on the reactor; every other one goes straight for it.
                    b.goal = L.reactor;
                    b.goalRadius = 45f;
                    b.engageRange = 130f;
                    if (i % 2 == 0) b.priorityTarget = reactor;
                    break;
                case MissionGoal.HoldUplink:
                    b.Capture(L.zones[1] + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f)), 30f);
                    break;
                case MissionGoal.KillCommander:
                    if (commander != null) b.Escort(commander.transform, 140f);
                    break;
                case MissionGoal.Sabotage:
                {
                    // Half guard the cores, half go out to meet the attack.
                    var core = cores.Count > 0 ? cores[i % cores.Count] : null;
                    if (i % 2 == 0 && core != null && !core.IsDead)
                    {
                        Vector3 w = L.Base(0) - core.transform.position;
                        w.y = 0f;
                        b.Defend(core.transform.position + w.normalized * 30f, 22f, 140f, w.normalized);
                    }
                    else { b.Hunt(); b.flankOffset = (i % 3 - 1) * 80f; }
                    break;
                }
                case MissionGoal.Extraction:
                    b.Hunt();
                    b.flankOffset = Random.Range(-90f, 90f);
                    b.aggression = Mathf.Max(b.aggression, 0.7f);
                    break;
                case MissionGoal.Duel:
                    ace = e;
                    e.displayName = "ACE " + e.displayName;
                    e.maxHealth *= 1.6f;
                    e.Heal(e.maxHealth);
                    b.aggression = 0.95f;
                    b.Hunt();
                    b.startDelay = 2f;
                    Markers.Add(new WorldMarker { label = "ACE", color = UiKit.Red, target = e, height = 9f });
                    break;
                default:
                    // Team battle: hunt the enemy, each coming in on its own line.
                    b.Hunt();
                    b.flankOffset = (i % 3 - 1) * 90f;
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

            if (goal == MissionGoal.Extraction && M.Player != null && !M.Player.IsDead)
            {
                Vector3 off = M.Player.transform.position - extraction;
                off.y = 0f;
                if (off.magnitude < 45f) { Win("MISSION COMPLETE", "Extracted. The dropship is lifting off."); return; }
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

        void DepotDestroyed(Damageable d)
        {
            if (Over) return;
            // The remaining guards close ranks round the tanks still standing.
            foreach (var e in M.Enemies)
            {
                var b = Brain(e);
                if (b == null || e.IsDead) continue;
                Damageable nearest = null;
                float best = float.MaxValue;
                foreach (var dep in depots)
                {
                    if (dep == null || dep.IsDead) continue;
                    float dd = Vector3.Distance(dep.transform.position, e.transform.position);
                    if (dd < best) { best = dd; nearest = dep; }
                }
                if (nearest != null && best > 60f)
                {
                    Vector3 w = L.Base(0) - nearest.transform.position;
                    w.y = 0f;
                    b.Defend(nearest.transform.position + w.normalized * 26f, 26f, 150f, w.normalized);
                }
            }
            M.Announce("FUEL TANK DESTROYED", UiKit.Orange);
            if (Alive(depots) == 0) Win("MISSION COMPLETE", "The enemy fuel supply is burning.");
            UpdateText();
        }

        void CoreDestroyed()
        {
            if (Over) return;
            M.Announce("POWER CORE DESTROYED", UiKit.Orange);
            if (Alive(cores) == 0) Win("MISSION COMPLETE", "Their power is out. The base is ours.");
            UpdateText();
        }

        /// <summary>A tank is under fire: the guards turn on whoever is shooting it.</summary>
        void AlertGuards(Damageable dep)
        {
            var attacker = dep.LastAttacker;
            if (attacker == null) return;
            foreach (var e in M.Enemies)
            {
                var b = Brain(e);
                if (b != null && !e.IsDead && Vector3.Distance(e.transform.position, dep.transform.position) < 320f) b.Alert(attacker);
            }
        }

        void UpdateText()
        {
            int alive = Alive(M.Enemies);
            switch (goal)
            {
                case MissionGoal.DestroyDepots:
                    Objective = "Destroy the fuel tanks  " + (depots.Count - Alive(depots)) + " / " + depots.Count;
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
                case MissionGoal.Sabotage:
                    Objective = "Destroy the power cores  " + (cores.Count - Alive(cores)) + " / " + cores.Count;
                    Status = "ENEMIES " + alive;
                    break;
                case MissionGoal.Extraction:
                {
                    float d = M.Player != null ? Vector3.Distance(new Vector3(M.Player.transform.position.x, 0f, M.Player.transform.position.z), extraction) : 0f;
                    Objective = "Reach the extraction point";
                    Status = Mathf.RoundToInt(d) + " m TO EXTRACTION";
                    break;
                }
                case MissionGoal.Duel:
                    Objective = "Defeat the enemy ace";
                    Status = ace != null ? "ACE " + Mathf.CeilToInt(ace.HealthFraction * 100f) + "%" : "";
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
            if (d.team == 1 && (goal == MissionGoal.DefendReactor || goal == MissionGoal.HoldUplink || goal == MissionGoal.Extraction || goal == MissionGoal.Sabotage))
                reinforceQueue.Add(goal == MissionGoal.DefendReactor ? 7f : goal == MissionGoal.Extraction ? 6f : 9f);

            switch (goal)
            {
                case MissionGoal.KillCommander:
                    if (d == commander) Win("MISSION COMPLETE", "WARLORD is scrap metal. Their army is leaderless.");
                    break;
                case MissionGoal.Duel:
                    if (d == ace) Win("MISSION COMPLETE", "The ace is down. Nobody will challenge you again.");
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

        static readonly RobotType[] Light = { RobotType.Hornet, RobotType.Raptor, RobotType.Arachne, RobotType.Strider };
        static readonly RobotType[] Medium = { RobotType.Titan, RobotType.Lancer, RobotType.Raptor, RobotType.Arachne, RobotType.Hornet, RobotType.Vanguard, RobotType.Strider };
        static readonly RobotType[] Heavy = { RobotType.Juggernaut, RobotType.Bastion, RobotType.Titan, RobotType.Mammoth, RobotType.Colossus, RobotType.Scarab };

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
                    var e = M.SpawnRobot(queue[0], 1, SafeSpawn(1), NextEnemyName());
                    var eb = Brain(e);
                    eb.startDelay = 1.5f;
                    eb.Hunt();
                    eb.flankOffset = Random.Range(-100f, 100f);
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
}
