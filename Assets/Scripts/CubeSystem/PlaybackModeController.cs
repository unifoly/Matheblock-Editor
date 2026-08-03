using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using RuntimePlayback;
using HexMap;

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

    // 流速默认值（与 EasingSlotConfigs 流速槽 defaultValue=30 一致，等价 1x 速度）
    private const float k_defaultFlowSpeed = 30f;

    // 网格淡入淡出时长
    private const float k_fadeDuration = 0.4f;

    // 标定线原始透明度（与 GridManager.CreateReferenceLine 一致）
    private const float k_refLineAlpha = 0.9f;

    // Note 在方体前方的 Z 偏移（方体可见面在 local Z=-0.5，Note 在其前方）
    private const float k_noteZOffset = -0.55f;

    // 编辑模式下背景曲绘（曲目美术）的变暗系数（0=全黑，1=原色）
    private const float k_trackDimFactor = 0.4f;
    private static readonly Color k_trackDimColor = new Color(k_trackDimFactor, k_trackDimFactor, k_trackDimFactor, 1f);
    private static readonly Color k_fullColor = Color.white;

    // 编辑模式下展示区（方体展示面板）的变暗系数（0=全黑，1=原色）
    private const float k_cubeDimFactor = 0.4f;
    private static readonly Color k_cubeDimColor = new Color(k_cubeDimFactor, k_cubeDimFactor, k_cubeDimFactor, 1f);

    // Fake Note 半透明颜色（3D 预览与编辑器一致，且命中后无打击特效）
    private static readonly Color k_fakeColor = new Color(1f, 1f, 1f, 0.5f);

    // ---- 引用 ----
    private AudioSource m_audioSource;
    private GridManager m_gridManager;
    private NotePlacementManager m_notePlacementManager;
    private CubeManager m_cubeManager;

    // ---- 状态 ----
    private bool m_isPlaying;
    private float m_prevTime;

    // Display 模式：放映时保留网格、编辑 Note 层、标定线、缓动区（不淡出）
    private bool m_keepGridDuringPlayback;

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
    private float m_lastChartCheckTime;

    // ---- 打击特效 ----
    private HitEffectManager m_hitEffect;

    // ---- Combo 计数显示 ----
    // combo SDF 字体资源路径（Assets/Fonts/combo SDF.asset）
    private const string k_comboFontAssetPath = "Assets/Fonts/combo SDF.asset";
    // combo 源字体文件路径（combo SDF 资产图集为空时的回退）
    private const string k_comboSourceFontPath = "Assets/Fonts/combo.ttf";

    // 数字行与 COMBO 标签行的显示名称
    private const string k_comboNumberName = "ComboNumber";
    private const string k_comboLabelName = "ComboLabel";

    // 数字顶部距 PlayScreen 顶部的偏移（像素）
    private const float k_comboTopMargin = 20f;
    // 数字字号
    private const float k_comboNumberFontSize = 72f;
    // COMBO 标签字号
    private const float k_comboLabelFontSize = 24f;
    // 数字底部与标签顶部之间的间距（像素）
    private const float k_comboLabelGap = 6f;

    // 文字描边（放映时背景变亮，描边保证数字/标签清晰可读）
    private const float k_comboOutlineWidth = 0.2f;
    private static readonly Color k_comboOutlineColor = new Color(0f, 0f, 0f, 1f);

    private TMP_FontAsset m_comboFont;
    private TextMeshProUGUI m_comboNumberText;
    private TextMeshProUGUI m_comboLabelText;
    private int m_comboCount;

    private struct PlaybackNote
    {
        public GameObject View;
        public SpriteRenderer Renderer;
        public float HitTime;
        public int Lane;
        public Vector3 StartPos;
        public Vector3 EndPos;
        // 生成时的有效预览时间窗口（受流速影响）
        public float EffectiveLookAhead;
        // ---- Hold 专用 ----
        public float EndTime;
        public bool IsHold;
        public Vector3 FallingDir;
        public float FallSpeed;
        public Transform MidTransform;
        public Transform TailTransform;
        public float MidPerpScale;
        public float MidBodyFactor;
        // 中段位置偏移因子（基于 sprite pivot，确保中段在头尾之间）
        public float MidPivotFactor;
        // 是否已触发命中特效（普通 Note 一次性，Hold 持续）
        public bool HitEffectStarted;
        // 是否 Fake Note（命中后无打击特效）
        public bool IsFake;
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

        // 初始化打击特效管理器（对象池）
        m_hitEffect = GetComponent<HitEffectManager>();
        if (m_hitEffect == null)
            m_hitEffect = gameObject.AddComponent<HitEffectManager>();

        // 初始化方体动画播放器
        m_chartPlayback = gameObject.GetComponent<ChartPlaybackController>();
        if (m_chartPlayback == null)
            m_chartPlayback = gameObject.AddComponent<ChartPlaybackController>();
        m_chartPlayback.SetAudioSource(m_audioSource);
        if (m_cubeManager != null)
            m_chartPlayback.SetCubeParent(m_cubeManager.transform);

        // 加载谱面数据并发现方体（编辑模式下也驱动动画）
        RefreshChartPlayback();

        // 创建 Combo 计数显示（PlayScreen 竖中线顶部）
        CreateComboDisplay();
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
        // 播放状态由谱面播放器驱动（含 offset>0 的音乐前奏等待期，此时音频尚未开始但谱面已启动）
        m_isPlaying = m_chartPlayback != null && m_chartPlayback.IsPlaying;

        if (m_isPlaying && !wasPlaying)
        {
            EnterPlaybackMode();
        }
        else if (!m_isPlaying && wasPlaying)
        {
            ExitPlaybackMode();
        }

        // 当前时间：播放时用谱面时钟（音乐偏移已在播放时钟中处理），编辑时用网格时间
        float currentTime = m_isPlaying && m_chartPlayback != null
            ? m_chartPlayback.CurrentTime
            : (m_gridManager != null ? m_gridManager.CurrentTime : 0f);

        // Display 模式：网格保持可见并跟随播放自动滚动
        if (m_isPlaying && m_keepGridDuringPlayback && m_gridManager != null)
        {
            m_gridManager.SetScrollOffsetToTime(currentTime);
        }

        // 始终缓存淡出目标（含背景变暗初始化）
        CacheFadeTargets();

        // Note 下落（播放和编辑模式都更新）
        UpdatePlaybackNotes(currentTime);

        // Combo 由时间驱动：既定播放器默认全中，combo = 当前时间前已消失的 Note 数（滚动条跳转自动重算）
        UpdateComboByTime(currentTime);

        // 始终驱动方体动画
        if (m_chartPlayback != null && m_chartPlayback.ChartData != null)
        {
            m_chartPlayback.UpdateAllCubes(currentTime);
        }

        // 保证 Combo 显示始终渲染在最上层（其他编辑层可能在 Start 后才创建）
        EnsureComboDisplayOnTop();
    }

    /// <summary>
    /// 检测谱面文件是否被修改（用户编辑锚点后触发），若修改则热重载
    /// </summary>
    private void CheckChartFileChanged()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath)) return;

        // 限频：每 0.5s 检查一次文件系统，避免每帧 File.Exists/GetLastWriteTime 系统调用
        if (Time.unscaledTime - m_lastChartCheckTime < 0.5f) return;
        m_lastChartCheckTime = Time.unscaledTime;

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
        m_chartPlayback.LoadChart(chartPath);
        m_chartPlayback.DiscoverCubes();

        if (System.IO.File.Exists(chartPath))
            m_lastChartWriteTime = System.IO.File.GetLastWriteTime(chartPath);
    }

    /// <summary>
    /// 从最新 chart.tmp 读取音乐偏移（秒），供播放按钮延迟音乐启动使用。
    /// 每次点击播放时重新读取，避免用户刚修改 offset 就立即播放时读到旧值。
    /// </summary>
    public float GetPlaybackOffsetSeconds()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath)) return 0f;

        string chartPath = System.IO.Path.Combine(EditorInit.ChartPath, "chart.tmp");
        if (!System.IO.File.Exists(chartPath)) return 0f;

        try
        {
            string json = System.IO.File.ReadAllText(chartPath);
            var data = JsonUtility.FromJson<PlaybackChartData>(json);
            return data?.info?.offset != null ? data.info.offset / 1000f : 0f;
        }
        catch (System.Exception ex)
        {
            return 0f;
        }
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

        // 背景曲绘 Image（PlayScreen 自身的 Graphic）：
        // 统一使用默认 UI 材质，保证 Graphic.color 着色（DOColor 变暗/变亮）生效
        if (m_playScreenGraphic == null)
        {
            m_playScreenGraphic = GetComponent<Graphic>();
            if (m_playScreenGraphic != null)
            {
                m_playScreenGraphic.material = Canvas.GetDefaultCanvasMaterial();
                // 编辑模式下初始变暗（放映时亮度由 Enter/ExitPlaybackMode 的 DOColor 控制）
                if (!m_isPlaying)
                {
                    m_playScreenGraphic.color = k_trackDimColor;
                }
            }
        }

        // 方体展示区 RawImage（编辑模式下变暗，与曲绘独立变量控制）
        if (m_cubeDisplayGraphic == null && m_cubeManager != null)
        {
            m_cubeDisplayGraphic = m_cubeManager.CubeDisplay;
            // 编辑模式下初始变暗
            if (m_cubeDisplayGraphic != null && !m_isPlaying)
            {
                m_cubeDisplayGraphic.color = k_cubeDimColor;
            }
        }
    }

    // ---- 放映模式进出 ----

    /// <summary>
    /// 开始播放（编辑器播放按钮入口）：播放前刷新最新 offset，再启动谱面时钟与音乐。
    /// 谱面时钟保证：offset&gt;0 时谱面从 t=0 立即滚动、音乐延后；offset&lt;0 时音乐立即播放、谱面延后。
    /// </summary>
    public void PlayMusic(float seekTime)
    {
        if (m_chartPlayback == null) return;

        // 每次播放前重新读取 offset，避免用户刚修改就播放时读到旧值
        m_chartPlayback.SetPlaybackOffsetSeconds(GetPlaybackOffsetSeconds());
        m_chartPlayback.Play(seekTime);
    }

    /// <summary>暂停播放</summary>
    public void PauseMusic()
    {
        m_chartPlayback?.Pause();
    }

    /// <summary>
    /// 设置放映时是否保留网格等编辑层（true = Display 模式：放映时网格可见并自动滚动）。
    /// 正在放映中切换模式时，即时调整网格显隐与跟随状态。
    /// </summary>
    public void SetKeepGridDuringPlayback(bool keepGrid)
    {
        if (m_keepGridDuringPlayback == keepGrid) return;

        m_keepGridDuringPlayback = keepGrid;

        // 仅当正在放映时即时生效；否则由 Enter/ExitPlaybackMode 处理
        if (!m_isPlaying) return;

        CacheFadeTargets();

        if (keepGrid)
        {
            // 进入 Display 模式：恢复网格等编辑层并开启自动滚动跟随，背景与展示区保持编辑模式的变暗状态
            m_gridGroup?.DOFade(1f, k_fadeDuration);
            m_noteLayerGroup?.DOFade(1f, k_fadeDuration);
            m_easingGroup?.DOFade(1f, k_fadeDuration);
            m_referenceLineGraphic?.DOFade(k_refLineAlpha, k_fadeDuration);
            m_playScreenGraphic?.DOColor(k_trackDimColor, k_fadeDuration);
            m_cubeDisplayGraphic?.DOColor(k_cubeDimColor, k_fadeDuration);
            m_gridManager?.SetFollowPlayback(true);
        }
        else
        {
            // 退出 Display 模式：淡出网格等编辑层并关闭跟随，曲绘保持变暗、展示区恢复全亮
            m_gridGroup?.DOFade(0f, k_fadeDuration);
            m_noteLayerGroup?.DOFade(0f, k_fadeDuration);
            m_easingGroup?.DOFade(0f, k_fadeDuration);
            m_referenceLineGraphic?.DOFade(0f, k_fadeDuration);
            m_playScreenGraphic?.DOColor(k_trackDimColor, k_fadeDuration);
            m_cubeDisplayGraphic?.DOColor(k_fullColor, k_fadeDuration);
            m_gridManager?.SetFollowPlayback(false);
        }
    }

    private void EnterPlaybackMode()
    {
        CacheFadeTargets();

        if (m_keepGridDuringPlayback)
        {
            // Display 模式：网格保持可见并开启自动滚动跟随
            m_gridManager?.SetFollowPlayback(true);
        }
        else
        {
            // 淡出网格、编辑 Note 层、标定线、缓动区
            m_gridGroup?.DOFade(0f, k_fadeDuration);
            m_noteLayerGroup?.DOFade(0f, k_fadeDuration);
            m_easingGroup?.DOFade(0f, k_fadeDuration);
            m_referenceLineGraphic?.DOFade(0f, k_fadeDuration);
        }

        // 非 Display 模式：曲绘保持变暗，展示区恢复全亮
        if (!m_keepGridDuringPlayback)
        {
            m_playScreenGraphic?.DOColor(k_trackDimColor, k_fadeDuration);
            m_cubeDisplayGraphic?.DOColor(k_fullColor, k_fadeDuration);
        }

        // 切换 CubeCamera 到放映模式（居中方体正面）
        m_cubeManager?.SetPlaybackCameraMode(true);

        // 重新加载谱面（获取最新锚点修改）
        RefreshChartPlayback();

        // 清掉编辑模式下已在下落的预览 Note，避免进入放映后重复生成导致 Combo 翻倍
        ClearPlaybackNotes();
        m_prevTime = m_chartPlayback != null ? m_chartPlayback.CurrentTime : 0f;
    }

    private void ExitPlaybackMode()
    {
        if (m_keepGridDuringPlayback)
        {
            // Display 模式结束：关闭自动滚动跟随（网格本就未淡出，保持可见）
            m_gridManager?.SetFollowPlayback(false);
        }
        else
        {
            // 恢复网格、编辑 Note 层、标定线、缓动区
            m_gridGroup?.DOFade(1f, k_fadeDuration);
            m_noteLayerGroup?.DOFade(1f, k_fadeDuration);
            m_easingGroup?.DOFade(1f, k_fadeDuration);
            m_referenceLineGraphic?.DOFade(k_refLineAlpha, k_fadeDuration);
        }

        // 非 Display 模式：退出放映后背景曲绘和展示区恢复变暗
        if (!m_keepGridDuringPlayback)
        {
            m_playScreenGraphic?.DOColor(k_trackDimColor, k_fadeDuration);
            m_cubeDisplayGraphic?.DOColor(k_cubeDimColor, k_fadeDuration);
        }

        // 恢复 CubeCamera 到编辑模式
        m_cubeManager?.SetPlaybackCameraMode(false);

        // 方体动画继续由 UpdateCubeAnimation 驱动（切换为网格时间）

        ClearPlaybackNotes();
    }

    // ---- 3D Note 下落逻辑 ----

    /// <summary>
    /// 根据面方向计算下落向量和轨道轴。
