using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 方体管理器：负责方体的创建、选择、JSON 持久化以及3D可视化。
/// 每个方体包含6面×4方向=24条note轨道，数据存储于 chart.tmp 的 cubes 字段。
/// </summary>
public class CubeManager : MonoBehaviour
{
    [Header("方体设置")]
    [Tooltip("新方体的默认边长")]
    [SerializeField] private float m_defaultCubeSize = 1f;

    [Tooltip("多方体之间的间距（X轴方向）")]
    [SerializeField] private float m_cubeSpacing = 2f;

    [Header("Shader")]
    [Tooltip("方体渲染 Shader（Unlit/CubeUnlit）。如未指定将尝试 Shader.Find 加载。")]
    [SerializeField] private Shader m_cubeShader;

    // ---- 方体渲染层 ----
    // 专用 Layer，CubeCamera 仅渲染此层，避免方体出现在主相机背景中
    // （常量定义见 LayerConstants.Cube / LayerConstants.Ui）
    // 正交相机半高，控制方体在 RawImage 中的视觉大小
    private const float k_cameraOrthoSize = 0.8f;
    // 相机 Y 偏移：使方体顶棱对齐标定线（视口 1/4 处）
    private const float k_cameraYOffset = 0f;

    private Camera m_cubeCamera;
    private RenderTexture m_cubeRenderTexture;
    private RawImage m_cubeDisplay;

    /// <summary>方体显示 RawImage，供 PlaybackModeController 调整透明度</summary>
    public RawImage CubeDisplay => m_cubeDisplay;

    /// <summary>方体渲染层（CubeCamera 仅渲染此层）</summary>
    public int CubeLayer => LayerConstants.Cube;

    /// <summary>方体渲染相机（供外部计算视野范围）</summary>
    public Camera CubeCamera => m_cubeCamera;

    /// <summary>当前活跃方体的 Transform（供 3D Note 挂载）</summary>
    public Transform ActiveCubeTransform =>
        m_visualizers.TryGetValue(m_activeCubeId, out var v) ? v.transform : null;

    /// <summary>方体边长（供 3D Note 定位）</summary>
    public float CubeSize => m_defaultCubeSize;

    // ---- 运行时状态 ----
    private readonly List<CubeData> m_cubes = new List<CubeData>();
    private readonly Dictionary<int, CubeVisualizer> m_visualizers = new Dictionary<int, CubeVisualizer>();
    private int m_nextCubeId = 0;

    // ---- 选择状态 ----
    private int m_activeCubeId = -1;
    private CubeFace m_activeFace = CubeFace.Front;
    private FaceDirection m_activeDirection = FaceDirection.Up;

    // ---- 事件 ----
    /// <summary>方体被创建时触发</summary>
    public event Action<CubeData> CubeCreated;

    /// <summary>方体被删除时触发</summary>
    public event Action<int> CubeDeleted;

    /// <summary>选中的方体发生变化时触发（切换轨道组）</summary>
    public event Action<int> ActiveCubeChanged;

    /// <summary>当前选中的面/方向发生变化时触发</summary>
    public event Action<CubeFace, FaceDirection> ActiveTrackChanged;

    /// <summary>所有方体数据（只读访问）</summary>
    public IReadOnlyList<CubeData> Cubes => m_cubes;

    /// <summary>当前选中的方体 ID</summary>
    public int ActiveCubeId => m_activeCubeId;

    /// <summary>当前选中的面</summary>
    public CubeFace ActiveFace => m_activeFace;

    /// <summary>当前选中的方向</summary>
    public FaceDirection ActiveDirection => m_activeDirection;

    private void Start()
    {
        // 从谱面目录加载已有方体数据（EditorInit.Awake 已在所有 Start 之前运行）
        LoadCubesFromJson();

        // 如果加载后没有方体，创建默认的 Cube_1
        if (m_cubes.Count == 0)
        {
            CreateDefaultCube();
        }

        // 设置方体渲染到 PlayScreen 的 RenderTexture 显示
        SetupCubeDisplay();

        // 为所有方体创建可视化
        foreach (var cube in m_cubes)
        {
            CreateVisualizerForCube(cube);
        }

        // 确保有选中的方体
        if (m_activeCubeId < 0 && m_cubes.Count > 0)
        {
            SetActiveCube(m_cubes[0].cubeId);
        }

        // 初始定位相机对准活跃方体
        UpdateCubeCameraPosition();
    }

    private void OnDestroy()
    {
        // 释放 RenderTexture，避免重复初始化（AddComponent/重载）时泄漏
        if (m_cubeRenderTexture != null)
        {
            m_cubeRenderTexture.Release();
            Destroy(m_cubeRenderTexture);
            m_cubeRenderTexture = null;
        }
    }

