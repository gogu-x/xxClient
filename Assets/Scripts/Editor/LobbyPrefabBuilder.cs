using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XiuXian.Core;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// 生成大厅 UI 用到的通用 Prefab：
    ///   - LobbyActionButton：底部功能按钮（挑战/角色/背包/功法）的统一样式模板。
    ///   - BuildingHotspot：大厅建筑热点的通用结构（图标 + 发光层 + 标签）。
    ///
    /// 生成后的 Prefab 是正常的资产文件，后续样式调整（颜色、字体、层级）
    /// 直接在编辑器里对 Prefab 进行修改即可，不需要重新运行本工具。
    /// 重复运行会覆盖已存在的同名 Prefab，重新生成前会保留原有的场景引用（Prefab 实例不受影响，
    /// 除非手动重新拖拽赋值）。
    ///
    /// 使用：Tools > XiuXian > 生成大厅通用 Prefab
    /// </summary>
    public static class LobbyPrefabBuilder
    {
        private const string PrefabDir = "Assets/Prefabs/UI";
        private const string ActionButtonPath = PrefabDir + "/LobbyActionButton.prefab";
        private const string BuildingHotspotPath = PrefabDir + "/BuildingHotspot.prefab";

        [MenuItem("Tools/XiuXian/生成大厅通用 Prefab")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(PrefabDir);
            BuildActionButtonPrefab();
            BuildBuildingHotspotPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LobbyPrefabBuilder] 大厅通用 Prefab 已生成/更新。");
        }

        // ---------- LobbyActionButton ----------

        private static void BuildActionButtonPrefab()
        {
            var go = new GameObject("LobbyActionButton", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 110);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.85f, 0.65f, 0.25f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 200;
            layoutElement.preferredHeight = 110;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "按钮";
            text.fontSize = 30;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.black;
            ApplyChineseFont(text);

            SaveAsPrefab(go, ActionButtonPath);
        }

        // ---------- BuildingHotspot ----------

        private static void BuildBuildingHotspotPrefab()
        {
            var go = new GameObject("BuildingHotspot", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220, 220);

            // 发光层放在图标下面，Bind() 时按需显示
            var glowGo = new GameObject("Glow", typeof(RectTransform));
            glowGo.transform.SetParent(go.transform, false);
            var glowRect = glowGo.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-20, -20);
            glowRect.offsetMax = new Vector2(20, 20);
            var glowImage = glowGo.AddComponent<Image>();
            glowImage.color = new Color(1f, 0.95f, 0.7f, 0.6f);
            glowImage.raycastTarget = false;
            glowGo.SetActive(false);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;

            // 点击区域用整体的透明 Image 承载（保证没有 icon 贴图时依然可点击/可见范围明确）
            var clickImage = go.AddComponent<Image>();
            clickImage.color = new Color(1f, 1f, 1f, 0.01f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0, 8);
            labelRect.sizeDelta = new Vector2(180, 50);

            var labelBgImage = labelGo.AddComponent<Image>();
            labelBgImage.color = new Color(0f, 0f, 0f, 0.45f);
            labelGo.AddComponent<LayoutElement>();

            var labelTextGo = new GameObject("Text", typeof(RectTransform));
            labelTextGo.transform.SetParent(labelGo.transform, false);
            var labelTextRect = labelTextGo.GetComponent<RectTransform>();
            labelTextRect.anchorMin = Vector2.zero;
            labelTextRect.anchorMax = Vector2.one;
            labelTextRect.offsetMin = Vector2.zero;
            labelTextRect.offsetMax = Vector2.zero;
            var labelText = labelTextGo.AddComponent<TextMeshProUGUI>();
            labelText.text = "建筑";
            labelText.fontSize = 30;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            ApplyChineseFont(labelText);

            var hotspot = go.AddComponent<BuildingHotspot>();
            BindSerialized(hotspot, new()
            {
                { "iconImage", iconImage },
                { "glowImage", glowImage },
                { "labelText", labelText },
            });

            SaveAsPrefab(go, BuildingHotspotPath);
        }

        // ---------- 辅助 ----------

        private static void ApplyChineseFont(TMP_Text text)
        {
            var fontAsset = ChineseFontSetup.GetOrCreateFontAsset();
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }
        }

        private static void SaveAsPrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void BindSerialized(Object target, System.Collections.Generic.Dictionary<string, Object> fields)
        {
            var so = new SerializedObject(target);
            foreach (var kv in fields)
            {
                var prop = so.FindProperty(kv.Key);
                if (prop == null)
                {
                    Debug.LogWarning($"[LobbyPrefabBuilder] 字段未找到: {target.GetType().Name}.{kv.Key}");
                    continue;
                }
                prop.objectReferenceValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
