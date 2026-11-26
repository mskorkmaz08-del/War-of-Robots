using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// How a giant machine takes a hit. The shot's momentum is tiny next to a
    /// 30-60 t robot, so nothing gets thrown around; what you see is the
    /// structure absorbing it: the upper body rocks away from the impact on its
    /// waist bearing and springs back, the hull sways a little on its legs or
    /// suspension, and a heavy hit makes a walker sag at the knees before it
    /// recovers. Bigger hits and lighter robots react more.
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        Damageable dmg;
        Rigidbody rb;
        Turret turret;
        WalkerLocomotion walker;
        float cooldown;

        void Start()
        {
            dmg = GetComponent<Damageable>();
            rb = GetComponent<Rigidbody>();
            turret = GetComponent<Turret>();
            walker = GetComponent<WalkerLocomotion>();
            if (dmg != null) dmg.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (dmg != null) dmg.Damaged -= OnDamaged;
        }

        void Update()
        {
            cooldown -= Time.deltaTime;
        }

        void OnDamaged(float amount, Vector3 point)
        {
            if (dmg == null || dmg.IsDead || amount <= 0f) return;
            // 0..1.5: a hit worth ~8% of the robot's health is a solid 1.
            float severity = Mathf.Clamp(amount / Mathf.Max(1f, dmg.maxHealth) * 12f, 0f, 1.5f);
            if (severity < 0.05f) return;
            // Many small hits in a row (gatling) blend into a shudder instead of stacking up.
            if (cooldown > 0f) severity *= 0.35f;
            cooldown = 0.12f;

            float massScale = rb != null ? Mathf.Sqrt(30000f / Mathf.Max(5000f, rb.mass)) : 1f;
            Vector3 centre = rb != null ? rb.worldCenterOfMass : transform.position;
            Vector3 away = centre - point;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            // The upper body rocks away from the impact: pitch back from a frontal hit,
            // roll from a side hit, and twist a little from an off-centre one.
            if (turret != null && turret.yawPivot != null)
            {
                Vector3 local = turret.yawPivot.InverseTransformDirection(away);
                Vector3 off = turret.yawPivot.InverseTransformPoint(point);
                float k = 70f * severity * massScale;
                turret.Flinch(new Vector3(-local.z * k * 0.8f, Mathf.Clamp(off.x, -1f, 1f) * -local.z * k * 0.6f, -local.x * k));
            }

            // The hull sways: a push at the hit point, so a high hit also tips it.
            if (rb != null)
            {
                float dv = 0.9f * severity * massScale;
                Vector3 at = Vector3.Lerp(centre, point, 0.6f);
                rb.AddForceAtPosition(away * dv, at, ForceMode.VelocityChange);
            }

            if (walker != null) walker.Stagger(away, severity);
        }
    }
}
