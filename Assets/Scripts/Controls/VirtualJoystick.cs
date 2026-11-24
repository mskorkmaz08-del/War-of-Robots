using UnityEngine;
using UnityEngine.EventSystems;

namespace RobotArena
{
    /// <summary>On-screen thumb stick for touch screens (also works with a mouse).</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform background;
        public RectTransform handle;
        [Tooltip("How far the handle can move from the centre, in canvas units.")]
        public float radius = 120f;
        [Range(0f, 0.5f)] public float deadZone = 0.1f;

        public Vector2 Value { get; private set; }
        public bool IsActive { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Vector2 v = Vector2.ClampMagnitude(local / radius, 1f);
            Value = v.magnitude < deadZone ? Vector2.zero : v;
            if (handle != null) handle.anchoredPosition = v * radius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        void OnDisable()
        {
            IsActive = false;
            Value = Vector2.zero;
        }
    }
}
