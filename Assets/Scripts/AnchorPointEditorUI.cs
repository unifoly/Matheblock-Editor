using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 锚点编辑面板 UI：当选中缓动区锚点时，在 FunctionChanger 位置弹出面板，
/// 支持修改锚点数值、缓动类型，以及删除锚点。
/// 通过订阅 EasingAreaManager 的 AnchorSelected / AnchorDeselected 事件驱动显示。
/// </summary>
public class AnchorPointEditorUI : MonoBehaviour
{
    // ---- 布局常量 ----
    private const float k_panelWidth = 350f;
    private const float k_panelHeight = 900f;
    private const float k_titleHeight = 50f;
    private const float k_sectionHeight = 60f;
    private const float k_buttonHeight = 44f;
    private const float k_padding = 12f;
    private const float k_inputWidth = 180f;
    private const float k_inputHeight = 36f;

    // ---- 引用 ----
    private EasingAreaManager m_easingAreaManager;
    private GameObject m_functionChanger;
    private GameObject m_panel;
    private TMP_FontAsset m_chineseFont;

    // ---- UI 控件 ----
    private TextMeshProUGUI m_slotLabel;
    private TextMeshProUGUI m_timeLabel;
    private TMP_InputField m_valueInput;
    private TMP_Dropdown m_easingDropdown;
    private Slider m_weightSlider;
    private TextMeshProUGUI m_weightValueLabel;
    private RectTransform m_previewArea;
    private readonly List<Image> m_previewSegments = new List<Image>();
    private const int k_previewSamples = 40;
    private const float k_previewHeight = 100f;

    private void Start()
    {
        m_easingAreaManager = GetComponent<EasingAreaManager>();
        FindFunctionChanger();

        if (m_easingAreaManager != null)
        {
            m_easingAreaManager.AnchorSelected += OnAnchorSelected;
            m_easingAreaManager.AnchorDeselected += OnAnchorDeselected;
        }
    }

    private bool m_needPreviewRefresh;

    private void Update()
    {
        if (m_needPreviewRefresh)
        {
            RefreshCurvePreview();
            m_needPreviewRefresh = false;
        }
    }

    private void OnDestroy()
    {
        if (m_easingAreaManager != null)
        {
            m_easingAreaManager.AnchorSelected -= OnAnchorSelected;
            m_easingAreaManager.AnchorDeselected -= OnAnchorDeselected;
        }
    }

    /// <summary>
    /// 查找场景中的 FunctionChanger 容器
    /// </summary>
    private void FindFunctionChanger()
    {
        var fcObj = GameObject.Find("FunctionChanger");
        if (fcObj != null)
        {
            m_functionChanger = fcObj;
        }
        else
        {
            Debug.LogWarning($"[{GetType().Name}] 未找到 FunctionChanger，锚点编辑面板无法显示");
        }
    }

    #region 事件处理

    /// <summary>
    /// 锚点被选中：显示编辑面板
    /// </summary>
    private void OnAnchorSelected()
    {
        if (m_functionChanger == null) return;

        if (m_panel == null)
        {
            BuildPanel();
        }

        UpdatePanelContent();
        ShowPanel();
    }

    /// <summary>
    /// 锚点被取消选中：隐藏编辑面板
    /// </summary>
    private void OnAnchorDeselected()
    {
        HidePanel();
    }

    #endregion

    #region 面板构建

    /// <summary>
    /// 构建锚点编辑面板（首次显示时懒加载）
    /// </summary>
    private void BuildPanel()
    {
        m_panel = CreateUIObject("AnchorEditorPanel", m_functionChanger.transform);
        var panelRect = m_panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        // 背景
        var bg = m_panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.14f, 0.96f);

        float yPos = -k_padding;

        // ---- 标题 ----
        var title = CreateText("锚点编辑", m_panel.transform, 22f);
        PositionElement(title.rectTransform, k_padding, yPos, k_panelWidth - k_padding * 2, k_titleHeight);
        title.alignment = TextAlignmentOptions.Center;
        yPos -= k_titleHeight + k_padding;

