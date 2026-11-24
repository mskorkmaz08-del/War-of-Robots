using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RobotArena
{
    /// <summary>
    /// Player controls: keyboard, gamepad and on-screen touch controls.
    /// Writes into the robot's RobotCommands every frame.
    /// </summary>
    [RequireComponent(typeof(RobotCommands))]
    public class RobotInput : MonoBehaviour
    {
        public VirtualJoystick joystick;
        public TouchButton fireButton;
        public TouchButton specialButton;
        public TouchButton boostButton;

        RobotCommands commands;

        void Awake()
        {
            commands = GetComponent<RobotCommands>();
        }

        void Update()
        {
            float throttle = 0f, steer = 0f;
            bool fire = false, special = false, boost = false;

#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                fire |= kb.spaceKey.isPressed || kb.jKey.isPressed;
                special |= kb.eKey.isPressed || kb.kKey.isPressed;
                boost |= kb.leftShiftKey.isPressed || kb.lKey.isPressed;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                fire |= mouse.leftButton.isPressed && !TouchUiActive();
                special |= mouse.rightButton.isPressed;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                throttle += stick.y;
                steer += stick.x;
                fire |= pad.rightTrigger.isPressed || pad.buttonSouth.isPressed;
                special |= pad.leftTrigger.isPressed || pad.buttonWest.isPressed;
                boost |= pad.leftShoulder.isPressed || pad.rightShoulder.isPressed || pad.buttonEast.isPressed;
            }
#else
            throttle += Input.GetAxisRaw("Vertical");
            steer += Input.GetAxisRaw("Horizontal");
            fire |= Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.J) || (Input.GetMouseButton(0) && !TouchUiActive());
            special |= Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.K) || Input.GetMouseButton(1);
            boost |= Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.L);
#endif

            if (joystick != null && joystick.IsActive)
            {
                throttle += joystick.Value.y;
                steer += joystick.Value.x;
            }
            if (fireButton != null) fire |= fireButton.IsHeld;
            if (specialButton != null) special |= specialButton.IsHeld;
            if (boostButton != null) boost |= boostButton.IsHeld;

            commands.throttle = Mathf.Clamp(throttle, -1f, 1f);
            commands.steer = Mathf.Clamp(steer, -1f, 1f);
            commands.primary = fire;
            commands.special = special;
            commands.boost = boost;
        }

        bool TouchUiActive()
        {
            // Avoid firing when the mouse is used on the on-screen buttons.
            return (joystick != null && joystick.IsActive)
                   || (fireButton != null && fireButton.IsHeld)
                   || (specialButton != null && specialButton.IsHeld)
                   || (boostButton != null && boostButton.IsHeld)
                   || (UnityEngine.EventSystems.EventSystem.current != null
                       && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject());
        }
    }
}