/// 下落方向 = 面中心指向棱的方向；轨道轴 = 垂直于下落方向的轴。
    /// </summary>
    private static void GetDirectionVectors(FaceDirection dir,
        out Vector3 fallingDir, out Vector3 laneAxis)
    {
        // 默认值与 Up 一致（枚举四值已全覆盖，此处保证 out 参数明确赋值）
        fallingDir = Vector3.up;
        laneAxis = Vector3.right;

        switch (dir)
        {
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
        }
    }

    /// <summary>
    /// 获取当前轨道在指定时间点的流速值（默认 30）。
    /// 流速越高，Note 下落越快，预览时间窗口越短。
    /// </summary>
    private float GetFlowSpeed(float currentTime)
    {
        if (m_cubeManager == null) return 30f;

        var cube = m_cubeManager.GetCube(m_cubeManager.ActiveCubeId);
        if (cube == null) return 30f;

        var track = cube.GetTrack(m_cubeManager.ActiveFace, m_cubeManager.ActiveDirection);
        if (track == null || track.easingSlots == null || track.easingSlots.Count < 2) return 30f;

        var slotData = track.easingSlots[1]; // 流速（第二个轨道级槽）
        if (slotData == null) return 30f;

        var config = EasingSlotConfigs.Slots[EasingSlotConfigs.CubeSlotCount + 1];
        return slotData.EvaluateAt(currentTime, config.defaultValue, config);
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

        var cubeTransform = m_cubeManager.ActiveCubeTransform;
        int cubeLayer = m_cubeManager.CubeLayer;

        // 流速影响有效预览窗口：流速越高，Note 下落越快，窗口越短（默认流速 30 时等于 k_lookAhead）
        float flowSpeed = GetFlowSpeed(currentTime);
        // 非正流速（缓动锚点被编辑为负值/0）会导致预览窗口长达数百秒、Note 堆积，回退默认流速
        if (flowSpeed <= 0f)
        {
            flowSpeed = k_defaultFlowSpeed;
        }

        float effectiveLookAhead = k_lookAhead * k_defaultFlowSpeed / flowSpeed;

        // 生成即将到达的 Note
        foreach (var note in currentNotes)
        {
            float timeUntilHit = note.time - currentTime;
            if (timeUntilHit > 0f && timeUntilHit <= effectiveLookAhead)
            {
                long key = (long)note.lane * 100000L + (long)(note.time * 1000f);
                if (!m_spawnedKeys.Contains(key))
                {
                    SpawnPlaybackNote(note, currentTime, fallingDir, laneAxis, cubeHalf, effectiveLookAhead);
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

            if (pb.IsHold)
            {
                // ---- Hold Note：头部位置 + 中间条长度 ----
                if (timeUntilHit > 0f)
                {
                    // 下落中：整体从 StartPos 移动到 EndPos
                    float progress = 1f - timeUntilHit / pb.EffectiveLookAhead;
                    pb.View.transform.localPosition = Vector3.Lerp(pb.StartPos, pb.EndPos, progress);

                    // 头部可见
                    if (pb.Renderer != null)
                        pb.Renderer.gameObject.SetActive(true);

                    // 中间条保持全长
                    float fullBody = (pb.EndTime - pb.HitTime) * pb.FallSpeed;
                    if (pb.MidTransform != null)
                    {
                        pb.MidTransform.gameObject.SetActive(true);
                        pb.MidTransform.localPosition = -pb.FallingDir * (fullBody * pb.MidPivotFactor);
                        pb.MidTransform.localScale = new Vector3(
                            pb.MidPerpScale, fullBody * pb.MidBodyFactor, 1);
                    }
                    if (pb.TailTransform != null)
                    {
                        pb.TailTransform.gameObject.SetActive(true);
                        pb.TailTransform.localPosition = -pb.FallingDir * fullBody;
                    }
                }
                else
                {
                    // 头部已到达棱：隐藏头部，中间条从头部侧逐渐消失
                    pb.View.transform.localPosition = pb.EndPos;

                    // 隐藏头部（已被棱"吞没"）
                    if (pb.Renderer != null)
                        pb.Renderer.gameObject.SetActive(false);

                    // 打击特效：Hold 持续期间在棱位置持续散射，直到 EndTime 销毁
                    // Fake Hold：不产生打击特效
                    if (!pb.IsFake && cubeTransform != null)
                        m_hitEffect?.EmitHold(cubeTransform, pb.EndPos, fallingDir, laneAxis, cubeLayer);

                    // 中间条前端固定在棱位置（EndPos），尾部逐渐靠近
                    float remaining = pb.EndTime - currentTime;
                    float bodyLength = Mathf.Max(0f, remaining * pb.FallSpeed);

                    if (pb.MidTransform != null)
                    {
                        if (bodyLength > 0.001f)
                        {
                            pb.MidTransform.gameObject.SetActive(true);
                            pb.MidTransform.localPosition = -pb.FallingDir * (bodyLength * pb.MidPivotFactor);
                            pb.MidTransform.localScale = new Vector3(
                                pb.MidPerpScale, bodyLength * pb.MidBodyFactor, 1);
                        }
                        else
                        {
                            pb.MidTransform.gameObject.SetActive(false);
                        }
                    }
                    if (pb.TailTransform != null)
                    {
                        pb.TailTransform.localPosition = -pb.FallingDir * bodyLength;
                        pb.TailTransform.gameObject.SetActive(bodyLength > 0.001f);
                    }
                }
            }
            else
            {
                // ---- 普通 Note ----
                if (timeUntilHit > 0f)
                {
                    float progress = 1f - timeUntilHit / pb.EffectiveLookAhead;
                    pb.View.transform.localPosition = Vector3.Lerp(pb.StartPos, pb.EndPos, progress);
                }
                else
                {
                    pb.View.transform.localPosition = pb.EndPos;

                    // 打击特效：命中瞬间在命中点散射橙色小方块（0.2s 消散）
                    // Fake Note：命中后不产生打击特效
                    if (!pb.HitEffectStarted)
                    {
                        pb.HitEffectStarted = true;
                        if (!pb.IsFake && cubeTransform != null)
                            m_hitEffect?.SpawnBurst(cubeTransform, pb.EndPos, fallingDir, laneAxis, cubeLayer);
                        m_playbackNotes[i] = pb;
                    }
                }
            }
        }

        // 清除已命中 Note（Hold 在 EndTime 后销毁，普通 Note 在 HitTime 后销毁）
        for (int i = m_playbackNotes.Count - 1; i >= 0; i--)
        {
            float destroyTime = m_playbackNotes[i].IsHold
                ? m_playbackNotes[i].EndTime
                : m_playbackNotes[i].HitTime;

            if (currentTime >= destroyTime)
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
    /// Hold 类型生成头+中间条+尾的三段结构，中间条长度随时长缩放。
    /// </summary>
    private void SpawnPlaybackNote(NoteJsonNode note, float currentTime,
        Vector3 fallingDir, Vector3 laneAxis, float cubeHalf, float effectiveLookAhead)
    {
        var cubeTransform = m_cubeManager.ActiveCubeTransform;
        if (cubeTransform == null) return;

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
        float noteHalf = k_fixedNoteSize * 0.5f;
        float startDist = viewHalfExtent + noteHalf + 0.05f;

        // 棱有宽度，Note 贴图中线到达棱外边缘时销毁
        var visualizer = cubeTransform.GetComponent<CubeVisualizer>();
        float edgeHalf = visualizer != null ? visualizer.EdgeThickness * 0.5f : 0f;
        Vector3 startPos = faceCenter - fallingDir * startDist + laneOffset;
        Vector3 endPos = faceCenter + fallingDir * (cubeHalf - edgeHalf) + laneOffset;

        // 解析 Note 类型
        NotePlacementManager.NoteType noteType = default;
        bool parsed = System.Enum.TryParse<NotePlacementManager.NoteType>(note.type, out noteType);
        bool isHold = parsed && noteType == NotePlacementManager.NoteType.Hold && note.endTime > 0f;
        bool isFake = note.isFake;

        if (isHold)
        {
            SpawnHoldNote(note, cubeTransform, fallingDir, startPos, endPos,
                k_fixedNoteSize, isVerticalFall, effectiveLookAhead);
            return;
        }

        // ---- 普通 Note：单个 SpriteRenderer ----
        var go = new GameObject("PB_Note3D");
        go.transform.SetParent(cubeTransform, false);
        go.layer = m_cubeManager.CubeLayer;
        go.transform.localPosition = startPos;
        go.transform.localRotation = Quaternion.Euler(0, 180, 0);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        Sprite sprite = parsed ? m_notePlacementManager?.GetNoteSprite(noteType) : null;
        sr.sprite = sprite;
        sr.color = isFake ? k_fakeColor : Color.white;

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
            EndPos = endPos,
            EffectiveLookAhead = effectiveLookAhead,
            IsFake = isFake
        });
    }

    /// <summary>
    /// 生成 Hold 类型的 3D 预览 Note：头部 + 中间条 + 尾部。
    /// 中间条长度与时长成正比，下落过程中头部先到达棱，之后中间条逐渐缩短直到尾点到达。
    /// </summary>
    private void SpawnHoldNote(NoteJsonNode note, Transform cubeTransform,
        Vector3 fallingDir, Vector3 startPos, Vector3 endPos,
        float fixedNoteSize, bool isVerticalFall, float effectiveLookAhead)
    {
        // Fake Hold：整体半透明（与编辑器一致）
        bool isFake = note.isFake;
        Color partColor = isFake ? k_fakeColor : Color.white;

        // 下落速度（单位/秒），受流速影响
        float fallDistance = (endPos - startPos).magnitude;
        float fallSpeed = fallDistance / effectiveLookAhead;

        // 中间条全长 = 时长 × 下落速度
        float duration = note.endTime - note.time;
        float fullBodyLength = duration * fallSpeed;

        // 父物体（位置 = 头部位置）
        var parent = new GameObject("PB_Hold3D");
        parent.transform.SetParent(cubeTransform, false);
        parent.layer = m_cubeManager.CubeLayer;
        parent.transform.localPosition = startPos;

        // 面向相机的基准旋转
        Quaternion faceCamera = Quaternion.Euler(0, 180, 0);

        // ---- 头部（使用 holdTailSprite，与 2D 编辑器一致）----
        Sprite headSprite = m_notePlacementManager?.HoldTailSprite;
        var headGo = CreateHoldPart(parent.transform, headSprite, fixedNoteSize, faceCamera, 10);
        headGo.transform.localPosition = Vector3.zero;
        var headSr = headGo.GetComponent<SpriteRenderer>();
        if (headSr != null) headSr.color = partColor;

        // ---- 中间条（使用 holdMidSprite，沿下落方向拉伸）----
        Sprite midSprite = m_notePlacementManager?.HoldMidSprite;
        Transform midTransform = null;
        float midPerpScale = 1f;
        float midBodyFactor = 1f;
        float midPivotFactor = 0.5f; // 默认中心 pivot

        if (midSprite != null && midSprite.pixelsPerUnit > 0 && fullBodyLength > 0.01f)
        {
            var midGo = new GameObject("Mid");
            midGo.transform.SetParent(parent.transform, false);
            midGo.layer = m_cubeManager.CubeLayer;

            var midSr = midGo.AddComponent<SpriteRenderer>();
            midSr.sprite = midSprite;
            midSr.color = partColor;
            midSr.sortingOrder = 9;
            midSr.flipY = true; // Y 翻转（与 2D 编辑器一致）

            // 缩放：垂直方向用 noteSize，沿下落方向用 bodyLength
            float midWorldWidth = midSprite.rect.width / midSprite.pixelsPerUnit;
            float midWorldHeight = midSprite.rect.height / midSprite.pixelsPerUnit;
            midPerpScale = fixedNoteSize / midWorldWidth;
            midBodyFactor = 1f / midWorldHeight; // bodyLength × midBodyFactor = Y 缩放

            // 基于 sprite pivot 计算位置偏移因子
            // flipY=true 翻转了贴图，有效 pivot 也翻转，所以用 pivotY 而非 1-pivotY
            float pivotY = midSprite.pivot.y / midSprite.rect.height;
            midPivotFactor = pivotY;

            // 旋转：垂直下落不需要额外旋转，水平下落旋转 90°
            Quaternion midRot = isVerticalFall
                ? faceCamera
                : faceCamera * Quaternion.Euler(0, 0, 90);

            midGo.transform.localRotation = midRot;
            // 位置：使中段视觉上从头部延伸到尾部
            midGo.transform.localPosition = -fallingDir * (fullBodyLength * midPivotFactor);
            midGo.transform.localScale = new Vector3(midPerpScale, fullBodyLength * midBodyFactor, 1);

            midTransform = midGo.transform;
        }

        // ---- 尾部（使用 holdHeadSprite，与 2D 编辑器一致）----
        Sprite tailSprite = m_notePlacementManager?.HoldHeadSprite;
        var tailGo = CreateHoldPart(parent.transform, tailSprite, fixedNoteSize, faceCamera, 10);
        tailGo.transform.localPosition = -fallingDir * fullBodyLength;
        var tailSr = tailGo.GetComponent<SpriteRenderer>();
        if (tailSr != null) tailSr.color = partColor;

        m_playbackNotes.Add(new PlaybackNote
        {
            View = parent,
            Renderer = headSr,
            HitTime = note.time,
            Lane = note.lane,
            StartPos = startPos,
            EndPos = endPos,
            EffectiveLookAhead = effectiveLookAhead,
            EndTime = note.endTime,
            IsHold = true,
            FallingDir = fallingDir,
            FallSpeed = fallSpeed,
            MidTransform = midTransform,
            TailTransform = tailGo.transform,
            MidPerpScale = midPerpScale,
            MidBodyFactor = midBodyFactor,
            MidPivotFactor = midPivotFactor,
            IsFake = isFake
        });
    }

    /// <summary>
    /// 创建 Hold 的头部或尾部 GameObject（带 SpriteRenderer，统一缩放）
    /// </summary>
    private GameObject CreateHoldPart(Transform parent, Sprite sprite,
        float fixedNoteSize, Quaternion rotation, int sortingOrder)
    {
        var go = new GameObject("Part");
        go.transform.SetParent(parent, false);
        go.layer = m_cubeManager.CubeLayer;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder;
        sr.flipY = true; // Y 翻转（与 2D 编辑器一致）

        if (sprite != null && sprite.pixelsPerUnit > 0)
        {
            float spriteWorldSize = sprite.rect.width / sprite.pixelsPerUnit;
            float scale = fixedNoteSize / spriteWorldSize;
            go.transform.localScale = new Vector3(scale, scale, 1);
        }

        go.transform.localRotation = rotation;
        return go;
    }

    /// <summary>
    /// 清除所有放映 Note 并终止 DOTween 动画。
    /// 供 NotePlacementManager 在 Note 移动/删除后调用，强制重新生成 3D 预览。
    /// </summary>
    public void ClearPlaybackNotes()
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
        m_hitEffect?.ClearAll();
    }

    // ---- Combo 计数显示 ----

    /// <summary>
    /// 在 PlayScreen 竖中线顶部创建 Combo 计数显示：
    /// 第一行是 Combo 数字，第二行是 COMBO 五个字母，均使用 combo SDF 字体。
    /// </summary>
    private void CreateComboDisplay()
    {
        if (transform.Find(k_comboNumberName) != null || transform.Find(k_comboLabelName) != null) return;

        // 数字（第一行）
        m_comboNumberText = CreateComboText(k_comboNumberName, -k_comboTopMargin, k_comboNumberFontSize);

        // COMBO 标签（第二行，位于数字下方）
        float labelTop = -(k_comboTopMargin + k_comboNumberFontSize + k_comboLabelGap);
        m_comboLabelText = CreateComboText(k_comboLabelName, labelTop, k_comboLabelFontSize);
        m_comboLabelText.text = "COMBO";

        UpdateComboDisplay();
    }

    /// <summary>
    /// 创建单个 Combo TMP 文本：锚定在 PlayScreen 顶部水平居中（竖中线顶部），
    /// topOffset 为文本顶部相对 PlayScreen 顶部的偏移（负值向下）。
    /// </summary>
    private TextMeshProUGUI CreateComboText(string objectName, float topOffset, float fontSize)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.layer = LayerConstants.Ui;

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, topOffset);
        rect.sizeDelta = new Vector2(400f, 120f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = GetComboFont();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Top;
        text.color = Color.white;
        text.outlineColor = k_comboOutlineColor;
        text.outlineWidth = k_comboOutlineWidth;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.text = "0";

        go.transform.SetAsLastSibling();
        return text;
    }

    /// <summary>
    /// 获取 combo SDF 字体资源（优先加载项目中已有的 combo SDF 资产；
    /// 资产图集无效时回退为从源字体重新创建，确保显示正常）
    /// </summary>
    private TMP_FontAsset GetComboFont()
    {
        if (m_comboFont != null) return m_comboFont;

#if UNITY_EDITOR
        // 编辑器下直接按路径加载已制作好的 combo SDF 字体资源
        TMP_FontAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_comboFontAssetPath);
        if (asset != null && !IsFontAtlasValid(asset))
        {
            // 图集无效（0 尺寸/无贴图）：从源字体重新创建动态字体资产
            var sourceFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(k_comboSourceFontPath);
            if (sourceFont != null)
            {
                asset = TMP_FontAsset.CreateFontAsset(sourceFont);
            }
        }
        m_comboFont = asset;
