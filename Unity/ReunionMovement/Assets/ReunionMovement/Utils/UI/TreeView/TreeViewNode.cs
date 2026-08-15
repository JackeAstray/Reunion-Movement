using ReunionMovement.Common.Util;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReunionMovement
{
    /// <summary>
    /// 树形视图节点组件，挂载在每个树节点 GameObject 上。
    /// 负责节点的渲染、展开/折叠交互、颜色设置、缩进占位符生成等 UI 行为。
    /// 继承自 UIBehaviour，可响应 Unity UI 生命周期事件。
    /// </summary>
    public class TreeViewNode : UIBehaviour
    {
        /// <summary>当前节点的层级深度，用于控制缩进和颜色</summary>
        public int layer = 0;

        /// <summary>叶子节点（无子节点）使用的透明占位精灵，替换箭头图标</summary>
        public Sprite transparent;

        /// <summary>可展开节点的箭头精灵图标</summary>
        public Sprite arrow;

        /// <summary>是否启用多重占位符模式：为每一层生成独立的缩进 Placeholder，而不是只显示一个</summary>
        public bool multiPlaceholder = false;

        /// <summary>当前节点对应的数据模型</summary>
        private TreeViewData treeData;

        /// <summary>所属的 TreeView 控制器引用</summary>
        private TreeView uiTree;

        /// <summary>用于展开/折叠交互的 Toggle 组件</summary>
        private Toggle toggle;

        /// <summary>节点背景 Image 组件，用于设置层级颜色</summary>
        private Image bg;

        /// <summary>缩进占位符 Transform，控制节点内容的水平偏移</summary>
        private Transform placeholder;

        /// <summary>Placeholder 的父物体 Transform，用于管理多重占位符的克隆体</summary>
        private Transform placeholderParent;

        /// <summary>装饰元素 Transform（如额外的图标或标记）</summary>
        private Transform decorate;

        /// <summary>节点文本显示组件（TextMeshPro）</summary>
        private TextMeshProUGUI text;

        /// <summary>Toggle 中箭头图标的 Transform</summary>
        private Transform toggleTransform;

        /// <summary>当前节点自身 Transform 的缓存引用</summary>
        private Transform myTransform;

        /// <summary>Container 子物体的 Transform，容纳 Toggle、Text、Placeholder 等</summary>
        private Transform container;
        /// <summary>Container 的 RectTransform 缓存（Insert 缩进计算使用，避免每次 GetComponent）</summary>
        private RectTransform containerRect;

        /// <summary>当前已展开的子节点 GameObject 列表，用于回收管理</summary>
        private List<GameObject> children = new List<GameObject>();

        /// <summary>节点点击时触发的回调委托</summary>
        private Action<TreeViewData> action;

        /// <summary>
        /// 懒加载方式获取并缓存当前节点所需的全部 UI 组件引用。
        /// 仅在首次调用时执行查找，后续调用直接返回 true（通过 myTransform != null 判断）。
        /// 查找路径基于固定的节点预制体层级结构；
        /// 结构不完整时输出明确错误并禁用节点，返回 false 由调用方短路，避免后续链式调用 NRE。
        /// </summary>
        private bool TryCacheComponents()
        {
            // 已缓存过则跳过，避免重复查找
            if (myTransform != null) return true;

            myTransform = this.transform;
            bg = myTransform.GetComponent<Image>();
            container = myTransform.Find("Container");
            containerRect = container != null ? container.GetComponent<RectTransform>() : null;

            // Container 缺失：后续所有 Find 都会 NRE，直接降级
            if (container == null)
            {
                Debug.LogError($"TreeViewNode: 节点 '{myTransform.name}' 缺少 'Container' 子物体，节点已禁用。请检查预制体结构。", this);
                enabled = false;
                return false;
            }

            // Container 下的 UI 元素
            toggle = container.Find("Toggle")?.GetComponent<Toggle>();
            text = container.Find("Toggle/Text")?.GetComponent<TextMeshProUGUI>();
            decorate = container.Find("Decorate");
            placeholder = container.Find("Toggle/Placeholder");
            placeholderParent = placeholder != null ? placeholder.parent : null;

            // 关键组件缺失：禁用节点，避免 Insert/SetColor 等后续调用 NRE
            if (toggle == null || text == null || placeholder == null)
            {
                Debug.LogError($"TreeViewNode: 节点 '{myTransform.name}' 的 Container 结构不完整" +
                    $"（Toggle={toggle != null}，Text={text != null}，Placeholder={placeholder != null}），节点已禁用。请检查预制体结构。", this);
                enabled = false;
                return false;
            }

            // Toggle 中的箭头图标
            toggleTransform = toggle.transform.Find("Icon");

            // 向上逐级查找 TreeView 控制器（替代硬编码三级查找，层级结构变化时仍可靠）
            uiTree = null;
            var search = myTransform.parent;
            while (search != null)
            {
                uiTree = search.GetComponent<TreeView>();
                if (uiTree != null) break;
                search = search.parent;
            }
            return true;
        }

        /// <summary>
        /// 将节点 UI 重置为默认折叠状态：
        /// 水平偏移归零、箭头旋转 90°（指向右侧）、图标设为箭头精灵。
        /// </summary>
        private void ResetComponent()
        {
            // 重置 Container 的水平偏移
            if (container != null)
            {
                container.localPosition = new Vector3(0, container.localPosition.y, 0);
            }

            // 箭头默认指向右侧（折叠状态）；Icon 节点缺失时跳过（GetComponent 校验漏了 Icon 会 NRE）
            if (toggleTransform != null)
            {
                toggleTransform.localEulerAngles = new Vector3(0, 0, 90);
                var arrowImage = toggleTransform.GetComponent<Image>();
                if (arrowImage != null) arrowImage.sprite = arrow;
            }
        }

        /// <summary>
        /// 将 TreeViewData 数据填充到当前节点 UI 中。
        /// 会先移除旧的事件监听、重置组件状态，再根据数据设置文本、
        /// 缩进偏移、箭头图标、背景颜色、装饰显示等。
        /// </summary>
        /// <param name="data">要填充的树节点数据</param>
        public void Insert(TreeViewData data)
        {
            // 数据为空或组件缓存失败（预制体结构不完整/节点被禁用）时短路，避免 NRE
            if (data == null || !TryCacheComponents()) return;

            // 先移除旧监听，防止重复注册导致多次回调
            RemoveListener();
            ResetComponent();

            treeData = data;
            text.text = data.name;
            toggle.isOn = false;

            // 注册 Toggle 值变化回调
            toggle.onValueChanged.AddListener(OpenOrClose);

            // 根据层级计算水平缩进偏移（RectTransform 已缓存于 TryCacheComponents，避免每次 Insert GetComponent）
            float indentStep = containerRect != null ? containerRect.sizeDelta.y : 0f;
            container.localPosition += new Vector3(
                indentStep * treeData.layer,
                0, 0);

            // 叶子节点（无子节点）显示透明占位图代替箭头
            if (data.childNodes.Count.Equals(0) && toggleTransform != null)
            {
                var arrowImage = toggleTransform.GetComponent<Image>();
                if (arrowImage != null) arrowImage.sprite = transparent;
            }

            // 缓存点击回调
            action = data.action;

            // 应用层级颜色和装饰状态
            SetColor(data.layer);
            SetDisplayDecorate(data.displayDecorate);

            // 回收旧的已展开子节点：走与 CloseChildren 相同的路径（移除监听 + 递归回收 + 推回对象池），
            // 裸 Clear() 会使子节点 GameObject 变孤儿且 Toggle 监听残留，多次 Refresh 持续泄漏
            CloseChildren();
        }

        /// <summary>
        /// 根据层级深度设置节点的背景颜色和缩进占位符。
        /// 支持两种模式：
        /// - 普通模式（multiPlaceholder = false）：仅显示一个 Placeholder
        /// - 多重占位符模式（multiPlaceholder = true）：为每一层克隆一个 Placeholder，
        ///   形成逐级缩进的视觉效果。
        /// 背景颜色从 TreeView.colors 列表中按层级索引取值。
        /// </summary>
        /// <param name="layer">当前节点的层级深度</param>
        public void SetColor(int layer)
        {
            this.layer = layer;

            if (multiPlaceholder)
            {
                // === 多重占位符模式 ===
                // 先清理上一轮克隆的多余 Placeholder（保留原始模板和 Icon、Text）
                for (int i = placeholderParent.childCount - 1; i >= 0; i--)
                {
                    var child = placeholderParent.GetChild(i);
                    if (child != placeholder && child.name == "Placeholder")
                    {
                        // 编辑模式下 Destroy 不生效，需 DestroyImmediate 立即清理，避免占位符克隆累积
                        if (Application.isPlaying) GameObject.Destroy(child.gameObject);
                        else GameObject.DestroyImmediate(child.gameObject);
                    }
                }

                if (layer > 0)
                {
                    // 启用原始 Placeholder 模板
                    placeholder.gameObject.SetActive(true);
                    placeholder.SetSiblingIndex(0);

                    // 为第 2 层及以后克隆额外的 Placeholder
                    for (int i = 1; i < layer; i++)
                    {
                        var clone = GameObject.Instantiate(placeholder.gameObject, placeholderParent);
                        clone.name = "Placeholder";
                        clone.SetActive(true);
                        clone.transform.SetSiblingIndex(i - 1);
                    }
                }
                else
                {
                    // layer = 0 时隐藏所有 Placeholder
                    placeholder.gameObject.SetActive(false);
                }
            }
            else
            {
                // === 普通模式：仅使用一个 Placeholder ===
                if (layer > 0)
                {
                    placeholder.SetActive(true);
                    placeholder.SetSiblingIndex(0);
                }
                else
                {
                    placeholder.SetActive(false);
                }

                // 清理之前可能残留的克隆 Placeholder
                for (int i = placeholderParent.childCount - 1; i >= 0; i--)
                {
                    var child = placeholderParent.GetChild(i);
                    if (child != placeholder && child.name == "Placeholder")
                    {
                        // 编辑模式下 Destroy 不生效，需 DestroyImmediate 立即清理
                        if (Application.isPlaying) GameObject.Destroy(child.gameObject);
                        else GameObject.DestroyImmediate(child.gameObject);
                    }
                }
            }

            // 从 TreeView 配色方案中取对应层级的背景色
            if (uiTree != null && layer < uiTree.colors.Count)
            {
                bg.color = uiTree.colors[layer];
            }
        }

        /// <summary>
        /// 设置当前节点的装饰元素（Decorate）的显示/隐藏状态。
        /// </summary>
        /// <param name="displayDecorate">true 显示装饰，false 隐藏</param>
        public void SetDisplayDecorate(bool displayDecorate)
        {
            if (decorate != null)
            {
                decorate.gameObject.SetActive(displayDecorate);
            }
        }

        /// <summary>
        /// 递归设置当前节点及其所有已展开子节点的装饰元素显示状态。
        /// 遍历 treeData.childNodes，通过 FindChildNode 找到对应的 UI 节点并递归调用。
        /// </summary>
        /// <param name="displayDecorate">true 显示装饰，false 隐藏</param>
        public void SetDisplayDecorateRecursive(bool displayDecorate)
        {
            SetDisplayDecorate(displayDecorate);
            if (treeData.childNodes != null)
            {
                foreach (var child in treeData.childNodes)
                {
                    var node = FindChildNode(child.name);
                    node?.SetDisplayDecorateRecursive(displayDecorate);
                }
            }
        }

        /// <summary>
        /// 刷新当前节点：使用当前持有的 treeData 重新执行 Insert，
        /// 用于在数据未变但 UI 需要重建时（如回收后重新显示）。
        /// </summary>
        public void Refresh()
        {
            Insert(treeData);
        }

        /// <summary>
        /// 获取当前节点绑定的 TreeViewData 数据模型。
        /// </summary>
        /// <returns>树节点数据，可能为 null</returns>
        public TreeViewData GetTreeData() => treeData;

        /// <summary>
        /// Toggle 值变化回调：处理节点的展开与折叠。
        /// 
        /// 分支逻辑：
        /// - 叶子节点（无子节点）：每次点击都触发 action 回调，并将 Toggle 重置为关闭状态，
        ///   使其作为普通按钮使用。
        /// - 非叶子节点：根据 isOn 展开或折叠子节点，同时旋转箭头图标（0° 向下 / 90° 向右），
        ///   并触发 action 回调。
        /// </summary>
        /// <param name="isOn">Toggle 当前是否为选中（展开）状态</param>
        private void OpenOrClose(bool isOn)
        {
            // 叶子节点：作为点击项，每次点击都触发回调
            if (treeData == null || treeData.childNodes == null || treeData.childNodes.Count == 0)
            {
                action?.Invoke(treeData);
                // 重置 Toggle 为关闭状态，使其可被重复点击
                toggle.SetIsOnWithoutNotify(false);
                return;
            }

            // 非叶子节点：展开或折叠
            if (isOn)
                OpenChildren();
            else
                CloseChildren();

            // 旋转箭头：展开时指向下方 (0°)，折叠时指向右侧 (90°)
            // 预制体缺 "Icon" 子物体时 toggleTransform 为 null（TryCacheComponents 不校验），
            // 判空与 ResetComponent 分支保持一致，避免点击展开/折叠即 NRE
            if (toggleTransform != null)
            {
                toggleTransform.localEulerAngles = isOn
                    ? new Vector3(0, 0, 0)
                    : new Vector3(0, 0, 90);
            }

            // 触发节点点击回调
            action?.Invoke(treeData);
        }

        /// <summary>
        /// 展开当前节点的子节点。
        /// 调用 TreeView.Pop 从对象池中取出子节点 GameObject 并插入到当前节点下方。
        /// </summary>
        private void OpenChildren()
        {
            // uiTree 可能为 null（节点脱离 TreeView 使用/组件缓存失败路径），判空避免 NRE
            if (uiTree != null)
            {
                children = uiTree.Pop(treeData.childNodes, transform.GetSiblingIndex());
            }
        }

        /// <summary>
        /// 折叠当前节点的子节点并回收到对象池。
        /// 会递归关闭所有后代节点（移除监听、回收 GameObject），
        /// 最后将子节点列表推回 TreeView 的对象池。
        /// </summary>
        protected void CloseChildren()
        {
            // 递归关闭每个子节点
            foreach (var childObj in children)
            {
                if (childObj == null) continue;
                TreeViewNode node = childObj.GetComponent<TreeViewNode>();
                if (node != null)
                {
                    node.RemoveListener();   // 移除 Toggle 事件监听
                    node.CloseChildren();    // 递归关闭更深层级
                }
            }

            // 将所有子节点 GameObject 回收到对象池（uiTree 为 null 时直接丢弃引用，防 NRE）
            if (uiTree != null)
            {
                uiTree.Push(children);
            }
            children.Clear();
        }

        /// <summary>
        /// 移除 Toggle 上的 onValueChanged 监听器，
        /// 防止节点回收/销毁后因事件残留导致的内存泄漏或无效调用。
        /// </summary>
        private void RemoveListener()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(OpenOrClose);
        }

        /// <summary>
        /// 在已展开的子节点中按名称递归查找 TreeViewNode。
        /// 先在当前层级的 children 列表中查找，未找到则递归进入每个子节点继续搜索（深度优先）。
        /// 
        /// 注意：此方法仅在节点已展开时有效，因为未展开的节点其 UI GameObject 尚未实例化。
        /// </summary>
        /// <param name="name">要查找的节点名称</param>
        /// <returns>找到的 TreeViewNode 组件，未找到则返回 null</returns>
        public TreeViewNode FindChildNode(string name)
        {
            if (treeData.childNodes == null) return null;

            foreach (var child in treeData.childNodes)
            {
                // 名称匹配：在当前已实例化的 children 中查找对应的 GameObject
                if (child.name == name)
                {
                    foreach (var go in children)
                    {
                        if (go == null) continue;
                        var node = go.GetComponent<TreeViewNode>();
                        if (node != null && node.treeData == child)
                            return node;
                    }
                    // 已匹配当前层级名称，跳过递归（避免同名但不同层级时误入更深层级）
                    continue;
                }

                // 当前层级未匹配，递归进入每个已展开的子节点继续查找
                foreach (var go in children)
                {
                    if (go == null) continue;
                    var node = go.GetComponent<TreeViewNode>();
                    var found = node?.FindChildNode(name);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}