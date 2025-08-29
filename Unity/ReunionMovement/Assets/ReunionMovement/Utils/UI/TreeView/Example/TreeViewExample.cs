using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Example
{
    public class TreeViewExample : MonoBehaviour
    {
        public TreeView UITree = null;
        List<TreeViewData> rootData = new List<TreeViewData>();

        public void Awake()
        {
            var data = new TreeViewData("一级结构 1", new List<TreeViewData>()
            {
                new TreeViewData("二级结构 1",new List<TreeViewData>()
                {
                    new TreeViewData("三级结构 1",new List<TreeViewData>()
                    {
                        new TreeViewData("四级结构 1",new List<TreeViewData>()
                        {
                        },
                        ()=>{ Debug.Log("四级结构 1 测试"); }),
                    },
                    ()=>{ Debug.Log("三级结构 1 测试"); })
                    },
                null),
            }, null);
            rootData.Add(data);
            data = new TreeViewData("一级结构 2", new List<TreeViewData>()
            {
                new TreeViewData("二级结构 2",new List<TreeViewData>()
                {
                    new TreeViewData("三级结构 2",new List<TreeViewData>()
                    {
                        new TreeViewData("四级结构 2",new List<TreeViewData>()
                        {
                        },
                        ()=>{ Debug.Log("四级结构 2 测试"); }),
                    },
                    ()=>{ Debug.Log("三级结构 2 测试"); })
                    },
                null),
            }, null, 0, false, true);
            rootData.Add(data);

            UITree.Insert(rootData);
        }

        public void UpdateNodeDisplayDecorate(TreeViewData data, bool displayDecorate)
        {
            // 遍历所有的 TreeViewNode 并更新 displayDecorate
            foreach (var node in UITree.treeRootNodes)
            {
                if (node.GetTreeData() == data)
                {
                    node.SetDisplayDecorateRecursive(displayDecorate);
                }
            }
        }
    }
}