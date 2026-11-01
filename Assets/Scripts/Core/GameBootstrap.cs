using UnityEngine;
using XiuXian.Network;

namespace XiuXian.Core
{
    /// <summary>
    /// 游戏启动入口，挂在启动场景的一个空物体上。
    /// 负责初始化各个全局单例管理器（网络、UI、数据等）。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManagerPrefab;

        private void Awake()
        {
            EnsureNetworkManager();
        }

        private void Start()
        {
            AudioListener.volume=PlayerPrefs.GetFloat("ImmortalV3.Volume",1f);
            ImmortalUIArtPreview.OpenLive();
        }

        private void EnsureNetworkManager()
        {
            if (NetworkManager.Instance != null)
            {
                return;
            }

            if (networkManagerPrefab != null)
            {
                Instantiate(networkManagerPrefab);
            }
            else
            {
                var go = new GameObject("NetworkManager");
                go.AddComponent<NetworkManager>();
            }
        }
    }
}
