using UnityEngine;

namespace RobotArena
{
    /// <summary>
    /// Bloom, colour grading and vignette on the camera image.
    /// High quality: everything. Medium: grading and vignette only. Low: off.
    /// Switches itself off if the phone can't run the shader.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        public Material material;

        [Header("Bloom")]
        public float threshold = 0.9f;
        public float intensity = 0.55f;
        public int iterations = 4;

        [Header("Grading")]
        public float contrast = 1.08f;
        public float saturation = 1.04f;
        public Color tint = new Color(1.02f, 1f, 0.97f);
        public float vignette = 1.0f;

        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");

        readonly RenderTexture[] chain = new RenderTexture[8];
        bool supported;

        void Awake()
        {
            supported = material != null && material.shader != null && material.shader.isSupported;
            if (!supported) enabled = false;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var q = GameSettings.GraphicsQuality;
            if (!supported || q == GameSettings.Quality.Low)
            {
                Graphics.Blit(src, dst);
                return;
            }

            material.SetFloat(ContrastId, contrast);
            material.SetFloat(SaturationId, saturation);
            material.SetFloat(VignetteId, vignette);
            material.SetColor(TintId, tint);

            if (q == GameSettings.Quality.Medium)
            {
                Graphics.Blit(src, dst, material, 4);
                return;
            }

            material.SetFloat(ThresholdId, threshold);
            material.SetFloat(IntensityId, intensity);

            int w = src.width / 2, h = src.height / 2;
            int count = 0;
            var current = RenderTexture.GetTemporary(w, h, 0, src.format);
            chain[count++] = current;
            Graphics.Blit(src, current, material, 0);
            for (int i = 1; i < iterations; i++)
            {
                w /= 2; h /= 2;
                if (w < 8 || h < 8) break;
                var next = RenderTexture.GetTemporary(w, h, 0, src.format);
                chain[count++] = next;
                Graphics.Blit(current, next, material, 1);
                current = next;
            }
            for (int i = count - 2; i >= 0; i--)
            {
                Graphics.Blit(current, chain[i], material, 2);
                current = chain[i];
            }

            material.SetTexture(BloomTexId, current);
            Graphics.Blit(src, dst, material, 3);

            for (int i = 0; i < count; i++)
            {
                RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
        }
    }
}
