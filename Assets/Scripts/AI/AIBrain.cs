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
        float startDelay = 2.5f;

        void Awake()
        {
            commands = GetComponent<RobotCommands>();
            turret = GetComponent<Turret>();
            self = GetComponent<Damageable>();
            weapons = GetComponents<Weapon>();
            locomotion = GetComponent<Locomotion>();
            strafeSign = Random.value < 0.5f ? -1f : 1f;
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
            if (target == null) return;

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

            // Avoid obstacles ahead.
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (Physics.SphereCast(pos + Vector3.up * 3f, 3f, fwd, out RaycastHit hit, 26f, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
            {
                Vector3 away = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                desired = (desired + away * 1.5f).normalized;
            }

            float angle = Vector3.SignedAngle(fwd, desired, Vector3.up);
            commands.steer = Mathf.Clamp(angle / 35f, -1f, 1f);
            commands.throttle = Mathf.Clamp01(1f - Mathf.Abs(angle) / 120f) * (0.6f + aggression * 0.4f);

            // Unstick: if pushing but not moving, back up and turn.
            float speed = GetComponent<Rigidbody>() != null ? Compat.GetVelocity(GetComponent<Rigidbody>()).magnitude : 0f;
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

            // Weapons.
            bool onTarget = turret != null && turret.Target == target;
            foreach (var w in weapons)
            {
                if (w == null || w.muzzles.Length == 0 || !onTarget) continue;
                float err = turret.AimError(w.muzzles[0]);
                if (w.slot == Weapon.Slot.Primary && dist < fireRange && err < 12f) commands.primary = true;
                if (w.slot == Weapon.Slot.Special && dist < specialRange && err < 25f && w.IsReady && Random.value < 0.02f + aggression * 0.03f)
                    commands.special = true;
            }

            // Boost: wheeled bots charge in; walkers hop when stuck or under fire at close range.
            if (locomotion != null)
            {
                if (usesBoostToCharge && dist > preferredRange && Mathf.Abs(angle) < 15f && locomotion.Fuel01 > 0.6f) commands.boost = true;
                if (!usesBoostToCharge && reverseTimer > 0f && locomotion.Fuel01 > 0.8f) commands.boost = true;
            }
        }

        Damageable PickTarget()
        {
            Damageable best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || (self != null && d.team == self.team)) continue;
                float dist = Vector3.Distance(d.Center, transform.position);
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
