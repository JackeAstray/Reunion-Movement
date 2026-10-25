using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Tilemaps;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// 下载执行器工厂
    /// </summary>
    public static class DownloadExecutorFactory
    {
        /// <summary>
        /// 下载执行器类型映射（类名->类型）
        /// </summary>
        private static Dictionary<string, Type> typeMap = new Dictionary<string, Type>();

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static DownloadExecutorFactory()
        {
            // 注意：这里不能用 ToDictionary(t => t.Name, t => t)。若程序集内存在同名（不同命名空间）的执行器，
            // 它会在静态构造函数里抛 ArgumentException，使本类第一次被触碰时以 TypeInitializationException 整体崩溃
            // （而工厂本身"找不到就记日志返回 null"的设计是不会崩的）。改用"后者覆盖前者"：
            // 名称唯一时与原先行为完全一致；出现重名时退化为一条可读日志，而不是崩掉。
            foreach (Type t in typeof(IDownloadExecutor).Assembly.GetTypes())
            {
                if (t.IsAbstract || !t.IsSubclassOf(typeof(IDownloadExecutor))) continue;
                if (typeMap.ContainsKey(t.Name))
                {
                    Log.Error("下载执行器名称重复：{0}（{1} 与 {2}），仅保留后者", t.Name, typeMap[t.Name].FullName, t.FullName);
                }
                typeMap[t.Name] = t;
            }
        }

        /// <summary>
        /// 创建下载执行器
        /// </summary>
        /// <param name="classname"></param>
        /// <returns></returns>
        public static IDownloadExecutor CreateFromClassName(string classname)
        {
            if (string.IsNullOrEmpty(classname))
            {
                Log.Error("{0} 是Null 或 Empty", classname);
                return null;
            }

            if (!typeMap.TryGetValue(classname, out var type))
            {
                Log.Error("未找到名为 {0} 的下载执行器类型", classname);
                return null;
            }

            return (IDownloadExecutor)Activator.CreateInstance(type);
        }
    }
}
