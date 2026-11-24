using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Tank-style drive for a wheeled combat robot using WheelColliders.
    /// Left and right wheels get different torque to turn, with a small yaw assist
    /// so turning feels responsive. Also turns the robot back upright if it stays
    /// flipped over for a few seconds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RobotDrive : MonoBehaviour
    {
        [System.Serializable]
        public class Wheel
        {
            public WheelCollider collider;
            public Transform visual;
            public bool leftSide;
        }

        [Header("Setup")]
        public RobotInput input;
        public Wheel[] wheels = new Wheel[0];

        [Tooltip("Other rigidbodies joined to this robot (weapons, arms) that should move with it when it is reset.")]
        public Rigidbody[] attachedParts = new Rigidbody[0];

        [Header("Driving")]
        public float motorTorque = 90f;
        public float brakeTorque = 250f;
        public float maxSpeed = 11f;
        [Tooltip("How strongly the robot is helped to reach the turn rate you ask for.")]
        public float turnAssist = 6f;
        [Tooltip("Turn rate at full steer, in radians per second.")]
        public float maxTurnRate = 3.2f;

        [Header("Stability")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.25f, 0f);
        [Tooltip("Seconds upside down (and nearly still) before the robot rights itself. 0 disables.")]
        public float autoRightDelay = 2.5f;

        Rigidbody rb;
        float upsideDownTime;

        public Rigidbody Body => rb;

        public bool IsGrounded
        {
            get
            {
                foreach (var w in wheels)
                    if (w.collider != null && w.collider.isGrounded) return true;
                return false;
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass += centerOfMassOffset;
            if (input == null) input = GetComponent<RobotInput>();
        }

        void FixedUpdate()
        {
            float throttle = input != null ? input.Throttle : 0f;
            float steer = input != null ? input.Steer : 0f;

            Vector3 velocity = Compat.GetVelocity(rb);
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);

            float left = Mathf.Clamp(throttle + steer, -1f, 1f);
            float right = Mathf.Clamp(throttle - steer, -1f, 1f);
            bool idle = Mathf.Abs(throttle) < 0.05f && Mathf.Abs(steer) < 0.05f;
            bool overSpeed = Mathf.Abs(forwardSpeed) > maxSpeed
                             && Mathf.Abs(throttle) > 0.05f
                             && Mathf.Sign(forwardSpeed) == Mathf.Sign(throttle);

            foreach (var w in wheels)
            {
                if (w.collider == null) continue;
                float command = w.leftSide ? left : right;
                w.collider.motorTorque = overSpeed ? 0f : command * motorTorque;
                w.collider.brakeTorque = idle ? brakeTorque : 0f;
            }

            if (IsGrounded && Mathf.Abs(steer) > 0.05f)
            {
                float yawRate = Vector3.Dot(rb.angularVelocity, transform.up);
                float targetRate = steer * maxTurnRate;
                rb.AddTorque(transform.up * (targetRate - yawRate) * turnAssist, ForceMode.Acceleration);
            }

            UpdateAutoRight(velocity);
        }

        void Update()
        {
            foreach (var w in wheels)
            {
                if (w.collider == null || w.visual == null) continue;
                w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                w.visual.SetPositionAndRotation(pos, rot);
            }
        }

        void UpdateAutoRight(Vector3 velocity)
        {
            if (autoRightDelay <= 0f) return;

            bool upsideDown = transform.up.y < 0.2f;
            bool nearlyStill = velocity.sqrMagnitude < 1f;

            if (upsideDown && nearlyStill)
            {
                upsideDownTime += Time.fixedDeltaTime;
                if (upsideDownTime >= autoRightDelay)
                {
                    upsideDownTime = 0f;
                    RightItself();
                }
            }
            else
            {
                upsideDownTime = 0f;
            }
        }

        /// <summary>Lifts the robot slightly and turns it upright, keeping its heading.</summary>
        public void RightItself()
        {
            Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 0.01f)
                heading = Vector3.ProjectOnPlane(-transform.up, Vector3.up);
            if (heading.sqrMagnitude < 0.01f)
                heading = Vector3.forward;

            Vector3 oldPos = rb.position;
            Quaternion oldRot = rb.rotation;
            Vector3 newPos = oldPos + Vector3.up * 1.0f;
            Quaternion newRot = Quaternion.LookRotation(heading.normalized, Vector3.up);
            Quaternion delta = newRot * Quaternion.Inverse(oldRot);

            rb.position = newPos;
            rb.rotation = newRot;
            Compat.SetVelocity(rb, Vector3.zero);
            rb.angularVelocity = Vector3.zero;

            foreach (var part in attachedParts)
            {
                if (part == null) continue;
                part.position = newPos + delta * (part.position - oldPos);
                part.rotation = delta * part.rotation;
                Compat.SetVelocity(part, Vector3.zero);
                part.angularVelocity = Vector3.zero;
            }
        }
    }
}
