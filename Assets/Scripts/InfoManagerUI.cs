using System;
using System.Collections.Generic;
using System.IO;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 基本信息管理面板：编辑歌曲名、谱师名、曲师名、曲绘师，并可更换曲绘图片。
/// 点击 BasicInfo 按钮打开面板，从 chart.tmp 读取已有数据，编辑后即时保存。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class InfoManagerUI : MonoBehaviour
{
    private const float k_padding = 12f;
    private const float k_titleHeight = 40f;
    private const float k_rowHeight = 44f;
    private const float k_rowSpacing = 8f;
    private const float k_labelWidth = 100f;
    private const float k_inputWidth = 280f;
    private const float k_previewSize = 240f;
    private const float k_changeBtnHeight = 44f;
    private const float k_backButtonWidth = 60f;
    private const float k_backButtonHeight = 36f;
    private const float k_fontSize = 24f;

    private GameObject m_functionChanger;
    private Button m_infoButton;
    private GameObject m_infoPanel;
    private Button m_saveButton;

    // 输入框引用
    private TMP_InputField m_musicNameInput;
    private TMP_InputField m_charterInput;
    private TMP_InputField m_musicianInput;
    private TMP_InputField m_illustrationerInput;
    private Image m_previewImage;

    // 撤回功能：记录编辑前的状态快照
    private ChartJsonInfo m_lastSavedState;

    // 中文字体缓存（与 BpmManagerUI 独立缓存，避免相互依赖）
    private static TMP_FontAsset s_chineseFont;

    #region JSON 序列化类

    [Serializable]
    private class ChartJsonInfo
    {
        public string MusicName;
        public string Charter;
        public string Illustrationer;
        public string Musician;
    }

    [Serializable]
    private class BpmJsonNode
    {
        public float time;
        public float bpm;
    }

    [Serializable]
    private class ChartJsonData
    {
        public ChartJsonInfo info;
        public List<BpmJsonNode> bpmNodes;
        public List<NoteJsonNode> notes;
    }

    #endregion

    private void Awake()
    {
        m_functionChanger = transform.parent.gameObject;
        m_infoButton = GetComponent<Button>();

        // 清理可能残留的旧 InfoPanel
        var oldPanel = m_functionChanger.transform.Find("InfoPanel");
        if (oldPanel != null)
        {
            Destroy(oldPanel.gameObject);
        }

        // 查找 Save 按钮
        var saveObj = GameObject.Find("Save");
        if (saveObj != null)
        {
            m_saveButton = saveObj.GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (m_infoButton != null)
        {
            m_infoButton.onClick.RemoveListener(HandleInfoButtonClicked);
            m_infoButton.onClick.AddListener(HandleInfoButtonClicked);
        }

        if (m_saveButton != null)
        {
            m_saveButton.onClick.RemoveListener(HandleSaveButtonClicked);
            m_saveButton.onClick.AddListener(HandleSaveButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (m_infoButton != null)
        {
            m_infoButton.onClick.RemoveListener(HandleInfoButtonClicked);
        }

        if (m_saveButton != null)
        {
            m_saveButton.onClick.RemoveListener(HandleSaveButtonClicked);
        }
    }

    /// <summary>
    /// 点击"基本信息"按钮：首次点击创建面板，后续点击显示面板
    /// </summary>
    private void HandleInfoButtonClicked()
    {
        SetOriginalButtonsActive(false);

        // 懒加载：首次点击才构建面板
        if (m_infoPanel == null)
        {
            BuildInfoPanel();
        }

        // 每次打开时重新读取数据
        LoadInfoFromJson();
        RefreshPreview();

        m_infoPanel.SetActive(true);
        m_infoPanel.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 点击返回键：隐藏面板，还原原始按钮
    /// </summary>
    private void HandleBackButtonClicked()
    {
        m_infoPanel.SetActive(false);
        SetOriginalButtonsActive(true);
    }

    /// <summary>
    /// Save 按钮：chart.tmp 已是最新，此处仅日志确认
    /// </summary>
    private void HandleSaveButtonClicked()
    {
        // 确保 info 字段已写入 chart.tmp
        SaveInfoToJson();
        Debug.Log($"[{GetType().Name}] Info 数据已确认保存");
    }

    #region 面板构建

    /// <summary>
    /// 构建信息面板 UI（仅执行一次）
    /// </summary>
    private void BuildInfoPanel()
    {
        if (m_infoPanel != null) return;

        var existing = m_functionChanger.transform.Find("InfoPanel");
        if (existing != null)
        {
            m_infoPanel = existing.gameObject;
            return;
        }

        // 面板根节点
        m_infoPanel = CreateUIObject("InfoPanel", m_functionChanger.transform);
        var panelRect = m_infoPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelBg = m_infoPanel.AddComponent<Image>();
        panelBg.color = new Color(0.235f, 0.235f, 0.235f, 1f);
        panelBg.raycastTarget = false;

        BuildTopBar();
        BuildContentArea();
    }

    /// <summary>
    /// 顶部栏：返回键 + 标题
    /// </summary>
    private void BuildTopBar()
    {
        var topBar = CreateUIObject("TopBar", m_infoPanel.transform);
        var topBarRect = topBar.GetComponent<RectTransform>();
        topBarRect.anchorMin = new Vector2(0, 1);
        topBarRect.anchorMax = new Vector2(1, 1);
        topBarRect.pivot = new Vector2(0.5f, 1f);
        topBarRect.anchoredPosition = new Vector2(0, -k_padding);
        topBarRect.sizeDelta = new Vector2(0, k_titleHeight);

        // 返回键
        var backBtnGo = CreateUIObject("BackButton", topBar.transform);
        var backBtnRect = backBtnGo.GetComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0, 0.5f);
        backBtnRect.anchorMax = new Vector2(0, 0.5f);
        backBtnRect.pivot = new Vector2(0, 0.5f);
        backBtnRect.anchoredPosition = new Vector2(k_padding, 0);
        backBtnRect.sizeDelta = new Vector2(k_backButtonWidth, k_backButtonHeight);

        var backBtnImg = backBtnGo.AddComponent<Image>();
        backBtnImg.color = new Color(0.4f, 0.4f, 0.4f, 1f);

        var backBtn = backBtnGo.AddComponent<Button>();
        backBtn.targetGraphic = backBtnImg;
        backBtn.onClick.AddListener(HandleBackButtonClicked);

        var backText = CreateText("<", backBtnGo.transform, 28);
        backText.raycastTarget = false;
        var backTextRect = backText.GetComponent<RectTransform>();
        backTextRect.anchorMin = Vector2.zero;
        backTextRect.anchorMax = Vector2.one;
        backTextRect.offsetMin = Vector2.zero;
        backTextRect.offsetMax = Vector2.zero;
        backText.alignment = TextAlignmentOptions.Center;

        // 标题
        var titleText = CreateText("基本信息", topBar.transform, 28);
        titleText.raycastTarget = false;
        var titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(200, k_titleHeight);
        titleText.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>
    /// 内容区：4 个信息输入行 + 曲绘预览 + 更换按钮
    /// </summary>
    private void BuildContentArea()
    {
        // ScrollView 外层：填充面板剩余空间
        var scrollGo = CreateUIObject("ScrollView", m_infoPanel.transform);
        var scrollRectTrans = scrollGo.GetComponent<RectTransform>();
        scrollRectTrans.anchorMin = new Vector2(0, 0);
        scrollRectTrans.anchorMax = new Vector2(1, 1);
        scrollRectTrans.offsetMin = new Vector2(k_padding, k_padding);
        scrollRectTrans.offsetMax = new Vector2(-k_padding, -(k_titleHeight + k_padding * 2));

        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        scrollImg.raycastTarget = false;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // Viewport：承载遮罩
        var viewportGo = CreateUIObject("Viewport", scrollGo.transform);
        var viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 1f);
        viewportImg.raycastTarget = false;

        var viewportMask = viewportGo.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // Content：可滚动内容
        var contentGo = CreateUIObject("Content", viewportGo.transform);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var contentBg = contentGo.AddComponent<Image>();
        contentBg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        contentBg.raycastTarget = false;

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = k_rowSpacing;
        layout.padding = new RectOffset((int)k_padding, (int)k_padding, (int)k_padding, (int)k_padding);

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        // 4 个信息行
        m_musicNameInput = BuildInfoRow(contentGo.transform, "歌曲名", "");
        m_charterInput = BuildInfoRow(contentGo.transform, "谱师名", "");
        m_musicianInput = BuildInfoRow(contentGo.transform, "曲师名", "");
        m_illustrationerInput = BuildInfoRow(contentGo.transform, "曲绘作者", "");

        // 所有输入框编辑结束时记录撤回并保存
        m_musicNameInput.onEndEdit.AddListener(_ => HandleInputEndEdit());
        m_charterInput.onEndEdit.AddListener(_ => HandleInputEndEdit());
        m_musicianInput.onEndEdit.AddListener(_ => HandleInputEndEdit());
        m_illustrationerInput.onEndEdit.AddListener(_ => HandleInputEndEdit());

        // 曲绘预览
        BuildPreviewArea(contentGo.transform);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 构建单个信息行（标签 + 输入框），返回输入框组件
    /// </summary>
    private TMP_InputField BuildInfoRow(Transform parent, string label, string defaultText)
    {
        var rowGo = CreateUIObject($"Row_{label}", parent);

        // LayoutElement 仅控制行高，宽度由 VerticalLayoutGroup 撑满
        var rowLE = rowGo.AddComponent<LayoutElement>();
        rowLE.preferredHeight = k_rowHeight;

        // 标签：锚定左侧，固定宽度
        var labelTmp = CreateText(label, rowGo.transform, k_fontSize);
        var labelRect = labelTmp.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.5f);
        labelRect.anchorMax = new Vector2(0, 0.5f);
        labelRect.pivot = new Vector2(0, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(k_labelWidth, k_rowHeight);

        // 输入框：锚定填充标签右侧到行右边缘
        var inputGo = CreateTMPInput(defaultText, rowGo.transform, k_fontSize);
        var inputRect = inputGo.GetComponent<RectTransform>();
        float inputLeft = k_labelWidth + 8f;
        inputRect.anchorMin = new Vector2(0, 0);
        inputRect.anchorMax = new Vector2(1, 1);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.offsetMin = new Vector2(inputLeft, 0);
        inputRect.offsetMax = Vector2.zero;

        return inputGo.GetComponent<TMP_InputField>();
    }

    /// <summary>
    /// 构建曲绘预览区域和更换按钮
    /// </summary>
    private void BuildPreviewArea(Transform parent)
    {
        // 分隔标题
        var titleTmp = CreateText("曲绘预览", parent, k_fontSize);
        var titleLE = titleTmp.gameObject.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 30f;

        // 预览图片容器
        var previewGo = CreateUIObject("PreviewImage", parent);
        var previewLE = previewGo.AddComponent<LayoutElement>();
        previewLE.preferredWidth = k_previewSize;
        previewLE.preferredHeight = k_previewSize;

        m_previewImage = previewGo.AddComponent<Image>();
        m_previewImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        m_previewImage.raycastTarget = false;

        // 更换曲绘按钮
        var changeBtnGo = CreateUIObject("ChangeIllustrationBtn", parent);
        var changeLE = changeBtnGo.AddComponent<LayoutElement>();
        changeLE.preferredHeight = k_changeBtnHeight;

        var changeBtnImg = changeBtnGo.AddComponent<Image>();
        changeBtnImg.color = new Color(0.3f, 0.5f, 0.3f, 1f);

        var changeBtn = changeBtnGo.AddComponent<Button>();
        changeBtn.targetGraphic = changeBtnImg;
        changeBtn.onClick.AddListener(HandleChangeIllustration);

        var changeText = CreateText("更换曲绘", changeBtnGo.transform, k_fontSize);
        changeText.raycastTarget = false;
        var changeTextRect = changeText.GetComponent<RectTransform>();
        changeTextRect.anchorMin = Vector2.zero;
        changeTextRect.anchorMax = Vector2.one;
        changeTextRect.offsetMin = Vector2.zero;
        changeTextRect.offsetMax = Vector2.zero;
        changeText.alignment = TextAlignmentOptions.Center;
    }

    #endregion

    #region 曲绘更换

    /// <summary>
    /// 点击"更换曲绘"：打开文件选择器，选择图片后复制到谱面目录
    /// </summary>
    private void HandleChangeIllustration()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath)) return;

        var extensions = new[] { new ExtensionFilter("图片文件", "png", "jpg", "jpeg") };
        var results = StandaloneFileBrowser.OpenFilePanel("选择曲绘图片", "", extensions, false);

        if (results == null || results.Length == 0) return;

        string srcPath = results[0];
        string destPath = Path.Combine(EditorInit.ChartPath, "illustration.png");

        try
        {
            // 复制选中的图片到谱面目录，覆盖已有的 illustration.png
            File.Copy(srcPath, destPath, overwrite: true);
            Debug.Log($"[{GetType().Name}] 曲绘已更换: {srcPath} -> {destPath}");

            // 刷新预览
            RefreshPreview();

            // 更新 PlayScreen 背景
            UpdatePlayScreenIllustration(destPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 更换曲绘失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新曲绘预览图
    /// </summary>
    private void RefreshPreview()
    {
        if (m_previewImage == null || string.IsNullOrEmpty(EditorInit.ChartPath)) return;

        var illustrationPath = Path.Combine(EditorInit.ChartPath, "illustration.png");
        if (!File.Exists(illustrationPath)) return;

        try
        {
            var tex = new Texture2D(1, 1);
            tex.LoadImage(File.ReadAllBytes(illustrationPath));
            m_previewImage.sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
            m_previewImage.preserveAspect = true;
            // 重置为白色，避免 BuildPreviewArea 中设的暗色背景 tint 导致图片偏暗
            m_previewImage.color = Color.white;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{GetType().Name}] 加载曲绘预览失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新 PlayScreen 背景图
    /// </summary>
    private void UpdatePlayScreenIllustration(string illustrationPath)
    {
        var playScreen = GameObject.Find("PlayScreen");
        if (playScreen == null) return;

        try
        {
            var tex = new Texture2D(1, 1);
            tex.LoadImage(File.ReadAllBytes(illustrationPath));
            playScreen.GetComponent<Image>().sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{GetType().Name}] 更新 PlayScreen 背景失败: {ex.Message}");
        }
    }

    #endregion

    #region JSON 读写

    /// <summary>
    /// 从 chart.tmp 读取 info 字段并填充到输入框
    /// </summary>
    private void LoadInfoFromJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath) || !File.Exists(tmpPath)) return;

        try
        {
            var json = File.ReadAllText(tmpPath);
            var data = JsonUtility.FromJson<ChartJsonData>(json);

            if (data?.info == null) return;

            m_musicNameInput.text = data.info.MusicName ?? "";
            m_charterInput.text = data.info.Charter ?? "";
            m_musicianInput.text = data.info.Musician ?? "";
            m_illustrationerInput.text = data.info.Illustrationer ?? "";

            // 记录初始状态（用于检测编辑变化）
            m_lastSavedState = CaptureCurrentInputs();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 读取信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将输入框内容写入 chart.tmp（保留 bpmNodes、notes 等其他字段）
    /// </summary>
    private void SaveInfoToJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath)) return;

        try
        {
            ChartJsonData data;
            if (File.Exists(tmpPath))
            {
                var json = File.ReadAllText(tmpPath);
                data = JsonUtility.FromJson<ChartJsonData>(json) ?? new ChartJsonData();
            }
            else
            {
                data = new ChartJsonData();
            }

            // 只替换 info 字段
            data.info = new ChartJsonInfo
            {
                MusicName = m_musicNameInput.text,
                Charter = m_charterInput.text,
                Musician = m_musicianInput.text,
                Illustrationer = m_illustrationerInput.text
            };

            var jsonStr = JsonUtility.ToJson(data);
            File.WriteAllText(tmpPath, jsonStr);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 保存信息失败: {ex.Message}");
        }
    }

    private string GetTmpJsonPath()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath))
        {
            return null;
        }

        return Path.Combine(EditorInit.ChartPath, "chart.tmp");
    }

    #endregion

    #region 撤回功能

    /// <summary>
    /// 输入框编辑结束：检测内容变化，注册到全局撤回/重做系统并保存
    /// </summary>
    private void HandleInputEndEdit()
    {
        var current = CaptureCurrentInputs();

        // 仅在内容实际变化时记录到全局撤回/重做系统
        if (m_lastSavedState != null && !IsSameState(m_lastSavedState, current))
        {
            var oldState = m_lastSavedState;
            var newState = current;

            UndoRedoManager.Execute(
                undo: () => { RestoreState(oldState); m_lastSavedState = oldState; SaveInfoToJson(); },
                redo: () => { RestoreState(newState); m_lastSavedState = newState; SaveInfoToJson(); });
        }

        m_lastSavedState = current;
        SaveInfoToJson();
    }

    /// <summary>
    /// 捕获当前所有输入框内容为快照
    /// </summary>
    private ChartJsonInfo CaptureCurrentInputs()
    {
        return new ChartJsonInfo
        {
            MusicName = m_musicNameInput.text,
            Charter = m_charterInput.text,
            Musician = m_musicianInput.text,
            Illustrationer = m_illustrationerInput.text
        };
    }

    /// <summary>
    /// 将快照恢复到输入框
    /// </summary>
    private void RestoreState(ChartJsonInfo state)
    {
        m_musicNameInput.text = state.MusicName ?? "";
        m_charterInput.text = state.Charter ?? "";
        m_musicianInput.text = state.Musician ?? "";
        m_illustrationerInput.text = state.Illustrationer ?? "";
    }

    /// <summary>
    /// 比较两个快照内容是否一致
    /// </summary>
    private static bool IsSameState(ChartJsonInfo a, ChartJsonInfo b)
    {
        return a.MusicName == b.MusicName
            && a.Charter == b.Charter
            && a.Musician == b.Musician
            && a.Illustrationer == b.Illustrationer;
    }

    #endregion

    #region UI 工具方法

    /// <summary>
    /// 切换 FunctionChanger 下原始按钮的可见性（跳过自身和所有面板）
    /// </summary>
    private void SetOriginalButtonsActive(bool isActive)
    {
        foreach (Transform child in m_functionChanger.transform)
        {
            // 跳过自身和所有面板（面板由各自的返回按钮控制可见性）
            if (child.gameObject == gameObject || child.name.EndsWith("Panel"))
            {
                continue;
            }

            child.gameObject.SetActive(isActive);
        }
    }

    private static TMP_FontAsset GetChineseFont()
    {
        if (s_chineseFont != null) return s_chineseFont;

        var sourceFont = Resources.Load<Font>("Fonts/black");
        if (sourceFont == null) return null;

        s_chineseFont = TMP_FontAsset.CreateFontAsset(sourceFont);
        if (s_chineseFont != null)
        {
            // 预填充常用字符到动态 atlas，确保光标可渲染
            // （动态字体 atlas 为空时光标 UV 映射到透明区域，导致光标不可见）
            s_chineseFont.TryAddCharacters(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .");
        }

        return s_chineseFont;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5; // UI Layer
        return go;
    }

    private TextMeshProUGUI CreateText(string text, Transform parent, float fontSize)
    {
        var go = CreateUIObject("Text", parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.font = GetChineseFont();
        return tmp;
    }

    private GameObject CreateTMPInput(string defaultText, Transform parent, float fontSize)
    {
        var go = CreateUIObject("InputField", parent);

        var inputRect = go.GetComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(k_inputWidth, k_rowHeight);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        img.raycastTarget = true;

        var input = go.AddComponent<TMP_InputField>();
        // 显式设置 targetGraphic，确保点击输入框能激活编辑
        input.targetGraphic = img;
        input.interactable = true;
        // 光标设置：加宽到 3px、白色、闪烁，确保可见
        input.caretColor = Color.white;
        input.caretWidth = 3;
        input.caretBlinkRate = 0.85f;
        input.selectionColor = new Color(0.5f, 0.7f, 1f, 0.5f);

        var textArea = CreateUIObject("TextArea", go.transform);
        var textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(8, 4);
        textAreaRect.offsetMax = new Vector2(-8, -4);

        var textComp = textArea.AddComponent<TextMeshProUGUI>();
        textComp.fontSize = fontSize;
        textComp.color = Color.white;
        textComp.text = defaultText;
        textComp.font = GetChineseFont();
        textComp.alignment = TextAlignmentOptions.Left;
        textComp.raycastTarget = false;

        var placeholder = CreateUIObject("Placeholder", textArea.transform);
        var placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        var placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderText.fontSize = fontSize;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholderText.text = "...";
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.font = GetChineseFont();
        placeholderText.raycastTarget = false;

        input.textViewport = textAreaRect;
        input.textComponent = textComp;
        input.placeholder = placeholderText;

        return go;
    }

    #endregion
}
