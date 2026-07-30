using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 方体3D可视化器：创建并管理正方体的12条棱和6个面。
/// 使用 Unlit/CubeUnlit shader：不受光照影响、双面渲染、恒为白色。
/// - 12条棱：100% 不透明白色
/// - 6个面：alpha 80% 白色
/// 颜色和透明度后续可扩展调整。
/// </summary>
public class CubeVisualizer : MonoBehaviour
{
    [Header("棱设置")]
    [Tooltip("棱的粗细（世界单位）")]
    [SerializeField] private float m_edgeThickness = 0.02f;

    [Tooltip("棱的颜色（默认不透明白）")]
    [SerializeField] private Color m_edgeColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    [Header("面设置")]
    [Tooltip("面的颜色（默认 80% 透明白）")]
    [SerializeField] private Color m_faceColor = new Color(0.9f, 0.9f, 0.9f, 0.4f);

    [Header("方体尺寸")]
    [Tooltip("方体边长（世界单位）")]
    [SerializeField] private float m_cubeSize = 1f;

    // ---- 内部引用 ----
    private GameObject m_edgesContainer;
    private GameObject m_facesContainer;
    private Material m_edgeMaterial;
    private Material m_faceMaterial;
    private readonly List<GameObject> m_edgeObjects = new List<GameObject>();
    private readonly List<GameObject> m_faceObjects = new List<GameObject>();

    /// <summary>所属方体的 CubeId</summary>
    public int CubeId { get; private set; } = -1;

    /// <summary>
    /// 初始化可视化器，创建棱和面的 GameObject
    /// </summary>
    /// <param name="cubeId">所属方体 ID</param>
    public void Initialize(int cubeId)
    {
        CubeId = cubeId;
        CreateMaterials();
        CreateEdges();
        CreateFaces();
    }

    /// <summary>
    /// 创建棱和面的材质（使用 Unlit/CubeUnlit shader：不受光照影响、双面渲染、支持透明）
    /// </summary>
    private void CreateMaterials()
    {
        Shader unlitShader = Shader.Find("Unlit/CubeUnlit");
        if (unlitShader == null)
        {
            Debug.LogError($"[{GetType().Name}] 未找到 Unlit/CubeUnlit shader，回退到 Unlit/Texture");
            unlitShader = Shader.Find("Unlit/Texture");
        }

        // 棱材质：不透明白
        m_edgeMaterial = new Material(unlitShader);
        m_edgeMaterial.name = $"EdgeMat_Cube{CubeId}";
        m_edgeMaterial.color = m_edgeColor;

        // 面材质：80% 透明白
        m_faceMaterial = new Material(unlitShader);
        m_faceMaterial.name = $"FaceMat_Cube{CubeId}";
        m_faceMaterial.color = m_faceColor;
    }

    /// <summary>
    /// 创建12条棱（使用细长方体）
    /// </summary>
    private void CreateEdges()
    {
        m_edgesContainer = new GameObject("Edges");
        m_edgesContainer.transform.SetParent(transform, false);

        float half = m_cubeSize * 0.5f;
        float t = m_edgeThickness;

        // 正方体8个顶点
        Vector3[] corners =
        {
            new Vector3(-half, -half, -half), // v0 bottom-back-left
            new Vector3( half, -half, -half), // v1 bottom-back-right
            new Vector3( half, -half,  half), // v2 bottom-front-right
            new Vector3(-half, -half,  half), // v3 bottom-front-left
            new Vector3(-half,  half, -half), // v4 top-back-left
            new Vector3( half,  half, -half), // v5 top-back-right
            new Vector3( half,  half,  half), // v6 top-front-right
            new Vector3(-half,  half,  half)  // v7 top-front-left
        };

        // 12条棱的顶点对
        int[][] edgePairs =
        {
            // 底面4条
            new[] {0, 1}, new[] {1, 2}, new[] {2, 3}, new[] {3, 0},
            // 顶面4条
            new[] {4, 5}, new[] {5, 6}, new[] {6, 7}, new[] {7, 4},
            // 竖直4条
            new[] {0, 4}, new[] {1, 5}, new[] {2, 6}, new[] {3, 7}
        };

        for (int i = 0; i < edgePairs.Length; i++)
        {
            Vector3 p1 = corners[edgePairs[i][0]];
            Vector3 p2 = corners[edgePairs[i][1]];
            CreateEdgeObject(i, p1, p2, t);
        }
    }

