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
                // container 可能为 null（Find("Viewport/Content") 未命中）或没有子节点，需防越界
                if (nodePrefab == null && container != null && container.childCount > 0)
                {
                    nodePrefab = container.GetChild(0).gameObject;
                }
                return nodePrefab;
            }
            set { nodePrefab = value; }
        }

        // 对象池
        private readonly Queue<GameObject> pool = new Queue<GameObject>();
        // 池容量上限：数据反复刷新/高频展开折叠时池无限增长会积压节点；超出直接销毁
        private const int MaxPoolSize = 128;
        private Transform poolParent = null;
        // 模板节点在容器中的索引缓存（-1 未缓存）：避免每次 Pop 线性扫描 container.childCount（批量展开 O(n²)→O(n)）；
        // 使用时校验缓存仍指向模板，失效自动重扫
        private int cachedTemplateIndex = -1;

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
            // 销毁旧根节点：仅 Clear 列表会泄漏 GameObject 与 Toggle 监听（数据刷新即累积泄漏）
            for (int i = 0; i < treeRootNodes.Count; i++)
            {
                var oldNode = treeRootNodes[i];
                if (oldNode != null) Destroy(oldNode.gameObject);
            }
            treeRootNodes.Clear();
            foreach (var item in rootData)
            {
                TreeViewNode treeView = Instantiate(tvObj);
                treeView.transform.SetParent(container, false);
                treeView.transform.localScale = Vector3.one;
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
            treeNode.SetActive(true);
            treeNode.GetComponent<TreeViewNode>().Insert(data);
            treeNode.transform.SetSiblingIndex(GetInsertSiblingIndex(siblingIndex));
            return treeNode;
        }

        /// <summary>
        /// 计算插入位置：不再假设模板节点恒在 index 0，基于模板当前实际位置动态计算（容器布局改动后仍正确）。
        /// 模板索引带缓存：仅首次/缓存失效时扫描一次，批量展开不再 O(n²)。
        /// </summary>
        private int GetInsertSiblingIndex(int siblingIndex)
        {
            int templateIndex = 0;
            if (nodePrefab != null && container != null)
            {
                // 缓存校验：索引越界或该位置不再是模板（布局被外部改动）时重新扫描
                if (cachedTemplateIndex < 0 || cachedTemplateIndex >= container.childCount
                    || container.GetChild(cachedTemplateIndex).gameObject != nodePrefab)
                {
                    cachedTemplateIndex = 0;
                    for (int i = 0; i < container.childCount; i++)
                    {
                        if (container.GetChild(i).gameObject == nodePrefab)
                        {
                            cachedTemplateIndex = i;
                            break;
                        }
                    }
                }
                templateIndex = cachedTemplateIndex;
            }
            return templateIndex + 1 + siblingIndex;
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
            if (treeNode == null) return;
            // 容量上限：池满时直接销毁，防止数据反复刷新场景下无限积压节点
            if (pool.Count >= MaxPoolSize)
            {
                UnityEngine.Object.Destroy(treeNode);
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
            // poolParent 是独立根对象，不随 TreeView 销毁。
            // 若 Clear() 未被调用（如运行中直接销毁 TreeView），必须在此清理，
            // 否则池内节点与 CachePool 会变成场景中的孤儿根对象长期累积。
            if (pool.Count > 0 || poolParent != null)
            {
                foreach (var obj in pool)
                {
                    if (obj != null) UnityEngine.Object.Destroy(obj);
                }
                pool.Clear();
                if (poolParent != null)
                {
                    UnityEngine.Object.Destroy(poolParent.gameObject);
                    poolParent = null;
                }
            }
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
        /// 清除所有已创建的节点与缓存（根节点、缓存池和池父对象），并销毁容器中除模板外的子对象。
        /// </summary>
        public void Clear()
        {
            if (container == null)
            {
                GetComponent();
            }

            // 销毁根节点对应的 GameObject
            foreach (var node in treeRootNodes)
            {
                if (node == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(node.gameObject);
                else
                    UnityEngine.Object.Destroy(node.gameObject);
#else
                UnityEngine.Object.Destroy(node.gameObject);
#endif
            }
            treeRootNodes.Clear();

            // 销毁池中对象
            foreach (var obj in pool)
            {
                if (obj == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(obj);
                else
                    UnityEngine.Object.Destroy(obj);
#else
                UnityEngine.Object.Destroy(obj);
#endif
            }
            pool.Clear();

            // 销毁池父对象
            if (poolParent != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(poolParent.gameObject);
                else
                    UnityEngine.Object.Destroy(poolParent.gameObject);
#else
                UnityEngine.Object.Destroy(poolParent.gameObject);
#endif
                poolParent = null;
            }

            // 清理容器中除模板外的子对象（如果有模板则保留）
            if (container != null)
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    var child = container.GetChild(i);
                    if (nodePrefab != null && child.gameObject == nodePrefab) continue;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    else
                        UnityEngine.Object.Destroy(child.gameObject);
#else
                    UnityEngine.Object.Destroy(child.gameObject);
#endif
                }
            }
        }
    }
}
