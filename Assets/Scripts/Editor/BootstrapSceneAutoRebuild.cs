using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// 让 BootstrapSceneBuilder 的改动自动生效，不必再手动点菜单。
    ///
    /// 手动点菜单有个几乎必然踩到的陷阱：在外部编辑器改完脚本后如果立刻点菜单，
    /// Unity 可能还没重新编译完，执行的仍是上一次的程序集 —— 生成出来的场景
    /// 看起来"改了没反应"。这个钩子在**编译完成后的域重载**里运行，天然不可能用到旧程序集。
    ///
    /// 安全约束（缺一不可，否则会覆盖你正在编辑的内容）：
    ///   1. 当前打开的必须就是生成的 Bootstrap 场景；
    ///   2. 该场景没有未保存修改；
    ///   3. 不在 Play 模式或模式切换中。
    /// 想临时关掉：菜单 Tools > XiuXian > 自动重建场景（开关）。
    /// </summary>
    [InitializeOnLoad]
    public static class BootstrapSceneAutoRebuild
    {
        private const string EnabledKey = "XiuXian.BootstrapSceneAutoRebuild.Enabled";
        private const string HashKey = "XiuXian.BootstrapSceneAutoRebuild.InputHash";

        /// <summary>
        /// 会影响生成结果的文件。新增这一类脚本（比如又一个运行时皮肤组件）时，
        /// 记得把它加进来，否则改它不会触发重建。
        /// </summary>
        private static readonly string[] GenerationInputs =
        {
            "Assets/Scripts/Editor/BootstrapSceneBuilder.cs",
            "Assets/Scripts/Editor/LobbyPrefabBuilder.cs",
            "Assets/Scripts/Editor/ChineseFontSetup.cs",
            "Assets/Scripts/UI/UIButtonSkin.cs",
            "Assets/Scripts/UI/UIGearIcon.cs",
            "Assets/Scripts/UI/LobbyReferenceView.cs",
            "Assets/Art/Resources/UI/Lobby/Lobby_Background.png",
            "Assets/Art/Resources/UI/Lobby/Lobby_Icons.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_14.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_36.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_40.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_43.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_46.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_50.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_53.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_53_57.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_54_11.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 20_58_21.png",
            "Assets/Resources/game/Codex 图像 2026年9月22日 21_22_16.png",
            "Assets/Resources/Fonts/NotoSansSC-VF.ttf",
        };

        /// <summary>层级变化回调触发得很频繁，用它做兜底检查时限流，避免每帧都读 EditorPrefs。</summary>
        private const double CheckIntervalSeconds = 0.5;

        private static double _nextCheckTime;
        private static bool _rebuilding;

        static BootstrapSceneAutoRebuild()
        {
            // 域重载期间 AssetDatabase 尚未就绪，推迟到编辑器空闲再判断。
            EditorApplication.delayCall += TryRebuild;
            // 兜底：打开/切换场景时也可能需要重建（此时未必伴随一次编译）。
            EditorApplication.hierarchyChanged += TryRebuild;
        }

        private static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, true);

        [MenuItem("Tools/XiuXian/自动重建场景 (开关)")]
        private static void ToggleEnabled()
        {
            bool enabled = !IsEnabled;
            EditorPrefs.SetBool(EnabledKey, enabled);
            Debug.Log($"[BootstrapSceneAutoRebuild] 自动重建场景已{(enabled ? "开启" : "关闭")}。");
        }

        private static void TryRebuild()
        {
            if (_rebuilding || !IsEnabled)
            {
                return;
            }
            // 重建会打断运行中的游戏，也会和模式切换打架。
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextCheckTime)
            {
                return;
            }
            _nextCheckTime = now + CheckIntervalSeconds;

            var active = SceneManager.GetActiveScene();
            // 只在生成产物本身上动手，绝不碰其它场景。
            if (active.path != BootstrapSceneBuilder.ScenePath)
            {
                return;
            }
            // 有未保存修改就不动 —— 重建是从零生成，会全部覆盖。
            if (active.isDirty)
            {
                return;
            }

            string hash = ComputeInputHash();
            if (EditorPrefs.GetString(HashKey, string.Empty) == hash)
            {
                return;
            }
            EditorPrefs.SetString(HashKey, hash);

            _rebuilding = true;
            try
            {
                BootstrapSceneBuilder.BuildScene();
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private static string ComputeInputHash()
        {
            var builder = new System.Text.StringBuilder();
            foreach (string path in GenerationInputs)
            {
                builder.Append(path)
                       .Append(':')
                       .Append(AssetDatabase.GetAssetDependencyHash(path).ToString())
                       .Append(';');
            }
            return builder.ToString();
        }
    }
}
