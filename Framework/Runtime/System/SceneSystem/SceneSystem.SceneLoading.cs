using Gear.Common;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.Resources;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Gear.Framework.Runtime.Scene
{
    /// <summary>
    /// <see cref="SceneSystem"/> 的场景加载执行部分（按职责从主文件拆出）：
    /// Addressables 双轨加载、旧场景句柄释放、过渡场景超时保护。
    /// </summary>
    public partial class SceneSystem
    {
        /// <summary>
        /// 检查 Addressables key 是否已注册（Addressables 2.9.1 无 Addressables.Exists API，
        /// 通过遍历 ResourceLocators 判断）。须在 Addressables 初始化后调用。
        /// </summary>
        private static bool AddressableKeyExists(object key, Type type)
        {
            try
            {
                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator.Locate(key, type, out var locations))
                    {
                        return locations != null && locations.Count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Addressables 检查 key 失败 {0}: {1}", key, ex.Message);
            }
            return false;
        }

        /// <summary>
        /// 通过 Addressables 加载目标场景（地址 Remote/Scenes/{name}），成功返回 true。
        /// 切换前先卸载旧 Addressable 场景（释放 Bundle）；进度映射为 0.15~1.0，与 SceneManager 流程对齐。
        /// </summary>
        private async UniTask<bool> LoadTargetSceneViaAddressablesAsync(string levelName, LoadSceneMode loadSceneMode, CancellationToken ct = default)
        {
            try
            {
                // 进度映射：Addressables 0~1 → SceneSystem 0.15~1.0（与 SceneManager 流程起始回调点对齐）
                var progress = new Progress<float>(p => CallbackProgress(0.15f + 0.85f * Mathf.Clamp01(p)));
                var addressable = Modules.Get<AddressableSystem>();
                var handle = await addressable.LoadSceneHandleAsync(
                    AddressableKeys.SceneRoot + levelName, loadSceneMode, progress, ct);
                if (!handle.IsValid() || !handle.Result.Scene.IsValid())
                {
                    if (handle.IsValid()) addressable.ReleaseSceneHandle(handle);
                    return false;
                }

                loadedSceneHandle = handle;
                loadedSceneInstance = handle.Result;
                CallbackProgress(1f);
                if (!openLoad)
                {
                    ExecuteBslcc();
                }
                OnTargetSceneLoaded();
                ClearExcludeSet();
                ExecuteSlcc();
                Log.Debug("目标场景（Addressables）加载完成: {0}", levelName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 场景加载异常 {0}: {1}", levelName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 释放当前由 Addressables 加载的场景（如果有）。句柄有效性独立于 Scene 有效性，
        /// 所以即使场景已被其他 Single 加载销毁也能归还引用计数。
        /// </summary>
        private async UniTask ReleaseLoadedAddressableSceneAsync()
        {
            var handle = loadedSceneHandle;
            loadedSceneHandle = default;
            loadedSceneInstance = default;
            if (!handle.IsValid()) return;

            await Modules.Get<AddressableSystem>().UnloadSceneAsync(handle);
        }

        /// <summary>
        /// 加载过渡场景。返回 false 表示过渡场景加载失败/超时，调用方必须中止本次切换，
        /// 不能继续加载目标场景（否则残留的 Single 加载操作激活后会摧毁目标场景）。
        /// </summary>
        private async UniTask<bool> OnLoadingSceneAsync(string loadSceneName, LoadSceneMode loadSceneMode)
        {
            var async = SceneManager.LoadSceneAsync(loadSceneName, loadSceneMode);
            if (async == null)
            {
                Log.Warning("过渡场景 {0} 加载失败，跳过过渡场景", loadSceneName);
                // 即便失败也要触发回调，避免回调泄漏
                ExecuteBslcc();
                CallbackProgress(0);
                return false;
            }

            // 过渡场景超时保护：加载卡死时不再无限等待
            float timeoutDeadline = sceneLoadTimeoutSeconds > 0
                ? Time.realtimeSinceStartup + sceneLoadTimeoutSeconds
                : float.PositiveInfinity;

            while (!async.isDone)
            {
                if (Time.realtimeSinceStartup > timeoutDeadline)
                {
                    Log.Error("过渡场景 {0} 加载超时（>{1}s），中止本次场景切换", loadSceneName, sceneLoadTimeoutSeconds);
                    // 关键：不能让该 AsyncOperation 继续残留。否则它稍后完成时会以 Single 模式激活，
                    // 把随后加载好的目标场景卸载掉。能控制激活的平台立刻放行；WebGL 不支持控制，
                    // 但仍中止后续目标场景加载，避免"目标场景被过渡场景顶掉"。
                    try
                    {
                        if (Application.platform != RuntimePlatform.WebGLPlayer)
                        {
                            async.allowSceneActivation = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("过渡场景超时后恢复激活失败: {0}", ex.Message);
                    }
                    ExecuteBslcc();
                    CallbackProgress(0);
                    return false;
                }
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            Log.Debug("Loading场景加载完成！");
            ExecuteBslcc();
            CallbackProgress(0);
            return true;
        }
    }
}
