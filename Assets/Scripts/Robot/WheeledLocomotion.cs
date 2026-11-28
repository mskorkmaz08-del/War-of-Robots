using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Heavy six-wheeled drive with raycast suspension, tank steering and a
    /// rocket booster ("Ultra Thrust") that can launch the robot off ramps and
    /// turn it into a battering ram.
    /// </summary>
    public class WheeledLocomotion : Locomotion
    {
        [System.Serializable]
        public class Wheel
        {
            public Transform anchor;
            public Transform visual;
            [System.NonSerialized] public float compression;
            [System.NonSerialized] public float lastCompression;
            [System.NonSerialized] public float spin;
            [System.NonSerialized] public bool grounded;
        }

        public Wheel[] wheels = new Wheel[0];
        public Transform[] thrusterNozzles = new Transform[0];

        [Header("Suspension")]
        public float wheelRadius = 1.4f;
        public float suspensionTravel = 1.1f;
        [Tooltip("Spring strength as acceleration per metre of compression.")]
        public float springStiffness = 26f;
        public float springDamping = 9f;

        [Header("Driving")]
        public float driveAccel = 9f;
        public float maxSpeed = 26f;
        public float lateralGrip = 7f;
        public float rollingBrake = 1.3f;
        public float turnRate = 1.25f;
        public float turnResponse = 4f;

        [Header("Ultra Thrust")]
        public float boostAccel = 24f;
        public float boostMaxSpeed = 62f;

        ParticleSystem[] flames = new ParticleSystem[0];
        AudioSource engineAudio;
        AudioSource thrustAudio;
        float upsideDownTime;
        int groundedCount;
        float airTime, lastVy;

        public override bool IsGrounded => groundedCount > 0;

        protected override void Awake()
        {
            base.Awake();
            rb.centerOfMass = new Vector3(0f, 0.6f, 0f);
        }

        void Start()
        {
            flames = new ParticleSystem[thrusterNozzles.Length];
            for (int i = 0; i < thrusterNozzles.Length; i++)
                flames[i] = Fx.ThrusterFlame(thrusterNozzles[i], 1.6f, new Color(0.35f, 0.6f, 1f));

            engineAudio = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(engineAudio, 20f, 220f);
            engineAudio.clip = GameAudio.EngineLoop();
            engineAudio.loop = true;
            engineAudio.volume = 0.45f;
            engineAudio.Play();

            thrustAudio = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(thrustAudio, 25f, 300f);
            thrustAudio.clip = GameAudio.ThrustLoop();
            thrustAudio.loop = true;
            thrustAudio.volume = 0f;
            thrustAudio.Play();
        }

        float smoothSteer;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float throttle = commands.throttle * moveScale;
            // Ease the steering in so a flick of the stick doesn't snap the hull around.
            smoothSteer = Mathf.MoveTowards(smoothSteer, commands.steer, dt * 3.5f);
            float steer = smoothSteer;
            bool boosting = UpdateBoost(BoostInput);

            Vector3 up = transform.up;
            Vector3 velocity = Compat.GetVelocity(rb);
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            float massShare = rb.mass / Mathf.Max(1, wheels.Length);
            bool wasAirborne = groundedCount == 0;
            groundedCount = 0;

            for (int wi = 0; wi < wheels.Length; wi++)
            {
                var w = wheels[wi];
                if (w.anchor == null) continue;
                Vector3 origin = w.anchor.position;
                float rayLength = suspensionTravel + wheelRadius;
                w.lastCompression = w.compression;
                w.grounded = GroundProbe.Cast(origin, -up, rayLength, rb, out RaycastHit hit);

                if (!w.grounded)
                {
                    w.compression = 0f;
                    continue;
                }

                groundedCount++;
                w.compression = Mathf.Clamp(rayLength - hit.distance, 0f, suspensionTravel);
                float compressionSpeed = (w.compression - w.lastCompression) / dt;
                float springAccel = Mathf.Max(0f, springStiffness * w.compression + springDamping * compressionSpeed);
                // Destroyed: the suspension gives out and the hull sags, more on one side.
                if (collapsing) springAccel *= Mathf.Lerp(1f, CollapseSag(wi), Mathf.Clamp01((Time.time - collapseTime) / 0.6f));
                float normalForce = springAccel * massShare;
                rb.AddForceAtPosition(up * normalForce, origin, ForceMode.Force);

                Vector3 pointVel = rb.GetPointVelocity(hit.point);
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
                Vector3 right = Vector3.Cross(hit.normal, fwd);

                // Sideways grip, limited by how hard the wheel is pressed down.
                float lateral = Vector3.Dot(pointVel, right);
                float gripForce = Mathf.Clamp(-lateral * lateralGrip * massShare, -normalForce * 1.3f, normalForce * 1.3f);
                rb.AddForceAtPosition(right * gripForce, hit.point, ForceMode.Force);

                // Drive or roll to a stop.
                if (Mathf.Abs(throttle) > 0.05f)
                {
                    bool overSpeed = forwardSpeed * Mathf.Sign(throttle) > maxSpeed;
                    if (!overSpeed)
                        rb.AddForceAtPosition(fwd * throttle * driveAccel * massShare, hit.point, ForceMode.Force);
                }
                else
                {
                    float along = Vector3.Dot(pointVel, fwd);
                    rb.AddForceAtPosition(-fwd * along * rollingBrake * (collapsing ? 4f : 1f) * massShare, hit.point, ForceMode.Force);
                }
            }

            if (groundedCount > 0)
            {
                // Tank steering: spin the hull toward the requested turn rate.
                float yawRate = Vector3.Dot(rb.angularVelocity, up);
                float speedFactor = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / boostMaxSpeed));
                float target = steer * turnRate * speedFactor;
                rb.AddTorque(up * (target - yawRate) * turnResponse, ForceMode.Acceleration);
                // Stabiliser: damp rocking and keep the hull level on bumps and at speed.
                Vector3 av = rb.angularVelocity;
                Vector3 rock = av - up * Vector3.Dot(av, up);
                Vector3 level = Vector3.Cross(up, Vector3.up);
                rb.AddTorque(level * 1.5f - rock * 2.2f, ForceMode.Acceleration);
            }
            else
            {
                // A little air control keeps it from tumbling forever.
                Vector3 righting = Vector3.Cross(up, Vector3.up);
                rb.AddTorque(righting * 2.5f - rb.angularVelocity * 0.4f, ForceMode.Acceleration);
                rb.AddTorque(Vector3.up * steer * 1.2f, ForceMode.Acceleration);
            }

            if (boosting && forwardSpeed < boostMaxSpeed)
            {
                rb.AddForce(transform.forward * boostAccel, ForceMode.Acceleration);
                if (groundedCount > 0) rb.AddForce(-up * 6f, ForceMode.Acceleration); // keep it planted
            }

            ApplyExtraGravity(groundedCount > 0);
            // Touchdown after a jump or a drop off a ledge: the harder the landing, the bigger the impact.
            if (groundedCount > 0 && wasAirborne && airTime > 0.3f && !collapsing) Touchdown(-lastVy);
            airTime = groundedCount == 0 ? airTime + dt : 0f;
            lastVy = velocity.y;
            UpdateAutoRight(velocity);
        }

        void Update()
        {
            float speed = Compat.GetVelocity(rb).magnitude;
            float forwardSpeed = Vector3.Dot(Compat.GetVelocity(rb), transform.forward);

            foreach (var w in wheels)
            {
                if (w.anchor == null || w.visual == null) continue;
                float drop = suspensionTravel - w.compression;
                w.visual.position = w.anchor.position - transform.up * drop;
                w.spin += forwardSpeed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime;
                w.visual.rotation = transform.rotation * Quaternion.Euler(w.spin, 0f, 0f);
            }

            float flameRate = IsBoosting ? 320f : (Mathf.Abs(commands.throttle) > 0.1f ? 25f : 6f);
            foreach (var f in flames) Fx.SetRate(f, flameRate);

            if (engineAudio != null)
                engineAudio.pitch = Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01(speed / boostMaxSpeed) + Mathf.Abs(commands.throttle) * 0.1f);
            if (thrustAudio != null)
                thrustAudio.volume = Mathf.MoveTowards(thrustAudio.volume, IsBoosting ? 1f : 0f, Time.deltaTime * 4f);

            if (IsBoosting) FollowCamera.Shake(0.02f, transform.position, 40f);
        }

        int brokenSide;
        public override void Collapse(Vector3 awayFromHit)
        {
            base.Collapse(awayFromHit);
            brokenSide = Vector3.Dot(awayFromHit, transform.right) >= 0f ? 1 : -1;
        }

        /// <summary>How much spring a wheel keeps once the robot is destroyed.</summary>
        float CollapseSag(int index)
        {
            var w = wheels[index];
            if (w.anchor == null) return 0.5f;
            float side = Vector3.Dot(w.anchor.position - transform.position, transform.right);
            return side * brokenSide > 0f ? 0.32f : 0.62f;
        }

        void Touchdown(float fallSpeed)
        {
            if (fallSpeed < 4f) return;
            float strength = Mathf.Clamp01((fallSpeed - 4f) / 22f);
            Vector3 pos = rb.position;
            if (GroundProbe.Cast(pos + Vector3.up, Vector3.down, 12f, rb, out RaycastHit g, true)) pos = g.point;
            GameAudio.Play(Sfx.Landing, pos, Mathf.Lerp(0.5f, 1f, strength), Mathf.Lerp(1.1f, 0.8f, strength));
            if (strength > 0.45f) GameAudio.Play(Sfx.Debris, pos, 0.5f * strength, 0.85f);
            Fx.ImpactDust(pos, 1.5f, strength);
            FollowCamera.Shake(Mathf.Lerp(0.25f, 1.1f, strength), pos, 150f + 60f * strength);
            if (strength > 0.25f)
                foreach (var w in wheels)
                    if (w.anchor != null && w.grounded) Fx.GroundCrack(w.anchor.position - transform.up * (suspensionTravel + wheelRadius * 0.5f), Mathf.Lerp(1.2f, 2.4f, strength));
        }

        void UpdateAutoRight(Vector3 velocity)
        {
            bool upsideDown = transform.up.y < 0.25f;
            if (upsideDown && velocity.sqrMagnitude < 4f)
            {
                upsideDownTime += Time.fixedDeltaTime;
                if (upsideDownTime > 2.5f)
                {
                    upsideDownTime = 0f;
                    Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                    if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
                    rb.position += Vector3.up * 4f;
                    rb.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
                    Compat.SetVelocity(rb, Vector3.zero);
                    rb.angularVelocity = Vector3.zero;
                    Fx.Dust(rb.position, 2f);
                }
            }
            else
            {
                upsideDownTime = 0f;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var f in flames) Fx.SetRate(f, 0f);
            if (engineAudio != null) engineAudio.Stop();
            if (thrustAudio != null) thrustAudio.Stop();
        }
    }
}
