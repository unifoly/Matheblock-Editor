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

        [Header("菜单按钮容器（Menu 下含 ScrollRect 的子节点）")]
        [SerializeField] private Transform m_menuContent;

        [Header("设置页面容器（Settings 下子页面的父节点）")]
        [SerializeField] private Transform m_pageContainer;

        [Header("高亮色")]
        [SerializeField] private Color m_activeColor = new Color(0.3f, 0.6f, 0.9f);
        [SerializeField] private Color m_inactiveColor = new Color(0.226f, 0.226f, 0.226f);

        private Button m_currentActiveButton;

        private void Start()
        {
            HideAllPages();
            WireUpButtons();
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

                // 滑块
                var slider = child.GetComponent<Slider>();
                if (slider != null && name.StartsWith("Row_"))
                {
                    BindSlider(slider, name);
                }

                // 开关
                var toggle = child.GetComponent<Toggle>();
                if (toggle != null && name.StartsWith("Row_"))
                {
                    BindToggle(toggle, name);
                }

                // 下拉框
                var dropdown = child.GetComponent<TMP_Dropdown>();
                if (dropdown != null && name.StartsWith("Row_"))
                {
                    BindDropdown(dropdown, name);
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

        private void BindToggle(Toggle toggle, string rowName)
        {
            if (rowName == "Row_Fullscreen")
            {
                toggle.isOn = SettingsDataManager.IsFullscreen;
                toggle.onValueChanged.AddListener(v =>
                {
                    SettingsDataManager.IsFullscreen = v;
                    SettingsDataManager.ApplySettings();
                    SettingsDataManager.Save();
                });
            }
        }

        private void BindDropdown(TMP_Dropdown dropdown, string rowName)
        {
            if (rowName == "Row_Quality")
            {
                dropdown.value = SettingsDataManager.QualityLevel;
                dropdown.onValueChanged.AddListener(v =>
                {
                    SettingsDataManager.QualityLevel = v;
                    SettingsDataManager.ApplySettings();
                    SettingsDataManager.Save();
                });
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
    }
}