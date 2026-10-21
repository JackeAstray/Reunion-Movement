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
    /// 只验证"不依赖 Unity 运行时"的部分：模块排序与校验、Modules 访问器、Bootstrap 配置判断。
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
        private static Assembly DynamicFakes;

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

            PreloadFrameworkAssemblies();
            Console.WriteLine("已加载框架程序集: " + string.Join(", ",
                new[] { "ReunionMovement.Base", "ReunionMovement.Common", "ReunionMovement.Utils", "ReunionMovement.Core" }
                    .Where(n => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == n))));
            Console.WriteLine();

            Run("模块排序（按 ModuleOrder 升序）", () => CheckCatalogSorting(core));
            Run("模块排序（声明顺序不影响结果）", () => CheckCatalogIgnoresDeclarationOrder(core));
            Run("模块排序（依赖优先于序号）", () => CheckCatalogDependencyWinsOverOrder(core));
            Run("模块校验（重复注册）", () => CheckCatalogDuplicate(core));
            Run("模块校验（缺少依赖）", () => CheckCatalogMissingDependency(core));
            Run("Modules 访问器（引擎未启动）", () => CheckModulesAccessor(core));
            Run("游戏入口解析（默认取框架声明）", () => CheckEntryResolver(core));
            Run("Bootstrap 配置（测试场景判定）", () => CheckBootstrapOptions(core));

            Console.WriteLine();

            if (!args.Any(a => a == "--no-tests"))
            {
                RunUnityTests(repoRoot);
            }

            Console.WriteLine($"=== 结果: 通过 {passed} / 跳过 {skipped} / 失败 {failed} ===");
            return failed == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- 模块目录

        /// <summary>三个模块声明顺序为 30/10/20，排序后应为 10/20/30。</summary>
        private static void CheckCatalogSorting(Assembly core)
        {
            FakeModuleFactory fakes = CreateFactory(core);
            object c30 = fakes.Instantiate(fakes.Create(30));
            object c10 = fakes.Instantiate(fakes.Create(10));
            object c20 = fakes.Instantiate(fakes.Create(20));

            var (ok, ordered, errors) = TryBuild(core, c30, c10, c20);

            Assert(ok, "应构建成功，实际错误: " + errors);
            Assert(Orders(ordered) == "10,20,30", "顺序应为 10,20,30，实际: " + Orders(ordered));
        }

        /// <summary>打乱声明顺序，排序结果必须一致。</summary>
        private static void CheckCatalogIgnoresDeclarationOrder(Assembly core)
        {
            FakeModuleFactory fakes = CreateFactory(core);
            object[] modules =
            {
                fakes.Instantiate(fakes.Create(50)),
                fakes.Instantiate(fakes.Create(10)),
                fakes.Instantiate(fakes.Create(40)),
                fakes.Instantiate(fakes.Create(20)),
                fakes.Instantiate(fakes.Create(30)),
            };

            var (ok, ordered, errors) = TryBuild(core, modules);

            Assert(ok, "应构建成功，实际错误: " + errors);
            Assert(Orders(ordered) == "10,20,30,40,50", "顺序应为 10..50，实际: " + Orders(ordered));
        }

        /// <summary>B 序号更小但依赖 A：A 必须先于 B（依赖优先于序号）。</summary>
        private static void CheckCatalogDependencyWinsOverOrder(Assembly core)
        {
            FakeModuleFactory fakes = CreateFactory(core);
            Type typeA = fakes.Create(20);
            Type typeB = fakes.Create(10, typeA);        // B 序号 10 < A 的 20，但 B 依赖 A
            object a = fakes.Instantiate(typeA);
            object b = fakes.Instantiate(typeB);

            var (ok, ordered, errors) = TryBuild(core, b, a);   // 故意先声明 b

            Assert(ok, "应构建成功，实际错误: " + errors);
            string actual = Orders(ordered);
            Assert(actual == "20,10", "依赖应压过序号：A(20) 必须在 B(10) 之前，实际: " + actual);
        }

        private static void CheckCatalogDuplicate(Assembly core)
        {
            FakeModuleFactory fakes = CreateFactory(core);
            object module = fakes.Instantiate(fakes.Create(10));

            var (ok, ordered, errors) = TryBuild(core, module, module);

            Assert(!ok, "重复注册应返回 false");
            Assert(errors != null && errors.Contains("重复"), "错误信息应提到重复注册，实际: " + errors);
        }

        private static void CheckCatalogMissingDependency(Assembly core)
        {
            FakeModuleFactory fakes = CreateFactory(core);
            Type missing = fakes.Create(99);
            Type dependent = fakes.Create(10, missing);
            object only = fakes.Instantiate(dependent);   // 只注册依赖方，不注册被依赖方

            var (ok, ordered, errors) = TryBuild(core, only);

            Assert(!ok, "缺依赖应返回 false");
            Assert(errors != null && errors.Contains("未注册"), "错误信息应指出依赖未注册，实际: " + errors);
        }

        // ---------------------------------------------------------------- 其它用例

        private static void CheckModulesAccessor(Assembly core)
        {
            Type modules = GetType(core, "ReunionMovement.Core.Modules");
            Type systemType = GetType(core, "ReunionMovement.Core.UI.UISystem");

            IList all = (IList)modules.GetProperty("All", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert(all != null, "Modules.All 不应为 null");
            Assert(all.Count == 0, "引擎未启动时 Modules.All 应为空，实际 " + all.Count);

            MethodInfo tryGet = modules.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(systemType);
            var tryArgs = new object[] { null };
            bool found = (bool)tryGet.Invoke(null, tryArgs);
            Assert(!found && tryArgs[0] == null, "引擎未启动时 TryGet 应返回 false");

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
                Assert(tie.InnerException is InvalidOperationException,
                    "应抛 InvalidOperationException，实际: " + tie.InnerException?.GetType().Name);
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

        /// <summary>跑 ReunionMovement.Tests 里的用例；需要 Unity 的会自动算 SKIP。</summary>
        private static void RunUnityTests(string repoRoot)
        {
            string path = Path.Combine(repoRoot, "Unity/ReunionMovement/Temp/bin/Debug/ReunionMovement.Tests.dll");
            if (!File.Exists(path))
            {
                Console.WriteLine("[SKIP] Unity 测试程序集不存在（先 dotnet build ReunionMovement.Tests.csproj）");
                return;
            }

            Console.WriteLine("--- Unity Test Runner 的用例（本机可跑的部分） ---");

            Assembly tests;
            try
            {
                tests = Assembly.LoadFrom(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SKIP] 加载测试程序集失败: " + ex.Message);
                return;
            }

            UnityTestRunner.Result result = UnityTestRunner.Run(tests, null, Console.WriteLine);

            passed += result.Passed;
            failed += result.Failed;
            skipped += result.Skipped;

            foreach (string failure in result.Failures)
            {
                Console.WriteLine("       [FAIL] " + failure);
            }

            if (result.Skips.Count > 0 && Environment.GetEnvironmentVariable("HEADLESS_VERBOSE") == "1")
            {
                Console.WriteLine("       --- 跳过的用例（需要 Unity） ---");
                foreach (string skip in result.Skips) Console.WriteLine("       [SKIP] " + skip);
            }

            Console.WriteLine();
            Console.WriteLine($"    小计: 通过 {result.Passed} / 失败 {result.Failed} / 跳过 {result.Skipped}");
            Console.WriteLine();
        }
        // ---------------------------------------------------------------- 工具

        private static FakeModuleFactory CreateFactory(Assembly core)
        {
            var factory = BuildFactory(core);
            DynamicFakes = factory.DynamicAssembly;
            return factory;
        }

        private static FakeModuleFactory BuildFactory(Assembly core)
        {
            return new FakeModuleFactory(
                GetType(core, "ReunionMovement.Core.Base.ICustomSystem"),
                GetType(core, "ReunionMovement.Core.Base.ISystemInitializable"),
                GetType(core, "Cysharp.Threading.Tasks.UniTask"),
                GetType(core, "ReunionMovement.Core.Base.ModuleOrderAttribute"));
        }

        /// <summary>把假模块装进 List&lt;ICustomSystem&gt; 再交给 TryBuild。</summary>
        private static (bool ok, IList ordered, string errors) TryBuild(Assembly core, params object[] modules)
        {
            Type systemInterface = GetType(core, "ReunionMovement.Core.Base.ICustomSystem");
            Type listType = typeof(List<>).MakeGenericType(systemInterface);
            var list = (IList)Activator.CreateInstance(listType);
            foreach (object module in modules) list.Add(module);

            Type catalog = GetType(core, "ReunionMovement.Core.ModuleCatalog");
            MethodInfo tryBuild = catalog.GetMethod("TryBuild", BindingFlags.Public | BindingFlags.Static);
            var args = new object[] { list, null, null };
            bool ok = (bool)tryBuild.Invoke(null, args);
            return (ok, (IList)args[1], (string)args[2]);
        }

        /// <summary>把排好序的模块按 [ModuleOrder] 的序号拼成 "10,20,30" 便于断言。</summary>
        private static string Orders(IList ordered)
        {
            Type attributeType = GetType(Assembly.GetExecutingAssembly(), "ReunionMovement.Core.Base.ModuleOrderAttribute");
            var parts = new List<string>();
            foreach (object module in ordered)
            {
                object attr = module.GetType().GetCustomAttribute(attributeType);
                object order = attr?.GetType().GetProperty("Order")?.GetValue(attr);
                parts.Add(Convert.ToString(order));
            }
            return string.Join(",", parts);
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
            if (ex is TypeInitializationException && ex.InnerException != null) return IsUnityRuntimeBlocked(ex.InnerException);
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

        private static Type GetType(Assembly core, string fullName)
        {
            Type type = core.GetType(fullName, false);
            if (type != null) return type;

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

            // 从 Unity 生成的 csproj 里收集 UnityEngine 等 DLL 的绝对路径
            // 所有 Unity 生成的 csproj 都扫一遍：nunit 只在 Tests.csproj 的 HintPath 里
            foreach (string csproj in Directory.GetFiles(project, "ReunionMovement*.csproj"))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(csproj), @"<HintPath>([^<]+)</HintPath>"))
                {
                    string hint = m.Groups[1].Value.Trim();
                    if (hint.Length == 0) continue;

                    // HintPath 可能是相对路径（Library\PackageCache\...），要相对工程根解析
                    string full = Path.IsPathRooted(hint) ? hint : Path.GetFullPath(Path.Combine(project, hint));
                    if (File.Exists(full)) AddSearchPath(Path.GetDirectoryName(full));
                }
            }
        }

        private static void AddSearchPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            if (!SearchPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) SearchPaths.Add(path);
        }

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
                    // 缺哪个程序集，用例会给出更具体的信息
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

            // 假模块程序集只存在于内存里，按名字加载不到，必须在这里还给它
            if (simpleName == FakeModuleFactory.DynamicAssemblyName) return DynamicFakes;
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
