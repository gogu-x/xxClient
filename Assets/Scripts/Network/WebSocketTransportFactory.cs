namespace XiuXian.Network
{
    /// <summary>
    /// 根据当前平台创建对应的 WebSocket 传输层实现。
    /// NativeWebSocket 库本身已经处理了 WebGL / Native 平台差异，
    /// 这里统一使用同一个实现即可。
    /// </summary>
    public static class WebSocketTransportFactory
    {
        public static IWebSocketTransport Create(string url)
        {
            return new NativeWebSocketTransport(url);
        }
    }
}
