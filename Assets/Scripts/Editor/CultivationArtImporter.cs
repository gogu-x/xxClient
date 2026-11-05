using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XiuXian.Editor
{
    public static class CultivationArtImporter
    {
        private const string Root = "Assets/Art/Resources/UI/QuietInkV3/Cultivation/";

        [MenuItem("仙途/美术/导入修炼资源")]
        public static void Import()
        {
            Configure(Root + "meditation.png", null);
            byte[] bytes = File.ReadAllBytes(Root + "pills.png");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes)) throw new InvalidOperationException("无法读取丹药图集");
                var pixels = texture.GetPixels32();
                var slices = new List<SpriteMetaData>();
                for (int i = 0; i < 3; i++)
                {
                    int left = i * texture.width / 3, right = (i + 1) * texture.width / 3;
                    int minX = right, maxX = left, minY = texture.height, maxY = 0;
                    for (int y = 0; y < texture.height; y++)
                        for (int x = left; x < right; x++)
                            if (pixels[y * texture.width + x].a >= 38)
                            { minX = Math.Min(x, minX); maxX = Math.Max(x, maxX); minY = Math.Min(y, minY); maxY = Math.Max(y, maxY); }
                    if (minX > maxX || minY > maxY) throw new InvalidOperationException("丹药图集存在空白格");
                    int side = Mathf.CeilToInt(Mathf.Max(maxX - minX + 1, maxY - minY + 1) * 1.02f);
                    int x0 = Mathf.RoundToInt((minX + maxX + 1 - side) * .5f);
                    int y0 = Mathf.RoundToInt((minY + maxY + 1 - side) * .5f);
                    if (x0 < left || x0 + side > right || y0 < 0 || y0 + side > texture.height)
                        throw new InvalidOperationException("丹药切片留白不足");
                    slices.Add(new SpriteMetaData { name = "cultivation_pill_" + (i + 1), rect = new Rect(x0, y0, side, side), alignment = 0, pivot = new Vector2(.5f, .5f) });
                }
                Configure(Root + "pills.png", slices.ToArray());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            Debug.Log("[CultivationArt] 已导入静修人物与三枚等比例丹药图标");
        }

        private static void Configure(string path, SpriteMetaData[] sprites)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("纹理导入器不存在：" + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sprites == null ? SpriteImportMode.Single : SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
#pragma warning disable 0618
            if (sprites != null) importer.spritesheet = sprites;
#pragma warning restore 0618
            importer.SaveAndReimport();
        }
    }
}
