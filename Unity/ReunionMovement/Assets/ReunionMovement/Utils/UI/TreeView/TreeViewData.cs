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
        /// </summary>
        /// <param name="parent">新的父节点</param>
        public void SetParent(TreeViewData parent)
        {
            // 如果已经是同一个父节点，无需操作
            if (this.parent == parent) return;

            // 从旧父节点中移除自身
            this.parent?.RemoveChild(this);

            // 设置新父节点并更新层级
            this.parent = parent;
            this.layer = parent.layer + 1;

            // 确保自身已加入新父节点的子节点列表
            if (!parent.childNodes.Contains(this))
                parent.childNodes.Add(this);

            // 递归修正所有后代节点的 parent 和 layer
            ResetChildren(this);
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
            childNodes.Remove(child);
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
            return other.name.Equals(name) && other.layer.Equals(layer);
        }

        /// <summary>
        /// 获取当前节点的哈希码。
        /// 基于 parent、childNodes、layer、name 组合计算，
        /// 以支持在字典或哈希集合中使用。
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (parent != null ? parent.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (childNodes != null ? childNodes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ layer;
                hashCode = (hashCode * 397) ^ (name != null ? name.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}