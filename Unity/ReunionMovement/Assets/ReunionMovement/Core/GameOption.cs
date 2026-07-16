using ReunionMovement.Common;
using System;
using UnityEngine;
using ReunionMovement.Core.Sound;

namespace ReunionMovement.Core
{
    public static class GameOption
    {
        public class Option
        {
            // 版本号
            public string version = "1.0.0";
            // 全屏模式
            public bool fullscreen = true;
            // 分辨率宽度
            public int resolutionWidth = 1920;
            // 分辨率高度
            public int resolutionHeight = 1080;
            // 垂直同步
            public bool vsync = true;
            // 帧率
            public int framerate = 60;
            // 多语言支持
            public Multilingual language = Multilingual.ZH_CN;
            // 图形质量
            public int graphicsQuality = 2; // 0: 低, 1: 中, 2: 高
            // 亮度
            public float brightness = 1.0f;

            #region 声音
            // 自动暂停（默认关闭以避免加载时静音）
            public bool autoPause = false;
            // 主音量设置（默认不静音）
            public bool masterVolumeMuted = false;
            // 主音量
            public float masterVolume = 0.8f;
            // 音乐设置
            public bool musicMuted = false;
            // 音乐音量
            public float musicVolume = 0.5f;
            // 音效设置
            public bool sfxMuted = false;
            // 音效音量
            public float sfxVolume = 0.5f;
            //淡入淡出时间
            public float musicFadeTime = 2f;
            #endregion

            #region UI 输入
            // 键盘导航 —— 上
            public string uiNavUp = "w";
            // 键盘导航 —— 下
            public string uiNavDown = "s";
            // 键盘导航 —— 左
            public string uiNavLeft = "a";
            // 键盘导航 —— 右
            public string uiNavRight = "d";
            // 键盘提交/确认
            public string uiSubmit = "enter";
            // 键盘取消/返回
            public string uiCancel = "escape";
            // 切换到 UI 控制模式
            public string uiToggleToUI = "tab";
            // 退出 UI 控制模式
            public string uiToggleToGameplay = "escape";
            #endregion
        }

        public static Option currentOption = new Option();

        private static bool isLoaded = false;

        /// <summary>
        /// 加载游戏选项从 PlayerPrefs（默认仅首次加载，后续从内存读取）
        /// </summary>
        /// <param name="forceReload">强制重新从 PlayerPrefs 读取（例如恢复默认后重新加载）</param>
        public static void LoadOptions(bool forceReload = false)
        {
            if (isLoaded && !forceReload) return;
            isLoaded = true;

            currentOption.version = PlayerPrefs.GetString("version", currentOption.version);
            currentOption.fullscreen = PlayerPrefs.GetInt("fullscreen", currentOption.fullscreen ? 1 : 0) == 1;
            currentOption.resolutionWidth = PlayerPrefs.GetInt("resolutionWidth", currentOption.resolutionWidth);
            currentOption.resolutionHeight = PlayerPrefs.GetInt("resolutionHeight", currentOption.resolutionHeight);
            currentOption.vsync = PlayerPrefs.GetInt("vsync", currentOption.vsync ? 1 : 0) == 1;
            currentOption.framerate = PlayerPrefs.GetInt("framerate", currentOption.framerate);

            // 将 Multilingual 枚举转换为 string，再从 PlayerPrefs 获取后解析回枚举
            string langStr = PlayerPrefs.GetString("language", currentOption.language.ToString());
            if (Enum.TryParse<Multilingual>(langStr, out var langEnum))
            {
                currentOption.language = langEnum;
            }
            currentOption.graphicsQuality = PlayerPrefs.GetInt("graphicsQuality", currentOption.graphicsQuality);
            currentOption.brightness = PlayerPrefs.GetFloat("brightness", currentOption.brightness);

            // 声音设置
            currentOption.autoPause = PlayerPrefs.GetInt("autoPause", currentOption.autoPause ? 1 : 0) == 1;
            currentOption.masterVolumeMuted = PlayerPrefs.GetInt("masterVolumeMuted", currentOption.masterVolumeMuted ? 1 : 0) == 1;
            currentOption.masterVolume = PlayerPrefs.GetFloat("masterVolume", currentOption.masterVolume);
            currentOption.musicMuted = PlayerPrefs.GetInt("musicMuted", currentOption.musicMuted ? 1 : 0) == 1;
            currentOption.musicVolume = PlayerPrefs.GetFloat("musicVolume", currentOption.musicVolume);
            currentOption.musicFadeTime = PlayerPrefs.GetFloat("musicFadeTime", currentOption.musicFadeTime);
            currentOption.sfxMuted = PlayerPrefs.GetInt("sfxMuted", currentOption.sfxMuted ? 1 : 0) == 1;
            currentOption.sfxVolume = PlayerPrefs.GetFloat("sfxVolume", currentOption.sfxVolume);

            // UI 输入按键绑定
            currentOption.uiNavUp = PlayerPrefs.GetString("uiNavUp", currentOption.uiNavUp);
            currentOption.uiNavDown = PlayerPrefs.GetString("uiNavDown", currentOption.uiNavDown);
            currentOption.uiNavLeft = PlayerPrefs.GetString("uiNavLeft", currentOption.uiNavLeft);
            currentOption.uiNavRight = PlayerPrefs.GetString("uiNavRight", currentOption.uiNavRight);
            currentOption.uiSubmit = PlayerPrefs.GetString("uiSubmit", currentOption.uiSubmit);
            currentOption.uiCancel = PlayerPrefs.GetString("uiCancel", currentOption.uiCancel);
            currentOption.uiToggleToUI = PlayerPrefs.GetString("uiToggleToUI", currentOption.uiToggleToUI);
            currentOption.uiToggleToGameplay = PlayerPrefs.GetString("uiToggleToGameplay", currentOption.uiToggleToGameplay);

            // 读取完毕后立即应用到游戏（分辨率、音量、质量等）
            ApplyOptions();
        }

