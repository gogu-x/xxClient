using System.Collections.Generic;
using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>
    /// 大厅建筑总控：读取 BuildingRegistry 配置，为每个 BuildingDef 实例化一个
    /// BuildingHotspot（Prefab），统一处理点击后的解锁判断与界面跳转。
    ///
    /// 新增建筑时只需要新建 BuildingDef 资产并加入 BuildingRegistry，
    /// 不需要改这个类或场景结构。
    /// </summary>
    public class LobbyBuildingController : MonoBehaviour
    {
        [SerializeField] private BuildingRegistry registry;
        [SerializeField] private BuildingHotspot hotspotPrefab;
        [SerializeField] private RectTransform container;

        /// <summary>当前玩家等级，用于解锁判断。尚无角色系统前先留空占位，默认视为已解锁。</summary>
        [SerializeField] private int playerLevel = 999;

        private readonly List<BuildingHotspot> _spawned = new();

        private void OnEnable()
        {
            if (_spawned.Count == 0)
            {
                SpawnAll();
            }
            RefreshUnlockStates(playerLevel);
        }

        private void SpawnAll()
        {
            if (registry == null || hotspotPrefab == null || container == null)
            {
                Debug.LogWarning("[LobbyBuildingController] registry/hotspotPrefab/container 未配置，跳过建筑生成。");
                return;
            }

            foreach (var def in registry.buildings)
            {
                if (def == null)
                {
                    continue;
                }

                var hotspot = Instantiate(hotspotPrefab, container);
                hotspot.name = $"Building_{def.id}";
                hotspot.Bind(def);
                hotspot.OnBuildingClicked += HandleBuildingClicked;
                _spawned.Add(hotspot);
            }
        }

        /// <summary>根据玩家等级刷新每座建筑的解锁显示状态，供登录/升级后调用。</summary>
        public void RefreshUnlockStates(int level)
        {
            playerLevel = level;
            foreach (var hotspot in _spawned)
            {
                if (hotspot.Def == null)
                {
                    continue;
                }
                hotspot.SetLocked(level < hotspot.Def.unlockLevel);
            }
        }

        private void HandleBuildingClicked(BuildingDef def)
        {
            if (def == null)
            {
                return;
            }

            if (playerLevel < def.unlockLevel)
            {
                Debug.Log($"[LobbyBuildingController] {def.displayName} 未解锁：{def.lockedTip}");
                // TODO: 接入 Toast/气泡提示系统后，把 Debug.Log 换成实际的 UI 提示。
                return;
            }

            UIManager.Instance.Show(def.targetScreen);
        }

        private void OnDisable()
        {
            foreach (var hotspot in _spawned)
            {
                if (hotspot != null)
                {
                    hotspot.OnBuildingClicked -= HandleBuildingClicked;
                }
            }
        }
    }
}
