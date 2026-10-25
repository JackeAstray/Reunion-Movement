using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReunionMovement.Core.UI
{
    public partial class UISystem
    {
        #region UI operation - open / close / pool
        /// <summary>
        /// 打开窗口（非复制）
        /// <summary>
        /// 打开窗口（字符串键）—— 保持现有 API 兼容
        /// </summary>
        internal UILoadState OpenWindow(string uiName, params object[] args)
        {
            //TODO: 需要先创建脚本对象，再根据脚本中的值进行加载资源
            UILoadState uiState;

            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                uiState = LoadWindow(uiName, true, args);
                Log.Debug("[UISystem] OpenWindow({0}) LoadWindow → {1}", uiName, (uiState != null ? "OK" : "NULL"));
                return uiState;
            }

            if (!uiState.isOnInit)
            {
                uiState.isOnInit = true;
                if (uiState.uiWindow != null)
                {
                    // 用窗口自身初始化标志（池化复用场景 IsInitialized 已为 true，不再重复 OnInit）
                    if (!uiState.uiWindow.IsInitialized)
                    {
                        uiState.uiWindow.OnInit();
                        uiState.uiWindow.MarkInitialized();
                    }
                }
            }

            OnOpen(uiState, args);
            return uiState;
        }

        /// <summary>
        /// [推荐] 异步打开窗口 —— 走 LoadWindowAsync（Addressables 双轨加载，失败自动降级 Resources）。
        /// </summary>
        internal async UniTask<UILoadState> OpenWindowAsync(string uiName, params object[] args)
        {
            UILoadState uiState;

            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                uiState = await LoadWindowAsync(uiName, true, args);
                Log.Debug("[UISystem] OpenWindowAsync({0}) LoadWindowAsync → {1}", uiName, (uiState != null ? "OK" : "NULL"));
                return uiState;
            }

            if (!uiState.isOnInit)
            {
                uiState.isOnInit = true;
                if (uiState.uiWindow != null)
                {
                    if (!uiState.uiWindow.IsInitialized)
                    {
                        uiState.uiWindow.OnInit();
                        uiState.uiWindow.MarkInitialized();
                    }
                }
            }

            OnOpen(uiState, args);
            return uiState;
        }

        /// <summary>
        /// [推荐] 类型安全的异步打开窗口 —— 通过 UIController 类型名自动推断 UI 名称。
        /// 使用方式：UISystem.Instance.OpenWindowAsync&lt;PopupUIPlane&gt;(args);
        /// </summary>
        public async UniTask<UILoadState> OpenWindowAsync<T>(params object[] args) where T : UIController
        {
            return await OpenWindowAsync(typeof(T).Name, args);
        }

        /// <summary>
        /// [推荐] 类型安全的打开窗口重载 —— 通过 UIController 类型名自动推断 UI 名称。
        /// 使用方式：UISystem.Instance.OpenWindow&lt;PopupUIPlane&gt;(args);
        /// 优点：编译时检查、重构安全、无字符串拼写错误。
        /// </summary>
        /// <typeparam name="T">UI 控制器类型（类名需与 prefab 名一致）</typeparam>
        /// <param name="args">打开参数</param>
        public UILoadState OpenWindow<T>(params object[] args) where T : UIController
        {
            // 约定：类型名 = prefab 名（如 PopupUIPlane 对应 PopupUIPlane.prefab）
            string uiName = typeof(T).Name;
            return OpenWindow(uiName, args);
        }

        /// <summary>
        /// 设置窗口
        /// </summary>
        /// <param name="uiState"></param>
        /// <param name="args"></param>
        private void OnSet(UILoadState uiState, params object[] args)
        {
            // LoadWindow 为同步加载，不存在“加载中”状态，无需 isLoading 分支
            UIController uiBase = uiState.uiWindow;

            if (uiBase != null && uiBase.gameObject.activeSelf)
            {
                // 在 BeforeOpen 之前激活，确保 UI 可见
                uiBase.gameObject.SetActive(true);

                uiBase.BeforeOpen(() =>
                {
                    float setStartTime = Time.realtimeSinceStartup;
                    uiBase.OnReopen();
                    float setElapsed = Time.realtimeSinceStartup - setStartTime;

                    Log.Debug(string.Format("OnSet UI {0}, cost {1}", uiBase.gameObject.name, setElapsed));

                    // 订阅者异常隔离：坏订阅者不应中断 SetWindow 流程
                    try
                    {
                        OnSetSubject.OnNext(uiBase);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("OnSetSubject 订阅者异常（已隔离）: {0}", ex.Message);
                    }
                });
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="t"></param>
        public void CloseWindow(Type t)
        {
            CloseWindow(t.Name);
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void CloseWindow<T>()
        {
            CloseWindow(typeof(T));
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="name"></param>
        public void CloseWindow(string name)
        {
            UILoadState uiState;

            // 未开始Load
            if (!uiStateCache.TryGetValue(name, out uiState))
            {
                Log.Error("[CloseWindow]没有加载的UIWindow: {0}", name);
                return;
            }

            // LoadWindow 为同步加载，不存在“加载中”状态，无需 isLoading 分支
            // 判空防护：窗口 GameObject 被外部销毁（极端并发/误删）时不得 NRE 中断关闭流程
            if (uiState?.uiWindow == null)
            {
                Log.Warning("[CloseWindow] uiWindow 已不存在，移除残留缓存项: {0}", name);
                uiStateCache.Remove(name);
                return;
            }
            uiState.uiWindow.gameObject.SetActive(false);

            uiState.uiWindow.OnClose();

            // 订阅者异常隔离：坏订阅者不应中断 CloseWindow 后续的窗口销毁
            try
            {
                OnCloseSubject.OnNext(uiState.uiWindow);
            }
            catch (Exception ex)
            {
                Log.Error("OnCloseSubject 订阅者异常（已隔离）: {0}", ex.Message);
            }

            if (!uiState.isStaticUI)
            {
                DestroyWindow(name);
            }
        }

        /// <summary>
        /// 销毁所有具有LoadState的窗口。请小心使用。
        /// </summary>
        public void DestroyAllWindows()
        {
            CloseAllWindows();

            List<string> LoadList = new List<string>(uiStateCache.Keys);
            foreach (string item in LoadList)
            {
                DestroyWindow(item);
            }
        }

        /// <summary>
        /// 关闭全部窗口
        /// </summary>
        public void CloseAllWindows()
        {
            // 复用成员缓冲（避免每次批量关闭 new List 分配）
            closeBuffer.Clear();
            foreach (KeyValuePair<string, UILoadState> uiWindow in uiStateCache)
            {
                if (IsOpen(uiWindow.Key))
                {
                    closeBuffer.Add(uiWindow.Key);
                }
            }

            for (int i = closeBuffer.Count - 1; i >= 0; i--)
            {
                CloseWindow(closeBuffer[i]);
            }
            closeBuffer.Clear();
        }

        /// <summary>
        /// 销毁窗口
        /// </summary>
        /// <param name="uiName"></param>
        /// <param name="destroyImmediate"></param>
        public void DestroyWindow(string uiName)
        {
            UILoadState uiState;
            uiStateCache.TryGetValue(uiName, out uiState);
            if (uiState == null || uiState.uiWindow == null)
            {
                Log.Warning("{0} 已被销毁", uiName);
                return;
            }

            // 池化路径：白名单内的轻量窗口放池复用（消除反复 DestroyImmediate/Instantiate 的 GC 与 Canvas 重建）；
            // 同帧关闭再打开时从池取出同一对象，天然避免双实例
// 池化：窗口声明了 [UIWindow(Pooled = true)] 或仍在老白名单里就复用。
            // 老白名单保留是为了不破坏现有工程的显式配置。
            bool poolable = UIWindowCatalog.Get(uiName).Pooled;
            if (enableWindowPool && poolable)
            {
                PoolWindow(uiName, uiState.uiWindow.gameObject);
            }
            else
            {
                // 使用 DestroyImmediate 立即销毁：Destroy 延迟到帧末，若同帧内重新打开同名窗口，
                // LoadWindow 会实例化新对象而旧对象尚未销毁 → 短暂双实例。
                // UI 窗口关闭时本应停止自身协程/动画，立即销毁是安全且符合预期的。
                UnityEngine.Object.DestroyImmediate(uiState.uiWindow.gameObject);
            }

            uiState.uiWindow = null;
            uiStateCache.Remove(uiName);
        }

        /// <summary>窗口放回对象池：调用 OnDespawned 钩子 → 挂池根并隐藏。池满直接销毁当前对象。</summary>
        private void PoolWindow(string uiName, GameObject windowObj)
        {
            // Clear() 已销毁 UI 根（uiRoot=null）：不再池化，直接销毁，避免窗口挂到根层级成为孤儿对象
            if (uiRoot == null)
            {
                UnityEngine.Object.DestroyImmediate(windowObj);
                return;
            }
            if (!windowPool.TryGetValue(uiName, out var stack))
            {
                stack = new Stack<GameObject>();
                windowPool[uiName] = stack;
            }
            if (stack.Count >= Mathf.Max(0, maxPooledWindowsPerName))
            {
                UnityEngine.Object.DestroyImmediate(windowObj);
                return;
            }

            var controller = windowObj.GetComponent<UIController>();
            if (controller != null)
            {
                try { controller.OnDespawned(); }
                catch (Exception ex) { Log.Warning("[UISystem] {0} OnDespawned 异常（已隔离）: {1}", uiName, ex.Message); }
            }
            windowObj.transform.SetParent(uiRoot != null ? uiRoot.transform : null);
            windowObj.SetActive(false);
            stack.Push(windowObj);
        }

        /// <summary>从对象池取出窗口（无则返回 null）。fake-null 对象自动丢弃。</summary>
        private GameObject GetPooledWindow(string uiName)
        {
            if (!enableWindowPool) return null;
            if (!windowPool.TryGetValue(uiName, out var stack) || stack.Count == 0) return null;

            var go = stack.Pop();
            if (go == null) return null; // 池内对象被外部销毁（fake-null）：丢弃

            var controller = go.GetComponent<UIController>();
            if (controller != null)
            {
                try { controller.OnSpawned(); }
                catch (Exception ex) { Log.Warning("[UISystem] {0} OnSpawned 异常（已隔离）: {1}", uiName, ex.Message); }
            }
            return go;
        }
        #endregion
    }
}
