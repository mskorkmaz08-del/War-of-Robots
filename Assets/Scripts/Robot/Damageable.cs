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

        /// <summary>Damage enemies deal to the player is multiplied by this (difficulty).</summary>
        public static float EnemyDamageToPlayer = 0.45f;

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

        public void TakeDamage(float amount, Vector3 point, Damageable attacker = null)
        {
            if (NetClient) return;
            ApplyDamage(amount, point, attacker);
        }

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
            if (!raw && team == 0 && attacker != null && attacker.team != 0) amount *= EnemyDamageToPlayer;

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

        void BlowOff(Transform part, Vector3 hitPoint)
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
            var rb = part.gameObject.GetComponent<Rigidbody>();
            if (rb == null) rb = part.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(200f, part.lossyScale.x * part.lossyScale.y * part.lossyScale.z * 400f);
            Compat.SetVelocity(rb, Body != null ? Compat.GetVelocity(Body) : Vector3.zero);
            Vector3 away = (part.position - hitPoint).normalized + Vector3.up;
            rb.AddForce(away.normalized * 14f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
            Fx.Sparks(part.position, away, 30, 25f, 90f);
            Fx.Explosion(part.position, 0.6f * size);
            GameAudio.Play(Sfx.MetalHit, part.position, 1f, 0.7f);
            Destroy(part.gameObject, 12f);
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Health = 0f;

            Vector3 c = Center;
            Fx.Explosion(c, 3.5f * size);
            Fx.Explosion(c + Random.insideUnitSphere * 3f * size, 2f * size);
            GameAudio.Play(Sfx.BigExplosion, c, 1f, 0.8f);
            GameAudio.Play(Sfx.Debris, c, 1f, 0.8f);
            FollowCamera.Shake(0.9f, c, 200f);
            Explosions.ApplyForce(c, 25f * size, 8f, this);

            // Throw the remaining detachable parts.
            for (int i = partsDetached; i < detachableParts.Count; i++) BlowOff(detachableParts[i], c);
            partsDetached = detachableParts.Count;

            // Throw a few chunks of debris.
            var assets = GameAssets.Get();
            for (int i = 0; i < 6; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "Debris";
                chunk.transform.position = c + Random.insideUnitSphere * 2f * size;
                chunk.transform.rotation = Random.rotation;
                chunk.transform.localScale = Vector3.one * Random.Range(0.6f, 1.6f) * size;
                if (assets != null) chunk.GetComponent<Renderer>().sharedMaterial = assets.Burnt();
                var rb = chunk.AddComponent<Rigidbody>();
                rb.mass = 300f;
                rb.AddForce((Random.insideUnitSphere + Vector3.up * 1.5f).normalized * Random.Range(12f, 26f), ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
                Destroy(chunk, 10f);
            }

            // Burn the wreck.
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

            // Let physics take over: the wreck slumps and tips.
            if (Body != null)
            {
                Body.AddForce(Vector3.up * 6f, ForceMode.VelocityChange);
                Body.AddTorque(Random.insideUnitSphere * 1.5f, ForceMode.VelocityChange);
            }

            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == this) continue;
                if (behaviour is IDisableOnDeath) behaviour.enabled = false;
            }

            Died?.Invoke(this);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsDead || Time.time - lastImpactTime < 0.25f) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < impactThreshold * 0.6f) return;

            var contact = collision.GetContact(0);
            lastImpactTime = Time.time;

            // Heavy things grinding together: sparks and a clang.
            Fx.Sparks(contact.point, contact.normal, Mathf.RoundToInt(Mathf.Clamp(speed * 2f, 8f, 60f)), speed, 100f);
            GameAudio.Play(Sfx.MetalHit, contact.point, Mathf.Clamp01(speed / 25f), Random.Range(0.6f, 0.9f));
            FollowCamera.Shake(Mathf.Clamp01(speed / 40f) * 0.5f, contact.point, 80f);

            if (speed < impactThreshold) return;
            // Shells, missiles and debris do their damage through explosions instead.
            if (collision.rigidbody != null && collision.rigidbody.mass < 500f) return;

            // The faster and heavier the other object, the more it hurts.
            float otherMass = collision.rigidbody != null ? collision.rigidbody.mass : Body.mass * 2f;
            float massFactor = Mathf.Clamp(otherMass / Body.mass, 0.05f, 2.5f);
            float damage = (speed - impactThreshold) * impactDamagePerMs * massFactor;
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
            Fx.Explosion(pos, fxSize);
            GameAudio.Play(fxSize > 1.5f ? Sfx.BigExplosion : Sfx.Explosion, pos, 1f, Random.Range(0.85f, 1.1f));
            if (fxSize >= 1f) GameAudio.Play(Sfx.Debris, pos, 0.7f, Random.Range(0.9f, 1.1f));
            FollowCamera.Shake(0.35f * fxSize, pos, 120f);

            var hitRobots = HitRobots;
            hitRobots.Clear();
            int n = Physics.OverlapSphereNonAlloc(pos, radius, DetonateBuffer, ~0, QueryTriggerInteraction.Ignore);
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
