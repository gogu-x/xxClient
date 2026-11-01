using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>
    /// 打包时指定的 gate WebSocket 地址。
    ///
    /// 资源固定放在 Assets/Resources/GateConfig.asset，运行时通过 Resources.Load 读取，
    /// 因此会随包一起打入。通过菜单 Tools/XiuXian/网关地址配置 创建并编辑。
    /// gateUrl 为空时回退到 NetworkManager 的默认规则（WebGL 用页面主机:8001，其它平台用本机）。
    /// </summary>
    public class GateConfig : ScriptableObject
    {
        public const string ResourcePath = "GateConfig";

        [Tooltip("gate WebSocket 地址，例如 wss://game.example.com/ws 或 ws://192.168.1.10:8001/ws。留空则自动推断。")]
        public string gateUrl = string.Empty;

        /// <summary>读取包内配置的网关地址；未配置时返回 null。</summary>
        public static string LoadConfiguredUrl()
        {
            var config = Resources.Load<GateConfig>(ResourcePath);
            if (config == null || string.IsNullOrWhiteSpace(config.gateUrl))
            {
                return null;
            }
            return config.gateUrl.Trim();
        }
    }
}
