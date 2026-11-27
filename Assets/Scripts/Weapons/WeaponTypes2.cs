using System.Collections;
using UnityEngine;

namespace RobotArena
{
    /// <summary>Spinning multi-barrel gun: a stream of tracer rounds.</summary>
    public class Gatling : Weapon
    {
        public float damage = 10f;
        public float range = 240f;
        public Transform[] spinners = new Transform[0];
        float spinSpeed;
        AudioSource fireLoop;

        void Start()
        {
            fireLoop = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(fireLoop, 25f, 380f);
            fireLoop.clip = GameAudio.GatlingLoop();
            fireLoop.loop = true;
            fireLoop.volume = 0f;
            fireLoop.Play();
        }

        void OnDisable() { if (fireLoop != null) fireLoop.volume = 0f; }

        protected override void Update()
        {
            // Barrels spin up while firing and wind down after, and while reloading.
            TickMagazine();
            bool shooting = Triggered && !Reloading;
            spinSpeed = Mathf.MoveTowards(spinSpeed, shooting ? 1440f : 0f, Time.deltaTime * 1800f);
            foreach (var s in spinners) if (s != null) s.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
            bool firing = shooting && spinSpeed >= 700f;
            if (fireLoop != null) fireLoop.volume = Mathf.MoveTowards(fireLoop.volume, firing ? 0.75f : 0f, Time.deltaTime * (firing ? 12f : 6f));
            if (shooting && spinSpeed < 700f) return; // spinning up
            base.Update();
        }

        protected override void Fire()
        {
            var muzzle = NextMuzzle();
            Vector3 dir = AimDirection(muzzle);
            dir = Quaternion.AngleAxis(Random.Range(-1.2f, 1.2f), Random.onUnitSphere) * dir;
            Vector3 from = muzzle.position;
            Vector3 to = from + dir * range;
            if (HitScan(from, dir, range, out RaycastHit hit))
            {
                to = hit.point;
                var d = hit.collider.GetComponentInParent<Damageable>();
                if (d != null) d.TakeDamage(damage, hit.point, owner);
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    hit.rigidbody.AddForceAtPosition(dir * Mathf.Min(1.5f, 300f / hit.rigidbody.mass) * (d != null ? 0.5f : 1f), hit.point, ForceMode.VelocityChange);
                Fx.Sparks(hit.point, hit.normal, 6, 16f, 80f);
                if (Random.value < 0.3f) Fx.Dust(hit.point, 0.4f);
            }
            Beam.Spawn(from, to, 0.22f, new Color(1f, 0.85f, 0.4f), 0.05f);
            Fx.MuzzleFlash(from, dir, 0.45f);
            if (IsPlayer) FollowCamera.Shake(0.03f, from, 40f);
        }
    }

    /// <summary>Pods that fire unguided rockets in quick pairs.</summary>
    public class RocketPods : Weapon
    {
        public float damage = 48f;
        public float radius = 6f;

        protected override void Fire()
        {
            var muzzle = NextMuzzle();
            Vector3 dir = AimDirection(muzzle);
            var m = Missile.Spawn(muzzle.position + dir * 0.8f, dir, owner, null);
            m.damage = damage;
            m.radius = radius;
            m.launchTime = 0.05f;
            m.speed = 95f;
            m.turnRate = 0f;
            Fx.MuzzleFlash(muzzle.position, dir, 0.7f);
            GameAudio.Play(Sfx.MissileLaunch, muzzle.position, 0.7f, Random.Range(1.1f, 1.3f));
            if (IsPlayer) FollowCamera.Shake(0.06f, muzzle.position, 50f);
        }
    }

    /// <summary>Lobs a barrage of high-explosive shells in tall arcs onto the aim point.</summary>
    public class MortarBarrage : Weapon
    {
        public int shells = 6;
        public float flightTime = 2.2f;
        public float damage = 95f;
        public float scatter = 9f;

        protected override void Fire()
        {
            StartCoroutine(Barrage());
        }

