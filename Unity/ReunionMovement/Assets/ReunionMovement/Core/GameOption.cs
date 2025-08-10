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
            // 音乐设置
            public bool bgmEnabled = true;
            // 音乐音量
            public float bgmVolume = 0.5f;
            // 音效设置
            public bool sfxEnabled = true;
            // 音效音量
            public float sfxVolume = 0.5f;
            // 主音量设置
            public bool masterVolumeEnabled = true;
            // 主音量
            public float masterVolume = 0.8f;
            // 图形质量
            public int graphicsQuality = 2; // 0: Low, 1: Medium, 2: High
            // 亮度
            public float brightness = 1.0f;
        }

        public static Option CurrentOption = new Option();

        // 修复 LoadOptions 方法中 Multilingual 类型与 string 类型不兼容的问题
        public static void LoadOptions()
        {
            CurrentOption.version = PlayerPrefs.GetString("version", CurrentOption.version);
            CurrentOption.fullscreen = PlayerPrefs.GetInt("fullscreen", CurrentOption.fullscreen ? 1 : 0) == 1;
            CurrentOption.resolutionWidth = PlayerPrefs.GetInt("resolutionWidth", CurrentOption.resolutionWidth);
            CurrentOption.resolutionHeight = PlayerPrefs.GetInt("resolutionHeight", CurrentOption.resolutionHeight);
            CurrentOption.vsync = PlayerPrefs.GetInt("vsync", CurrentOption.vsync ? 1 : 0) == 1;
            CurrentOption.framerate = PlayerPrefs.GetInt("framerate", CurrentOption.framerate);
            // 将 Multilingual 枚举转换为 string，再从 PlayerPrefs 获取后解析回枚举
            string langStr = PlayerPrefs.GetString("language", CurrentOption.language.ToString());
            if (Enum.TryParse<Multilingual>(langStr, out var langEnum))
            {
                CurrentOption.language = langEnum;
            }
            CurrentOption.bgmEnabled = PlayerPrefs.GetInt("bgmEnabled", CurrentOption.bgmEnabled ? 1 : 0) == 1;
            CurrentOption.bgmVolume = PlayerPrefs.GetFloat("bgmVolume", CurrentOption.bgmVolume);
            CurrentOption.sfxEnabled = PlayerPrefs.GetInt("sfxEnabled", CurrentOption.sfxEnabled ? 1 : 0) == 1;
            CurrentOption.sfxVolume = PlayerPrefs.GetFloat("sfxVolume", CurrentOption.sfxVolume);
            CurrentOption.masterVolumeEnabled = PlayerPrefs.GetInt("masterVolumeEnabled", CurrentOption.masterVolumeEnabled ? 1 : 0) == 1;
            CurrentOption.masterVolume = PlayerPrefs.GetFloat("masterVolume", CurrentOption.masterVolume);
            CurrentOption.graphicsQuality = PlayerPrefs.GetInt("graphicsQuality", CurrentOption.graphicsQuality);
            CurrentOption.brightness = PlayerPrefs.GetFloat("brightness", CurrentOption.brightness);
        }

        // 修复 SaveOptions 方法中 Multilingual 类型与 string 类型不兼容的问题
        public static void SaveOptions()
        {
            PlayerPrefs.SetString("version", CurrentOption.version);
            PlayerPrefs.SetInt("fullscreen", CurrentOption.fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("resolutionWidth", CurrentOption.resolutionWidth);
            PlayerPrefs.SetInt("resolutionHeight", CurrentOption.resolutionHeight);
            PlayerPrefs.SetInt("vsync", CurrentOption.vsync ? 1 : 0);
            PlayerPrefs.SetInt("framerate", CurrentOption.framerate);
            // 将 Multilingual 枚举转换为 string 存储
            PlayerPrefs.SetString("language", CurrentOption.language.ToString());
            PlayerPrefs.SetInt("bgmEnabled", CurrentOption.bgmEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("bgmVolume", CurrentOption.bgmVolume);
            PlayerPrefs.SetInt("sfxEnabled", CurrentOption.sfxEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("sfxVolume", CurrentOption.sfxVolume);
            PlayerPrefs.SetInt("masterVolumeEnabled", CurrentOption.masterVolumeEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("masterVolume", CurrentOption.masterVolume);
            PlayerPrefs.SetInt("graphicsQuality", CurrentOption.graphicsQuality);
            PlayerPrefs.SetFloat("brightness", CurrentOption.brightness);
            PlayerPrefs.Save();
        }
    }
}