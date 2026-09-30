using ReunionMovement.Common;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Example
{
    public class TreeViewExample : MonoBehaviour
    {
        public TreeView UITree = null;
        private TreeView subscribedTree;

        private sealed class NodeInfo
        {
            public string Code { get; }
            public int Level { get; }

            public NodeInfo(string code, int level)
            {
                Code = code;
                Level = level;
            }
        }

        public void Awake()
        {
            if (UITree == null)
            {
                Debug.LogError("TreeViewExample: 请指定 UITree。", this);
                enabled = false;
                return;
            }

            var roots = new List<TreeViewData>
            {
                CreateBranch("group-1", "1", false),
                CreateBranch("group-2", "2", true)
            };
            UITree.SetData(roots);
        }

        private void OnEnable()
        {
            if (UITree == null) return;
            subscribedTree = UITree;
            subscribedTree.NodeClicked += TextAction;
            subscribedTree.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            if (subscribedTree != null)
            {
                subscribedTree.NodeClicked -= TextAction;
                subscribedTree.SelectionChanged -= OnSelectionChanged;
            }
            subscribedTree = null;
        }

        private static TreeViewData CreateBranch(string id, string suffix, bool displayDecorate)
        {
            var root = new TreeViewData(id, "一级结构 " + suffix, new NodeInfo(id, 1));
            root.displayDecorate = displayDecorate;
            var parent = root;
            string[] names = { "二级结构 ", "三级结构 ", "四级结构 " };
            for (int index = 0; index < names.Length; index++)
            {
                string childId = id + "-level-" + (index + 2);
                var child = new TreeViewData(childId, names[index] + suffix,
                    new NodeInfo(childId, index + 2));
                parent.AddChild(child);
                parent = child;
            }
            return root;
        }

        [ContextMenu("TreeView/定位第一个四级节点（静默）")]
        public void SelectFirstLeaf()
        {
            SelectNode("group-1-level-4");
        }

        public void SelectNode(string id)
        {
            if (UITree == null || !UITree.ExpandTo(id)) return;
            UITree.Select(id);
        }

        public void ActivateNode(string id)
        {
            if (UITree == null || !UITree.ExpandTo(id)) return;
            UITree.Select(id, true);
        }

        public void SetNodeExpanded(string id, bool expanded)
        {
            if (UITree == null) return;
            if (expanded) UITree.Expand(id);
            else UITree.Collapse(id);
        }

        public void UpdateNodeDisplayDecorate(TreeViewData data, bool displayDecorate)
        {
            if (UITree == null || data == null) return;
            var target = UITree.FindById(data.Id);
            if (target == null) return;
            var pending = new Stack<TreeViewData>();
            pending.Push(target);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                node.displayDecorate = displayDecorate;
                if (node.childNodes == null) continue;
                foreach (var child in node.childNodes)
                {
                    if (child != null) pending.Push(child);
                }
            }
            UITree.RefreshAll();
        }

        private void OnSelectionChanged(TreeViewData data)
        {
            Log.Debug($"选中节点：{data.Id} / {data.name}");
        }

        public void TextAction(TreeViewData data)
        {
            if (data == null) return;
            if (data.TryGetData<NodeInfo>(out var info))
                Log.Debug($"点击了 {data.name}，业务编号：{info.Code}，级别：{info.Level}");
            else
                Log.Debug($"点击了 {data.name}，ID：{data.Id}");
        }
    }
}