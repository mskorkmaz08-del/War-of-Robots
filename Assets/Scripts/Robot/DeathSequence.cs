using System.Collections;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// How a robot dies. It is short and stays in place:
    /// 1. Critical failure (about 1.3 s): warning beeps that speed up and rise in pitch,
    ///    a red warning light flashing with them, sparks and small pops bursting from the
    ///    machine, and the robot shuddering.
    /// 2. The blast: a big explosion, armour and a few of the robot's own parts thrown off,
    ///    the wreck burnt black.
    /// 3. Collapse: walkers fall the way a knock-down does and never get up; wheeled robots'
    ///    suspension gives out and the hull sags onto one side and brakes to a stop.
    /// Once the wreck has come to rest it is frozen in place, so nothing can send it
    /// sliding, spinning or flying away afterwards.
    /// </summary>
    public class DeathSequence : MonoBehaviour
    {
        // Beep times (s): slow at first, then faster and faster before the blast.
        static readonly float[] Beeps = { 0f, 0.36f, 0.64f, 0.85f, 1.0f, 1.11f, 1.19f };
        const float BlastAt = 1.32f;
        const float MaxSettleTime = 5f;

        static readonly Color WarningRed = new Color(1f, 0.12f, 0.08f);

        Damageable robot;
        Locomotion loco;
        Renderer[] renderers;

        public static void Begin(Damageable robot, Locomotion loco)
        {
            var seq = robot.gameObject.AddComponent<DeathSequence>();
            seq.robot = robot;
            seq.loco = loco;
            seq.StartCoroutine(seq.Run());
        }

        IEnumerator Run()
        {
            renderers = GetComponentsInChildren<MeshRenderer>();
            var walker = loco as WalkerLocomotion;
            Vector3 away = Random.insideUnitSphere;
            if (robot.LastAttacker != null) away = robot.Center - robot.LastAttacker.Center;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            // ---- 1. Critical failure ----
            float t = 0f;
            int beep = 0;
            float nextSpark = 0.1f;
            int pops = 0;
            while (t < BlastAt)
            {
                while (beep < Beeps.Length && t >= Beeps[beep])
                {
                    float k = (float)beep / (Beeps.Length - 1);
                    Vector3 c = robot.Center;
                    GameAudio.Play(Sfx.Alarm, c, Mathf.Lerp(0.75f, 1f, k), Mathf.Lerp(1f, 1.3f, k));
                    Fx.Flash(c + Vector3.up * 2f * robot.size, WarningRed, 14f * robot.size, Mathf.Lerp(2.5f, 4f, k), 0.14f);
                    beep++;
                }
                if (t >= nextSpark)
                {
                    nextSpark = t + Random.Range(0.12f, 0.24f);
                    Vector3 p = RandomPointOnRobot();
                    Vector3 n = (p - robot.Center).normalized + Vector3.up * 0.5f;
                    Fx.Sparks(p, n, 12, 14f, 80f);
                    GameAudio.Play(Sfx.Sizzle, p, 0.45f, Random.Range(0.8f, 1.1f));
                }
                // Two small internal blasts: a pop, a jolt.
                if (pops < 2 && t >= (pops == 0 ? 0.5f : 0.95f))
                {
                    pops++;
                    Vector3 p = RandomPointOnRobot();
                    Fx.SmallBlast(p, 0.45f * robot.size);
                    GameAudio.Play(Sfx.Explosion, p, 0.6f, 1.3f);
                    FollowCamera.Shake(0.25f, p, 90f);
                    if (walker != null) walker.Stagger((p - robot.Center).normalized * -1f, 0.8f);
                    else if (robot.Body != null && !robot.Body.isKinematic)
                        robot.Body.AddTorque(Random.insideUnitSphere * 0.25f, ForceMode.VelocityChange);
                }
                t += Time.deltaTime;
                yield return null;
            }

            // ---- 2. The blast ----
            robot.Explode();
            if (loco != null) loco.Collapse(away);

            // ---- 3. Collapse, then freeze the wreck where it lies ----
            float settle = 0f;
            while (settle < MaxSettleTime)
            {
                if (loco == null || loco.Settled) break;
                settle += Time.deltaTime;
                yield return null;
            }
            if (loco != null) loco.enabled = false;
            var body = robot.Body;
            if (body != null && !body.isKinematic)
            {
                Compat.SetVelocity(body, Vector3.zero);
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        Vector3 RandomPointOnRobot()
        {
            for (int i = 0; i < 4 && renderers != null && renderers.Length > 0; i++)
            {
                var r = renderers[Random.Range(0, renderers.Length)];
                if (r == null || !r.enabled) continue;
                var b = r.bounds;
                return b.center + Vector3.Scale(b.extents, Random.insideUnitSphere) * 0.8f;
            }
            return robot.Center + Random.insideUnitSphere * 2f * robot.size;
        }
    }
}
