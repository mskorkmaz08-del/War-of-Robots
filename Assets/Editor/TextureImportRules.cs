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
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
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
