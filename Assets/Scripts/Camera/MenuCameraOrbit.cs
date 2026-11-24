using UnityEngine;

namespace RobotArena
{
    /// <summary>Slowly circles the arena behind the robot-select menu.</summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        public Vector3 center = new Vector3(0f, 10f, 0f);
        public float radius = 150f;
        public float height = 45f;
        public float speed = 6f;

        FollowCamera follow;
        float angle = 200f;

        void Awake()
        {
            follow = GetComponent<FollowCamera>();
        }

        void LateUpdate()
        {
            if (follow != null && follow.target != null) return;
            angle += speed * Time.deltaTime;
            float a = angle * Mathf.Deg2Rad;
            transform.position = center + new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
            transform.rotation = Quaternion.LookRotation(center - transform.position);
        }
    }
}
