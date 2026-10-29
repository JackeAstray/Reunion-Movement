# Editor —— 编辑器工具（不进包体）

**放**：Addressables 流水线、Excel 导表、打包导出（`Astray.unitypackage`）、工具箱、UI 代码生成、碰撞器可视化。

**不放**：运行时代码。本目录只参与编辑器编译，放进来的东西打包时不会被包含（同理，运行时也不该引用这里）。

**入口**：`Toolbox/AstrayToolboxWindow.cs` —— Unity 菜单 `Astray → 工具箱`。

**常见任务**：
- 导表：菜单 `Astray → Excel`
- 出包：菜单 `Astray → 打包`
- 资源迁移 / 构建 / 上传：菜单 `Astray → Addressables`
