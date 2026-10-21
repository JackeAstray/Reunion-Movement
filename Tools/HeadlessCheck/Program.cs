using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ReunionMovement.HeadlessCheck
{
    /// <summary>
    /// 无 Unity 的核心逻辑自检工具。
    /// <para>
    /// 它只验证"不依赖 Unity 运行时"的部分：模块排序与校验、Modules 访问器、入口解析、Bootstrap 配置判断。
    /// 需要 UnityEngine 的路径（Time / AudioListener / AssetDatabase）一律不碰，那是 Unity Test Runner 的事。
    /// </para>
    /// <para>
    /// 全部通过反射调用，编译期不引用任何 Unity 相关程序集，因此这个工具在任何装了 .NET SDK 的机器上都能编译。
    /// </para>
    /// </summary>
    public static class Program
    {
        private const string CoreAssemblyName = "ReunionMovement.Core";

        private static readonly List<string> SearchPaths = new List<string>();
        private static int passed;
        private static int skipped;
        private static int failed;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string repoRoot = FindRepoRoot();
            if (repoRoot == null)
            {
                Console.WriteLine("[FAIL] 找不到仓库根目录（缺少 Unity/ReunionMovement 目录）");
                return 2;
            }

            PrepareSearchPaths(repoRoot);
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            Assembly core;
            try
            {
                core = Assembly.LoadFrom(Path.Combine(repoRoot, "Unity/ReunionMovement/Temp/bin/Debug", CoreAssemblyName + ".dll"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] 无法加载 " + CoreAssemblyName + ".dll：" + ex.Message);
                Console.WriteLine("       请先在 Unity/ReunionMovement 下执行 dotnet build（生成 Temp/bin/Debug）。");
                return 2;
            }

            Console.WriteLine("=== Reunion-Movement 无 Unity 自检 ===");
            Console.WriteLine("程序集: " + core.Location);

            // 先把框架自己的程序集加载进 AppDomain：
            // ICustomSystem / GameEntry 在 Base，Log 在 Common，各模块在 Utils/Core。
            // 不预加载的话 GetType 遍历已加载程序集时会找不到它们。
            PreloadFrameworkAssemblies();
            Console.WriteLine("已加载框架程序集: " + string.Join(", ",
                new[] { "ReunionMovement.Base", "ReunionMovement.Common", "ReunionMovement.Utils", "ReunionMovement.Core" }
                    .Where(n => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == n))));
            Console.WriteLine();

            Run("模块顺序（声明顺序 == 属性顺序）", () => CheckCatalogOrder(core));
            Run("模块顺序（打乱声明顺序后结果不变）", () => CheckCatalogIgnoresDeclarationOrder(core));
            Run("模块校验（缺依赖）", () => CheckCatalogMissingDependency(core));
            Run("模块校验（重复注册）", () => CheckCatalogDuplicate(core));
            Run("Modules 访问器（引擎未启动）", () => CheckModulesAccessor(core));
            Run("游戏入口解析（默认取框架声明）", () => CheckEntryResolver(core));
            Run("Bootstrap 配置（测试场景判定）", () => CheckBootstrapOptions(core));

            Console.WriteLine();
            Console.WriteLine($"=== 结果: 通过 {passed} / 跳过 {skipped} / 失败 {failed} ===");
            return failed == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- 用例

        private static void CheckCatalogOrder(Assembly core)
        {
            object catalog = GetType(core, "ReunionMovement.Core.ModuleCatalog");
            IList declared = CreateModuleList(core, DeclarationOrder);
            var (ok, ordered, errors) = TryBuild(catalog, declared);

            Assert(ok, "TryBuild 应成功，实际错误: " + errors);
            Assert(errors == null || errors.Length == 0, "成功时不应有错误信息: " + errors);
            AssertSequence(ordered, DeclarationOrder);
        }

        private static void CheckCatalogIgnoresDeclarationOrder(Assembly core)
        {
            object catalog = GetType(core, "ReunionMovement.Core.ModuleCatalog");
            var shuffled = DeclarationOrder.ToList();
            var rnd = new Random(20251021);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            var (ok, ordered, errors) = TryBuild(catalog, CreateModuleList(core, shuffled));

            Assert(ok, "打乱后仍应成功: " + errors);
            AssertSequence(ordered, DeclarationOrder);
        }

        private static void CheckCatalogMissingDependency(Assembly core)
        {
            object catalog = GetType(core, "ReunionMovement.Core.ModuleCatalog");
            // 去掉 ResourcesSystem：SoundSystem / AddressableSystem 等都声明依赖它
            var declared = DeclarationOrder.Where(n => n != "ResourcesSystem").ToList();
            var (ok, ordered, errors) = TryBuild(catalog, CreateModuleList(core, declared));

            Assert(!ok, "缺依赖时应返回 false");
            Assert(errors != null && errors.Contains("ResourcesSystem"), "错误信息应指出缺少哪个依赖，实际: " + errors);
            Assert(ordered != null && ordered.Count == declared.Count, "校验失败时应退回声明顺序，且条目数不变");
        }

        private static void CheckCatalogDuplicate(Assembly core)
        {
            object catalog = GetType(core, "ReunionMovement.Core.ModuleCatalog");
            IList declared = CreateModuleList(core, DeclarationOrder);
            declared.Add(declared[0]);   // 同一个实例再加一次

            var (ok, ordered, errors) = TryBuild(catalog, declared);

            Assert(!ok, "重复注册应返回 false");
            Assert(errors != null && errors.Contains("重复"), "错误信息应提到重复注册，实际: " + errors);
        }

        private static void CheckModulesAccessor(Assembly core)
        {
            Type modules = GetType(core, "ReunionMovement.Core.Modules");
            Type systemType = GetType(core, "ReunionMovement.Core.UI.UISystem");

            IList all = (IList)modules.GetProperty("All", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert(all != null, "Modules.All 不应为 null");
            Assert(all.Count == 0, "引擎未启动时 Modules.All 应为空，实际 " + all.Count);

            MethodInfo tryGet = modules.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(systemType);
            var args = new object[] { null };
            bool found = (bool)tryGet.Invoke(null, args);
            Assert(!found && args[0] == null, "引擎未启动时 TryGet 应返回 false");

            string text = (string)modules.GetMethod("DescribeAll", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            Assert(!string.IsNullOrEmpty(text), "DescribeAll 不应返回空文本");

            MethodInfo get = modules.GetMethod("Get", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(systemType);
            try
            {
                get.Invoke(null, null);
                Assert(false, "Get 应抛异常");
            }
            catch (TargetInvocationException tie)
            {
                string message = tie.InnerException?.Message ?? string.Empty;
                Assert(tie.InnerException is InvalidOperationException, "应抛 InvalidOperationException，实际: " + tie.InnerException?.GetType().Name);
                Assert(message.Contains("UISystem"), "异常信息应包含类型名，实际: " + message);
            }
        }

        private static void CheckEntryResolver(Assembly core)
        {
            Type resolver = GetType(core, "ReunionMovement.Core.GameEntryResolver");
            Type entryType = (Type)resolver.GetMethod("FindEntryType", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

            Assert(entryType != null, "应解析到入口类型");
            Assert(entryType.FullName == "ReunionMovement.Core.StartGame",
                "默认入口应是 Core 里的 StartGame，实际: " + entryType?.FullName);
            Assert(typeof(object).IsAssignableFrom(entryType), "入口类型应可实例化");
        }

        private static void CheckBootstrapOptions(Assembly core)
        {
            Type optionsType = GetType(core, "ReunionMovement.Core.BootstrapOptions");
            object options = Activator.CreateInstance(optionsType);
            MethodInfo isTestScene = optionsType.GetMethod("IsTestScene");

            Assert((bool)isTestScene.Invoke(options, new object[] { "TestScene" }), "TestScene 命中前缀 Test");
            Assert((bool)isTestScene.Invoke(options, new object[] { "_Sandbox" }), "_Sandbox 命中前缀 _");
            Assert((bool)isTestScene.Invoke(options, new object[] { "SomeExampleScene" }), "SomeExampleScene 命中关键字 Example");
            Assert((bool)isTestScene.Invoke(options, new object[] { "MusicMain" }), "MusicMain 命中关键字 Music");
            Assert(!(bool)isTestScene.Invoke(options, new object[] { "StartGame" }), "StartGame 不应被判为测试场景");
            Assert(!(bool)isTestScene.Invoke(options, new object[] { "" }), "空串不应判为测试场景");
        }

        // ---------------------------------------------------------------- 工具

        /// <summary>与 Core/StartGame.cs 的 CreateModules 书写顺序一致。</summary>
        private static readonly string[] DeclarationOrder =
        {
            "ResourcesSystem", "AddressableSystem", "SceneSystem", "EventMessageSystem",
            "LanguagesSystem", "PauseSystem", "SoundSystem", "TimerMgr",
            "UISystem", "UIInputSystem", "UIToolkitSystem", "TerminalSystem",
            "NetworkMgr", "HttpMgr",
        };

        private static IList CreateModuleList(Assembly core, IEnumerable<string> typeNames)
        {
            Type systemInterface = GetType(core, "ReunionMovement.Core.Base.ICustomSystem");
            Type listType = typeof(List<>).MakeGenericType(systemInterface);
            var list = (IList)Activator.CreateInstance(listType);

            foreach (string name in typeNames)
            {
                // 模块分散在 Core（ResourcesSystem…）与 Utils（TimerMgr/HttpMgr/NetworkMgr）两个程序集
                Type type = GetAllTypes().First(t => t.Name == name);
                PropertyInfo instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (instanceProp == null) throw new Exception(name + "（" + type.FullName + "）没有 public static Instance 属性");
                object instance = instanceProp.GetValue(null);
                if (instance == null) throw new Exception(name + " 的 Instance 返回了 null");
                list.Add(instance);
            }
            return list;
        }

        private static (bool ok, IList ordered, string errors) TryBuild(object catalog, IList declared)
        {
            MethodInfo tryBuild = catalog.GetType().GetMethod("TryBuild", BindingFlags.Public | BindingFlags.Static);
            var args = new object[] { declared, null, null };
            bool ok = (bool)tryBuild.Invoke(null, args);
            return (ok, (IList)args[1], (string)args[2]);
        }

        private static void AssertSequence(IList ordered, string[] expected)
        {
            var actual = ordered.Cast<object>().Select(o => o.GetType().Name).ToArray();
            string got = string.Join(" → ", actual);
            string want = string.Join(" → ", expected);
            Assert(actual.Length == expected.Length && actual.SequenceEqual(expected),
                "顺序不符\n     期望: " + want + "\n     实际: " + got);
        }

        private static void Run(string name, Action action)
        {
            try
            {
                action();
                passed++;
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                Exception real = Flatten(ex);
                if (IsUnityRuntimeBlocked(real))
                {
                    skipped++;
                    Console.WriteLine("[SKIP] " + name);
                    Console.WriteLine("       该路径会触到 Unity 原生调用（" + real.GetType().Name + "），只能在 Unity 里跑");
                    return;
                }

                failed++;
                Console.WriteLine("[FAIL] " + name);
                Console.WriteLine("       " + real.ToString().Replace("\n", "\n       "));
            }
        }

        /// <summary>Unity 的 ECall / Resources 加载在纯 .NET 进程里必然失败，属于"跑不了"而不是"没通过"。</summary>
        private static bool IsUnityRuntimeBlocked(Exception ex)
        {
            if (ex is System.Security.SecurityException) return true;
            if (ex is TypeInitializationException && IsUnityRuntimeBlocked(ex.InnerException)) return true;
            string stack = ex.StackTrace ?? string.Empty;
            return stack.Contains("UnityEngine.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static Exception Flatten(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }

        private static IEnumerable<Type> GetAllTypes()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name;
                if (!seen.Add(name)) continue;
                if (name != null && (name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft", StringComparison.Ordinal))) continue;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type t in types) yield return t;
            }
        }
        private static Type GetType(Assembly core, string fullName)
        {
            Type type = core.GetType(fullName, false);
            if (type != null) return type;

            // ICustomSystem / GameEntry 等定义在 Base 程序集，不在 Core 里
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetType(fullName, false);
                }
                catch
                {
                    continue;
                }
                if (type != null) return type;
            }

            throw new Exception("找不到类型 " + fullName);
        }

        // ---------------------------------------------------------------- 程序集解析

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Unity/ReunionMovement"))) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private static void PrepareSearchPaths(string repoRoot)
        {
            string project = Path.Combine(repoRoot, "Unity/ReunionMovement");

            AddSearchPath(Path.Combine(project, "Temp/bin/Debug"));
            AddSearchPath(Path.Combine(project, "Library/ScriptAssemblies"));

            // 工程里以 DLL 形式引入的第三方（R3 / DOTween / ZString / UniTask 等）
            string assets = Path.Combine(project, "Assets");
            if (Directory.Exists(assets))
            {
                foreach (string dll in Directory.GetFiles(assets, "*.dll", SearchOption.AllDirectories))
                {
                    AddSearchPath(Path.GetDirectoryName(dll));
                }
            }

            // 从 Unity 生成的 csproj 里收集 UnityEngine 等 DLL 的绝对路径，
            // 这样换一台机器也不用改配置（只要 dotnet build 过）
            string csproj = Path.Combine(project, "ReunionMovement.Core.csproj");
            if (File.Exists(csproj))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(csproj), @"<HintPath>([^<]+)</HintPath>"))
                {
                    string hint = m.Groups[1].Value.Trim();
                    if (hint.Length > 0 && File.Exists(hint)) AddSearchPath(Path.GetDirectoryName(hint));
                }
            }
        }

        private static void AddSearchPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            if (!SearchPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) SearchPaths.Add(path);
        }

        /// <summary>按名字把框架程序集加载进来（找不到就静默跳过，由后续用例报真实原因）。</summary>
        private static void PreloadFrameworkAssemblies()
        {
            string[] names =
            {
                "ReunionMovement.Base",
                "ReunionMovement.Common",
                "ReunionMovement.Utils",
                "ReunionMovement.Core",
                "UniTask",
                "R3",
            };

            foreach (string name in names)
            {
                try
                {
                    Assembly.LoadFrom(FindAssemblyPath(name));
                }
                catch
                {
                    // 交由用例层报错：缺哪个程序集，用例会给出更具体的信息
                }
            }
        }

        private static string FindAssemblyPath(string simpleName)
        {
            foreach (string dir in SearchPaths)
            {
                string candidate = Path.Combine(dir, simpleName + ".dll");
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException("找不到程序集 " + simpleName);
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs e)
        {
            string simpleName = new AssemblyName(e.Name).Name;
            foreach (string dir in SearchPaths)
            {
                string candidate = Path.Combine(dir, simpleName + ".dll");
                if (File.Exists(candidate))
                {
                    try { return Assembly.LoadFrom(candidate); }
                    catch { /* 继续找下一个 */ }
                }
            }
            return null;
        }
    }
}
