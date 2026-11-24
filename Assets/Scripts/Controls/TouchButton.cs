using UnityEngine;
using UnityEngine.EventSystems;

namespace RobotArena
{
    /// <summary>On-screen button that reports whether it is being held.</summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => IsHeld = true;
        public void OnPointerUp(PointerEventData eventData) => IsHeld = false;
        public void OnPointerExit(PointerEventData eventData) => IsHeld = false;

        void OnDisable() => IsHeld = false;
    }
}
