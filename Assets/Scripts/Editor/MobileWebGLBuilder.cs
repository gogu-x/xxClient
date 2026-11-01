using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using XiuXian.Network;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// 手机竖屏 WebGL 构建工具。
    ///
    /// 普通增量 Build 可能只更新 Build/*.data 等产物，而保留输出目录中旧的
    /// index.html 与 TemplateData。本工具会先同步导入自定义模板，在编辑器内存中
    /// 强制应用竖屏设置，再经用户确认后清理旧输出并执行完整构建。
    /// </summary>
    public static class MobileWebGLBuilder
    {
        private const string PlayerSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string TemplateDirectory = "Assets/WebGLTemplates/MobilePortrait";
        private const string TemplateName = "PROJECT:MobilePortrait";
        private const string LastOutputKey = "XiuXian.MobileWebGLBuilder.LastOutput";

        // 开发工具宏。只能通过 PlayerSettings API 改：编辑器运行时 PlayerSettings 常驻内存，
        // 手工编辑 ProjectSettings.asset 会在 Unity 下次保存时被内存里的旧值覆盖。
        private const string DevToolsSymbol = "XIUXIAN_DEV_TOOLS";
        private const string DevToolsMenuPath = "Tools/XiuXian/打包包含开发入口 (XIUXIAN_DEV_TOOLS)";

        private static NamedBuildTarget[] DevToolsTargets => new[]
        {
            NamedBuildTarget.WebGL,
            NamedBuildTarget.iOS,
        };

        [MenuItem(DevToolsMenuPath)]
        public static void ToggleDevTools()
        {
            bool enable = !IsDevToolsEnabled();
            foreach (NamedBuildTarget target in DevToolsTargets)
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                var list = defines.Where(d => !string.IsNullOrWhiteSpace(d) && d != DevToolsSymbol).ToList();
                if (enable)
                {
                    list.Add(DevToolsSymbol);
                }
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }

            AssetDatabase.SaveAssets();
            Menu.SetChecked(DevToolsMenuPath, enable);
            Debug.Log($"[MobileWebGLBuilder] {DevToolsSymbol} = {(enable ? "开启" : "关闭")}（WebGL / iOS），Unity 将重新编译脚本。");
        }

        [MenuItem(DevToolsMenuPath, true)]
        private static bool ToggleDevToolsValidate()
        {
            Menu.SetChecked(DevToolsMenuPath, IsDevToolsEnabled());
            return true;
        }

        /// <summary>
        /// 以 WebGL 平台的符号列表为准，判断打出来的网页包里是否包含开发入口。
        /// </summary>
        public static bool IsDevToolsEnabled()
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL, out string[] defines);
            return defines.Contains(DevToolsSymbol);
        }

        private const string GateConfigAssetPath = "Assets/Resources/GateConfig.asset";

        [MenuItem("Tools/XiuXian/网关地址配置")]
        public static void SelectGateConfig()
        {
            GateConfig config = LoadOrCreateGateConfig();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        private static GateConfig LoadOrCreateGateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GateConfig>(GateConfigAssetPath);
            if (config != null)
            {
                return config;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            config = ScriptableObject.CreateInstance<GateConfig>();
            AssetDatabase.CreateAsset(config, GateConfigAssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>
        /// 校验并让用户确认本次打包使用的网关地址。返回 false 表示取消构建。
        /// </summary>
        private static bool ConfirmGateUrl()
        {
            var config = AssetDatabase.LoadAssetAtPath<GateConfig>(GateConfigAssetPath);
            string url = config != null ? config.gateUrl?.Trim() : null;

            if (!string.IsNullOrEmpty(url) &&
                (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || (uri.Scheme != "ws" && uri.Scheme != "wss")))
            {
                EditorUtility.DisplayDialog(
                    "网关地址无效",
                    $"GateConfig.gateUrl 不是合法的 ws:// 或 wss:// 地址：\n\n{url}\n\n" +
                    "请通过 Tools/XiuXian/网关地址配置 修改。",
                    "确定");
                SelectGateConfig();
                return false;
            }

            string display = string.IsNullOrEmpty(url)
                ? "（未配置）自动使用 网页所在主机:8001/ws"
                : url;
            Debug.Log($"[MobileWebGLBuilder] 网关地址：{display}");

            int choice = EditorUtility.DisplayDialogComplex(
                "确认网关地址",
                $"本次打包的网关地址：\n\n{display}",
                "继续构建",
                "取消",
                "修改地址");
            if (choice == 2)
            {
                SelectGateConfig();
            }
            return choice == 0;
        }

        [MenuItem("Tools/XiuXian/应用 WebGL 手机版设置")]
        public static void ApplySettingsFromMenu()
        {
            if (!TryApplySettings(out string error))
            {
                EditorUtility.DisplayDialog("WebGL 设置失败", error, "确定");
                return;
            }

            EditorUtility.DisplayDialog(
                "WebGL 设置已应用",
                "已设置为 1080×1920、竖屏和 MobilePortrait 模板。\n\n" +
                "请使用“构建 WebGL 手机版（清理旧输出）”生成网页。",
                "确定");
        }

        [MenuItem("Tools/XiuXian/构建 WebGL 手机版（清理旧输出）")]
        public static void BuildMobileWebGL()
        {
            if (!TryApplySettings(out string settingsError))
            {
                EditorUtility.DisplayDialog("WebGL 设置失败", settingsError, "确定");
                return;
            }

            if (!ConfirmGateUrl())
            {
                return;
            }

            string defaultOutput = EditorPrefs.GetString(LastOutputKey, GetDefaultOutputPath());
            string selectedOutput = EditorUtility.SaveFolderPanel(
                "选择 WebGL 输出目录（目录内旧文件将被清理）",
                Path.GetDirectoryName(defaultOutput),
                Path.GetFileName(defaultOutput));

            if (string.IsNullOrEmpty(selectedOutput))
            {
                return;
            }

            string outputPath = Path.GetFullPath(selectedOutput);
            if (!IsSafeOutputDirectory(outputPath, out string safetyError))
            {
                EditorUtility.DisplayDialog("不能使用该输出目录", safetyError, "确定");
                return;
            }

            if (Directory.Exists(outputPath) && Directory.EnumerateFileSystemEntries(outputPath).Any())
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "清理旧 WebGL 输出",
                    $"为确保新模板生效，将删除以下目录内的全部旧文件：\n\n{outputPath}\n\n" +
                    "请确认该目录只用于 WebGL 构建输出。",
                    "清理并构建",
                    "取消");

                if (!confirmed)
                {
                    return;
                }
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("无法构建", "Build Settings 中没有启用的场景。", "确定");
                return;
            }

            try
            {
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
                Directory.CreateDirectory(outputPath);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "清理输出目录失败",
                    $"无法清理：{outputPath}\n\n{exception.Message}\n\n" +
                    "请先关闭正在占用该目录的本地服务器或文件窗口。",
                    "确定");
                return;
            }

            EditorPrefs.SetString(LastOutputKey, outputPath);

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.CleanBuildCache,
            };

            Debug.Log($"[MobileWebGLBuilder] 开发入口 {DevToolsSymbol}：" +
                      $"{(IsDevToolsEnabled() ? "已包含（登录页会显示开发账号按钮）" : "未包含")}");
            Debug.Log($"[MobileWebGLBuilder] 开始完整构建：{outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    "WebGL 构建失败",
                    $"构建结果：{report.summary.result}\n错误数：{report.summary.totalErrors}\n" +
                    "请在 Console 中查看详细错误。",
                    "确定");
                return;
            }

            if (!ValidateBuildOutput(outputPath, out string validationError))
            {
                Debug.LogError($"[MobileWebGLBuilder] {validationError}");
                EditorUtility.DisplayDialog("构建输出校验失败", validationError, "确定");
                return;
            }

            Debug.Log($"[MobileWebGLBuilder] 构建并校验成功：{outputPath}");
            EditorUtility.DisplayDialog(
                "WebGL 手机版构建成功",
                $"输出目录：\n{outputPath}\n\n" +
                "已确认使用 MobilePortrait 模板，Canvas 初始尺寸为 1080×1920。",
                "打开目录");
            EditorUtility.RevealInFinder(Path.Combine(outputPath, "index.html"));
        }

        private static bool TryApplySettings(out string error)
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!AssetDatabase.IsValidFolder(TemplateDirectory) ||
                    !File.Exists(Path.Combine(TemplateDirectory, "index.html")))
                {
                    error = $"找不到 WebGL 模板：{TemplateDirectory}/index.html";
                    return false;
                }

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(PlayerSettingsPath);
                if (assets == null || assets.Length == 0)
                {
                    error = $"无法加载 {PlayerSettingsPath}";
                    return false;
                }

                var settings = new SerializedObject(assets[0]);
                // 固定为正常竖屏。Unity 6000.5 的 Device Simulator 不接受 AutoRotation 作为初始 UIOrientation。
                SetInteger(settings, "defaultScreenOrientation", (int)ScreenOrientation.Portrait);
                SetBoolean(settings, "allowedAutorotateToPortrait", true);
                SetBoolean(settings, "allowedAutorotateToPortraitUpsideDown", false);
                SetBoolean(settings, "allowedAutorotateToLandscapeRight", false);
                SetBoolean(settings, "allowedAutorotateToLandscapeLeft", false);
                SetBoolean(settings, "useOSAutorotation", true);
                SetInteger(settings, "defaultScreenWidthWeb", 1080);
                SetInteger(settings, "defaultScreenHeightWeb", 1920);
                SetString(settings, "webGLTemplate", TemplateName);
                SetBoolean(settings, "m_ShowUnitySplashScreen", false);
                SetBoolean(settings, "m_ShowUnitySplashLogo", false);
                settings.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.SaveAssets();
                error = null;
                Debug.Log("[MobileWebGLBuilder] 已应用 1080×1920 竖屏与 MobilePortrait 模板。");
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogException(exception);
                return false;
            }
        }

        private static void SetInteger(SerializedObject settings, string name, int value)
        {
            SerializedProperty property = settings.FindProperty(name)
                ?? throw new InvalidOperationException($"PlayerSettings 缺少字段：{name}");
            property.intValue = value;
        }

        private static void SetString(SerializedObject settings, string name, string value)
        {
            SerializedProperty property = settings.FindProperty(name)
                ?? throw new InvalidOperationException($"PlayerSettings 缺少字段：{name}");
            property.stringValue = value;
        }

        private static void SetBoolean(SerializedObject settings, string name, bool value)
        {
            SerializedProperty property = settings.FindProperty(name)
                ?? throw new InvalidOperationException($"PlayerSettings 缺少字段：{name}");
            property.boolValue = value;
        }

        private static bool ValidateBuildOutput(string outputPath, out string error)
        {
            string indexPath = Path.Combine(outputPath, "index.html");
            if (!File.Exists(indexPath))
            {
                error = $"构建完成但未找到：{indexPath}";
                return false;
            }

            string html = File.ReadAllText(indexPath);
            bool usesMobileTemplate = html.Contains("id=\"loading-screen\"") &&
                                      html.Contains("id=\"orientation-hint\"");
            bool usesPortraitSize = html.Contains("width=\"1080\"") &&
                                    html.Contains("height=\"1920\"");
            bool containsDefaultBranding = html.Contains("id=\"unity-logo\"") ||
                                           html.Contains("id=\"unity-footer\"") ||
                                           html.Contains("unity-webgl-logo");

            if (!usesMobileTemplate || !usesPortraitSize || containsDefaultBranding)
            {
                error = "index.html 不是预期的 MobilePortrait 1080×1920 输出，" +
                        "或仍包含 Unity 默认网页 Logo。请查看 Console 和 Player Settings。";
                return false;
            }

            error = null;
            return true;
        }

        private static string GetDefaultOutputPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, "..", "web"));
        }

        private static bool IsSafeOutputDirectory(string outputPath, out string error)
        {
            string normalized = outputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string root = Path.GetPathRoot(normalized)?.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsPath = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.IsNullOrEmpty(normalized) ||
                string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, projectRoot, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "不能选择磁盘根目录、Unity 项目根目录或 Assets 目录。请选择专用的 WebGL 输出目录。";
                return false;
            }

            error = null;
            return true;
        }
    }
}
