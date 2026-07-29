using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 右半缓动函数区管理器：在右半区域绘制 14 条竖线作为后续拓展占位，
/// 支持鼠标拖拽左右滑动。缓动功能暂缺，仅搭建可滚动的结构框架。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EasingAreaManager : MonoBehaviour
{
    [Header("缓动区设置")]
    [Tooltip("竖线数量（供后续拓展）")]
    [SerializeField] private int m_lineCount = 14;
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

    private GridManager m_gridManager;
    private RectTransform m_playScreenRect;
    private RectTransform m_easingViewport;
    private RectTransform m_easingContent;

    // 水平拖拽滚动状态
    private bool m_isDragging;
    private float m_lastMouseX;
    private float m_easingScrollOffset;

    // 内容总宽度与最大滚动范围（缓存，仅在尺寸变化时更新）
    private float m_contentWidth;
    private float m_maxScroll;

    private void Start()
    {
        m_playScreenRect = GetComponent<RectTransform>();
        CacheGridManager();
        CreateEasingArea();
    }

    private void CacheGridManager()
    {
        if (m_gridManager == null)
        {
            m_gridManager = GetComponent<GridManager>();
        }
    }

    private void Update()
    {
        CacheGridManager();
        if (m_easingContent == null) return;

        // 每帧更新最大滚动范围，防止 Start 时 rect 尚未就绪导致 m_maxScroll 为 0
        UpdateMaxScroll();
        HandleDragScroll();
    }

    /// <summary>
    /// 创建缓动区 viewport、content 及竖线
    /// </summary>
    private void CreateEasingArea()
    {
        if (m_playScreenRect == null) return;

        // 若已存在则跳过（防止重复创建）
        var existing = transform.Find("EasingViewport");
        if (existing != null)
        {
            m_easingViewport = existing as RectTransform;
            m_easingContent = m_easingViewport.Find("EasingContent") as RectTransform;
            return;
        }

        float noteAreaRatio = m_gridManager != null ? m_gridManager.m_noteAreaRatio : 0.5f;

        // ---- EasingViewport：右半区域容器，带遮罩裁剪 ----
        var viewportGo = new GameObject("EasingViewport", typeof(RectTransform));
        viewportGo.transform.SetParent(transform, false);
        viewportGo.layer = 5; // UI Layer

        m_easingViewport = viewportGo.GetComponent<RectTransform>();
        // 锚定到右半区域：从 noteAreaRatio 到 1.0
        m_easingViewport.anchorMin = new Vector2(noteAreaRatio, 0);
        m_easingViewport.anchorMax = new Vector2(1, 1);
        m_easingViewport.offsetMin = Vector2.zero;
        m_easingViewport.offsetMax = Vector2.zero;
        m_easingViewport.pivot = new Vector2(0.5f, 0.5f);

        // 背景图：仅装饰，不拦截射线（避免阻挡 PlayScreen 的滚轮事件）
        var bg = viewportGo.AddComponent<Image>();
        bg.color = m_backgroundColor;
        bg.raycastTarget = false;

        // 遮罩裁剪，使内容超出视口部分不可见
        viewportGo.AddComponent<RectMask2D>();

        // ---- EasingContent：可水平滚动的内容容器 ----
        var contentGo = new GameObject("EasingContent", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        contentGo.layer = 5;

        m_easingContent = contentGo.GetComponent<RectTransform>();
        m_easingContent.anchorMin = new Vector2(0, 0);
        m_easingContent.anchorMax = new Vector2(0, 1);
        m_easingContent.pivot = new Vector2(0, 0.5f);
        m_easingContent.anchoredPosition = Vector2.zero;

        // 内容宽度 = 竖线数量 * 间距 + 两侧留白
        m_contentWidth = m_lineCount * m_lineSpacing + m_lineSpacing;
        m_easingContent.sizeDelta = new Vector2(m_contentWidth, 0);

        UpdateMaxScroll();

        // 绘制竖线
        DrawVerticalLines();

        // 绘制水平参考线（视觉辅助，不随时间轴滚动）
        DrawHorizontalReferenceLines();
    }

    /// <summary>
    /// 绘制 14 条竖线，从内容左边缘均匀分布
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
        }
    }

    /// <summary>
    /// 绘制水平参考线（固定间距，纯视觉辅助）
    /// </summary>
    private void DrawHorizontalReferenceLines()
    {
        if (m_easingContent == null || m_hLineCount < 2) return;

        // 使用视口高度估算间距（内容高度跟随视口）
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
    /// 鼠标拖拽实现水平滚动：仅在右半缓动区内响应。
    /// 使用 EasingViewport 自身 rect 做命中检测，确保判定准确。
    /// </summary>
    private void HandleDragScroll()
    {
        if (m_playScreenRect == null || m_easingViewport == null) return;

        // 使用 EasingViewport 自身 rect 做命中检测
        Vector2 viewportLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_easingViewport, Input.mousePosition, null, out viewportLocal);
        bool inEasingArea = m_easingViewport.rect.Contains(viewportLocal);

        // 按下鼠标且在缓动区内：开始拖拽
        if (Input.GetMouseButtonDown(0) && inEasingArea)
        {
            m_isDragging = true;
            m_lastMouseX = Input.mousePosition.x;
        }

        if (Input.GetMouseButtonUp(0))
        {
            m_isDragging = false;
        }

        if (!m_isDragging) return;

        // 拖拽增量：鼠标右移 -> 内容右移 -> 显示左侧更多线
        float delta = Input.mousePosition.x - m_lastMouseX;
        m_lastMouseX = Input.mousePosition.x;

        ApplyScroll(-delta);
    }

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

        // 内容向左偏移 = scrollOffset（offset 增大时内容左移，显示右侧线）
        m_easingContent.anchoredPosition = new Vector2(-m_easingScrollOffset, 0);
    }

    /// <summary>
    /// 更新最大滚动范围（视口宽度变化时调用）
    /// </summary>
    private void UpdateMaxScroll()
    {
        float viewportWidth = m_easingViewport != null ? m_easingViewport.rect.width : 0;
        m_maxScroll = Mathf.Max(0, m_contentWidth - viewportWidth);
    }
}
