using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Keeps a robot part turning about one of its own axes: radar dishes sweeping,
    /// cooling fans and turbines running. Winds down when its robot is destroyed.
    /// </summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float speed = 90f;
        Damageable owner;
        float rate = 1f;

        void Start()
        {
            owner = GetComponentInParent<Damageable>();
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.up;
            axis.Normalize();
            // Start each one at a different angle so a squad's radars don't sweep in step.
            transform.localRotation *= Quaternion.AngleAxis(Random.Range(0f, 360f), axis);
        }

        void Update()
        {
            bool dead = owner != null && owner.IsDead;
            rate = Mathf.MoveTowards(rate, dead ? 0f : 1f, Time.deltaTime * (dead ? 0.25f : 1f));
            if (rate <= 0f) return;
            transform.localRotation *= Quaternion.AngleAxis(speed * rate * Time.deltaTime, axis);
        }
    }
}
