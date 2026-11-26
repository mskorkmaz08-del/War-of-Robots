using System.Collections;
using UnityEngine;

namespace RobotArena
{
    /// <summary>Arm cannons firing heavy explosive shells, alternating left and right.</summary>
    public class HeavyCannon : Weapon
    {
        public float shellSpeed = 110f;
        public float damage = 110f;
        public float blastRadius = 7f;
        public float recoil = 0.9f;

        protected override void Fire()
        {
            var muzzle = NextMuzzle();
            Vector3 dir = AimDirection(muzzle);
            Vector3 inherit = body != null ? Compat.GetVelocity(body) : Vector3.zero;
            var shell = Shell.Spawn(muzzle.position + dir * 1.5f, dir * shellSpeed + inherit, owner, color);
            shell.damage = damage;
            shell.radius = blastRadius;

            Fx.MuzzleFlash(muzzle.position, dir, 1.6f);
            GameAudio.Play(Sfx.Cannon, muzzle.position, 1f, Random.Range(0.9f, 1.05f));
            FollowCamera.Shake(IsPlayer ? 0.28f : 0.1f, muzzle.position, 80f);
            Recoil(dir, muzzle.position, recoil);
            KickBarrel(muzzle);
        }

        void KickBarrel(Transform muzzle)
        {
            // Visible barrel recoil: the muzzle's parent slides back and returns.
            var barrel = muzzle.parent;
            if (barrel != null) CoroutineHost.Run(Kick(barrel));
        }

        static IEnumerator Kick(Transform barrel)
        {
            Vector3 rest = barrel.localPosition;
            float t = 0f;
            while (t < 0.35f && barrel != null)
            {
                t += Time.deltaTime;
                float k = t < 0.05f ? t / 0.05f : 1f - (t - 0.05f) / 0.3f;
                barrel.localPosition = rest + Vector3.back * 1.1f * Mathf.Clamp01(k);
                yield return null;
            }
            if (barrel != null) barrel.localPosition = rest;
        }
    }

    /// <summary>Shoulder pods that ripple-fire a swarm of homing missiles.</summary>
    public class MissileSwarm : Weapon
    {
        public int missilesPerSalvo = 8;
        public float interval = 0.09f;
        public float damagePerMissile = 55f;

        protected override void Fire()
        {
            StartCoroutine(Salvo());
        }

        IEnumerator Salvo()
        {
            var target = turret != null ? turret.Target : null;
            for (int i = 0; i < missilesPerSalvo; i++)
            {
                if (!enabled) yield break;
                var tube = NextMuzzle();
                Vector3 launch = (tube.forward + Random.insideUnitSphere * 0.15f).normalized;
                var m = Missile.Spawn(tube.position + tube.forward * 0.8f, launch, owner, target);
                m.damage = damagePerMissile;
                if (target == null && turret != null) m.aimPoint = turret.AimPoint + Random.insideUnitSphere * 3f;
                Fx.MuzzleFlash(tube.position, tube.forward, 0.6f);
                GameAudio.Play(Sfx.MissileLaunch, tube.position, 0.8f, Random.Range(0.9f, 1.15f));
                if (IsPlayer) FollowCamera.Shake(0.06f, tube.position, 60f);
                yield return new WaitForSeconds(interval);
            }
        }
    }

    /// <summary>Rapid-fire twin energy cannons (instant hit).</summary>
    public class PulseLaser : Weapon
    {
        public float damage = 16f;
        public float range = 260f;

        protected override void Fire()
        {
            var muzzle = NextMuzzle();
            Vector3 dir = AimDirection(muzzle);
            Vector3 from = muzzle.position;
            Vector3 to = from + dir * range;

            if (HitScan(from, dir, range, out RaycastHit hit))
            {
                to = hit.point;
                var d = hit.collider.GetComponentInParent<Damageable>();
                if (d != null) d.TakeDamage(damage, hit.point, owner);
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    hit.rigidbody.AddForceAtPosition(dir * Mathf.Min(2f, 400f / hit.rigidbody.mass) * (d != null ? 0.5f : 1f), hit.point, ForceMode.VelocityChange);
                Fx.Sparks(hit.point, hit.normal, 10, 18f, 70f);
                Fx.EnergyBurst(hit.point, color, 0.5f);
                if (Random.value < 0.5f) GameAudio.Play(Sfx.Sizzle, hit.point, 0.5f, Random.Range(0.9f, 1.2f));
            }

            Beam.Spawn(from, to, 0.5f, color, 0.08f);
            Fx.EnergyBurst(from, color, 0.35f);
            GameAudio.Play(Sfx.Laser, from, 0.55f, Random.Range(0.9f, 1.1f));
        }
    }

    /// <summary>
    /// Super weapon: charges up, then fires a devastating plasma rail shot that
    /// blasts targets backwards and shoves its own robot with recoil.
    /// </summary>
    public class PlasmaRailgun : Weapon
    {
        public float chargeTime = 1.3f;
        public float damage = 380f;
        public float range = 420f;
        public float knockback = 9f;
        public Transform chargeGlow;

        bool charging;
        float chargeStart;
        int shot;

        public bool IsCharging => charging;
        // Arm-mounted rifles alternate between the two arms.
        Transform Current => muzzles.Length > 0 ? muzzles[shot % muzzles.Length] : transform;

        protected override void Update()
        {
            if (charging)
            {
                float k = Mathf.Clamp01((Time.time - chargeStart) / chargeTime);
                if (chargeGlow != null) chargeGlow.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.8f, k) * (1f + Mathf.Sin(Time.time * 40f) * 0.08f);
                if (Random.value < 0.25f) Fx.EnergyBurst(Current.position, color, 0.25f + k * 0.5f);
                if (k >= 1f) Release();
                return;
            }

            if (chargeGlow != null) chargeGlow.localScale = Vector3.MoveTowards(chargeGlow.localScale, Vector3.one * 0.2f, Time.deltaTime * 4f);
            base.Update();
        }

        protected override void Fire()
        {
            charging = true;
            chargeStart = Time.time;
            var m = Current;
            GameAudio.Play(Sfx.RailCharge, m.position, 0.9f, 1f);
        }

        void Release()
        {
            charging = false;
            lastFire = Time.time;
            var muzzle = Current;
            shot++;
            Vector3 dir = AimDirection(muzzle);
            Vector3 from = muzzle.position;
            Vector3 to = from + dir * range;

            if (HitScan(from, dir, range, out RaycastHit hit))
            {
                to = hit.point;
                var d = hit.collider.GetComponentInParent<Damageable>();
                if (d != null)
                {
                    d.TakeDamage(damage, hit.point, owner);
                    if (d.Body != null) d.Body.AddForceAtPosition((dir * knockback + Vector3.up * 2f) * 0.5f, Vector3.Lerp(hit.point, d.Body.worldCenterOfMass, 0.5f), ForceMode.VelocityChange);
                }
                Explosions.Detonate(hit.point, 10f, 70f, 16f, owner, 2f);
            }

            Beam.Spawn(from, to, 2.4f, color, 0.5f);
            Beam.Spawn(from, to, 0.8f, Color.white, 0.35f);
            Fx.EnergyBurst(from, color, 2f);
            Fx.MuzzleFlash(from, dir, 2.5f);
            GameAudio.Play(Sfx.RailFire, from, 1f, 1f);
            FollowCamera.Shake(IsPlayer ? 0.75f : 0.3f, from, 150f);
            if (body != null) body.AddForce(-dir * 0.8f, ForceMode.VelocityChange);
        }

        void OnDisable()
        {
            charging = false;
        }
    }
}
