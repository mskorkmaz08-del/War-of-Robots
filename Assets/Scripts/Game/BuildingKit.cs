using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Turns plain blocks into buildings: window facades with lit rooms, a concrete plinth,
    /// doors, a roof parapet and rooftop machinery. Facades are one mesh per building with
    /// procedural textures (window grid / ribbed metal), so they stay cheap on phones.
    /// </summary>
    public static class BuildingKit
    {
        public enum Style { Office, Industrial, Bunker }

        static Texture2D officeTex, officeGlow, metalTex, metalGlow;
        static Material officeMat, metalMat;

        static GameAssets A => GameAssets.Get();

        // ---------------------------------------------------------------------
        // Procedural textures
        // ---------------------------------------------------------------------

        static float Hash(int x, int y) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f, 1f);

        /// <summary>4 x 4 windows per tile: concrete frame, dark glass with sky reflection, some rooms lit.</summary>
        static void MakeOffice()
        {
            const int N = 256, cell = N / 4;
            officeTex = new Texture2D(N, N, TextureFormat.RGB24, true) { name = "OfficeFacade", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            officeGlow = new Texture2D(N, N, TextureFormat.RGB24, true) { name = "OfficeGlow", wrapMode = TextureWrapMode.Repeat };
            var c = new Color[N * N];
            var g = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int cx = x / cell, cy = y / cell;
                float u = (x % cell) / (float)cell, v = (y % cell) / (float)cell;
                float grain = (Hash(x, y) - 0.5f) * 0.05f;
                // Window opening inside each cell: 12% frame on the sides, sill at the bottom.
                bool glass = u > 0.12f && u < 0.88f && v > 0.22f && v < 0.9f;
                bool mullion = glass && (Mathf.Abs(u - 0.5f) < 0.025f || Mathf.Abs(v - 0.62f) < 0.02f);
                bool sill = v > 0.16f && v <= 0.22f && u > 0.08f && u < 0.92f;
                Color col, glow = Color.black;
                if (glass && !mullion)
                {
                    bool lit = Hash(cx * 7 + 3, cy * 13 + 1) > 0.68f;
                    float refl = Mathf.Lerp(0.2f, 0.36f, v) + (u - v) * 0.05f;
                    col = lit ? new Color(0.55f, 0.45f, 0.3f) : new Color(refl * 0.62f, refl * 0.72f, refl * 0.82f);
                    if (lit)
                    {
                        float blind = v > 0.75f ? 0.55f : 1f; // half-drawn blinds
                        glow = new Color(1f, 0.74f, 0.45f) * 0.55f * blind * (0.75f + Hash(cx, cy) * 0.5f);
                    }
                }
                else if (mullion) col = new Color(0.16f, 0.17f, 0.18f);
                else if (sill) col = new Color(0.68f, 0.67f, 0.64f);
                else
                {
                    // Concrete with a slab line every floor and faint streaks under the sills.
                    float slab = v < 0.06f ? 0.86f : 1f;
                    float streak = (v < 0.16f && u > 0.2f && u < 0.8f) ? 0.9f + Hash(x, 7) * 0.06f : 1f;
                    float k = (0.52f + grain) * slab * streak;
                    col = new Color(k, k * 0.98f, k * 0.95f);
                }
                c[y * N + x] = col;
                g[y * N + x] = glow;
            }
            officeTex.SetPixels(c); officeTex.Apply(true, true);
            officeGlow.SetPixels(g); officeGlow.Apply(true, true);
        }

        /// <summary>Ribbed steel cladding with a strip of high windows near the top of each tile.</summary>
        static void MakeMetal()
        {
            const int N = 128;
            metalTex = new Texture2D(N, N, TextureFormat.RGB24, true) { name = "MetalCladding", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            metalGlow = new Texture2D(N, N, TextureFormat.RGB24, true) { name = "MetalGlow", wrapMode = TextureWrapMode.Repeat };
            var c = new Color[N * N];
            var g = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float rib = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 8f));
                float dirt = Mathf.Lerp(0.8f, 1f, v) + (Hash(x, y) - 0.5f) * 0.05f;
                float k = 0.42f * rib * dirt;
                Color col = new Color(k * 0.95f, k, k * 1.04f);
                Color glow = Color.black;
                bool window = v > 0.78f && v < 0.9f && Mathf.Repeat(u * 4f, 1f) > 0.1f && Mathf.Repeat(u * 4f, 1f) < 0.9f;
                if (window)
                {
                    col = new Color(0.2f, 0.24f, 0.27f);
                    glow = new Color(0.9f, 0.85f, 0.7f) * 0.25f;
                }
                if (v > 0.74f && v <= 0.78f || v >= 0.9f && v < 0.93f) col = new Color(0.2f, 0.21f, 0.22f);
                c[y * N + x] = col;
                g[y * N + x] = glow;
            }
            metalTex.SetPixels(c); metalTex.Apply(true, true);
            metalGlow.SetPixels(g); metalGlow.Apply(true, true);
        }

        static Material FacadeMat(bool office)
        {
            if (office && officeMat != null) return officeMat;
            if (!office && metalMat != null) return metalMat;
            if (office) MakeOffice(); else MakeMetal();
            var src = A != null ? A.emissiveTemplate : null;
            var m = src != null ? new Material(src) : new Material(Shader.Find("Standard"));
            m.name = office ? "OfficeFacade" : "MetalFacade";
            m.color = Color.white;
            m.mainTexture = office ? officeTex : metalTex;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", office ? officeGlow : metalGlow);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white * (office ? 1.6f : 1.2f));
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", office ? 0.62f : 0.4f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", office ? 0.15f : 0.55f);
            if (office) officeMat = m; else metalMat = m;
            return m;
        }

        // ---------------------------------------------------------------------
        // Building parts
        // ---------------------------------------------------------------------

        static Transform Box(Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = false, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            go.name = "Part";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Transform Cyl(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>One mesh with a textured quad on each wall, UVs sized so windows keep real proportions.</summary>
        static void Facade(Transform parent, Vector3 size, float bottom, float top, float cellW, float cellH, float rowsPerTile, Material mat)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            float hx = size.x * 0.5f + 0.06f, hz = size.z * 0.5f + 0.06f;
            float rows = Mathf.Max(1, Mathf.Floor((top - bottom) / cellH));
            float h = rows * cellH;
            for (int f = 0; f < 4; f++)
            {
                // Face basis: normal n, right r, half-width w, offset d.
                Vector3 n = f == 0 ? Vector3.forward : f == 1 ? Vector3.back : f == 2 ? Vector3.right : Vector3.left;
                Vector3 r = Vector3.Cross(Vector3.up, n);
                float w = (f < 2 ? size.x : size.z);
                float d = f < 2 ? hz : hx;
                float cols = Mathf.Max(1, Mathf.Round((w - 1.2f) / cellW));
                float fw = Mathf.Min(w - 0.6f, cols * cellW);
                int i0 = verts.Count;
                Vector3 c = n * d + Vector3.up * bottom;
                verts.Add(c - r * fw * 0.5f); verts.Add(c + r * fw * 0.5f);
                verts.Add(c + r * fw * 0.5f + Vector3.up * h); verts.Add(c - r * fw * 0.5f + Vector3.up * h);
                float u = cols / 4f, v = rows / rowsPerTile;
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(u, 0f)); uvs.Add(new Vector2(u, v)); uvs.Add(new Vector2(0f, v));
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            var mesh = new Mesh { name = "Facade" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var go = new GameObject("Facade");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// Dresses a block-shaped building. <paramref name="center"/> is the ground point under the
        /// middle of the block, <paramref name="size"/> its full size. Returns the detail root.
        /// </summary>
        public static Transform Dress(Transform parent, Vector3 center, float yaw, Vector3 size, Style style, int seed)
        {
            var a = A;
            var root = new GameObject("Building").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(center.x, 0f, center.z);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (a == null) return root;

            var concrete = a.Paint(new Color(0.46f, 0.45f, 0.43f), 0f, 0.25f, false);
            var dark = a.Paint(new Color(0.14f, 0.15f, 0.16f), 0.6f, 0.4f, false);
            var steel = a.Paint(new Color(0.36f, 0.38f, 0.4f), 0.8f, 0.45f, false);
            var roofMat = a.Paint(new Color(0.28f, 0.28f, 0.27f), 0.1f, 0.2f, false);
            var lamp = a.Glow(new Color(1f, 0.8f, 0.55f), 2.2f);
            float H = size.y, hx = size.x * 0.5f, hz = size.z * 0.5f;
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();

            // Plinth: darker concrete base all round.
            float plinth = style == Style.Bunker ? 1.2f : Mathf.Min(2.6f, H * 0.25f);
            Box(root, new Vector3(0f, plinth * 0.5f, 0f), new Vector3(size.x + 0.5f, plinth, size.z + 0.5f), concrete);

            // Walls
            if (style == Style.Office)
                Facade(root, size, plinth + 0.4f, H - 1.4f, 3.4f, 4f, 4f, FacadeMat(true));
            else if (style == Style.Industrial)
                Facade(root, size, plinth, H - 0.9f, 4.5f, Mathf.Max(4f, H - 0.9f - plinth), 1f, FacadeMat(false));
            else
            {
                // Bunker: firing slits on each side.
                for (int f = 0; f < 4; f++)
                {
                    bool alongX = f < 2;
                    float w = alongX ? size.x : size.z;
                    float d = (alongX ? hz : hx) + 0.06f;
                    Vector3 n = f == 0 ? Vector3.forward : f == 1 ? Vector3.back : f == 2 ? Vector3.right : Vector3.left;
                    var slit = Box(root, n * d + Vector3.up * (H * 0.62f), alongX ? new Vector3(w * 0.7f, 0.6f, 0.14f) : new Vector3(0.14f, 0.6f, w * 0.7f), dark);
                    _ = slit;
                    Box(root, n * (d + 0.25f) + Vector3.up * (H * 0.62f + 0.45f), alongX ? new Vector3(w * 0.76f, 0.2f, 0.6f) : new Vector3(0.6f, 0.2f, w * 0.76f), concrete);
                }
            }

            // Corner pilasters frame the facade.
            if (style != Style.Bunker)
                for (int i = 0; i < 4; i++)
                {
                    float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                    Box(root, new Vector3(sx * (hx + 0.05f), H * 0.5f, sz * (hz + 0.05f)), new Vector3(0.9f, H, 0.9f), style == Style.Office ? concrete : steel);
                }

            // Door on the front (-z) side with a canopy and a lamp.
            float doorW = style == Style.Industrial ? Mathf.Min(size.x * 0.4f, 7f) : 2.4f;
            float doorH = style == Style.Industrial ? Mathf.Min(H * 0.6f, 6f) : Mathf.Min(2.8f, H * 0.5f);
            Box(root, new Vector3(0f, doorH * 0.5f, -hz - 0.32f), new Vector3(doorW, doorH, 0.2f), style == Style.Industrial ? steel : dark);
            Box(root, new Vector3(0f, doorH + 0.25f, -hz - 1.1f), new Vector3(doorW + 1.6f, 0.25f, 1.8f), dark);
            Box(root, new Vector3(0f, doorH + 0.05f, -hz - 0.45f), new Vector3(doorW * 0.5f, 0.15f, 0.15f), lamp);
            if (style == Style.Industrial)
                for (int k = 1; k < 6; k++)
                    Box(root, new Vector3(0f, doorH * k / 6f, -hz - 0.44f), new Vector3(doorW, 0.06f, 0.06f), dark);

            // Roof: parapet ring and rooftop machinery.
            Box(root, new Vector3(0f, H + 0.05f, 0f), new Vector3(size.x - 0.4f, 0.1f, size.z - 0.4f), roofMat);
            float pH = style == Style.Bunker ? 0.6f : 1f;
            Box(root, new Vector3(0f, H + pH * 0.5f, hz), new Vector3(size.x + 0.4f, pH, 0.5f), concrete);
            Box(root, new Vector3(0f, H + pH * 0.5f, -hz), new Vector3(size.x + 0.4f, pH, 0.5f), concrete);
            Box(root, new Vector3(hx, H + pH * 0.5f, 0f), new Vector3(0.5f, pH, size.z + 0.4f), concrete);
            Box(root, new Vector3(-hx, H + pH * 0.5f, 0f), new Vector3(0.5f, pH, size.z + 0.4f), concrete);

            if (style == Style.Bunker)
            {
                // Sandbags along the roof edge and a small antenna.
                Cyl(root, new Vector3(hx * 0.6f, H + 2.5f, hz * 0.4f), new Vector3(0.15f, 2.5f, 0.15f), steel);
                return root;
            }

            int units = 1 + (int)(size.x * size.z / 120f);
            for (int i = 0; i < Mathf.Min(units, 4); i++)
            {
                float ux = (R() - 0.5f) * (size.x - 4f), uz = (R() - 0.5f) * (size.z - 4f);
                Box(root, new Vector3(ux, H + 0.8f, uz), new Vector3(2.2f, 1.4f, 1.6f), steel, false, R() * 20f);
                Box(root, new Vector3(ux, H + 1.55f, uz), new Vector3(1.2f, 0.12f, 1.2f), dark);
            }
            if (H > 12f)
            {
                // Water tank on legs and a mast with a red light.
                float tx = hx * 0.45f, tz = -hz * 0.35f;
                for (int i = 0; i < 4; i++)
                    Box(root, new Vector3(tx + (i % 2 - 0.5f) * 2f, H + 1.5f, tz + (i / 2 - 0.5f) * 2f), new Vector3(0.25f, 3f, 0.25f), dark);
                Cyl(root, new Vector3(tx, H + 4.4f, tz), new Vector3(3f, 1.4f, 3f), steel);
                Cyl(root, new Vector3(-hx * 0.5f, H + 4f, hz * 0.4f), new Vector3(0.2f, 4f, 0.2f), steel);
                Box(root, new Vector3(-hx * 0.5f, H + 8.1f, hz * 0.4f), new Vector3(0.45f, 0.45f, 0.45f), a.Glow(new Color(1f, 0.15f, 0.1f), 3f));
            }
            // Drainpipe down one side.
            Cyl(root, new Vector3(hx + 0.3f, H * 0.5f, hz * 0.6f), new Vector3(0.35f, H * 0.5f, 0.35f), dark);
            return root;
        }

        /// <summary>Dresses the Foundry's baked blocks and tower (built by ArenaBuilder) at load.</summary>
        public static void DressFoundry()
        {
            var arena = GameObject.Find("Arena");
            if (arena == null) return;
            var details = new GameObject("BuildingDetails").transform;
            int seed = 1;
            foreach (Transform t in arena.transform)
            {
                if (t.name != "Block" && t.name != "Tower") continue;
                Vector3 s = t.localScale;
                Style style = t.name == "Tower" || s.y >= 12f ? Style.Office : s.y >= 8f ? Style.Industrial : Style.Bunker;
                Dress(details, t.position, t.eulerAngles.y, s, style, seed++);
            }
            StaticBatchingUtility.Combine(details.gameObject);
        }
    }
}
