using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 程序集相关工具类
    /// </summary>
    public static class AssemblyUtil
    {
        /// <summary>
        /// 获取当前 AppDomain 已加载的所有程序集
        /// </summary>
        /// <returns></returns>
        public static Assembly[] GetAllAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 获取 UnityEditor 程序集
        /// </summary>
        /// <returns></returns>
        public static Assembly GetUnityEditorAssembly()
        {
            return typeof(Editor).Assembly;
        }
#endif

        /// <summary>
        /// 获取 UnityEngine 程序集
        /// </summary>
        /// <returns></returns>
        public static Assembly GetUnityEngineAssembly()
        {
            return typeof(UnityEngine.Object).Assembly;
        }

        /// <summary>
        /// 安全地获取程序集中的类型列表，处理 ReflectionTypeLoadException 并过滤 null
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        /// <summary>
        /// 获取所有带有指定特性的类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Type[] GetTypesWithAttribute<T>() where T : Attribute
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => t.GetCustomAttributes(typeof(T), true).Length > 0)
                .ToArray();
        }

        /// <summary>
        /// 获取当前执行程序集中所有类型的数组
        /// </summary>
        /// <returns></returns>
        public static Type[] GetExecutingAssemblyTypes()
        {
            return SafeGetTypes(Assembly.GetExecutingAssembly()).ToArray();
        }

        /// <summary>
        /// 获取所有接口类型
        /// </summary>
        /// <returns></returns>
        public static Type[] GetAllInterfaceTypes()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => t.IsInterface)
                .ToArray();
        }

        /// <summary>
        /// 获取所有抽象类类型
        /// </summary>
        /// <returns></returns>
        public static Type[] GetAllAbstractClassTypes()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => t.IsClass && t.IsAbstract)
                .ToArray();
        }

        /// <summary>
        /// 获取所有枚举类型
        /// </summary>
        /// <returns></returns>
        public static Type[] GetAllEnumTypes()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => t.IsEnum)
                .ToArray();
        }

        /// <summary>
        /// 获取指定命名空间下的所有类型
        /// </summary>
        /// <param name="ns"></param>
        /// <returns></returns>
        public static Type[] GetTypesInNamespace(string ns)
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => string.Equals(t.Namespace, ns, StringComparison.Ordinal))
                .ToArray();
        }

        /// <summary>
        /// 获取所有 public 类
        /// </summary>
        /// <returns></returns>
        public static Type[] GetAllPublicClassTypes()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => t.IsClass && t.IsPublic)
                .ToArray();
        }

        #region Unity

        /// <summary>
        /// 获取所有 MonoBehaviour 派生类（可用于查找所有脚本组件）
        /// </summary>
        /// <returns></returns>
        public static Type[] GetAllMonoBehaviourTypes()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToArray();
        }

        /// <summary>
        /// 获取所有 ScriptableObject 派生类
        /// </summary>
        /// <returns></returns>
        public static Type[] GetAllScriptableObjectTypes()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => typeof(ScriptableObject).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToArray();
        }

        /// <summary>
        /// 获取所有 Unity 组件类型（继承自 Component）
        /// </summary>
        /// <returns></returns>
        public static Type[] GetAllComponentTypes()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => typeof(Component).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToArray();
        }
        #endregion

        /// <summary>
        /// 获取所有类型的全名（可用于调试或自动化工具）
        /// </summary>
        /// <returns></returns>
        public static string[] GetAllTypeFullNames()
        {
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName)
                .ToArray();
        }

        /// <summary>
        /// 获取某类型的所有字段名
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string[] GetFieldNames(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Select(f => f.Name)
                .ToArray();
        }

        /// <summary>
        /// 获取某类型的所有属性名
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string[] GetPropertyNames(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Select(p => p.Name)
                .ToArray();
        }

        #region 根据字符串获取类、类的方法、属性等信息
        /// <summary>
        /// 根据类的全名获取 Type
        /// </summary>
        /// <param name="typeFullName">类型全名</param>
        /// <returns></returns>
        public static Type GetTypeByName(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName)) return null;
            return GetAllAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.FullName == typeFullName || t.Name == typeFullName);
        }

        /// <summary>
        /// 根据类名和方法名获取 MethodInfo
        /// </summary>
        /// <param name="typeFullName">类型全名</param>
        /// <param name="methodName">方法名</param>
        /// <returns>方法信息</returns>
        public static MethodInfo GetMethodByName(string typeFullName, string methodName)
        {
            var type = GetTypeByName(typeFullName);
            if (type == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        /// <summary>
        /// 根据类名和属性名获取 PropertyInfo
        /// </summary>
        /// <param name="typeFullName">类型全名</param>
        /// <param name="propertyName">属性名</param>
        /// <returns>属性信息</returns>
        public static PropertyInfo GetPropertyByName(string typeFullName, string propertyName)
        {
            var type = GetTypeByName(typeFullName);
            if (type == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }
            return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }
        #endregion
    }
}
