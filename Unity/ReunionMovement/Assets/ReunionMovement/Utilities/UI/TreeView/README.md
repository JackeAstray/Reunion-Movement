# TreeView 调用

```csharp
var group = new TreeViewData("group-1", "设备分组", null);
var device = new TreeViewData("device-1", "设备一", deviceInfo);
group.AddChild(device);
tree.NodeClicked += OnNodeClicked;
tree.SetData(new[] { group });

void OnNodeClicked(TreeViewData node)
{
    if (node.TryGetData<DeviceInfo>(out var data))
        OpenDevicePanel(data);
}
```

`DeviceInfo`、`deviceInfo` 和 `OpenDevicePanel` 为业务代码。订阅者销毁时应执行 `tree.NodeClicked -= OnNodeClicked`。

- `Id` 创建后不可修改。同一棵树内必须唯一；重复 ID、共享节点或环会在替换旧视图前被拒绝。
- 旧构造函数自动生成 ID；重新创建数据并保留状态时请使用显式 ID 构造函数。
- `Tag` 保存任意业务对象，`TryGetData<T>` 安全读取。
- `FindById(id)` 返回数据，即使节点尚未展开。
- `Select(id)` 只更新选择，不展开、不通知；`Select(id, true)` 显式通知。
- `SelectionChanged` 仅在启用通知且选择变化时触发；`NodeClicked` 在点击或显式通知时触发。
- `ExpandTo(id)` 展开祖先，使目标节点实例化，不自动滚动；`Expand(id)` 同时展开目标；`Collapse(id)` 折叠可见目标。
- `SetData` 按 ID 恢复仍存在的展开和选择状态，不发送点击事件。修改数据结构后调用 `RefreshAll()` 重建索引。
- 点击 `Container/Toggle/Icon` 的矩形区域只切换展开；点击文字、行背景或键盘提交只选择并激活业务事件。
- 选择状态可从 `SelectedNode` 读取；本组件不额外定义选中配色。
- `enableAction=false` 禁止用户业务点击，但允许箭头展开和代码静默选择。
- 有 `NodeClicked` 订阅者时使用统一事件，否则调用旧的 `data.action`，两条路径不同时执行。
- 现有模板需要保留 `Container/Toggle`、`Text`、`Placeholder` 和 `Icon` 结构；运行时通过 Toggle 上的内置 `EventTrigger` 转发点击和提交事件。

建议场景回归：分别点击箭头、文字和背景；重复点击同一节点；键盘提交；折叠再展开；以相同 ID 替换数据；检查业务回调每次只执行一次。