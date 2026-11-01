using System.Collections.Generic;
using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>
    /// 简单的单场景 UI 面板管理器：Home（启动+选服+登录合一）、Main（大厅）、
    /// 以及大厅内各建筑对应的子面板（仙府等）都是同一场景下的 GameObject，
    /// 通过 Show/Hide 切换，避免多场景加载的开销。
    ///
    /// 面板绑定通过 screenBindings 列表在 Inspector 里配置，新增面板时
    /// 只需要在场景里拖一条绑定，不需要改这个类的代码。
    ///
    /// DefaultExecutionOrder(-1000) 确保本类的 Awake() 一定早于场景里其他
    /// 依赖 UIManager.Instance 的面板脚本（HomePanel/MainPanel 等）执行，
    /// 避免出现 Instance 还是 null 时就被访问导致的 NullReferenceException。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;

        public static UIManager Instance
        {
            get
            {
                // Scene panels can be enabled before the singleton is registered (for
                // example after entering Play Mode with domain reload disabled). Recover
                // the active scene instance instead of exposing a transient null.
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<UIManager>();
                }

                return _instance;
            }
            private set => _instance = value;
        }

        [System.Serializable]
        public struct ScreenBinding
        {
            public UIScreen screen;
            public GameObject panel;
        }

        [SerializeField] private List<ScreenBinding> screenBindings = new();

        private Dictionary<UIScreen, GameObject> _panels;

        /// <summary>玩家在选服弹窗中选中的服务器，登录时组装 LoginReq 使用。</summary>
        public ServerInfo SelectedServer { get; private set; }

        /// <summary>
        /// 供编辑器场景生成脚本（BootstrapSceneBuilder）调用，直接写入绑定列表。
        /// 运行时/正常使用场景下应在 Inspector 里配置 screenBindings，不需要调用这个方法。
        /// </summary>
        public void SetBindingsForEditorSetup(List<ScreenBinding> bindings)
        {
            screenBindings = bindings;
        }

private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsurePanels();
            EnsureAdaptiveCanvas();
        }

private void EnsureAdaptiveCanvas()
        {
            foreach (ScreenBinding binding in screenBindings)
            {
                if (binding.panel == null) continue;

                Canvas canvas = binding.panel.GetComponentInParent<Canvas>();
                if (canvas == null) continue;

                if (canvas.GetComponent<MobileAdaptiveCanvasScaler>() == null)
                {
                    canvas.gameObject.AddComponent<MobileAdaptiveCanvasScaler>();
                }
                return;
            }
        }

private void EnsurePanels()
        {
            if (_panels != null)
                return;

            _panels = new Dictionary<UIScreen, GameObject>();
            foreach (var binding in screenBindings)
            {
                if (binding.panel != null)
                    _panels[binding.screen] = binding.panel;
            }
        }


        public void SetSelectedServer(ServerInfo server)
        {
            SelectedServer = server;
        }

public void RegisterScreen(UIScreen screen, GameObject panel)
        {
            if (panel == null)
                return;

            EnsurePanels();
            _panels[screen] = panel;
            // Keep runtime screens discoverable after an Editor script reload too.
            // Otherwise their still-active objects can cover the next screen.
            int index = screenBindings.FindIndex(binding => binding.screen == screen);
            var runtimeBinding = new ScreenBinding { screen = screen, panel = panel };
            if (index >= 0) screenBindings[index] = runtimeBinding;
            else screenBindings.Add(runtimeBinding);
        }

public void HideLegacyPanels()
        {
            EnsurePanels();
            foreach (var panel in _panels.Values)
            {
                if (panel == null) continue;
                var battle = panel.GetComponent<BattleView>();
                if (battle != null) battle.SetScreenVisible(false);
                else panel.SetActive(false);
            }
        }

public void Show(UIScreen screen)
        {
            // Login is an account boundary, not a minimized gameplay screen.
            if (screen == UIScreen.Home && BattleView.HasResumableBattle)
            {
                var battle = FindAnyObjectByType<BattleView>(FindObjectsInactive.Include);
                if (battle != null) battle.CloseBattleSession();
            }
            if(Application.isPlaying && ImmortalUIArtPreview.RouteLive(screen))return;
            EnsurePanels();
            foreach (var kv in _panels)
            {
                if (kv.Value == null)
                    continue;
                var battle = kv.Value.GetComponent<BattleView>();
                if (battle != null) battle.SetScreenVisible(kv.Key == screen);
                else kv.Value.SetActive(kv.Key == screen);
            }
        }
    }
}
