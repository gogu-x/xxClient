using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using A = XiuXian.RealmMap.RealmMapArt;

namespace XiuXian.RealmMap.Editor
{
    /// <summary>Optional package importer. Operates only on Assets/RealmMapV1, never game scenes or backend state.</summary>
    public static class RealmMapArtImporter
    {
        public const string Destination = "Assets/RealmMapV1/Resources/UI/RealmMapV1";
        public const string PrefabFolder = "Assets/RealmMapV1/Prefabs";
        private static readonly string[] Metadata = { "entrances-layout.json", "ui-layout.json", "lobby-support-layout.json" };

        [Serializable] private sealed class SliceLayout { public string coordinate_origin; public Slice[] sprites; }
        [Serializable] private sealed class Slice
        {
            public string atlas, name;
            public float x, y, width, height;
            public float[] proposed_border_lbrt, pivot, pivot_normalized;
        }
        private sealed class Plan
        {
            public string path, filename, target;
            public int width, height;
            public readonly List<SpriteMetaData> sprites = new List<SpriteMetaData>();
        }
        [Serializable] private sealed class LobbyLayout
        {
            public float design_width = 941, design_height = 1672;
            public LobbyLayer[] layers;
            public LobbyText[] texts;
        }
        [Serializable] private sealed class LobbyLayer
        {
            public string name, asset;
            public float x, y, width, height, opacity = 1;
            public bool preserveAspect;
        }
        [Serializable] private sealed class LobbyText
        {
            public string name, text, color, alignment;
            public float x, y, width, height, fontSize = 28;
        }

        [MenuItem("仙途/秘境资源包/导入资源并生成组合预制体")]
        public static void ImportFromMenu() { Debug.Log(Import()); }

