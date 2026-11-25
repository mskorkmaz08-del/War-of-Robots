using UnityEngine;

namespace RobotArena
{
    /// <summary>Base for all robot weapons.</summary>
    [RequireComponent(typeof(RobotCommands))]
    public abstract class Weapon : MonoBehaviour, IDisableOnDeath
    {
        public enum Slot { Primary, Special }

        public string displayName = "Weapon";
        public Slot slot = Slot.Primary;
        public Transform[] muzzles = new Transform[0];
        public float cooldown = 1f;
        [Tooltip("How far (degrees) shots may bend from the barrel toward the aim point.")]
        public float aimAssist = 8f;
        [Tooltip("Random inaccuracy in degrees (used to make enemies miss sometimes).")]
        public float spread;
        public Color color = new Color(1f, 0.6f, 0.2f);

        protected Turret turret;
        protected Damageable owner;
        protected RobotCommands commands;
        protected Rigidbody body;
        protected float lastFire = -999f;
        int muzzleIndex;

        public float Ready01 => Mathf.Clamp01((Time.time - lastFire) / Mathf.Max(0.01f, cooldown));
        public bool IsReady => Time.time >= lastFire + cooldown;
        bool? isPlayer;
        protected bool IsPlayer => isPlayer ??= GetComponent<RobotInput>() != null;

        protected virtual void Awake()
        {
            turret = GetComponent<Turret>();
            owner = GetComponent<Damageable>();
            commands = GetComponent<RobotCommands>();
            body = GetComponent<Rigidbody>();
        }

        protected bool Triggered => slot == Slot.Primary ? commands.primary : commands.special;

        protected virtual void Update()
        {
            if (Triggered && IsReady && !TurretStillSwinging())
            {
                lastFire = Time.time;
                Fire();
            }
        }

        protected abstract void Fire();

        /// <summary>
        /// Player only: while the guns are still swinging onto a locked enemy,
        /// wait instead of spraying shots into the scenery.
        /// </summary>
        bool TurretStillSwinging()
        {
            if (!IsPlayer || turret == null || turret.Target == null || muzzles.Length == 0 || muzzles[0] == null) return false;
            return turret.AimError(muzzles[0]) > 25f;
        }

        protected Transform NextMuzzle()
        {
            if (muzzles.Length == 0) return transform;
            var m = muzzles[muzzleIndex % muzzles.Length];
            muzzleIndex++;
            return m != null ? m : transform;
        }

        protected Vector3 AimDirection(Transform muzzle)
        {
            Vector3 dir = muzzle.forward;
            if (turret != null)
            {
                Vector3 wanted = (turret.AimPoint - muzzle.position).normalized;
                float bend = IsPlayer ? Mathf.Max(aimAssist, 25f) : aimAssist;
                dir = Vector3.RotateTowards(muzzle.forward, wanted, bend * Mathf.Deg2Rad, 0f).normalized;
            }
            if (spread > 0f)
                dir = Quaternion.AngleAxis(Random.Range(-spread, spread), Random.onUnitSphere) * dir;
            return dir.normalized;
        }

        protected void Recoil(Vector3 dir, Vector3 at, float speedChange)
        {
            if (body == null) return;
            body.AddForceAtPosition(-dir * speedChange * 0.5f * body.mass, at, ForceMode.Impulse);
        }

        /// <summary>Raycast that skips the owner's own colliders.</summary>
        protected bool HitScan(Vector3 from, Vector3 dir, float range, out RaycastHit hit)
        {
            return GroundProbe.Cast(from, dir, range, body, out hit);
        }
    }
}
