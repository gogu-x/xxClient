using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XiuXian.Network;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    /// <summary>
    /// 首页：启动加载、服务器选择与游客登录。
    /// 登录页使用可组合的 QuietInk 资源，运行时应用到现有 Bootstrap 场景，
    /// 因此不依赖重新生成场景也能立即使用新版界面。
    /// </summary>
    public class HomePanel : MonoBehaviour
    {
#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
        private const string DeveloperAccountPrefsKey = "XiuXian.DeveloperAccount";
#endif

        // 进入按钮的两种文案不写在代码里：由编辑器（Inspector / BootstrapSceneBuilder）填入，
        // 留空时保留场景里已经摆好的文字，避免运行时代码里出现界面文案。
        [SerializeField] private string enterGameLabel;
        [SerializeField] private string createRoleLabel;

        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject readyGroup;
        [SerializeField] private TMP_Text serverNameText;
        [SerializeField] private Button serverPickerButton;
        [SerializeField] private Button enterButton;
        [SerializeField] private ServerPickerPopup serverPickerPopup;

        private Coroutine _startupRoutine;
        private NetworkManager _startupConnection;
        private System.Action _startupConnectedHandler;
        private System.Action<string> _startupErrorHandler;
        private Button _repairButton;
        private Button _announcementButton;
        private Button _switchAccountButton;
#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
        private Button _developerAccountButton;
        private GameObject _developerAccountDialog;
        private TMP_InputField _developerAccountInput;
#endif
        private GameObject _messageDialog;
        private TMP_Text _messageDialogTitle;
        private TMP_Text _messageDialogBody;
        private TMP_Text _enterButtonText;
        private readonly List<ServerInfo> _availableServers = new();
        private bool _serverListQueryFinished;
        private bool _needsRegistration;
        private string _serverListQueryError;
        private string _account;

        private void Awake()
        {
            ApplyLoginTheme();
        }

        private void OnEnable()
        {
            RefreshServerNameText();

            BindButton(serverPickerButton, OnServerPickerClicked);
            BindButton(enterButton, OnEnterClicked);



#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS

#endif

            _enterButtonText = enterButton != null ? enterButton.GetComponentInChildren<TMP_Text>(true) : null;

            // 编辑器里生成/加载场景时 OnEnable 也会被调用。以下部分要碰网络层和启动流程，
            // 只能在运行时做：编辑模式下 NetworkManager.Instance 会自己 new 一个不属于
            // 场景的对象，关闭场景时就会报 "Some objects were not cleaned up"。
            if (!Application.isPlaying)
            {
                return;
            }

            RestoreCoreControls();
            SetStartupVisuals(true);

            var net = NetworkManager.Instance;
            net.RegisterProtoHandler("ServerListAck", global::ServerListAck.Parser, OnServerListAck);
            net.RegisterProtoHandler("RegisterAck", global::RegisterAck.Parser, OnRegisterAck);
            net.RegisterProtoHandler("LoginAck", global::LoginAck.Parser, OnLoginAck);

            StartStartupFlow();
        }

        private void OnDisable()
        {
            StopStartupFlow();

            if (_messageDialog != null)
            {
                _messageDialog.SetActive(false);
            }

            if (_moreDialog != null) _moreDialog.SetActive(false);
            if (_registrationDialog != null) _registrationDialog.SetActive(false);
            if (serverPickerPopup != null) serverPickerPopup.Hide();
#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
            if (_developerAccountDialog != null) _developerAccountDialog.SetActive(false);
#endif
            // 编辑器里关闭/重建场景时 OnDisable 也会被调用。那一瞬间场景正在被拆解，
            // NetworkManager.Instance 会自己 new 出第二个 NetworkManager，而这个对象
            // 不属于任何场景 —— 正是 "Some objects were not cleaned up when closing
            // the scene" 的来源。运行时才需要反注册，编辑模式下直接跳过。
            if (!Application.isPlaying)
            {
                return;
            }


            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.UnregisterHandler("ServerListAck");
                net.UnregisterHandler("RegisterAck");
                net.UnregisterHandler("LoginAck");
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void StartStartupFlow()
        {
            // 编辑器里挂载/生成这个面板时 OnEnable 也会被调用，但那时候不该真的去连服务器，
            // 而且 UIManager.Instance 可能还没就绪。只在运行时跑这套启动流程。
            if (!Application.isPlaying)
            {
                return;
            }

            StopStartupFlow();

            ClearRoleSession();
            _serverListQueryFinished = false;
            _serverListQueryError = null;
            _needsRegistration = false;
            _availableServers.Clear();
            UIManager.Instance?.SetSelectedServer(null);
            serverPickerPopup?.SetServers(_availableServers);
            RefreshServerNameText();
            _startupRoutine = StartCoroutine(RunStartupFlow());
        }

        private void StopStartupFlow()
        {
            // StopCoroutine does not guarantee iterator cleanup. Unsubscribe explicitly on every stop path.
            ClearStartupConnectionHandlers();
            if (_startupRoutine == null) return;
            StopCoroutine(_startupRoutine);
            _startupRoutine = null;
        }

        private void ClearStartupConnectionHandlers()
        {
            // Use the actual subscribed instance, without creating a singleton while the scene is closing.
            if (_startupConnection != null)
            {
                _startupConnection.OnConnected -= _startupConnectedHandler;
                _startupConnection.OnError -= _startupErrorHandler;
            }
            _startupConnection = null;
            _startupConnectedHandler = null;
            _startupErrorHandler = null;
        }

        private IEnumerator RunStartupFlow()
        {
            SetStartupVisuals(true);
            SetProgress(0f, "初始化...");
            yield return null;

            SetProgress(0.2f, "连接服务器...");


            var net = NetworkManager.Instance;
            bool connected = net.IsConnected;
            bool failed = false;

            if (!connected)
            {
                _startupConnection = net;
                _startupConnectedHandler = () => connected = true;
                _startupErrorHandler = _ => failed = true;
                net.OnConnected += _startupConnectedHandler;
                net.OnError += _startupErrorHandler;
                try
                {
                    net.Connect();

                    float timeout = 8f;
                    while (!connected && !failed && timeout > 0f)
                    {
                        timeout -= Time.deltaTime;
                        yield return null;
                    }
                }
                finally
                {
                    ClearStartupConnectionHandlers();
                }
            }

            if (!connected)
            {
                SetProgress(0.2f, "连接失败，请检查网络或点击底部“更多”中的“重新连接”");
                _startupRoutine = null;
                yield break;
            }

            SetProgress(0.6f, "加载配置...");
            yield return new WaitForSeconds(0.3f);

            SetProgress(0.75f, "正在查询角色...");
            _account = GetAccountForStartup();
            net.SendProto(new global::GetServerListReq { Account = _account });

            float queryTimeout = 8f;
            while (!_serverListQueryFinished && queryTimeout > 0f)
            {
                queryTimeout -= Time.deltaTime;
                yield return null;
            }

            if (!_serverListQueryFinished)
            {
                _serverListQueryError = "查询角色超时，请点击底部“更多”中的“重新连接”后重试。";
            }

            if (!string.IsNullOrEmpty(_serverListQueryError))
            {
                SetProgress(0.75f, _serverListQueryError);
                _startupRoutine = null;
                yield break;
            }

            SetProgress(1f, _needsRegistration ? "未找到角色，请选择服务器创建角色" : "角色加载完成");
            yield return new WaitForSeconds(0.2f);

            SetStartupVisuals(false);
            UpdateEnterButtonText();
            _startupRoutine = null;
        }

        private void RestoreCoreControls()
        {
            // Unity objects use custom null semantics; explicit checks also handle destroyed references.
            if (progressBar != null) progressBar.transform.SetAsLastSibling();
            if (statusText != null) statusText.transform.SetAsLastSibling();
            if (readyGroup != null) readyGroup.transform.SetAsLastSibling();
            if (serverPickerPopup != null) serverPickerPopup.transform.SetAsLastSibling();
        }
        private void SetStartupVisuals(bool loading)
        {
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(loading);
            }
            if (statusText != null)
            {
                statusText.gameObject.SetActive(loading);
            }
            if (readyGroup != null)
            {
                readyGroup.SetActive(!loading);
            }
            if (enterButton != null)
            {
                enterButton.interactable = true;
            }
        }

        private void OnRepairClicked()
        {
            StopStartupFlow();

            NetworkManager.Instance.Disconnect();
            SetProgress(0f, "正在重新连接...");
            _startupRoutine = StartCoroutine(RestartAfterDisconnect());
        }

        private IEnumerator RestartAfterDisconnect()
        {
            SetStartupVisuals(true);
            yield return null;
            _startupRoutine = null;
            StartStartupFlow();
        }

        private void OnAnnouncementClicked()
        {
            ShowMessage("公告", "公告服务尚未接入。");
        }

        private void OnSwitchAccountClicked()
        {
            ShowMessage("账号说明", "当前版本使用本机游客账号一键登录，正式账号登录与账号绑定功能尚未接入。\n\n为避免游客进度丢失，本次不会自动清除本地账号。");
        }

        private string GetAccountForStartup()
        {
#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
            var developerAccount = PlayerPrefs.GetString(DeveloperAccountPrefsKey, string.Empty).Trim();
            if (!string.IsNullOrEmpty(developerAccount))
            {
                return developerAccount;
            }
#endif
            return GuestAccount.GetOrCreateAccount();
        }

#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
        private void OnDeveloperAccountClicked()
        {
            if (_developerAccountDialog == null) BuildDeveloperAccountDialog();
            if (_developerAccountDialog == null || _developerAccountInput == null)
            {
                return;
            }

            _developerAccountInput.text = _account ?? PlayerPrefs.GetString(DeveloperAccountPrefsKey, string.Empty);
            _developerAccountDialog.SetActive(true);
            _developerAccountDialog.transform.SetAsLastSibling();
            _developerAccountInput.Select();
            _developerAccountInput.ActivateInputField();
        }

        private void ConfirmDeveloperAccount()
        {
            var account = _developerAccountInput.text.Trim();
            if (string.IsNullOrEmpty(account))
            {
                _developerAccountDialog.SetActive(false);
                ShowMessage("\u5f00\u53d1\u8d26\u53f7\u4e0d\u80fd\u4e3a\u7a7a", "\u8bf7\u8f93\u5165\u8d26\u53f7\u540e\u518d\u91cd\u65b0\u67e5\u8be2\u89d2\u8272\u3002");
                return;
            }

            PlayerPrefs.SetString(DeveloperAccountPrefsKey, account);
            PlayerPrefs.Save();
            _account = account;
            _developerAccountDialog.SetActive(false);
            // 切换账号时重建传输，旧账号在途的登录回包不能进入新会话。
            StopStartupFlow();
            NetworkManager.Instance.Disconnect();
            StartStartupFlow();
        }
#endif

        private void OnServerListAck(global::ServerListAck response)
        {
            var accountLogs = new List<string>(response.Accounts.Count);
            foreach (var account in response.Accounts)
            {
                accountLogs.Add($"{{ \"server_id\": {account.ServerId}, \"uid\": {account.Uid} }}");
            }
            Debug.Log(NetworkManagerExtensions.ColorizeNetworkLog(
                $"[Proto] <= ServerListAck {{ \"code\": \"{response.Code}\", \"accounts\": [{string.Join(", ", accountLogs)}] }}"));

            bool hasNoRole = response.Code == global::ErrCode.Ok && response.Accounts.Count == 0;
            if (response.Code != global::ErrCode.Ok)
            {
                CompleteServerListQuery(LoadoutErrors.ToUserMessage(response.Code));
                return;
            }

            _availableServers.Clear();
            if (hasNoRole)
            {
                _needsRegistration = true;
                _availableServers.AddRange(GetRegistrationServers());
            }
            else
            {
                _needsRegistration = false;
                var serverIds = new HashSet<int>();
                foreach (var account in response.Accounts)
                {
                    if (account.ServerId == 0 || account.ServerId > int.MaxValue)
                    {
                        continue;
                    }

                    int serverId = checked((int)account.ServerId);
                    if (!serverIds.Add(serverId))
                    {
                        continue;
                    }
                    _availableServers.Add(new ServerInfo(
                        serverId,
                        GetServerDisplayName(serverId),
                        true,
                        account.Uid));
                }
            }

            if (_availableServers.Count == 0)
            {
                CompleteServerListQuery("服务器列表为空，请稍后重试。");
                return;
            }

            serverPickerPopup?.SetServers(_availableServers);
            UIManager.Instance?.SetSelectedServer(_availableServers[0]);
            _serverListQueryFinished = true;
            _serverListQueryError = null;
            RefreshServerNameText();
        }

        private void CompleteServerListQuery(string error)
        {
            _serverListQueryError = error;
            _serverListQueryFinished = true;
            Debug.LogWarning($"[HomePanel] {error}");
        }

        private static string EscapeLogString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static bool IsUserNotFound(string message)
        {
            return !string.IsNullOrEmpty(message) &&
                   message.IndexOf("user not found", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<ServerInfo> GetRegistrationServers()
        {
            // GetServerListReq 返回的是当前账号已有角色所在区服，不是全服目录。
            // 新账号注册仍使用客户端当前开放的区服配置。
            return new List<ServerInfo>
            {
                new ServerInfo(1, "青云山 · 一区"),
                new ServerInfo(2, "天启峰 · 二区"),
                new ServerInfo(3, "紫霄宗 · 三区"),
            };
        }

        private static string GetServerDisplayName(int serverId)
        {
            return serverId switch
            {
                1 => "青云山 · 一区",
                2 => "天启峰 · 二区",
                3 => "紫霄宗 · 三区",
                _ => $"服务器 {serverId}",
            };
        }

        private void OnServerPickerClicked()
        {
            if (_serverListQueryFinished && string.IsNullOrEmpty(_serverListQueryError) && serverPickerPopup != null)
            {
                serverPickerPopup.Show(OnServerSelected);
            }
        }

        private void OnServerSelected(ServerInfo server)
        {
            UIManager.Instance.SetSelectedServer(server);
            RefreshServerNameText();
        }

        private void RefreshServerNameText()
        {
            if (serverNameText == null || UIManager.Instance == null)
            {
                return;
            }
            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Debug.LogError("[HomePanel] 当前场景未找到 UIManager，无法继续登录。请检查启动场景中的 UIManager 对象。");
                ShowMessage("暂时无法进入", "界面尚未初始化完成，请返回登录界面后重试。若问题持续，请重新打开游戏。");
                return;
            }

            var server = uiManager.SelectedServer;
            serverNameText.text = server != null
                ? server.Name
                : (_serverListQueryFinished ? "请选择服务器" : "正在查询角色...");
        }

        private void UpdateEnterButtonText()
        {
            if (_enterButtonText == null)
            {
                return;
            }

            var label = _needsRegistration ? createRoleLabel : enterGameLabel;
            if (!string.IsNullOrEmpty(label))
            {
                _enterButtonText.text = label;
            }
        }

        private void OnEnterClicked()
        {
            if (!_serverListQueryFinished || !string.IsNullOrEmpty(_serverListQueryError))
            {
                return;
            }

            var server = UIManager.Instance.SelectedServer;
            if (server == null)
            {
                serverPickerPopup?.Show(OnServerSelected);
                return;
            }

            if (enterButton != null)
            {
                enterButton.interactable = false;
            }

            _account ??= GuestAccount.GetOrCreateAccount();
            if (_needsRegistration) { if(enterButton!=null)enterButton.interactable=true;ShowRegistrationDialog();return; }

            SendLogin(server);
        }

        private void OnRegisterAck(global::RegisterAck ack)
        {
            if (ack.Code != global::ErrCode.Ok)
            {
                if (enterButton != null)
                {
                    enterButton.interactable = true;
                }
                Debug.LogWarning($"[HomePanel] 创建角色失败: code={ack.Code}");
                ShowMessage("创建角色失败", LoadoutErrors.ToUserMessage(ack.Code));
                return;
            }

            // RegisterAck 只完成账号注册；继续 LoginReq 才会由 Gate 转发进入 Game。
            _needsRegistration = false;
            UpdateEnterButtonText();
            SendLogin(UIManager.Instance.SelectedServer);
        }

        private void SendLogin(ServerInfo server)
        {
            if (server == null)
            {
                if (enterButton != null)
                {
                    enterButton.interactable = true;
                }
                return;
            }

            NetworkManager.Instance.SendProto(new global::LoginReq
            {
                Account = _account ?? GuestAccount.GetOrCreateAccount(),
                Password = GuestAccount.GetPassword(),
                ServerId = checked((uint)server.ServerId),
            });
        }

        private void OnLoginAck(global::LoginAck ack)
        {
            if (enterButton != null)
            {
                enterButton.interactable = true;
            }

            if (ack.Code == global::ErrCode.Ok)
            {
                ClearRoleSession();
                UIManager.Instance.Show(UIScreen.Main);
            }
            else
            {
                Debug.LogWarning($"[HomePanel] 登录失败: code={ack.Code}");
                ShowMessage("登录失败", LoadoutErrors.ToUserMessage(ack.Code));
            }
        }

        private GameObject _moreDialog;

        private static void ClearRoleSession()
        {
            RoleEquipmentPanel.ClearSession();
            SoulJadeService.ClearSession();
            TalentService.ClearSession();
            CombatStatsService.ClearSession();
        }

        private void ApplyLoginTheme()
        {
            foreach (Transform child in transform)
                if (serverPickerPopup == null || child != serverPickerPopup.transform)
                    child.gameObject.SetActive(false);
            var root = Q.Design(transform, "LoginQuietInkV2");
            var background = GetComponent<Image>();
            if (background != null)
            {
                background.sprite = null;
                background.color = Q.Paper;
            }
            Q.Art(root, "bg-login", 0, 0, 941, 1672);
            Q.Art(root, "logo", 145, 108, 651, 326, true);
            Q.Text(root, "Tagline", "三千大道 · 自在修仙", 170, 440, 601, 48, 30);
            Q.Art(root, "hero-login", 170, 735, 350, 350, true);
            var ready = Q.Rect(root, "ReadyQuietInk", 0, 0, 941, 1672);
            readyGroup = ready.gameObject;
            serverPickerButton = Q.Button(ready, "SelectServer", "", 220, 1280, 501, 82,
                OnServerPickerClicked, "selector_row");
            serverNameText = Q.Text(serverPickerButton.transform, "ServerName",
                "选择服务器", 25, 10, 411, 62, 30);
            Q.Text(serverPickerButton.transform, "Chevron", "⌄", 432, 8, 46, 62, 34, Q.Jade);
            enterButton = Q.Button(ready, "Enter", "进入仙途", 170, 1390, 601, 118,
                OnEnterClicked, "button_primary", 44);
            enterGameLabel = "进入仙途";
            createRoleLabel = "创建角色";
            Q.Text(root, "AccountHint", "本机游客账号 · 自动登录", 170, 1525, 601, 40, 26, Q.Muted);
            Q.Button(root, "More", "更多  ›", 695, 1582, 170, 64, ShowMoreDialog, null, 30);
            statusText = Q.Text(root, "StartupStatus", "正在连接…", 155, 1268, 631, 116, 30, Q.Ink);
            var progress = Q.Rect(root, "StartupProgress", 220, 1424, 501, 8);
            progress.gameObject.AddComponent<Image>().color = Q.LineColor;
            progressBar = progress.gameObject.AddComponent<Slider>();
            progressBar.interactable = false;
            var fillArea = Q.Rect(progress, "FillArea", 0, 0, 0, 0);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(0, 1);
            fillArea.offsetMax = new Vector2(0, -1);
            var fill = Q.Rect(fillArea, "Fill", 0, 0, 0, 0);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = Q.Jade;
            progressBar.direction = Slider.Direction.LeftToRight;
            progressBar.transition = Selectable.Transition.None;
            progressBar.fillRect = fill;
            progressBar.minValue = 0;
            progressBar.maxValue = 1;
            progressBar.value = 0;
        }

        private void ShowMoreDialog()
        {
            if (_moreDialog == null)
            {
                _moreDialog = new GameObject("LoginMoreDialog", typeof(RectTransform), typeof(Image));
                _moreDialog.transform.SetParent(transform, false);
                StretchToParent((RectTransform)_moreDialog.transform);
                _moreDialog.GetComponent<Image>().color = new Color(Q.Ink.r, Q.Ink.g, Q.Ink.b, .42f);
                var d = Q.Design(_moreDialog.transform);
                LoginCelestialSkin.Art(d, LoginCelestialSkin.Panel, 135, 420, 671, 840);
                LoginCelestialSkin.Text(d, "Title", "更多", 230, 480, 481, 80, 44);
                Q.Divider(d, 230, 574, 481);
                _announcementButton = LoginCelestialSkin.Button(d, "Announcement", "公告", 235, 600, 471, 78,
                    () => { _moreDialog.SetActive(false); OnAnnouncementClicked(); });
                _switchAccountButton = LoginCelestialSkin.Button(d, "Account", "账号说明", 235, 710, 471, 78,
                    () => { _moreDialog.SetActive(false); OnSwitchAccountClicked(); });
                _repairButton = LoginCelestialSkin.Button(d, "Reconnect", "重新连接", 235, 820, 471, 78,
                    () => { _moreDialog.SetActive(false); OnRepairClicked(); }, LoginCelestialSkin.Primary);
#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
                _developerAccountButton = LoginCelestialSkin.Button(d, "DeveloperAccount", "开发账号", 235, 930, 471, 78,
                    () => { _moreDialog.SetActive(false); OnDeveloperAccountClicked(); });
#endif
                Q.BackButton(d, "Close", 390, 1093, () => _moreDialog.SetActive(false));
            }
            _moreDialog.SetActive(true);
            _moreDialog.transform.SetAsLastSibling();
        }
#if UNITY_EDITOR || XIUXIAN_DEV_TOOLS
        private void BuildDeveloperAccountDialog()
        {
            _developerAccountDialog = new GameObject("DeveloperAccountDialog", typeof(RectTransform), typeof(Image));
            _developerAccountDialog.transform.SetParent(transform, false);
            StretchToParent((RectTransform)_developerAccountDialog.transform);
            _developerAccountDialog.GetComponent<Image>().color = new Color(Q.Ink.r, Q.Ink.g, Q.Ink.b, .42f);
            var design = Q.Design(_developerAccountDialog.transform);
            Q.Panel(design, "Panel", 90, 560, 761, 540, "dialog_panel");
            Q.Text(design, "Title", "开发环境账号", 150, 608, 641, 70, 42);
            Q.Text(design, "Hint", "输入后将重新查询该账号的角色数据", 150, 687, 641, 54, 26, Q.Muted);
            var inputImage = Q.Panel(design, "AccountInput", 150, 772, 641, 86, "selector_row");
            inputImage.raycastTarget = true;
            _developerAccountInput = inputImage.gameObject.AddComponent<TMP_InputField>();
            _developerAccountInput.targetGraphic = inputImage;
            _developerAccountInput.characterLimit = 64;
            var viewport = Q.Rect(inputImage.transform, "Viewport", 24, 10, 593, 66);
            viewport.gameObject.AddComponent<RectMask2D>();
            _developerAccountInput.textViewport = viewport;
            var placeholder = Q.Text(viewport, "Placeholder", "请输入账号", 0, 0, 593, 66, 30,
                Q.Muted, TextAlignmentOptions.Left);
            var inputText = Q.Text(viewport, "Text", "", 0, 0, 593, 66, 30,
                Q.Ink, TextAlignmentOptions.Left);
            inputText.enableWordWrapping = false;
            _developerAccountInput.placeholder = placeholder;
            _developerAccountInput.textComponent = inputText;
            Q.Button(design, "ConfirmButton", "确定", 150, 938, 300, 88,
                ConfirmDeveloperAccount, "button_primary", 34);
            Q.Button(design, "CancelButton", "取消", 491, 938, 300, 88,
                () => _developerAccountDialog.SetActive(false), "button_secondary", 34);
            _developerAccountDialog.SetActive(false);
        }
#endif

        private GameObject _registrationDialog;
        private void ShowRegistrationDialog()
        {
            if (_registrationDialog != null) Destroy(_registrationDialog);
            _registrationDialog = new GameObject("RegistrationQuietInk", typeof(RectTransform), typeof(Image));
            _registrationDialog.transform.SetParent(transform, false);
            StretchToParent((RectTransform)_registrationDialog.transform);
            _registrationDialog.GetComponent<Image>().color = new Color(Q.Ink.r, Q.Ink.g, Q.Ink.b, .42f);
            var d = Q.Design(_registrationDialog.transform);
            Q.Panel(d, "Panel", 80, 360, 781, 1050, "dialog_panel");
            Q.Text(d, "Title", "创建角色", 140, 420, 661, 80, 48);
            Q.Divider(d, 140, 526, 661);
            string[] names = { "登录方式", "账号凭据", "选择区服" };
            string[] values =
            {
                "本机游客账号", "自动生成并保存在本机",
                UIManager.Instance.SelectedServer == null ? "尚未选择" : UIManager.Instance.SelectedServer.Name
            };
            for (int i = 0; i < names.Length; i++)
            {
                float y = 570 + i * 150;
                Q.Text(d, "FieldLabel" + i, names[i], 145, y, 651, 44, 27, Q.Muted, TextAlignmentOptions.Left);
                Q.Text(d, "FieldValue" + i, values[i], 145, y + 48, 651, 58, 34, Q.Ink, TextAlignmentOptions.Left);
                Q.Divider(d, 145, y + 117, 651);
            }
            Q.Text(d, "AccountSupport", "当前仅支持游客注册。自定义账号与密码尚未接入。",
                145, 1054, 651, 78, 27, Q.Muted);
            Q.Button(d, "Create", "确认创建", 210, 1180, 521, 102, () =>
            {
                var server = UIManager.Instance.SelectedServer;
                if (server == null)
                {
                    _registrationDialog.SetActive(false);
                    serverPickerPopup?.Show(OnServerSelected);
                    return;
                }
                _registrationDialog.SetActive(false);
                if (enterButton != null) enterButton.interactable = false;
                _account ??= GuestAccount.GetOrCreateAccount();
                NetworkManager.Instance.SendProto(new global::RegisterReq
                {
                    Account = _account, Password = GuestAccount.GetPassword(),
                    ServerId = checked((uint)server.ServerId)
                });
            }, "button_primary", 40);
            Q.BackButton(d, "Cancel", 390, 1285,
                () => _registrationDialog.SetActive(false));
            _registrationDialog.transform.SetAsLastSibling();
        }

        private void BuildMessageDialog()
        {
            _messageDialog = new GameObject("LoginMessageDialog", typeof(RectTransform), typeof(Image));
            _messageDialog.transform.SetParent(transform, false);
            StretchToParent((RectTransform)_messageDialog.transform);
            _messageDialog.GetComponent<Image>().color = new Color(Q.Ink.r, Q.Ink.g, Q.Ink.b, .42f);
            var d = Q.Design(_messageDialog.transform);
            Q.Panel(d, "Panel", 95, 470, 751, 750, "dialog_panel");
            _messageDialogTitle = Q.Text(d, "Title", "提示", 140, 530, 661, 75, 44);
            Q.Divider(d, 150, 628, 641);
            _messageDialogBody = Q.Text(d, "Body", "", 150, 670, 641, 330, 30,
                Q.Ink, TextAlignmentOptions.TopLeft);
            Q.Button(d, "Confirm", "确定", 250, 1080, 441, 95,
                () => _messageDialog.SetActive(false), "button_primary", 38);
            _messageDialog.transform.SetAsLastSibling();
            _messageDialog.SetActive(false);
        }

        private void ShowMessage(string title, string body)
        {
            if (_messageDialog == null) BuildMessageDialog();
            if (_messageDialog == null)
            {
                return;
            }

            _messageDialogTitle.text = title;
            _messageDialogBody.text = body;
            _messageDialog.SetActive(true);
            _messageDialog.transform.SetAsLastSibling();
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetProgress(float value, string status)
        {
            if (progressBar != null)
            {
                progressBar.value = Mathf.Clamp01(value);
            }
            if (statusText != null)
            {
                statusText.text = status;
            }
        }
    }
}
