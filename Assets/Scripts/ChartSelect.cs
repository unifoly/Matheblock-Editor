using System;
using System.IO;
using DG.Tweening;
using Timers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;

public class ChartSelect : MonoBehaviour
{
    /// <summary>
    /// 当前活动的 ChartSelect 单例引用，用于 EditorInit 等脚本直接获取
    /// 替代不可靠的 GameObject.Find("PortalHandler")
    /// </summary>
    public static ChartSelect Instance { get; private set; }

    public GameObject Content;
    public GameObject ChartButton;
    public GameObject NewChartPanel;
    public GameObject MusicPathDisplay;
    public GameObject IllustrationPathDisplay;
    public GameObject SecondPanel;
    
    public string SelectMusic;
    
    private string m_folderPath;
    private bool m_isActive;
    private bool m_isClicked;
    private bool m_isExecute;
    private bool m_isInit;
    private RectTransform m_contentRect;
    private ImageTypeManager m_imageTypeManager;
    private FileBrowserManager m_fileBrowserManager;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        m_imageTypeManager = new ImageTypeManager();
        m_fileBrowserManager = new FileBrowserManager();
        m_contentRect = Content.GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (m_isActive && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseNewChartPanel();
        }
    }

    private void ResetExecuteFlag()
    {
        m_isExecute = false;
    }

    private void ReadFolder()
    {
        m_folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps");
        
        if (!Directory.Exists(m_folderPath))
        {
            Directory.CreateDirectory(m_folderPath);
            return;
        }

        var direction = new DirectoryInfo(m_folderPath);
        var folders = direction.GetDirectories("*", SearchOption.TopDirectoryOnly);

        foreach (var folder in folders)
        {
            CreateChartButton(folder.FullName);
        }
    }

    private void CreateChartButton(string folderPath)
    {
        var chartButton = Instantiate(ChartButton);
        var contentTransform = Content.transform.Find("Scroll View/Viewport/Content");
        chartButton.transform.SetParent(contentTransform);
        chartButton.transform.localScale = Vector3.one;

        // 设置封面图片（允许缺失 illustration.png）
        try
        {
            var illustrationPath = Path.Combine(folderPath, "illustration.png");
            if (File.Exists(illustrationPath))
            {
                var tex = m_imageTypeManager.GetTextureByString(
                    m_imageTypeManager.SetImageToString(illustrationPath));
                chartButton.transform.GetChild(0).GetComponent<Image>().sprite = 
                    Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (Exception ex)
        {
        }

        // 读取并解析JSON文件（单目录损坏不应中断整个列表构建）
        try
        {
            var chartPath = Path.Combine(folderPath, "chart.json");
            if (!File.Exists(chartPath))
            {
                Destroy(chartButton.gameObject);
                return;
            }

            var readData = File.ReadAllText(chartPath);
            var data = JsonUtility.FromJson<ChartData>(readData);
            if (data?.info == null)
            {
                Destroy(chartButton.gameObject);
                return;
            }

            chartButton.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text =
                $"{data.info.MusicName}\n \n{data.info.Charter}";
        }
        catch (Exception ex)
        {
            Destroy(chartButton.gameObject);
            return;
        }

        // 为每个按钮绑定独立的路径
        chartButton.GetComponent<Button>().onClick.AddListener(() => LoadEditorScene(folderPath));
    }

    public void ChartButtonClick()
    {
        if (!m_isInit)
        {
            ReadFolder();
            m_isInit = true;
        }

        if (!m_isExecute)
        {
            var targetX = m_isClicked 
                ? m_contentRect.localPosition.x + 1300 
                : m_contentRect.localPosition.x - 1300;
            
            m_contentRect.DOLocalMove(
                new Vector3(targetX, m_contentRect.localPosition.y, m_contentRect.localPosition.z), 2);
            
            m_isClicked = !m_isClicked;
            m_isExecute = true;
            TimersManager.SetTimer(this, 2f, ResetExecuteFlag);
        }
    }

    public void CloseNewChartPanel()
    {
        NewChartPanel.SetActive(false);
        m_isActive = false;
    }

    public void NewCharts()
    {
        NewChartPanel.SetActive(true);
        m_isActive = true;
    }

    public void OpenMusicFile()
    {
        var musicPath = m_fileBrowserManager.OpenFiles("音乐文件", "wav", "mp3", "ogg", "flac", "aac");
        MusicPathDisplay.GetComponent<TMP_InputField>().text = musicPath;
    }

    public void OpenIllustrationFile()
    {
        var illustrationPath = m_fileBrowserManager.OpenFiles("图像文件", "jpg", "jpeg", "bmp", "png");
        IllustrationPathDisplay.GetComponent<TMP_InputField>().text = illustrationPath;
    }

    public void ChartCreate()
    {
        m_folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps");
        var musicNameInput = SecondPanel.transform.Find("MusicName").GetChild(0).GetComponent<TMP_InputField>();
        var chartPath = Path.Combine(m_folderPath, musicNameInput.text);

        // 处理已存在的目录
        if (Directory.Exists(chartPath))
        {
            var index = 1;
            while (Directory.Exists(chartPath + index))
            {
                index++;
            }
            chartPath = chartPath + index;
        }

        Directory.CreateDirectory(chartPath);

        // 复制文件（源路径缺失时提示并中止，避免 ArgumentException 与半成品目录）
        try
        {
            var musicPath = MusicPathDisplay.GetComponent<TMP_InputField>().text;
            var illustrationPath = IllustrationPathDisplay.GetComponent<TMP_InputField>().text;

            if (string.IsNullOrEmpty(musicPath) || string.IsNullOrEmpty(illustrationPath))
            {
                Directory.Delete(chartPath);
                return;
            }

            File.Copy(musicPath, Path.Combine(chartPath, "music.mp3"));
            File.Copy(illustrationPath, Path.Combine(chartPath, "illustration.png"));
        }
        catch (Exception ex)
        {
            Directory.Delete(chartPath, true);
            return;
        }

        // 创建JSON数据
        var info = new ChartInfo
        {
            MusicName = musicNameInput.text,
            Charter = SecondPanel.transform.Find("Charter").GetChild(0).GetComponent<TMP_InputField>().text,
            Musician = SecondPanel.transform.Find("Musician").GetChild(0).GetComponent<TMP_InputField>().text,
            Illustrationer = ""
        };

        var data = new ChartData { info = info };
        var json = JsonUtility.ToJson(data);

        // 写入 chart.json，失败时清理半成品目录，避免留下缺 JSON 的目录
        try
        {
            File.WriteAllText(Path.Combine(chartPath, "chart.json"), json);
        }
        catch (Exception ex)
        {
            Directory.Delete(chartPath, true);
            return;
        }

        CloseNewChartPanel();

        // 重载选曲界面
        ReloadChartList();
    }

    private void ReloadChartList()
    {
        var father = GameObject.Find("Content");
        for (var i = 0; i < father.transform.childCount; i++)
        {
            Destroy(father.transform.GetChild(i).gameObject);
        }
        ReadFolder();
    }

    private void LoadEditorScene(string folderPath)
    {
        SelectMusic = folderPath;
        SceneManager.LoadScene(1);
    }

    /// <summary>
    /// 打开设置页面（Additive 叠加模式，不销毁选曲界面）
    /// 绑定到 Splash 场景中的 Settings 按钮 onClick
    /// </summary>
    public void OpenSettings()
    {
        if (!SceneManager.GetSceneByName("Setting").isLoaded)
        {
            SceneManager.LoadScene("Setting", LoadSceneMode.Additive);
        }
    }

    [Serializable]
    private class ChartInfo
    {
        public string MusicName;
        public string Charter;
        public string Illustrationer;
        public string Musician;
    }

    [Serializable]
    private class ChartData
    {
        public ChartInfo info;
    }
}