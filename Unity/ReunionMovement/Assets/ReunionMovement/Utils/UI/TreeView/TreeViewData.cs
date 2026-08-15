using System;
using System.Collections.Generic;

namespace ReunionMovement
{
    /// <summary>
    /// 树节点数据类，用于构建和管理树形结构数据。
    /// 每个节点可包含父节点引用、子节点列表、层级深度、名称、
    /// 可选的点击回调 Action，以及是否启用操作/显示装饰的开关。
    /// </summary>
    public class TreeViewData
    {
        /// <summary>父节点引用，根节点的 parent 为 null</summary>
        public TreeViewData parent;

        /// <summary>子节点列表</summary>
        public List<TreeViewData> childNodes = new List<TreeViewData>();

        /// <summary>当前节点在树中的层级深度（根节点为 0）</summary>
        public int layer = 0;

        /// <summary>节点显示名称</summary>
        public string name = string.Empty;

        /// <summary>节点点击时触发的回调委托，参数为当前节点自身</summary>
        public Action<TreeViewData> action = null;

        /// <summary>是否启用点击操作（供 UI 层判断是否可交互）</summary>
        public bool enableAction = false;

        /// <summary>是否显示装饰元素（如展开/折叠箭头图标）</summary>
        public bool displayDecorate = false;

        /// <summary>
        /// 无参构造函数，创建一个空的树节点。
        /// </summary>
        public TreeViewData() { }

        /// <summary>
        /// 使用名称和层级创建树节点。
        /// </summary>
        /// <param name="name">节点名称</param>
        /// <param name="layer">层级深度，默认为 0</param>
        public TreeViewData(string name, int layer = 0)
        {
            this.name = name;
            this.layer = layer;
            parent = null;
            childNodes = new List<TreeViewData>();
        }

        /// <summary>
        /// 完整参数构造函数，创建带子节点、回调等全部属性的树节点。
        /// 构造完成后会自动调用 <see cref="ResetChildren"/> 修正所有子节点的 parent 与 layer。
        /// </summary>
        /// <param name="name">节点名称</param>
        /// <param name="childNodes">子节点列表，为 null 时自动初始化为空列表</param>
        /// <param name="action">节点点击回调</param>
        /// <param name="layer">层级深度，默认为 0</param>
        /// <param name="enableAction">是否启用点击操作，默认 false</param>
        /// <param name="displayDecorate">是否显示装饰，默认 false</param>
        public TreeViewData(string name, List<TreeViewData> childNodes, Action<TreeViewData> action, int layer = 0, bool enableAction = false, bool displayDecorate = false)
        {
            this.name = name;
            parent = null;
            this.childNodes = childNodes ?? new List<TreeViewData>();
            this.action = action;
            this.layer = layer;
            this.enableAction = enableAction;
            this.displayDecorate = displayDecorate;
            ResetChildren(this);
        }

        /// <summary>
        /// 设置当前节点的父节点。
        /// 会自动从旧父节点中移除自身，加入新父节点的子节点列表，
        /// 并递归更新自身及所有后代节点的层级。
        /// parent 传 null 表示将该节点提升为根节点（层级归 0）。
        /// </summary>
        /// <param name="parent">新的父节点</param>
        public void SetParent(TreeViewData parent)
        {
            // 挂到自身：无操作（不触发同父短路时 childNodes.Add(this) 会形成自环）
            if (parent == this) return;

            // 循环检测：新父节点不能是自身的后代，否则 childNodes 成环 → ResetChildren 无限递归栈溢出（IL2CPP 直接崩溃）
            if (IsDescendantOf(parent)) return;

            // 如果已经是同一个父节点，无需操作
            if (this.parent == parent) return;

            // 从旧父节点中移除自身
            this.parent?.RemoveChild(this);

            // 设置新父节点并更新层级（null 表示成为根节点，避免 NRE）
            this.parent = parent;
            this.layer = parent != null ? parent.layer + 1 : 0;

            // 确保自身已加入新父节点的子节点列表。
            // 引用比较：Equals 按 name+layer 值相等，同名同层的兄弟节点会被 Contains 误判为已存在，
            // 导致新子节点静默丢失
            if (parent != null && !ContainsReference(parent.childNodes, this))
                parent.childNodes.Add(this);

            // 递归修正所有后代节点的 parent 和 layer
            ResetChildren(this);
        }

