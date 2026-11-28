using UnityEditor;
using UnityEngine;

namespace RobotArena.EditorTools
{
    /// <summary>
    /// Import settings for the PBR textures and the sky photo: masks and normal maps are
    /// data (linear, not sRGB), surface textures tile, and everything stays phone-sized.
    /// </summary>
    public class TextureImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            var ti = (TextureImporter)assetImporter;
            if (p.StartsWith("Assets/Textures/Pbr/"))
            {
                bool data = p.EndsWith("_mask.jpg") || p.EndsWith("_n.jpg");
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = !data;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                // Terrain roughness/AO masks are soft: half size keeps the download small.
                ti.maxTextureSize = p.Contains("/terrain_") && p.EndsWith("_mask.jpg") ? 512 : 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
            }
            else if (p.StartsWith("Assets/Resources/Fx/"))
            {
                // Particle puff sheets: keep alpha, no tiling bleed between cells.
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
            }
            else if (p.StartsWith("Assets/Resources/UI/"))
            {
                // UI art such as the game logo: a sprite with clean transparent edges.
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                // The full title logo is wide and must stay sharp on big screens.
                ti.maxTextureSize = p.EndsWith("LogoTitle.png") ? 2048 : 512;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            else if (p == "Assets/Art/AppIcon.png")
            {
                // Home-screen icon: Unity scales it to every icon size itself.
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 1024;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else if (p == "Assets/Textures/Sky.jpg")
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 2048;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }
    }
}