        // ---- 槽位信息 ----
        var slotLabelText = CreateText("数据槽:", m_panel.transform, 16f);
        PositionElement(slotLabelText.rectTransform, k_padding, yPos, 100, k_sectionHeight);
        m_slotLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_slotLabel.rectTransform, k_padding + 100, yPos, k_panelWidth - k_padding * 2 - 100, k_sectionHeight);
        yPos -= k_sectionHeight;

        // ---- 时间信息 ----
        var timeLabelText = CreateText("时间:", m_panel.transform, 16f);
        PositionElement(timeLabelText.rectTransform, k_padding, yPos, 100, k_sectionHeight);
        m_timeLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_timeLabel.rectTransform, k_padding + 100, yPos, k_panelWidth - k_padding * 2 - 100, k_sectionHeight);
        yPos -= k_sectionHeight + k_padding;

        // ---- 数值输入 ----
        var valueLabel = CreateText("数值:", m_panel.transform, 16f);
        PositionElement(valueLabel.rectTransform, k_padding, yPos, 80, k_inputHeight);
        m_valueInput = CreateValueInput(m_panel.transform);
        PositionElement(m_valueInput.GetComponent<RectTransform>(), k_padding + 80, yPos, k_inputWidth, k_inputHeight);
        yPos -= k_inputHeight + k_padding;

        // ---- 缓动类型下拉 ----
        var easingLabel = CreateText("缓动:", m_panel.transform, 16f);
        PositionElement(easingLabel.rectTransform, k_padding, yPos, 80, k_inputHeight);
        m_easingDropdown = CreateEasingDropdown(m_panel.transform);
        PositionElement(m_easingDropdown.GetComponent<RectTransform>(), k_padding + 80, yPos, k_inputWidth, k_inputHeight);
        yPos -= k_inputHeight + k_padding;

        // ---- 缓动权重滑块 ----
        var weightLabel = CreateText("权重:", m_panel.transform, 16f);
        PositionElement(weightLabel.rectTransform, k_padding, yPos, 80, k_inputHeight);
        m_weightSlider = CreateWeightSlider(m_panel.transform);
        PositionElement(m_weightSlider.GetComponent<RectTransform>(), k_padding + 80, yPos, k_inputWidth - 50, k_inputHeight);
        m_weightValueLabel = CreateText("1.00", m_panel.transform, 14f);
        PositionElement(m_weightValueLabel.rectTransform, k_padding + 80 + k_inputWidth - 45, yPos, 45, k_inputHeight);
        m_weightValueLabel.alignment = TextAlignmentOptions.Center;
        yPos -= k_inputHeight + k_padding;

        // ---- 缓动曲线预览 ----
        var previewTitle = CreateText("曲线预览:", m_panel.transform, 14f);
        PositionElement(previewTitle.rectTransform, k_padding, yPos, k_panelWidth - k_padding * 2, 20);
        yPos -= 24;
        m_previewArea = CreatePreviewArea(m_panel.transform);
        PositionElement(m_previewArea, k_padding, yPos, k_panelWidth - k_padding * 2, k_previewHeight);
        yPos -= k_previewHeight + k_padding * 2;

        // ---- 删除按钮 ----
        var deleteBtn = CreateButton("删除锚点", m_panel.transform, new Color(0.7f, 0.2f, 0.2f, 1f));
        PositionElement(deleteBtn.GetComponent<RectTransform>(), k_padding, yPos, k_panelWidth - k_padding * 2, k_buttonHeight);
        deleteBtn.onClick.AddListener(OnDeleteClicked);
        yPos -= k_buttonHeight + k_padding;

        // ---- 关闭按钮 ----
        var closeBtn = CreateButton("关闭", m_panel.transform, new Color(0.3f, 0.3f, 0.4f, 1f));
        PositionElement(closeBtn.GetComponent<RectTransform>(), k_padding, yPos, k_panelWidth - k_padding * 2, k_buttonHeight);
        closeBtn.onClick.AddListener(OnCloseClicked);

        m_panel.SetActive(false);
    }

    /// <summary>
    /// 更新面板内容（选中锚点变化时调用）
    /// </summary>
    private void UpdatePanelContent()
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        int slot = m_easingAreaManager.SelectedSlot;
        var anchor = m_easingAreaManager.GetSelectedAnchorPoint();
        if (anchor == null) return;

        // 槽位标签
        m_slotLabel.text = m_easingAreaManager.GetSlotLabel(slot);

        // 时间
        m_timeLabel.text = $"{anchor.time:F2}s";

        // 数值
        m_valueInput.text = anchor.value.ToString("F3");

        // 缓动类型
        m_easingDropdown.value = EaseDisplayNames.GetIndex(anchor.easingType);

        // 权重
        m_weightSlider.value = anchor.weight;
        m_weightValueLabel.text = anchor.weight.ToString("F2");

        // 刷新曲线预览
        RefreshCurvePreview();
    }

    #endregion

    #region 面板显示/隐藏

    /// <summary>
    /// 显示面板：隐藏 FunctionChanger 下所有其他子物体
    /// </summary>
    private void ShowPanel()
    {
        if (m_panel == null || m_functionChanger == null) return;

        // 隐藏 FunctionChanger 下所有其他子物体
        foreach (Transform child in m_functionChanger.transform)
        {
            if (child.gameObject != m_panel)
            {
                child.gameObject.SetActive(false);
            }
        }

        m_panel.SetActive(true);
        // 下一帧刷新预览（等待布局完成）
        m_needPreviewRefresh = true;
    }

    /// <summary>
    /// 隐藏面板：恢复显示原始按钮（非 Panel 子物体）
    /// </summary>
    private void HidePanel()
    {
        if (m_panel == null) return;
        m_panel.SetActive(false);

        if (m_functionChanger == null) return;

        // 恢复显示原始按钮（名称不含 Panel 的子物体）
        foreach (Transform child in m_functionChanger.transform)
        {
            if (!child.name.EndsWith("Panel"))
            {
                child.gameObject.SetActive(true);
            }
        }
    }

    #endregion

    #region 按钮回调

    /// <summary>
    /// 删除按钮：删除当前选中锚点
    /// </summary>
    private void OnDeleteClicked()
    {
        if (m_easingAreaManager != null)
        {
            m_easingAreaManager.DeleteSelectedAnchor();
        }
    }

    /// <summary>
    /// 关闭按钮：取消选中锚点
    /// </summary>
    private void OnCloseClicked()
    {
        if (m_easingAreaManager != null)
        {
            m_easingAreaManager.DeselectAnchor();
        }
    }

    /// <summary>
    /// 数值输入结束：更新锚点数值
    /// </summary>
    private void OnValueEndEdit(string text)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        if (float.TryParse(text, out float value))
        {
            m_easingAreaManager.UpdateSelectedAnchorValue(value);
            // 刷新显示（可能被 Clamp 过）
            var anchor = m_easingAreaManager.GetSelectedAnchorPoint();
            if (anchor != null)
            {
                m_valueInput.text = anchor.value.ToString("F3");
            }
        }
        else
        {
            // 解析失败：恢复原值
            var anchor = m_easingAreaManager.GetSelectedAnchorPoint();
            if (anchor != null)
            {
                m_valueInput.text = anchor.value.ToString("F3");
            }
        }
    }

    /// <summary>
    /// 缓动类型下拉变化：更新锚点缓动类型并刷新预览
    /// </summary>
    private void OnEasingChanged(int index)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        m_easingAreaManager.UpdateSelectedAnchorEasing(EaseDisplayNames.GetEaseAt(index));
        RefreshCurvePreview();
    }

    /// <summary>
    /// 权重滑块变化：更新锚点权重并刷新预览
    /// </summary>
    private void OnWeightChanged(float value)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        m_weightValueLabel.text = value.ToString("F2");
        m_easingAreaManager.UpdateSelectedAnchorWeight(value);
        RefreshCurvePreview();
    }

    #endregion

    #region 曲线预览

    /// <summary>
    /// 刷新缓动曲线预览（根据当前缓动类型和权重绘制）
    /// </summary>
    private void RefreshCurvePreview()
    {
        if (m_previewArea == null) return;

        var anchor = m_easingAreaManager?.GetSelectedAnchorPoint();
        if (anchor == null) return;

        Ease ease = anchor.easingType;
        float weight = anchor.weight;

        float w = m_previewArea.rect.width;
        float h = m_previewArea.rect.height;
        if (w <= 0 || h <= 0) return;

        // 更新对角参考线（线性参考）
        var refLine = m_previewArea.Find("__REFLINE__");
        if (refLine != null)
        {
            var refRect = refLine.GetComponent<RectTransform>();
            float diagLen = Mathf.Sqrt(w * w + h * h);
            float diagAngle = Mathf.Atan2(h, w) * Mathf.Rad2Deg;
            refRect.anchoredPosition = new Vector2(w * 0.5f, h * 0.5f);
            refRect.sizeDelta = new Vector2(diagLen, 1);
            refRect.localEulerAngles = new Vector3(0, 0, diagAngle);
        }

        // 预览坐标系：左下角为 (0,0)，右上角为 (w,h)
        // 曲线从 (0, 0) 到 (w, h)，表示从 0 到 1 的归一化缓动
        int segIdx = 0;
        float prevX = 0f;
        float prevY = 0f;

        for (int s = 1; s <= k_previewSamples; s++)
        {
            float t = (float)s / k_previewSamples;
            float easedT = DOVirtual.EasedValue(0f, 1f, t, ease);
            float weightedT = Mathf.Lerp(t, easedT, weight);

            float x = t * w;
            float y = weightedT * h;

            Image seg = GetOrCreatePreviewSegment(segIdx);
            seg.gameObject.SetActive(true);
            PositionPreviewSegment(seg, prevX, prevY, x, y);
            segIdx++;

            prevX = x;
            prevY = y;
        }

        // 停用多余线段
        for (int i = segIdx; i < m_previewSegments.Count; i++)
        {
            if (m_previewSegments[i] != null)
            {
                m_previewSegments[i].gameObject.SetActive(false);
            }
        }
    }

    private Image GetOrCreatePreviewSegment(int index)
    {
        if (index < m_previewSegments.Count && m_previewSegments[index] != null)
        {
            return m_previewSegments[index];
        }

        var go = CreateUIObject($"PreviewSeg_{index}", m_previewArea);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.5f, 0.85f, 1f, 0.9f);
        img.raycastTarget = false;

        while (m_previewSegments.Count <= index)
        {
            m_previewSegments.Add(null);
        }
        m_previewSegments[index] = img;
        return img;
    }

    private void PositionPreviewSegment(Image seg, float x1, float y1, float x2, float y2)
    {
        var rect = seg.rectTransform;
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        if (length < 0.01f)
        {
            seg.gameObject.SetActive(false);
            return;
        }
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        rect.anchoredPosition = new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f);
        rect.sizeDelta = new Vector2(length, 2f);
        rect.localEulerAngles = new Vector3(0, 0, angle);
    }

    #endregion

    #region UI 创建辅助

    private void PositionElement(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
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

    private TMP_InputField CreateValueInput(Transform parent)
    {
        var go = CreateUIObject("ValueInput", parent);

        // 背景
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var input = go.AddComponent<TMP_InputField>();

        // 文字区域
        var textArea = CreateUIObject("TextArea", go.transform);
        var textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(4, 2);
        textAreaRect.offsetMax = new Vector2(-4, -2);

        var textComp = textArea.AddComponent<TextMeshProUGUI>();
        textComp.fontSize = 16f;
        textComp.color = Color.white;
        textComp.font = GetChineseFont();

        // Placeholder
        var placeholder = CreateUIObject("Placeholder", textArea.transform);
        var placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        var placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderText.fontSize = 16f;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholderText.text = "输入数值...";
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.font = GetChineseFont();

        input.textViewport = textAreaRect;
        input.textComponent = textComp;
        input.placeholder = placeholderText;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;

        input.onEndEdit.AddListener(OnValueEndEdit);

        return input;
    }

    private TMP_Dropdown CreateEasingDropdown(Transform parent)
    {
        var go = CreateUIObject("EasingDropdown", parent);

        // 背景
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var dropdown = go.AddComponent<TMP_Dropdown>();

        // 标签
        var label = CreateUIObject("Label", go.transform);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.offsetMin = new Vector2(8, 2);
        labelRect.offsetMax = new Vector2(-20, -2);

        var labelText = label.AddComponent<TextMeshProUGUI>();
        labelText.fontSize = 14f;
        labelText.color = Color.white;
        labelText.font = GetChineseFont();

        // 箭头
        var arrow = CreateUIObject("Arrow", go.transform);
        var arrowRect = arrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1, 0.5f);
        arrowRect.anchorMax = new Vector2(1, 0.5f);
        arrowRect.pivot = new Vector2(1, 0.5f);
        arrowRect.sizeDelta = new Vector2(20, 20);
        arrowRect.anchoredPosition = new Vector2(-2, 0);
        var arrowText = arrow.AddComponent<TextMeshProUGUI>();
        arrowText.text = "v";
        arrowText.fontSize = 14f;
        arrowText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.font = GetChineseFont();

        // 模板
        var template = CreateUIObject("Template", go.transform);
        var templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0, 0);
        templateRect.anchorMax = new Vector2(1, 0);
        templateRect.pivot = new Vector2(0.5f, 1);
        templateRect.sizeDelta = new Vector2(0, 150);
        templateRect.anchoredPosition = new Vector2(0, 2);

        var templateImg = template.AddComponent<Image>();
        templateImg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

        var scrollRect = template.AddComponent<ScrollRect>();

        // Viewport
        var viewport = CreateUIObject("Viewport", template.transform);
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        var viewportMask = viewport.AddComponent<RectMask2D>();

        // Content
        var content = CreateUIObject("Content", viewport.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 28);

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.scrollSensitivity = 35f;

        // Item
        var item = CreateUIObject("Item", content.transform);
        var itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 0.5f);
        itemRect.anchorMax = new Vector2(1, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.sizeDelta = new Vector2(0, 28);

        var itemToggle = item.AddComponent<Toggle>();

        // Item Background
        var itemBg = CreateUIObject("Item Background", item.transform);
        var itemBgRect = itemBg.GetComponent<RectTransform>();
        itemBgRect.anchorMin = Vector2.zero;
        itemBgRect.anchorMax = Vector2.one;
        itemBgRect.offsetMin = Vector2.zero;
        itemBgRect.offsetMax = Vector2.zero;
        var itemBgImg = itemBg.AddComponent<Image>();
        itemBgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        // Item Checkmark
        var checkmark = CreateUIObject("Item Checkmark", item.transform);
        var checkmarkRect = checkmark.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0, 0.5f);
        checkmarkRect.pivot = new Vector2(0, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(20, 20);
        checkmarkRect.anchoredPosition = new Vector2(4, 0);
        var checkmarkImg = checkmark.AddComponent<Image>();
        checkmarkImg.color = new Color(0.3f, 1f, 0.3f, 1f);

        // Item Label
        var itemLabelObj = CreateUIObject("Item Label", item.transform);
        var itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(28, 2);
        itemLabelRect.offsetMax = new Vector2(-4, -2);
        var itemLabelText = itemLabelObj.AddComponent<TextMeshProUGUI>();
        itemLabelText.fontSize = 14f;
        itemLabelText.color = Color.white;
        itemLabelText.font = GetChineseFont();

        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = checkmarkImg;
        itemToggle.isOn = false;

        dropdown.template = templateRect;
        dropdown.captionText = labelText;
        dropdown.itemText = itemLabelText;

        // 填充缓动类型选项
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(EaseDisplayNames.AllNames));
        dropdown.value = 0;

        dropdown.onValueChanged.AddListener(OnEasingChanged);

        template.SetActive(false);

        return dropdown;
    }

    private Button CreateButton(string text, Transform parent, Color bgColor)
    {
        var go = CreateUIObject($"Button_{text}", parent);

        var rect = go.GetComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // 按钮文字
        var labelObj = CreateUIObject("Label", go.transform);
        var labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 18f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.font = GetChineseFont();

        return btn;
    }

    /// <summary>
    /// 创建权重滑块 (0-2, 默认1)
    /// </summary>
    private Slider CreateWeightSlider(Transform parent)
    {
        var go = CreateUIObject("WeightSlider", parent);

        // 背景
        var bgRect = go.GetComponent<RectTransform>();
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var slider = go.AddComponent<Slider>();

        // 填充区域
        var fillArea = CreateUIObject("Fill Area", go.transform);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(2, 2);
        fillAreaRect.offsetMax = new Vector2(-2, -2);

        var fill = CreateUIObject("Fill", fillArea.transform);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.6f, 1f, 1f);

        // 手柄区域
        var handleArea = CreateUIObject("Handle Slide Area", go.transform);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8, 2);
        handleAreaRect.offsetMax = new Vector2(-8, -2);

        var handle = CreateUIObject("Handle", handleArea.transform);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0, 0.5f);
        handleRect.anchorMax = new Vector2(0, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(16, 16);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.minValue = 0f;
        slider.maxValue = 2f;
        slider.value = 1f;
        slider.onValueChanged.AddListener(OnWeightChanged);

        return slider;
    }

    /// <summary>
    /// 创建曲线预览区域（深色背景 + 边框）
    /// </summary>
    private RectTransform CreatePreviewArea(Transform parent)
    {
        var go = CreateUIObject("PreviewArea", parent);
        var rect = go.GetComponent<RectTransform>();

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        bg.raycastTarget = false;

        // 对角参考线 (线性, 从左下到右上)
        var refLine = CreateUIObject("RefLine", go.transform);
        var refRect = refLine.GetComponent<RectTransform>();
        refRect.anchorMin = new Vector2(0, 0);
        refRect.anchorMax = new Vector2(0, 0);
        refRect.pivot = new Vector2(0.5f, 0.5f);
        // 会由 RefreshCurvePreview 设置实际位置和大小, 这里先放占位
        var refImg = refLine.AddComponent<Image>();
        refImg.color = new Color(1f, 1f, 1f, 0.15f);
        refImg.raycastTarget = false;
        // 标记为参考线, 在 RefreshCurvePreview 中不更新
        refLine.name = "__REFLINE__";

        return rect;
    }

    private TMP_FontAsset GetChineseFont()
    {
        if (m_chineseFont != null) return m_chineseFont;

        var sourceFont = Resources.Load<Font>("Fonts/black");
        if (sourceFont == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 未找到 Fonts/black 字体，使用 TMP 默认字体");
            return null;
        }

        m_chineseFont = TMP_FontAsset.CreateFontAsset(sourceFont);
        if (m_chineseFont != null)
        {
            m_chineseFont.TryAddCharacters(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .锚点编辑数据槽时间数值缓动删除关闭输入权重曲线预览...");
        }

        return m_chineseFont;
    }

    #endregion
}
