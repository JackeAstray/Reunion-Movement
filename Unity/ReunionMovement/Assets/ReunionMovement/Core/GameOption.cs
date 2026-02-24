using ReunionMovement.Common;
using System;
using System.Reflection;
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
            public int graphicsQuality = 2; // 0: Low, 1: Medium, 2: High
            // 亮度
            public float brightness = 1.0f;

            #region 声音
            //自动暂停（默认关以避免加载时静音）
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
        }

        public static Option currentOption = new Option();

        /// <summary>
        /// 加载游戏选项从 PlayerPrefs
        /// </summary>
        public static void LoadOptions()
        {
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

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 将当前选项应用到游戏（分辨率、画质、音量等）
        /// </summary>
        public static void ApplyOptions()
        {
            try
            {
                // 分辨率与全屏
                Screen.SetResolution(currentOption.resolutionWidth, currentOption.resolutionHeight, currentOption.fullscreen);

                // 垂直同步
                QualitySettings.vSyncCount = currentOption.vsync ? 1 : 0;

                // 目标帧率
                Application.targetFrameRate = currentOption.framerate;

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
                    try { ss.fadeDuration = currentOption.musicFadeTime; } catch { }

                    // 通过反射获取私有 AudioSource 并立即应用静音/音量/loop 等
                    var field = ss.GetType().GetField("source", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var audio = field.GetValue(ss) as AudioSource;
                        if (audio != null)
                        {
                            audio.mute = currentOption.musicMuted;
                            audio.volume = currentOption.musicVolume;
                        }
                    }
                }

                // 其它可扩展的应用（亮度等）：尝试设置全局 shader 属性 以便 shader 使用
                Shader.SetGlobalFloat("_GameBrightness", currentOption.brightness);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ApplyOptions 异常: {ex}");
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
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)(PlayerPrefs.GetInt(key, (bool)(object)defaultValue ? 1 : 0) == 1);
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)PlayerPrefs.GetInt(key, (int)(object)defaultValue);
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)PlayerPrefs.GetFloat(key, (float)(object)defaultValue);
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)PlayerPrefs.GetString(key, (string)(object)defaultValue);
            }
            else
            {
                throw new NotSupportedException($"不支持的类型: {typeof(T)}");
            }
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
            if (typeof(T) == typeof(bool))
            {
                PlayerPrefs.SetInt(key, (bool)(object)value ? 1 : 0);
            }
            else if (typeof(T) == typeof(int))
            {
                PlayerPrefs.SetInt(key, (int)(object)value);
            }
            else if (typeof(T) == typeof(float))
            {
                PlayerPrefs.SetFloat(key, (float)(object)value);
            }
            else if (typeof(T) == typeof(string))
            {
                PlayerPrefs.SetString(key, (string)(object)value);
            }
            else
            {
                throw new NotSupportedException($"不支持的类型: {typeof(T)}");
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
                        // 对于不在上面列表的 key，只是写入 PlayerPrefs
                        SetOption(key, value);
                        break;
                }

                // 持久化并应用
                SetOption(key, value);
                PlayerPrefs.Save();
                ApplyOptions();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ApplyOption 异常: {ex}");
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
                Debug.LogError($"ResetOptions 异常: {ex}");
            }
        }
    }
}