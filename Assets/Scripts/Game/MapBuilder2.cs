using UnityEngine;

namespace RobotArena
{
    /// <summary>The newer battle maps: Frozen Pass, Ruined City and Refinery.</summary>
    public static partial class MapBuilder
    {
        // ---------------------------------------------------------------------
        // FROZEN PASS: snowbound research base between ice ridges
        // ---------------------------------------------------------------------

        static void BuildFrozenPass(Transform root, MapLayout l)
        {
            var a = A;
            var ice = a.Tinted(a.wallMat, new Color(0.78f, 0.86f, 0.95f));
            var iceDark = a.Tinted(a.wallMat, new Color(0.55f, 0.64f, 0.76f));
            var snow = a.Tinted(a.floorMat, new Color(0.93f, 0.95f, 0.98f));
            var rnd = new System.Random(41);
            float R() => (float)rnd.NextDouble();

            // Ice ridges: long tilted slabs that split the pass into lanes.
            var ridges = new (Vector3 p, float yaw, float len)[]
            {
                (new Vector3(-60f, 0f, -60f), 20f, 60f), (new Vector3(60f, 0f, 60f), 20f, 60f),
                (new Vector3(-150f, 0f, 40f), -10f, 50f), (new Vector3(150f, 0f, -40f), -10f, 50f),
                (new Vector3(55f, 0f, -110f), 70f, 40f), (new Vector3(-55f, 0f, 110f), 70f, 40f),
            };
            foreach (var r in ridges)
            {
                if (!Frontier.Clear(r.p, l, 26f)) continue;
                for (int k = 0; k < 5; k++)
                {
                    Vector3 off = Quaternion.Euler(0f, r.yaw, 0f) * new Vector3((k - 2f) * r.len * 0.2f, 0f, R() * 6f - 3f);
                    float h = 8f + R() * 12f;
                    Prim(PrimitiveType.Cube, root, r.p + off + Vector3.up * h * 0.4f, r.yaw + R() * 20f - 10f,
                        new Vector3(r.len * 0.26f, h, 7f + R() * 5f), k % 2 == 0 ? ice : iceDark, R() * 14f - 7f, R() * 14f - 7f);
                }
            }

            // Research base: radar domes, labs and a comms mast in the middle.
            var domes = new[] { new Vector3(-35f, 0f, 20f), new Vector3(38f, 0f, -18f), new Vector3(-120f, 0f, -120f), new Vector3(120f, 0f, 120f) };
            foreach (var d in domes)
            {
                if (!Frontier.Clear(d, l, 22f)) continue;
                Prim(PrimitiveType.Cylinder, root, d + Vector3.up * 3f, 0f, new Vector3(18f, 3f, 18f), a.buildingMat);
                Prim(PrimitiveType.Sphere, root, d + Vector3.up * 6f, 0f, new Vector3(16f, 16f, 16f), a.Paint(new Color(0.88f, 0.9f, 0.92f), 0.1f, 0.4f, true));
                Prim(PrimitiveType.Cube, root, d + new Vector3(0f, 1.5f, -9.2f), 0f, new Vector3(4f, 3f, 1f), a.hazardMat);
            }
            int seed = 70;
            var labs = new (Vector3 p, float yaw, Vector3 s)[]
            {
                (new Vector3(-90f, 0f, -20f), 0f, new Vector3(26f, 9f, 14f)), (new Vector3(92f, 0f, 24f), 180f, new Vector3(26f, 9f, 14f)),
                (new Vector3(0f, 0f, -70f), 0f, new Vector3(20f, 7f, 12f)), (new Vector3(0f, 0f, 72f), 180f, new Vector3(20f, 7f, 12f)),
                (new Vector3(-170f, 0f, -90f), 90f, new Vector3(18f, 12f, 16f)), (new Vector3(170f, 0f, 90f), -90f, new Vector3(18f, 12f, 16f)),
            };
            foreach (var b in labs)
            {
                if (!Frontier.Clear(b.p, l, 20f)) continue;
                Prim(PrimitiveType.Cube, root, b.p + Vector3.up * b.s.y * 0.5f, b.yaw, b.s, a.buildingMat);
                BuildingKit.Dress(root, b.p, b.yaw, b.s, BuildingKit.Style.Industrial, seed++);
                // Snow on the roof
                Prim(PrimitiveType.Cube, root, b.p + Vector3.up * (b.s.y + 0.4f), b.yaw, new Vector3(b.s.x - 0.6f, 0.5f, b.s.z - 0.6f), snow);
            }
            Prim(PrimitiveType.Cylinder, root, new Vector3(-12f, 18f, -12f), 0f, new Vector3(1.4f, 18f, 1.4f), a.hazardMat);
            Prim(PrimitiveType.Sphere, root, new Vector3(-12f, 36.6f, -12f), 0f, Vector3.one * 1.3f, a.glowMat);

            // Fuel bladders, snow drifts and boulders for cover.
            for (int k = 0; k < 40; k++)
            {
                Vector3 p = new Vector3((R() * 2f - 1f) * 215f, 0f, (R() * 2f - 1f) * 215f);
                if (!Frontier.Clear(p, l, 26f)) continue;
                float s = 3f + R() * 5f;
                if (k % 3 == 0)
                    Prim(PrimitiveType.Sphere, root, p + Vector3.up * s * 0.15f, R() * 360f, new Vector3(s * 3f, s * 0.8f, s * 2f), snow);
                else
                    Prim(PrimitiveType.Sphere, root, p + Vector3.up * s * 0.3f, R() * 360f, new Vector3(s * 1.3f, s, s * 1.1f), k % 2 == 0 ? ice : iceDark);
            }

            Lighting(new Color(0.9f, 0.95f, 1f), 1.25f, Quaternion.Euler(24f, -120f, 0f),
                new Color(0.78f, 0.83f, 0.9f), 120f, 900f,
                new Color(0.62f, 0.68f, 0.78f), new Color(0.6f, 0.63f, 0.68f));
        }

