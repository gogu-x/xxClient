using UnityEditor;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// 自动将 Assets/Art/ 目录下的图片导入为 Sprite (2D and UI) 类型，
    /// 避免每张新增的美术资源都要手动在 Inspector 里改 Texture Type。
    /// </summary>
    public class ArtTextureImportProcessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            var importer = (TextureImporter)assetImporter;

            bool isCharacterTexture =
                normalizedPath.Contains("Assets/Resources/Characters/MawTextures/") ||
                normalizedPath.Contains("Assets/Resources/Characters/VampireTextures/");
            if (isCharacterTexture)
            {
                bool isNormal = normalizedPath.EndsWith("MAW_normal.png") ||
                                normalizedPath.EndsWith("Vampire_normal.png");
                bool isSpecular = normalizedPath.EndsWith("MAW_specular.png") ||
                                  normalizedPath.EndsWith("Vampire_specular.png");
                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !isNormal && !isSpecular;
                importer.mipmapEnabled = true;
                return;
            }

            if (!normalizedPath.Contains("Assets/Art/"))
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            // 新水墨图集保留显式切片，避免导入后被重置为整张单 Sprite。
            if (!normalizedPath.StartsWith("Assets/Art/Resources/UI/ImmortalV3/") &&
                !normalizedPath.StartsWith("Assets/Art/Resources/UI/QuietInkV1/") &&
                !normalizedPath.StartsWith("Assets/Art/Resources/UI/QuietInkV2/") &&
                !normalizedPath.StartsWith("Assets/Art/Resources/UI/QuietInkV3/"))
                importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;

            if (normalizedPath.Contains("Assets/Art/Resources/UI/ImmortalV4/"))
            {
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.filterMode = UnityEngine.FilterMode.Trilinear;
            }
            if (normalizedPath.Contains("Assets/Art/Resources/UI/SoulJade/DesignElements/"))
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                // Detailed transparent artwork is displayed much smaller than its source.
                importer.mipmapEnabled = true;
                importer.filterMode = UnityEngine.FilterMode.Trilinear;
            }
        }
    }
}


