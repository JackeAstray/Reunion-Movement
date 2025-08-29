using ReunionMovement.Common.Util;
using ReunionMovement.Common;
using System;
using UnityEngine;
using System.Threading.Tasks;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// UIController是每个UI界面的基类，负责管理UI的生命周期和基本功能。
    /// </summary>
    public class UIController : MonoBehaviour
    {
        // 界面名称，必须唯一
        public string UIName = "";
        // 界面优先级，数值越大优先级越高
        public int Priority { get; set; } = 0;

        #region 每个界面都有一个Canvas
        private Canvas canvas;
        public Canvas Canvas => canvas ??= GetComponent<Canvas>();
        #endregion

        #region 每个界面都有一个UIWindowAsset
        private UIWindowAsset windowAsset;
        public UIWindowAsset WindowAsset => windowAsset ??= GetComponent<UIWindowAsset>();
        #endregion

        /// <summary>
        /// 是否可见（直接与activeSelf绑定）
        /// </summary>
        public bool IsVisiable
        {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }

        public virtual void OnInit()
        {

        }

        public virtual void BeforeOpen(object[] onOpenArgs, Action doOpen)
        {
            doOpen?.Invoke();
        }

        /// <summary>
        /// UIController打开窗口时调用的方法，子类可以重写此方法来实现自定义逻辑。
        /// </summary>
        /// <param name="args"></param>
        public virtual void OnOpen(params object[] args)
        {
            IsVisiable = true;
        }

        /// <summary>
        /// UIController设置参数时调用的方法，子类可以重写此方法来实现自定义逻辑。
        /// </summary>
        /// <param name="args"></param>
        public virtual void OnSet(params object[] args)
        {

        }

        /// <summary>
        /// UIController关闭窗口时调用的方法，子类可以重写此方法来实现自定义逻辑。
        /// </summary>
        public virtual void OnClose()
        {
            IsVisiable = false;
        }

        /// <summary>
        /// UIModule打开窗口的快捷方式
        /// </summary>
        protected void OpenWindow(string uiName, params object[] args)
        {
            UISystem.Instance.OpenWindow(uiName, args);
        }

        /// <summary>
        /// UIModule关闭窗口的快捷方式
        /// </summary>
        /// <param name="uiName"></param>
        protected void CloseWindow(string uiName = null)
        {
            UISystem.Instance.CloseWindow(uiName ?? UIName);
        }

        /// <summary>
        /// 推荐使用字符串UI名称进行UI通讯，灵活且不易出错
        /// </summary>
        public static void CallUI(string uiName, Action<UIController, object[]> callback, params object[] args)
        {
            UISystem.Instance.CallUI(uiName, callback, args);
        }

        #region 功能
        /// <summary>
        /// 输入uri搜寻控件
        /// findTrans默认参数null时使用this.transform
        /// </summary>
        public T GetControl<T>(string uri, Transform findTrans = null, bool isLog = true) where T : UnityEngine.Object
        {
            return (T)GetControl(typeof(T), uri, findTrans, isLog);
        }

        /// <summary>
        /// 输入uri搜寻控件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="uri"></param>
        /// <param name="findTrans"></param>
        /// <param name="isLog"></param>
        /// <returns></returns>
        public object GetControl(Type type, string uri, Transform findTrans = null, bool isLog = true)
        {
            findTrans ??= transform;
            Transform trans = findTrans.Find(uri);
            if (trans == null)
            {
                if (isLog)
                {
                    Log.Error($"Get UI<{type.Name}> Control Error: {uri}");
                }
                return null;
            }

            return type == typeof(GameObject) ? trans.gameObject : trans.GetComponent(type);
        }

        /// <summary>
        /// 查找控件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public T FindControl<T>(string name) where T : Component
        {
            return AlgorithmUtil.Child<T>(gameObject, name);
        }

        /// <summary>
        /// 查找对象
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public GameObject FindGameObject(string name)
        {
            return AlgorithmUtil.Child(gameObject, name);
        }

        /// <summary>
        /// 清除一个GameObject下面所有的孩子
        /// </summary>
        /// <param name="go"></param>
        public void DestroyGameObjectChildren(GameObject go)
        {
            go.ClearChild();
        }

        /// <summary>
        /// 从数组获取参数，安全返回
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="openArgs"></param>
        /// <param name="offset"></param>
        /// <param name="isLog"></param>
        /// <returns></returns>
        protected T GetFromArgs<T>(object[] openArgs, int offset, bool isLog = true)
        {
            return openArgs.Get<T>(offset, isLog);
        }

        /// <summary>
        /// 淡入效果
        /// </summary>
        /// <param name="duration"></param>
        /// <returns></returns>
        public virtual async Task FadeIn(float duration = 0.2f)
        {
            var canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            gameObject.SetActive(true);
            float t = 0;
            while (t < duration)
            {
                canvasGroup.alpha = t / duration;
                t += Time.deltaTime;
                await Task.Yield();
            }
            canvasGroup.alpha = 1;
        }

        /// <summary>
        /// 淡出效果
        /// </summary>
        /// <param name="duration"></param>
        /// <returns></returns>
        public virtual async Task FadeOut(float duration = 0.2f)
        {
            var canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            float t = 0;
            while (t < duration)
            {
                canvasGroup.alpha = 1 - t / duration;
                t += Time.deltaTime;
                await Task.Yield();
            }
            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
        }
        #endregion
    }
}