    /// <summary>
    /// 创建默认初始方体 Cube_1（ID=1），仅在无已有数据时调用
    /// </summary>
    private void CreateDefaultCube()
    {
        var cubeData = new CubeData
        {
            cubeId = 1,
            cubeName = "Cube_1",
            cubeNote = ""
        };

        cubeData.InitializeDefaultTracks();
        cubeData.InitializeDefaultEasingSlots();
        m_cubes.Add(cubeData);
        m_nextCubeId = 2;
        m_activeCubeId = 1;

        SaveCubesToJson();
        Debug.Log($"[{GetType().Name}] 创建默认方体: Cube_1 (ID=1)");
    }

    /// <summary>
    /// 创建新方体，自动分配24条空轨道
    /// </summary>
    /// <returns>新创建的方体数据</returns>
    public CubeData CreateCube()
    {
        var cubeData = new CubeData
        {
            cubeId = m_nextCubeId++,
            cubeName = $"Cube_{m_nextCubeId - 1}"
        };

        cubeData.InitializeDefaultTracks();
        cubeData.InitializeDefaultEasingSlots();
        m_cubes.Add(cubeData);

        // 创建3D可视化
        CreateVisualizerForCube(cubeData);

        // 自动选中新方体
        SetActiveCube(cubeData.cubeId);

        // 保存到 JSON
        SaveCubesToJson();

        CubeCreated?.Invoke(cubeData);
        Debug.Log($"[{GetType().Name}] 创建方体: {cubeData.cubeName} (ID={cubeData.cubeId})，含 {cubeData.tracks.Count} 条轨道");

        return cubeData;
    }

    /// <summary>
    /// 删除指定方体（仅剩1个方体时禁止删除）
    /// </summary>
    public void DeleteCube(int cubeId)
    {
        // 仅剩1个方体时不允许删除
        if (m_cubes.Count <= 1)
        {
            Debug.LogWarning($"[{GetType().Name}] 仅剩1个方体，不允许删除");
            return;
        }

        int index = m_cubes.FindIndex(c => c.cubeId == cubeId);
        if (index < 0)
        {
            Debug.LogWarning($"[{GetType().Name}] 方体不存在: ID={cubeId}");
            return;
        }

        m_cubes.RemoveAt(index);

        // 销毁可视化
        if (m_visualizers.TryGetValue(cubeId, out var visualizer))
        {
            if (visualizer != null)
            {
                Destroy(visualizer.gameObject);
            }
            m_visualizers.Remove(cubeId);
        }

        // 如果删除的是当前选中的方体，切换到第一个可用的方体
        bool activeCubeChanged = false;
        if (m_activeCubeId == cubeId)
        {
            m_activeCubeId = m_cubes.Count > 0 ? m_cubes[0].cubeId : -1;
            activeCubeChanged = true;
        }

        SaveCubesToJson();
        CubeDeleted?.Invoke(cubeId);

        // 删除的是活跃方体时，通知 UI 更新高亮并重新加载轨道
        if (activeCubeChanged && m_activeCubeId > 0)
        {
            UpdateCubeCameraPosition();
            ActiveCubeChanged?.Invoke(m_activeCubeId);
        }

        Debug.Log($"[{GetType().Name}] 删除方体: ID={cubeId}");
    }

    /// <summary>
    /// 设置当前选中的方体（切换轨道组）
    /// </summary>
    public void SetActiveCube(int cubeId)
    {
        if (m_activeCubeId == cubeId) return;

        m_activeCubeId = cubeId;
        UpdateCubeCameraPosition();
        ActiveCubeChanged?.Invoke(cubeId);
        Debug.Log($"[{GetType().Name}] 选中方体: ID={cubeId}，切换轨道组");
    }

    /// <summary>
    /// 设置当前选中的面和方向（决定展示哪组note轨道）
    /// </summary>
    public void SetActiveTrack(CubeFace face, FaceDirection direction)
    {
        m_activeFace = face;
        m_activeDirection = direction;
        ActiveTrackChanged?.Invoke(face, direction);
        Debug.Log($"[{GetType().Name}] 选中轨道: {face}_{direction}");
    }

    /// <summary>
    /// 获取当前选中方体的当前选中轨道数据
    /// </summary>
    public CubeNoteTrackData GetActiveTrack()
    {
        var cube = GetCube(m_activeCubeId);
        return cube?.GetTrack(m_activeFace, m_activeDirection);
    }

