using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Third-person chase camera that follows the robot's heading smoothly
    /// and ignores the robot's pitch and roll, so flips don't spin the view.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 7f;
        public float height = 3.5f;
        public float lookHeight = 0.5f;
        public float followSharpness = 6f;
        public float rotateSharpness = 3f;

        float yaw;

        void Start()
        {
            if (target != null) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.01f)
            {
                float targetYaw = Quaternion.LookRotation(flatForward).eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-rotateSharpness * Time.deltaTime));
            }

            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 desired = target.position + rotation * new Vector3(0f, height, -distance);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
