using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class BpmManagerUI : MonoBehaviour
{
    private const float k_nodeHeight = 104f;
    private const float k_nodeSpacing = 6f;
    private const float k_backButtonWidth = 60f;
    private const float k_backButtonHeight = 36f;
    private const float k_addButtonHeight = 40f;
    private const float k_titleHeight = 40f;
    private const float k_padding = 12f;

    private GameObject m_functionChanger;
    private Button m_bpmButton;
    private GameObject m_bpmPanel;
    private List<BpmNodeEntry> m_nodeEntries;
    private Transform m_contentContainer;

    // 中文字体（动态 TMP 字体，从 Resources/Fonts/black.ttf 加载）
    private TMP_FontAsset m_chineseFont;
    private RectTransform m_contentRect;
    private Button m_saveButton;
    private Slider m_musicTimeSlider;

    // 撤回/重做：编辑前的 BPM 节点快照（onSelect 时捕获）
    private List<(float time, float bpm)> m_bpmBeforeSnapshot;

    private class BpmNodeEntry
    {
        public GameObject Root;
        public TMP_InputField TimeInput;
        public TMP_InputField BpmInput;
        public Button RemoveButton;
    }

    [Serializable]
    private class ChartJsonInfo
    {
        public string MusicName;
        public string Charter;
        public string Illustrationer;
        public string Musician;
        // 音乐偏移（毫秒），保留 InfoManagerUI 写入的字段，避免 BPM 保存时丢失
        public float offset;
    }

    [Serializable]
    private class BpmJsonNode
    {
        public float time;
        public float bpm;
    }

    /// <summary>
    /// 静态 BPM 缓存，供 GridManager 等组件查询当前 BPM
    /// </summary>
    private static List<(float time, float bpm)> s_cachedBpmNodes = new List<(float, float)>();

    /// <summary>
    /// BPM 数据版本号，缓存更新时递增，GridManager 检测变化后自动刷新网格
    /// </summary>
    public static int BpmVersion { get; private set; } = 0;

    /// <summary>
    /// 获取指定时间点的 BPM 值（取最近一个时间 ≤ 指定时间的节点）
    /// </summary>
    public static float GetBpmAtTime(float time)
    {
        if (s_cachedBpmNodes.Count == 0)
            return 120f;

        float result = s_cachedBpmNodes[0].bpm;
        for (int i = 0; i < s_cachedBpmNodes.Count; i++)
        {
            if (s_cachedBpmNodes[i].time <= time + 0.001f)
                result = s_cachedBpmNodes[i].bpm;
            else
                break;
        }
        return result;
    }

    /// <summary>
    /// 从 chart.tmp 加载 BPM 节点到静态缓存（不涉及 UI 面板）。
    /// 供 GridManager 等组件在初始化时调用，确保不需要打开 BPM 面板也能获取 BPM 数据。
    /// </summary>
    public static void LoadBpmCacheFromJson()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath))
            return;

        var tmpPath = System.IO.Path.Combine(EditorInit.ChartPath, "chart.tmp");
        if (!System.IO.File.Exists(tmpPath))
            return;

        try
        {
            var json = System.IO.File.ReadAllText(tmpPath);
            var data = JsonUtility.FromJson<ChartJsonData>(json);

            if (data == null || data.bpmNodes == null || data.bpmNodes.Count == 0)
                return;

            PopulateCacheFromNodes(data.bpmNodes);
            Debug.Log($"[BpmManagerUI] 启动时自动加载 {s_cachedBpmNodes.Count} 个 BPM 节点到缓存");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BpmManagerUI] 加载BPM缓存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将 BpmJsonNode 列表排序后写入静态缓存
    /// </summary>
    private static void PopulateCacheFromNodes(List<BpmJsonNode> nodes)
    {
        nodes.Sort((a, b) => a.time.CompareTo(b.time));
        s_cachedBpmNodes.Clear();
        foreach (var node in nodes)
        {
            s_cachedBpmNodes.Add((node.time, node.bpm));
        }
        BpmVersion++;
    }

    /// <summary>
    /// 缓存更新后通知 GridManager 立即刷新网格
    /// </summary>
    private static void NotifyGridRefresh()
    {
        var grid = UnityEngine.Object.FindObjectOfType<GridManager>();
        if (grid != null)
        {
            grid.CreateGrid();
        }
    }

    [Serializable]
    private class ChartJsonData
    {
        public ChartJsonInfo info;
        public List<BpmJsonNode> bpmNodes;
        // 保留 notes 字段，避免 BPM 保存时丢失 NotePlacementManager 写入的数据
        public List<NoteJsonNode> notes;
        // 保留 cubes 字段，避免 BPM 保存时丢失 CubeManager 写入的方体数据
        public List<CubeData> cubes;
    }

    private void Awake()
    {
        m_nodeEntries = new List<BpmNodeEntry>();

        // BPMManager 的父节点是 FunctionChanger
        m_functionChanger = transform.parent.gameObject;

        // 获取自身 Button 组件
        m_bpmButton = GetComponent<Button>();

        // 清理可能残留的旧 BpmPanel（防止重复）
        var oldPanel = m_functionChanger.transform.Find("BpmPanel");
        if (oldPanel != null)
        {
            Destroy(oldPanel.gameObject);
        }

        // 查找MusicTime滑块以获取当前播放时间
        var sliderObj = GameObject.Find("MusicTime");
        if (sliderObj != null)
        {
            m_musicTimeSlider = sliderObj.GetComponent<Slider>();
        }

        // 查找 UpperList 下的 Save 按钮
        var saveObj = GameObject.Find("Save");
        if (saveObj != null)
        {
            m_saveButton = saveObj.GetComponent<Button>();
            Debug.Log($"[{GetType().Name}] Save 按钮绑定成功: {saveObj.name}");
        }
        else
        {
            Debug.LogError($"[{GetType().Name}] 未找到 Save 按钮！请确认场景中存在名为 Save 的 GameObject");
        }
    }

    private void Start()
    {
        // 不在 Start 中创建面板，改为懒加载（首次点击时创建）
    }

    private void OnEnable()
    {
        if (m_bpmButton != null)
        {
            m_bpmButton.onClick.RemoveListener(HandleBpmButtonClicked);
            m_bpmButton.onClick.AddListener(HandleBpmButtonClicked);
        }

        if (m_saveButton != null)
        {
            m_saveButton.onClick.RemoveListener(HandleSaveButtonClicked);
            m_saveButton.onClick.AddListener(HandleSaveButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (m_bpmButton != null)
        {
            m_bpmButton.onClick.RemoveListener(HandleBpmButtonClicked);
        }

        // 不移除 Save 按钮监听器：AnchorPointEditorUI 等面板会禁用 FunctionChanger 子物体，
        // 触发 OnDisable，若移除监听器则 Save 按钮失效。OnEnable 中已有 RemoveListener 防重复注册。
    }

    /// <summary>
    /// 点击 "BPM管理" 按钮：首次点击创建面板，后续点击显示面板
    /// </summary>
    private void HandleBpmButtonClicked()
    {
        // 先隐藏原始按钮（避免面板创建时短暂重叠）
        SetOriginalButtonsActive(false);

        // 懒加载：首次点击时才构建面板
        if (m_bpmPanel == null)
        {
            BuildBpmPanel();

            // 首次构建后从 JSON 加载已有节点
            LoadBpmNodesFromJson();

            // 若 .tmp 中尚无 bpmNodes，将默认节点写入 .tmp
            var tmpPath = GetTmpJsonPath();
            if (!string.IsNullOrEmpty(tmpPath) && File.Exists(tmpPath))
            {
                var json = File.ReadAllText(tmpPath);
                var data = JsonUtility.FromJson<ChartJsonData>(json);
                if (data == null || data.bpmNodes == null || data.bpmNodes.Count == 0)
                {
                    SaveBpmNodesToJson();
                }
            }
        }

        // 确保面板激活并置顶
        m_bpmPanel.SetActive(true);
        m_bpmPanel.transform.SetAsLastSibling();

        // 强制重建 Canvas 以确保动态 UI 按钮的射线检测生效
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 点击返回键：隐藏 BPM 面板，还原原始按钮
    /// </summary>
    private void HandleBackButtonClicked()
    {
        m_bpmPanel.SetActive(false);
        SetOriginalButtonsActive(true);
    }

    /// <summary>
    /// 点击保存按钮：先将 BPM 写入 chart.tmp，再覆写 chart.json 完成持久化
    /// </summary>
    private void HandleSaveButtonClicked()
    {
        Debug.Log($"[{GetType().Name}] Save 点击: entries={m_nodeEntries.Count}, panelActive={(m_bpmPanel != null && m_bpmPanel.activeSelf)}");

        // 确保所有数据（含方体/锚点）持久化到 chart.json
        EditorInit.PersistToChartJson();
    }

    /// <summary>
    /// 构建整个 BPM 管理面板 UI（仅执行一次，由首次点击触发）
    /// </summary>
    private void BuildBpmPanel()
    {
        // 双重去重：再次检查是否已有面板
        if (m_bpmPanel != null)
        {
            return;
        }

        // 最终保底：检查 FunctionChanger 下是否已存在（命名冲突场景）
        var existing = m_functionChanger.transform.Find("BpmPanel");
        if (existing != null)
        {
            m_bpmPanel = existing.gameObject;
            return;
        }

        // 创建面板根节点（创建即活跃，不做 SetActive(false)）
        m_bpmPanel = CreateUIObject("BpmPanel", m_functionChanger.transform);
        var panelRect = m_bpmPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 背景图：仅装饰，不拦截射线
        var panelBg = m_bpmPanel.AddComponent<Image>();
        panelBg.color = new Color(0.235f, 0.235f, 0.235f, 1f);
        panelBg.raycastTarget = false;

        // 顶部区域：返回键 + 标题
        BuildTopBar();

        // 可滚动节点列表区域
        BuildScrollView();

        // 添加节点按钮（关键：此时面板已活跃，Button.onClick 正常注册）
        BuildAddButton();

        // 添加默认节点（时间=0）
        AddNode(0f, 120f);

        // 构建完成后强制刷新布局（从最底层 Content 开始逐级重建）
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_contentRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 构建顶部标题栏和返回键
    /// </summary>
    private void BuildTopBar()
    {
        var topBar = CreateUIObject("TopBar", m_bpmPanel.transform);
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
        backBtn.interactable = true;
        backBtn.onClick.AddListener(HandleBackButtonClicked);

        // 返回键文字
        var backText = CreateText("<", backBtnGo.transform, 24);
        backText.raycastTarget = false;
        var backTextRect = backText.GetComponent<RectTransform>();
        backTextRect.anchorMin = Vector2.zero;
        backTextRect.anchorMax = Vector2.one;
        backTextRect.offsetMin = Vector2.zero;
        backTextRect.offsetMax = Vector2.zero;
        backText.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>
    /// 构建可滚动节点列表
    /// </summary>
    private void BuildScrollView()
    {
        // ScrollView 外层容器
        var scrollGo = CreateUIObject("ScrollView", m_bpmPanel.transform);
        var scrollRectTrans = scrollGo.GetComponent<RectTransform>();
        scrollRectTrans.anchorMin = new Vector2(0, 0);
        scrollRectTrans.anchorMax = new Vector2(1, 1);
        // 顶部留出标题栏 + 分隔，底部留出添加按钮
        scrollRectTrans.offsetMin = new Vector2(k_padding, k_addButtonHeight + k_padding + k_nodeSpacing);
        scrollRectTrans.offsetMax = new Vector2(-k_padding, -(k_titleHeight + k_padding * 2));

        // ScrollView 自身背景（仅装饰，不参与遮罩）
        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        scrollImg.raycastTarget = false;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // 遮罩层：用独立 Viewport 承载 Mask（不在 scrollGo 上加 Mask，避免嵌套裁剪冲突）
        var viewportGo = CreateUIObject("Viewport", scrollGo.transform);
        var viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        // Viewport 的 Image 定义裁剪区域形状；color 保持不透明用于 Mask 计算
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 1f);
        viewportImg.raycastTarget = false;

        var viewportMask = viewportGo.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // Content
        var contentGo = CreateUIObject("Content", viewportGo.transform);
        m_contentRect = contentGo.GetComponent<RectTransform>();
        m_contentRect.anchorMin = new Vector2(0, 1);
        m_contentRect.anchorMax = new Vector2(1, 1);
        m_contentRect.pivot = new Vector2(0.5f, 1f);
        m_contentRect.anchoredPosition = Vector2.zero;
        m_contentRect.sizeDelta = new Vector2(0, 0);

        // Content 加一个醒目调试背景（后续可去掉）
        var contentBg = contentGo.AddComponent<Image>();
        contentBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        contentBg.raycastTarget = false;

        var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.spacing = k_nodeSpacing;
        contentLayout.padding = new RectOffset((int)k_padding, (int)k_padding, (int)k_padding, (int)k_padding);

        var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        m_contentContainer = contentGo.transform;

        scrollRect.viewport = viewportRect;
        scrollRect.content = m_contentRect;
    }

    /// <summary>
    /// 构建"添加节点"按钮
    /// </summary>
    private void BuildAddButton()
    {
        var addBtnGo = CreateUIObject("AddButton", m_bpmPanel.transform);
        var addBtnRect = addBtnGo.GetComponent<RectTransform>();
        addBtnRect.anchorMin = new Vector2(0.5f, 0);
        addBtnRect.anchorMax = new Vector2(0.5f, 0);
        addBtnRect.pivot = new Vector2(0.5f, 0);
        addBtnRect.anchoredPosition = new Vector2(0, k_padding);
        addBtnRect.sizeDelta = new Vector2(320, k_addButtonHeight);

        var addBtnImg = addBtnGo.AddComponent<Image>();
        addBtnImg.color = new Color(0.3f, 0.5f, 0.3f, 1f);
        addBtnImg.raycastTarget = true;

        var addButton = addBtnGo.AddComponent<Button>();
        addButton.targetGraphic = addBtnImg;
        addButton.interactable = true;
        addButton.onClick.AddListener(HandleAddNodeClicked);

        var addText = CreateText("+ 添加节点", addBtnGo.transform, 18);
        addText.raycastTarget = false;
        var addTextRect = addText.GetComponent<RectTransform>();
        addTextRect.anchorMin = Vector2.zero;
        addTextRect.anchorMax = Vector2.one;
        addTextRect.offsetMin = Vector2.zero;
        addTextRect.offsetMax = Vector2.zero;
        addText.alignment = TextAlignmentOptions.Center;

        // 确保按钮在最上层以接收点击
        addBtnGo.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 处理"添加节点"按钮点击
    /// </summary>
    private void HandleAddNodeClicked()
    {
        Debug.Log($"[BpmManagerUI] 添加节点按钮被点击");

        var beforeSnapshot = CaptureBpmNodes();

        var currentTime = GetCurrentMusicTime();
        AddNode(currentTime, -1f);

        // 时间冲突时自动微调，而非直接拒绝
        if (!EnsureUniqueTime(m_nodeEntries.Count - 1))
        {
            // 无法找到合适的时间（极端情况），移除节点
            var lastIndex = m_nodeEntries.Count - 1;
            var invalidEntry = m_nodeEntries[lastIndex];
            m_nodeEntries.RemoveAt(lastIndex);
            Destroy(invalidEntry.Root);
            ShowWarningPopup("无法添加节点：时间密度过高");
            return;
        }

        // 即时写入 .tmp
        SaveBpmNodesToJson();

        // 记录到全局撤回/重做系统
        var afterSnapshot = CaptureBpmNodes();
        UndoRedoManager.Execute(
            undo: () => RestoreBpmNodes(beforeSnapshot),
            redo: () => RestoreBpmNodes(afterSnapshot));

        // 新增节点后强制刷新整个 Content 布局树
        if (m_contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_contentRect);
            Canvas.ForceUpdateCanvases();
        }
    }

    /// <summary>
    /// 确保指定索引的节点时间唯一且升序，必要时自动微调时间
    /// </summary>
    private bool EnsureUniqueTime(int index)
    {
        if (index < 0 || index >= m_nodeEntries.Count)
        {
            return false;
        }

        var entry = m_nodeEntries[index];
        if (!float.TryParse(entry.TimeInput.text, out float currentTime))
        {
            return false;
        }

        float minTime = 0f;
        if (index > 0 && float.TryParse(m_nodeEntries[index - 1].TimeInput.text, out float prevTime))
        {
            minTime = prevTime + 0.01f;
        }

        float maxTime = float.MaxValue;
        if (index < m_nodeEntries.Count - 1
            && float.TryParse(m_nodeEntries[index + 1].TimeInput.text, out float nextTime))
        {
            maxTime = nextTime - 0.01f;
        }

        // 当前时间在有效区间内，无需调整
        if (currentTime >= minTime && currentTime <= maxTime)
        {
            return true;
        }

        // 自动微调：取前一个节点时间 + 0.01
        float adjustedTime = MathF.Round(minTime, 2);

        if (adjustedTime >= maxTime)
        {
            return false;
        }

        entry.TimeInput.text = adjustedTime.ToString("F2", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>
    /// 添加一个 BPM 节点到列表
    /// </summary>
    private void AddNode(float defaultTime, float defaultBpm, bool isFirstNode = false)
    {
        // 如果未显式指定 isFirstNode，由当前列表是否为空决定
        if (!isFirstNode)
        {
            isFirstNode = m_nodeEntries.Count == 0;
        }

        var entry = new BpmNodeEntry();

        // 节点行容器（垂直布局：时间行 + BPM行）
        entry.Root = CreateUIObject($"Node_{m_nodeEntries.Count}", m_contentContainer);
        var rowRect = entry.Root.GetComponent<RectTransform>();
        // 锚点拉伸至父级全宽，确保背景覆盖整个展示区域
        rowRect.anchorMin = new Vector2(0, 0.5f);
        rowRect.anchorMax = new Vector2(1, 0.5f);
        rowRect.sizeDelta = new Vector2(0, k_nodeHeight);

        // 必须设置 LayoutElement，否则 VerticalLayoutGroup 无法获取行高
        var rowLayoutElement = entry.Root.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = k_nodeHeight;
        rowLayoutElement.preferredHeight = k_nodeHeight;

        // 垂直布局（时间行在上，BPM行在下；子行撑满宽度以覆盖背景）
        var verticalLayout = entry.Root.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = true;
        verticalLayout.spacing = 6f;
        verticalLayout.padding = new RectOffset(10, 54, 10, 10);

        // 行背景 + 醒目标边框
        var rowBg = entry.Root.AddComponent<Image>();
        rowBg.color = new Color(0.3f, 0.35f, 0.45f, 1f);
        var rowOutline = entry.Root.AddComponent<Outline>();
        rowOutline.effectColor = new Color(0.6f, 0.75f, 0.9f, 1f);
        rowOutline.effectDistance = new Vector2(2f, 2f);

        // ---- 第一行：时间 ----
        var timeRow = CreateUIObject("TimeRow", entry.Root.transform);
        var timeRowLE = timeRow.AddComponent<LayoutElement>();
        timeRowLE.preferredHeight = 36f;

        var timeRowHLG = timeRow.AddComponent<HorizontalLayoutGroup>();
        timeRowHLG.childAlignment = TextAnchor.MiddleLeft;
        timeRowHLG.spacing = 1f;
        timeRowHLG.childControlWidth = true;
        timeRowHLG.childControlHeight = true;
        timeRowHLG.childForceExpandWidth = false;
        timeRowHLG.childForceExpandHeight = false;

        var timeLabel = CreateText("时间", timeRow.transform, 32);
        var timeLabelLayout = timeLabel.gameObject.AddComponent<LayoutElement>();
        timeLabelLayout.preferredWidth = 80f;

        var timeInputGo = CreateTMPInput("0.00", timeRow.transform, 32);
        var timeInputLayout = timeInputGo.AddComponent<LayoutElement>();
        timeInputLayout.preferredWidth = 150f;

        var timeOutline = timeInputGo.AddComponent<Outline>();
        timeOutline.effectColor = new Color(0.45f, 0.55f, 0.7f, 1f);
        timeOutline.effectDistance = new Vector2(1f, 1f);

        entry.TimeInput = timeInputGo.GetComponent<TMP_InputField>();

        // 时间输入框：聚焦时捕获快照，结束编辑时校验并记录撤回
        var capturedEntry = entry;
        entry.TimeInput.onSelect.AddListener(_ => m_bpmBeforeSnapshot = CaptureBpmNodes());
        entry.TimeInput.onEndEdit.AddListener((value) => HandleTimeInputEndEdit(capturedEntry, value));

        // ---- 第二行：BPM ----
        var bpmRow = CreateUIObject("BpmRow", entry.Root.transform);
        var bpmRowLE = bpmRow.AddComponent<LayoutElement>();
        bpmRowLE.preferredHeight = 36f;

        var bpmRowHLG = bpmRow.AddComponent<HorizontalLayoutGroup>();
        bpmRowHLG.childAlignment = TextAnchor.MiddleLeft;
        bpmRowHLG.spacing = 1f;
        bpmRowHLG.childControlWidth = true;
        bpmRowHLG.childControlHeight = true;
        bpmRowHLG.childForceExpandWidth = false;
        bpmRowHLG.childForceExpandHeight = false;

        var bpmLabel = CreateText("BPM", bpmRow.transform, 32);
        var bpmLabelLayout = bpmLabel.gameObject.AddComponent<LayoutElement>();
        bpmLabelLayout.preferredWidth = 80f;

        var bpmInputGo = CreateTMPInput("120", bpmRow.transform, 32);
        var bpmInputLayout = bpmInputGo.AddComponent<LayoutElement>();
        bpmInputLayout.preferredWidth = 150f;

        var bpmOutline = bpmInputGo.AddComponent<Outline>();
        bpmOutline.effectColor = new Color(0.45f, 0.55f, 0.7f, 1f);
        bpmOutline.effectDistance = new Vector2(1f, 1f);

        entry.BpmInput = bpmInputGo.GetComponent<TMP_InputField>();

        // BPM 修改后即时写入 .tmp 并记录撤回
        entry.BpmInput.onSelect.AddListener(_ => m_bpmBeforeSnapshot = CaptureBpmNodes());
        entry.BpmInput.onEndEdit.AddListener((value) => HandleBpmInputEndEdit());

        // ---- 移除按钮（绝对定位到右上角） ----
        var removeBtnGo = CreateUIObject("RemoveButton", entry.Root.transform);
        var removeBtnRect = removeBtnGo.GetComponent<RectTransform>();
        // 忽略布局组，防止 VerticalLayoutGroup 将其排入流中
        var removeBtnIgnore = removeBtnGo.AddComponent<LayoutElement>();
        removeBtnIgnore.ignoreLayout = true;
        removeBtnRect.anchorMin = new Vector2(1, 1);
        removeBtnRect.anchorMax = new Vector2(1, 1);
        removeBtnRect.pivot = new Vector2(1, 1);
        removeBtnRect.anchoredPosition = new Vector2(-8, -8);
        removeBtnRect.sizeDelta = new Vector2(30, 30);

        var removeBtnImg = removeBtnGo.AddComponent<Image>();
        removeBtnImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);

        // 删除按钮加浅色边框
        var removeBtnOutline = removeBtnGo.AddComponent<Outline>();
        removeBtnOutline.effectColor = new Color(0.7f, 0.5f, 0.5f, 1f);
        removeBtnOutline.effectDistance = new Vector2(1f, 1f);

        entry.RemoveButton = removeBtnGo.AddComponent<Button>();
        entry.RemoveButton.targetGraphic = removeBtnImg;
        entry.RemoveButton.interactable = true;

        var removeText = CreateText("X", removeBtnGo.transform, 16);
        removeText.raycastTarget = false;
        var removeTextRect = removeText.GetComponent<RectTransform>();
        removeTextRect.anchorMin = Vector2.zero;
        removeTextRect.anchorMax = Vector2.one;
        removeTextRect.offsetMin = Vector2.zero;
        removeTextRect.offsetMax = Vector2.zero;
        removeText.alignment = TextAlignmentOptions.Center;

        int capturedIndex = m_nodeEntries.Count;
        entry.RemoveButton.onClick.AddListener(() => HandleRemoveNodeClicked(capturedIndex));

        // 第一个节点不可删除
        if (isFirstNode)
        {
            entry.RemoveButton.interactable = false;
            removeBtnImg.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        }

        // 设置默认值
        entry.TimeInput.text = isFirstNode ? "0" : defaultTime.ToString("F2", CultureInfo.InvariantCulture);

        // BPM：若未指定则继承上一个节点的 BPM，无上一节点时默认 120
        if (defaultBpm < 0)
        {
            if (m_nodeEntries.Count > 0)
            {
                entry.BpmInput.text = m_nodeEntries[m_nodeEntries.Count - 1].BpmInput.text;
            }
            else
            {
                entry.BpmInput.text = "120";
            }
        }
        else
        {
            entry.BpmInput.text = defaultBpm.ToString("F0", CultureInfo.InvariantCulture);
        }

        // 约束输入类型
        entry.TimeInput.contentType = TMP_InputField.ContentType.DecimalNumber;
        entry.BpmInput.contentType = TMP_InputField.ContentType.DecimalNumber;

        m_nodeEntries.Add(entry);
    }

    /// <summary>
    /// 处理移除节点按钮（第一节点不可移除）
    /// </summary>
    private void HandleRemoveNodeClicked(int index)
    {
        if (index <= 0 || index >= m_nodeEntries.Count)
        {
            return;
        }

        var beforeSnapshot = CaptureBpmNodes();

        var entry = m_nodeEntries[index];
        m_nodeEntries.RemoveAt(index);
        Destroy(entry.Root);

        // 更新后续节点的监听回调索引
        RefreshRemoveListeners();

        // 即时写入 .tmp
        SaveBpmNodesToJson();

        // 记录到全局撤回/重做系统
        var afterSnapshot = CaptureBpmNodes();
        UndoRedoManager.Execute(
            undo: () => RestoreBpmNodes(beforeSnapshot),
            redo: () => RestoreBpmNodes(afterSnapshot));
    }

    /// <summary>
    /// 刷新所有移除按钮的监听器（在删除节点后调用）
    /// </summary>
    private void RefreshRemoveListeners()
    {
        for (int i = 1; i < m_nodeEntries.Count; i++)
        {
            int capturedIndex = i;
            m_nodeEntries[i].RemoveButton.onClick.RemoveAllListeners();
            m_nodeEntries[i].RemoveButton.onClick.AddListener(() => HandleRemoveNodeClicked(capturedIndex));
        }
    }

    /// <summary>
    /// 校验时间输入框编辑结果：不得重复、不得小于前一个节点、不得大于后一个节点
    /// </summary>
    private void HandleTimeInputEndEdit(BpmNodeEntry entry, string value)
    {
        // 校验全部节点（当前编辑的值已在 entry.TimeInput.text 中生效）
        var error = ValidateAllNodes();
        if (!string.IsNullOrEmpty(error))
        {
            // 回退：取前一个节点时间（第0个节点回退为0）
            float revertTime = 0f;
            int index = m_nodeEntries.IndexOf(entry);
            if (index > 0 && float.TryParse(m_nodeEntries[index - 1].TimeInput.text, out float prevTime))
            {
                revertTime = prevTime;
            }

            entry.TimeInput.text = revertTime.ToString("F2", CultureInfo.InvariantCulture);
            ShowWarningPopup(error);
            return;
        }

        // 校验通过，即时写入 .tmp
        SaveBpmNodesToJson();

        // 记录到全局撤回/重做系统
        var afterSnapshot = CaptureBpmNodes();
        var beforeSnapshot = m_bpmBeforeSnapshot ?? afterSnapshot;

        if (!SnapshotsEqual(beforeSnapshot, afterSnapshot))
        {
            UndoRedoManager.Execute(
                undo: () => RestoreBpmNodes(beforeSnapshot),
                redo: () => RestoreBpmNodes(afterSnapshot));
        }
    }

    /// <summary>
    /// BPM 输入框编辑结束：保存并记录撤回
    /// </summary>
    private void HandleBpmInputEndEdit()
    {
        SaveBpmNodesToJson();

        var afterSnapshot = CaptureBpmNodes();
        var beforeSnapshot = m_bpmBeforeSnapshot ?? afterSnapshot;

        if (!SnapshotsEqual(beforeSnapshot, afterSnapshot))
        {
            UndoRedoManager.Execute(
                undo: () => RestoreBpmNodes(beforeSnapshot),
                redo: () => RestoreBpmNodes(afterSnapshot));
        }
    }

    /// <summary>
    /// 全面校验所有节点：时间必须升序排列且互不重复
    /// </summary>
    private string ValidateAllNodes()
    {
        if (m_nodeEntries.Count == 0)
        {
            return null;
        }

        // 校验升序：每个节点时间 >= 前一个节点
        for (int i = 1; i < m_nodeEntries.Count; i++)
        {
            if (float.TryParse(m_nodeEntries[i - 1].TimeInput.text, out float prevTime)
                && float.TryParse(m_nodeEntries[i].TimeInput.text, out float curTime)
                && curTime < prevTime)
            {
                return $"时间 {curTime:F2} 小于前一个节点（{prevTime:F2}）";
            }
        }

        // 校验重复
        for (int i = 0; i < m_nodeEntries.Count; i++)
        {
            if (!float.TryParse(m_nodeEntries[i].TimeInput.text, out float timeA))
            {
                continue;
            }

            for (int j = i + 1; j < m_nodeEntries.Count; j++)
            {
                if (float.TryParse(m_nodeEntries[j].TimeInput.text, out float timeB)
                    && Mathf.Abs(timeA - timeB) < 0.001f)
                {
                    return $"时间 {timeA:F2} 冲突";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 显示一个自动消失的警告气泡（2秒后销毁）
    /// </summary>
    private void ShowWarningPopup(string message)
    {
        if (m_bpmPanel == null)
        {
            return;
        }

        var popupGo = CreateUIObject("WarningPopup", m_bpmPanel.transform);
        var popupRect = popupGo.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.sizeDelta = new Vector2(360, 60);

        var popupBg = popupGo.AddComponent<Image>();
        popupBg.color = new Color(0.8f, 0.2f, 0.2f, 0.95f);

        var popupText = CreateText(message, popupGo.transform, 16);
        var popupTextRect = popupText.GetComponent<RectTransform>();
        popupTextRect.anchorMin = Vector2.zero;
        popupTextRect.anchorMax = Vector2.one;
        popupTextRect.offsetMin = new Vector2(12, 4);
        popupTextRect.offsetMax = new Vector2(-12, -4);
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.color = Color.white;

        Destroy(popupGo, 2f);
    }

    /// <summary>
    /// 切换 FunctionChanger 下原始按钮的可见性（跳过自身和所有面板）
    /// </summary>
    private void SetOriginalButtonsActive(bool isActive)
    {
        foreach (Transform child in m_functionChanger.transform)
        {
            // 跳过自身和所有面板（面板由各自的返回按钮控制可见性）
            // 必须保持自身活跃，否则所有 onClick 回调失效！
            if (child.gameObject == gameObject || child.name.EndsWith("Panel"))
            {
                continue;
            }

            child.gameObject.SetActive(isActive);
        }
    }

    /// <summary>
    /// 获取当前音乐的播放时间（秒）
    /// </summary>
    private float GetCurrentMusicTime()
    {
        if (m_musicTimeSlider != null)
        {
            return (float)(m_musicTimeSlider.value * MusicTimeStampController.MusicTime);
        }

        return 0f;
    }

    /// <summary>
    /// 从 chart.tmp 加载已有的 BPM 节点并填充到面板中
    /// </summary>
    private void LoadBpmNodesFromJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath) || !File.Exists(tmpPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(tmpPath);
            var data = JsonUtility.FromJson<ChartJsonData>(json);

            if (data == null || data.bpmNodes == null || data.bpmNodes.Count == 0)
            {
                return;
            }

            // 清除当前所有节点后重建
            ClearAllNodes();

            // 写入静态缓存
            PopulateCacheFromNodes(data.bpmNodes);
            NotifyGridRefresh();

            for (int i = 0; i < data.bpmNodes.Count; i++)
            {
                var node = data.bpmNodes[i];
                AddNode(node.time, node.bpm, isFirstNode: i == 0);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 加载BPM节点失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将当前 BPM 节点合并写入 chart.tmp（保留 info、notes 等其他字段）
    /// </summary>
    private void SaveBpmNodesToJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath))
        {
            return;
        }

        try
        {
            // 读取现有 chart.tmp（保留 info、notes 等字段）
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

            // 只替换 bpmNodes 字段，time 保留两位小数
            data.bpmNodes = new List<BpmJsonNode>(m_nodeEntries.Count);
            foreach (var entry in m_nodeEntries)
            {
                if (float.TryParse(entry.TimeInput.text, out float time)
                    && float.TryParse(entry.BpmInput.text, out float bpm))
                {
                    data.bpmNodes.Add(new BpmJsonNode
                    {
                        // 保留解析原值，不再 2 位截断（浮点噪声由下方 Regex 统一清理）
                        time = time,
                        bpm = bpm
                    });
                }
                else
                {
                    Debug.LogWarning($"[{GetType().Name}] 跳过无效节点: time={entry.TimeInput?.text}, bpm={entry.BpmInput?.text}");
                }
            }

            var jsonStr = JsonUtility.ToJson(data);
            // 消除 IEEE 754 二进制近似噪声（如 0.01f → 0.009999999...）。
            // 用不区分区域设置的解析 + 保留 6 位小数：既清理浮点噪声，又不破坏
            // notes/cubes 中真实的 3~6 位精度数据（旧的 2 位四舍五入会静默改动谱面）。
            // 负向后顾 (?<!") 排除引号内文本（如谱面名 "v1.234567"），只处理 JSON 数字键值。
            jsonStr = Regex.Replace(jsonStr, @"(?<!"")\d+\.\d{3,}",
                m => Math.Round(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture), 6)
                    .ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
            Debug.Log($"[{GetType().Name}] SaveBpm → .tmp: nodes={data.bpmNodes.Count}");
            File.WriteAllText(tmpPath, jsonStr);

            // 更新静态缓存，供 GridManager 查询
            PopulateCacheFromNodes(data.bpmNodes);
            NotifyGridRefresh();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 保存BPM节点失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取 chart.tmp 路径（编辑期间的临时工作副本，所有读写操作基于此文件）
    /// </summary>
    private string GetTmpJsonPath()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath))
        {
            return null;
        }

        return Path.Combine(EditorInit.ChartPath, "chart.tmp");
    }

    /// <summary>
    /// 获取 chart.json 路径（最终持久化文件，仅在 Save 按钮点击时覆写）
    /// </summary>
    private string GetFinalJsonPath()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath))
        {
            return null;
        }

        return Path.Combine(EditorInit.ChartPath, "chart.json");
    }

    /// <summary>
    /// 清除所有已有节点（用于重新加载）
    /// </summary>
    private void ClearAllNodes()
    {
        for (int i = m_nodeEntries.Count - 1; i >= 0; i--)
        {
            Destroy(m_nodeEntries[i].Root);
        }

        m_nodeEntries.Clear();
    }

    #region 撤回/重做快照

    /// <summary>
    /// 捕获当前所有 BPM 节点为快照
    /// </summary>
    private List<(float time, float bpm)> CaptureBpmNodes()
    {
        var nodes = new List<(float, float)>(m_nodeEntries.Count);
        foreach (var entry in m_nodeEntries)
        {
            if (float.TryParse(entry.TimeInput.text, out float time)
                && float.TryParse(entry.BpmInput.text, out float bpm))
            {
                nodes.Add((time, bpm));
            }
        }
        return nodes;
    }

    /// <summary>
    /// 从快照恢复所有 BPM 节点（清除现有节点后重建）
    /// </summary>
    private void RestoreBpmNodes(List<(float time, float bpm)> nodes)
    {
        ClearAllNodes();

        for (int i = 0; i < nodes.Count; i++)
        {
            AddNode(nodes[i].time, nodes[i].bpm, isFirstNode: i == 0);
            // 覆盖文本以确保精确还原快照值
            m_nodeEntries[i].TimeInput.text = nodes[i].time.ToString("F2", CultureInfo.InvariantCulture);
            m_nodeEntries[i].BpmInput.text = nodes[i].bpm.ToString("F2", CultureInfo.InvariantCulture);
        }

        SaveBpmNodesToJson();

        if (m_contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_contentRect);
            Canvas.ForceUpdateCanvases();
        }
    }

    /// <summary>
    /// 比较两个 BPM 快照是否一致
    /// </summary>
    private static bool SnapshotsEqual(List<(float time, float bpm)> a, List<(float time, float bpm)> b)
    {
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (Mathf.Abs(a[i].time - b[i].time) > 0.001f) return false;
            if (Mathf.Abs(a[i].bpm - b[i].bpm) > 0.001f) return false;
        }
        return true;
    }

    #endregion

    #region UI 工具方法

    /// <summary>
    /// 加载中文字体（Resources/Fonts/black.ttf），创建动态 TMP 字体资产。
    /// 默认 TMP 字体不含中文字形，需用 black.ttf 动态生成才能正常显示中文。
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
        else
        {
            // 预填充常用字符到动态 atlas，确保光标可渲染
            m_chineseFont.TryAddCharacters(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .");
        }

        return m_chineseFont;
    }

    /// <summary>
    /// 创建一个空 UI GameObject 并附带 RectTransform
    /// </summary>
    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerConstants.Ui;
        return go;
    }

    /// <summary>
    /// 创建一个 TMP 文本对象
    /// </summary>
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

    /// <summary>
    /// 创建一个 TMP 输入框
    /// </summary>
    private GameObject CreateTMPInput(string defaultText, Transform parent, float fontSize)
    {
        var go = CreateUIObject("InputField", parent);

        var inputRect = go.GetComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(180, 36);

        // 背景
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // 输入框
        var input = go.AddComponent<TMP_InputField>();

        // 文字区域
        var textArea = CreateUIObject("TextArea", go.transform);
        var textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(2, 2);
        textAreaRect.offsetMax = new Vector2(-4, -2);

        var textComp = textArea.AddComponent<TextMeshProUGUI>();
        textComp.fontSize = fontSize;
        textComp.color = Color.white;
        textComp.text = defaultText;
        textComp.font = GetChineseFont();

        // Placeholder
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

        input.textViewport = textAreaRect;
        input.textComponent = textComp;
        input.placeholder = placeholderText;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;

        return go;
    }

    #endregion
}
