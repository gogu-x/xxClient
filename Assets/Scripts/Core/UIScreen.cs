namespace XiuXian.Core
{
    /// <summary>
    /// 客户端流程中的顶层界面标识，UIManager 按此切换显示的面板。
    /// 选服不再是独立页面，而是 HomePanel 内的弹窗（见 ServerPickerPopup）。
    /// </summary>
    public enum UIScreen
    {
        Home,
        Main,
        XianFu,
        Character,
        Battle,
        Realm,
    }
}
