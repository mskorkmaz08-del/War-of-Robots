using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// The small everyday things that make a street look lived in, and big landmarks
    /// that show how tall the robots are: benches, bins, bus shelters, hydrants, signs,
    /// crossings, hedges, shipping containers, billboards, tower cranes, hay bales.
    /// All at true size, like the rest of PropKit.
    /// </summary>
    public static partial class PropKit
    {
        static readonly Color32 BinGreen = C(0.12f, 0.24f, 0.16f, 0.25f);
        static readonly Color32 Hydrant = C(0.72f, 0.1f, 0.08f, 0.45f);
        static readonly Color32 SignGreen = C(0.08f, 0.36f, 0.2f, 0.4f);
        static readonly Color32 SignBlue = C(0.1f, 0.24f, 0.55f, 0.4f);
        static readonly Color32 SignFace = C(0.9f, 0.9f, 0.88f, 0.3f);
        static readonly Color32 CraneYellow = C(0.86f, 0.64f, 0.1f, 0.35f);
        static readonly Color32 Hay = C(0.72f, 0.6f, 0.32f, 0.02f);

        /// <summary>A park bench, 1.8 m: cast-iron ends, wooden slats.</summary>
        public static void Bench(PropMesh pm, Vector3 pos, float yaw)
        {
            pm.At(pos, yaw);
            var wood = C(0.42f, 0.28f, 0.17f, 0.15f);
            foreach (float x in new[] { -0.8f, 0.8f })
            {
                pm.Box(new Vector3(x, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.5f), SteelDark);
                pm.Box(new Vector3(x, 0.65f, 0.24f), new Vector3(0.08f, 0.5f, 0.06f), SteelDark);
            }
            pm.Box(new Vector3(0f, 0.46f, -0.08f), new Vector3(1.8f, 0.05f, 0.18f), wood);
            pm.Box(new Vector3(0f, 0.46f, 0.12f), new Vector3(1.8f, 0.05f, 0.18f), wood);
            pm.Box(new Vector3(0f, 0.72f, 0.26f), new Vector3(1.8f, 0.16f, 0.04f), wood);
        }

        /// <summary>A litter bin, 1 m.</summary>
        public static void Bin(PropMesh pm, Vector3 pos)
        {
            pm.At(pos, 0f);
            pm.Cyl(Vector3.zero, 0.3f, 0.32f, 0.95f, 7, BinGreen, false);
            pm.Cyl(new Vector3(0f, 0.95f, 0f), 0.34f, 0.2f, 0.12f, 7, SteelDark);
        }

        /// <summary>A fire hydrant, 0.8 m.</summary>
        public static void FireHydrant(PropMesh pm, Vector3 pos)
        {
            pm.At(pos, 0f);
            pm.Cyl(Vector3.zero, 0.16f, 0.14f, 0.65f, 6, Hydrant, false);
            pm.Cyl(new Vector3(0f, 0.65f, 0f), 0.17f, 0.04f, 0.16f, 6, Hydrant);
            pm.CylX(new Vector3(0f, 0.45f, 0f), 0.06f, 0.46f, 5, Hydrant, false);
        }

        /// <summary>A street name sign on a 3 m pole.</summary>
        public static void StreetSign(PropMesh pm, Vector3 pos, float yaw, bool blue)
        {
            pm.At(pos, yaw);
            pm.Cyl(Vector3.zero, 0.05f, 0.05f, 3f, 5, Steel, false);
            pm.Box(new Vector3(0f, 2.8f, 0f), new Vector3(1.2f, 0.28f, 0.04f), blue ? SignBlue : SignGreen);
            pm.Face(new Vector3(0f, 2.8f, -0.02f), Vector3.back, 0.9f, 0.08f, SignFace);
            pm.Face(new Vector3(0f, 2.8f, 0.02f), Vector3.forward, 0.9f, 0.08f, SignFace);
        }

        /// <summary>A bus shelter, 4 m: glass back and sides, roof, bench, timetable sign.</summary>
        public static void BusStop(PropMesh pm, Vector3 pos, float yaw)
        {
            pm.At(pos, yaw);
            // Local -z faces the road.
            pm.Box(new Vector3(0f, 2.5f, 0f), new Vector3(4.2f, 0.12f, 1.8f), SteelDark);
            foreach (float x in new[] { -2f, 2f })
            {
                pm.Box(new Vector3(x, 1.25f, 0.8f), new Vector3(0.08f, 2.5f, 0.08f), Steel);
                pm.Box(new Vector3(x, 1.25f, -0.7f), new Vector3(0.08f, 2.5f, 0.08f), Steel);
                pm.Box(new Vector3(x, 1.35f, 0.05f), new Vector3(0.03f, 1.9f, 1.4f), Glass);
            }
            pm.Box(new Vector3(0f, 1.35f, 0.82f), new Vector3(3.9f, 1.9f, 0.03f), Glass);
            pm.Box(new Vector3(0f, 0.45f, 0.55f), new Vector3(2.4f, 0.06f, 0.4f), Steel);
            // Advert panel lit from inside on one end.
            pm.Box(new Vector3(1.96f, 1.35f, 0.05f), new Vector3(0.05f, 1.6f, 1.1f), C(0.78f, 0.74f, 0.62f, 0.7f));
            pm.Cyl(new Vector3(-2.6f, 0f, -0.6f), 0.05f, 0.05f, 2.8f, 5, Steel, false);
            pm.Box(new Vector3(-2.6f, 2.6f, -0.6f), new Vector3(0.5f, 0.5f, 0.04f), SignBlue);
        }

        /// <summary>Zebra crossing across a street (world-space points on the road surface).</summary>
        public static void Crossing(PropMesh pm, Vector3 centre, Vector3 across, float width)
        {
            pm.At(Vector3.zero, 0f);
            Vector3 along = Vector3.Cross(Vector3.up, across).normalized;
            int stripes = Mathf.Max(3, Mathf.FloorToInt((width - 1f) / 1.1f));
            float start = -(stripes - 1) * 1.1f * 0.5f;
            for (int i = 0; i < stripes; i++)
            {
                Vector3 c = centre + across * (start + i * 1.1f) + Vector3.up * 0.11f;
                pm.Panel(c, along, across, 3.2f, 0.55f, Marking);
            }
        }

        /// <summary>A clipped garden hedge between two local points, 1.3 m tall.</summary>
        public static void Hedge(PropMesh pm, Vector3 a, Vector3 b, Biome biome, System.Random r)
        {
            pm.At(Vector3.zero, 0f);
            var col = Vary(biome == Biome.Snow ? C(0.62f, 0.68f, 0.7f) : C(0.18f, 0.3f, 0.12f), r, 0.08f);
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pm.At((a + b) * 0.5f, yaw);
            pm.Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.9f, 1.2f, len), Shade(col, 0.85f));
            pm.Box(new Vector3(0f, 1.25f, 0f), new Vector3(0.8f, 0.1f, len - 0.1f), col);
        }

        /// <summary>
        /// A 40 ft shipping container (12.2 x 2.6 x 2.44 m) at local centre-bottom c,
        /// ribbed sides, doors with locking bars at one end.
        /// </summary>
        static void Container(PropMesh pm, Vector3 c, Color32 col)
        {
            const float L = 12.2f, H = 2.6f, W = 2.44f;
            pm.Box(c + new Vector3(0f, H * 0.5f, 0f), new Vector3(W, H, L), col, true);
            var rib = Shade(col, 0.72f);
            for (int i = 0; i < 9; i++)
            {
                float z = -L * 0.5f + 0.7f + i * (L - 1.4f) / 8f;
                pm.Face(c + new Vector3(-W * 0.5f, H * 0.5f, z), Vector3.left, 0.22f, H - 0.3f, rib);
                pm.Face(c + new Vector3(W * 0.5f, H * 0.5f, z), Vector3.right, 0.22f, H - 0.3f, rib);
            }
            // Door end: two leaves and their bars.
            pm.Face(c + new Vector3(0f, H * 0.5f, L * 0.5f), Vector3.forward, 0.05f, H - 0.2f, Shade(col, 0.5f));
            for (int k = -1; k <= 1; k += 2)
            {
                pm.Face(c + new Vector3(k * 0.35f, H * 0.5f, L * 0.5f + 0.02f), Vector3.forward, 0.05f, H - 0.3f, SteelDark);
                pm.Face(c + new Vector3(k * 0.85f, H * 0.5f, L * 0.5f + 0.02f), Vector3.forward, 0.05f, H - 0.3f, SteelDark);
            }
            // Corner castings.
            pm.Face(c + new Vector3(0f, H - 0.12f, L * 0.5f + 0.01f), Vector3.forward, W, 0.2f, Shade(col, 0.6f));
            pm.Face(c + new Vector3(0f, 0.12f, L * 0.5f + 0.01f), Vector3.forward, W, 0.2f, Shade(col, 0.6f));
        }

        static readonly Color32[] ContainerColours =
        {
            C(0.62f, 0.16f, 0.1f, 0.3f), C(0.1f, 0.28f, 0.5f, 0.3f), C(0.18f, 0.42f, 0.3f, 0.3f), C(0.78f, 0.58f, 0.14f, 0.3f),
            C(0.55f, 0.55f, 0.55f, 0.3f), C(0.4f, 0.22f, 0.14f, 0.3f), C(0.82f, 0.8f, 0.76f, 0.3f), C(0.2f, 0.32f, 0.38f, 0.3f)
        };

        /// <summary>
        /// A stack of containers, rows x tiers, along local z. Returns its height.
        /// Width = rows * 2.5 m, length 12.2 m.
        /// </summary>
        public static float ContainerStack(PropMesh pm, Vector3 pos, float yaw, int rows, int tiers, System.Random r)
        {
            pm.At(pos, yaw);
            float top = 0f;
            for (int i = 0; i < rows; i++)
            {
                int n = Mathf.Max(1, tiers - (R(r) < 0.3f ? 1 : 0));
                for (int k = 0; k < n; k++)
                {
                    float x = (i - (rows - 1) * 0.5f) * 2.5f;
                    float dz = Range(r, -0.3f, 0.3f);
                    Container(pm, new Vector3(x, k * 2.6f, dz), Vary(Pick(r, ContainerColours), r, 0.06f));
                    top = Mathf.Max(top, (k + 1) * 2.6f);
                }
            }
            return top;
        }

        /// <summary>A roadside billboard on two posts, 12 x 4 m panel, 9 m to the top. Returns height.</summary>
        public static float Billboard(PropMesh pm, Vector3 pos, float yaw, System.Random r)
        {
            pm.At(pos, yaw);
            foreach (float x in new[] { -3.5f, 3.5f })
                pm.Box(new Vector3(x, 3f, 0.2f), new Vector3(0.35f, 6f, 0.35f), SteelDark);
            pm.Box(new Vector3(0f, 7f, 0.25f), new Vector3(12.4f, 4.2f, 0.3f), SteelDark);
            // The advert: a big colour field, a band and a block, like a real poster from afar.
            var bg = Vary(Pick(r, new[] { C(0.85f, 0.82f, 0.74f), C(0.12f, 0.3f, 0.55f), C(0.7f, 0.18f, 0.12f), C(0.2f, 0.45f, 0.3f), C(0.9f, 0.7f, 0.15f) }), r);
            pm.Face(new Vector3(0f, 7f, 0.08f), Vector3.back, 12f, 3.8f, bg);
            pm.Face(new Vector3(-1.8f, 7.6f, 0.05f), Vector3.back, 7f, 0.9f, Shade(bg, 0.4f));
            pm.Face(new Vector3(3.6f, 6.6f, 0.05f), Vector3.back, 3.4f, 2.6f, Vary(Pick(r, new[] { C(0.95f, 0.95f, 0.92f), C(0.1f, 0.1f, 0.12f) }), r));
            pm.Face(new Vector3(-2.2f, 6.2f, 0.05f), Vector3.back, 6f, 0.4f, Shade(bg, 1.25f));
            // Walkway and lamps along the bottom.
            pm.Box(new Vector3(0f, 4.85f, -0.4f), new Vector3(12.4f, 0.1f, 0.8f), Steel);
            for (int i = -1; i <= 1; i++) pm.Beam(new Vector3(i * 4f, 4.9f, -0.7f), new Vector3(i * 4f, 5.4f, -1.2f), 0.08f, SteelDark);
            return 9.1f;
        }

        /// <summary>
        /// A construction tower crane, about 42 m to the jib: lattice mast, jib and
        /// counter-jib with concrete weights, operator cab, hook. The one thing in town
        /// that stands taller than a giant robot.
        /// </summary>
        public static float TowerCrane(PropMesh pm, Vector3 pos, float yaw, System.Random r)
        {
            pm.At(pos, yaw);
            float H = Range(r, 38f, 46f);
            const float s = 1.8f;   // mast half width
            pm.Box(new Vector3(0f, 0.5f, 0f), new Vector3(7f, 1f, 7f), Concrete);
            // Mast: four chords and zig-zag bracing on each face.
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -s : s, z = (i & 2) == 0 ? -s : s;
                pm.Beam(new Vector3(x, 1f, z), new Vector3(x, H, z), 0.2f, CraneYellow);
            }
            for (float y = 1f; y < H - 3f; y += 3.6f)
            {
                pm.Beam(new Vector3(-s, y, -s), new Vector3(s, y + 3.6f, -s), 0.1f, CraneYellow);
                pm.Beam(new Vector3(s, y, s), new Vector3(-s, y + 3.6f, s), 0.1f, CraneYellow);
                pm.Beam(new Vector3(-s, y, s), new Vector3(-s, y + 3.6f, -s), 0.1f, CraneYellow);
                pm.Beam(new Vector3(s, y, -s), new Vector3(s, y + 3.6f, s), 0.1f, CraneYellow);
            }
            // Slewing unit and cab.
            pm.Box(new Vector3(0f, H + 0.8f, 0f), new Vector3(4.2f, 1.6f, 4.2f), CraneYellow);
            pm.Box(new Vector3(2.4f, H + 0.2f, -1.2f), new Vector3(1.8f, 2.4f, 2f), C(0.9f, 0.9f, 0.88f, 0.3f));
            pm.Face(new Vector3(3.3f, H + 0.5f, -1.2f), Vector3.right, 1.6f, 1.2f, Glass);
            // Jib (forward, 48 m) and counter-jib (back, 14 m): triangular lattice.
            float jy = H + 1.6f;
            float jib = 48f, back = 14f;
            pm.Beam(new Vector3(-0.9f, jy, 0f), new Vector3(-0.9f, jy, jib), 0.16f, CraneYellow);
            pm.Beam(new Vector3(0.9f, jy, 0f), new Vector3(0.9f, jy, jib), 0.16f, CraneYellow);
            pm.Beam(new Vector3(0f, jy + 1.8f, 0f), new Vector3(0f, jy + 0.6f, jib), 0.14f, CraneYellow);
            for (float z = 0f; z < jib - 2f; z += 4f)
            {
                float t0 = z / jib, t1 = (z + 4f) / jib;
                pm.Beam(new Vector3(-0.9f, jy, z), new Vector3(0f, jy + Mathf.Lerp(1.8f, 0.6f, t1), z + 4f), 0.07f, CraneYellow);
                pm.Beam(new Vector3(0.9f, jy, z), new Vector3(0f, jy + Mathf.Lerp(1.8f, 0.6f, t1), z + 4f), 0.07f, CraneYellow);
            }
            pm.Box(new Vector3(0f, jy + 0.3f, -back * 0.5f), new Vector3(2.4f, 0.6f, back), CraneYellow);
            pm.Box(new Vector3(0f, jy - 0.9f, -back + 2.2f), new Vector3(2.8f, 2.4f, 3.4f), Concrete);   // counterweights
            // Apex and tie bars.
            pm.Beam(new Vector3(0f, jy, 0f), new Vector3(0f, jy + 8f, 0f), 0.22f, CraneYellow);
            pm.Ribbon(new Vector3(0f, jy + 8f, 0f), new Vector3(0f, jy + 0.6f, jib * 0.7f), 0.08f, SteelDark);
            pm.Ribbon(new Vector3(0f, jy + 8f, 0f), new Vector3(0f, jy + 0.6f, -back), 0.08f, SteelDark);
            // Trolley, cable and hook block over the site.
            float hz = Range(r, jib * 0.35f, jib * 0.85f), hookY = Range(r, 8f, H * 0.6f);
            pm.Box(new Vector3(0f, jy - 0.3f, hz), new Vector3(1.6f, 0.5f, 1.6f), SteelDark);
            pm.Ribbon(new Vector3(0f, jy - 0.5f, hz), new Vector3(0f, hookY + 0.6f, hz), 0.06f, SteelDark);
            pm.Box(new Vector3(0f, hookY, hz), new Vector3(0.7f, 1.2f, 0.5f), CraneYellow);
            return H + 10f;
        }

        /// <summary>Temporary site fencing: mesh panels on feet, 2 m.</summary>
        public static void SiteFence(PropMesh pm, Vector3 a, Vector3 b)
        {
            pm.At(Vector3.zero, 0f);
            Vector3 d = b - a;
            float len = d.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 3.5f));
            Vector3 right = d / len;
            for (int i = 0; i <= n; i++)
            {
                Vector3 q = Vector3.Lerp(a, b, i / (float)n);
                pm.Box(q + Vector3.up * 1f, new Vector3(0.06f, 2f, 0.06f), Steel);
                pm.Box(q + Vector3.up * 0.08f, new Vector3(0.25f + Mathf.Abs(right.z) * 0.4f, 0.16f, 0.25f + Mathf.Abs(right.x) * 0.4f), Concrete);
            }
            // The mesh panels, see-through enough from afar: two thin bands and a dark field.
            pm.Beam(a + Vector3.up * 1.95f, b + Vector3.up * 1.95f, 0.05f, Steel);
            pm.Beam(a + Vector3.up * 0.25f, b + Vector3.up * 0.25f, 0.05f, Steel);
            Vector3 nrm = Vector3.Cross(Vector3.up, right);
            pm.QuadW(a + Vector3.up * 0.3f, a + Vector3.up * 1.9f, b + Vector3.up * 1.9f, b + Vector3.up * 0.3f, C(0.3f, 0.32f, 0.3f, 0.3f));
            pm.QuadW(b + Vector3.up * 0.3f, b + Vector3.up * 1.9f, a + Vector3.up * 1.9f, a + Vector3.up * 0.3f, C(0.3f, 0.32f, 0.3f, 0.3f));
        }

        /// <summary>
        /// A building going up: concrete columns on a grid, floor slabs, the top floor
        /// still just columns with rebar sticking out. Returns its height.
        /// </summary>
        public static float ConcreteFrame(PropMesh pm, Vector3 pos, float w, float d, int floors, System.Random r)
        {
            pm.At(pos, 0f);
            const float fh = 3.4f;
            int nx = Mathf.Max(2, Mathf.RoundToInt(w / 6f)), nz = Mathf.Max(2, Mathf.RoundToInt(d / 6f));
            var conc = Vary(C(0.66f, 0.65f, 0.62f), r, 0.04f);
            var rebar = C(0.32f, 0.2f, 0.14f, 0.2f);
            for (int f = 0; f < floors; f++)
            {
                float y0 = f * fh;
                bool last = f == floors - 1;
                for (int i = 0; i <= nx; i++)
                for (int k = 0; k <= nz; k++)
                {
                    float x = -w * 0.5f + i * w / nx, z = -d * 0.5f + k * d / nz;
                    if (last && R(r) < 0.35f) continue;
                    float ch = last ? fh * Range(r, 0.4f, 1f) : fh;
                    pm.Box(new Vector3(x, y0 + ch * 0.5f, z), new Vector3(0.5f, ch, 0.5f), conc);
                    if (last) pm.Beam(new Vector3(x, y0 + ch, z), new Vector3(x + 0.05f, y0 + ch + 1.2f, z), 0.04f, rebar);
                }
                if (!last)
                {
                    pm.Box(new Vector3(0f, y0 + fh, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), conc, true);
                    // Stair core walls.
                    pm.Box(new Vector3(-w * 0.3f, y0 + fh * 0.5f, 0f), new Vector3(0.25f, fh, d * 0.3f), Shade(conc, 0.9f));
                }
            }
            // Formwork and scaffold boards on the open top floor.
            pm.Box(new Vector3(w * 0.15f, (floors - 1) * fh + 0.2f, 0f), new Vector3(w * 0.4f, 0.1f, d * 0.5f), C(0.6f, 0.45f, 0.25f, 0.1f));
            return floors * fh;
        }

        /// <summary>A round hay bale lying on its side, 1.5 m across.</summary>
        public static void HayBale(PropMesh pm, Vector3 pos, float yaw, Biome b)
        {
            pm.At(pos, yaw);
            pm.CylX(new Vector3(0f, 0.75f, 0f), 0.75f, 1.2f, 8, Hay);
            if (b == Biome.Snow) pm.Box(new Vector3(0f, 1.48f, 0f), new Vector3(1.1f, 0.08f, 0.6f), Snow);
        }

        /// <summary>Little things around a family house: downpipes, an AC unit or a dish, wheelie bins.</summary>
        static void HouseExtras(PropMesh pm, float w, float d, float h, Biome b, System.Random r)
        {
            var pipe = b == Biome.Snow ? C(0.3f, 0.3f, 0.3f, 0.3f) : C(0.85f, 0.85f, 0.82f, 0.3f);
            pm.Box(new Vector3(-w * 0.5f - 0.08f, h * 0.5f, -d * 0.5f - 0.08f), new Vector3(0.12f, h, 0.12f), pipe);
            pm.Box(new Vector3(w * 0.5f + 0.08f, h * 0.5f, -d * 0.5f - 0.08f), new Vector3(0.12f, h, 0.12f), pipe);
            pm.Box(new Vector3(0f, h + 0.25f, -d * 0.5f - 0.15f), new Vector3(w + 0.3f, 0.14f, 0.18f), pipe);   // gutter
            if (R(r) < 0.5f)
                pm.Box(new Vector3(w * 0.5f + 0.35f, 0.9f, Range(r, -d * 0.3f, d * 0.3f)), new Vector3(0.6f, 0.75f, 0.85f), C(0.82f, 0.82f, 0.8f, 0.3f));
            if (R(r) < 0.45f)
            {
                pm.Cyl(new Vector3(-w * 0.3f, h * 0.75f, -d * 0.5f - 0.3f), 0.04f, 0.04f, 0.5f, 4, Steel, false);
                pm.Cyl(new Vector3(-w * 0.3f, h * 0.75f + 0.5f, -d * 0.5f - 0.45f), 0.45f, 0.05f, 0.2f, 8, C(0.85f, 0.85f, 0.85f, 0.3f));
            }
            if (R(r) < 0.6f)
                for (int k = 0; k < 2; k++)
                    pm.Box(new Vector3(-w * 0.5f + 0.6f + k * 0.75f, 0.55f, d * 0.5f + 0.6f), new Vector3(0.6f, 1.1f, 0.7f), k == 0 ? BinGreen : C(0.12f, 0.14f, 0.2f, 0.3f));
        }
    }
}
