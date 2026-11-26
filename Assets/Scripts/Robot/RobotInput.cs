using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RobotArena
{
    /// <summary>
    /// Player controls: keyboard + mouse, gamepad and on-screen touch controls.
    /// Movement is relative to the camera (push the stick toward where you want
    /// to go and the robot turns and heads there). Looking around is free:
    /// mouse, right stick, or dragging anywhere on the right of the screen.
    /// Writes into the robot's RobotCommands every frame.
    /// </summary>
    [RequireComponent(typeof(RobotCommands))]
    public class RobotInput : MonoBehaviour
    {
        public VirtualJoystick joystick;
        public TouchButton fireButton;
        public TouchButton specialButton;
        public TouchButton boostButton;
        public LookPad lookPad;

        [Header("Look sensitivity")]
        public float mouseSensitivity = 0.12f;
        public float stickLookSpeed = 140f;
        public float touchSensitivity = 0.16f;

        RobotCommands commands;
        Turret turret;
        bool walker;
        bool reversing;
        Vector3 latchedDir;
        Vector2 latchedStick;

        void Awake()
        {
            commands = GetComponent<RobotCommands>();
            turret = GetComponent<Turret>();
            walker = GetComponent<WalkerLocomotion>() != null;
            // The player's guns swing faster so shots line up while moving.
            if (turret != null)
            {
                turret.yawSpeed *= 2.8f;
                turret.pitchSpeed *= 2.6f;
            }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (commands != null) commands.Clear();
        }

        void Update()
        {
            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;
            bool fire = false, special = false, boost = false;
            bool overUi = PointerOverUi();
            bool locked = Cursor.lockState == CursorLockMode.Locked;

#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                fire |= kb.spaceKey.isPressed || kb.jKey.isPressed;
                special |= kb.eKey.isPressed || kb.kKey.isPressed;
                boost |= kb.leftShiftKey.isPressed || kb.lKey.isPressed;
                if (kb.qKey.isPressed) look.x -= stickLookSpeed * 0.8f * Time.unscaledDeltaTime;
                if (kb.rKey.isPressed) look.x += stickLookSpeed * 0.8f * Time.unscaledDeltaTime;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (locked)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    look += new Vector2(d.x, -d.y) * mouseSensitivity;
                    fire |= mouse.leftButton.isPressed;
                    special |= mouse.rightButton.isPressed;
                }
                else if (mouse.leftButton.wasPressedThisFrame && !overUi && !TouchUiActive())
                {
                    LockCursor();
                }
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                move += pad.leftStick.ReadValue();
                Vector2 rs = pad.rightStick.ReadValue();
                look += new Vector2(rs.x, -rs.y) * stickLookSpeed * Time.unscaledDeltaTime;
                fire |= pad.rightTrigger.isPressed || pad.buttonSouth.isPressed;
                special |= pad.leftTrigger.isPressed || pad.buttonWest.isPressed;
                boost |= pad.leftShoulder.isPressed || pad.rightShoulder.isPressed || pad.buttonEast.isPressed;
            }
#else
            move.y += Input.GetAxisRaw("Vertical");
            move.x += Input.GetAxisRaw("Horizontal");
            fire |= Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.J);
            special |= Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.K);
            boost |= Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.L);
            if (Input.GetKey(KeyCode.Q)) look.x -= stickLookSpeed * 0.8f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.R)) look.x += stickLookSpeed * 0.8f * Time.unscaledDeltaTime;

            if (!Input.touchSupported || Input.touchCount == 0)
            {
                if (locked)
                {
                    look += new Vector2(Input.GetAxisRaw("Mouse X"), -Input.GetAxisRaw("Mouse Y")) * mouseSensitivity * 10f;
                    fire |= Input.GetMouseButton(0);
                    special |= Input.GetMouseButton(1);
                }
                else if (Input.GetMouseButtonDown(0) && !overUi && !TouchUiActive() && !Application.isMobilePlatform)
                {
                    LockCursor();
                }
            }
