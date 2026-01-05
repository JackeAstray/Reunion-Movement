using System;
using UnityEngine;
using Conditional = System.Diagnostics.ConditionalAttribute;

namespace Mirror.SimpleWeb
{
    public static class Log
    {
        // .NET 控制台颜色名称大致对应的 CSS 颜色名称：

        // Black:       Black
        // Blue:        Blue
        // Cyan:        Aqua or Cyan
        // DarkBlue:    DarkBlue
        // DarkCyan:    DarkCyan
        // DarkGray:    DarkGray
        // DarkGreen:   DarkGreen
        // DarkMagenta: DarkMagenta
        // DarkRed:     DarkRed
        // DarkYellow:  DarkOrange or DarkGoldenRod
        // Gray:        Gray
        // Green:       Green
        // Magenta:     Magenta
        // Red:         Red
        // White:       White
        // Yellow:      Yellow

        // 我们不能使用接近白色或黑色的颜色，因为
        // 它们在服务器控制台或浏览器控制台中显示效果不好

        public enum Levels
        {
            Flood,
            Verbose,
            Info,
            Warn,
            Error,
            None
        }

        public static ILogger logger = Debug.unityLogger;
        public static Levels minLogLevel = Levels.None;

        /// <summary>
        /// 将所有异常记录到控制台
        /// </summary>
        /// <param name="e">要记录的异常</param>
        public static void Exception(Exception e)
        {
#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[SWT:异常] {e.GetType().Name}: {e.Message}\n{e.StackTrace}\n\n");
            Console.ResetColor();
#else
            logger.Log(LogType.Exception, $"[SWT:异常] {e.GetType().Name}: {e.Message}\n{e.StackTrace}\n\n");
#endif
        }

        /// <summary>
        /// 当 minLogLevel 设置为 Flood 或更低时，将 Flood 日志记录到控制台
        /// </summary>
        /// <param name="msg">要记录的消息文本</param>
        [Conditional("DEBUG")]
        public static void Flood(string msg)
        {
            if (minLogLevel > Levels.Flood) return;

#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.Gray;
            logger.Log(LogType.Log, msg.Trim());
            Console.ResetColor();
#else
            logger.Log(LogType.Log, msg.Trim());
#endif
        }

        /// <summary>
        /// 当 minLogLevel 设置为 Flood 或更低时，将缓冲区以可读形式记录到控制台
        /// <para>需要 Debug 模式，例如 Unity 编辑器 或 开发构建</para>
        /// </summary>
        /// <param name="label">日志来源标签</param>
        /// <param name="buffer">要记录的字节数组</param>
        /// <param name="offset">字节数组的起始位置</param>
        /// <param name="length">要读取的字节数量</param>
        [Conditional("DEBUG")]
        public static void DumpBuffer(string label, byte[] buffer, int offset, int length)
        {
            if (minLogLevel > Levels.Flood) return;

#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            logger.Log(LogType.Log, $"{label}: {BufferToString(buffer, offset, length)}");
            Console.ResetColor();
#else
            logger.Log(LogType.Log, $"<color=cyan>{label}: {BufferToString(buffer, offset, length)}</color>");
#endif
        }

        /// <summary>
        /// 当 minLogLevel 设置为 Flood 或更低时，将 ArrayBuffer 的内容记录到控制台
        /// <para>需要 Debug 模式，例如 Unity 编辑器 或 开发构建</para>
        /// </summary>
        /// <param name="label">日志来源标签</param>
        /// <param name="arrayBuffer">要显示详细信息的 ArrayBuffer</param>
        [Conditional("DEBUG")]
        public static void DumpBuffer(string label, ArrayBuffer arrayBuffer)
        {
            if (minLogLevel > Levels.Flood) return;

#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            logger.Log(LogType.Log, $"{label}: {BufferToString(arrayBuffer.array, 0, arrayBuffer.count)}");
            Console.ResetColor();
#else
            logger.Log(LogType.Log, $"<color=cyan>{label}: {BufferToString(arrayBuffer.array, 0, arrayBuffer.count)}</color>");
#endif
        }

        /// <summary>
        /// 当 minLogLevel 设置为 Verbose 或更低时，将详细信息记录到控制台
        /// </summary>
        /// <param name="msg">要记录的消息文本</param>
        public static void Verbose(string msg)
        {
            if (minLogLevel > Levels.Verbose) return;

#if DEBUG
            // Debug 构建和 Unity 编辑器
            logger.Log(LogType.Log, msg.Trim());
#else
            // 服务器或 WebGL
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(msg.Trim());
            Console.ResetColor();
#endif
        }

