using UnityEngine;

namespace RobotArena
{
    /// <summary>The look of a map's towns and countryside.</summary>
    public enum Biome { Temperate, Ruined, Desert, Snow }

    /// <summary>
    /// Real-size everyday things, built into a <see cref="PropMesh"/>: houses with
    /// windows and doors, apartment blocks, cars, trucks, street lamps, power poles,
    /// trees, fences, fuel tanks. Everything is in metres at true scale (a car is
    /// 4.5 m long, a house storey 3 m, a street lamp 8 m), so the robots next to them
    /// read as the 15 to 20 m giants they are.
    /// Pure geometry, no Unity objects: the preview tool runs it outside Unity too.
    /// </summary>
    public static partial class PropKit
    {
        static Color32 C(float r, float g, float b, float gloss = 0.08f) => PropMesh.Col(new Color(r, g, b), gloss);
        static float R(System.Random r) => (float)r.NextDouble();
        static float Range(System.Random r, float a, float b) => a + (b - a) * (float)r.NextDouble();
        static T Pick<T>(System.Random r, T[] a) => a[r.Next(a.Length)];

        static Color32 Shade(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);

        static Color32 Vary(Color32 c, System.Random r, float amount = 0.08f)
        {
            float k = 1f + (R(r) * 2f - 1f) * amount;
            return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);
        }

        // ---------------------------------------------------------------------
        // Palettes
        // ---------------------------------------------------------------------

        static readonly Color32[] WallsTemperate = { C(0.86f, 0.82f, 0.72f), C(0.9f, 0.89f, 0.86f), C(0.66f, 0.42f, 0.32f), C(0.86f, 0.78f, 0.56f), C(0.62f, 0.66f, 0.7f), C(0.68f, 0.72f, 0.62f), C(0.78f, 0.68f, 0.6f) };
        static readonly Color32[] WallsDesert = { C(0.82f, 0.66f, 0.48f), C(0.76f, 0.6f, 0.45f), C(0.88f, 0.78f, 0.62f), C(0.7f, 0.52f, 0.4f) };
        static readonly Color32[] WallsSnow = { C(0.42f, 0.28f, 0.18f), C(0.5f, 0.2f, 0.15f), C(0.36f, 0.3f, 0.24f), C(0.62f, 0.6f, 0.55f) };
        static readonly Color32[] Roofs = { C(0.55f, 0.25f, 0.18f), C(0.26f, 0.27f, 0.29f), C(0.36f, 0.26f, 0.2f), C(0.45f, 0.2f, 0.16f), C(0.32f, 0.34f, 0.36f) };
        static readonly Color32[] CarColours =
        {
            C(0.88f, 0.88f, 0.87f, 0.7f), C(0.62f, 0.63f, 0.65f, 0.75f), C(0.08f, 0.08f, 0.09f, 0.7f), C(0.14f, 0.2f, 0.36f, 0.7f),
            C(0.55f, 0.08f, 0.07f, 0.7f), C(0.36f, 0.37f, 0.38f, 0.7f), C(0.72f, 0.66f, 0.55f, 0.6f), C(0.2f, 0.3f, 0.24f, 0.6f)
        };
        static readonly Color32 Glass = C(0.1f, 0.13f, 0.16f, 0.9f);
        static readonly Color32 GlassLit = C(0.55f, 0.48f, 0.34f, 0.6f);
        static readonly Color32 Frame = C(0.88f, 0.87f, 0.84f, 0.2f);
        static readonly Color32 DoorWood = C(0.32f, 0.2f, 0.13f, 0.2f);
        static readonly Color32 Concrete = C(0.62f, 0.61f, 0.59f);
        static readonly Color32 ConcreteDark = C(0.42f, 0.41f, 0.4f);
        static readonly Color32 Rubber = C(0.06f, 0.06f, 0.06f, 0.1f);
        static readonly Color32 Steel = C(0.5f, 0.51f, 0.52f, 0.4f);
        static readonly Color32 SteelDark = C(0.22f, 0.23f, 0.24f, 0.3f);
        static readonly Color32 Burnt = C(0.07f, 0.065f, 0.06f, 0.02f);
        static readonly Color32 Snow = C(0.93f, 0.95f, 0.98f, 0.25f);
        public static readonly Color32 Asphalt = C(0.19f, 0.19f, 0.2f, 0.12f);
        public static readonly Color32 Marking = C(0.86f, 0.85f, 0.78f, 0.2f);
        public static readonly Color32 Sidewalk = C(0.6f, 0.6f, 0.58f, 0.1f);
        public static readonly Color32 Kerb = C(0.7f, 0.7f, 0.68f, 0.1f);
        static readonly Color32 Lawn = C(0.3f, 0.4f, 0.2f, 0.05f);
        static readonly Color32 Dirt = C(0.42f, 0.35f, 0.27f, 0.02f);

        public static Color32 GroundPatch(Biome b) => b == Biome.Desert ? C(0.66f, 0.55f, 0.4f, 0.02f) : b == Biome.Snow ? Snow : b == Biome.Ruined ? Dirt : Lawn;

        // ---------------------------------------------------------------------
        // Buildings
        // ---------------------------------------------------------------------

