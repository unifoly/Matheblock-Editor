using System;
using System.IO;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 设置数据持久化管理器（运行时，基于 JSON 文件）
    /// 持久化到 Application.persistentDataPath/Settings.json
    /// </summary>
    public static class SettingsDataManager
    {
        private const string k_fileName = "Settings.json";

        private static string s_filePath;
        private static bool s_initialized;

        // 序列化用数据类
        [Serializable]
        private class SettingsData
        {
            public float masterVolume = 1f;
            public float musicVolume = 1f;
            public float sfxVolume = 1f;
            public bool isFullscreen = true;
            public int qualityLevel = 2;
            public int resolutionIndex = 0;
            public int autoSaveMinutes = 10;
        }

        public static float MasterVolume { get; set; } = 1f;
        public static float MusicVolume { get; set; } = 1f;
        public static float SFXVolume { get; set; } = 1f;
        public static bool IsFullscreen { get; set; } = true;
        public static int QualityLevel { get; set; } = 2;
        public static int ResolutionIndex { get; set; } = 0;

        /// <summary>
        /// 自动保存间隔（分钟），0 表示关闭自动保存
        /// </summary>
        public static int AutoSaveMinutes { get; set; } = 10;

        /// <summary>
        /// 持久化文件完整路径
        /// </summary>
        private static string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(s_filePath))
                {
                    s_filePath = Path.Combine(Application.persistentDataPath, k_fileName);
                }

                return s_filePath;
            }
        }

        static SettingsDataManager()
        {
            Load();
        }

        public static void Load()
        {
            // 优先从 JSON 文件加载
            if (File.Exists(FilePath))
            {
                LoadFromJson();
            }
            else
            {
                // 兼容旧版 PlayerPrefs 数据：尝试迁移
                MigrateFromPlayerPrefs();
            }

            ApplySettings();
            s_initialized = true;
        }

        /// <summary>
        /// 从 JSON 文件加载设置
        /// </summary>
        private static void LoadFromJson()
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SettingsData>(json);

                if (data != null)
                {
                    MasterVolume = data.masterVolume;
                    MusicVolume = data.musicVolume;
                    SFXVolume = data.sfxVolume;
                    IsFullscreen = data.isFullscreen;
                    QualityLevel = data.qualityLevel;
                    ResolutionIndex = data.resolutionIndex;
                    AutoSaveMinutes = data.autoSaveMinutes;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsDataManager] 加载 JSON 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从旧版 PlayerPrefs 迁移数据
        /// </summary>
        private static void MigrateFromPlayerPrefs()
        {
            bool hasOldData = PlayerPrefs.HasKey("Settings_MasterVolume")
                              || PlayerPrefs.HasKey("Settings_Fullscreen");

            if (!hasOldData)
            {
                return;
            }

            // 从 PlayerPrefs 读取旧数据
            MasterVolume = PlayerPrefs.GetFloat("Settings_MasterVolume", 1f);
            MusicVolume = PlayerPrefs.GetFloat("Settings_MusicVolume", 1f);
            SFXVolume = PlayerPrefs.GetFloat("Settings_SFXVolume", 1f);
            IsFullscreen = PlayerPrefs.GetInt("Settings_Fullscreen", 1) == 1;
            QualityLevel = PlayerPrefs.GetInt("Settings_QualityLevel", 2);
            ResolutionIndex = PlayerPrefs.GetInt("Settings_ResolutionIndex", 0);

            // 保存到 JSON 文件
            Save();

            // 清理旧 PlayerPrefs 数据
            PlayerPrefs.DeleteKey("Settings_MasterVolume");
            PlayerPrefs.DeleteKey("Settings_MusicVolume");
            PlayerPrefs.DeleteKey("Settings_SFXVolume");
            PlayerPrefs.DeleteKey("Settings_Fullscreen");
            PlayerPrefs.DeleteKey("Settings_QualityLevel");
            PlayerPrefs.DeleteKey("Settings_ResolutionIndex");
            PlayerPrefs.Save();

            Debug.Log("[SettingsDataManager] 已从 PlayerPrefs 迁移到 JSON 文件");
        }

        public static void Save()
        {
            var data = new SettingsData
            {
                masterVolume = MasterVolume,
                musicVolume = MusicVolume,
                sfxVolume = SFXVolume,
                isFullscreen = IsFullscreen,
                qualityLevel = QualityLevel,
                resolutionIndex = ResolutionIndex,
                autoSaveMinutes = AutoSaveMinutes
            };

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsDataManager] 保存 JSON 失败: {ex.Message}");
            }
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
            AutoSaveMinutes = 10;
            Save();
        }
    }
}