    /// <summary>
    /// 根据 ID 获取方体数据
    /// </summary>
    public CubeData GetCube(int cubeId)
    {
        return m_cubes.Find(c => c.cubeId == cubeId);
    }

    /// <summary>
    /// 为方体创建3D可视化 GameObject
    /// </summary>
    private void CreateVisualizerForCube(CubeData cubeData)
    {
        var cubeGo = new GameObject($"Cube_{cubeData.cubeId}");
        cubeGo.transform.SetParent(transform, false);
        cubeGo.layer = LayerConstants.Cube;

        // 多方体沿 X 轴排列
        cubeGo.transform.localPosition = new Vector3(cubeData.cubeId * m_cubeSpacing, 0, 0);

        var visualizer = cubeGo.AddComponent<CubeVisualizer>();
        visualizer.Initialize(cubeData.cubeId, LayerConstants.Cube, m_cubeShader);

        m_visualizers[cubeData.cubeId] = visualizer;
    }

    // ---- 方体 RenderTexture 显示 ----

    /// <summary>
    /// 创建 CubeCamera + RenderTexture + RawImage，将3D方体渲染到 PlayScreen 中（曲绘上方、网格下方）
    /// </summary>
    private void SetupCubeDisplay()
    {
        var playScreenObj = GameObject.Find("PlayScreen");
        if (playScreenObj == null)
        {
            Debug.LogError($"[{GetType().Name}] 未找到 PlayScreen，无法设置方体显示");
            return;
        }

        var playScreenRect = playScreenObj.GetComponent<RectTransform>();

        // 创建 RenderTexture（与 PlayScreen 尺寸一致，保证正确宽高比；最小 1x1 避免无效尺寸）
        int texWidth = Mathf.Max(1, Mathf.RoundToInt(playScreenRect.rect.width));
        int texHeight = Mathf.Max(1, Mathf.RoundToInt(playScreenRect.rect.height));
        m_cubeRenderTexture = new RenderTexture(texWidth, texHeight, 24, RenderTextureFormat.ARGB32);

        // 创建 CubeCamera（正交，仅渲染方体层，透明背景）
        var cameraGo = new GameObject("CubeCamera");
        m_cubeCamera = cameraGo.AddComponent<Camera>();
        m_cubeCamera.clearFlags = CameraClearFlags.SolidColor;
        m_cubeCamera.backgroundColor = new Color(0, 0, 0, 0);
        m_cubeCamera.orthographic = true;
        m_cubeCamera.orthographicSize = k_cameraOrthoSize;
        m_cubeCamera.cullingMask = 1 << LayerConstants.Cube;
        m_cubeCamera.targetTexture = m_cubeRenderTexture;
        m_cubeCamera.depth = 100;

        // 从主相机中移除方体层，避免方体出现在编辑器背景中
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.cullingMask &= ~(1 << LayerConstants.Cube);
        }

        // 创建 RawImage 插入 PlayScreen 第一个子物体位置（曲绘之上、网格之下）
        var displayGo = new GameObject("CubeDisplay", typeof(RectTransform));
        displayGo.transform.SetParent(playScreenRect, false);
        displayGo.transform.SetAsFirstSibling();
        displayGo.layer = LayerConstants.Ui;

        var displayRect = displayGo.GetComponent<RectTransform>();
        displayRect.anchorMin = Vector2.zero;
        displayRect.anchorMax = Vector2.one;
        displayRect.offsetMin = Vector2.zero;
        displayRect.offsetMax = Vector2.zero;

        m_cubeDisplay = displayGo.AddComponent<RawImage>();
        m_cubeDisplay.texture = m_cubeRenderTexture;
        m_cubeDisplay.raycastTarget = false;

