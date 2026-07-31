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
/// 用户可在格点上点击添加锚点（关键帧），选中锚点后可在 FunctionSelect 位置编辑数值与缓动类型。
/// 锚点之间的数值变化通过缓动函数插值，并以曲线图形式可视化。
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
    // ---- 锚点命中检测半径（像素） ----
    private const float k_anchorHitRadius = 14f;

    // 锚点删除快捷键的 Action 名（与 Settings 页面中一致，用于 KeyBindingsStore 持久化）
    private const string k_actionDelete = "Anchor_Delete";

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

    [Header("锚点与曲线设置")]
    [Tooltip("锚点标记大小（像素）")]
    [SerializeField] private float m_anchorSize = 12f;
    [Tooltip("锚点颜色")]
    [SerializeField] private Color m_anchorColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("选中锚点颜色")]
    [SerializeField] private Color m_anchorSelectedColor = new Color(0.3f, 1f, 0.3f, 1f);
    [Tooltip("曲线颜色")]
    [SerializeField] private Color m_curveColor = new Color(0.5f, 0.8f, 1f, 0.85f);
    [Tooltip("曲线线宽（像素）")]
    [SerializeField] private float m_curveWidth = 2.5f;
    [Tooltip("每段缓动曲线的采样数")]
    [SerializeField] private int m_curveSamples = 24;

    // ---- 事件 ----
    /// <summary>锚点被选中时触发</summary>
    public event Action AnchorSelected;
    /// <summary>锚点取消选中时触发</summary>
    public event Action AnchorDeselected;

    // ---- 引用 ----
    private GridManager m_gridManager;
    private CubeManager m_cubeManager;
    private RectTransform m_playScreenRect;
    private RectTransform m_easingViewport;
    private RectTransform m_easingContent;
    private TMP_FontAsset m_chineseFont;

    // ---- 曲线渲染（Image 线段池） ----
    private RectTransform m_curveLayer;
    private readonly List<Image> m_curveSegments = new List<Image>();

    // ---- 锚点标记 ----
    private RectTransform m_anchorLayer;
    private readonly List<GameObject> m_anchorMarkers = new List<GameObject>();

    // ---- 选择状态 ----
    private int m_selectedSlot = -1;
    private int m_selectedAnchorIndex = -1;

    // ---- 锚点删除快捷键 ----
    private KeyCombo m_deleteCombo;
    private bool m_deleteComboLoaded;

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
    }

    private void Update()
    {
        CacheGridManager();
        CacheCubeManager();
        TrySubscribeCubeEvent();
        if (m_easingContent == null) return;

        // 首帧 CubeManager 就绪后重建锚点（显示已加载的数据）
        if (m_needInitialRebuild && m_cubeManager != null)
        {
            EnsureDefaultAnchors();
            RebuildAnchorMarkers();
            m_needInitialRebuild = false;
        }

        UpdateMaxScroll();
        HandleMouseInteraction();
        UpdateAnchorVisuals();
        HandleKeyboardShortcuts();
    }

    /// <summary>
    /// 处理键盘快捷键：Delete 删除选中的锚点。
    /// 文本输入框获焦时跳过，避免与文本编辑冲突。
    /// </summary>
    private void HandleKeyboardShortcuts()
    {
        if (!m_deleteComboLoaded)
        {
            LoadShortcuts();
        }

        if (UndoRedoManager.IsTextInputFocused()) return;

        if (m_deleteCombo.IsPressed() && HasSelection)
        {
            DeleteSelectedAnchor();
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
                if (m_easingContent.Find("CurveLayer") == null)
                {
                    CreateCurveLayer();
                }
                if (m_easingContent.Find("AnchorLayer") == null)
                {
                    CreateAnchorLayer();
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
        CreateCurveLayer();
        CreateAnchorLayer();
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
    /// 绘制水平参考线（固定间距，纯视觉辅助）
    /// </summary>
    private void DrawHorizontalReferenceLines()
    {
        if (m_easingContent == null || m_hLineCount < 2) return;

        float viewportHeight = m_easingViewport.rect.height;
        if (viewportHeight <= 0) return;

        float hSpacing = viewportHeight / (m_hLineCount - 1);

        for (int i = 0; i < m_hLineCount; i++)
        {
            var lineGo = new GameObject($"EasingHLine_{i}", typeof(RectTransform));
            lineGo.transform.SetParent(m_easingContent, false);
            lineGo.layer = 5;

            var rect = lineGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, -viewportHeight * 0.5f + i * hSpacing);

            var img = lineGo.AddComponent<Image>();
            img.color = m_hLineColor;
            img.raycastTarget = false;
        }
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

    /// <summary>
    /// 创建锚点标记层
    /// </summary>
    private void CreateAnchorLayer()
    {
        var layerGo = new GameObject("AnchorLayer", typeof(RectTransform));
        layerGo.transform.SetParent(m_easingContent, false);
        layerGo.layer = 5;

        m_anchorLayer = layerGo.GetComponent<RectTransform>();
        m_anchorLayer.anchorMin = new Vector2(0, 0);
        m_anchorLayer.anchorMax = new Vector2(0, 1);
        m_anchorLayer.pivot = new Vector2(0, 0.5f);
        m_anchorLayer.sizeDelta = new Vector2(m_contentWidth, 0);
        m_anchorLayer.anchoredPosition = Vector2.zero;
    }

    #endregion

    #region 鼠标交互

    /// <summary>
    /// 处理鼠标交互：区分点击与拖拽。
    /// 点击空位 -> 添加锚点；点击锚点 -> 选中；拖拽 -> 水平滚动。
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
    /// 处理点击：选中已有锚点或添加新锚点
    /// </summary>
    private void HandleClick()
    {
        // 将鼠标位置转换为 EasingContent 本地坐标
        Vector2 contentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingContent, Input.mousePosition, null, out contentLocal);

        // 确定点击的数据槽
        float startX = m_lineSpacing * 0.5f;
        int slot = Mathf.FloorToInt((contentLocal.x - startX + m_lineSpacing * 0.5f) / m_lineSpacing);
        slot = Mathf.Clamp(slot, 0, m_lineCount - 1);

        // 将 Y 转换为时间并吸附到节拍
        float rawTime = LocalYToTime(contentLocal.y);
        float snappedTime = SnapToBeat(rawTime);

        // 检查是否点击了已有锚点
        int existingIndex = FindAnchorNear(slot, snappedTime);
        if (existingIndex >= 0)
        {
            SelectAnchorPoint(slot, existingIndex);
            return;
        }

        // 添加新锚点
        AddAnchorPoint(slot, snappedTime);
    }

    /// <summary>
    /// 在指定数据槽中查找靠近给定时间的锚点
    /// </summary>
    private int FindAnchorNear(int slot, float time)
    {
        EasingSlotData slotData = GetSlotData(slot);
        if (slotData == null || slotData.anchorPoints == null) return -1;

        // 时间容差：半拍
        float bpm = BpmManagerUI.GetBpmAtTime(time);
        if (bpm <= 0) bpm = m_gridManager != null ? m_gridManager.m_referenceBpm : 120f;
        float intervalFactor = m_gridManager != null ? m_gridManager.IntervalFactor : 1f;
        float beatInterval = intervalFactor / bpm;
        float tolerance = beatInterval * 0.5f;

        for (int i = 0; i < slotData.anchorPoints.Count; i++)
        {
            if (Mathf.Abs(slotData.anchorPoints[i].time - time) <= tolerance)
            {
                return i;
            }
        }

        return -1;
    }

    #endregion

    #region 锚点管理

    /// <summary>
    /// 添加新锚点，注册撤回/重做
    /// </summary>
    private void AddAnchorPoint(int slot, float time)
    {
        EasingSlotData slotData = GetOrCreateSlotData(slot);
        if (slotData == null) return;

        // 默认值为当前时间点的插值结果（若无锚点则用配置默认值）
        var config = EasingSlotConfigs.Slots[slot];
        float value = slotData.EvaluateAt(time, config.defaultValue, config);

        var anchor = new AnchorPoint(time, value, Ease.Linear);

        int insertIndex = InsertAnchorByTime(slot, anchor);

        SaveCubeData();
        RebuildAnchorMarkers();
        SelectAnchorPoint(slot, insertIndex);

        // 记录到全局撤回/重做系统
        var anchorClone = anchor.Clone();
        UndoRedoManager.Execute(
            undo: () =>
            {
                RemoveAnchorAtTime(slot, time);
                SaveCubeData();
                RebuildAnchorMarkers();
                DeselectAnchor();
            },
            redo: () =>
            {
                int idx = InsertAnchorByTime(slot, anchorClone.Clone());
                SaveCubeData();
                RebuildAnchorMarkers();
                SelectAnchorPoint(slot, idx);
            });

        Debug.Log($"[{GetType().Name}] 添加锚点: 槽{slot} 时间{time:F2}s 值{value:F2}");
    }

    /// <summary>
    /// 选中锚点
    /// </summary>
    private void SelectAnchorPoint(int slot, int anchorIndex)
    {
        m_selectedSlot = slot;
        m_selectedAnchorIndex = anchorIndex;
        UpdateAnchorMarkerColors();
        AnchorSelected?.Invoke();
    }

    /// <summary>
    /// 取消选中
    /// </summary>
    public void DeselectAnchor()
    {
        if (m_selectedSlot < 0) return;
        m_selectedSlot = -1;
        m_selectedAnchorIndex = -1;
        UpdateAnchorMarkerColors();
        AnchorDeselected?.Invoke();
    }

    /// <summary>
    /// 删除指定锚点，注册撤回/重做。time=0 处的固定锚点不可删除。
    /// </summary>
    public void DeleteAnchor(int slot, int anchorIndex)
    {
        EasingSlotData slotData = GetSlotData(slot);
        if (slotData == null || anchorIndex < 0 || anchorIndex >= slotData.anchorPoints.Count) return;

        // 不允许删除 time=0 的固定锚点
        if (Mathf.Approximately(slotData.anchorPoints[anchorIndex].time, 0f)) return;

        // 捕获被删除的锚点数据，用于撤回时恢复
        var deletedAnchor = slotData.anchorPoints[anchorIndex].Clone();

        slotData.anchorPoints.RemoveAt(anchorIndex);
        SaveCubeData();

        if (m_selectedSlot == slot && m_selectedAnchorIndex == anchorIndex)
        {
            DeselectAnchor();
        }
        else if (m_selectedSlot == slot && m_selectedAnchorIndex > anchorIndex)
        {
            m_selectedAnchorIndex--;
        }

        RebuildAnchorMarkers();

        // 记录到全局撤回/重做系统
        UndoRedoManager.Execute(
            undo: () =>
            {
                int idx = InsertAnchorByTime(slot, deletedAnchor.Clone());
                SaveCubeData();
                RebuildAnchorMarkers();
                SelectAnchorPoint(slot, idx);
            },
            redo: () =>
            {
                RemoveAnchorAtTime(slot, deletedAnchor.time);
                SaveCubeData();
                RebuildAnchorMarkers();
                DeselectAnchor();
            });
    }

    /// <summary>
    /// 删除当前选中的锚点
    /// </summary>
    public void DeleteSelectedAnchor()
    {
        if (m_selectedSlot < 0 || m_selectedAnchorIndex < 0) return;
        DeleteAnchor(m_selectedSlot, m_selectedAnchorIndex);
    }

    /// <summary>
    /// 按时间升序将锚点插入指定数据槽，返回插入位置的索引。
    /// 供撤回/重做回调使用，确保插入位置与原始操作一致。
    /// </summary>
    private int InsertAnchorByTime(int slot, AnchorPoint anchor)
    {
        EasingSlotData slotData = GetOrCreateSlotData(slot);
        if (slotData == null) return -1;

        int insertIndex = 0;
        for (int i = 0; i < slotData.anchorPoints.Count; i++)
        {
            if (slotData.anchorPoints[i].time < anchor.time)
            {
                insertIndex = i + 1;
            }
            else
            {
                break;
            }
        }
        slotData.anchorPoints.Insert(insertIndex, anchor);
        return insertIndex;
    }

    /// <summary>
    /// 移除指定数据槽中匹配时间的锚点。供撤回/重做回调使用。
    /// </summary>
    private void RemoveAnchorAtTime(int slot, float time)
    {
        EasingSlotData slotData = GetSlotData(slot);
        if (slotData == null) return;

        for (int i = 0; i < slotData.anchorPoints.Count; i++)
        {
            if (Mathf.Abs(slotData.anchorPoints[i].time - time) < 0.001f)
            {
                slotData.anchorPoints.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// 更新选中锚点的数值
    /// </summary>
    public void UpdateSelectedAnchorValue(float value)
    {
        AnchorPoint anchor = GetSelectedAnchorPoint();
        if (anchor == null) return;

        var config = EasingSlotConfigs.Slots[m_selectedSlot];
        value = Mathf.Clamp(value, config.minValue, config.maxValue);
        anchor.value = value;

        SaveCubeData();
        RebuildAnchorMarkers();
    }

    /// <summary>
    /// 更新选中锚点的缓动类型
    /// </summary>
    public void UpdateSelectedAnchorEasing(Ease easingType)
    {
        AnchorPoint anchor = GetSelectedAnchorPoint();
        if (anchor == null) return;

        anchor.easingType = easingType;
        SaveCubeData();
    }

    /// <summary>
    /// 更新选中锚点的缓动权重 (0=线性, 1=完整缓动)
    /// </summary>
    public void UpdateSelectedAnchorWeight(float weight)
    {
        AnchorPoint anchor = GetSelectedAnchorPoint();
        if (anchor == null) return;

        anchor.weight = weight;
        SaveCubeData();
    }

    /// <summary>
    /// 获取当前选中的锚点
    /// </summary>
    public AnchorPoint GetSelectedAnchorPoint()
    {
        if (m_selectedSlot < 0 || m_selectedAnchorIndex < 0) return null;
        EasingSlotData slotData = GetSlotData(m_selectedSlot);
        if (slotData == null || m_selectedAnchorIndex >= slotData.anchorPoints.Count) return null;
        return slotData.anchorPoints[m_selectedAnchorIndex];
    }

    /// <summary>当前是否选中了锚点</summary>
    public bool HasSelection => m_selectedSlot >= 0 && m_selectedAnchorIndex >= 0;

    /// <summary>选中锚点所在的数据槽索引</summary>
    public int SelectedSlot => m_selectedSlot;

    /// <summary>选中锚点在其槽中的索引</summary>
    public int SelectedAnchorIndex => m_selectedAnchorIndex;

    /// <summary>获取数据槽标签名</summary>
    public string GetSlotLabel(int slot)
    {
        return slot >= 0 && slot < k_slotLabels.Length ? k_slotLabels[slot] : slot.ToString();
    }

    #endregion

    #region 可视化更新

    /// <summary>
    /// 每帧更新锚点标记位置与曲线（跟随时间轴滚动）
    /// </summary>
    private void UpdateAnchorVisuals()
    {
        if (m_gridManager == null) return;

        // 检测垂直滚动是否变化，避免不必要的重绘
        float currentScroll = m_gridManager.EffectivePPS; // 间接引用确保 gridManager 存活

        // 更新锚点标记位置
        UpdateAnchorMarkerPositions();

        // 更新曲线
        UpdateCurvePoints();
    }

    /// <summary>
    /// 重建所有锚点标记（锚点增删后调用）
    /// </summary>
    private void RebuildAnchorMarkers()
    {
        // 清除旧标记
        foreach (var marker in m_anchorMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        m_anchorMarkers.Clear();

        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.anchorPoints == null) continue;

            foreach (var anchor in slotData.anchorPoints)
            {
                var marker = CreateAnchorMarker(slot, anchor);
                m_anchorMarkers.Add(marker);
            }
        }

        UpdateAnchorMarkerPositions();
        UpdateAnchorMarkerColors();
    }

    /// <summary>
    /// 创建单个锚点标记
    /// </summary>
    private GameObject CreateAnchorMarker(int slot, AnchorPoint anchor)
    {
        var markerGo = new GameObject($"Anchor_{slot}_{anchor.time:F1}", typeof(RectTransform));
        markerGo.transform.SetParent(m_anchorLayer, false);
        markerGo.layer = 5;

        var rect = markerGo.GetComponent<RectTransform>();
        // 锚点设为左中 (0, 0.5)，与 AnchorLayer 的 pivot 一致，
        // 使 anchoredPosition 的 Y 直接对应 EasingContent 中心坐标系
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.sizeDelta = new Vector2(m_anchorSize, m_anchorSize);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = markerGo.AddComponent<Image>();
        img.color = m_anchorColor;
        img.raycastTarget = false;

        // 记录槽位索引供选择判定使用
        var markerData = markerGo.AddComponent<AnchorMarkerData>();
        markerData.SlotIndex = slot;

        return markerGo;
    }

    /// <summary>
    /// 更新所有锚点标记的位置（跟随时间轴滚动）
    /// </summary>
    private void UpdateAnchorMarkerPositions()
    {
        if (m_gridManager == null) return;

        int markerIdx = 0;
        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.anchorPoints == null) continue;

            foreach (var anchor in slotData.anchorPoints)
            {
                if (markerIdx >= m_anchorMarkers.Count) break;
                var marker = m_anchorMarkers[markerIdx];
                if (marker != null)
                {
                    float x = ValueToSlotX(slot, anchor.value);
                    float y = TimeToLocalY(anchor.time);
                    marker.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                }
                markerIdx++;
            }
        }
    }

    /// <summary>
    /// 更新锚点标记颜色（选中状态高亮）
    /// </summary>
    private void UpdateAnchorMarkerColors()
    {
        int markerIdx = 0;
        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.anchorPoints == null) continue;

            for (int i = 0; i < slotData.anchorPoints.Count; i++)
            {
                if (markerIdx >= m_anchorMarkers.Count) break;
                var marker = m_anchorMarkers[markerIdx];
                if (marker != null)
                {
                    var img = marker.GetComponent<Image>();
                    if (img != null)
                    {
                        bool isSelected = (slot == m_selectedSlot && i == m_selectedAnchorIndex);
                        img.color = isSelected ? m_anchorSelectedColor : m_anchorColor;
                    }
                }
                markerIdx++;
            }
        }
    }

    /// <summary>
    /// 更新曲线：使用 Image 线段池绘制锚点间的缓动函数曲线。
    /// 每帧调用，线段位置随时间轴滚动实时更新。
    /// </summary>
    private void UpdateCurvePoints()
    {
        int segIdx = 0;

        for (int slot = 0; slot < m_lineCount; slot++)
        {
            EasingSlotData slotData = GetSlotData(slot);
            if (slotData == null || slotData.anchorPoints == null || slotData.anchorPoints.Count < 2)
            {
                continue;
            }

            var anchors = slotData.anchorPoints;

            // 遍历锚点对，采样缓动曲线并绘制线段
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                AnchorPoint curr = anchors[i];
                AnchorPoint next = anchors[i + 1];

                float currX = ValueToSlotX(slot, curr.value);
                float currY = TimeToLocalY(curr.time);
                float nextX = ValueToSlotX(slot, next.value);
                float nextY = TimeToLocalY(next.time);

                // 起始点到第一个采样点
                float prevX = currX;
                float prevY = currY;

                for (int s = 1; s <= m_curveSamples; s++)
                {
                    float t = (float)s / m_curveSamples;
                    // 权重混合：weight=0 时线性，weight=1 时完整缓动
                    float easedT = DOVirtual.EasedValue(0f, 1f, t, curr.easingType);
                    float weightedT = Mathf.Lerp(t, easedT, curr.weight);
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

        // 轨道级槽（棱偏移、流速）——从当前选中轨道获取
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

        // 轨道级槽——确保轨道有缓动数据
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
    /// 确保方体级和当前轨道级数据槽在 time=0 处有不可删除的固定锚点（兼容旧数据）。
    /// 方体级：检查 cube.easingSlots 的前 13 个槽；
    /// 轨道级：检查当前选中轨道的 2 个槽。
    /// </summary>
    private void EnsureDefaultAnchors()
    {
        if (m_cubeManager == null) return;
        var cube = m_cubeManager.GetCube(m_cubeManager.ActiveCubeId);
        if (cube == null) return;

        bool modified = false;

        // ---- 方体级槽 ----
        if (cube.easingSlots != null)
        {
            // 兼容旧数据：旧版有15个槽，截断到13个（棱偏移/流速已移至轨道级）
            if (cube.easingSlots.Count > EasingSlotConfigs.CubeSlotCount)
            {
                cube.easingSlots.RemoveRange(EasingSlotConfigs.CubeSlotCount,
                    cube.easingSlots.Count - EasingSlotConfigs.CubeSlotCount);
                modified = true;
            }
            else if (cube.easingSlots.Count < EasingSlotConfigs.CubeSlotCount)
            {
                cube.InitializeDefaultEasingSlots();
                modified = true;
            }
            else
            {
                for (int i = 0; i < cube.easingSlots.Count; i++)
                {
                    var slotData = cube.easingSlots[i];
                    if (slotData.anchorPoints == null || slotData.anchorPoints.Count == 0 ||
                        !Mathf.Approximately(slotData.anchorPoints[0].time, 0f))
                    {
                        var config = EasingSlotConfigs.Slots[i];
                        slotData.anchorPoints.Insert(0, new AnchorPoint(0f, config.defaultValue));
                        modified = true;
                    }
                }
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
                    var slotData = track.easingSlots[i];
                    if (slotData.anchorPoints == null || slotData.anchorPoints.Count == 0 ||
                        !Mathf.Approximately(slotData.anchorPoints[0].time, 0f))
                    {
                        var config = EasingSlotConfigs.Slots[EasingSlotConfigs.CubeSlotCount + i];
                        slotData.anchorPoints.Insert(0, new AnchorPoint(0f, config.defaultValue));
                        modified = true;
                    }
                }
            }
        }

        if (modified)
        {
            SaveCubeData();
        }
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
    /// 活跃方体切换时重新加载锚点
    /// </summary>
    private void OnCubeChanged(int cubeId)
    {
        DeselectAnchor();
        EnsureDefaultAnchors();
        RebuildAnchorMarkers();
    }

    /// <summary>
    /// note 轨道切换时重新加载轨道级锚点（棱偏移、流速）
    /// </summary>
    private void OnTrackChanged(CubeFace face, FaceDirection direction)
    {
        DeselectAnchor();
        EnsureDefaultAnchors();
        RebuildAnchorMarkers();
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
        m_maxScroll = Mathf.Max(0, m_contentWidth - viewportWidth);
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
}

/// <summary>
/// 锚点标记辅助组件：记录所属数据槽索引
/// </summary>
public class AnchorMarkerData : MonoBehaviour
{
    /// <summary>所属数据槽索引</summary>
    public int SlotIndex { get; set; }
}
