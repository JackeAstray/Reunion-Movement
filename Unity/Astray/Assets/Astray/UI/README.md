# UI —— 项目自带的界面逻辑

**放**：具体界面的业务逻辑 —— 本地多人选择、设置面板、开始菜单、轮播选择。

**不放**：
- 可复用的 UI 组件 → `Utils/UI/`（`ImageEx`、`LoopScrollRect` 等）
- 窗口管理框架 → `Core/System/UISystem/`

**现状**：这一层是"示例 + 业务"混合体（9 文件 / 约 3 千行），只有 `Select/CarouselSelectionController.cs` 具备通用性。按重构计划阶段 5，示例部分会迁到 `Samples/`。

**窗口怎么开**：`UISystem.Instance.OpenWindowAsync(UINames.XXX)`，窗口名常量在 `Core/System/UISystem/UINames.cs`。
