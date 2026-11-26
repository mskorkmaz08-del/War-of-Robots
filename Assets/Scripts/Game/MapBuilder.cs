using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>Where things go on a map: team bases, capture points, depots, reactor.</summary>
    public class MapLayout
    {
        public Vector3[] allySpawns;
        public Vector3[] enemySpawns;
        public Vector3[] zones;
        public float zoneRadius = 20f;
        public Vector3[] depots;
        public Vector3 reactor;
    }

    /// <summary>
    /// Builds the battle maps. The Foundry is built in the editor (ArenaBuilder) and baked
    /// into the scene; the Dockyard and Canyon Outpost are built here at deploy time from
    /// the same textured materials, then static-batched.
    /// </summary>
    public static partial class MapBuilder
    {
        public const float ArenaSize = 280f;
        static GameAssets A => GameAssets.Get();

        public static MapLayout Layout(MapId map)
        {
            var l = new MapLayout
            {
                allySpawns = new[] { new Vector3(0f, 0f, -105f), new Vector3(-40f, 0f, -105f), new Vector3(40f, 0f, -105f), new Vector3(-68f, 0f, -112f), new Vector3(68f, 0f, -112f) },
                enemySpawns = new[] { new Vector3(0f, 0f, 100f), new Vector3(-40f, 0f, 100f), new Vector3(40f, 0f, 100f), new Vector3(-68f, 0f, 110f), new Vector3(68f, 0f, 110f) },
            };
            switch (map)
            {
                case MapId.Foundry:
                    l.zones = new[] { new Vector3(-75f, 0f, -35f), new Vector3(0f, 0f, 0f), new Vector3(78f, 0f, 15f) };
                    l.zoneRadius = 22f;
                    l.depots = new[] { new Vector3(-92f, 0f, 42f), new Vector3(0f, 0f, 82f), new Vector3(86f, 0f, 52f) };
                    l.reactor = new Vector3(0f, 0f, -80f);
                    break;
                case MapId.Dockyard:
                    l.zones = new[] { new Vector3(-80f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(80f, 0f, 0f) };
                    l.depots = new[] { new Vector3(-90f, 0f, 64f), new Vector3(0f, 0f, 82f), new Vector3(90f, 0f, 60f) };
                    l.reactor = new Vector3(0f, 0f, -82f);
                    break;
                default:
                    l.zones = new[] { new Vector3(-80f, 0f, 5f), new Vector3(0f, 0f, 0f), new Vector3(80f, 0f, -5f) };
                    l.depots = new[] { new Vector3(-84f, 0f, 56f), new Vector3(0f, 0f, 80f), new Vector3(84f, 0f, 56f) };
                    l.reactor = new Vector3(0f, 0f, -78f);
                    break;
            }
            // The battlefield is bigger than the original yard: spread everything out.
            // Bases sit near the edges, objectives across the wider middle.
            for (int i = 0; i < l.allySpawns.Length; i++) l.allySpawns[i] = new Vector3(l.allySpawns[i].x * 1.3f, 0f, -175f - (Mathf.Abs(l.allySpawns[i].x) > 60f ? 8f : 0f));
            for (int i = 0; i < l.enemySpawns.Length; i++) l.enemySpawns[i] = new Vector3(l.enemySpawns[i].x * 1.3f, 0f, 172f + (Mathf.Abs(l.enemySpawns[i].x) > 60f ? 8f : 0f));
            for (int i = 0; i < l.zones.Length; i++) l.zones[i] *= 1.35f;
            for (int i = 0; i < l.depots.Length; i++) l.depots[i] *= 1.35f;
            l.reactor *= 1.35f;
            return l;
        }

        /// <summary>Replaces the Foundry with another map (no-op for the Foundry).</summary>
        public static void Build(MapId map)
        {
            if (A == null) return;
            if (map == MapId.Foundry)
            {
                Frontier.Build(map, Layout(map));
                return;
            }
            foreach (var name in new[] { "Arena", "Containers", "BuildingDetails" })
            {
                var old = GameObject.Find(name);
                // Deactivate first: Destroy only happens at the end of the frame, and the
                // new map's placement checks must not see the Foundry's colliders.
                if (old != null) { old.SetActive(false); Object.Destroy(old); }
            }
            var root = new GameObject("Arena").transform;
            var layout = Layout(map);
            switch (map)
            {
                case MapId.Dockyard: BuildDockyard(root); break;
                case MapId.Canyon: BuildCanyon(root); break;
                case MapId.FrozenPass: BuildFrozenPass(root, layout); break;
                case MapId.RuinedCity: BuildRuinedCity(root, layout); break;
                default: BuildRefinery(root, layout); break;
            }
            StaticBatchingUtility.Combine(root.gameObject);
            Frontier.Build(map, Layout(map));
        }

        // ---------------------------------------------------------------------

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, float yaw, Vector3 scale, Material mat, float pitch = 0f, float roll = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void Floor(Transform root, Material mat)
        {
            Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, 0f), 0f, new Vector3(ArenaSize + 40f, 1f, ArenaSize + 40f), mat);
        }

        static void Walls(Transform root, Material wall, Material trim)
        {
            float half = ArenaSize * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f;
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Prim(PrimitiveType.Cube, root, r * new Vector3(0f, 9f, half + 2f), yaw, new Vector3(ArenaSize + 8f, 18f, 4f), wall);
                if (trim != null) Prim(PrimitiveType.Cube, root, r * new Vector3(0f, 18.3f, half + 2f), yaw, new Vector3(ArenaSize + 8f, 0.6f, 4.4f), trim);
                for (int k = -3; k <= 3; k++)
                    Prim(PrimitiveType.Cube, root, r * new Vector3(k * 38f, 10f, half - 1f), yaw, new Vector3(4f, 20f, 4f), A.buildingMat);
            }
        }

        static Material Container(int i) => A.containerMats != null && A.containerMats.Length > 0
            ? A.containerMats[i % A.containerMats.Length] : A.buildingMat;

        /// <summary>A stack of shipping containers. Loose ones get physics so they can be smashed.</summary>
        static void Stack(Transform root, Vector3 pos, float yaw, int count, int seed, bool loose = false)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < count; i++)
            {
                int layer = i / 2;
                float x = (i % 2 - 0.5f) * 2.9f;
                if (count % 2 == 1 && i == count - 1) x = 0f;
                var c = Prim(PrimitiveType.Cube, loose ? null : root, pos + r * new Vector3(x, 1.35f + layer * 2.7f, 0f), yaw, new Vector3(2.6f, 2.6f, 8f), Container(seed + i));
                if (loose)
                {
                    c.name = "Container";
                    var rb = c.AddComponent<Rigidbody>();
                    rb.mass = 4000f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }
        }

        // ---------------------------------------------------------------------
        // DOCKYARD
        // ---------------------------------------------------------------------

        static void BuildDockyard(Transform root)
        {

            // Container rows along both sides of the central lane, leaving the capture points open.
            int seed = 0;
            foreach (float z in new[] { -38f, 38f })
            for (float x = -115f; x <= 115f; x += 14f)
            {
                if (Mathf.Abs(x - 0f) < 16f || Mathf.Abs(Mathf.Abs(x) - 80f) < 10f) continue;
                int count = 2 + (seed * 7 % 4);
                Stack(root, new Vector3(x, 0f, z + ((seed % 3) - 1) * 2f), 90f + ((seed % 2) * 6f - 3f), count, seed++);
            }
            // Scattered stacks deeper in each half
            var extra = new (Vector3 p, float yaw, int n)[]
            {
                (new Vector3(-60f, 0f, -70f), 20f, 3), (new Vector3(55f, 0f, -75f), -15f, 4), (new Vector3(-20f, 0f, -60f), 75f, 2),
                (new Vector3(-55f, 0f, 72f), -20f, 4), (new Vector3(60f, 0f, 70f), 15f, 3), (new Vector3(22f, 0f, 62f), 80f, 2),
            };
            foreach (var e in extra) Stack(root, e.p, e.yaw, e.n, seed++);
            // A few loose containers to smash
            Stack(null, new Vector3(-35f, 0f, 0f), 10f, 2, seed++, true);
            Stack(null, new Vector3(38f, 0f, 12f), -25f, 2, seed++, true);
            var loose = new GameObject("Containers");
            foreach (var go in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                if (go.name == "Container" && go.transform.parent == null) go.transform.SetParent(loose.transform, true);

            // Gantry cranes at both ends of the yard
            foreach (float x in new[] { -118f, 118f })
            {
                var steel = A.hazardMat;
                for (int s = -1; s <= 1; s += 2)
                {
                    Prim(PrimitiveType.Cube, root, new Vector3(x - 8f, 16f, s * 50f), 0f, new Vector3(2f, 32f, 2f), steel);
                    Prim(PrimitiveType.Cube, root, new Vector3(x + 8f, 16f, s * 50f), 0f, new Vector3(2f, 32f, 2f), steel);
                    Prim(PrimitiveType.Cube, root, new Vector3(x, 31f, s * 50f), 0f, new Vector3(18f, 2f, 2.4f), steel);
                    Prim(PrimitiveType.Cube, root, new Vector3(x, 0.6f, s * 50f), 0f, new Vector3(20f, 1.2f, 3f), A.buildingMat);
                }
                Prim(PrimitiveType.Cube, root, new Vector3(x, 33f, 0f), 0f, new Vector3(4f, 3f, 104f), A.buildingMat);
                Prim(PrimitiveType.Cube, root, new Vector3(x, 29f, 12f), 0f, new Vector3(6f, 4f, 6f), A.buildingMat);
                Prim(PrimitiveType.Cube, root, new Vector3(x, 25.5f, 12f), 0f, new Vector3(0.3f, 4f, 0.3f), A.buildingMat);
            }

            // Warehouses in two corners
            var houses = new (Vector3 p, Vector3 s)[]
            {
                (new Vector3(-100f, 11f, -100f), new Vector3(34f, 22f, 30f)),
                (new Vector3(100f, 11f, 100f), new Vector3(34f, 22f, 30f)),
            };
            int hs = 11;
            foreach (var h in houses)
            {
                // Door side faces the middle of the yard.
                float yaw = h.p.z < 0f ? 180f : 0f;
                Prim(PrimitiveType.Cube, root, h.p, yaw, h.s, A.buildingMat);
                BuildingKit.Dress(root, h.p, yaw, h.s, BuildingKit.Style.Industrial, hs++);
            }
            // Harbour offices in the other two corners.
            foreach (var (p, yaw) in new[] { (new Vector3(-110f, 0f, 112f), 0f), (new Vector3(110f, 0f, -112f), 180f) })
            {
                var size = new Vector3(18f, 16f, 14f);
                Prim(PrimitiveType.Cube, root, p + Vector3.up * size.y * 0.5f, yaw, size, A.buildingMat);
                BuildingKit.Dress(root, p, yaw, size, BuildingKit.Style.Office, hs++);
            }

            // Bollards around the capture points
            foreach (float x in new[] { -80f, 0f, 80f })
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 60f * Mathf.Deg2Rad + 0.5f;
                    Prim(PrimitiveType.Cylinder, root, new Vector3(x + Mathf.Cos(a) * 24f, 1f, Mathf.Sin(a) * 24f), 0f, new Vector3(1.2f, 1f, 1.2f), A.hazardMat);
                }

            Lighting(new Color(0.92f, 0.94f, 1f), 1.2f, Quaternion.Euler(40f, -30f, 0f),
                new Color(0.47f, 0.51f, 0.57f), 100f, 650f,
                new Color(0.5f, 0.55f, 0.63f), new Color(0.44f, 0.45f, 0.47f));
        }

        // ---------------------------------------------------------------------
        // CANYON OUTPOST
        // ---------------------------------------------------------------------

        static void BuildCanyon(Transform root)
        {
            var sand = A.Tinted(A.floorMat, new Color(0.86f, 0.7f, 0.52f));
            var rock = A.Tinted(A.wallMat, new Color(0.62f, 0.44f, 0.33f));
            var rockDark = A.Tinted(A.wallMat, new Color(0.45f, 0.32f, 0.25f));
            var rnd = new System.Random(7);
            // Mesas: clusters of big tilted rocks.
            var mesas = new (Vector3 p, float size)[]
            {
                (new Vector3(-45f, 0f, -45f), 14f), (new Vector3(48f, 0f, -40f), 12f), (new Vector3(-40f, 0f, 48f), 13f),
                (new Vector3(45f, 0f, 50f), 15f), (new Vector3(-110f, 0f, -45f), 11f), (new Vector3(110f, 0f, 45f), 11f),
                (new Vector3(-108f, 0f, 70f), 9f), (new Vector3(108f, 0f, -75f), 9f),
            };
            foreach (var m in mesas)
            {
                for (int k = 0; k < 4; k++)
                {
                    Vector3 off = new Vector3((float)(rnd.NextDouble() * 2 - 1), 0f, (float)(rnd.NextDouble() * 2 - 1)) * m.size * 0.5f;
                    float h = m.size * (0.8f + (float)rnd.NextDouble() * 0.9f);
                    Prim(PrimitiveType.Cube, root, m.p + off + Vector3.up * h * 0.45f, (float)rnd.NextDouble() * 360f,
                        new Vector3(m.size * (0.7f + (float)rnd.NextDouble() * 0.5f), h, m.size * (0.6f + (float)rnd.NextDouble() * 0.5f)),
                        k % 2 == 0 ? rock : rockDark, (float)(rnd.NextDouble() * 12 - 6), (float)(rnd.NextDouble() * 12 - 6));
                }
            }

            // Boulders
            for (int k = 0; k < 26; k++)
            {
                Vector3 p = new Vector3((float)(rnd.NextDouble() * 2 - 1) * 120f, 0f, (float)(rnd.NextDouble() * 2 - 1) * 120f);
                bool nearZone = Mathf.Abs(p.z) < 26f && (Mathf.Abs(p.x) < 26f || Mathf.Abs(Mathf.Abs(p.x) - 80f) < 26f);
                bool nearBase = Mathf.Abs(Mathf.Abs(p.z) - 104f) < 16f && Mathf.Abs(p.x) < 90f;
                if (nearZone || nearBase) continue;
                float s = 2f + (float)rnd.NextDouble() * 4f;
                Prim(PrimitiveType.Sphere, root, p + Vector3.up * s * 0.3f, (float)rnd.NextDouble() * 360f, new Vector3(s * 1.3f, s, s * 1.1f), rockDark);
            }

            // Outpost: bunkers with sandbags and a comm mast in the middle.
            var bunkers = new[] { new Vector3(-22f, 0f, -70f), new Vector3(24f, 0f, -72f), new Vector3(-24f, 0f, 70f), new Vector3(22f, 0f, 72f) };
            int bs = 31;
            foreach (var b in bunkers)
            {
                float yaw = b.z < 0f ? 0f : 180f;
                Prim(PrimitiveType.Cube, root, b + Vector3.up * 3f, 0f, new Vector3(12f, 6f, 8f), A.buildingMat);
                BuildingKit.Dress(root, b, yaw, new Vector3(12f, 6f, 8f), BuildingKit.Style.Bunker, bs++);
                for (int k = -2; k <= 2; k++)
                    Prim(PrimitiveType.Cylinder, root, b + new Vector3(k * 2.6f, 0.6f, Mathf.Sign(-b.z) * 7f), 90f, new Vector3(1.2f, 1.3f, 1.2f), rockDark, 0f, 90f);
            }
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 1f, 0f), 45f, new Vector3(8f, 2f, 8f), A.buildingMat);
            Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 14f, 0f), 0f, new Vector3(1.6f, 13f, 1.6f), A.hazardMat);
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 26f, 0f), 0f, new Vector3(6f, 0.5f, 0.5f), A.buildingMat);
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 22f, 0f), 90f, new Vector3(5f, 0.5f, 0.5f), A.buildingMat);
            Prim(PrimitiveType.Sphere, root, new Vector3(0f, 27.4f, 0f), 0f, Vector3.one * 1.2f, A.glowMat);

            // Ramps of packed earth
            foreach (var (p, yaw) in new[] { (new Vector3(-80f, 0f, -45f), 0f), (new Vector3(80f, 0f, 45f), 180f) })
                Prim(PrimitiveType.Cube, root, p + Vector3.up * 1.6f, yaw, new Vector3(12f, 1f, 18f), sand, -12f);

            Lighting(new Color(1f, 0.84f, 0.64f), 1.45f, Quaternion.Euler(32f, -60f, 0f),
                new Color(0.7f, 0.58f, 0.47f), 90f, 620f,
                new Color(0.58f, 0.55f, 0.55f), new Color(0.55f, 0.46f, 0.38f));
        }

        static void Lighting(Color sunColor, float sunIntensity, Quaternion sunRot, Color fog, float fogStart, float fogEnd, Color ambientSky, Color ambientEquator)
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.transform.rotation = sunRot;
            }
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
        }
    }
}
