using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HexMap;

/// <summary>
/// Note JSON 节点，供 NotePlacementManager 和 BpmManagerUI 共同使用，
/// 确保 BPM 保存时不会丢失 notes 字段。
/// </summary>
[Serializable]
public class NoteJsonNode
{
    public string type;
    public int lane;
    public float time;
}

/// <summary>
/// 左半 Note 区放置管理器：鼠标悬停在格点上时按 Q/E/R 放置对应类型的 Note。
/// 已放置的 Note 固定在放置位置，不随网格线移动。Note 数据持久化到 chart.tmp。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class NotePlacementManager : MonoBehaviour
{
    // Note 类型枚举：Hold 暂不实现
    public enum NoteType
    {
        Click,
        Flick,
        Drag,
        ReverseFlick
    }

    [Header("Note 图片")]
    [Tooltip("Click 类型图片（快捷键 Q）")]
    [SerializeField] private Sprite m_clickSprite;
    [Tooltip("Flick 类型图片（快捷键 E）")]
    [SerializeField] private Sprite m_flickSprite;
    [Tooltip("Drag 类型图片（快捷键 R）")]
    [SerializeField] private Sprite m_dragSprite;

    [Header("Note 显示")]
    [SerializeField] private float m_noteSize = 80f;
    [Tooltip("悬停指示器颜色")]
    [SerializeField] private Color m_hoverColor = new Color(1f, 1f, 1f, 0.3f);
    [Tooltip("未指定图片时的回退颜色")]
    [SerializeField] private Color m_fallbackColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    // Note 快捷键的 Action 名（与 SettingsPageBuilder 中一致，用于 KeyBindingsStore 持久化）
    private const string k_actionClick = "Note_Click";
    private const string k_actionFlick = "Note_Flick";
    private const string k_actionDrag = "Note_Drag";
    private const string k_actionReverseFlick = "Note_ReverseFlick";

    // 运行时从 KeyBindingsStore 加载的快捷键映射（支持组合键）
    private List<(KeyCombo combo, NoteType type)> m_hotkeyList;

    private GridManager m_gridManager;
    private RectTransform m_playScreenRect;

    // Note 渲染层：独立于 GridContainer，确保 Note 始终渲染在网格线之上
    private RectTransform m_noteLayer;

    // 所有已放置的 Note 列表
    private readonly List<NoteEntry> m_notes = new List<NoteEntry>();

    // Note 视觉对象池（复用，避免频繁创建销毁）
    private readonly List<GameObject> m_noteViewPool = new List<GameObject>();

    // 悬停状态
    private GameObject m_hoverIndicator;
    private bool m_isHovering;
    private int m_hoveredLane;
    private float m_hoveredTime;

    // 标记是否已从 JSON 加载过 Note（仅加载一次）
    private bool m_notesLoaded;

    // Note 数据结构
    private class NoteEntry
    {
        public NoteType Type;
        public int Lane;
        public float Time;
        public GameObject View;
        // 放置时缓存的本地 X 坐标，改变竖线数量时不重新计算
        public float CachedLocalX;
    }

    // ---- JSON 序列化用类（与 BpmManagerUI 保持字段一致，额外增加 notes）----

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

    private void Start()
    {
        m_playScreenRect = GetComponent<RectTransform>();
        CacheGridManager();
        LoadHotkeys();
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    /// <summary>
    /// Setting 场景关闭后重新加载快捷键（用户可能修改了绑定）
    /// </summary>
    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == "Setting")
        {
            LoadHotkeys();
        }
    }

    /// <summary>
    /// 从 KeyBindingsStore 加载快捷键绑定，解析为 KeyCombo（支持组合键）
    /// </summary>
    private void LoadHotkeys()
    {
        m_hotkeyList = new List<(KeyCombo, NoteType)>();

        TryAddHotkey(k_actionClick, "Q", NoteType.Click);
        TryAddHotkey(k_actionFlick, "R", NoteType.Flick);
        TryAddHotkey(k_actionDrag, "E", NoteType.Drag);
        TryAddHotkey(k_actionReverseFlick, "T", NoteType.ReverseFlick);
    }

    private void TryAddHotkey(string actionName, string defaultKey, NoteType type)
    {
        // 使用 KeyCombo 替代单 KeyCode，支持组合键
        KeyCombo defaultCombo = KeyCombo.Parse(defaultKey);
        KeyCombo combo = KeyBindingsStore.GetKeyCombo(actionName, defaultCombo);

        if (combo.IsValid)
        {
            m_hotkeyList.Add((combo, type));
        }
        else
        {
            Debug.LogWarning($"[NotePlacementManager] 无法解析快捷键: {combo.ToDisplayString()} (action={actionName})，使用默认 {defaultKey}");
            m_hotkeyList.Add((defaultCombo, type));
        }
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
        if (m_gridManager == null) return;

        EnsureNoteLayer();
        EnsureNoteLayerOnTop();

        // NoteLayer 就绪后加载一次已保存的 Note
        if (!m_notesLoaded && m_noteLayer != null)
        {
            m_notesLoaded = true;
            LoadNotesFromJson();
        }

        UpdateHover();
        HandlePlacementInput();
        UpdateNotePositions();
    }

    /// <summary>
    /// 每帧根据滚动偏移更新所有 Note 的视觉位置，隐藏视口外的 Note。
    /// Note 的轨道和时间数据不变，仅视觉位置跟随网格滚动。
    /// </summary>
    private void UpdateNotePositions()
    {
        if (m_noteLayer == null) return;

        float halfHeight = m_gridManager.ViewportHeight * 0.5f;
        float margin = m_noteSize;

        foreach (var note in m_notes)
        {
            if (note.View == null) continue;

            // Y 跟随网格滚动，X 使用放置时缓存的坐标（不随竖线数量变化）
            float localY = m_gridManager.TimeToLocalY(note.Time);

            // 视口外的 Note 隐藏以节省渲染
            bool isVisible = localY >= -halfHeight - margin && localY <= halfHeight + margin;
            note.View.SetActive(isVisible);

            if (isVisible)
            {
                note.View.transform.localPosition = new Vector3(note.CachedLocalX, localY, 0);
            }
        }
    }

    /// <summary>
    /// 创建 Note 渲染层（与 GridContainer 同坐标系，带遮罩裁剪）
    /// </summary>
    private void EnsureNoteLayer()
    {
        if (m_noteLayer != null) return;
        if (m_gridManager?.GridContainerRect == null) return;

        var layerObj = new GameObject("NoteLayer", typeof(RectTransform));
        layerObj.transform.SetParent(transform, false);
        layerObj.layer = 5; // UI Layer

        m_noteLayer = layerObj.GetComponent<RectTransform>();
        m_noteLayer.anchorMin = Vector2.zero;
        m_noteLayer.anchorMax = Vector2.one;
        m_noteLayer.sizeDelta = Vector2.zero;
        m_noteLayer.pivot = new Vector2(0.5f, 0.5f);

        layerObj.AddComponent<RectMask2D>();
        m_noteLayer.SetAsLastSibling();

        CreateHoverIndicator();
    }

    /// <summary>
    /// 确保 NoteLayer 始终在 GridContainer 之上渲染
    /// </summary>
    private void EnsureNoteLayerOnTop()
    {
        if (m_noteLayer == null) return;

        var gridContainer = m_gridManager.GridContainerRect;
        if (gridContainer == null) return;

        // GridContainer 可能在 CreateGrid 时被重建，需保证 NoteLayer 在其之后
        if (m_noteLayer.GetSiblingIndex() < gridContainer.GetSiblingIndex())
        {
            m_noteLayer.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 创建悬停指示器（半透明方块，吸附到最近格点）
    /// </summary>
    private void CreateHoverIndicator()
    {
        m_hoverIndicator = new GameObject("HoverIndicator", typeof(RectTransform));
        m_hoverIndicator.transform.SetParent(m_noteLayer, false);
        m_hoverIndicator.layer = 5;

        var rect = m_hoverIndicator.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(m_noteSize, m_noteSize);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = m_hoverIndicator.AddComponent<Image>();
        img.color = m_hoverColor;
        img.raycastTarget = false;
        m_hoverIndicator.SetActive(false);
    }

    /// <summary>
    /// 每帧追踪鼠标位置，吸附到最近格点并显示悬停指示器
    /// </summary>
    private void UpdateHover()
    {
        if (m_playScreenRect == null || m_noteLayer == null || m_hoverIndicator == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_playScreenRect, Input.mousePosition, null, out localPoint);

        // 仅在左半 Note 区显示悬停指示器
        if (!m_gridManager.IsInNoteArea(localPoint.x))
        {
            m_isHovering = false;
            m_hoverIndicator.SetActive(false);
            return;
        }

        // 吸附到最近格点
        m_hoveredLane = m_gridManager.LocalXToLane(localPoint.x);
        float rawTime = m_gridManager.LocalYToTime(localPoint.y);
        m_hoveredTime = SnapToBeat(rawTime);
        m_isHovering = true;

        // 更新指示器位置
        float x = m_gridManager.LaneToLocalX(m_hoveredLane);
        float y = m_gridManager.TimeToLocalY(m_hoveredTime);
        m_hoverIndicator.transform.localPosition = new Vector3(x, y, 0);
        m_hoverIndicator.SetActive(true);
    }

    /// <summary>
    /// 检测快捷键（支持组合键），在悬停位置放置对应类型的 Note
    /// </summary>
    private void HandlePlacementInput()
    {
        if (!m_isHovering || m_hotkeyList == null) return;

        foreach (var (combo, type) in m_hotkeyList)
        {
            if (combo.IsPressed())
            {
                PlaceNote(type, m_hoveredLane, m_hoveredTime);
            }
        }
    }

    /// <summary>
    /// 将时间吸附到最近的节拍格点。
    /// 使用当前时间点的 BPM 计算节拍间隔，保证与网格水平线对齐。
    /// </summary>
    private float SnapToBeat(float rawTime)
    {
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

    /// <summary>
    /// 放置一个 Note：创建视觉对象、记录数据、保存到 JSON
    /// </summary>
    private void PlaceNote(NoteType type, int lane, float time)
    {
        if (m_noteLayer == null) return;

        // 同格点去重：已有 Note 则跳过
        if (HasNoteAt(lane, time))
        {
            Debug.Log($"[NotePlacementManager] 格点已存在 Note: lane={lane} time={time:F2}s，跳过放置");
            return;
        }

        CreateNoteView(type, lane, time);
        SaveNotesToJson();

        // 记录到全局撤回/重做系统
        UndoRedoManager.Execute(
            undo: () => { RemoveNoteAt(lane, time); SaveNotesToJson(); },
            redo: () => { CreateNoteView(type, lane, time); SaveNotesToJson(); });

        Debug.Log($"[NotePlacementManager] 放置 {type} Note: lane={lane} time={time:F2}s");
    }

    /// <summary>
    /// 创建 Note 视觉对象并加入数据列表（放置和加载共用）
    /// </summary>
    private void CreateNoteView(NoteType type, int lane, float time)
    {
        GameObject view = GetOrCreateNoteView(m_noteLayer);
        // 缓存放置时的 X 坐标，改变竖线数量后不再重新计算
        float cachedX = m_gridManager.LaneToLocalX(lane);
        view.transform.localPosition = new Vector3(
            cachedX,
            m_gridManager.TimeToLocalY(time),
            0);

        Image img = view.GetComponent<Image>();
        Sprite sprite = GetSpriteForType(type);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
        }
        else
        {
            img.color = m_fallbackColor;
        }

        // 倒置 Flick：复用 flick 贴图，Y 轴翻转
        view.transform.localScale = (type == NoteType.ReverseFlick)
            ? new Vector3(1, -1, 1)
            : Vector3.one;

        m_notes.Add(new NoteEntry
        {
            Type = type,
            Lane = lane,
            Time = time,
            View = view,
            CachedLocalX = cachedX
        });
    }

    /// <summary>
    /// 检查指定格点是否已有 Note（同轨道 + 近似时间）
    /// </summary>
    private bool HasNoteAt(int lane, float time)
    {
        foreach (var note in m_notes)
        {
            if (note.Lane == lane && Mathf.Abs(note.Time - time) < 0.001f)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 移除指定位置的 Note（撤回放置操作时调用）：停用视觉对象（归还对象池），从数据列表移除
    /// </summary>
    private void RemoveNoteAt(int lane, float time)
    {
        for (int i = m_notes.Count - 1; i >= 0; i--)
        {
            if (m_notes[i].Lane == lane && Mathf.Abs(m_notes[i].Time - time) < 0.001f)
            {
                m_notes[i].View.SetActive(false);
                m_notes.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// 从对象池获取或创建 Note 视觉对象
    /// </summary>
    private GameObject GetOrCreateNoteView(Transform parent)
    {
        // 优先复用已禁用的池对象
        foreach (var pooled in m_noteViewPool)
        {
            if (!pooled.activeSelf)
            {
                pooled.SetActive(true);
                pooled.transform.SetParent(parent, false);
                return pooled;
            }
        }

        // 创建新的 Note 视觉对象
        var go = new GameObject("Note", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5; // UI Layer

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(m_noteSize, m_noteSize);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;

        m_noteViewPool.Add(go);
        return go;
    }

    /// <summary>
    /// 根据 Note 类型返回对应图片
    /// </summary>
    private Sprite GetSpriteForType(NoteType type)
    {
        switch (type)
        {
            case NoteType.Click: return m_clickSprite;
            case NoteType.Flick: return m_flickSprite;
            case NoteType.ReverseFlick: return m_flickSprite;
            case NoteType.Drag: return m_dragSprite;
            default: return null;
        }
    }

    #region JSON 持久化

    /// <summary>
    /// 获取 chart.tmp 路径（编辑期间的临时工作副本）
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
    /// 将当前所有 Note 保存到 chart.tmp（保留 info、bpmNodes 等其他字段）
    /// </summary>
    private void SaveNotesToJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath)) return;

        try
        {
            // 读取现有 chart.tmp（保留 info、bpmNodes 等字段）
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

            // 只替换 notes 字段，time 保留两位小数
            data.notes = new List<NoteJsonNode>(m_notes.Count);
            foreach (var note in m_notes)
            {
                data.notes.Add(new NoteJsonNode
                {
                    type = note.Type.ToString(),
                    lane = note.Lane,
                    time = Mathf.Round(note.Time * 100f) / 100f
                });
            }

            var jsonStr = JsonUtility.ToJson(data);
            File.WriteAllText(tmpPath, jsonStr);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 保存 Note 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 chart.tmp 加载已保存的 Note 并创建视觉对象
    /// </summary>
    private void LoadNotesFromJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath) || !File.Exists(tmpPath)) return;

        try
        {
            var json = File.ReadAllText(tmpPath);
            var data = JsonUtility.FromJson<ChartJsonData>(json);

            if (data == null || data.notes == null || data.notes.Count == 0) return;

            foreach (var node in data.notes)
            {
                // 解析 Note 类型字符串
                if (!Enum.TryParse<NoteType>(node.type, out NoteType type))
                {
                    Debug.LogWarning($"[{GetType().Name}] 未知 Note 类型: {node.type}，跳过");
                    continue;
                }

                CreateNoteView(type, node.lane, node.time);
            }

            Debug.Log($"[{GetType().Name}] 从 chart.tmp 加载 {m_notes.Count} 个 Note");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 加载 Note 失败: {ex.Message}");
        }
    }

    #endregion
}
