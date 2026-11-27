using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The edge of every battlefield. Instead of a concrete wall around a small yard,
    /// the fight happens in a big open area hemmed in by things that are actually that
    /// big: a skyline of ruined towers and cooling towers in the city maps, and real
    /// mountain ranges (one generated mesh) on the horizon. Invisible blockers keep the
    /// robots inside; they sit just in front of the buildings and slopes, so the edge
    /// always looks like terrain, never like a wall.
    /// </summary>
    public static class Frontier
    {
        /// <summary>Half the size of the playable area, in metres.</summary>
        public const float Half = 520f;

        static GameAssets A => GameAssets.Get();

        public static void Build(MapId map, MapLayout layout)
        {
            var a = A;
            if (a == null) return;
            RemoveOldEdge();

            var root = new GameObject("Frontier").transform;
            var rnd = new System.Random(11 + (int)map * 13);
            BattleGround.Reset();

            Material ground = GroundMat(map);
            // Plain ground far out; the sculpted BattleGround mesh sits just above it near the fight.
            Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.56f, 0f), 0f, new Vector3(2000f, 1f, 2000f), ground);

            if (map == MapId.Canyon)
            {
                // Canyon walls rise straight from the edge of the battlefield.
                Mountains(root, map, RockMat(new Color(0.6f, 0.43f, 0.32f)), Half - 4f, 70f, 120f, 150f, 0.75f, 5);
            }
            else if (map == MapId.FrozenPass)
            {
                // Snow-covered ridges close in on the pass.
                Mountains(root, map, RockMat(new Color(0.86f, 0.9f, 0.95f)), Half - 4f, 90f, 140f, 230f, 0.7f, 3);
            }
            else
            {
                Skyline(root, map, rnd);
                // Distant range behind the city, grey-blue in the haze.
                Mountains(root, map, RockMat(new Color(0.48f, 0.47f, 0.46f)), Half + 95f, 260f, 150f, 280f, 0.5f, 9);
            }
            // Roads, towns and base defences are planned first: the ground stays flat under them.
            var detail = WorldDetail.Plan(map, layout);
            WorldDetail.BuildBases(root, map, layout);
            Outskirts(root, map, layout, rnd);
            Blockers(root);

            StaticBatchingUtility.Combine(root.gameObject);

            // Sculpted, photo-textured ground. Built after everything with a collider so it can stay flat under them.
            var groundRoot = new GameObject("Ground").transform;
            if (!BattleGround.Build(groundRoot, map, layout)) Object.Destroy(groundRoot.gameObject);

            // Real-size towns, roads, cars, trees and power lines, set on the finished ground.
            WorldDetail.Build(detail);

            // See the horizon: haze starts further out so the mountains read as far away and huge.
            RenderSettings.fogStartDistance = 220f;
            RenderSettings.fogEndDistance = 1700f;
            var cam = Camera.main;
            if (cam != null) cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2400f);
        }

        /// <summary>The Foundry's old concrete walls and small floor (baked in the scene).</summary>
        static void RemoveOldEdge()
        {
            var arena = GameObject.Find("Arena");
            if (arena == null) return;
            var doomed = new List<GameObject>();
            foreach (Transform t in arena.transform)
                if (t.name == "Wall" || t.name == "WallTrim" || t.name == "WallLamp" || t.name == "Buttress" || t.name == "Floor")
                    doomed.Add(t.gameObject);
            foreach (var g in doomed) { g.SetActive(false); Object.Destroy(g); }
        }

        public static Material GroundMat(MapId map)
        {
            var a = A;
            switch (map)
            {
                case MapId.Canyon: return a.Tinted(a.floorMat, new Color(0.86f, 0.7f, 0.52f));
                case MapId.FrozenPass: return a.Tinted(a.floorMat, new Color(0.9f, 0.93f, 0.97f));
                case MapId.RuinedCity: return a.Tinted(a.floorMat, new Color(0.6f, 0.59f, 0.57f));
                case MapId.Refinery: return a.Tinted(a.floorMat, new Color(0.62f, 0.58f, 0.52f));
                default: return a.floorMat;
            }
        }

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

        static Material RockMat(Color c)
        {
            var m = new Material(A.wallMat) { name = "Mountain", color = c };
            // Mountains are hundreds of metres across: coarser texture so it doesn't look tiled.
            if (m.HasProperty("_Tiling")) m.SetFloat("_Tiling", 36f);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1.6f);
            return m;
        }

        // ---------------------------------------------------------------------
        // Mountains: one height-field mesh around the battlefield
        // ---------------------------------------------------------------------

        /// <summary>
        /// A ring of terrain that starts at <paramref name="start"/> (distance from the
        /// centre, squared-off with rounded corners), climbs over <paramref name="rise"/>
        /// metres to peaks between minH and maxH. steep (0..1) sharpens the first slope.
        /// </summary>
        static void Mountains(Transform root, MapId map, Material mat, float start, float rise, float minH, float maxH, float steep, int seed)
        {
            const float L = 1400f;
            const int N = 140;
            float cell = L * 2f / N;
            var verts = new Vector3[(N + 1) * (N + 1)];
            var heights = new float[verts.Length];
            float ox = seed * 17.3f, oz = seed * 9.1f;
            for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
            {
                float x = -L + i * cell, z = -L + j * cell;
                float d = EdgeDistance(x, z);
                // Uneven foot line so the range doesn't follow a ruler.
                float wobble = (Mathf.PerlinNoise(x * 0.004f + ox, z * 0.004f + oz) - 0.5f) * 40f;
                float t = Mathf.Clamp01((d - start - wobble) / rise);
                float lift = 1f - Mathf.Pow(1f - t, 1f + steep * 2f);
                float peaks = Mathf.Lerp(minH, maxH, Fbm(x * 0.0025f + ox, z * 0.0025f + oz));
                float ridges = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.011f + oz, z * 0.011f + ox) * 2f - 1f);
                float h = lift * peaks * (0.75f + 0.25f * ridges);
                // Rocky detail on the slopes.
                h += lift * (Mathf.PerlinNoise(x * 0.05f, z * 0.05f) - 0.5f) * 10f;
                heights[j * (N + 1) + i] = h;
                verts[j * (N + 1) + i] = new Vector3(x, h - 0.5f, z);
            }
            var tris = new List<int>();
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int a = j * (N + 1) + i, b = a + 1, c = a + N + 1, e = c + 1;
                // Flat cells inside the battlefield are just ground: leave them out.
                if (heights[a] <= 0.01f && heights[b] <= 0.01f && heights[c] <= 0.01f && heights[e] <= 0.01f) continue;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(e);
            }
            var mesh = new Mesh { name = "Mountains" };
            mesh.vertices = verts;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var terrain = BattleGround.Mat(map, true);
            if (terrain != null) { BattleGround.PaintSlopes(mesh, map); mat = terrain; }
            var go = new GameObject("Mountains");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Distance from the centre on a square with rounded corners.</summary>
        static float EdgeDistance(float x, float z)
        {
            float cheb = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float round = Mathf.Sqrt(x * x + z * z) * 0.86f;
            return Mathf.Max(cheb, round);
        }

        static float Fbm(float x, float z)
        {
            float v = Mathf.PerlinNoise(x, z) * 0.6f + Mathf.PerlinNoise(x * 2.1f + 5f, z * 2.1f + 3f) * 0.3f + Mathf.PerlinNoise(x * 4.3f + 9f, z * 4.3f + 1f) * 0.1f;
            return Mathf.Clamp01((v - 0.25f) / 0.55f);
        }

        // ---------------------------------------------------------------------
        // Skyline: giant buildings along the edge of the city maps
        // ---------------------------------------------------------------------

        static void Skyline(Transform root, MapId map, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            var a = A;
            var concrete = a.Tinted(a.wallMat, new Color(0.62f, 0.61f, 0.6f));
            int seed = 200;
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                float u = -Half - 40f;
                while (u < Half + 40f)
                {
                    float w = 24f + R() * 18f;
                    float depth = 22f + R() * 16f;
                    float gap = 3f + R() * 5f;
                    Vector3 c = r * new Vector3(u + w * 0.5f, 0f, Half + 8f + depth * 0.5f + R() * 10f);
                    float roll = R();
                    bool industrial = map == MapId.Foundry || map == MapId.Refinery;
                    if (industrial && roll < (map == MapId.Refinery ? 0.3f : 0.18f))
                        CoolingTower(root, c, concrete, R());
                    else if (industrial && roll < (map == MapId.Refinery ? 0.5f : 0.3f))
                        Smokestacks(root, c, concrete, rnd);
                    else
                    {
                        // Towers: tallest at the middle of each side, some broken off.
                        float centre = 1f - Mathf.Abs(u) / (Half + 40f);
                        float h = Mathf.Lerp(40f, 90f, R()) + centre * 50f;
                        var size = new Vector3(w, h, depth);
                        Prim(PrimitiveType.Cube, root, c + Vector3.up * h * 0.5f, yaw, size, a.buildingMat);
                        BuildingKit.Dress(root, c, yaw, size, h > 30f ? BuildingKit.Style.Office : BuildingKit.Style.Industrial, seed++);
                        if (R() < 0.3f)
                        {
                            // Bombed-out top: a tilted slab of the upper floors.
                            Prim(PrimitiveType.Cube, root, c + Vector3.up * (h + 4f), yaw + R() * 20f - 10f, new Vector3(w * 0.7f, 8f, depth * 0.7f), a.buildingMat, R() * 16f - 8f, R() * 16f - 8f);
                        }
                    }
                    u += w + gap;
                }
            }

            // A second, taller row further back gives the city depth.
            for (int k = 0; k < 40; k++)
            {
                float ang = k * (360f / 40f) + R() * 4f;
                float dist = Half + 95f + R() * 60f;
                Vector3 c = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0f, dist);
                float h = 90f + R() * 120f;
                var size = new Vector3(30f + R() * 20f, h, 30f + R() * 20f);
                Prim(PrimitiveType.Cube, root, c + Vector3.up * h * 0.5f, ang, size, a.buildingMat);
                BuildingKit.Dress(root, c, ang, size, BuildingKit.Style.Office, seed++);
            }
        }

        static void CoolingTower(Transform root, Vector3 c, Material concrete, float r01)
        {
            float h = 60f + r01 * 25f;
            // Waisted hyperboloid shape from stacked rings.
            for (int i = 0; i < 6; i++)
            {
                float t = (i + 0.5f) / 6f;
                float radius = Mathf.Lerp(24f, 15f, Mathf.Sin(t * Mathf.PI * 0.85f)) * (t > 0.8f ? 1.06f : 1f);
                Prim(PrimitiveType.Cylinder, root, c + Vector3.up * (t * h), 0f, new Vector3(radius * 2f, h / 12f, radius * 2f), concrete);
            }
            Prim(PrimitiveType.Cylinder, root, c + Vector3.up * (h + 0.6f), 0f, new Vector3(31f, 0.6f, 31f), A.hazardMat);
        }

        static void Smokestacks(Transform root, Vector3 c, Material concrete, System.Random rnd)
        {
            Prim(PrimitiveType.Cube, root, c + Vector3.up * 9f, 0f, new Vector3(30f, 18f, 24f), A.buildingMat);
            BuildingKit.Dress(root, c, 0f, new Vector3(30f, 18f, 24f), BuildingKit.Style.Industrial, rnd.Next(1000));
            for (int i = -1; i <= 1; i += 2)
            {
                float h = 70f + (float)rnd.NextDouble() * 40f;
                Prim(PrimitiveType.Cylinder, root, c + new Vector3(i * 8f, h * 0.5f, 4f), 0f, new Vector3(6f, h * 0.5f, 6f), concrete);
                for (int b = 1; b <= 3; b++)
                    Prim(PrimitiveType.Cylinder, root, c + new Vector3(i * 8f, h - b * 6f, 4f), 0f, new Vector3(6.4f, 0.8f, 6.4f), b == 1 ? A.hazardMat : concrete);
                Prim(PrimitiveType.Sphere, root, c + new Vector3(i * 8f, h + 1f, 4f), 0f, Vector3.one * 1.4f, A.glowMat);
            }
        }

        // ---------------------------------------------------------------------
        // Outskirts: cover in the new outer part of the battlefield
        // ---------------------------------------------------------------------

        static void Outskirts(Transform root, MapId map, MapLayout layout, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            var a = A;
            var rock = a.Tinted(a.wallMat, new Color(0.6f, 0.44f, 0.33f));
            var rockDark = a.Tinted(a.wallMat, new Color(0.44f, 0.32f, 0.25f));
            int placed = 0, tries = 0, seed = 600;
            while (placed < 34 && tries++ < 600)
            {
                Vector3 p = new Vector3((R() * 2f - 1f) * (Half - 18f), 0f, (R() * 2f - 1f) * (Half - 18f));
                // Only the new outer band: the old centre already has its cover.
                if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) < 150f) continue;
                if (!Clear(p, layout, 34f)) continue;
                if (BattleGround.FlatMask(p.x, p.z, 1f) < 1f || BattleGround.FlatMask(p.x, p.z, 30f) < 0.5f) continue; // not on roads or in towns
                float yaw = R() * 360f;
                switch (map)
                {
                    case MapId.Canyon:
                    case MapId.FrozenPass:
                        var r1 = map == MapId.FrozenPass ? a.Tinted(a.wallMat, new Color(0.82f, 0.88f, 0.95f)) : rock;
                        var r2 = map == MapId.FrozenPass ? a.Tinted(a.wallMat, new Color(0.62f, 0.7f, 0.8f)) : rockDark;
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 off = new Vector3(R() * 2f - 1f, 0f, R() * 2f - 1f) * 7f;
                            float h = 10f + R() * 16f;
                            Prim(PrimitiveType.Cube, root, p + off + Vector3.up * h * 0.45f, R() * 360f,
                                new Vector3(9f + R() * 7f, h, 8f + R() * 6f), k % 2 == 0 ? r1 : r2, R() * 12f - 6f, R() * 12f - 6f);
                        }
                        break;
                    case MapId.Dockyard:
                        if (R() < 0.6f) ContainerStack(root, p, yaw, 3 + rnd.Next(4), seed++);
                        else Ruin(root, p, yaw, rnd, seed++);
                        break;
                    default:
                        Ruin(root, p, yaw, rnd, seed++);
                        break;
                }
                placed++;
            }
        }

        public static bool Clear(Vector3 p, MapLayout l, float radius)
        {
            if (l == null) return true;
            foreach (var arr in new[] { l.allySpawns, l.enemySpawns, l.zones, l.depots, l.allyReserve, l.enemyReserve })
                if (arr != null)
                    foreach (var q in arr)
                        if ((q - p).sqrMagnitude < radius * radius) return false;
            return (l.reactor - p).sqrMagnitude > radius * radius;
        }

        /// <summary>A half-destroyed building: a big block with a broken upper slab.</summary>
        static void Ruin(Transform root, Vector3 p, float yaw, System.Random rnd, int seed)
        {
            float R() => (float)rnd.NextDouble();
            var size = new Vector3(14f + R() * 14f, 9f + R() * 14f, 12f + R() * 10f);
            Prim(PrimitiveType.Cube, root, p + Vector3.up * size.y * 0.5f, yaw, size, A.buildingMat);
            BuildingKit.Dress(root, p, yaw, size, size.y > 16f ? BuildingKit.Style.Office : BuildingKit.Style.Industrial, seed);
            if (R() < 0.6f)
                Prim(PrimitiveType.Cube, root, p + Quaternion.Euler(0f, yaw, 0f) * new Vector3(size.x * 0.55f, 2f, 0f), yaw + 15f,
                    new Vector3(size.x * 0.5f, 4f, size.z * 0.8f), A.buildingMat, 0f, 25f);
        }

        static void ContainerStack(Transform root, Vector3 p, float yaw, int count, int seed)
        {
            var mats = A.containerMats;
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < count; i++)
            {
                int layer = i / 2;
                float x = (i % 2 - 0.5f) * 2.9f;
                var mat = mats != null && mats.Length > 0 ? mats[(seed + i) % mats.Length] : A.buildingMat;
                Prim(PrimitiveType.Cube, root, p + r * new Vector3(x, 1.35f + layer * 2.7f, 0f), yaw, new Vector3(2.6f, 2.6f, 8f), mat);
            }
        }

        // ---------------------------------------------------------------------

        /// <summary>Invisible walls just inside the edge (robots can't climb out over the slopes).</summary>
        static void Blockers(Transform root)
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("EdgeBlocker");
                go.transform.SetParent(root, false);
                go.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                go.transform.localPosition = go.transform.localRotation * new Vector3(0f, 150f, Half + 6f);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(Half * 2f + 40f, 300f, 8f);
                go.layer = 2; // Ignore Raycast: shots and the camera pass, robots don't
            }
        }
    }
}