        public static void Verbose<T>(string msg, T arg1)
        {
            if (minLogLevel > Levels.Verbose) return;
            Verbose(String.Format(msg, arg1));
        }

        public static void Verbose<T1, T2>(string msg, T1 arg1, T2 arg2)
        {
            if (minLogLevel > Levels.Verbose) return;
            Verbose(String.Format(msg, arg1, arg2));
        }

        /// <summary>
        /// 当 minLogLevel 设置为 Info 或更低时，将信息记录到控制台
        /// </summary>
        /// <param name="msg">要记录的消息文本</param>
        /// <param name="consoleColor">控制台颜色（默认青色在服务器和浏览器控制台中效果较好）</param>
        static void Info(string msg, ConsoleColor consoleColor = ConsoleColor.Cyan)
        {
#if DEBUG
            // Debug 构建和 Unity 编辑器
            logger.Log(LogType.Log, msg.Trim());
#else
            // 服务器或 WebGL
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(msg.Trim());
            Console.ResetColor();
#endif
        }

        public static void Info<T>(string msg, T arg1, ConsoleColor consoleColor = ConsoleColor.Cyan)
        {
            if (minLogLevel > Levels.Info) return;
            Info(String.Format(msg, arg1), consoleColor);
        }

        public static void Info<T1, T2>(string msg, T1 arg1, T2 arg2, ConsoleColor consoleColor = ConsoleColor.Cyan)
        {
            if (minLogLevel > Levels.Info) return;
            Info(String.Format(msg, arg1, arg2), consoleColor);
        }

        /// <summary>
        /// 记录异常（信息级别）到控制台，如果 minLogLevel 允许
        /// </summary>
        /// <param name="e">要记录的异常</param>
        public static void InfoException(Exception e)
        {
            if (minLogLevel > Levels.Info) return;

#if DEBUG
            // Debug 构建和 Unity 编辑器
            logger.Log(LogType.Exception, e.Message);
#else
            // 服务器或 WebGL
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(e.Message);
            Console.ResetColor();
#endif
        }

        /// <summary>
        /// 当 minLogLevel 设置为 Warn 或更低时，将警告记录到控制台
        /// </summary>
        /// <param name="msg">要记录的消息文本</param>
        public static void Warn(string msg)
        {
            if (minLogLevel > Levels.Warn) return;

#if DEBUG
            // Debug 构建和 Unity 编辑器
            logger.Log(LogType.Warning, msg.Trim());
#else
            // 服务器或 WebGL
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg.Trim());
            Console.ResetColor();
#endif
        }

        public static void Warn<T>(string msg, T arg1)
        {
            if (minLogLevel > Levels.Warn) return;
            Warn(String.Format(msg, arg1));
        }

        /// <summary>
        /// 当 minLogLevel 设置为 Error 或更低时，将错误记录到控制台
        /// </summary>
        /// <param name="msg">要记录的消息文本</param>
        public static void Error(string msg)
        {
            if (minLogLevel > Levels.Error) return;

#if DEBUG
            // Debug 构建和 Unity 编辑器
            logger.Log(LogType.Error, msg.Trim());
#else
            // 服务器或 WebGL
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg.Trim());
            Console.ResetColor();
#endif
        }

        public static void Error<T>(string msg, T arg1)
        {
            if (minLogLevel > Levels.Error) return;
            Error(String.Format(msg, arg1));
        }

        public static void Error<T1, T2>(string msg, T1 arg1, T2 arg2)
        {
            if (minLogLevel > Levels.Error) return;
            Error(String.Format(msg, arg1, arg2));
        }

        public static void Error<T1, T2, T3>(string msg, T1 arg1, T2 arg2, T3 arg3)
        {
            if (minLogLevel > Levels.Error) return;
            Error(String.Format(msg, arg1, arg2, arg3));
        }

        /// <summary>
        /// 返回从 offset 开始，长度为 length 的字节数组的字符串表示
        /// </summary>
        /// <param name="buffer">要读取的字节数组</param>
        /// <param name="offset">字节数组的起始位置</param>
        /// <param name="length">要读取的字节数量</param>
        /// <returns></returns>
        public static string BufferToString(byte[] buffer, int offset = 0, int? length = null) => BitConverter.ToString(buffer, offset, length ?? buffer.Length);
    }
}
