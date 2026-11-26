using System.Collections.Generic;
using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Holds the few material assets the game needs (so their shaders are included
    /// in builds) and hands out coloured copies, particle materials and generated
    /// textures at runtime. One instance lives in the scene.
    /// </summary>
    public class GameAssets : MonoBehaviour
    {
        public static GameAssets Instance { get; private set; }

        [Header("Material templates (created by the scene builder)")]
        public Material litTemplate;
        public Material emissiveTemplate;
        public Material particleAdditive;
        public Material particleAlpha;

        [Header("Arena materials (used to build the other maps at runtime)")]
        public Material floorMat;
        public Material wallMat;
        public Material buildingMat;
        public Material hazardMat;
        public Material glowMat;
        public Material[] containerMats = new Material[0];
        /// <summary>Photo-scanned ground per biome: 0 industrial, 1 dock, 2 desert, 3 snow, 4 city.</summary>
        public Material[] terrainMats = new Material[0];

        /// <summary>A tinted copy of an arena material (e.g. sand-coloured concrete for the canyon).</summary>
        public Material Tinted(Material source, Color color)
        {
            if (source == null) return Paint(color, 0f, 0.3f, false);
            string key = $"tint_{source.name}_{ColorUtility.ToHtmlStringRGBA(color)}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(source) { name = key, color = color };
            cache[key] = m;
            return m;
        }

        readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        Texture2D softDot;
        Texture2D panelTexture;
        Sprite circleSprite;
        Material additiveSoft;
        Material alphaSoft;

        void Awake()
        {
            Instance = this;
        }

        public static GameAssets Get()
        {
            if (Instance == null) Instance = FindAnyObjectByType<GameAssets>();
            return Instance;
        }

        // ---------- Lit materials ----------

        /// <summary>Painted or bare metal. Smoothness 0..1, metallic 0..1.</summary>
        public Material Paint(Color color, float metallic = 0.6f, float smoothness = 0.55f, bool panels = true)
        {
            string key = $"lit_{ColorUtility.ToHtmlStringRGBA(color)}_{metallic:0.00}_{smoothness:0.00}_{panels}";
            if (cache.TryGetValue(key, out var m)) return m;

            m = new Material(litTemplate) { name = key, color = color };
            bool pbr = m.HasProperty("_MaskTex");
            SetFloatIfPresent(m, "_Metallic", metallic);
            if (pbr)
            {
                // Textured template: smoothness scales the scratched-paint roughness map.
                SetFloatIfPresent(m, "_Glossiness", Mathf.Lerp(0.55f, 1.4f, smoothness));
                // Plain (unpanelled) parts get finer, fainter detail.
                if (!panels) { SetFloatIfPresent(m, "_Tiling", 1.6f); SetFloatIfPresent(m, "_BumpScale", 0.5f); }
            }
            else
            {
                SetFloatIfPresent(m, "_Glossiness", smoothness);
                SetFloatIfPresent(m, "_Smoothness", smoothness);
                if (panels)
                {
                    m.mainTexture = PanelTexture();
                    m.mainTextureScale = new Vector2(1f, 1f);
                }
            }
            cache[key] = m;
            return m;
        }

        /// <summary>Self-lit material for visors, engine glow, warning lights.</summary>
        public Material Glow(Color color, float intensity = 2f)
        {
            string key = $"glow_{ColorUtility.ToHtmlStringRGBA(color)}_{intensity:0.0}";
            if (cache.TryGetValue(key, out var m)) return m;

            m = new Material(emissiveTemplate) { name = key, color = color * 0.6f };
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * intensity);
            cache[key] = m;
            return m;
        }

        /// <summary>A darker, burnt version used for wrecks.</summary>
        public Material Burnt()
        {
            return Paint(new Color(0.07f, 0.065f, 0.06f), 0.3f, 0.15f, true);
        }

        static void SetFloatIfPresent(Material m, string prop, float value)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, value);
        }

        // ---------- Particles ----------

        public Material ParticleAdditive()
        {
            if (additiveSoft == null)
            {
                additiveSoft = new Material(particleAdditive) { name = "AdditiveSoft", mainTexture = SoftDot() };
                if (additiveSoft.HasProperty("_TintColor")) additiveSoft.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }
            return additiveSoft;
        }

        public Material ParticleAlpha()
        {
            if (alphaSoft == null)
            {
                alphaSoft = new Material(particleAlpha) { name = "AlphaSoft", mainTexture = SoftDot() };
                if (alphaSoft.HasProperty("_TintColor")) alphaSoft.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }
            return alphaSoft;
        }

        // Volumetric puffs baked offline (Tools/bake_billows.py): 4x4 sheets of lit smoke
        // and glowing fire, so blasts read as solid 3D clouds instead of flat soft dots.
        Material smokeBillow, fireBillow, fireGlow;

        static Material BillowMat(Material template, string texture, Color tint, Material fallback)
        {
            var tex = Resources.Load<Texture2D>("Fx/" + texture);
            if (tex == null || template == null) return fallback;
            var m = new Material(template) { name = texture, mainTexture = tex };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
            return m;
        }

        /// <summary>True when the baked puff sheets are present (materials below use 4x4 sheets).</summary>
        public bool HasBillows => Resources.Load<Texture2D>("Fx/SmokeBillows") != null;

        public Material SmokeBillow()
        {
            if (smokeBillow == null) smokeBillow = BillowMat(particleAlpha, "SmokeBillows", new Color(0.5f, 0.5f, 0.5f, 0.5f), ParticleAlpha());
            return smokeBillow;
        }

        /// <summary>Alpha-blended fire: bright when young, can darken into smoke as it cools.</summary>
        public Material FireBillow()
        {
            if (fireBillow == null) fireBillow = BillowMat(particleAlpha, "FireBillows", new Color(1f, 1f, 1f, 0.5f), ParticleAdditive());
            return fireBillow;
        }

        /// <summary>Additive glow with the same puff shapes, for the white-hot core.</summary>
        public Material FireGlow()
        {
            if (fireGlow == null) fireGlow = BillowMat(particleAdditive, "FireBillows", new Color(0.5f, 0.5f, 0.5f, 0.5f), ParticleAdditive());
            return fireGlow;
        }

        Material ringAdditive;
        Material scorchMat;

        /// <summary>Additive material with a soft ring: explosion shockwaves.</summary>
        public Material RingAdditive()
        {
            if (ringAdditive != null) return ringAdditive;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Ring" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.16f);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            ringAdditive = new Material(particleAdditive) { name = "RingAdditive", mainTexture = tex };
            if (ringAdditive.HasProperty("_TintColor")) ringAdditive.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            return ringAdditive;
        }

        /// <summary>Dark blotchy burn mark left on the ground by explosions.</summary>
        public Material Scorch()
        {
            if (scorchMat != null) return scorchMat;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Scorch" };
            var px = new Color32[size * size];
            var rng = new System.Random(11);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float edge = 0.75f + 0.12f * Mathf.Sin(ang * 7f) + 0.06f * Mathf.Sin(ang * 13f + 1f);
                float a = Mathf.Clamp01((edge - d) / 0.35f) * (0.75f + (float)rng.NextDouble() * 0.25f);
                px[y * size + x] = new Color32(12, 10, 9, (byte)(a * 235));
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            scorchMat = new Material(particleAlpha) { name = "Scorch", mainTexture = tex };
            if (scorchMat.HasProperty("_TintColor")) scorchMat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            return scorchMat;
        }

        // ---------- Generated textures ----------

        public Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int size = 64;
            softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            softDot.SetPixels32(pixels);
            softDot.Apply();
            return softDot;
        }

        /// <summary>Subtle panel lines and grime so flat boxes read as armour plates.</summary>
        public Texture2D PanelTexture()
        {
            if (panelTexture != null) return panelTexture;
            const int size = 128;
            panelTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Panels" };
            var pixels = new Color32[size * size];
            var rng = new System.Random(7);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = 0.92f + (float)rng.NextDouble() * 0.08f;
                bool seam = x % 64 == 0 || y % 64 == 0 || x % 64 == 1 || y % 64 == 1;
                bool rivet = (x % 64 == 5 || x % 64 == 59) && (y % 64 == 5 || y % 64 == 59);
                if (seam) v *= 0.55f;
                if (rivet) v *= 1.15f;
                float edgeGrime = Mathf.Min(Mathf.Min(x % 64, 64 - x % 64), Mathf.Min(y % 64, 64 - y % 64)) / 10f;
                v *= Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(edgeGrime));
                byte b = (byte)Mathf.Clamp(v * 255f, 0f, 255f);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
            panelTexture.SetPixels32(pixels);
            panelTexture.Apply(true);
            return panelTexture;
        }

        /// <summary>Concrete arena floor with a large grid, which really helps sell the scale.</summary>
        public Texture2D FloorTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Floor" };
            var pixels = new Color32[size * size];
            var rng = new System.Random(3);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = 0.85f + (float)rng.NextDouble() * 0.15f;
                float line = (x % 128 < 2 || y % 128 < 2) ? 0.6f : 1f;
                float fine = (x % 32 == 0 || y % 32 == 0) ? 0.9f : 1f;
                byte b = (byte)Mathf.Clamp(n * line * fine * 255f, 0, 255);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true);
            return tex;
        }

        public Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Circle" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) - size * 0.5f;
                float dy = (y + 0.5f) - size * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(size * 0.5f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return circleSprite;
        }
    }
}
