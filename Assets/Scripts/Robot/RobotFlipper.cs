using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// A pneumatic-style flipper: a plate on a hinge at the front of the robot.
    /// When fired, the hinge spring snaps the plate up and anything resting on it
    /// gets launched. The plate is a real physics body, so it also pushes and wedges.
    /// </summary>
    [RequireComponent(typeof(HingeJoint), typeof(Rigidbody))]
    public class RobotFlipper : MonoBehaviour
    {
        public RobotInput input;

        [Header("Angles (degrees)")]
        [Tooltip("Resting angle. Positive tilts the front of the plate down like a wedge.")]
        public float restAngle = 15f;
        [Tooltip("Fired angle. Negative swings the plate up.")]
        public float fireAngle = -60f;
        [Tooltip("Tick this if the plate swings the wrong way in your Unity version.")]
        public bool invertDirection = false;

        [Header("Motion")]
        public float spring = 6000f;
        public float damper = 150f;
        public float holdTime = 0.25f;
        public float cooldown = 0.9f;

        [Header("Launch")]
        [Tooltip("Upward push given to bodies on the plate when it fires.")]
        public float launchImpulse = 900f;
        [Tooltip("Caps the launch speed so light objects don't fly off the map.")]
        public float maxLaunchSpeed = 8f;
        [Tooltip("Size of the area above the plate that gets launched (local space).")]
        public Vector3 launchArea = new Vector3(1.4f, 0.7f, 0.9f);
        [Tooltip("Centre of the launch area along the plate (local Z).")]
        public float launchAreaForward = 0.35f;

        HingeJoint hinge;
        Rigidbody selfBody;
        Rigidbody ownerBody;
        float firedAt = -999f;
        bool firing;

        /// <summary>0 when ready, rising to 1 right after firing.</summary>
        public float CooldownFraction => Mathf.Clamp01(1f - (Time.time - firedAt) / cooldown);

        /// <summary>Raised when the flipper fires (for effects and sounds later).</summary>
        public event System.Action Fired;

        void Awake()
        {
            hinge = GetComponent<HingeJoint>();
            selfBody = GetComponent<Rigidbody>();
            ownerBody = hinge.connectedBody;

            float sign = invertDirection ? -1f : 1f;
            hinge.useSpring = true;
            hinge.useLimits = true;
            var limits = hinge.limits;
            float a = restAngle * sign;
            float b = fireAngle * sign;
            limits.min = Mathf.Min(a, b) - 5f;
            limits.max = Mathf.Max(a, b) + 5f;
            hinge.limits = limits;

            SetTarget(restAngle);
        }

        void Update()
        {
            if (input != null && input.AttackHeld && Time.time >= firedAt + cooldown)
                Fire();

            if (firing && Time.time >= firedAt + holdTime)
            {
                firing = false;
                SetTarget(restAngle);
            }
        }

        public void Fire()
        {
            firedAt = Time.time;
            firing = true;
            SetTarget(fireAngle);
            LaunchBodiesOnPlate();
            Fired?.Invoke();
        }

        void SetTarget(float angle)
        {
            var s = hinge.spring;
            s.spring = spring;
            s.damper = damper;
            s.targetPosition = invertDirection ? -angle : angle;
            hinge.spring = s;
        }

        void LaunchBodiesOnPlate()
        {
            Vector3 centre = transform.TransformPoint(new Vector3(0f, launchArea.y * 0.5f, launchAreaForward));
            Collider[] hits = Physics.OverlapBox(centre, launchArea * 0.5f, transform.rotation);
            var launched = new HashSet<Rigidbody>();

            Vector3 forward = ownerBody != null ? ownerBody.transform.forward : transform.forward;
            Vector3 direction = (Vector3.up + forward * 0.4f).normalized;

            foreach (var hit in hits)
            {
                Rigidbody body = hit.attachedRigidbody;
                if (body == null || body == selfBody || body == ownerBody || body.isKinematic) continue;
                if (!launched.Add(body)) continue;

                float impulse = Mathf.Min(launchImpulse, body.mass * maxLaunchSpeed);
                body.AddForceAtPosition(direction * impulse, hit.ClosestPoint(transform.position), ForceMode.Impulse);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0f, launchArea.y * 0.5f, launchAreaForward), launchArea);
        }
    }
}
