using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// What the robot is being told to do this frame. Written by the player's
    /// controls or by the AI, read by locomotion and weapons.
    /// </summary>
    public class RobotCommands : MonoBehaviour
    {
        /// <summary>-1 (reverse) to 1 (forward).</summary>
        public float throttle;
        /// <summary>-1 (left) to 1 (right).</summary>
        public float steer;
        /// <summary>Main gun held.</summary>
        public bool primary;
        /// <summary>Special weapon held (missiles / railgun).</summary>
        public bool special;
        /// <summary>Boost held (jump jets / ultra thrust).</summary>
        public bool boost;

        public void Clear()
        {
            throttle = 0f;
            steer = 0f;
            primary = false;
            special = false;
            boost = false;
        }
    }
}
