using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HexMap
{
    /// <summary>
    /// 构建 Setting 场景的设置页面与控件，并把按钮 → 页面映射通过 SettingsMenuController 串起来。
    /// 菜单：HexMap → Build Settings Pages
    /// 幂等：重复执行会先清空旧页面再重建。
    /// </summary>
    public static class SettingsPageBuilder
    {
        private const string k_fontAssetPath = "Assets/Fonts/simhei SDF.asset";

        private static TMP_FontAsset s_font;
        private static Color s_panelBg = new Color(0.18f, 0.18f, 0.20f, 1f);
        private static Color s_rowBg = new Color(0.24f, 0.24f, 0.26f, 1f);
        private static Color s_rowBgAlt = new Color(0.22f, 0.22f, 0.24f, 1f);
        private static Color s_textColor = Color.white;
        private static Color s_accent = new Color(0.30f, 0.60f, 0.90f, 1f);
        private static Color s_headerColor = new Color(0.60f, 0.60f, 0.60f, 1f);
        private static Color s_dividerColor = new Color(0.30f, 0.30f, 0.32f, 1f);

        [MenuItem("HexMap/Build Settings Pages")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Equals("Setting"))
            {
                EditorUtility.DisplayDialog("Build Settings Pages",
                    "请先打开 Setting 场景。", "OK");
                return;
            }

            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_fontAssetPath);
            if (s_font == null)
            {
                s_font = TMP_Settings.defaultFontAsset;
                if (s_font == null)
                {
                    Debug.LogError("[SettingsPageBuilder] 找不到字体资产: " + k_fontAssetPath
                        + "，且无默认 TMP 字体可用。");
                    return;
                }

                Debug.LogWarning("[SettingsPageBuilder] 指定字体 '" + k_fontAssetPath
                    + "' 未找到，已降级使用默认 TMP 字体。");
            }

            var settings = GameObject.Find("Settings");
            var settingsController = GameObject.Find("SettingsController");
            var canvas = GameObject.Find("Canvas");
            var content = GameObject.Find("Content");
            if (settings == null || settingsController == null || canvas == null || content == null)
            {
                Debug.LogError("[SettingsPageBuilder] 缺少 Settings / SettingsController / Canvas / Content 中的某个对象。");
                return;
            }

            // 注意：不修改 Canvas 的 localScale，由 CanvasScaler (ScaleWithScreenSize 1920x1080) 自行管理
            var canvasComp = canvas.GetComponent<Canvas>();
            if (canvasComp != null && canvasComp.additionalShaderChannels != AdditionalCanvasShaderChannels.TexCoord1)
            {
                canvasComp.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                    | AdditionalCanvasShaderChannels.TexCoord2
                    | AdditionalCanvasShaderChannels.TexCoord3
                    | AdditionalCanvasShaderChannels.Normal;
                Debug.Log("[SettingsPageBuilder] 已修正 Canvas 额外着色器通道。");
            }

            // 修复整体布局：Menu 左侧固定宽度，Settings 填满剩余区域
            FixLayout(canvas, settings);

            ClearOldPages(settings.transform);

            var pageAudio = BuildAudioPage(settings.transform);
            var pageGameplay = BuildGameplayPage(settings.transform);
            var pageShortcut = BuildShortcutKeyPage(settings.transform);

            pageAudio.SetActive(true);
            pageGameplay.SetActive(false);
            pageShortcut.SetActive(false);

            var menuCtrl = GetOrAddComponent<SettingsMenuController>(settingsController);
            var menuField = typeof(SettingsMenuController).GetField(
                "m_menuEntries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var entries = new List<SettingsMenuController.MenuEntry>
            {
                new() { menuButton = FindButton("Btn_Audio"),    pagePanel = pageAudio },
                new() { menuButton = FindButton("Btn_Gameplay"), pagePanel = pageGameplay },
                new() { menuButton = FindButton("Btn_ShortcutKey"), pagePanel = pageShortcut },
            };
            menuField.SetValue(menuCtrl, entries);

            var sceneCtrl = GetOrAddComponent<SettingsSceneController>(settingsController);
            var backField = typeof(SettingsSceneController).GetField(
                "m_backButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            backField.SetValue(sceneCtrl, FindButton("Btn_BackToEditor"));

            ConfigureMenuButtons();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SettingsPageBuilder] 完成！已创建 3 页面并接线。");
        }

        // ===== 页面构建 =====

        private static GameObject BuildAudioPage(Transform parent)
        {
            var page = CreatePage(parent, "Page_Audio");
            var scroll = AddScrollRect(page);
            var content = scroll.content;
            AddVerticalLayout(content.gameObject, 12f, new RectOffset(24, 24, 24, 24));

            BuildSectionHeader(content, "音量设置");
            BuildSliderRow(content, "主音量", "MasterVolume");
            BuildSliderRow(content, "音乐音量", "MusicVolume");
            BuildSliderRow(content, "音效音量", "SFXVolume");
            return page;
        }

        private static GameObject BuildGameplayPage(Transform parent)
        {
            var page = CreatePage(parent, "Page_Gameplay");
            var scroll = AddScrollRect(page);
            var content = scroll.content;
            AddVerticalLayout(content.gameObject, 12f, new RectOffset(24, 24, 24, 24));

            BuildSectionHeader(content, "显示设置");
            BuildToggleRow(content, "全屏", "Fullscreen");
            BuildDropdownRow(content, "画质", "Quality", new[] { "低", "中", "高", "超高", "极致" });
            return page;
        }

        private static GameObject BuildShortcutKeyPage(Transform parent)
        {
            var page = CreatePage(parent, "Page_ShortcutKey");
            var scroll = AddScrollRect(page);
            var content = scroll.content;
            AddVerticalLayout(content.gameObject, 2f, new RectOffset(24, 24, 24, 24));
            var sizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildSectionHeader(content, "快捷键设置");

            var rows = new Dictionary<string, string>
            {
                { "向上滚动",       "↑"           },
                { "向下滚动",       "↓"           },
                { "放大",           "Ctrl + ="    },
                { "缩小",           "Ctrl + -"    },
                { "读取地图",       "R"           },
                { "保存地图",       "S"           },
                { "快速读取",       "Ctrl + R"    },
                { "冻结",           "F"           },
                { "放置六边形",     "H"           },
                { "出生位",         "P"           },
                { "镜头向上移动",   "W"           },
            };

            int index = 0;
            foreach (var kvp in rows)
            {
                // 分组分隔线：视口 / 缩放 / 文件 / 编辑 / 镜头
                if (index == 2 || index == 4 || index == 6 || index == 8 || index == 9)
                {
                    BuildDivider(content);
                }

                BuildKeyRow(content, kvp.Key, kvp.Value, index);
                index++;
            }

            // 重置按钮
            BuildSpacer(content, 12f);
            BuildDivider(content);
            BuildSpacer(content, 8f);

            var resetRow = CreateUIObject("ResetRow", content.transform);
            AddLayoutElement(resetRow, minHeight: 48f, flexibleWidth: 1f);
            var resetBtn = resetRow.AddComponent<Button>();
            var resetImg = resetRow.AddComponent<Image>();
            resetImg.color = s_accent;
            var resetLabel = CreateText("重置全部快捷键", resetRow.transform, 22);
            resetLabel.alignment = TextAlignmentOptions.Center;
            StretchFill(resetLabel.GetComponent<RectTransform>());

            var btnColors = resetBtn.colors;
            btnColors.normalColor = s_accent;
            btnColors.highlightedColor = s_accent * 1.2f;
            btnColors.pressedColor = s_accent * 0.8f;
            resetBtn.colors = btnColors;

            return page;
        }

        // ===== 行构建 =====

        private static void BuildSectionHeader(Transform parent, string title)
        {
            var header = CreateUIObject("Header_" + title, parent);
            AddLayoutElement(header, minHeight: 36f, flexibleWidth: 1f);

            var label = CreateText(title, header.transform, 16);
            label.alignment = TextAlignmentOptions.Left;
            label.color = s_headerColor;
            StretchFill(label.GetComponent<RectTransform>());
        }

        private static void BuildDivider(Transform parent)
        {
            var divider = CreateUIObject("Divider", parent);
            AddLayoutElement(divider, minHeight: 2f, flexibleWidth: 1f);
            var img = divider.AddComponent<Image>();
            img.color = s_dividerColor;
        }

        private static void BuildSpacer(Transform parent, float height)
        {
            var spacer = CreateUIObject("Spacer", parent);
            AddLayoutElement(spacer, minHeight: height, flexibleWidth: 1f);
        }

        private static void BuildSliderRow(Transform parent, string label, string settingKey)
        {
            var row = CreateUIObject("Row_" + settingKey, parent);
            AddLayoutElement(row, minHeight: 64f, flexibleWidth: 1f);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 10, 10);
            rowLayout.spacing = 16;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var labelObj = CreateText(label, row.transform, 22);
            labelObj.alignment = TextAlignmentOptions.Left;
            AddLayoutElement(labelObj.gameObject, minWidth: 120, flexibleWidth: 0);

            var sliderObj = CreateUIObject("Slider", row.transform);
            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.direction = Slider.Direction.LeftToRight;

            // Background
            var bg = CreateUIObject("Background", sliderObj.transform);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(0, 8);
            bgRect.offsetMax = new Vector2(-12, -8);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = s_rowBg;

            // Fill Area
            var fillArea = CreateUIObject("Fill Area", sliderObj.transform);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(4, 8);
            fillAreaRect.offsetMax = new Vector2(-16, -8);
            var fill = CreateUIObject("Fill", fillArea.transform);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = s_accent;

            // Handle Area
            var handleArea = CreateUIObject("Handle Slide Area", sliderObj.transform);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);
            var handle = CreateUIObject("Handle", handleArea.transform);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
        }

        private static void BuildToggleRow(Transform parent, string label, string settingKey)
        {
            var row = CreateUIObject("Row_" + settingKey, parent);
            AddLayoutElement(row, minHeight: 64f, flexibleWidth: 1f);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 10, 10);
            rowLayout.spacing = 16;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var labelObj = CreateText(label, row.transform, 22);
            labelObj.alignment = TextAlignmentOptions.Left;
            AddLayoutElement(labelObj.gameObject, minWidth: 120, flexibleWidth: 0);

            var toggleObj = CreateUIObject("Toggle", row.transform);
            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = true;

            var bg = CreateUIObject("Background", toggleObj.transform);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(28, 28);
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.anchoredPosition = new Vector2(14, 0);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = s_rowBg;

            var check = CreateUIObject("Checkmark", bg.transform);
            var checkRect = check.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            var checkImg = check.AddComponent<Image>();
            checkImg.color = s_accent;

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
        }

        private static void BuildDropdownRow(Transform parent, string label, string settingKey, string[] options)
        {
            var row = CreateUIObject("Row_" + settingKey, parent);
            AddLayoutElement(row, minHeight: 64f, flexibleWidth: 1f);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 10, 10);
            rowLayout.spacing = 16;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var labelObj = CreateText(label, row.transform, 22);
            labelObj.alignment = TextAlignmentOptions.Left;
            AddLayoutElement(labelObj.gameObject, minWidth: 120, flexibleWidth: 0);

            var ddObj = CreateUIObject("Dropdown", row.transform);
            var dd = ddObj.AddComponent<TMP_Dropdown>();

            // Background
            var bg = CreateUIObject("Background", ddObj.transform);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bg.transform.SetAsFirstSibling();
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = s_rowBg;

            // Label (caption text)
            var labelTxt = CreateText(options[0], ddObj.transform, 22);
            labelTxt.alignment = TextAlignmentOptions.Left;
            var labelRect = labelTxt.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12, 2);
            labelRect.offsetMax = new Vector2(-24, -2);

            // Arrow
            var arrow = CreateUIObject("Arrow", ddObj.transform);
            var arrowRect = arrow.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-12, 0);
            var arrowImg = arrow.AddComponent<Image>();
            arrowImg.color = new Color(0.7f, 0.7f, 0.7f, 1);

            // Template (dropdown list)
            var template = CreateUIObject("Template", ddObj.transform);
            template.SetActive(false);
            var templateRt = template.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0, 1);
            templateRt.anchorMax = new Vector2(1, 1);
            templateRt.pivot = new Vector2(0.5f, 1);
            templateRt.sizeDelta = new Vector2(0, 180);
            var templateImg = template.AddComponent<Image>();

            var scrollRect = template.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = CreateUIObject("Viewport", template.transform);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = s_rowBg;
            viewport.AddComponent<Mask>();

            var content = CreateUIObject("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, 0);

            // Item template
            var item = CreateUIObject("Item", content.transform);
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 1);
            itemRt.anchorMax = new Vector2(1, 1);
            itemRt.sizeDelta = new Vector2(0, 40);
            itemRt.pivot = new Vector2(0.5f, 1);
            var toggle = item.AddComponent<Toggle>();

            var itemBg = CreateUIObject("Item Background", item.transform);
            var itemBgRt = itemBg.GetComponent<RectTransform>();
            itemBgRt.anchorMin = Vector2.zero;
            itemBgRt.anchorMax = Vector2.one;
            itemBgRt.offsetMin = Vector2.zero;
            itemBgRt.offsetMax = Vector2.zero;
            var itemBgImg = itemBg.AddComponent<Image>();
            itemBgImg.color = s_rowBg;

            var itemCheckmark = CreateUIObject("Item Checkmark", item.transform);
            var itemChkRt = itemCheckmark.GetComponent<RectTransform>();
            itemChkRt.anchorMin = new Vector2(0, 0.5f);
            itemChkRt.anchorMax = new Vector2(0, 0.5f);
            itemChkRt.sizeDelta = new Vector2(20, 20);
            itemChkRt.anchoredPosition = new Vector2(10, 0);
            var chkImg = itemCheckmark.AddComponent<Image>();

            var itemLabel = CreateText("", item.transform, 18);
            itemLabel.alignment = TextAlignmentOptions.Left;
            var itemLabelRt = itemLabel.GetComponent<RectTransform>();
            itemLabelRt.anchorMin = Vector2.zero;
            itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(34, 2);
            itemLabelRt.offsetMax = new Vector2(-8, -2);

            toggle.targetGraphic = itemBgImg;
            toggle.graphic = chkImg;

            scrollRect.viewport = vpRt;
            scrollRect.content = contentRt;

            dd.template = templateRt;
            dd.captionText = labelTxt;
            dd.itemText = itemLabel;
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));
        }

        private static void BuildKeyRow(Transform parent, string actionName, string defaultKey, int index)
        {
            var row = CreateUIObject("Row_" + actionName, parent);
            AddLayoutElement(row, minHeight: 56f, flexibleWidth: 1f);

            // 交替背景色 —— 用独立子对象做背景，不干扰 LayoutGroup
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(row.transform, false);
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = (index % 2 == 0) ? s_rowBg : s_rowBgAlt;
            var bgRect = bgGo.GetComponent<RectTransform>();
            StretchFill(bgRect);
            bgGo.transform.SetAsFirstSibling();

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 8, 8);
            rowLayout.spacing = 12;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            // ActionLabel
            var actionLabel = CreateText(actionName, row.transform, 20);
            actionLabel.alignment = TextAlignmentOptions.Left;
            AddLayoutElement(actionLabel.gameObject, minWidth: 160, flexibleWidth: 2);

            // KeyLabel container (background)
            var keyLabelContainer = CreateUIObject("KeyLabel", row.transform);
            var keyBg = keyLabelContainer.AddComponent<Image>();
            keyBg.color = s_panelBg;
            keyBg.raycastTarget = true;
            AddLayoutElement(keyLabelContainer, minWidth: 100, flexibleWidth: 1);

            // KeyLabel (TMP text child)
            var keyLabel = CreateText(defaultKey, keyLabelContainer.transform, 20);
            keyLabel.alignment = TextAlignmentOptions.Center;
            StretchFill(keyLabel.GetComponent<RectTransform>());

            // RebindButton
            var rebindBtn = new GameObject("RebindButton");
            rebindBtn.transform.SetParent(row.transform, false);
            rebindBtn.AddComponent<RectTransform>();
            rebindBtn.AddComponent<CanvasRenderer>();
            var btn = rebindBtn.AddComponent<Button>();
            var btnImg = rebindBtn.AddComponent<Image>();
            btnImg.color = s_accent;

            var btnLabel = CreateText("重绑", rebindBtn.transform, 18);
            btnLabel.alignment = TextAlignmentOptions.Center;
            StretchFill(btnLabel.GetComponent<RectTransform>());
            AddLayoutElement(rebindBtn, minWidth: 64, flexibleWidth: 0);

            // RebindButton 组件
            var rebind = rebindBtn.AddComponent<RebindButton>();
            var keyDisplayField = typeof(RebindButton).GetField(
                "m_keyDisplay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyDisplayField.SetValue(rebind, keyLabel);
            var actionNameField = typeof(RebindButton).GetField(
                "m_actionName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            actionNameField.SetValue(rebind, actionName);

            var defaultKeyField = typeof(RebindButton).GetField(
                "m_defaultKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            defaultKeyField.SetValue(rebind, defaultKey);

            string saved = KeyBindingsStore.GetBinding(actionName, defaultKey);
            keyLabel.text = saved;
        }

        // ===== 工具方法 =====

        private static GameObject CreatePage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = s_panelBg;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static ScrollRect AddScrollRect(GameObject page)
        {
            var sr = page.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 20f;

            var viewport = CreateUIObject("Viewport", page.transform);
            var vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;

            var content = CreateUIObject("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.pivot = new Vector2(0.5f, 1f);

            sr.content = contentRect;
            sr.viewport = vpRect;
            return sr;
        }

        private static void AddVerticalLayout(GameObject go, float spacing, RectOffset padding)
        {
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = padding;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateText(string text, Transform parent, int fontSize)
        {
            var go = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = s_textColor;
            if (s_font != null)
            {
                tmp.font = s_font;
            }

            return tmp;
        }

        private static void AddLayoutElement(GameObject go, float minWidth = -1, float minHeight = -1,
            float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (minWidth >= 0) le.minWidth = minWidth;
            if (minHeight >= 0) le.minHeight = minHeight;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static Button FindButton(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) return null;
            return go.GetComponent<Button>();
        }

        private static void ConfigureMenuButtons()
        {
            var names = new[] { "Btn_Audio", "Btn_Gameplay", "Btn_ShortcutKey" };
            foreach (var n in names)
            {
                var btn = FindButton(n);
                if (btn == null) continue;
                var cb = btn.colors;
                cb.normalColor = new Color(0.226f, 0.226f, 0.226f, 1);
                cb.highlightedColor = new Color(0.35f, 0.35f, 0.37f, 1);
                cb.selectedColor = new Color(0.30f, 0.60f, 0.90f, 1);
                cb.pressedColor = new Color(0.20f, 0.40f, 0.60f, 1);
                btn.colors = cb;

                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.fontSize = 22;
                }

                var layout = btn.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = btn.gameObject.AddComponent<LayoutElement>();
                }

                layout.minHeight = 56;
            }

            var backBtn = FindButton("Btn_BackToEditor");
            if (backBtn != null)
            {
                var label = backBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.fontSize = 22;
                }
            }
        }

        /// <summary>
        /// 统一修复 Setting 场景布局：
        /// - Menu 固定左侧宽度 (280)，垂直撑满，带 ScrollRect
        /// - Settings 右侧填满剩余区域
        /// - Viewport 有 Mask 裁剪，Content 顶部对齐可滚动
        /// </summary>
        private static void FixLayout(GameObject canvas, GameObject settingsPanel)
        {
            var menu = FindChildByName(canvas.transform, "Menu");
            const float k_menuWidth = 300f;

            // --- Menu 锚点：固定左侧，垂直撑满 ---
            if (menu != null)
            {
                var menuRt = menu.GetComponent<RectTransform>();
                menuRt.anchorMin = new Vector2(0, 0);
                menuRt.anchorMax = new Vector2(0, 1);
                menuRt.pivot = new Vector2(0, 0.5f);
                menuRt.offsetMin = new Vector2(0, 0);
                menuRt.offsetMax = new Vector2(k_menuWidth, 0);

                // 背景色
                var img = menu.GetComponent<Image>();
                if (img != null) img.color = s_panelBg;

                // --- Menu 内部 ScrollRect 结构 ---
                var sr = menu.GetComponent<ScrollRect>();

                // 查找/确保 Viewport
                var vpGo = FindChildByName(menu.transform, "Viewport");
                RectTransform vpRt = vpGo != null ? vpGo.GetComponent<RectTransform>() : null;
                if (vpRt == null)
                {
                    var vp = CreateUIObject("Viewport", menu.transform);
                    vpRt = vp.GetComponent<RectTransform>();
                }
                vpRt.anchorMin = Vector2.zero;
                vpRt.anchorMax = Vector2.one;
                vpRt.offsetMin = Vector2.zero;
                vpRt.offsetMax = Vector2.zero;

                // Viewport: Image + Mask（裁剪溢出）
                var vpImg = vpRt.GetComponent<Image>();
                if (vpImg == null) vpImg = vpRt.gameObject.AddComponent<Image>();
                vpImg.color = new Color(1f, 1f, 1f, 0.01f);

                var vpMask = vpRt.GetComponent<Mask>();
                if (vpMask == null) vpRt.gameObject.AddComponent<Mask>();

                // 查找/确保 Content
                var ctGo = FindChildByName(vpRt, "Content");
                RectTransform ctRt = ctGo != null ? ctGo.GetComponent<RectTransform>() : null;
                if (ctRt == null)
                {
                    var ct = CreateUIObject("Content", vpRt);
                    ctRt = ct.GetComponent<RectTransform>();
                    // 确保 Content 有 VerticalLayoutGroup
                    var vlg = ct.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 0;
                    vlg.padding = new RectOffset(0, 0, 12, 12);
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    var fitter = ct.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                // Content 锚点：顶部对齐，宽度撑满
                ctRt.anchorMin = new Vector2(0, 1);
                ctRt.anchorMax = Vector2.one;
                ctRt.pivot = new Vector2(0.5f, 1);
                ctRt.offsetMin = Vector2.zero;
                ctRt.offsetMax = Vector2.zero;

                // ScrollRect 组件
                if (sr == null) sr = menu.AddComponent<ScrollRect>();
                sr.horizontal = false;
                sr.vertical = true;
                sr.scrollSensitivity = 20f;
                sr.viewport = vpRt;
                sr.content = ctRt;

                Debug.Log("[SettingsPageBuilder] Menu 布局已修复（左侧固定 300px + ScrollRect）。");
            }

            // --- Settings 面板：填满右侧剩余区域 ---
            if (settingsPanel != null)
            {
                var stRt = settingsPanel.GetComponent<RectTransform>();
                stRt.anchorMin = Vector2.zero;
                stRt.anchorMax = Vector2.one;
                stRt.pivot = new Vector2(0.5f, 0.5f);
                stRt.offsetMin = new Vector2(k_menuWidth, 0);
                stRt.offsetMax = Vector2.zero;

                var img = settingsPanel.GetComponent<Image>();
                if (img != null) img.color = s_panelBg;

                Debug.Log("[SettingsPageBuilder] Settings 布局已修复（填满右侧）。");
            }
        }

        private static GameObject FindChildByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child.gameObject;
            }
            return null;
        }

        private static void ClearOldPages(Transform settings)
        {
            for (int i = settings.childCount - 1; i >= 0; i--)
            {
                var child = settings.GetChild(i);
                if (child.name.StartsWith("Page_"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
