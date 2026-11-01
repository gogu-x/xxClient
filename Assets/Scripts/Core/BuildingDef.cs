using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>
    /// 大厅建筑（仙府、藏经阁、坊市……）的静态配置。
    /// 每座建筑对应一个 BuildingDef 资产，由 LobbyBuildingController 在运行时读取批量生成，
    /// 新增/调整建筑只需要新建或编辑资产，不需要改代码。
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingDef", menuName = "XiuXian/大厅建筑配置 (BuildingDef)")]
    public class BuildingDef : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("唯一标识，代码/存档里用它定位建筑，不要和其他 BuildingDef 重复")]
        public string id;

        [Tooltip("显示名称，如“仙府”，作为常驻标签显示在建筑上方")]
        public string displayName;

        [Header("外观")]
        [Tooltip("建筑贴图（独立抠图，带透明背景）")]
        public Sprite icon;

        [Tooltip("可选：高亮/发光态贴图，鼠标悬浮或按下时叠加显示，不填则只做缩放反馈")]
        public Sprite glowIcon;

        [Header("布局（相对 Canvas 中心的锚点坐标，参照分辨率 1080x1920）")]
        public Vector2 anchoredPosition;
        public Vector2 size = new Vector2(220, 220);

        [Tooltip("同层级建筑之间的遮挡顺序，值越大越靠前显示")]
        public int sortOrder;

        [Header("交互")]
        [Tooltip("点击后跳转的目标界面")]
        public UIScreen targetScreen;

        [Tooltip("解锁所需等级，0 表示始终解锁")]
        public int unlockLevel;

        [Tooltip("未解锁时点击的提示文案")]
        public string lockedTip = "尚未开放";
    }
}
