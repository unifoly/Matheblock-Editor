using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridManager : MonoBehaviour
{
    [Header("网格设置")]
    public float m_pixelsPerWholeNote = 200f;
    public float m_referenceBpm = 120f;
    public int m_defaultYLines = 10;
    public float m_pixelsPerSecond = 200f;
    // 网格线：纯白色，在暗化背景上保持清晰可见
    public Color m_gridColor = new Color(1f, 1f, 1f, 0.7f);
    // 中心/边缘垂直线：纯白色
    public Color m_centerLineColor = new Color(1f, 1f, 1f, 0.7f);
    // 每个音节（整拍）对应的粗线条：金色
    public Color m_beatLineColor = new Color(1f, 0.843f, 0f, 0.7f);

    private RectTransform m_playScreenRect;
    private RectTransform m_gridContainerRect;
    private int m_yLineCount;
    private int m_xLineValue;
    private float m_scrollOffset;
    private float m_viewportWidth;
    private float m_viewportHeight;
    private float m_cachedIntervalFactor;
    private TMP_InputField m_xLineInput;
    private TMP_InputField m_yLineInput;

    // 缩放倍率：Ctrl+滚轮调整，仅放大/缩小线间距（时间轴像素密度），实质节拍位置不变
    private const float k_minZoomScale = 0.1f;
    private const float k_maxZoomScale = 8f;
    // 每次缩放的步进系数：×1.1 放大、×1/1.1 缩小（10% 一档，连续可调）
    private const float k_zoomStep = 1.1f;
    private float m_zoomScale = 1f;

    // 水平线对象池，避免每帧 DestroyImmediate + new GameObject
    private List<GameObject> m_hLinePool = new List<GameObject>();

    // 水平线 Image 引用缓存，与 m_hLinePool 索引一一对应，避免每帧 GetComponent
    private List<Image> m_hLineImages = new List<Image>();

    // 基准线（固定在视口 3/4 处的橙色粗线）
    private GameObject m_referenceLine;

    // 时间同步相关
    private Slider m_timeSlider;
    private TextMeshProUGUI m_timeText;
    private double m_totalMusicTime;
    private bool m_isUpdatingFromSlider;
    private bool m_scrollInitialized;

    private void Start()
    {
        m_xLineValue = 4;
        m_yLineCount = m_defaultYLines;

        FindPlayScreen();
        CreateGridContainer();
        CreateReferenceLine();
        FindInputFields();
        FindTimeControls();
        UpdateViewportSize();

        // 缓存 intervalFactor，后续仅在 xLineValue 变化时更新
        UpdateCachedIntervalFactor();

        // 确保 BPM 缓存已加载，即使未打开 BPM 管理面板
        BpmManagerUI.LoadBpmCacheFromJson();

        InitScrollFromSlider();
        CreateGrid();

        // 注册事件监听：GridManager 由 EditorInit 在运行时 AddComponent 挂载，
        // OnEnable 会在 Start 之前执行（此时引用尚未初始化），因此监听在此处注册。
        RegisterCallbacks();
    }

    // 注册输入框和滑块的事件回调
    private void RegisterCallbacks()
    {
        if (m_xLineInput != null)
            m_xLineInput.onValueChanged.AddListener(OnXSpacingChanged);
        if (m_yLineInput != null)
            m_yLineInput.onValueChanged.AddListener(OnYLineCountChanged);
        if (m_timeSlider != null)
            m_timeSlider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    // 缩放后的有效像素密度，用于像素坐标↔时间换算（绘制位置、视口时间窗口）
    private float EffectivePixelsPerSecond => m_pixelsPerSecond * m_zoomScale;

    private void UpdateCachedIntervalFactor()
    {
        // intervalFactor 表示"每条网格线的时间间隔"量纲，必须独立于像素密度，
        // 否则 pixelSpacing = (intervalFactor/bpm) * EffPPS 会把 zoom 系数约掉。
        // 因此这里用基准 m_pixelsPerSecond，zoom 只通过 EffPPS 影响"像素间距"，
        // 不影响"哪一时刻打一条线"——即缩放只改变视觉密度，不动节拍的位置。
        if (m_pixelsPerSecond > 0)
            m_cachedIntervalFactor = m_pixelsPerWholeNote * m_referenceBpm / (m_xLineValue * m_pixelsPerSecond);
    }

    // OnEnable 在 AddComponent 时早于 Start 执行，此时引用为空，
    // 实际注册统一在 Start 末尾通过 RegisterCallbacks() 完成。
    // 若组件被禁用后再次启用（Start 不会重新执行），此处负责重新注册。
    private void OnEnable()
    {
        RegisterCallbacks();
    }

    private void OnDisable()
    {
        if (m_xLineInput != null)
            m_xLineInput.onValueChanged.RemoveListener(OnXSpacingChanged);
        if (m_yLineInput != null)
            m_yLineInput.onValueChanged.RemoveListener(OnYLineCountChanged);
        if (m_timeSlider != null)
            m_timeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    private void FindPlayScreen()
    {
        m_playScreenRect = GetComponent<RectTransform>();
    }

    private void CreateReferenceLine()
    {
        // 基准线固定在视口 3/4 处（距顶部 3/4），不随滚动移动
        var go = new GameObject("ReferenceLine");
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        RectTransform rect = go.AddComponent<RectTransform>();
        // 锚点设在 y=0.25（即距底部 1/4 = 距顶部 3/4），横向撑满
        rect.anchorMin = new Vector2(0, 0.25f);
        rect.anchorMax = new Vector2(1, 0.25f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0, 6);

        Image image = go.AddComponent<Image>();
        image.color = new Color(1f, 0.5f, 0f, 0.9f);
        image.raycastTarget = false;
    }

    private void CreateGridContainer()
    {
        var containerObj = transform.Find("GridContainer");
        if (containerObj != null) return;

        var go = new GameObject("GridContainer");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;

        m_gridContainerRect = go.AddComponent<RectTransform>();
        m_gridContainerRect.anchorMin = Vector2.zero;
        m_gridContainerRect.anchorMax = Vector2.one;
        m_gridContainerRect.sizeDelta = Vector2.zero;
        m_gridContainerRect.pivot = new Vector2(0.5f, 0.5f);

        go.AddComponent<RectMask2D>();
    }

    private void FindInputFields()
    {
        var xLineObj = GameObject.Find("XLine");
        if (xLineObj != null)
        {
            m_xLineInput = xLineObj.transform.Find("Input").GetComponent<TMP_InputField>();
            if (m_xLineInput != null)
            {
                m_xLineInput.text = m_xLineValue.ToString();
            }
        }

        var yLineObj = GameObject.Find("YLine");
        if (yLineObj != null)
        {
            m_yLineInput = yLineObj.transform.Find("Input").GetComponent<TMP_InputField>();
            if (m_yLineInput != null)
            {
                m_yLineInput.text = m_defaultYLines.ToString();
            }
        }
    }

    private void FindTimeControls()
    {
        var sliderObj = GameObject.Find("MusicTime");
        if (sliderObj != null)
        {
            m_timeSlider = sliderObj.GetComponent<Slider>();
        }

        var timeObj = GameObject.Find("Time");
        if (timeObj != null)
        {
            m_timeText = timeObj.GetComponent<TextMeshProUGUI>();
        }

        m_totalMusicTime = MusicTimeStampController.MusicTime;
    }

    private void InitScrollFromSlider()
    {
        if (m_timeSlider != null && TotalScrollRange > 0)
        {
            m_scrollOffset = m_timeSlider.value * TotalScrollRange;
            m_scrollInitialized = true;
        }
        else
        {
            m_scrollOffset = 0;
        }
    }

    private void UpdateTotalMusicTime()
    {
        m_totalMusicTime = MusicTimeStampController.MusicTime;
    }

    private float TotalScrollRange
    {
        get { return (float)m_totalMusicTime * EffectivePixelsPerSecond; }
    }

    private void OnSliderValueChanged(float normalizedValue)
    {
        if (m_isUpdatingFromSlider) return;

        UpdateTotalMusicTime();
        m_scrollOffset = normalizedValue * TotalScrollRange;
        CreateGrid();
        UpdateTimeText();
    }

    private void UpdateTimeText()
    {
        if (m_timeText == null) return;

        double currentTime = m_totalMusicTime;
        if (TotalScrollRange > 0)
        {
            currentTime = (m_scrollOffset / TotalScrollRange) * m_totalMusicTime;
        }
        currentTime = System.Math.Round(currentTime, 2);
        m_timeText.text = $"{currentTime}/{System.Math.Round(m_totalMusicTime, 2)}";
    }

    private void UpdateSlider()
    {
        if (m_timeSlider == null) return;

        float normalized = 1f;
        if (TotalScrollRange > 0)
        {
            normalized = Mathf.Clamp01(m_scrollOffset / TotalScrollRange);
        }

        m_isUpdatingFromSlider = true;
        m_timeSlider.value = normalized;
        m_isUpdatingFromSlider = false;
    }

    private void UpdateViewportSize()
    {
        if (m_playScreenRect != null)
        {
            m_viewportWidth = m_playScreenRect.rect.width;
            m_viewportHeight = m_playScreenRect.rect.height;
        }
    }

    private void OnXSpacingChanged(string value)
    {
        if (int.TryParse(value, out int noteValue) && noteValue > 0)
        {
            m_xLineValue = noteValue;
            UpdateCachedIntervalFactor();
            CreateGrid();
        }
    }

    private void OnYLineCountChanged(string value)
    {
        if (int.TryParse(value, out int newCount) && newCount >= 2)
        {
            m_yLineCount = newCount;
            CreateGrid();
        }
    }

    public void CreateGrid()
    {
        UpdateViewportSize();
        UpdateTotalMusicTime();

        ClearVerticalLines();
        DrawVerticalLines();
        DrawHorizontalLines();
    }

    /// <summary>
    /// 只清除垂直线（水平线由对象池管理，不销毁）
    /// </summary>
    private void ClearVerticalLines()
    {
        if (m_gridContainerRect == null) return;

        for (int i = m_gridContainerRect.childCount - 1; i >= 0; i--)
        {
            var child = m_gridContainerRect.GetChild(i);
            if (child.name.StartsWith("VLine_"))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void DrawVerticalLines()
    {
        if (m_playScreenRect == null || m_yLineCount < 2 || m_viewportWidth <= 0) return;

        float spacing = m_viewportWidth / (m_yLineCount - 1);
        float startX = -m_viewportWidth / 2f;

        for (int i = 0; i < m_yLineCount; i++)
        {
            GameObject lineObj = new GameObject($"VLine_{i}");
            lineObj.transform.SetParent(m_gridContainerRect);
            lineObj.transform.localScale = Vector3.one;

            RectTransform rect = lineObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(2, 0);
            rect.localPosition = new Vector3(startX + i * spacing, 0, 0);

            Image image = lineObj.AddComponent<Image>();
            image.color = (i == 0 || i == m_yLineCount - 1) ? m_centerLineColor : m_gridColor;
        }
    }

    /// <summary>
    /// 从 time=0 向前迭代至可见范围，使用对象池绘制水平线。
    /// 每条线通过 BpmManagerUI.GetBpmAtTime() 获取对应时间点的 BPM 来确定间距。
    /// m_scrollOffset / m_pixelsPerSecond = 3/4 基准线处对应的时间点，视口窗口始终固定。
    /// </summary>
    private void DrawHorizontalLines()
    {
        if (m_playScreenRect == null || m_viewportHeight <= 0 || EffectivePixelsPerSecond <= 0) return;
        if (m_totalMusicTime <= 0) return;

        float intervalFactor = m_cachedIntervalFactor;
        if (intervalFactor <= 0) return;

        // currentTime: 视口 3/4 基准线处对应的时间点
        float currentTime = m_scrollOffset / EffectivePixelsPerSecond;

        // 视口时间窗口：基准线向上 3/4 视口高 + 向下 1/4 视口高
        float halfWindowAbove = 3f * m_viewportHeight / (4f * EffectivePixelsPerSecond);
        float halfWindowBelow = m_viewportHeight / (4f * EffectivePixelsPerSecond);

        float visibleTimeMin = Mathf.Max(0, currentTime - halfWindowBelow);
        float visibleTimeMax = Mathf.Min((float)m_totalMusicTime, currentTime + halfWindowAbove);

        // 安全余量：提前一个beat开始绘制，避免在BPM变化边界处漏线
        float margin = intervalFactor / m_referenceBpm;
        float drawTimeMin = Mathf.Max(0, visibleTimeMin - margin);

        // 从 time=0 向前迭代，跳过可视范围之前的 beat（纯CPU，无GameObject创建）
        float time = 0f;
        int beatIndex = 0;

        while (time < drawTimeMin && time < (float)m_totalMusicTime)
        {
            float bpm = BpmManagerUI.GetBpmAtTime(time);
            if (bpm <= 0) bpm = m_referenceBpm;
            time += intervalFactor / bpm;
            beatIndex++;

            // 安全上限，防止无限循环（最多约100万次迭代 ≈ 24小时音频）
            if (beatIndex > 1000000) break;
        }

        // 从第一个可见范围内的 beat 开始绘制
        int poolIndex = 0;

        // 调试：记录可见线中第一条和最后一条 BPM，以及BPM发生变化的位置
        System.Text.StringBuilder posLog = new System.Text.StringBuilder();
        int posLogCount = 0;
        float firstBpm = 0f;
        float lastBpm = 0f;
        int bpmChangeCount = 0;
        float prevDrawnBpm = -1f;

        while (time <= visibleTimeMax + 0.001f && time < (float)m_totalMusicTime)
        {
            float bpm = BpmManagerUI.GetBpmAtTime(time);
            if (bpm <= 0) bpm = m_referenceBpm;

            // yPos: 相对于视口中心的坐标，基准线在 -vh/4（视口 3/4 处从顶部算）
            float yPosRaw = (time - currentTime) * EffectivePixelsPerSecond - m_viewportHeight * 0.25f;
            float yPos = Mathf.Round(yPosRaw);

            // 只绘制在视口内的线（留一点余量）
            if (yPosRaw >= -m_viewportHeight / 2f - 10f && yPosRaw <= m_viewportHeight / 2f + 10f)
            {
                bool isWholeNote = (beatIndex % m_xLineValue == 0);
                GameObject lineObj = GetOrCreateHLine(poolIndex);
                poolIndex++;

                RectTransform rect = lineObj.transform as RectTransform;
                rect.sizeDelta = new Vector2(0, isWholeNote ? 5 : 2);
                rect.localPosition = new Vector3(0, yPos, 0);

                Image image = m_hLineImages[poolIndex - 1];
                image.color = isWholeNote ? m_beatLineColor : m_gridColor;

                // 追踪 BPM 变化
                if (poolIndex == 1) firstBpm = bpm;
                lastBpm = bpm;
                if (prevDrawnBpm > 0 && Mathf.Abs(bpm - prevDrawnBpm) > 0.1f)
                    bpmChangeCount++;
                prevDrawnBpm = bpm;

                // 记录前8条线的位置
                if (posLogCount < 8)
                {
                    posLog.Append($" [t={time:F3}bpm={bpm:F0}y={yPos:F0}]");
                    posLogCount++;
                }
            }

            time += intervalFactor / bpm;
            beatIndex++;

            // 安全上限
            if (poolIndex > 5000) break;
        }

        // 停用多余的池对象
        for (int i = poolIndex; i < m_hLinePool.Count; i++)
        {
            m_hLinePool[i].SetActive(false);
        }

        // 调试输出（功能已完善，注释掉）
        // Debug.Log($"[GridManager] curTime={currentTime:F2}s visTime=[{visibleTimeMin:F1}, {visibleTimeMax:F1}] " +
        //     $"beats={poolIndex} BPM:first={firstBpm:F0} last={lastBpm:F0} changes={bpmChangeCount}{posLog}");
    }

    /// <summary>
    /// 从对象池获取或创建水平线GameObject
    /// </summary>
    private GameObject GetOrCreateHLine(int index)
    {
        if (index < m_hLinePool.Count)
        {
            m_hLinePool[index].SetActive(true);
            return m_hLinePool[index];
        }

        GameObject lineObj = new GameObject($"HLine_{index}");
        lineObj.transform.SetParent(m_gridContainerRect);
        lineObj.transform.localScale = Vector3.one;

        RectTransform rect = lineObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0.5f);

        lineObj.AddComponent<Image>();

        m_hLinePool.Add(lineObj);
        m_hLineImages.Add(lineObj.GetComponent<Image>());
        return lineObj;
    }

    public void HandleScroll(float delta)
    {
        UpdateTotalMusicTime();

        if (!m_scrollInitialized && TotalScrollRange > 0 && m_timeSlider != null)
        {
            m_scrollOffset = m_timeSlider.value * TotalScrollRange;
            m_scrollInitialized = true;
        }

        m_scrollOffset -= delta * (m_pixelsPerWholeNote / m_xLineValue) * 0.5f * m_zoomScale;

        if (TotalScrollRange > 0)
        {
            m_scrollOffset = Mathf.Clamp(m_scrollOffset, 0, TotalScrollRange);
        }

        CreateGrid();

        if (TotalScrollRange > 0)
        {
            UpdateSlider();
            UpdateTimeText();
        }
    }

    /// <summary>
    /// 缩放线间距：Ctrl+滚轮调用。只改变像素密度（视觉间距），不改节拍的物理时长与位置。
    /// 锚点：缩放前后视口中心对应的绝对时间保持不变，因此缩放时不会发生跳变。
    /// </summary>
    public void HandleZoom(float scrollDelta)
    {
        // delta 为 0（回调偶发空值）时直接忽略，避免误触发缩小
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        // 步进只看符号，每次固定 ×k_zoomStep 或 ÷k_zoomStep（10% 一档，连续可调），
        // 这样无论输入幅度（滚轮 ±0.1 / 键盘 ±1）都产生一致的可预期步进
        float factor = scrollDelta > 0 ? k_zoomStep : 1f / k_zoomStep;

        // 缩放前记录视口中心对应的绝对时间（基准线在 3/4，视口中心相对其偏移 +vh/4）
        float refTime = m_scrollOffset / EffectivePixelsPerSecond;
        float centerTime = refTime + m_viewportHeight * 0.25f / EffectivePixelsPerSecond;

        m_zoomScale = Mathf.Clamp(m_zoomScale * factor, k_minZoomScale, k_maxZoomScale);

        UpdateTotalMusicTime();
        UpdateCachedIntervalFactor();

        // 重新计算 m_scrollOffset 使缩放后中心时间锚点不变：
        // centerTime = m_scrollOffset/EffPPS + vh*0.25/EffPPS
        // => m_scrollOffset = centerTime * EffPPS - vh*0.25
        m_scrollOffset = centerTime * EffectivePixelsPerSecond - m_viewportHeight * 0.25f;

        if (TotalScrollRange > 0)
        {
            m_scrollOffset = Mathf.Clamp(m_scrollOffset, 0, TotalScrollRange);
        }

        CreateGrid();

        if (TotalScrollRange > 0)
        {
            UpdateSlider();
            UpdateTimeText();
        }

        // Debug.Log($"[GridManager] HandleZoom delta={scrollDelta:F3} factor={factor:F3} " +
        //          $"zoomScale={m_zoomScale:F3} centerTime={centerTime:F3}s scrollOffset={m_scrollOffset:F1}");
    }

    public float CurrentOffset => m_scrollOffset;

    public void ResetOffset()
    {
        m_scrollOffset = 0;
        m_scrollInitialized = false;
        CreateGrid();
    }
}
