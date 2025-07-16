using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 分辨率管理
    /// </summary>
    public class ResolutionMgr : SingletonMgr<ResolutionMgr>
    {
        public enum AspectRatio
        {
            AspectRatio_2_1,    // 2:1
            AspectRatio_3_2,    // 3:2
            AspectRatio_4_3,
            AspectRatio_5_4,    // 5:4
            AspectRatio_16_9,
            AspectRatio_16_10,
            AspectRatio_18_9,   // 18:9
            AspectRatio_21_9,
            AspectRatio_32_9,
        }

        // 可配置参数
        [Header("分辨率设置")]
        public AspectRatio aspectRatio = AspectRatio.AspectRatio_16_9;
        public bool fixedAspectRatio = true;

        // 目标纵横比
        public float targetAspectRatio { get; private set; } = 4f / 3f;
        // 窗口纵横比
        public float windowedAspectRatio { get; private set; } = 4f / 3f;

        // 预定义分辨率宽度
        private static readonly int[] predefinedWidths =
        {
            600, 720, 800, 900, 1024, 1280, 1400, 1440, 1600, 1680, 1920, 2048, 2560, 2880, 3440, 3840, 5120, 7680
        };
        private const float maxResolutionRatio = 0.8f;
        private const float halfResolutionRatio = 0.5f;

        public Resolution displayResolution { get; private set; }
        public List<Vector2> windowedResolutions { get; private set; }
        public List<Vector2> fullscreenResolutions { get; private set; }

        private int currWindowedRes;
        private int currFullscreenRes;

        private void Start()
        {
            SetAspectRatio(aspectRatio);
            StartCoroutine(InitRoutine());
        }

        /// <summary>
        /// 设置纵横比
        /// </summary>
        private void SetAspectRatio(AspectRatio aspect)
        {
            float ratio = aspect switch
            {
                AspectRatio.AspectRatio_2_1 => 2f / 1f,
                AspectRatio.AspectRatio_3_2 => 3f / 2f,
                AspectRatio.AspectRatio_4_3 => 4f / 3f,
                AspectRatio.AspectRatio_5_4 => 5f / 4f,
                AspectRatio.AspectRatio_16_9 => 16f / 9f,
                AspectRatio.AspectRatio_16_10 => 16f / 10f,
                AspectRatio.AspectRatio_18_9 => 18f / 9f,
                AspectRatio.AspectRatio_21_9 => 21f / 9f,
                AspectRatio.AspectRatio_32_9 => 32f / 9f,
                _ => 16f / 9f
            };
            targetAspectRatio = ratio;
            windowedAspectRatio = ratio;
        }

        /// <summary>
        /// 初始化协程
        /// </summary>
        private IEnumerator InitRoutine()
        {
            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                displayResolution = Screen.currentResolution;
            }
            else
            {
                if (Screen.fullScreen)
                {
                    var r = Screen.currentResolution;
                    Screen.fullScreen = false;
                    yield return null; yield return null;
                    displayResolution = Screen.currentResolution;
                    Screen.SetResolution(r.width, r.height, true);
                    yield return null;
                }
                else
                {
                    displayResolution = Screen.currentResolution;
                }
            }
            InitResolutions();
        }

        /// <summary>
        /// 初始化分辨率列表
        /// </summary>
        private void InitResolutions()
        {
            float screenAspect = (float)displayResolution.width / displayResolution.height;
            windowedResolutions = new List<Vector2>();
            fullscreenResolutions = new List<Vector2>();

            foreach (int w in predefinedWidths)
            {
                if (w < displayResolution.width * maxResolutionRatio)
                {
                    AddResolution(w, screenAspect);
                }
            }

            // 添加当前显示分辨率和一半分辨率
            AddUniqueResolution(fullscreenResolutions, new Vector2(displayResolution.width, displayResolution.height));
            Vector2 halfNative = new Vector2(displayResolution.width * halfResolutionRatio, displayResolution.height * halfResolutionRatio);
            if (halfNative.x > predefinedWidths[0])
            {
                AddUniqueResolution(fullscreenResolutions, halfNative);
            }

            fullscreenResolutions = fullscreenResolutions.OrderBy(r => r.x).ToList();

            bool found = false;
            if (Screen.fullScreen)
            {
                currWindowedRes = windowedResolutions.Count - 1;
                for (int i = 0; i < fullscreenResolutions.Count; i++)
                {
                    if (Mathf.Approximately(fullscreenResolutions[i].x, Screen.width) &&
                        Mathf.Approximately(fullscreenResolutions[i].y, Screen.height))
                    {
                        currFullscreenRes = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    SetResolution(fullscreenResolutions.Count - 1, true);
                }
            }
            else
            {
                currFullscreenRes = fullscreenResolutions.Count - 1;
                for (int i = 0; i < windowedResolutions.Count; i++)
                {
                    if (Mathf.Approximately(windowedResolutions[i].x, Screen.width) &&
                        Mathf.Approximately(windowedResolutions[i].y, Screen.height))
                    {
                        currWindowedRes = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    SetResolution(windowedResolutions.Count - 1, false);
            }
        }

        /// <summary>
        /// 添加分辨率到列表（避免重复）
        /// </summary>
        private void AddUniqueResolution(List<Vector2> list, Vector2 res)
        {
            if (!list.Any(r => Mathf.Approximately(r.x, res.x) && Mathf.Approximately(r.y, res.y)))
            {
                list.Add(res);
            }
        }

        /// <summary>
        /// 添加分辨率
        /// </summary>
        private void AddResolution(int width, float screenAspect)
        {
            float heightWindowed = Mathf.Round(width / (fixedAspectRatio ? targetAspectRatio : windowedAspectRatio));
            Vector2 windowed = new Vector2(width, heightWindowed);
            if (windowed.y < displayResolution.height * maxResolutionRatio)
            {
                AddUniqueResolution(windowedResolutions, windowed);
            }

            float heightFullscreen = Mathf.Round(width / screenAspect);
            AddUniqueResolution(fullscreenResolutions, new Vector2(width, heightFullscreen));
        }

        /// <summary>
        /// 设置分辨率
        /// </summary>
        public void SetResolution(int index, bool fullscreen)
        {
            Vector2 r = fullscreen
                ? fullscreenResolutions[currFullscreenRes = index]
                : windowedResolutions[currWindowedRes = index];

            bool fullscreen2windowed = Screen.fullScreen && !fullscreen;
            Screen.SetResolution((int)r.x, (int)r.y, fullscreen);

            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                StopAllCoroutines();
                if (fullscreen2windowed)
                    StartCoroutine(SetResolutionAfterResize(r));
            }
        }

        /// <summary>
        /// 分辨率切换后修正（Mac专用）
        /// </summary>
        private IEnumerator SetResolutionAfterResize(Vector2 r)
        {
            int maxTime = 5;
            float startTime = Time.time;
            int lastW = Screen.width, lastH = Screen.height;
            yield return null; yield return null;

            while (Time.time - startTime < maxTime)
            {
                if (lastW != Screen.width || lastH != Screen.height)
                {
                    Screen.SetResolution((int)r.x, (int)r.y, Screen.fullScreen);
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>
        /// 切换全屏
        /// </summary>
        public void ToggleFullscreen()
        {
            SetResolution(Screen.fullScreen ? currWindowedRes : currFullscreenRes, !Screen.fullScreen);
        }

        /// <summary>
        /// 获取当前分辨率
        /// </summary>
        public Vector2 GetCurrentResolution() => new Vector2(Screen.width, Screen.height);

        /// <summary>
        /// 获取当前纵横比
        /// </summary>
        public float GetCurrentAspectRatio() => (float)Screen.width / Screen.height;
    }
}