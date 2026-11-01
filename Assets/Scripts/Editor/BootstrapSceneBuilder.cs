using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XiuXian.Core;
using XiuXian.Network;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// 一键生成启动流程场景：Home（启动+选服+登录合一，参考主流手游启动页设计）→ Main(占位)。
    /// 选服不是独立页面，而是 Home 内的弹窗（ServerPickerPopup）。
    ///
    /// 使用：Unity 菜单栏 Tools > XiuXian > 生成启动流程场景
    /// 生成后场景另存为 Assets/Scenes/Bootstrap.unity。
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        private const string BgHome = "Assets/Art/Resources/UI/Login/Login_Background.png";
        private const string BgMain = "Assets/Art/Resources/UI/Lobby/Lobby_Background.png";

        private const string CharacterPanelFrameSprite = "Assets/Art/UI/CharacterPanel_Frame.png";
        private const string CharacterInfoCardSprite = "Assets/Art/UI/CharacterInfoCard_Bg.png";
        private const string CharacterBackButtonSprite = "Assets/Art/UI/Btn_Back.png";
        private const string CharacterPanelBgSprite = "Assets/Art/Backgrounds/CharacterPanel_Bg.png";

        private const string ActionButtonPrefabPath = "Assets/Prefabs/UI/LobbyActionButton.prefab";

        // 登录页主按钮的两种文案：文案只放在编辑器侧，运行时脚本通过序列化字段读取。
        private const string EnterGameLabel = "踏 入 仙 途";
        private const string CreateRoleLabel = "创 建 角 色";

        // 底部功能区尺寸（设计尺寸，实际会乘 UIButtonSkin.Scale）。
        // 四个入口排成一行：挑战 / 角色 / 背包 / 功法，各 200 宽 + 24 间距 = 872，
        // 而 Canvas 参考宽度是 1080。设置与退出登录收在右上角的独立入口里，不占这里。
        private const int ActionButtonCount = 4;
        private const float ActionButtonWidth = 200f;
        private const float ActionButtonHeight = 110f;
        private const float ActionRowSpacing = 24f;
        private const float ActionRowWidth =
            ActionButtonWidth * ActionButtonCount + ActionRowSpacing * (ActionButtonCount - 1);
        private const float SettingsEntrySize = 140f;
        private const float SettingsEntryMargin = 40f;
        private const string BuildingHotspotPrefabPath = "Assets/Prefabs/UI/BuildingHotspot.prefab";
        private const string BuildingRegistryPath = "Assets/Configs/BuildingRegistry.asset";

        private const string CharacterPreviewLayerName = "CharacterPreview";
        private const string RenderTexturePath = "Assets/Configs/RT_CharacterPreview.renderTexture";

        [MenuItem("Tools/XiuXian/生成启动流程场景")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[BootstrapSceneBuilder] 请先停止游戏，再生成场景。");
                return;
            }

            // Validate the shared font BEFORE replacing the open scene. Previously a
            // font exception in BuildHomePanel left an untitled scene with only a logo.
            var font = ChineseFontSetup.GetOrCreateFontAsset();
            if (font == null || font.material == null || font.atlasTextureCount == 0)
            {
                Debug.LogError("[BootstrapSceneBuilder] 字体尚未就绪，保留当前场景，未开始生成。");
                return;
            }
            for (int i = 0; i < font.atlasTextureCount; i++)
            {
                if (font.atlasTextures[i] == null || !font.atlasTextures[i].isReadable)
                {
                    Debug.LogError("[BootstrapSceneBuilder] 字体图集无效，保留当前场景，请先修复字体。");
                    return;
                }
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureLayerExists(CharacterPreviewLayerName);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEventSystem();
            CreateMainCamera();
            var canvas = CreateCanvas();

            var networkManagerGo = new GameObject("NetworkManager");
            networkManagerGo.AddComponent<NetworkManager>();

            var previewStage = BuildCharacterPreviewStage();

            // UIManager 必须早于各面板创建。面板用 AddComponent 挂到已激活的物体上时，
            // 编辑器会立刻调用它们的 Awake/OnEnable，而面板里要访问 UIManager.Instance ——
            // 放在面板后面会让 Instance 还是 null，日志里刷出一串 NullReferenceException。
            var uiManagerGo = new GameObject("UIManager");
            var uiManager = uiManagerGo.AddComponent<UIManager>();

            var home = BuildHomePanel(canvas.transform);
            var main = BuildMainPanel(canvas.transform);
            var xianFu = BuildXianFuPanel(canvas.transform);
            var character = BuildCharacterPanel(canvas.transform, previewStage);

            uiManager.SetBindingsForEditorSetup(new List<UIManager.ScreenBinding>
            {
                new() { screen = UIScreen.Home, panel = home.gameObject },
                new() { screen = UIScreen.Main, panel = main },
                new() { screen = UIScreen.XianFu, panel = xianFu },
                new() { screen = UIScreen.Character, panel = character },
            });

            home.gameObject.SetActive(true);
            main.SetActive(false);
            xianFu.SetActive(false);
            character.SetActive(false);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[BootstrapSceneBuilder] 场景已生成: {ScenePath}");
        }

        private static void CreateEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        /// <summary>
        /// 场景里必须有至少一个 Camera，否则 Game 视图会一直显示
        /// "Display 1, No cameras rendering" 提示。UI 全部用 Screen Space - Overlay
        /// 渲染，不依赖这个摄像机拍到任何 3D 内容，这里只用纯色清屏。
        /// 显式排除 CharacterPreview 层，避免未来大厅场景升级为3D时，
        /// 主摄像机意外拍到角色预览舞台（那个舞台只应该被自己的专用摄像机渲染到 RenderTexture）。
        /// </summary>
        private static void CreateMainCamera()
        {
            if (Object.FindObjectOfType<Camera>() != null)
            {
                return;
            }
            var go = new GameObject("Main Camera");
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = ~LayerMask.GetMask(CharacterPreviewLayerName);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            go.tag = "MainCamera";
        }

        /// <summary>
        /// 确保指定名称的 Layer 已注册到 TagManager，不存在时自动写入第一个空闲的 User Layer 槏位。
        /// 避免每次新增场景相关 Layer 都要手动去 Project Settings 里配置。
        /// </summary>
        private static void EnsureLayerExists(string layerName)
        {
            if (!string.IsNullOrEmpty(LayerMask.LayerToName(LayerMask.NameToLayer(layerName))))
            {
                return;
            }

            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogWarning("[BootstrapSceneBuilder] 未找到 TagManager.asset，无法自动注册 Layer，" +
                                  $"请手动在 Project Settings > Tags and Layers 中添加 \"{layerName}\"。");
                return;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layersProp = tagManager.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[BootstrapSceneBuilder] 已注册 Layer: {layerName}");
                    return;
                }
            }

            Debug.LogWarning($"[BootstrapSceneBuilder] 没有空闲的 User Layer 槏位，无法自动注册 \"{layerName}\"。");
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<MobileAdaptiveCanvasScaler>();

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ---------- Home ----------

        private static HomePanel BuildHomePanel(Transform parent)
        {
            var root = CreateFullScreenPanel("HomePanel", parent, new Color(0.06f, 0.05f, 0.1f), BgHome);
            var panel = root.gameObject.AddComponent<HomePanel>();

            var title = CreateText(root, "Logo", "修 仙 传", 64, new Vector2(0, 500), new Vector2(700, 140));
            title.alignment = TextAlignmentOptions.Center;

            // 加载中显示的内容
            var slider = CreateSlider(root, "ProgressBar", new Vector2(0, -600), new Vector2(700, 30));
            var status = CreateText(root, "StatusText", "初始化...", 28, new Vector2(0, -660), new Vector2(700, 60));
            status.alignment = TextAlignmentOptions.Center;

            // 加载完成后显示的内容：服务器信息条 + 进入按钮
            var readyGroup = new GameObject("ReadyGroup", typeof(RectTransform));
            readyGroup.transform.SetParent(root, false);
            var readyRect = readyGroup.GetComponent<RectTransform>();
            readyRect.anchorMin = Vector2.zero;
            readyRect.anchorMax = Vector2.one;
            readyRect.offsetMin = Vector2.zero;
            readyRect.offsetMax = Vector2.zero;

            var serverBar = BuildServerInfoBar(readyGroup.transform, out var serverNameText, out var serverPickerButton);
            // 主行动按钮改用纯色按钮：原来挂的是一张 640x121 的贴图(有效图案只占中间一块，
            // 四周大量透明留白)，换分辨率时图案容易变形，配色也只能改图不能改代码。
            var enterButton = CreateButton(readyGroup.transform, "EnterButton", EnterGameLabel,
                new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(440, 116),
                new Color(0.87f, 0.66f, 0.26f), 38);

            var popup = BuildServerPickerPopup(root);

            BindSerialized(panel, new()
            {
                { "progressBar", slider },
                { "statusText", status },
                { "readyGroup", readyGroup },
                { "serverNameText", serverNameText },
                { "serverPickerButton", serverPickerButton },
                { "enterButton", enterButton },
                { "serverPickerPopup", popup },
            });

            BindSerializedStrings(panel, new()
            {
                { "enterGameLabel", EnterGameLabel },
                { "createRoleLabel", CreateRoleLabel },
            });

            return panel;
        }

        private static RectTransform BuildServerInfoBar(Transform parent, out TMP_Text serverNameText, out Button pickerButton)
        {
            var go = new GameObject("ServerInfoBar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(900, 110);
            rect.anchoredPosition = new Vector2(0, 260);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = bg;

            var nameText = CreateText(go.transform, "ServerNameText", "请选择服务器", 34, new Vector2(-40, 0), new Vector2(700, 80));
            nameText.alignment = TextAlignmentOptions.Left;

            var arrow = CreateText(go.transform, "Arrow", ">", 36, new Vector2(400, 0), new Vector2(60, 60));
            arrow.alignment = TextAlignmentOptions.Center;

            serverNameText = nameText;
            pickerButton = button;
            return rect;
        }

        private static ServerPickerPopup BuildServerPickerPopup(Transform parent)
        {
            var overlayGo = new GameObject("ServerPickerPopup", typeof(RectTransform));
            overlayGo.transform.SetParent(parent, false);
            var overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var maskImage = overlayGo.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.6f);
            var maskButton = overlayGo.AddComponent<Button>();
            maskButton.targetGraphic = maskImage;

            var popup = overlayGo.AddComponent<ServerPickerPopup>();

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(overlayGo.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(860, 1100);
            panelRect.anchoredPosition = Vector2.zero;
            var panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.1f, 0.1f, 0.14f, 0.95f);

            // 阻止点击面板内部时触发遮罩的关闭逻辑（面板自身也挂 Button 拦截射线穿透）。
            var panelBlocker = panelGo.AddComponent<Button>();
            panelBlocker.targetGraphic = panelBg;
            panelBlocker.transition = Selectable.Transition.None;

            var title = CreateText(panelGo.transform, "Title", "选择服务器", 40, new Vector2(0, 480), new Vector2(600, 80));
            title.alignment = TextAlignmentOptions.Center;

            var closeButton = CreateButton(panelGo.transform, "CloseButton", "×", new Vector2(380, 480), new Vector2(80, 80));

            var scrollRoot = new GameObject("ListContainer", typeof(RectTransform));
            scrollRoot.transform.SetParent(panelGo.transform, false);
            var listRect = scrollRoot.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.5f, 0.5f);
            listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.sizeDelta = new Vector2(760, 850);
            listRect.anchoredPosition = new Vector2(0, -60);

            var layout = scrollRoot.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;

            var itemPrefab = BuildServerListItemPrefab();

            BindSerialized(popup, new()
            {
                { "listContainer", scrollRoot.transform },
                { "itemPrefab", itemPrefab },
                { "closeButton", closeButton },
                { "backgroundMask", maskButton },
            });

            return popup;
        }

        private static ServerListItem BuildServerListItemPrefab()
        {
            var go = new GameObject("ServerListItem", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(760, 120);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.08f);

            var item = go.AddComponent<ServerListItem>();

            var nameText = CreateText(go.transform, "NameText", "服务器名称", 32, new Vector2(-150, 15), new Vector2(400, 60));
            nameText.alignment = TextAlignmentOptions.Left;

            var statusText = CreateText(go.transform, "StatusText", "流畅", 24, new Vector2(-150, -30), new Vector2(400, 40));
            statusText.alignment = TextAlignmentOptions.Left;
            statusText.color = new Color(0.4f, 1f, 0.4f);

            var button = CreateButton(go.transform, "SelectButton", "进入", new Vector2(280, 0), new Vector2(160, 80));

            BindSerialized(item, new()
            {
                { "nameText", nameText },
                { "statusText", statusText },
                { "selectButton", button },
            });

            // 保存为 Prefab，场景中的实例不需要保留，仅供 Instantiate 使用。
            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
            var prefabPath = "Assets/Prefabs/UI/ServerListItem.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ServerListItem>();
        }

        // ---------- Main (大厅) ----------

