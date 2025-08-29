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
            get { return nodePrefab ?? (nodePrefab = container.GetChild(0).gameObject); }
            set { nodePrefab = value; }
        }

        // 对象池
        private readonly List<GameObject> pool = new List<GameObject>();
        private Transform poolParent = null;

        /// <summary>
        /// 插入数据
        /// </summary>
        /// <param name="rootData"></param>
        public void Insert(List<TreeViewData> rootData)
        {
            if (container == null)
            {
                GetComponent();
            }
            treeRootNodes.Clear();
            foreach (var item in rootData)
            {
                TreeViewNode treeView = Instantiate(tvObj, container);
                treeView.Insert(item);
                treeRootNodes.Add(treeView);
            }
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
                var found = FindNodeRecursive(node, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 递归查找节点
        /// </summary>
        /// <param name="node"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private TreeViewNode FindNodeRecursive(TreeViewNode node, string name)
        {
            if (node.GetTreeData().name == name) return node;
            var data = node.GetTreeData();
            if (data.childNodes != null)
            {
                foreach (var child in data.childNodes)
                {
                    var childNode = FindNodeByName(child.name);
                    if (childNode != null) return childNode;
                }
            }
            return null;
        }

        /// <summary>
        /// 刷新所有节点
        /// </summary>
        public void RefreshAll()
        {
            foreach (var node in treeRootNodes)
            {
                node.Refresh();
            }
        }

        /// <summary>
        /// 批量设置装饰
        /// </summary>
        /// <param name="display"></param>
        public void SetAllDisplayDecorate(bool display)
        {
            foreach (var node in treeRootNodes)
            {
                node.SetDisplayDecorateRecursive(display);
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
                result.Add(Pop(datas[i], siblingIndex));
            }
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
            if (pool.Count > 0)
            {
                treeNode = pool[0];
                pool.RemoveAt(0);
            }
            else
            {
                treeNode = CloneTreeNode();
            }
            treeNode.transform.SetParent(container);
            treeNode.SetActive(true);
            treeNode.GetComponent<TreeViewNode>().Insert(data);
            treeNode.transform.SetSiblingIndex(siblingIndex + 1);
            return treeNode;
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
            if (poolParent == null)
            {
                poolParent = new GameObject("CachePool").transform;
            }
            treeNode.transform.SetParent(poolParent);
            treeNode.SetActive(false);
            pool.Add(treeNode);
        }
        /// <summary>
        /// 克隆节点
        /// </summary>
        /// <returns></returns>
        private GameObject CloneTreeNode()
        {
            GameObject result = Instantiate(NodePrefab);
            result.transform.SetParent(container);
            return result;
        }
    }
}
