using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// FPS计数器工具
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

        private Rect rect;
        private GUIStyle style;

        private void Awake()
        {
            if (editorOnly && !Application.isEditor) return;

            float percent = targetFrameRate / 100f;
            okFps = targetFrameRate - percent * 10;
            badFps = targetFrameRate - percent * 40;

            int linesHeight = 40;
            int linesWidth = 130;
            int xPos = (anchor == Anchor.RightTop || anchor == Anchor.RightBottom) ? Screen.width - linesWidth : 0;
            int yPos = (anchor == Anchor.LeftBottom || anchor == Anchor.RightBottom) ? Screen.height - linesHeight : 0;
            xPos += xOffset;
            yPos += yOffset;
            rect = new Rect(xPos, yPos, linesWidth, linesHeight);

            style = new GUIStyle
            {
                fontSize = 30,
                normal = { textColor = goodColor }
            };

            elapsed = updateInterval;
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
            }
        }

        private void OnGUI()
        {
            if (editorOnly && !Application.isEditor) return;

            style.normal.textColor = fps <= badFps ? badColor : (fps <= okFps ? okColor : goodColor);
            GUI.Label(rect, $"FPS: {(int)fps}", style);
        }
    }
}