        /// <summary>Windows along one wall (local frame of the building).</summary>
        static void WindowRow(PropMesh pm, Vector3 wallCentre, Vector3 facing, float wallWidth, float y, float w, float h, float spacing, System.Random r, float litChance = 0.08f, bool sills = false)
        {
            Vector3 right = Vector3.Cross(facing, Vector3.up).normalized;
            int n = Mathf.Max(1, Mathf.FloorToInt((wallWidth - 1.2f) / spacing));
            float start = -(n - 1) * spacing * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Vector3 c = wallCentre + right * (start + i * spacing) + Vector3.up * y;
                pm.Face(c, facing, w + 0.24f, h + 0.24f, Frame);
                pm.Face(c + facing * 0.02f, facing, w, h, R(r) < litChance ? GlassLit : Glass);
                // Sill and lintel: little ledges that catch the light and give depth.
                if (sills)
                {
                    pm.Box(c + facing * 0.1f + Vector3.down * (h * 0.5f + 0.16f), new Vector3(Mathf.Abs(right.x) * (w + 0.4f) + Mathf.Abs(right.z) * 0.2f, 0.08f, Mathf.Abs(right.z) * (w + 0.4f) + Mathf.Abs(right.x) * 0.2f), Frame);
                }
            }
        }

        /// <summary>
        /// A detached family house: one or two storeys of 3 m, real windows and a door,
        /// a gable or hip roof with eaves, a chimney. About 9 x 8 m. Returns its height.
        /// </summary>
        public static float House(PropMesh pm, Vector3 pos, float yaw, Biome b, System.Random r)
        {
            pm.At(pos, yaw);
            int floors = b == Biome.Snow ? 1 + (R(r) < 0.3f ? 1 : 0) : 1 + (R(r) < 0.6f ? 1 : 0);
            float w = Range(r, 8f, 12f), d = Range(r, 7f, 9.5f), fh = 3f;
            float h = floors * fh + 0.5f;
            Color32 wall = Vary(Pick(r, b == Biome.Desert ? WallsDesert : b == Biome.Snow ? WallsSnow : WallsTemperate), r);
            Color32 roof = b == Biome.Snow ? Snow : Vary(Pick(r, Roofs), r, 0.06f);

            pm.Box(new Vector3(0f, 0.25f, 0f), new Vector3(w + 0.3f, 0.5f, d + 0.3f), ConcreteDark); // plinth
            pm.Box(new Vector3(0f, h * 0.5f + 0.25f, 0f), new Vector3(w, h - 0.5f, d), wall);
            for (int f = 0; f < floors; f++)
            {
                float y = 0.5f + f * fh + 1.55f;
                WindowRow(pm, new Vector3(0f, 0f, -d * 0.5f), Vector3.back, w, y, 1.2f, 1.4f, 2.6f, r, 0.08f, true);
                WindowRow(pm, new Vector3(0f, 0f, d * 0.5f), Vector3.forward, w, y, 1.2f, 1.4f, 2.6f, r, 0.08f, true);
                WindowRow(pm, new Vector3(-w * 0.5f, 0f, 0f), Vector3.left, d, y, 1.1f, 1.3f, 3f, r, 0.08f, true);
                WindowRow(pm, new Vector3(w * 0.5f, 0f, 0f), Vector3.right, d, y, 1.1f, 1.3f, 3f, r, 0.08f, true);
            }
            // Front door with a little porch roof.
            float dx = Range(r, -w * 0.25f, w * 0.25f);
            pm.Face(new Vector3(dx, 1.55f, -d * 0.5f - 0.05f), Vector3.back, 1.1f, 2.1f, DoorWood);
            pm.Box(new Vector3(dx, 2.85f, -d * 0.5f - 0.6f), new Vector3(2f, 0.15f, 1.2f), roof);
            pm.Box(new Vector3(dx, 0.6f, -d * 0.5f - 0.8f), new Vector3(1.8f, 0.2f, 1.2f), Concrete);

            float top = h + 0.25f;
            HouseExtras(pm, w, d, h, b, r);
            if (b == Biome.Desert)
            {
                // Flat roof with a low parapet and a water tank.
                pm.Box(new Vector3(0f, top + 0.35f, 0f), new Vector3(w + 0.2f, 0.7f, d + 0.2f), Vary(wall, r, 0.05f));
                pm.Box(new Vector3(0f, top + 0.05f, 0f), new Vector3(w - 0.6f, 0.1f, d - 0.6f), Vary(wall, r, 0.1f));
                if (R(r) < 0.6f) pm.Cyl(new Vector3(w * 0.25f, top, d * 0.2f), 0.7f, 0.7f, 1.4f, 8, C(0.2f, 0.2f, 0.22f, 0.3f));
                return top + 1.4f;
            }
            float roofH = Range(r, 2.2f, 3.4f);
            if (R(r) < 0.65f) pm.Gable(new Vector3(0f, top, 0f), w + 0.9f, d + 1.1f, roofH, roof, wall);
            else pm.Hip(new Vector3(0f, top, 0f), w + 0.9f, d + 1.1f, roofH, roof);
            pm.Box(new Vector3(w * 0.28f, top + roofH * 0.6f, d * 0.15f), new Vector3(0.8f, roofH * 1.2f, 0.8f), b == Biome.Snow ? C(0.4f, 0.38f, 0.36f) : C(0.48f, 0.3f, 0.24f));
            return top + roofH;
        }

        /// <summary>
        /// An apartment or office block: 4 to 9 storeys, rows of windows, a shop band on
        /// the ground floor, balconies, a roof parapet with lift housing and AC units.
        /// ruined = bombed out: missing upper corner, black scorch, rubble at the base.
        /// </summary>
        public static float Apartment(PropMesh pm, Vector3 pos, float yaw, float w, float d, int floors, Biome b, bool ruined, System.Random r)
        {
            pm.At(pos, yaw);
            float fh = 3.1f;
            float h = floors * fh + 1.2f;
            Color32 wall = Vary(b == Biome.Desert ? Pick(r, WallsDesert) : Pick(r, new[] { C(0.72f, 0.7f, 0.66f), C(0.8f, 0.76f, 0.68f), C(0.6f, 0.58f, 0.56f), C(0.68f, 0.5f, 0.42f), C(0.78f, 0.78f, 0.76f) }), r);
            if (ruined) wall = Vary(new Color32((byte)(wall.r * 0.7f), (byte)(wall.g * 0.7f), (byte)(wall.b * 0.7f), 10), r);

            pm.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wall);
            // Ground floor shop fronts.
            Color32 shop = ruined ? Burnt : C(0.14f, 0.16f, 0.18f, 0.9f);
            foreach (var (c, f, len) in new[] { (new Vector3(0f, 0f, -d * 0.5f), Vector3.back, w), (new Vector3(0f, 0f, d * 0.5f), Vector3.forward, w) })
            {
                pm.Face(c + Vector3.up * 1.7f, f, len - 1.5f, 2.6f, shop);
                pm.Face(c + Vector3.up * 3.35f + f * 0.05f, f, len - 1f, 0.7f, ruined ? ConcreteDark : Vary(Pick(r, new[] { C(0.6f, 0.12f, 0.1f), C(0.12f, 0.25f, 0.45f), C(0.15f, 0.4f, 0.3f), C(0.85f, 0.7f, 0.2f) }), r));
            }
            for (int fl = 1; fl < floors; fl++)
            {
                float y = 1.2f + fl * fh + 1.5f;
                WindowRow(pm, new Vector3(0f, 0f, -d * 0.5f), Vector3.back, w, y, 1.5f, 1.6f, 3f, r, ruined ? 0f : 0.12f);
                WindowRow(pm, new Vector3(0f, 0f, d * 0.5f), Vector3.forward, w, y, 1.5f, 1.6f, 3f, r, ruined ? 0f : 0.12f);
                WindowRow(pm, new Vector3(-w * 0.5f, 0f, 0f), Vector3.left, d, y, 1.4f, 1.6f, 3.2f, r, 0.05f);
                WindowRow(pm, new Vector3(w * 0.5f, 0f, 0f), Vector3.right, d, y, 1.4f, 1.6f, 3.2f, r, 0.05f);
                // Balconies on the front every other floor.
                if (fl % 2 == 0 && !ruined)
                    for (float x = -w * 0.5f + 3f; x < w * 0.5f - 2f; x += 6f)
                    {
                        pm.Box(new Vector3(x, 1.2f + fl * fh + 0.1f, -d * 0.5f - 0.7f), new Vector3(2.6f, 0.2f, 1.4f), Concrete);
                        pm.Box(new Vector3(x, 1.2f + fl * fh + 0.65f, -d * 0.5f - 1.38f), new Vector3(2.6f, 0.9f, 0.06f), SteelDark);
                    }
                // Floor slab line.
                pm.Box(new Vector3(0f, 1.2f + fl * fh, 0f), new Vector3(w + 0.16f, 0.22f, d + 0.16f), Vary(wall, r, 0.04f));
            }
            if (ruined)
            {
                // A shell-blasted corner: dark void, slabs hanging, rubble at the foot.
                float cut = Range(r, 0.3f, 0.6f) * h;
                pm.Box(new Vector3(w * 0.3f, h - cut * 0.5f + 0.05f, -d * 0.25f), new Vector3(w * 0.42f, cut, d * 0.52f), C(0.05f, 0.05f, 0.05f, 0f));
                pm.BoxR(new Vector3(w * 0.32f, h - cut, -d * 0.5f - 1f), new Vector3(w * 0.4f, 0.3f, 3f), new Vector3(Range(r, -30f, -15f), 0f, Range(r, -10f, 10f)), Concrete);
                for (int k = 0; k < 7; k++)
                    pm.Blob(new Vector3(Range(r, -w * 0.5f, w * 0.5f), 0.4f, -d * 0.5f - Range(r, 1f, 5f)), new Vector3(Range(r, 1.2f, 3f), Range(r, 0.6f, 1.5f), Range(r, 1.2f, 2.5f)), Vary(ConcreteDark, r, 0.15f), r, 0.3f);
                return h;
            }
            // Roof: parapet, lift housing, AC units, antenna.
            pm.Box(new Vector3(0f, h + 0.5f, -d * 0.5f + 0.15f), new Vector3(w, 1f, 0.3f), wall);
            pm.Box(new Vector3(0f, h + 0.5f, d * 0.5f - 0.15f), new Vector3(w, 1f, 0.3f), wall);
            pm.Box(new Vector3(-w * 0.5f + 0.15f, h + 0.5f, 0f), new Vector3(0.3f, 1f, d), wall);
            pm.Box(new Vector3(w * 0.5f - 0.15f, h + 0.5f, 0f), new Vector3(0.3f, 1f, d), wall);
            pm.Box(new Vector3(Range(r, -w * 0.2f, w * 0.2f), h + 1.6f, Range(r, -d * 0.15f, d * 0.15f)), new Vector3(4f, 3.2f, 4f), Concrete);
            for (int k = 0; k < 3; k++)
                pm.Box(new Vector3(Range(r, -w * 0.4f, w * 0.4f), h + 0.6f, Range(r, -d * 0.35f, d * 0.35f)), new Vector3(1.4f, 1.1f, 1f), Steel);
            if (R(r) < 0.5f) pm.Beam(new Vector3(w * 0.35f, h, d * 0.3f), new Vector3(w * 0.35f, h + 7f, d * 0.3f), 0.12f, SteelDark);
            return h + 3.2f;
        }

        /// <summary>A shed-roofed industrial hall with roller doors and roof lights.</summary>
        public static float Warehouse(PropMesh pm, Vector3 pos, float yaw, float w, float d, float h, System.Random r)
        {
            pm.At(pos, yaw);
            var clad = Vary(Pick(r, new[] { C(0.55f, 0.57f, 0.58f, 0.3f), C(0.38f, 0.45f, 0.5f, 0.3f), C(0.62f, 0.6f, 0.52f, 0.3f), C(0.48f, 0.3f, 0.25f, 0.3f) }), r);
            pm.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), clad);
            pm.Gable(new Vector3(0f, h, 0f), w + 0.6f, d + 0.6f, d * 0.12f, C(0.36f, 0.38f, 0.4f, 0.35f), clad);
            int doors = Mathf.Max(1, Mathf.FloorToInt(w / 9f));
            for (int i = 0; i < doors; i++)
            {
                float x = (i - (doors - 1) * 0.5f) * (w / doors);
                pm.Face(new Vector3(x, 2.6f, -d * 0.5f), Vector3.back, 4.5f, 5.2f, C(0.32f, 0.33f, 0.34f, 0.3f));
                pm.Face(new Vector3(x, 5.6f, -d * 0.5f), Vector3.back, 5f, 0.25f, C(0.8f, 0.6f, 0.1f));
            }
            WindowRow(pm, new Vector3(0f, 0f, d * 0.5f), Vector3.forward, w, h - 1.6f, 2.2f, 0.9f, 3.2f, r, 0f);
            return h + d * 0.12f;
        }

        // ---------------------------------------------------------------------
        // Vehicles
        // ---------------------------------------------------------------------

        static void Wheel(PropMesh pm, Vector3 c, float radius, float width)
        {
            // Six-sided tyre; the outer face is the grey wheel rim.
            pm.CylX(c, radius, width, 6, Rubber, false);
            float hx = width * 0.5f * Mathf.Sign(c.x);
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f;
                Vector3 o = c + new Vector3(hx, 0f, 0f);
                Vector3 d0 = new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)) * radius, d1 = new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                if (hx > 0f) pm.Tri(o, o + d1, o + d0, i % 2 == 0 ? Steel : Rubber);
                else pm.Tri(o, o + d0, o + d1, i % 2 == 0 ? Steel : Rubber);
            }
        }

        /// <summary>A 4.5 m family car (saloon, hatchback or SUV). Wheels are 0.66 m across.</summary>
        public static void Car(PropMesh pm, Vector3 pos, float yaw, System.Random r, bool burnt = false)
        {
            pm.At(pos, yaw);
            int kind = r.Next(3); // 0 saloon, 1 hatch, 2 SUV
            float len = kind == 1 ? 4.0f : kind == 2 ? 4.7f : 4.6f;
            float wid = kind == 2 ? 1.9f : 1.8f;
            float body = kind == 2 ? 0.95f : 0.75f, ground = kind == 2 ? 0.38f : 0.3f;
            var paint = burnt ? Burnt : Vary(Pick(r, CarColours), r, 0.05f);
            var glass = burnt ? C(0.03f, 0.03f, 0.03f, 0f) : Glass;
            // Note: local +z is the front of the car.
            pm.Box(new Vector3(0f, ground + body * 0.5f, 0f), new Vector3(wid, body, len), paint);
            float cabLen = kind == 1 ? len * 0.6f : len * 0.5f;
            float cabZ = kind == 0 ? -0.15f : kind == 1 ? -0.4f : -0.3f;
            float cabH = kind == 2 ? 0.75f : 0.62f;
            float cy = ground + body + cabH * 0.5f;
            pm.Box(new Vector3(0f, cy, cabZ), new Vector3(wid - 0.12f, cabH, cabLen), paint);
            // Windows: windscreen, rear, sides.
            pm.Face(new Vector3(0f, cy, cabZ + cabLen * 0.5f), Vector3.forward, wid - 0.3f, cabH - 0.16f, glass);
            pm.Face(new Vector3(0f, cy, cabZ - cabLen * 0.5f), Vector3.back, wid - 0.3f, cabH - 0.16f, glass);
            pm.Face(new Vector3(-(wid - 0.12f) * 0.5f, cy, cabZ), Vector3.left, cabLen - 0.25f, cabH - 0.18f, glass);
            pm.Face(new Vector3((wid - 0.12f) * 0.5f, cy, cabZ), Vector3.right, cabLen - 0.25f, cabH - 0.18f, glass);
            // Lights and bumpers.
            if (!burnt)
            {
                pm.Face(new Vector3(-wid * 0.33f, ground + body * 0.65f, len * 0.5f), Vector3.forward, 0.38f, 0.16f, C(0.92f, 0.92f, 0.86f, 0.9f));
                pm.Face(new Vector3(wid * 0.33f, ground + body * 0.65f, len * 0.5f), Vector3.forward, 0.38f, 0.16f, C(0.92f, 0.92f, 0.86f, 0.9f));
                pm.Face(new Vector3(-wid * 0.36f, ground + body * 0.7f, -len * 0.5f), Vector3.back, 0.34f, 0.18f, C(0.6f, 0.05f, 0.04f, 0.8f));
                pm.Face(new Vector3(wid * 0.36f, ground + body * 0.7f, -len * 0.5f), Vector3.back, 0.34f, 0.18f, C(0.6f, 0.05f, 0.04f, 0.8f));
            }
            pm.Face(new Vector3(0f, ground + 0.14f, len * 0.5f), Vector3.forward, wid, 0.24f, SteelDark);
            pm.Face(new Vector3(0f, ground + 0.14f, -len * 0.5f), Vector3.back, wid, 0.24f, SteelDark);
            float wr = kind == 2 ? 0.38f : 0.33f;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Wheel(pm, new Vector3(sx * (wid * 0.5f - 0.08f), wr, sz * len * 0.32f), wr, 0.24f);
        }

        /// <summary>A box truck (7.5 m) or a city bus (12 m).</summary>
        public static void Truck(PropMesh pm, Vector3 pos, float yaw, System.Random r, bool burnt = false)
        {
            pm.At(pos, yaw);
            bool bus = R(r) < 0.3f;
            var paint = burnt ? Burnt : bus ? Vary(Pick(r, new[] { C(0.8f, 0.7f, 0.15f, 0.6f), C(0.75f, 0.18f, 0.14f, 0.6f), C(0.85f, 0.85f, 0.82f, 0.6f) }), r) : Vary(Pick(r, CarColours), r);
            if (bus)
            {
                pm.Box(new Vector3(0f, 1.75f, 0f), new Vector3(2.5f, 2.6f, 12f), paint);
                pm.Face(new Vector3(-1.26f, 2.2f, 0.3f), Vector3.left, 10.5f, 1.1f, burnt ? Burnt : Glass);
                pm.Face(new Vector3(1.26f, 2.2f, 0.3f), Vector3.right, 10.5f, 1.1f, burnt ? Burnt : Glass);
                pm.Face(new Vector3(0f, 2.1f, 6.01f), Vector3.forward, 2.2f, 1.5f, burnt ? Burnt : Glass);
                foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { -3.8f, 3.6f })
                    Wheel(pm, new Vector3(sx * 1.12f, 0.5f, z), 0.5f, 0.3f);
                return;
            }
            pm.Box(new Vector3(0f, 1.45f, 2.9f), new Vector3(2.3f, 2.1f, 1.8f), paint);                 // cab
            pm.Face(new Vector3(0f, 1.95f, 3.81f), Vector3.forward, 2f, 0.9f, burnt ? Burnt : Glass);
            pm.Box(new Vector3(0f, 2.05f, -0.9f), new Vector3(2.45f, 3.1f, 5.6f), burnt ? Burnt : C(0.82f, 0.82f, 0.8f, 0.3f)); // box
            pm.Box(new Vector3(0f, 0.65f, 0.4f), new Vector3(1f, 0.3f, 7f), SteelDark);
            foreach (float sx in new[] { -1f, 1f })
            foreach (float z in new[] { -2.6f, 2.7f })
                Wheel(pm, new Vector3(sx * 1.05f, 0.5f, z), 0.5f, 0.32f);
        }

        // ---------------------------------------------------------------------
        // Street furniture
        // ---------------------------------------------------------------------

        /// <summary>An 8 m street lamp with its arm over the road (local +z).</summary>
        public static void StreetLamp(PropMesh pm, Vector3 pos, float yaw)
        {
            pm.At(pos, yaw);
            pm.Cyl(new Vector3(0f, 0f, 0f), 0.16f, 0.08f, 8.2f, 5, Steel, false);
            pm.Beam(new Vector3(0f, 8f, 0f), new Vector3(0f, 8.3f, 1.8f), 0.09f, Steel);
            pm.Box(new Vector3(0f, 8.2f, 2f), new Vector3(0.35f, 0.18f, 0.8f), SteelDark);
            pm.Box(new Vector3(0f, 8.1f, 2f), new Vector3(0.28f, 0.04f, 0.6f), C(1f, 0.92f, 0.7f, 0.9f));
        }

        public static void TrafficLight(PropMesh pm, Vector3 pos, float yaw)
        {
            pm.At(pos, yaw);
            pm.Cyl(Vector3.zero, 0.1f, 0.1f, 5.4f, 6, SteelDark);
            pm.Beam(new Vector3(0f, 5.3f, 0f), new Vector3(0f, 5.3f, 4f), 0.12f, SteelDark);
            pm.Box(new Vector3(0f, 4.8f, 3.6f), new Vector3(0.35f, 1f, 0.3f), C(0.1f, 0.1f, 0.1f, 0.3f));
            pm.Face(new Vector3(0f, 5.1f, 3.6f), Vector3.back, 0.2f, 0.2f, C(0.9f, 0.15f, 0.1f, 0.9f));
        }

        /// <summary>A 10 m wooden power pole with a cross arm. Returns the wire tie points (local).</summary>
        public static void PowerPole(PropMesh pm, Vector3 pos, float yaw, bool snowy)
        {
            pm.At(pos, yaw);
            var wood = C(0.34f, 0.26f, 0.19f);
            pm.Cyl(Vector3.zero, 0.16f, 0.12f, 10.5f, 6, wood);
            pm.Box(new Vector3(0f, 9.8f, 0f), new Vector3(2.6f, 0.14f, 0.14f), wood);
            pm.Beam(new Vector3(-0.9f, 9.1f, 0f), new Vector3(0f, 9.8f, 0f), 0.08f, wood);
            for (int i = -1; i <= 1; i++) pm.Cyl(new Vector3(i * 1.1f, 9.87f, 0f), 0.06f, 0.06f, 0.25f, 5, C(0.4f, 0.55f, 0.45f, 0.6f));
            if (snowy) pm.Box(new Vector3(0f, 9.92f, 0f), new Vector3(2.6f, 0.06f, 0.18f), Snow);
        }

        /// <summary>Three sagging wires between two poles (world positions of the pole bases, same yaw).</summary>
        public static void Wires(PropMesh pm, Vector3 a, Vector3 b, float yaw)
        {
            var wire = C(0.08f, 0.08f, 0.08f, 0.3f);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            pm.At(Vector3.zero, 0f);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 off = q * new Vector3(i * 1.1f, 9.98f, 0f);
                Vector3 p0 = a + off, p3 = b + off;
                const int seg = 4;
                Vector3 prev = p0;
                for (int s = 1; s <= seg; s++)
                {
                    float t = s / (float)seg;
                    Vector3 p = Vector3.Lerp(p0, p3, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.9f;
                    pm.Ribbon(prev, p, 0.07f, wire);
                    prev = p;
                }
            }
        }

        public static void Fence(PropMesh pm, Vector3 a, Vector3 b, Biome biome)
        {
            pm.At(Vector3.zero, 0f);
            var col = biome == Biome.Snow ? C(0.4f, 0.3f, 0.22f) : biome == Biome.Desert ? C(0.7f, 0.58f, 0.42f) : C(0.82f, 0.8f, 0.76f);
            float len = Vector3.Distance(a, b);
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 2.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)(posts - 1));
                pm.Box(p + Vector3.up * 0.6f, new Vector3(0.12f, 1.2f, 0.12f), col);
            }
            pm.Beam(a + Vector3.up * 0.95f, b + Vector3.up * 0.95f, 0.07f, col);
            pm.Beam(a + Vector3.up * 0.45f, b + Vector3.up * 0.45f, 0.07f, col);
        }

        /// <summary>Concrete road barrier (0.8 m tall) along local x.</summary>
        public static void Barrier(PropMesh pm, Vector3 pos, float yaw)
        {
            pm.At(pos, yaw);
            pm.Box(new Vector3(0f, 0.2f, 0f), new Vector3(3f, 0.4f, 0.6f), Concrete);
            pm.Box(new Vector3(0f, 0.6f, 0f), new Vector3(3f, 0.4f, 0.3f), Concrete);
        }

        // ---------------------------------------------------------------------
        // Nature
        // ---------------------------------------------------------------------

        /// <summary>A tree for the biome, 7 to 18 m tall. Returns the height.</summary>
        public static float Tree(PropMesh pm, Vector3 pos, Biome b, System.Random r, float scale = 1f, bool simple = false)
        {
            pm.At(pos, Range(r, 0f, 360f));
            var bark = Vary(C(0.3f, 0.22f, 0.16f), r);
            switch (b)
            {
                case Biome.Snow:
                {
                    // Spruce: stacked cones, snow on the upper tiers.
                    // Six drooping tiers, each a dark green skirt with snow lying on it.
                    float h = Range(r, 11f, 18f) * scale;
                    pm.Cyl(Vector3.zero, 0.32f * scale, 0.1f, h * 0.4f, 6, bark, false);
                    var green = Vary(C(0.1f, 0.19f, 0.13f), r);
                    var snow = Vary(C(0.8f, 0.84f, 0.88f, 0.2f), r, 0.04f);
                    int tiers = 6;
                    for (int i = 0; i < tiers; i++)
                    {
                        float t = i / (float)(tiers - 1);
                        float rad = Mathf.Lerp(3.4f, 0.9f, t) * scale * Range(r, 0.88f, 1.1f);
                        float y = h * (0.14f + t * 0.7f);
                        float th = h * Mathf.Lerp(0.22f, 0.26f, t);
                        // Needles, darker on the lower tiers, then snow lying on top.
                        pm.ConeSmooth(new Vector3(0f, y, 0f), rad, th, 9, Shade(green, 0.8f + t * 0.25f), th * 0.12f, r);
                        if (t > 0.15f) pm.ConeSmooth(new Vector3(0f, y + th * 0.3f, 0f), rad * 0.7f, th * 0.72f, 9, snow, th * 0.06f, r);
                    }
                    return h;
                }
                case Biome.Desert:
                {
                    if (R(r) < 0.55f)
                    {
                        // Palm: leaning trunk, fan of fronds.
                        float h = Range(r, 8f, 13f) * scale;
                        Vector3 top = new Vector3(Range(r, -1.2f, 1.2f), h, Range(r, -1.2f, 1.2f));
                        pm.Beam(Vector3.zero, top * 0.5f, 0.45f * scale, bark);
                        pm.Beam(top * 0.5f, top, 0.36f * scale, bark);
                        var frond = Vary(C(0.3f, 0.38f, 0.16f), r);
                        for (int i = 0; i < 7; i++)
                        {
                            float a = i * 51f * Mathf.Deg2Rad;
                            Vector3 tip = top + new Vector3(Mathf.Cos(a), -0.35f, Mathf.Sin(a)) * 4f * scale;
                            Vector3 mid = (top + tip) * 0.5f + Vector3.up * 0.6f;
                            Vector3 side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.6f * scale;
                            pm.Tri(top, mid + side, mid - side, frond);
                            pm.Tri(mid - side, mid + side, tip, frond);
                            pm.Tri(top, mid - side, mid + side, frond);
                            pm.Tri(mid + side, mid - side, tip, frond);
                        }
                        return h;
                    }
                    // Scrubby desert tree.
                    float hh = Range(r, 4f, 7f) * scale;
                    pm.Cyl(Vector3.zero, 0.25f, 0.15f, hh * 0.5f, 5, bark, false);
                    var dry = Vary(C(0.42f, 0.42f, 0.24f), r);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 c = new Vector3(Range(r, -1.5f, 1.5f), hh * Range(0.62f, 0.85f, r), Range(r, -1.5f, 1.5f));
                        if (!simple) pm.Beam(new Vector3(0f, hh * 0.4f, 0f), c, 0.12f, bark);
                        pm.Foliage(c, new Vector3(2.4f, 1.1f, 2.4f) * scale, Vary(dry, r, 0.08f), r, 0.22f, simple);
                    }
                    return hh;
                }
                case Biome.Ruined:
                {
                    if (R(r) < 0.4f)
                    {
                        // Dead, burnt tree: bare trunk and a few branches.
                        float h = Range(r, 7f, 11f) * scale;
                        var dead = C(0.16f, 0.14f, 0.12f);
                        pm.Cyl(Vector3.zero, 0.35f * scale, 0.12f, h, 5, dead, false);
                        for (int i = 0; i < 4; i++)
                        {
                            float y = h * Range(r, 0.45f, 0.85f);
                            float a = Range(r, 0f, Mathf.PI * 2f);
                            pm.Beam(new Vector3(0f, y, 0f), new Vector3(Mathf.Cos(a) * 2.5f, y + 2f, Mathf.Sin(a) * 2.5f) * scale, 0.14f * scale, dead);
                        }
                        return h;
                    }
                    goto default;
                }
                default:
                {
                    // Broadleaf: a flared trunk forking into branches, each carrying a soft
                    // mass of leaves, with a crown mass on top.
                    float h = Range(r, 8f, 14f) * scale;
                    // (simple = forest trees seen from afar: no flare or branches, one mass fewer)
                    if (!simple) pm.Cyl(Vector3.zero, 0.5f * scale, 0.36f * scale, 0.8f, 6, bark, false);   // root flare
                    pm.Cyl(new Vector3(0f, simple ? 0f : 0.8f, 0f), 0.36f * scale, 0.2f * scale, h * 0.5f + (simple ? 0.8f : 0f), 6, bark, false);
                    var leaf = Vary(Pick(r, new[] { C(0.2f, 0.32f, 0.13f), C(0.27f, 0.38f, 0.15f), C(0.22f, 0.3f, 0.17f), C(0.33f, 0.36f, 0.14f) }), r);
                    int blobs = simple ? 2 : 3;
                    float fork = h * 0.42f;
                    for (int i = 0; i < blobs; i++)
                    {
                        float a = i * 2.1f + Range(r, 0f, 1f);
                        float rad = h * Range(0.19f, 0.25f, r);
                        Vector3 c = new Vector3(Mathf.Cos(a) * h * 0.15f, h * Range(0.58f, 0.74f, r), Mathf.Sin(a) * h * 0.15f);
                        if (!simple) pm.Beam(new Vector3(0f, fork, 0f), Vector3.Lerp(new Vector3(0f, fork, 0f), c, 0.85f), 0.16f * scale, bark);
                        else rad *= 1.12f;
                        pm.Foliage(c, new Vector3(rad, rad * 0.8f, rad), Vary(leaf, r, 0.07f), r, 0.16f, simple);
                    }
                    float top = h * 0.24f;
                    pm.Foliage(new Vector3(0f, h - top * 0.9f, 0f), new Vector3(top, top * 0.85f, top), Vary(leaf, r, 0.07f), r, 0.16f, simple);
                    return h;
                }
            }
        }

        static float Range(float a, float b, System.Random r) => a + (b - a) * (float)r.NextDouble();

        public static void Bush(PropMesh pm, Vector3 pos, Biome b, System.Random r)
        {
            pm.At(pos, 0f);
            var col = b == Biome.Desert ? C(0.45f, 0.42f, 0.26f) : b == Biome.Snow ? C(0.75f, 0.8f, 0.83f) : C(0.22f, 0.33f, 0.14f);
            pm.Foliage(new Vector3(0f, 0.55f, 0f), new Vector3(Range(r, 1.2f, 2f), Range(r, 0.8f, 1.3f), Range(r, 1.2f, 2f)), Vary(col, r, 0.12f), r, 0.2f);
        }

        public static void Rock(PropMesh pm, Vector3 pos, float size, Biome b, System.Random r)
        {
            pm.At(pos, Range(r, 0f, 360f));
            var col = b == Biome.Desert ? C(0.6f, 0.44f, 0.33f) : b == Biome.Snow ? C(0.55f, 0.58f, 0.62f) : C(0.45f, 0.44f, 0.42f);
            pm.Blob(new Vector3(0f, size * 0.25f, 0f), new Vector3(size, size * 0.7f, size * 0.85f), Vary(col, r, 0.1f), r, 0.3f);
        }

        // ---------------------------------------------------------------------
        // Industry
        // ---------------------------------------------------------------------

        /// <summary>A fuel storage tank (12-20 m across) with a ladder and walkway.</summary>
        public static float Tank(PropMesh pm, Vector3 pos, float radius, float height, System.Random r)
        {
            pm.At(pos, Range(r, 0f, 360f));
            var shell = Vary(Pick(r, new[] { C(0.82f, 0.82f, 0.8f, 0.35f), C(0.62f, 0.64f, 0.6f, 0.35f), C(0.5f, 0.55f, 0.5f, 0.3f) }), r, 0.04f);
            pm.Cyl(Vector3.zero, radius + 0.6f, radius + 0.6f, 0.4f, 16, Concrete);
            pm.Cyl(new Vector3(0f, 0.4f, 0f), radius, radius, height, 16, shell);
            pm.Cyl(new Vector3(0f, height + 0.4f, 0f), radius, radius * 0.15f, radius * 0.18f, 16, shell);
            pm.Beam(new Vector3(radius + 0.4f, 0.4f, 0f), new Vector3(radius + 0.4f, height + 1f, 0f), 0.12f, SteelDark);
            pm.Box(new Vector3(0f, height * 0.5f, -radius - 0.02f), new Vector3(radius * 0.8f, 1.4f, 0.04f), C(0.75f, 0.12f, 0.1f));
            return height + radius * 0.18f;
        }

        public static void WaterTower(PropMesh pm, Vector3 pos, System.Random r)
        {
            pm.At(pos, Range(r, 0f, 90f));
            var steel = C(0.5f, 0.52f, 0.5f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                pm.Beam(new Vector3(Mathf.Cos(a) * 3.2f, 0f, Mathf.Sin(a) * 3.2f), new Vector3(Mathf.Cos(a) * 2.2f, 14f, Mathf.Sin(a) * 2.2f), 0.3f, steel);
            }
            pm.Cyl(new Vector3(0f, 14f, 0f), 3.6f, 3.6f, 4.5f, 12, C(0.72f, 0.72f, 0.68f, 0.3f));
            pm.Cyl(new Vector3(0f, 18.5f, 0f), 3.6f, 0f, 1.6f, 12, steel);
        }

        /// <summary>A pile of rubble and broken slabs.</summary>
        public static void Rubble(PropMesh pm, Vector3 pos, float size, System.Random r)
        {
            pm.At(pos, Range(r, 0f, 360f));
            for (int k = 0; k < 5; k++)
                pm.Blob(new Vector3(Range(r, -size, size), 0.2f, Range(r, -size, size)) * 0.6f, new Vector3(Range(r, 0.6f, 1.6f), Range(r, 0.3f, 0.9f), Range(r, 0.6f, 1.4f)) * size * 0.6f, Vary(ConcreteDark, r, 0.18f), r, 0.35f);
            pm.BoxR(new Vector3(0f, 0.5f, 0f), new Vector3(size * 1.2f, 0.25f, size * 0.8f), new Vector3(Range(r, -20f, 20f), Range(r, 0f, 90f), Range(r, -20f, 20f)), Concrete);
        }
    }
}