    /// <summary>
    /// 创建单条棱的 GameObject
    /// </summary>
    private void CreateEdgeObject(int index, Vector3 p1, Vector3 p2, float thickness)
    {
        Vector3 center = (p1 + p2) * 0.5f;
        Vector3 direction = p2 - p1;
        float length = direction.magnitude;

        var edgeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        edgeGo.name = $"Edge_{index}";
        edgeGo.transform.SetParent(m_edgesContainer.transform, false);
        edgeGo.transform.localPosition = center;

        // 根据方向设置缩放（沿 X/Y/Z 轴之一拉伸）
        if (Mathf.Abs(direction.x) > 0.01f)
        {
            edgeGo.transform.localScale = new Vector3(length, thickness, thickness);
        }
        else if (Mathf.Abs(direction.y) > 0.01f)
        {
            edgeGo.transform.localScale = new Vector3(thickness, length, thickness);
        }
        else
        {
            edgeGo.transform.localScale = new Vector3(thickness, thickness, length);
        }

        // 赋予棱材质
        var renderer = edgeGo.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = m_edgeMaterial;

        m_edgeObjects.Add(edgeGo);
    }

    /// <summary>
    /// 创建6个面（使用 Quad，法线朝外）
    /// </summary>
    private void CreateFaces()
    {
        m_facesContainer = new GameObject("Faces");
        m_facesContainer.transform.SetParent(transform, false);

        float half = m_cubeSize * 0.5f;

        // 6个面的配置：位置、旋转（使 Quad 法线朝外）
        FaceConfig[] faceConfigs =
        {
            new FaceConfig(CubeFace.Up,    new Vector3(0,  half, 0),  new Vector3(-90, 0, 0)),
            new FaceConfig(CubeFace.Down,  new Vector3(0, -half, 0),  new Vector3(90, 0, 0)),
            new FaceConfig(CubeFace.Left,  new Vector3(-half, 0, 0),  new Vector3(0, 90, 0)),
            new FaceConfig(CubeFace.Right, new Vector3( half, 0, 0),  new Vector3(0, -90, 0)),
            new FaceConfig(CubeFace.Front, new Vector3(0, 0,  half),  new Vector3(0, 0, 0)),
            new FaceConfig(CubeFace.Back,  new Vector3(0, 0, -half),  new Vector3(0, 180, 0))
        };

        foreach (var config in faceConfigs)
        {
            CreateFaceObject(config);
        }
    }

    /// <summary>
    /// 创建单个面的 GameObject
    /// </summary>
    private void CreateFaceObject(FaceConfig config)
    {
        var faceGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceGo.name = $"Face_{config.face}";
        faceGo.transform.SetParent(m_facesContainer.transform, false);
        faceGo.transform.localPosition = config.position;
        faceGo.transform.localEulerAngles = config.rotation;
        faceGo.transform.localScale = Vector3.one * m_cubeSize;

        // 赋予面材质
        var renderer = faceGo.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = m_faceMaterial;

        m_faceObjects.Add(faceGo);
    }

    /// <summary>
    /// 设置棱颜色（供后续扩展调用）
    /// </summary>
    public void SetEdgeColor(Color color)
    {
        m_edgeColor = color;
        if (m_edgeMaterial != null)
        {
            m_edgeMaterial.color = color;
        }
    }

    /// <summary>
    /// 设置面颜色和透明度（供后续扩展调用）
    /// </summary>
    public void SetFaceColor(Color color)
    {
        m_faceColor = color;
        if (m_faceMaterial != null)
        {
            m_faceMaterial.color = color;
        }
    }

    /// <summary>
    /// 设置方体边长
    /// </summary>
    public void SetCubeSize(float size)
    {
        m_cubeSize = size;
        // 重建几何体
        ClearVisuals();
        CreateEdges();
        CreateFaces();
    }

    /// <summary>
    /// 清理所有可视化对象（运行时使用 Destroy，延迟到帧末销毁）
    /// </summary>
    private void ClearVisuals()
    {
        if (m_edgesContainer != null)
        {
            Destroy(m_edgesContainer);
            m_edgesContainer = null;
        }

        if (m_facesContainer != null)
        {
            Destroy(m_facesContainer);
            m_facesContainer = null;
        }

        m_edgeObjects.Clear();
        m_faceObjects.Clear();
    }

    private void OnDestroy()
    {
        // 清理创建的材质实例
        if (m_edgeMaterial != null)
        {
            Destroy(m_edgeMaterial);
        }

        if (m_faceMaterial != null)
        {
            Destroy(m_faceMaterial);
        }
    }

    /// <summary>
    /// 面配置结构体（内部使用）
    /// </summary>
    private struct FaceConfig
    {
        public readonly CubeFace face;
        public readonly Vector3 position;
        public readonly Vector3 rotation;

        public FaceConfig(CubeFace face, Vector3 position, Vector3 rotation)
        {
            this.face = face;
            this.position = position;
            this.rotation = rotation;
        }
    }
}