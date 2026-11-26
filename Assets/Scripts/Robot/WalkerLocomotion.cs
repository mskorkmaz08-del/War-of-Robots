using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Giant walker. The hull is held up by a heavy, springy "stance" force and
    /// steered with physics. The legs follow a steady gait cycle (legs in
    /// alternating groups, fixed rhythm, stride scaled to speed) solved with
    /// two-bone IK so the feet plant on the ground. Walking can go any way the
    /// stick points while the hull faces the aim. Uphill the hull leans in and
    /// lifts the feet higher; downhill it leans back. In the air the legs hold
    /// still poses: crouch before a jump, tucked while rising, trailing while
    /// flying forward, reaching down while falling.
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
            [System.NonSerialized] public float phaseOffset;
            [System.NonSerialized] public bool swinging;
            [System.NonSerialized] public Vector3 airPose;
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
        float gaitPhase;
        float uphill;
        float jumpCrouch;
        bool boostHeld;
        float weightShift;   // -1 leaning on the left foot, +1 on the right
        float SizeScale => Mathf.Max(0.6f, standHeight / 7f);
        const float SwingFraction = 0.42f;

        public override bool IsGrounded => grounded;

        void Start()
        {
            AssignGaitGroups();
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
            humAudio.volume = 0.14f;
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

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);

            // Slope along the way we're walking: lean into climbs, back on descents.
            uphill = 0f;
            if (grounded && flatVel.sqrMagnitude > 1f)
                uphill = -Vector3.Dot(Vector3.ProjectOnPlane(groundNormal, Vector3.up), flatVel.normalized);
            Vector3 targetUp = Vector3.up;
            if (grounded && flatVel.sqrMagnitude > 1f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, flatVel.normalized);
                targetUp = Quaternion.AngleAxis(Mathf.Clamp(uphill * 45f, -9f, 9f), axis) * Vector3.up;
            }
            // Heavy side-to-side weight shift onto the standing foot.
            if (grounded)
                targetUp = Quaternion.AngleAxis(-weightShift * (legs.Length <= 2 ? 4f : 1.5f), flatForward) * targetUp;

            // Stay upright (or leaning as above), allow yaw. Firm and well damped so it doesn't wobble.
            Vector3 tilt = Vector3.Cross(transform.up, targetUp);
            Vector3 angVel = rb.angularVelocity;
            Vector3 angNoYaw = angVel - Vector3.up * Vector3.Dot(angVel, Vector3.up);
            float uprightScale = grounded ? 1f : 0.6f;
            rb.AddTorque((tilt * uprightStiffness * 1.3f - angNoYaw * uprightDamping * 1.5f) * uprightScale, ForceMode.Acceleration);

            // Walk: push the hull toward the wanted ground speed.
            Vector3 wanted;
            if (commands.hasMove)
            {
                Vector3 m = Vector3.ClampMagnitude(new Vector3(commands.move.x, 0f, commands.move.z), 1f);
                float facing = m.sqrMagnitude > 0.0001f ? Vector3.Dot(m.normalized, flatForward) : 1f;
                // Backing up and side-stepping are slower than walking forward.
                wanted = m * walkSpeed * Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(facing));
            }
            else
            {
                float speedLimit = walkSpeed * (commands.throttle < 0f ? 0.6f : 1f);
                wanted = flatForward * commands.throttle * speedLimit;
            }
            if (jumpCrouch > 0f) wanted *= 0.3f;
            float control = grounded ? acceleration : acceleration * 0.35f;
            Vector3 change = Vector3.ClampMagnitude(wanted - flatVel, 50f) * control;
            rb.AddForce(change, ForceMode.Acceleration);

            // Turn: toward the aim direction, or by the steering input.
            float yawRate = Vector3.Dot(rb.angularVelocity, Vector3.up);
            float wantedYaw = commands.steer * turnRate;
            if (commands.hasFace && commands.face.sqrMagnitude > 0.0001f)
            {
                float ang = Vector3.SignedAngle(flatForward, Vector3.ProjectOnPlane(commands.face, Vector3.up), Vector3.up);
                wantedYaw = Mathf.Clamp(ang / 30f, -1f, 1f) * turnRate * 1.3f;
            }
            rb.AddTorque(Vector3.up * (wantedYaw - yawRate) * 3f, ForceMode.Acceleration);

            // Jump jets: a short crouch first, then lift off.
            bool pressed = commands.boost && !boostHeld;
            boostHeld = commands.boost;
            if (pressed && grounded && Fuel01 > 0.2f)
            {
                jumpCrouch = 0.16f;
                bodyDip = Mathf.Max(bodyDip, 1.6f);
            }
            if (jumpCrouch > 0f) jumpCrouch -= dt;
            if (boosting && jumpCrouch <= 0f)
            {
                float lift = grounded ? jetAccel * 1.6f : jetAccel;
                Vector3 push = commands.hasMove ? Vector3.ClampMagnitude(commands.move, 1f) : flatForward * Mathf.Max(0.3f, commands.throttle);
                rb.AddForce(Vector3.up * lift + push * 7f, ForceMode.Acceleration);
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
            GameAudio.Play(Sfx.Landing, pos, Mathf.Lerp(0.6f, 1f, strength), 0.9f);
            GameAudio.Play(Sfx.Hiss, pos + Vector3.up * 6f, 0.35f);
            Fx.Dust(pos, Mathf.Lerp(2f, 5f, strength));
            FollowCamera.Shake(Mathf.Lerp(0.3f, 0.9f, strength), pos, 160f);

            foreach (var leg in legs)
            {
                Vector3 above = new Vector3(leg.footPos.x, leg.hip.position.y + 2f, leg.footPos.z);
                leg.footPos = GroundProbe.Cast(above, Vector3.down, 40f, rb, out RaycastHit h, true) ? h.point : FootHome(leg, Vector3.zero);
                leg.stepT = 1f;
                leg.planted = true;
                leg.swinging = false;
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
            else UpdateAirPose(velocity, flatVel, dt);

            foreach (var leg in legs) SolveLeg(leg);

            bool jetsOn = IsBoosting;
            foreach (var j in jets) Fx.SetRate(j, jetsOn ? 260f : 0f);
            if (jetAudio != null) jetAudio.volume = Mathf.MoveTowards(jetAudio.volume, jetsOn ? 1f : 0f, dt * 4f);
            if (humAudio != null) humAudio.pitch = Mathf.Lerp(0.6f, 0.9f, flatVel.magnitude / walkSpeed);
        }

        /// <summary>
        /// Legs are split into two groups (left/right for two legs, diagonals or tripods for more)
        /// that take turns. One gait phase drives everything, so the rhythm is even at any speed.
        /// </summary>
        void AssignGaitGroups()
        {
            var zs = new System.Collections.Generic.List<float>();
            foreach (var leg in legs)
            {
                float z = Mathf.Round(leg.footOffset.z * 2f) / 2f;
                if (!zs.Contains(z)) zs.Add(z);
            }
            zs.Sort((x, y) => y.CompareTo(x));
            foreach (var leg in legs)
            {
                int zr = zs.IndexOf(Mathf.Round(leg.footOffset.z * 2f) / 2f);
                int sideIdx = leg.side > 0f ? 1 : 0;
                leg.phaseOffset = ((zr + sideIdx) % 2) * 0.5f;
            }
        }

        void UpdateSteps(Vector3 flatVel, float dt)
        {
            float speed = flatVel.magnitude;
            float yawFootSpeed = Mathf.Abs(Vector3.Dot(rb.angularVelocity, Vector3.up)) * 4f;
            float drive = Mathf.Max(speed, yawFootSpeed);

            bool anySwing = false, needSettle = false;
            foreach (var leg in legs)
            {
                if (leg.swinging) anySwing = true;
                else if (Vector3.Distance(Flat(leg.footPos), Flat(FootHome(leg, Vector3.zero))) > strideLength * 0.3f) needSettle = true;
            }

            // Cycles per second: follows speed, within limits so steps never get frantic or sluggish.
            // Long strides at a slow, heavy cadence: a giant takes few big steps.
            // Side-steps and backing up use shorter, quicker steps, so the feet keep up
            // with the body without having to reach across each other.
            Vector3 lv = transform.InverseTransformDirection(flatVel);
            float forwardness = speed > 0.5f ? Mathf.Clamp01(lv.z / speed) : 1f;
            float cycle = strideLength * 2.4f * Mathf.Lerp(0.55f, 1f, forwardness);
            float minRate = 0.22f;
            float maxRate = 1f / (Mathf.Max(stepDuration, 0.4f) * 1.8f);
            float rate = 0f;
            if (drive > 0.4f) rate = Mathf.Clamp(drive / cycle, minRate, maxRate);
            else if (anySwing || needSettle) rate = 0.4f;
            gaitPhase = Mathf.Repeat(gaitPhase + rate * dt, 1f);

            float lift = stepHeight * (1f + Mathf.Clamp01(uphill * 3f) * 0.8f);
            float stanceTime = rate > 0.01f ? (1f - SwingFraction) / rate : 0f;
            // Where the feet reach to, in the hull's frame: full stride forward, shorter
            // backward, and short side-steps that never cross one leg over the other.
            Vector3 leadLocal = transform.InverseTransformDirection(flatVel * stanceTime * 0.5f);
            leadLocal.y = 0f;
            if (leadLocal.z < 0f) leadLocal.z *= 0.75f;
            float bob = 0f;
            float shift = 0f;
            float moving01 = Mathf.Clamp01(speed / 2.5f);

            foreach (var leg in legs)
            {
                float p = Mathf.Repeat(gaitPhase + leg.phaseOffset, 1f);
                bool inSwing = rate > 0f && p < SwingFraction;
                if (inSwing)
                {
                    float s = p / SwingFraction;
                    if (!leg.swinging)
                    {
                        leg.swinging = true;
                        leg.planted = false;
                        leg.stepFrom = leg.footPos;
                        GameAudio.Play(Sfx.Servo, leg.hip.position, 0.3f, Random.Range(0.85f, 1.05f));
                    }
                    leg.stepT = s;
                    Vector3 legLead = leadLocal;
                    // Stepping toward the other leg: small step. Stepping outward: moderate.
                    legLead.x *= leg.side * legLead.x < 0f ? 0.3f : 0.55f;
                    leg.stepTo = FootHome(leg, transform.TransformDirection(legLead));
                    // Heavy step: the foot lifts first, swings through high, then drops
                    // down hard at the end instead of hopping in a quick arc.
                    float hs = Mathf.Clamp01((s - 0.12f) / 0.76f);
                    float e = hs * hs * (3f - 2f * hs);
                    Vector3 pos = Vector3.Lerp(leg.stepFrom, leg.stepTo, e);
                    float arc = Mathf.Sin(Mathf.Pow(s, 0.7f) * Mathf.PI);
                    pos.y += arc * lift * Mathf.Clamp(drive / 3f, 0.45f, 1f);
                    leg.footPos = pos;
                    // The hull rises over the standing leg and sinks as the foot comes down.
                    bob = Mathf.Max(bob, Mathf.Sin(s * Mathf.PI) * 0.5f * SizeScale * moving01);
                    shift += -leg.side * Mathf.Sin(s * Mathf.PI) * moving01;
                }
                else if (leg.swinging)
                {
                    leg.swinging = false;
                    leg.stepT = 1f;
                    leg.footPos = leg.stepTo;
                    Footfall(leg);
                }
            }
            gaitBob = bob;
            float swingCount = Mathf.Max(1f, legs.Length * 0.5f);
            weightShift = Mathf.Lerp(weightShift, Mathf.Clamp(shift / swingCount, -1f, 1f), 1f - Mathf.Exp(-8f * dt));
        }

        /// <summary>Still leg poses in the air, held relative to the hull and blended smoothly.</summary>
        void UpdateAirPose(Vector3 velocity, Vector3 flatVel, float dt)
        {
            gaitBob = 0f;
            weightShift = 0f;
            float reach = thighLength + shinLength;
            Vector3 localVel = transform.InverseTransformDirection(flatVel);
            foreach (var leg in legs)
            {
                if (leg.planted || leg.swinging)
                {
                    // Just left the ground: start from where the foot is.
                    leg.airPose = transform.InverseTransformPoint(leg.footPos);
                    leg.planted = false;
                    leg.swinging = false;
                }
                Vector3 hip = transform.InverseTransformPoint(leg.hip.position);
                Vector3 spread = leg.footOffset == Vector3.zero
                    ? new Vector3(leg.side * 0.35f, 0f, 0f)
                    : new Vector3(leg.footOffset.x * 0.75f, 0f, leg.footOffset.z * 0.75f);
                Vector3 pose;
                if (IsBoosting && localVel.magnitude > 4f)
                {
                    // Flying forward: legs trail behind the motion.
                    Vector3 back = -localVel.normalized * reach * 0.3f;
                    pose = hip + spread + new Vector3(back.x, -reach * 0.74f, back.z);
                }
                else if (velocity.y > 1.5f)
                    pose = hip + spread * 0.8f + new Vector3(0f, -reach * 0.58f, reach * 0.1f); // tucked, rising
                else
                    pose = hip + spread + new Vector3(0f, -reach * 0.88f, reach * 0.04f); // reaching down to land
                leg.airPose = Vector3.Lerp(leg.airPose, pose, 1f - Mathf.Exp(-6f * dt));
                leg.footPos = transform.TransformPoint(leg.airPose);
                leg.stepT = 1f;
            }
        }

        void Footfall(Leg leg)
        {
            leg.planted = true;
            Vector3 p = leg.footPos;
            GameAudio.Play(Sfx.Footstep, p, 1f, Random.Range(0.85f, 1f));
            Fx.Dust(p, 1.2f);
            FollowCamera.Shake(0.12f, p, 90f);
            // The whole hull sinks a little under its weight on every footfall.
            bodyDip = Mathf.Max(bodyDip, 0.45f * SizeScale);
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
