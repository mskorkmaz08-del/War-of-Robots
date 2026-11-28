using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Each robot's own superpower, on the power button (the old boost button).
    /// A power runs for a while, then recharges. The same commands drive it for the
    /// player, the AI and online robots, so it behaves the same everywhere.
    /// </summary>
    public abstract class Superpower : MonoBehaviour, IDisableOnDeath
    {
        /// <summary>Full name for the hangar ("FORTRESS MODE").</summary>
        public string title = "POWER";
        /// <summary>Short label for the button ("FORTRESS").</summary>
        public string button = "POWER";
        public float cooldown = 18f;
        public float duration = 6f;

        protected Damageable self;
        protected RobotCommands commands;
        protected Locomotion loco;
        protected WalkerLocomotion walker;
        protected WheeledLocomotion wheels;
        protected Rigidbody rb;
        protected Turret turret;
        protected Weapon[] weapons = new Weapon[0];
        protected Spinner[] spinners = new Spinner[0];

        float readyAt;
        float activeUntil;
        float startedAt;
        bool hullMeasured;
        Vector3 hullCenter;
        Vector3 hullExtents = Vector3.one * 3f;

        public bool Active { get; private set; }
        public bool IsReady => !Active && Time.time >= readyAt;
        /// <summary>Button ring: drains while active, fills while recharging.</summary>
        public float Ready01 => Active
            ? Mathf.Clamp01((activeUntil - Time.time) / Mathf.Max(0.01f, duration))
            : 1f - Mathf.Clamp01((readyAt - Time.time) / Mathf.Max(0.01f, cooldown));
        protected float Elapsed => Time.time - startedAt;
        protected bool IsPlayer => GetComponent<RobotInput>() != null;

        protected virtual void Awake()
        {
            self = GetComponent<Damageable>();
            commands = GetComponent<RobotCommands>();
            loco = GetComponent<Locomotion>();
            walker = loco as WalkerLocomotion;
            wheels = loco as WheeledLocomotion;
            rb = GetComponent<Rigidbody>();
            turret = GetComponent<Turret>();
            if (loco != null) loco.power = this;
            // Ready a few seconds into the fight, not on the first frame.
            readyAt = Time.time + 4f;
        }

        protected virtual void Start()
        {
            weapons = GetComponents<Weapon>();
            spinners = GetComponentsInChildren<Spinner>();
        }

        protected virtual void Update()
        {
            if (self != null && self.IsDead) return;
            if (Active)
            {
                Tick(Time.deltaTime);
                if (Active && Time.time >= activeUntil) Stop();
                return;
            }
            if (commands != null && commands.boost && IsReady && CanStart())
            {
                Active = true;
                startedAt = Time.time;
                activeUntil = Time.time + duration;
                Begin();
            }
        }

        void FixedUpdate()
        {
            if (Active && (self == null || !self.IsDead)) FixedTick(Time.fixedDeltaTime);
        }

        /// <summary>Ends the power now and starts the recharge.</summary>
        protected void Stop()
        {
            if (!Active) return;
            Active = false;
            readyAt = Time.time + cooldown;
            End();
        }

        void OnDisable()
        {
            if (Active)
            {
                Active = false;
                End();
            }
            if (loco != null) loco.powerThrust = false;
        }

        protected virtual bool CanStart() => true;
        protected abstract void Begin();
        protected virtual void Tick(float dt) { }
        protected virtual void FixedTick(float dt) { }
        protected abstract void End();

        /// <summary>Should the AI use it now against this target?</summary>
        public abstract bool AiWants(Damageable target, float dist);

        // ---------- Shared helpers ----------

        protected float Size => self != null ? Mathf.Max(0.8f, self.size) : 1f;
        protected int Team => self != null ? self.team : 0;
        protected float HealthFraction => self != null ? self.HealthFraction : 1f;

        /// <summary>The hull's own bounds in local space (from its renderers), measured once.</summary>
        protected Vector3 HullCenter { get { MeasureHull(); return hullCenter; } }
        protected Vector3 HullExtents { get { MeasureHull(); return hullExtents; } }

        void MeasureHull()
        {
            if (hullMeasured) return;
            hullMeasured = true;
            bool any = false;
            Bounds b = default;
            var inv = transform.worldToLocalMatrix;
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponent<EnergyShell>() != null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                var m = inv * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            if (any)
            {
                hullCenter = b.center;
                hullExtents = b.extents;
            }
        }

        /// <summary>A shell radius that wraps the whole machine.</summary>
        protected float WrapRadius => Mathf.Max(HullExtents.x, HullExtents.y, HullExtents.z) * 1.08f;

        protected static readonly List<Damageable> Found = new List<Damageable>();

        /// <summary>Enemy machines within range of a point (fills Found).</summary>
        protected List<Damageable> EnemiesNear(Vector3 pos, float radius)
        {
            Found.Clear();
            float r2 = radius * radius;
            foreach (var d in Damageable.All)
            {
                if (d == null || d == self || d.IsDead || d.team == Team) continue;
                if ((d.Center - pos).sqrMagnitude <= r2) Found.Add(d);
            }
            return Found;
        }

        /// <summary>Throws another machine, scaled down for heavier targets.</summary>
        protected void Shove(Damageable d, Vector3 dir, float speed)
        {
            var body = d.Body;
            if (body == null || body.isKinematic) return;
            float ownMass = rb != null ? rb.mass : 30000f;
            float k = Mathf.Clamp(ownMass / Mathf.Max(1000f, body.mass), 0.35f, 1.4f);
            body.AddForce(dir.normalized * speed * k, ForceMode.VelocityChange);
        }

        /// <summary>Knocks a walker off its feet, or rocks a wheeled machine hard.</summary>
        protected void KnockOver(Damageable d, Vector3 away)
        {
            var w = d.GetComponent<WalkerLocomotion>();
            if (w != null && w.KnockDown(away)) return;
            if (w != null) w.Stagger(away, 1f);
            Shove(d, away + Vector3.up * 0.6f, 7f);
        }

        protected void Stagger(Damageable d, Vector3 away, float severity)
        {
            var w = d.GetComponent<WalkerLocomotion>();
            if (w != null) w.Stagger(away, severity);
        }

        protected static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        protected void SetSpin(float boost)
        {
            foreach (var s in spinners) if (s != null) s.boost = boost;
        }

        protected void SetRate(float mul)
        {
            foreach (var w in weapons) if (w != null) w.RateMul = mul;
        }

        // ---------- Which robot gets which power ----------

        public static Superpower Attach(GameObject go, RobotType type)
        {
            Superpower p;
            switch (type)
            {
                case RobotType.Titan: p = go.AddComponent<FortressMode>(); break;
                case RobotType.Raptor: p = go.AddComponent<RocketRam>(); break;
                case RobotType.Arachne: p = go.AddComponent<Pounce>(); break;
                case RobotType.Hornet: p = go.AddComponent<EmpBurst>(); break;
                case RobotType.Juggernaut: p = go.AddComponent<Bulldoze>(); break;
                case RobotType.Lancer: p = go.AddComponent<SkyPerch>(); break;
                case RobotType.Bastion: p = go.AddComponent<DozerShield>(); break;
                case RobotType.Vanguard: p = go.AddComponent<Aegis>(); break;
                case RobotType.Mammoth: p = go.AddComponent<QuakeStomp>(); break;
                case RobotType.Strider: p = go.AddComponent<HunterSprint>(); break;
                case RobotType.Colossus: p = go.AddComponent<ReactorOverdrive>(); break;
                case RobotType.Scarab: p = go.AddComponent<NanoRepair>(); break;
                default: p = go.AddComponent<FortressMode>(); break;
            }
            return p;
        }

        /// <summary>What the hangar says about each robot's power.</summary>
        public static string Describe(RobotType type)
        {
            switch (type)
            {
                case RobotType.Titan: return "FORTRESS MODE: plants its feet, takes 60% less damage and hits 25% harder";
                case RobotType.Raptor: return "ROCKET RAM: rockets into the enemy, smashing them with its weight";
                case RobotType.Arachne: return "POUNCE: leaps onto the target and lands with a shockwave";
                case RobotType.Hornet: return "EMP BURST: jams every enemy gun nearby for a few seconds";
                case RobotType.Juggernaut: return "BULLDOZE: an armoured charge that knocks robots off their feet";
                case RobotType.Lancer: return "SKY PERCH: rockets high into the sky, then glides down sniping";
                case RobotType.Bastion: return "DOZER SHIELD: an energy wall over the blade stops fire from the front";
                case RobotType.Vanguard: return "AEGIS: a shield bubble soaks up a heavy beating";
                case RobotType.Mammoth: return "QUAKE STOMP: rears up and slams down, flattening everything near";
                case RobotType.Strider: return "HUNTER SPRINT: a burst of speed that makes it hard to hit";
                case RobotType.Colossus: return "REACTOR OVERDRIVE: turbines howl, guns fire faster and harder";
                case RobotType.Scarab: return "NANO REPAIR: a swarm of nanites rebuilds a third of its armour";
            }
            return "";
        }
    }

    // ======================================================================
    // TITAN: the walking fortress digs in.
    // ======================================================================
    public class FortressMode : Superpower
    {
        EnergyShell shell;
        float nextFx;

        protected override void Awake()
        {
            base.Awake();
            title = "FORTRESS MODE"; button = "FORTRESS"; cooldown = 22f; duration = 8f;
        }

        protected override void Begin()
        {
            self.DamageTakenMul = 0.4f;
            self.DamageDealtMul = 1.25f;
            if (loco != null) loco.moveScale = 0f;
            shell = PowerFx.Bubble(transform, HullCenter, WrapRadius, new Color(1f, 0.62f, 0.18f));
            Vector3 feet = transform.position + Vector3.down * (walker != null ? walker.standHeight : 2f);
            Fx.Dust(feet, 2.2f * Size);
            PowerFx.Shockwave(feet, 6f * Size, new Color(1f, 0.6f, 0.2f));
            GameAudio.Play(Sfx.Landing, feet, 1f, 0.6f);
            GameAudio.Play(Sfx.Hiss, transform.position, 0.9f, 0.7f);
            FollowCamera.Shake(0.35f, transform.position, 90f);
        }

        protected override void Tick(float dt)
        {
            if (Time.time < nextFx) return;
            nextFx = Time.time + 0.25f;
            // Vents bleed heat while the reactor feeds the shield.
            Vector3 p = transform.TransformPoint(HullCenter + Vector3.Scale(Random.onUnitSphere, HullExtents));
            Fx.EnergyBurst(p, new Color(1f, 0.55f, 0.15f), 0.35f * Size);
        }

        protected override void End()
        {
            if (self != null) { self.DamageTakenMul = 1f; self.DamageDealtMul = 1f; }
            if (loco != null) loco.moveScale = 1f;
            if (shell != null) shell.Dismiss();
            GameAudio.Play(Sfx.Hiss, transform.position, 0.6f, 0.9f);
        }

        public override bool AiWants(Damageable target, float dist) => dist < 170f && HealthFraction < 0.85f;
    }

    // ======================================================================
    // RAPTOR and JUGGERNAUT: speed is a weapon.
    // ======================================================================
    public abstract class RamPower : Superpower
    {
        protected float accelMul = 1.8f, speedMul = 1.25f, damagePerMs = 30f, armour = 1f, keepSpeed = 0.55f;
        protected bool knocks;
        readonly HashSet<Damageable> hit = new HashSet<Damageable>();
        float baseAccel, baseMax;
        static readonly Collider[] Overlap = new Collider[48];

        protected override bool CanStart() => wheels != null || walker != null;

        protected override void Begin()
        {
            hit.Clear();
            if (wheels != null)
            {
                baseAccel = wheels.boostAccel; baseMax = wheels.boostMaxSpeed;
                wheels.boostAccel *= accelMul;
                wheels.boostMaxSpeed *= speedMul;
            }
            if (loco != null) loco.powerThrust = true;
            self.DamageTakenMul = armour;
            GameAudio.Play(Sfx.Whoosh, transform.position, 1f, 0.8f);
            GameAudio.Play(Sfx.MissileLaunch, transform.position, 0.7f, 0.6f);
        }

        protected override void FixedTick(float dt)
        {
            if (rb == null) return;
            Vector3 fwd = transform.forward;
            float speed = Vector3.Dot(Compat.GetVelocity(rb), fwd);
            if (speed < 6f) return;
            // Whatever is right in front of the nose takes the hit.
            Vector3 nose = transform.TransformPoint(HullCenter + Vector3.forward * HullExtents.z);
            float r = Mathf.Max(HullExtents.x, HullExtents.y) * 1.1f;
            int n = Physics.OverlapSphereNonAlloc(nose, r, Overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var d = Overlap[i].GetComponentInParent<Damageable>();
                if (d == null || d == self || d.IsDead || d.team == Team || !hit.Add(d)) continue;
                Smash(d, Overlap[i].ClosestPoint(nose), speed, fwd);
            }
        }

        void Smash(Damageable d, Vector3 at, float speed, Vector3 fwd)
        {
            float massK = Mathf.Clamp((rb != null ? rb.mass : 30000f) / 30000f, 0.6f, 1.6f);
            float dmg = speed * damagePerMs * massK;
            d.TakeDamage(dmg, at, self);
            Vector3 away = (Flat(fwd) + Vector3.up * 0.25f).normalized;
            Shove(d, away, speed * 0.55f);
            if (knocks) KnockOver(d, away);
            else Stagger(d, away, 1f);
            // The rammer loses speed in the hit.
            if (rb != null) Compat.SetVelocity(rb, Compat.GetVelocity(rb) * keepSpeed);
            Fx.Sparks(at, -fwd, 60, speed, 110f);
            Fx.SmallBlast(at, 1.2f * Size);
            GameAudio.Play(Sfx.MetalHit, at, 1f, 0.55f);
            GameAudio.Play(Sfx.Debris, at, 0.8f, 0.8f);
            FollowCamera.Shake(0.7f, at, 120f);
            WorldDetail.Blast(at, 6f * Size, dmg * 0.5f);
        }

        protected override void End()
        {
            if (wheels != null && baseMax > 0f)
            {
                wheels.boostAccel = baseAccel;
                wheels.boostMaxSpeed = baseMax;
            }
            if (loco != null) loco.powerThrust = false;
            if (self != null) self.DamageTakenMul = 1f;
        }

        public override bool AiWants(Damageable target, float dist)
        {
            if (target == null || dist < 18f || dist > 95f) return false;
            Vector3 to = Flat(target.Center - transform.position);
            return Vector3.Angle(Flat(transform.forward), to) < 12f;
        }
    }

    public class RocketRam : RamPower
    {
        protected override void Awake()
        {
            base.Awake();
            title = "ROCKET RAM"; button = "RAM"; cooldown = 12f; duration = 2.6f;
            accelMul = 2.0f; speedMul = 1.3f; damagePerMs = 24f; armour = 0.85f; keepSpeed = 0.5f;
        }
    }

    public class Bulldoze : RamPower
    {
        EnergyShell shell;

        protected override void Awake()
        {
            base.Awake();
            title = "BULLDOZE"; button = "BULLDOZE"; cooldown = 16f; duration = 3.4f;
            accelMul = 1.7f; speedMul = 1.2f; damagePerMs = 30f; armour = 0.5f; keepSpeed = 0.85f; knocks = true;
        }

        protected override void Begin()
        {
            base.Begin();
            // The ram plate glows: a short energy wall over the nose.
            shell = PowerFx.Wall(transform, new Vector3(0f, HullCenter.y - HullExtents.y, HullCenter.z + HullExtents.z * 0.6f),
                110f, HullExtents.x * 1.4f, HullExtents.y * 1.5f, new Color(1f, 0.45f, 0.15f));
        }

        protected override void End()
        {
            base.End();
            if (shell != null) shell.Dismiss();
        }
    }

    // ======================================================================
    // ARACHNE: the spider jumps on its prey.
    // ======================================================================
    public class Pounce : Superpower
    {
        bool airborne, landed;

        protected override void Awake()
        {
            base.Awake();
            title = "POUNCE"; button = "POUNCE"; cooldown = 14f; duration = 3.5f;
        }

        protected override bool CanStart() => walker != null && walker.IsGrounded && !walker.IsKnockedDown;

        protected override void Begin()
        {
            airborne = landed = false;
            Vector3 dir = Flat(transform.forward).normalized;
            float dist = 30f;
            var t = turret != null ? turret.Target : null;
            if (t != null && !t.IsDead)
            {
                Vector3 to = Flat(t.Center - transform.position);
                if (to.magnitude < 70f) { dir = to.normalized; dist = Mathf.Max(8f, to.magnitude - HullExtents.z * 1.5f); }
            }
            else if (commands.hasMove && commands.move.sqrMagnitude > 0.04f) dir = Flat(commands.move).normalized;
            dist = Mathf.Clamp(dist, 10f, 48f);
            float g = Mathf.Abs(Physics.gravity.y) * (1f + walker.extraAirGravity);
            float vy = 19f;
            float flight = 2f * vy / g;
            Vector3 v = Compat.GetVelocity(rb);
            Vector3 want = dir * (dist / flight) + Vector3.up * vy;
            rb.AddForce(want - v, ForceMode.VelocityChange);
            Vector3 feet = transform.position + Vector3.down * walker.standHeight;
            Fx.Dust(feet, 1.8f * Size);
            GameAudio.Play(Sfx.Servo, transform.position, 1f, 0.6f);
            GameAudio.Play(Sfx.Hiss, transform.position, 0.8f, 1.1f);
        }

        protected override void FixedTick(float dt)
        {
            if (landed) return;
            if (!airborne && Elapsed > 0.15f && !walker.IsGrounded) airborne = true;
            if (airborne && walker.IsGrounded) Slam();
            else if (Elapsed > 0.6f && walker.IsGrounded) Slam();
        }

        void Slam()
        {
            landed = true;
            Vector3 pos = transform.position + Vector3.down * walker.standHeight;
            float radius = 9f * Size;
            foreach (var d in EnemiesNear(pos, radius))
            {
                float k = Mathf.Clamp01(1f - Vector3.Distance(d.Center, pos) / (radius * 1.2f));
                d.TakeDamage(Mathf.Lerp(120f, 320f, k), d.Center, self);
                Vector3 away = (Flat(d.Center - pos).normalized + Vector3.up * 0.3f).normalized;
                Stagger(d, away, 1f);
                Shove(d, away, 5f * k);
            }
            PowerFx.Shockwave(pos, radius, new Color(0.55f, 0.9f, 1f));
            Fx.Dust(pos, 3f * Size);
            WorldDetail.Blast(pos, radius, 300f);
            GameAudio.Play(Sfx.Landing, pos, 1f, 0.7f);
            GameAudio.Play(Sfx.Explosion, pos, 0.8f, 0.6f);
            FollowCamera.Shake(0.8f, pos, 120f);
            Stop();
        }

        protected override void End() { }

        public override bool AiWants(Damageable target, float dist) => target != null && dist > 14f && dist < 48f;
    }

    // ======================================================================
    // HORNET: the tesla raider shorts out everything around it.
    // ======================================================================
    public class EmpBurst : Superpower
    {
        protected override void Awake()
        {
            base.Awake();
            title = "EMP BURST"; button = "EMP"; cooldown = 20f; duration = 0.4f;
        }

        protected override void Begin()
        {
            Color c = new Color(0.45f, 0.8f, 1f);
            Vector3 pos = self.Center;
            float radius = 50f;
            foreach (var d in EnemiesNear(pos, radius))
            {
                foreach (var w in d.GetComponents<Weapon>()) w.JammedUntil = Time.time + 4.5f;
                d.TakeDamage(140f, d.Center, self);
                Arc(pos, d.Center, c);
                var jam = d.GetComponent<JammedFx>();
                if (jam == null) jam = d.gameObject.AddComponent<JammedFx>();
                jam.until = Time.time + 4.5f;
            }
            Fx.EnergyBurst(pos, c, 2.2f * Size);
            PowerFx.Shockwave(pos + Vector3.down * 1.5f, radius, c, 0.45f);
            GameAudio.Play(Sfx.Tesla, pos, 1f, 0.7f);
            GameAudio.Play(Sfx.Sizzle, pos, 1f, 0.8f);
            FollowCamera.Shake(0.4f, pos, 90f);
        }

        /// <summary>A jagged lightning arc out to a target.</summary>
        static void Arc(Vector3 from, Vector3 to, Color c)
        {
            Vector3 prev = from;
            const int segs = 5;
            for (int i = 1; i <= segs; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)segs);
                if (i < segs) p += Random.insideUnitSphere * 2.5f;
                Beam.Spawn(prev, p, 0.55f, c, 0.3f);
                prev = p;
            }
            Fx.EnergyBurst(to, c, 1f);
        }

        protected override void End() { }

        public override bool AiWants(Damageable target, float dist) => target != null && dist < 42f;
    }

    /// <summary>Shows that a robot's guns are jammed: sparks crawl over it.</summary>
    public class JammedFx : MonoBehaviour
    {
        public float until;
        float next;
        Damageable d;

        void Awake() { d = GetComponent<Damageable>(); }

        void Update()
        {
            if (d == null || d.IsDead || Time.time > until) return;
            if (Time.time < next) return;
            next = Time.time + 0.12f;
            Fx.EnergyBurst(d.Center + Random.insideUnitSphere * 2.5f * Mathf.Max(1f, d.size), new Color(0.45f, 0.8f, 1f), 0.35f);
        }
    }

    // ======================================================================
    // LANCER: the sniper takes the high ground.
    // ======================================================================
    public class SkyPerch : Superpower
    {
        // A rocket jump, not a hover: the jets throw it high and keep it climbing, then
        // brake a long, slow glide back down while it keeps its momentum and can steer.
        const float Crouch = 0.3f, Launch = 1.1f, Climb = 1.2f;
        bool airborne;

        protected override void Awake()
        {
            base.Awake();
            title = "SKY PERCH"; button = "PERCH"; cooldown = 16f; duration = 7f;
        }

        protected override bool CanStart() => walker != null && !walker.IsKnockedDown;

        protected override void Begin()
        {
            airborne = false;
            loco.powerThrust = true;
            self.DamageDealtMul = 1.2f;
            GameAudio.Play(Sfx.Whoosh, transform.position, 1f, 0.7f);
        }

        protected override void FixedTick(float dt)
        {
            if (rb == null) return;
            float t = Elapsed;
            if (t < Crouch) return; // the crouch before lift-off
            Vector3 v = Compat.GetVelocity(rb);
            if (!airborne && t > Crouch + 0.25f && !walker.IsGrounded) airborne = true;
            if (airborne && walker.IsGrounded) { Stop(); return; }

            Vector3 steer = commands.hasMove ? Vector3.ClampMagnitude(new Vector3(commands.move.x, 0f, commands.move.z), 1f) : Vector3.zero;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (t < Crouch + Launch)
            {
                // Lift-off: a hard kick that builds to a fast climb.
                v.y = Mathf.MoveTowards(v.y, 24f, 75f * dt);
                flat = Vector3.MoveTowards(flat, flat + steer * 9f, 10f * dt);
            }
            else if (t < Crouch + Launch + Climb)
            {
                // Still gaining height, slowing toward the top of the arc.
                v.y = Mathf.MoveTowards(v.y, 6f, 16f * dt);
                flat = Vector3.MoveTowards(flat, steer * 16f, 6f * dt);
            }
            else
            {
                // Glide: the jets only brake the fall, which gets faster as they tire.
                // Momentum carries on; the stick steers the drift.
                float k = Mathf.Clamp01((t - Crouch - Launch - Climb) / Mathf.Max(0.1f, duration - Crouch - Launch - Climb));
                float sink = Mathf.Lerp(3.5f, 9f, k);
                v.y = Mathf.MoveTowards(v.y, -sink, 14f * dt);
                flat = Vector3.MoveTowards(flat, steer * 14f, (steer.sqrMagnitude > 0.01f ? 5f : 1.2f) * dt);
            }
            v.x = flat.x; v.z = flat.z;
            Compat.SetVelocity(rb, v);
        }

        protected override void End()
        {
            if (loco != null) loco.powerThrust = false;
            if (self != null) self.DamageDealtMul = 1f;
        }

        public override bool AiWants(Damageable target, float dist) => target != null && dist > 70f && dist < 230f && walker != null && walker.IsGrounded;
    }

    // ======================================================================
    // BASTION: the dozer blade becomes an energy wall.
    // ======================================================================
    public class DozerShield : Superpower
    {
        EnergyShell shell;
        float nextSpark;

        protected override void Awake()
        {
            base.Awake();
            title = "DOZER SHIELD"; button = "SHIELD"; cooldown = 20f; duration = 9f;
        }

        protected override void Begin()
        {
            self.Absorb = Filter;
            Vector3 ext = HullExtents, c = HullCenter;
            shell = PowerFx.Wall(transform, new Vector3(0f, c.y - ext.y, c.z - ext.z * 0.2f), 120f, ext.z * 1.15f, ext.y * 2.4f, new Color(0.35f, 0.8f, 1f));
            GameAudio.Play(Sfx.RailCharge, transform.position, 0.8f, 0.6f);
        }

        float Filter(float amount, Vector3 point)
        {
            Vector3 to = Flat(point - transform.TransformPoint(HullCenter));
            if (Vector3.Dot(to.normalized, Flat(transform.forward).normalized) < 0.25f) return amount;
            if (Time.time > nextSpark)
            {
                nextSpark = Time.time + 0.08f;
                Fx.EnergyBurst(point, new Color(0.35f, 0.8f, 1f), 0.5f);
            }
            if (shell != null) shell.Flare(0.8f);
            return amount * 0.12f;
        }

        protected override void End()
        {
            if (self != null && self.Absorb == Filter) self.Absorb = null;
            if (shell != null) shell.Dismiss();
        }

        public override bool AiWants(Damageable target, float dist)
        {
            if (target == null || dist > 170f) return false;
            return Vector3.Angle(Flat(transform.forward), Flat(target.Center - transform.position)) < 35f;
        }
    }

    // ======================================================================
    // VANGUARD: the knight raises a full shield bubble.
    // ======================================================================
    public class Aegis : Superpower
    {
        EnergyShell shell;
        float pool;
        const float Capacity = 1100f;

        protected override void Awake()
        {
            base.Awake();
            title = "AEGIS"; button = "AEGIS"; cooldown = 20f; duration = 8f;
        }

        protected override void Begin()
        {
            pool = Capacity;
            self.Absorb = Soak;
            shell = PowerFx.Bubble(transform, HullCenter, WrapRadius, new Color(0.35f, 0.7f, 1f));
            GameAudio.Play(Sfx.RailCharge, transform.position, 0.9f, 0.8f);
        }

        float Soak(float amount, Vector3 point)
        {
            float taken = Mathf.Min(pool, amount);
            pool -= taken;
            if (shell != null) shell.Flare(Mathf.Clamp(amount / 150f, 0.3f, 1.2f));
            if (pool <= 0f)
            {
                // The shield gives out with a crack.
                Fx.EnergyBurst(self.Center, new Color(0.35f, 0.7f, 1f), 2f * Size);
                GameAudio.Play(Sfx.Tesla, transform.position, 1f, 1.3f);
                Stop();
            }
            return amount - taken;
        }

        protected override void End()
        {
            if (self != null && self.Absorb == Soak) self.Absorb = null;
            if (shell != null) shell.Dismiss();
        }

        public override bool AiWants(Damageable target, float dist) => target != null && dist < 150f && HealthFraction < 0.75f;
    }

    // ======================================================================
    // MAMMOTH: the siege beast rears up and stomps.
    // ======================================================================
    public class QuakeStomp : Superpower
    {
        bool slammed;

        protected override void Awake()
        {
            base.Awake();
            title = "QUAKE STOMP"; button = "STOMP"; cooldown = 18f; duration = 1.8f;
        }

        protected override bool CanStart() => walker != null && walker.IsGrounded && !walker.IsKnockedDown;

        protected override void Begin()
        {
            slammed = false;
            // Rear up: the front lifts as the hull heaves upward.
            rb.AddForce(Vector3.up * 5.5f, ForceMode.VelocityChange);
            rb.AddTorque(-transform.right * 0.6f, ForceMode.VelocityChange);
            GameAudio.Play(Sfx.Servo, transform.position, 1f, 0.5f);
            GameAudio.Play(Sfx.Hiss, transform.position, 1f, 0.6f);
        }

        protected override void FixedTick(float dt)
        {
            if (slammed) return;
            if (Elapsed > 0.3f) rb.AddForce(Vector3.down * 30f, ForceMode.Acceleration);
            if (Elapsed > 0.35f && walker.IsGrounded || Elapsed > 1.2f) Slam();
        }

        void Slam()
        {
            slammed = true;
            Vector3 pos = transform.position + Vector3.down * walker.standHeight;
            float radius = 11f * Size;
            foreach (var d in EnemiesNear(pos, radius))
            {
                float dist = Vector3.Distance(Flat(d.Center), Flat(pos));
                float k = Mathf.Clamp01(1f - dist / radius);
                d.TakeDamage(Mathf.Lerp(110f, 380f, k), d.Center, self);
                Vector3 away = (Flat(d.Center - pos).normalized + Vector3.up * 0.4f).normalized;
                if (dist < radius * 0.6f && d.Body != null && rb != null && d.Body.mass < rb.mass * 1.2f) KnockOver(d, away);
                else Stagger(d, away, 1f);
                Shove(d, away, 6f * k);
            }
            PowerFx.Shockwave(pos, radius, new Color(1f, 0.6f, 0.25f), 0.7f);
            PowerFx.Shockwave(pos, radius * 0.6f, new Color(1f, 0.85f, 0.5f), 0.45f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Fx.Dust(pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.4f, 2.4f * Size);
            }
            WorldDetail.Blast(pos, radius, 420f);
            Explosions.ApplyForce(pos, radius, 6f, self);
            GameAudio.Play(Sfx.BigExplosion, pos, 1f, 0.55f);
            GameAudio.Play(Sfx.Landing, pos, 1f, 0.5f);
            FollowCamera.Shake(1.1f, pos, 160f);
        }

        protected override void End()
        {
            if (!slammed && walker != null && self != null && !self.IsDead) Slam();
        }

        public override bool AiWants(Damageable target, float dist) => target != null && dist < 6f * Size && walker != null && walker.IsGrounded;
    }

    // ======================================================================
    // STRIDER: the scout runs flat out.
    // ======================================================================
    public class HunterSprint : Superpower
    {
        protected override void Awake()
        {
            base.Awake();
            title = "HUNTER SPRINT"; button = "SPRINT"; cooldown = 14f; duration = 6f;
        }

        protected override void Begin()
        {
            loco.powerThrust = true;
            self.DamageTakenMul = 0.65f;
            SetSpin(3f);
            GameAudio.Play(Sfx.Whoosh, transform.position, 1f, 1.1f);
        }

        protected override void End()
        {
            if (loco != null) loco.powerThrust = false;
            if (self != null) self.DamageTakenMul = 1f;
            SetSpin(1f);
        }

        public override bool AiWants(Damageable target, float dist) => target != null && (dist > 110f || (HealthFraction < 0.5f && dist < 60f));
    }

    // ======================================================================
    // COLOSSUS: the twin reactors run past their limits.
    // ======================================================================
    public class ReactorOverdrive : Superpower
    {
        float nextFx;

        protected override void Awake()
        {
            base.Awake();
            title = "REACTOR OVERDRIVE"; button = "OVERDRIVE"; cooldown = 24f; duration = 8f;
        }

        protected override void Begin()
        {
            SetRate(1.5f);
            self.DamageDealtMul = 1.3f;
            SetSpin(3.5f);
            GameAudio.Play(Sfx.RailCharge, transform.position, 1f, 0.5f);
            Fx.EnergyBurst(self.Center, new Color(1f, 0.45f, 0.15f), 1.5f * Size);
        }

        protected override void Tick(float dt)
        {
            if (Time.time < nextFx) return;
            nextFx = Time.time + 0.12f;
            foreach (var s in spinners)
                if (s != null && Random.value < 0.5f)
                    Fx.EnergyBurst(s.transform.position + Random.insideUnitSphere * 0.6f * Size, new Color(1f, 0.5f, 0.15f), 0.4f * Size);
        }

        protected override void End()
        {
            SetRate(1f);
            if (self != null) self.DamageDealtMul = 1f;
            SetSpin(1f);
        }

        public override bool AiWants(Damageable target, float dist) => target != null && dist < 180f;
    }

    // ======================================================================
    // SCARAB: the armoured beetle rebuilds its shell.
    // ======================================================================
    public class NanoRepair : Superpower
    {
        float nextFx;

        protected override void Awake()
        {
            base.Awake();
            title = "NANO REPAIR"; button = "REPAIR"; cooldown = 26f; duration = 6f;
        }

        protected override void Begin()
        {
            self.DamageTakenMul = 0.85f;
            GameAudio.Play(Sfx.Confirm, transform.position, 0.8f, 0.8f);
            GameAudio.Play(Sfx.Sizzle, transform.position, 0.6f, 1.3f);
        }

        protected override void Tick(float dt)
        {
            self.Heal(self.maxHealth * 0.34f * dt / duration);
            if (Time.time < nextFx) return;
            nextFx = Time.time + 0.1f;
            Vector3 p = transform.TransformPoint(HullCenter + Vector3.Scale(Random.insideUnitSphere, HullExtents));
            Fx.EnergyBurst(p, new Color(0.35f, 1f, 0.55f), 0.35f * Size);
        }

        protected override void End()
        {
            if (self != null) self.DamageTakenMul = 1f;
        }

        public override bool AiWants(Damageable target, float dist) => HealthFraction < 0.55f;
    }
}
