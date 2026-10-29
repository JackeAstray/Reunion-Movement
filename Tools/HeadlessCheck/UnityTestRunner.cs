using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Astray.HeadlessCheck
{
    /// <summary>
    /// 极简 NUnit 执行器：把 <c>Astray.Tests.dll</c> 里的用例在纯 .NET 进程里跑一遍。
    /// <para>
    /// 不引用 NUnit，全部按名字匹配；断言失败靠 <c>NUnit.Framework.AssertionException</c> 识别。
    /// 需要 Unity 的用例会抛 <c>SecurityException</c> 或栈里带 <c>UnityEngine.</c>，统一算 SKIP 而不是 FAIL。
    /// </para>
    /// </summary>
    internal static class UnityTestRunner
    {
        public sealed class Result
        {
            public int Passed;
            public int Failed;
            public int Skipped;
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Skips = new List<string>();
        }

        public static Result Run(Assembly testAssembly, string filter, Action<string> log)
        {
            var result = new Result();
            var classes = testAssembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Where(t => Methods(t).Any())
                .Where(t => string.IsNullOrEmpty(filter) || t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(t => t.Name)
                .ToList();

            foreach (Type type in classes)
            {
                int classPassed = 0, classFailed = 0, classSkipped = 0;

                foreach (MethodInfo test in Methods(type))
                {
                    object instance;
                    try
                    {
                        instance = Activator.CreateInstance(type);
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        result.Failures.Add($"{type.Name}: 无法实例化 - {Flatten(ex).Message}");
                        continue;
                    }

                    List<object[]> cases = Cases(test);

                    foreach (object[] arguments in cases)
                    {
                        string name = type.Name + "." + test.Name +
                            (arguments.Length > 0 ? "(" + string.Join(",", arguments.Select(Format)) + ")" : string.Empty);

                        try
                        {
                            InvokeAll(type, instance, "SetUpAttribute");
                            Invoke(test, instance, arguments);
                            InvokeAll(type, instance, "TearDownAttribute");

                            result.Passed++;
                            classPassed++;
                        }
                        catch (Exception ex)
                        {
                            Exception real = Flatten(ex);

                            // 每个用例都要跑 TearDown，否则后续用例会被脏状态连累
                            try { InvokeAll(type, instance, "TearDownAttribute"); } catch { }

                            string reason = UnityBlockReason(real);
                            if (reason != null)
                            {
                                result.Skipped++;
                                classSkipped++;
                                result.Skips.Add(name + "  [" + reason + "]");
                            }
                            else
                            {
                                result.Failed++;
                                classFailed++;
                                result.Failures.Add(name + ": " + real.Message);
                            }
                        }
                    }
                }

                log($"  {type.Name,-34} 通过 {classPassed,2} / 失败 {classFailed,2} / 跳过 {classSkipped,2}");
            }

            return result;
        }

        private static IEnumerable<MethodInfo> Methods(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes().Any(a => IsAttribute(a, "TestAttribute") || IsAttribute(a, "TestCaseAttribute")))
                .OrderBy(m => m.Name);
        }

        /// <summary>返回该方法的参数集列表（无 [TestCase] 时是一组空参数）。</summary>
        private static List<object[]> Cases(MethodInfo method)
        {
            var cases = new List<object[]>();
            foreach (object attribute in method.GetCustomAttributes())
            {
                if (!IsAttribute(attribute, "TestCaseAttribute")) continue;
                var arguments = attribute.GetType().GetProperty("Arguments")?.GetValue(attribute) as object[];
                cases.Add(Box(arguments, method));
            }

            if (cases.Count == 0) cases.Add(Array.Empty<object>());
            return cases;
        }

        /// <summary>TestCase 的参数类型可能与形参不完全一致（如 int 传给 long），做一次收敛。</summary>
        private static object[] Box(object[] arguments, MethodInfo method)
        {
            if (arguments == null) return Array.Empty<object>();
            ParameterInfo[] parameters = method.GetParameters();
            var result = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                object value = i < arguments.Length ? arguments[i] : null;
                Type target = parameters[i].ParameterType;
                if (value != null && !target.IsInstanceOfType(value) && target.IsPrimitive)
                {
                    try { value = Convert.ChangeType(value, target); } catch { }
                }
                result[i] = value;
            }
            return result;
        }

        private static void InvokeAll(Type type, object instance, string attributeName)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes().Any(a => IsAttribute(a, attributeName))))
            {
                Invoke(method, instance, Array.Empty<object>());
            }
        }

        private static void Invoke(MethodInfo method, object instance, object[] arguments)
        {
            object returned = method.Invoke(instance, arguments);
            if (method.ReturnType == typeof(void) || returned == null) return;

            // async Task / async UniTask：等到完成再算这条用例结束
            MethodInfo getAwaiter = returned.GetType().GetMethod("GetAwaiter", Type.EmptyTypes);
            if (getAwaiter == null) return;

            object awaiter = getAwaiter.Invoke(returned, null);
            MethodInfo getResult = awaiter.GetType().GetMethod("GetResult", Type.EmptyTypes);
            getResult?.Invoke(awaiter, null);
        }

        private static string Format(object value)
        {
            if (value == null) return "null";
            string text = value.ToString();
            return text.Length > 16 ? text.Substring(0, 16) + "..." : text;
        }

        private static bool IsAttribute(object attribute, string simpleName)
        {
            for (Type t = attribute.GetType(); t != null; t = t.BaseType)
            {
                if (t.Name == simpleName) return true;
            }
            return false;
        }

        /// <summary>判断这次失败是不是"因为不在 Unity 里"；是的话返回证据（哪个 Unity 调用），否则返回 null。</summary>
        private static string UnityBlockReason(Exception ex)
        {
            if (ex.GetType().Name == "IgnoreException") return "Assert.Ignore";
            if (ex is System.Security.SecurityException) return "SecurityException（Unity 原生调用）";
            if (ex is TypeInitializationException && ex.InnerException != null) return UnityBlockReason(ex.InnerException);

            string stack = ex.StackTrace ?? string.Empty;
            foreach (string line in stack.Split('\n'))
            {
                if (line.Contains("UnityEngine."))
                {
                    return line.Trim().TrimStart('a', 't', ' ');
                }
            }
            return null;
        }

        private static Exception Flatten(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }
    }
}
