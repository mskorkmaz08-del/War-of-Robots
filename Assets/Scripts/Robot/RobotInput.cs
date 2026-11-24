using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RobotArena
{
    /// <summary>
    /// Reads player controls from keyboard, gamepad and the on-screen touch controls,
    /// and exposes them as simple values the robot systems read.
    /// Works with both the new Input System and the old Input Manager.
    /// </summary>
    public class RobotInput : MonoBehaviour
    {
        [Tooltip("Optional on-screen joystick for touch devices.")]
        public VirtualJoystick joystick;

        [Tooltip("Optional on-screen attack button for touch devices.")]
        public TouchButton attackButton;

        /// <summary>-1 (reverse) to 1 (forward).</summary>
        public float Throttle { get; private set; }

        /// <summary>-1 (left) to 1 (right).</summary>
        public float Steer { get; private set; }

        /// <summary>True while the attack control is held.</summary>
        public bool AttackHeld { get; private set; }

        void Update()
        {
            float throttle = 0f;
            float steer = 0f;
            bool attack = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                attack |= kb.spaceKey.isPressed;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                throttle += stick.y;
                steer += stick.x;
                attack |= pad.buttonSouth.isPressed || pad.rightTrigger.isPressed;
            }
#else
            throttle += Input.GetAxisRaw("Vertical");
            steer += Input.GetAxisRaw("Horizontal");
            attack |= Input.GetKey(KeyCode.Space);
#endif

            if (joystick != null && joystick.IsActive)
            {
                throttle += joystick.Value.y;
                steer += joystick.Value.x;
            }

            if (attackButton != null)
                attack |= attackButton.IsHeld;

            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            AttackHeld = attack;
        }
    }
}
