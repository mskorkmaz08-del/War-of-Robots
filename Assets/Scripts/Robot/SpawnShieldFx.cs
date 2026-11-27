using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Shows a freshly (re)spawned robot's protection: blue energy flickers over the
    /// hull until the shield runs out or the robot opens fire.
    /// </summary>
    public class SpawnShieldFx : MonoBehaviour
    {
        Damageable d;
        float next;

        void Awake() { d = GetComponent<Damageable>(); }

        void Update()
        {
            if (d == null || d.IsDead || !d.Shielded) return;
            if (Time.time < next) return;
            next = Time.time + 0.18f;
            Vector3 c = d.Center + Random.insideUnitSphere * 3f * Mathf.Max(1f, d.size);
            Fx.EnergyBurst(c, new Color(0.35f, 0.75f, 1f), 0.6f * d.size);
        }
    }
}
