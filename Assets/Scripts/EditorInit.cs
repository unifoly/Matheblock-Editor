using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EditorInit : MonoBehaviour
{
    public static string ChartPath;

    public AudioSource music;
    
    private string m_infoDir;
    private TextMeshProUGUI m_txtDisplayer;
    private GameObject m_portalHandler;
    private GameObject m_initHandler;
    private ImageTypeManager m_imageTypeManager;

    private void Awake()
    {
        // 缓存组件引用，避免协程中重复查找
        m_txtDisplayer = GameObject.Find("Time").GetComponent<TextMeshProUGUI>();
        m_portalHandler = GameObject.Find("PortalHandler");
        m_initHandler = GameObject.Find("InitManager");
        m_imageTypeManager = new ImageTypeManager();
        
        // 获取谱面路径 — 优先使用静态单例引用，备选 GameObject.Find
        var chartSelect = ChartSelect.Instance;
        if (chartSelect == null)
        {
            chartSelect = m_portalHandler?.GetComponent<ChartSelect>();
        }
        
        m_infoDir = chartSelect != null ? chartSelect.SelectMusic : string.Empty;
        ChartPath = m_infoDir;

        // 初始化撤回/重做系统（清空上一谱面的历史，绑定场景按钮）
        UndoRedoManager.Clear();
        UndoRedoManager.Initialize();

        // 将 chart.json 复制为 chart.tmp，此后所有编辑操作基于 .tmp，Save 时才覆写回 .json
        CopyChartToTemp();

        // 设置背景图片
        LoadIllustration();
        
        // 初始化网格系统
        InitializeGridSystem();
        
        // 异步加载音频
        StartCoroutine(LoadAudioClip());
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
            UndoRedoManager.ReloadShortcuts();
        }
    }

    private void Update()
    {
        // 每帧轮询撤回/重做快捷键（Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z）
        UndoRedoManager.ProcessKeyboardShortcuts();
    }

    private void InitializeGridSystem()
    {
        var playScreen = GameObject.Find("PlayScreen");
        if (playScreen == null)
        {
            Debug.LogError("PlayScreen not found in the scene!");
            return;
        }

        if (playScreen.GetComponent<GridManager>() == null)
        {
            playScreen.AddComponent<GridManager>();
        }

        if (playScreen.GetComponent<GridScrollHandler>() == null)
        {
            playScreen.AddComponent<GridScrollHandler>();
        }

        // 左半 Note 放置管理器：Q/E/R 切换 Click/Flick/Drag
        if (playScreen.GetComponent<NotePlacementManager>() == null)
        {
            playScreen.AddComponent<NotePlacementManager>();
        }

        // 右半缓动函数区管理器：15 条竖线数据槽 + 锚点编辑 + 曲线可视化 + 水平滚动
        if (playScreen.GetComponent<EasingAreaManager>() == null)
        {
            playScreen.AddComponent<EasingAreaManager>();
        }

        // 锚点编辑面板 UI：选中锚点后在 FunctionChanger 位置弹出编辑面板
        if (playScreen.GetComponent<AnchorPointEditorUI>() == null)
        {
            playScreen.AddComponent<AnchorPointEditorUI>();
        }

        // 放映模式控制器：播放时淡出网格、DOTween 驱动 Note 下落
        if (playScreen.GetComponent<PlaybackModeController>() == null)
        {
            playScreen.AddComponent<PlaybackModeController>();
        }
    }

    /// <summary>
    /// 将 chart.json 复制为 chart.tmp，作为编辑期间的临时工作副本
    /// </summary>
    private void CopyChartToTemp()
    {
        var chartPath = Path.Combine(m_infoDir, "chart.json");
        var tmpPath = Path.Combine(m_infoDir, "chart.tmp");

        if (File.Exists(chartPath))
        {
            File.Copy(chartPath, tmpPath, overwrite: true);
        }
        else
        {
            // chart.json 不存在时创建一个空的 tmp
            File.WriteAllText(tmpPath, "{}");
        }
    }

    private void OnApplicationQuit()
    {
        if (string.IsNullOrEmpty(m_infoDir))
        {
            return;
        }

        var tmpPath = Path.Combine(m_infoDir, "chart.tmp");
        if (File.Exists(tmpPath))
        {
            File.Delete(tmpPath);
            Debug.Log($"[{GetType().Name}] 已清理临时文件: {tmpPath}");
        }
    }

    private void LoadIllustration()
    {
        var illustrationPath = Path.Combine(m_infoDir, "illustration.png");
        var tex = m_imageTypeManager.GetTextureByString(
            m_imageTypeManager.SetImageToString(illustrationPath));

        // 预模糊曲绘纹理，替代场景中的 GrabPass Blur shader
        var blurredTex = CreateBlurredTexture(tex);
        GameObject.Find("PlayScreen").GetComponent<Image>().sprite =
            Sprite.Create(blurredTex, new Rect(0, 0, blurredTex.width, blurredTex.height), new Vector2(0.5f, 0.5f));

        // 禁用场景中的 Blur GameObject（已由预模糊纹理替代）
        var blurObj = GameObject.Find("PlayScreen")?.transform.Find("Blur");
        if (blurObj != null)
        {
            blurObj.gameObject.SetActive(false);
            Debug.Log($"[{GetType().Name}] 已禁用 Blur GameObject（改用预模糊曲绘）");
        }
    }

    /// <summary>
    /// 使用双 Pass 高斯模糊 shader 预模糊纹理，替代 GrabPass Blur。
    /// </summary>
    private static Texture2D CreateBlurredTexture(Texture2D source)
    {
        if (source == null) return null;

        var blurShader = Shader.Find("Hidden/GaussianBlur");
        if (blurShader == null)
        {
            Debug.LogWarning("[EditorInit] 未找到 Hidden/GaussianBlur shader，返回原图");
            return source;
        }

        var blurMat = new Material(blurShader);
        blurMat.SetFloat("_BlurSize", 2.0f);

        // 双 Pass 高斯模糊：水平 -> 垂直
        var rt1 = RenderTexture.GetTemporary(source.width, source.height, 0);
        var rt2 = RenderTexture.GetTemporary(source.width, source.height, 0);

        Graphics.Blit(source, rt1, blurMat, 0); // Pass 0: 水平
        Graphics.Blit(rt1, rt2, blurMat, 1);    // Pass 1: 垂直

        // 读回为 Texture2D
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt2;
        var blurred = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        blurred.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        blurred.Apply();
        RenderTexture.active = prevActive;

        RenderTexture.ReleaseTemporary(rt1);
        RenderTexture.ReleaseTemporary(rt2);
        Destroy(blurMat);

        return blurred;
    }

    private IEnumerator LoadAudioClip()
    {
        var path = "file://" + Path.GetFullPath(Path.Combine(m_infoDir, "music.mp3"));
        
        using (var www = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError($"音频加载失败: {www.error}");
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(www);
            music.clip = clip;
            
            // 初始化音乐时间信息
            var musicTime = Math.Round(clip.length, 2);
            m_txtDisplayer.text = $"0.00/{musicTime}";
            MusicTimeStampController.MusicTime = musicTime;
            
            // 清理临时对象
            Destroy(m_portalHandler);
            Destroy(m_initHandler);
        }
    }
}