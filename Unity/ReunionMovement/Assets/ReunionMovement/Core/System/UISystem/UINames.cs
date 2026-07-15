namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// UI 窗口名称常量 —— 替代硬编码字符串，避免 GC 分配和拼写错误。
    /// 使用方式：UISystem.Instance.OpenWindow(UINames.Popup);
    /// </summary>
    public static class UINames
    {
        /// <summary>通用弹窗</summary>
        public const string Popup = "PopupUIPlane";

        /// <summary>启动画面</summary>
        public const string StartGame = "StartGameUIPlane";

        /// <summary>终端控制台</summary>
        public const string Terminal = "TerminalUIPlane";
    }
}