        /// <summary>
        /// 保存游戏选项到 PlayerPrefs
        /// </summary>
        public static void SaveOptions()
        {
            PlayerPrefs.SetString("version", currentOption.version);
            PlayerPrefs.SetInt("fullscreen", currentOption.fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("resolutionWidth", currentOption.resolutionWidth);
            PlayerPrefs.SetInt("resolutionHeight", currentOption.resolutionHeight);
            PlayerPrefs.SetInt("vsync", currentOption.vsync ? 1 : 0);
            PlayerPrefs.SetInt("framerate", currentOption.framerate);

            // 将 Multilingual 枚举转换为 string 存储
            PlayerPrefs.SetString("language", currentOption.language.ToString());
            PlayerPrefs.SetInt("graphicsQuality", currentOption.graphicsQuality);
            PlayerPrefs.SetFloat("brightness", currentOption.brightness);

            // 声音设置
            PlayerPrefs.SetInt("autoPause", currentOption.autoPause ? 1 : 0);
            PlayerPrefs.SetInt("masterVolumeMuted", currentOption.masterVolumeMuted ? 1 : 0);
            PlayerPrefs.SetFloat("masterVolume", currentOption.masterVolume);
            PlayerPrefs.SetInt("musicMuted", currentOption.musicMuted ? 1 : 0);
            PlayerPrefs.SetFloat("musicVolume", currentOption.musicVolume);
            PlayerPrefs.SetFloat("musicFadeTime", currentOption.musicFadeTime);
            PlayerPrefs.SetInt("sfxMuted", currentOption.sfxMuted ? 1 : 0);
            PlayerPrefs.SetFloat("sfxVolume", currentOption.sfxVolume);

            // UI 输入按键绑定
            PlayerPrefs.SetString("uiNavUp", currentOption.uiNavUp);
            PlayerPrefs.SetString("uiNavDown", currentOption.uiNavDown);
            PlayerPrefs.SetString("uiNavLeft", currentOption.uiNavLeft);
            PlayerPrefs.SetString("uiNavRight", currentOption.uiNavRight);
            PlayerPrefs.SetString("uiSubmit", currentOption.uiSubmit);
            PlayerPrefs.SetString("uiCancel", currentOption.uiCancel);
            PlayerPrefs.SetString("uiToggleToUI", currentOption.uiToggleToUI);
            PlayerPrefs.SetString("uiToggleToGameplay", currentOption.uiToggleToGameplay);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 将当前选项应用到游戏（分辨率、画质、音量等）
        /// </summary>
        public static void ApplyOptions()
        {
            try
            {
#if UNITY_WEBGL
                // WebGL 平台：分辨率由浏览器控制，Screen.SetResolution/QualitySettings.vSyncCount 不可用
                // 目标帧率在 WebGL 上由浏览器 requestAnimationFrame 控制
                Application.targetFrameRate = currentOption.framerate;
#else
                // 分辨率与全屏
                Screen.SetResolution(currentOption.resolutionWidth, currentOption.resolutionHeight, currentOption.fullscreen);

                // 垂直同步
                QualitySettings.vSyncCount = currentOption.vsync ? 1 : 0;

                // 目标帧率
                Application.targetFrameRate = currentOption.framerate;
#endif

                // 图形质量
                int qualityIndex = Mathf.Clamp(currentOption.graphicsQuality, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(qualityIndex, true);

                // 主音量（使用 AudioListener 作为全局主音量）
                AudioListener.volume = currentOption.masterVolumeMuted ? 0f : currentOption.masterVolume;

                // 自动暂停（如果为 true，启用 Unity 的 AudioListener.pause 行为；注意这会暂停所有音频）
                AudioListener.pause = currentOption.autoPause;

                // 应用音乐和音效设置到 SoundSystem（如果已初始化）
                var ss = SoundSystem.Instance;
                if (ss != null)
                {
                    // 将淡入淡出时间同步
                    try { ss.fadeDuration = currentOption.musicFadeTime; }
                    catch (Exception ex) { Log.Warning("同步淡入淡出时间失败: {0}", ex.Message); }

                    // 使用公共方法设置音乐属性（替代反射）
                    ss.SetMusicProperties(currentOption.musicVolume, currentOption.musicMuted);
                    ss.SetSfxProperties(currentOption.sfxVolume, currentOption.sfxMuted);
                }

                // 其它可扩展的应用（亮度等）：尝试设置全局 shader 属性 以便 shader 使用
                Shader.SetGlobalFloat("_GameBrightness", currentOption.brightness);
            }
            catch (Exception ex)
            {
                Log.Error("ApplyOptions 异常: {0}", ex);
            }
        }

        /// <summary>
        /// 获取单个选项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static T GetOption<T>(string key, T defaultValue)
        {
            return defaultValue switch
            {
                bool b => (T)(object)(PlayerPrefs.GetInt(key, b ? 1 : 0) == 1),
                int i => (T)(object)PlayerPrefs.GetInt(key, i),
                float f => (T)(object)PlayerPrefs.GetFloat(key, f),
                string s => (T)(object)PlayerPrefs.GetString(key, s),
                _ => throw new NotSupportedException($"不支持的类型: {typeof(T)}")
            };
        }

        /// <summary>
        /// 设置单个选项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="NotSupportedException"></exception>
        public static void SetOption<T>(string key, T value)
        {
            switch (value)
            {
                case bool b: PlayerPrefs.SetInt(key, b ? 1 : 0); break;
                case int i: PlayerPrefs.SetInt(key, i); break;
                case float f: PlayerPrefs.SetFloat(key, f); break;
                case string s: PlayerPrefs.SetString(key, s); break;
                default: throw new NotSupportedException($"不支持的类型: {typeof(T)}");
            }
        }

        /// <summary>
        /// 设置单个选项并立即应用
        /// </summary>
        public static void ApplyOption<T>(string key, T value)
        {
            // 更新内存中的 currentOption 对象 的常见键
            try
            {
                // 特殊处理常见字段，便于即时应用
                switch (key)
                {
                    case "fullscreen": currentOption.fullscreen = Convert.ToBoolean(value); break;
                    case "resolutionWidth": currentOption.resolutionWidth = Convert.ToInt32(value); break;
                    case "resolutionHeight": currentOption.resolutionHeight = Convert.ToInt32(value); break;
                    case "vsync": currentOption.vsync = Convert.ToBoolean(value); break;
                    case "framerate": currentOption.framerate = Convert.ToInt32(value); break;
                    case "graphicsQuality": currentOption.graphicsQuality = Convert.ToInt32(value); break;
                    case "brightness": currentOption.brightness = Convert.ToSingle(value); break;
                    case "autoPause": currentOption.autoPause = Convert.ToBoolean(value); break;
                    case "masterVolumeMuted": currentOption.masterVolumeMuted = Convert.ToBoolean(value); break;
                    case "masterVolume": currentOption.masterVolume = Convert.ToSingle(value); break;
                    case "musicMuted": currentOption.musicMuted = Convert.ToBoolean(value); break;
                    case "musicVolume": currentOption.musicVolume = Convert.ToSingle(value); break;
                    case "musicFadeTime": currentOption.musicFadeTime = Convert.ToSingle(value); break;
                    case "sfxMuted": currentOption.sfxMuted = Convert.ToBoolean(value); break;
                    case "sfxVolume": currentOption.sfxVolume = Convert.ToSingle(value); break;
                    case "language":
                        if (value is string s && Enum.TryParse<Multilingual>(s, out var le)) currentOption.language = le;
                        break;
                    default:
                        // 对于不在上面列表的 key，不在此重复 SetOption（下方统一调用一次）
                        break;
                }

                // 持久化并应用（SetOption 对所有 key 仅调用一次）
                SetOption(key, value);
                PlayerPrefs.Save();
                ApplyOptions();
            }
            catch (Exception ex)
            {
                Log.Error("ApplyOption 异常: {0}", ex);
            }
        }

        /// <summary>
        /// 重置游戏选项为默认值并保存/应用。
        /// 编辑器模式下可通过菜单调用：Tools/ReunionMovement/Reset Game Options
        /// </summary>
        public static void ResetOptions()
        {
            try
            {
                currentOption = new Option();
                SaveOptions();
                ApplyOptions();
            }
            catch (Exception ex)
            {
                Log.Error("ResetOptions 异常: {0}", ex);
            }
        }
    }
}