        public static string Import()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string source = Path.Combine(project, "ArtSource/RealmMapV1");
            if (!Directory.Exists(Path.Combine(source, "Assets"))) source = Path.Combine(project, Destination);
            string assetFolder = Directory.Exists(Path.Combine(source, "Assets")) ? Path.Combine(source, "Assets") : source;
            var plans = BuildPlan(source, assetFolder);
            Directory.CreateDirectory(Path.Combine(project, Destination));
            var report = new StringBuilder();
            int total = 0;
            try
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    var plan = plans[i];
                    EditorUtility.DisplayProgressBar("秘境资源包", plan.filename, i / (float)plans.Count);
                    string targetFile = Path.Combine(project, plan.target);
                    if (!EqualBytes(plan.path, targetFile)) File.Copy(plan.path, targetFile, true);
                    if (!EqualBytes(plan.path, targetFile)) throw new IOException("PNG 复制校验失败：" + plan.filename);
                    AssetDatabase.ImportAsset(plan.target, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(plan.target) as TextureImporter;
                    if (importer == null) throw new InvalidDataException("PNG 导入器缺失：" + plan.filename);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = plan.sprites.Count > 0 ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100;
                    importer.spritePivot = new Vector2(.5f, .5f);
                    importer.spriteBorder = Vector4.zero;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 4096;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
                        importer.ClearPlatformTextureSettings(platform);
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
#pragma warning disable 0618
                    importer.spritesheet = plan.sprites.ToArray();
#pragma warning restore 0618
                    importer.SaveAndReimport();
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(plan.target);
                    if (texture == null || texture.width != plan.width || texture.height != plan.height)
                        throw new InvalidDataException("导入改变了原始分辨率：" + plan.filename);
                    var actual = new Dictionary<string, Sprite>();
                    foreach (var item in AssetDatabase.LoadAllAssetsAtPath(plan.target))
                        if (item is Sprite sprite) actual.Add(sprite.name, sprite);
                    if (actual.Count != Mathf.Max(1, plan.sprites.Count)) throw new InvalidDataException("Sprite 数量错误：" + plan.filename);
                    foreach (var slice in plan.sprites)
                    {
                        if (!actual.TryGetValue(slice.name, out var sprite) || sprite.rect != slice.rect || sprite.border != slice.border ||
                            Vector2.Distance(sprite.pivot, Vector2.Scale(slice.pivot, slice.rect.size)) > .05f)
                            throw new InvalidDataException("切片/边框/枢轴校验失败：" + slice.name);
                    }
                    total += actual.Count;
                    report.AppendLine(plan.filename + " | " + plan.width + "x" + plan.height + " | sprites " + actual.Count + " | PNG SHA256 unchanged");
                }
                foreach (string filename in new[] { "world-layout.json", "lobby-layout.json" })
                {
                    string original = Path.Combine(source, filename), target = Path.Combine(project, Destination, filename);
                    if (!File.Exists(original)) throw new FileNotFoundException("缺少组合布局", original);
                    if (!EqualBytes(original, target)) File.Copy(original, target, true);
                    AssetDatabase.ImportAsset(Destination + "/" + filename, ImportAssetOptions.ForceSynchronousImport);
                }
                AssetDatabase.SaveAssets(); A.ClearCache();
                ValidateWorld();
                string prefabs = BuildPreviewPrefabs();
                string result = "RealmMapV1 import verified: " + plans.Count + " PNG, " + total + " Sprite.\n" +
                    "Source pixel bytes preserved; no upscale or raster editing. Runtime visual review is separate.\n" +
                    report + prefabs;
                File.WriteAllText(Path.Combine(source, "unity-import-report.txt"), result, new UTF8Encoding(false));
                return result;
            }
            finally { A.ClearCache(); EditorUtility.ClearProgressBar(); }
        }

        private static List<Plan> BuildPlan(string source, string assetFolder)
        {
            var slices = new Dictionary<string, List<Slice>>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string filename in Metadata)
            {
                string path = Path.Combine(source, filename);
                if (!File.Exists(path)) throw new FileNotFoundException("缺少图集元数据", path);
                var data = JsonUtility.FromJson<SliceLayout>(File.ReadAllText(path));
                if (data?.sprites == null || (data.coordinate_origin != null && data.coordinate_origin != "top-left"))
                    throw new InvalidDataException("图集元数据必须采用 top-left 像素坐标：" + filename);
                foreach (var slice in data.sprites)
                {
                    if (string.IsNullOrEmpty(slice.name) || string.IsNullOrEmpty(slice.atlas) || Path.GetFileName(slice.atlas) != slice.atlas || !names.Add(slice.name))
                        throw new InvalidDataException("Sprite 名称重复或路径无效：" + slice.name);
                    if (!slices.TryGetValue(slice.atlas, out var group)) slices.Add(slice.atlas, group = new List<Slice>());
                    group.Add(slice);
                }
            }
            var plans = new List<Plan>();
            string[] files = Directory.GetFiles(assetFolder, "*.png"); Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                var plan = new Plan { path = file, filename = Path.GetFileName(file) };
                plan.target = Destination + "/" + plan.filename;
                PngDimensions(file, out plan.width, out plan.height);
                if (slices.TryGetValue(plan.filename, out var group))
                {
                    foreach (var slice in group)
                    {
                        Rect rect = new Rect(slice.x, plan.height - slice.y - slice.height, slice.width, slice.height);
                        if (slice.width <= 0 || slice.height <= 0 || slice.x < 0 || slice.y < 0 || slice.x + slice.width > plan.width || slice.y + slice.height > plan.height)
                            throw new InvalidDataException("切片越界：" + slice.name);
                        foreach (float value in new[] { slice.x, slice.y, slice.width, slice.height })
                            if (float.IsNaN(value) || float.IsInfinity(value) || value != Mathf.Floor(value)) throw new InvalidDataException("切片必须为整数像素：" + slice.name);
                        foreach (var other in plan.sprites)
                            if (rect.Overlaps(other.rect)) throw new InvalidDataException("切片重叠：" + slice.name + "/" + other.name);
                        float[] b = slice.proposed_border_lbrt ?? new float[4];
                        if (b.Length != 4 || b[0] < 0 || b[1] < 0 || b[2] < 0 || b[3] < 0 || b[0] + b[2] >= slice.width || b[1] + b[3] >= slice.height)
                            throw new InvalidDataException("九宫格边框无效：" + slice.name);
                        foreach (float edge in b)
                            if (float.IsNaN(edge) || float.IsInfinity(edge) || edge != Mathf.Floor(edge))
                                throw new InvalidDataException("九宫格边框必须为有限整数像素：" + slice.name);
                        float[] p = slice.pivot ?? slice.pivot_normalized ?? new[] { .5f, .5f };
                        if (p.Length != 2 || p[0] < 0 || p[1] < 0 || p[0] > 1 || p[1] > 1)
                            throw new InvalidDataException("Sprite 枢轴无效：" + slice.name);
                        foreach (float axis in p)
                            if (float.IsNaN(axis) || float.IsInfinity(axis)) throw new InvalidDataException("Sprite 枢轴必须为有限值：" + slice.name);
                        plan.sprites.Add(new SpriteMetaData { name = slice.name, rect = rect, border = new Vector4(b[0], b[1], b[2], b[3]),
                            alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(p[0], p[1]) });
                    }
                    slices.Remove(plan.filename);
                }
                else if (!names.Add(Path.GetFileNameWithoutExtension(file))) throw new InvalidDataException("独立图命名与切片冲突：" + file);
                plans.Add(plan);
            }
            if (slices.Count > 0 || plans.Count == 0) throw new InvalidDataException("元数据引用的 PNG 缺失");
            return plans;
        }

        private static void ValidateWorld()
        {
            var world = A.LoadLayout();
            if (world == null || world.map_width <= 0 || world.map_height <= 0 || world.nodes == null) throw new InvalidDataException("地图布局无效");
            var names = new HashSet<string>();
            foreach (var node in world.nodes)
            {
                float x = world.normalized ? node.x * world.map_width : node.x;
                float y = world.normalized ? node.y * world.map_height : node.y;
                if (string.IsNullOrEmpty(node.id) || !names.Add(node.id) || A.Sprite(node.art) == null || node.width <= 0 || node.height <= 0 ||
                    x < 0 || y < 0 || x > world.map_width || y > world.map_height)
                    throw new InvalidDataException("入口坐标/资源/ID 无效：" + node.id);
            }
            var terrain = A.Sprite("world-map-base");
            if (terrain == null || Mathf.Abs(terrain.rect.width / terrain.rect.height - world.map_width / world.map_height) > .005f)
                throw new InvalidDataException("地图设计域与原始底图宽高比不一致");
        }

        [MenuItem("仙途/秘境资源包/生成独立组合预制体")]
        public static void BuildPreviewFromMenu() { Debug.Log(BuildPreviewPrefabs()); }
        public static string BuildPreviewPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            GameObject map = null, lobby = null;
            try
            {
                map = CanvasRoot("RealmMapPreview");
                map.AddComponent<RealmMapPanel>().BuildForPreview();
                PrefabUtility.SaveAsPrefabAsset(map, PrefabFolder + "/RealmMapPreview.prefab");
                var source = Resources.Load<TextAsset>(A.ResourceRoot + "lobby-layout");
                if (source == null) throw new InvalidDataException("缺少大厅组合布局");
                var layout = JsonUtility.FromJson<LobbyLayout>(source.text);
                lobby = CanvasRoot("LobbyComposition");
                var design = A.Rect(lobby.transform, "LobbyDesign", 0, 0, layout.design_width, layout.design_height);
                design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f); design.anchoredPosition = Vector2.zero;
                design.gameObject.AddComponent<RealmMapPortraitFit>();
                A.Fill(design, "Paper", 0, 0, layout.design_width, layout.design_height, A.Paper);
                if (layout.layers != null) foreach (var layer in layout.layers)
                {
                    if (A.Sprite(layer.asset) == null) throw new InvalidDataException("大厅图层资源缺失：" + layer.asset);
                    var art = A.Art(design, layer.asset, layer.x, layer.y, layer.width, layer.height, layer.preserveAspect);
                    art.name = layer.name ?? layer.asset; art.color = new Color(1, 1, 1, Mathf.Clamp01(layer.opacity));
                }
                if (layout.texts != null) foreach (var item in layout.texts)
                {
                    Color color; if (!ColorUtility.TryParseHtmlString(item.color ?? "", out color)) color = A.Ink;
                    TextAlignmentOptions alignment;
                    if (!Enum.TryParse(item.alignment ?? "Center", true, out alignment)) alignment = TextAlignmentOptions.Center;
                    A.Text(design, item.name, item.text, item.x, item.y, item.width, item.height, item.fontSize, color, alignment);
                }
                PrefabUtility.SaveAsPrefabAsset(lobby, PrefabFolder + "/LobbyComposition.prefab");
                AssetDatabase.SaveAssets();
                return "Composition prefabs: " + PrefabFolder + "/RealmMapPreview.prefab, LobbyComposition.prefab\n";
            }
            finally
            {
                if (map != null) UnityEngine.Object.DestroyImmediate(map);
                if (lobby != null) UnityEngine.Object.DestroyImmediate(lobby);
            }
        }
        private static GameObject CanvasRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(941, 1672); scaler.matchWidthOrHeight = .5f;
            return root;
        }
        private static void PngDimensions(string path, out int w, out int h)
        {
            var bytes = new byte[24];
            using (var file = File.OpenRead(path)) if (file.Read(bytes, 0, bytes.Length) != bytes.Length) throw new InvalidDataException("PNG 文件头不完整");
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; i++) if (signature[i] != bytes[i]) throw new InvalidDataException("不是 PNG：" + path);
            w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) throw new InvalidDataException("源图必须保留原分辨率且不超过4096：" + path);
        }
        private static bool EqualBytes(string a, string b)
        {
            if (!File.Exists(b) || new FileInfo(a).Length != new FileInfo(b).Length) return false;
            using (var hash = SHA256.Create())
            {
                byte[] x, y; using (var input = File.OpenRead(a)) x = hash.ComputeHash(input);
                using (var input = File.OpenRead(b)) y = hash.ComputeHash(input);
                for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
                return true;
            }
        }
    }
}
