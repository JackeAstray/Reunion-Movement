using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace Gear.Framework.Runtime
{
    /// <summary>
    /// 引擎级 Profiler 埋点。
    ///
    /// <para>
    /// 没有这些标记时，Profiler 时间轴上 <c>GameEngineDriver.Update</c> 是一整块黑盒，
    /// 里面混着全部模块的 Update、R3 订阅者广播与周期事件 —— 出现卡顿时
    /// "这几毫秒是谁吃的"只能靠猜。有了逐模块 marker，直接在时间轴上就能看出归属。
    /// </para>
    ///
    /// <para>
    /// 所有采样都包在 <c>ENABLE_PROFILER</c> 里：发布包（非 Development Build）中
    /// <see cref="Scope"/> 退化为空结构体，<see cref="Dispose"/> 是空方法，
    /// JIT 会把整个 begin/end 消掉，调用点零开销。编辑器与 Development Build 自动启用。
    /// </para>
    ///
    /// <para>
    /// 逐模块 marker 按运行时类型缓存，避免每帧拼接字符串与分配 marker 名字。
    /// </para>
    /// </summary>
    internal static class EngineProfiler
    {
        /// <summary>每次 Update 的模块驱动循环（不含前面的 R3 广播）。</summary>
        public static readonly ProfilerMarker UpdateLoop = new ProfilerMarker("Gear.GameEngine.Update");

        /// <summary>固定步长模块驱动循环。</summary>
        public static readonly ProfilerMarker FixedUpdateLoop = new ProfilerMarker("Gear.GameEngine.FixedUpdate");

        /// <summary>延迟更新模块驱动循环。</summary>
        public static readonly ProfilerMarker LateUpdateLoop = new ProfilerMarker("Gear.GameEngine.LateUpdate");

        /// <summary>R3 订阅者广播（Update / 300ms / 1s 三路）。</summary>
        public static readonly ProfilerMarker SubjectDispatch = new ProfilerMarker("Gear.GameEngine.Subject");

#if ENABLE_PROFILER
        private static readonly Dictionary<Type, ProfilerMarker> ModuleMarkers =
            new Dictionary<Type, ProfilerMarker>(32);
#endif

        /// <summary>给单个模块的 Update/FixedUpdate/LateUpdate 打点（按类型名，首次访问时创建）。</summary>
        public static Scope Module(object module)
        {
#if ENABLE_PROFILER
            Type type = module.GetType();
            if (!ModuleMarkers.TryGetValue(type, out ProfilerMarker marker))
            {
                marker = new ProfilerMarker("Gear.Module." + type.Name);
                ModuleMarkers.Add(type, marker);
            }
            return new Scope(marker.Auto());
#else
            return default;
#endif
        }

        /// <summary>给一个固定区段打点。</summary>
        public static Scope Section(ProfilerMarker marker)
        {
#if ENABLE_PROFILER
            return new Scope(marker.Auto());
#else
            return default;
#endif
        }

        /// <summary>
        /// 采样作用域：<c>using (EngineProfiler.Module(module)) { ... }</c>。
        /// 未启用 Profiler 时是空结构体，编译器可完全内联掉。
        /// </summary>
        public struct Scope : IDisposable
        {
#if ENABLE_PROFILER
            private ProfilerMarker.AutoScope scope;

            public Scope(ProfilerMarker.AutoScope scope)
            {
                this.scope = scope;
            }

            public void Dispose()
            {
                scope.Dispose();
            }
#else
            public void Dispose() { }
#endif
        }
    }
}
