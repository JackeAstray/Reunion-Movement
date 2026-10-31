using Gear.Common;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Framework.Runtime.UI
{
    public partial class UISystem
    {
        #region UI操作
        /// <summary>
        /// 初始化UI
        /// </summary>
        /// <param name="uiObj"></param>
        private void InitUIAsset(GameObject uiObj)
        {
            if (!uiObj)
            {
                Log.Error("UI对象为空。");
                return;
            }
            var windowAsset = uiObj.GetComponent<UIWindowAsset>();
            if (windowAsset == null)
            {
                Log.Error("UI对象 {0} 缺少 UIWindowAsset 组件！", uiObj.name);
                return;
            }
            var parent = windowAsset.panelType switch
            {
                PanelType.MainUI => mainUIRoot.transform,
                PanelType.NormalUI => normalUIRoot.transform,
                PanelType.HeadInfoUI => headInfoUIRoot.transform,
                PanelType.TipsUI => tipsUIRoot.transform,
                _ => uiRoot.transform
            };
            // 层级来源：显式声明的 [UIWindow(Layer = …)] 优先；没声明就沿用预制体上的 PanelType，
            // 因此现有 3 个窗口行为完全不变。
            var descriptor = UIWindowCatalog.Get(uiObj.name);
            if (descriptor.Declared && descriptor.Layer != windowAsset.panelType)
            {
                parent = descriptor.Layer switch
                {
                    PanelType.MainUI => mainUIRoot.transform,
                    PanelType.NormalUI => normalUIRoot.transform,
                    PanelType.HeadInfoUI => headInfoUIRoot.transform,
                    PanelType.TipsUI => tipsUIRoot.transform,
                    _ => uiRoot.transform
                };
            }
            uiObj.transform.SetParent(parent);
            if (parent == uiRoot.transform)
            {
                Log.Error("没有默认PanelType: {0}", descriptor.Declared ? descriptor.Layer : windowAsset.panelType);
            }
        }

        /// <summary>
        /// 加载UI
        /// </summary>
        /// <param name="name"></param>
        /// <param name="openWhenFinish"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal UILoadState LoadWindow(string name, bool openWhenFinish, params object[] args)
        {
            if (uiStateCache.TryGetValue(name, out var existingState))
            {
                return existingState;
            }

            // 并发去重（与异步路径对齐）：已有同名窗口在途异步加载时，同步实例化会与
            // 异步完成后的 SetupLoadedWindow → uiStateCache.Add 键冲突（抛异常 + 孤儿窗口）。
            // 同步方法无法等待异步结果，返回 null 提示调用方等待或改走异步。
            if (windowLoadGate.IsInflight(name))
            {
                Log.Warning("[UISystem] LoadWindow({0}) 与在途 LoadWindowAsync 冲突，返回 null（请等待异步结果或改用异步 API）", name);
                return null;
            }

            // 同步路径：仅走 Resources（Addressables 无法真正同步加载，WebGL 不支持 WaitForCompletion）。
            // 推荐使用 LoadWindowAsync 走 Addressables 双轨。
            // 优先从窗口池复用（白名单内轻量窗口），池空才实例化
            GameObject uiObj = GetPooledWindow(name);
            if (uiObj == null)
            {
                uiObj = Modules.Get<ResourcesSystem>().InstantiateAsset<GameObject>(Config.UIPath + name);
            }
            Log.Debug("[UISystem] LoadWindow({0}) Instantiate → {1}, activeSelf={2}", name, (uiObj ? uiObj.name : "NULL"), uiObj?.activeSelf);
            if (uiObj == null)
            {
                return null;
            }

            return SetupLoadedWindow(uiObj, name, openWhenFinish, args);
        }

        /// <summary>
        /// [推荐] 异步加载 UI —— Addressables 优先（AddressableKeys.UIRoot + name），
        /// 失败自动降级 Resources（Config.UIPath + name）。
        /// 实例化完成后立即释放源 Prefab 的 Addressables 引用，避免泄漏。
        /// </summary>
        internal async UniTask<UILoadState> LoadWindowAsync(string name, bool openWhenFinish, params object[] args)
        {
            return await LoadWindowAsyncCore(name, openWhenFinish, args);
        }

        /// <summary>
        /// 带超时重载：timeoutSeconds > 0 时超时返回 null（调用方自行降级/重试），
        /// 防止 Catalog 失败或 CDN 卡死时调用方无限等待；<=0 表示不超时。
        /// </summary>
        internal async UniTask<UILoadState> LoadWindowAsync(string name, bool openWhenFinish, float timeoutSeconds, params object[] args)
        {
            var task = LoadWindowAsyncCore(name, openWhenFinish, args);
            if (timeoutSeconds <= 0f)
            {
                return await task;
            }
            // 超时不受 timeScale 影响；超时返回 null，在途加载继续（结果仍会广播给其他等待方）
            var (isTimeout, result) = await task.TimeoutWithoutException(TimeSpan.FromSeconds(timeoutSeconds), DelayType.UnscaledDeltaTime);
            return isTimeout ? null : result;
        }

        private async UniTask<UILoadState> LoadWindowAsyncCore(string name, bool openWhenFinish, object[] args)
        {
            if (uiStateCache.TryGetValue(name, out var existingState))
            {
                return existingState;
            }

            // 并发去重：同名窗口在途加载时等待同一结果（SingleFlightLoader 统一管理
            // 单飞/取消/摘除语义；Clear() 会 CancelAll，等待方按“加载失败”返回 null）
            UILoadState loaded = null;
            try
            {
                loaded = await windowLoadGate.RunAsync(name, async () =>
                {
                    GameObject prefab = null;
                    bool fromAddressables = false;

                    // 双轨：Addressables 优先
                    if (Config.AddressablesMode != AddressablesMode.Off)
                    {
                        prefab = await AddressableSystem.Instance.LoadAssetAsync<GameObject>(AddressableKeys.UIRoot + name);
                        fromAddressables = prefab != null;
                        if (prefab != null)
                        {
                            Log.Debug("[UISystem] LoadWindowAsync({0}) 从 Addressables 加载成功: {1}", name, AddressableKeys.UIRoot + name);
                        }
                    }

                    // Clear() 防护：await 期间系统可能被清理（UI 根节点已销毁、缓存已清空），
                    // 继续 Instantiate + 登记会把孤儿窗口写进新缓存或触发空引用
                    if (uiRoot == null)
                    {
                        Log.Warning("[UISystem] LoadWindowAsync({0}) 中止：加载期间系统已清理", name);
                        // 已从 Addressables 取到引用时同样要归还，否则 bundle 引用计数泄漏
                        ReleaseLoadedPrefab(name, prefab, fromAddressables);
                        return null;
                    }

                    // 降级：Resources
                    if (prefab == null)
                    {
                        prefab = Modules.Get<ResourcesSystem>().Load<GameObject>(Config.UIPath + name);
                        if (prefab != null)
                        {
                            Log.Debug("[UISystem] LoadWindowAsync({0}) 降级 Resources 加载成功: {1}", name, Config.UIPath + name);
                        }
                    }

                    if (prefab == null)
                    {
                        Log.Error("[UISystem] LoadWindowAsync({0}) 加载失败（Addressables + Resources 均未命中）", name);
                        return null;
                    }

                    GameObject uiObj;
                    try
                    {
                        uiObj = GetPooledWindow(name);
                        if (uiObj == null)
                        {
                            uiObj = UnityEngine.Object.Instantiate(prefab);
                        }
                    }
                    finally
                    {
                        // 不论走池化还是新建，预加载的资源引用都必须归还：
                        // 旧实现只在 Instantiate 分支释放，池化命中时每打开一次就泄漏一次。
                        ReleaseLoadedPrefab(name, prefab, fromAddressables);
                    }

                    return SetupLoadedWindow(uiObj, name, openWhenFinish, args);
                });
            }
            finally
            {
                // 加载失败/取消时，加载期间累积的关闭意图已经无意义，直接丢弃
                if (loaded == null) pendingCloseRequests.Remove(name);
            }

            // 加载期间用户点了关闭：窗口已经登记并打开，立刻关闭并从结果里抹掉，
            // 避免"点了取消反而弹出窗口"，也避免 UIRouter 把已关闭窗口压栈。
            if (loaded != null && pendingCloseRequests.Remove(name))
            {
                Log.Debug("[UISystem] LoadWindowAsync({0}) 加载期间收到关闭请求，直接关闭", name);
                CloseWindow(name);
                return null;
            }

            return loaded;
        }

        /// <summary>归还窗口预加载得到的资源引用（Addressables 句柄 / Resources 缓存）。</summary>
        private void ReleaseLoadedPrefab(string name, GameObject prefab, bool fromAddressables)
        {
            if (prefab == null) return;
            try
            {
                if (fromAddressables)
                {
                    AddressableSystem.Instance.ReleaseAsset(prefab);
                }
                else
                {
                    Modules.Get<ResourcesSystem>().DeleteAssetCache(Config.UIPath + name);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[UISystem] 归还窗口资源引用失败 {0}: {1}", name, ex.Message);
            }
        }

        /// <summary>
        /// 对已实例化的 UI GameObject 完成初始化并登记（LoadWindow / LoadWindowAsync 共用）。
        /// </summary>
        private UILoadState SetupLoadedWindow(GameObject uiObj, string name, bool openWhenFinish, object[] args)
        {
            uiObj.name = name;
            InitUIAsset(uiObj);
            uiObj.transform.localRotation = Quaternion.identity;
            uiObj.transform.localScale = Vector3.one;

            var uiController = uiObj.GetComponent<UIController>() ?? CreateUIController(uiObj, name);
            if (uiController == null)
            {
                Log.Error("加载 UI {0} 失败，找不到或无法创建 UIController 脚本！", name);
                UnityEngine.Object.Destroy(uiObj);
                return null;
            }
            Log.Debug("[UISystem] LoadWindow({0}) uiController={1}", name, uiController.GetType().Name);

            var uiLoadState = new UILoadState(name)
            {
                uiWindow = uiController,
                openWhenFinish = openWhenFinish,
                openArgs = args,
                isOnInit = true
            };
            uiLoadState.uiWindow.uiName = name;

            // 双保险兑底：任何并发竞态（如同步+异步同名并发）到达此处时，若缓存已存在同名条目，
            // 销毁冗余实例并复用旧状态，避免 Add 键冲突异常与孤儿窗口。
            if (uiStateCache.TryGetValue(name, out var existing))
            {
                Log.Warning("[UISystem] SetupLoadedWindow({0}) 缓存已存在同名条目，销毁冗余实例", name);
                UnityEngine.Object.Destroy(uiObj);
                return existing;
            }
            uiStateCache.Add(name, uiLoadState);

            InitWindow(uiLoadState, uiLoadState.uiWindow, uiLoadState.openWhenFinish, uiLoadState.openArgs);
            Log.Debug("[UISystem] LoadWindow({0}) 完成, activeSelf={1}", name, uiController.gameObject.activeSelf);

            return uiLoadState;
        }

        /// <summary>
        /// 初始化UI
        /// </summary>
        private void InitWindow(UILoadState uiState, UIController uiBase, bool open, params object[] args)
        {
            // 先关闭防止初始化过程中的闪烁，open=true 时 OnOpen 会重新激活
            uiBase.gameObject.SetActive(false);

            // OnInit 全生命周期只执行一次：窗口池复用时跳过，防止 OnInit 内的订阅等重复执行
            if (!uiBase.IsInitialized)
            {
                uiBase.OnInit();
                uiBase.MarkInitialized();

                Log.Debug("OnInit UI {0}", uiBase.gameObject.name);

                // 订阅者异常隔离：坏订阅者不应中断 InitWindow 后续的开窗流程
                try
                {
                    OnInitSubject.OnNext(uiBase);
                }
                catch (Exception ex)
                {
                    Log.Error("OnInitSubject 订阅者异常（已隔离）: {0}", ex.Message);
                }
            }

            if (open)
            {
                OnOpen(uiState, args);
                // 防御：OnOpen 可能因 BeforeOpen 被子类覆盖而延迟激活，此处兜底
                if (!uiBase.gameObject.activeSelf)
                {
                    Log.Warning("UI {0} OnOpen 后仍未激活，强制激活", uiBase.gameObject.name);
                    uiBase.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 打开窗口
        /// </summary>
        /// <param name="uiState"></param>
        /// <param name="args"></param>
        private void OnOpen(UILoadState uiState, params object[] args)
        {
            // LoadWindow 为同步加载，不存在“加载中”状态，无需 isLoading 分支
            UIController uiBase = uiState.uiWindow;

            // fake-null 防护：uiWindow 被外部销毁（极端并发/误删）时不 NRE 中断打开流程
            if (uiBase == null)
            {
                Log.Warning("[UISystem] OnOpen({0}) uiWindow 已不存在（外部销毁），移除残留缓存项", uiState.uiName);
                uiStateCache.Remove(uiState.uiName);
                return;
            }

            Log.Debug("[UISystem] OnOpen({0}) activeSelf before={1}", uiBase.gameObject.name, uiBase.gameObject.activeSelf);

            if (uiBase.gameObject.activeSelf)
            {
                // 窗口已激活：重复打开不应先广播"关闭"事件（语义矛盾，
                // 会导致 UIInputSystem.OnUIClosed 误触发 RestorePreviousFocus 弹出焦点）。
                // 直接走 OnOpen 刷新数据即可。
                uiBase.BeforeOpen(() =>
                {
                    LogElapsedTime(() =>
                    {
                        uiBase.OnOpen();
                    }, $"OnOpen UI {uiBase.gameObject.name}");

                    // 订阅者异常隔离：坏订阅者不应中断重复打开流程
                    try
                    {
                        OnOpenSubject.OnNext(uiBase);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("OnOpenSubject 订阅者异常（已隔离）: {0}", ex.Message);
                    }
                });
                return;
            }

            // 在 BeforeOpen 之前激活，确保 UI 可见（不论子类 BeforeOpen 行为如何）
            uiBase.gameObject.SetActive(true);
            Log.Debug("[UISystem] OnOpen({0}) SetActive(true) → activeSelf={1}", uiBase.gameObject.name, uiBase.gameObject.activeSelf);

            uiBase.BeforeOpen(() =>
            {
                LogElapsedTime(() =>
                {
                    uiBase.OnOpen();
                }, $"OnOpen UI {uiBase.gameObject.name}");

                // 订阅者异常隔离：坏订阅者不应中断开窗流程
                try
                {
                    OnOpenSubject.OnNext(uiBase);
                }
                catch (Exception ex)
                {
                    Log.Error("OnOpenSubject 订阅者异常（已隔离）: {0}", ex.Message);
                }
            });
        }

        #endregion
    }
}
