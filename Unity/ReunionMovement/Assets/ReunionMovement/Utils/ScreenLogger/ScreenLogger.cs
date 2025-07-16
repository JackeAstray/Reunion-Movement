using UnityEngine;
using System.Collections.Generic;

namespace LLAFramework
{
    /// <summary>
    /// 屏幕Log工具
    /// </summary>
    public class ScreenLogger : MonoBehaviour
    {
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

        [Range(0f, 01f)]
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

        static Queue<LogMessage> queue = new Queue<LogMessage>();

        GUIStyle styleContainer, styleText;
        int padding = 5;

        public void Awake()
        {
            Texture2D back = new Texture2D(1, 1);
            backgroundColor.a = backgroundOpacity;
            back.SetPixel(0, 0, backgroundColor);
            back.Apply();

            styleContainer = new GUIStyle();
            styleContainer.normal.background = back;
            styleContainer.wordWrap = true;
            styleContainer.padding = new RectOffset(padding, padding, padding, padding);

            styleText = new GUIStyle();
            styleText.fontSize = fontSize;

            if (isPersistent)
            {
                DontDestroyOnLoad(this);
            }
        }

        void OnEnable()
        {
            if (!showInEditor && Application.isEditor) return;

            queue = new Queue<LogMessage>();

            Application.logMessageReceived += HandleLog;
        }

        void OnDisable()
        {
            if (!showInEditor && Application.isEditor) return;

            Application.logMessageReceived -= HandleLog;
        }

        void Update()
        {
            if (!showInEditor && Application.isEditor) return;

            while (queue.Count > ((Screen.height - 2 * margin) * height - 2 * padding) / styleText.lineHeight)
            {
                queue.Dequeue();
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