using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReunionMovement
{
    /// <summary>
    /// 树形视图
    /// </summary>
    public class TreeView : UIBehaviour
    {
        // 图标资源
        public Sprite openIcon;
        public Sprite closeIcon;
        public Sprite lastLayerIcon;
        public List<Color> colors = new List<Color>();
        public TreeViewNode tvObj;
        public List<TreeViewNode> treeRootNodes = new List<TreeViewNode>();
        private Transform container;
        private GameObject nodePrefab;
        public GameObject NodePrefab
        {
            get
            {
                return nodePrefab != null ? nodePrefab : tvObj != null ? tvObj.gameObject : null;
            }
            set { nodePrefab = value; }
        }

        // 对象池
        private readonly Queue<GameObject> pool = new Queue<GameObject>();
        // 池容量上限：数据反复刷新/高频展开折叠时池无限增长会积压节点；超出直接销毁
        private const int MaxPoolSize = 128;
        private Transform poolParent = null;
        private readonly Dictionary<string, TreeViewData> dataById = new Dictionary<string, TreeViewData>();
        private readonly Dictionary<string, string> parentById = new Dictionary<string, string>();
        public event Action<TreeViewData> NodeClicked;
        public event Action<TreeViewData> SelectionChanged;
        public TreeViewData SelectedNode { get; private set; }

        public void SetData(IEnumerable<TreeViewData> roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            Insert(new List<TreeViewData>(roots));
        }

        private void IndexNodes(IEnumerable<TreeViewData> nodes, string parentId,
            Dictionary<string, TreeViewData> index, Dictionary<string, string> parents)
        {
            foreach (var node in nodes)
            {
                if (node == null) continue;
                if (index.ContainsKey(node.Id)) throw new ArgumentException("Duplicate node ID or cyclic tree: " + node.Id);
                index.Add(node.Id, node);
                parents.Add(node.Id, parentId);
                if (node.childNodes != null) IndexNodes(node.childNodes, node.Id, index, parents);
            }
        }

        public TreeViewData FindById(string id)
        {
            return id != null && dataById.TryGetValue(id, out var data) ? data : null;
        }

        private TreeViewNode FindView(string id)
        {
            foreach (var root in treeRootNodes)
            {
                if (root == null) continue;
                var view = root.FindById(id);
                if (view != null) return view;
            }
            return null;
        }

        public bool ExpandTo(string id)
        {
            if (FindById(id) == null) return false;
            var ancestors = new Stack<string>();
            var parentId = parentById[id];
            while (parentId != null)
            {
                ancestors.Push(parentId);
                parentId = parentById[parentId];
            }
            while (ancestors.Count > 0) FindView(ancestors.Pop())?.SetExpanded(true);
            return FindView(id) != null;
        }

        public bool Expand(string id)
        {
            if (!ExpandTo(id)) return false;
            FindView(id).SetExpanded(true);
            return true;
        }

        public bool Collapse(string id)
        {
            var view = FindView(id);
            if (view == null) return false;
            view.SetExpanded(false);
            return true;
        }

        public bool Select(string id, bool notify = false)
        {
            var data = FindById(id);
            if (data == null) return false;
            bool changed = !ReferenceEquals(SelectedNode, data);
            SelectedNode = data;
            if (notify)
            {
                if (changed) SelectionChanged?.Invoke(data);
                if (data.enableAction)
                {
                    if (NodeClicked != null) NodeClicked.Invoke(data);
                    else data.action?.Invoke(data);
                }
            }
            return true;
        }

        /// <summary>
        /// 插入数据
        /// </summary>
        /// <param name="rootData"></param>
        public void Insert(List<TreeViewData> rootData)
        {
            if (rootData == null) throw new ArgumentNullException(nameof(rootData));
            if (container == null)
            {
                GetComponent();
            }
            if (container == null || NodePrefab == null || NodePrefab.GetComponent<TreeViewNode>() == null)
            {
                Debug.LogError("TreeView requires Viewport/Content and a TreeViewNode prefab.", this);
                return;
            }
            var index = new Dictionary<string, TreeViewData>();
            var parents = new Dictionary<string, string>();
            IndexNodes(rootData, null, index, parents);
            var selectedId = SelectedNode?.Id;
            var expandedIds = new List<string>();
            foreach (var entry in dataById)
            {
                if (FindView(entry.Key)?.IsExpanded == true) expandedIds.Add(entry.Key);
            }
            ReleaseRoots();
            dataById.Clear();
            parentById.Clear();
            foreach (var entry in index) dataById.Add(entry.Key, entry.Value);
            foreach (var entry in parents) parentById.Add(entry.Key, entry.Value);
            foreach (var item in rootData)
            {
                if (item == null) continue;
                var root = Pop(item, container.childCount);
                treeRootNodes.Add(root.GetComponent<TreeViewNode>());
            }
            SelectedNode = FindById(selectedId);
            foreach (var id in expandedIds) Expand(id);
        }

        private void ReleaseRoots()
        {
            foreach (var root in treeRootNodes)
            {
                if (root != null) Push(root.gameObject);
            }
            treeRootNodes.Clear();
        }

        private static void DestroyNode(GameObject node)
        {
            if (node == null) return;
            node.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(node);
            else UnityEngine.Object.DestroyImmediate(node);
        }

        /// <summary>
        /// 查找节点（按名称）
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public TreeViewNode FindNodeByName(string name)
        {
            foreach (var node in treeRootNodes)
            {
                // 与 269/284 的销毁循环一致：节点可能被外部销毁而列表尚未清理（假空），需跳过
                if (node == null) continue;
                if (node.GetTreeData() != null && node.GetTreeData().name == name) return node;
                var found = node.FindChildNode(name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 刷新所有节点
        /// </summary>
        public void RefreshAll()
        {
            var roots = new List<TreeViewData>();
            foreach (var node in treeRootNodes)
                if (node != null && node.GetTreeData() != null) roots.Add(node.GetTreeData());
            SetData(roots);
        }

        /// <summary>
        /// 批量设置装饰
        /// </summary>
        /// <param name="display"></param>
        public void SetAllDisplayDecorate(bool display)
        {
            foreach (var node in treeRootNodes)
            {
                // 与 269/284 的销毁循环一致：跳过被外部销毁的假空节点（本文件三处 treeRootNodes
                // 遍历的守卫至此补齐：FindNodeByName / RefreshAll / 此处）
                if (node != null) node.SetDisplayDecorateRecursive(display);
            }
        }

        /// <summary>
        /// 获取组件
        /// </summary>
        private void GetComponent()
        {
            container = transform.Find("Viewport/Content");
        }

        /// <summary>
        /// 批量弹出节点
        /// </summary>
        /// <param name="datas"></param>
        /// <param name="siblingIndex"></param>
        /// <returns></returns>
        public List<GameObject> Pop(List<TreeViewData> datas, int siblingIndex)
        {
            List<GameObject> result = new List<GameObject>();
            for (int i = datas.Count - 1; i >= 0; i--)
            {
                if (datas[i] != null) result.Add(Pop(datas[i], siblingIndex));
            }
            result.Reverse();
            return result;
        }
        /// <summary>
        /// 弹出节点
        /// </summary>
        /// <param name="data"></param>
        /// <param name="siblingIndex"></param>
        /// <returns></returns>
        public GameObject Pop(TreeViewData data, int siblingIndex)
        {
            GameObject treeNode = null;
            // Queue 出队 O(1)：原 List.RemoveAt(0) 每次 Pop 移位 O(n)，批量展开退化 O(n²)。
            // 场景切换/池根被销毁后，池内引用会变成 fake-null，出队时逐个剔除重建
            while (pool.Count > 0)
            {
                treeNode = pool.Dequeue();
                if (treeNode != null) break;
                treeNode = null;
            }
            if (treeNode == null)
            {
                treeNode = CloneTreeNode();
            }
            treeNode.transform.SetParent(container, false);
            treeNode.transform.localScale = Vector3.one;
            treeNode.GetComponent<TreeViewNode>().Insert(data);
            treeNode.transform.SetSiblingIndex(GetInsertSiblingIndex(siblingIndex));
            treeNode.SetActive(true);
            return treeNode;
        }

        /// <summary>
        /// 在父节点的实际同级索引之后插入。
        /// </summary>
        private int GetInsertSiblingIndex(int siblingIndex)
        {
            return siblingIndex + 1;
        }
        /// <summary>
        /// 批量回收节点
        /// </summary>
        /// <param name="treeNodes"></param>
        public void Push(List<GameObject> treeNodes)
        {
            foreach (GameObject node in treeNodes)
            {
                Push(node);
            }
        }
        /// <summary>
        /// 回收节点
        /// </summary>
        /// <param name="treeNode"></param>
        public void Push(GameObject treeNode)
        {
            if (treeNode == null || treeNode == NodePrefab || pool.Contains(treeNode)) return;
            treeNode.GetComponent<TreeViewNode>()?.Unbind();
            // 容量上限：池满时直接销毁，防止数据反复刷新场景下无限积压节点
            if (pool.Count >= MaxPoolSize)
            {
                DestroyNode(treeNode);
                return;
            }
            if (poolParent == null)
            {
                poolParent = new GameObject("CachePool").transform;
            }
            treeNode.transform.SetParent(poolParent, false);
            treeNode.transform.localScale = Vector3.one;
            treeNode.SetActive(false);
            pool.Enqueue(treeNode);
        }

        protected override void OnDestroy()
        {
            Clear();
            base.OnDestroy();
        }
        /// <summary>
        /// 克隆节点
        /// </summary>
        /// <returns></returns>
        private GameObject CloneTreeNode()
        {
            GameObject result = Instantiate(NodePrefab);
            result.transform.SetParent(container, false);
            result.transform.localScale = Vector3.one;
            return result;
        }

        /// <summary>
        /// 清除本树创建的节点与缓存，保留模板和容器内其他对象。
        /// </summary>
        public void Clear()
        {
            ReleaseRoots();
            dataById.Clear();
            parentById.Clear();
            SelectedNode = null;
            foreach (var obj in pool)
            {
                DestroyNode(obj);
            }
            pool.Clear();
            if (poolParent != null)
            {
                DestroyNode(poolParent.gameObject);
                poolParent = null;
            }
        }
    }
}
