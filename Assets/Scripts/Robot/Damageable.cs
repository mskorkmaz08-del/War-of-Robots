using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Health, team, damage and destruction for a robot.
    /// As health drops, armour plates blow off and the robot starts smoking.
    /// On death it explodes, throws parts around and leaves a burnt wreck.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Damageable : MonoBehaviour
    {
        public static readonly List<Damageable> All = new List<Damageable>();

        public string displayName = "Robot";
        public int team;
        public float maxHealth = 1000f;
        [Tooltip("Parts that get blown off as the robot takes damage, in order.")]
        public List<Transform> detachableParts = new List<Transform>();
        [Tooltip("Where smoke pours out when the robot is badly damaged.")]
        public Transform smokePoint;
        [Tooltip("Rough size used to scale effects.")]
        public float size = 1f;

        [Tooltip("Damage multiplier for impacts on the front (rams are built to hit things).")]
        public float frontArmor = 1f;

        [Header("Collision damage")]
        public float impactThreshold = 9f;
        public float impactDamagePerMs = 14f;

        /// <summary>
        /// Damage enemies deal to the player is multiplied by this. 1: everyone plays by
        /// the same rules (the old 0.45 made the player far tougher than the enemies).
        /// </summary>
        public static float EnemyDamageToPlayer = 1f;

        /// <summary>Who hit this robot last (for kill credit).</summary>
        public Damageable LastAttacker { get; private set; }

        /// <summary>Can't be hurt until then: just respawned. Firing ends it early.</summary>
        public float ShieldUntil { get; private set; } = -1f;
        public bool Shielded => Time.time < ShieldUntil;
        public event System.Action ShieldChanged;

        /// <summary>Starts the spawn shield (a few seconds to get away from the base).</summary>
        public void StartSpawnShield(float seconds)
        {
            ShieldUntil = Time.time + seconds;
            ShieldChanged?.Invoke();
        }

        /// <summary>Opening fire drops the shield: no shooting from behind it.</summary>
        public void DropShield()
        {
            if (!Shielded) return;
            ShieldUntil = -1f;
            ShieldChanged?.Invoke();
        }

        /// <summary>Online, on a joining player's device: health comes only from the host.</summary>
        public static bool NetClient;

        public float Health { get; private set; }
        public bool IsDead { get; private set; }
        public float HealthFraction => Mathf.Clamp01(Health / maxHealth);
        public Rigidbody Body { get; private set; }

        public event System.Action<Damageable> Died;
        public event System.Action<float, Vector3> Damaged;

        int partsDetached;
        ParticleSystem damageSmoke;
        ParticleSystem damageFire;
        float lastImpactTime;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Health = maxHealth;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public Vector3 Center => Body != null ? Body.worldCenterOfMass : transform.position;

        public void Heal(float amount)
        {
            if (IsDead) return;
            Health = Mathf.Min(maxHealth, Health + amount);
        }

        float nextBoundsCheck;

        /// <summary>
        /// A robot that falls through or under the map, or gets outside the battlefield,
        /// is destroyed, so a match can never get stuck waiting for it.
        /// </summary>
        void Update()
        {
            if (IsDead || NetClient || Body == null || Body.isKinematic || Time.time < nextBoundsCheck) return;
            nextBoundsCheck = Time.time + 0.5f;
            Vector3 p = transform.position;
            float limit = Frontier.Half + 25f;
            if (p.y < -30f || Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit)
                ApplyDamage(Health + maxHealth, p, null, true);
        }

        public void TakeDamage(float amount, Vector3 point, Damageable attacker = null)
        {
            if (NetClient) return;
            ApplyDamage(amount, point, attacker);
        }

        /// <summary>Superpowers: incoming damage is multiplied by this (shields, fortress mode).</summary>
        [System.NonSerialized] public float DamageTakenMul = 1f;
        /// <summary>Superpowers: damage this robot deals is multiplied by this (overdrive).</summary>
        [System.NonSerialized] public float DamageDealtMul = 1f;
        /// <summary>Superpowers: a shield that soaks up damage. Gets the damage and the hit point, returns what gets through.</summary>
        public System.Func<float, Vector3, float> Absorb;

        /// <summary>Online clients: the host says this is the robot's health now.</summary>
        public void NetSetHealth(float health)
        {
            if (IsDead) return;
            if (health > Health) { Health = Mathf.Min(maxHealth, health); return; }
            if (health < Health - 0.01f) ApplyDamage(Health - Mathf.Max(0f, health), Center, null, true);
        }

        void ApplyDamage(float amount, Vector3 point, Damageable attacker, bool raw = false)
        {
            if (IsDead || amount <= 0f) return;
            if (attacker != null && attacker.team == team && attacker != this) return;
            if (!raw && Shielded) return;
            if (!raw && team == 0 && attacker != null && attacker.team != 0) amount *= EnemyDamageToPlayer;
            if (!raw)
            {
                if (attacker != null && attacker != this) amount *= attacker.DamageDealtMul;
                amount *= DamageTakenMul;
                if (Absorb != null) amount = Absorb(amount, point);
                if (amount <= 0f) return;
            }
            if (attacker != null && attacker != this) LastAttacker = attacker;

            Health -= amount;
            Damaged?.Invoke(amount, point);

            float lost = 1f - HealthFraction;
            int shouldHaveDetached = Mathf.FloorToInt(lost * (detachableParts.Count + 1));
            while (partsDetached < shouldHaveDetached && partsDetached < detachableParts.Count)
            {
                BlowOff(detachableParts[partsDetached], point);
                partsDetached++;
            }

            if (HealthFraction < 0.5f && damageSmoke == null && smokePoint != null)
                damageSmoke = Fx.SmokeTrail(smokePoint, 2.2f * size, new Color(0.12f, 0.12f, 0.12f, 0.7f));

            if (HealthFraction < 0.25f && damageFire == null && smokePoint != null)
            {
                damageFire = Fx.ThrusterFlame(smokePoint, 0.5f * size, new Color(1f, 0.45f, 0.1f));
                if (damageFire != null)
                {
                    damageFire.transform.rotation = Quaternion.LookRotation(Vector3.up);
                    Fx.SetRate(damageFire, 30f);
                }
            }

            if (Health <= 0f) Die();
        }

        void BlowOff(Transform part, Vector3 hitPoint, bool ownBlast = true)
        {
            if (part == null) return;
            part.SetParent(null, true);
            var col = part.GetComponent<Collider>();
            if (col == null)
            {
                var box = part.gameObject.AddComponent<BoxCollider>();
                var renderers = part.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    box.center = part.InverseTransformPoint(b.center);
                    Vector3 s = part.lossyScale;
                    box.size = new Vector3(b.size.x / Mathf.Max(0.01f, s.x), b.size.y / Mathf.Max(0.01f, s.y), b.size.z / Mathf.Max(0.01f, s.z)) * 0.8f;
                }
                col = box;
            }
            col.enabled = true;
            // The part starts inside the hull: never let it collide with the robot it came
            // from, or the overlap shoves both apart and the wreck goes flying.
            foreach (var own in GetComponentsInChildren<Collider>())
                if (own != col) Physics.IgnoreCollision(col, own, true);
            var rb = part.gameObject.GetComponent<Rigidbody>();
            if (rb == null) rb = part.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(200f, part.lossyScale.x * part.lossyScale.y * part.lossyScale.z * 400f);
            Compat.SetVelocity(rb, Body != null ? Compat.GetVelocity(Body) : Vector3.zero);
            Vector3 away = (part.position - hitPoint).normalized + Vector3.up;
            rb.AddForce(away.normalized * 14f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
            Fx.Sparks(part.position, away, ownBlast ? 30 : 14, 25f, 90f);
            if (ownBlast) Fx.Explosion(part.position, 0.6f * size);
            GameAudio.Play(Sfx.MetalHit, part.position, 1f, 0.7f);
            Destroy(part.gameObject, 12f);
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Health = 0f;

            // Everything that fights or thinks stops now. A robot's legs or wheels keep
            // running a little longer: they carry the wreck down to the ground.
            var loco = GetComponent<Locomotion>();
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == this || behaviour == loco) continue;
                if (behaviour is IDisableOnDeath) behaviour.enabled = false;
            }
            var commands = GetComponent<RobotCommands>();
            if (commands != null) commands.Clear();

            if (loco != null && loco.enabled) DeathSequence.Begin(this, loco);
            else
            {
                if (loco != null) loco.enabled = false;
                Explode();
            }

            Died?.Invoke(this);
        }

        /// <summary>
        /// The final blast: fireball, the remaining armour and a few of the robot's own parts
        /// thrown off, the wreck burnt black and smoking. The hull itself is not thrown:
        /// it stays where it is and slumps (see DeathSequence).
        /// </summary>
        public void Explode()
        {
            Vector3 c = Center;
            Fx.Explosion(c, 3.5f * size);
            Fx.Explosion(c + Random.insideUnitSphere * 3f * size, 2f * size);
            GameAudio.Play(Sfx.BigExplosion, c, 1f, 0.8f);
            GameAudio.Play(Sfx.Debris, c, 1f, 0.8f);
            FollowCamera.Shake(0.9f, c, 200f);
            Explosions.ApplyForce(c, 25f * size, 8f, this);

            // Throw the remaining detachable parts.
            for (int i = partsDetached; i < detachableParts.Count; i++) BlowOff(detachableParts[i], c, false);
            partsDetached = detachableParts.Count;

            // Break off a few of the robot's own pieces (rams, plates, gun parts, wheels):
            // the wreckage that flies is always part of this machine, never stand-in blocks.
            BreakApart(c);

            // Burn the wreck.
            var assets = GameAssets.Get();
            if (assets != null)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    r.sharedMaterial = assets.Burnt();
                }
            }

            if (damageSmoke == null && smokePoint != null)
                damageSmoke = Fx.SmokeTrail(smokePoint, 3f * size, new Color(0.08f, 0.08f, 0.08f, 0.8f));
        }

        /// <summary>
        /// Picks real parts of this robot that could plausibly tear loose in the blast
        /// (mid-sized pieces, not the hull itself) and throws them as burning wreckage.
        /// </summary>
        void BreakApart(Vector3 centre)
        {
            var candidates = new List<Transform>();
            float robotSize = 1f;
            var all = GetComponentsInChildren<MeshRenderer>();
            if (all.Length > 0)
            {
                Bounds rb = all[0].bounds;
                foreach (var r in all) rb.Encapsulate(r.bounds);
                robotSize = rb.size.magnitude;
            }
            // The wreck's joints are frozen: stop the rams so a torn-off cylinder flies free.
            foreach (var ram in GetComponentsInChildren<HydraulicRam>()) ram.enabled = false;
            foreach (var r in all)
            {
                if (r.GetComponent<MeshFilter>() == null) continue;
                float s = r.bounds.size.magnitude;
                if (s < 0.9f || s > robotSize * 0.33f) continue;
                candidates.Add(r.transform);
            }
            // Throw the pieces nearest the blast first, plus a little luck.
            var key = new Dictionary<Transform, float>();
            foreach (var t in candidates) key[t] = Vector3.Distance(t.position, centre) * Random.Range(0.7f, 1.3f);
            candidates.Sort((a, b) => key[a].CompareTo(key[b]));
            int count = Mathf.Min(candidates.Count, Mathf.RoundToInt(4f + size * 1.5f));
            for (int i = 0; i < count; i++)
            {
                var part = candidates[i];
                if (part == null) continue;
                // A part that is still inside another thrown part goes with it.
                bool insideThrown = false;
                for (int j = 0; j < i; j++) if (candidates[j] != null && part.IsChildOf(candidates[j])) insideThrown = true;
                if (insideThrown) continue;
                BlowOff(part, centre, false);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsDead || Time.time - lastImpactTime < 0.25f) return;
            var contact = collision.GetContact(0);
            // Only the head-on part of the impact counts: scraping or skimming along the
            // ground at boost speed is not a crash.
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed < impactThreshold * 0.6f) return;
            lastImpactTime = Time.time;

            // Heavy things grinding together: sparks and a clang.
            Fx.Sparks(contact.point, contact.normal, Mathf.RoundToInt(Mathf.Clamp(speed * 2f, 8f, 60f)), speed, 100f);
            GameAudio.Play(Sfx.MetalHit, contact.point, Mathf.Clamp01(speed / 25f), Random.Range(0.6f, 0.9f));
            FollowCamera.Shake(Mathf.Clamp01(speed / 40f) * 0.5f, contact.point, 80f);

            if (speed < impactThreshold) return;
            // Landing on or bumping over the ground never hurts.
            if (collision.rigidbody == null && contact.normal.y > 0.6f) return;
            // Shells, missiles and debris do their damage through explosions instead.
            if (collision.rigidbody != null && collision.rigidbody.mass < 500f) return;

            // The faster and heavier the other object, the more it hurts.
            bool world = collision.rigidbody == null;
            float otherMass = world ? Body.mass * 0.6f : collision.rigidbody.mass;
            float massFactor = Mathf.Clamp(otherMass / Body.mass, 0.05f, 2.5f);
            float damage = (speed - impactThreshold) * impactDamagePerMs * massFactor;
            // Hitting a building or rock is a hard knock, never a kill: at most 8% of health.
            if (world) damage = Mathf.Min(damage, maxHealth * 0.08f);
            Vector3 toContact = (contact.point - Center).normalized;
            if (Vector3.Dot(toContact, transform.forward) > 0.5f) damage *= frontArmor;

            var attacker = collision.rigidbody != null ? collision.rigidbody.GetComponent<Damageable>() : null;
            if (attacker != null && attacker.team == team) return;
            TakeDamage(damage, contact.point, attacker);
        }
    }

    /// <summary>Components with this marker get switched off when their robot dies.</summary>
    public interface IDisableOnDeath { }

    /// <summary>Area damage and physics push.</summary>
    public static class Explosions
    {
        static readonly Collider[] Buffer = new Collider[128];
        // Separate buffer: a robot dying inside Detonate calls ApplyForce, which reuses Buffer.
        static readonly Collider[] DetonateBuffer = new Collider[128];
        static readonly HashSet<Damageable> HitRobots = new HashSet<Damageable>();
        static readonly HashSet<Rigidbody> Pushed = new HashSet<Rigidbody>();

        /// <summary>Damages robots in range (with falloff) and pushes physics objects.</summary>
        public static void Detonate(Vector3 pos, float radius, float damage, float force, Damageable attacker, float fxSize)
        {
            int n = Physics.OverlapSphereNonAlloc(pos, radius, DetonateBuffer, ~0, QueryTriggerInteraction.Ignore);
            // Went off on a machine (torn armour flies) or in the dirt (earth flies)?
            bool onMachine = false;
            for (int i = 0; i < n && !onMachine; i++)
            {
                var c = DetonateBuffer[i];
                if (c == null || c.attachedRigidbody == null) continue;
                if ((c.ClosestPoint(pos) - pos).sqrMagnitude < 9f) onMachine = true;
            }
            Fx.Explosion(pos, fxSize, onMachine);
            WorldDetail.Blast(pos, radius, damage);
            GameAudio.Play(fxSize > 1.5f ? Sfx.BigExplosion : Sfx.Explosion, pos, 1f, Random.Range(0.85f, 1.1f));
            if (fxSize >= 1f) GameAudio.Play(Sfx.Debris, pos, 0.7f, Random.Range(0.9f, 1.1f));
            FollowCamera.Shake(0.35f * fxSize, pos, 120f);

            var hitRobots = HitRobots;
            hitRobots.Clear();
            for (int i = 0; i < n; i++)
            {
                var d = DetonateBuffer[i].GetComponentInParent<Damageable>();
                if (d == null || d.IsDead || !hitRobots.Add(d)) continue;
                if (attacker != null && d.team == attacker.team && d != attacker) continue;
                if (d == attacker) continue;
                Vector3 closest = DetonateBuffer[i].ClosestPoint(pos);
                float falloff = Mathf.Clamp01(1f - Vector3.Distance(closest, pos) / radius);
                d.TakeDamage(damage * Mathf.Lerp(0.35f, 1f, falloff), closest, attacker);
            }

            ApplyForce(pos, radius * 1.3f, force, attacker);
        }

        /// <summary>Physics push only. Force is roughly the speed change (m/s) at the centre.</summary>
        public static void ApplyForce(Vector3 pos, float radius, float force, Damageable ignore)
        {
            var pushed = Pushed;
            pushed.Clear();
            int n = Physics.OverlapSphereNonAlloc(pos, radius, Buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var rb = Buffer[i].attachedRigidbody;
                if (rb == null || rb.isKinematic || !pushed.Add(rb)) continue;
                if (ignore != null && rb == ignore.Body) continue;
                float falloff = Mathf.Clamp01(1f - Vector3.Distance(rb.worldCenterOfMass, pos) / radius);
                // Giant robots shrug off small blasts; light objects fly.
                float massScale = Mathf.Clamp(3000f / rb.mass, 0.08f, 1f);
                Vector3 dir = (rb.worldCenterOfMass - pos).normalized + Vector3.up * 0.4f;
                Vector3 at = Buffer[i].ClosestPoint(pos);
                if (rb.GetComponent<Damageable>() != null)
                {
                    // Robots rock a little but don't get thrown about: softer push,
                    // applied closer to the centre of mass so it spins them less.
                    massScale *= 0.45f;
                    at = Vector3.Lerp(at, rb.worldCenterOfMass, 0.6f);
                }
                rb.AddForceAtPosition(dir.normalized * force * falloff * massScale, at, ForceMode.VelocityChange);
            }
        }
    }
}
