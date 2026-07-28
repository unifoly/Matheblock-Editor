using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 快捷键绑定持久化存储。
    /// 运行时通过 PlayerPrefs 存取；编辑器下同时写一份到 Assets/Settings/KeyBindings.json 供 ShortcutKeyPageBuilder 读取。
    /// </summary>
    public static class KeyBindingsStore
    {
        private const string k_playerPrefsKey = "HexMap_KeyBindings";
        private const string k_editorFolder = "Settings";
        private const string k_editorFileName = "KeyBindings.json";
        private const char k_entrySeparator = '\n';
        private const char k_fieldSeparator = '|';

        private static Dictionary<string, string> s_bindings;

        /// <summary>
        /// 获取某个操作的快捷键，若无则返回默认值
        /// </summary>
        public static string GetBinding(string actionName, string defaultKey)
        {
            if (s_bindings == null)
            {
                Load();
            }

            if (s_bindings.TryGetValue(actionName, out string saved))
            {
                return saved;
            }

            return defaultKey;
        }

        /// <summary>
        /// 设置/更新某个操作的快捷键并保存
        /// </summary>
        public static void SetBinding(string actionName, string keyName)
        {
            if (s_bindings == null)
            {
                Load();
            }

            s_bindings[actionName] = keyName;
            Save();
        }

        /// <summary>
        /// 清除所有自定义绑定（恢复默认）
        /// </summary>
        public static void ResetAll()
        {
            s_bindings = new Dictionary<string, string>();
            PlayerPrefs.DeleteKey(k_playerPrefsKey);
            PlayerPrefs.Save();
            DeleteEditorFile();
        }

        private static void Load()
        {
            s_bindings = new Dictionary<string, string>();
            string raw = PlayerPrefs.GetString(k_playerPrefsKey, "");

            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] lines = raw.Split(k_entrySeparator);
            foreach (string line in lines)
            {
                string[] parts = line.Split(k_fieldSeparator);
                if (parts.Length == 2 && !string.IsNullOrEmpty(parts[0]))
                {
                    s_bindings[parts[0]] = parts[1];
                }
            }
        }

        private static void Save()
        {
            var sb = new StringBuilder();
            bool first = true;

            foreach (var kvp in s_bindings)
            {
                if (!first)
                {
                    sb.Append(k_entrySeparator);
                }

                sb.Append(kvp.Key);
                sb.Append(k_fieldSeparator);
                sb.Append(kvp.Value);
                first = false;
            }

            string raw = sb.ToString();
            PlayerPrefs.SetString(k_playerPrefsKey, raw);
            PlayerPrefs.Save();
            WriteEditorFile(raw);
        }

        /// <summary>
        /// 编辑器下将原始数据导出到 Assets/Settings/KeyBindings.json 供 ShortcutKeyPageBuilder 读取
        /// </summary>
        private static void WriteEditorFile(string rawData)
        {
#if UNITY_EDITOR
            try
            {
                string dir = System.IO.Path.Combine(UnityEngine.Application.dataPath, k_editorFolder);
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                string path = System.IO.Path.Combine(dir, k_editorFileName);
                System.IO.File.WriteAllText(path, rawData);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KeyBindingsStore] 写入 Editor 文件失败: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// 编辑器下从文件加载原始数据
        /// </summary>
        public static string LoadEditorRaw()
        {
#if UNITY_EDITOR
            try
            {
                string path = System.IO.Path.Combine(UnityEngine.Application.dataPath, k_editorFolder, k_editorFileName);
                if (System.IO.File.Exists(path))
                {
                    return System.IO.File.ReadAllText(path);
                }
            }
            catch { }
#endif
            return "";
        }

        private static void DeleteEditorFile()
        {
#if UNITY_EDITOR
            try
            {
                string path = System.IO.Path.Combine(UnityEngine.Application.dataPath, k_editorFolder, k_editorFileName);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch { }
#endif
        }
    }
}
