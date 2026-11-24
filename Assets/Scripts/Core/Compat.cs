using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Small helpers so the project compiles on both Unity 2022.3 and Unity 6,
    /// where Rigidbody.velocity was renamed to linearVelocity.
    /// </summary>
    public static class Compat
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }
    }
}