        Debug.Log($"[{GetType().Name}] 方体 RenderTexture 显示已设置: {texWidth}x{texHeight}");
    }

    /// <summary>
    /// 更新 CubeCamera 位置，跟随当前活跃方体
    /// </summary>
    private void UpdateCubeCameraPosition()
    {
        if (m_cubeCamera == null) return;

        var cube = GetCube(m_activeCubeId);
        if (cube == null) return;

        float cubeX = cube.cubeId * m_cubeSpacing;
        m_cubeCamera.transform.position = new Vector3(cubeX, k_cameraYOffset, 0);
        m_cubeCamera.orthographicSize = k_cameraOrthoSize;
    }

    /// <summary>
    /// 切换放映模式相机：居中方体正面，收紧视野
    /// </summary>
    public void SetPlaybackCameraMode(bool enabled)
    {
        if (m_cubeCamera == null) return;

        var cube = GetCube(m_activeCubeId);
        if (cube == null) return;

        float cubeX = cube.cubeId * m_cubeSpacing;

        if (enabled)
        {
            // 居中方体，收紧视野以充满画面
            m_cubeCamera.transform.position = new Vector3(cubeX, 0, 0);
            m_cubeCamera.orthographicSize = 0.8f;
        }
        else
        {
            // 恢复编辑模式：方体顶棱对齐标定线
            m_cubeCamera.transform.position = new Vector3(cubeX, k_cameraYOffset, 0);
            m_cubeCamera.orthographicSize = k_cameraOrthoSize;
        }
    }

    // ---- JSON 持久化 ----

    // chart.tmp 的数据结构（与 NotePlacementManager / BpmManagerUI 保持一致，
    // 额外包含 cubes 字段；读取时保留 info / bpmNodes / notes 等已有字段）
    [Serializable]
    private class ChartJsonInfo
    {
        public string MusicName;
        public string Charter;
        public string Illustrationer;
        public string Musician;
        // 音乐偏移（毫秒），保留 InfoManagerUI 写入的字段，避免方体保存时丢失
        public float offset;
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
        public List<CubeData> cubes;
    }

    /// <summary>
    /// 获取 chart.tmp 的完整路径
    /// </summary>
    private string GetTmpJsonPath()
    {
        if (string.IsNullOrEmpty(EditorInit.ChartPath))
        {
            Debug.LogWarning($"[{GetType().Name}] EditorInit.ChartPath 为空，无法定位谱面数据文件");
            return null;
        }

        return Path.Combine(EditorInit.ChartPath, "chart.tmp");
    }

    /// <summary>
    /// 将方体数据保存到 chart.tmp（保留 info、bpmNodes、notes 等其他字段）
    /// </summary>
    public void SaveCubesToJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath)) return;

        try
        {
            // 读取现有 chart.tmp，保留 info / bpmNodes / notes 等字段
            ChartJsonData data;
            if (File.Exists(tmpPath))
            {
                string json = File.ReadAllText(tmpPath);
                data = JsonUtility.FromJson<ChartJsonData>(json) ?? new ChartJsonData();
            }
            else
            {
                data = new ChartJsonData();
            }

            // 只替换 cubes 字段
            data.cubes = new List<CubeData>(m_cubes.Count);
            foreach (var cube in m_cubes)
            {
                data.cubes.Add(cube);
            }

            string jsonStr = JsonUtility.ToJson(data, true);
            File.WriteAllText(tmpPath, jsonStr);

            Debug.Log($"[{GetType().Name}] 保存 {m_cubes.Count} 个方体到 chart.tmp");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 保存方体数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 chart.tmp 加载方体数据
    /// </summary>
    private void LoadCubesFromJson()
    {
        var tmpPath = GetTmpJsonPath();
        if (string.IsNullOrEmpty(tmpPath) || !File.Exists(tmpPath))
        {
            Debug.Log($"[{GetType().Name}] 无已有方体数据，将在创建时初始化");
            return;
        }

        try
        {
            string json = File.ReadAllText(tmpPath);
            var data = JsonUtility.FromJson<ChartJsonData>(json);

            if (data?.cubes != null && data.cubes.Count > 0)
            {
                m_cubes.Clear();
                m_cubes.AddRange(data.cubes);

                // 更新下一个可用的 ID，并补全缺失的缓动数据槽
                foreach (var cube in m_cubes)
                {
                    if (cube.cubeId >= m_nextCubeId)
                    {
                        m_nextCubeId = cube.cubeId + 1;
                    }

                    // 兼容旧数据：若无缓动槽则初始化
                    if (cube.easingSlots == null || cube.easingSlots.Count == 0)
                    {
                        cube.InitializeDefaultEasingSlots();
                    }

                    // 兼容旧数据：若无轨道则初始化24条轨道（含轨道级缓动槽）
                    if (cube.tracks == null || cube.tracks.Count == 0)
                    {
                        cube.InitializeDefaultTracks();
                    }
                    else
                    {
                        // 确保每条轨道有缓动槽
                        foreach (var track in cube.tracks)
                        {
                            if (track.easingSlots == null || track.easingSlots.Count == 0)
                            {
                                track.InitializeDefaultTrackEasingSlots();
                            }
                        }
                    }
                }

                // 默认选中第一个方体
                m_activeCubeId = m_cubes[0].cubeId;

                Debug.Log($"[{GetType().Name}] 从 chart.tmp 加载 {m_cubes.Count} 个方体");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{GetType().Name}] 加载方体数据失败: {ex.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        // 退出时自动保存
        SaveCubesToJson();
    }
}