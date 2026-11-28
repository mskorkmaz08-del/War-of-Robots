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
    ///
    /// Weight (scaling research: a 10-15 m machine walks at well under one step a
    /// second and can't change speed quickly): the hull leans into every change of
    /// speed and direction; each footfall drops the hull a little on bending knees
    /// (a damped spring that overshoots slightly, like a real suspension); the feet
    /// push off on the toes and come down heel first; jumps start with a deep crouch
    /// and thrusters that take a moment to spool up; landings compress the legs in
    /// proportion to the fall and leave the robot briefly sluggish while it recovers;
    /// boosting pitches the body into the thrust.
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
        public float heightDamping = 10f;
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
        [Tooltip("Height of the ankle pivot above the sole.")]
        public float ankleHeight = 0.35f;

        [Header("Weight")]
        [Tooltip("Degrees of lean per m/s² of acceleration.")]
        public float leanPerAccel = 1.1f;
        public float maxLean = 6f;

        [Header("Jump jets")]
        public float jetAccel = 22f;
        [Tooltip("Heavy walkers: boost is a thruster-assisted charge along the ground instead of a jump.")]
        public bool sprintBoost;
        public float sprintMultiplier = 1.9f;
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
        float dipVel;        // body-dip spring velocity
        float dipTarget;     // where the dip spring pulls to (crouch before a jump)
        float jetThrottle;   // thrusters spool up and down rather than switching
        float landStun;      // seconds of sluggish control after a hard landing
        float staggerTime;
        Vector3 prevFlatVel, smoothAccel;
        bool legPush;
        float SizeScale => Mathf.Max(0.6f, standHeight / 7f);

        // Ground the hull stands on, taken from where the planted feet are rather than
        // from what happens to be under the hull: a crate or a wreck between the legs
        // no longer heaves the body up and makes it lurch.
        float supportY = float.NaN;

        // Knock-down: only a very heavy hit puts a giant on the ground.
        enum Knock { None, Falling, Down, Rising }
        Knock knock = Knock.None;
        float knockT;
        Vector3 knockAway = Vector3.back;
        float lastKnock = -999f;
        bool risePlaced;
        public bool IsKnockedDown => knock != Knock.None;
        bool Biped => legs.Length <= 2;
        float FallTime => Biped ? 1.05f + 0.1f * SizeScale : 0.7f;
        float DownTime => Biped ? 1.3f : 0.9f;
        float RiseTime => Biped ? 2.6f + 0.3f * SizeScale : 2.0f;
        float KnockTilt => Biped ? 64f : 9f;
        float KnockLow => standHeight * (Biped ? 0.4f : 0.5f);
        const float SwingFraction = 0.42f;

        public override bool IsGrounded => grounded;

        void Start()
        {
            // A 30-60 t machine on two or more planted legs: knocks against props and
            // other robots barely tip it. (The upright and lean control below is set as
            // an acceleration, so it is not slowed down by this.)
            if (rb != null) rb.inertiaTensor = rb.inertiaTensor * 5f;
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
            bool boosting = UpdateBoost(BoostInput);
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

            if (knock != Knock.None)
            {
                UpdateKnock(dt, velocity, hasGround ? height : standHeight);
                lastFallSpeed = velocity.y;
                return;
            }

            // Stand on the feet: the hull's height is measured from the planted feet,
            // which follow the real ground, and changes in it are eased in.
            if (grounded)
            {
                float feetY = 0f; int planted = 0;
                foreach (var leg in legs)
                    if (leg.planted) { feetY += leg.footPos.y; planted++; }
                float support = planted > 0 ? feetY / planted : groundY;
                // Never stand on something far below the probe (stepping off an edge).
                support = Mathf.Max(support, groundY - 1.5f);
                if (!wasGrounded || float.IsNaN(supportY) || Mathf.Abs(supportY - support) > 4f) supportY = support;
                else supportY = Mathf.Lerp(supportY, support, 1f - Mathf.Exp(-5f * dt));
                height = probeOrigin.y - 2f - supportY;
            }

            if (grounded)
            {
                // Stance: holds the hull at stand height, well damped so a giant never bounces.
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
            Vector3 flatAcc = (flatVel - prevFlatVel) / Mathf.Max(dt, 0.001f);
            prevFlatVel = flatVel;
            smoothAccel = Vector3.Lerp(smoothAccel, Vector3.ClampMagnitude(flatAcc, 12f), 1f - Mathf.Exp(-5f * dt));

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
                targetUp = Quaternion.AngleAxis(-weightShift * (legs.Length <= 2 ? 1.3f : 0.4f), flatForward) * targetUp;
            // Lean into acceleration: forward when setting off, back when braking, inward on turns.
            if (smoothAccel.sqrMagnitude > 0.04f)
            {
                float lean = Mathf.Min(smoothAccel.magnitude * leanPerAccel, maxLean) * (grounded ? 1f : 0.5f);
                targetUp = Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, smoothAccel.normalized)) * targetUp;
            }
            // In the air under thrust the body pitches into the push.
            if (!grounded && jetThrottle > 0.1f && commands.hasMove && commands.move.sqrMagnitude > 0.01f)
            {
                Vector3 push = Vector3.ProjectOnPlane(commands.move, Vector3.up).normalized;
                targetUp = Quaternion.AngleAxis(9f * jetThrottle, Vector3.Cross(Vector3.up, push)) * targetUp;
            }

            // Stay upright (or leaning as above), allow yaw. Firm and well damped so it doesn't wobble.
            Vector3 tilt = Vector3.Cross(transform.up, targetUp);
            Vector3 angVel = rb.angularVelocity;
            Vector3 angNoYaw = angVel - Vector3.up * Vector3.Dot(angVel, Vector3.up);
            float uprightScale = grounded ? 1f : 0.6f;
            // Firm and close to critically damped: the body goes where the gait and the
            // leans put it, without swinging back and forth as if hung from a rope.
            float kUp = uprightStiffness * 3f;
            float cUp = 1.9f * Mathf.Sqrt(kUp);
            float off = Vector3.Angle(transform.up, targetUp);
            if (off > 12f) kUp *= 1f + (off - 12f) / 6f; // something shoved it hard: plant the legs and straighten
            rb.AddTorque((tilt * kUp - angNoYaw * cUp) * uprightScale, ForceMode.Acceleration);

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
            if (sprintBoost && jetThrottle > 0.01f && grounded)
            {
                // Charge: a longer, faster stride driven by the back thrusters as they spool up.
                if (wanted.sqrMagnitude < 0.01f) wanted = flatForward * walkSpeed;
                wanted *= Mathf.Lerp(1f, sprintMultiplier, jetThrottle);
            }
            wanted *= moveScale;
            float control = grounded ? acceleration : acceleration * 0.35f;
            // After a hard landing the legs are busy absorbing it: barely any drive for a moment.
            if (landStun > 0f) control *= 0.25f;
            if (staggerTime > 0f) control *= 0.6f;
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
            bool boostIn = BoostInput;
            bool pressed = boostIn && !boostHeld;
            boostHeld = boostIn;
            if (!sprintBoost && pressed && grounded && (power != null || Fuel01 > 0.2f) && landStun <= 0f)
            {
                // Anticipation: a heavy machine sinks deep into its knees before it can leave the ground.
                jumpCrouch = 0.2f + 0.08f * SizeScale;
                dipTarget = Mathf.Min(1.3f * SizeScale, standHeight * 0.22f);
                legPush = true;
            }
            if (jumpCrouch > 0f)
            {
                jumpCrouch -= dt;
                if (jumpCrouch <= 0f)
                {
                    dipTarget = 0f;
                    // The legs straighten hard and throw the hull up as the jets light.
                    if (legPush && grounded) rb.AddForce(Vector3.up * (3.2f + 0.6f * SizeScale), ForceMode.VelocityChange);
                    legPush = false;
                }
            }
            // Thrusters spool up over a third of a second and wind down a little faster.
            bool thrusting = boosting && (sprintBoost || jumpCrouch <= 0f);
            jetThrottle = Mathf.MoveTowards(jetThrottle, thrusting ? 1f : 0f, dt * (thrusting ? 3f : 4.5f));
            if (!sprintBoost && jetThrottle > 0.001f && jumpCrouch <= 0f && (boosting || !grounded))
            {
                float lift = (grounded ? jetAccel * 1.6f : jetAccel) * jetThrottle;
                Vector3 push = commands.hasMove ? Vector3.ClampMagnitude(commands.move, 1f) : flatForward * Mathf.Max(0.3f, commands.throttle);
                rb.AddForce(Vector3.up * lift + push * 7f * jetThrottle, ForceMode.Acceleration);
            }

            ApplyExtraGravity(grounded);

            // The body-dip spring: footfalls, landings and hits push it; it settles with a slight overshoot.
            float kDip = 30f / SizeScale;
            float cDip = 2f * 0.5f * Mathf.Sqrt(kDip);
            dipVel += (-kDip * (bodyDip - dipTarget) - cDip * dipVel) * dt;
            bodyDip += dipVel * dt;
            float maxDip = standHeight * 0.3f;
            if (bodyDip > maxDip) { bodyDip = maxDip; dipVel = Mathf.Min(dipVel, 0f); }
            if (bodyDip < -0.5f) { bodyDip = -0.5f; dipVel = Mathf.Max(dipVel, 0f); }
            landStun -= dt;
            staggerTime -= dt;
        }

        void Land(float fallSpeed)
        {
            if (fallSpeed < 3f) return;
            Vector3 pos = new Vector3(rb.position.x, groundY, rb.position.z);
            float strength = Mathf.Clamp01(fallSpeed / 20f);
            // The legs take the impact: the hull keeps sinking on bending knees, then recovers.
            dipVel += fallSpeed * 0.85f;
            dipTarget = 0f;
            landStun = Mathf.Lerp(0.15f, 0.9f, strength) * Mathf.Sqrt(SizeScale);
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

            if (knock != Knock.None) UpdateKnockLegs(dt);
            else if (grounded) UpdateSteps(flatVel, dt);
            else UpdateAirPose(velocity, flatVel, dt);

            foreach (var leg in legs) SolveLeg(leg);

            foreach (var j in jets) Fx.SetRate(j, jetThrottle > 0.05f ? 260f * jetThrottle : 0f);
            if (jetAudio != null) jetAudio.volume = Mathf.MoveTowards(jetAudio.volume, jetThrottle, dt * 4f);
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
            if (sprintBoost && IsBoosting) maxRate *= 1.45f;
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
                    bob = Mathf.Max(bob, Mathf.Sin(s * Mathf.PI) * 0.16f * SizeScale * moving01);
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
            gaitBob = Mathf.Lerp(gaitBob, bob, 1f - Mathf.Exp(-4f * dt));
            float swingCount = Mathf.Max(1f, legs.Length * 0.5f);
            weightShift = Mathf.Lerp(weightShift, Mathf.Clamp(shift / swingCount, -1f, 1f), 1f - Mathf.Exp(-3f * dt));
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
            FollowCamera.Shake(0.06f + 0.04f * SizeScale, p, 90f);
            // The hull settles under its weight on every footfall (heavier robots, harder steps).
            float pace = Mathf.Clamp01(new Vector3(Compat.GetVelocity(rb).x, 0f, Compat.GetVelocity(rb).z).magnitude / Mathf.Max(1f, walkSpeed));
            dipVel += (0.6f + 0.9f * pace) * SizeScale;
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
            Vector3 footTarget = leg.footPos + Vector3.up * ankleHeight;
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
                    // Toe-off then heel strike: the foot rolls forward off its toes as it lifts,
                    // and comes down heel first, toes raised, flattening as it plants.
                    float st = leg.stepT;
                    float pitchDeg = st < 0.45f ? Mathf.Sin(st / 0.45f * Mathf.PI) * 22f : -Mathf.Sin((st - 0.45f) / 0.55f * Mathf.PI) * 14f;
                    if (!grounded) pitchDeg = 8f;
                    leg.foot.rotation = yaw * Quaternion.Euler(pitchDeg, 0f, 0f);
                }
            }
        }

        // =====================================================================
        // Knock-down and getting up
        // =====================================================================

        /// <summary>
        /// A very heavy hit can put the robot on the ground. Returns false when it can't
        /// fall right now (in the air, already down, or it got up only moments ago).
        /// </summary>
        public bool KnockDown(Vector3 awayFromHit)
        {
            if (knock != Knock.None || !grounded || Time.time - lastKnock < 20f) return false;
            awayFromHit.y = 0f;
            if (awayFromHit.sqrMagnitude < 0.01f) awayFromHit = -transform.forward;
            knockAway = awayFromHit.normalized;
            if (!Biped)
            {
                // Legged bodies drop onto their belly and roll a little away from the hit.
                Vector3 side = Vector3.Dot(knockAway, transform.right) >= 0f ? transform.right : -transform.right;
                knockAway = Vector3.ProjectOnPlane(side, Vector3.up).normalized;
            }
            knock = Knock.Falling;
            knockT = 0f;
            lastKnock = Time.time;
            jumpCrouch = 0f; legPush = false; dipTarget = 0f; jetThrottle = 0f;
            foreach (var leg in legs) { leg.swinging = false; leg.stepT = 1f; leg.planted = true; }
            GameAudio.Play(Sfx.Servo, transform.position + Vector3.up * standHeight, 0.8f, 0.55f);
            GameAudio.Play(Sfx.Hiss, transform.position + Vector3.up * standHeight, 0.6f, 0.7f);
            return true;
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>
        /// The scripted part of a fall. Physics still carries the hull (it can be pushed and
        /// it lands on whatever is there); these springs only say where the body is heading:
        /// gravity takes it down faster and faster, it hits the ground, lies there a moment,
        /// then gets up the way its body allows.
        /// Two legs: it goes over backwards or sideways onto its back and hips, rolls up onto
        /// its knees with the feet pulled in under it, then pushes up to standing.
        /// More legs: the legs buckle outwards and the belly drops; then the legs walk back in
        /// under the body and lift it.
        /// </summary>
        void UpdateKnock(float dt, Vector3 velocity, float height)
        {
            knockT += dt;
            float tilt, low;
            switch (knock)
            {
                case Knock.Falling:
                {
                    float u = knockT / FallTime;
                    float g = u * u; // speeds up like a fall
                    tilt = KnockTilt * g;
                    low = Mathf.Lerp(0f, 1f, g);
                    if (u >= 1f)
                    {
                        knock = Knock.Down; knockT = 0f;
                        Vector3 pos = rb.worldCenterOfMass;
                        pos.y = groundY;
                        GameAudio.Play(Sfx.Landing, pos, 1f, 0.7f);
                        GameAudio.Play(Sfx.MetalHit, pos, 1f, 0.6f);
                        Fx.Dust(pos, 4f * SizeScale);
                        Fx.Dust(pos + knockAway * standHeight * 0.5f, 3f * SizeScale);
                        FollowCamera.Shake(0.7f, pos, 140f);
                        dipVel = 0f; bodyDip = 0f;
                    }
                    break;
                }
                case Knock.Down:
                {
                    // Settled on the ground; a small rebound right after the impact.
                    float bounce = Mathf.Sin(Mathf.Clamp01(knockT / 0.35f) * Mathf.PI) * 0.06f;
                    tilt = KnockTilt * (1f - bounce);
                    low = 1f - bounce * 0.5f;
                    if (knockT >= DownTime)
                    {
                        knock = Knock.Rising; knockT = 0f; risePlaced = false;
                        GameAudio.Play(Sfx.Hiss, transform.position, 0.8f, 0.8f);
                    }
                    break;
                }
                default:
                {
                    float u = knockT / RiseTime;
                    if (Biped)
                    {
                        // 0-40%: roll up onto the knees, feet drawn in. 40-100%: push up to stand.
                        float a = Smooth(u / 0.4f), b = Smooth((u - 0.4f) / 0.6f);
                        tilt = KnockTilt * Mathf.Lerp(1f, 0.28f, a) * (1f - b);
                        low = Mathf.Lerp(1f, 0.82f, a) * (1f - b);
                    }
                    else
                    {
                        // 0-45%: legs walk back in under the body. 45-100%: lift.
                        float a = Smooth(u / 0.45f), b = Smooth((u - 0.45f) / 0.55f);
                        tilt = KnockTilt * (1f - a);
                        low = 1f - b;
                    }
                    if (u >= 0.4f && !risePlaced)
                    {
                        risePlaced = true;
                        GameAudio.Play(Sfx.Servo, transform.position + Vector3.up * 2f, 0.9f, 0.6f);
                    }
                    if (u >= 1f)
                    {
                        knock = Knock.None;
                        supportY = float.NaN;
                        foreach (var leg in legs) { leg.planted = true; leg.swinging = false; leg.stepT = 1f; }
                        GameAudio.Play(Sfx.Footstep, transform.position, 1f, 0.8f);
                    }
                    break;
                }
            }

            // Height: the hull drops from standing to resting on its hips/belly.
            float target = Mathf.Lerp(standHeight, KnockLow, low);
            float k = 14f, c = 2f * Mathf.Sqrt(k);
            float acc = k * (target - height) - c * velocity.y + Physics.gravity.magnitude * (knock == Knock.Falling ? 0.75f : 1f);
            rb.AddForce(Vector3.up * Mathf.Max(0f, acc), ForceMode.Acceleration);

            // Tilt toward the way it is falling (and back up when it rises).
            Vector3 axis = Vector3.Cross(Vector3.up, knockAway);
            Vector3 targetUp = Quaternion.AngleAxis(tilt, axis) * Vector3.up;
            Vector3 err = Vector3.Cross(transform.up, targetUp);
            Vector3 av = rb.angularVelocity;
            float kt = 30f, ct = 2f * Mathf.Sqrt(kt);
            rb.AddTorque(err * kt - av * ct, ForceMode.Acceleration);

            // No walking while down: the feet grip and the body drifts to a stop.
            Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 drift = knock == Knock.Falling ? knockAway * Mathf.Lerp(1.5f, 0f, knockT / FallTime) : Vector3.zero;
            rb.AddForce((drift - flatVel) * 3f, ForceMode.Acceleration);
            jetThrottle = 0f;
        }

        /// <summary>Where the feet go while the robot is down or getting up.</summary>
        void UpdateKnockLegs(float dt)
        {
            gaitBob = 0f; weightShift = 0f;
            float u = knock == Knock.Rising ? knockT / RiseTime : 0f;
            foreach (var leg in legs)
            {
                Vector3 home = FootHome(leg, Vector3.zero);
                Vector3 hip = leg.hip.position;
                Vector3 outward = Vector3.ProjectOnPlane(home - new Vector3(transform.position.x, home.y, transform.position.z), Vector3.up);
                if (Biped)
                {
                    if (knock == Knock.Rising)
                    {
                        // Draw each foot back in under the hips, one after the other, then plant it.
                        float start = leg.side > 0f ? 0.05f : 0.18f;
                        float s = Mathf.Clamp01((u - start) / 0.22f);
                        if (s > 0f && s < 1f && !leg.swinging) { leg.swinging = true; leg.stepFrom = leg.footPos; }
                        if (leg.swinging)
                        {
                            Vector3 to = home;
                            Vector3 p = Vector3.Lerp(leg.stepFrom, to, Smooth(s));
                            p.y = Mathf.Lerp(leg.stepFrom.y, to.y, s) + Mathf.Sin(s * Mathf.PI) * stepHeight * 0.4f;
                            leg.footPos = p;
                            leg.stepT = s;
                            leg.planted = false;
                            if (s >= 1f) { leg.swinging = false; leg.planted = true; leg.stepT = 1f; Footfall(leg); }
                        }
                    }
                    // Falling and lying: the feet stay where they were; the knees fold as the hips come down.
                }
                else
                {
                    // Legs buckle outwards as the belly drops, and walk back in to lift it.
                    float splay = knock == Knock.Falling ? Smooth(knockT / FallTime) : knock == Knock.Down ? 1f : 1f - Smooth(u / 0.45f);
                    Vector3 want = home + outward.normalized * Mathf.Min(outward.magnitude, 3f * SizeScale) * 0.45f * splay;
                    if (GroundProbe.Cast(want + Vector3.up * 6f, Vector3.down, 30f, rb, out RaycastHit h, true)) want = h.point;
                    float rate = knock == Knock.Rising ? 4f : 6f;
                    leg.footPos = Vector3.Lerp(leg.footPos, want, 1f - Mathf.Exp(-rate * dt));
                    leg.planted = true;
                }
            }
        }

        /// <summary>A hit: the knees give a little and drive falters; a big hit forces a recovery step.</summary>
        public void Stagger(Vector3 awayFromHit, float severity)
        {
            dipVel += severity * 1.1f;
            staggerTime = Mathf.Max(staggerTime, 0.25f * severity);
            weightShift = Mathf.Clamp(weightShift + Vector3.Dot(awayFromHit, transform.right) * severity * 0.8f, -1f, 1f);
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
