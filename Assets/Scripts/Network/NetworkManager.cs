using System;
using System.Collections.Generic;
using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>
    /// WebSocket 网络管理器，负责与 gate 服务建立连接、收发 protobuf 消息。
    ///
    /// 传输层基于 NativeWebSocket（同时支持 WebGL 和 Native 平台），
    /// 消息编解码格式对齐后端 gogs/codec.ProtoCodec：
    ///   | 2字节 msgID (BigEndian) | protobuf 消息体 |
    /// 详见 FrameCodec、MsgIdRegistry。
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        private static NetworkManager _instance;

        /// <summary>
        /// Play 模式退出时重置静态单例引用。
        ///
        /// 项目若启用了"Enter Play Mode Options"里的"Reload Domain"关闭选项，
        /// 静态字段在两次 Play 之间不会被清空，会导致 _instance 指向上一轮已销毁、
        /// 残留在编辑器里的 NetworkManager（表现为 Unity 提示
        /// "Some objects were not cleaned up when closing the scene"，
        /// 并且这次 Play 拿到的连接状态实际上是上一轮遗留的）。
        /// 这里用 RuntimeInitializeOnLoadMethod 在每次进入 Play 模式时强制清空，确保
        /// NetworkManager.Instance 总是取到本轮新创建的实例。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnPlayModeEnter()
        {
            _instance = null;
        }

        /// <summary>
        /// 懒加载单例：优先复用场景中已有实例（如 Bootstrap 场景里放置的 NetworkManager），
        /// 找不到则自动创建一个。这样不依赖脚本执行顺序 —— 任何脚本在自己的 OnEnable/Awake
        /// 里访问 NetworkManager.Instance 都能拿到有效对象，不会出现 Awake 顺序竞争导致的 null。
        /// </summary>
        public static NetworkManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<NetworkManager>();
                }
                if (_instance == null)
                {
                    var go = new GameObject("NetworkManager");
                    _instance = go.AddComponent<NetworkManager>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// gate 地址解析顺序：
        /// 1. 打包时写入 Resources/GateConfig.asset 的 gateUrl（非空即用）；
        /// 2. WebGL 客户端连接托管当前页面的主机的 8001 端口；
        /// 3. Unity Editor 和原生客户端使用本机开发地址。
        /// 手机浏览器中的 127.0.0.1 指向手机自身，因此不能用于 WebGL。
        /// </summary>
        public static string DefaultGateUrl
        {
            get
            {
                string configured = GateConfig.LoadConfiguredUrl();
                if (!string.IsNullOrEmpty(configured))
                {
                    return configured;
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var pageUri) &&
                    (pageUri.Scheme == Uri.UriSchemeHttp || pageUri.Scheme == Uri.UriSchemeHttps))
                {
                    var socketUri = new UriBuilder(pageUri)
                    {
                        Scheme = pageUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
                        Port = 8001,
                        Path = "/ws",
                        Query = string.Empty,
                        Fragment = string.Empty
                    };
                    return socketUri.Uri.ToString();
                }
#endif
                return "ws://127.0.0.1:8001/ws";
            }
        }

        private string serverUrl;

        private IWebSocketTransport _transport;
        private readonly Dictionary<string, Action<byte[]>> _handlers = new();

        public bool IsConnected => _transport != null && _transport.IsOpen;

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;

        private void Awake()
        {
            // 场景里可能残留上一轮 Play 遗留的 NetworkManager（DontDestroyOnLoad 对象在
            // "Enter Play Mode without Domain Reload" 设置下不会随 Play/Stop 自动清空静态引用），
            // 这里主动清掉所有其它同类型实例，只保留当前这个，避免拿到失效的旧连接。
            var existing = FindObjectsOfType<NetworkManager>();
            foreach (var other in existing)
            {
                if (other != this)
                {
                    Destroy(other.gameObject);
                }
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // WebGL 下浏览器标签页失焦会暂停 WebSocket 收发回调，
            // 保持后台运行可以让挂机战斗等场景在切后台时仍能收消息。
            Application.runInBackground = true;
        }

        /// <summary>建立到固定 gate WebSocket 地址的连接。</summary>
        public void Connect()
        {
            serverUrl = DefaultGateUrl;

            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                HandleError($"无效的 WebSocket 地址：{serverUrl}");
                return;
            }

            if (_transport != null)
            {
                if (_transport.IsOpen)
                {
                    Debug.Log($"[NetworkManager] already connected: {serverUrl}");
                    return;
                }

                ReleaseTransport(true);
            }

            Debug.Log($"[NetworkManager] connecting: {serverUrl}");
            _transport = WebSocketTransportFactory.Create(serverUrl);
            _transport.OnOpen += HandleOpen;
            _transport.OnClose += HandleClose;
            _transport.OnError += HandleError;
            _transport.OnMessage += HandleMessage;
            _transport.Connect();
        }

        public void Disconnect()
        {
            bool hadTransport = _transport != null;
            ReleaseTransport(true);
            // 主动断开也结束当前会话，避免角色服务继续保留可写的旧快照。
            if (hadTransport) OnDisconnected?.Invoke("连接已主动断开");
        }

        private void ReleaseTransport(bool close)
        {
            if (_transport == null)
            {
                return;
            }

            var transport = _transport;
            _transport = null;
            transport.OnOpen -= HandleOpen;
            transport.OnClose -= HandleClose;
            transport.OnError -= HandleError;
            transport.OnMessage -= HandleMessage;

            if (close)
            {
                transport.Close();
            }
        }

        /// <summary>
        /// 注册某个消息类型（msgType，对应 proto message 名）的处理回调。
        /// payload 为该消息的 protobuf 二进制内容，由上层用对应的 proto 类做 Unmarshal。
        /// </summary>
        public void RegisterHandler(string msgType, Action<byte[]> handler)
        {
            _handlers[msgType] = handler;
        }

        public void UnregisterHandler(string msgType)
        {
            _handlers.Remove(msgType);
        }

        /// <summary>
        /// 发送一条消息。msgType 对应后端 Frame.msg_type（proto message 名），
        /// payload 是已经 Marshal 好的 protobuf 二进制。
        /// </summary>
        public void Send(string msgType, byte[] payload)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[NetworkManager] Send failed, not connected. msgType={msgType}");
                return;
            }
            var frame = FrameCodec.Encode(msgType, payload);
            _transport.Send(frame);
        }

        private void HandleOpen()
        {
            Debug.Log("[NetworkManager] connected");
            OnConnected?.Invoke();
        }

        private void HandleClose(string reason)
        {
            Debug.Log($"[NetworkManager] disconnected: {reason}");
            OnDisconnected?.Invoke(reason);
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[NetworkManager] error: {error}");
            OnError?.Invoke(error);
        }

        private void HandleMessage(byte[] raw)
        {
            if (!FrameCodec.TryDecode(raw, out var msgType, out var payload))
            {
                Debug.LogWarning("[NetworkManager] failed to decode frame");
                return;
            }
            if (_handlers.TryGetValue(msgType, out var handler))
            {
                handler.Invoke(payload);
            }
            else
            {
                Debug.LogWarning($"[NetworkManager] no handler for msgType={msgType}");
            }
        }

        private void Update()
        {
            _transport?.DispatchMessageQueue();
        }

        private void OnDestroy()
        {
            ReleaseTransport(true);
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
