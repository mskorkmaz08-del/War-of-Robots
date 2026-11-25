using UnityEngine;
using UnityEngine.EventSystems;

namespace RobotArena
{
    /// <summary>
    /// Invisible area behind the touch buttons: drag on it to look around.
    /// With a mouse, clicking it captures the cursor for mouse-look.
    /// </summary>
    public class LookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        Vector2 delta;
        int pointerId = int.MinValue;

        public bool IsActive => pointerId != int.MinValue;

        /// <summary>Drag since the last call, in screen pixels (y up = look up).</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 d = delta;
            delta = Vector2.zero;
            // Screen drag up should tilt the view up; camera pitch is positive downwards.
            return new Vector2(d.x, -d.y);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.pointerId < 0 && !Application.isMobilePlatform)
            {
                // Mouse: capture the cursor for mouse-look (Esc releases it).
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }
            if (IsActive) return;
            pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            // Normalise to a 1080p-tall screen so sensitivity feels the same on every phone.
            float scale = 1080f / Mathf.Max(1f, Screen.height);
            delta += eventData.delta * scale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) pointerId = int.MinValue;
        }

        void OnDisable()
        {
            pointerId = int.MinValue;
            delta = Vector2.zero;
        }
    }
}
