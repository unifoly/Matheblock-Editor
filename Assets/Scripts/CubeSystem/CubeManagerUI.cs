using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 方体管理面板 UI：点击「方体管理」按钮后弹出面板，支持创建/选中/删除方体。
/// 同时绑定 UpperList 中的 CubeID 输入框、Surface 面选择按钮、Side 方向选择按钮。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class CubeManagerUI : MonoBehaviour
{
    private const float k_nodeHeight = 100f;
    private const float k_nodeSpacing = 8f;
    private const float k_backButtonWidth = 70f;
    private const float k_backButtonHeight = 44f;
    private const float k_addButtonHeight = 50f;
    private const float k_titleHeight = 50f;
    private const float k_padding = 12f;

    // ---- 场景引用 ----
    private GameObject m_functionChanger;
    private Button m_cubeButton;
    private GameObject m_cubePanel;
    private CubeManager m_cubeManager;
    private Transform m_contentContainer;
    private RectTransform m_contentRect;
    private TMP_FontAsset m_chineseFont;

    // ---- 快捷选择引用 ----
    private TMP_InputField m_cubeIdInput;
    private NotePlacementManager m_notePlacementManager;
    private readonly Dictionary<string, Button> m_surfaceButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Button> m_sideButtons = new Dictionary<string, Button>();

    // ---- 按钮高亮 ----
    // 选中按钮的黄色高亮
    private static readonly Color k_buttonActiveColor = new Color(1f, 0.92f, 0.3f, 1f);
    // 未选中按钮的默认颜色
    private static readonly Color k_buttonDefaultColor = Color.white;

    // ---- 列表条目 ----
    private readonly List<CubeListEntry> m_entries = new List<CubeListEntry>();

    private class CubeListEntry
    {
        public GameObject Root;
        public TextMeshProUGUI IdLabel;
        public TMP_InputField NoteInput;
        public Button SelectButton;
        public Button RemoveButton;
        public Image Background;
        public int CubeId;
    }

    private void Awake()
    {
        m_functionChanger = transform.parent.gameObject;
        m_cubeButton = GetComponent<Button>();

        // 清理可能残留的旧面板
        var oldPanel = m_functionChanger.transform.Find("CubePanel");
        if (oldPanel != null)
        {
            Destroy(oldPanel.gameObject);
        }
    }

    private void Start()
    {
        // 查找场景中的 CubeManager 组件（位于 CubeSystem GameObject 上）
        m_cubeManager = FindObjectOfType<CubeManager>();
        if (m_cubeManager == null)
        {
            Debug.LogError($"[{GetType().Name}] 未找到 CubeManager 组件！请确认场景中存在 CubeSystem GameObject");
            return;
        }

        // 绑定快捷选择控件
        BindQuickSelectionControls();
    }

    private void OnEnable()
    {
        if (m_cubeButton != null)
        {
            m_cubeButton.onClick.RemoveListener(HandleCubeButtonClicked);
            m_cubeButton.onClick.AddListener(HandleCubeButtonClicked);
        }

        // 失活→激活后恢复 CubeManager 事件订阅（面板已构建时列表才能继续自动刷新）
        if (m_cubePanel != null && m_cubeManager != null)
        {
            m_cubeManager.CubeCreated -= OnCubeCreated;
            m_cubeManager.CubeDeleted -= OnCubeDeleted;
            m_cubeManager.ActiveCubeChanged -= OnActiveCubeChanged;
            m_cubeManager.CubeCreated += OnCubeCreated;
            m_cubeManager.CubeDeleted += OnCubeDeleted;
            m_cubeManager.ActiveCubeChanged += OnActiveCubeChanged;
        }
    }

    private void OnDisable()
    {
        if (m_cubeButton != null)
        {
            m_cubeButton.onClick.RemoveListener(HandleCubeButtonClicked);
        }

        if (m_cubeManager != null)
        {
            m_cubeManager.CubeCreated -= OnCubeCreated;
            m_cubeManager.CubeDeleted -= OnCubeDeleted;
            m_cubeManager.ActiveCubeChanged -= OnActiveCubeChanged;
        }
    }

    /// <summary>
    /// 应用退出前保存当前轨道的 Note，避免放置后未切换轨道导致数据丢失
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveCurrentNotesToCubeTrack();
    }

    /// <summary>
    /// 绑定 UpperList 中的快捷选择控件：CubeID 输入框、Surface 面按钮、Side 方向按钮
    /// </summary>
    private void BindQuickSelectionControls()
    {
        // CubeID 输入框（位于 UpperList > TmpDataChanger > CubeID > Input）
        var cubeIdObj = GameObject.Find("CubeID");
        if (cubeIdObj != null)
        {
            m_cubeIdInput = cubeIdObj.GetComponentInChildren<TMP_InputField>();
            if (m_cubeIdInput != null)
            {
                m_cubeIdInput.onEndEdit.RemoveListener(HandleCubeIdInputEndEdit);
                m_cubeIdInput.onEndEdit.AddListener(HandleCubeIdInputEndEdit);

                // 默认值为 1（首个方体 ID 通常为 1）
                if (string.IsNullOrWhiteSpace(m_cubeIdInput.text))
                {
                    m_cubeIdInput.text = "1";
                }

                Debug.Log($"[{GetType().Name}] CubeID 输入框绑定成功");
            }
        }

        // Surface 面选择按钮（6个面：Up/Down/Left/Right/Front/Back）
        var surfaceObj = GameObject.Find("Surface");
        if (surfaceObj != null)
        {
            BindDirectionButtons(surfaceObj, m_surfaceButtons, HandleSurfaceButtonClicked);
            Debug.Log($"[{GetType().Name}] Surface 按钮绑定: {m_surfaceButtons.Count} 个");
        }

        // Side 方向选择按钮（4个方向：Up/Down/Left/Right）
        var sideObj = GameObject.Find("Side");
        if (sideObj != null)
        {
            BindDirectionButtons(sideObj, m_sideButtons, HandleSideButtonClicked);
            Debug.Log($"[{GetType().Name}] Side 按钮绑定: {m_sideButtons.Count} 个");
        }

        // NotePlacementManager（用于切换轨道组时重新加载 Note）
        m_notePlacementManager = FindObjectOfType<NotePlacementManager>();

        // 初始化按钮高亮：默认选中 Front 面 + Up 方向
        UpdateSurfaceButtonHighlight(m_cubeManager.ActiveFace.ToString());
        UpdateSideButtonHighlight(m_cubeManager.ActiveDirection.ToString());

        // 初始加载当前活跃方体的轨道（首次无需要保存）
        LoadActiveTrackNotes();
    }

    /// <summary>
    /// 绑定容器下的所有按钮，按名称映射
    /// </summary>
    private void BindDirectionButtons(GameObject container, Dictionary<string, Button> dict, Action<string> callback)
    {
        foreach (Transform child in container.transform)
        {
            var btn = child.GetComponent<Button>();
            if (btn != null)
            {
                string name = child.gameObject.name;
                dict[name] = btn;
                // 注意：lambda 无法退订，BindDirectionButtons 仅在 Start 调用一次，故无需 Remove
                btn.onClick.AddListener(() => callback(name));
            }
        }
    }

    // ---- 事件处理 ----

    /// <summary>
    /// 点击「方体管理」按钮：首次点击创建面板，后续点击显示面板
    /// </summary>
    private void HandleCubeButtonClicked()
    {
        SetOriginalButtonsActive(false);

        if (m_cubePanel == null)
        {
            BuildCubePanel();
            RefreshCubeList();

            // 订阅 CubeManager 事件，数据变化时自动刷新列表
            if (m_cubeManager != null)
            {
                m_cubeManager.CubeCreated += OnCubeCreated;
                m_cubeManager.CubeDeleted += OnCubeDeleted;
                m_cubeManager.ActiveCubeChanged += OnActiveCubeChanged;
            }
        }

        m_cubePanel.SetActive(true);
        m_cubePanel.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 点击返回键：隐藏面板，还原原始按钮
    /// </summary>
    private void HandleBackButtonClicked()
    {
        if (m_cubePanel != null)
        {
            m_cubePanel.SetActive(false);
        }

        SetOriginalButtonsActive(true);
    }

    /// <summary>
    /// 点击「创建方体」按钮
    /// </summary>
    private void HandleCreateCubeClicked()
    {
        if (m_cubeManager != null)
        {
            SaveCurrentNotesToCubeTrack();
            m_cubeManager.CreateCube();
        }
    }

    /// <summary>
    /// CubeManager 创建方体后自动刷新列表
    /// </summary>
    private void OnCubeCreated(CubeData cube)
    {
        RefreshCubeList();
    }

    /// <summary>
    /// CubeManager 删除方体后自动刷新列表
    /// </summary>
    private void OnCubeDeleted(int cubeId)
    {
        RefreshCubeList();
        LoadActiveTrackNotes();
    }

    /// <summary>
    /// 活跃方体切换后更新高亮
    /// </summary>
    private void OnActiveCubeChanged(int cubeId)
    {
        UpdateSelectionHighlight();
        LoadActiveTrackNotes();
    }

    /// <summary>
    /// CubeID 输入框回车：按 ID 选中方体
    /// </summary>
    private void HandleCubeIdInputEndEdit(string value)
    {
        if (m_cubeManager == null || string.IsNullOrEmpty(value)) return;

        if (int.TryParse(value, out int cubeId))
        {
            var cube = m_cubeManager.GetCube(cubeId);
            if (cube != null)
            {
                SaveCurrentNotesToCubeTrack();
                m_cubeManager.SetActiveCube(cubeId);
                Debug.Log($"[{GetType().Name}] 快捷选中方体: ID={cubeId}");
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] 方体不存在: ID={cubeId}");
            }
        }
    }

    /// <summary>
    /// 将当前 NotePlacementManager 中的 Note 保存到当前方体的活跃轨道（切换前调用）
    /// 每个轨道（face + direction 组合）独立存储，保存实际 lane 值。
    /// </summary>
    private void SaveCurrentNotesToCubeTrack()
    {
        if (m_cubeManager == null || m_notePlacementManager == null) return;

        var cube = m_cubeManager.GetCube(m_cubeManager.ActiveCubeId);
        if (cube == null) return;

        CubeFace face = m_cubeManager.ActiveFace;
        FaceDirection dir = m_cubeManager.ActiveDirection;
        var currentNotes = m_notePlacementManager.GetCurrentNotes();

        // 仅清空并写入当前 (face, direction) 对应的单个轨道
        var track = cube.GetTrack(face, dir);
        if (track == null) return;

        track.notes.Clear();
        foreach (var note in currentNotes)
        {
            if (note.lane < 0) continue;

            track.notes.Add(new CubeNoteData
            {
                type = note.type,
                lane = note.lane,
                time = note.time,
                endTime = note.endTime
            });
        }

        m_cubeManager.SaveCubesToJson();
    }

    /// <summary>
    /// 从当前方体的活跃轨道加载 Note 到左侧显示（切换后调用）
    /// 仅读取当前 (face, direction) 对应的单个轨道。
    /// </summary>
    private void LoadActiveTrackNotes()
    {
        if (m_cubeManager == null || m_notePlacementManager == null) return;

        var cube = m_cubeManager.GetCube(m_cubeManager.ActiveCubeId);
        if (cube == null) return;

        CubeFace face = m_cubeManager.ActiveFace;
        FaceDirection dir = m_cubeManager.ActiveDirection;

        // 读取当前轨道的 Note，使用保存时的实际 lane 值
        var noteList = new List<NoteJsonNode>();
        var track = cube.GetTrack(face, dir);
        if (track?.notes != null)
        {
            foreach (var note in track.notes)
            {
                noteList.Add(new NoteJsonNode
                {
                    type = note.type,
                    lane = note.lane,
                    time = note.time,
                    endTime = note.endTime
                });
            }
        }

        m_notePlacementManager.ReloadNotes(noteList);
        Debug.Log($"[{GetType().Name}] 加载轨道: Cube#{cube.cubeId} {face}_{dir}，{noteList.Count} 个 Note");
    }

    /// <summary>
    /// Surface 面按钮点击：选中对应的面，刷新左侧轨道
    /// </summary>
    private void HandleSurfaceButtonClicked(string faceName)
    {
        if (m_cubeManager == null) return;

        if (Enum.TryParse<CubeFace>(faceName, out CubeFace face))
        {
            // 同一组内只能选中一个，切换前保存当前轨道
            SaveCurrentNotesToCubeTrack();
            m_cubeManager.SetActiveTrack(face, m_cubeManager.ActiveDirection);
            UpdateSurfaceButtonHighlight(faceName);
            LoadActiveTrackNotes();
            Debug.Log($"[{GetType().Name}] 快捷选中面: {faceName}");
        }
    }

    /// <summary>
    /// Side 方向按钮点击：选中对应的方向，刷新左侧轨道
    /// </summary>
    private void HandleSideButtonClicked(string dirName)
    {
        if (m_cubeManager == null) return;

        if (Enum.TryParse<FaceDirection>(dirName, out FaceDirection direction))
        {
            // 同一组内只能选中一个，切换前保存当前轨道
            SaveCurrentNotesToCubeTrack();
            m_cubeManager.SetActiveTrack(m_cubeManager.ActiveFace, direction);
            UpdateSideButtonHighlight(dirName);
            LoadActiveTrackNotes();
            Debug.Log($"[{GetType().Name}] 快捷选中方向: {dirName}");
        }
    }

    /// <summary>
    /// 更新 Surface 按钮组高亮：仅选中面标黄
    /// </summary>
    private void UpdateSurfaceButtonHighlight(string activeName)
    {
        foreach (var kvp in m_surfaceButtons)
        {
            var img = kvp.Value.GetComponent<Image>();
            if (img != null)
            {
                img.color = (kvp.Key == activeName) ? k_buttonActiveColor : k_buttonDefaultColor;
            }
        }
    }

    /// <summary>
    /// 更新 Side 按钮组高亮：仅选中方向标黄
    /// </summary>
    private void UpdateSideButtonHighlight(string activeName)
    {
        foreach (var kvp in m_sideButtons)
        {
            var img = kvp.Value.GetComponent<Image>();
            if (img != null)
            {
                img.color = (kvp.Key == activeName) ? k_buttonActiveColor : k_buttonDefaultColor;
            }
        }
    }

    // ---- 面板构建 ----

    /// <summary>
    /// 构建方体管理面板 UI（仅执行一次，由首次点击触发）
    /// </summary>
    private void BuildCubePanel()
    {
        if (m_cubePanel != null) return;

        var existing = m_functionChanger.transform.Find("CubePanel");
        if (existing != null)
        {
            m_cubePanel = existing.gameObject;
            return;
        }

        // 面板根节点
        m_cubePanel = CreateUIObject("CubePanel", m_functionChanger.transform);
        var panelRect = m_cubePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelBg = m_cubePanel.AddComponent<Image>();
        panelBg.color = new Color(0.235f, 0.235f, 0.235f, 1f);
        panelBg.raycastTarget = false;

        BuildTopBar();
        BuildScrollView();
        BuildAddButton();

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_contentRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 构建顶部标题栏和返回键
    /// </summary>
    private void BuildTopBar()
    {
        var topBar = CreateUIObject("TopBar", m_cubePanel.transform);
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

        // 标题文字
        var titleText = CreateText("方体管理", topBar.transform, 28);
        titleText.raycastTarget = false;
        var titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(200, k_titleHeight);
        titleText.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>
    /// 构建可滚动方体列表
    /// </summary>
    private void BuildScrollView()
    {
        var scrollGo = CreateUIObject("ScrollView", m_cubePanel.transform);
        var scrollRectTrans = scrollGo.GetComponent<RectTransform>();
        scrollRectTrans.anchorMin = new Vector2(0, 0);
        scrollRectTrans.anchorMax = new Vector2(1, 1);
        scrollRectTrans.offsetMin = new Vector2(k_padding, k_addButtonHeight + k_padding + k_nodeSpacing);
        scrollRectTrans.offsetMax = new Vector2(-k_padding, -(k_titleHeight + k_padding * 2));

        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        scrollImg.raycastTarget = false;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // Viewport
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

        // Content
        var contentGo = CreateUIObject("Content", viewportGo.transform);
        m_contentRect = contentGo.GetComponent<RectTransform>();
        m_contentRect.anchorMin = new Vector2(0, 1);
        m_contentRect.anchorMax = new Vector2(1, 1);
        m_contentRect.pivot = new Vector2(0.5f, 1f);
        m_contentRect.anchoredPosition = Vector2.zero;
        m_contentRect.sizeDelta = new Vector2(0, 0);

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
    /// 构建「创建方体」按钮
    /// </summary>
    private void BuildAddButton()
    {
        var addBtnGo = CreateUIObject("AddButton", m_cubePanel.transform);
        var addBtnRect = addBtnGo.GetComponent<RectTransform>();
        addBtnRect.anchorMin = new Vector2(0.5f, 0);
        addBtnRect.anchorMax = new Vector2(0.5f, 0);
        addBtnRect.pivot = new Vector2(0.5f, 0);
        addBtnRect.anchoredPosition = new Vector2(0, k_padding);
        addBtnRect.sizeDelta = new Vector2(320, k_addButtonHeight);

        var addBtnImg = addBtnGo.AddComponent<Image>();
        addBtnImg.color = new Color(0.3f, 0.5f, 0.3f, 1f);

        var addButton = addBtnGo.AddComponent<Button>();
        addButton.targetGraphic = addBtnImg;
        addButton.onClick.AddListener(HandleCreateCubeClicked);

        var addText = CreateText("+ 创建方体", addBtnGo.transform, 24);
        addText.raycastTarget = false;
        var addTextRect = addText.GetComponent<RectTransform>();
        addTextRect.anchorMin = Vector2.zero;
        addTextRect.anchorMax = Vector2.one;
        addTextRect.offsetMin = Vector2.zero;
        addTextRect.offsetMax = Vector2.zero;
        addText.alignment = TextAlignmentOptions.Center;

        addBtnGo.transform.SetAsLastSibling();
    }

    // ---- 列表刷新 ----

    /// <summary>
    /// 清除并重建方体列表
    /// </summary>
    private void RefreshCubeList()
    {
        if (m_cubeManager == null || m_contentContainer == null) return;

        // 清除旧条目
        foreach (var entry in m_entries)
        {
            if (entry.Root != null)
            {
                Destroy(entry.Root);
            }
        }
        m_entries.Clear();

        // 为每个方体创建列表条目
        foreach (var cube in m_cubeManager.Cubes)
        {
            AddCubeListEntry(cube);
        }

        // 更新高亮
        UpdateSelectionHighlight();

        if (m_contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_contentRect);
            Canvas.ForceUpdateCanvases();
        }
    }

    /// <summary>
    /// 添加一个方体列表条目（ID 标签 + 备注输入框 + 选中/删除按钮）
    /// </summary>
    private void AddCubeListEntry(CubeData cubeData)
    {
        var entry = new CubeListEntry { CubeId = cubeData.cubeId };

        // 行容器
        entry.Root = CreateUIObject($"CubeEntry_{cubeData.cubeId}", m_contentContainer);
        var rowRect = entry.Root.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 0.5f);
        rowRect.anchorMax = new Vector2(1, 0.5f);
        rowRect.sizeDelta = new Vector2(0, k_nodeHeight);

        var rowLayout = entry.Root.AddComponent<LayoutElement>();
        rowLayout.minHeight = k_nodeHeight;
        rowLayout.preferredHeight = k_nodeHeight;

        var rowHLG = entry.Root.AddComponent<HorizontalLayoutGroup>();
        rowHLG.childAlignment = TextAnchor.MiddleLeft;
        rowHLG.spacing = 8f;
        rowHLG.childControlWidth = true;
        rowHLG.childControlHeight = true;
        rowHLG.childForceExpandWidth = false;
        rowHLG.childForceExpandHeight = true;
        rowHLG.padding = new RectOffset(10, 10, 0, 0);

        entry.Background = entry.Root.AddComponent<Image>();
        entry.Background.color = new Color(0.3f, 0.35f, 0.45f, 1f);

        var outline = entry.Root.AddComponent<Outline>();
        outline.effectColor = new Color(0.6f, 0.75f, 0.9f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);

        // ID 标签
        entry.IdLabel = CreateText($"#{cubeData.cubeId}", entry.Root.transform, 26);
        var idLayout = entry.IdLabel.gameObject.AddComponent<LayoutElement>();
        idLayout.preferredWidth = 50f;
        idLayout.minHeight = 50f;

        // 备注输入框
        var noteInputGo = CreateTMPInput(cubeData.cubeNote, entry.Root.transform, 22);
        var noteLayout = noteInputGo.AddComponent<LayoutElement>();
        noteLayout.preferredWidth = 200f;
        noteLayout.flexibleWidth = 1;
        noteLayout.minHeight = 50f;

        entry.NoteInput = noteInputGo.GetComponent<TMP_InputField>();
        entry.NoteInput.contentType = TMP_InputField.ContentType.Standard;

        int capturedId = cubeData.cubeId;
        entry.NoteInput.onEndEdit.AddListener((value) => HandleNoteInputEndEdit(capturedId, value));

        // 选中按钮
        var selectBtnGo = CreateUIObject("SelectButton", entry.Root.transform);
        var selectLayout = selectBtnGo.AddComponent<LayoutElement>();
        selectLayout.preferredWidth = 90f;
        selectLayout.minHeight = 50f;

        var selectImg = selectBtnGo.AddComponent<Image>();
        selectImg.color = new Color(0.2f, 0.4f, 0.6f, 1f);

        entry.SelectButton = selectBtnGo.AddComponent<Button>();
        entry.SelectButton.targetGraphic = selectImg;

        var selectText = CreateText("选中", selectBtnGo.transform, 22);
        selectText.raycastTarget = false;
        var selectTextRect = selectText.GetComponent<RectTransform>();
        selectTextRect.anchorMin = Vector2.zero;
        selectTextRect.anchorMax = Vector2.one;
        selectTextRect.offsetMin = Vector2.zero;
        selectTextRect.offsetMax = Vector2.zero;
        selectText.alignment = TextAlignmentOptions.Center;

        entry.SelectButton.onClick.AddListener(() => HandleSelectCube(capturedId));

        // 删除按钮（仅剩1个方体时禁用）
        var removeBtnGo = CreateUIObject("RemoveButton", entry.Root.transform);
        var removeLayout = removeBtnGo.AddComponent<LayoutElement>();
        removeLayout.preferredWidth = 60f;
        removeLayout.minHeight = 50f;

        var removeImg = removeBtnGo.AddComponent<Image>();
        removeImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);

        entry.RemoveButton = removeBtnGo.AddComponent<Button>();
        entry.RemoveButton.targetGraphic = removeImg;

        var removeText = CreateText("X", removeBtnGo.transform, 24);
        removeText.raycastTarget = false;
        var removeTextRect = removeText.GetComponent<RectTransform>();
        removeTextRect.anchorMin = Vector2.zero;
        removeTextRect.anchorMax = Vector2.one;
        removeTextRect.offsetMin = Vector2.zero;
        removeTextRect.offsetMax = Vector2.zero;
        removeText.alignment = TextAlignmentOptions.Center;

        entry.RemoveButton.onClick.AddListener(() => HandleDeleteCube(capturedId));

        // 仅剩1个方体时禁用删除按钮
        if (m_cubeManager.Cubes.Count <= 1)
        {
            entry.RemoveButton.interactable = false;
            removeImg.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        }

        m_entries.Add(entry);
    }

    /// <summary>
    /// 选中指定方体（高亮由 ActiveCubeChanged 事件自动更新）
    /// </summary>
    private void HandleSelectCube(int cubeId)
    {
        if (m_cubeManager != null)
        {
            SaveCurrentNotesToCubeTrack();
            m_cubeManager.SetActiveCube(cubeId);
        }
    }

    /// <summary>
    /// 删除指定方体
    /// </summary>
    private void HandleDeleteCube(int cubeId)
    {
        if (m_cubeManager != null)
        {
            m_cubeManager.DeleteCube(cubeId);
        }
    }

    /// <summary>
    /// 备注输入框编辑结束：保存备注到 CubeData 并持久化
    /// </summary>
    private void HandleNoteInputEndEdit(int cubeId, string value)
    {
        if (m_cubeManager == null) return;

        var cube = m_cubeManager.GetCube(cubeId);
        if (cube != null)
        {
            cube.cubeNote = value;
            m_cubeManager.SaveCubesToJson();
            Debug.Log($"[{GetType().Name}] 方体 #{cubeId} 备注已更新: {value}");
        }
    }

    /// <summary>
    /// 更新列表中选中项的高亮显示
    /// </summary>
    private void UpdateSelectionHighlight()
    {
        if (m_cubeManager == null) return;

        foreach (var entry in m_entries)
        {
            bool isActive = entry.CubeId == m_cubeManager.ActiveCubeId;
            if (entry.Background != null)
            {
                entry.Background.color = isActive
                    ? new Color(0.4f, 0.6f, 0.4f, 1f)
                    : new Color(0.3f, 0.35f, 0.45f, 1f);
            }
        }
    }

    // ---- 辅助方法 ----

    /// <summary>
    /// 切换 FunctionChanger 下原始按钮的可见性（跳过自身和所有面板）
    /// </summary>
    private void SetOriginalButtonsActive(bool isActive)
    {
        foreach (Transform child in m_functionChanger.transform)
        {
            if (child.gameObject == gameObject || child.name.EndsWith("Panel"))
            {
                continue;
            }

            child.gameObject.SetActive(isActive);
        }
    }

    /// <summary>
    /// 加载中文字体
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
        if (m_chineseFont != null)
        {
            m_chineseFont.TryAddCharacters(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .方体管理创建选中删除#备注");
        }

        return m_chineseFont;
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

    /// <summary>
    /// 创建一个 TMP 输入框（带背景、文字区域、Placeholder）
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
        textAreaRect.offsetMin = new Vector2(4, 2);
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
        placeholderText.text = "备注...";
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.font = GetChineseFont();

        input.textViewport = textAreaRect;
        input.textComponent = textComp;
        input.placeholder = placeholderText;
        input.contentType = TMP_InputField.ContentType.Standard;

        return go;
    }
}