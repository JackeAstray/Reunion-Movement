using Gear.Common;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Gear.Framework.Runtime.Resources
{
    /// <summary>
    /// <see cref="AddressableSystem"/> 的场景加载/卸载部分（按职责从主文件拆出，避免单文件继续膨胀）。
    /// 句柄语义：加载返回的 AsyncOperationHandle 由调用方保存；卸载/释放优先用句柄重载，
    /// 这样即使 Scene 已被其他 Single 加载销毁，也能正确归还引用计数。
    /// </summary>
    public partial class AddressableSystem
    {
        /// <summary>
        /// 异步加载场景（Addressable 场景）。返回的 SceneInstance 通过 <see cref="UnloadSceneAsync(SceneInstance, IProgress{float}, CancellationToken)"/> 卸载。
        /// <para>
        /// 注意：只拿到 <see cref="SceneInstance"/> 时，如果场景在卸载前被别的 Single 加载销毁，
        /// 就无法再通过 SceneInstance 卸载（卸载 API 会因 Scene 失效而失败），句柄引用计数会泄漏。
        /// 需要可靠释放的场景请改用 <see cref="LoadSceneHandleAsync"/> 并保存句柄。
        /// </para>
        /// </summary>
        public async UniTask<SceneInstance> LoadSceneAsync(object key, LoadSceneMode mode = LoadSceneMode.Single, IProgress<float> progress = null, CancellationToken ct = default)
        {
            var handle = await LoadSceneHandleAsync(key, mode, progress, ct);
            return handle.IsValid() ? handle.Result : default;
        }

        /// <summary>
        /// 异步加载场景并返回底层句柄（推荐）：调用方保存句柄，用
        /// <see cref="UnloadSceneAsync(AsyncOperationHandle{SceneInstance}, IProgress{float}, CancellationToken)"/>
        /// 或 <see cref="ReleaseSceneHandle"/> 释放，即使 Scene 已失效也能正确归还引用计数。
        /// 失败/取消时返回 default（无效句柄）。
        /// </summary>
        public async UniTask<AsyncOperationHandle<SceneInstance>> LoadSceneHandleAsync(object key,
            LoadSceneMode mode = LoadSceneMode.Single, IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (Mode == AddressablesMode.Off)
            {
                Log.Warning("AddressableSystem 处于 Off 模式，无法加载场景: {0}", key);
                return default;
            }
            if (!isInited)
            {
                Log.Warning("AddressableSystem 未初始化，自动执行 Init()");
                await Init();
                if (!isInited) return default;
            }

            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                handle = Addressables.LoadSceneAsync(key, mode);
                await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Log.Error("Addressables 场景加载失败: {0}, Status: {1}", key, handle.Status);
                    if (handle.IsValid()) Addressables.Release(handle);
                    return default;
                }
                Interlocked.Increment(ref loadCount);
                Interlocked.Increment(ref activeCount);
                return handle;
            }
            catch (OperationCanceledException)
            {
                // 取消路径：autoReleaseWhenCanceled 已自动释放句柄
                return default;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 场景加载异常: {0}, {1}", key, ex.Message);
                // 异常路径也必须释放句柄
                if (handle.IsValid()) Addressables.Release(handle);
                return default;
            }
        }

        /// <summary>卸载 Addressable 场景（与 <see cref="LoadSceneAsync"/> 成对；Scene 已失效时会失败，建议改用句柄重载）</summary>
        public async UniTask UnloadSceneAsync(SceneInstance sceneInstance, IProgress<float> progress = null, CancellationToken ct = default)
        {
            try
            {
                var handle = Addressables.UnloadSceneAsync(sceneInstance);
                await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                Interlocked.Increment(ref releaseCount);
                Interlocked.Decrement(ref activeCount);
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 场景卸载异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 卸载 Addressable 场景（句柄重载，推荐）：
        /// 即使场景已被其他 Single 加载销毁，也会在卸载失败时兜底 Release 句柄，避免引用计数泄漏。
        /// </summary>
        public async UniTask UnloadSceneAsync(AsyncOperationHandle<SceneInstance> handle,
            IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (!handle.IsValid()) return;

            try
            {
                var unload = Addressables.UnloadSceneAsync(handle, autoReleaseHandle: true);
                await unload.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                Interlocked.Increment(ref releaseCount);
                Interlocked.Decrement(ref activeCount);
            }
            catch (Exception ex)
            {
                // 场景已失效 / 取消：至少要归还句柄引用，否则 bundle 永久驻留
                Log.Error("Addressables 场景卸载异常（已兜底释放句柄）: {0}", ex.Message);
                ReleaseSceneHandle(handle);
            }
        }

        /// <summary>
        /// 同步释放场景句柄（引擎清理/退出时用）：只 Release 引用计数，不执行卸载流程。
        /// 场景对象本身随引擎或下一次 Single 加载销毁。
        /// </summary>
        public void ReleaseSceneHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            if (!handle.IsValid()) return;
            try
            {
                Addressables.Release(handle);
                Interlocked.Increment(ref releaseCount);
                Interlocked.Decrement(ref activeCount);
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 场景句柄释放异常: {0}", ex.Message);
            }
        }
    }
}
