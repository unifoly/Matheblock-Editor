using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexMap
{
    /// <summary>
    /// 快捷键重绑按钮行为：点击后等待按键，将按下的组合键名显示到关联的 KeyLabel。
    /// 启动时自动加载已保存的绑定并显示。
    /// </summary>
    public class RebindButton : MonoBehaviour
    {
        private const float k_timeoutSeconds = 5f;

        [SerializeField] private TextMeshProUGUI m_keyDisplay;
        [SerializeField] private string m_actionName;
        [SerializeField] private string m_defaultKey;

        private bool m_isRebinding;
        private Button m_button;

        public TextMeshProUGUI KeyDisplay
        {
            get => m_keyDisplay;
            set => m_keyDisplay = value;
        }

        /// <summary>
        /// 绑定的操作名（如 "拖拽模式"、"桥" 等），用于持久化存储
        /// </summary>
        public string ActionName
        {
            get => m_actionName;
            set => m_actionName = value;
        }

        public string DefaultKey
        {
            get => m_defaultKey;
            set => m_defaultKey = value;
        }

        /// <summary>
        /// 恢复默认按键并清除已保存的绑定
        /// </summary>
        public void ResetToDefault()
        {
            if (m_keyDisplay != null)
            {
                m_keyDisplay.text = m_defaultKey;
            }

            if (!string.IsNullOrEmpty(m_actionName))
            {
                KeyBindingsStore.SetBinding(m_actionName, m_defaultKey);
            }
        }

        private void Awake()
        {
            m_button = GetComponent<Button>();
            if (m_button != null)
            {
                m_button.onClick.AddListener(OnRebindClick);
            }
        }

        private void Start()
        {
            // 启动时加载已保存的绑定并显示
            LoadSavedBinding();
        }

        /// <summary>
        /// 从 KeyBindingsStore 加载已保存的绑定，若无则显示默认值
        /// </summary>
        private void LoadSavedBinding()
        {
            if (m_keyDisplay == null || string.IsNullOrEmpty(m_actionName))
            {
                return;
            }

            string savedKey = KeyBindingsStore.GetBinding(m_actionName, m_defaultKey);
            m_keyDisplay.text = savedKey;
        }

        public void OnRebindClick()
        {
            if (m_isRebinding) return;
            StartCoroutine(DoRebind());
        }

        private IEnumerator DoRebind()
        {
            m_isRebinding = true;

            if (m_keyDisplay == null)
            {
                m_isRebinding = false;
                yield break;
            }

            string originalText = m_keyDisplay.text;
            m_keyDisplay.text = "等待按键...";

            // 跳过当前帧，避免误捕获鼠标点击
            yield return null;

            float elapsed = 0f;

            while (elapsed < k_timeoutSeconds)
            {
                // 检测键盘按键（含修饰键）
                if (Input.anyKeyDown)
                {
                    KeyCode pressed = DetectPressedKey();

                    // Esc 视为取消，不保存并恢复原绑定
                    if (pressed == KeyCode.Escape)
                    {
                        m_keyDisplay.text = originalText;
                        m_isRebinding = false;
                        yield break;
                    }

                    if (pressed != KeyCode.None)
                    {
                        string keyName = BuildCombinedKeyName(pressed);
                        m_keyDisplay.text = keyName;

                        // 持久化保存
                        if (!string.IsNullOrEmpty(m_actionName))
                        {
                            KeyBindingsStore.SetBinding(m_actionName, keyName);
                        }

                        m_isRebinding = false;
                        yield break;
                    }
                }

                // 检测鼠标按键（anyKeyDown 不包含鼠标）
                KeyCode mouseKey = DetectMouseKey();
                if (mouseKey != KeyCode.None)
                {
                    string keyName = BuildCombinedKeyName(mouseKey);
                    m_keyDisplay.text = keyName;

                    // 持久化保存
                    if (!string.IsNullOrEmpty(m_actionName))
                    {
                        KeyBindingsStore.SetBinding(m_actionName, keyName);
                    }

                    m_isRebinding = false;
                    yield break;
                }

                // 检测鼠标滚轮
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    string wheelName = BuildWheelName(wheel > 0f);
                    m_keyDisplay.text = wheelName;

                    if (!string.IsNullOrEmpty(m_actionName))
                    {
                        KeyBindingsStore.SetBinding(m_actionName, wheelName);
                    }

                    m_isRebinding = false;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 超时，恢复原文本
            m_keyDisplay.text = originalText;
            m_isRebinding = false;
        }

        private KeyCode DetectPressedKey()
        {
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(key))
                {
                    return key;
                }
            }

            return KeyCode.None;
        }

        private KeyCode DetectMouseKey()
        {
            if (Input.GetMouseButtonDown(0)) return KeyCode.Mouse0;
            if (Input.GetMouseButtonDown(1)) return KeyCode.Mouse1;
            if (Input.GetMouseButtonDown(2)) return KeyCode.Mouse2;
            if (Input.GetMouseButtonDown(3)) return KeyCode.Mouse3;
            if (Input.GetMouseButtonDown(4)) return KeyCode.Mouse4;
            if (Input.GetMouseButtonDown(5)) return KeyCode.Mouse5;
            if (Input.GetMouseButtonDown(6)) return KeyCode.Mouse6;

            return KeyCode.None;
        }

        private bool IsModifierKey(KeyCode key)
        {
            return KeyCombo.IsModifierKey(key);
        }

        /// <summary>
        /// 根据当前按住的修饰键 + 按下键，构建键名。
        /// 若按下的本身就是修饰键，则只显示该修饰键名（不带前缀）。
        /// </summary>
        private string BuildCombinedKeyName(KeyCode pressedKey)
        {
            // 修饰键单独按下时，直接显示其名称
            if (IsModifierKey(pressedKey))
            {
                return KeyCombo.FormatKeyCode(pressedKey);
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                sb.Append("Ctrl + ");
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                sb.Append("Shift + ");
            }

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                sb.Append("Alt + ");
            }

            sb.Append(KeyCombo.FormatKeyCode(pressedKey));

            return sb.ToString();
        }

        /// <summary>
        /// 构建滚轮绑定名（含修饰键前缀）
        /// </summary>
        private string BuildWheelName(bool wheelUp)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                sb.Append("Ctrl + ");
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                sb.Append("Shift + ");
            }

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                sb.Append("Alt + ");
            }

            sb.Append(wheelUp ? "滚轮上" : "滚轮下");

            return sb.ToString();
        }
    }
}
