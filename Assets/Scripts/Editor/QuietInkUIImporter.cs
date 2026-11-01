using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using XiuXian.Core;

namespace XiuXian.Editor
{
    /// <summary>Imports the approved composable QuietInk assets without changing source pixels or gameplay state.</summary>
    public static class QuietInkUIImporter
    {
        private const string SourceFolder = "ArtSource/QuietInkUIV2";
        private const string Destination = "Assets/Art/Resources/UI/QuietInkV2";
        private const int AlphaThreshold = 16;
        private const int TrimPadding = 4;
        private static readonly HashSet<string> TrimmedCharacters = new HashSet<string>(StringComparer.Ordinal)
        {
            "hero-character", "hero-character-female", "battle-player",
            "battle-enemy-mage", "battle-enemy-swordsman"
        };
        private static readonly HashSet<string> SlicedControls = new HashSet<string>(StringComparer.Ordinal)
        {
            "button_primary", "button_secondary", "button_disabled", "selector_row",
            "dialog_panel", "info_card", "equipment_slot", "equipment_slot_selected",
            "tooltip_panel", "health_track", "health_fill"
        };

        [Serializable]
        private sealed class Layout
        {
            public string coordinate_origin;
            public Slice[] sprites;
            public Slice[] items;
        }
        [Serializable]
        private sealed class Slice
        {
            public string atlas;
            public string name;
            public float x, y, width, height;
            public float[] proposed_border_lbrt;
        }
        private sealed class ImagePlan
        {
            public string Source, Filename, Name, Target;
            public int Width, Height;
            public bool Trimmed;
            public readonly List<SpriteMetaData> Sprites = new List<SpriteMetaData>();
        }

        [MenuItem("仙途/美术/导入彩墨界面资源")]
        public static void ImportFromMenu()
        {
            Debug.Log(Import());
        }

        /// <summary>Explicit import only. Returns and saves a report after every image and Sprite is verified.</summary>
        public static string Import()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string source = Path.Combine(project, SourceFolder);
            var plans = BuildPlan(source);
            var report = new StringBuilder();
            int spriteCount = 0, atlasCount = 0, trimCount = 0, borderCount = 0, copiedCount = 0;

            // Validate all dimensions, names, rectangles and borders before copying any resource.
            Directory.CreateDirectory(Path.Combine(project, Destination));
            try
            {
                for (int index = 0; index < plans.Count; index++)
                {
                    var plan = plans[index];
                    EditorUtility.DisplayProgressBar("导入彩墨界面资源", plan.Filename, index / (float)plans.Count);
                    string targetFile = Path.Combine(project, plan.Target);
                    bool copied = !FilesEqual(plan.Source, targetFile);
                    if (copied)
                    {
                        File.Copy(plan.Source, targetFile, true);
                        copiedCount++;
                    }
                    if (!FilesEqual(plan.Source, targetFile))
                        throw new IOException("资源复制校验失败：" + plan.Filename);

                    AssetDatabase.ImportAsset(plan.Target, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(plan.Target) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("找不到纹理导入器：" + plan.Target);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.spritePixelsPerUnit = 100;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(plan.Width, plan.Height)));
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    // Existing platform overrides must not silently rescale or compress atlas coordinates.
                    foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
                        importer.ClearPlatformTextureSettings(platform);
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    importer.spriteImportMode = plan.Sprites.Count > 0 ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                    importer.spriteBorder = Vector4.zero;
                    importer.spritePivot = new Vector2(.5f, .5f);
#pragma warning disable 0618
                    importer.spritesheet = plan.Sprites.ToArray();
#pragma warning restore 0618
                    importer.SaveAndReimport();

                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(plan.Target);
                    if (texture == null || texture.width != plan.Width || texture.height != plan.Height)
                        throw new InvalidOperationException("导入后纹理尺寸发生改变：" + plan.Filename);
                    var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(plan.Target))
                        if (asset is Sprite sprite) sprites.Add(sprite.name, sprite);
                    int expected = Mathf.Max(1, plan.Sprites.Count);
                    if (sprites.Count != expected)
                        throw new InvalidOperationException(plan.Filename + " 的 Sprite 数量不符，预期 " + expected + "，实际 " + sprites.Count);
                    if (plan.Sprites.Count == 0)
                    {
                        if (!sprites.ContainsKey(plan.Name))
                            throw new InvalidOperationException("单图 Sprite 名称不符：" + plan.Name);
                    }
                    else
                    {
                        foreach (var slice in plan.Sprites)
                        {
                            if (!sprites.TryGetValue(slice.name, out var sprite) ||
                                sprite.rect != slice.rect || sprite.border != slice.border)
                                throw new InvalidOperationException("Sprite 矩形或九宫格边界不符：" + slice.name);
                            if (slice.border.sqrMagnitude > 0) borderCount++;
                        }
                    }

