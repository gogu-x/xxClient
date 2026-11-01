using System;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>
    /// 基于 NativeWebSocket 2.x（https://github.com/endel/NativeWebSocket, upm-2 分支）的传输层实现。
    /// 该库同时支持 WebGL（走 JS 互操作）和其他平台（走 System.Net.WebSockets），用同一套 API。
    ///
    /// 2.x 版本通过 SynchronizationContext 自动把事件分发到 Unity 主线程，
    /// 不再需要每帧手动调用 DispatchMessageQueue()（那是 1.x 的用法）。
    /// </summary>
    public class NativeWebSocketTransport : IWebSocketTransport
    {
        private readonly string _url;
        private WebSocket _socket;
        private bool _errorReported;

        public bool IsOpen => _socket != null && _socket.State == WebSocketState.Open;

        public event Action OnOpen;
        public event Action<string> OnClose;
        public event Action<string> OnError;
        public event Action<byte[]> OnMessage;

        public NativeWebSocketTransport(string url)
        {
            _url = url;
        }

        public void Connect()
        {
            _errorReported = false;

            // 不发送 Sec-WebSocket-Protocol。gate 在未协商子协议时默认使用
            // ProtoCodec；只有明确协商为 "json" 时才切换到 JsonCodec。
            _socket = new WebSocket(_url);

            _socket.OnOpen += () => OnOpen?.Invoke();
            _socket.OnError += ReportError;
            _socket.OnClose += code => OnClose?.Invoke(code.ToString());
            _socket.OnMessage += bytes => OnMessage?.Invoke(bytes);

            // Connect() 是 async Task，这里不 await，状态变化/异常通过上面的事件通知，
            // 调用方（NetworkManager）不需要处理 async/await。
            _ = ConnectInternalAsync();
        }

        private async Task ConnectInternalAsync()
        {
            try
            {
                await _socket.Connect();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ReportError(e.Message);
            }
        }

        private void ReportError(string error)
        {
            if (_errorReported)
            {
                return;
            }

            _errorReported = true;
            OnError?.Invoke($"{error}（地址：{_url}）");
        }

        public void Send(byte[] data)
        {
            if (!IsOpen)
            {
                Debug.LogWarning("[NativeWebSocketTransport] Send skipped, socket not open.");
                return;
            }
            _ = _socket.Send(data);
        }

        public void Close()
        {
            if (_socket == null)
            {
                return;
            }
            _ = _socket.Close();
        }

        /// <summary>
        /// NativeWebSocket 2.x 通过 SynchronizationContext 自动分发事件到主线程，
        /// 此方法保留为空实现，仅为兼容 IWebSocketTransport 接口（其他平台实现可能仍需要它）。
        /// </summary>
        public void DispatchMessageQueue()
        {
        }
    }
}
