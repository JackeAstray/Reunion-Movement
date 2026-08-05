using UnityEngine;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace ReunionMovement
{
    /// <summary>
    /// 屏幕Log工具
    /// </summary>
    public class ScreenLogger : MonoBehaviour
    {
        /// <summary>日志队列最大容量（超出时丢弃最旧消息，防止无界增长）</summary>
        private const int MaxQueueSize = 500;
        public enum LogAnchor
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        public bool isPersistent = true;
        public bool showInEditor = true;

        [Tooltip("日志区域的高度占屏幕高度的百分比")]
        [Range(0.3f, 1.0f)]
        public float height = 1f;

        [Tooltip("日志区域的宽度占屏幕宽度的百分比")]
        [Range(0.3f, 1.0f)]
        public float width = 1f;

        public int margin = 20;

        public LogAnchor anchorPosition = LogAnchor.BottomLeft;

        public int fontSize = 20;

        [Range(0f, 1f)]
        public float backgroundOpacity = 0.5f;
        public Color backgroundColor = Color.black;

        public bool logMessages = true;
        public bool logWarnings = true;
        public bool logErrors = true;

        public Color messageColor = Color.green;
        public Color warningColor = Color.yellow;
        public Color errorColor = new Color(1, 0f, 0.25f);

        public bool stackTraceMessages = false;
        public bool stackTraceWarnings = false;
        public bool stackTraceErrors = true;

        // 线程安全队列：Application.logMessageReceived 可能在后台线程触发，
        // 与主线程 Update/OnGUI 的读写必须并发安全
        static readonly ConcurrentQueue<LogMessage> queue = new ConcurrentQueue<LogMessage>();
        private static readonly HashSet<ScreenLogger> activeInstances = new HashSet<ScreenLogger>();

        GUIStyle styleContainer, styleText;
        int padding = 5;
        Texture2D backgroundTex; // 保存引用以便在销毁时释放

        public void Awake()
        {
            backgroundTex = new Texture2D(1, 1);
            // 用局部变量设置 alpha，避免直接修改公共字段 backgroundColor 的透明度分量
            var bgColor = backgroundColor;
            bgColor.a = backgroundOpacity;
            backgroundTex.SetPixel(0, 0, bgColor);
            backgroundTex.Apply();

            styleContainer = new GUIStyle();
            styleContainer.normal.background = backgroundTex;
            styleContainer.wordWrap = true;
            styleContainer.padding = new RectOffset(padding, padding, padding, padding);

            styleText = new GUIStyle();
            styleText.fontSize = fontSize;

            if (isPersistent)
            {
                DontDestroyOnLoad(this);
            }
        }

        void OnDestroy()
        {
            // 从活跃实例列表中移除
            lock (activeInstances)
            {
                activeInstances.Remove(this);
            }
            // 确保退订日志事件：若组件在 disabled 状态下被销毁，OnDisable 不会执行，
            // 不退订会永久泄漏（被销毁实例继续接收全项目日志）
            Application.logMessageReceived -= HandleLog;
            if (backgroundTex != null)
            {
                Destroy(backgroundTex);
                backgroundTex = null;
            }
        }

        void OnEnable()
        {
            if (!showInEditor && Application.isEditor) return;

            lock (activeInstances)
            {
                activeInstances.Add(this);
            }
            // 只由第一个启用的实例清空队列，避免多实例互相覆盖
            if (activeInstances.Count == 1)
            {
                while (queue.TryDequeue(out _)) { }
            }

            Application.logMessageReceived += HandleLog;
        }

        void OnDisable()
        {
            if (!showInEditor && Application.isEditor) return;

            Application.logMessageReceived -= HandleLog;

            lock (activeInstances)
            {
                activeInstances.Remove(this);
            }
        }

        void Update()
        {
            if (!showInEditor && Application.isEditor) return;

            // 防止 lineHeight 为 0 导致除零异常（字体未加载时可能为 0）
            float lineH = styleText.lineHeight > 0 ? styleText.lineHeight : Mathf.Max(fontSize, 1);
            while (queue.Count > ((Screen.height - 2 * margin) * height - 2 * padding) / lineH)
            {
                queue.TryDequeue(out _);
            }
        }

        void OnGUI()
        {
            if (!showInEditor && Application.isEditor) return;

            float w = (Screen.width - 2 * margin) * width;
            float h = (Screen.height - 2 * margin) * height;
            float x = 1, y = 1;

            switch (anchorPosition)
            {
                case LogAnchor.BottomLeft:
                    x = margin;
                    y = margin + (Screen.height - 2 * margin) * (1 - height);
                    break;

                case LogAnchor.BottomRight:
                    x = margin + (Screen.width - 2 * margin) * (1 - width);
                    y = margin + (Screen.height - 2 * margin) * (1 - height);
                    break;

                case LogAnchor.TopLeft:
                    x = margin;
                    y = margin;
                    break;

                case LogAnchor.TopRight:
                    x = margin + (Screen.width - 2 * margin) * (1 - width);
                    y = margin;
                    break;
            }

            GUILayout.BeginArea(new Rect(x, y, w, h), styleContainer);

            foreach (LogMessage m in queue)
            {
                switch (m.Type)
                {
                    case LogType.Warning:
                        styleText.normal.textColor = warningColor;
                        break;

                    case LogType.Log:
                        styleText.normal.textColor = messageColor;
                        break;

                    case LogType.Assert:
                    case LogType.Exception:
                    case LogType.Error:
                        styleText.normal.textColor = errorColor;
                        break;

                    default:
                        styleText.normal.textColor = messageColor;
                        break;
                }
                GUILayout.Label(m.Message, styleText);
            }
            GUILayout.EndArea();
        }

        void HandleLog(string message, string stackTrace, LogType type)
        {
            if (!ShouldLog(type)) return;

            // 容量上限：超出时丢弃最旧消息，防止单帧大量报错导致队列无界膨胀
            while (queue.Count >= MaxQueueSize)
            {
                queue.TryDequeue(out _);
            }

            queue.Enqueue(new LogMessage(message, type));

            if (!ShouldStackTrace(type)) return;

            string[] trace = stackTrace.Split(new char[] { '\n' });

            foreach (string t in trace)
            {
                if (t.Length != 0)
                {
                    queue.Enqueue(new LogMessage("  " + t, type));
                }
            }
        }

        bool ShouldLog(LogType type)
        {
            switch (type)
            {
                case LogType.Assert:
                case LogType.Error:
                case LogType.Exception:
                    return logErrors;

                case LogType.Log:
                    return logMessages;

                case LogType.Warning:
                    return logWarnings;

                default:
                    return false;
            }
        }

        bool ShouldStackTrace(LogType type)
        {
            switch (type)
            {
                case LogType.Assert:
                case LogType.Error:
                case LogType.Exception:
                    return stackTraceErrors;

                case LogType.Log:
                    return stackTraceMessages;

                case LogType.Warning:
                    return stackTraceWarnings;

                default:
                    return false;
            }
        }
    }

    class LogMessage
    {
        public string Message;
        public LogType Type;

        public LogMessage(string msg, LogType type)
        {
            Message = msg;
            Type = type;
        }
    }
}