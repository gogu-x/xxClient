using System;

namespace XiuXian.Network
{
    /// <summary>
    /// WebSocket 传输层接口，屏蔽不同平台的底层实现差异
    /// （WebGL 下走 JS 互操作，桌面/移动端走标准 WebSocket 客户端库）。
    /// </summary>
    public interface IWebSocketTransport
    {
        bool IsOpen { get; }

        event Action OnOpen;
        event Action<string> OnClose;
        event Action<string> OnError;
        event Action<byte[]> OnMessage;

        void Connect();
        void Send(byte[] data);
        void Close();

        /// <summary>
        /// 部分平台（如 WebGL）的消息回调不在 Unity 主线程触发，
        /// 需要在 Update 中调用以将排队消息分发到主线程。
        /// 非 WebGL 实现可以空实现。
        /// </summary>
        void DispatchMessageQueue();
    }
}
