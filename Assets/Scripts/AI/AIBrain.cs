using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Simple combat AI: closes to its preferred range, circles the target,
    /// fires when its guns line up, uses its special weapon and boost,
    /// and steers around buildings.
    /// </summary>
    [RequireComponent(typeof(RobotCommands))]
    public class AIBrain : MonoBehaviour, IDisableOnDeath
    {
        public float preferredRange = 60f;
        public float fireRange = 180f;
        public float specialRange = 140f;
        [Range(0f, 1f)] public float aggression = 0.6f;
        public bool usesBoostToCharge;

        [Header("Team play")]
        /// <summary>Stay near this robot (allies follow the player) when nothing is in reach.</summary>
        public Transform follow;
        /// <summary>Where to go when nothing is in reach (capture point, depot, reactor).</summary>
        public Vector3? goal;
        public float goalRadius = 14f;
        /// <summary>Only enemies this close are engaged when a goal or leader is set.</summary>
        public float engageRange = 160f;
        /// <summary>Preferred target (the reactor, the commander) when it is within reach.</summary>
        public Damageable priorityTarget;
        public float startDelay = 4f;

        RobotCommands commands;
        Turret turret;
        Damageable self;
        Weapon[] weapons;
        Locomotion locomotion;
        Damageable target;
        float strafeSign = 1f;
        float strafeTimer;
        float stuckTimer;
        float reverseTimer;
        float thinkTimer;
        float burstTimer;
        bool burstOn;
        Vector3 followOffset;

        void Awake()
        {
            commands = GetComponent<RobotCommands>();
            turret = GetComponent<Turret>();
            self = GetComponent<Damageable>();
            weapons = GetComponents<Weapon>();
            locomotion = GetComponent<Locomotion>();
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            followOffset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, -0.3f)).normalized * Random.Range(22f, 34f);
        }

        void Update()
        {
            commands.Clear();
            if (startDelay > 0f)
            {
                startDelay -= Time.deltaTime;
                return;
            }

            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f || target == null || target.IsDead)
            {
                thinkTimer = 0.5f;
                target = PickTarget();
            }
            if (target == null)
            {
                MoveToGoal();
                return;
            }

            Vector3 pos = transform.position;
            Vector3 to = target.Center - pos;
            Vector3 flatTo = Vector3.ProjectOnPlane(to, Vector3.up);
            float dist = flatTo.magnitude;

            // Change circling direction now and then.
            strafeTimer -= Time.deltaTime;
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(4f, 9f);
                if (Random.value < 0.5f) strafeSign = -strafeSign;
            }

            // Desired heading: straight at the target when far, circling when close.
            Vector3 dirToTarget = flatTo.normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, dirToTarget) * strafeSign;
            float closeness = Mathf.Clamp01((preferredRange * 1.4f - dist) / preferredRange);
            Vector3 desired = Vector3.Slerp(dirToTarget, tangent, closeness * 0.85f);
            if (dist < preferredRange * 0.5f) desired = (tangent - dirToTarget * 0.6f).normalized;

            float angle = Drive(desired, 0.6f + aggression * 0.4f);

            // Weapons: fire in bursts with pauses, so the player gets breathing room.
            burstTimer -= Time.deltaTime;
            if (burstTimer <= 0f)
            {
                burstOn = !burstOn;
                burstTimer = burstOn ? Random.Range(1.5f, 3f) : Random.Range(1.2f, 2.5f);
            }
            bool onTarget = turret != null && turret.Target == target;
            foreach (var w in weapons)
            {
                if (w == null || w.muzzles.Length == 0 || !onTarget) continue;
                float err = turret.AimError(w.muzzles[0]);
                if (w.slot == Weapon.Slot.Primary && dist < fireRange && err < 6f && burstOn) commands.primary = true;
                if (w.slot == Weapon.Slot.Special && dist < specialRange && err < 25f && w.IsReady && Random.value < (0.004f + aggression * 0.006f) * Time.deltaTime * 60f)
                    commands.special = true;
            }

            // Boost: wheeled bots charge in; walkers hop when stuck or under fire at close range.
            if (locomotion != null)
            {
                if (usesBoostToCharge && dist > preferredRange && Mathf.Abs(angle) < 15f && locomotion.Fuel01 > 0.6f) commands.boost = true;
                if (!usesBoostToCharge && reverseTimer > 0f && locomotion.Fuel01 > 0.8f) commands.boost = true;
            }
        }

        /// <summary>Steers towards a flat direction, avoiding walls and backing out when stuck. Returns the heading error.</summary>
        float Drive(Vector3 desired, float throttle)
        {
            Vector3 pos = transform.position;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (Physics.SphereCast(pos + Vector3.up * 3f, 3f, fwd, out RaycastHit hit, 26f, ~0, QueryTriggerInteraction.Ignore)
                && (hit.rigidbody == null || hit.rigidbody.isKinematic))
            {
                Vector3 away = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                desired = (desired + away * 1.5f).normalized;
            }

            float angle = Vector3.SignedAngle(fwd, desired, Vector3.up);
            commands.steer = Mathf.Clamp(angle / 35f, -1f, 1f);
            commands.throttle = Mathf.Clamp01(1f - Mathf.Abs(angle) / 120f) * throttle;

            // Unstick: if pushing but not moving, back up and turn.
            var rb = self != null ? self.Body : null;
            float speed = rb != null ? Compat.GetVelocity(rb).magnitude : 0f;
            if (commands.throttle > 0.4f && speed < 1f) stuckTimer += Time.deltaTime;
            else stuckTimer = Mathf.Max(0f, stuckTimer - Time.deltaTime);
            if (stuckTimer > 2f)
            {
                stuckTimer = 0f;
                reverseTimer = 1.5f;
            }
            if (reverseTimer > 0f)
            {
                reverseTimer -= Time.deltaTime;
                commands.throttle = -0.8f;
                commands.steer = strafeSign;
            }
            return angle;
        }

        /// <summary>No enemy in reach: head for the goal or stay with the leader.</summary>
        void MoveToGoal()
        {
            Vector3 dest;
            float radius = goalRadius;
            if (follow != null && follow.gameObject.activeInHierarchy)
            {
                dest = follow.position + follow.rotation * followOffset;
                radius = 12f;
            }
            else if (goal.HasValue) dest = goal.Value;
            else return;

            Vector3 flat = Vector3.ProjectOnPlane(dest - transform.position, Vector3.up);
            float dist = flat.magnitude;
            if (dist < radius)
            {
                // Arrived: drift slowly in a circle so the robot doesn't look frozen.
                if (goal.HasValue && follow == null)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, flat.normalized) * strafeSign;
                    Drive((tangent + flat.normalized * 0.3f).normalized, 0.25f);
                }
                return;
            }
            float angle = Drive(flat / dist, dist > radius * 3f ? 0.9f : 0.5f);
            if (locomotion != null && usesBoostToCharge && dist > 90f && Mathf.Abs(angle) < 10f && locomotion.Fuel01 > 0.7f) commands.boost = true;
        }

        bool HasErrand => (follow != null && follow.gameObject.activeInHierarchy) || goal.HasValue;

        Damageable PickTarget()
        {
            Vector3 pos = transform.position;
            float reach = HasErrand ? engageRange : float.MaxValue;
            if (priorityTarget != null && !priorityTarget.IsDead && Vector3.Distance(priorityTarget.Center, pos) < Mathf.Max(reach, 200f))
                return priorityTarget;

            // When following the leader, also defend it: anything near the leader counts as near.
            Vector3 leader = follow != null ? follow.position : pos;
            Damageable best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || (self != null && d.team == self.team)) continue;
                float dist = Vector3.Distance(d.Center, pos);
                float distLeader = follow != null ? Vector3.Distance(d.Center, leader) : dist;
                if (Mathf.Min(dist, distLeader) > reach) continue;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = d;
                }
            }
            return best;
        }

        void OnDisable()
        {
            if (commands != null) commands.Clear();
        }
    }
}
