using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    // =========================================================================
    // Arm weapons
    // =========================================================================

    /// <summary>40 mm autocannon: three-round bursts of high-explosive shells, a short pause between bursts.</summary>
    public class Autocannon : Weapon, IAiRange
    {
        public float damage = 45f;
        public float shellSpeed = 170f;
        public float blastRadius = 3.5f;
        public int burst = 3;
        public float burstInterval = 0.09f;

        public float IdealRange => 90f;
        public float MaxRange => 260f;
        public bool Indirect => false;

        protected override void Fire()
        {
            StartCoroutine(Burst());
        }

        IEnumerator Burst()
        {
            for (int i = 0; i < burst; i++)
            {
                if (!enabled) yield break;
                var muzzle = NextMuzzle();
                Vector3 dir = AimDirection(muzzle);
                dir = Quaternion.AngleAxis(Random.Range(-0.6f, 0.6f), Random.onUnitSphere) * dir;
                Vector3 inherit = body != null ? Compat.GetVelocity(body) : Vector3.zero;
                var shell = Shell.Spawn(muzzle.position + dir * 1.2f, dir * shellSpeed + inherit, owner, new Color(1f, 0.75f, 0.35f), 0.45f);
                shell.damage = damage;
                shell.radius = blastRadius;
                shell.force = 4f;
                shell.fxSize = 0.55f;
                Fx.MuzzleFlash(muzzle.position, dir, 0.9f);
                GameAudio.Play(Sfx.Cannon, muzzle.position, 0.65f, Random.Range(1.35f, 1.5f));
                if (IsPlayer) FollowCamera.Shake(0.07f, muzzle.position, 50f);
                Recoil(dir, muzzle.position, 0.25f);
                KickBarrel(muzzle, 0.45f);
                yield return new WaitForSeconds(burstInterval);
            }
        }

        static void KickBarrel(Transform muzzle, float dist)
        {
            var barrel = muzzle.parent;
            if (barrel != null) CoroutineHost.Run(Kick(barrel, dist));
        }

        internal static IEnumerator Kick(Transform barrel, float dist)
        {
            Vector3 rest = barrel.localPosition;
            float t = 0f;
            while (t < 0.12f && barrel != null)
            {
                t += Time.deltaTime;
                float k = t < 0.025f ? t / 0.025f : 1f - (t - 0.025f) / 0.095f;
                barrel.localPosition = rest + Vector3.back * dist * Mathf.Clamp01(k);
                yield return null;
            }
            if (barrel != null) barrel.localPosition = rest;
        }
    }

    /// <summary>
    /// Scatter cannon: a giant's shotgun. Nine heavy slugs in a cone; brutal up close,
    /// the slugs lose their punch beyond about 60 m.
    /// </summary>
    public class ScatterCannon : Weapon, IAiRange
    {
        public int pellets = 9;
        public float damage = 22f;
        public float coneDegrees = 4.5f;
        public float range = 160f;
        public float fullDamageRange = 55f;

        public float IdealRange => 32f;
        public float MaxRange => 110f;
        public bool Indirect => false;

        protected override void Fire()
        {
            var muzzle = NextMuzzle();
            Vector3 aim = AimDirection(muzzle);
            Vector3 from = muzzle.position;
            var hitOnce = new HashSet<Damageable>();
            for (int i = 0; i < pellets; i++)
            {
                Vector2 c = Random.insideUnitCircle * coneDegrees;
                Vector3 dir = Quaternion.AngleAxis(c.x, Vector3.up) * Quaternion.AngleAxis(c.y, Vector3.Cross(aim, Vector3.up).normalized) * aim;
                Vector3 to = from + dir * range;
                if (HitScan(from, dir, range, out RaycastHit hit))
                {
                    to = hit.point;
                    float falloff = Mathf.Clamp(1f - (hit.distance - fullDamageRange) / (range - fullDamageRange), 0.2f, 1f);
                    var d = hit.collider.GetComponentInParent<Damageable>();
                    if (d != null)
                    {
                        d.TakeDamage(damage * falloff, hit.point, owner);
                        if (hitOnce.Add(d) && d.Body != null && !d.Body.isKinematic)
                            d.Body.AddForceAtPosition(dir * 0.35f * falloff, hit.point, ForceMode.VelocityChange);
                    }
                    else if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                        hit.rigidbody.AddForceAtPosition(dir * Mathf.Min(3f, 600f / hit.rigidbody.mass), hit.point, ForceMode.VelocityChange);
                    Fx.Sparks(hit.point, hit.normal, 8, 18f, 70f);
                    if (i % 3 == 0) Fx.Dust(hit.point, 0.5f);
                }
                Beam.Spawn(from, to, 0.18f, new Color(1f, 0.82f, 0.45f), 0.06f);
            }
            Fx.MuzzleFlash(from, aim, 2.1f);
            Fx.Dust(from + aim * 2f, 0.8f);
            GameAudio.Play(Sfx.Cannon, from, 1f, Random.Range(0.75f, 0.85f));
            FollowCamera.Shake(IsPlayer ? 0.32f : 0.1f, from, 70f);
            Recoil(aim, from, 1.1f);
            CoroutineHost.Run(Autocannon.Kick(muzzle.parent, 0.9f));
        }
    }

    /// <summary>
    /// Ion beam: a continuous beam while the trigger is held. Holding it on the same target
    /// heats the target's armour, ramping the damage up to 150% after two seconds.
    /// </summary>
    public class IonBeam : Weapon, IAiRange
    {
        public float damagePerSecond = 150f;
        public float range = 230f;
        public float tick = 0.1f;
        Damageable lastHit;
        float onTarget;

        public float IdealRange => 70f;
        public float MaxRange => 210f;
        public bool Indirect => false;

        float nextSound;

        protected override void Update()
        {
            if (!Triggered) { onTarget = 0f; lastHit = null; }
            base.Update();
        }

        protected override void Fire()
        {
            cooldown = tick;
            var muzzle = NextMuzzle();
            Vector3 dir = AimDirection(muzzle);
            Vector3 from = muzzle.position;
            Vector3 to = from + dir * range;
            float ramp = 1f;
            if (HitScan(from, dir, range, out RaycastHit hit))
            {
                to = hit.point;
                var d = hit.collider.GetComponentInParent<Damageable>();
                if (d != null)
                {
                    if (d == lastHit) onTarget += tick; else { lastHit = d; onTarget = 0f; }
                    ramp = Mathf.Lerp(0.7f, 1.5f, Mathf.Clamp01(onTarget / 2f));
                    d.TakeDamage(damagePerSecond * tick * ramp, hit.point, owner);
                }
                else { lastHit = null; onTarget = 0f; }
                if (Random.value < 0.6f) Fx.Sparks(hit.point, hit.normal, 5, 12f, 90f);
                Fx.EnergyBurst(hit.point, color, 0.35f + ramp * 0.25f);
            }
            float w = 0.35f + (ramp - 0.7f) * 0.5f;
            Beam.Spawn(from, to, w, color, tick * 1.6f);
            Beam.Spawn(from, to, w * 0.35f, Color.white, tick * 1.4f);
            if (Random.value < 0.3f) Fx.EnergyBurst(from, color, 0.3f);
            if (Time.time >= nextSound)
            {
                nextSound = Time.time + 0.32f;
                GameAudio.Play(Sfx.Sizzle, from, 0.45f, Random.Range(0.6f, 0.7f));
                if (lastHit != null) GameAudio.Play(Sfx.Sizzle, to, 0.5f, Random.Range(0.85f, 1f));
            }
        }
    }

    /// <summary>Revolver grenade launcher: lobs heavy grenades in an arc onto the aim point. Clears cover.</summary>
    public class GrenadeLauncher : Weapon, IAiRange
    {
        public float damage = 95f;
        public float radius = 8f;
        public float speed = 75f;
        public Transform drum;
        float drumAngle;

        public float IdealRange => 80f;
        public float MaxRange => 210f;
        public bool Indirect => false;

        protected override void Update()
        {
            if (drum != null)
            {
                float target = drumAngle;
                drum.localRotation = Quaternion.Slerp(drum.localRotation, Quaternion.Euler(0f, 0f, target), Time.deltaTime * 14f);
            }
            base.Update();
        }

        protected override void Fire()
        {
            var muzzle = NextMuzzle();
            Vector3 from = muzzle.position;
            Vector3 aimPt = turret != null ? turret.AimPoint : from + muzzle.forward * 80f;
            Vector3 v = Ballistic(from, aimPt, speed);
            if (spread > 0f) v = Quaternion.AngleAxis(Random.Range(-spread, spread), Random.onUnitSphere) * v;
            var g = Shell.Spawn(from + v.normalized * 1.2f, v, owner, new Color(0.95f, 0.85f, 0.4f), 0.75f);
            g.damage = damage;
            g.radius = radius;
            g.fxSize = 1.1f;
            Fx.MuzzleFlash(from, v.normalized, 1.2f);
            GameAudio.Play(Sfx.Cannon, from, 0.75f, Random.Range(1.15f, 1.25f));
            if (IsPlayer) FollowCamera.Shake(0.12f, from, 50f);
            Recoil(v.normalized, from, 0.4f);
            drumAngle += 60f;
        }

        /// <summary>Launch velocity at a fixed speed that lands on the point (flat arc when it can).</summary>
        public static Vector3 Ballistic(Vector3 from, Vector3 to, float speed)
        {
            Vector3 d = to - from;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float x = flat.magnitude, y = d.y, g = -Physics.gravity.y;
            float v2 = speed * speed;
            float disc = v2 * v2 - g * (g * x * x + 2f * y * v2);
            if (x < 0.5f) return d.normalized * speed;
            if (disc < 0f)
            {
                // Out of reach: 45 degrees gives the longest throw.
                return (flat.normalized + Vector3.up).normalized * speed;
            }
            float angle = Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (g * x));
            return (flat.normalized * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * speed;
        }
    }

    // =========================================================================
    // Specials
    // =========================================================================

    /// <summary>A heavy canister fired high over the target that bursts into a carpet of bomblets.</summary>
    public class ClusterBomb : Weapon, IAiRange
    {
        public int bomblets = 12;
        public float damage = 55f;
        public float spreadRadius = 18f;
        public float flightTime = 2.4f;

        public float IdealRange => 120f;
        public float MaxRange => 240f;
        public bool Indirect => true;

        protected override void Fire()
        {
            var muzzle = NextMuzzle();
            Vector3 target = turret != null ? turret.AimPoint : transform.position + transform.forward * 120f;
            if (turret != null && turret.Target != null) target = turret.Target.transform.position;
            Vector3 from = muzzle.position;
            // Burst point: above the target, reached at the top of a tall arc.
            Vector3 burst = target + Vector3.up * 34f;
            float t = flightTime;
            Vector3 v = (burst - from - 0.5f * Physics.gravity * t * t) / t;
            var can = Shell.Spawn(from, v, owner, new Color(1f, 0.6f, 0.25f), 1.3f);
            can.damage = damage;
            can.radius = 6f;
            var c = can.gameObject.AddComponent<ClusterCanister>();
            c.burstAt = Time.time + t;
            c.count = bomblets;
            c.damage = damage;
            c.spread = spreadRadius;
            c.owner = owner;
            Fx.MuzzleFlash(from, v.normalized, 2f);
            Fx.Dust(from, 1.2f);
            GameAudio.Play(Sfx.Cannon, from, 1f, 0.7f);
            GameAudio.Play(Sfx.Whoosh, from, 0.8f, 0.8f);
            FollowCamera.Shake(IsPlayer ? 0.3f : 0.1f, from, 90f);
        }
    }

    /// <summary>The canister in flight: at its burst time it splits into bomblets.</summary>
    public class ClusterCanister : MonoBehaviour
    {
        public float burstAt;
        public int count = 12;
        public float damage = 55f;
        public float spread = 18f;
        public Damageable owner;
        bool done;

        void Update()
        {
            if (done || Time.time < burstAt) return;
            done = true;
            Vector3 p = transform.position;
            var rb = GetComponent<Rigidbody>();
            Vector3 v = rb != null ? Compat.GetVelocity(rb) : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Vector2 r = Random.insideUnitCircle * spread;
                Vector3 bv = new Vector3(v.x * 0.3f + r.x * 0.9f, -4f - Random.value * 6f, v.z * 0.3f + r.y * 0.9f);
                var b = Shell.Spawn(p + new Vector3(r.x, 0f, r.y) * 0.08f, bv, owner, new Color(1f, 0.55f, 0.2f), 0.5f);
                b.damage = damage;
                b.radius = 6.5f;
                b.fxSize = 0.9f;
            }
            Fx.SmallBlast(p, 1.4f);
            Fx.Flash(p, new Color(1f, 0.7f, 0.3f), 40f, 3f, 0.2f);
            GameAudio.Play(Sfx.Explosion, p, 0.8f, 1.3f);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// EMP blast: charges, then a shockwave rolls out from the robot. Every enemy machine
    /// caught in it takes damage and has its weapons knocked offline for a few seconds.
    /// </summary>
    public class EmpBlast : Weapon, IAiRange
    {
        public float damage = 140f;
        public float radius = 75f;
        public float jamSeconds = 3.5f;
        public float chargeTime = 0.8f;
        bool charging;
        float chargeStart;

        public float IdealRange => 40f;
        public float MaxRange => 70f;
        public bool Indirect => true;

        protected override void Update()
        {
            if (charging)
            {
                if (muzzles.Length > 0 && Random.value < 0.5f) Fx.EnergyBurst(muzzles[0].position, color, 0.5f + (Time.time - chargeStart) / chargeTime);
                if (Time.time - chargeStart >= chargeTime) Discharge();
                return;
            }
            base.Update();
        }

        protected override void Fire()
        {
            charging = true;
            chargeStart = Time.time;
            GameAudio.Play(Sfx.RailCharge, transform.position, 0.8f, 0.8f);
        }

        void Discharge()
        {
            charging = false;
            lastFire = Time.time;
            Vector3 c = owner != null ? owner.Center : transform.position;
            Shockwave.Spawn(c, radius, color);
            Fx.Flash(c, color, radius, 4f, 0.3f);
            GameAudio.Play(Sfx.Tesla, c, 1f, 0.7f);
            GameAudio.Play(Sfx.BigExplosion, c, 0.6f, 1.6f);
            FollowCamera.Shake(IsPlayer ? 0.5f : 0.2f, c, 160f);
            foreach (var d in new List<Damageable>(Damageable.All))
            {
                if (d == null || d.IsDead || d == owner || (owner != null && d.team == owner.team)) continue;
                float dist = Vector3.Distance(d.Center, c);
                if (dist > radius) continue;
                float k = 1f - dist / radius * 0.5f;
                d.TakeDamage(damage * k, d.Center, owner);
                if (d.GetComponent<Locomotion>() != null) EmpJam.Apply(d, jamSeconds * k, color);
            }
        }

        void OnDisable() { charging = false; }
    }

    /// <summary>Knocks a robot's weapons offline for a while, with sparks crawling over it.</summary>
    public class EmpJam : MonoBehaviour
    {
        float until;
        Color color;
        readonly List<Weapon> jammed = new List<Weapon>();

        public static void Apply(Damageable d, float seconds, Color color)
        {
            var j = d.GetComponent<EmpJam>();
            if (j == null) j = d.gameObject.AddComponent<EmpJam>();
            j.color = color;
            j.until = Mathf.Max(j.until, Time.time + seconds);
            foreach (var w in d.GetComponents<Weapon>())
                if (w.enabled && !j.jammed.Contains(w)) { w.enabled = false; j.jammed.Add(w); }
        }

        void Update()
        {
            var d = GetComponent<Damageable>();
            if (Random.value < 0.25f && d != null) Fx.Sparks(d.Center + Random.insideUnitSphere * d.size * 2.5f, Random.onUnitSphere, 6, 10f, 180f);
            if (Time.time < until && (d == null || !d.IsDead)) return;
            // Only switch back on what we switched off, and never on a wreck.
            if (d != null && !d.IsDead) foreach (var w in jammed) if (w != null) w.enabled = true;
            jammed.Clear();
            Destroy(this);
        }
    }

    /// <summary>An expanding ring of light along the ground (the EMP's visible wave).</summary>
    public class Shockwave : MonoBehaviour
    {
        float radius, age;
        const float Life = 0.55f;
        Material mat;
        Color color;

        public static void Spawn(Vector3 pos, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Shockwave";
            go.transform.position = new Vector3(pos.x, BattleGround.HeightAt(pos.x, pos.z) + 1.5f, pos.z);
            go.transform.localScale = new Vector3(1f, 0.05f, 1f);
            var s = go.AddComponent<Shockwave>();
            s.radius = radius;
            s.color = color;
            var a = GameAssets.Get();
            if (a != null)
            {
                s.mat = new Material(a.Glow(color, 3f));
                go.GetComponent<Renderer>().sharedMaterial = s.mat;
            }
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age / Life;
            float r = Mathf.Lerp(2f, radius * 2f, 1f - (1f - k) * (1f - k));
            transform.localScale = new Vector3(r, Mathf.Lerp(3f, 0.05f, k), r);
            if (mat != null) mat.color = new Color(color.r, color.g, color.b, 1f - k);
            if (age >= Life)
            {
                if (mat != null) Destroy(mat);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Hellfire: two heavy rockets leave their cells straight up, climb, then turn over
    /// and dive onto the target from above, where the armour is thinnest.
    /// </summary>
    public class Hellfire : Weapon, IAiRange
    {
        public int rockets = 2;
        public float damage = 230f;
        public float radius = 9f;

        public float IdealRange => 140f;
        public float MaxRange => 300f;
        public bool Indirect => true;

        protected override void Fire()
        {
            StartCoroutine(Salvo());
        }

        IEnumerator Salvo()
        {
            var target = turret != null ? turret.Target : null;
            Vector3 aim = turret != null ? turret.AimPoint : transform.position + transform.forward * 150f;
            for (int i = 0; i < rockets; i++)
            {
                if (!enabled) yield break;
                var cell = NextMuzzle();
                var m = Missile.Spawn(cell.position + Vector3.up * 1f, Vector3.up, owner, target);
                m.damage = damage;
                m.radius = radius;
                m.speed = 85f;
                m.turnRate = 95f;
                m.launchTime = 0.85f;
                if (target == null) m.aimPoint = aim;
                m.transform.localScale = Vector3.one * 1.7f;
                Fx.MuzzleFlash(cell.position, Vector3.up, 1.6f);
                Fx.Dust(cell.position, 1.4f);
                GameAudio.Play(Sfx.MissileLaunch, cell.position, 1f, 0.75f);
                FollowCamera.Shake(IsPlayer ? 0.2f : 0.08f, cell.position, 80f);
                yield return new WaitForSeconds(0.35f);
            }
        }
    }
}
