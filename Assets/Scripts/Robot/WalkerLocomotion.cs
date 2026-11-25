using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Giant two-legged walker. The hull is held up by a heavy, springy "stance"
    /// force and steered with physics; the legs are animated procedurally with
    /// two-bone IK so the feet plant on the ground and stomp from step to step.
    /// Every footstep thuds, kicks up dust and shakes the camera.
    /// Boost fires jump jets; landing from a jump causes a shockwave.
    /// </summary>
    public class WalkerLocomotion : Locomotion
    {
        [System.Serializable]
        public class Leg
        {
            public Transform hip;
            public Transform thigh;
            public Transform knee;
            public Transform shin;
            public Transform foot;
            public float side = 1f;
            [Tooltip("Where the foot rests relative to the hip (local, flat). Zero = just under the hip.")]
            public Vector3 footOffset;
            [Tooltip("Which way the knee bends (local). Zero = forward, like a person.")]
            public Vector3 bendHint;
            [System.NonSerialized] public Vector3 footPos;
            [System.NonSerialized] public Vector3 stepFrom;
            [System.NonSerialized] public Vector3 stepTo;
            [System.NonSerialized] public float stepT = 1f;
            [System.NonSerialized] public bool planted = true;
        }

        public Leg[] legs = new Leg[0];
        public Transform[] jetNozzles = new Transform[0];

        [Header("Body")]
        public float standHeight = 7f;
        public float heightStiffness = 9f;
        public float heightDamping = 6f;
        public float uprightStiffness = 14f;
        public float uprightDamping = 6f;

        [Header("Walking")]
        public float walkSpeed = 7.5f;
        public float acceleration = 3.2f;
        public float turnRate = 0.75f;
        public float strideLength = 4.2f;
        public float stepDuration = 0.55f;
        public float stepHeight = 1.8f;
        public float thighLength = 4.2f;
        public float shinLength = 4.4f;

        [Header("Jump jets")]
        public float jetAccel = 22f;
        public float landingShockSpeed = 7f;

        ParticleSystem[] jets = new ParticleSystem[0];
        AudioSource jetAudio;
        AudioSource humAudio;
        bool grounded = true;
        float groundY;
        Vector3 groundNormal = Vector3.up;
        float airTime;
        float lastFallSpeed;
        int nextLeg;
        float idleTimer;
        float bodyDip;
        float gaitBob;

        public override bool IsGrounded => grounded;

        void Start()
        {
            foreach (var leg in legs)
            {
                Vector3 home = FootHome(leg, Vector3.zero);
                leg.footPos = home;
                leg.stepTo = home;
                leg.stepFrom = home;
                leg.stepT = 1f;
                leg.planted = true;
            }

            jets = new ParticleSystem[jetNozzles.Length];
            for (int i = 0; i < jetNozzles.Length; i++)
                jets[i] = Fx.ThrusterFlame(jetNozzles[i], 1.4f, new Color(1f, 0.55f, 0.2f));

            jetAudio = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(jetAudio, 25f, 320f);
            jetAudio.clip = GameAudio.ThrustLoop();
            jetAudio.loop = true;
            jetAudio.volume = 0f;
            jetAudio.Play();

            humAudio = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(humAudio, 15f, 160f);
            humAudio.clip = GameAudio.EngineLoop();
            humAudio.loop = true;
            humAudio.volume = 0.25f;
            humAudio.pitch = 0.6f;
            humAudio.Play();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool boosting = UpdateBoost(commands.boost);
            Vector3 velocity = Compat.GetVelocity(rb);

            // Find the ground under the hull.
            Vector3 probeOrigin = rb.position + Vector3.up * 2f;
            bool hasGround = GroundProbe.Cast(probeOrigin, Vector3.down, standHeight + 4.5f, rb, out RaycastHit hit, true);
            float height = hasGround ? probeOrigin.y - 2f - hit.point.y : float.MaxValue;
            bool wasGrounded = grounded;
            grounded = hasGround && height < standHeight + 1.2f;
            if (hasGround)
            {
                groundY = hit.point.y;
                groundNormal = hit.normal;
            }

            if (grounded)
            {
                // Springy stance: holds the hull at stand height with a heavy, slightly bouncy feel.
                float targetHeight = standHeight - bodyDip + gaitBob;
                float error = targetHeight - height;
                float accel = heightStiffness * error - heightDamping * velocity.y + Physics.gravity.magnitude;
                rb.AddForce(Vector3.up * Mathf.Max(0f, accel), ForceMode.Acceleration);
            }

            if (!wasGrounded && grounded) Land(-lastFallSpeed);
            lastFallSpeed = velocity.y;
            airTime = grounded ? 0f : airTime + dt;

            // Stay upright, allow yaw.
            Vector3 tilt = Vector3.Cross(transform.up, Vector3.up);
            Vector3 angVel = rb.angularVelocity;
            Vector3 angNoYaw = angVel - Vector3.up * Vector3.Dot(angVel, Vector3.up);
            float uprightScale = grounded ? 1f : 0.5f;
            rb.AddTorque((tilt * uprightStiffness - angNoYaw * uprightDamping) * uprightScale, ForceMode.Acceleration);

            // Walk: push the hull toward the wanted ground speed.
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float speedLimit = walkSpeed * (commands.throttle < 0f ? 0.6f : 1f);
            Vector3 wanted = flatForward * commands.throttle * speedLimit;
            Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
            float control = grounded ? acceleration : acceleration * 0.35f;
            Vector3 change = Vector3.ClampMagnitude(wanted - flatVel, 50f) * control;
            rb.AddForce(change, ForceMode.Acceleration);

            // Turn.
            float yawRate = Vector3.Dot(rb.angularVelocity, Vector3.up);
            rb.AddTorque(Vector3.up * (commands.steer * turnRate - yawRate) * 3f, ForceMode.Acceleration);

            // Jump jets.
            if (boosting)
            {
                float lift = grounded ? jetAccel * 1.6f : jetAccel;
                rb.AddForce(Vector3.up * lift + flatForward * 6f * Mathf.Max(0.3f, commands.throttle), ForceMode.Acceleration);
            }

            ApplyExtraGravity(grounded);

            // Recover the dip after each stomp.
            bodyDip = Mathf.MoveTowards(bodyDip, 0f, dt * 1.8f);
        }

        void Land(float fallSpeed)
        {
            if (fallSpeed < 3f) return;
            Vector3 pos = new Vector3(rb.position.x, groundY, rb.position.z);
            float strength = Mathf.Clamp01(fallSpeed / 20f);
            bodyDip = Mathf.Lerp(0.6f, 2.2f, strength);
            GameAudio.Play(Sfx.Landing, pos, Mathf.Lerp(0.6f, 1f, strength), 0.8f);
            Fx.Dust(pos, Mathf.Lerp(2f, 5f, strength));
            FollowCamera.Shake(Mathf.Lerp(0.3f, 0.9f, strength), pos, 160f);

            foreach (var leg in legs)
            {
                leg.footPos = FootHome(leg, Vector3.zero);
                leg.stepT = 1f;
                leg.planted = true;
            }

            if (fallSpeed > landingShockSpeed)
            {
                var self = GetComponent<Damageable>();
                Explosions.ApplyForce(pos, 22f, 10f * strength + 4f, self);
                foreach (var d in Damageable.All)
                {
                    if (d == self || d.IsDead || (self != null && d.team == self.team)) continue;
                    float dist = Vector3.Distance(d.Center, pos);
                    if (dist < 18f) d.TakeDamage(Mathf.Lerp(120f, 20f, dist / 18f) * strength, d.Center, self);
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 velocity = Compat.GetVelocity(rb);
            Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);

            if (grounded) UpdateSteps(flatVel, dt);
            else
            {
                gaitBob = 0f;
                // Legs tuck under the hull while airborne.
                foreach (var leg in legs)
                {
                    Vector3 tuck = leg.hip.position - transform.up * (thighLength + shinLength) * 0.75f + transform.forward * 0.8f;
                    leg.footPos = Vector3.Lerp(leg.footPos, tuck, 1f - Mathf.Exp(-8f * dt));
                    leg.planted = false;
                }
            }

            foreach (var leg in legs) SolveLeg(leg);

            bool jetsOn = IsBoosting;
            foreach (var j in jets) Fx.SetRate(j, jetsOn ? 260f : 0f);
            if (jetAudio != null) jetAudio.volume = Mathf.MoveTowards(jetAudio.volume, jetsOn ? 1f : 0f, dt * 4f);
            if (humAudio != null) humAudio.pitch = Mathf.Lerp(0.6f, 0.9f, flatVel.magnitude / walkSpeed);
        }

        void UpdateSteps(Vector3 flatVel, float dt)
        {
            float speed = flatVel.magnitude;
            bool anyStepping = false;
            float bob = 0f;
            foreach (var leg in legs)
            {
                if (leg.stepT < 1f)
                {
                    float duration = Mathf.Lerp(stepDuration, stepDuration * 0.7f, speed / walkSpeed);
                    leg.stepT = Mathf.Min(1f, leg.stepT + dt / duration);
                    float t = leg.stepT;
                    float eased = t * t * (3f - 2f * t);
                    Vector3 p = Vector3.Lerp(leg.stepFrom, leg.stepTo, eased);
                    p.y += Mathf.Sin(t * Mathf.PI) * stepHeight;
                    leg.footPos = p;
                    // The hull rises as a leg swings through and drops onto the next footfall.
                    bob = Mathf.Max(bob, Mathf.Sin(t * Mathf.PI) * 0.45f * Mathf.Clamp01(speed / 2f));
                    if (leg.stepT >= 1f) Footfall(leg);
                    // At speed, let the next leg lift off before this one lands so the feet keep up.
                    else if (leg.stepT < Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(speed / walkSpeed))) anyStepping = true;
                    continue;
                }
            }
            gaitBob = bob;

            if (anyStepping) return;

            // Pick the next leg to move if its foot has fallen too far behind.
            Leg candidate = legs[nextLeg % legs.Length];
            if (candidate.stepT < 1f) return; // still finishing its step
            Vector3 lead = flatVel * 0.45f;
            Vector3 home = FootHome(candidate, lead);
            float dist = Vector3.Distance(Flat(candidate.footPos), Flat(home));
            float yawTurning = Mathf.Abs(Vector3.Dot(rb.angularVelocity, Vector3.up));

            bool moving = speed > 0.6f || yawTurning > 0.15f;
            idleTimer = moving ? 0f : idleTimer + dt;
            bool needStep = dist > strideLength * 0.5f || (idleTimer > 0.4f && dist > 0.7f);

            if (needStep)
            {
                candidate.stepFrom = candidate.footPos;
                candidate.stepTo = home;
                candidate.stepT = 0f;
                candidate.planted = false;
                nextLeg++;
                GameAudio.Play(Sfx.Servo, candidate.hip.position, 0.35f, Random.Range(0.8f, 1.1f));
            }
            else
            {
                nextLeg++;
            }
        }

        void Footfall(Leg leg)
        {
            leg.planted = true;
            Vector3 p = leg.footPos;
            GameAudio.Play(Sfx.Footstep, p, 1f, Random.Range(0.85f, 1f));
            Fx.Dust(p, 1.2f);
            FollowCamera.Shake(0.22f, p, 90f);
            bodyDip = Mathf.Max(bodyDip, 0.45f);
            // Sway the hull toward the planted foot for weight.
            rb.AddTorque(transform.forward * -leg.side * 0.25f, ForceMode.VelocityChange);
            Explosions.ApplyForce(p, 5f, 3f, GetComponent<Damageable>());
        }

        Vector3 FootHome(Leg leg, Vector3 lead)
        {
            Vector3 hip = leg.hip.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward);
            Vector3 offset = leg.footOffset == Vector3.zero
                ? flatRight * leg.side * 0.4f
                : flatRight * leg.footOffset.x + flatForward * leg.footOffset.z;
            Vector3 start = hip + offset + lead + Vector3.up * 2f;
            if (GroundProbe.Cast(start, Vector3.down, 30f, rb, out RaycastHit hit, true))
                return hit.point;
            return new Vector3(start.x, groundY, start.z);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Two-bone IK: places thigh, knee, shin and foot between hip and foot target.</summary>
        void SolveLeg(Leg leg)
        {
            Vector3 hip = leg.hip.position;
            Vector3 footTarget = leg.footPos + Vector3.up * 0.35f;
            Vector3 toFoot = footTarget - hip;
            float maxReach = thighLength + shinLength - 0.05f;
            float d = Mathf.Clamp(toFoot.magnitude, 0.5f, maxReach);
            Vector3 dir = toFoot.sqrMagnitude > 0.0001f ? toFoot.normalized : -transform.up;
            Vector3 foot = hip + dir * d;

            float a = (thighLength * thighLength - shinLength * shinLength + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - a * a));
            Vector3 hint = leg.bendHint == Vector3.zero ? transform.forward : transform.TransformDirection(leg.bendHint);
            Vector3 bend = Vector3.ProjectOnPlane(hint, dir).normalized;
            Vector3 knee = hip + dir * a + bend * h;

            PlaceSegment(leg.thigh, hip, knee);
            PlaceSegment(leg.shin, knee, foot);
            if (leg.knee != null) leg.knee.position = knee;
            if (leg.knee != null) leg.knee.rotation = transform.rotation;

            if (leg.foot != null)
            {
                leg.foot.position = foot;
                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                Quaternion yaw = Quaternion.LookRotation(flatForward.sqrMagnitude > 0.001f ? flatForward : Vector3.forward, Vector3.up);
                if (leg.planted)
                {
                    Quaternion ground = Quaternion.FromToRotation(Vector3.up, groundNormal);
                    leg.foot.rotation = ground * yaw;
                }
                else
                {
                    // Toes dip during the swing.
                    leg.foot.rotation = yaw * Quaternion.Euler(Mathf.Sin(leg.stepT * Mathf.PI) * 18f, 0f, 0f);
                }
            }
        }

        /// <summary>Stands the legs on the ground without physics (for the hangar preview).</summary>
        public void PoseLegs()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            foreach (var leg in legs)
            {
                leg.footPos = FootHome(leg, Vector3.zero);
                leg.planted = true;
                leg.stepT = 1f;
                SolveLeg(leg);
            }
        }

        void PlaceSegment(Transform seg, Vector3 from, Vector3 to)
        {
            if (seg == null) return;
            Vector3 v = to - from;
            seg.position = (from + to) * 0.5f;
            if (v.sqrMagnitude > 0.0001f) seg.rotation = Quaternion.LookRotation(v, transform.forward);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var j in jets) Fx.SetRate(j, 0f);
            if (jetAudio != null) jetAudio.Stop();
            if (humAudio != null) humAudio.Stop();
        }
    }
}
