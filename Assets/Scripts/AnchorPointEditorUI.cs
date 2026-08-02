using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 长条编辑面板 UI：当选中缓动区长条时，在 FunctionChanger 位置弹出面板，
/// 支持修改长条头尾数值、缓动类型，以及删除长条。
/// 同时展示当前选中的 Cube 编号、面和方向信息。
/// 通过订阅 EasingAreaManager 的 BarSelected / BarDeselected 事件驱动显示。
/// </summary>
public class AnchorPointEditorUI : MonoBehaviour
{
    // ---- 布局常量 ----
    private const float k_panelWidth = 350f;
    private const float k_titleHeight = 50f;
    private const float k_infoHeight = 30f;
    private const float k_inputHeight = 36f;
    private const float k_buttonHeight = 44f;
    private const float k_padding = 12f;
    private const float k_inputWidth = 180f;
    private const float k_previewHeight = 100f;

    // ---- 引用 ----
    private EasingAreaManager m_easingAreaManager;
    private GameObject m_functionChanger;
    private GameObject m_panel;
    private TMP_FontAsset m_chineseFont;

    // ---- UI 控件：信息显示 ----
    private TextMeshProUGUI m_eventTypeLabel;
    private TextMeshProUGUI m_cubeLabel;
    private TextMeshProUGUI m_faceLabel;
    private TextMeshProUGUI m_directionLabel;
    private TextMeshProUGUI m_slotLabel;
    private TextMeshProUGUI m_timeLabel;

    // ---- UI 控件：数值输入 ----
    private TMP_InputField m_startValueInput;
    private TMP_InputField m_endValueInput;

    // ---- UI 控件：缓动设置 ----
    private TMP_Dropdown m_easingDropdown;
    private Slider m_weightSlider;
    private TextMeshProUGUI m_weightValueLabel;

    // ---- UI 控件：全局模式方体/轨道变更 ----
    private GameObject m_globalSection;
    private TMP_InputField m_cubeIdInput;
    private TMP_Dropdown m_faceDropdown;
    private TMP_Dropdown m_directionDropdown;

    // ---- UI 控件：曲线预览 ----
    private RectTransform m_previewArea;
    private readonly List<Image> m_previewSegments = new List<Image>();
    private const int k_previewSamples = 40;

    private bool m_needPreviewRefresh;