                    spriteCount += sprites.Count;
                    if (plan.Trimmed) trimCount++;
                    else if (plan.Sprites.Count > 0) atlasCount++;
                    report.Append(plan.Filename).Append(" => ").Append(sprites.Count).Append(" sprites; ")
                        .Append(plan.Width).Append('x').Append(plan.Height)
                        .Append(copied ? "; copied byte-for-byte" : "; original bytes unchanged");
                    if (plan.Trimmed) report.Append("; alpha >= ").Append(AlphaThreshold)
                        .Append(" rect ").Append(plan.Sprites[0].rect).Append("; padding ").Append(TrimPadding);
                    report.AppendLine();
                }
                AssetDatabase.SaveAssets();
                QuietInkUI.ClearCache();
                string result = "QuietInk UI V2 import completed\n" +
                    "PNG: " + plans.Count + "; atlases: " + atlasCount +
                    "; standalone images: " + (plans.Count - atlasCount) +
                    "; sprites: " + spriteCount + "; trimmed character rectangles: " + trimCount +
                    "; nine-slice controls: " + borderCount + "; copied PNG: " + copiedCount + "\n" +
                    "Source PNG bytes unchanged. Sprite bounds only; no pixel editing.\n" +
                    "Import checks do not replace runtime visual review.\n\n" + report;
                File.WriteAllText(Path.Combine(source, "unity-import-report.txt"), result, new UTF8Encoding(false));
                return result;
            }
            finally
            {
                QuietInkUI.ClearCache();
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<ImagePlan> BuildPlan(string source)
        {
            string assetFolder = Path.Combine(source, "Assets");
            string metadataPath = Path.Combine(source, "atlas-layout.json");
            if (!Directory.Exists(assetFolder)) throw new DirectoryNotFoundException(assetFolder);
            if (!File.Exists(metadataPath)) throw new FileNotFoundException("缺少图集元数据", metadataPath);
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(metadataPath));
            if (layout == null) throw new InvalidDataException("无法读取图集元数据");
            if (!string.IsNullOrEmpty(layout.coordinate_origin) && layout.coordinate_origin != "top-left")
                throw new InvalidDataException("图集坐标必须从左上角计算");
            var slices = layout.sprites ?? layout.items;
            if (slices == null || slices.Length == 0) throw new InvalidDataException("图集元数据没有 Sprite 切片");

            var grouped = new Dictionary<string, List<Slice>>(StringComparer.Ordinal);
            var allNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slice in slices)
            {
                if (slice == null || string.IsNullOrWhiteSpace(slice.name) || string.IsNullOrWhiteSpace(slice.atlas) ||
                    Path.GetFileName(slice.atlas) != slice.atlas)
                    throw new InvalidDataException("图集切片缺少合法 atlas/name");
                if (!allNames.Add(slice.name)) throw new InvalidDataException("重复 Sprite 名称：" + slice.name);
                if (!grouped.TryGetValue(slice.atlas, out var entries))
                    grouped.Add(slice.atlas, entries = new List<Slice>());
                entries.Add(slice);
            }

            string[] paths = Directory.GetFiles(assetFolder, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);
            if (paths.Length == 0) throw new InvalidDataException("没有可导入的 PNG");
            var plans = new List<ImagePlan>();
            foreach (var path in paths)
            {
                var plan = new ImagePlan {
                    Source = path, Filename = Path.GetFileName(path),
                    Name = Path.GetFileNameWithoutExtension(path)
                };
                plan.Target = Destination + "/" + plan.Filename;
                ReadPngDimensions(path, out plan.Width, out plan.Height);
                if (grouped.TryGetValue(plan.Filename, out var entries))
                {
                    foreach (var slice in entries)
                    {
                        ValidateRect(slice, plan.Width, plan.Height);
                        var border = ReadBorder(slice);
                        plan.Sprites.Add(new SpriteMetaData {
                            name = slice.name,
                            rect = new Rect(slice.x, plan.Height - slice.y - slice.height, slice.width, slice.height),
                            alignment = (int)SpriteAlignment.Custom,
                            pivot = new Vector2(.5f, .5f),
                            border = border
                        });
                    }
                    grouped.Remove(plan.Filename);
                }
                else
                {
                    if (!allNames.Add(plan.Name)) throw new InvalidDataException("单图与图集 Sprite 名称冲突：" + plan.Name);
                    if (TrimmedCharacters.Contains(plan.Name))
                    {
                        plan.Trimmed = true;
                        plan.Sprites.Add(new SpriteMetaData {
                            name = plan.Name, rect = MeasureAlphaBounds(path),
                            alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, .5f)
                        });
                    }
                }
                plans.Add(plan);
            }
            if (grouped.Count > 0)
                throw new InvalidDataException("元数据引用了不存在的 PNG：" + string.Join(", ", grouped.Keys));
            return plans;
        }

        private static void ValidateRect(Slice slice, int width, int height)
        {
            foreach (float value in new[] { slice.x, slice.y, slice.width, slice.height })
                if (float.IsNaN(value) || float.IsInfinity(value) || value != Mathf.Floor(value))
                    throw new InvalidDataException("切片必须使用整数像素：" + slice.name);
            if (slice.x < 0 || slice.y < 0 || slice.width <= 0 || slice.height <= 0 ||
                slice.x + slice.width > width || slice.y + slice.height > height)
                throw new InvalidDataException("切片超出原始 PNG：" + slice.name);
        }

        private static Vector4 ReadBorder(Slice slice)
        {
            var values = slice.proposed_border_lbrt;
            if (values == null || values.Length == 0)
            {
                if (SlicedControls.Contains(slice.name))
                    throw new InvalidDataException("可拉伸控件缺少 proposed_border_lbrt：" + slice.name);
                return Vector4.zero;
            }
            if (values.Length != 4) throw new InvalidDataException("九宫格必须为 left,bottom,right,top：" + slice.name);
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value != Mathf.Floor(value))
                    throw new InvalidDataException("九宫格必须使用非负整数像素：" + slice.name);
            if (values[0] + values[2] >= slice.width || values[1] + values[3] >= slice.height)
                throw new InvalidDataException("九宫格边界没有留下可拉伸中心：" + slice.name);
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

        private static Rect MeasureAlphaBounds(string path)
        {
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(path), false))
                    throw new InvalidDataException("无法解码角色 PNG：" + Path.GetFileName(path));
                Color32[] pixels = decoded.GetPixels32();
                int width = decoded.width, height = decoded.height;
                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (pixels[y * width + x].a >= AlphaThreshold)
                        {
                            minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                            maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                        }
                if (maxX < minX) throw new InvalidDataException("角色 PNG 没有可见像素：" + Path.GetFileName(path));
                minX = Mathf.Max(0, minX - TrimPadding); minY = Mathf.Max(0, minY - TrimPadding);
                maxX = Mathf.Min(width - 1, maxX + TrimPadding); maxY = Mathf.Min(height - 1, maxY + TrimPadding);
                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        private static void ReadPngDimensions(string path, out int width, out int height)
        {
            byte[] header = new byte[24];
            using (var input = File.OpenRead(path))
                if (input.Read(header, 0, header.Length) != header.Length)
                    throw new InvalidDataException("PNG 文件头不完整：" + path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; i++)
                if (header[i] != signature[i]) throw new InvalidDataException("文件不是 PNG：" + path);
            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
                throw new InvalidDataException("PNG 尺寸不受支持：" + path);
        }

        private static bool FilesEqual(string source, string destination)
        {
            if (!File.Exists(destination) || new FileInfo(source).Length != new FileInfo(destination).Length)
                return false;
            using (var sha = SHA256.Create())
            {
                byte[] a, b;
                using (var input = File.OpenRead(source)) a = sha.ComputeHash(input);
                using (var input = File.OpenRead(destination)) b = sha.ComputeHash(input);
                for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
                return true;
            }
        }
    }
}