#endif

            if (joystick != null && joystick.IsActive) move += joystick.Value;
            if (lookPad != null) look += lookPad.ConsumeDelta() * touchSensitivity;
            if (fireButton != null) fire |= fireButton.IsHeld;
            if (specialButton != null) special |= specialButton.IsHeld;
            if (boostButton != null) boost |= boostButton.IsHeld;

            var cam = FollowCamera.Main;
            look *= GameSettings.LookSensitivity;
            // Aim friction: the view slows down while the crosshair sits on a locked
            // enemy, so small thumb movements don't drag the aim off it.
            if (cam != null && turret != null && turret.Target != null)
            {
                Vector3 to = turret.Target.Center - cam.transform.position;
                float off = Vector3.Angle(cam.transform.forward, to);
                float near = Mathf.Atan2(turret.Target.size * 3f, to.magnitude) * Mathf.Rad2Deg + 3f;
                if (off < near) look *= Mathf.Lerp(0.55f, 1f, off / near);
            }
            if (cam != null) cam.AddLook(look.x, look.y);

            bool looking = look.sqrMagnitude > 0.0001f;
            DriveTowards(Vector2.ClampMagnitude(move, 1f), cam, fire || special, looking);

            // Keep a locked enemy under the crosshair while moving or shooting.
            if (cam != null && turret != null && turret.Target != null && (fire || special || move.sqrMagnitude > 0.02f))
                cam.Assist(turret.Target.Center, looking ? 0.3f : 1f);
            commands.primary = fire;
            commands.special = special;
            commands.boost = boost;
        }

        /// <summary>
        /// Turns the stick (relative to the camera) into movement. Walkers walk in that direction
        /// (forward, back or sideways) while the hull turns to face where you look. Wheeled robots
        /// steer toward it, or reverse when it is behind them. The direction is fixed in the world
        /// while the stick is held, so the robot doesn't chase the camera round in circles.
        /// </summary>
        void DriveTowards(Vector2 move, FollowCamera cam, bool shooting, bool looking)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Quaternion camYaw = cam != null ? cam.FlatRotation : Quaternion.LookRotation(fwd);
            Vector3 camFwd = camYaw * Vector3.forward;
            float mag = move.magnitude;

            if (mag < 0.12f)
            {
                latchedStick = Vector2.zero;
                commands.throttle = 0f;
                commands.steer = 0f;
                if (walker)
                {
                    commands.hasMove = true;
                    commands.move = Vector3.zero;
                    // Shooting turns the hull toward where you look; otherwise stand still.
                    commands.hasFace = shooting;
                    commands.face = camFwd;
                    return;
                }
                // Standing still and shooting behind? Turn to face where the camera looks.
                if (shooting && turret != null)
                {
                    float a = Vector3.SignedAngle(fwd, camFwd, Vector3.up);
                    if (Mathf.Abs(a) > turret.maxYaw * 0.8f) commands.steer = Mathf.Clamp(a / 40f, -1f, 1f);
                }
                return;
            }

            // Re-read the direction when the stick moves a lot, or while the player looks around.
            if (latchedStick == Vector2.zero || looking || Vector2.Angle(latchedStick, move) > 20f)
            {
                latchedStick = move;
                latchedDir = camYaw * new Vector3(move.x, 0f, move.y).normalized;
            }
            Vector3 wanted = latchedDir;

            if (walker)
            {
                commands.hasMove = true;
                commands.move = wanted * mag;
                commands.hasFace = true;
                commands.face = camFwd;
                commands.throttle = Vector3.Dot(wanted, fwd) * mag;
                commands.steer = 0f;
                return;
            }

            // Wheeled robots drive like tanks: stick up/down is forward/reverse, stick
            // left/right always turns the hull that way (also while reversing). This
            // never depends on the camera, so turning can't flip left and right.
            {
                float x = move.x, y = move.y;
                commands.steer = Mathf.Clamp(x * (0.55f + 0.45f * Mathf.Abs(x)) * 1.15f, -1f, 1f);
                commands.throttle = Mathf.Abs(y) < 0.15f ? 0f : Mathf.Clamp(y * 1.1f, -1f, 1f);
                return;
            }
#pragma warning disable CS0162
            float angle = Vector3.SignedAngle(fwd, wanted, Vector3.up);
            // Reverse when the wanted direction is behind; keep reversing until it's well in front.
            if (reversing) { if (Mathf.Abs(angle) < 70f) reversing = false; }
            else if (Mathf.Abs(angle) > 120f) reversing = true;

            if (reversing)
            {
                float backAngle = Vector3.SignedAngle(-fwd, wanted, Vector3.up);
                commands.throttle = -mag * Mathf.Clamp01(1.15f - Mathf.Abs(backAngle) / 110f);
                commands.steer = Mathf.Clamp(backAngle / 35f, -1f, 1f);
                return;
            }

            commands.steer = Mathf.Clamp(angle / 35f, -1f, 1f);
            commands.throttle = mag * Mathf.Clamp01(1.15f - Mathf.Abs(angle) / 110f);
        }

        static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        static bool PointerOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        bool TouchUiActive()
        {
            return (joystick != null && joystick.IsActive)
                   || (fireButton != null && fireButton.IsHeld)
                   || (specialButton != null && specialButton.IsHeld)
                   || (boostButton != null && boostButton.IsHeld)
                   || (lookPad != null && lookPad.IsActive);
        }
    }
}
