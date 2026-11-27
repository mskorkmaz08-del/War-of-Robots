using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The pilot of an AI robot. Every few tenths of a second it thinks: which enemy is the
    /// best target, can it actually see it, where should it be standing, and what its orders
    /// from the game mode want (hunt, escort, defend, capture, guard). Every frame it walks
    /// that plan out: it follows a route round buildings on the NavGrid, keeps its distance
    /// and side-steps while it fights (walkers face the enemy, wheeled machines circle),
    /// plants its feet to fire, falls back to its team when badly hurt, never shoots into
    /// walls or teammates, and calls for help when it is hit.
    /// </summary>
    [RequireComponent(typeof(RobotCommands))]
    public class AIBrain : MonoBehaviour, IDisableOnDeath
    {
        public float preferredRange = 60f;
        public float fireRange = 180f;
        public float specialRange = 140f;
        [Range(0f, 1f)] public float aggression = 0.6f;
        public bool usesBoostToCharge;

        [Header("Orders")]
        /// <summary>Escort this robot (allies stay with the player) when nothing needs fighting.</summary>
        public Transform follow;
        /// <summary>Where the orders say to be (capture point, depot, reactor).</summary>
        public Vector3? goal;
        public float goalRadius = 14f;
        /// <summary>Enemies further than this from the robot (or from the escorted robot) are ignored while it has orders.</summary>
        public float engageRange = 160f;
        /// <summary>While fighting, never stray further than this from the goal (0 = chase freely).</summary>
        public float leash;
        /// <summary>Preferred target (the reactor, the commander) when it is within reach.</summary>
        public Damageable priorityTarget;
        /// <summary>With no orders and nothing in sight, go and find the enemy.</summary>
        public bool hunt = true;
        /// <summary>Which way a guard looks while it waits (usually toward the enemy base).</summary>
        public Vector3 watchDirection = Vector3.forward;
        /// <summary>Hunting: approach the enemy this far off to one side, so a team doesn't arrive in a single file.</summary>
        public float flankOffset;
        public float startDelay = 4f;

        RobotCommands commands;
        Turret turret;
        Damageable self;
        Weapon[] weapons;
        Locomotion locomotion;
        bool walker;
        int clearance = 2;

        Damageable target;
        bool targetVisible;
        float targetSince;
        float lastSeenTarget = -99f;
        Vector3 lastSeenPos;

        readonly List<Vector3> route = new List<Vector3>();
        int routeIndex;
        Vector3 routeDest;
        bool hasRoute;
        float repathTimer;

        float thinkTimer;
        float strafeSign = 1f;
        float strafeTimer;
        float plantTimer;
        bool planted;
        float burstTimer;
        bool burstOn;
        float specialRoll;
        float retreatUntil = -99f;
        float nextRetreatAllowed;
        float stuckTimer;
        float reverseTimer;
        float jumpCooldown;
        Vector3 followOffset;
        Vector3 idlePoint;
        float idleWait;
        bool hasIdlePoint;

        enum Plan { Idle, Travel, Fight, Retreat }
        Plan plan;
        Vector3 travelDest;
        float travelThrottle = 1f;

        void Awake()
        {
            commands = GetComponent<RobotCommands>();
            turret = GetComponent<Turret>();
            self = GetComponent<Damageable>();
            weapons = GetComponents<Weapon>();
            locomotion = GetComponent<Locomotion>();
            walker = locomotion is WalkerLocomotion;
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            followOffset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, -0.3f)).normalized * Random.Range(26f, 40f);
            thinkTimer = Random.Range(0f, 0.3f);
        }

        void Start()
        {
            if (self != null)
            {
                self.Damaged += OnDamaged;
                // Footprint: the bigger the machine, the more room it wants round buildings.
                clearance = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(4f, self.size * 3.2f) / NavGrid.Cell + 1f), 1, 3);
            }
            TuneToWeapons();
        }

        void OnDestroy()
        {
            if (self != null) self.Damaged -= OnDamaged;
        }

        /// <summary>Fighting distances follow the guns actually fitted, not just the robot type.</summary>
        void TuneToWeapons()
        {
            foreach (var w in weapons)
            {
                if (w == null || w.slot != Weapon.Slot.Primary) continue;
                float ideal = preferredRange, reach = fireRange;
                switch (w)
                {
                    case Flamethrower _: ideal = 22f; reach = 48f; break;
                    case PlasmaRailgun _: ideal = 150f; reach = 380f; break; // the rail rifle
                    case HeavyCannon _: ideal = 75f; reach = 240f; break;
                    case PulseLaser _: ideal = 80f; reach = 250f; break;
                    case Gatling _: ideal = 60f; reach = 220f; break;
                    case RocketPods _: ideal = 65f; reach = 200f; break;
                    default:
                        if (w is IAiRange r) { ideal = r.IdealRange; reach = r.MaxRange; }
                        break;
                }
                preferredRange = Mathf.Lerp(preferredRange, ideal, 0.7f);
                fireRange = Mathf.Max(reach, preferredRange * 1.5f);
                break;
            }
            if (turret != null) turret.range = Mathf.Max(turret.range, fireRange + 20f, specialRange + 20f);
        }

        // =====================================================================
        // Orders, used by the game modes
        // =====================================================================

        /// <summary>Hold an area: patrol it, fight what comes into it, don't chase far.</summary>
        public void Defend(Vector3 where, float radius, float leashRange, Vector3 watch)
        {
            if (Same(where)) { leash = leashRange; watchDirection = watch; return; }
            follow = null;
            goal = where;
            goalRadius = radius;
            leash = leashRange;
            engageRange = Mathf.Max(leashRange + 60f, 120f);
            watchDirection = watch;
            hasIdlePoint = false;
        }

        /// <summary>Stand inside a capture zone and clear it of enemies.</summary>
        public void Capture(Vector3 where, float radius)
        {
            if (Same(where)) return;
            follow = null;
            goal = where;
            goalRadius = radius * 0.6f;
            leash = radius + 70f;
            engageRange = radius + 90f;
            hasIdlePoint = false;
        }

        bool Same(Vector3 where) => follow == null && goal.HasValue && Flat(goal.Value - where).magnitude < 12f;

        /// <summary>Stay with a robot and fight whatever threatens it.</summary>
        public void Escort(Transform who, float range = 150f)
        {
            follow = who;
            goal = null;
            leash = 0f;
            engageRange = range;
        }

        /// <summary>No orders: find the enemy and fight.</summary>
        public void Hunt()
        {
            follow = null;
            goal = null;
            leash = 0f;
            engageRange = 160f;
            hunt = true;
        }

        /// <summary>Another robot of the team was hit by this attacker: come and help if free.</summary>
        public void Alert(Damageable attacker)
        {
            if (attacker == null || attacker.IsDead || self == null || self.IsDead) return;
            if (target != null && !target.IsDead && targetVisible) return;
            if (!InReach(attacker, out _)) return;
            SetTarget(attacker);
            lastSeenPos = attacker.transform.position;
            lastSeenTarget = Time.time;
            thinkTimer = 0f;
        }

        void OnDamaged(float amount, Vector3 point)
        {
            var attacker = self.LastAttacker;
            if (attacker == null || attacker.IsDead || attacker.team == self.team) return;
            // Shot by someone it isn't fighting: turn on them unless the current fight is going well.
            if (target == null || target.IsDead || !targetVisible || (target != attacker && Random.value < 0.35f))
            {
                if (InReach(attacker, out _) || leash <= 0f) SetTarget(attacker);
                lastSeenPos = attacker.transform.position;
                lastSeenTarget = Time.time;
            }
            // Call it in to teammates close by.
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || d.team != self.team) continue;
                if ((d.transform.position - transform.position).sqrMagnitude > 260f * 260f) continue;
                var b = d.GetComponent<AIBrain>();
                if (b != null) b.Alert(attacker);
            }
        }

        // =====================================================================
        // Thinking
        // =====================================================================

        void Update()
        {
            commands.Clear();
            if (self != null && self.IsDead) return;
            if (startDelay > 0f)
            {
                startDelay -= Time.deltaTime;
                return;
            }

            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f || (target != null && target.IsDead))
            {
                thinkTimer = Random.Range(0.25f, 0.4f);
                Think();
            }

            switch (plan)
            {
                case Plan.Fight: Fight(); break;
                case Plan.Retreat: FollowRoute(1f, true); break;
                case Plan.Travel: FollowRoute(travelThrottle, target != null && targetVisible); break;
                default: Idle(); break;
            }
            Shoot();
            if (jumpCooldown > 0f) jumpCooldown -= Time.deltaTime;
        }

        void Think()
        {
            var picked = PickTarget();
            if (picked != target) SetTarget(picked);
            if (turret != null) turret.preferred = target;

            targetVisible = target != null && CanSee(target);
            if (targetVisible)
            {
                lastSeenTarget = Time.time;
                lastSeenPos = target.transform.position;
            }

            Vector3 pos = transform.position;

            // Badly hurt and alone at the front: fall back to the team for a while.
            if (Time.time < retreatUntil)
            {
                plan = Plan.Retreat;
                SetDestination(RetreatPoint(), 3f);
                return;
            }
            if (self != null && self.HealthFraction < 0.3f && leash <= 0f && Time.time > nextRetreatAllowed
                && Random.value > aggression * 0.8f && target != null && targetVisible)
            {
                Vector3 rp = RetreatPoint();
                if ((rp - pos).sqrMagnitude > 70f * 70f)
                {
                    retreatUntil = Time.time + Random.Range(5f, 8f);
                    nextRetreatAllowed = Time.time + 25f;
                    plan = Plan.Retreat;
                    SetDestination(rp, 0f);
                    return;
                }
            }

            if (target != null)
            {
                float dist = Flat(target.transform.position - pos).magnitude;
                bool tooFar = dist > fireRange * 0.85f;
                bool lostSight = !targetVisible && Time.time - lastSeenTarget > 0.8f;
                if (!tooFar && !lostSight)
                {
                    plan = Plan.Fight;
                    return;
                }
                // Close in (or go round the building in the way) by the route.
                Vector3 dest = targetVisible || Time.time - lastSeenTarget > 6f ? target.transform.position : lastSeenPos;
                if (leash > 0f && goal.HasValue) dest = ClampToLeash(dest);
                plan = Plan.Travel;
                travelThrottle = 1f;
                SetDestination(dest, 2.5f);
                return;
            }

            // Nothing to fight: carry out the orders.
            if (follow != null && follow.gameObject.activeInHierarchy)
            {
                Vector3 dest = follow.position + Quaternion.Euler(0f, follow.eulerAngles.y, 0f) * followOffset;
                if (Flat(dest - pos).magnitude < 16f) { plan = Plan.Idle; hasIdlePoint = false; return; }
                plan = Plan.Travel;
                travelThrottle = Flat(dest - pos).magnitude > 60f ? 1f : 0.6f;
                SetDestination(dest, 1.5f);
                return;
            }
            if (goal.HasValue)
            {
                if (Flat(goal.Value - pos).magnitude < goalRadius + 6f) { plan = Plan.Idle; return; }
                plan = Plan.Travel;
                travelThrottle = 1f;
                SetDestination(goal.Value, 4f);
                return;
            }
            if (hunt)
            {
                var prey = NearestEnemy();
                if (prey != null)
                {
                    Vector3 p = prey.transform.position;
                    Vector3 to = Flat(p - pos);
                    // Spread out while still far away: each robot comes in on its own line.
                    if (to.magnitude > 180f && Mathf.Abs(flankOffset) > 1f)
                        p += Vector3.Cross(Vector3.up, to.normalized) * flankOffset;
                    plan = Plan.Travel;
                    travelThrottle = 1f;
                    SetDestination(p, 4f);
                    return;
                }
            }
            plan = Plan.Idle;
        }

        void SetTarget(Damageable d)
        {
            if (d == target) return;
            target = d;
            targetSince = Time.time;
            targetVisible = false;
            if (turret != null) turret.preferred = d;
            specialRoll = Random.value;
        }

        bool InReach(Damageable d, out float dist)
        {
            Vector3 pos = transform.position;
            dist = Vector3.Distance(d.Center, pos);
            bool errand = (follow != null && follow.gameObject.activeInHierarchy) || goal.HasValue;
            if (!errand) return true;
            float reach = engageRange;
            if (follow != null && Vector3.Distance(d.Center, follow.position) < reach) return true;
            if (goal.HasValue && leash > 0f && Flat(d.transform.position - goal.Value).magnitude < leash + 40f) return true;
            return dist < reach;
        }

        Damageable PickTarget()
        {
            Vector3 pos = transform.position;
            if (priorityTarget != null && !priorityTarget.IsDead && Vector3.Distance(priorityTarget.Center, pos) < Mathf.Max(engageRange, 220f))
            {
                // Attackers still defend themselves when someone is shooting them up close.
                var a = self != null ? self.LastAttacker : null;
                if (a == null || a.IsDead || Vector3.Distance(a.Center, pos) > 70f) return priorityTarget;
            }

            // Escorts help the leader: what it is fighting comes first.
            Damageable leaderTarget = null;
            if (follow != null)
            {
                var lt = follow.GetComponent<Turret>();
                if (lt != null && lt.Target != null && !lt.Target.IsDead) leaderTarget = lt.Target;
            }

            Damageable best = null;
            float bestScore = float.MaxValue;
            var attacker = self != null ? self.LastAttacker : null;
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || (self != null && d.team == self.team)) continue;
                if (d.GetComponent<Locomotion>() == null && d != priorityTarget) continue; // robots, not scenery
                if (!InReach(d, out float dist)) continue;
                if (dist > 700f) continue;
                float score = dist;
                if (d.Shielded) score += 400f;
                if (d == target) score -= 45f;                       // stick with the current fight
                if (d == attacker) score -= 70f;                     // whoever is shooting me
                if (d == leaderTarget) score -= 90f;
                score -= (1f - d.HealthFraction) * 50f;              // finish off the damaged
                if (d == target && targetVisible) score -= 30f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }
            return best;
        }

        Damageable NearestEnemy()
        {
            Damageable best = null;
            float bestD = float.MaxValue;
            Vector3 pos = transform.position;
            foreach (var d in Damageable.All)
            {
                if (d == null || d.IsDead || self == null || d.team == self.team) continue;
                if (d.GetComponent<Locomotion>() == null) continue;
                float dd = (d.transform.position - pos).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = d; }
            }
            return best;
        }

        static readonly RaycastHit[] losHits = new RaycastHit[12];

        Vector3 Eye
        {
            get
            {
                if (turret != null && turret.pitchPivot != null) return turret.pitchPivot.position;
                if (turret != null && turret.yawPivot != null) return turret.yawPivot.position;
                return transform.position + Vector3.up * 6f;
            }
        }

        /// <summary>A clear shot: nothing solid and no teammate between the guns and the target.</summary>
        bool CanSee(Damageable d)
        {
            Vector3 from = Eye;
            Vector3 to = d.Center;
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 1f) return true;
            dir /= len;
            int n = Physics.RaycastNonAlloc(from, dir, losHits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = losHits[i];
                if (self != null && h.rigidbody != null && h.rigidbody == self.Body) continue;
                var hd = h.collider.GetComponentInParent<Damageable>();
                if (hd == d) continue;
                if (hd != null && self != null && hd.team == self.team && hd != self) return false; // a friend in the way
                if (hd == self) continue;
                if (h.rigidbody != null && !h.rigidbody.isKinematic && hd == null) continue; // loose debris
                if (h.distance > len - 3f) continue; // grazing the target's own surroundings
                return false;
            }
            return true;
        }

        Vector3 RetreatPoint()
        {
            Vector3 pos = transform.position;
            Damageable best = null;
            float bestD = float.MaxValue;
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || self == null || d.team != self.team) continue;
                if (d.GetComponent<Locomotion>() == null) continue;
                if (d.HealthFraction < 0.35f) continue;
                float dd = (d.transform.position - pos).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = d; }
            }
            if (best != null) return best.transform.position;
            // Nobody to fall back on: back off away from the threat.
            Vector3 away = target != null ? Flat(pos - target.transform.position).normalized : -transform.forward;
            return pos + away * 90f;
        }

        Vector3 ClampToLeash(Vector3 p)
        {
            if (!goal.HasValue || leash <= 0f) return p;
            Vector3 off = Flat(p - goal.Value);
            if (off.magnitude <= leash) return p;
            return goal.Value + off.normalized * leash;
        }

        // =====================================================================
        // Moving
        // =====================================================================

        void SetDestination(Vector3 dest, float repathEvery)
        {
            travelDest = dest;
            repathTimer -= 0.35f;
            bool moved = !hasRoute || Flat(dest - routeDest).magnitude > 18f;
            if ((moved || repathTimer <= 0f) && NavGrid.CanPathThisFrame)
            {
                NavGrid.Route(transform.position, dest, clearance, route);
                routeIndex = 0;
                routeDest = dest;
                hasRoute = true;
                repathTimer = repathEvery > 0f ? repathEvery + Random.Range(0f, 1f) : 3f;
            }
        }

        /// <summary>Walks the route; returns false when there is nothing left to walk.</summary>
        bool FollowRoute(float throttle, bool faceTarget)
        {
            if (!hasRoute || route.Count == 0) return false;
            Vector3 pos = transform.position;
            while (routeIndex < route.Count - 1 && Flat(route[routeIndex] - pos).magnitude < 11f) routeIndex++;
            Vector3 wp = route[Mathf.Min(routeIndex, route.Count - 1)];
            Vector3 to = Flat(wp - pos);
            float dist = to.magnitude;
            bool last = routeIndex >= route.Count - 1;
            if (last && dist < 6f) return false;
            // Look ahead: once the next leg is in a straight line, cut toward it.
            if (routeIndex < route.Count - 1 && dist < 24f)
            {
                Vector3 next = route[routeIndex + 1];
                if (NavGrid.StraightClear(pos, next, clearance)) to = Vector3.Lerp(to.normalized, Flat(next - pos).normalized, 0.5f) * dist;
            }
            Vector3 dir = to.normalized;
            float slow = last ? Mathf.Clamp01(dist / 25f) * 0.7f + 0.3f : 1f;
            Vector3 face = faceTarget && target != null ? Flat(target.transform.position - pos) : dir;
            Steer(dir, throttle * slow, face);

            // Long straight run: charge (wheels) or sprint (walkers built for it).
            if (locomotion != null && !last && dist > 40f && Vector3.Angle(Flat(transform.forward), dir) < 12f)
            {
                bool far = Flat(travelDest - pos).magnitude > 140f;
                var wl = locomotion as WalkerLocomotion;
                if (far && locomotion.Fuel01 > 0.6f && (usesBoostToCharge || (wl != null && wl.sprintBoost))) commands.boost = true;
            }
            return true;
        }

        void Fight()
        {
            if (target == null) { Idle(); return; }
            Vector3 pos = transform.position;
            Vector3 to = Flat(target.transform.position - pos);
            float dist = to.magnitude;
            Vector3 dirTo = dist > 0.01f ? to / dist : transform.forward;

            // Change side now and then; giant robots also plant their feet and just fire.
            strafeTimer -= Time.deltaTime;
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(3.5f, 8f);
                if (Random.value < 0.45f) strafeSign = -strafeSign;
            }
            plantTimer -= Time.deltaTime;
            if (plantTimer <= 0f)
            {
                planted = walker && Random.value < 0.45f - aggression * 0.2f;
                plantTimer = planted ? Random.Range(1.5f, 3.5f) : Random.Range(3f, 6f);
            }

            float err = (dist - preferredRange) / Mathf.Max(10f, preferredRange);
            Vector3 radial = dirTo * Mathf.Clamp(err, -1f, 1f);
            Vector3 tangent = Vector3.Cross(Vector3.up, dirTo) * strafeSign * Mathf.Lerp(0.45f, 0.8f, aggression);
            if (planted && Mathf.Abs(err) < 0.5f) tangent = Vector3.zero;

            // Don't side-step into a building: try the other way, then just hold.
            if (tangent.sqrMagnitude > 0.01f && !NavGrid.Walkable(pos + tangent.normalized * 18f, 1))
            {
                strafeSign = -strafeSign;
                tangent = -tangent;
                if (!NavGrid.Walkable(pos + tangent.normalized * 18f, 1)) tangent = Vector3.zero;
            }
            if (radial.sqrMagnitude > 0.01f && Vector3.Dot(radial, dirTo) < 0f && !NavGrid.Walkable(pos + radial.normalized * 18f, 1))
                radial = Vector3.zero; // a wall behind: stop backing up

            // Defenders hold their ground: no chasing past the leash.
            if (leash > 0f && goal.HasValue)
            {
                Vector3 off = Flat(pos - goal.Value);
                if (off.magnitude > leash * 0.9f) radial += -off.normalized * 0.8f;
            }

            Vector3 move = radial + tangent;
            float amount = Mathf.Clamp01(move.magnitude);
            if (amount < 0.12f) amount = 0f;
            Vector3 dir = move.sqrMagnitude > 0.0001f ? move.normalized : dirTo;
            if (walker)
            {
                Steer(dir, amount * Mathf.Lerp(0.55f, 0.9f, aggression), dirTo);
            }
            else
            {
                // Wheeled machines can't strafe: they circle, sweeping the turret across.
                Vector3 circle = Vector3.Slerp(dirTo, Vector3.Cross(Vector3.up, dirTo) * strafeSign, Mathf.Clamp01(1.2f - err));
                if (dist < preferredRange * 0.5f) circle = (Vector3.Cross(Vector3.up, dirTo) * strafeSign - dirTo * 0.6f).normalized;
                if (!NavGrid.Walkable(pos + circle * 20f, 1)) { strafeSign = -strafeSign; circle = Vector3.Reflect(circle, dirTo); }
                Steer(circle, 0.65f + aggression * 0.35f, circle);
                if (usesBoostToCharge && dist > preferredRange * 1.3f && locomotion != null && locomotion.Fuel01 > 0.6f
                    && Vector3.Angle(Flat(transform.forward), dirTo) < 15f) commands.boost = true;
            }
        }

        void Idle()
        {
            Vector3 pos = transform.position;
            Vector3 centre = goal ?? (follow != null ? follow.position : pos);
            if (!goal.HasValue && follow == null)
            {
                // Nowhere to be: hold still and scan.
                FaceOnly(watchDirection);
                return;
            }
            // Guards walk a slow beat round their post instead of standing frozen.
            if (!hasIdlePoint || (idleWait <= 0f && Flat(idlePoint - pos).magnitude < 8f))
            {
                if (hasIdlePoint && idleWait <= 0f && Random.value < 0.6f)
                {
                    idleWait = Random.Range(2f, 5f);
                }
                else
                {
                    float r = Mathf.Max(10f, goal.HasValue ? goalRadius * 1.3f : 20f);
                    Vector2 c = Random.insideUnitCircle * r;
                    idlePoint = NavGrid.OpenSpot(centre + new Vector3(c.x, 0f, c.y), clearance);
                    hasIdlePoint = true;
                }
            }
            if (idleWait > 0f)
            {
                idleWait -= Time.deltaTime;
                Vector3 look = watchDirection.sqrMagnitude > 0.01f ? watchDirection : transform.forward;
                // Sweep the gaze a little either side of the watch direction.
                look = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.4f + GetInstanceID()) * 35f, 0f) * look;
                FaceOnly(look);
                return;
            }
            Vector3 to = Flat(idlePoint - pos);
            if (to.magnitude < 6f) { idleWait = Random.Range(1.5f, 4f); return; }
            Steer(to.normalized, 0.4f, to.normalized);
        }

        void FaceOnly(Vector3 dir)
        {
            dir = Flat(dir);
            if (dir.sqrMagnitude < 0.01f) return;
            if (walker)
            {
                commands.hasFace = true;
                commands.face = dir.normalized;
            }
        }

        /// <summary>Turns a wanted flat direction into stick commands, keeping clear of teammates and getting unstuck.</summary>
        void Steer(Vector3 dir, float throttle, Vector3 face)
        {
            Vector3 pos = transform.position;
            // Keep a giant's distance from other robots so the team doesn't bunch up or collide.
            Vector3 push = Vector3.zero;
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || d.GetComponent<Locomotion>() == null) continue;
                Vector3 off = Flat(pos - d.transform.position);
                float m = off.magnitude;
                float keep = 18f + (self != null ? self.size : 2f) * 3f;
                if (m < keep && m > 0.01f) push += off / m * (1f - m / keep);
            }
            if (push.sqrMagnitude > 0.0001f)
            {
                dir = (dir * Mathf.Max(throttle, 0.3f) + push * 0.9f);
                throttle = Mathf.Clamp01(Mathf.Max(throttle, dir.magnitude));
                dir = dir.normalized;
            }

            var rb = self != null ? self.Body : null;
            float speed = rb != null ? Flat(Compat.GetVelocity(rb)).magnitude : 0f;
            if (throttle > 0.4f && speed < 0.8f) stuckTimer += Time.deltaTime;
            else stuckTimer = Mathf.Max(0f, stuckTimer - Time.deltaTime * 2f);
            if (stuckTimer > 1.8f)
            {
                stuckTimer = 0f;
                repathTimer = 0f;
                hasRoute = false;
                if (walker && jumpCooldown <= 0f && locomotion != null && locomotion.Fuel01 > 0.5f)
                {
                    commands.boost = true;   // step up and over
                    jumpCooldown = 6f;
                }
                else reverseTimer = 1.3f;
            }

            if (walker)
            {
                commands.hasMove = true;
                commands.move = dir * Mathf.Clamp01(throttle);
                if (reverseTimer > 0f)
                {
                    reverseTimer -= Time.deltaTime;
                    commands.move = Quaternion.Euler(0f, 120f * strafeSign, 0f) * dir * 0.7f;
                }
                Vector3 f = Flat(face);
                if (f.sqrMagnitude > 0.0001f)
                {
                    commands.hasFace = true;
                    commands.face = f.normalized;
                }
                return;
            }

            Vector3 fwd = Flat(transform.forward).normalized;
            float angle = Vector3.SignedAngle(fwd, dir, Vector3.up);
            commands.steer = Mathf.Clamp(angle / 35f, -1f, 1f);
            commands.throttle = Mathf.Clamp01(1f - Mathf.Abs(angle) / 110f) * throttle;
            if (Mathf.Abs(angle) > 100f && throttle > 0.3f) commands.throttle = 0.35f; // swing round, don't stall
            if (reverseTimer > 0f)
            {
                reverseTimer -= Time.deltaTime;
                commands.throttle = -0.8f;
                commands.steer = -Mathf.Sign(angle == 0f ? strafeSign : angle);
            }
        }

        // =====================================================================
        // Shooting
        // =====================================================================

        void Shoot()
        {
            if (target == null || target.IsDead || turret == null) return;
            // A moment to react after picking a new target, shorter for aggressive pilots.
            if (Time.time - targetSince < Mathf.Lerp(1.0f, 0.4f, aggression)) return;
            bool aimed = turret.Target == target;
            float dist = Vector3.Distance(target.Center, transform.position);

            burstTimer -= Time.deltaTime;
            if (burstTimer <= 0f)
            {
                burstOn = !burstOn;
                burstTimer = burstOn ? Random.Range(1.6f, 3.2f) : Random.Range(0.9f, 1.8f);
            }

            foreach (var w in weapons)
            {
                if (w == null || !w.enabled || w.muzzles.Length == 0 || !aimed) continue;
                float err = turret.AimError(w.muzzles[0]);
                // Close, big targets fill more of the view: allow a wider error.
                float tol = Mathf.Clamp(Mathf.Atan2(target.size * 3f, Mathf.Max(1f, dist)) * Mathf.Rad2Deg, 3f, 12f);
                if (w.slot == Weapon.Slot.Primary)
                {
                    if (targetVisible && dist < fireRange && err < tol && burstOn) commands.primary = true;
                }
                else if (w.IsReady && dist < specialRange)
                {
                    bool indirect = w is MissileSwarm || w is MortarBarrage || (w is IAiRange r && r.Indirect);
                    if (!indirect && (!targetVisible || err > 20f)) continue;
                    if (indirect && dist < 35f) continue; // too close to lob shells
                    // Hold the big shot until a good moment: soon after it's ready, roll once per target.
                    if (specialRoll < 0.15f + aggression * 0.6f) commands.special = true;
                    else specialRoll = Mathf.Max(0f, specialRoll - Time.deltaTime * 0.15f);
                }
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnDisable()
        {
            if (commands != null) commands.Clear();
            if (turret != null) turret.preferred = null;
        }
    }

    /// <summary>Weapons the AI doesn't know by type say how it should use them.</summary>
    public interface IAiRange
    {
        float IdealRange { get; }
        float MaxRange { get; }
        /// <summary>Can hit targets behind cover (lobbed or guided).</summary>
        bool Indirect { get; }
    }
}
