using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
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

        // 将 chart.json 复制为 chart.tmp，此后所有编辑操作基于 .tmp，Save 时才覆写回 .json
        CopyChartToTemp();

        // 设置背景图片
        LoadIllustration();
        
        // 初始化网格系统
        InitializeGridSystem();
        
        // 异步加载音频
        StartCoroutine(LoadAudioClip());
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
        GameObject.Find("PlayScreen").GetComponent<Image>().sprite = 
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
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