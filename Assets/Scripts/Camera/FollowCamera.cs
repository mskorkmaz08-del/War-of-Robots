using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Third-person chase camera for giant robots. It sits low and behind so the
    /// robot towers over the frame, follows heading smoothly, avoids walls,
    /// and shakes on footsteps, explosions and hits.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        public static FollowCamera Main { get; private set; }

        public Transform target;
        public float distance = 30f;
        public float height = 12f;
        public float lookHeight = 9f;
        public float lookAhead = 8f;
        public float followSharpness = 5f;
        public float rotateSharpness = 2.5f;
        public float baseFov = 60f;
        public float speedFovBoost = 14f;

        [Header("Shake")]
        public float maxShakeAngle = 3.5f;
        public float maxShakeOffset = 0.8f;
        public float traumaDecay = 1.4f;

        float yaw;
        float trauma;
        float seed;
        Camera cam;
        Rigidbody targetBody;

        void Awake()
        {
            Main = this;
            cam = GetComponent<Camera>();
            seed = Random.value * 100f;
        }

        public void SetTarget(Transform newTarget, float newDistance, float newHeight, float newLookHeight)
        {
            target = newTarget;
            distance = newDistance;
            height = newHeight;
            lookHeight = newLookHeight;
            targetBody = target != null ? target.GetComponent<Rigidbody>() : null;
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                transform.position = target.position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, height, -distance);
            }
        }

        /// <summary>Adds screen shake. Strength fades with distance from the camera.</summary>
        public static void Shake(float amount, Vector3 source, float radius = 120f)
        {
            if (Main == null) return;
            float d = Vector3.Distance(Main.transform.position, source);
            float falloff = Mathf.Clamp01(1f - d / radius);
            Main.trauma = Mathf.Clamp01(Main.trauma + amount * falloff);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.01f)
            {
                float targetYaw = Quaternion.LookRotation(flatForward).eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-rotateSharpness * dt));
            }

            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * lookHeight;
            Vector3 desired = target.position + rotation * new Vector3(0f, height, -distance);

            // Pull in if a wall or building is between the robot and the camera.
            Vector3 toCam = desired - pivot;
            if (Physics.SphereCast(pivot, 1.5f, toCam.normalized, out RaycastHit hit, toCam.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
            {
                desired = pivot + toCam.normalized * Mathf.Max(6f, hit.distance - 1f);
            }

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * dt));
            Vector3 lookPoint = pivot + rotation * Vector3.forward * lookAhead;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

            if (cam != null)
            {
                float speed = targetBody != null ? Compat.GetVelocity(targetBody).magnitude : 0f;
                float fov = baseFov + Mathf.Clamp01((speed - 15f) / 40f) * speedFovBoost;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
            }

            ApplyShake(dt);
        }

        void ApplyShake(float dt)
        {
            if (trauma <= 0f) return;
            float s = trauma * trauma;
            float t = Time.time * 25f;
            float pitch = maxShakeAngle * s * (Mathf.PerlinNoise(seed, t) * 2f - 1f);
            float yawS = maxShakeAngle * s * (Mathf.PerlinNoise(seed + 1f, t) * 2f - 1f);
            float roll = maxShakeAngle * s * (Mathf.PerlinNoise(seed + 2f, t) * 2f - 1f);
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(seed + 3f, t) * 2f - 1f,
                Mathf.PerlinNoise(seed + 4f, t) * 2f - 1f,
                0f) * maxShakeOffset * s;

            transform.rotation *= Quaternion.Euler(pitch, yawS, roll);
            transform.position += transform.rotation * offset;
            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
        }
    }
}