        // ---------------------------------------------------------------------
        // RUINED CITY: bombed-out downtown with streets between broken towers
        // ---------------------------------------------------------------------

        static void BuildRuinedCity(Transform root, MapLayout l)
        {
            var a = A;
            var rubbleMat = a.Tinted(a.wallMat, new Color(0.55f, 0.53f, 0.5f));
            var burnt = a.Paint(new Color(0.12f, 0.11f, 0.1f), 0.3f, 0.2f, true);
            var rnd = new System.Random(53);
            float R() => (float)rnd.NextDouble();
            int seed = 120;

            // City blocks on a grid, with 40 m avenues between them.
            for (float x = -175f; x <= 175f; x += 70f)
            for (float z = -140f; z <= 140f; z += 70f)
            {
                Vector3 p = new Vector3(x + R() * 8f - 4f, 0f, z + R() * 8f - 4f);
                if (!Frontier.Clear(p, l, 32f)) continue;
                if (R() < 0.18f) continue; // a cleared lot
                var size = new Vector3(22f + R() * 10f, 10f + R() * 34f, 22f + R() * 10f);
                float yaw = Mathf.Round(R() * 3f) * 90f;
                Prim(PrimitiveType.Cube, root, p + Vector3.up * size.y * 0.5f, yaw, size, a.buildingMat);
                BuildingKit.Dress(root, p, yaw, size, size.y > 18f ? BuildingKit.Style.Office : BuildingKit.Style.Industrial, seed++);
                // Shell damage: a collapsed corner slab and a rubble heap at the foot.
                if (R() < 0.55f)
                {
                    Vector3 corner = Quaternion.Euler(0f, yaw, 0f) * new Vector3(size.x * 0.45f, 0f, size.z * 0.45f);
                    Prim(PrimitiveType.Cube, root, p + corner + Vector3.up * 2.5f, yaw + R() * 40f, new Vector3(size.x * 0.4f, 5f, size.z * 0.3f), a.buildingMat, R() * 30f - 15f, R() * 30f - 15f);
                    for (int k = 0; k < 4; k++)
                        Prim(PrimitiveType.Cube, root, p + corner * 1.25f + new Vector3(R() * 6f - 3f, 0.8f, R() * 6f - 3f), R() * 360f,
                            new Vector3(2f + R() * 3f, 1.5f + R() * 1.5f, 2f + R() * 3f), rubbleMat, R() * 30f, R() * 30f);
                }
            }

            // Burnt-out vehicles and barricades in the streets.
            for (int k = 0; k < 30; k++)
            {
                Vector3 p = new Vector3((R() * 2f - 1f) * 210f, 0f, (R() * 2f - 1f) * 210f);
                if (!Frontier.Clear(p, l, 24f)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 2f, 4f)) continue;
                float yaw = R() * 360f;
                if (k % 2 == 0)
                {
                    // Bus or truck wreck
                    Prim(PrimitiveType.Cube, root, p + Vector3.up * 1.6f, yaw, new Vector3(3f, 3f, 10f), burnt, 0f, R() * 10f - 5f);
                    Prim(PrimitiveType.Cube, root, p + Vector3.up * 3.3f, yaw, new Vector3(2.6f, 0.4f, 8f), rubbleMat);
                }
                else
                {
                    // Concrete barricade line
                    for (int i = -2; i <= 2; i++)
                        Prim(PrimitiveType.Cube, root, p + Quaternion.Euler(0f, yaw, 0f) * new Vector3(i * 2.4f, 0.9f, 0f), yaw, new Vector3(2.2f, 1.8f, 1f), a.wallMat);
                }
            }

