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
            public string version = "0.1.0";

            public bool fullscreen = true;
            public int resolutionWidth = 1920;
            public int resolutionHeight = 1080;

            public bool bgmEnabled = true;
            public float bgmVolume = 0.5f;
            public bool sfxEnabled = true;
            public float sfxVolume = 0.5f;
        }

        public static Option CurrentOption = new Option();

        public static void LoadOptions()
        {
            CurrentOption.version = PlayerPrefs.GetString("version", CurrentOption.version);
            CurrentOption.fullscreen = PlayerPrefs.GetInt("fullscreen", CurrentOption.fullscreen ? 1 : 0) == 1;
            CurrentOption.resolutionWidth = PlayerPrefs.GetInt("resolutionWidth", CurrentOption.resolutionWidth);
            CurrentOption.resolutionHeight = PlayerPrefs.GetInt("resolutionHeight", CurrentOption.resolutionHeight);
            CurrentOption.bgmEnabled = PlayerPrefs.GetInt("bgmEnabled", CurrentOption.bgmEnabled ? 1 : 0) == 1;
            CurrentOption.bgmVolume = PlayerPrefs.GetFloat("bgmVolume", CurrentOption.bgmVolume);
            CurrentOption.sfxEnabled = PlayerPrefs.GetInt("sfxEnabled", CurrentOption.sfxEnabled ? 1 : 0) == 1;
            CurrentOption.sfxVolume = PlayerPrefs.GetFloat("sfxVolume", CurrentOption.sfxVolume);
        }

        public static void SaveOptions()
        {
            PlayerPrefs.SetString("version", CurrentOption.version);
            PlayerPrefs.SetInt("fullscreen", CurrentOption.fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("resolutionWidth", CurrentOption.resolutionWidth);
            PlayerPrefs.SetInt("resolutionHeight", CurrentOption.resolutionHeight);
            PlayerPrefs.SetInt("bgmEnabled", CurrentOption.bgmEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("bgmVolume", CurrentOption.bgmVolume);
            PlayerPrefs.SetInt("sfxEnabled", CurrentOption.sfxEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("sfxVolume", CurrentOption.sfxVolume);
            PlayerPrefs.Save();
        }
    }
}