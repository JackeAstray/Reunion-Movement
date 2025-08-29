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
    /// 树节点
    /// </summary>
    public class TreeViewNode : UIBehaviour
    {
        public int layer = 0;
        public Sprite transparent;
        public Sprite arrow;
        public bool multiPlaceholder = false; // 新增开关
        private TreeViewData treeData;
        private TreeView uiTree;
        private Toggle toggle;
        private Image bg;
        private Transform placeholder;
        private Transform placeholderParent; // 新增：placeholder父物体
        private Transform decorate;
        private TextMeshProUGUI text;
        private Transform toggleTransform;
        private Transform myTransform;
        private Transform container;
        private List<GameObject> children = new List<GameObject>();
        private Action action;
        private bool enableAction = false;

        /// <summary>
        /// 获取组件
        /// </summary>
        private void GetComponent()
        {
            if (myTransform != null) return;
            myTransform = this.transform;
            bg = myTransform.GetComponent<Image>();
            container = myTransform.Find("Container");
            toggle = container.Find("Toggle").GetComponent<Toggle>();
            text = container.Find("Toggle/Text").GetComponent<TextMeshProUGUI>();
            decorate = container.Find("Decorate");
            placeholder = container.Find("Toggle/Placeholder");
            placeholderParent = placeholder.parent; // 新增：获取父物体
            toggleTransform = toggle.transform.Find("Icon");
            uiTree = myTransform.parent.parent.parent.GetComponent<TreeView>();
        }

        /// <summary>
        /// 重置组件状态
        /// </summary>
        private void ResetComponent()
        {
            container.localPosition = new Vector3(0, container.localPosition.y, 0);
            toggleTransform.localEulerAngles = new Vector3(0, 0, 90);
            toggleTransform.GetComponent<Image>().sprite = arrow;
        }

        /// <summary>
        /// 插入数据
        /// </summary>
        /// <param name="data"></param>
        public void Insert(TreeViewData data)
        {
            GetComponent();
            RemoveListener(); // 先移除旧监听，防止重复
            ResetComponent();
            treeData = data;
            text.text = data.name;
            toggle.isOn = false;
            toggle.onValueChanged.AddListener(OpenOrClose);
            container.localPosition += new Vector3(container.GetComponent<RectTransform>().sizeDelta.y * treeData.layer, 0, 0);
            if (data.childNodes.Count.Equals(0))
            {
                toggleTransform.GetComponent<Image>().sprite = transparent;
            }
            enableAction = data.enableAction;
            action = data.action;
            SetColor(data.layer);
            SetDisplayDecorate(data.displayDecorate);
            children.Clear();
        }

        /// <summary>
        /// 设置节点颜色
        /// </summary>
        /// <param name="layer"></param>
        public void SetColor(int layer)
        {
            this.layer = layer;
            if (multiPlaceholder)
            {
                // 只删除多余的Placeholder，保留Icon和Text
                for (int i = placeholderParent.childCount - 1; i >= 0; i--)
                {
                    var child = placeholderParent.GetChild(i);
                    if (child != placeholder && child.name == "Placeholder")
                    {
                        GameObject.Destroy(child.gameObject);
                    }
                }
                // 生成layer个placeholder（第一个用原有的，剩下的克隆）
                if (layer > 0)
                {
                    placeholder.gameObject.SetActive(true);
                    placeholder.SetSiblingIndex(0); // 确保模板在最前
                    for (int i = 1; i < layer; i++)
                    {
                        var clone = GameObject.Instantiate(placeholder.gameObject, placeholderParent);
                        clone.name = "Placeholder"; // 保证名字一致
                        clone.SetActive(true);
                        clone.transform.SetSiblingIndex(i - 1); // 依次插入到前面
                    }
                }
                else
                {
                    placeholder.gameObject.SetActive(false);
                }
            }
            else
            {
                if (layer > 0)
                {
                    placeholder.SetActive(true);
                    placeholder.SetSiblingIndex(0); // 只显示一个时也放最前
                }
                else
                {
                    placeholder.SetActive(false);
                }
                // 只删除多余的Placeholder，保留Icon和Text
                for (int i = placeholderParent.childCount - 1; i >= 0; i--)
                {
                    var child = placeholderParent.GetChild(i);
                    if (child != placeholder && child.name == "Placeholder")
                    {
                        GameObject.Destroy(child.gameObject);
                    }
                }
            }
            if (uiTree != null && layer < uiTree.colors.Count)
            {
                bg.color = uiTree.colors[layer];
            }
        }

        /// <summary>
        /// 设置当前节点的displayDecorate
        /// </summary>
        /// <param name="displayDecorate"></param>
        public void SetDisplayDecorate(bool displayDecorate)
        {
            if (decorate != null)
            {
                decorate.gameObject.SetActive(displayDecorate);
            }
        }

        /// <summary>
        /// 设置当前节点及其所有子节点的displayDecorate
        /// </summary>
        /// <param name="displayDecorate"></param>
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
        /// 刷新当前节点
        /// </summary>
        public void Refresh()
        {
            Insert(treeData);
        }

        public TreeViewData GetTreeData() => treeData;

        /// <summary>
        /// 打开或关闭子节点
        /// </summary>
        /// <param name="isOn"></param>
        private void OpenOrClose(bool isOn)
        {
            if (isOn) OpenChildren();
            else CloseChildren();
            toggleTransform.localEulerAngles = isOn ? new Vector3(0, 0, 0) : new Vector3(0, 0, 90);
            if (enableAction)
            {
                action?.Invoke();
            }
        }

        /// <summary>
        /// 打开子节点
        /// </summary>
        private void OpenChildren()
        {
            if (!enableAction)
            {
                action?.Invoke();
            }
            children = uiTree.Pop(treeData.childNodes, transform.GetSiblingIndex());
        }

        /// <summary>
        /// 关闭子节点并回收
        /// </summary>
        protected void CloseChildren()
        {
            foreach (var childObj in children)
            {
                if (childObj == null) continue;
                TreeViewNode node = childObj.GetComponent<TreeViewNode>();
                if (node != null)
                {
                    node.RemoveListener();
                    node.CloseChildren();
                }
            }
            uiTree.Push(children);
            children.Clear();
        }

        /// <summary>
        /// 移除监听，防止内存泄漏
        /// </summary>
        private void RemoveListener()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(OpenOrClose);
        }

        /// <summary>
        /// 查找子节点（递归）
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public TreeViewNode FindChildNode(string name)
        {
            if (treeData.childNodes == null) return null;
            foreach (var child in treeData.childNodes)
            {
                if (child.name == name)
                {
                    // 在当前children中查找对应的GameObject
                    foreach (var go in children)
                    {
                        var node = go.GetComponent<TreeViewNode>();
                        if (node != null && node.treeData == child)
                            return node;
                    }
                }
                // 递归查找
                foreach (var go in children)
                {
                    var node = go.GetComponent<TreeViewNode>();
                    var found = node?.FindChildNode(name);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}