        IEnumerator Barrage()
        {
            Vector3 target = turret != null ? turret.AimPoint : transform.position + transform.forward * 100f;
            if (turret != null && turret.Target != null) target = turret.Target.Center;
            for (int i = 0; i < shells; i++)
            {
                if (!enabled) yield break;
                var muzzle = NextMuzzle();
                Vector3 aim = target + new Vector3(Random.Range(-scatter, scatter), 0f, Random.Range(-scatter, scatter));
                Vector3 from = muzzle.position;
                float t = flightTime * Random.Range(0.9f, 1.1f);
                Vector3 v = (aim - from - 0.5f * Physics.gravity * t * t) / t;
                var shell = Shell.Spawn(from, v, owner, new Color(1f, 0.5f, 0.2f), 0.9f);
                shell.damage = damage;
                shell.radius = 9f;
                shell.fxSize = 1.6f;
                Fx.MuzzleFlash(from, v.normalized, 1.2f);
                GameAudio.Play(Sfx.Cannon, from, 0.8f, Random.Range(1.2f, 1.35f));
                if (IsPlayer) FollowCamera.Shake(0.12f, from, 60f);
                yield return new WaitForSeconds(0.16f);
            }
        }
    }

    /// <summary>Charges, then lightning jumps between up to three enemies.</summary>
    public class TeslaStorm : Weapon
    {
        public float damage = 150f;
        public float range = 160f;
        public float chainRange = 70f;
        public int maxTargets = 3;
        public float chargeTime = 0.6f;
        bool charging;
        float chargeStart;

        protected override void Update()
        {
            if (charging)
            {
                if (muzzles.Length > 0 && Random.value < 0.4f) Fx.EnergyBurst(muzzles[0].position, color, 0.4f);
                if (Time.time - chargeStart >= chargeTime) Discharge();
                return;
            }
            base.Update();
        }

        protected override void Fire()
        {
            charging = true;
            chargeStart = Time.time;
            GameAudio.Play(Sfx.RailCharge, transform.position, 0.6f, 1.4f);
        }

        void Discharge()
        {
            charging = false;
            lastFire = Time.time;
            var muzzle = muzzles.Length > 0 ? muzzles[0] : transform;
            Vector3 from = muzzle.position;

            // First target: the locked one, else whatever is under the aim point.
            Damageable first = turret != null ? turret.Target : null;
            if (first == null)
            {
                Vector3 dir = AimDirection(muzzle);
                if (HitScan(from, dir, range, out RaycastHit hit))
                {
                    first = hit.collider.GetComponentInParent<Damageable>();
                    if (first == null) { Bolt(from, hit.point); Fx.EnergyBurst(hit.point, color, 1.2f); }
                }
                else Bolt(from, from + dir * range);
            }

            var hitList = new System.Collections.Generic.List<Damageable>();
            Damageable current = first;
            Vector3 p = from;
            while (current != null && hitList.Count < maxTargets)
            {
                hitList.Add(current);
                Bolt(p, current.Center);
                current.TakeDamage(damage * (hitList.Count == 1 ? 1f : 0.7f), current.Center, owner);
                Fx.EnergyBurst(current.Center, color, 1.6f);
                Fx.Sparks(current.Center, Vector3.up, 30, 22f, 180f);
                p = current.Center;
                current = NextInChain(p, hitList);
            }

            Fx.EnergyBurst(from, color, 1.4f);
            GameAudio.Play(Sfx.Tesla, from, 1f);
            GameAudio.Play(Sfx.Sizzle, p, 0.9f);
            FollowCamera.Shake(IsPlayer ? 0.4f : 0.15f, from, 120f);
        }

