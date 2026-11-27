using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    // =========================================================================
    // Modes where fallen robots come back: shared respawn handling
    // =========================================================================

    /// <summary>Base for modes where destroyed robots are rebuilt at their base after a delay.</summary>
    public abstract class RespawnMode : ModeController
    {
        protected float RespawnDelay = 7f;
        protected readonly List<(Damageable d, float t)> respawns = new List<(Damageable, float)>();

        protected void TickRespawns(float dt)
        {
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

        /// <summary>Where a robot of this team comes back.</summary>
        protected virtual Vector3 RespawnPoint(int team) => SafeSpawn(team);

        /// <summary>Orders for a rebuilt AI robot.</summary>
        protected virtual void Orders(Damageable d) { }

        protected Damageable Respawn(Damageable old)
        {
            if (old == null) return null;
            bool wasPlayer = old == M.Player;
            int team = old.team;
            Vector3 pos = RespawnPoint(team);
            if (wasPlayer)
            {
                M.RemoveWreck(old);
                var p = M.SpawnPlayer(pos);
                M.ShowAnnouncement("BACK IN THE FIGHT", UiKit.Cyan);
                OnPlayerBack(p);
                return p;
            }
            if (M.IsRemoteHuman(old, out ulong owner))
            {
                var rtype = M.TypeOf(old);
                string rname = old.displayName;
                M.RemoveWreck(old);
                return M.SpawnRemotePlayer(owner, rtype, pos, rname);
            }
            var type = M.TypeOf(old);
            string name = old.displayName;
            M.RemoveWreck(old);
            var d = M.SpawnRobot(type, team, pos, name);
            var b = Brain(d);
            if (b != null) b.startDelay = 0.5f;
            Orders(d);
            return d;
        }

        protected virtual void OnPlayerBack(Damageable p) { }

        protected IEnumerator SpawnEnemyTeam(int count)
        {
            var types = RandomTeam(count);
            for (int i = 0; i < count; i++)
            {
                var e = M.SpawnRobot(types[i], 1, L.enemySpawns[i % L.enemySpawns.Length], NextEnemyName());
                Orders(e);
                yield return null;
            }
        }

        protected static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }

    // =========================================================================
    // Domination
    // =========================================================================

    /// <summary>Three capture points; owned points score. Robots respawn. First to 100.</summary>
    public class DominationMode : RespawnMode
    {
        const float ScoreToWin = 100f;
        const float PointsPerZonePerSecond = 0.6f;
        const float TimeLimit = 420f;

        readonly List<CaptureZone> zones = new List<CaptureZone>();
        readonly List<WorldMarker> zoneMarkers = new List<WorldMarker>();
        readonly float[] score = new float[2];
        float clock = TimeLimit;
        float assignTimer;

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
            yield return SpawnEnemyTeam(3);
            AssignGoals();
            UpdateText();
        }

        protected override void Orders(Damageable d) => assignTimer = 0f;

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
                        // Points under attack pull defenders back.
                        if (z.Owner == d.team && (d.team == 0 ? z.Count1 : z.Count0) > 0) s -= 120f;
                        if (taken.TryGetValue(z, out int n)) s += 70f * n;
                        if (s < bestScore)
                        {
                            bestScore = s;
                            best = z;
                        }
                    }
                    if (best == null) continue;
                    taken[best] = taken.TryGetValue(best, out int c) ? c + 1 : 1;
                    b.Capture(best.transform.position, best.radius);
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
            TickRespawns(dt);

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
    }

    // =========================================================================
    // King of the Hill
    // =========================================================================

    /// <summary>One hill to hold. It moves every 80 seconds. Robots respawn. First to 100.</summary>
    public class KingOfTheHillMode : RespawnMode
    {
        const float ScoreToWin = 100f;
        const float PointsPerSecond = 1f;
        const float MoveEvery = 80f;
        const float TimeLimit = 480f;

        CaptureZone hill;
        WorldMarker marker;
        readonly List<Vector3> sites = new List<Vector3>();
        int site;
        float moveTimer = MoveEvery;
        float clock = TimeLimit;
        float assignTimer;
        bool warned;
        readonly float[] score = new float[2];

        public override string ModeName => "KING OF THE HILL";

        public override IEnumerator Setup()
        {
            RespawnDelay = 6f;
            // The hill visits the middle, then the flanks, then the middle again.
            sites.Add(L.zones[1]);
            sites.Add(L.zones[0]);
            sites.Add(L.zones[1] + new Vector3(0f, 0f, 90f));
            sites.Add(L.zones[2]);
            sites.Add(L.zones[1] + new Vector3(0f, 0f, -90f));
            yield return SpawnPlayerAndAllies(2, false);
            PlaceHill();
            yield return SpawnEnemyTeam(3);
            AssignGoals();
            UpdateText();
        }

        void PlaceHill()
        {
            if (hill != null) Object.Destroy(hill.gameObject);
            Vector3 p = NavGrid.OpenSpot(sites[site % sites.Count], 2);
            hill = CaptureZone.Create(p, L.zoneRadius * 1.15f, "HILL");
            hill.captureTime = 4f;
            hill.Captured += (z, team) => M.Announce(team == 0 ? "HILL TAKEN" : "ENEMY HOLDS THE HILL", team == 0 ? UiKit.Cyan : UiKit.Red);
            if (marker == null)
            {
                marker = new WorldMarker { label = "HILL", height = 18f };
                Markers.Add(marker);
            }
            marker.position = hill.transform.position;
            moveTimer = MoveEvery;
            warned = false;
            assignTimer = 0f;
        }

        protected override void Orders(Damageable d) => assignTimer = 0f;

        void AssignGoals()
        {
            if (hill == null) return;
            foreach (var list in new[] { M.Allies, M.Enemies })
            {
                int k = 0;
                foreach (var d in list)
                {
                    var b = Brain(d);
                    if (b == null || d.IsDead) continue;
                    // Most go for the hill; one in three swings wide to catch robots on their way in.
                    if (k++ % 3 == 2) { b.Hunt(); b.flankOffset = 110f; }
                    else b.Capture(hill.transform.position, hill.radius);
                }
            }
        }

        public override void Tick(float dt)
        {
            if (Over) return;
            if (hill != null && hill.Owner >= 0 && !hill.Contested) score[hill.Owner] += PointsPerSecond * dt;
            if (marker != null && hill != null) marker.color = CaptureZone.TeamColor(hill.Owner);

            moveTimer -= dt;
            if (!warned && moveTimer < 10f)
            {
                warned = true;
                M.Announce("HILL MOVES IN 10", UiKit.Orange);
            }
            if (moveTimer <= 0f)
            {
                site++;
                PlaceHill();
                M.Announce("THE HILL HAS MOVED", UiKit.Orange);
            }
            assignTimer -= dt;
            if (assignTimer <= 0f)
            {
                assignTimer = 5f;
                AssignGoals();
            }
            TickRespawns(dt);

            clock -= dt;
            if (score[0] >= ScoreToWin) Win("VICTORY", "You were king of the hill.");
            else if (score[1] >= ScoreToWin) Lose("DEFEAT", "The enemy held the hill.");
            else if (clock <= 0f)
            {
                if (score[0] >= score[1]) Win("VICTORY", "Time is up. You held the hill longer.");
                else Lose("DEFEAT", "Time is up. The enemy held the hill longer.");
            }
            UpdateText();
        }

        void UpdateText()
        {
            Status = Mathf.FloorToInt(score[0]) + "   VS   " + Mathf.FloorToInt(score[1]) + "      " + Clock(clock);
            string state = hill == null ? "" : hill.Contested ? "CONTESTED" : hill.Owner == 0 ? "YOU HOLD IT" : hill.Owner == 1 ? "ENEMY HOLDS IT" : "UNCLAIMED";
            Objective = RespawnIn > 0f ? "Respawning in " + Mathf.CeilToInt(RespawnIn)
                : "Hold the hill  " + state + "   moves in " + Mathf.CeilToInt(moveTimer);
        }
    }

    // =========================================================================
    // Siege
    // =========================================================================

    /// <summary>Each base has two power cores. Destroy the enemy's, protect yours. Robots respawn.</summary>
    public class SiegeMode : RespawnMode
    {
        const float TimeLimit = 540f;
        readonly List<Damageable> ourCores = new List<Damageable>();
        readonly List<Damageable> theirCores = new List<Damageable>();
        float clock = TimeLimit;
        float assignTimer;

        public override string ModeName => "SIEGE";

        public override IEnumerator Setup()
        {
            RespawnDelay = 8f;
            for (int team = 0; team < 2; team++)
            {
                Vector3 b = L.Base(team);
                Vector3 fwd = FlatDir(-b);
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 p = NavGrid.OpenSpot(b + fwd * 95f + side * s * 90f, 2);
                    var core = Structures.PowerCore(p, team, Quaternion.LookRotation(fwd).eulerAngles.y);
                    (team == 0 ? ourCores : theirCores).Add(core);
                    int t = team;
                    core.Died += _ => CoreDestroyed(t);
                    core.Damaged += (amt, at) => CoreHit(core);
                    Markers.Add(new WorldMarker { label = team == 0 ? "DEFEND" : "CORE", color = team == 0 ? UiKit.Cyan : UiKit.Red, target = core, height = 22f });
                    yield return null;
                }
            }
            yield return SpawnPlayerAndAllies(2, false);
            yield return SpawnEnemyTeam(3);
            AssignGoals();
            UpdateText();
        }

        protected override void Orders(Damageable d) => assignTimer = 0f;

        static Damageable NearestAlive(List<Damageable> cores, Vector3 from)
        {
            Damageable best = null;
            float bd = float.MaxValue;
            foreach (var c in cores)
            {
                if (c == null || c.IsDead) continue;
                float dd = Vector3.Distance(c.transform.position, from);
                if (dd < bd) { bd = dd; best = c; }
            }
            return best;
        }

        void AssignGoals()
        {
            for (int team = 0; team < 2; team++)
            {
                var list = team == 0 ? M.Allies : M.Enemies;
                var mine = team == 0 ? ourCores : theirCores;
                var theirs = team == 0 ? theirCores : ourCores;
                int k = 0;
                foreach (var d in list)
                {
                    var b = Brain(d);
                    if (b == null || d.IsDead) continue;
                    var guard = NearestAlive(mine, d.transform.position);
                    var strike = NearestAlive(theirs, d.transform.position);
                    // One robot per team holds the base; the rest go after the cores.
                    if (k++ == 0 && guard != null)
                    {
                        Vector3 w = FlatDir(-L.Base(team));
                        b.priorityTarget = null;
                        b.Defend(guard.transform.position + w * 28f, 20f, 130f, w);
                    }
                    else if (strike != null)
                    {
                        b.follow = null;
                        b.goal = strike.transform.position;
                        b.goalRadius = 50f;
                        b.leash = 0f;
                        b.engageRange = 150f;
                        b.priorityTarget = strike;
                    }
                }
            }
        }

        void CoreHit(Damageable core)
        {
            var attacker = core.LastAttacker;
            if (attacker == null) return;
            var list = core.team == 0 ? M.Allies : M.Enemies;
            foreach (var d in list)
            {
                var b = Brain(d);
                if (b != null && !d.IsDead && Vector3.Distance(d.transform.position, core.transform.position) < 300f) b.Alert(attacker);
            }
        }

        void CoreDestroyed(int team)
        {
            if (Over) return;
            M.Announce(team == 0 ? "WE LOST A POWER CORE" : "ENEMY POWER CORE DESTROYED", team == 0 ? UiKit.Red : UiKit.Cyan);
            if (Alive(theirCores) == 0) Win("VICTORY", "The enemy base has lost all power.");
            else if (Alive(ourCores) == 0) Lose("DEFEAT", "Our base has lost all power.");
            assignTimer = 0f;
        }

        public override void Tick(float dt)
        {
            if (Over) return;
            assignTimer -= dt;
            if (assignTimer <= 0f)
            {
                assignTimer = 6f;
                AssignGoals();
            }
            TickRespawns(dt);
            clock -= dt;
            if (clock <= 0f)
            {
                float us = 0f, them = 0f;
                foreach (var c in ourCores) if (c != null && !c.IsDead) us += c.HealthFraction;
                foreach (var c in theirCores) if (c != null && !c.IsDead) them += c.HealthFraction;
                if (us >= them) Win("VICTORY", "Time is up. Our base has more power left.");
                else Lose("DEFEAT", "Time is up. The enemy base has more power left.");
            }
            UpdateText();
        }

        static int Pct(List<Damageable> cores)
        {
            float sum = 0f;
            foreach (var c in cores) if (c != null && !c.IsDead) sum += c.HealthFraction;
            return Mathf.CeilToInt(sum / Mathf.Max(1, cores.Count) * 100f);
        }

        void UpdateText()
        {
            Status = "OURS " + Pct(ourCores) + "%   THEIRS " + Pct(theirCores) + "%   " + Clock(clock);
            Objective = RespawnIn > 0f ? "Respawning in " + Mathf.CeilToInt(RespawnIn)
                : "Destroy the enemy power cores  " + (theirCores.Count - Alive(theirCores)) + " / " + theirCores.Count;
        }
    }

    // =========================================================================
    // Escort
    // =========================================================================

    /// <summary>
    /// Walk a damaged command walker across the map to the extraction point. It only moves
    /// while friendly robots are near it, and the enemy keeps coming. Lose it and it's over.
    /// </summary>
    public class EscortMode : RespawnMode
    {
        readonly int missionIndex;
        Damageable vip;
        AIBrain vipBrain;
        readonly List<Vector3> waypoints = new List<Vector3>();
        int wp;
        Vector3 extraction;
        float waveTimer = 25f;
        int enemyCap = 3;
        float assignTimer;
        float moving;

        public override bool IsMission => missionIndex >= 0;
        public override string ModeName => IsMission ? MatchSetup.Missions[missionIndex].name : "ESCORT";

        public EscortMode(int missionIndex = -1)
        {
            this.missionIndex = missionIndex;
        }

        public override IEnumerator Setup()
        {
            RespawnDelay = 8f;
            Vector3 start = L.Base(0) + FlatDir(-L.Base(0)) * 70f;
            extraction = NavGrid.OpenSpot(L.enemyReserve.Length > 0 ? L.enemyReserve[0] * 0.75f : L.Base(1) * 0.7f, 3);
            // A route through the middle of the map, with a dog-leg through a flank.
            waypoints.Add(NavGrid.OpenSpot(start + new Vector3(0f, 0f, 120f), 3));
            waypoints.Add(NavGrid.OpenSpot(L.zones[0] * 0.8f + new Vector3(0f, 0f, -60f), 3));
            waypoints.Add(NavGrid.OpenSpot(L.zones[1], 3));
            waypoints.Add(NavGrid.OpenSpot(Vector3.Lerp(L.zones[1], extraction, 0.5f), 3));
            waypoints.Add(extraction);

            yield return SpawnPlayerAndAllies(2, true);
            vip = M.SpawnRobot(RobotType.Mammoth, 0, NavGrid.OpenSpot(start, 3), "ATLAS PRIME");
            vip.maxHealth *= 2.2f;
            vip.Heal(vip.maxHealth);
            vipBrain = Brain(vip);
            vipBrain.aggression = 0.35f;
            vipBrain.startDelay = 3f;
            Markers.Add(new WorldMarker { label = "ATLAS PRIME", color = UiKit.Cyan, target = vip, height = 22f });
            Markers.Add(new WorldMarker { label = "EXTRACTION", color = UiKit.Green, position = extraction, height = 20f });
            vip.Died += _ => Lose(IsMission ? "MISSION FAILED" : "DEFEAT", "ATLAS PRIME has been destroyed.");

            // The allies stay with the walker, not with you.
            foreach (var a in M.Allies)
            {
                if (a == vip) continue;
                var b = Brain(a);
                if (b != null) b.Escort(vip.transform, 160f);
            }
            yield return SpawnEnemyTeam(3);
            UpdateText();
        }

        protected override void Orders(Damageable d)
        {
            var b = Brain(d);
            if (b == null) return;
            if (d.team == 0) { b.Escort(vip != null ? vip.transform : null, 160f); return; }
            // Two in three go straight for the walker; the rest hunt its escort.
            b.Hunt();
            b.flankOffset = Random.Range(-120f, 120f);
            if (Random.value < 0.66f && vip != null) b.priorityTarget = vip;
        }

        bool EscortNear()
        {
            if (vip == null) return false;
            if (M.Player != null && !M.Player.IsDead && Vector3.Distance(M.Player.transform.position, vip.transform.position) < 110f) return true;
            foreach (var a in M.Allies)
                if (a != null && a != vip && !a.IsDead && Vector3.Distance(a.transform.position, vip.transform.position) < 90f) return true;
            return false;
        }

        public override void Tick(float dt)
        {
            if (Over || vip == null) return;
            // The walker advances while its escort is with it, and waits for them otherwise.
            bool go = EscortNear();
            moving = go ? 1f : 0f;
            if (wp < waypoints.Count && Vector3.Distance(Flat(vip.transform.position), Flat(waypoints[wp])) < 30f) wp++;
            if (wp >= waypoints.Count || Vector3.Distance(Flat(vip.transform.position), Flat(extraction)) < 40f)
            {
                Win(IsMission ? "MISSION COMPLETE" : "VICTORY", "ATLAS PRIME made it out.");
                return;
            }
            vipBrain.follow = null;
            vipBrain.leash = 0f;
            vipBrain.engageRange = 90f;
            vipBrain.hunt = false;
            vipBrain.goal = go ? waypoints[wp] : vip.transform.position;
            vipBrain.goalRadius = go ? 8f : 30f;

            // Enemy pressure grows the further the walker gets.
            waveTimer -= dt;
            enemyCap = 3 + Mathf.Min(3, wp);
            if (waveTimer <= 0f)
            {
                waveTimer = 22f;
                if (Alive(M.Enemies) < enemyCap)
                {
                    var e = M.SpawnRobot(RandomType(RobotType.Mammoth), 1, FreeSpawn(L.enemyReserve.Length > 0 ? L.enemyReserve : L.enemySpawns), NextEnemyName());
                    Orders(e);
                }
            }
            assignTimer -= dt;
            if (assignTimer <= 0f)
            {
                assignTimer = 5f;
                foreach (var a in M.Allies)
                {
                    if (a == vip || a.IsDead) continue;
                    var b = Brain(a);
                    if (b != null && b.follow == null) b.Escort(vip.transform, 160f);
                }
            }
            TickRespawns(dt);
            UpdateText();
        }

        public override void RobotDied(Damageable d)
        {
            if (Over) return;
            if (d == vip) return;
            base.RobotDied(d);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void UpdateText()
        {
            if (vip == null) return;
            float left = 0f;
            Vector3 p = vip.transform.position;
            for (int i = wp; i < waypoints.Count; i++)
            {
                left += Vector3.Distance(Flat(p), Flat(waypoints[i]));
                p = waypoints[i];
            }
            Status = "ATLAS " + Mathf.CeilToInt(vip.HealthFraction * 100f) + "%    " + Mathf.RoundToInt(left) + " m TO GO";
            Objective = RespawnIn > 0f ? "Respawning in " + Mathf.CeilToInt(RespawnIn)
                : moving > 0f ? "Escort ATLAS PRIME to extraction" : "ATLAS PRIME is waiting for its escort";
        }
    }

    // =========================================================================
    // Free for all
    // =========================================================================

    /// <summary>Six machines, every one for itself. First to 8 kills, or the most kills when time runs out.</summary>
    public class FreeForAllMode : RespawnMode
    {
        const int KillsToWin = 8;
        const float TimeLimit = 420f;
        readonly Dictionary<Damageable, int> kills = new Dictionary<Damageable, int>();
        readonly Dictionary<string, int> score = new Dictionary<string, int>();
        readonly List<Vector3> spots = new List<Vector3>();
        float clock = TimeLimit;
        int nextTeam = 1;

        public override string ModeName => "FREE FOR ALL";

        public override IEnumerator Setup()
        {
            RespawnDelay = 5f;
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                spots.Add(NavGrid.OpenSpot(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 300f, 2));
            }
            var player = M.SpawnPlayer(spots[0]);
            score[player.displayName] = 0;
            yield return null;
            var types = RandomTeam(5);
            for (int i = 0; i < 5; i++)
            {
                // Every robot is its own team.
                var e = M.SpawnRobot(types[i] == M.PlayerType ? RandomType(M.PlayerType) : types[i], nextTeam++, spots[(i + 1) * 2 % spots.Count], NextEnemyName());
                Orders(e);
                score[e.displayName] = 0;
                yield return null;
            }
            UpdateText();
        }

        protected override void Orders(Damageable d)
        {
            var b = Brain(d);
            if (b == null) return;
            b.Hunt();
            b.flankOffset = Random.Range(-80f, 80f);
        }

        protected override Vector3 RespawnPoint(int team)
        {
            // The spot furthest from every other robot.
            Vector3 best = spots[0];
            float bestD = -1f;
            foreach (var s in spots)
            {
                float near = float.MaxValue;
                foreach (var d in Damageable.All)
                {
                    if (d == null || d.IsDead || d.GetComponent<Locomotion>() == null) continue;
                    near = Mathf.Min(near, Vector3.Distance(d.transform.position, s));
                }
                if (near > bestD) { bestD = near; best = s; }
            }
            return best;
        }

        public override void RobotDied(Damageable d)
        {
            if (Over) return;
            var killer = d.LastAttacker;
            if (killer != null && killer != d)
            {
                score.TryGetValue(killer.displayName, out int k);
                score[killer.displayName] = k + 1;
                if (k + 1 >= KillsToWin) { Finish(); return; }
            }
            base.RobotDied(d);
            UpdateText();
        }

        void Finish()
        {
            string you = M.Player != null ? M.Player.displayName : "";
            string leader = Leader(out int top);
            score.TryGetValue(you, out int mine);
            if (mine >= top) Win("VICTORY", "Top of the scrapheap with " + mine + " kills.");
            else Lose("DEFEAT", leader + " won with " + top + " kills. You scored " + mine + ".");
        }

        string Leader(out int top)
        {
            top = -1;
            string who = "";
            foreach (var kv in score)
                if (kv.Value > top) { top = kv.Value; who = kv.Key; }
            return who;
        }

        public override void Tick(float dt)
        {
            if (Over) return;
            TickRespawns(dt);
            clock -= dt;
            if (clock <= 0f) { Finish(); return; }
            UpdateText();
        }

        void UpdateText()
        {
            string you = M.Player != null ? M.Player.displayName : "";
            score.TryGetValue(you, out int mine);
            string leader = Leader(out int top);
            Status = "YOU " + mine + "    LEAD " + top + "    " + Clock(clock);
            Objective = RespawnIn > 0f ? "Respawning in " + Mathf.CeilToInt(RespawnIn)
                : leader == you || top <= mine ? "You lead. First to " + KillsToWin + " kills wins" : leader + " leads. First to " + KillsToWin + " kills wins";
        }
    }
}
