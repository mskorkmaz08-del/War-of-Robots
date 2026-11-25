using UnityEngine;

namespace RobotArena
{
    /// <summary>Base for the ways robots move (walking, wheels).</summary>
    [RequireComponent(typeof(Rigidbody), typeof(RobotCommands))]
    public abstract class Locomotion : MonoBehaviour, IDisableOnDeath
    {
        [Header("Weight")]
        [Tooltip("Extra gravity while airborne, as a fraction of normal gravity. Giant machines should fall hard, not float.")]
        public float extraAirGravity = 0.8f;

        [Header("Boost")]
        public float maxFuel = 100f;
        public float fuelDrain = 30f;
        public float fuelRegen = 14f;
        [Tooltip("Fuel needed before boost can start again after running dry.")]
        public float fuelRestart = 25f;

        protected Rigidbody rb;
        protected RobotCommands commands;
        protected float fuel;
        protected bool boostLocked;

        public float Fuel01 => Mathf.Clamp01(fuel / maxFuel);
        public bool IsBoosting { get; protected set; }
        public abstract bool IsGrounded { get; }

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            commands = GetComponent<RobotCommands>();
            fuel = maxFuel;
        }

        /// <summary>Call once per FixedUpdate; returns whether boost is active this step.</summary>
        protected bool UpdateBoost(bool wanted)
        {
            float dt = Time.fixedDeltaTime;
            if (boostLocked && fuel >= fuelRestart) boostLocked = false;

            bool active = wanted && !boostLocked && fuel > 0f;
            if (active)
            {
                fuel -= fuelDrain * dt;
                if (fuel <= 0f)
                {
                    fuel = 0f;
                    boostLocked = true;
                    active = false;
                }
            }
            else
            {
                fuel = Mathf.Min(maxFuel, fuel + fuelRegen * dt);
            }
            IsBoosting = active;
            return active;
        }

        /// <summary>Makes jumps and ramp launches feel heavy. Call from FixedUpdate.</summary>
        protected void ApplyExtraGravity(bool grounded)
        {
            if (!grounded && extraAirGravity > 0f)
                rb.AddForce(Physics.gravity * extraAirGravity, ForceMode.Acceleration);
        }

        protected virtual void OnDisable()
        {
            IsBoosting = false;
        }
    }

    /// <summary>Raycasts that ignore a given robot's own colliders and loose physics props.</summary>
    public static class GroundProbe
    {
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static bool Cast(Vector3 origin, Vector3 direction, float distance, Rigidbody ignore, out RaycastHit best, bool ignoreDynamic = false)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (ignore != null && h.rigidbody == ignore) continue;
                if (ignoreDynamic && h.rigidbody != null && !h.rigidbody.isKinematic) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }
    }
}