#endif

        // 加载失败时返回 null，TMP 会回退到默认字体，不影响显示
        return m_comboFont;
    }

    /// <summary>
    /// 检查字体资产图集是否有效（无效时需回退重建）
    /// </summary>
    private static bool IsFontAtlasValid(TMP_FontAsset fontAsset)
    {
        if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0) return false;

        Texture2D atlas = fontAsset.atlasTextures[0];
        return atlas != null && atlas.width > 0 && atlas.height > 0;
    }

    /// <summary>
    /// 保证 Combo 显示始终在 PlayScreen 最上层渲染
    /// </summary>
    private void EnsureComboDisplayOnTop()
    {
        if (m_comboNumberText == null || m_comboLabelText == null) return;

        Transform root = transform;
        if (root.childCount < 2) return;

        int lastIndex = root.childCount - 1;
        // 两个 Combo 文本应占据最后两个兄弟位置；仅当其他层在其后创建时才需重排
        if (m_comboNumberText.transform.GetSiblingIndex() < lastIndex - 1
            || m_comboLabelText.transform.GetSiblingIndex() < lastIndex - 1)
        {
            m_comboLabelText.transform.SetAsLastSibling();
            m_comboNumberText.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 按时间驱动 Combo：既定播放器默认每个键都被精准击中，
    /// combo = 当前时间之前已到达击打时间（已消失）的非 Fake Note 数量。
    /// 滚动条跳转时时间变化，combo 随之重算，无需特殊重置。
    /// </summary>
    private void UpdateComboByTime(float currentTime)
    {
        if (m_notePlacementManager == null) return;

        var notes = m_notePlacementManager.GetCurrentNotes();
        int count = 0;
        foreach (var note in notes)
        {
            // Fake Note 不计入 Combo；Hold 只按开头击打时间计一次
            if (note.isFake || note.time > currentTime) continue;
            count++;
        }

        if (m_comboCount != count)
        {
            m_comboCount = count;
            UpdateComboDisplay();
        }
    }

    private void UpdateComboDisplay()
    {
        if (m_comboNumberText != null)
        {
            m_comboNumberText.text = m_comboCount.ToString();
        }
    }

    private void OnDisable()
    {
        ClearPlaybackNotes();
    }
}
