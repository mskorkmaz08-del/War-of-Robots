using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Visuals for the robots' superpowers: hex-grid energy shells (bubbles and
    /// curved walls) and ground shockwave rings.
    /// </summary>
    public static class PowerFx
    {
        static Texture2D hexTex;
        static Mesh sphereMesh;
        static Mesh ringMesh;

        /// <summary>A soft hexagon lattice: bright cell edges, a faint fill.</summary>
        static Texture2D HexTexture()
        {
            if (hexTex != null) return hexTex;
            const int w = 256;
            float size = w / 4f / Mathf.Sqrt(3f); // four cells across
            int h = Mathf.RoundToInt(3f * size); // two rows of cells tile vertically
            hexTex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "HexGrid" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float edge = HexEdgeDistance(x + 0.5f, y + 0.5f, size, w, h);
                float line = Mathf.Clamp01(1f - edge / 2.2f);
                float glow = Mathf.Clamp01(1f - edge / 10f) * 0.35f;
                float a = Mathf.Clamp01(line + glow + 0.06f);
                byte v = (byte)(a * 255f);
                px[y * w + x] = new Color32(v, v, v, v);
            }
            hexTex.SetPixels32(px);
            hexTex.Apply(true);
            return hexTex;
        }

        /// <summary>Distance in pixels from a point to the nearest hexagon edge (pointy-top grid, wrapped).</summary>
        static float HexEdgeDistance(float x, float y, float size, int w, int h)
        {
            float best = float.MaxValue, second = float.MaxValue;
            float dx = Mathf.Sqrt(3f) * size, dy = 1.5f * size;
            for (int row = -1; row <= 3; row++)
            for (int col = -1; col <= 5; col++)
            {
                float cx = col * dx + (row & 1) * dx * 0.5f;
                float cy = row * dy;
                float ex = x - cx, ey = y - cy;
                // Wrap so the texture tiles.
                ex -= Mathf.Round(ex / w) * w;
                ey -= Mathf.Round(ey / h) * h;
                float d = Mathf.Sqrt(ex * ex + ey * ey);
                if (d < best) { second = best; best = d; }
                else if (d < second) second = d;
            }
            return (second - best) * 0.5f;
        }

        static Material NewShellMaterial(Color color)
        {
            var assets = GameAssets.Get();
            if (assets == null) return null;
            var m = new Material(assets.ParticleAdditive()) { name = "PowerShell", mainTexture = HexTexture() };
            m.mainTextureScale = new Vector2(6f, 3f);
            SetTint(m, color, 0f);
            return m;
        }

        public static void SetTint(Material m, Color color, float strength)
        {
            if (m == null) return;
            var c = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.5f * Mathf.Clamp01(strength));
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
            else m.color = c;
        }

        static Mesh Sphere()
        {
            if (sphereMesh != null) return sphereMesh;
            const int lat = 18, lon = 32;
            var v = new Vector3[(lat + 1) * (lon + 1)];
            var uv = new Vector2[v.Length];
            var tris = new int[lat * lon * 6];
            for (int i = 0; i <= lat; i++)
            for (int j = 0; j <= lon; j++)
            {
                float th = Mathf.PI * i / lat, ph = 2f * Mathf.PI * j / lon;
                v[i * (lon + 1) + j] = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                uv[i * (lon + 1) + j] = new Vector2((float)j / lon, (float)i / lat);
            }
            int t = 0;
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j, b = a + lon + 1;
                tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
            }
            sphereMesh = new Mesh { name = "PowerSphere", vertices = v, uv = uv, triangles = tris };
            sphereMesh.RecalculateBounds();
            return sphereMesh;
        }

        /// <summary>A curved band in front of the robot: degrees of arc, radius, height (local units).</summary>
        static Mesh Arc(float degrees, float radius, float height)
        {
            const int seg = 24;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * (-degrees * 0.5f + degrees * i / seg);
                Vector3 p = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                v[i * 2] = p;
                v[i * 2 + 1] = p + Vector3.up * height;
                float u = (float)i / seg * degrees / 60f;
                uv[i * 2] = new Vector2(u, 0f);
                uv[i * 2 + 1] = new Vector2(u, height / radius * 0.8f);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                tris[i * 6] = a; tris[i * 6 + 1] = a + 1; tris[i * 6 + 2] = a + 2;
                tris[i * 6 + 3] = a + 2; tris[i * 6 + 4] = a + 1; tris[i * 6 + 5] = a + 3;
            }
            var m = new Mesh { name = "PowerArc", vertices = v, uv = uv, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A hex-grid energy sphere around the robot.</summary>
        public static EnergyShell Bubble(Transform parent, Vector3 localCenter, float radius, Color color)
        {
            var go = new GameObject("PowerBubble");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = Vector3.one * radius / Mathf.Max(0.01f, parent.lossyScale.x);
            go.AddComponent<MeshFilter>().sharedMesh = Sphere();
            return Finish(go, color);
        }

        /// <summary>A curved hex-grid shield standing in front of the robot.</summary>
        public static EnergyShell Wall(Transform parent, Vector3 localBase, float degrees, float radius, float height, Color color)
        {
            var go = new GameObject("PowerWall");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localBase;
            float s = 1f / Mathf.Max(0.01f, parent.lossyScale.x);
            go.AddComponent<MeshFilter>().sharedMesh = Arc(degrees, radius * s, height * s);
            return Finish(go, color);
        }

        static EnergyShell Finish(GameObject go, Color color)
        {
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = NewShellMaterial(color);
            var shell = go.AddComponent<EnergyShell>();
            shell.color = color;
            shell.rend = r;
            return shell;
        }

        static Mesh RingQuad()
        {
            if (ringMesh != null) return ringMesh;
            ringMesh = new Mesh
            {
                name = "RingQuad",
                vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            ringMesh.RecalculateBounds();
            return ringMesh;
        }

        /// <summary>A glowing ring racing out across the ground from a slam or a burst.</summary>
        public static void Shockwave(Vector3 pos, float radius, Color color, float time = 0.55f)
        {
            var assets = GameAssets.Get();
            if (assets == null) return;
            var go = new GameObject("Shockwave");
            go.transform.position = pos + Vector3.up * 0.4f;
            go.AddComponent<MeshFilter>().sharedMesh = RingQuad();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = new Material(assets.RingAdditive());
            var ring = go.AddComponent<ShockRing>();
            ring.radius = radius;
            ring.color = color;
            ring.time = time;
            ring.mat = r.sharedMaterial;
        }

        class ShockRing : MonoBehaviour
        {
            public float radius, time;
            public Color color;
            public Material mat;
            float age;

            void Update()
            {
                age += Time.deltaTime;
                float k = Mathf.Clamp01(age / time);
                float e = 1f - (1f - k) * (1f - k);
                transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 2.2f, e) * radius;
                SetTint(mat, color, (1f - k) * 1.6f);
                if (k >= 1f)
                {
                    Destroy(mat);
                    Destroy(gameObject);
                }
            }
        }
    }

    /// <summary>A shield shell: fades in, shimmers, flares where it is hit, and fades out.</summary>
    public class EnergyShell : MonoBehaviour
    {
        public Color color;
        public MeshRenderer rend;
        float strength, target = 1f, flare;
        float spin;

        public void Flare(float amount = 1f) { flare = Mathf.Max(flare, amount); }

        public void Dismiss() { target = 0f; }

        void Update()
        {
            strength = Mathf.MoveTowards(strength, target, Time.deltaTime * (target > 0f ? 3f : 2.2f));
            flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 4f);
            float shimmer = 0.55f + 0.12f * Mathf.Sin(Time.time * 7f) + 0.06f * Mathf.Sin(Time.time * 23f);
            var m = rend != null ? rend.sharedMaterial : null;
            PowerFx.SetTint(m, color, strength * (shimmer + flare));
            spin += Time.deltaTime * 0.04f;
            if (m != null) m.mainTextureOffset = new Vector2(spin, spin * 0.5f);
            if (target <= 0f && strength <= 0f)
            {
                if (m != null) Destroy(m);
                Destroy(gameObject);
            }
        }
    }
}