        Damageable NextInChain(Vector3 from, System.Collections.Generic.List<Damageable> done)
        {
            Damageable best = null;
            float bestDist = chainRange;
            foreach (var d in Damageable.All)
            {
                if (d == null || d.IsDead || done.Contains(d) || d == owner) continue;
                if (owner != null && d.team == owner.team) continue;
                float dist = Vector3.Distance(from, d.Center);
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            return best;
        }

        void Bolt(Vector3 a, Vector3 b)
        {
            // Jagged lightning: several short beams with random kinks.
            const int segments = 7;
            Vector3 prev = a;
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            float len = Vector3.Distance(a, b);
            for (int i = 1; i <= segments; i++)
            {
                float k = i / (float)segments;
                Vector3 next = Vector3.Lerp(a, b, k);
                if (i < segments) next += (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * len * 0.05f;
                Beam.Spawn(prev, next, 0.9f, color, 0.25f);
                Beam.Spawn(prev, next, 0.3f, Color.white, 0.18f);
                prev = next;
            }
        }

        void OnDisable() { charging = false; }
    }

    /// <summary>Short-range flame jets: hold to roast anything in front.</summary>
    public class Flamethrower : Weapon
    {
        public float damagePerSecond = 150f;
        public float range = 42f;
        public float coneAngle = 16f;
        ParticleSystem[] flames;
        AudioSource roar;
        bool wasOn;
        float fuelUsed;
        static readonly Collider[] Hits = new Collider[64];
        static readonly System.Collections.Generic.HashSet<Damageable> Seen = new System.Collections.Generic.HashSet<Damageable>();

        void Start()
        {
            flames = new ParticleSystem[muzzles.Length];
            for (int i = 0; i < muzzles.Length; i++)
            {
                flames[i] = Fx.ThrusterFlame(muzzles[i], 2.2f, new Color(1f, 0.5f, 0.12f));
                if (flames[i] != null)
                {
                    var m = flames[i].main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(45f, 60f);
                    m.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                    m.maxParticles = 400;
                    var shape = flames[i].shape;
                    shape.angle = coneAngle * 0.6f;
                }
            }
            roar = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(roar, 20f, 200f);
            roar.clip = GameAudio.FlameLoop();
            roar.loop = true;
            roar.volume = 0f;
            roar.pitch = 1f;
            roar.Play();
        }

        protected override void Update()
        {
            TickMagazine();
            bool on = Triggered && !Reloading;
            if (on)
            {
                // Burns through its fuel tank, then refills (one round = 0.1 s of flame).
                fuelUsed += Time.deltaTime;
                while (fuelUsed >= 0.1f && !Reloading) { fuelUsed -= 0.1f; UseRound(); }
                if (Reloading) fuelUsed = 0f;
            }
            if (on && !wasOn && muzzles.Length > 0) GameAudio.Play(Sfx.FlameStart, muzzles[0].position, 0.8f);
            wasOn = on;
            if (flames != null) foreach (var f in flames) Fx.SetRate(f, on ? 140f : 0f);
            if (roar != null) roar.volume = Mathf.MoveTowards(roar.volume, on ? 0.8f : 0f, Time.deltaTime * 5f);
            if (!on || muzzles.Length == 0) return;

            lastFire = Time.time;
            if (owner != null) owner.DropShield();
            var muzzle = muzzles[0];
            Vector3 dir = AimDirection(muzzle);
            Vector3 from = muzzle.position;
            int n = Physics.OverlapSphereNonAlloc(from + dir * range * 0.5f, range * 0.55f, Hits, ~0, QueryTriggerInteraction.Ignore);
            var seen = Seen; seen.Clear();
            for (int i = 0; i < n; i++)
            {
                var d = Hits[i].GetComponentInParent<Damageable>();
                if (d == null || d == owner || d.IsDead || !seen.Add(d)) continue;
                Vector3 to = d.Center - from;
                if (Vector3.Angle(dir, to) > coneAngle + 10f || to.magnitude > range + d.size * 3f) continue;
                d.TakeDamage(damagePerSecond * Time.deltaTime, d.Center, owner);
            }
            if (IsPlayer) FollowCamera.Shake(0.015f, from, 30f);
        }

        protected override void Fire() { }

        void OnDisable()
        {
            if (flames != null) foreach (var f in flames) Fx.SetRate(f, 0f);
            if (roar != null) roar.volume = 0f;
        }
    }
}
