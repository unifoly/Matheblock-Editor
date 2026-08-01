using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using HexMap;

/// <summary>
/// 右半缓动函数区管理器：15 条竖线数据槽，每条对应一个方体属性。
/// 用户通过 S 键两次确认创建紫色长条（表示一段时间内的数值变化），
/// 长条内通过缓动函数插值，长条之外数值保持不变。
/// Y 轴与左侧 Note 区同步（共享 GridManager 时间轴），支持水平拖拽滚动。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EasingAreaManager : MonoBehaviour
{
    /// <summary>
    /// 15 个数据槽标签，顺序对应 CubeData 中的属性字段。
    /// lx/ly/lz=长宽高, rx/ry/rz=倾斜角, px/py/pz=位置,
    /// R/G/B/A=颜色, 棱偏移=中间note偏移, 流速=下落速度倍率
    /// </summary>
    private static readonly string[] k_slotLabels =
    {
        "lx", "ly", "lz",
        "rx", "ry", "rz",
        "px", "py", "pz",
        "R", "G", "B", "A",
        "棱偏移", "流速"
    };

    // ---- 点击 vs 拖拽判定阈值 ----
    private const float k_clickThreshold = 6f;

    // ---- 快捷键 Action 名（与 KeyBindingsStore 持久化一致）----
    private const string k_actionDelete = "Bar_Delete";
    private const string k_actionCreate = "Bar_Create";
    private const string k_actionGlobal = "Global_Toggle";

    [Header("缓动区设置")]
    [Tooltip("竖线数量（数据槽数量）")]
    [SerializeField] private int m_lineCount = 15;
    [Tooltip("竖线间距（像素）")]
    [SerializeField] private float m_lineSpacing = 80f;
    [Tooltip("竖线颜色")]
    [SerializeField] private Color m_lineColor = new Color(0.5f, 0.7f, 1f, 0.6f);
    [Tooltip("缓动区背景色")]
    [SerializeField] private Color m_backgroundColor = new Color(0.12f, 0.12f, 0.16f, 0.5f);
    [Tooltip("水平参考线颜色")]
    [SerializeField] private Color m_hLineColor = new Color(1f, 1f, 1f, 0.15f);
    [Tooltip("水平参考线数量")]
    [SerializeField] private int m_hLineCount = 8;

    [Header("标签设置")]
    [Tooltip("标签字体大小")]
    [SerializeField] private float m_labelFontSize = 22f;
    [Tooltip("标签颜色")]
    [SerializeField] private Color m_labelColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("长条与曲线设置")]
    [Tooltip("长条背景色（紫色半透明）")]
    [SerializeField] private Color m_barColor = new Color(0.6f, 0.2f, 0.8f, 0.3f);
    [Tooltip("选中长条背景色")]
    [SerializeField] private Color m_barSelectedColor = new Color(0.7f, 0.3f, 0.9f, 0.5f);
    [Tooltip("长条宽度占竖线间距的比例")]
    [SerializeField] private float m_barWidthRatio = 0.7f;
    [Tooltip("曲线颜色（亮紫色）")]
    [SerializeField] private Color m_curveColor = new Color(0.85f, 0.5f, 1f, 0.9f);
    [Tooltip("曲线线宽（像素）")]
    [SerializeField] private float m_curveWidth = 2.5f;
    [Tooltip("每段缓动曲线的采样数")]
    [SerializeField] private int m_curveSamples = 24;
    [Tooltip("待定长条标记颜色")]
    [SerializeField] private Color m_pendingMarkerColor = new Color(0.8f, 0.4f, 1f, 0.8f);

    [Tooltip("轨道级长条背景色（棱偏移/流速，橙色）")]
    [SerializeField] private Color m_trackBarColor = new Color(0.8f, 0.6f, 0.2f, 0.3f);
    [Tooltip("选中轨道级长条背景色")]
    [SerializeField] private Color m_trackBarSelectedColor = new Color(0.9f, 0.7f, 0.3f, 0.5f);

    [Header("瞬时事件设置")]
    [Tooltip("瞬时事件长条颜色（浅绿色）")]
    [SerializeField] private Color m_instantBarColor = new Color(0.5f, 1f, 0.5f, 0.6f);
    [Tooltip("瞬时事件选中颜色")]
    [SerializeField] private Color m_instantBarSelectedColor = new Color(0.7f, 1f, 0.7f, 0.85f);
    [Tooltip("瞬时事件长条高度（像素）")]
    [SerializeField] private float m_instantBarHeight = 10f;

    [Header("全局事件区设置")]
    [Tooltip("全局事件区每条轨道宽度（像素）")]
    [SerializeField] private float m_globalLaneWidth = 120f;
    [Tooltip("全局事件区瞬时事件高度（像素）")]
    [SerializeField] private float m_globalInstantBarHeight = 14f;
    [Tooltip("全局事件区长条背景色（与常规编辑一致：蓝紫）")]
    [SerializeField] private Color m_globalBarColor = new Color(0.6f, 0.2f, 0.8f, 0.3f);
    [Tooltip("全局事件区瞬时事件背景色（青色）")]
    [SerializeField] private Color m_globalInstantColor = new Color(0.2f, 0.85f, 0.85f, 0.5f);
    [Tooltip("全局事件区轨道级事件背景色（棱偏移/流速，橙色）")]
    [SerializeField] private Color m_globalTrackBarColor = new Color(0.8f, 0.6f, 0.2f, 0.35f);
    [Tooltip("全局事件区标签字体大小")]
    [SerializeField] private float m_globalLabelFontSize = 16f;
    [Tooltip("全局事件区事件代号标签颜色")]
    [SerializeField] private Color m_globalCodeLabelColor = new Color(1f, 1f, 1f, 0.95f);
    [Tooltip("全局事件区方体信息标签颜色")]
    [SerializeField] private Color m_globalInfoLabelColor = new Color(1f, 1f, 0.5f, 0.9f);

    // ---- 事件 ----
    /// <summary>长条被选中时触发</summary>
    public event Action BarSelected;
    /// <summary>长条取消选中时触发</summary>
    public event Action BarDeselected;

    // ---- 引用 ----
    private GridManager m_gridManager;
    private CubeManager m_cubeManager;
    private RectTransform m_playScreenRect;
    private RectTransform m_easingViewport;
    private RectTransform m_easingContent;
    private TMP_FontAsset m_chineseFont;

    // ---- 长条背景渲染 ----
    private RectTransform m_barLayer;
    private readonly List<GameObject> m_barVisuals = new List<GameObject>();

    // ---- 曲线渲染（Image 线段池） ----
    private RectTransform m_curveLayer;
    private readonly List<Image> m_curveSegments = new List<Image>();

    // ---- 待定长条标记（S 键第一次按下后显示） ----
    private GameObject m_pendingMarker;
    private GameObject m_pendingPreview;

    // ---- 选择状态 ----
    private int m_selectedSlot = -1;
    private int m_selectedBarIndex = -1;

    // ---- 待定长条状态（S 键两次确认） ----
    private bool m_isPendingBar;
    private int m_pendingBarSlot = -1;
    private float m_pendingBarStartTime;

    // ---- 快捷键 ----
    private KeyCombo m_deleteCombo;
    private bool m_deleteComboLoaded;
    private KeyCombo m_createCombo;
    private bool m_createComboLoaded;
    private KeyCombo m_globalCombo;
    private bool m_globalComboLoaded;

    // ---- 水平拖拽滚动状态 ----
    private bool m_isDragging;
    private bool m_isPotentialClick;
    private Vector2 m_mouseDownPos;
    private float m_lastMouseX;
    private float m_easingScrollOffset;

    // ---- 内容总宽度与最大滚动范围 ----
    private float m_contentWidth;
    private float m_maxScroll;

    // ---- 上次垂直滚动量（用于检测是否需要重绘） ----
    private float m_lastGridScrollOffset = float.MinValue;

    // ---- 初始化标记 ----
    private bool m_needInitialRebuild;

    // ---- 全局事件区状态 ----
    private bool m_isGlobalMode;
    private RectTransform m_globalLayer;
    private readonly List<GameObject> m_globalBarVisuals = new List<GameObject>();
    private readonly List<GlobalEventData> m_globalEvents = new List<GlobalEventData>();
    private float m_globalContentWidth;
    private bool m_needGlobalRebuild;
    private int m_globalSelectedIndex = -1;

    // ---- 暴露 CubeManager 供编辑面板查询 cube/面/方向信息 ----
    public CubeManager CubeManager => m_cubeManager;

    /// <summary>当前是否处于全局事件区模式</summary>
    public bool IsGlobalMode => m_isGlobalMode;

    #region Unity 生命周期

    private void Start()
    {
        m_playScreenRect = GetComponent<RectTransform>();
        CacheGridManager();
        CacheCubeManager();
        CreateEasingArea();
        m_needInitialRebuild = true;
    }

    private bool m_cubeEventSubscribed;
    private bool m_trackEventSubscribed;

    private void OnEnable()
    {
        CacheCubeManager();
        TrySubscribeCubeEvent();
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        if (m_cubeManager != null)
        {
            if (m_cubeEventSubscribed)
            {
                m_cubeManager.ActiveCubeChanged -= OnCubeChanged;
                m_cubeEventSubscribed = false;
            }
            if (m_trackEventSubscribed)
            {
                m_cubeManager.ActiveTrackChanged -= OnTrackChanged;
                m_trackEventSubscribed = false;
            }
        }
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    /// <summary>
    /// Setting 场景关闭后重新加载快捷键（用户可能修改了绑定）
    /// </summary>
    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == "Setting")
        {
            LoadShortcuts();
        }
    }

    /// <summary>
    /// 从 KeyBindingsStore 加载快捷键绑定
    /// </summary>
    private void LoadShortcuts()
    {
        m_deleteCombo = KeyBindingsStore.GetKeyCombo(k_actionDelete, KeyCombo.Parse("Delete"));
        m_deleteComboLoaded = true;
        m_createCombo = KeyBindingsStore.GetKeyCombo(k_actionCreate, KeyCombo.Parse("S"));
        m_createComboLoaded = true;
        m_globalCombo = KeyBindingsStore.GetKeyCombo(k_actionGlobal, KeyCombo.Parse("O"));
        m_globalComboLoaded = true;
    }

    private void Update()
    {
        CacheGridManager();
        CacheCubeManager();
        TrySubscribeCubeEvent();
        if (m_easingContent == null) return;

        // 首帧 CubeManager 就绪后重建长条（显示已加载的数据）
        if (m_needInitialRebuild && m_cubeManager != null)
        {
            EnsureSlotDataExists();
            RebuildBarVisuals();
            m_needInitialRebuild = false;
        }

        // 全局事件区需要重建时（方体/轨道切换或数据变化后）
        if (m_isGlobalMode && m_needGlobalRebuild && m_cubeManager != null)
        {
            CollectGlobalEvents();
            AssignGlobalLanes();
            RebuildGlobalBarVisuals();
            m_needGlobalRebuild = false;
        }

        UpdateMaxScroll();
        HandleMouseInteraction();
        HandleKeyboardShortcuts();

        if (m_isGlobalMode)
        {
            UpdateGlobalBarPositions();
        }
        else
        {
            UpdateBarVisuals();
        }
    }

    /// <summary>
    /// 处理键盘快捷键：S 键创建长条，Delete 删除选中长条。
    /// 文本输入框获焦时跳过，避免与文本编辑冲突。
    /// </summary>
    private void HandleKeyboardShortcuts()
    {
        if (!m_deleteComboLoaded || !m_createComboLoaded || !m_globalComboLoaded)
        {
            LoadShortcuts();
        }

        if (UndoRedoManager.IsTextInputFocused()) return;

        // 全局事件快捷键：切换全局事件区模式
        if (m_globalCombo.IsPressed())
        {
            ToggleGlobalMode();
            return;
        }

        // 全局事件区模式下：S 键创建，Delete 删除，Escape 取消
        if (m_isGlobalMode)
        {
            if (m_createCombo.IsPressed())
            {
                HandleGlobalBarCreation();
            }

            if (m_deleteCombo.IsPressed() && m_globalSelectedIndex >= 0)
            {
                DeleteSelectedBar();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (m_isPendingBar)
                {
                    CancelPendingBar();
                }
                else if (m_globalSelectedIndex >= 0)
                {
                    DeselectBar();
                }
            }
            return;
        }

        // S 键：创建长条（两次确认）
        if (m_createCombo.IsPressed())
        {
            HandleBarCreation();
        }

        // Delete 键：删除选中长条
        if (m_deleteCombo.IsPressed() && HasSelection)
        {
            DeleteSelectedBar();
        }

        // Escape：取消待定状态或取消选中
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (m_isPendingBar)
            {
                CancelPendingBar();
            }
            else if (HasSelection)
            {
                DeselectBar();
            }
        }
    }

    /// <summary>
    /// 尝试订阅 CubeManager 事件（仅订阅一次）
    /// </summary>
    private void TrySubscribeCubeEvent()
    {
        if (m_cubeManager != null)
        {
            if (!m_cubeEventSubscribed)
            {
                m_cubeManager.ActiveCubeChanged += OnCubeChanged;
                m_cubeEventSubscribed = true;
            }
            if (!m_trackEventSubscribed)
            {
                m_cubeManager.ActiveTrackChanged += OnTrackChanged;
                m_trackEventSubscribed = true;
            }
        }
    }

    #endregion

    #region 初始化

    private void CacheGridManager()
    {
        if (m_gridManager == null)
        {
            m_gridManager = GetComponent<GridManager>();
        }
    }

    private void CacheCubeManager()
    {
        if (m_cubeManager == null)
        {
            var cubeSystemObj = GameObject.Find("CubeSystem");
            if (cubeSystemObj != null)
            {
                m_cubeManager = cubeSystemObj.GetComponent<CubeManager>();
            }
        }
    }

    /// <summary>
    /// 创建缓动区 viewport、content 及竖线
    /// </summary>
    private void CreateEasingArea()
    {
        if (m_playScreenRect == null) return;

        // 若已存在则复用引用，但确保新层已创建
        var existing = transform.Find("EasingViewport");
        if (existing != null)
        {
            m_easingViewport = existing as RectTransform;
            m_easingContent = m_easingViewport.Find("EasingContent") as RectTransform;

            // 确保新增的功能层存在
            if (m_easingContent != null)
            {
                // 清理旧的 AnchorLayer（兼容旧版）
                var oldAnchorLayer = m_easingContent.Find("AnchorLayer");
                if (oldAnchorLayer != null) Destroy(oldAnchorLayer.gameObject);

                if (m_easingContent.Find("BarLayer") == null)
                {
                    CreateBarLayer();
                }
                else
                {
                    m_barLayer = m_easingContent.Find("BarLayer") as RectTransform;
                }
                if (m_easingContent.Find("CurveLayer") == null)
                {
                    CreateCurveLayer();
                }
                else
                {
                    m_curveLayer = m_easingContent.Find("CurveLayer") as RectTransform;
                }
                if (m_easingContent.Find("GlobalLayer") == null)
                {
                    CreateGlobalLayer();
                }
                else
                {
                    m_globalLayer = m_easingContent.Find("GlobalLayer") as RectTransform;
                    m_globalLayer.gameObject.SetActive(false);
                }
            }
            return;
        }

        float noteAreaRatio = m_gridManager != null ? m_gridManager.m_noteAreaRatio : 0.5f;

        // ---- EasingViewport：右半区域容器，带遮罩裁剪 ----
        var viewportGo = new GameObject("EasingViewport", typeof(RectTransform));
        viewportGo.transform.SetParent(transform, false);
        viewportGo.layer = 5;

        m_easingViewport = viewportGo.GetComponent<RectTransform>();
        m_easingViewport.anchorMin = new Vector2(noteAreaRatio, 0);
        m_easingViewport.anchorMax = new Vector2(1, 1);
        m_easingViewport.offsetMin = Vector2.zero;
        m_easingViewport.offsetMax = Vector2.zero;
        m_easingViewport.pivot = new Vector2(0.5f, 0.5f);

        var bg = viewportGo.AddComponent<Image>();
        bg.color = m_backgroundColor;
        bg.raycastTarget = false;

        viewportGo.AddComponent<RectMask2D>();

        // 确保 EasingViewport 在最上层渲染，遮挡 GridContainer 的网格线
        m_easingViewport.SetAsLastSibling();

        // ---- EasingContent：可水平滚动的内容容器 ----
        var contentGo = new GameObject("EasingContent", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        contentGo.layer = 5;

        m_easingContent = contentGo.GetComponent<RectTransform>();
        m_easingContent.anchorMin = new Vector2(0, 0);
        m_easingContent.anchorMax = new Vector2(0, 1);
        m_easingContent.pivot = new Vector2(0, 0.5f);
        m_easingContent.anchoredPosition = Vector2.zero;

        m_contentWidth = m_lineCount * m_lineSpacing + m_lineSpacing;
        m_easingContent.sizeDelta = new Vector2(m_contentWidth, 0);

        UpdateMaxScroll();

        // 绘制各层
        DrawVerticalLines();
        CreateBarLayer();
        CreateCurveLayer();
        CreateGlobalLayer();
    }

    /// <summary>
    /// 绘制 15 条竖线及对应标签
    /// </summary>
    private void DrawVerticalLines()
    {
        if (m_easingContent == null) return;

        float startX = m_lineSpacing * 0.5f;

        for (int i = 0; i < m_lineCount; i++)
        {
            var lineGo = new GameObject($"EasingVLine_{i}", typeof(RectTransform));
            lineGo.transform.SetParent(m_easingContent, false);
            lineGo.layer = 5;

            var rect = lineGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2, 0);
            rect.anchoredPosition = new Vector2(startX + i * m_lineSpacing, 0);

            var img = lineGo.AddComponent<Image>();
            img.color = m_lineColor;
            img.raycastTarget = false;

            string label = i < k_slotLabels.Length ? k_slotLabels[i] : i.ToString();
            CreateSlotLabel(label, startX + i * m_lineSpacing);
        }
    }

    /// <summary>
    /// 在指定 X 坐标处创建数据槽标签（位于内容区域顶部）
    /// </summary>
    private void CreateSlotLabel(string text, float posX)
    {
        var labelGo = new GameObject($"SlotLabel_{text}", typeof(RectTransform));
        labelGo.transform.SetParent(m_easingContent, false);
        labelGo.layer = 5;

        var rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(m_lineSpacing, m_labelFontSize + 6f);
        rect.anchoredPosition = new Vector2(posX, -4f);

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = m_labelFontSize;
        tmp.color = m_labelColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.font = GetChineseFont();
        tmp.raycastTarget = false;
    }

    /// <summary>
    /// 创建长条背景层
    /// </summary>
    private void CreateBarLayer()
    {
        var barGo = new GameObject("BarLayer", typeof(RectTransform));
        barGo.transform.SetParent(m_easingContent, false);
        barGo.layer = 5;

        m_barLayer = barGo.GetComponent<RectTransform>();
        m_barLayer.anchorMin = new Vector2(0, 0);
        m_barLayer.anchorMax = new Vector2(0, 1);
        m_barLayer.pivot = new Vector2(0, 0.5f);
        m_barLayer.sizeDelta = new Vector2(m_contentWidth, 0);
        m_barLayer.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 创建曲线渲染层（Image 线段池的容器）
    /// </summary>
    private void CreateCurveLayer()
    {
        var curveGo = new GameObject("CurveLayer", typeof(RectTransform));
        curveGo.transform.SetParent(m_easingContent, false);
        curveGo.layer = 5;

        m_curveLayer = curveGo.GetComponent<RectTransform>();
        m_curveLayer.anchorMin = new Vector2(0, 0);
        m_curveLayer.anchorMax = new Vector2(0, 1);
        m_curveLayer.pivot = new Vector2(0, 0.5f);
        m_curveLayer.sizeDelta = new Vector2(m_contentWidth, 0);
        m_curveLayer.anchoredPosition = Vector2.zero;
    }

    #endregion

    #region S 键长条创建（两次确认）

    /// <summary>
    /// 处理 S 键长条创建：鼠标指在格点上第一次按下确定起点，第二次按下确定终点。
    /// </summary>
    private void HandleBarCreation()
    {
        if (m_playScreenRect == null || m_easingViewport == null) return;

        // 检测鼠标是否在缓动区内
        Vector2 viewportLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingViewport, Input.mousePosition, null, out viewportLocal);
        bool inEasingArea = m_easingViewport.rect.Contains(viewportLocal);

        if (!inEasingArea)
        {
            // 鼠标不在缓动区内时取消待定状态
            if (m_isPendingBar)
            {
                CancelPendingBar();
            }
            return;
        }

        // 将鼠标位置转换为 EasingContent 本地坐标
        Vector2 contentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingContent, Input.mousePosition, null, out contentLocal);

        // 确定数据槽
        float startX = m_lineSpacing * 0.5f;
        int slot = Mathf.FloorToInt((contentLocal.x - startX + m_lineSpacing * 0.5f) / m_lineSpacing);
        slot = Mathf.Clamp(slot, 0, m_lineCount - 1);

        // 将 Y 转换为时间并吸附到节拍
        float rawTime = LocalYToTime(contentLocal.y);
        float snappedTime = SnapToBeat(rawTime);

        if (!m_isPendingBar)
        {
            // time=0 已有初始瞬时事件，不允许在此创建
            if (Mathf.Approximately(snappedTime, 0f))
            {
                Debug.LogWarning($"[{GetType().Name}] time=0 已有初始瞬时事件，请选择其他时间点");
                return;
            }

            // 第一次按下：记录起点
            m_isPendingBar = true;
            m_pendingBarSlot = slot;
            m_pendingBarStartTime = snappedTime;
            ShowPendingMarker(slot, snappedTime);
            Debug.Log($"[{GetType().Name}] 长条起点已确定: 槽{slot} 时间{snappedTime:F2}s，请移动鼠标并再次按 S 确定终点");
        }
        else
        {
            // 第二次按下：确定终点并创建长条
            if (slot != m_pendingBarSlot)
            {
                Debug.LogWarning($"[{GetType().Name}] 第二次按下 S 时槽位不一致（{m_pendingBarSlot} vs {slot}），已取消");
                CancelPendingBar();
                return;
            }

            float startTime = m_pendingBarStartTime;
            float endTime = snappedTime;

            // 同一格点两次确认 -> 瞬时赋值事件
            if (Mathf.Approximately(startTime, endTime))
            {
                CancelPendingBar();
                AddInstantBar(slot, startTime);
                return;
            }

            // 确保起点在前
            if (endTime < startTime)
            {
                float temp = startTime;
                startTime = endTime;
                endTime = temp;
            }

            // 检查最小时长（至少半拍）
            float bpm = BpmManagerUI.GetBpmAtTime(startTime);
            if (bpm <= 0) bpm = m_gridManager != null ? m_gridManager.m_referenceBpm : 120f;
            float intervalFactor = m_gridManager != null ? m_gridManager.IntervalFactor : 1f;
            float beatInterval = intervalFactor / bpm;
            float minDuration = beatInterval * 0.5f;

            if (endTime - startTime < minDuration)
            {
                Debug.LogWarning($"[{GetType().Name}] 长条时长过短（{endTime - startTime:F2}s），需要至少半拍");
                CancelPendingBar();
                return;
            }

            CancelPendingBar();
            AddBar(slot, startTime, endTime);
        }
    }

    /// <summary>
    /// 显示待定长条起点标记
    /// </summary>
    private void ShowPendingMarker(int slot, float time)
    {
        // 清理旧标记（不影响待定状态）
        if (m_pendingMarker != null)
        {
            Destroy(m_pendingMarker);
            m_pendingMarker = null;
        }
        if (m_pendingPreview != null)
        {
            Destroy(m_pendingPreview);
            m_pendingPreview = null;
        }

        // 起点标记：紫色水平线
        m_pendingMarker = new GameObject("PendingMarker", typeof(RectTransform));
        m_pendingMarker.transform.SetParent(m_barLayer, false);
        m_pendingMarker.layer = 5;

        float slotCenter = m_lineSpacing * 0.5f + slot * m_lineSpacing;
        float barWidth = m_lineSpacing * m_barWidthRatio;
        float y = TimeToLocalY(time);

        var rect = m_pendingMarker.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(barWidth, 4f);
        rect.anchoredPosition = new Vector2(slotCenter, y);

        var img = m_pendingMarker.AddComponent<Image>();
        img.color = m_pendingMarkerColor;
        img.raycastTarget = false;

        // 预览矩形（从起点到鼠标当前位置）
        m_pendingPreview = new GameObject("PendingPreview", typeof(RectTransform));
        m_pendingPreview.transform.SetParent(m_barLayer, false);
        m_pendingPreview.layer = 5;

        var previewRect = m_pendingPreview.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0, 0.5f);
        previewRect.anchorMax = new Vector2(0, 0.5f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.anchoredPosition = new Vector2(slotCenter, y);

        var previewImg = m_pendingPreview.AddComponent<Image>();
        previewImg.color = new Color(m_barColor.r, m_barColor.g, m_barColor.b, m_barColor.a * 0.5f);
        previewImg.raycastTarget = false;
    }

    /// <summary>
    /// 更新待定长条预览（每帧调用，跟随鼠标位置）
    /// </summary>
    private void UpdatePendingPreview()
    {
        if (!m_isPendingBar || m_pendingPreview == null || m_easingViewport == null) return;

        Vector2 viewportLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingViewport, Input.mousePosition, null, out viewportLocal);
        if (!m_easingViewport.rect.Contains(viewportLocal)) return;

        Vector2 contentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingContent, Input.mousePosition, null, out contentLocal);

        float currentY = contentLocal.y;
        float startY = TimeToLocalY(m_pendingBarStartTime);

        float barWidth = m_lineSpacing * m_barWidthRatio;
        float slotCenter = m_lineSpacing * 0.5f + m_pendingBarSlot * m_lineSpacing;
        float previewY = (startY + currentY) * 0.5f;
        float previewHeight = Mathf.Abs(currentY - startY);

        var rect = m_pendingPreview.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(slotCenter, previewY);
        rect.sizeDelta = new Vector2(barWidth, Mathf.Max(previewHeight, 2f));
    }

    /// <summary>
    /// 取消待定长条状态
    /// </summary>
    private void CancelPendingBar()
    {
        m_isPendingBar = false;
        m_pendingBarSlot = -1;

        if (m_pendingMarker != null)
        {
            Destroy(m_pendingMarker);
            m_pendingMarker = null;
        }
        if (m_pendingPreview != null)
        {
            Destroy(m_pendingPreview);
            m_pendingPreview = null;
        }
    }

    #endregion

    #region 鼠标交互

    /// <summary>
    /// 处理鼠标交互：区分点击与拖拽。
    /// 点击长条 -> 选中；拖拽 -> 水平滚动。
    /// </summary>
    private void HandleMouseInteraction()
    {
        if (m_playScreenRect == null || m_easingViewport == null) return;

        // 命中检测：鼠标是否在缓动区内
        Vector2 viewportLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingViewport, Input.mousePosition, null, out viewportLocal);
        bool inEasingArea = m_easingViewport.rect.Contains(viewportLocal);

        // 鼠标按下
        if (Input.GetMouseButtonDown(0) && inEasingArea)
        {
            m_isDragging = true;
            m_isPotentialClick = true;
            m_mouseDownPos = Input.mousePosition;
            m_lastMouseX = Input.mousePosition.x;
        }

        // 鼠标抬起：判定点击 vs 拖拽
        if (Input.GetMouseButtonUp(0))
        {
            if (m_isPotentialClick && inEasingArea)
            {
                HandleClick();
            }
            m_isDragging = false;
            m_isPotentialClick = false;
        }

        if (!m_isDragging) return;

        // 拖拽中：超过阈值则取消点击意图
        float delta = Input.mousePosition.x - m_lastMouseX;
        if (m_isPotentialClick && Mathf.Abs(Input.mousePosition.x - m_mouseDownPos.x) > k_clickThreshold)
        {
            m_isPotentialClick = false;
        }

        if (!m_isPotentialClick)
        {
            ApplyScroll(-delta);
        }

        m_lastMouseX = Input.mousePosition.x;
    }

    /// <summary>
    /// 处理点击：选中已有长条
    /// </summary>
    private void HandleClick()
    {
        // 全局事件区模式下：点击选中全局长条
        if (m_isGlobalMode)
        {
            HandleGlobalClick();
            return;
        }

        // 将鼠标位置转换为 EasingContent 本地坐标
        Vector2 contentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingContent, Input.mousePosition, null, out contentLocal);

        // 确定点击的数据槽
        float startX = m_lineSpacing * 0.5f;
        int slot = Mathf.FloorToInt((contentLocal.x - startX + m_lineSpacing * 0.5f) / m_lineSpacing);
        slot = Mathf.Clamp(slot, 0, m_lineCount - 1);

        // 将 Y 转换为时间
        float time = LocalYToTime(contentLocal.y);

        // 查找点击位置的长条
        int barIndex = FindBarAt(slot, time);
        if (barIndex >= 0)
        {
            SelectBar(slot, barIndex);
        }
        else
        {
            DeselectBar();
        }
    }

    /// <summary>
    /// 在指定数据槽中查找包含给定时间的长条。
    /// 普通长条使用精确时间范围匹配；瞬时事件使用半拍容差（因为始末时间相同）。
    /// 优先匹配普通长条，其次匹配瞬时事件。
    /// </summary>
    private int FindBarAt(int slot, float time)
    {
        EasingSlotData slotData = GetSlotData(slot);
        if (slotData == null || slotData.bars == null) return -1;

        // 第一遍：精确匹配普通长条
        for (int i = 0; i < slotData.bars.Count; i++)
        {
            var bar = slotData.bars[i];
            if (!bar.isInstant && time >= bar.startTime && time <= bar.endTime)
            {
                return i;
            }
        }

        // 第二遍：容差匹配瞬时事件
        float bpm = BpmManagerUI.GetBpmAtTime(time);
        if (bpm <= 0) bpm = m_gridManager != null ? m_gridManager.m_referenceBpm : 120f;
        float intervalFactor = m_gridManager != null ? m_gridManager.IntervalFactor : 1f;
        float tolerance = intervalFactor / bpm * 0.5f; // 半拍容差

        for (int i = 0; i < slotData.bars.Count; i++)
        {
            var bar = slotData.bars[i];
            if (bar.isInstant && Mathf.Abs(time - bar.startTime) <= tolerance)
            {
                return i;
            }
        }

        return -1;
    }

    #endregion

    #region 长条管理

    /// <summary>
    /// 添加新长条，注册撤回/重做。
    /// 起始值和结束值默认为当前时间的插值结果（无长条则用配置默认值）。
    /// </summary>
    private void AddBar(int slot, float startTime, float endTime)
    {
        EasingSlotData slotData = GetOrCreateSlotData(slot);
        if (slotData == null) return;

        var config = EasingSlotConfigs.Slots[slot];

        // 头尾数值默认为当前数值
        float startValue = slotData.EvaluateAt(startTime, config.defaultValue, config);
        float endValue = slotData.EvaluateAt(endTime, config.defaultValue, config);

        var bar = new EasingBar(startTime, endTime, startValue, endValue, Ease.Linear);

        int insertIndex = InsertBarByStartTime(slot, bar);

        SaveCubeData();
        RebuildBarVisuals();
        SelectBar(slot, insertIndex);

        // 记录到全局撤回/重做系统
        var barClone = bar.Clone();
        UndoRedoManager.Execute(
            undo: () =>
            {
                RemoveBarAtIndex(slot, insertIndex);
                SaveCubeData();
                RebuildBarVisuals();
                DeselectBar();
            },
            redo: () =>
            {
                int idx = InsertBarByStartTime(slot, barClone.Clone());
                SaveCubeData();
                RebuildBarVisuals();
                SelectBar(slot, idx);
            });

        Debug.Log($"[{GetType().Name}] 添加长条: 槽{slot} 时间{startTime:F2}s~{endTime:F2}s 值{startValue:F2}->{endValue:F2}");
    }

    /// <summary>
    /// 添加瞬时赋值事件（同一格点两次确认），注册撤回/重做。
    /// 始末值相同，视为在该时间点的瞬时赋值。
    /// </summary>
    private void AddInstantBar(int slot, float time)
    {
        EasingSlotData slotData = GetOrCreateSlotData(slot);
        if (slotData == null) return;

        var config = EasingSlotConfigs.Slots[slot];
        float value = slotData.EvaluateAt(time, config.defaultValue, config);

        var bar = new EasingBar(time, time, value, value, Ease.Linear, 1f, true);

        int insertIndex = InsertBarByStartTime(slot, bar);

        SaveCubeData();
        RebuildBarVisuals();
        SelectBar(slot, insertIndex);

        // 记录到全局撤回/重做系统
        var barClone = bar.Clone();
        UndoRedoManager.Execute(
            undo: () =>
            {
                RemoveBarAtIndex(slot, insertIndex);
                SaveCubeData();
                RebuildBarVisuals();
                DeselectBar();
            },
            redo: () =>
            {
                int idx = InsertBarByStartTime(slot, barClone.Clone());
                SaveCubeData();
                RebuildBarVisuals();
                SelectBar(slot, idx);
            });

        Debug.Log($"[{GetType().Name}] 添加瞬时事件: 槽{slot} 时间{time:F2}s 值{value:F2}");
    }

    /// <summary>
    /// 选中长条
    /// </summary>
    private void SelectBar(int slot, int barIndex)
    {
        m_selectedSlot = slot;
        m_selectedBarIndex = barIndex;
        UpdateBarColors();
        BarSelected?.Invoke();
    }

    /// <summary>
    /// 取消选中
    /// </summary>
    public void DeselectBar()
    {
        if (m_isGlobalMode)
        {
            if (m_globalSelectedIndex < 0) return;
            m_globalSelectedIndex = -1;
            UpdateGlobalBarColors();
            BarDeselected?.Invoke();
            return;
        }

        if (m_selectedSlot < 0) return;
        m_selectedSlot = -1;
        m_selectedBarIndex = -1;
        UpdateBarColors();
        BarDeselected?.Invoke();
    }

    /// <summary>
    /// 删除指定长条，注册撤回/重做。
    /// time=0 处的瞬时事件（初始值）不可删除。
    /// </summary>
    public void DeleteBar(int slot, int barIndex)
    {
        EasingSlotData slotData = GetSlotData(slot);
        if (slotData == null || barIndex < 0 || barIndex >= slotData.bars.Count) return;

        // 不允许删除 time=0 的瞬时事件（初始值）
        var barToDelete = slotData.bars[barIndex];
        if (barToDelete.isInstant && Mathf.Approximately(barToDelete.startTime, 0f))
        {
            Debug.LogWarning($"[{GetType().Name}] time=0 的初始瞬时事件不可删除");
            return;
        }

        // 捕获被删除的长条数据，用于撤回时恢复
        var deletedBar = slotData.bars[barIndex].Clone();

        slotData.bars.RemoveAt(barIndex);
        SaveCubeData();

        if (m_selectedSlot == slot && m_selectedBarIndex == barIndex)
        {
            DeselectBar();
        }
        else if (m_selectedSlot == slot && m_selectedBarIndex > barIndex)
        {
            m_selectedBarIndex--;
        }

        RebuildBarVisuals();

        // 记录到全局撤回/重做系统
        UndoRedoManager.Execute(
            undo: () =>
            {
                int idx = InsertBarByStartTime(slot, deletedBar.Clone());
                SaveCubeData();
                RebuildBarVisuals();
                SelectBar(slot, idx);
            },
            redo: () =>
            {
                RemoveBarAtIndex(slot, barIndex);
                SaveCubeData();
                RebuildBarVisuals();
                DeselectBar();
            });
    }

    /// <summary>
    /// 删除当前选中的长条
    /// </summary>
    public void DeleteSelectedBar()
    {
        if (m_isGlobalMode)
        {
            DeleteGlobalBar(m_globalSelectedIndex);
            return;
        }
        if (m_selectedSlot < 0 || m_selectedBarIndex < 0) return;
        DeleteBar(m_selectedSlot, m_selectedBarIndex);
    }

    /// <summary>
    /// 按起始时间升序将长条插入指定数据槽，返回插入位置的索引。
    /// </summary>
    private int InsertBarByStartTime(int slot, EasingBar bar)
    {
        EasingSlotData slotData = GetOrCreateSlotData(slot);
        if (slotData == null) return -1;

        int insertIndex = 0;
        for (int i = 0; i < slotData.bars.Count; i++)
        {
            if (slotData.bars[i].startTime < bar.startTime)
            {
                insertIndex = i + 1;
            }
            else
            {
                break;
            }
        }
        slotData.bars.Insert(insertIndex, bar);
        return insertIndex;
    }

    /// <summary>
    /// 移除指定数据槽中指定索引的长条。供撤回/重做回调使用。
    /// </summary>
    private void RemoveBarAtIndex(int slot, int index)
    {
        EasingSlotData slotData = GetSlotData(slot);
        if (slotData == null || index < 0 || index >= slotData.bars.Count) return;
        slotData.bars.RemoveAt(index);
    }

    /// <summary>
    /// 更新选中长条的起始数值
    /// </summary>
    public void UpdateSelectedBarStartValue(float value)
    {
        EasingBar bar = GetSelectedBar();
        if (bar == null) return;

        var config = EasingSlotConfigs.Slots[SelectedSlot];
        bar.startValue = Mathf.Clamp(value, config.minValue, config.maxValue);

        SaveCubeData();
        RebuildSelectedVisuals();
    }

    /// <summary>
    /// 更新选中长条的结束数值
    /// </summary>
    public void UpdateSelectedBarEndValue(float value)
    {
        EasingBar bar = GetSelectedBar();
        if (bar == null) return;

        var config = EasingSlotConfigs.Slots[SelectedSlot];
        bar.endValue = Mathf.Clamp(value, config.minValue, config.maxValue);

        SaveCubeData();
        RebuildSelectedVisuals();
    }

    /// <summary>
    /// 更新选中长条的缓动类型
    /// </summary>
    public void UpdateSelectedBarEasing(Ease easingType)
    {
        EasingBar bar = GetSelectedBar();
        if (bar == null) return;

        bar.easingType = easingType;
        SaveCubeData();
    }

    /// <summary>
    /// 更新选中长条的缓动权重 (0=线性, 1=完整缓动)
    /// </summary>
    public void UpdateSelectedBarWeight(float weight)
    {
        EasingBar bar = GetSelectedBar();
        if (bar == null) return;

        bar.weight = weight;
        SaveCubeData();
    }

    /// <summary>
    /// 重建当前模式的可视化（普通模式重建长条，全局模式重建全局长条）
    /// </summary>
    private void RebuildSelectedVisuals()
    {
        if (m_isGlobalMode)
        {
            RebuildGlobalBarVisuals();
        }
        else
        {
            RebuildBarVisuals();
        }
    }

    /// <summary>
    /// 获取当前选中的长条
    /// </summary>
    public EasingBar GetSelectedBar()
    {
        if (m_isGlobalMode)
        {
            if (m_globalSelectedIndex < 0 || m_globalSelectedIndex >= m_globalEvents.Count) return null;
            return m_globalEvents[m_globalSelectedIndex].bar;
        }

        if (m_selectedSlot < 0 || m_selectedBarIndex < 0) return null;
        EasingSlotData slotData = GetSlotData(m_selectedSlot);
        if (slotData == null || m_selectedBarIndex >= slotData.bars.Count) return null;
        return slotData.bars[m_selectedBarIndex];
    }

    /// <summary>当前是否选中了长条</summary>
    public bool HasSelection => m_isGlobalMode
        ? m_globalSelectedIndex >= 0
        : (m_selectedSlot >= 0 && m_selectedBarIndex >= 0);

    /// <summary>选中长条所在的数据槽索引</summary>
    public int SelectedSlot => m_isGlobalMode
        ? (m_globalSelectedIndex >= 0 ? m_globalEvents[m_globalSelectedIndex].slotIndex : -1)
        : m_selectedSlot;

    /// <summary>选中长条在其槽中的索引</summary>
    public int SelectedBarIndex => m_isGlobalMode ? m_globalSelectedIndex : m_selectedBarIndex;

    /// <summary>获取数据槽标签名</summary>
    public string GetSlotLabel(int slot)
    {
        return slot >= 0 && slot < k_slotLabels.Length ? k_slotLabels[slot] : slot.ToString();
    }

    #endregion

    #region 可视化更新

    /// <summary>
    /// 每帧更新长条位置与曲线（跟随时间轴滚动）
    /// </summary>
    private void UpdateBarVisuals()
    {
        if (m_gridManager == null) return;

        // 更新长条背景位置
        UpdateBarPositions();

        // 更新曲线
        UpdateCurvePoints();

        // 更新待定长条预览
        UpdatePendingPreview();
    }

    /// <summary>
    /// 重建所有长条可视化（长条增删后调用）
    /// </summary>
    private void RebuildBarVisuals()
    {
        // 清除旧长条
        foreach (var visual in m_barVisuals)
        {
            if (visual != null) Destroy(visual);
        }
        m_barVisuals.Clear();

        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.bars == null) continue;

            foreach (var bar in slotData.bars)
            {
                var visual = CreateBarVisual(slot, bar);
                m_barVisuals.Add(visual);
            }
        }

        UpdateBarPositions();
        UpdateBarColors();
    }

    /// <summary>
    /// 创建单个长条背景
    /// </summary>
    private GameObject CreateBarVisual(int slot, EasingBar bar)
    {
        var barGo = new GameObject($"Bar_{slot}_{bar.startTime:F1}", typeof(RectTransform));
        barGo.transform.SetParent(m_barLayer, false);
        barGo.layer = 5;

        var rect = barGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = barGo.AddComponent<Image>();
        img.color = m_barColor;
        img.raycastTarget = false;

        return barGo;
    }

    /// <summary>
    /// 更新所有长条背景的位置和尺寸（跟随时间轴滚动）
    /// </summary>
    private void UpdateBarPositions()
    {
        if (m_gridManager == null) return;

        float barWidth = m_lineSpacing * m_barWidthRatio;
        int visualIdx = 0;

        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.bars == null) continue;

            float slotCenter = m_lineSpacing * 0.5f + slot * m_lineSpacing;

            foreach (var bar in slotData.bars)
            {
                if (visualIdx >= m_barVisuals.Count) break;
                var visual = m_barVisuals[visualIdx];
                if (visual != null)
                {
                    float startY = TimeToLocalY(bar.startTime);
                    float endY = TimeToLocalY(bar.endTime);
                    float center = (startY + endY) * 0.5f;
                    // 瞬时事件使用固定薄高度
                    float height = bar.isInstant ? m_instantBarHeight : Mathf.Abs(endY - startY);

                    var rect = visual.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(slotCenter, center);
                    rect.sizeDelta = new Vector2(barWidth, Mathf.Max(height, 2f));
                }
                visualIdx++;
            }
        }
    }

    /// <summary>
    /// 更新长条背景颜色（选中状态高亮）
    /// </summary>
    private void UpdateBarColors()
    {
        int visualIdx = 0;
        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.bars == null) continue;

            for (int i = 0; i < slotData.bars.Count; i++)
            {
                if (visualIdx >= m_barVisuals.Count) break;
                var visual = m_barVisuals[visualIdx];
                if (visual != null)
                {
                    var img = visual.GetComponent<Image>();
                    if (img != null)
                    {
                        bool isSelected = (slot == m_selectedSlot && i == m_selectedBarIndex);
                        var bar = slotData.bars[i];
                        bool isTrackSlot = slot >= EasingSlotConfigs.CubeSlotCount;

                        if (isTrackSlot)
                        {
                            // 轨道级事件（棱偏移/流速）使用橙色
                            img.color = isSelected ? m_trackBarSelectedColor : m_trackBarColor;
                        }
                        else if (bar.isInstant)
                        {
                            img.color = isSelected ? m_instantBarSelectedColor : m_instantBarColor;
                        }
                        else
                        {
                            img.color = isSelected ? m_barSelectedColor : m_barColor;
                        }
                    }
                }
                visualIdx++;
            }
        }
    }

    /// <summary>
    /// 更新曲线：使用 Image 线段池绘制长条内的缓动函数曲线。
    /// 每帧调用，线段位置随时间轴滚动实时更新。
    /// </summary>
    private void UpdateCurvePoints()
    {
        int segIdx = 0;

        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.bars == null || slotData.bars.Count == 0)
            {
                continue;
            }

            // 遍历每个长条，采样缓动曲线并绘制线段
            foreach (var bar in slotData.bars)
            {
                // 瞬时事件无曲线（零时长）
                if (bar.isInstant) continue;
                float currX = ValueToSlotX(slot, bar.startValue);
                float currY = TimeToLocalY(bar.startTime);
                float nextX = ValueToSlotX(slot, bar.endValue);
                float nextY = TimeToLocalY(bar.endTime);

                // 起始点到第一个采样点
                float prevX = currX;
                float prevY = currY;

                for (int s = 1; s <= m_curveSamples; s++)
                {
                    float t = (float)s / m_curveSamples;
                    // 权重混合：weight=0 时线性，weight=1 时完整缓动
                    float easedT = DOVirtual.EasedValue(0f, 1f, t, bar.easingType);
                    float weightedT = Mathf.Lerp(t, easedT, bar.weight);
                    float x = currX + (nextX - currX) * weightedT;
                    float y = currY + (nextY - currY) * t;

                    Image seg = GetOrCreateSegment(segIdx);
                    seg.gameObject.SetActive(true);
                    PositionSegment(seg, prevX, prevY, x, y);
                    segIdx++;

                    prevX = x;
                    prevY = y;
                }
            }
        }

        // 停用多余的线段
        for (int i = segIdx; i < m_curveSegments.Count; i++)
        {
            if (m_curveSegments[i] != null)
            {
                m_curveSegments[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 从池中获取或创建线段 Image
    /// </summary>
    private Image GetOrCreateSegment(int index)
    {
        if (index < m_curveSegments.Count && m_curveSegments[index] != null)
        {
            return m_curveSegments[index];
        }

        var go = new GameObject($"CurveSeg_{index}", typeof(RectTransform));
        go.transform.SetParent(m_curveLayer, false);
        go.layer = 5;

        var rect = go.GetComponent<RectTransform>();
        // 锚点设为左中 (0, 0.5)，与 CurveLayer 的 pivot 一致
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.color = m_curveColor;
        img.raycastTarget = false;

        // 扩容列表
        while (m_curveSegments.Count <= index)
        {
            m_curveSegments.Add(null);
        }
        m_curveSegments[index] = img;

        return img;
    }

    /// <summary>
    /// 将 Image 线段定位为从 (x1,y1) 到 (x2,y2) 的线段
    /// </summary>
    private void PositionSegment(Image seg, float x1, float y1, float x2, float y2)
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
        rect.sizeDelta = new Vector2(length, m_curveWidth);
        rect.localEulerAngles = new Vector3(0, 0, angle);
    }

    #endregion

    #region 数据访问

    /// <summary>
    /// 获取当前活跃方体指定槽的缓动数据。
    /// 前 13 个槽（lx~A）为方体级，后 2 个槽（棱偏移、流速）为轨道级，
    /// 轨道级数据从当前选中面+方向的 CubeNoteTrackData 获取。
    /// </summary>
    private EasingSlotData GetSlotData(int slot)
    {
        if (m_cubeManager == null) return null;
        var cube = m_cubeManager.GetCube(m_cubeManager.ActiveCubeId);
        if (cube == null) return null;

        // 方体级槽（0 ~ CubeSlotCount-1）
        if (slot < EasingSlotConfigs.CubeSlotCount)
        {
            if (cube.easingSlots == null || slot >= cube.easingSlots.Count) return null;
            return cube.easingSlots[slot];
        }

        // 轨道级槽（棱偏移、流速）--从当前选中轨道获取
        var track = cube.GetTrack(m_cubeManager.ActiveFace, m_cubeManager.ActiveDirection);
        if (track == null || track.easingSlots == null) return null;
        int trackSlot = slot - EasingSlotConfigs.CubeSlotCount;
        if (trackSlot >= track.easingSlots.Count) return null;
        return track.easingSlots[trackSlot];
    }

    /// <summary>
    /// 获取或创建槽数据（兼容旧数据）
    /// </summary>
    private EasingSlotData GetOrCreateSlotData(int slot)
    {
        if (m_cubeManager == null) return null;
        var cube = m_cubeManager.GetCube(m_cubeManager.ActiveCubeId);
        if (cube == null) return null;

        // 方体级槽
        if (slot < EasingSlotConfigs.CubeSlotCount)
        {
            if (cube.easingSlots == null || cube.easingSlots.Count == 0)
            {
                cube.InitializeDefaultEasingSlots();
            }
            if (slot >= cube.easingSlots.Count) return null;
            return cube.easingSlots[slot];
        }

        // 轨道级槽--确保轨道有缓动数据
        var track = cube.GetTrack(m_cubeManager.ActiveFace, m_cubeManager.ActiveDirection);
        if (track == null) return null;
        if (track.easingSlots == null || track.easingSlots.Count == 0)
        {
            track.InitializeDefaultTrackEasingSlots();
        }
        int trackSlot = slot - EasingSlotConfigs.CubeSlotCount;
        if (trackSlot >= track.easingSlots.Count) return null;
        return track.easingSlots[trackSlot];
    }

    /// <summary>
    /// 确保方体级和当前轨道级数据槽存在，且每个槽在 time=0 处有不可删除的瞬时事件（初始值）。
    /// </summary>
    private void EnsureSlotDataExists()
    {
        if (m_cubeManager == null) return;
        var cube = m_cubeManager.GetCube(m_cubeManager.ActiveCubeId);
        if (cube == null) return;

        bool modified = false;

        // ---- 方体级槽 ----
        if (cube.easingSlots == null || cube.easingSlots.Count == 0)
        {
            cube.InitializeDefaultEasingSlots();
            modified = true;
        }
        else
        {
            for (int i = 0; i < cube.easingSlots.Count; i++)
            {
                modified |= EnsureDefaultInstantEvent(cube.easingSlots[i], EasingSlotConfigs.Slots[i]);
            }
        }

        // ---- 轨道级槽（当前选中轨道）----
        var track = cube.GetTrack(m_cubeManager.ActiveFace, m_cubeManager.ActiveDirection);
        if (track != null)
        {
            if (track.easingSlots == null || track.easingSlots.Count != EasingSlotConfigs.TrackSlotCount)
            {
                track.InitializeDefaultTrackEasingSlots();
                modified = true;
            }
            else
            {
                for (int i = 0; i < track.easingSlots.Count; i++)
                {
                    var config = EasingSlotConfigs.Slots[EasingSlotConfigs.CubeSlotCount + i];
                    modified |= EnsureDefaultInstantEvent(track.easingSlots[i], config);
                }
            }
        }

        if (modified)
        {
            SaveCubeData();
        }
    }

    /// <summary>
    /// 确保指定槽数据在 time=0 处有不可删除的瞬时事件（初始值）。
    /// 若不存在则插入，返回是否进行了修改。
    /// </summary>
    private bool EnsureDefaultInstantEvent(EasingSlotData slotData, EasingSlotConfig config)
    {
        if (slotData == null || slotData.bars == null || slotData.bars.Count == 0 ||
            !Mathf.Approximately(slotData.bars[0].startTime, 0f) ||
            !slotData.bars[0].isInstant)
        {
            slotData.bars.Insert(0, new EasingBar(0f, 0f, config.defaultValue, config.defaultValue,
                Ease.Linear, 1f, true));
            return true;
        }
        return false;
    }

    /// <summary>
    /// 保存方体数据到 JSON
    /// </summary>
    private void SaveCubeData()
    {
        if (m_cubeManager != null)
        {
            m_cubeManager.SaveCubesToJson();
        }
    }

    /// <summary>
    /// 活跃方体切换时重新加载长条
    /// </summary>
    private void OnCubeChanged(int cubeId)
    {
        if (m_isGlobalMode)
        {
            m_needGlobalRebuild = true;
            return;
        }
        DeselectBar();
        CancelPendingBar();
        EnsureSlotDataExists();
        RebuildBarVisuals();
    }

    /// <summary>
    /// note 轨道切换时重新加载轨道级长条（棱偏移、流速）
    /// </summary>
    private void OnTrackChanged(CubeFace face, FaceDirection direction)
    {
        if (m_isGlobalMode)
        {
            m_needGlobalRebuild = true;
            return;
        }
        DeselectBar();
        CancelPendingBar();
        EnsureSlotDataExists();
        RebuildBarVisuals();
    }

    #endregion

    #region 坐标转换

    /// <summary>
    /// 将数值映射到数据槽内的 X 坐标（EasingContent 本地坐标）。
    /// 分段线性映射：以 defaultValue 为中线（normalized=0.5，竖线位置），
    /// 上半段 [default, max] 映射到 [0.5, 1.0]，下半段 [min, default] 映射到 [0, 0.5]。
    /// </summary>
    private float ValueToSlotX(int slot, float value)
    {
        var config = EasingSlotConfigs.Slots[slot];

        float upperRange = config.maxValue - config.defaultValue;
        float lowerRange = config.defaultValue - config.minValue;

        float normalized;
        if (value >= config.defaultValue)
        {
            normalized = (upperRange > 0f)
                ? 0.5f + (value - config.defaultValue) / upperRange * 0.5f
                : 0.5f;
        }
        else
        {
            normalized = (lowerRange > 0f)
                ? 0.5f - (config.defaultValue - value) / lowerRange * 0.5f
                : 0.5f;
        }
        normalized = Mathf.Clamp01(normalized);

        float startX = m_lineSpacing * 0.5f;
        float slotCenter = startX + slot * m_lineSpacing;
        float columnWidth = m_lineSpacing * 0.8f; // 留 10% 两侧边距

        return slotCenter + (normalized - 0.5f) * columnWidth;
    }

    /// <summary>
    /// 将数据槽内的 X 坐标映射回数值（ValueToSlotX 的逆映射，分段线性）
    /// </summary>
    private float SlotXToValue(int slot, float x)
    {
        var config = EasingSlotConfigs.Slots[slot];
        float upperRange = config.maxValue - config.defaultValue;
        float lowerRange = config.defaultValue - config.minValue;

        float startX = m_lineSpacing * 0.5f;
        float slotCenter = startX + slot * m_lineSpacing;
        float columnWidth = m_lineSpacing * 0.8f;

        float normalized = (x - slotCenter) / columnWidth + 0.5f;
        normalized = Mathf.Clamp01(normalized);

        if (normalized >= 0.5f)
        {
            return (upperRange > 0f)
                ? config.defaultValue + (normalized - 0.5f) * 2f * upperRange
                : config.defaultValue;
        }
        else
        {
            return (lowerRange > 0f)
                ? config.defaultValue - (0.5f - normalized) * 2f * lowerRange
                : config.defaultValue;
        }
    }

    /// <summary>
    /// 将时间（秒）转换为 Y 坐标（与 GridManager 同步）
    /// </summary>
    private float TimeToLocalY(float time)
    {
        if (m_gridManager == null) return 0f;
        return m_gridManager.TimeToLocalY(time);
    }

    /// <summary>
    /// 将 Y 坐标转换为时间（秒）
    /// </summary>
    private float LocalYToTime(float localY)
    {
        if (m_gridManager == null) return 0f;
        return m_gridManager.LocalYToTime(localY);
    }

    /// <summary>
    /// 将时间吸附到最近的节拍
    /// </summary>
    private float SnapToBeat(float rawTime)
    {
        if (m_gridManager == null) return rawTime;

        float intervalFactor = m_gridManager.IntervalFactor;
        float bpm = BpmManagerUI.GetBpmAtTime(rawTime);
        if (bpm <= 0) bpm = m_gridManager.m_referenceBpm;
        if (intervalFactor <= 0) return rawTime;

        float beatInterval = intervalFactor / bpm;
        if (beatInterval <= 0) return rawTime;

        int beatIndex = Mathf.RoundToInt(rawTime / beatInterval);
        float snapped = beatIndex * beatInterval;
        return Mathf.Max(0f, snapped);
    }

    #endregion

    #region 滚动

    /// <summary>
    /// 滚轮水平滚动（供 GridScrollHandler 转发调用）
    /// </summary>
    public void ScrollHorizontal(float delta)
    {
        ApplyScroll(delta);
    }

    /// <summary>
    /// 应用水平滚动偏移并更新内容位置
    /// </summary>
    private void ApplyScroll(float delta)
    {
        m_easingScrollOffset += delta;
        m_easingScrollOffset = Mathf.Clamp(m_easingScrollOffset, 0, m_maxScroll);
        m_easingContent.anchoredPosition = new Vector2(-m_easingScrollOffset, 0);
    }

    /// <summary>
    /// 更新最大滚动范围
    /// </summary>
    private void UpdateMaxScroll()
    {
        float viewportWidth = m_easingViewport != null ? m_easingViewport.rect.width : 0;
        float contentWidth = m_isGlobalMode ? m_globalContentWidth : m_contentWidth;
        m_maxScroll = Mathf.Max(0, contentWidth - viewportWidth);
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 获取中文字体（用于标签显示"棱偏移""流速"等中文文本）
    /// </summary>
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
        if (m_chineseFont == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 创建动态 TMP 字体失败，使用 TMP 默认字体");
        }

        return m_chineseFont;
    }

    #endregion

    #region 全局事件区

    /// <summary>
    /// 创建全局事件区容器层（默认隐藏）
    /// </summary>
    private void CreateGlobalLayer()
    {
        var globalGo = new GameObject("GlobalLayer", typeof(RectTransform));
        globalGo.transform.SetParent(m_easingContent, false);
        globalGo.layer = 5;

        m_globalLayer = globalGo.GetComponent<RectTransform>();
        m_globalLayer.anchorMin = new Vector2(0, 0);
        m_globalLayer.anchorMax = new Vector2(0, 1);
        m_globalLayer.pivot = new Vector2(0, 0.5f);
        m_globalLayer.sizeDelta = new Vector2(m_contentWidth, 0);
        m_globalLayer.anchoredPosition = Vector2.zero;
        m_globalLayer.gameObject.SetActive(false);
    }

    /// <summary>
    /// 切换全局事件区模式（O 键触发）
    /// </summary>
    private void ToggleGlobalMode()
    {
        if (m_isGlobalMode)
        {
            ExitGlobalMode();
        }
        else
        {
            EnterGlobalMode();
        }
    }

    /// <summary>
    /// 进入全局事件区模式：隐藏普通模式元素，收集并显示所有方体的所有事件
    /// </summary>
    private void EnterGlobalMode()
    {
        m_isGlobalMode = true;

        // 取消选中与待定状态
        DeselectBar();
        CancelPendingBar();

        // 隐藏普通模式元素
        SetNormalModeVisualsActive(false);

        // 显示全局层
        if (m_globalLayer != null)
        {
            m_globalLayer.gameObject.SetActive(true);
        }

        // 收集事件并渲染
        CollectGlobalEvents();
        AssignGlobalLanes();
        RebuildGlobalBarVisuals();

        // 更新内容宽度以适配滚动
        m_easingContent.sizeDelta = new Vector2(m_globalContentWidth, 0);
        if (m_globalLayer != null)
        {
            m_globalLayer.sizeDelta = new Vector2(m_globalContentWidth, 0);
        }
        UpdateMaxScroll();
        m_easingScrollOffset = Mathf.Clamp(m_easingScrollOffset, 0, m_maxScroll);
        m_easingContent.anchoredPosition = new Vector2(-m_easingScrollOffset, 0);

        Debug.Log($"[{GetType().Name}] 进入全局事件区模式，共 {m_globalEvents.Count} 个事件，{GetGlobalLaneCount()} 条轨道");
    }

    /// <summary>
    /// 退出全局事件区模式：恢复普通模式显示
    /// </summary>
    private void ExitGlobalMode()
    {
        // 先取消全局选中与待定状态（此时 m_isGlobalMode 仍为 true）
        DeselectBar();
        CancelPendingBar();

        m_isGlobalMode = false;

        // 隐藏全局层
        if (m_globalLayer != null)
        {
            m_globalLayer.gameObject.SetActive(false);
        }

        // 清理全局长条可视化
        ClearGlobalBarVisuals();
        m_globalEvents.Clear();
        m_globalContentWidth = 0;

        // 恢复普通模式元素
        SetNormalModeVisualsActive(true);

        // 恢复内容宽度
        m_contentWidth = m_lineCount * m_lineSpacing + m_lineSpacing;
        m_easingContent.sizeDelta = new Vector2(m_contentWidth, 0);
        UpdateMaxScroll();
        m_easingScrollOffset = Mathf.Clamp(m_easingScrollOffset, 0, m_maxScroll);
        m_easingContent.anchoredPosition = new Vector2(-m_easingScrollOffset, 0);

        // 重建普通模式长条
        EnsureSlotDataExists();
        RebuildBarVisuals();

        Debug.Log($"[{GetType().Name}] 退出全局事件区模式");
    }

    /// <summary>
    /// 切换普通模式元素的可见性（竖线、标签、长条层、曲线层）
    /// </summary>
    private void SetNormalModeVisualsActive(bool active)
    {
        if (m_easingContent == null) return;

        for (int i = 0; i < m_easingContent.childCount; i++)
        {
            var child = m_easingContent.GetChild(i);
            if (child.name.StartsWith("EasingVLine_") || child.name.StartsWith("SlotLabel_"))
            {
                child.gameObject.SetActive(active);
            }
        }

        if (m_barLayer != null) m_barLayer.gameObject.SetActive(active);
        if (m_curveLayer != null) m_curveLayer.gameObject.SetActive(active);
    }

    /// <summary>
    /// 收集谱面内所有方体的所有缓动事件，按起始时间排序
    /// </summary>
    private void CollectGlobalEvents()
    {
        m_globalEvents.Clear();

        if (m_cubeManager == null) return;

        foreach (var cube in m_cubeManager.Cubes)
        {
            if (cube == null) continue;

            // 方体级事件槽（0~12：lx~A）
            if (cube.easingSlots != null)
            {
                for (int slot = 0; slot < cube.easingSlots.Count && slot < EasingSlotConfigs.CubeSlotCount; slot++)
                {
                    var slotData = cube.easingSlots[slot];
                    if (slotData?.bars == null) continue;

                    var config = EasingSlotConfigs.Slots[slot];

                    foreach (var bar in slotData.bars)
                    {
                        // 跳过 time=0 且值等于默认值的初始瞬时事件
                        if (IsDefaultInitialEvent(bar, config)) continue;

                        m_globalEvents.Add(new GlobalEventData
                        {
                            bar = bar,
                            cubeId = cube.cubeId,
                            cubeName = cube.cubeName,
                            slotIndex = slot,
                            isTrackLevel = false
                        });
                    }
                }
            }

            // 轨道级事件槽（13~14：棱偏移、流速），每个方体24条轨道各2个槽
            if (cube.tracks != null)
            {
                foreach (var track in cube.tracks)
                {
                    if (track?.easingSlots == null) continue;

                    if (!Enum.TryParse(track.face, out CubeFace face)) continue;
                    if (!Enum.TryParse(track.direction, out FaceDirection direction)) continue;

                    for (int slotIdx = 0; slotIdx < track.easingSlots.Count && slotIdx < EasingSlotConfigs.TrackSlotCount; slotIdx++)
                    {
                        var slotData = track.easingSlots[slotIdx];
                        if (slotData?.bars == null) continue;

                        int globalSlotIndex = EasingSlotConfigs.CubeSlotCount + slotIdx;
                        var config = EasingSlotConfigs.Slots[globalSlotIndex];

                        foreach (var bar in slotData.bars)
                        {
                            // 跳过 time=0 且值等于默认值的初始瞬时事件
                            if (IsDefaultInitialEvent(bar, config)) continue;

                            m_globalEvents.Add(new GlobalEventData
                            {
                                bar = bar,
                                cubeId = cube.cubeId,
                                cubeName = cube.cubeName,
                                slotIndex = globalSlotIndex,
                                isTrackLevel = true,
                                face = face,
                                direction = direction
                            });
                        }
                    }
                }
            }
        }

        // 按起始时间升序排序
        m_globalEvents.Sort((a, b) => a.bar.startTime.CompareTo(b.bar.startTime));
    }

    /// <summary>
    /// 判断是否为 time=0 且值等于默认值的初始瞬时事件（此类事件在全局事件区中不显示）
    /// </summary>
    private bool IsDefaultInitialEvent(EasingBar bar, EasingSlotConfig config)
    {
        return bar.isInstant
               && Mathf.Approximately(bar.startTime, 0f)
               && Mathf.Approximately(bar.startValue, config.defaultValue);
    }

    /// <summary>
    /// 为全局事件分配水平轨道（避免时间重叠的事件在同一轨道）
    /// 贪心算法：按时间排序后，每个事件分配第一个不冲突的轨道
    /// </summary>
    private void AssignGlobalLanes()
    {
        var laneEndTimes = new List<float>();

        foreach (var evt in m_globalEvents)
        {
            // 瞬时事件添加微小虚拟时长，避免同一时间点的多个瞬时事件叠在同一轨道
            float effectiveEndTime = evt.bar.isInstant
                ? evt.bar.startTime + 0.0001f
                : evt.bar.endTime;

            int assignedLane = -1;
            for (int i = 0; i < laneEndTimes.Count; i++)
            {
                // 该轨道最后一个事件的结束时间 <= 当前事件起始时间时可复用
                if (laneEndTimes[i] <= evt.bar.startTime)
                {
                    assignedLane = i;
                    laneEndTimes[i] = effectiveEndTime;
                    break;
                }
            }

            if (assignedLane == -1)
            {
                assignedLane = laneEndTimes.Count;
                laneEndTimes.Add(effectiveEndTime);
            }

            evt.lane = assignedLane;
        }

        m_globalContentWidth = (laneEndTimes.Count + 1) * m_globalLaneWidth;
    }

    /// <summary>获取全局事件区当前轨道数</summary>
    private int GetGlobalLaneCount()
    {
        int maxLane = -1;
        foreach (var evt in m_globalEvents)
        {
            if (evt.lane > maxLane) maxLane = evt.lane;
        }
        return maxLane + 1;
    }

    /// <summary>
    /// 重建全局事件区长条可视化
    /// </summary>
    private void RebuildGlobalBarVisuals()
    {
        ClearGlobalBarVisuals();

        foreach (var evt in m_globalEvents)
        {
            var visual = CreateGlobalBarVisual(evt);
            m_globalBarVisuals.Add(visual);
        }

        UpdateGlobalBarPositions();
        UpdateGlobalBarColors();
    }

    /// <summary>
    /// 清理全局长条可视化对象
    /// </summary>
    private void ClearGlobalBarVisuals()
    {
        foreach (var visual in m_globalBarVisuals)
        {
            if (visual != null) Destroy(visual);
        }
        m_globalBarVisuals.Clear();
    }

    /// <summary>
    /// 创建单个全局事件长条（含事件代号标签和方体信息标签）
    /// </summary>
    private GameObject CreateGlobalBarVisual(GlobalEventData evt)
    {
        var barGo = new GameObject(
            $"GBar_{evt.cubeId}_{evt.slotIndex}_{evt.bar.startTime:F1}",
            typeof(RectTransform));
        barGo.transform.SetParent(m_globalLayer, false);
        barGo.layer = 5;

        var rect = barGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = barGo.AddComponent<Image>();
        img.color = GetGlobalBarColor(evt);
        img.raycastTarget = false;

        // 事件代号标签（左下角）
        var codeLabelGo = new GameObject("EventCode", typeof(RectTransform));
        codeLabelGo.transform.SetParent(barGo.transform, false);
        codeLabelGo.layer = 5;

        var codeRect = codeLabelGo.GetComponent<RectTransform>();
        codeRect.anchorMin = new Vector2(0, 0);
        codeRect.anchorMax = new Vector2(0, 0);
        codeRect.pivot = new Vector2(0, 0);
        codeRect.anchoredPosition = new Vector2(3, 2);
        codeRect.sizeDelta = new Vector2(m_globalLaneWidth, m_globalLabelFontSize + 4);

        var codeTmp = codeLabelGo.AddComponent<TextMeshProUGUI>();
        codeTmp.text = GetSlotLabel(evt.slotIndex);
        codeTmp.fontSize = m_globalLabelFontSize;
        codeTmp.color = m_globalCodeLabelColor;
        codeTmp.alignment = TextAlignmentOptions.BottomLeft;
        codeTmp.font = GetChineseFont();
        codeTmp.raycastTarget = false;

        // 方体信息标签（右下角）
        var infoLabelGo = new GameObject("CubeInfo", typeof(RectTransform));
        infoLabelGo.transform.SetParent(barGo.transform, false);
        infoLabelGo.layer = 5;

        var infoRect = infoLabelGo.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(1, 0);
        infoRect.anchorMax = new Vector2(1, 0);
        infoRect.pivot = new Vector2(1, 0);
        infoRect.anchoredPosition = new Vector2(-3, 2);
        // 轨道级事件信息分两行显示，需要更高的高度
        float infoHeight = evt.isTrackLevel
            ? m_globalLabelFontSize * 2 + 6
            : m_globalLabelFontSize + 4;
        infoRect.sizeDelta = new Vector2(m_globalLaneWidth, infoHeight);

        var infoTmp = infoLabelGo.AddComponent<TextMeshProUGUI>();
        infoTmp.text = BuildCubeInfoText(evt);
        infoTmp.fontSize = m_globalLabelFontSize;
        infoTmp.color = m_globalInfoLabelColor;
        infoTmp.alignment = TextAlignmentOptions.BottomRight;
        infoTmp.font = GetChineseFont();
        infoTmp.raycastTarget = false;

        return barGo;
    }

    /// <summary>
    /// 获取全局事件长条颜色（按事件类型区分）
    /// </summary>
    private Color GetGlobalBarColor(GlobalEventData evt)
    {
        if (evt.bar.isInstant)
        {
            return m_globalInstantColor;
        }
        return evt.isTrackLevel ? m_globalTrackBarColor : m_globalBarColor;
    }

    /// <summary>
    /// 构建方体信息文本：方体级事件仅显示 Cube ID，轨道级事件额外显示面和方向
    /// </summary>
    private string BuildCubeInfoText(GlobalEventData evt)
    {
        if (evt.isTrackLevel)
        {
            // 轨道级事件分两行：第一行 Cube ID，第二行 面+方向
            return $"Cube{evt.cubeId}\n{GetFaceShortName(evt.face)}{GetDirectionShortName(evt.direction)}";
        }
        return $"Cube{evt.cubeId}";
    }

    /// <summary>
    /// 每帧更新全局长条位置（Y 轴跟随时间轴滚动）
    /// </summary>
    private void UpdateGlobalBarPositions()
    {
        if (m_gridManager == null) return;

        float barWidth = m_globalLaneWidth * m_barWidthRatio;
        float startX = m_globalLaneWidth * 0.5f;

        for (int i = 0; i < m_globalEvents.Count && i < m_globalBarVisuals.Count; i++)
        {
            var evt = m_globalEvents[i];
            var visual = m_globalBarVisuals[i];
            if (visual == null) continue;

            float startY = TimeToLocalY(evt.bar.startTime);
            float endY = TimeToLocalY(evt.bar.endTime);
            float centerY = (startY + endY) * 0.5f;
            float height = evt.bar.isInstant ? m_globalInstantBarHeight : Mathf.Abs(endY - startY);

            float laneCenter = startX + evt.lane * m_globalLaneWidth;

            var rect = visual.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(laneCenter, centerY);
            rect.sizeDelta = new Vector2(barWidth, Mathf.Max(height, 2f));
        }

        // 更新待定标记的 Y 位置（跟随时间轴滚动）
        if (m_isPendingBar && m_pendingMarker != null)
        {
            float markerY = TimeToLocalY(m_pendingBarStartTime);
            float markerX = m_globalLaneWidth * 0.5f;
            var markerRect = m_pendingMarker.GetComponent<RectTransform>();
            markerRect.anchoredPosition = new Vector2(markerX, markerY);
        }
    }

    /// <summary>
    /// 将 CubeFace 枚举转换为中文短名
    /// </summary>
    private string GetFaceShortName(CubeFace face)
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
    /// 将 FaceDirection 枚举转换为中文短名
    /// </summary>
    private string GetDirectionShortName(FaceDirection dir)
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

    #region 全局事件区编辑

    /// <summary>
    /// 全局模式下点击选中长条
    /// </summary>
    private void HandleGlobalClick()
    {
        if (m_easingContent == null) return;

        Vector2 contentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingContent, Input.mousePosition, null, out contentLocal);

        int clickedIndex = FindGlobalBarAt(contentLocal);
        if (clickedIndex >= 0)
        {
            SelectGlobalBar(clickedIndex);
        }
        else
        {
            DeselectBar();
        }
    }

    /// <summary>
    /// 查找点击位置对应的全局长条索引
    /// </summary>
    private int FindGlobalBarAt(Vector2 contentLocal)
    {
        float barWidth = m_globalLaneWidth * m_barWidthRatio;
        float startX = m_globalLaneWidth * 0.5f;
        float halfWidth = barWidth * 0.5f;

        for (int i = 0; i < m_globalEvents.Count; i++)
        {
            var evt = m_globalEvents[i];
            float laneCenter = startX + evt.lane * m_globalLaneWidth;

            float startY = TimeToLocalY(evt.bar.startTime);
            float endY = TimeToLocalY(evt.bar.endTime);
            float centerY = (startY + endY) * 0.5f;
            float height = evt.bar.isInstant ? m_globalInstantBarHeight : Mathf.Abs(endY - startY);
            float halfHeight = Mathf.Max(height * 0.5f, 8f); // 最小点击高度 8px

            if (Mathf.Abs(contentLocal.x - laneCenter) <= halfWidth &&
                Mathf.Abs(contentLocal.y - centerY) <= halfHeight)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 选中全局事件区长条
    /// </summary>
    private void SelectGlobalBar(int index)
    {
        m_globalSelectedIndex = index;
        UpdateGlobalBarColors();
        BarSelected?.Invoke();
    }

    /// <summary>
    /// 更新全局长条颜色（选中状态高亮）
    /// </summary>
    private void UpdateGlobalBarColors()
    {
        for (int i = 0; i < m_globalBarVisuals.Count; i++)
        {
            if (m_globalBarVisuals[i] == null) continue;
            var img = m_globalBarVisuals[i].GetComponent<Image>();
            if (img == null) continue;

            var baseColor = GetGlobalBarColor(m_globalEvents[i]);
            bool isSelected = (i == m_globalSelectedIndex);

            img.color = isSelected
                ? new Color(
                    Mathf.Min(baseColor.r + 0.15f, 1f),
                    Mathf.Min(baseColor.g + 0.15f, 1f),
                    Mathf.Min(baseColor.b + 0.15f, 1f),
                    Mathf.Min(baseColor.a + 0.2f, 1f))
                : baseColor;
        }
    }

    /// <summary>
    /// 删除全局事件区长条（从原始方体数据中移除）
    /// </summary>
    private void DeleteGlobalBar(int index)
    {
        if (index < 0 || index >= m_globalEvents.Count) return;

        var evt = m_globalEvents[index];

        // 不允许删除 time=0 的初始瞬时事件
        if (evt.bar.isInstant && Mathf.Approximately(evt.bar.startTime, 0f))
        {
            Debug.LogWarning($"[{GetType().Name}] time=0 的初始瞬时事件不可删除");
            return;
        }

        var slotData = GetGlobalEventSlotData(evt);
        if (slotData == null) return;

        int barIndex = slotData.bars.IndexOf(evt.bar);
        if (barIndex < 0)
        {
            Debug.LogWarning($"[{GetType().Name}] 未在原始数据中找到要删除的长条");
            return;
        }

        slotData.bars.RemoveAt(barIndex);
        SaveCubeData();
        DeselectBar();

        // 重建全局事件区
        CollectGlobalEvents();
        AssignGlobalLanes();
        RebuildGlobalBarVisuals();

        Debug.Log($"[{GetType().Name}] 删除全局长条: Cube{evt.cubeId} 槽{evt.slotIndex} 时间{evt.bar.startTime:F2}s");
    }

    /// <summary>
    /// 全局模式下 S 键创建长条（两次确认）。
    /// 新长条继承当前选中事件的方体/槽/轨道，若无选中则使用活跃方体槽0。
    /// </summary>
    private void HandleGlobalBarCreation()
    {
        if (m_playScreenRect == null || m_easingViewport == null) return;

        // 检测鼠标是否在缓动区内
        Vector2 viewportLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingViewport, Input.mousePosition, null, out viewportLocal);
        if (!m_easingViewport.rect.Contains(viewportLocal)) return;

        // 将鼠标位置转换为 EasingContent 本地坐标
        Vector2 contentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingContent, Input.mousePosition, null, out contentLocal);

        // Y 转换为时间并吸附到节拍
        float rawTime = LocalYToTime(contentLocal.y);
        float snappedTime = SnapToBeat(rawTime);

        if (!m_isPendingBar)
        {
            if (Mathf.Approximately(snappedTime, 0f))
            {
                Debug.LogWarning($"[{GetType().Name}] time=0 已有初始瞬时事件，请选择其他时间点");
                return;
            }

            m_isPendingBar = true;
            m_pendingBarStartTime = snappedTime;
            // 复用普通模式的 pendingBarSlot 存储创建信息（-1 表示全局模式）
            m_pendingBarSlot = -1;
            ShowGlobalPendingMarker(snappedTime);
            Debug.Log($"[{GetType().Name}] 全局长条起点已确定: 时间{snappedTime:F2}s，请再次按 S 确定终点");
        }
        else
        {
            float startTime = m_pendingBarStartTime;
            float endTime = snappedTime;

            if (Mathf.Approximately(startTime, endTime))
            {
                CancelPendingBar();
                AddGlobalBar(startTime, startTime, true);
                return;
            }

            if (endTime < startTime)
            {
                float temp = startTime;
                startTime = endTime;
                endTime = temp;
            }

            // 检查最小时长
            float bpm = BpmManagerUI.GetBpmAtTime(startTime);
            if (bpm <= 0) bpm = m_gridManager != null ? m_gridManager.m_referenceBpm : 120f;
            float intervalFactor = m_gridManager != null ? m_gridManager.IntervalFactor : 1f;
            float minDuration = intervalFactor / bpm * 0.5f;

            if (endTime - startTime < minDuration)
            {
                Debug.LogWarning($"[{GetType().Name}] 长条时长过短，需要至少半拍");
                CancelPendingBar();
                return;
            }

            CancelPendingBar();
            AddGlobalBar(startTime, endTime, false);
        }
    }

    /// <summary>
    /// 显示全局模式待定长条标记
    /// </summary>
    private void ShowGlobalPendingMarker(float time)
    {
        if (m_pendingMarker != null) Destroy(m_pendingMarker);
        if (m_pendingPreview != null) Destroy(m_pendingPreview);

        float y = TimeToLocalY(time);
        float markerX = m_globalLaneWidth * 0.5f;

        m_pendingMarker = new GameObject("GlobalPendingMarker", typeof(RectTransform));
        m_pendingMarker.transform.SetParent(m_globalLayer, false);
        m_pendingMarker.layer = 5;

        var rect = m_pendingMarker.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(m_globalLaneWidth * m_barWidthRatio, 4f);
        rect.anchoredPosition = new Vector2(markerX, y);

        var img = m_pendingMarker.AddComponent<Image>();
        img.color = m_pendingMarkerColor;
        img.raycastTarget = false;
    }

    /// <summary>
    /// 在全局模式下添加长条到原始方体数据
    /// </summary>
    private void AddGlobalBar(float startTime, float endTime, bool isInstant)
    {
        // 确定目标方体/槽/轨道
        int targetCubeId;
        int targetSlot;
        bool isTrackLevel = false;
        CubeFace face = CubeFace.Front;
        FaceDirection direction = FaceDirection.Up;

        if (m_globalSelectedIndex >= 0 && m_globalSelectedIndex < m_globalEvents.Count)
        {
            // 继承当前选中事件的方体/槽/轨道
            var selected = m_globalEvents[m_globalSelectedIndex];
            targetCubeId = selected.cubeId;
            targetSlot = selected.slotIndex;
            isTrackLevel = selected.isTrackLevel;
            face = selected.face;
            direction = selected.direction;
        }
        else if (m_cubeManager != null)
        {
            // 默认使用活跃方体，槽0（lx）
            targetCubeId = m_cubeManager.ActiveCubeId;
            targetSlot = 0;
        }
        else
        {
            return;
        }

        var cube = m_cubeManager?.GetCube(targetCubeId);
        if (cube == null) return;

        // 获取目标槽数据
        EasingSlotData slotData;
        if (isTrackLevel)
        {
            var track = cube.GetTrack(face, direction);
            if (track == null) return;
            if (track.easingSlots == null || track.easingSlots.Count == 0)
            {
                track.InitializeDefaultTrackEasingSlots();
            }
            int trackSlot = targetSlot - EasingSlotConfigs.CubeSlotCount;
            slotData = track.easingSlots[trackSlot];
        }
        else
        {
            if (cube.easingSlots == null || cube.easingSlots.Count == 0)
            {
                cube.InitializeDefaultEasingSlots();
            }
            slotData = cube.easingSlots[targetSlot];
        }

        var config = EasingSlotConfigs.Slots[targetSlot];
        float startValue = slotData.EvaluateAt(startTime, config.defaultValue, config);
        float endValue = isInstant ? startValue : slotData.EvaluateAt(endTime, config.defaultValue, config);

        var bar = new EasingBar(startTime, endTime, startValue, endValue, Ease.Linear, 1f, isInstant);

        // 按起始时间插入
        int insertIndex = 0;
        for (int i = 0; i < slotData.bars.Count; i++)
        {
            if (slotData.bars[i].startTime < bar.startTime)
            {
                insertIndex = i + 1;
            }
            else
            {
                break;
            }
        }
        slotData.bars.Insert(insertIndex, bar);

        SaveCubeData();

        // 重建全局事件区
        CollectGlobalEvents();
        AssignGlobalLanes();
        RebuildGlobalBarVisuals();

        // 选中刚创建的长条
        for (int i = 0; i < m_globalEvents.Count; i++)
        {
            if (m_globalEvents[i].cubeId == targetCubeId &&
                m_globalEvents[i].slotIndex == targetSlot &&
                m_globalEvents[i].isTrackLevel == isTrackLevel &&
                m_globalEvents[i].bar.startTime == startTime &&
                m_globalEvents[i].bar.endTime == endTime)
            {
                SelectGlobalBar(i);
                break;
            }
        }

        Debug.Log($"[{GetType().Name}] 全局添加长条: Cube{targetCubeId} 槽{targetSlot} 时间{startTime:F2}s~{endTime:F2}s");
    }

    /// <summary>
    /// 获取全局事件对应的原始槽数据
    /// </summary>
    private EasingSlotData GetGlobalEventSlotData(GlobalEventData evt)
    {
        if (m_cubeManager == null) return null;
        var cube = m_cubeManager.GetCube(evt.cubeId);
        if (cube == null) return null;

        if (evt.isTrackLevel)
        {
            var track = cube.GetTrack(evt.face, evt.direction);
            if (track == null || track.easingSlots == null) return null;
            int trackSlot = evt.slotIndex - EasingSlotConfigs.CubeSlotCount;
            if (trackSlot >= track.easingSlots.Count) return null;
            return track.easingSlots[trackSlot];
        }

        if (cube.easingSlots == null || evt.slotIndex >= cube.easingSlots.Count) return null;
        return cube.easingSlots[evt.slotIndex];
    }

    // ---- 公共接口：供 AnchorPointEditorUI 查询和修改全局事件来源 ----

    /// <summary>选中全局事件所属方体 ID</summary>
    public int GetSelectedGlobalEventCubeId()
    {
        if (m_globalSelectedIndex < 0 || m_globalSelectedIndex >= m_globalEvents.Count) return -1;
        return m_globalEvents[m_globalSelectedIndex].cubeId;
    }

    /// <summary>选中的全局事件是否为轨道级事件</summary>
    public bool IsSelectedGlobalEventTrackLevel()
    {
        if (m_globalSelectedIndex < 0 || m_globalSelectedIndex >= m_globalEvents.Count) return false;
        return m_globalEvents[m_globalSelectedIndex].isTrackLevel;
    }

    /// <summary>选中全局事件所属面（轨道级事件）</summary>
    public CubeFace GetSelectedGlobalEventFace()
    {
        if (m_globalSelectedIndex < 0 || m_globalSelectedIndex >= m_globalEvents.Count) return CubeFace.Front;
        return m_globalEvents[m_globalSelectedIndex].face;
    }

    /// <summary>选中全局事件所属方向（轨道级事件）</summary>
    public FaceDirection GetSelectedGlobalEventDirection()
    {
        if (m_globalSelectedIndex < 0 || m_globalSelectedIndex >= m_globalEvents.Count) return FaceDirection.Up;
        return m_globalEvents[m_globalSelectedIndex].direction;
    }

    /// <summary>
    /// 修改选中全局事件所属方体（将长条从原方体移动到新方体的同一槽位）
    /// </summary>
    public void ChangeSelectedGlobalEventCube(int newCubeId)
    {
        if (m_globalSelectedIndex < 0 || m_cubeManager == null) return;
        var evt = m_globalEvents[m_globalSelectedIndex];
        if (evt.cubeId == newCubeId) return;

        var newCube = m_cubeManager.GetCube(newCubeId);
        if (newCube == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 方体不存在: ID={newCubeId}");
            return;
        }

        // 从原位置移除
        var oldSlotData = GetGlobalEventSlotData(evt);
        if (oldSlotData == null) return;
        int barIndex = oldSlotData.bars.IndexOf(evt.bar);
        if (barIndex < 0) return;

        var barClone = evt.bar.Clone();
        oldSlotData.bars.RemoveAt(barIndex);

        // 插入到新方体的对应槽位
        EasingSlotData newSlotData;
        if (evt.isTrackLevel)
        {
            var track = newCube.GetTrack(evt.face, evt.direction);
            if (track == null) return;
            if (track.easingSlots == null || track.easingSlots.Count == 0)
            {
                track.InitializeDefaultTrackEasingSlots();
            }
            int trackSlot = evt.slotIndex - EasingSlotConfigs.CubeSlotCount;
            newSlotData = track.easingSlots[trackSlot];
        }
        else
        {
            if (newCube.easingSlots == null || newCube.easingSlots.Count == 0)
            {
                newCube.InitializeDefaultEasingSlots();
            }
            newSlotData = newCube.easingSlots[evt.slotIndex];
        }

        int insertIdx = 0;
        for (int i = 0; i < newSlotData.bars.Count; i++)
        {
            if (newSlotData.bars[i].startTime < barClone.startTime)
            {
                insertIdx = i + 1;
            }
            else
            {
                break;
            }
        }
        newSlotData.bars.Insert(insertIdx, barClone);

        SaveCubeData();
        DeselectBar();
        CollectGlobalEvents();
        AssignGlobalLanes();
        RebuildGlobalBarVisuals();

        // 重新选中移动后的长条
        for (int i = 0; i < m_globalEvents.Count; i++)
        {
            if (m_globalEvents[i].cubeId == newCubeId &&
                m_globalEvents[i].slotIndex == evt.slotIndex &&
                m_globalEvents[i].isTrackLevel == evt.isTrackLevel &&
                m_globalEvents[i].bar.startTime == barClone.startTime &&
                m_globalEvents[i].bar.endTime == barClone.endTime)
            {
                SelectGlobalBar(i);
                break;
            }
        }

        Debug.Log($"[{GetType().Name}] 全局长条方体变更: Cube{evt.cubeId} -> Cube{newCubeId}");
    }

    /// <summary>
    /// 修改选中全局事件所属轨道（面+方向），仅对轨道级事件有效
    /// </summary>
    public void ChangeSelectedGlobalEventTrack(CubeFace newFace, FaceDirection newDirection)
    {
        if (m_globalSelectedIndex < 0 || m_cubeManager == null) return;
        var evt = m_globalEvents[m_globalSelectedIndex];
        if (!evt.isTrackLevel) return;
        if (evt.face == newFace && evt.direction == newDirection) return;

        var cube = m_cubeManager.GetCube(evt.cubeId);
        if (cube == null) return;

        // 从原轨道移除
        var oldSlotData = GetGlobalEventSlotData(evt);
        if (oldSlotData == null) return;
        int barIndex = oldSlotData.bars.IndexOf(evt.bar);
        if (barIndex < 0) return;

        var barClone = evt.bar.Clone();
        oldSlotData.bars.RemoveAt(barIndex);

        // 插入到新轨道的对应槽位
        var newTrack = cube.GetTrack(newFace, newDirection);
        if (newTrack == null) return;
        if (newTrack.easingSlots == null || newTrack.easingSlots.Count == 0)
        {
            newTrack.InitializeDefaultTrackEasingSlots();
        }
        int trackSlot = evt.slotIndex - EasingSlotConfigs.CubeSlotCount;
        var newSlotData = newTrack.easingSlots[trackSlot];

        int insertIdx = 0;
        for (int i = 0; i < newSlotData.bars.Count; i++)
        {
            if (newSlotData.bars[i].startTime < barClone.startTime)
            {
                insertIdx = i + 1;
            }
            else
            {
                break;
            }
        }
        newSlotData.bars.Insert(insertIdx, barClone);

        SaveCubeData();
        DeselectBar();
        CollectGlobalEvents();
        AssignGlobalLanes();
        RebuildGlobalBarVisuals();

        // 重新选中
        for (int i = 0; i < m_globalEvents.Count; i++)
        {
            if (m_globalEvents[i].cubeId == evt.cubeId &&
                m_globalEvents[i].slotIndex == evt.slotIndex &&
                m_globalEvents[i].face == newFace &&
                m_globalEvents[i].direction == newDirection &&
                m_globalEvents[i].bar.startTime == barClone.startTime &&
                m_globalEvents[i].bar.endTime == barClone.endTime)
            {
                SelectGlobalBar(i);
                break;
            }
        }

        Debug.Log($"[{GetType().Name}] 全局长条轨道变更: {evt.face}_{evt.direction} -> {newFace}_{newDirection}");
    }

    #endregion

    #region 全局事件区数据模型

    /// <summary>
    /// 全局事件数据：包含缓动长条及其来源方体/轨道信息
    /// </summary>
    private class GlobalEventData
    {
        /// <summary>缓动长条数据引用</summary>
        public EasingBar bar;

        /// <summary>来源方体 ID</summary>
        public int cubeId;

        /// <summary>来源方体名称</summary>
        public string cubeName;

        /// <summary>事件槽索引（0~14，对应 k_slotLabels）</summary>
        public int slotIndex;

        /// <summary>是否为轨道级事件（棱偏移/流速）</summary>
        public bool isTrackLevel;

        /// <summary>轨道级事件所属面</summary>
        public CubeFace face;

        /// <summary>轨道级事件所属方向</summary>
        public FaceDirection direction;

        /// <summary>分配的水平轨道索引</summary>
        public int lane;
    }

    #endregion
}
