using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using RuntimePlayback;

/// <summary>
/// 放映模式控制器：音乐播放时淡出网格与编辑层，用 DOTween 驱动 3D Note
/// 从方体上方下落到方体顶棱。Note 作为 3D SpriteRenderer 渲染在 CubeCamera 场景中。
/// 挂载在 PlayScreen 上，与 GridManager / NotePlacementManager 同级。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PlaybackModeController : MonoBehaviour
{
    // 下落预览时间窗口（秒）：Note 在到达 hit time 前 k_lookAhead 秒开始下落
    private const float k_lookAhead = 3f;

    // 网格淡入淡出时长
    private const float k_fadeDuration = 0.4f;

    // Note 命中后保留时长（秒），到时清理
    private const float k_noteRemoveDelay = 0.3f;

    // 标定线原始透明度（与 GridManager.CreateReferenceLine 一致）
    private const float k_refLineAlpha = 0.9f;

    // Note 在方体前方的 Z 偏移（方体可见面在 local Z=-0.5，Note 在其前方）
    private const float k_noteZOffset = -0.55f;

    // 编辑模式下背景和展示区的变暗系数（0=全黑，1=原色）
    private const float k_dimFactor = 0.4f;
    private static readonly Color k_dimColor = new Color(k_dimFactor, k_dimFactor, k_dimFactor, 1f);
    private static readonly Color k_fullColor = Color.white;

    // ---- 引用 ----
    private AudioSource m_audioSource;
    private GridManager m_gridManager;
    private NotePlacementManager m_notePlacementManager;
    private CubeManager m_cubeManager;

    // ---- 状态 ----
    private bool m_isPlaying;
    private float m_prevTime;

    // ---- 淡出目标缓存 ----
    private CanvasGroup m_gridGroup;
    private CanvasGroup m_noteLayerGroup;
    private CanvasGroup m_easingGroup;
    private Graphic m_referenceLineGraphic;
    private Graphic m_playScreenGraphic;
    private Graphic m_cubeDisplayGraphic;

    // ---- 放映中的 3D Note ----
    private readonly List<PlaybackNote> m_playbackNotes = new List<PlaybackNote>();
    private readonly HashSet<long> m_spawnedKeys = new HashSet<long>();

    // ---- 方体动画播放器 ----
    private ChartPlaybackController m_chartPlayback;
    private System.DateTime m_lastChartWriteTime;

    private struct PlaybackNote
    {
        public GameObject View;
        public SpriteRenderer Renderer;
        public float HitTime;
        public int Lane;
        public Vector3 StartPos;
        public Vector3 EndPos;
    }

    private void Start()
    {
        var audioObj = GameObject.Find("Audio Source");
        if (audioObj != null)
        {
            m_audioSource = audioObj.GetComponent<AudioSource>();
        }

        m_gridManager = GetComponent<GridManager>();
        m_notePlacementManager = GetComponent<NotePlacementManager>();
        m_cubeManager = FindObjectOfType<CubeManager>();

        // 初始化方体动画播放器
        m_chartPlayback = gameObject.GetComponent<ChartPlaybackController>();
        if (m_chartPlayback == null)
            m_chartPlayback = gameObject.AddComponent<ChartPlaybackController>();
        m_chartPlayback.SetAudioSource(m_audioSource);
        if (m_cubeManager != null)
            m_chartPlayback.SetCubeParent(m_cubeManager.transform);

        // 加载谱面数据并发现方体（编辑模式下也驱动动画）
        RefreshChartPlayback();
    }

    private void Update()
    {
        // 重试加载谱面（EditorInit.ChartPath 可能在 Start 后才就绪）
        if (m_chartPlayback != null && m_chartPlayback.ChartData == null
            && m_cubeManager != null && !string.IsNullOrEmpty(EditorInit.ChartPath))
        {
            RefreshChartPlayback();
        }

        // 重试发现方体（首次加载时方体 GameObject 可能尚未创建）
        if (m_chartPlayback != null && m_chartPlayback.ChartData != null
            && m_chartPlayback.CubeAnimatorCount == 0 && m_cubeManager != null)
        {
            m_chartPlayback.DiscoverCubes();
        }

        // 检测谱面文件变化（用户修改了锚点）
        CheckChartFileChanged();

        bool wasPlaying = m_isPlaying;
        m_isPlaying = m_audioSource != null && m_audioSource.isPlaying;

        if (m_isPlaying && !wasPlaying)
        {
            EnterPlaybackMode();
        }
        else if (!m_isPlaying && wasPlaying)
        {
            ExitPlaybackMode();
        }

        // 当前时间：播放时用音频时间，编辑时用网格时间
        float currentTime = m_isPlaying && m_audioSource != null
            ? m_audioSource.time
            : (m_gridManager != null ? m_gridManager.CurrentTime : 0f);

        // 始终缓存淡出目标（含背景变暗初始化）
        CacheFadeTargets();

        // Note 下落（播放和编辑模式都更新）
        UpdatePlaybackNotes(currentTime);

        // 始终驱动方体动画
        if (m_chartPlayback != null && m_chartPlayback.ChartData != null)
        {
            m_chartPlayback.UpdateAllCubes(currentTime);
        }
    }

    /// <summary>
    /// 检测谱面文件是否被修改（用户编辑锚点后触发），若修改则热重载
    /// </summary>
    private void CheckChartFileChanged()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath)) return;
        string chartPath = System.IO.Path.Combine(EditorInit.ChartPath, "chart.tmp");
        if (!System.IO.File.Exists(chartPath)) return;

        var writeTime = System.IO.File.GetLastWriteTime(chartPath);
        if (writeTime != m_lastChartWriteTime)
        {
            m_lastChartWriteTime = writeTime;
            if (m_chartPlayback != null && m_chartPlayback.ChartData != null)
            {
                m_chartPlayback.ReloadChartData(chartPath);
            }
        }
    }

    /// <summary>
    /// 重新加载谱面数据（获取最新锚点修改）。
    /// 不调用 SaveCubesToJson——chart.tmp 已由 CopyChartToTemp / EasingAreaManager 写入，
    /// 启动时保存内存数据会覆盖文件中的用户锚点。
    /// </summary>
    private void RefreshChartPlayback()
    {
        if (m_chartPlayback == null || m_cubeManager == null) return;
        if (string.IsNullOrEmpty(EditorInit.ChartPath)) return;

        string chartPath = System.IO.Path.Combine(EditorInit.ChartPath, "chart.tmp");
        bool loaded = m_chartPlayback.LoadChart(chartPath);
        m_chartPlayback.DiscoverCubes();

        if (System.IO.File.Exists(chartPath))
            m_lastChartWriteTime = System.IO.File.GetLastWriteTime(chartPath);

        Debug.Log($"[{GetType().Name}] RefreshChartPlayback: loaded={loaded}, " +
                  $"animators={m_chartPlayback.CubeAnimatorCount}, " +
                  $"chartData={m_chartPlayback.ChartData != null}");
    }

    /// <summary>
    /// 延迟缓存需要淡出的目标（GridContainer、NoteLayer、ReferenceLine 在 Start 后才创建）
    /// </summary>
    private void CacheFadeTargets()
    {
        if (m_gridGroup == null && m_gridManager?.GridContainerRect != null)
        {
            m_gridGroup = m_gridManager.GridContainerRect.gameObject.GetComponent<CanvasGroup>();
            if (m_gridGroup == null)
            {
                m_gridGroup = m_gridManager.GridContainerRect.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (m_noteLayerGroup == null && m_notePlacementManager?.NoteLayerRect != null)
        {
            m_noteLayerGroup = m_notePlacementManager.NoteLayerRect.gameObject.GetComponent<CanvasGroup>();
            if (m_noteLayerGroup == null)
            {
                m_noteLayerGroup = m_notePlacementManager.NoteLayerRect.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (m_referenceLineGraphic == null)
        {
            var refLine = transform.Find("ReferenceLine");
            if (refLine != null)
            {
                m_referenceLineGraphic = refLine.GetComponent<Graphic>();
            }
        }

        // 缓动区（EasingViewport，包含锚点线、曲线、锚点标记）
        if (m_easingGroup == null)
        {
            var easingVp = transform.Find("EasingViewport");
            if (easingVp != null)
            {
                m_easingGroup = easingVp.gameObject.GetComponent<CanvasGroup>();
                if (m_easingGroup == null)
                {
                    m_easingGroup = easingVp.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        // 背景曲绘 Image（PlayScreen 自身的 Graphic）
        if (m_playScreenGraphic == null)
        {
            m_playScreenGraphic = GetComponent<Graphic>();
            // 编辑模式下初始变暗
            if (m_playScreenGraphic != null && !m_isPlaying)
            {
                m_playScreenGraphic.color = k_dimColor;
            }
        }

        // 方体展示区 RawImage
        if (m_cubeDisplayGraphic == null && m_cubeManager != null)
        {
            m_cubeDisplayGraphic = m_cubeManager.CubeDisplay;
            // 编辑模式下初始变暗
            if (m_cubeDisplayGraphic != null && !m_isPlaying)
            {
                m_cubeDisplayGraphic.color = k_dimColor;
            }
        }
    }

    // ---- 放映模式进出 ----

    private void EnterPlaybackMode()
    {
        CacheFadeTargets();

        // 淡出网格、编辑 Note 层、标定线、缓动区
        m_gridGroup?.DOFade(0f, k_fadeDuration);
        m_noteLayerGroup?.DOFade(0f, k_fadeDuration);
        m_easingGroup?.DOFade(0f, k_fadeDuration);
        m_referenceLineGraphic?.DOFade(0f, k_fadeDuration);

        // 背景和展示区恢复全亮
        m_playScreenGraphic?.DOColor(k_fullColor, k_fadeDuration);
        m_cubeDisplayGraphic?.DOColor(k_fullColor, k_fadeDuration);

        // 切换 CubeCamera 到放映模式（居中方体正面）
        m_cubeManager?.SetPlaybackCameraMode(true);

        // 重新加载谱面（获取最新锚点修改）
        RefreshChartPlayback();

        m_spawnedKeys.Clear();
        m_prevTime = m_audioSource.time;

        Debug.Log($"[{GetType().Name}] 进入放映模式");
    }

    private void ExitPlaybackMode()
    {
        // 恢复网格、编辑 Note 层、标定线、缓动区
        m_gridGroup?.DOFade(1f, k_fadeDuration);
        m_noteLayerGroup?.DOFade(1f, k_fadeDuration);
        m_easingGroup?.DOFade(1f, k_fadeDuration);
        m_referenceLineGraphic?.DOFade(k_refLineAlpha, k_fadeDuration);

        // 背景和展示区变暗
        m_playScreenGraphic?.DOColor(k_dimColor, k_fadeDuration);
        m_cubeDisplayGraphic?.DOColor(k_dimColor, k_fadeDuration);

        // 恢复 CubeCamera 到编辑模式
        m_cubeManager?.SetPlaybackCameraMode(false);

        // 方体动画继续由 UpdateCubeAnimation 驱动（切换为网格时间）

        ClearPlaybackNotes();

        Debug.Log($"[{GetType().Name}] 退出放映模式");
    }

    // ---- 3D Note 下落逻辑 ----

    /// <summary>
    /// 根据面方向计算下落向量和轨道轴。
/// 下落方向 = 面中心指向棱的方向；轨道轴 = 垂直于下落方向的轴。
    /// </summary>
    private static void GetDirectionVectors(FaceDirection dir,
        out Vector3 fallingDir, out Vector3 laneAxis)
    {
        switch (dir)
        {
            case FaceDirection.Up:
                fallingDir = Vector3.up;
                laneAxis = Vector3.right;
                break;
            case FaceDirection.Down:
                fallingDir = Vector3.down;
                laneAxis = Vector3.right;
                break;
            case FaceDirection.Left:
                fallingDir = Vector3.left;
                laneAxis = Vector3.up;
                break;
            case FaceDirection.Right:
                fallingDir = Vector3.right;
                laneAxis = Vector3.up;
                break;
            default:
                fallingDir = Vector3.up;
                laneAxis = Vector3.right;
                break;
        }
    }

    private void UpdatePlaybackNotes(float currentTime)
    {
        if (m_gridManager == null || m_notePlacementManager == null || m_cubeManager == null) return;

        // 检测跳转（拖动滑块）：时间大幅变化时清空重新生成
        if (Mathf.Abs(currentTime - m_prevTime) > 1f)
        {
            ClearPlaybackNotes();
        }
        m_prevTime = currentTime;

        // 从 NotePlacementManager 获取当前编辑中的 Note
        var currentNotes = m_notePlacementManager.GetCurrentNotes();
        if (currentNotes == null) return;

        float cubeHalf = m_cubeManager.CubeSize * 0.5f;

        // 根据当前面方向计算下落向量和轨道轴
        GetDirectionVectors(m_cubeManager.ActiveDirection, out var fallingDir, out var laneAxis);

        // 生成即将到达的 Note
        foreach (var note in currentNotes)
        {
            float timeUntilHit = note.time - currentTime;
            if (timeUntilHit > 0f && timeUntilHit <= k_lookAhead)
            {
                long key = (long)note.lane * 100000L + (long)(note.time * 1000f);
                if (!m_spawnedKeys.Contains(key))
                {
                    SpawnPlaybackNote(note, currentTime, fallingDir, laneAxis, cubeHalf);
                    m_spawnedKeys.Add(key);
                }
            }
        }

        // 每帧更新 Note 位置（基于当前时间，不依赖 DOTween）
        for (int i = 0; i < m_playbackNotes.Count; i++)
        {
            var pb = m_playbackNotes[i];
            if (pb.View == null) continue;

            float timeUntilHit = pb.HitTime - currentTime;

            if (timeUntilHit > 0f)
            {
                // 下落中：progress 从 0（刚生成）到 1（贴图中线到达棱）
                float progress = 1f - timeUntilHit / k_lookAhead;
                pb.View.transform.localPosition = Vector3.Lerp(pb.StartPos, pb.EndPos, progress);
            }
            else
            {
                // 到达棱：贴片中线对齐棱位置
                pb.View.transform.localPosition = pb.EndPos;
            }
        }

        // 清除已命中 Note（贴图中线到达棱后立即销毁）
        for (int i = m_playbackNotes.Count - 1; i >= 0; i--)
        {
            if (currentTime >= m_playbackNotes[i].HitTime)
            {
                if (m_playbackNotes[i].View != null)
                {
                    m_playbackNotes[i].View.SetActive(false);
                    Destroy(m_playbackNotes[i].View);
                }
                m_playbackNotes.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 在方体面上生成一个 3D Note，从面对侧边缘沿下落方向落到目标棱。
    /// </summary>
    private void SpawnPlaybackNote(NoteJsonNode note, float currentTime,
        Vector3 fallingDir, Vector3 laneAxis, float cubeHalf)
    {
        var cubeTransform = m_cubeManager.ActiveCubeTransform;
        if (cubeTransform == null) return;

        var go = new GameObject("PB_Note3D");
        go.transform.SetParent(cubeTransform, false);
        go.layer = m_cubeManager.CubeLayer;

        // 固定 Note 大小（不随轨道数量或方体大小变化），离屏边距由此派生
        const float k_fixedNoteSize = 0.18f;

        // 面中心（可见面上）
        Vector3 faceCenter = new Vector3(0, 0, k_noteZOffset);

        // 轨道位置（垂直于下落方向）
        int laneCount = m_gridManager.LaneCount;
        float lanePos = laneCount > 1
            ? -cubeHalf + (float)note.lane / (laneCount - 1) * cubeHalf * 2f
            : 0f;
        Vector3 laneOffset = laneAxis * lanePos;

        // 起点：从屏幕外开始下落（超出相机视野不渲染），而非堆积在屏幕边缘
        Camera cubeCam = m_cubeManager.CubeCamera;
        float orthoSize = cubeCam != null ? cubeCam.orthographicSize : 0.8f;
        float aspect = cubeCam != null && cubeCam.targetTexture != null
            ? (float)cubeCam.targetTexture.width / cubeCam.targetTexture.height
            : (float)Screen.width / Screen.height;
        bool isVerticalFall = fallingDir == Vector3.up || fallingDir == Vector3.down;
        float viewHalfExtent = isVerticalFall ? orthoSize : orthoSize * aspect;
        float noteHalf = k_fixedNoteSize * 0.5f; // Note 固定尺寸的一半
        float startDist = viewHalfExtent + noteHalf + 0.05f; // 略超视野边界，确保从屏幕外开始

        // 棱有宽度，Note 贴图中线到达棱外边缘时销毁
        var visualizer = cubeTransform.GetComponent<CubeVisualizer>();
        float edgeHalf = visualizer != null ? visualizer.EdgeThickness * 0.5f : 0f;
        Vector3 startPos = faceCenter - fallingDir * startDist + laneOffset;
        Vector3 endPos = faceCenter + fallingDir * (cubeHalf - edgeHalf) + laneOffset;

        go.transform.localPosition = startPos;
        go.transform.localRotation = Quaternion.Euler(0, 180, 0);

        // SpriteRenderer
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        Sprite sprite = null;
        if (System.Enum.TryParse<NotePlacementManager.NoteType>(note.type, out var type))
        {
            sprite = m_notePlacementManager?.GetNoteSprite(type);
        }
        sr.sprite = sprite;
        sr.color = Color.white;

        // 固定 Note 大小（已在上方声明），缩放基于 Note 尺寸与精灵原始尺寸
        if (sprite != null && sprite.pixelsPerUnit > 0)
        {
            float spriteWorldSize = sprite.rect.width / sprite.pixelsPerUnit;
            float scale = k_fixedNoteSize / spriteWorldSize;
            go.transform.localScale = new Vector3(scale, scale, 1);
        }

        m_playbackNotes.Add(new PlaybackNote
        {
            View = go,
            Renderer = sr,
            HitTime = note.time,
            Lane = note.lane,
            StartPos = startPos,
            EndPos = endPos
        });
    }

    /// <summary>
    /// 清除所有放映 Note 并终止 DOTween 动画
    /// </summary>
    private void ClearPlaybackNotes()
    {
        foreach (var pb in m_playbackNotes)
        {
            if (pb.View != null)
            {
                pb.View.SetActive(false);
                Destroy(pb.View);
            }
        }
        m_playbackNotes.Clear();
        m_spawnedKeys.Clear();
    }

    private void OnDisable()
    {
        ClearPlaybackNotes();
    }
}
