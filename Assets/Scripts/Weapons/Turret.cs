using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Finds the nearest enemy in front of the robot and swings the upper body
    /// (yaw) and gun arms (pitch) toward it, leading moving targets.
    /// Auto-aim keeps the game playable on a phone.
    /// </summary>
    public class Turret : MonoBehaviour, IDisableOnDeath
    {
        public Transform yawPivot;
        public Transform pitchPivot;
        public float yawSpeed = 60f;
        public float pitchSpeed = 35f;
        public float maxYaw = 100f;
        public float minPitch = -25f;
        public float maxPitch = 30f;

        [Header("Targeting")]
        public float range = 260f;
        [Tooltip("Half-angle in front of the hull where targets are picked up.")]
        public float acquireCone = 80f;
        [Tooltip("Speed of the main gun's projectile, used to lead targets. 0 = instant.")]
        public float projectileSpeed;
        public bool compensateGravity;
        [Tooltip("Player robots aim where this camera looks, locking onto enemies near the crosshair.")]
        public FollowCamera aimCamera;
        public float crosshairLockAngle = 13f;

        /// <summary>Online: aim comes from the network instead of targeting here.</summary>
        public bool netControlled;
        public Vector3 netAim;

        public Damageable Target { get; private set; }
        public Vector3 AimPoint { get; private set; }

        Damageable self;
        float retargetTimer;
        float keepLockUntil;
        float yaw;
        float pitch;

        void Awake()
        {
            self = GetComponent<Damageable>();
        }

        void Update()
        {
            retargetTimer -= Time.deltaTime;
            if (netControlled)
            {
                Target = null;
                retargetTimer = 1f;
            }
            else if (retargetTimer <= 0f || Target == null || Target.IsDead)
            {
                retargetTimer = aimCamera != null ? 0.1f : 0.3f;
                Target = aimCamera != null ? FindTargetNearCrosshair() : FindTarget();
            }

            Vector3 origin = pitchPivot != null ? pitchPivot.position : transform.position;
            if (netControlled) AimPoint = netAim;
            else if (Target != null)
            {
                Vector3 aim = Target.Center;
                if (projectileSpeed > 0f)
                {
                    float t = Vector3.Distance(origin, aim) / projectileSpeed;
                    Vector3 v = Target.Body != null ? Compat.GetVelocity(Target.Body) : Vector3.zero;
                    aim += v * t * 0.85f;
                    if (compensateGravity) aim += Vector3.up * 0.5f * Physics.gravity.magnitude * t * t;
                }
                AimPoint = aim;
            }
            else if (aimCamera != null)
            {
                // Aim at whatever is under the crosshair.
                Ray ray = aimCamera.AimRay;
                AimPoint = GroundProbe.Cast(ray.origin, ray.direction, 600f, self != null ? self.Body : null, out RaycastHit hit)
                    ? hit.point
                    : ray.origin + ray.direction * 600f;
            }
            else
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                AimPoint = origin + flatForward * 120f;
            }

            if (yawPivot != null)
            {
                Vector3 local = transform.InverseTransformDirection(AimPoint - yawPivot.position);
                float wantedYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
                yaw = Mathf.MoveTowardsAngle(yaw, wantedYaw, yawSpeed * Time.deltaTime);
                // Hit flinch: a damped spring that rocks the upper body and settles back.
                flinchVel += (-flinch * 90f - flinchVel * 9f) * Time.deltaTime;
                flinch += flinchVel * Time.deltaTime;
                yawPivot.localRotation = Quaternion.Euler(flinch.x, yaw + flinch.y, flinch.z);
            }

            if (pitchPivot != null)
            {
                Transform space = yawPivot != null ? yawPivot : transform;
                Vector3 local = space.InverseTransformDirection(AimPoint - pitchPivot.position);
                float wantedPitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                wantedPitch = Mathf.Clamp(wantedPitch, -maxPitch, -minPitch);
                pitch = Mathf.MoveTowardsAngle(pitch, wantedPitch, pitchSpeed * Time.deltaTime);
                pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        Vector3 flinch, flinchVel;

        /// <summary>Kicks the upper body (degrees per second of pitch, yaw, roll); it rocks and settles back.</summary>
        public void Flinch(Vector3 kick)
        {
            flinchVel += kick;
        }

        /// <summary>Angle in degrees between where a muzzle points and the aim point.</summary>
        public float AimError(Transform muzzle)
        {
            if (muzzle == null) return 180f;
            return Vector3.Angle(muzzle.forward, AimPoint - muzzle.position);
        }

        /// <summary>The enemy closest to the centre of the screen, if any is near it.</summary>
        Damageable FindTargetNearCrosshair()
        {
            Damageable best = null;
            float bestAngle = crosshairLockAngle;
            Ray ray = aimCamera.AimRay;
            // Sticky lock: keep the current enemy while it stays fairly near the
            // crosshair, so strafing or backing up doesn't flick the aim elsewhere.
            Damageable current = Target;
            if (current != null && !current.IsDead && Vector3.Distance(current.Center, transform.position) <= range)
            {
                float a = CrosshairAngle(ray, current);
                if (a < crosshairLockAngle * 2.2f)
                {
                    best = current;
                    bestAngle = Mathf.Min(bestAngle, a) - 5f; // others must be clearly closer
                    keepLockUntil = Time.time + 0.6f;
                }
                else if (Time.time < keepLockUntil)
                {
                    // Briefly slid off (a sharp step or a jolt): hold the lock a moment
                    // unless another enemy is right under the crosshair.
                    best = current;
                }
            }
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || d == current) continue;
                if (self != null && d.team == self.team) continue;
                if (Vector3.Distance(d.Center, transform.position) > range) continue;
                float angle = CrosshairAngle(ray, d);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = d;
                }
            }
            return best;
        }

        static float CrosshairAngle(Ray ray, Damageable d)
        {
            // Big robots are easier to lock: allow a wider angle when they're close.
            Vector3 to = d.Center - ray.origin;
            return Vector3.Angle(ray.direction, to) - Mathf.Atan2(d.size * 6f, to.magnitude) * Mathf.Rad2Deg;
        }

        Damageable FindTarget()
        {
            Damageable best = null;
            float bestScore = float.MaxValue;
            Vector3 pos = transform.position;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead) continue;
                if (self != null && d.team == self.team) continue;
                Vector3 to = d.Center - pos;
                float dist = to.magnitude;
                if (dist > range) continue;
                float angle = Vector3.Angle(fwd, Vector3.ProjectOnPlane(to, Vector3.up));
                if (angle > acquireCone) continue;
                float score = dist + angle * 1.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }
            return best;
        }
    }
}
