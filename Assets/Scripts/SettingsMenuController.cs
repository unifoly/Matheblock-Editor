using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexMap
{
    /// <summary>
    /// Settings 菜单控制器
    /// 左侧 Menu 区域滚动列表 → 点击 → 右侧显示对应设置子页面
    /// 同时在 Start 时自动绑定各页面控件到 SettingsDataManager
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        [Serializable]
        public struct MenuEntry
        {
            public Button menuButton;
            public GameObject pagePanel;
        }

        [Header("菜单条目（按钮 → 对应设置面板）")]
        [SerializeField] private List<MenuEntry> m_menuEntries = new();

        [Header("高亮色")]
        [SerializeField] private Color m_activeColor = new Color(0.3f, 0.6f, 0.9f);
        [SerializeField] private Color m_inactiveColor = new Color(0.226f, 0.226f, 0.226f);

        private Button m_currentActiveButton;
        private TMP_FontAsset m_chineseFont;

        /// <summary>
        /// 左侧菜单条目所在容器（滚动列表 Content 或菜单按钮的父节点），
        /// 供 SettingsSceneController 将自动创建的按钮放入菜单列表底部
        /// </summary>
        public Transform MenuContent
        {
            get
            {
                for (int i = 0; i < m_menuEntries.Count; i++)
                {
                    if (m_menuEntries[i].menuButton != null)
                    {
                        return m_menuEntries[i].menuButton.transform.parent;
                    }
                }

                return null;
            }
        }

        private void Start()
        {
            HideAllPages();
            WireUpButtons();
            CreateEditorSettingsRows();
            BindPageControls();
        }

        /// <summary>
        /// 隐藏所有设置子页面
        /// </summary>
        private void HideAllPages()
        {
            foreach (var entry in m_menuEntries)
            {
                if (entry.pagePanel != null)
                {
                    entry.pagePanel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 为所有菜单按钮绑定点击事件
        /// </summary>
        private void WireUpButtons()
        {
            for (int i = 0; i < m_menuEntries.Count; i++)
            {
                int index = i;
                MenuEntry entry = m_menuEntries[i];

                if (entry.menuButton == null)
                {
                    continue;
                }

                entry.menuButton.onClick.AddListener(() => ShowPage(index));
            }
        }

        /// <summary>
        /// 切换到指定索引的设置页面
        /// </summary>
        private void ShowPage(int index)
        {
            if (index < 0 || index >= m_menuEntries.Count)
            {
                return;
            }

            // 高亮切换
            if (m_currentActiveButton != null)
            {
                SetButtonColor(m_currentActiveButton, m_inactiveColor);
            }

            m_currentActiveButton = m_menuEntries[index].menuButton;
            SetButtonColor(m_currentActiveButton, m_activeColor);

            // 隐藏所有页面，显示目标页面
            HideAllPages();
            if (m_menuEntries[index].pagePanel != null)
            {
                m_menuEntries[index].pagePanel.SetActive(true);
            }
        }

        private void SetButtonColor(Button btn, Color color)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = color;
            cb.selectedColor = color;
            cb.highlightedColor = color * 1.2f;
            btn.colors = cb;
        }

        #region 控件绑定

        /// <summary>
        /// 根据 Row 名称约定自动绑定滑块/开关/下拉框到 SettingsDataManager
        /// </summary>
        private void BindPageControls()
        {
            foreach (var entry in m_menuEntries)
            {
                if (entry.pagePanel == null) continue;
                BindControlsRecursive(entry.pagePanel.transform);
            }
        }

        private void BindControlsRecursive(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var name = child.name;

                // ResetRow 按钮：重置所有快捷键
                if (name == "ResetRow")
                {
                    var btn = child.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(ResetAllKeyBindings);
                    }
                }

                // 控件可能位于 Row_ 行自身，也可能位于行内子对象（兼容场景与运行时创建两种结构）
                if (name.StartsWith("Row_"))
                {
                    // 滑块
                    var slider = child.GetComponent<Slider>();
                    if (slider == null) slider = child.GetComponentInChildren<Slider>(true);
                    if (slider != null)
                    {
                        BindSlider(slider, name);
                    }

                    // 输入框
                    var inputField = child.GetComponent<TMP_InputField>();
                    if (inputField == null) inputField = child.GetComponentInChildren<TMP_InputField>(true);
                    if (inputField != null)
                    {
                        BindInputField(inputField, name);
                    }

                    // 下拉框
                    var dropdown = child.GetComponent<TMP_Dropdown>();
                    if (dropdown == null) dropdown = child.GetComponentInChildren<TMP_Dropdown>(true);
                    if (dropdown != null)
                    {
                        BindDropdown(dropdown, name);
                    }
                }

                // 递归
                BindControlsRecursive(child);
            }
        }

        private void BindSlider(Slider slider, string rowName)
        {
            switch (rowName)
            {
                case "Row_MasterVolume":
                    slider.value = SettingsDataManager.MasterVolume;
                    slider.onValueChanged.AddListener(v =>
                    {
                        SettingsDataManager.MasterVolume = v;
                        SettingsDataManager.ApplySettings();
                        SettingsDataManager.Save();
                    });
                    break;
                case "Row_MusicVolume":
                    slider.value = SettingsDataManager.MusicVolume;
                    slider.onValueChanged.AddListener(v =>
                    {
                        SettingsDataManager.MusicVolume = v;
                        SettingsDataManager.ApplySettings();
                        SettingsDataManager.Save();
                    });
                    break;
                case "Row_SFXVolume":
                    slider.value = SettingsDataManager.SFXVolume;
                    slider.onValueChanged.AddListener(v =>
                    {
                        SettingsDataManager.SFXVolume = v;
                        SettingsDataManager.ApplySettings();
                        SettingsDataManager.Save();
                    });
                    break;
            }
        }

        private void BindInputField(TMP_InputField inputField, string rowName)
        {
            if (rowName != "Row_AutoSave")
            {
                return;
            }

            // 显示当前设置的自动保存分钟数（默认 10）
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.text = SettingsDataManager.AutoSaveMinutes.ToString();

            inputField.onEndEdit.AddListener(value =>
            {
                // 解析失败或超出范围时回退为当前设置值
                if (int.TryParse(value, out int minutes))
                {
                    SettingsDataManager.AutoSaveMinutes = Mathf.Clamp(minutes, 1, 60);
                }

                inputField.text = SettingsDataManager.AutoSaveMinutes.ToString();
                SettingsDataManager.Save();
            });
        }

        private void BindDropdown(TMP_Dropdown dropdown, string rowName)
        {
            switch (rowName)
            {
                case "Row_Quality":
                    // 显示当前画质等级，变更时立即应用并持久化
                    dropdown.value = SettingsDataManager.QualityLevel;
                    dropdown.onValueChanged.AddListener(v =>
                    {
                        SettingsDataManager.QualityLevel = v;
                        SettingsDataManager.ApplySettings();
                        SettingsDataManager.Save();
                    });
                    break;

                case "Row_FakeNoteMode":
                    // 显示当前 Fake Note 放置模式（0=切换，1=按住），变更时持久化
                    dropdown.value = SettingsDataManager.FakeNoteMode;
                    dropdown.onValueChanged.AddListener(v =>
                    {
                        SettingsDataManager.FakeNoteMode = v;
                        SettingsDataManager.Save();
                    });
                    break;
            }
        }

        #endregion

        /// <summary>
        /// 重置所有快捷键绑定到默认值
        /// </summary>
        private void ResetAllKeyBindings()
        {
            KeyBindingsStore.ResetAll();

            foreach (var entry in m_menuEntries)
            {
                if (entry.pagePanel == null) continue;
                var rebindButtons = entry.pagePanel.GetComponentsInChildren<RebindButton>(true);
                foreach (var rb in rebindButtons)
                {
                    rb.ResetToDefault();
                }
            }
        }

        #region 画质下拉动态创建

        // 画质选项显示名（与 ProjectSettings/QualitySettings.asset 的等级顺序一一对应）
        private static readonly string[] k_qualityDisplayNames = { "极低", "低", "中", "高", "很高", "极高" };

        /// <summary>
        /// 在编辑器设置页（Page_Editor）动态创建下拉设置行（画质、Fake Note 放置模式），
        /// 运行时创建以避免直接修改场景 YAML 的脆弱性
        /// </summary>
        private void CreateEditorSettingsRows()
        {
            Transform content = FindEditorSettingsContent();
            if (content == null)
            {
                return;
            }

            // 画质下拉（第一行）
            CreateDropdownRow(content, "Row_Quality", "画质", BuildQualityOptions(), 0);

            // Fake Note 放置模式下拉（第二行）
            CreateDropdownRow(content, "Row_FakeNoteMode", "Fake Note 放置", new List<string> { "切换", "按住" }, 1);
        }

        /// <summary>
        /// 创建下拉设置行（水平布局：左侧标签 + 右侧下拉框），插入到指定兄弟索引
        /// </summary>
        private TMP_Dropdown CreateDropdownRow(Transform content, string rowName, string labelText, List<string> options, int siblingIndex)
        {
            // 行容器（水平布局：左侧标签 + 右侧下拉框）
            var rowGo = CreateUIObject(rowName, content);
            rowGo.transform.SetSiblingIndex(siblingIndex);

            var rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 10, 10);
            rowLayout.spacing = 16f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childControlWidth = true;

            var rowLayoutElement = rowGo.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 64f;
            rowLayoutElement.flexibleWidth = 1f;

            CreateRowLabel(rowGo.transform, labelText);
            return CreateDropdown(rowGo.transform, options);
        }

        /// <summary>
        /// 定位编辑器设置页（Page_Editor）的滚动内容容器
        /// </summary>
        private Transform FindEditorSettingsContent()
        {
            for (int i = 0; i < m_menuEntries.Count; i++)
            {
                var pagePanel = m_menuEntries[i].pagePanel;
                if (pagePanel == null || pagePanel.name != "Page_Editor")
                {
                    continue;
                }

                var layout = pagePanel.GetComponentInChildren<VerticalLayoutGroup>(true);
                if (layout != null)
                {
                    return layout.transform;
                }
            }

            return null;
        }

        /// <summary>
        /// 创建行左侧的标签文本
        /// </summary>
        private void CreateRowLabel(Transform parent, string text)
        {
            var labelGo = CreateUIObject("Text (TMP)", parent);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var layoutElement = labelGo.AddComponent<LayoutElement>();
            layoutElement.minWidth = 180f;
            layoutElement.flexibleWidth = 0f;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 22f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.font = GetChineseFont();
        }

        /// <summary>
        /// 创建行右侧的下拉框（选项列表由调用方指定）
        /// </summary>
        private TMP_Dropdown CreateDropdown(Transform parent, List<string> options)
        {
            var go = CreateUIObject("Dropdown", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.minWidth = 200f;
            layoutElement.flexibleWidth = 1f;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var dropdown = go.AddComponent<TMP_Dropdown>();

            // 当前选项标签
            var label = CreateUIObject("Label", go.transform);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12, 2);
            labelRect.offsetMax = new Vector2(-20, -2);

            var labelText = label.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = 20f;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.font = GetChineseFont();

            // 右侧箭头
            var arrow = CreateUIObject("Arrow", go.transform);
            var arrowRect = arrow.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.pivot = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-4, 0);

            var arrowText = arrow.AddComponent<TextMeshProUGUI>();
            arrowText.text = "v";
            arrowText.fontSize = 18f;
            arrowText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.font = GetChineseFont();

            // 展开模板（滚动列表）
            var template = CreateUIObject("Template", go.transform);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.sizeDelta = new Vector2(0, 180);
            templateRect.anchoredPosition = new Vector2(0, 2);

            var templateImg = template.AddComponent<Image>();
            templateImg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

            var scrollRect = template.AddComponent<ScrollRect>();

            var viewport = CreateUIObject("Viewport", template.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            var content = CreateUIObject("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 28);

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.scrollSensitivity = 35f;

            // 列表项
            var item = CreateUIObject("Item", content.transform);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 28);

            var itemToggle = item.AddComponent<Toggle>();

            var itemBg = CreateUIObject("Item Background", item.transform);
            var itemBgRect = itemBg.GetComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            var itemBgImg = itemBg.AddComponent<Image>();
            itemBgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var checkmark = CreateUIObject("Item Checkmark", item.transform);
            var checkmarkRect = checkmark.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0, 0.5f);
            checkmarkRect.pivot = new Vector2(0, 0.5f);
            checkmarkRect.sizeDelta = new Vector2(20, 20);
            checkmarkRect.anchoredPosition = new Vector2(4, 0);
            var checkmarkImg = checkmark.AddComponent<Image>();
            checkmarkImg.color = new Color(0.3f, 1f, 0.3f, 1f);

            var itemLabelObj = CreateUIObject("Item Label", item.transform);
            var itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(28, 2);
            itemLabelRect.offsetMax = new Vector2(-4, -2);
            var itemLabelText = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabelText.fontSize = 20f;
            itemLabelText.color = Color.white;
            itemLabelText.font = GetChineseFont();

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = checkmarkImg;
            itemToggle.isOn = false;

            dropdown.template = templateRect;
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabelText;

            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            template.SetActive(false);

            return dropdown;
        }

        /// <summary>
        /// 生成画质下拉选项（中文显示名，按 QualitySettings 等级顺序）
        /// </summary>
        private List<string> BuildQualityOptions()
        {
            string[] names = QualitySettings.names;
            var options = new List<string>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                // 数量不一致时回退为引擎原始名称，保证索引与画质等级对应
                options.Add(i < k_qualityDisplayNames.Length ? k_qualityDisplayNames[i] : names[i]);
            }

            return options;
        }

        private GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerConstants.Ui;
            return go;
        }

        private TMP_FontAsset GetChineseFont()
        {
            if (m_chineseFont != null)
            {
                return m_chineseFont;
            }

            var sourceFont = Resources.Load<Font>("Fonts/black");
            if (sourceFont == null)
            {
                return null;
            }

            m_chineseFont = TMP_FontAsset.CreateFontAsset(sourceFont);
            m_chineseFont.TryAddCharacters("画质极低中高很低切换按住Fake Note 放置");

            return m_chineseFont;
        }

        #endregion
    }
}