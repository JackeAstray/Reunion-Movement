using ReunionMovement.Core.Base;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 模块目录 —— 把"注册顺序即初始化顺序"升级为"声明依赖 + 拓扑排序 + 启动前校验"。
    ///
    /// <para>
    /// 规则：
    /// 1. 声明了 <see cref="ModuleOrderAttribute"/> 的模块按其 <c>Order</c> 排序；
    ///    未声明的按注册顺序参与排序（视为 Order = 0）。
    /// 2. <c>DependsOn</c> 声明的模块必须也在本次注册列表中，且排在被依赖方之后。
    /// 3. 排序是<b>稳定</b>的：Order 相同的模块保持注册顺序，因此给既有工程补声明不会打乱顺序。
    /// 4. 依赖匹配用 <c>Type.IsInstanceOfType</c>：因此 <c>DependsOn</c> 可以写接口或基类；
    ///    若多个已注册模块都满足同一个接口，取<b>注册顺序里的第一个</b>。
    /// 5. 重复注册按两种方式判：同一实例、以及同一类型的两个不同实例（后者会导致重复 Init/Update）。
    /// </para>
    ///
    /// <para>
    /// 失败策略：校验不通过时<b>不</b>中断启动 —— 返回 false 并给出完整原因，
    /// 由调用方沿用注册顺序继续（保证不比改造前更差），同时把问题暴露在启动日志里。
    /// </para>
    /// </summary>
    public static class ModuleCatalog
    {
        /// <summary>
        /// 校验并排序模块列表。
        /// </summary>
        /// <param name="declared">注册顺序的模块列表</param>
        /// <param name="ordered">排序结果；校验失败时为原始列表</param>
        /// <param name="errors">失败原因（多条以分号分隔）；成功时为空字符串</param>
        /// <returns>校验与排序是否成功</returns>
        public static bool TryBuild(IList<ICustomSystem> declared, out IList<ICustomSystem> ordered, out string errors)
        {
            ordered = declared;
            errors = string.Empty;

            if (declared == null || declared.Count == 0) return true;

            var nodes = new List<Node>(declared.Count);
            var problems = new List<string>();

            // ---- 1. 建立节点，收集空项与重复注册 ----
            for (int i = 0; i < declared.Count; i++)
            {
                var module = declared[i];
                if (module == null)
                {
                    problems.Add($"第 {i} 项为 null");
                    continue;
                }

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (ReferenceEquals(nodes[j].Module, module))
                    {
                        problems.Add($"{module.GetType().Name} 被重复注册（第 {i} 项与第 {nodes[j].Index} 项是同一实例）");
                        break;
                    }
                }

                // 同一类型的两个不同实例：单例写法下不该出现，但构造函数是 public 的模块
                // （如 TimerMgr）可以 new 出来。只按实例判重会漏掉这种情况，
                // 结果是同一个类型被 Init/Update 两次。
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (!ReferenceEquals(nodes[j].Module, module) && nodes[j].Module.GetType() == module.GetType())
                    {
                        problems.Add($"{module.GetType().Name} 被注册了两次（第 {i} 项与第 {nodes[j].Index} 项是同一类型的两个实例），会重复 Init/Update");
                        break;
                    }
                }

                var meta = ReadMetadata(module.GetType());
                nodes.Add(new Node
                {
                    Module = module,
                    Order = meta.Order,
                    DependsOn = meta.DependsOn,
                    Index = i,
                });
            }

            // ---- 2. 解析依赖边：依赖必须存在，且不能指向自己 ----
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.DependsOn == null) continue;

                for (int d = 0; d < node.DependsOn.Length; d++)
                {
                    Type required = node.DependsOn[d];
                    if (required == null) continue;

                    if (required.IsInstanceOfType(node.Module))
                    {
                        problems.Add($"{node.Module.GetType().Name} 依赖了自己（{required.Name}）");
                        continue;
                    }

                    int providerIndex = IndexOfProvider(nodes, required);
                    if (providerIndex < 0)
                    {
                        problems.Add($"{node.Module.GetType().Name} 依赖 {required.Name}，但该模块未注册");
                        continue;
                    }

                    node.Dependencies.Add(providerIndex);
                    nodes[providerIndex].Dependents.Add(i);
                }
            }

            if (problems.Count > 0)
            {
                errors = string.Join("；", problems);
                return false;
            }

            // ---- 3. 稳定拓扑排序：每次取 Order 最小、其次注册序号最小的就绪节点 ----
            var result = new List<ICustomSystem>(nodes.Count);
            var placed = new bool[nodes.Count];
            var remainingInDegree = new int[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) remainingInDegree[i] = nodes[i].Dependencies.Count;

            while (result.Count < nodes.Count)
            {
                int pick = -1;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (placed[i] || remainingInDegree[i] > 0) continue;
                    if (pick < 0 || IsBefore(nodes[i], nodes[pick])) pick = i;
                }

                if (pick < 0)
                {
                    // 环：把剩余节点按依赖关系列出来，方便定位
                    var cycle = new List<string>();
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (!placed[i]) cycle.Add(nodes[i].Module.GetType().Name);
                    }
                    errors = "模块依赖存在环：" + string.Join(" → ", cycle);
                    return false;
                }

                placed[pick] = true;
                result.Add(nodes[pick].Module);

                var dependents = nodes[pick].Dependents;
                for (int i = 0; i < dependents.Count; i++) remainingInDegree[dependents[i]]--;
            }

            ordered = result;
            return true;
        }

        /// <summary>
        /// 按各模块 <see cref="ModuleOrderAttribute"/> 声明的依赖，把"被依赖但没有注册"的模块补进列表（幂等）。
        ///
        /// <para>
        /// 用途：接入方"只留我需要的模块"时不必再背依赖表 —— 删掉一个被依赖的模块，
        /// 依赖它的一方会把提供者自动补回来；补入后仍由 <see cref="TryBuild"/> 做拓扑排序与校验。
        /// </para>
        /// <para>
        /// 补入方式：优先取模块类型上的静态入口 <c>Instance</c>（其次 <c>Current</c>），
        /// 没有单例入口时退回无参构造。两种都失败则记入 <paramref name="unresolved"/>，
        /// 行为退回改造前（告警 + 按注册顺序启动），不会因为展开失败而中断启动。
        /// </para>
        /// </summary>
        /// <param name="declared">注册顺序的模块列表（不会被修改）</param>
        /// <param name="added">本次补入的模块说明（形如 <c>AddressableSystem（UISystem 依赖 AddressableSystem）</c>）</param>
        /// <param name="unresolved">补入失败的依赖说明</param>
        /// <returns>补全后的模块列表；入参为空时原样返回</returns>
        public static IList<ICustomSystem> ExpandDependencies(IList<ICustomSystem> declared,
            out IList<string> added, out IList<string> unresolved)
        {
            var addedList = new List<string>();
            var unresolvedList = new List<string>();
            added = addedList;
            unresolved = unresolvedList;

            if (declared == null || declared.Count == 0) return declared;

            var working = new List<ICustomSystem>(declared);
            var reportedUnresolved = new HashSet<Type>();

            // 最多做清单长度 +1 轮：每轮可能补入新模块，而新模块自己也可能带依赖。
            int maxPasses = working.Count + 1;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool changed = false;

                for (int i = 0; i < working.Count; i++)
                {
                    var module = working[i];
                    if (module == null) continue;

                    var meta = ReadMetadata(module.GetType());
                    if (meta.DependsOn == null || meta.DependsOn.Length == 0) continue;

                    for (int d = 0; d < meta.DependsOn.Length; d++)
                    {
                        Type required = meta.DependsOn[d];
                        if (required == null) continue;
                        if (IsSatisfied(working, required)) continue;

                        if (TryCreateProvider(required, out var provider))
                        {
                            working.Add(provider);
                            addedList.Add($"{provider.GetType().Name}（{module.GetType().Name} 依赖 {required.Name}）");
                            changed = true;
                        }
                        else if (reportedUnresolved.Add(required))
                        {
                            unresolvedList.Add($"{module.GetType().Name} 依赖 {required.Name}，未注册且无法自动创建");
                        }
                    }
                }

                if (!changed) break;
            }

            return working;
        }

        /// <summary>列表里是否已有模块能满足该依赖类型（支持接口 / 基类声明）。</summary>
        private static bool IsSatisfied(IList<ICustomSystem> modules, Type required)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module != null && required.IsInstanceOfType(module)) return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试为依赖类型造一个实例：静态单例入口优先，其次无参构造。
        /// 真实模块多是 <c>SingletonMgr&lt;T&gt;</c>（MonoBehaviour），只能通过 <c>Instance</c> 拿到。
        /// </summary>
        private static bool TryCreateProvider(Type required, out ICustomSystem provider)
        {
            provider = null;
            if (required == null) return false;
            if (!typeof(ICustomSystem).IsAssignableFrom(required)) return false;
            if (required.IsAbstract || required.IsInterface) return false;

            var singleton = TryGetStaticEntry(required);
            if (singleton is ICustomSystem fromEntry && required.IsInstanceOfType(fromEntry))
            {
                provider = fromEntry;
                return true;
            }

            try
            {
                var ctor = required.GetConstructor(Type.EmptyTypes);
                if (ctor != null && ctor.Invoke(null) is ICustomSystem created && required.IsInstanceOfType(created))
                {
                    provider = created;
                    return true;
                }
            }
            catch
            {
                // 构造抛异常（缺少依赖 / 需要 Unity 运行时）：交给调用方按"未解析"告警
            }

            return false;
        }

        /// <summary>读取类型上的静态入口属性/字段（<c>Instance</c>、<c>Current</c>），取不到返回 null。</summary>
        private static object TryGetStaticEntry(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            string[] names = { "Instance", "Current" };

            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var property = type.GetProperty(names[i], flags);
                    if (property != null && property.CanRead)
                    {
                        var value = property.GetValue(null);
                        if (value != null) return value;
                    }

                    var field = type.GetField(names[i], flags);
                    if (field != null)
                    {
                        var value = field.GetValue(null);
                        if (value != null) return value;
                    }
                }
                catch
                {
                    // 静态入口内可能依赖 Unity 运行时；取不到就当没有
                }
            }

            return null;
        }

        /// <summary>
        /// 输出排序结果（形如 <c>A(10) → B(20) → C(30)</c>），便于启动日志核对。
        /// </summary>
        public static string Describe(IList<ICustomSystem> ordered)
        {
            if (ordered == null || ordered.Count == 0) return "(空)";

            var sb = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i] == null) continue;
                if (sb.Length > 0) sb.Append(" → ");
                var meta = ReadMetadata(ordered[i].GetType());
                sb.Append(ordered[i].GetType().Name).Append('(').Append(meta.Order).Append(')');
            }
            return sb.ToString();
        }

        private static bool IsBefore(Node a, Node b)
        {
            if (a.Order != b.Order) return a.Order < b.Order;
            return a.Index < b.Index;
        }

        private static int IndexOfProvider(List<Node> nodes, Type required)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (required.IsInstanceOfType(nodes[i].Module)) return i;
            }
            return -1;
        }

        /// <summary>读取模块上的 <see cref="ModuleOrderAttribute"/>；没有声明时返回 0 / 空依赖。</summary>
        private static ModuleMetadata ReadMetadata(Type moduleType)
        {
            var attribute = moduleType.GetCustomAttribute<ModuleOrderAttribute>(inherit: false);
            return attribute == null
                ? new ModuleMetadata(0, null)
                : new ModuleMetadata(attribute.Order, attribute.DependsOn);
        }

        private readonly struct ModuleMetadata
        {
            public readonly int Order;
            public readonly Type[] DependsOn;

            public ModuleMetadata(int order, Type[] dependsOn)
            {
                Order = order;
                DependsOn = dependsOn;
            }
        }

        private sealed class Node
        {
            public ICustomSystem Module;
            public int Order;
            public Type[] DependsOn;
            public int Index;
            public readonly List<int> Dependencies = new List<int>();
            public readonly List<int> Dependents = new List<int>();
        }
    }
}
