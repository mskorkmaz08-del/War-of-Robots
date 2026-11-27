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

        [Tooltip("Rounds per magazine. 0 = no magazine (lasers, plasma, specials fire on their own recharge).")]
        public int magazine;
        [Tooltip("Seconds to reload a full magazine.")]
        public float reloadTime = 3f;

        /// <summary>Rounds left in the magazine (only meaningful when magazine > 0).</summary>
        public int Ammo { get; protected set; }
        public bool HasMagazine => magazine > 0;
        public bool Reloading => HasMagazine && Time.time < reloadEnd;
        public float Reload01 => Reloading ? 1f - (reloadEnd - Time.time) / Mathf.Max(0.01f, reloadTime) : 1f;
        public float Ammo01 => HasMagazine ? (Reloading ? Reload01 : Ammo / (float)magazine) : 1f;
        float reloadEnd = -1f;
        bool reloadPending;

        public float Ready01 => Mathf.Clamp01((Time.time - lastFire) / Mathf.Max(0.01f, cooldown));
        public bool IsReady => Time.time >= lastFire + cooldown && !Reloading;
        bool? isPlayer;
        protected bool IsPlayer => isPlayer ??= GetComponent<RobotInput>() != null;

        protected virtual void Awake()
        {
            turret = GetComponent<Turret>();
            owner = GetComponent<Damageable>();
            commands = GetComponent<RobotCommands>();
            body = GetComponent<Rigidbody>();
            Magazines.Configure(this);
            Ammo = magazine;
        }

        protected bool Triggered => slot == Slot.Primary ? commands.primary : commands.special;

        protected virtual void Update()
        {
            TickMagazine();
            if (Triggered && IsReady && !TurretStillSwinging())
            {
                lastFire = Time.time;
                if (owner != null) owner.DropShield();
                Fire();
                UseRound();
            }
        }

        protected virtual void OnEnable() { Ammo = magazine; reloadEnd = -1f; reloadPending = false; }

        /// <summary>
        /// Guns with a magazine reload on their own: when it runs dry, or quietly
        /// topping up after a couple of seconds without firing.
        /// </summary>
        protected void TickMagazine()
        {
            if (!HasMagazine) return;
            if (reloadPending && !Reloading)
            {
                reloadPending = false;
                Ammo = magazine;
                if (IsPlayer && muzzles.Length > 0 && muzzles[0] != null)
                    GameAudio.Play(Sfx.MetalHit, muzzles[0].position, 0.35f, 1.35f);
            }
            if (!Reloading && Ammo < magazine && !Triggered && Time.time - lastFire > 2.5f)
                StartReload();
        }

        /// <summary>Takes one round; starts the reload when the magazine is empty.</summary>
        protected void UseRound()
        {
            if (!HasMagazine) return;
            Ammo = Mathf.Max(0, Ammo - 1);
            if (Ammo == 0) StartReload();
        }

        public void StartReload()
        {
            if (!HasMagazine || Reloading || Ammo >= magazine) return;
            // A partly used magazine reloads faster than an empty one.
            float k = Mathf.Lerp(0.5f, 1f, 1f - Ammo / (float)magazine);
            reloadEnd = Time.time + reloadTime * k;
            reloadPending = true;
            if (muzzles.Length > 0 && muzzles[0] != null)
                GameAudio.Play(Sfx.Servo, muzzles[0].position, IsPlayer ? 0.5f : 0.3f, 0.8f);
        }

        protected abstract void Fire();

        /// <summary>
        /// Player only: while the guns are still swinging onto a locked enemy,
        /// wait instead of spraying shots into the scenery.
        /// </summary>
        bool TurretStillSwinging()
        {
            if (slot != Slot.Primary || !IsPlayer || turret == null || turret.Target == null || muzzles.Length == 0 || muzzles[0] == null)
            {
                waitingSince = -1f;
                return false;
            }
            // Only guns that swing with the turret wait. Missile tubes and mortar
            // racks are fixed to the hull and guide their shots themselves.
            Transform aimed = turret.pitchPivot != null ? turret.pitchPivot : turret.yawPivot;
            if (aimed == null || !muzzles[0].IsChildOf(aimed)) return false;
            if (turret.AimError(muzzles[0]) <= 25f) { waitingSince = -1f; return false; }
            // Never hold fire for long, whatever the geometry.
            if (waitingSince < 0f) waitingSince = Time.time;
            return Time.time - waitingSince < 0.4f;
        }

        float waitingSince = -1f;

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
            // A giant absorbs its own recoil: a small push through the centre of mass, no rocking.
            body.AddForce(-dir * speedChange * 0.15f, ForceMode.VelocityChange);
        }

        /// <summary>Raycast that skips the owner's own colliders.</summary>
        protected bool HitScan(Vector3 from, Vector3 dir, float range, out RaycastHit hit)
        {
            return GroundProbe.Cast(from, dir, range, body, out hit);
        }
    }
}
