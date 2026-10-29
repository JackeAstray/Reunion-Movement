using System;
using System.Collections.Generic;
using System.Reflection;
using Astray.Common;

namespace Astray.Framework.Runtime.UI
{
    /// <summary>
    /// 一个窗口的运行时描述（由 <see cref="UIWindowAttribute"/> 生成，或按老规则回退推导）。
    /// </summary>
    public readonly struct UIWindowDescriptor
    {
        /// <summary>窗口标识（= 资源名）。</summary>
        public readonly string Id;

        /// <summary>窗口控制器类型。</summary>
        public readonly Type ControllerType;

        /// <summary>层级。</summary>
        public readonly PanelType Layer;

        /// <summary>是否模态。</summary>
        public readonly bool Modal;

        /// <summary>是否允许池化。</summary>
        public readonly bool Pooled;

        /// <summary>是否来自 <see cref="UIWindowAttribute"/>（false 表示按老规则回退推导）。</summary>
        public readonly bool Declared;

        public UIWindowDescriptor(string id, Type controllerType, PanelType layer, bool modal, bool pooled, bool declared)
        {
            Id = id;
            ControllerType = controllerType;
            Layer = layer;
            Modal = modal;
            Pooled = pooled;
            Declared = declared;
        }

        public override string ToString()
        {
            return $"{Id}（{ControllerType?.Name}, {Layer}, modal={Modal}, pooled={Pooled}, declared={Declared}）";
        }
    }

    /// <summary>
    /// 窗口目录 —— 启动时扫一遍所有 <see cref="UIController"/> 派生类，把
    /// <see cref="UIWindowAttribute"/> 声明的元数据收进两张表（按 Id / 按类型）。
    ///
    /// <para>
    /// 为什么要它：<see cref="UISystem"/> 原来只有"窗口名 → 控制器类型"这一半映射（源码生成器
    /// <c>UIControllerRegistry</c>），另一半（层级 / 模态 / 池化 / 是否是声明过的窗口）是无处可查的。
    /// 这里补齐，且**只扫一次**并缓存，后续查询零反射。
    /// </para>
    ///
    /// <para>
    /// 兼容老窗口：没有 <see cref="UIWindowAttribute"/> 的类型也会登记（<c>Declared = false</c>，
    /// Id 取类名、层级 <see cref="PanelType.NormalUI"/>、不模态、不池化），行为与改造前一致。
    /// </para>
    /// </summary>
    public static class UIWindowCatalog
    {
        private static readonly Dictionary<string, UIWindowDescriptor> byId =
            new Dictionary<string, UIWindowDescriptor>(StringComparer.Ordinal);

        private static readonly Dictionary<Type, UIWindowDescriptor> byType =
            new Dictionary<Type, UIWindowDescriptor>();

        private static bool built;

        /// <summary>目录里登记的窗口数量。</summary>
        public static int Count
        {
            get { EnsureBuilt(); return byId.Count; }
        }

        /// <summary>所有已登记的窗口（用于编辑器检查/调试输出）。</summary>
        public static IReadOnlyCollection<UIWindowDescriptor> All
        {
            get
            {
                EnsureBuilt();
                return byId.Values;
            }
        }

        /// <summary>按 Id 查描述符；未登记时返回 <c>false</c>。</summary>
        public static bool TryGet(string id, out UIWindowDescriptor descriptor)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(id))
            {
                descriptor = default;
                return false;
            }
            return byId.TryGetValue(id, out descriptor);
        }

        /// <summary>按 Id 查描述符；未登记时回退成"老规则"描述符（不抛异常，保证旧调用路径可用）。</summary>
        public static UIWindowDescriptor Get(string id)
        {
            return TryGet(id, out var descriptor)
                ? descriptor
                : new UIWindowDescriptor(id, null, PanelType.NormalUI, false, false, false);
        }

        /// <summary>按控制器类型查描述符。</summary>
        public static bool TryGet(Type controllerType, out UIWindowDescriptor descriptor)
        {
            EnsureBuilt();
            if (controllerType == null)
            {
                descriptor = default;
                return false;
            }
            return byType.TryGetValue(controllerType, out descriptor);
        }

        /// <summary>按控制器类型查描述符。</summary>
        public static bool TryGet<T>(out UIWindowDescriptor descriptor) where T : UIController
        {
            return TryGet(typeof(T), out descriptor);
        }

        /// <summary>
        /// 取类型对应的窗口 Id：优先特性声明，其次类型名（与改造前 `typeof(T).Name` 一致）。
        /// </summary>
        public static string GetId(Type controllerType)
        {
            if (controllerType == null) return string.Empty;
            return TryGet(controllerType, out var descriptor) ? descriptor.Id : controllerType.Name;
        }

        /// <summary>取类型对应的窗口 Id（泛型重载）。</summary>
        public static string GetId<T>() where T : UIController
        {
            return GetId(typeof(T));
        }

        /// <summary>
        /// 预热：在 <see cref="UISystem"/> 初始化时调用一次，避免首次打开窗口时才扫描（去掉首帧毛刺）。
        /// </summary>
        public static void Warmup()
        {
            EnsureBuilt();
        }

        /// <summary>清空缓存（域重载 / 测试用）。</summary>
        public static void Reset()
        {
            byId.Clear();
            byType.Clear();
            built = false;
        }

        /// <summary>
        /// 扫描所有已加载程序集里的 <see cref="UIController"/> 派生类并登记。
        /// 只读取类型与特性，不实例化任何对象。
        /// </summary>
        private static void EnsureBuilt()
        {
            if (built) return;
            built = true;

            Assembly[] assemblies;
            try
            {
                assemblies = AppDomain.CurrentDomain.GetAssemblies();
            }
            catch (Exception ex)
            {
                Log.Warning("[UIWindowCatalog] 枚举程序集失败: {0}", ex.Message);
                return;
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (Exception)
                {
                    // 个别程序集（动态/反射生成）取类型会抛，跳过即可
                    continue;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    Register(types[t]);
                }
            }

            Log.Debug("[UIWindowCatalog] 已登记 {0} 个窗口（其中 {1} 个显式声明）",
                byId.Count, CountDeclared());
        }

        private static int CountDeclared()
        {
            int n = 0;
            foreach (var kv in byId)
            {
                if (kv.Value.Declared) n++;
            }
            return n;
        }

        private static void Register(Type type)
        {
            if (type == null || type.IsAbstract || type.IsGenericTypeDefinition) return;
            if (!typeof(UIController).IsAssignableFrom(type)) return;
            if (byType.ContainsKey(type)) return;

            var attribute = type.GetCustomAttribute<UIWindowAttribute>(inherit: false);
            bool declared = attribute != null;

            string id = declared && !string.IsNullOrEmpty(attribute.Id) ? attribute.Id : type.Name;

            var descriptor = new UIWindowDescriptor(
                id,
                type,
                declared ? attribute.Layer : PanelType.NormalUI,
                declared && attribute.Modal,
                declared && attribute.Pooled,
                declared);

            byType[type] = descriptor;

            if (byId.TryGetValue(id, out var existing))
            {
                // 同名窗口：显式声明的优先，其次保留先登记的（与"资源名唯一"的约定一致）
                if (existing.Declared || !declared) return;
            }
            byId[id] = descriptor;
        }
    }
}