            Lighting(new Color(1f, 0.78f, 0.58f), 1.2f, Quaternion.Euler(18f, -40f, 0f),
                new Color(0.5f, 0.45f, 0.42f), 90f, 800f,
                new Color(0.5f, 0.5f, 0.55f), new Color(0.48f, 0.42f, 0.38f));
        }

        // ---------------------------------------------------------------------
        // REFINERY: storage tanks, pipe racks and flare stacks at dusk
        // ---------------------------------------------------------------------

        static void BuildRefinery(Transform root, MapLayout l)
        {
            var a = A;
            var tankMat = a.Paint(new Color(0.7f, 0.7f, 0.68f), 0.5f, 0.35f, true);
            var rust = a.Paint(new Color(0.42f, 0.24f, 0.14f), 0.4f, 0.25f, true);
            var pipe = a.Paint(new Color(0.35f, 0.36f, 0.37f), 0.8f, 0.4f, false);
            var flame = a.Glow(new Color(1f, 0.55f, 0.15f), 4f);
            var rnd = new System.Random(67);
            float R() => (float)rnd.NextDouble();

            // Tank farms: clusters of big storage tanks with bund walls.
            var farms = new[] { new Vector3(-130f, 0f, -60f), new Vector3(130f, 0f, 60f), new Vector3(-120f, 0f, 120f), new Vector3(120f, 0f, -120f), new Vector3(-40f, 0f, 60f), new Vector3(40f, 0f, -60f) };
            foreach (var f in farms)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = f + new Vector3((k - 1f) * 22f, 0f, R() * 10f - 5f);
                    if (!Frontier.Clear(p, l, 22f)) continue;
                    float r = 8f + R() * 3f, h = 10f + R() * 8f;
                    Prim(PrimitiveType.Cylinder, root, p + Vector3.up * h * 0.5f, 0f, new Vector3(r * 2f, h * 0.5f, r * 2f), k == 1 ? rust : tankMat);
                    Prim(PrimitiveType.Cylinder, root, p + Vector3.up * (h + 0.3f), 0f, new Vector3(r * 2.02f, 0.3f, r * 2.02f), pipe);
                    // Spiral stair hint: a ladder strip up the side
                    Prim(PrimitiveType.Cube, root, p + new Vector3(r + 0.2f, h * 0.5f, 0f), 0f, new Vector3(0.4f, h, 1.2f), a.hazardMat);
                }
            }

            // Pipe racks: long steel bridges carrying pipes across the site.
            var racks = new (Vector3 a, Vector3 b)[]
            {
                (new Vector3(-190f, 0f, 0f), new Vector3(-50f, 0f, 0f)), (new Vector3(50f, 0f, 0f), new Vector3(190f, 0f, 0f)),
                (new Vector3(0f, 0f, -150f), new Vector3(0f, 0f, -40f)), (new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 150f)),
            };
            foreach (var r in racks)
            {
                Vector3 dir = (r.b - r.a);
                float len = dir.magnitude;
                float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
                Vector3 mid = (r.a + r.b) * 0.5f;
                // Raised high enough for robots to walk under.
                for (float t = 0f; t <= len; t += 18f)
                {
                    Vector3 p = r.a + dir.normalized * t;
                    if (!Frontier.Clear(p, l, 14f)) continue;
                    for (int s = -1; s <= 1; s += 2)
                        Prim(PrimitiveType.Cube, root, p + Quaternion.Euler(0f, yaw, 0f) * new Vector3(s * 3f, 9f, 0f), yaw, new Vector3(0.8f, 18f, 0.8f), pipe);
                }
                for (int i = 0; i < 4; i++)
                    Prim(PrimitiveType.Cylinder, root, mid + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-2.2f + i * 1.45f, 18.6f, 0f), yaw, new Vector3(1.2f, len * 0.5f, 1.2f), i == 1 ? rust : pipe, 90f);
                Prim(PrimitiveType.Cube, root, mid + Vector3.up * 17.8f, yaw, new Vector3(7f, 0.4f, len), pipe);
            }

            // Flare stacks with burning tips, and a cracking tower in the middle.
            foreach (var p in new[] { new Vector3(-175f, 0f, -170f), new Vector3(175f, 0f, 170f), new Vector3(-180f, 0f, 160f) })
            {
                Prim(PrimitiveType.Cylinder, root, p + Vector3.up * 30f, 0f, new Vector3(2.4f, 30f, 2.4f), pipe);
                Prim(PrimitiveType.Sphere, root, p + Vector3.up * 62f, 0f, new Vector3(5f, 8f, 5f), flame);
            }
            Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 22f, 0f), 0f, new Vector3(8f, 22f, 8f), tankMat);
            for (int b = 1; b <= 5; b++)
                Prim(PrimitiveType.Cylinder, root, new Vector3(0f, b * 8f, 0f), 0f, new Vector3(10f, 0.5f, 10f), a.hazardMat);
            Prim(PrimitiveType.Sphere, root, new Vector3(0f, 45f, 0f), 0f, Vector3.one * 1.6f, a.glowMat);

            // Cover: drum stacks and skid-mounted pumps.
            for (int k = 0; k < 34; k++)
            {
                Vector3 p = new Vector3((R() * 2f - 1f) * 210f, 0f, (R() * 2f - 1f) * 210f);
                if (!Frontier.Clear(p, l, 24f)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 2f, 5f)) continue;
                float yaw = R() * 360f;
                Prim(PrimitiveType.Cube, root, p + Vector3.up * 0.4f, yaw, new Vector3(8f, 0.8f, 5f), pipe);
                Prim(PrimitiveType.Cube, root, p + Vector3.up * 2.4f, yaw, new Vector3(6f, 3.2f, 3.6f), k % 2 == 0 ? rust : tankMat);
            }

            Lighting(new Color(1f, 0.62f, 0.4f), 1.1f, Quaternion.Euler(10f, -70f, 0f),
                new Color(0.55f, 0.42f, 0.36f), 90f, 850f,
                new Color(0.45f, 0.42f, 0.5f), new Color(0.55f, 0.4f, 0.32f));
        }
    }
}
