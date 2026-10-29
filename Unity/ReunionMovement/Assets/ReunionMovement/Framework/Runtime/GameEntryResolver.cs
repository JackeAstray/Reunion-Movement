using System;
using System.Reflection;
using ReunionMovement.Common;
using ReunionMovement.Framework.Runtime.Base;

namespace ReunionMovement.Framework.Runtime
{
    /// <summary>
    /// 解析 <see cref="ReunionGameEntryAttribute"/> 声明的游戏入口。
    /// <para>
    /// 扫描已加载程序集一次并缓存结果；<see cref="Bootstrap"/> 在每次进入 Play Mode 时
    /// 通过 <see cref="Refresh"/> 清缓存，避免关闭 Domain Reload 时复用上一会话的状态。
    /// </para>
    /// </summary>
    public static class GameEntryResolver
    {
        /// <summary>框架自身程序集前缀：同名的框架声明优先级低于用户程序集声明。</summary>
        private const string FrameworkAssemblyPrefix = "ReunionMovement.";

        /// <summary>启动早期没有加载价值的程序集，跳过以省掉反射开销。</summary>
        private static readonly string[] SkipPrefixes =
        {
            "Unity", "UnityEngine", "UnityEditor", "System", "Microsoft", "mscorlib", "netstandard",
            "nunit", "Mono.", "Bee", "PlayerBuild", "Anonymously", "Newtonsoft", "Cysharp",
            "R3", "DOTween", "DG.", "kcp2k", "Telepathy", "Sirenix", "JetBrains",
        };

        private static Type cachedEntryType;
        private static bool resolved;

        /// <summary>清缓存（由 Bootstrap 在 SubsystemRegistration 阶段调用）。</summary>
        internal static void Refresh()
        {
            cachedEntryType = null;
            resolved = false;
        }

        /// <summary>查找声明的入口类型；未声明时返回 null。</summary>
        public static Type FindEntryType()
        {
            if (resolved) return cachedEntryType;
            resolved = true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Type frameworkDeclared = null;
            var frameworkAssembly = string.Empty;
            var userAssembly = string.Empty;

            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly asm = assemblies[i];
                string asmName;
                try
                {
                    asmName = asm.GetName().Name;
                }
                catch (Exception)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(asmName) || ShouldSkip(asmName)) continue;

                ReunionGameEntryAttribute declaration;
                try
                {
                    declaration = (ReunionGameEntryAttribute)Attribute.GetCustomAttribute(asm, typeof(ReunionGameEntryAttribute), false);
                }
                catch (Exception ex)
                {
                    Log.Warning("[GameEntryResolver] 读取程序集 {0} 的特性失败（已跳过）: {1}", asmName, ex.Message);
                    continue;
                }

                if (declaration?.EntryType == null) continue;

                bool isFrameworkAssembly = asmName.StartsWith(FrameworkAssemblyPrefix, StringComparison.Ordinal);

                if (!isFrameworkAssembly)
                {
                    if (cachedEntryType != null)
                    {
                        Log.Warning("[GameEntryResolver] 检测到多个入口声明，采用 {0}（来自 {1}），忽略 {2}（来自 {3}）",
                            cachedEntryType.FullName, userAssembly, declaration.EntryType.FullName, asmName);
                        continue;
                    }
                    cachedEntryType = declaration.EntryType;
                    userAssembly = asmName;
                }
                else if (frameworkDeclared == null)
                {
                    frameworkDeclared = declaration.EntryType;
                    frameworkAssembly = asmName;
                }
            }

            if (cachedEntryType == null && frameworkDeclared != null)
            {
                cachedEntryType = frameworkDeclared;
                userAssembly = frameworkAssembly;
            }

            if (cachedEntryType != null)
            {
                Log.Debug("[GameEntryResolver] 入口: {0}（来自 {1}）", cachedEntryType.FullName, userAssembly);
            }

            return cachedEntryType;
        }

        /// <summary>
        /// 创建入口实例；未声明或类型不合法时返回 null，由调用方回退到内置入口。
        /// </summary>
        public static GameEntry CreateEntry()
        {
            Type type = FindEntryType();
            if (type == null) return null;

            if (!typeof(GameEntry).IsAssignableFrom(type))
            {
                Log.Error("[GameEntryResolver] {0} 不是 GameEntry 的子类，已回退到内置入口", type.FullName);
                return null;
            }
            if (type.IsAbstract)
            {
                Log.Error("[GameEntryResolver] {0} 是抽象类，无法实例化，已回退到内置入口", type.FullName);
                return null;
            }
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Log.Error("[GameEntryResolver] {0} 缺少无参构造函数，已回退到内置入口", type.FullName);
                return null;
            }

            try
            {
                return (GameEntry)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Log.Error("[GameEntryResolver] 创建 {0} 失败（已回退到内置入口）: {1}", type.FullName, ex.Message);
                return null;
            }
        }

        private static bool ShouldSkip(string assemblyName)
        {
            for (int i = 0; i < SkipPrefixes.Length; i++)
            {
                if (assemblyName.StartsWith(SkipPrefixes[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
