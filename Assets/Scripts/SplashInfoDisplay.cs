using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexMap
{
    /// <summary>
    /// 在 Splash 场景左下角显示当前版本号和自动更新进度。
    /// 运行时自动创建 TextMeshProUGUI 文本元素并锚定到画布左下角，
    /// 订阅 AutoUpdateManager 的事件实时更新显示内容。
    /// </summary>
    public class SplashInfoDisplay : MonoBehaviour
    {
        // --- 显示常量 ---
        private const string k_objName = "SplashInfoText";
        private const float k_margin = 16f;
        private const float k_fontSize = 16f;
        private const float k_maxWidth = 400f;

        [Header("字体")]
        [Tooltip("用于显示版本号与更新状态的中文字体（TMP Font Asset），留空时回退到 Resources/Fonts/black")]
        [SerializeField] private TMP_FontAsset m_fontAsset;

        // --- 运行时组件 ---
        private TextMeshProUGUI m_infoText;
        private TMP_FontAsset m_font;
        private AutoUpdateManager m_updateManager;

        // 已加载的字符集，避免每次 TryAddCharacters 重复操作
        private bool m_fontInitialized;

        private void Start()
        {
            // 确保场景中存在 AutoUpdateManager，若不存在则自动创建
            m_updateManager = AutoUpdateManager.Instance;
            if (m_updateManager == null)
            {
                var go = new GameObject("AutoUpdateManager");
                m_updateManager = go.AddComponent<AutoUpdateManager>();
            }

            CreateInfoText();
            SubscribeEvents();
            UpdateDisplayText();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// 在场景 Canvas 的左下角创建版本信息文本
        /// </summary>
        private void CreateInfoText()
        {
            var canvas = GetCanvasInScene();
            if (canvas == null)
            {
                Debug.LogError("[SplashInfoDisplay] 场景中未找到 Canvas，无法创建信息文本", this);
                return;
            }

            var go = new GameObject(k_objName, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.layer = LayerConstants.Ui;

            var rect = go.GetComponent<RectTransform>();

            // 锚定左下角
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(k_margin, k_margin);
            rect.sizeDelta = new Vector2(k_maxWidth, 60f);

            m_infoText = go.AddComponent<TextMeshProUGUI>();
            m_infoText.fontSize = k_fontSize;
            m_infoText.color = new Color(0.85f, 0.85f, 0.85f, 0.8f);
            m_infoText.alignment = TextAlignmentOptions.BottomLeft;
            m_infoText.richText = true;
            m_infoText.font = GetChineseFont();
        }

        /// <summary>
        /// 订阅自动更新管理器的事件
        /// </summary>
        private void SubscribeEvents()
        {
            if (m_updateManager == null) return;

            m_updateManager.StatusTextChanged += HandleStatusChanged;
            m_updateManager.DownloadProgressChanged += HandleDownloadProgress;
            m_updateManager.UpdateCheckCompleted += HandleCheckCompleted;
            m_updateManager.DownloadCompleted += HandleDownloadCompleted;
            m_updateManager.UpdateErrorOccurred += HandleError;
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (m_updateManager == null) return;

            m_updateManager.StatusTextChanged -= HandleStatusChanged;
            m_updateManager.DownloadProgressChanged -= HandleDownloadProgress;
            m_updateManager.UpdateCheckCompleted -= HandleCheckCompleted;
            m_updateManager.DownloadCompleted -= HandleDownloadCompleted;
            m_updateManager.UpdateErrorOccurred -= HandleError;
        }

        // --- 事件回调 ---

        private void HandleStatusChanged(string status)
        {
            UpdateDisplayText();
        }

        private void HandleDownloadProgress(float progress, long downloaded, long total)
        {
            UpdateDisplayText();
        }

        private void HandleCheckCompleted(bool hasUpdate, string latestVersion, string currentVersion)
        {
            UpdateDisplayText();
        }

        private void HandleDownloadCompleted(string path)
        {
            UpdateDisplayText();
        }

        private void HandleError(string error)
        {
            UpdateDisplayText();
        }

        /// <summary>
        /// 根据当前更新状态刷新显示文本
        /// </summary>
        private void UpdateDisplayText()
        {
            if (m_infoText == null) return;

            string versionLine = $"v{AppVersion.CurrentVersion}";

            if (m_updateManager == null)
            {
                m_infoText.text = versionLine;
                return;
            }

            string updateLine;

            switch (m_updateManager.State)
            {
                case AutoUpdateManager.UpdateState.Idle:
                    updateLine = $"<color=#888888>就绪</color>";
                    break;

                case AutoUpdateManager.UpdateState.Checking:
                    updateLine = "<color=#AAAAAA>正在检查更新…</color>";
                    break;

                case AutoUpdateManager.UpdateState.UpToDate:
                    updateLine = "<color=#66AA66>已是最新版本</color>";
                    break;

                case AutoUpdateManager.UpdateState.UpdateAvailable:
                    updateLine = $"<color=#CCAA44>发现新版本 v{m_updateManager.LatestVersion}</color>";
                    break;

                case AutoUpdateManager.UpdateState.Downloading:
                    var percent = Mathf.RoundToInt(m_updateManager.Progress * 100f);
                    updateLine = $"<color=#6688CC>正在下载更新… {percent}%</color>";
                    break;

                case AutoUpdateManager.UpdateState.DownloadComplete:
                    updateLine = "<color=#66AA66>下载完成，请重启以应用更新</color>";
                    break;

                case AutoUpdateManager.UpdateState.Error:
                    updateLine = "<color=#AA6666>更新失败</color>";
                    break;

                default:
                    updateLine = string.Empty;
                    break;
            }

            // 确保字体包含所有需要显示的字符
            EnsureFontCharacters(updateLine);

            m_infoText.text = $"{versionLine}\n{updateLine}";
        }

        /// <summary>
        /// 确保字体资源包含当前显示文本所需的所有字符
        /// </summary>
        private void EnsureFontCharacters(string text)
        {
            if (m_font == null || m_fontInitialized) return;

            // 收集所有非富文本标签字符
            var chars = StripRichTextTags(text);
            m_font.TryAddCharacters(chars + $"v{AppVersion.CurrentVersion}就绪正在检查更新…已是最新版本发现新版本，但暂无可下载的构建产物正在下载更新%下载完成，请重启以应用更新更新失败");
            m_fontInitialized = true;
        }

        /// <summary>
        /// 移除富文本标签，返回纯文本内容
        /// </summary>
        private static string StripRichTextTags(string text)
        {
            var result = new System.Text.StringBuilder(text.Length);
            var inTag = false;

            foreach (var c in text)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (!inTag) result.Append(c);
            }

            return result.ToString();
        }

        /// <summary>
        /// 定位场景中的 Canvas：优先自身层级，其次场景内查找
        /// </summary>
        private Canvas GetCanvasInScene()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }

            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var candidate in canvases)
            {
                if (candidate.gameObject.scene == gameObject.scene)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取中文字体：优先使用 Inspector 配置的 TMP 字体资产，
        /// 否则回退为从 Resources 加载动态字体（仅当未配置时）
        /// </summary>
        private TMP_FontAsset GetChineseFont()
        {
            if (m_font != null)
            {
                return m_font;
            }

            // 优先使用 Inspector 中配置的 TMP 字体资产（推荐，字符集完整）
            if (m_fontAsset != null)
            {
                m_font = m_fontAsset;
                return m_font;
            }

            // 回退方案：从 Resources 加载源字体并动态创建 TMP 字体
            var sourceFont = Resources.Load<Font>("Fonts/black");
            if (sourceFont == null)
            {
                return null;
            }

            m_font = TMP_FontAsset.CreateFontAsset(sourceFont);
            return m_font;
        }
    }
}
