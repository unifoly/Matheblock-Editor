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
    // Hold 类型专用：结束时间（非 Hold 类型为 0）
    public float endTime;
}

/// <summary>
/// 左半 Note 区放置管理器：鼠标悬停在格点上时按 Q/E/R 放置对应类型的 Note。
/// 已放置的 Note 固定在放置位置，不随网格线移动。Note 数据持久化到 chart.tmp。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class NotePlacementManager : MonoBehaviour
{
    // Note 类型枚举
    public enum NoteType
    {
        Click,
        Flick,
        Drag,
        ReverseFlick,
        Hold
    }

    [Header("Note 图片")]
    [Tooltip("Click 类型图片（快捷键 Q）")]
    [SerializeField] private Sprite m_clickSprite;
    [Tooltip("Flick 类型图片（快捷键 E）")]
    [SerializeField] private Sprite m_flickSprite;
    [Tooltip("Drag 类型图片（快捷键 E）")]
    [SerializeField] private Sprite m_dragSprite;

    [Header("Hold 图片")]
    [Tooltip("Hold 头部图片（快捷键 W）")]
    [SerializeField] private Sprite m_holdHeadSprite;
    [Tooltip("Hold 中间部分图片（自动平铺）")]
    [SerializeField] private Sprite m_holdMidSprite;
    [Tooltip("Hold 尾部图片")]
    [SerializeField] private Sprite m_holdTailSprite;

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
    private const string k_actionHold = "Note_Hold";
    private const string k_actionDelete = "Note_Delete";

    // 运行时从 KeyBindingsStore 加载的快捷键映射（支持组合键）
    private List<(KeyCombo combo, NoteType type)> m_hotkeyList;
    private KeyCombo m_deleteCombo;

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

    // Hold 放置中间状态：第一次按 W 后等待第二次按 W 确认尾点
    private bool m_holdPending;
    private int m_holdPendingLane;
    private float m_holdPendingTime;
    private GameObject m_holdPendingView;

    // 待加载的方体轨道 Note（m_noteLayer 尚未就绪时暂存）
    private List<NoteJsonNode> m_pendingReloadNotes;

    // Note 数据结构
    private class NoteEntry
    {
        public NoteType Type;
        public int Lane;
        public float Time;
        public GameObject View;
        // 放置时缓存的本地 X 坐标，改变竖线数量时不重新计算
        public float CachedLocalX;
        // Hold 专用：结束时间（非 Hold 为 0）
        public float EndTime;
        // Hold 专用：中间和尾部视觉对象（head 在 View 中）
        public List<GameObject> ExtraViews;
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
        // 保留 cubes 字段，避免 Note 保存时丢失 CubeManager 写入的方体数据
        public List<CubeData> cubes;
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
        TryAddHotkey(k_actionHold, "W", NoteType.Hold);

        // 删除快捷键（默认 Delete 键）
        m_deleteCombo = KeyBindingsStore.GetKeyCombo(k_actionDelete, KeyCombo.Parse("Delete"));
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

            if (m_pendingReloadNotes != null)
            {
                // 方体系统已提供待加载的 Note，优先使用（不加载 flat notes 数组）
                DoReloadNotes(m_pendingReloadNotes);
                m_pendingReloadNotes = null;
            }
            else
            {
                LoadNotesFromJson();
            }
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
            float endY = (note.Type == NoteType.Hold) ? m_gridManager.TimeToLocalY(note.EndTime) : localY;
            bool isVisible = Mathf.Max(localY, endY) >= -halfHeight - margin
                          && Mathf.Min(localY, endY) <= halfHeight + margin;

            note.View.SetActive(isVisible);

            if (isVisible)
            {
                note.View.transform.localPosition = new Vector3(note.CachedLocalX, localY, 0);

                // Hold 中间和尾部也要更新位置
                if (note.Type == NoteType.Hold && note.ExtraViews != null)
                {
                    foreach (var v in note.ExtraViews)
                    {
                        if (v == null) continue;
                        v.SetActive(true);
                        var img = v.GetComponent<Image>();
                        // 中间部分：位置在中点（通过 sprite 判断）
                        if (img != null && img.sprite == m_holdMidSprite)
                        {
                            v.transform.localPosition = new Vector3(note.CachedLocalX, (localY + endY) * 0.5f, 0);
                        }
                        else
                        {
                            // 尾部
                            v.transform.localPosition = new Vector3(note.CachedLocalX, endY, 0);
                        }
                    }
                }
            }
            else if (note.Type == NoteType.Hold && note.ExtraViews != null)
            {
                foreach (var v in note.ExtraViews)
                {
                    if (v != null) v.SetActive(false);
                }
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

        // 文本输入框获焦时跳过快捷键，避免与文本编辑冲突
        if (UndoRedoManager.IsTextInputFocused()) return;

        // Esc 取消 Hold 等待状态
        if (m_holdPending && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelHoldPending();
            return;
        }

        // Delete 键：删除悬停位置的 Note
        if (m_deleteCombo.IsPressed())
        {
            DeleteNoteAtHover();
            return;
        }

        foreach (var (combo, type) in m_hotkeyList)
        {
            if (combo.IsPressed())
            {
                if (type == NoteType.Hold)
                {
                    HandleHoldPlacement(m_hoveredLane, m_hoveredTime);
                }
                else
                {
                    // 切换到其他 Note 类型时取消 Hold 等待
                    if (m_holdPending) CancelHoldPending();
                    PlaceNote(type, m_hoveredLane, m_hoveredTime);
                }
            }
        }
    }

    /// <summary>
    /// Hold 两步放置：
    /// 第一次按 W：在悬停位置生成头部，进入等待状态
    /// 第二次按 W（同一列）：自动填充中间和尾部，完成 Hold
    /// </summary>
    private void HandleHoldPlacement(int lane, float time)
    {
        if (!m_holdPending)
        {
            // 第一次按 W：记录头部位置，显示临时头部
            m_holdPending = true;
            m_holdPendingLane = lane;
            m_holdPendingTime = time;
            m_holdPendingView = GetOrCreateNoteView(m_noteLayer);
            SetupNoteSprite(m_holdPendingView, m_holdHeadSprite);
            m_holdPendingView.transform.localPosition = new Vector3(
                m_gridManager.LaneToLocalX(lane),
                m_gridManager.TimeToLocalY(time),
                0);
            Debug.Log($"[NotePlacementManager] Hold 头部待确认: lane={lane} time={time:F2}s，按 W 确认尾点，Esc 取消");
        }
        else
        {
            // 第二次按 W
            if (lane != m_holdPendingLane)
            {
                // 不同列：取消旧等待，在新位置开始
                CancelHoldPending();
                m_holdPending = true;
                m_holdPendingLane = lane;
                m_holdPendingTime = time;
                m_holdPendingView = GetOrCreateNoteView(m_noteLayer);
                SetupNoteSprite(m_holdPendingView, m_holdHeadSprite);
                m_holdPendingView.transform.localPosition = new Vector3(
                    m_gridManager.LaneToLocalX(lane),
                    m_gridManager.TimeToLocalY(time),
                    0);
                Debug.Log($"[NotePlacementManager] Hold 切换到新列: lane={lane} time={time:F2}s");
                return;
            }

            // 同列：完成 Hold
            float startTime = Mathf.Min(m_holdPendingTime, time);
            float endTime = Mathf.Max(m_holdPendingTime, time);

            // 移除临时头部
            if (m_holdPendingView != null) m_holdPendingView.SetActive(false);
            m_holdPending = false;

            // 起止点相同：取消
            if (Mathf.Approximately(startTime, endTime))
            {
                Debug.Log("[NotePlacementManager] Hold 起止点相同，取消放置");
                return;
            }

            // 去重检查
            if (HasNoteAt(lane, startTime))
            {
                Debug.Log($"[NotePlacementManager] 格点已存在 Note: lane={lane} time={startTime:F2}s，跳过");
                return;
            }

            PlaceHold(lane, startTime, endTime);
        }
    }

    /// <summary>
    /// 取消 Hold 等待状态，归还临时头部到对象池
    /// </summary>
    private void CancelHoldPending()
    {
        if (m_holdPendingView != null) m_holdPendingView.SetActive(false);
        m_holdPending = false;
        m_holdPendingView = null;
    }

    /// <summary>
    /// 放置完整的 Hold（头 + 中间 + 尾），注册撤回/重做
    /// </summary>
    private void PlaceHold(int lane, float startTime, float endTime)
    {
        CreateHoldView(lane, startTime, endTime);
        SaveNotesToJson();

        UndoRedoManager.Execute(
            undo: () => { RemoveHoldAt(lane, startTime); SaveNotesToJson(); },
            redo: () => { CreateHoldView(lane, startTime, endTime); SaveNotesToJson(); });

        Debug.Log($"[NotePlacementManager] 放置 Hold: lane={lane} time={startTime:F2}s~{endTime:F2}s");
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
    /// 设置 Note 视觉对象的图片（提取公共逻辑）
    /// </summary>
    private void SetupNoteSprite(GameObject view, Sprite sprite)
    {
        var img = view.GetComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
        }
        else
        {
            img.color = m_fallbackColor;
        }
    }

    /// <summary>
    /// 创建 Hold 视觉对象（头 + 中间平铺 + 尾），加入数据列表
    /// </summary>
    private void CreateHoldView(int lane, float startTime, float endTime)
    {
        if (m_noteLayer == null) return;

        float cachedX = m_gridManager.LaneToLocalX(lane);
        float startY = m_gridManager.TimeToLocalY(startTime);
        float endY = m_gridManager.TimeToLocalY(endTime);

        // 头部（使用尾部贴图）
        GameObject headView = GetOrCreateNoteView(m_noteLayer);
        SetupNoteSprite(headView, m_holdTailSprite);
        headView.transform.localPosition = new Vector3(cachedX, startY, 0);
        // 整体 Y 轴翻转（贴图方向需要翻转）
        headView.transform.localScale = new Vector3(1, -1, 1);

        var extraViews = new List<GameObject>();

        // 中间部分（拉伸覆盖头尾中心之间，避免透明间隔）
        float midHeight = Mathf.Abs(endY - startY);
        if (midHeight > 1f && m_holdMidSprite != null)
        {
            GameObject midView = GetOrCreateNoteView(m_noteLayer);
            var midImg = midView.GetComponent<Image>();
            midImg.sprite = m_holdMidSprite;
            midImg.color = Color.white;
            midImg.type = Image.Type.Simple;
            midImg.preserveAspect = false;

            var midRect = midView.GetComponent<RectTransform>();
            midRect.sizeDelta = new Vector2(m_noteSize, midHeight);
            midView.transform.localPosition = new Vector3(cachedX, (startY + endY) * 0.5f, 0);
            // 中间段不翻转
            midView.transform.localScale = Vector3.one;
            // 中间段渲染在头尾之下
            midView.transform.SetAsFirstSibling();
            extraViews.Add(midView);
        }

        // 尾部（使用头部贴图）
        GameObject tailView = GetOrCreateNoteView(m_noteLayer);
        SetupNoteSprite(tailView, m_holdHeadSprite);
        tailView.transform.localPosition = new Vector3(cachedX, endY, 0);
        tailView.transform.localScale = new Vector3(1, -1, 1);
        extraViews.Add(tailView);

        m_notes.Add(new NoteEntry
        {
            Type = NoteType.Hold,
            Lane = lane,
            Time = startTime,
            EndTime = endTime,
            View = headView,
            CachedLocalX = cachedX,
            ExtraViews = extraViews
        });
    }

    /// <summary>
    /// 移除指定位置的 Hold（头 + 中间 + 尾全部移除，作为整体撤回）
    /// </summary>
    private void RemoveHoldAt(int lane, float startTime)
    {
        for (int i = m_notes.Count - 1; i >= 0; i--)
        {
            var note = m_notes[i];
            if (note.Type == NoteType.Hold && note.Lane == lane
                && Mathf.Abs(note.Time - startTime) < 0.001f)
            {
                // 归还头部
                if (note.View != null) note.View.SetActive(false);
                // 归还中间和尾部
                if (note.ExtraViews != null)
                {
                    foreach (var v in note.ExtraViews)
                    {
                        if (v != null) v.SetActive(false);
                    }
                }
                m_notes.RemoveAt(i);
                return;
            }
        }
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
            // Hold 类型由 RemoveHoldAt 处理，此处跳过
            if (m_notes[i].Type == NoteType.Hold) continue;

            if (m_notes[i].Lane == lane && Mathf.Abs(m_notes[i].Time - time) < 0.001f)
            {
                m_notes[i].View.SetActive(false);
                m_notes.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// 删除悬停位置的 Note（含 Hold），注册撤回/重做。
    /// 若悬停位置无 Note 则不执行任何操作。
    /// </summary>
    private void DeleteNoteAtHover()
    {
        int lane = m_hoveredLane;
        float time = m_hoveredTime;

        // 查找悬停位置的 Note（含 Hold）
        for (int i = m_notes.Count - 1; i >= 0; i--)
        {
            var note = m_notes[i];
            if (note.Lane != lane || Mathf.Abs(note.Time - time) >= 0.001f) continue;

            NoteType type = note.Type;
            float startTime = note.Time;
            float endTime = note.EndTime;

            if (type == NoteType.Hold)
            {
                RemoveHoldAt(lane, startTime);
                SaveNotesToJson();

                UndoRedoManager.Execute(
                    undo: () => { CreateHoldView(lane, startTime, endTime); SaveNotesToJson(); },
                    redo: () => { RemoveHoldAt(lane, startTime); SaveNotesToJson(); });
            }
            else
            {
                RemoveNoteAt(lane, time);
                SaveNotesToJson();

                UndoRedoManager.Execute(
                    undo: () => { CreateNoteView(type, lane, time); SaveNotesToJson(); },
                    redo: () => { RemoveNoteAt(lane, time); SaveNotesToJson(); });
            }

            // 取消 Hold 等待状态（避免删除后仍显示临时头部）
            if (m_holdPending) CancelHoldPending();

            Debug.Log($"[NotePlacementManager] 删除 {type} Note: lane={lane} time={time:F2}s");
            return;
        }
    }

    /// <summary>
    /// 清除所有已放置的 Note（视觉对象归还对象池，数据列表清空）
    /// </summary>
    public void ClearAllNotes()
    {
        foreach (var note in m_notes)
        {
            if (note.View != null)
            {
                note.View.SetActive(false);
            }

            // Hold 的中间和尾部也要归还
            if (note.ExtraViews != null)
            {
                foreach (var v in note.ExtraViews)
                {
                    if (v != null) v.SetActive(false);
                }
            }
        }

        // 取消 Hold 等待状态
        CancelHoldPending();

        m_notes.Clear();
    }

    /// <summary>
    /// 获取当前所有已放置 Note 的数据副本（供方体系统保存到轨道）
    /// </summary>
    public List<NoteJsonNode> GetCurrentNotes()
    {
        var result = new List<NoteJsonNode>(m_notes.Count);
        foreach (var note in m_notes)
        {
            result.Add(new NoteJsonNode
            {
                type = note.Type.ToString(),
                lane = note.Lane,
                time = Mathf.Round(note.Time * 100f) / 100f,
                endTime = (note.Type == NoteType.Hold) ? Mathf.Round(note.EndTime * 100f) / 100f : 0f
            });
        }
        return result;
    }

    /// <summary>
    /// 清除现有 Note 并从指定列表重新加载（用于方体/轨道组切换）
    /// </summary>
    public void ReloadNotes(List<NoteJsonNode> notes)
    {
        ClearAllNotes();

        // m_noteLayer 尚未就绪：暂存待加载，等 Update 中 EnsureNoteLayer 完成后自动加载
        if (m_noteLayer == null)
        {
            m_pendingReloadNotes = notes;
            return;
        }

        // 标记已加载，防止 Update 中的 LoadNotesFromJson 用 flat notes 数组覆盖方体轨道数据
        m_notesLoaded = true;
        DoReloadNotes(notes);
    }

    /// <summary>
    /// 实际执行 Note 列表加载（内部方法，确保 m_noteLayer 和 m_gridManager 已就绪）
    /// </summary>
    private void DoReloadNotes(List<NoteJsonNode> notes)
    {
        if (notes == null) return;

        CacheGridManager();
        if (m_gridManager == null) return;

        foreach (var node in notes)
        {
            if (Enum.TryParse<NoteType>(node.type, out NoteType type))
            {
                if (type == NoteType.Hold && node.endTime > 0f)
                {
                    CreateHoldView(node.lane, node.time, node.endTime);
                }
                else
                {
                    CreateNoteView(type, node.lane, node.time);
                }
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
            case NoteType.Hold: return m_holdHeadSprite;
            default: return null;
        }
    }

    // ---- 公开访问器（供 PlaybackModeController 使用）----

    /// <summary>Note 渲染层 RectTransform</summary>
    public RectTransform NoteLayerRect => m_noteLayer;

    /// <summary>Note 视觉尺寸</summary>
    public float NoteSize => m_noteSize;

    /// <summary>根据 NoteType 获取对应 Sprite（公开接口）</summary>
    public Sprite GetNoteSprite(NoteType type) => GetSpriteForType(type);

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
                    time = Mathf.Round(note.Time * 100f) / 100f,
                    endTime = (note.Type == NoteType.Hold) ? Mathf.Round(note.EndTime * 100f) / 100f : 0f
                });
            }

            var jsonStr = JsonUtility.ToJson(data);
            File.WriteAllText(tmpPath, jsonStr);

            // 诊断日志：验证 cubes 字段是否被保留
            int cubeCount = data.cubes?.Count ?? 0;
            int totalAnchors = 0;
            if (data.cubes != null)
            {
                foreach (var cube in data.cubes)
                {
                    if (cube.easingSlots != null)
                        foreach (var slot in cube.easingSlots)
                            totalAnchors += slot?.anchorPoints?.Count ?? 0;
                }
            }
            Debug.Log($"[{GetType().Name}] 保存 Note: notes={data.notes?.Count ?? 0}, cubes={cubeCount}, anchors={totalAnchors}");
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

                if (type == NoteType.Hold && node.endTime > 0f)
                {
                    CreateHoldView(node.lane, node.time, node.endTime);
                }
                else
                {
                    CreateNoteView(type, node.lane, node.time);
                }
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
