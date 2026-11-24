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
        public Color color = new Color(1f, 0.6f, 0.2f);

        protected Turret turret;
        protected Damageable owner;
        protected RobotCommands commands;
        protected Rigidbody body;
        protected float lastFire = -999f;
        int muzzleIndex;

        public float Ready01 => Mathf.Clamp01((Time.time - lastFire) / Mathf.Max(0.01f, cooldown));
        public bool IsReady => Time.time >= lastFire + cooldown;
        protected bool IsPlayer => GetComponent<RobotInput>() != null;

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
            if (Triggered && IsReady)
            {
                lastFire = Time.time;
                Fire();
            }
        }

        protected abstract void Fire();

        protected Transform NextMuzzle()
        {
            if (muzzles.Length == 0) return transform;
            var m = muzzles[muzzleIndex % muzzles.Length];
            muzzleIndex++;
            return m != null ? m : transform;
        }

        protected Vector3 AimDirection(Transform muzzle)
        {
            if (turret == null) return muzzle.forward;
            Vector3 wanted = (turret.AimPoint - muzzle.position).normalized;
            return Vector3.RotateTowards(muzzle.forward, wanted, aimAssist * Mathf.Deg2Rad, 0f).normalized;
        }

        protected void Recoil(Vector3 dir, Vector3 at, float speedChange)
        {
            if (body == null) return;
            body.AddForceAtPosition(-dir * speedChange * body.mass, at, ForceMode.Impulse);
        }

        /// <summary>Raycast that skips the owner's own colliders.</summary>
        protected bool HitScan(Vector3 from, Vector3 dir, float range, out RaycastHit hit)
        {
            return GroundProbe.Cast(from, dir, range, body, out hit);
        }
    }
}
