using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The Highland Dam, Launch Site and Airbase maps. Their landmarks are Blender models
    /// (Tools/mechs/structures.py) placed with SceneryModels; the open ground between them
    /// gets cover from simple shapes. The roads, towns and countryside around them come
    /// from Frontier/WorldGen like every other map.
    /// </summary>
    public static partial class MapBuilder
    {
        /// <summary>Places a Struct&lt;name&gt; model. Falls back to a plain block when the build lacks it.</summary>
        static Transform Landmark(Transform root, string name, Vector3 pos, float yaw, Material[] mats, bool collide = true, float scale = 1f, Vector3? fallback = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (!SceneryModels.Place(t, "Struct" + name, mats, collide, scale))
            {
                Vector3 s = (fallback ?? new Vector3(10f, 8f, 10f)) * scale;
                Prim(PrimitiveType.Cube, t, new Vector3(0f, s.y * 0.5f, 0f), 0f, s, mats[0]);
            }
            return t;
        }

        static GameObject Deco(PrimitiveType type, Transform parent, Vector3 pos, float yaw, Vector3 scale, Material mat, float pitch = 0f, float roll = 0f)
        {
            var go = Prim(type, parent, pos, yaw, scale, mat, pitch, roll);
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A sagging cable from a to b, drawn as a chain of thin cylinders.</summary>
        static void Wire(Transform root, Vector3 a, Vector3 b, float sag, Material mat, float r = 0.12f, int segs = 8)
        {
            Vector3 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
                Vector3 d = p - prev;
                var go = Deco(PrimitiveType.Cylinder, root, (p + prev) * 0.5f, 0f, new Vector3(r, d.magnitude * 0.5f, r), mat);
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                prev = p;
            }
        }

        /// <summary>Concrete blocks, cable drums and crates: low cover scattered over open ground.</summary>
        static void Clutter(Transform root, MapLayout l, int count, int seed, Material concrete, Material drum, Material crate, float range = 220f)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            for (int k = 0; k < count; k++)
            {
                Vector3 p = new Vector3((R() * 2f - 1f) * range, 0f, (R() * 2f - 1f) * range);
                if (!Frontier.Clear(p, l, 22f)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 3f, 7f)) continue;
                float yaw = R() * 360f;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                switch (k % 3)
                {
                    case 0:
                        // A row of jersey barriers
                        for (int i = 0; i < 4; i++)
                            Prim(PrimitiveType.Cube, root, p + q * new Vector3((i - 1.5f) * 4.2f, 1.1f, 0f), yaw, new Vector3(4f, 2.2f, 1.4f), concrete);
                        break;
                    case 1:
                        // Cable drums on their sides
                        for (int i = 0; i < 3; i++)
                            Prim(PrimitiveType.Cylinder, root, p + q * new Vector3((i - 1f) * 3.4f, 1.6f, 0f), yaw, new Vector3(3.2f, 0.7f, 3.2f), drum, 0f, 90f);
                        break;
                    default:
                        Prim(PrimitiveType.Cube, root, p + Vector3.up * 1.6f, yaw, new Vector3(6f, 3.2f, 3.2f), crate);
                        Prim(PrimitiveType.Cube, root, p + q * new Vector3(1.2f, 4.1f, 0f), yaw + 8f, new Vector3(3.6f, 1.8f, 3f), crate);
                        break;
                }
            }
        }

        static Material[] Water(Material[] mats)
        {
            var m = (Material[])mats.Clone();
            m[4] = A.Paint(new Color(0.62f, 0.72f, 0.78f), 0.05f, 0.92f, false);
            return m;
        }

        // ---------------------------------------------------------------------
        // HIGHLAND DAM: a hydro station under a 95 m arch dam
        // ---------------------------------------------------------------------

        static void BuildHighlands(Transform root, MapLayout l)
        {
            var a = A;
            var concrete = SceneryModels.Palette(new Color(0.63f, 0.62f, 0.58f), new Color(0.52f, 0.52f, 0.5f), new Color(1f, 0.86f, 0.62f));
            var hall = SceneryModels.Palette(new Color(0.6f, 0.59f, 0.56f), new Color(0.4f, 0.5f, 0.56f), new Color(1f, 0.88f, 0.66f));
            var steel = SceneryModels.Palette(new Color(0.5f, 0.52f, 0.5f), new Color(0.58f, 0.58f, 0.55f), new Color(1f, 0.3f, 0.2f));
            var trafo = SceneryModels.Palette(new Color(0.5f, 0.52f, 0.5f), new Color(0.47f, 0.5f, 0.46f), new Color(0.5f, 0.9f, 1f));

            // The dam closes the valley beyond the east edge, its foot just outside the fence.
            Landmark(root, "Dam", new Vector3(Frontier.Half + 112f, 0f, 0f), -90f, Water(concrete), false, 0.85f, new Vector3(30f, 95f, 400f));

            // Two turbine halls fed by penstocks, one on each side of the valley floor.
            Landmark(root, "TurbineHall", new Vector3(110f, 0f, -78f), 0f, hall, true, 1f, new Vector3(64f, 22f, 26f));
            Landmark(root, "TurbineHall", new Vector3(-110f, 0f, 78f), 180f, hall, true, 1f, new Vector3(64f, 22f, 26f));

            // Switchyards: rows of transformers behind a low fence.
            foreach (var c in new[] { new Vector3(150f, 0f, 40f), new Vector3(-150f, 0f, -40f) })
            {
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                        Landmark(root, "Transformer", c + new Vector3((i - 1f) * 14f, 0f, (j - 0.5f) * 14f), 90f, trafo, true, 1f, new Vector3(9f, 6f, 6f));
                for (int s = -1; s <= 1; s += 2)
                {
                    Prim(PrimitiveType.Cube, root, c + new Vector3(0f, 0.9f, s * 16f), 0f, new Vector3(46f, 1.8f, 0.4f), a.Paint(new Color(0.4f, 0.42f, 0.42f), 0.7f, 0.4f, false));
                    Prim(PrimitiveType.Cube, root, c + new Vector3(s * 23f, 0.9f, 0f), 0f, new Vector3(0.4f, 1.8f, 32f), a.Paint(new Color(0.4f, 0.42f, 0.42f), 0.7f, 0.4f, false));
                }
            }

            // Power lines: two rows of pylons across the valley, with sagging conductors.
            var wire = a.Paint(new Color(0.1f, 0.1f, 0.11f), 0.6f, 0.4f, false);
            foreach (float z in new[] { -152f, 152f })
            {
                Vector3? prev = null;
                for (float x = -230f; x <= 231f; x += 115f)
                {
                    var p = new Vector3(x, 0f, z);
                    // Arms run across the line (along z), so the pylon turns 90 degrees.
                    Landmark(root, "Pylon", p, 90f, steel, true, 1f, new Vector3(10f, 44f, 10f));
                    if (prev.HasValue)
                        foreach (var off in new[] { new Vector3(0f, 21.4f, 11.15f), new Vector3(0f, 21.4f, -11.15f), new Vector3(0f, 29.4f, 8.83f), new Vector3(0f, 29.4f, -8.83f) })
                            Wire(root, prev.Value + off, p + off, 6f, wire, 0.14f, 10);
                    prev = p;
                }
            }

            Clutter(root, l, 30, 401, a.Paint(new Color(0.66f, 0.65f, 0.61f), 0.1f, 0.25f, true),
                a.Paint(new Color(0.42f, 0.3f, 0.18f), 0.1f, 0.3f, true), a.Paint(new Color(0.3f, 0.36f, 0.3f), 0.3f, 0.35f, true));

            // Cool morning light coming down the valley.
            Lighting(new Color(1f, 0.92f, 0.82f), 1.25f, Quaternion.Euler(28f, -120f, 0f),
                new Color(0.62f, 0.68f, 0.74f), 90f, 900f,
                new Color(0.55f, 0.62f, 0.7f), new Color(0.5f, 0.52f, 0.5f));
        }

        /// <summary>Clears the edge buildings that would stand in front of the dam.</summary>
        static void ClearDamView()
        {
            var frontier = GameObject.Find("Frontier");
            if (frontier == null) return;
            foreach (Transform t in frontier.transform)
            {
                Vector3 p = t.position;
                if (p.x > Frontier.Half + 4f && Mathf.Abs(p.z) < 230f && t.GetComponent<Renderer>() != null && t.name != "EdgeBlocker" && t.name != "Mountains")
                    t.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------------
        // LAUNCH SITE: a rocket on its pad, propellant spheres, tracking radars
        // ---------------------------------------------------------------------

        static void BuildLaunchSite(Transform root, MapLayout l)
        {
            var a = A;
            var rocket = SceneryModels.Palette(new Color(0.9f, 0.9f, 0.88f), new Color(0.16f, 0.17f, 0.19f), new Color(1f, 0.9f, 0.7f));
            var tank = SceneryModels.Palette(new Color(0.88f, 0.88f, 0.86f), new Color(0.72f, 0.22f, 0.16f), new Color(1f, 0.9f, 0.7f));
            var radar = SceneryModels.Palette(new Color(0.72f, 0.72f, 0.69f), new Color(0.86f, 0.86f, 0.84f), new Color(0.5f, 0.9f, 1f));
            var bunker = SceneryModels.Palette(new Color(0.62f, 0.61f, 0.57f), new Color(0.5f, 0.5f, 0.47f), new Color(1f, 0.8f, 0.5f));

            // The launch complex west of centre, its ramp facing the middle of the map.
            Landmark(root, "LaunchPad", new Vector3(-104f, 0f, 0f), 90f, rocket, true, 1f, new Vector3(46f, 90f, 46f));

            // Propellant farm east of centre: three spheres joined by pipe runs.
            var farm = new[] { new Vector3(92f, 0f, -34f), new Vector3(92f, 0f, 34f), new Vector3(132f, 0f, 0f) };
            float[] yaws = { 0f, 180f, 90f };
            for (int i = 0; i < farm.Length; i++)
                Landmark(root, "FuelSphere", farm[i], yaws[i], tank, true, 1f, new Vector3(16f, 18f, 16f));
            var pipe = a.Paint(new Color(0.55f, 0.56f, 0.56f), 0.8f, 0.45f, false);
            var sleeper = a.Paint(new Color(0.5f, 0.5f, 0.48f), 0.1f, 0.25f, true);
            void Run(Vector3 p0, Vector3 p1)
            {
                Vector3 d = p1 - p0;
                float yaw = Quaternion.LookRotation(d).eulerAngles.y;
                for (int k = 0; k < 2; k++)
                {
                    Vector3 o = Quaternion.Euler(0f, yaw, 0f) * new Vector3((k - 0.5f) * 1.4f, 1.2f, 0f);
                    Deco(PrimitiveType.Cylinder, root, (p0 + p1) * 0.5f + o, yaw, new Vector3(0.8f, d.magnitude * 0.5f, 0.8f), pipe, 90f);
                }
                // No colliders: robots wade through low pipework instead of tripping on it.
                for (float t = 4f; t < d.magnitude - 2f; t += 8f)
                    Deco(PrimitiveType.Cube, root, p0 + d.normalized * t + Vector3.up * 0.35f, yaw, new Vector3(3.4f, 0.7f, 0.8f), sleeper);
            }
            Run(new Vector3(92f, 0f, -21f), new Vector3(92f, 0f, 21f));
            Run(new Vector3(104f, 0f, 0f), new Vector3(119f, 0f, 0f));
            Run(new Vector3(-74f, 0f, -38f), new Vector3(80f, 0f, -38f));

            // Tracking radars and firing-room bunkers, placed point-symmetrically so both sides get the same cover.
            Landmark(root, "RadarDish", new Vector3(-120f, 0f, -118f), 30f, radar, true, 1f, new Vector3(12f, 20f, 12f));
            Landmark(root, "RadarDish", new Vector3(120f, 0f, 118f), 210f, radar, true, 1f, new Vector3(12f, 20f, 12f));
            Landmark(root, "Blockhouse", new Vector3(0f, 0f, -112f), 180f, bunker, true, 1f, new Vector3(24f, 8f, 16f));
            Landmark(root, "Blockhouse", new Vector3(0f, 0f, 112f), 0f, bunker, true, 1f, new Vector3(24f, 8f, 16f));
            Landmark(root, "Blockhouse", new Vector3(-180f, 0f, 80f), 90f, bunker, true, 1f, new Vector3(24f, 8f, 16f));
            Landmark(root, "Blockhouse", new Vector3(180f, 0f, -80f), 270f, bunker, true, 1f, new Vector3(24f, 8f, 16f));

            Clutter(root, l, 34, 517, a.Paint(new Color(0.7f, 0.69f, 0.65f), 0.1f, 0.25f, true),
                a.Paint(new Color(0.75f, 0.6f, 0.2f), 0.2f, 0.4f, true), a.Paint(new Color(0.72f, 0.72f, 0.7f), 0.4f, 0.4f, true));

            // Bright, hazy coastal afternoon.
            Lighting(new Color(1f, 0.95f, 0.86f), 1.35f, Quaternion.Euler(42f, -35f, 0f),
                new Color(0.72f, 0.77f, 0.82f), 100f, 950f,
                new Color(0.6f, 0.68f, 0.78f), new Color(0.58f, 0.56f, 0.52f));
        }

        // ---------------------------------------------------------------------
        // AIRBASE: a runway down the middle, arched hangars, a control tower
        // ---------------------------------------------------------------------

        static void BuildAirbase(Transform root, MapLayout l)
        {
            var a = A;
            var hangar = SceneryModels.Palette(new Color(0.46f, 0.5f, 0.46f), new Color(0.58f, 0.57f, 0.54f), new Color(1f, 0.9f, 0.72f));
            var tower = SceneryModels.Palette(new Color(0.8f, 0.8f, 0.77f), new Color(0.62f, 0.62f, 0.6f), new Color(0.6f, 0.95f, 1f));
            var radar = SceneryModels.Palette(new Color(0.72f, 0.72f, 0.69f), new Color(0.86f, 0.86f, 0.84f), new Color(0.5f, 0.9f, 1f));
            var jet = SceneryModels.Palette(new Color(0.5f, 0.54f, 0.58f), new Color(0.38f, 0.42f, 0.46f), new Color(0.4f, 1f, 0.5f));
            var tank = SceneryModels.Palette(new Color(0.86f, 0.86f, 0.84f), new Color(0.3f, 0.42f, 0.32f), new Color(1f, 0.9f, 0.7f));

            // Runway: a dark strip with painted markings and edge lights. Its thin collider keeps the ground flat under it.
            float len = 470f, wid = 46f;
            var asphalt = a.Paint(new Color(0.16f, 0.16f, 0.17f), 0.05f, 0.2f, true);
            var paint = a.Paint(new Color(0.9f, 0.9f, 0.88f), 0.0f, 0.3f, false);
            var amber = a.Glow(new Color(1f, 0.75f, 0.35f), 2.2f);
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.04f, 0f), 0f, new Vector3(len, 0.12f, wid), asphalt);
            for (float x = -len / 2 + 40f; x < len / 2 - 40f; x += 30f)
                Deco(PrimitiveType.Cube, root, new Vector3(x + 8f, 0.11f, 0f), 0f, new Vector3(16f, 0.02f, 0.9f), paint);
            for (int s = -1; s <= 1; s += 2)
            {
                Deco(PrimitiveType.Cube, root, new Vector3(0f, 0.11f, s * (wid / 2 - 1.2f)), 0f, new Vector3(len - 4f, 0.02f, 0.7f), paint);
                // Threshold "piano keys" and the aiming point bars at each end
                for (int k = 0; k < 8; k++)
                {
                    float z = (k - 3.5f) * 4.6f;
                    Deco(PrimitiveType.Cube, root, new Vector3(s * (len / 2 - 14f), 0.11f, z), 0f, new Vector3(20f, 0.02f, 1.8f), paint);
                }
                for (int k = -1; k <= 1; k += 2)
                    Deco(PrimitiveType.Cube, root, new Vector3(s * (len / 2 - 70f), 0.11f, k * 9f), 0f, new Vector3(30f, 0.02f, 4f), paint);
                for (float x = -len / 2 + 5f; x < len / 2; x += 20f)
                    Deco(PrimitiveType.Cube, root, new Vector3(x, 0.35f, s * (wid / 2 + 0.8f)), 0f, new Vector3(0.5f, 0.5f, 0.5f), amber);
            }
            // Taxiways from the runway up to the hangar aprons
            foreach (float x in new[] { -110f, 0f, 110f })
                Prim(PrimitiveType.Cube, root, new Vector3(x, 0.035f, 34f), 0f, new Vector3(24f, 0.1f, 26f), asphalt);

            // Three hangars along the north side, doors facing the runway, a jet inside each.
            foreach (float x in new[] { -110f, 0f, 110f })
            {
                Landmark(root, "Hangar", new Vector3(x, 0f, 82f), 180f, hangar, true, 1f, new Vector3(50f, 20f, 60f));
                Landmark(root, "Jet", new Vector3(x + 4f, 0f, 84f), 180f, jet, true, 1f, new Vector3(12f, 4f, 16f));
            }
            // Jets parked on the south apron, in sandbagged revetments
            var berm = a.Paint(new Color(0.42f, 0.4f, 0.34f), 0.0f, 0.15f, true);
            for (int i = 0; i < 4; i++)
            {
                float x = -96f + i * 64f;
                Landmark(root, "Jet", new Vector3(x, 0f, -58f), 0f, jet, true, 1f, new Vector3(12f, 4f, 16f));
                Prim(PrimitiveType.Cube, root, new Vector3(x, 2.2f, -74f), 0f, new Vector3(22f, 4.4f, 4f), berm);
                for (int s = -1; s <= 1; s += 2)
                    Prim(PrimitiveType.Cube, root, new Vector3(x + s * 12f, 2.2f, -62f), 0f, new Vector3(4f, 4.4f, 24f), berm);
            }
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.035f, -56f), 0f, new Vector3(290f, 0.1f, 40f), asphalt);

            // Control tower and radar on the south side, fuel storage to the west.
            Landmark(root, "ControlTower", new Vector3(150f, 0f, -110f), 0f, tower, true, 1f, new Vector3(22f, 37f, 14f));
            Landmark(root, "RadarDish", new Vector3(-158f, 0f, -112f), 20f, radar, true, 1f, new Vector3(12f, 20f, 12f));
            Landmark(root, "FuelSphere", new Vector3(-200f, 0f, 92f), 45f, tank, true, 1f, new Vector3(16f, 18f, 16f));
            Landmark(root, "FuelSphere", new Vector3(200f, 0f, -150f), 225f, tank, true, 1f, new Vector3(16f, 18f, 16f));

            Clutter(root, l, 26, 733, a.Paint(new Color(0.66f, 0.65f, 0.61f), 0.1f, 0.25f, true),
                a.Paint(new Color(0.35f, 0.38f, 0.3f), 0.3f, 0.35f, true), a.Paint(new Color(0.42f, 0.44f, 0.36f), 0.3f, 0.35f, true));

            // Clear midday light, long views down the runway.
            Lighting(new Color(1f, 0.97f, 0.9f), 1.3f, Quaternion.Euler(55f, -60f, 0f),
                new Color(0.7f, 0.75f, 0.8f), 110f, 1000f,
                new Color(0.62f, 0.68f, 0.76f), new Color(0.55f, 0.55f, 0.52f));
        }
    }
}
