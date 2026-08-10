using ReunionMovement.Common.Util;
using ReunionMovement.Common;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// UIController是每个UI界面的基类，负责管理UI的生命周期和基本功能。
    /// </summary>
    public class UIController : MonoBehaviour
    {
        // 界面名称，必须唯一
        public string uiName = "";
        // 界面优先级，数值越大优先级越高
        public int priority { get; set; } = 0;

        /// <summary>
        /// 键盘/手柄导航时，该窗口打开后默认选中的 GameObject（如第一个按钮）
        /// 在 Inspector 中拖入目标对象即可；若未设置，UIInputSystem 会自动查找第一个可交互的 Selectable
        /// </summary>
        public GameObject firstSelected;

        #region 每个界面都有一个Canvas
        private Canvas canvas;
        public Canvas Canvas
        {
            get
            {
                // 用 Unity == 判空（识别 fake-null）：组件被 Destroy 后缓存引用需重新获取，
                // ??= 无法识别已销毁对象，会抛 MissingReferenceException
                if (canvas == null) canvas = GetComponent<Canvas>();
                return canvas;
            }
        }
        #endregion

        #region 每个界面都有一个UIWindowAsset
        private UIWindowAsset windowAsset;
        public UIWindowAsset WindowAsset
        {
            get
            {
                if (windowAsset == null) windowAsset = GetComponent<UIWindowAsset>();
                return windowAsset;
            }
        }
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
            // 用 IsNullOrEmpty 替代 ??：空字符串不应原样传入 UISystem（会刷“未加载的UIWindow”错误）
            string target = string.IsNullOrEmpty(uiName) ? this.uiName : uiName;
            if (string.IsNullOrEmpty(target))
            {
                Log.Error("UIController.CloseWindow: uiName 与 this.uiName 均为空，无法关闭窗口");
                return;
            }
            UISystem.Instance.CloseWindow(target);
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
                    Log.Error("Get UI<{0}> Control Error: {1}", type.Name, uri);
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
        public virtual async UniTask FadeIn(float duration = 0.2f)
        {
            // 优先使用 TryGetComponent 避免 fake null 问题
            if (!gameObject.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0;
            gameObject.SetActive(true);
            float elapsed = 0;
            // 绑定销毁令牌：组件/窗口销毁时取消淡入，避免循环继续访问已销毁对象
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct); // UniTask.Yield 零 GC，替代 Task.Delay
                }
                canvasGroup.alpha = 1;
            }
            catch (OperationCanceledException)
            {
                // 窗口已销毁，静默退出
            }
        }

        /// <summary>
        /// 淡出效果
        /// </summary>
        /// <param name="duration"></param>
        /// <returns></returns>
        public virtual async UniTask FadeOut(float duration = 0.2f)
        {
            if (!gameObject.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            float elapsed = 0;
            // 绑定销毁令牌：组件/窗口销毁时取消淡出，避免循环继续访问已销毁对象
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(1f - elapsed / duration);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                canvasGroup.alpha = 0;
                gameObject.SetActive(false);
            }
            catch (OperationCanceledException)
            {
                // 窗口已销毁，静默退出
            }
        }
        #endregion
    }
}