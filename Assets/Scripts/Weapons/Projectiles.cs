using System.Collections;
using UnityEngine;

namespace RobotArena
{
    /// <summary>Heavy cannon shell: real physics, drops with gravity, explodes on impact.</summary>
    public class Shell : MonoBehaviour
    {
        public Damageable owner;
        public float damage = 120f;
        public float radius = 7f;
        public float force = 9f;
        public float fxSize = 1.2f;
        bool exploded;

        public static Shell Spawn(Vector3 pos, Vector3 velocity, Damageable owner, Color glow, float size = 0.7f)
        {
            var assets = GameAssets.Get();
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shell";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            if (assets != null) go.GetComponent<Renderer>().sharedMaterial = assets.Glow(glow, 3f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 60f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Compat.SetVelocity(rb, velocity);

            IgnoreOwner(go.GetComponent<Collider>(), owner);
            AddTrail(go, glow, size * 1.2f, 0.25f);

            var shell = go.AddComponent<Shell>();
            shell.owner = owner;
            Destroy(go, 6f);
            return shell;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (exploded) return;
            exploded = true;
            Vector3 p = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Explosions.Detonate(p, radius, damage, force, owner, fxSize);
            Destroy(gameObject);
        }

        public static void IgnoreOwner(Collider c, Damageable owner)
        {
            if (c == null || owner == null) return;
            foreach (var oc in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(c, oc, true);
        }

        public static TrailRenderer AddTrail(GameObject go, Color color, float width, float time)
        {
            var assets = GameAssets.Get();
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = time;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.5f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (assets != null) trail.sharedMaterial = assets.ParticleAdditive();
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.3f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            return trail;
        }
    }

    /// <summary>Homing missile: pops out of the pod, then curves onto its target.</summary>
    public class Missile : MonoBehaviour
    {
        public Damageable owner;
        public Damageable target;
        public float damage = 55f;
        public float radius = 8f;
        public float speed = 70f;
        public float turnRate = 140f;
        public float launchTime = 0.4f;

        Rigidbody rb;
        ParticleSystem smoke;
        ParticleSystem flame;
        float age;
        bool exploded;
        Vector3 wobbleSeed;

        public static Missile Spawn(Vector3 pos, Vector3 launchDir, Damageable owner, Damageable target)
        {
            var assets = GameAssets.Get();
            var go = new GameObject("Missile");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(launchDir));

            var bodyMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyMesh.transform.SetParent(go.transform, false);
            bodyMesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bodyMesh.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            Destroy(bodyMesh.GetComponent<Collider>());
            if (assets != null) bodyMesh.GetComponent<Renderer>().sharedMaterial = assets.Paint(new Color(0.85f, 0.85f, 0.8f), 0.4f, 0.5f, false);

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.9f);
            tip.transform.localScale = Vector3.one * 0.42f;
            Destroy(tip.GetComponent<Collider>());
            if (assets != null) tip.GetComponent<Renderer>().sharedMaterial = assets.Glow(new Color(1f, 0.2f, 0.1f), 2f);

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            var m = go.AddComponent<Missile>();
            m.owner = owner;
            m.target = target;
            m.rb = go.AddComponent<Rigidbody>();
            m.rb.mass = 40f;
            m.rb.useGravity = false;
            m.rb.interpolation = RigidbodyInterpolation.Interpolate;
            m.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Compat.SetVelocity(m.rb, launchDir * 22f + (owner != null && owner.Body != null ? Compat.GetVelocity(owner.Body) : Vector3.zero));
            Shell.IgnoreOwner(col, owner);

            var tail = new GameObject("Tail").transform;
            tail.SetParent(go.transform, false);
            tail.localPosition = new Vector3(0f, 0f, -1f);
            tail.localRotation = Quaternion.LookRotation(Vector3.back);
            m.smoke = Fx.SmokeTrail(tail, 0.9f, new Color(0.75f, 0.75f, 0.75f, 0.55f));
            m.flame = Fx.ThrusterFlame(tail, 0.35f, new Color(1f, 0.6f, 0.2f));
            Fx.SetRate(m.flame, 80f);
            m.wobbleSeed = Random.insideUnitSphere * 10f;
            return m;
        }

