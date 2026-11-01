namespace XiuXian.Core
{
    /// <summary>玩家可登录或注册的服务器信息。</summary>
    public class ServerInfo
    {
        public int ServerId;
        public ulong Uid;
        public string Name;
        public bool IsOnline;

        public ServerInfo(int serverId, string name, bool isOnline = true, ulong uid = 0)
        {
            ServerId = serverId;
            Uid = uid;
            Name = name;
            IsOnline = isOnline;
        }
    }
}
