using Gear.Common;
using Gear.Framework.Runtime.Base;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Gear.Framework.Runtime
{
    public sealed partial class GameEngine
    {
        #region 模块
        /// <summary>已注册的游戏模块（只读）</summary>
        public IList<ICustomSystem> Modules => modules;
        private IList<ICustomSystem> modules;

        /// <summary>
        /// 在已注册模块中查找第一个 <typeparamref name="T"/> 的实现（类型或接口），不存在时返回 null。
        /// 引擎层用它按接口取能力，避免直接引用具体模块类型。
        /// 走与 <see cref="TryGetModule"/> 相同的类型索引：精确类型 O(1)，接口退化为线性扫描。
        /// </summary>
        private T FindModule<T>() where T : class
        {
            if (modules == null) return null;
            return TryGetModule(typeof(T), out ICustomSystem module) ? module as T : null;
        }

        /// <summary>需要每帧 Update 的模块（预过滤，避免空调用）</summary>
        private readonly List<ISystemUpdatable> updatableModules = new List<ISystemUpdatable>();
        // 更新循环快照缓冲（复用，零分配）：模块 Update 内移除其他模块时列表收缩，
        // 直接倒序遍历会把同一模块同帧驱动两次
        private readonly List<ISystemUpdatable> updateSnapshot = new List<ISystemUpdatable>();

        /// <summary>
        /// 三个更新列表的"移除计数"。驱动循环在帧开始时记录一次，
        /// 只有计数变化过才需要逐模块做 Contains 复查（否则快照必然与列表一致）。
        /// 因此只有"移除"要自增：新增的模块当帧不在快照里，不影响一致性判断。
        /// </summary>
        private int updatableEpoch;
        private int fixedUpdateEpoch;
        private int lateUpdateEpoch;

        /// <summary>模块类型索引（快路径）：精确类型 → 模块实例，首次访问时按当前列表构建。</summary>
        private Dictionary<Type, ICustomSystem> moduleIndex;

        /// <summary>构建索引时的模块数量，用于检测列表被外部改动后重建。</summary>
        private int moduleIndexCount = -1;

        /// <summary>
        /// 按类型取模块：精确类型命中走字典（O(1)），接口/基类退化为线性扫描（保持
        /// "取第一个可赋值实现"的原语义）。列表可在运行期被 Add/RemoveModule 改动，
        /// 所以用数量做失效检测，外部直接改 <see cref="Modules"/> 列表也能自愈。
        /// </summary>
        internal bool TryGetModule(Type type, out ICustomSystem module)
        {
            module = null;
            IList<ICustomSystem> list = modules;
            if (list == null || list.Count == 0) return false;

            if (moduleIndex == null || moduleIndexCount != list.Count)
            {
                RebuildModuleIndex(list);
            }

            if (moduleIndex.TryGetValue(type, out module)) return true;

            // 接口或基类：保持原来的"第一个可赋值者"语义
            for (int i = 0; i < list.Count; i++)
            {
                ICustomSystem candidate = list[i];
                if (candidate != null && type.IsInstanceOfType(candidate))
                {
                    module = candidate;
                    return true;
                }
            }
            module = null;
            return false;
        }

        private void RebuildModuleIndex(IList<ICustomSystem> list)
        {
            if (moduleIndex == null) moduleIndex = new Dictionary<Type, ICustomSystem>(list.Count);
            else moduleIndex.Clear();

            for (int i = 0; i < list.Count; i++)
            {
                ICustomSystem candidate = list[i];
                if (candidate == null) continue;
                // 同一类型重复注册时保留先注册的那个（与原线性扫描"取第一个"语义一致）
                Type type = candidate.GetType();
                if (!moduleIndex.ContainsKey(type)) moduleIndex.Add(type, candidate);
            }
            moduleIndexCount = list.Count;
        }

        /// <summary>需要固定步长 FixedUpdate 的模块（预过滤，避免空调用）</summary>
        private readonly List<ISystemFixedUpdatable> fixedUpdateModules = new List<ISystemFixedUpdatable>();
        private readonly List<ISystemFixedUpdatable> fixedUpdateSnapshot = new List<ISystemFixedUpdatable>();

        /// <summary>需要 LateUpdate 的模块（预过滤，在 Update 之后、渲染之前驱动）</summary>
        private readonly List<ISystemLateUpdatable> lateUpdateModules = new List<ISystemLateUpdatable>();
        private readonly List<ISystemLateUpdatable> lateUpdateSnapshot = new List<ISystemLateUpdatable>();

        /// <summary>清理路径的可复用快照列表（避免每次 ClearModuleData 分配）</summary>
        private readonly List<ICustomSystem> clearSnapshot = new List<ICustomSystem>();

        /// <summary>启动取消链（外部 ct + 超时 + Dispose 取消），LaunchAsync 执行期间非 null</summary>
        private CancellationTokenSource launchCts;

        /// <summary>
        /// 运行时注册模块（主线程调用）。引擎已运行时先执行 Init 再纳入更新列表；
        /// 引擎未启动时仅加入列表（LaunchAsync 时统一 Init）。
        /// 注意：列表即 LaunchAsync 传入的列表，若外部重建列表则注册失效。
        /// </summary>
        /// <returns>是否注册成功（模块为 null/已存在/Init 失败返回 false）</returns>
        public async UniTask<bool> AddModuleAsync(ICustomSystem module)
        {
            if (module == null)
            {
                Log.Warning("[GameEngine] AddModuleAsync: module 为 null");
                return false;
            }
            if (modules == null)
            {
                Log.Warning("[GameEngine] AddModuleAsync: 模块列表未初始化（LaunchAsync 尚未调用）");
                return false;
            }
            if (modules.Contains(module))
            {
                Log.Debug("[GameEngine] 模块已注册，跳过: {0}", module.GetType().Name);
                return true;
            }

            // 引擎运行/暂停/启动中：模块需先完成自身初始化再进入更新列表。
            // Starting 阶段 InitModulesAsync 已结束，若在此阶段注册而不 Init，模块会
            // 以未初始化状态被每帧驱动（可能 NRE 或静默错乱）
            if (State == EngineState.Running || State == EngineState.Paused || State == EngineState.Starting)
            {
                try
                {
                    await module.Init();
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] 运行时模块 {0} Init 异常（未注册）: {1}", module.GetType().Name, ex.Message);
                    return false;
                }
            }

            modules.Add(module);
            if (module is ISystemUpdatable updatable) updatableModules.Add(updatable);
            if (module is ISystemFixedUpdatable fixedUpdatable) fixedUpdateModules.Add(fixedUpdatable);
            if (module is ISystemLateUpdatable lateUpdatable) lateUpdateModules.Add(lateUpdatable);
            // 让类型索引在下一次 Modules.Get 时重建（数量变化即失效）
            moduleIndex = null;
            moduleIndexCount = -1;
            Log.Debug("[GameEngine] 模块已注册: {0}", module.GetType().Name);
            return true;
        }

        /// <summary>
        /// 运行时移除模块（主线程调用）：从列表与更新列表摘除，并清理其数据（ISystemDisposable.Clear）。
        /// 模块可在自身 Update 内调用（更新循环的越界防护已覆盖列表收缩场景）。
        /// </summary>
        /// <returns>是否移除成功（模块为 null/未注册返回 false）</returns>
        public bool RemoveModule(ICustomSystem module)
        {
            if (module == null || modules == null) return false;
            if (!modules.Remove(module))
            {
                Log.Warning("[GameEngine] RemoveModule: 模块未注册: {0}", module.GetType().Name);
                return false;
            }

            if (module is ISystemUpdatable updatable)
            {
                if (updatableModules.Remove(updatable)) updatableEpoch++;
            }
            if (module is ISystemFixedUpdatable fixedUpdatable)
            {
                if (fixedUpdateModules.Remove(fixedUpdatable)) fixedUpdateEpoch++;
            }
            if (module is ISystemLateUpdatable lateUpdatable)
            {
                if (lateUpdateModules.Remove(lateUpdatable)) lateUpdateEpoch++;
            }

            // 让类型索引在下一次 Modules.Get 时重建
            moduleIndex = null;
            moduleIndexCount = -1;

            if (module is ISystemDisposable disposable)
            {
                try
                {
                    disposable.Clear();
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] 模块 {0} Clear 异常（已隔离）: {1}", module.GetType().Name, ex.Message);
                }
            }
            Log.Debug("[GameEngine] 模块已移除: {0}", module.GetType().Name);
            return true;
        }
        #endregion
    }
}
