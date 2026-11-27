using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The shape of the land, as pure maths (no scene objects), shared by the ground
    /// mesh, the prop builder and the offline preview tool: big hills and ridges,
    /// flat beds for towns and roads, flat pads for bases and objectives.
    /// </summary>
    public static class GroundShape
    {
        // Where the ground must stay flat (filled by WorldDetail before the ground is built)

        public struct FlatRect { public Vector2 min, max; }
        public struct Road { public Vector2 a, b; public float width; }

        /// <summary>Towns, yards and the like: the ground is dead flat inside, sloping up to the hills outside.</summary>
        public static readonly List<FlatRect> FlatRects = new List<FlatRect>();
        /// <summary>Roads keep a flat strip, so they cut passes through the hills.</summary>
        public static readonly List<Road> Roads = new List<Road>();

        /// <summary>0 on roads and in towns, rising to 1 a little way off.</summary>
        public static float FlatMask(float x, float z, float ramp = 45f)
        {
            float m = 1f;
            foreach (var r in FlatRects)
            {
                float dx = Mathf.Max(r.min.x - x, 0f, x - r.max.x);
                float dz = Mathf.Max(r.min.y - z, 0f, z - r.max.y);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < ramp) m = Mathf.Min(m, Mathf.SmoothStep(0f, 1f, d / ramp));
            }
            foreach (var r in Roads)
            {
                float d = SegDist(new Vector2(x, z), r.a, r.b) - r.width * 0.5f - 4f;
                if (d < ramp) m = Mathf.Min(m, d <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, d / ramp));
            }
            return m;
        }

        public static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// The big landforms: rolling hills and ridges between the bases, steeper and
        /// higher in the canyon and the pass, and a long ridge in front of each main
        /// base so the bases can't be shot into from the middle of the map.
        /// </summary>
        public static float Hills(MapId map, MapLayout l, float x, float z, float s)
        {
            float amp;
            switch (map)
            {
                case MapId.Canyon: amp = 38f; break;
                case MapId.FrozenPass: amp = 34f; break;
                case MapId.Foundry: amp = 24f; break;
                case MapId.Refinery: amp = 20f; break;
                case MapId.RuinedCity: amp = 16f; break;
                default: amp = 12f; break;
            }
            float n1 = Mathf.PerlinNoise(x * 0.0042f + s * 0.37f, z * 0.0042f - s * 0.21f);
            float n2 = Mathf.PerlinNoise(x * 0.011f - s, z * 0.011f + s * 0.5f);
            float ridged = 1f - Mathf.Abs(n2 * 2f - 1f);
            float h = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.82f, n1)) * (0.7f + 0.3f * ridged) * amp;
            // The old arena in the middle keeps its designed shape: only gentle swells there.
            float centre = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h *= Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(150f, 230f, centre));

            // Defensive ridge in front of each main base (the base road cuts the gate).
            if (l != null && l.allySpawns != null && l.enemySpawns != null)
                for (int team = 0; team < 2; team++)
                {
                    Vector3 b = l.Base(team);
                    float dir = b.z < 0f ? 1f : -1f;
                    float rz = b.z + dir * 115f;
                    float across = Mathf.Abs(x - b.x);
                    float along = Mathf.Abs(z - rz);
                    float lengthFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(150f, 200f, across));
                    float profile = Mathf.Exp(-(along * along) / (2f * 30f * 30f));
                    float wobble = 0.8f + 0.4f * Mathf.PerlinNoise(x * 0.02f + team * 7f, 3f);
                    h = Mathf.Max(h, 28f * profile * lengthFade * wobble);
                }
            return h;
        }

        /// <summary>0 at spawns and objectives (kept flat), rising to 1 well away from them.</summary>
        public static float LayoutClear(MapLayout l, float x, float z, float ramp = 20f)
        {
            if (l == null) return 1f;
            float m = 1f;
            void Point(Vector3 p, float r)
            {
                float d = Mathf.Sqrt((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z));
                m = Mathf.Min(m, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r + ramp, d)));
            }
            if (l.allySpawns != null) foreach (var p in l.allySpawns) Point(p, 12f);
            if (l.enemySpawns != null) foreach (var p in l.enemySpawns) Point(p, 12f);
            if (l.allyReserve != null) foreach (var p in l.allyReserve) Point(p, 30f);
            if (l.enemyReserve != null) foreach (var p in l.enemyReserve) Point(p, 30f);
            if (l.zones != null) foreach (var p in l.zones) Point(p, l.zoneRadius + 4f);
            if (l.depots != null) foreach (var p in l.depots) Point(p, 10f);
            if (l.reactor != Vector3.zero) Point(l.reactor, 14f);
            return m;
        }
    }
}
