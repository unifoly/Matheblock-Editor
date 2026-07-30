using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 组合键结构体：修饰键 + 主键，支持解析、格式化和实时检测。
    /// </summary>
    [Serializable]
    public struct KeyCombo
    {
        [SerializeField] private bool m_ctrl;
        [SerializeField] private bool m_shift;
        [SerializeField] private bool m_alt;
        [SerializeField] private KeyCode m_mainKey;

        public bool Ctrl => m_ctrl;
        public bool Shift => m_shift;
        public bool Alt => m_alt;
        public KeyCode MainKey => m_mainKey;

        public KeyCombo(bool ctrl, bool shift, bool alt, KeyCode mainKey)
        {
            m_ctrl = ctrl;
            m_shift = shift;
            m_alt = alt;
            m_mainKey = mainKey;
        }

        /// <summary>
        /// 从显示字符串解析为 KeyCombo（如 "Ctrl + Shift + A"）
        /// </summary>
        public static KeyCombo Parse(string displayString)
        {
            if (string.IsNullOrWhiteSpace(displayString))
            {
                return default;
            }

            string trimmed = displayString.Trim();
            bool ctrl = false;
            bool shift = false;
            bool alt = false;
            KeyCode mainKey = KeyCode.None;

            // 按 " + " 分割
            string[] parts = trimmed.Split(new[] { " + " }, StringSplitOptions.None);

            foreach (string part in parts)
            {
                string p = part.Trim();

                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl = true;
                }
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    shift = true;
                }
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    alt = true;
                }
                else
                {
                    // 主键
                    mainKey = ParseKeyCode(p);
                }
            }

            return new KeyCombo(ctrl, shift, alt, mainKey);
        }

        /// <summary>
        /// 格式化为显示字符串（如 "Ctrl + Shift + A"）
        /// </summary>
        public string ToDisplayString()
        {
            if (m_mainKey == KeyCode.None)
            {
                return "None";
            }

            var sb = new StringBuilder();

            if (m_ctrl)
            {
                sb.Append("Ctrl + ");
            }

            if (m_shift)
            {
                sb.Append("Shift + ");
            }

            if (m_alt)
            {
                sb.Append("Alt + ");
            }

            sb.Append(FormatKeyCode(m_mainKey));
            return sb.ToString();
        }

        /// <summary>
        /// 检测此组合键是否在当前帧被触发。
        /// 修饰键要求精确匹配（不多不少），主键要求 GetKeyDown。
        /// 当主键本身就是修饰键时，仅检测该键的 GetKeyDown。
        /// </summary>
        public bool IsPressed()
        {
            if (m_mainKey == KeyCode.None)
            {
                return false;
            }

            // 主键是修饰键本身（无组合）：检测 GetKeyDown（左右皆可）
            if (IsModifierKey(m_mainKey))
            {
                return AnyKeyDown(GetModifierVariants(m_mainKey));
            }

            // 组合键：修饰键精确匹配 + 主键 GetKeyDown
            bool ctrlHeld = IsAnyModifierHeld(KeyCode.LeftControl, KeyCode.RightControl);
            bool shiftHeld = IsAnyModifierHeld(KeyCode.LeftShift, KeyCode.RightShift);
            bool altHeld = IsAnyModifierHeld(KeyCode.LeftAlt, KeyCode.RightAlt);

            // 精确匹配：指定的修饰键必须按下，未指定的不能按下
            if (m_ctrl != ctrlHeld) return false;
            if (m_shift != shiftHeld) return false;
            if (m_alt != altHeld) return false;

            return Input.GetKeyDown(m_mainKey);
        }

        public bool IsValid => m_mainKey != KeyCode.None;

        /// <summary>
        /// 判断按键是否为修饰键
        /// </summary>
        public static bool IsModifierKey(KeyCode key)
        {
            return key == KeyCode.LeftShift || key == KeyCode.RightShift
                   || key == KeyCode.LeftControl || key == KeyCode.RightControl
                   || key == KeyCode.LeftAlt || key == KeyCode.RightAlt
                   || key == KeyCode.LeftCommand || key == KeyCode.RightCommand
                   || key == KeyCode.LeftWindows || key == KeyCode.RightWindows;
        }

        private static bool IsAnyModifierHeld(KeyCode left, KeyCode right)
        {
            return Input.GetKey(left) || Input.GetKey(right);
        }

        private static bool AnyKeyDown(KeyCode[] keys)
        {
            foreach (KeyCode key in keys)
            {
                if (Input.GetKeyDown(key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 返回修饰键的左右变体（用于检测时同时匹配 Left/Right）
        /// </summary>
        private static KeyCode[] GetModifierVariants(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return new[] { KeyCode.LeftShift, KeyCode.RightShift };
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return new[] { KeyCode.LeftControl, KeyCode.RightControl };
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return new[] { KeyCode.LeftAlt, KeyCode.RightAlt };
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                    return new[] { KeyCode.LeftCommand, KeyCode.RightCommand };
                case KeyCode.LeftWindows:
                case KeyCode.RightWindows:
                    return new[] { KeyCode.LeftWindows, KeyCode.RightWindows };
                default:
                    return new[] { key };
            }
        }

        /// <summary>
        /// 将显示名解析为 KeyCode
        /// </summary>
        private static KeyCode ParseKeyCode(string name)
        {
            // 先尝试直接枚举解析（字母键如 "Q"、"A" 等可直接匹配）
            if (Enum.TryParse<KeyCode>(name, out KeyCode direct))
            {
                return direct;
            }

            // 处理特殊映射（与 RebindButton.FormatKeyName 对应）
            switch (name)
            {
                case "0": return KeyCode.Alpha0;
                case "1": return KeyCode.Alpha1;
                case "2": return KeyCode.Alpha2;
                case "3": return KeyCode.Alpha3;
                case "4": return KeyCode.Alpha4;
                case "5": return KeyCode.Alpha5;
                case "6": return KeyCode.Alpha6;
                case "7": return KeyCode.Alpha7;
                case "8": return KeyCode.Alpha8;
                case "9": return KeyCode.Alpha9;
                case "Enter": return KeyCode.Return;
                case "Esc": return KeyCode.Escape;
                case "Space": return KeyCode.Space;
                case "Tab": return KeyCode.Tab;
                case "CapsLock": return KeyCode.CapsLock;
                case "Shift": return KeyCode.LeftShift;
                case "Ctrl": return KeyCode.LeftControl;
                case "Alt": return KeyCode.LeftAlt;
                case "LWin": return KeyCode.LeftWindows;
                case "RWin": return KeyCode.RightWindows;
                case "↑": return KeyCode.UpArrow;
                case "↓": return KeyCode.DownArrow;
                case "←": return KeyCode.LeftArrow;
                case "->": return KeyCode.RightArrow;
                case "鼠标左键": return KeyCode.Mouse0;
                case "鼠标右键": return KeyCode.Mouse1;
                case "鼠标中键": return KeyCode.Mouse2;
                default: return KeyCode.None;
            }
        }

        /// <summary>
        /// 将 KeyCode 格式化为显示名（与 RebindButton.FormatKeyName 保持一致）
        /// </summary>
        public static string FormatKeyCode(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Delete: return "Delete";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: return "Alt";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "->";
                case KeyCode.Space: return "Space";
                case KeyCode.Tab: return "Tab";
                case KeyCode.CapsLock: return "CapsLock";
                case KeyCode.LeftWindows: return "LWin";
                case KeyCode.RightWindows: return "RWin";
                case KeyCode.Mouse0: return "鼠标左键";
                case KeyCode.Mouse1: return "鼠标右键";
                case KeyCode.Mouse2: return "鼠标中键";
                case KeyCode.Mouse3: return "Mouse4";
                case KeyCode.Mouse4: return "Mouse5";
                case KeyCode.Mouse5: return "Mouse6";
                case KeyCode.Mouse6: return "Mouse7";
                default: return key.ToString();
            }
        }
    }

    /// <summary>
    /// 快捷键绑定持久化存储。
    /// 使用 JSON 文件持久化到 Application.persistentDataPath，支持组合键。
    /// </summary>
    public static class KeyBindingsStore
    {
        private const string k_fileName = "KeyBindings.json";

        private static Dictionary<string, KeyCombo> s_bindings;
        private static string s_filePath;

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

        /// <summary>
        /// 获取某个操作的组合键绑定，若无则返回默认值
        /// </summary>
        public static KeyCombo GetKeyCombo(string actionName, KeyCombo defaultCombo)
        {
            EnsureLoaded();

            if (s_bindings.TryGetValue(actionName, out KeyCombo saved))
            {
                return saved;
            }

            return defaultCombo;
        }

        /// <summary>
        /// 获取某个操作的快捷键显示名，若无则返回默认值
        /// </summary>
        public static string GetBinding(string actionName, string defaultKey)
        {
            EnsureLoaded();

            if (s_bindings.TryGetValue(actionName, out KeyCombo saved))
            {
                return saved.ToDisplayString();
            }

            return defaultKey;
        }

        /// <summary>
        /// 设置/更新某个操作的快捷键并保存
        /// </summary>
        public static void SetBinding(string actionName, string keyName)
        {
            EnsureLoaded();

            KeyCombo combo = KeyCombo.Parse(keyName);
            s_bindings[actionName] = combo;
            Save();
        }

        /// <summary>
        /// 设置/更新某个操作的组合键并保存
        /// </summary>
        public static void SetKeyCombo(string actionName, KeyCombo combo)
        {
            EnsureLoaded();

            s_bindings[actionName] = combo;
            Save();
        }

        /// <summary>
        /// 清除所有自定义绑定（恢复默认）
        /// </summary>
        public static void ResetAll()
        {
            s_bindings = new Dictionary<string, KeyCombo>();

            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KeyBindingsStore] 删除文件失败: {ex.Message}");
            }

            // 同时清理旧的 PlayerPrefs 数据（兼容迁移）
            PlayerPrefs.DeleteKey("HexMap_KeyBindings");
            PlayerPrefs.Save();
        }

        private static void EnsureLoaded()
        {
            if (s_bindings == null)
            {
                Load();
            }
        }

        private static void Load()
        {
            s_bindings = new Dictionary<string, KeyCombo>();

            // 优先从 JSON 文件加载
            if (File.Exists(FilePath))
            {
                LoadFromJson();
                return;
            }

            // 兼容旧版 PlayerPrefs 数据：尝试迁移
            MigrateFromPlayerPrefs();
        }

        /// <summary>
        /// 从 JSON 文件加载绑定
        /// </summary>
        private static void LoadFromJson()
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<BindingsData>(json);

                if (data?.entries != null)
                {
                    foreach (var entry in data.entries)
                    {
                        if (!string.IsNullOrEmpty(entry.actionName))
                        {
                            s_bindings[entry.actionName] = entry.combo;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KeyBindingsStore] 加载 JSON 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从旧版 PlayerPrefs 格式迁移数据
        /// </summary>
        private static void MigrateFromPlayerPrefs()
        {
            string raw = PlayerPrefs.GetString("HexMap_KeyBindings", "");

            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            // 旧格式：actionName|keyName\nactionName|keyName
            string[] lines = raw.Split('\n');
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                if (parts.Length == 2 && !string.IsNullOrEmpty(parts[0]))
                {
                    s_bindings[parts[0]] = KeyCombo.Parse(parts[1]);
                }
            }

            // 迁移完成后保存到 JSON 并清理 PlayerPrefs
            if (s_bindings.Count > 0)
            {
                Save();
                PlayerPrefs.DeleteKey("HexMap_KeyBindings");
                PlayerPrefs.Save();
                Debug.Log("[KeyBindingsStore] 已从 PlayerPrefs 迁移到 JSON 文件");
            }
        }

        private static void Save()
        {
            var data = new BindingsData
            {
                entries = new List<BindingEntry>(s_bindings.Count)
            };

            foreach (var kvp in s_bindings)
            {
                data.entries.Add(new BindingEntry
                {
                    actionName = kvp.Key,
                    combo = kvp.Value
                });
            }

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KeyBindingsStore] 保存 JSON 失败: {ex.Message}");
            }
        }

        // ---- JSON 序列化用类 ----

        [Serializable]
        private class BindingsData
        {
            public List<BindingEntry> entries;
        }

        [Serializable]
        private class BindingEntry
        {
            public string actionName;
            public KeyCombo combo;
        }
    }
}
