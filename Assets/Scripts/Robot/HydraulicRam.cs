using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// A working hydraulic ram between two moving parts (for example thigh and shin).
    /// Every frame, after the legs are posed, the sleeve is pinned to one anchor and
    /// aimed at the other, and the chrome rod slides in and out so the ram really
    /// compresses and extends as the joint bends.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class HydraulicRam : MonoBehaviour
    {
        public Transform anchorA;
        public Transform anchorB;
        public Transform sleeve;
        public Transform rod;

        void LateUpdate() => Apply();

        public void Apply()
        {
            if (anchorA == null || anchorB == null) return;
            Vector3 a = anchorA.position, b = anchorB.position;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector3 dir = d / len;
            Vector3 up = Vector3.Cross(dir, anchorA.right);
            if (up.sqrMagnitude < 1e-4f) up = anchorA.up;
            Quaternion rot = Quaternion.LookRotation(dir, up);
            if (sleeve != null) sleeve.SetPositionAndRotation(a, rot);
            if (rod != null) rod.SetPositionAndRotation(b, rot);
        }
    }
}