        void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (age > 7f)
            {
                Explode(transform.position);
                return;
            }
            Vector3 v = Compat.GetVelocity(rb);
            Vector3 dir = v.sqrMagnitude > 0.01f ? v.normalized : transform.forward;

            if (age > launchTime)
            {
                Vector3 desired = dir;
                if (target != null && !target.IsDead)
                {
                    Vector3 tv = target.Body != null ? Compat.GetVelocity(target.Body) : Vector3.zero;
                    float t = Vector3.Distance(transform.position, target.Center) / speed;
                    desired = (target.Center + tv * t * 0.7f - transform.position).normalized;
                }
                // A little wobble so a salvo spreads out like a swarm.
                Vector3 wobble = new Vector3(
                    Mathf.PerlinNoise(wobbleSeed.x, age * 2f) - 0.5f,
                    Mathf.PerlinNoise(wobbleSeed.y, age * 2f) - 0.5f,
                    Mathf.PerlinNoise(wobbleSeed.z, age * 2f) - 0.5f) * 0.25f;
                desired = (desired + wobble).normalized;
                dir = Vector3.RotateTowards(dir, desired, turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                float currentSpeed = Mathf.MoveTowards(v.magnitude, speed, 90f * Time.fixedDeltaTime);
                Compat.SetVelocity(rb, dir * currentSpeed);
            }
            else
            {
                Compat.SetVelocity(rb, v * 0.97f + Vector3.up * 0.2f);
            }

            rb.MoveRotation(Quaternion.LookRotation(dir));
        }

        void OnCollisionEnter(Collision collision)
        {
            Explode(collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        void Explode(Vector3 p)
        {
            if (exploded) return;
            exploded = true;
            Fx.Detach(smoke, 2.5f);
            Fx.Detach(flame, 0.5f);
            Explosions.Detonate(p, radius, damage, 6f, owner, 1f);
            Destroy(gameObject);
        }
    }

    /// <summary>Short-lived glowing beam drawn with a LineRenderer.</summary>
    public class Beam : MonoBehaviour
    {
        // Lasers fire many beams a second, so finished beams are kept and reused.
        static readonly System.Collections.Generic.Stack<Beam> Free = new System.Collections.Generic.Stack<Beam>();
        static readonly System.Collections.Generic.Dictionary<Color, Gradient> Gradients = new System.Collections.Generic.Dictionary<Color, Gradient>();

        LineRenderer line;
        float life;
        float age;
        float width;

        public static Beam Spawn(Vector3 from, Vector3 to, float width, Color color, float duration)
        {
            Beam b = null;
            while (Free.Count > 0 && b == null) b = Free.Pop();
            if (b == null)
            {
                var go = new GameObject("Beam");
                var newLine = go.AddComponent<LineRenderer>();
                newLine.positionCount = 2;
                newLine.useWorldSpace = true;
                newLine.numCapVertices = 4;
                newLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                newLine.receiveShadows = false;
                var assets = GameAssets.Get();
                if (assets != null) newLine.sharedMaterial = assets.ParticleAdditive();
                b = go.AddComponent<Beam>();
                b.line = newLine;
            }

            if (!Gradients.TryGetValue(color, out var g))
            {
                g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.5f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                Gradients[color] = g;
            }

            var line = b.line;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = width;
            line.endWidth = width * 0.7f;
            line.colorGradient = g;
            b.life = duration;
            b.width = width;
            b.age = 0f;
            b.gameObject.SetActive(true);
            return b;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(age / life);
            line.startWidth = width * k;
            line.endWidth = width * 0.7f * k;
            if (age >= life)
            {
                gameObject.SetActive(false);
                Free.Push(this);
            }
        }
    }

    /// <summary>Helper to run a coroutine from static code.</summary>
    public class CoroutineHost : MonoBehaviour
    {
        static CoroutineHost instance;

        public static Coroutine Run(IEnumerator routine)
        {
            if (instance == null) instance = new GameObject("Coroutines").AddComponent<CoroutineHost>();
            return instance.StartCoroutine(routine);
        }
    }
}