    private void Start()
    {
        m_easingAreaManager = GetComponent<EasingAreaManager>();
        FindFunctionChanger();

        if (m_easingAreaManager != null)
        {
            m_easingAreaManager.BarSelected += OnBarSelected;
            m_easingAreaManager.BarDeselected += OnBarDeselected;
        }
    }

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
            m_easingAreaManager.BarSelected -= OnBarSelected;
            m_easingAreaManager.BarDeselected -= OnBarDeselected;
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
            Debug.LogWarning($"[{GetType().Name}] 未找到 FunctionChanger，长条编辑面板无法显示");
        }
    }

    #region 事件处理

    /// <summary>
    /// 长条被选中：显示编辑面板
    /// </summary>
    private void OnBarSelected()
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
    /// 长条被取消选中：隐藏编辑面板
    /// </summary>
    private void OnBarDeselected()
    {
        HidePanel();
    }

    #endregion

    #region 面板构建

    /// <summary>
    /// 构建长条编辑面板（首次显示时懒加载）
    /// </summary>
    private void BuildPanel()
    {
        m_panel = CreateUIObject("BarEditorPanel", m_functionChanger.transform);
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
        var title = CreateText("长条编辑", m_panel.transform, 22f);
        PositionElement(title.rectTransform, k_padding, yPos, k_panelWidth - k_padding * 2, k_titleHeight);
        title.alignment = TextAlignmentOptions.Center;
        yPos -= k_titleHeight + k_padding;

        // ---- 事件类型标签 ----
        m_eventTypeLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_eventTypeLabel.rectTransform, k_padding, yPos, k_panelWidth - k_padding * 2, k_infoHeight);
        m_eventTypeLabel.alignment = TextAlignmentOptions.Center;
        yPos -= k_infoHeight + k_padding;

        // ---- 方体信息 ----
        var cubeLabelText = CreateText("方体:", m_panel.transform, 16f);
        PositionElement(cubeLabelText.rectTransform, k_padding, yPos, 80, k_infoHeight);
        m_cubeLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_cubeLabel.rectTransform, k_padding + 80, yPos, k_panelWidth - k_padding * 2 - 80, k_infoHeight);
        yPos -= k_infoHeight;

        // ---- 面信息 ----
        var faceLabelText = CreateText("面:", m_panel.transform, 16f);
        PositionElement(faceLabelText.rectTransform, k_padding, yPos, 80, k_infoHeight);
        m_faceLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_faceLabel.rectTransform, k_padding + 80, yPos, k_panelWidth - k_padding * 2 - 80, k_infoHeight);
        yPos -= k_infoHeight;

        // ---- 方向信息 ----
        var dirLabelText = CreateText("方向:", m_panel.transform, 16f);
        PositionElement(dirLabelText.rectTransform, k_padding, yPos, 80, k_infoHeight);
        m_directionLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_directionLabel.rectTransform, k_padding + 80, yPos, k_panelWidth - k_padding * 2 - 80, k_infoHeight);
        yPos -= k_infoHeight;

        // ---- 数据槽信息 ----
        var slotLabelText = CreateText("数据槽:", m_panel.transform, 16f);
        PositionElement(slotLabelText.rectTransform, k_padding, yPos, 80, k_infoHeight);
        m_slotLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_slotLabel.rectTransform, k_padding + 80, yPos, k_panelWidth - k_padding * 2 - 80, k_infoHeight);
        yPos -= k_infoHeight;

        // ---- 时间范围 ----
        var timeLabelText = CreateText("时间:", m_panel.transform, 16f);
        PositionElement(timeLabelText.rectTransform, k_padding, yPos, 80, k_infoHeight);
        m_timeLabel = CreateText("-", m_panel.transform, 16f);
        PositionElement(m_timeLabel.rectTransform, k_padding + 80, yPos, k_panelWidth - k_padding * 2 - 80, k_infoHeight);
        yPos -= k_infoHeight + k_padding;

        // ---- 全局模式：方体/轨道变更（仅全局模式显示）----
        BuildGlobalSection(ref yPos);

        // ---- 起始数值输入 ----
        var startValueLabel = CreateText("起始值:", m_panel.transform, 16f);
        PositionElement(startValueLabel.rectTransform, k_padding, yPos, 80, k_inputHeight);
        m_startValueInput = CreateValueInput(m_panel.transform, "输入起始值...");
        m_startValueInput.onEndEdit.AddListener(OnStartValueEndEdit);
        PositionElement(m_startValueInput.GetComponent<RectTransform>(), k_padding + 80, yPos, k_inputWidth, k_inputHeight);
        yPos -= k_inputHeight + 8;

        // ---- 结束数值输入 ----
        var endValueLabel = CreateText("结束值:", m_panel.transform, 16f);
        PositionElement(endValueLabel.rectTransform, k_padding, yPos, 80, k_inputHeight);
        m_endValueInput = CreateValueInput(m_panel.transform, "输入结束值...");
        m_endValueInput.onEndEdit.AddListener(OnEndValueEndEdit);
        PositionElement(m_endValueInput.GetComponent<RectTransform>(), k_padding + 80, yPos, k_inputWidth, k_inputHeight);
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
        var deleteBtn = CreateButton("删除长条", m_panel.transform, new Color(0.7f, 0.2f, 0.2f, 1f));
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
    /// 构建全局模式方体/轨道变更区域（默认隐藏，仅全局模式显示）
    /// </summary>
    private void BuildGlobalSection(ref float yPos)
    {
        m_globalSection = CreateUIObject("GlobalSection", m_panel.transform);

        // 分隔标题
        var sectionTitle = CreateText("── 全局模式设置 ──", m_globalSection.transform, 14f);
        PositionElement(sectionTitle.rectTransform, k_padding, 0, k_panelWidth - k_padding * 2, k_infoHeight);
        sectionTitle.alignment = TextAlignmentOptions.Center;
        sectionTitle.color = new Color(0.8f, 0.6f, 1f, 0.9f);
        float sectionY = -k_infoHeight - 4;

        // 方体 ID 输入
        var cubeIdLabel = CreateText("方体ID:", m_globalSection.transform, 16f);
        PositionElement(cubeIdLabel.rectTransform, k_padding, sectionY, 80, k_inputHeight);
        m_cubeIdInput = CreateValueInput(m_globalSection.transform, "输入方体ID...");
        m_cubeIdInput.onEndEdit.AddListener(OnCubeIdEndEdit);
        PositionElement(m_cubeIdInput.GetComponent<RectTransform>(), k_padding + 80, sectionY, k_inputWidth, k_inputHeight);
        sectionY -= k_inputHeight + 8;

        // 面下拉
        var faceLabel = CreateText("面:", m_globalSection.transform, 16f);
        PositionElement(faceLabel.rectTransform, k_padding, sectionY, 80, k_inputHeight);
        m_faceDropdown = CreateSimpleDropdown(m_globalSection.transform, new[] { "上", "下", "左", "右", "前", "后" });
        m_faceDropdown.onValueChanged.AddListener(OnFaceChanged);
        PositionElement(m_faceDropdown.GetComponent<RectTransform>(), k_padding + 80, sectionY, k_inputWidth, k_inputHeight);
        sectionY -= k_inputHeight + 8;

        // 方向下拉
        var dirLabel = CreateText("方向:", m_globalSection.transform, 16f);
        PositionElement(dirLabel.rectTransform, k_padding, sectionY, 80, k_inputHeight);
        m_directionDropdown = CreateSimpleDropdown(m_globalSection.transform, new[] { "上", "下", "左", "右" });
        m_directionDropdown.onValueChanged.AddListener(OnDirectionChanged);
        PositionElement(m_directionDropdown.GetComponent<RectTransform>(), k_padding + 80, sectionY, k_inputWidth, k_inputHeight);
        sectionY -= k_inputHeight + k_padding;

        // 定位整个 section 容器
        var sectionRect = m_globalSection.GetComponent<RectTransform>();
        sectionRect.anchorMin = new Vector2(0, 1);
        sectionRect.anchorMax = new Vector2(1, 1);
        sectionRect.pivot = new Vector2(0, 1);
        sectionRect.anchoredPosition = new Vector2(0, yPos);
        sectionRect.sizeDelta = new Vector2(0, -sectionY);

        // 调整外部 yPos（为后续元素留出空间）
        yPos += sectionY; // sectionY 是负值，等同于 yPos -= |sectionY|

        m_globalSection.SetActive(false);
    }

    /// <summary>
    /// 创建简单下拉菜单（选项列表）
    /// </summary>
    private TMP_Dropdown CreateSimpleDropdown(Transform parent, string[] options)
    {
        var go = CreateUIObject("Dropdown", parent);

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
        templateRect.sizeDelta = new Vector2(0, 120);
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
        viewport.AddComponent<RectMask2D>();

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
        itemLabelText.fontSize = 14f;
        itemLabelText.color = Color.white;
        itemLabelText.font = GetChineseFont();

        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = checkmarkImg;
        itemToggle.isOn = false;

        dropdown.template = templateRect;
        dropdown.captionText = labelText;
        dropdown.itemText = itemLabelText;

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.value = 0;

        template.SetActive(false);

        return dropdown;
    }

    /// <summary>
    /// 更新面板内容（选中长条变化时调用）
    /// </summary>
    private void UpdatePanelContent()
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        int slot = m_easingAreaManager.SelectedSlot;
        var bar = m_easingAreaManager.GetSelectedBar();
        if (bar == null) return;

        // 事件类型标签
        m_eventTypeLabel.text = bar.isInstant ? "瞬时赋值事件" : "数值变化事件";
        m_eventTypeLabel.color = bar.isInstant
            ? new Color(0.5f, 1f, 0.5f, 1f)
            : new Color(0.85f, 0.5f, 1f, 1f);

        // 方体/面/方向信息
        var cubeManager = m_easingAreaManager.CubeManager;
        if (m_easingAreaManager.IsGlobalMode)
        {
            // 全局模式：显示选中事件的来源方体/面/方向
            int cubeId = m_easingAreaManager.GetSelectedGlobalEventCubeId();
            m_cubeLabel.text = $"Cube {cubeId}";

            if (m_easingAreaManager.IsSelectedGlobalEventTrackLevel())
            {
                m_faceLabel.text = GetFaceDisplayName(m_easingAreaManager.GetSelectedGlobalEventFace());
                m_directionLabel.text = GetDirectionDisplayName(m_easingAreaManager.GetSelectedGlobalEventDirection());
            }
            else
            {
                m_faceLabel.text = "-";
                m_directionLabel.text = "-";
            }
        }
        else if (cubeManager != null)
        {
            m_cubeLabel.text = $"Cube {cubeManager.ActiveCubeId}";
            m_faceLabel.text = GetFaceDisplayName(cubeManager.ActiveFace);
            m_directionLabel.text = GetDirectionDisplayName(cubeManager.ActiveDirection);
        }

        // 数据槽标签
        m_slotLabel.text = m_easingAreaManager.GetSlotLabel(slot);

        // 时间范围
        m_timeLabel.text = $"{bar.startTime:F2}s ~ {bar.endTime:F2}s";

        // 起始/结束数值
        m_startValueInput.text = bar.startValue.ToString("F3");
        m_endValueInput.text = bar.endValue.ToString("F3");

        // 缓动类型：旧数据中 easingType 可能不在可用列表（GetIndex 返回 -1），回退 0。
        // SetValueWithoutNotify 避免赋值触发 OnEasingChanged 静默改值并写盘
        int easeIndex = EaseDisplayNames.GetIndex(bar.easingType);
        m_easingDropdown.SetValueWithoutNotify(easeIndex >= 0 ? easeIndex : 0);

        // 权重：SetValueWithoutNotify 避免打开面板时触发 OnWeightChanged 写盘
        m_weightSlider.SetValueWithoutNotify(bar.weight);
        m_weightValueLabel.text = bar.weight.ToString("F2", CultureInfo.InvariantCulture);

        // 全局模式：显示方体/轨道变更区域
        UpdateGlobalSection();

        // 刷新曲线预览
        RefreshCurvePreview();
    }

    /// <summary>
    /// 更新全局模式设置区域的显示状态和内容
    /// </summary>
    private void UpdateGlobalSection()
    {
        if (m_globalSection == null) return;

        bool showGlobal = m_easingAreaManager != null && m_easingAreaManager.IsGlobalMode;
        m_globalSection.SetActive(showGlobal);

        if (!showGlobal) return;

        // 填充方体 ID
        int cubeId = m_easingAreaManager.GetSelectedGlobalEventCubeId();
        m_cubeIdInput.text = cubeId.ToString();

        // 轨道级事件显示面/方向选择器
        bool isTrackLevel = m_easingAreaManager.IsSelectedGlobalEventTrackLevel();
        m_faceDropdown.gameObject.SetActive(isTrackLevel);
        m_directionDropdown.gameObject.SetActive(isTrackLevel);

        if (isTrackLevel)
        {
            // SetValueWithoutNotify：仅同步 UI 显示，避免触发 OnFaceChanged/OnDirectionChanged 回调
            // （回调会走 ChangeSelectedGlobalEventTrack，与当前选择相同值时应保持静默）
            m_faceDropdown.SetValueWithoutNotify((int)m_easingAreaManager.GetSelectedGlobalEventFace());
            m_directionDropdown.SetValueWithoutNotify((int)m_easingAreaManager.GetSelectedGlobalEventDirection());
        }
    }

    /// <summary>
    /// 方体 ID 输入结束：变更选中全局事件所属方体
    /// </summary>
    private void OnCubeIdEndEdit(string text)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.IsGlobalMode) return;

        if (int.TryParse(text, out int newCubeId))
        {
            m_easingAreaManager.ChangeSelectedGlobalEventCube(newCubeId);
        }
        else
        {
            // 恢复原值
            int cubeId = m_easingAreaManager.GetSelectedGlobalEventCubeId();
            m_cubeIdInput.text = cubeId.ToString();
        }
    }

    /// <summary>
    /// 面下拉变化：变更选中全局事件所属面（仅轨道级事件）
    /// </summary>
    private void OnFaceChanged(int index)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.IsGlobalMode) return;
        if (!m_easingAreaManager.IsSelectedGlobalEventTrackLevel()) return;

        var newFace = (CubeFace)index;
        var currentDir = m_easingAreaManager.GetSelectedGlobalEventDirection();
        m_easingAreaManager.ChangeSelectedGlobalEventTrack(newFace, currentDir);
    }

    /// <summary>
    /// 方向下拉变化：变更选中全局事件所属方向（仅轨道级事件）
    /// </summary>
    private void OnDirectionChanged(int index)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.IsGlobalMode) return;
        if (!m_easingAreaManager.IsSelectedGlobalEventTrackLevel()) return;

        var currentFace = m_easingAreaManager.GetSelectedGlobalEventFace();
        var newDir = (FaceDirection)index;
        m_easingAreaManager.ChangeSelectedGlobalEventTrack(currentFace, newDir);
    }

    /// <summary>
    /// 将 CubeFace 枚举转换为中文显示名
    /// </summary>
    private string GetFaceDisplayName(CubeFace face)
    {
        return face switch
        {
            CubeFace.Up => "上",
            CubeFace.Down => "下",
            CubeFace.Left => "左",
            CubeFace.Right => "右",
            CubeFace.Front => "前",
            CubeFace.Back => "后",
            _ => face.ToString()
        };
    }

    /// <summary>
    /// 将 FaceDirection 枚举转换为中文显示名
    /// </summary>
    private string GetDirectionDisplayName(FaceDirection dir)
    {
        return dir switch
        {
            FaceDirection.Up => "上",
            FaceDirection.Down => "下",
            FaceDirection.Left => "左",
            FaceDirection.Right => "右",
            _ => dir.ToString()
        };
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
    /// 删除按钮：删除当前选中长条
    /// </summary>
    private void OnDeleteClicked()
    {
        if (m_easingAreaManager != null)
        {
            m_easingAreaManager.DeleteSelectedBar();
        }
    }

    /// <summary>
    /// 关闭按钮：取消选中长条
    /// </summary>
    private void OnCloseClicked()
    {
        if (m_easingAreaManager != null)
        {
            m_easingAreaManager.DeselectBar();
        }
    }

    /// <summary>
    /// 起始数值输入结束：更新长条起始数值
    /// </summary>
    private void OnStartValueEndEdit(string text)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        if (float.TryParse(text, out float value))
        {
            m_easingAreaManager.UpdateSelectedBarStartValue(value);

            // 瞬时事件：同步结束值
            var bar = m_easingAreaManager.GetSelectedBar();
            if (bar != null && bar.isInstant)
            {
                m_easingAreaManager.UpdateSelectedBarEndValue(value);
                m_endValueInput.text = value.ToString("F3");
            }

            // 刷新显示（可能被 Clamp 过）
            if (bar != null)
            {
                m_startValueInput.text = bar.startValue.ToString("F3");
            }
        }
        else
        {
            // 解析失败：恢复原值
            var bar = m_easingAreaManager.GetSelectedBar();
            if (bar != null)
            {
                m_startValueInput.text = bar.startValue.ToString("F3");
            }
        }
    }

    /// <summary>
    /// 结束数值输入结束：更新长条结束数值
    /// </summary>
    private void OnEndValueEndEdit(string text)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        if (float.TryParse(text, out float value))
        {
            m_easingAreaManager.UpdateSelectedBarEndValue(value);

            // 瞬时事件：同步起始值
            var bar = m_easingAreaManager.GetSelectedBar();
            if (bar != null && bar.isInstant)
            {
                m_easingAreaManager.UpdateSelectedBarStartValue(value);
                m_startValueInput.text = value.ToString("F3");
            }

            // 刷新显示（可能被 Clamp 过）
            if (bar != null)
            {
                m_endValueInput.text = bar.endValue.ToString("F3");
            }
        }
        else
        {
            // 解析失败：恢复原值
            var bar = m_easingAreaManager.GetSelectedBar();
            if (bar != null)
            {
                m_endValueInput.text = bar.endValue.ToString("F3");
            }
        }
    }

    /// <summary>
    /// 缓动类型下拉变化：更新长条缓动类型并刷新预览
    /// </summary>
    private void OnEasingChanged(int index)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        m_easingAreaManager.UpdateSelectedBarEasing(EaseDisplayNames.GetEaseAt(index));
        RefreshCurvePreview();
    }

    /// <summary>
    /// 权重滑块变化：更新长条权重并刷新预览
    /// </summary>
    private void OnWeightChanged(float value)
    {
        if (m_easingAreaManager == null || !m_easingAreaManager.HasSelection) return;

        m_weightValueLabel.text = value.ToString("F2", CultureInfo.InvariantCulture);
        m_easingAreaManager.UpdateSelectedBarWeight(value);
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

        var bar = m_easingAreaManager?.GetSelectedBar();
        if (bar == null) return;

        Ease ease = bar.easingType;
        float weight = bar.weight;

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
        img.color = new Color(0.85f, 0.5f, 1f, 0.9f);
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
        go.layer = LayerConstants.Ui;
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

    private TMP_InputField CreateValueInput(Transform parent, string placeholderText)
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

        var placeholderTmp = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderTmp.fontSize = 16f;
        placeholderTmp.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholderTmp.text = placeholderText;
        placeholderTmp.fontStyle = FontStyles.Italic;
        placeholderTmp.font = GetChineseFont();

        input.textViewport = textAreaRect;
        input.textComponent = textComp;
        input.placeholder = placeholderTmp;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;

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
        fillImg.color = new Color(0.6f, 0.2f, 0.8f, 1f);

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
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .长条编辑方体面方向数据槽时间起始值结束缓动删除关闭输入权重曲线预览前后上下左右Cube瞬时赋值事件数值变化全局模式设置ID──");
        }

        return m_chineseFont;
    }

    #endregion
}
