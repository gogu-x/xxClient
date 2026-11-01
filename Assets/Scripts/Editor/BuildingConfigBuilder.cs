using UnityEditor;
using UnityEngine;
using XiuXian.Core;

namespace XiuXian.EditorTools
{
    /// <summary>
    /// 生成大厅建筑配置资产的初始数据：BuildingRegistry.asset 汇总表。
    ///
    /// 使用：Tools > XiuXian > 生成建筑配置资产（首次）
    /// 重复执行不会覆盖已存在的资产，避免误删策划已经调整过的数据。
    /// </summary>
    public static class BuildingConfigBuilder
    {
        private const string ConfigDir = "Assets/Configs";
        private const string RegistryPath = ConfigDir + "/BuildingRegistry.asset";

        [MenuItem("Tools/XiuXian/生成建筑配置资产（首次）")]
        public static void BuildInitialConfigs()
        {
            System.IO.Directory.CreateDirectory(ConfigDir);

            var registry = AssetDatabase.LoadAssetAtPath<BuildingRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<BuildingRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
                Debug.Log($"[BuildingConfigBuilder] 已创建 {RegistryPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BuildingConfigBuilder] 建筑配置资产已就绪。后续新增建筑：" +
                      "在 Project 窗口右键 Create > XiuXian > 大厅建筑配置 (BuildingDef) 新建资产，" +
                      "填好字段后拖进 BuildingRegistry.asset 的 buildings 列表即可。");
        }
    }
}
