using UnityEngine;
using TMPro;
using Cysharp.Text;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// FPS计数器工具（使用 TextMeshPro 替代旧版 OnGUI）。
    /// FPS 采样单一权威：不再自行每帧计数，直接读取 PerformanceMonitor.Instance.CurrentFps
    /// （PerformanceMonitor 每 1s 采样并附带低帧率/内存告警），本类只负责节流显示与阈值着色。
    /// </summary>
    public class FPSCounter : MonoBehaviour
    {
        public bool editorOnly;

        [SerializeField] private float updateInterval = 1f;
        [SerializeField] private int targetFrameRate = 30;
        [SerializeField] private Anchor anchor;
        [SerializeField] private int xOffset;
        [SerializeField] private int yOffset;

        private enum Anchor
        {
            LeftTop,
            LeftBottom,
            RightTop,
            RightBottom
        }

        [SerializeField]
        private float idleTime = 2f;
        private float elapsed;

        private Color goodColor = new Color(0.5f, 1f, 0f);
        private Color okColor = new Color(1f, 0.8f, 0f);
        private Color badColor = new Color(1f, 0f, 0.25f);

        private float okFps;
        private float badFps;

        private TMP_Text fpsText;

        private void Awake()
        {
            if (editorOnly && !Application.isEditor) return;

            float percent = targetFrameRate / 100f;
            okFps = targetFrameRate - percent * 10;
            badFps = targetFrameRate - percent * 40;

            elapsed = updateInterval;

            // 自动创建 Canvas + TMP_Text 显示 FPS
            CreateFpsDisplay();
        }

        private void CreateFpsDisplay()
        {
            var canvasGo = new GameObject("FPSCanvas");
            canvasGo.transform.SetParent(transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

            var textGo = new GameObject("FPSText");
            textGo.transform.SetParent(canvasGo.transform, false);
            fpsText = textGo.AddComponent<TextMeshProUGUI>();
            fpsText.fontSize = 30;
            fpsText.color = goodColor;
            fpsText.text = "FPS: --";

            // 定位锚点
            var rt = fpsText.rectTransform;
            rt.sizeDelta = new Vector2(130, 40);
            rt.anchorMin = rt.anchorMax = rt.pivot = GetAnchorVector();
            rt.anchoredPosition = new Vector2(xOffset, yOffset);
        }

        private Vector2 GetAnchorVector()
        {
            return anchor switch
            {
                Anchor.LeftTop => new Vector2(0, 1),
                Anchor.LeftBottom => new Vector2(0, 0),
                Anchor.RightTop => new Vector2(1, 1),
                Anchor.RightBottom => new Vector2(1, 0),
                _ => new Vector2(0, 1),
            };
        }

        private void Update()
        {
            if (editorOnly && !Application.isEditor) return;

            if (idleTime > 0)
            {
                // unscaledDeltaTime：暂停（timeScale=0）期间倒计时与刷新不冻结
                idleTime -= Time.unscaledDeltaTime;
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= updateInterval)
            {
                elapsed = 0;

                // FPS 采样由 PerformanceMonitor 统一完成（1s 采样 + 告警），此处只读取结果做显示
                float fps = PerformanceMonitor.Instance.CurrentFps;

                if (fpsText != null)
                {
                    fpsText.text = ZString.Format("FPS: {0}", (int)fps);
                    fpsText.color = fps <= badFps ? badColor : (fps <= okFps ? okColor : goodColor);
                }
            }
        }

        private void OnDestroy()
        {
            if (fpsText != null)
                Destroy(fpsText.transform.parent?.gameObject);
        }
    }
}