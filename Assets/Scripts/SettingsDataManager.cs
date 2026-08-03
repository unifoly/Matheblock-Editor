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

        // 画质默认等级：高（对应 QualitySettings 中的 High）
        private const string k_defaultQualityName = "High";

        // 画质字段在 Settings.json 中的键名，用于区分旧存档（无画质字段时保持默认）
        private const string k_qualityFieldName = "qualityLevel";

        // Fake Note 放置模式
        public const int FakeNoteModeToggle = 0; // 切换：按一次 Tab 开启，再按一次关闭
        public const int FakeNoteModeHold = 1;   // 按住：按住 Tab 时放置 Fake Note

        private static string s_filePath;

        // 序列化用数据类
        [Serializable]
        private class SettingsData
        {
            public float masterVolume = 1f;
            public float musicVolume = 1f;
            public float sfxVolume = 1f;
            public int resolutionIndex = 0;
            public int autoSaveMinutes = 10;
            public int qualityLevel = 3; // 默认高画质（QualitySettings 索引，索引 3 = High）
            public int fakeNoteMode = 0; // 默认切换模式（0=切换，1=按住）
        }

        public static float MasterVolume { get; set; } = 1f;
        public static float MusicVolume { get; set; } = 1f;
        public static float SFXVolume { get; set; } = 1f;
        public static int ResolutionIndex { get; set; } = 0;

        /// <summary>
        /// 画质等级索引（对应 QualitySettings.names），默认高画质
        /// </summary>
        public static int QualityLevel { get; set; } = FindQualityIndex(k_defaultQualityName);

        /// <summary>
        /// Fake Note 放置模式（0=切换，1=按住）
        /// </summary>
        public static int FakeNoteMode { get; set; } = FakeNoteModeToggle;

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
            // 静态构造内兜底 try-catch，避免文件 IO 异常触发 TypeInitializationException
            // 使后续所有静态成员永久不可用
            try
            {
                Load();
            }
            catch (Exception)
            {
            }
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
                    // 加载值范围校验：防止手改 JSON 写入越界值导致音量/分辨率异常
                    MasterVolume = Mathf.Clamp01(data.masterVolume);
                    MusicVolume = Mathf.Clamp01(data.musicVolume);
                    SFXVolume = Mathf.Clamp01(data.sfxVolume);
                    ResolutionIndex = Mathf.Clamp(data.resolutionIndex, 0, Mathf.Max(0, Screen.resolutions.Length - 1));
                    AutoSaveMinutes = Mathf.Max(0, data.autoSaveMinutes);

                    // 旧存档没有画质字段时保持默认（高画质），避免 JsonUtility 缺失字段回退为 0（极低画质）
                    QualityLevel = json.Contains(k_qualityFieldName)
                        ? Mathf.Clamp(data.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1))
                        : FindQualityIndex(k_defaultQualityName);

                    // 旧存档没有 fakeNoteMode 字段时由字段初始化器回退为默认（0=切换），这里仅做范围校验
                    FakeNoteMode = Mathf.Clamp(data.fakeNoteMode, FakeNoteModeToggle, FakeNoteModeHold);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 从旧版 PlayerPrefs 迁移数据
        /// </summary>
        private static void MigrateFromPlayerPrefs()
        {
            bool hasOldData = PlayerPrefs.HasKey("Settings_MasterVolume");

            if (!hasOldData)
            {
                return;
            }

            // 从 PlayerPrefs 读取旧数据
            MasterVolume = PlayerPrefs.GetFloat("Settings_MasterVolume", 1f);
            MusicVolume = PlayerPrefs.GetFloat("Settings_MusicVolume", 1f);
            SFXVolume = PlayerPrefs.GetFloat("Settings_SFXVolume", 1f);
            ResolutionIndex = PlayerPrefs.GetInt("Settings_ResolutionIndex", 0);

            // 保存到 JSON 文件
            Save();

            // 清理旧 PlayerPrefs 数据
            PlayerPrefs.DeleteKey("Settings_MasterVolume");
            PlayerPrefs.DeleteKey("Settings_MusicVolume");
            PlayerPrefs.DeleteKey("Settings_SFXVolume");
            PlayerPrefs.DeleteKey("Settings_ResolutionIndex");
            PlayerPrefs.Save();
        }

        public static void Save()
        {
            var data = new SettingsData
            {
                masterVolume = MasterVolume,
                musicVolume = MusicVolume,
                sfxVolume = SFXVolume,
                resolutionIndex = ResolutionIndex,
                autoSaveMinutes = AutoSaveMinutes,
                qualityLevel = QualityLevel,
                fakeNoteMode = FakeNoteMode
            };

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception)
            {
            }
        }

        public static void ApplySettings()
        {
            AudioListener.volume = MasterVolume;

            QualitySettings.SetQualityLevel(QualityLevel, true);
        }

        /// <summary>
        /// 查找指定名称的画质等级索引；未找到时回退为中档（索引 2）
        /// </summary>
        private static int FindQualityIndex(string name)
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                {
                    return i;
                }
            }

            return 2;
        }

        public static void ResetAll()
        {
            MasterVolume = 1f;
            MusicVolume = 1f;
            SFXVolume = 1f;
            ResolutionIndex = 0;
            AutoSaveMinutes = 10;
            QualityLevel = FindQualityIndex(k_defaultQualityName);
            FakeNoteMode = FakeNoteModeToggle;
            Save();
        }
    }
}