private static GameObject BuildMainPanel(Transform parent)
        {
            var root = CreateFullScreenPanel("MainPanel", parent, new Color(0.05f, 0.1f, 0.08f), BgMain);
            var mainPanelGo = root.gameObject;

            mainPanelGo.SetActive(false);
            var mainPanelComp = mainPanelGo.AddComponent<MainPanel>();
            var referenceLobby = LobbyReferenceView.Build(root);
            BindSerialized(mainPanelComp, new()
            {
                { "skillButton", referenceLobby.Cultivation },
                { "battleButton", referenceLobby.Battle },
                { "secretButton", referenceLobby.Secret },
                { "inventoryButton", referenceLobby.Inventory },
                { "sectButton", referenceLobby.Sect },
                { "magicArtifactButton", referenceLobby.MagicArtifact },
                { "moreButton", referenceLobby.More },
                { "characterSideButton", referenceLobby.CharacterSide },
                { "inventorySideButton", referenceLobby.InventorySide },
                { "taskSideButton", referenceLobby.TaskSide },
                { "sectSideButton", referenceLobby.SectSide },
                { "activitySideButton", referenceLobby.ActivitySide },
                { "welfareSideButton", referenceLobby.WelfareSide },
                { "questGuideButton", referenceLobby.QuestGuide },
                { "questBannerButton", referenceLobby.QuestBanner },
                { "secretPortalButton", referenceLobby.SecretPortal },
            });
            return mainPanelGo;
        }

        private static RectTransform BuildActionBar(Transform parent, out Button characterBtn,
            out Button inventoryBtn, out Button skillBtn, out Button autoBattleBtn)
        {
            var barGo = new GameObject("ActionBar", typeof(RectTransform));
            barGo.transform.SetParent(parent, false);
            var barRect = barGo.GetComponent<RectTransform>();
            // 锚点固定在底边中点，宽度固定为一行四个按钮的宽度，高度交给 ContentSizeFitter。
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(ActionRowWidth, 0f);
            barRect.anchoredPosition = new Vector2(0, 60);

            // 纵向排列三行，每行自己横向排。
            var column = barGo.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.LowerCenter;
            column.spacing = ActionRowSpacing;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            // 宽度已经写死为 ActionRowWidth，只让高度按内容收缩。
            var fitter = barGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var actionButtonPrefab = AssetDatabase.LoadAssetAtPath<Button>(ActionButtonPrefabPath);
            if (actionButtonPrefab == null)
            {
                Debug.LogWarning("[BootstrapSceneBuilder] 未找到 LobbyActionButton Prefab，" +
                                  "请先执行 Tools > XiuXian > 生成大厅通用 Prefab。底部功能按钮将不会生成。");
                characterBtn = inventoryBtn = skillBtn = autoBattleBtn = null;
                return barRect;
            }

            // 四个入口排在同一行。挑战（原"挂机战斗"）只有两个字，占通栏是浪费，
            // 排首位当主入口，其余三个是角色相关。
            var mainRow = CreateActionRow(barGo.transform, "MainRow");
            autoBattleBtn = InstantiateActionButton(actionButtonPrefab, mainRow, "挑战");
            characterBtn = InstantiateActionButton(actionButtonPrefab, mainRow, "角色");
            inventoryBtn = InstantiateActionButton(actionButtonPrefab, mainRow, "背包");
            skillBtn = InstantiateActionButton(actionButtonPrefab, mainRow, "功法");

            return barRect;
        }

        /// <summary>
        /// 右上角的设置入口：一个按钮 + 点开后的小菜单。
        /// 原来"设置"和"退出登录"是两个并列的底部按钮，既占地方又主次不分；
        /// 现在收成一个入口，以后加新设置项只要往菜单里再塞一条。
        /// </summary>
        private static Button BuildSettingsEntry(Transform parent, out GameObject menu,
            out Button menuSettingsButton, out Button menuLogoutButton)
        {
            // 纯图标按钮（label 传空 = 不挂文字），图标是运行时画出来的齿轮。
            var entry = CreateButton(parent, "SettingsButton", string.Empty,
                new Vector2(1f, 1f), new Vector2(-SettingsEntryMargin, -SettingsEntryMargin),
                new Vector2(SettingsEntrySize, SettingsEntrySize * 0.55f));

            var iconGo = new GameObject("GearIcon", typeof(RectTransform));
            iconGo.transform.SetParent(entry.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            // 四周留白随按钮一起缩放，避免改了 UIButtonSkin.Scale 之后图标顶到边。
            Vector2 iconPadding = new Vector2(22f, 14f) * UIButtonSkin.Scale;
            iconRect.offsetMin = iconPadding;
            iconRect.offsetMax = -iconPadding;
            iconRect.localScale = Vector3.one;
            iconGo.AddComponent<Image>();
            iconGo.AddComponent<UIGearIcon>();

            // 菜单挂在面板子节点最后，保证盖在建筑热点和其它元素之上。
            var menuGo = new GameObject("SettingsMenu", typeof(RectTransform));
            menuGo.transform.SetParent(parent, false);
            var menuRect = menuGo.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(1f, 1f);
            menuRect.anchorMax = new Vector2(1f, 1f);
            menuRect.pivot = new Vector2(1f, 1f);
            // 正好落在入口按钮下方
            menuRect.anchoredPosition = new Vector2(-SettingsEntryMargin,
                -SettingsEntryMargin - SettingsEntrySize * 0.55f * UIButtonSkin.Scale - 16f);
            menuRect.sizeDelta = new Vector2(SettingsEntrySize * UIButtonSkin.Scale, 0f);

            var layout = menuGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 12f * UIButtonSkin.Scale;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = menuGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            menuSettingsButton = CreateButton(menuGo.transform, "MenuSettingsButton", "设置",
                new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(SettingsEntrySize, SettingsEntrySize * 0.62f));
            menuLogoutButton = CreateButton(menuGo.transform, "MenuLogoutButton", "退出登录",
                new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(SettingsEntrySize, SettingsEntrySize * 0.62f));

            menu = menuGo;
            menuGo.SetActive(false);
            return entry;
        }

        /// <summary>创建底部功能区的一行（横向自动排列）。</summary>
        private static Transform CreateActionRow(Transform parent, string name,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var row = rowGo.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = alignment;
            row.spacing = ActionRowSpacing * UIButtonSkin.Scale;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return rowGo.transform;
        }

        /// <summary>覆盖按钮的 LayoutElement 尺寸，用于让主按钮通栏、比其它按钮更高。</summary>
        private static void SetActionButtonSize(Button button, float width, float height)
        {
            if (button == null)
            {
                return;
            }
            if (!button.TryGetComponent<LayoutElement>(out var element))
            {
                element = button.gameObject.AddComponent<LayoutElement>();
            }
            float scaledWidth = width * UIButtonSkin.Scale;
            float scaledHeight = height * UIButtonSkin.Scale;
            element.preferredWidth = scaledWidth;
            element.preferredHeight = scaledHeight;
            if (button.transform is RectTransform rect)
            {
                rect.sizeDelta = new Vector2(scaledWidth, scaledHeight);
            }
        }

        private static Button InstantiateActionButton(Button prefab, Transform parent, string label)
        {
            var instanceGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            instanceGo.name = $"{label}Button";
            var instance = instanceGo.GetComponent<Button>();
            var text = instance.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = label;
            }
            // 预制体自带 200x110 的 LayoutElement，这里按统一倍率重设一次，
            // 否则整体缩放作用不到它身上。
            SetActionButtonSize(instance, ActionButtonWidth, ActionButtonHeight);
            // 大厅预制体本身没带皮肤组件，这里补上，保证底部按钮与其它页面观感一致。
            return ApplyButtonSkin(instance);
        }

        // ---------- 仙府（占位） ----------

        private static GameObject BuildXianFuPanel(Transform parent)
        {
            var root = CreateFullScreenPanel("XianFuPanel", parent, new Color(0.08f, 0.06f, 0.12f));
            var panel = root.gameObject.AddComponent<XianFuPanel>();

            var title = CreateText(root, "TitleText", "仙府（开发中）", 44, new Vector2(0, 0), new Vector2(700, 100));
            title.alignment = TextAlignmentOptions.Center;

            var backButton = CreateButton(root, "BackButton", "返回大厅", new Vector2(0, -700), new Vector2(300, 100));

            BindSerialized(panel, new()
            {
                { "titleText", title },
                { "backButton", backButton },
            });

            return root.gameObject;
        }

        // ---------- 角色预览舞台（3D，独立于 UI Canvas） ----------

        /// <summary>
        /// 创建角色预览用的 3D 舞台：专用摄像机 + 占位模型（Capsule），渲染到一张
        /// RenderTexture 资产上。舞台本身放在世界坐标很远的地方（避免和未来的3D场景/大厅重叠），
        /// 用独立 Layer 隔离，只被自己的摄像机看到。
        /// 当前没有真实角色模型资源，用 Capsule Primitive 占位；
        /// 接入真实模型时调用 CharacterPreviewStage.SetModel() 替换即可，不需要改这里的搭建逻辑。
        /// </summary>
        private static CharacterPreviewStage BuildCharacterPreviewStage()
        {
            int previewLayer = LayerMask.NameToLayer(CharacterPreviewLayerName);

            var stageGo = new GameObject("CharacterPreviewStage");
            // 放在远离原点的位置，避免和未来可能添加的其他3D场景内容产生视觉/物理上的干扰。
            stageGo.transform.position = new Vector3(500f, 0f, 500f);

            var modelRoot = new GameObject("ModelRoot");
            modelRoot.transform.SetParent(stageGo.transform, false);
            modelRoot.layer = previewLayer;

            // 占位模型：Capsule，等真实角色模型资源到位后用 SetModel() 替换。
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = "PlaceholderModel";
            placeholder.transform.SetParent(modelRoot.transform, false);
            placeholder.layer = previewLayer;

            var cameraGo = new GameObject("PreviewCamera");
            cameraGo.transform.SetParent(stageGo.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            camera.cullingMask = LayerMask.GetMask(CharacterPreviewLayerName);
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.enabled = false; // 只在角色面板打开时才启用渲染，见 CharacterPreviewStage.SetActiveStage。

            var renderTexture = GetOrCreateRenderTexture();
            camera.targetTexture = renderTexture;

            // 简单三点布光的占位版本：一个方向光即可看清模型轮廓，后续可按美术要求调整。
            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(stageGo.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = LayerMask.GetMask(CharacterPreviewLayerName);
            light.intensity = 1.1f;

            var stage = stageGo.AddComponent<CharacterPreviewStage>();

            BindSerialized(stage, new()
            {
                { "previewCamera", camera },
                { "modelRoot", modelRoot.transform },
                { "renderTexture", renderTexture },
            });

            return stage;
        }

        private static RenderTexture GetOrCreateRenderTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (existing != null)
            {
                return existing;
            }

            var rt = new RenderTexture(512, 640, 16)
            {
                name = "RT_CharacterPreview",
                antiAliasing = 2,
            };
            System.IO.Directory.CreateDirectory("Assets/Configs");
            AssetDatabase.CreateAsset(rt, RenderTexturePath);
            AssetDatabase.SaveAssets();
            return rt;
        }

        // ---------- 角色面板 ----------

        private static GameObject BuildCharacterPanel(Transform parent, CharacterPreviewStage previewStage)
        {
            // 角色页仅保留预览画布；标题、信息卡、皮肤列表和返回按钮
            // 全部由 CharacterPanel 的新版衣橱界面在运行时统一创建。
            var root = CreateFullScreenPanel("CharacterPanel", parent,
                new Color(0.025f, 0.055f, 0.08f), CharacterPanelBgSprite);
            var panelGo = root.gameObject;

            var previewGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(root, false);
            var previewRect = previewGo.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0f, 0.5f);
            previewRect.anchorMax = new Vector2(0f, 0.5f);
            previewRect.pivot = new Vector2(0f, 0.5f);
            previewRect.anchoredPosition = new Vector2(35f, 20f);
            previewRect.sizeDelta = new Vector2(620f, 980f);

            var rawImage = previewGo.GetComponent<RawImage>();
            rawImage.texture = previewStage != null ? previewStage.RenderTexture : null;
            rawImage.color = Color.white;

            var orbitCamera = previewGo.AddComponent<CharacterOrbitCamera>();
            var panel = panelGo.AddComponent<CharacterPanel>();
            BindSerialized(panel, new()
            {
                { "previewImage", rawImage },
                { "previewStage", previewStage },
            });

            if (previewStage != null && previewStage.PreviewCamera != null && previewStage.ModelRoot != null)
                orbitCamera.Setup(previewStage.PreviewCamera, previewStage.ModelRoot, previewStage.InitialDistance);

            return panelGo;
        }
        // ---------- 通用构建辅助 ----------

        private static RectTransform CreateFullScreenPanel(string name, Transform parent, Color bg, string spriteAssetPath = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            var sprite = LoadSprite(spriteAssetPath);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
            }
            else
            {
                image.color = bg;
            }
            return rect;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[BootstrapSceneBuilder] 未找到背景图: {assetPath}，使用纯色占位。");
            }
            return sprite;
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = Color.white;
            ApplyChineseFont(text);
            return text;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.15f);

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = new Color(0.9f, 0.7f, 0.2f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            return slider;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var bgImage = go.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.1f);

            var textAreaGo = new GameObject("Text Area", typeof(RectTransform));
            textAreaGo.transform.SetParent(go.transform, false);
            var textAreaRect = textAreaGo.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(20, 10);
            textAreaRect.offsetMax = new Vector2(-20, -10);
            textAreaGo.AddComponent<RectMask2D>();

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textAreaGo.transform, false);
            var placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 30;
            placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
            placeholderText.fontStyle = FontStyles.Italic;
            ApplyChineseFont(placeholderText);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(textAreaGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var inputText = textGo.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 30;
            inputText.color = Color.white;
            ApplyChineseFont(inputText);

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRect;
            input.textComponent = inputText;
            input.placeholder = placeholderText;

            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            return CreateButton(parent, name, label, new Vector2(0.5f, 0.5f), anchoredPos, size);
        }

        /// <summary>
        /// 纯色矩形按钮。anchor01 同时作为 AnchorMin/AnchorMax/Pivot，
        /// 贴底、贴角这类相对定位用它，避免固定像素坐标在高宽比变化时跑偏。
        /// </summary>
        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor01,
            Vector2 anchoredPos, Vector2 size, Color? fill = null, int fontSize = 32,
            Color? labelColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor01;
            rect.anchorMax = anchor01;
            rect.pivot = anchor01;
            rect.sizeDelta = size * UIButtonSkin.Scale;
            rect.anchoredPosition = anchoredPos;

            var image = go.AddComponent<Image>();
            image.color = fill ?? new Color(0.85f, 0.65f, 0.25f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            // label 传空表示这是个纯图标按钮，不挂文字对象。
            if (!string.IsNullOrEmpty(label))
            {
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(go.transform, false);
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.text = label;
                text.fontSize = fontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = labelColor ?? Color.black;
                ApplyChineseFont(text);
            }

            return ApplyButtonSkin(button);
        }

        /// <summary>
        /// 给按钮挂上统一的皮肤组件（圆角/描边/渐变/交互反馈）。
        /// 贴图是运行时生成的，所以只在 Play 模式下可见；这里只是把组件装上去。
        /// </summary>
        private static Button ApplyButtonSkin(Button button)
        {
            if (button != null && !button.TryGetComponent<UIButtonSkin>(out _))
            {
                button.gameObject.AddComponent<UIButtonSkin>();
            }
            return button;
        }

        /// <summary>
        /// 创建以贴图为背景（而非纯色矩形）的按钮，锚点用相对锚点（anchor01同时作为
        /// AnchorMin/AnchorMax/Pivot），配合贴底部等相对定位方式，避免分辨率变化时
        /// 固定像素坐标跑偏。贴图按原始宽高比显示（preserveAspect），不会被拉伸变形。
        /// </summary>
        private static Button CreateImageButton(Transform parent, string name, string label, string spriteAssetPath,
            Vector2 anchor01, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor01;
            rect.anchorMax = anchor01;
            rect.pivot = anchor01;
            rect.sizeDelta = size * UIButtonSkin.Scale;
            rect.anchoredPosition = anchoredPos;

            var image = go.AddComponent<Image>();
            var sprite = LoadSprite(spriteAssetPath);
            if (sprite != null)
            {
                image.sprite = sprite;
                // 不用 preserveAspect：它是按整张贴图(含透明留白)的宽高比缩放，
                // 而这类贴图的有效图案往往只占画布中间一小块（大量透明留白），
                // 按整图比例缩放会导致图案在指定的 RectTransform 尺寸里被压扁/挤到看不清。
                // 这里直接信任调用方传入的 size 参数（应按贴图有效内容的真实宽高比设置），
                // 让贴图完整撑满这个尺寸框。
                image.preserveAspect = false;
                image.type = Image.Type.Simple;
            }
            else
            {
                image.color = new Color(0.85f, 0.65f, 0.25f);
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 32;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            ApplyChineseFont(text);

            return button;
        }

        /// <summary>应用中文字体资源（若尚未生成会自动生成一次），避免文字显示为方块。</summary>
        private static void ApplyChineseFont(TMP_Text text)
        {
            var fontAsset = ChineseFontSetup.GetOrCreateFontAsset();
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }
        }

        /// <summary>
        /// 通过 SerializedObject 把值写入私有的 [SerializeField] 字段，
        /// 避免为了让 Editor 脚本赋值而把字段改成 public。
        /// </summary>
        private static void BindSerialized(Object target, System.Collections.Generic.Dictionary<string, Object> fields)
        {
            var so = new SerializedObject(target);
            foreach (var kv in fields)
            {
                var prop = so.FindProperty(kv.Key);
                if (prop == null)
                {
                    Debug.LogWarning($"[BootstrapSceneBuilder] 字段未找到: {target.GetType().Name}.{kv.Key}");
                    continue;
                }
                prop.objectReferenceValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 同 BindSerialized，但写入字符串字段（界面文案只在编辑器侧维护）。
        /// </summary>
        private static void BindSerializedStrings(Object target, System.Collections.Generic.Dictionary<string, string> fields)
        {
            var so = new SerializedObject(target);
            foreach (var kv in fields)
            {
                var prop = so.FindProperty(kv.Key);
                if (prop == null)
                {
                    Debug.LogWarning($"[BootstrapSceneBuilder] 字段未找到: {target.GetType().Name}.{kv.Key}");
                    continue;
                }
                prop.stringValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
