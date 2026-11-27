using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The props of a map, split into square chunks (one mesh each, so the camera
    /// culls what it can't see), plus the list of things a robot can crush.
    /// </summary>
    public class PropWorld
    {
        public const float Cell = 160f;

        public enum Kind : byte { Car, Truck, Tree, Pole, House, Wall }

        public struct Crush
        {
            public long chunk;
            public int start, end;     // vertex range in the chunk mesh
            public Vector3 pivot;      // ground point the thing stands on
            public float radius;       // footprint
            public float height;
            public Kind kind;
            public float yaw;
        }

        /// <summary>A building big enough to give cover: gets an invisible box collider.</summary>
        public struct Solid
        {
            public Vector3 centre, size;
            public float yaw;
        }

        public readonly Dictionary<long, PropMesh> chunks = new Dictionary<long, PropMesh>();
        public readonly List<Crush> crush = new List<Crush>();
        public readonly List<Solid> solids = new List<Solid>();

        public static long Key(Vector3 p)
        {
            long ix = Mathf.FloorToInt(p.x / Cell) + 1000, iz = Mathf.FloorToInt(p.z / Cell) + 1000;
            return ix * 4096 + iz;
        }

        public PropMesh Get(Vector3 p)
        {
            long k = Key(p);
            if (!chunks.TryGetValue(k, out var pm)) { pm = new PropMesh(); chunks[k] = pm; }
            return pm;
        }

        /// <summary>Builds a crushable thing: call with the builder, records its vertex range.</summary>
        public void Add(Vector3 at, Kind kind, float radius, System.Func<PropMesh, float> build, float yaw = 0f)
        {
            var pm = Get(at);
            int start = pm.Count;
            float h = build(pm);
            crush.Add(new Crush { chunk = Key(at), start = start, end = pm.Count, pivot = at, radius = radius, height = h, kind = kind, yaw = yaw });
        }

        public int VertexCount
        {
            get { int n = 0; foreach (var c in chunks.Values) n += c.Count; return n; }
        }
    }

    /// <summary>
    /// Plans and builds the real-size world around the battle: a road network, towns
    /// with streets, houses, apartment blocks, parked cars, lamps and trees, farms and
    /// forests in the countryside, power lines along the roads. Pure maths and meshes,
    /// so the same code also runs in the offline preview tool.
    /// </summary>
    public static class WorldGen
    {
        public struct Road { public Vector2 a, b; public float width; public bool main; }
        public struct Rect { public Vector2 min, max; public Rect(Vector2 c, float half) { min = c - Vector2.one * half; max = c + Vector2.one * half; } }

        public class Town
        {
            public Vector2 centre;
            public int n;          // blocks per side (even)
            public float pitch;    // block + street
            public float street;
            public float Half => n * pitch * 0.5f + street * 0.5f;
        }

        public class Plan
        {
            public MapId map;
            public Biome biome;
            public MapLayout layout;
            public float half;
            public readonly List<Road> roads = new List<Road>();
            public readonly List<Town> towns = new List<Town>();
            public readonly List<Rect> flats = new List<Rect>();
            public int seed;
        }

        public static Biome BiomeOf(MapId map)
        {
            switch (map)
            {
                case MapId.Canyon: return Biome.Desert;
                case MapId.FrozenPass: return Biome.Snow;
                case MapId.RuinedCity: return Biome.Ruined;
                default: return Biome.Temperate;
            }
        }

        public static Plan MakePlan(MapId map, MapLayout l, float half)
        {
            var p = new Plan { map = map, biome = BiomeOf(map), layout = l, half = half, seed = 900 + (int)map * 77 };
            float ring = 275f, end = half - 50f;

            // Main roads: a square ring around the old arena, and a road from each base onto it.
            void Main(Vector2 a, Vector2 b, float w = 12f) => p.roads.Add(new Road { a = a, b = b, width = w, main = true });
            Main(new Vector2(-ring, -end), new Vector2(-ring, end));
            Main(new Vector2(ring, -end), new Vector2(ring, end));
            Main(new Vector2(-end, -ring), new Vector2(end, -ring));
            Main(new Vector2(-end, ring), new Vector2(end, ring));
            Main(new Vector2(0f, -end), new Vector2(0f, -ring));
            Main(new Vector2(0f, ring), new Vector2(0f, end));

            // Towns where the roads meet; villages in the canyon and the pass.
            bool village = p.biome == Biome.Desert || p.biome == Biome.Snow;
            var centres = new List<Vector2>
            {
                new Vector2(-ring, -ring), new Vector2(ring, -ring), new Vector2(-ring, ring), new Vector2(ring, ring),
                new Vector2(-ring, 0f), new Vector2(ring, 0f)
            };
            foreach (var c in centres)
            {
                var t = village
                    ? new Town { centre = c, n = 2, pitch = 62f, street = 12f }
                    : new Town { centre = c, n = 4, pitch = 58f, street = 14f };
                p.towns.Add(t);
                p.flats.Add(new Rect(c, t.Half + 6f));
                // Town streets (the main road through the middle is already there).
                for (int k = -t.n / 2; k <= t.n / 2; k++)
                {
                    float o = k * t.pitch;
                    p.roads.Add(new Road { a = c + new Vector2(o, -t.Half), b = c + new Vector2(o, t.Half), width = t.street });
                    p.roads.Add(new Road { a = c + new Vector2(-t.Half, o), b = c + new Vector2(t.Half, o), width = t.street });
                }
            }
            return p;
        }

        // ---------------------------------------------------------------------

        static float R(System.Random r) => (float)r.NextDouble();
        static float Range(System.Random r, float a, float b) => a + (b - a) * (float)r.NextDouble();

        public static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static bool OnRoad(Plan p, Vector2 q, float margin)
        {
            foreach (var r in p.roads)
                if (SegDist(q, r.a, r.b) < r.width * 0.5f + margin) return true;
            return false;
        }

        static bool InTown(Plan p, Vector2 q, float margin)
        {
            foreach (var f in p.flats)
                if (q.x > f.min.x - margin && q.x < f.max.x + margin && q.y > f.min.y - margin && q.y < f.max.y + margin) return true;
            return false;
        }

        /// <summary>Away from spawns, capture points, depots and the reactor.</summary>
        public static bool ClearOfLayout(MapLayout l, Vector3 q, float radius)
        {
            if (l == null) return true;
            foreach (var arr in new[] { l.allySpawns, l.enemySpawns, l.allyReserve, l.enemyReserve, l.depots })
                if (arr != null)
                    foreach (var x in arr)
                        if (Flat(x - q).sqrMagnitude < radius * radius) return false;
            if (l.zones != null)
                foreach (var z in l.zones)
                    if (Flat(z - q).sqrMagnitude < (radius + l.zoneRadius) * (radius + l.zoneRadius)) return false;
            return Flat(l.reactor - q).sqrMagnitude > radius * radius;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>
        /// Builds everything. height(x, z) gives the ground; blocked(point, radius) says
        /// whether a map building already stands there.
        /// </summary>
        public static void Generate(Plan p, PropWorld w, System.Func<float, float, float> height, System.Func<Vector3, float, bool> blocked)
        {
            var rnd = new System.Random(p.seed);
            Vector3 G(float x, float z) => new Vector3(x, height(x, z), z);
            bool Free(Vector3 q, float radius) => ClearOfLayout(p.layout, q, radius + 8f) && !blocked(q, radius);

            Roads(p, w, rnd, G);
            foreach (var t in p.towns) TownBlocks(p, t, w, rnd, G, Free);
            foreach (var t in p.towns) StreetLife(p, t, w, rnd, G, Free);
            RoadSides(p, w, rnd, G, Free);
            Countryside(p, w, rnd, G, Free);
        }

        // ---------------------------------------------------------------------
        // Roads
        // ---------------------------------------------------------------------

        static void Roads(Plan p, PropWorld w, System.Random rnd, System.Func<float, float, Vector3> G)
        {
            foreach (var r in p.roads)
            {
                Vector2 d = r.b - r.a;
                float len = d.magnitude;
                Vector2 dir = d / len;
                Vector2 side = new Vector2(-dir.y, dir.x) * (r.width * 0.5f);
                const float piece = 20f;
                for (float s = 0f; s < len - 0.01f; s += piece)
                {
                    float e = Mathf.Min(len, s + piece);
                    Vector2 a2 = r.a + dir * s, b2 = r.a + dir * e;
                    Vector3 a = G(a2.x, a2.y), b = G(b2.x, b2.y);
                    var pm = w.Get((a + b) * 0.5f);
                    pm.At(Vector3.zero, 0f);
                    Vector3 sd = new Vector3(side.x, 0f, side.y), up = Vector3.up * 0.07f;
                    pm.QuadW(a - sd + up, b - sd + up, b + sd + up, a + sd + up, PropKit.Asphalt);
                    pm.QuadW(a + sd + up, b + sd + up, b - sd + up, a - sd + up, PropKit.Asphalt); // two-sided, never vanishes on slopes
                    if (!r.main) continue;
                    // Dashed centre line and solid edge lines.
                    Vector3 fwd = new Vector3(dir.x, 0f, dir.y);
                    Vector3 nrm = new Vector3(-dir.y, 0f, dir.x);
                    for (float m = 0f; m < (e - s) - 3f; m += 9f)
                    {
                        Vector2 c2 = a2 + dir * (m + 1.5f);
                        if (NearOtherRoad(p, r, c2)) continue;
                        Vector3 c = G(c2.x, c2.y) + Vector3.up * 0.1f;
                        pm.QuadW(c - fwd * 1.5f - nrm * 0.08f, c + fwd * 1.5f - nrm * 0.08f, c + fwd * 1.5f + nrm * 0.08f, c - fwd * 1.5f + nrm * 0.08f, PropKit.Marking);
                    }
                    foreach (float sgn in new[] { -1f, 1f })
                    {
                        Vector3 o = nrm * (r.width * 0.5f - 0.5f) * sgn + Vector3.up * 0.1f;
                        if (NearOtherRoad(p, r, (a2 + b2) * 0.5f)) continue;
                        pm.QuadW(a + o - nrm * 0.07f, b + o - nrm * 0.07f, b + o + nrm * 0.07f, a + o + nrm * 0.07f, PropKit.Marking);
                    }
                }
            }
        }

        static bool NearOtherRoad(Plan p, Road self, Vector2 q)
        {
            foreach (var r in p.roads)
            {
                if (r.a == self.a && r.b == self.b) continue;
                if (SegDist(q, r.a, r.b) < r.width * 0.5f + 2f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // Towns
        // ---------------------------------------------------------------------

        static void TownBlocks(Plan p, Town t, PropWorld w, System.Random rnd, System.Func<float, float, Vector3> G, System.Func<Vector3, float, bool> Free)
        {
            float lot = t.pitch - t.street;
            for (int i = -t.n / 2; i < t.n / 2; i++)
            for (int j = -t.n / 2; j < t.n / 2; j++)
            {
                Vector2 c2 = t.centre + new Vector2((i + 0.5f) * t.pitch, (j + 0.5f) * t.pitch);
                Vector3 c = G(c2.x, c2.y);
                if (!Free(c, lot * 0.5f)) continue;
                bool core = Mathf.Abs(i + 0.5f) < 1f && Mathf.Abs(j + 0.5f) < 1f;

                // Pavement and the ground inside the block.
                var pm = w.Get(c);
                pm.At(c, 0f);
                pm.Box(new Vector3(0f, 0.09f, 0f), new Vector3(lot, 0.18f, lot), PropKit.Sidewalk);
                pm.Box(new Vector3(0f, 0.1f, 0f), new Vector3(lot + 0.3f, 0.2f, 0.25f), PropKit.Kerb);
                pm.Box(new Vector3(0f, 0.2f, 0f), new Vector3(lot - 5f, 0.05f, lot - 5f), PropKit.GroundPatch(p.biome));

                float roll = R(rnd);
                switch (p.biome)
                {
                    case Biome.Desert:
                    case Biome.Snow:
                        HouseBlock(p, w, rnd, c, lot, 2);
                        break;
                    case Biome.Ruined:
                        if (roll < 0.62f) ApartmentBlock(p, w, rnd, c, lot, true);
                        else if (roll < 0.82f) HouseBlock(p, w, rnd, c, lot, 2, true);
                        else RubbleBlock(p, w, rnd, c, lot);
                        break;
                    default:
                        bool industrial = p.map == MapId.Refinery || p.map == MapId.Foundry || p.map == MapId.Dockyard;
                        if (industrial && roll < 0.14f) { if (p.map == MapId.Refinery) TankBlock(w, rnd, c, lot); else WarehouseBlock(p, w, rnd, c, lot); }
                        else if (core || roll < 0.32f) ApartmentBlock(p, w, rnd, c, lot, false);
                        else if (roll < 0.4f) ParkBlock(p, w, rnd, c, lot);
                        else HouseBlock(p, w, rnd, c, lot, 2);
                        break;
                }
            }
        }

        /// <summary>Two by two house plots, each with a house, a garden tree, a fence and maybe a car.</summary>
        static void HouseBlock(Plan p, PropWorld w, System.Random rnd, Vector3 c, float lot, int k, bool ruined = false)
        {
            float plot = (lot - 4f) / k;
            for (int a = 0; a < k; a++)
            for (int b = 0; b < k; b++)
            {
                if (R(rnd) < 0.08f) continue;
                float ox = (a - (k - 1) * 0.5f) * plot, oz = (b - (k - 1) * 0.5f) * plot;
                // Face the nearer street.
                float yaw = Mathf.Abs(ox) > Mathf.Abs(oz) ? (ox > 0f ? 90f : -90f) : (oz > 0f ? 0f : 180f);
                // House front (local -z) faces the street: rotate so -z points outwards.
                yaw += 180f;
                Vector3 hp = c + new Vector3(ox, 0.2f, oz);
                if (ruined && R(rnd) < 0.35f) { w.Add(hp, PropWorld.Kind.House, 5f, pm => { PropKit.Rubble(pm, hp, 6f, rnd); return 2f; }); continue; }
                w.Add(hp, PropWorld.Kind.House, 6f, pm => PropKit.House(pm, hp, yaw, p.biome, rnd), yaw);
                // Garden: a tree behind, fence along the side.
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 tp = hp + q * new Vector3(Range(rnd, -4f, 4f), 0f, plot * 0.34f);
                if (p.biome != Biome.Ruined || R(rnd) < 0.5f)
                    w.Add(tp, PropWorld.Kind.Tree, 1.2f, pm => PropKit.Tree(pm, tp, p.biome, rnd, 0.85f));
                var fpm = w.Get(hp);
                Vector3 f0 = hp + q * new Vector3(-plot * 0.46f, -0.2f, -plot * 0.46f), f1 = hp + q * new Vector3(-plot * 0.46f, -0.2f, plot * 0.46f);
                if (p.biome != Biome.Desert) PropKit.Fence(fpm, f0, f1, p.biome);
                if (R(rnd) < 0.45f)
                {
                    Vector3 cp = hp + q * new Vector3(plot * 0.32f, -0.2f, -plot * 0.3f);
                    float cy = yaw + 180f;
                    bool burnt = ruined || R(rnd) < 0.05f;
                    w.Add(cp, PropWorld.Kind.Car, 2.4f, pm => { PropKit.Car(pm, cp, cy, rnd, burnt); return 1.6f; }, cy);
                }
            }
        }

        static void ApartmentBlock(Plan p, PropWorld w, System.Random rnd, Vector3 c, float lot, bool ruined)
        {
            // One long block along a street, or two facing ones.
            bool two = R(rnd) < 0.55f;
            int count = two ? 2 : 1;
            for (int k = 0; k < count; k++)
            {
                float bw = lot - 8f, bd = two ? Range(rnd, 11f, 14f) : Range(rnd, 16f, 22f);
                float oz = two ? (k == 0 ? -1f : 1f) * (lot * 0.5f - 4f - bd * 0.5f) : 0f;
                float yaw = two && k == 1 ? 180f : 0f;
                int floors = p.biome == Biome.Desert ? 2 + rnd.Next(2) : 3 + rnd.Next(4);
                Vector3 bp = c + new Vector3(0f, 0.2f, oz);
                var pm = w.Get(bp);
                float h = PropKit.Apartment(pm, bp, yaw, bw, bd, floors, p.biome, ruined && R(rnd) < 0.8f, rnd);
                w.solids.Add(new PropWorld.Solid { centre = bp + Vector3.up * h * 0.5f, size = new Vector3(bw, h, bd), yaw = yaw });
            }
            // Trees in the courtyard.
            for (int k = 0; k < 2; k++)
            {
                Vector3 tp = c + new Vector3(Range(rnd, -lot * 0.35f, lot * 0.35f), 0.2f, two ? Range(rnd, -3f, 3f) : (k == 0 ? -1f : 1f) * lot * 0.38f);
                if (!two && Mathf.Abs(tp.z - c.z) < 12f) continue;
                w.Add(tp, PropWorld.Kind.Tree, 1.2f, pm => PropKit.Tree(pm, tp, p.biome, rnd, 0.8f));
            }
        }

        static void WarehouseBlock(Plan p, PropWorld w, System.Random rnd, Vector3 c, float lot)
        {
            float bw = lot - 6f, bd = lot * 0.55f, h = Range(rnd, 9f, 13f);
            Vector3 bp = c + new Vector3(0f, 0.2f, lot * 0.12f);
            float top = PropKit.Warehouse(w.Get(bp), bp, 0f, bw, bd, h, rnd);
            w.solids.Add(new PropWorld.Solid { centre = bp + Vector3.up * top * 0.5f, size = new Vector3(bw, top, bd), yaw = 0f });
            // Yard with trucks.
            for (int k = 0; k < 3; k++)
            {
                Vector3 tp = c + new Vector3((k - 1) * 9f, 0.2f, -lot * 0.3f);
                if (R(rnd) < 0.75f) w.Add(tp, PropWorld.Kind.Truck, 4f, pm => { PropKit.Truck(pm, tp, 90f, rnd); return 3.5f; }, 90f);
            }
        }

        static void TankBlock(PropWorld w, System.Random rnd, Vector3 c, float lot)
        {
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                float rad = Range(rnd, 6.5f, 9f), h = Range(rnd, 9f, 14f);
                Vector3 tp = c + new Vector3(a * lot * 0.24f, 0.2f, b * lot * 0.24f);
                float top = PropKit.Tank(w.Get(tp), tp, rad, h, rnd);
                w.solids.Add(new PropWorld.Solid { centre = tp + Vector3.up * top * 0.5f, size = new Vector3(rad * 1.6f, top, rad * 1.6f), yaw = 0f });
            }
        }

        static void ParkBlock(Plan p, PropWorld w, System.Random rnd, Vector3 c, float lot)
        {
            for (int k = 0; k < 9; k++)
            {
                Vector3 tp = c + new Vector3(Range(rnd, -lot * 0.4f, lot * 0.4f), 0.2f, Range(rnd, -lot * 0.4f, lot * 0.4f));
                w.Add(tp, PropWorld.Kind.Tree, 1.2f, pm => PropKit.Tree(pm, tp, p.biome, rnd));
            }
            for (int k = 0; k < 4; k++)
            {
                var bpm = w.Get(c);
                PropKit.Bush(bpm, c + new Vector3(Range(rnd, -lot * 0.4f, lot * 0.4f), 0.2f, Range(rnd, -lot * 0.4f, lot * 0.4f)), p.biome, rnd);
            }
        }

        static void RubbleBlock(Plan p, PropWorld w, System.Random rnd, Vector3 c, float lot)
        {
            var pm = w.Get(c);
            for (int k = 0; k < 6; k++)
                PropKit.Rubble(pm, c + new Vector3(Range(rnd, -lot * 0.38f, lot * 0.38f), 0.2f, Range(rnd, -lot * 0.38f, lot * 0.38f)), Range(rnd, 4f, 8f), rnd);
            for (int k = 0; k < 2; k++)
            {
                Vector3 cp = c + new Vector3(Range(rnd, -lot * 0.3f, lot * 0.3f), 0.2f, Range(rnd, -lot * 0.3f, lot * 0.3f));
                float cy = Range(rnd, 0f, 360f);
                w.Add(cp, PropWorld.Kind.Car, 2.4f, q => { PropKit.Car(q, cp, cy, rnd, true); return 1.6f; }, cy);
            }
        }

        /// <summary>Lamps, parked cars and traffic lights along a town's streets.</summary>
        static void StreetLife(Plan p, Town t, PropWorld w, System.Random rnd, System.Func<float, float, Vector3> G, System.Func<Vector3, float, bool> Free)
        {
            bool ruined = p.biome == Biome.Ruined;
            for (int k = -t.n / 2; k <= t.n / 2; k++)
            for (int axis = 0; axis < 2; axis++)
            {
                float o = k * t.pitch;
                for (float s = -t.Half + 6f; s < t.Half - 6f; s += 1f)
                {
                    // Skip intersections.
                    float rel = Mathf.Repeat(s + t.pitch * 0.5f, t.pitch) - t.pitch * 0.5f;
                    if (Mathf.Abs(rel) < t.street * 0.5f + 4f) continue;
                    Vector2 along = axis == 0 ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
                    Vector2 across = new Vector2(along.y, along.x);
                    Vector2 basePt = t.centre + (axis == 0 ? new Vector2(o, s) : new Vector2(s, o));
                    float yawAlong = axis == 0 ? 0f : 90f;

                    // Street lamps on both sides every 30 m.
                    if (Mathf.Abs(Mathf.Repeat(s, 30f)) < 1f)
                        foreach (float sgn in new[] { -1f, 1f })
                        {
                            Vector2 q2 = basePt + across * sgn * (t.street * 0.5f + 1.2f);
                            Vector3 q = G(q2.x, q2.y) + Vector3.up * 0.18f;
                            if (!Free(q, 1f)) continue;
                            float yaw = axis == 0 ? (sgn > 0f ? -90f : 90f) : (sgn > 0f ? 180f : 0f);
                            if (ruined && R(rnd) < 0.3f) continue;
                            w.Add(q, PropWorld.Kind.Pole, 0.6f, pm => { PropKit.StreetLamp(pm, q, yaw); return 8.3f; }, yaw);
                        }

                    // Parked cars along the kerb, every 7 to 10 m.
                    if (Mathf.Abs(Mathf.Repeat(s + 4f, 8.5f)) < 1f && R(rnd) < (ruined ? 0.3f : 0.38f))
                    {
                        float sgn = R(rnd) < 0.5f ? -1f : 1f;
                        Vector2 q2 = basePt + across * sgn * (t.street * 0.5f - 1.3f);
                        Vector3 q = G(q2.x, q2.y);
                        if (!Free(q, 2f)) continue;
                        float cy = yawAlong + (R(rnd) < 0.5f ? 0f : 180f);
                        bool burnt = ruined ? R(rnd) < 0.6f : R(rnd) < 0.06f;
                        if (R(rnd) < 0.12f) w.Add(q, PropWorld.Kind.Truck, 4f, pm => { PropKit.Truck(pm, q, cy, rnd, burnt); return 3.5f; }, cy);
                        else w.Add(q, PropWorld.Kind.Car, 2.4f, pm => { PropKit.Car(pm, q, cy, rnd, burnt); return 1.6f; }, cy);
                    }

                    // Street trees between lamps (not in the canyon village).
                    if (p.biome != Biome.Desert && Mathf.Abs(Mathf.Repeat(s + 15f, 30f)) < 1f && R(rnd) < 0.7f)
                    {
                        float sgn = R(rnd) < 0.5f ? -1f : 1f;
                        Vector2 q2 = basePt + across * sgn * (t.street * 0.5f + 2f);
                        Vector3 q = G(q2.x, q2.y) + Vector3.up * 0.18f;
                        if (Free(q, 1.5f)) w.Add(q, PropWorld.Kind.Tree, 1.2f, pm => PropKit.Tree(pm, q, p.biome, rnd, 0.75f));
                    }
                }
            }
            // Traffic lights on the corners of the main crossing.
            if (p.biome == Biome.Temperate)
                foreach (var corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                {
                    Vector2 q2 = t.centre + corner * (t.street * 0.5f + 1f);
                    Vector3 q = G(q2.x, q2.y) + Vector3.up * 0.18f;
                    float yaw = Mathf.Atan2(-corner.x, -corner.y) * Mathf.Rad2Deg;
                    w.Add(q, PropWorld.Kind.Pole, 0.5f, pm => { PropKit.TrafficLight(pm, q, yaw); return 5.4f; }, yaw);
                }
        }

        // ---------------------------------------------------------------------
        // Along the main roads, outside the towns
        // ---------------------------------------------------------------------

        static void RoadSides(Plan p, PropWorld w, System.Random rnd, System.Func<float, float, Vector3> G, System.Func<Vector3, float, bool> Free)
        {
            bool snowy = p.biome == Biome.Snow;
            foreach (var r in p.roads)
            {
                if (!r.main) continue;
                Vector2 d = r.b - r.a;
                float len = d.magnitude;
                Vector2 dir = d / len;
                Vector2 side = new Vector2(-dir.y, dir.x);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

                // Power line on one side, a pole every 45 m, wires between neighbours.
                Vector3? prev = null;
                for (float s = 10f; s < len - 5f; s += 45f)
                {
                    Vector2 q2 = r.a + dir * s + side * (r.width * 0.5f + 7f);
                    if (InTown(p, q2, 4f)) { prev = null; continue; }
                    Vector3 q = G(q2.x, q2.y);
                    if (!Free(q, 2f)) { prev = null; continue; }
                    float py = yaw + 90f;
                    w.Add(q, PropWorld.Kind.Pole, 0.6f, pm => { PropKit.PowerPole(pm, q, py, snowy); return 10.5f; }, py);
                    if (prev.HasValue) PropKit.Wires(w.Get((prev.Value + q) * 0.5f), prev.Value, q, py);
                    prev = q;
                }

                // Abandoned and wrecked vehicles on the shoulder.
                for (float s = 20f; s < len - 20f; s += Range(rnd, 40f, 90f))
                {
                    Vector2 q2 = r.a + dir * s + side * (R(rnd) < 0.5f ? -1f : 1f) * (r.width * 0.5f + Range(rnd, -2f, 3f));
                    if (InTown(p, q2, 2f)) continue;
                    Vector3 q = G(q2.x, q2.y);
                    if (!Free(q, 3f)) continue;
                    float cy = yaw + Range(rnd, -35f, 35f) + (R(rnd) < 0.5f ? 180f : 0f);
                    bool burnt = R(rnd) < 0.35f;
                    if (R(rnd) < 0.25f) w.Add(q, PropWorld.Kind.Truck, 4f, pm => { PropKit.Truck(pm, q, cy, rnd, burnt); return 3.5f; }, cy);
                    else w.Add(q, PropWorld.Kind.Car, 2.4f, pm => { PropKit.Car(pm, q, cy, rnd, burnt); return 1.6f; }, cy);
                }
            }
        }

        // ---------------------------------------------------------------------
        // Countryside: forests, farms, rocks
        // ---------------------------------------------------------------------

        static void Countryside(Plan p, PropWorld w, System.Random rnd, System.Func<float, float, Vector3> G, System.Func<Vector3, float, bool> Free)
        {
            float lim = p.half - 25f;
            int clusters, perCluster;
            switch (p.biome)
            {
                case Biome.Snow: clusters = 70; perCluster = 20; break;
                case Biome.Desert: clusters = 26; perCluster = 7; break;
                case Biome.Ruined: clusters = 34; perCluster = 10; break;
                default: clusters = 38; perCluster = 14; break;
            }
            bool OkSpot(Vector2 q, float r) =>
                !InTown(p, q, 8f) && !OnRoad(p, q, 4f) && Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.y)) < lim;

            // Forest clusters.
            for (int c = 0, tries = 0; c < clusters && tries < clusters * 8; tries++)
            {
                Vector2 cc = new Vector2(Range(rnd, -lim, lim), Range(rnd, -lim, lim));
                if (!OkSpot(cc, 30f)) continue;
                if (!ClearOfLayout(p.layout, new Vector3(cc.x, 0f, cc.y), 50f)) continue;
                c++;
                float spread = Range(rnd, 22f, 55f);
                int count = Mathf.RoundToInt(perCluster * Range(rnd, 0.6f, 1.4f));
                for (int k = 0; k < count; k++)
                {
                    Vector2 q2 = cc + new Vector2(Range(rnd, -1f, 1f), Range(rnd, -1f, 1f)) * spread;
                    if (!OkSpot(q2, 3f)) continue;
                    Vector3 q = G(q2.x, q2.y) - Vector3.up * 0.3f;
                    if (!Free(q, 2.5f)) continue;
                    float sc = Range(rnd, 0.8f, 1.25f);
                    w.Add(q, PropWorld.Kind.Tree, 1.3f * sc, pm => PropKit.Tree(pm, q, p.biome, rnd, sc));
                }
            }

            // Lone trees, bushes and rocks everywhere in the open.
            int singles = p.biome == Biome.Desert ? 260 : 200;
            for (int k = 0; k < singles; k++)
            {
                Vector2 q2 = new Vector2(Range(rnd, -lim, lim), Range(rnd, -lim, lim));
                if (!OkSpot(q2, 3f)) continue;
                Vector3 q = G(q2.x, q2.y) - Vector3.up * 0.2f;
                if (!Free(q, 2f)) continue;
                float roll = R(rnd);
                if (roll < 0.35f) w.Add(q, PropWorld.Kind.Tree, 1.3f, pm => PropKit.Tree(pm, q, p.biome, rnd));
                else if (roll < 0.7f) PropKit.Bush(w.Get(q), q, p.biome, rnd);
                else PropKit.Rock(w.Get(q), q, Range(rnd, 1.2f, 3.5f), p.biome, rnd);
            }

            // Farms: a house, a barn, a fenced paddock, a vehicle.
            if (p.biome == Biome.Ruined) return;
            int farms = p.biome == Biome.Desert ? 10 : 16;
            for (int f = 0, tries = 0; f < farms && tries < 300; tries++)
            {
                Vector2 q2 = new Vector2(Range(rnd, -lim, lim), Range(rnd, -lim, lim));
                if (!OkSpot(q2, 30f) || InTown(p, q2, 40f)) continue;
                Vector3 q = G(q2.x, q2.y);
                if (!Free(q, 22f)) continue;
                // Only on level ground.
                float h0 = q.y, var = 0f;
                foreach (var o in new[] { new Vector2(14f, 14f), new Vector2(-14f, 14f), new Vector2(14f, -14f), new Vector2(-14f, -14f) })
                    var = Mathf.Max(var, Mathf.Abs(G(q2.x + o.x, q2.y + o.y).y - h0));
                if (var > 1.6f) continue;
                f++;
                float yaw = Range(rnd, 0f, 360f);
                Quaternion qr = Quaternion.Euler(0f, yaw, 0f);
                Vector3 hp = q + Vector3.up * 0.1f;
                w.Add(hp, PropWorld.Kind.House, 6f, pm => PropKit.House(pm, hp, yaw, p.biome, rnd), yaw);
                Vector3 bp = q + qr * new Vector3(16f, 0f, 4f);
                float barnH = 0f;
                var bpm = w.Get(bp);
                barnH = PropKit.Warehouse(bpm, bp, yaw + 90f, 14f, 10f, 6f, rnd);
                w.solids.Add(new PropWorld.Solid { centre = bp + Vector3.up * barnH * 0.5f, size = new Vector3(14f, barnH, 10f), yaw = yaw + 90f });
                var fpm = w.Get(q);
                Vector3 a0 = q + qr * new Vector3(-12f, 0f, 10f), a1 = q + qr * new Vector3(12f, 0f, 10f), a2 = q + qr * new Vector3(12f, 0f, 34f), a3 = q + qr * new Vector3(-12f, 0f, 34f);
                PropKit.Fence(fpm, a0, a1, p.biome); PropKit.Fence(fpm, a1, a2, p.biome); PropKit.Fence(fpm, a2, a3, p.biome); PropKit.Fence(fpm, a3, a0, p.biome);
                Vector3 cp = q + qr * new Vector3(-6f, 0f, -9f);
                float cy = yaw + 70f;
                w.Add(cp, R(rnd) < 0.5f ? PropWorld.Kind.Truck : PropWorld.Kind.Car, 3f, pm => { if (R(rnd) < 0.4f) PropKit.Truck(pm, cp, cy, rnd); else PropKit.Car(pm, cp, cy, rnd); return 2f; }, cy);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 tp = q + qr * new Vector3(Range(rnd, -14f, -6f), 0f, Range(rnd, -4f, 6f));
                    w.Add(tp, PropWorld.Kind.Tree, 1.3f, pm => PropKit.Tree(pm, tp, p.biome, rnd));
                }
            }
            if (p.biome == Biome.Desert)
                foreach (var t in p.towns)
                {
                    Vector3 wp = G(t.centre.x + t.pitch * 0.5f, t.centre.y + t.pitch * 0.5f) + Vector3.up * 0.2f;
                    PropKit.WaterTower(w.Get(wp), wp, rnd);
                    w.solids.Add(new PropWorld.Solid { centre = wp + Vector3.up * 10f, size = new Vector3(7f, 20f, 7f) });
                }
        }
    }
}
