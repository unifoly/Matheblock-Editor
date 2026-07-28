using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 设置数据持久化管理器（运行时，基于 PlayerPrefs）
    /// </summary>
    public static class SettingsDataManager
    {
        private const string k_masterVolumeKey = "Settings_MasterVolume";
        private const string k_musicVolumeKey = "Settings_MusicVolume";
        private const string k_sfxVolumeKey = "Settings_SFXVolume";
        private const string k_fullscreenKey = "Settings_Fullscreen";
        private const string k_qualityLevelKey = "Settings_QualityLevel";
        private const string k_resolutionIndexKey = "Settings_ResolutionIndex";

        private static bool s_initialized;

        public static float MasterVolume { get; set; } = 1f;
        public static float MusicVolume { get; set; } = 1f;
        public static float SFXVolume { get; set; } = 1f;
        public static bool IsFullscreen { get; set; } = true;
        public static int QualityLevel { get; set; } = 2;
        public static int ResolutionIndex { get; set; } = 0;

        static SettingsDataManager()
        {
            Load();
        }

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(k_masterVolumeKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(k_musicVolumeKey, 1f);
            SFXVolume = PlayerPrefs.GetFloat(k_sfxVolumeKey, 1f);
            IsFullscreen = PlayerPrefs.GetInt(k_fullscreenKey, 1) == 1;
            QualityLevel = PlayerPrefs.GetInt(k_qualityLevelKey, 2);
            ResolutionIndex = PlayerPrefs.GetInt(k_resolutionIndexKey, 0);

            ApplySettings();
            s_initialized = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(k_masterVolumeKey, MasterVolume);
            PlayerPrefs.SetFloat(k_musicVolumeKey, MusicVolume);
            PlayerPrefs.SetFloat(k_sfxVolumeKey, SFXVolume);
            PlayerPrefs.SetInt(k_fullscreenKey, IsFullscreen ? 1 : 0);
            PlayerPrefs.SetInt(k_qualityLevelKey, QualityLevel);
            PlayerPrefs.SetInt(k_resolutionIndexKey, ResolutionIndex);
            PlayerPrefs.Save();
        }

        public static void ApplySettings()
        {
            AudioListener.volume = MasterVolume;
            QualitySettings.SetQualityLevel(QualityLevel, true);
            Screen.fullScreen = IsFullscreen;
        }

        public static void ResetAll()
        {
            MasterVolume = 1f;
            MusicVolume = 1f;
            SFXVolume = 1f;
            IsFullscreen = true;
            QualityLevel = 2;
            ResolutionIndex = 0;
            Save();
        }
    }
}
