using System.Collections.Generic;
using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>
    /// 大厅所有建筑配置的汇总表。LobbyBuildingController 只认这一份资产，
    /// 新增建筑时把对应的 BuildingDef 拖进 buildings 列表即可，无需改代码或场景。
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingRegistry", menuName = "XiuXian/大厅建筑总表 (BuildingRegistry)")]
    public class BuildingRegistry : ScriptableObject
    {
        public List<BuildingDef> buildings = new();
    }
}
