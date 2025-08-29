using System;
using System.Collections.Generic;

namespace ReunionMovement
{
    /// <summary>
    /// 树节点数据
    /// </summary>
    public class TreeViewData
    {
        public TreeViewData parent;
        public List<TreeViewData> childNodes = new List<TreeViewData>();
        public int layer = 0;
        public string name = string.Empty;
        public Action action = null;
        public bool enableAction = false;
        public bool displayDecorate = false;

        public TreeViewData() { }
        public TreeViewData(string name, int layer = 0)
        {
            this.name = name;
            this.layer = layer;
            parent = null;
            childNodes = new List<TreeViewData>();
        }
        public TreeViewData(string name, List<TreeViewData> childNodes, Action action, int layer = 0, bool enableAction = false, bool displayDecorate = false)
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

        // 设置父节点
        public void SetParent(TreeViewData parent)
        {
            if (this.parent == parent) return;
            this.parent?.RemoveChild(this);
            this.parent = parent;
            this.layer = parent.layer + 1;
            if (!parent.childNodes.Contains(this))
                parent.childNodes.Add(this);
            ResetChildren(this);
        }
        // 添加子节点
        public void AddChild(TreeViewData child)
        {
            if (child == null) return;
            child.SetParent(this);
        }
        public void AddChild(IEnumerable<TreeViewData> children)
        {
            if (children == null) return;
            foreach (TreeViewData child in children)
            {
                AddChild(child);
            }
        }
        // 移除子节点
        public void RemoveChild(TreeViewData child)
        {
            if (child == null) return;
            childNodes.Remove(child);
            if (child.parent == this)
                child.parent = null;
        }
        public void RemoveChild(IEnumerable<TreeViewData> children)
        {
            if (children == null) return;
            foreach (TreeViewData child in children)
            {
                RemoveChild(child);
            }
        }
        // 清空子节点
        public void ClearChildren()
        {
            foreach (var child in childNodes)
            {
                if (child.parent == this)
                    child.parent = null;
            }
            childNodes.Clear();
        }
        // 重置子节点的父节点和层级
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
        // 查找子节点（按名称）
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

        public override bool Equals(object obj)
        {
            TreeViewData other = obj as TreeViewData;
            if (other == null) return false;
            return other.name.Equals(name) && other.layer.Equals(layer);
        }
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