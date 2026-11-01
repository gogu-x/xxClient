using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// 生成一个支持中文的 TMP 动态字体资源（Dynamic 模式，运行时按需渲染字形到贴图集，
    /// 不需要预先枚举字符集），并将其设置为 TMP 全局默认字体
    /// （TMP Settings > Default Font Asset）。
    ///
    /// 设为全局默认后，新建的 TextMeshProUGUI / TMP_InputField 会自动使用该字体，
    /// 不需要每次手动替换。BootstrapSceneBuilder 生成场景时也会直接读取这个默认字体。
    ///
    /// 使用：Tools > XiuXian > 生成中文字体并设为默认
    /// 字体资源只需生成一次，重复执行会直接复用已存在的 Asset，不会重新烘焙。
    ///
    /// 注意：msyh.ttc（微软雅黑）是 Windows 系统自带字体，这里仅用于本机开发/测试，
    /// 正式发布前需要确认字体授权范围，或替换为可商用的开源中文字体（如思源黑体）。
    /// </summary>
    public static class ChineseFontSetup
    {
        private const string SourceFontPath = "Assets/Fonts/MSYaHei-Regular.ttf";
        private const string RepairLabel = "MSYaHei_SDF_2048_v2";
        private const string AsciiFallbackFontAssetPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string RequiredDeveloperDialogCharacters =
            "\u5f00\u53d1\u73af\u5883\u8d26\u53f7\u8f93\u5165\u540e\u5c06\u91cd\u65b0\u67e5\u8be2\u8be5\u7684\u89d2\u8272\u6570\u636e\u8bf7\u786e\u5b9a\u53d6\u6d88\u4e0d\u80fd\u4e3a\u7a7a\u518d\u3002" +
            "\u53cc\u7403\u6218\u6597\u6f14\u793a\u5f85\u673a\u6211\u65b9\u654c\u65b9\u6b63\u5728\u5411\u8bf7\u6c42\u6d4b\u8bd5\u6062\u590d\u66b4\u51fb\u80dc\u5229\u5931\u8d25\u5e73\u5c40\u7ed3\u675f\u6821\u9a8c\u8fd4\u56de\u5927\u5385" +
            "清风道人八境界筑基中期战力当前主线前往秘境通关玄灵第层系统获得高级灵石角色背包修炼宗门伙伴更多任务排行福利设置退出登录晶玉火榜礼卷";
        public const string FontAssetPath = "Assets/Fonts/MSYaHei SDF.asset";

        [InitializeOnLoadMethod]
        private static void EnsureExistingFontCharacters()
        {
            // Wait for asset import to finish; never rebake persistent assets in Play mode.
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var fontAsset = GetOrCreateFontAsset();
                if (fontAsset != null) SetAsTmpDefault(fontAsset);
            };
        }

        [MenuItem("Tools/XiuXian/生成中文字体并设为默认")]
        public static void SetupFont()
        {
            var fontAsset = GetOrCreateFontAsset();
            if (fontAsset == null)
            {
                return;
            }

            SetAsTmpDefault(fontAsset);
            ApplyToOpenScenes(fontAsset);
            Debug.Log("[ChineseFontSetup] 已设为 TMP 默认字体，并应用到当前打开的场景。");
        }

        /// <summary>
        /// 获取字体资源，若尚未生成则创建；已存在则直接复用，避免重复烘焙。
        /// 供 BootstrapSceneBuilder 等其他 Editor 脚本调用。
        /// </summary>
        public static TMP_FontAsset GetOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                // Changing only the source font leaves old glyph IDs, face metrics and UVs.
                // Rebuild as one unit, preserving the font and material asset references.
                if (NeedsRebuild(existing))
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        Debug.LogError("[ChineseFontSetup] 字体图集需要修复，请先退出运行模式。");
                        return null;
                    }
                    RebuildInPlace(existing);
                }
                EnsureDynamicConfiguration(existing);
                EnsureAsciiFallback(existing);
                EnsureRequiredDeveloperDialogCharacters(existing);
                return existing;
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[ChineseFontSetup] 未找到字体源文件: {SourceFontPath}");
                return null;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90, 9,
                GlyphRenderMode.SDFAA,
                2048, 2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError("[ChineseFontSetup] 字体资源创建失败");
                return null;
            }

            fontAsset.name = "MSYaHei SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // CreateFontAsset() 只创建了内部的 atlas 贴图和材质对象，并没有把它们
            // 挂载为主资源文件的子资源。如果不手动 AddObjectToAsset，这些贴图/材质
            // 会在下次域重载或 GC 时变成孤立对象被回收，导致运行时报
            // MissingReferenceException: m_AtlasTextures ... doesn't exist anymore。
            // （Font Asset Creator 窗口生成时内部会做这一步，纯代码调用需要手动补上。）
            foreach (var texture in fontAsset.atlasTextures)
            {
                if (texture != null)
                {
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }
            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EnsureDynamicConfiguration(fontAsset);
            EnsureAsciiFallback(fontAsset);
            EnsureRequiredDeveloperDialogCharacters(fontAsset);
            PersistAtlases(fontAsset);
            MarkRepaired(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ChineseFontSetup] 字体资源已生成: {FontAssetPath}");
            return fontAsset;
        }

        private static bool NeedsRebuild(TMP_FontAsset font)
        {
            if (font.sourceFontFile != AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath)
                || font.atlasWidth != 2048 || font.atlasHeight != 2048
                || font.material == null || font.atlasTextures == null
                || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null
                || !System.Array.Exists(AssetDatabase.GetLabels(font), label => label == RepairLabel))
                return true;
            foreach (var glyph in font.glyphTable)
                if (glyph.atlasIndex >= font.atlasTextures.Length || font.atlasTextures[glyph.atlasIndex] == null)
                    return true;
            return false;
        }

        private static void RebuildInPlace(TMP_FontAsset font)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null) throw new System.InvalidOperationException("Missing Microsoft YaHei source font.");
            var fresh = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (fresh == null) throw new System.InvalidOperationException("Could not create Microsoft YaHei SDF.");

            // Preserve the shared material's local file ID used by serialized UI components.
            var material = font.material;
            if (material != null)
            {
                var temporaryMaterial = fresh.material;
                EditorUtility.CopySerialized(temporaryMaterial, material);
                fresh.material = material;
                Object.DestroyImmediate(temporaryMaterial);
                EditorUtility.SetDirty(material);
            }
            fresh.name = "MSYaHei SDF";
            EditorUtility.CopySerialized(fresh, font);
            // TMP.OnDestroy owns its atlas/material. Transfer ownership before disposal.
            fresh.atlasTextures = System.Array.Empty<Texture2D>();
            fresh.material = null;
            Object.DestroyImmediate(fresh);
            // CopySerialized does not update TMP's nonserialized cached atlas pointer.
            typeof(TMP_FontAsset).GetField("m_AtlasTexture",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(font, null);
            PersistAtlases(font);
            font.ReadFontAssetDefinition();
            EnsureRequiredDeveloperDialogCharacters(font);
            PersistAtlases(font);
            MarkRepaired(font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChineseFontSetup] 已完整重建微软雅黑字形、材质和 2048 SDF 图集，保留原资源引用。");
        }

        private static void MarkRepaired(TMP_FontAsset font)
        {
            var labels = new System.Collections.Generic.List<string>(AssetDatabase.GetLabels(font));
            if (!labels.Contains(RepairLabel)) labels.Add(RepairLabel);
            AssetDatabase.SetLabels(font, labels.ToArray());
        }

        private static void PersistAtlases(TMP_FontAsset font)
        {
            foreach (var atlas in font.atlasTextures)
            {
                if (atlas == null) continue; // TMP may leave unused capacity in the array.
                atlas.filterMode = FilterMode.Bilinear;
                atlas.wrapMode = TextureWrapMode.Clamp;
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
                EditorUtility.SetDirty(atlas);
            }
            if (font.material != null && !AssetDatabase.Contains(font.material))
                AssetDatabase.AddObjectToAsset(font.material, font);
        }

        private static void EnsureDynamicConfiguration(TMP_FontAsset fontAsset)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[ChineseFontSetup] 未找到字体源文件: {SourceFontPath}");
                return;
            }

            bool changed = false;
            var serializedFont = new SerializedObject(fontAsset);
            var sourceEditorRef = serializedFont.FindProperty("m_SourceFontFile_EditorRef");
            var sourceRuntimeRef = serializedFont.FindProperty("m_SourceFontFile");
            var sourceGuid = serializedFont.FindProperty("m_SourceFontFileGUID");
            if (sourceEditorRef != null && sourceEditorRef.objectReferenceValue != sourceFont)
            {
                sourceEditorRef.objectReferenceValue = sourceFont;
                changed = true;
            }
            if (sourceRuntimeRef != null && sourceRuntimeRef.objectReferenceValue != sourceFont)
            {
                sourceRuntimeRef.objectReferenceValue = sourceFont;
                changed = true;
            }
            string expectedGuid = AssetDatabase.AssetPathToGUID(SourceFontPath);
            if (sourceGuid != null && sourceGuid.stringValue != expectedGuid)
            {
                sourceGuid.stringValue = expectedGuid;
                changed = true;
            }
            if (changed)
            {
                serializedFont.ApplyModifiedPropertiesWithoutUndo();
            }

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                changed = true;
            }
            if (!fontAsset.isMultiAtlasTexturesEnabled)
            {
                fontAsset.isMultiAtlasTexturesEnabled = true;
                changed = true;
            }
            if (!changed)
            {
                return;
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ChineseFontSetup] 已将 {fontAsset.name} 校正为 Dynamic 模式并启用多图集。");
        }

        private static void EnsureAsciiFallback(TMP_FontAsset fontAsset)
        {
            var asciiFallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AsciiFallbackFontAssetPath);
            if (asciiFallback == null)
            {
                Debug.LogWarning($"[ChineseFontSetup] 未找到 ASCII 回退字体: {AsciiFallbackFontAssetPath}");
                return;
            }

            if (fontAsset.fallbackFontAssetTable.Contains(asciiFallback))
            {
                return;
            }

            fontAsset.fallbackFontAssetTable.Add(asciiFallback);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ChineseFontSetup] 已为 {fontAsset.name} 添加 ASCII 回退字体: {asciiFallback.name}");
        }

        private static void EnsureRequiredDeveloperDialogCharacters(TMP_FontAsset fontAsset)
        {
            var characters = new System.Collections.Generic.SortedSet<char>(RequiredDeveloperDialogCharacters);
            for (char c = ' '; c <= '~'; c++) characters.Add(c);
            // Bake UI Chinese ahead of time; dynamic atlases still cover player/server names.
            foreach (var path in System.IO.Directory.GetFiles("Assets/Scripts/UI", "*.cs"))
                foreach (char c in System.IO.File.ReadAllText(path))
                    if (c >= '\u4e00' && c <= '\u9fff') characters.Add(c);
            foreach (char c in "·，。！：／‹—…") characters.Add(c);
            string required = string.Concat(characters);
            bool needsGlyphs = false;
            foreach (char character in required)
            {
                if (!fontAsset.HasCharacter(character))
                {
                    needsGlyphs = true;
                    break;
                }
            }

            if (!needsGlyphs)
            {
                return;
            }

            if (!fontAsset.TryAddCharacters(required, out string missingCharacters))
            {
                // Missing source glyphs must not erase otherwise valid atlases.
                Debug.LogWarning($"[ChineseFontSetup] 字体源不包含这些字符，将使用回退字体: {missingCharacters}");
            }

            PersistAtlases(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChineseFontSetup] \u5df2\u9884\u70d8\u7119\u5f00\u53d1\u8d26\u53f7\u5f39\u7a97\u6240\u9700\u4e2d\u6587\u5b57\u7b26\u3002");
        }

        private static void SetAsTmpDefault(TMP_FontAsset fontAsset)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[ChineseFontSetup] 未找到 TMP Settings，无法设置全局默认字体，" +
                                  "请先执行 Window > TextMeshPro > Import TMP Essential Resources。");
                return;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop != null)
            {
                if (prop.objectReferenceValue == fontAsset) return;
                prop.objectReferenceValue = fontAsset;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private static void ApplyToOpenScenes(TMP_FontAsset fontAsset)
        {
            int count = 0;
            foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(text, "Unify Microsoft YaHei font");
                text.font = fontAsset;
                text.fontSharedMaterial = fontAsset.material;
                EditorUtility.SetDirty(text);
                count++;
            }
            foreach (var input in Object.FindObjectsByType<TMP_InputField>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (input.textComponent is TextMeshProUGUI tmp)
                {
                    tmp.font = fontAsset;
                }
                if (input.placeholder is TextMeshProUGUI placeholderTmp)
                {
                    placeholderTmp.font = fontAsset;
                }
                count++;
            }
            Debug.Log($"[ChineseFontSetup] 已替换 {count} 个已存在的 TMP 文本组件的字体。");
        }
    }
}
