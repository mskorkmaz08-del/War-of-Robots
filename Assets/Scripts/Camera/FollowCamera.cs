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

        [Header("Free look")]
        public float minPitch = -12f;
        public float maxPitch = 60f;
        public float defaultPitch = 12f;
        [Tooltip("Seconds without look input before the camera drifts back behind the robot while it moves.")]
        public float recenterDelay = 2.5f;

        float yaw;
        float pitch;
        float lastLookTime = -999f;
        float trauma;
        float smoothY = float.NaN;
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
                pitch = defaultPitch;
                transform.position = target.position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, height, -distance);
            }
        }

        /// <summary>Rotates the view (degrees). Called by mouse, touch drag and gamepad.</summary>
        public void AddLook(float yawDegrees, float pitchDegrees)
        {
            if (Mathf.Abs(yawDegrees) + Mathf.Abs(pitchDegrees) < 0.001f) return;
            yaw += yawDegrees;
            pitch = Mathf.Clamp(pitch + pitchDegrees, minPitch, maxPitch);
            lastLookTime = Time.unscaledTime;
        }

        float lastAssistTime = -999f;

        /// <summary>
        /// Aim assist: gently turns the view to keep a locked enemy under the crosshair,
        /// so strafing or backing up doesn't slide the aim off it. Only pulls when the
        /// enemy is already near the centre, so it never yanks the view around.
        /// </summary>
        public void Assist(Vector3 point, float strength)
        {
            Vector3 dir = point - transform.position;
            if (dir.sqrMagnitude < 1f) return;
            dir.Normalize();
            float off = Vector3.Angle(transform.forward, dir);
            if (off > 30f) return;
            float wantedYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float wantedPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
            float k = 1f - Mathf.Exp(-6f * strength * Time.unscaledDeltaTime);
            yaw = Mathf.LerpAngle(yaw, wantedYaw, k);
            pitch = Mathf.Lerp(pitch, wantedPitch, k * 0.6f);
            lastAssistTime = Time.unscaledTime;
        }

        /// <summary>The flat direction the camera faces; movement is relative to this.</summary>
        public Quaternion FlatRotation => Quaternion.Euler(0f, yaw, 0f);

        /// <summary>The ray through the centre of the screen (the crosshair).</summary>
        public Ray AimRay => new Ray(transform.position, transform.forward);

        /// <summary>Adds screen shake. Strength fades with distance from the camera.</summary>
        public static void Shake(float amount, Vector3 source, float radius = 120f)
        {
            if (Main == null || !GameSettings.CameraShake) return;
            float d = Vector3.Distance(Main.transform.position, source);
            float falloff = Mathf.Clamp01(1f - d / radius);
            Main.trauma = Mathf.Clamp01(Main.trauma + amount * falloff * 0.6f); // gentler overall
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            // Drift back behind the robot when the player stops looking around and drives.
            float speedNow = targetBody != null ? Compat.GetVelocity(targetBody).magnitude : 0f;
            if (Time.unscaledTime - lastLookTime > recenterDelay && Time.unscaledTime - lastAssistTime > 1f && speedNow > 3f)
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
                if (flatForward.sqrMagnitude > 0.01f)
                {
                    float targetYaw = Quaternion.LookRotation(flatForward).eulerAngles.y;
                    yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-rotateSharpness * 0.5f * dt));
                    pitch = Mathf.Lerp(pitch, defaultPitch, 1f - Mathf.Exp(-1f * dt));
                }
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            // Follow height slowly so the robot's stride and footfalls don't bob the view.
            float ty = target.position.y;
            smoothY = float.IsNaN(smoothY) || Mathf.Abs(smoothY - ty) > 15f ? ty : Mathf.Lerp(smoothY, ty, 1f - Mathf.Exp(-2.5f * dt));
            Vector3 pivot = new Vector3(target.position.x, smoothY, target.position.z) + Vector3.up * lookHeight;
            // Camera sits behind and above the pivot, so the robot stays in the lower
            // part of the screen and the crosshair in the middle is free to aim.
            Vector3 desired = pivot + rotation * new Vector3(0f, 0f, -distance) + Vector3.up * (height + distance * 0.45f);

            // Pull in if a wall or building is between the robot and the camera.
            Vector3 toCam = desired - pivot;
            if (Physics.SphereCast(pivot, 1.5f, toCam.normalized, out RaycastHit hit, toCam.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
            {
                desired = pivot + toCam.normalized * Mathf.Max(6f, hit.distance - 1f);
            }
            // Never go under the floor.
            desired.y = Mathf.Max(desired.y, 2f);

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * 2f * dt));
            transform.rotation = rotation;

            if (cam != null)
            {
                float fov = baseFov + Mathf.Clamp01((speedNow - 15f) / 40f) * speedFovBoost;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
            }

            ApplyShake(dt);
        }

        void ApplyShake(float dt)
        {
            if (trauma <= 0f) return;
            float s = trauma * trauma;
            // Low-frequency rumble reads as something huge; fast jitter reads as a small toy.
            float t = Time.time * 11f;
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
