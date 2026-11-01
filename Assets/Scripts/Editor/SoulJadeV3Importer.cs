using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace XiuXian.Editor
{
    /// <summary>Creates Sprite metadata from the original transparent atlas without changing its pixels.</summary>
    public static class SoulJadeV3Importer
    {
        private const string AtlasPath = "Assets/Art/Resources/UI/QuietInkV3/SoulJade/icons.png";
        private const string ReportPath = "ArtSource/QuietInkV3/SoulJade/import-report.json";
        private const int Columns = 5;
        private const int Rows = 2;
        private const float AlphaThreshold = .15f;
        private const float PaddingRatio = .01f;

        [Serializable]
        private sealed class PixelRect
        {
            public int x, y, width, height;

            public PixelRect(RectInt rect)
            {
                x = rect.x; y = rect.y; width = rect.width; height = rect.height;
            }
        }

        [Serializable]
        private sealed class SliceReport
        {
            public string name;
            public int column, row_from_top, padding_pixels;
            public PixelRect cell, alpha_bounds, sprite_rect;
        }

        [Serializable]
        private sealed class ImportReport
        {
            public string atlas = AtlasPath;
            public string coordinate_origin = "bottom-left";
            public string name_order = "top-left, then left-to-right, then next row";
            public string source_sha256;
            public bool source_bytes_unchanged;
            public int width, height, columns = Columns, rows = Rows, sprite_count;
            public float alpha_threshold = AlphaThreshold, padding_ratio_per_side = PaddingRatio;
            public SliceReport[] sprites;
        }

        [MenuItem("仙途/美术/导入新版魂玉图集")]
        public static void ImportFromMenu()
        {
            Debug.Log(Import());
        }

        public static string Import()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(project, AtlasPath);
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("缺少透明魂玉图集", sourcePath);

            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            var report = BuildPlan(sourceBytes, out var sprites);
            report.source_sha256 = Hash(sourceBytes);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("找不到魂玉纹理导入器：" + AtlasPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.spriteBorder = Vector4.zero;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(report.width, report.height)));
            foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
                importer.ClearPlatformTextureSettings(platform);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
#pragma warning disable 0618
            importer.spritesheet = sprites;
#pragma warning restore 0618
            importer.SaveAndReimport();

            VerifyImport(report, sprites);
            report.source_bytes_unchanged = Hash(File.ReadAllBytes(sourcePath)) == report.source_sha256;
            if (!report.source_bytes_unchanged) throw new IOException("导入期间魂玉源 PNG 字节发生变化。");
            string outputPath = Path.Combine(project, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            return "SoulJade V3: verified 10 sprites; source PNG unchanged; " + ReportPath;
        }

        private static ImportReport BuildPlan(byte[] sourceBytes, out SpriteMetaData[] sprites)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(image, sourceBytes, false))
                    throw new InvalidDataException("无法解码魂玉 PNG。");
                int width = image.width, height = image.height;
                if (width < Columns || height < Rows || Mathf.Max(width, height) > 16384)
                    throw new InvalidDataException("魂玉图集尺寸必须足够容纳 5×2 单元，且最大边不超过 16384。");

                var pixels = image.GetPixels32();
                int alphaMin = Mathf.CeilToInt(AlphaThreshold * 255f);
                sprites = new SpriteMetaData[Columns * Rows];
                var report = new ImportReport {
                    width = width, height = height, sprites = new SliceReport[sprites.Length]
                };
                for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
                {
                    int index = row * Columns + column;
                    string name = "soul_jade_" + index.ToString("00");
                    // Integer partitioning also handles PNG dimensions not divisible by five.
                    int left = column * width / Columns;
                    int right = (column + 1) * width / Columns;
                    int bottom = height - (row + 1) * height / Rows;
                    int top = height - row * height / Rows;
                    var cell = new RectInt(left, bottom, right - left, top - bottom);
                    int minX = right, maxX = left - 1, minY = top, maxY = bottom - 1;
                    for (int y = bottom; y < top; y++)
                    for (int x = left; x < right; x++)
                    {
                        if (pixels[y * width + x].a < alphaMin) continue;
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                    if (maxX < minX || maxY < minY)
                        throw new InvalidDataException(name + " 所在单元没有 alpha >= 0.15 的图像。");
                    var bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                    int visualSize = Mathf.Max(bounds.width, bounds.height);
                    int padding = Mathf.Max(1, Mathf.CeilToInt(visualSize * PaddingRatio));
                    int size = visualSize + padding * 2;
                    var rect = new RectInt(
                        Mathf.FloorToInt((bounds.xMin + bounds.xMax - size) * .5f),
                        Mathf.FloorToInt((bounds.yMin + bounds.yMax - size) * .5f), size, size);
                    if (rect.xMin < cell.xMin || rect.xMax > cell.xMax ||
                        rect.yMin < cell.yMin || rect.yMax > cell.yMax)
                        throw new InvalidDataException(name + " 的居中方形切片及留白超出单元边界；请检查图集间距和透明背景。");

                    sprites[index] = new SpriteMetaData {
                        name = name,
                        rect = new Rect(rect.x, rect.y, rect.width, rect.height),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = new Vector2(.5f, .5f), border = Vector4.zero
                    };
                    report.sprites[index] = new SliceReport {
                        name = name, column = column, row_from_top = row, padding_pixels = padding,
                        cell = new PixelRect(cell), alpha_bounds = new PixelRect(bounds), sprite_rect = new PixelRect(rect)
                    };
                }
                return report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void VerifyImport(ImportReport report, SpriteMetaData[] expected)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (texture == null || texture.width != report.width || texture.height != report.height || texture.isReadable)
                throw new InvalidOperationException("魂玉纹理尺寸或非可读导入设置不符。");
            var actual = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AtlasPath))
                if (asset is Sprite sprite) actual.Add(sprite.name, sprite);
            if (actual.Count != expected.Length)
                throw new InvalidOperationException("魂玉 Sprite 应为 10 个，实际为 " + actual.Count + "。");
            foreach (var slice in expected)
            {
                if (!actual.TryGetValue(slice.name, out var sprite) || sprite.rect != slice.rect ||
                    sprite.border != Vector4.zero || sprite.pivot != slice.rect.size * .5f || sprite.vertices.Length != 4)
                    throw new InvalidOperationException("魂玉 Sprite 的名称、矩形、居中锚点或 FullRect 网格不符：" + slice.name);
            }
            report.sprite_count = actual.Count;
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
