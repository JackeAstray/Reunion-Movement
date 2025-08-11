using ReunionMovement.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Core
{
    public static class GameOption
    {
        public class Option 
        {
            // 版本号
            public string version = "0.1.0";
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
            //自动暂停
            public bool autoPause = true;
            // 主音量设置
            public bool masterVolumeMuted = true;
            // 主音量
            public float masterVolume = 0.8f;
            // 音乐设置
            public bool musicMuted = true;
            // 音乐音量
            public float musicVolume = 0.5f;
            // 音效设置
            public bool sfxMuted = true;
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
    }
}