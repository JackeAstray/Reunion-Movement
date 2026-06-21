using UnityEngine;
using TMPro;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// FPS计数器工具（使用 TextMeshPro 替代旧版 OnGUI）
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

        private float idleTime = 2f;
        private float elapsed;
        private int frames;
        private float fps;

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
                idleTime -= Time.deltaTime;
                return;
            }

            elapsed += Time.deltaTime;
            ++frames;

            if (elapsed >= updateInterval)
            {
                fps = frames / elapsed;
                elapsed = 0;
                frames = 0;

                if (fpsText != null)
                {
                    fpsText.text = $"FPS: {(int)fps}";
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