        /// <summary>按引用（ReferenceEquals）判断列表是否包含指定节点，供结构操作使用（值相等语义会误判同名兄弟节点）</summary>
        private static bool ContainsReference(List<TreeViewData> list, TreeViewData node)
        {
            if (list == null || node == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], node)) return true;
            }
            return false;
        }

        /// <summary>
        /// 判断指定节点是否是自身的后代（沿其 parent 链向上能否到达自身）。
        /// 供 SetParent 循环检测使用。
        /// </summary>
        private bool IsDescendantOf(TreeViewData ancestor)
        {
            if (ancestor == null) return false;
            var p = ancestor.parent;
            while (p != null)
            {
                if (ReferenceEquals(p, this)) return true;
                p = p.parent;
            }
            return false;
        }

        /// <summary>
        /// 添加单个子节点。内部调用 <see cref="SetParent"/> 完成父子关联。
        /// </summary>
        /// <param name="child">要添加的子节点</param>
        public void AddChild(TreeViewData child)
        {
            if (child == null) return;
            child.SetParent(this);
        }

        /// <summary>
        /// 批量添加子节点。
        /// </summary>
        /// <param name="children">子节点集合</param>
        public void AddChild(IEnumerable<TreeViewData> children)
        {
            if (children == null) return;
            foreach (TreeViewData child in children)
            {
                AddChild(child);
            }
        }

        /// <summary>
        /// 移除单个子节点。从子节点列表中删除，并解除其 parent 引用。
        /// </summary>
        /// <param name="child">要移除的子节点</param>
        public void RemoveChild(TreeViewData child)
        {
            if (child == null) return;
            // 引用比较移除：Equals 按 name+layer 值相等，Remove(child) 可能删错同名同层的兄弟节点
            for (int i = childNodes.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(childNodes[i], child))
                {
                    childNodes.RemoveAt(i);
                    break;
                }
            }
            if (child.parent == this)
                child.parent = null;
        }

        /// <summary>
        /// 批量移除子节点。
        /// </summary>
        /// <param name="children">要移除的子节点集合</param>
        public void RemoveChild(IEnumerable<TreeViewData> children)
        {
            if (children == null) return;
            foreach (TreeViewData child in children)
            {
                RemoveChild(child);
            }
        }

        /// <summary>
        /// 清空所有子节点，并解除所有子节点的 parent 引用。
        /// </summary>
        public void ClearChildren()
        {
            foreach (var child in childNodes)
            {
                if (child.parent == this)
                    child.parent = null;
            }
            childNodes.Clear();
        }

        /// <summary>
        /// 递归重置指定节点及其所有后代节点的 parent 引用和层级深度。
        /// 通常在节点被移动或重新挂载到树中时调用，以保证数据一致性。
        /// </summary>
        /// <param name="treeData">需要重置的起始节点</param>
        private void ResetChildren(TreeViewData treeData)
        {
            if (treeData.childNodes == null) return;
            foreach (var node in treeData.childNodes)
            {
                node.parent = treeData;
                node.layer = treeData.layer + 1;
                ResetChildren(node);
            }
        }

        /// <summary>
        /// 按名称递归查找子节点（深度优先搜索）。
        /// 若当前节点的直接子节点中存在匹配，则立即返回；
        /// 否则递归进入每个子节点继续查找。
        /// </summary>
        /// <param name="name">要查找的节点名称</param>
        /// <returns>找到的第一个匹配节点，未找到则返回 null</returns>
        public TreeViewData FindChildByName(string name)
        {
            if (string.IsNullOrEmpty(name) || childNodes == null) return null;
            foreach (var child in childNodes)
            {
                if (child.name == name) return child;
                var found = child.FindChildByName(name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 判断两个树节点是否相等。仅比较 name 和 layer，
        /// 不比较 parent、childNodes 等引用字段。
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>若名称和层级均相同则返回 true</returns>
        public override bool Equals(object obj)
        {
            TreeViewData other = obj as TreeViewData;
            if (other == null) return false;
            // name 是公有可变字段可能为 null，用 string.Equals 避免 NRE（与 GetHashCode 的 null 防护一致）
            return string.Equals(other.name, name) && other.layer.Equals(layer);
        }

        /// <summary>
        /// 获取当前节点的哈希码。
        /// 仅基于 name 与 layer 计算（与 <see cref="Equals"/> 保持一致），
        /// 否则两个 Equals 相等的对象哈希不同，放入 Dictionary/HashSet 后查找会失效。
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = name != null ? name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ layer;
                return hashCode;
            }
        }
    }
}