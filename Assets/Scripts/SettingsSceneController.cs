using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HexMap
{
    /// <summary>
    /// Settings 场景控制器
    /// 放在 Setting.unity 场景中任意 GameObject 上
    /// 支持从 Splash（选曲界面）和 Editor（编辑器）两个场景进入，
    /// 返回时自动回到来源场景。
    /// Editor 进入时以 Additive 模式叠加，编辑器状态完整保留。
    /// </summary>
    public class SettingsSceneController : MonoBehaviour
    {
        [Header("返回按钮（可选，设计师可拖入）")]
        [SerializeField] private Button m_backButton;

        [Header("返回主菜单按钮（可选；未拖入时，从编辑器进入会自动在左下角创建）")]
        [SerializeField] private Button m_backToSplashButton;

        // “返回主菜单”自动创建按钮的常量配置
        private const string k_backToSplashButtonName = "BackToMenuButton";
        private const string k_backToSplashText = "返回主菜单";
        private const float k_buttonWidth = 160f;
        private const float k_buttonHeight = 48f;
        private const float k_buttonMargin = 20f;

        private TMP_FontAsset m_chineseFont;
        private bool m_originIsSplash;

        private void Start()
        {
            DetectOriginScene();

            if (m_backButton != null)
            {
                m_backButton.onClick.AddListener(GoBack);
            }

            // 返回主菜单按钮：仅从编辑器（非 Splash 来源）进入时显示
            bool showBackToSplash = !m_originIsSplash;
            if (m_backToSplashButton != null)
            {
                // 场景中已配置按钮时，仅切换显示状态并绑定跳转
                m_backToSplashButton.gameObject.SetActive(showBackToSplash);
                m_backToSplashButton.onClick.AddListener(GoToSplash);
            }
            else if (showBackToSplash)
            {
                // 未在场景中配置时，运行时在设置页左下角自动创建
                m_backToSplashButton = CreateBackToSplashButton();
            }
        }

        private void Update()
        {
            // Setting 场景可能以 Additive 叠加在 Splash/Editor 之上，此时它并非活动场景，
            // 因此以「自身场景已加载」作为响应条件，保证设置页始终可用 Esc 关闭。
            // 底层场景（编辑器/选曲）自身的 Esc 操作（取消待定长条、取消选择等）仍会执行，
            // 但设置页覆盖其上时这些操作对用户无感，属于可接受的叠加行为。
            if (!gameObject.scene.isLoaded)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                GoBack();
            }
        }

        /// <summary>
        /// 检测当前来源场景：Splash 或 Editor
        /// </summary>
        private void DetectOriginScene()
        {
            var splashScene = SceneManager.GetSceneByName("Splash");
            m_originIsSplash = splashScene.isLoaded;
        }

        /// <summary>
        /// 返回来源场景（卸载 Settings 场景）
        /// UI 设计师可将此方法绑到 Back 按钮的 onClick 上
        /// </summary>
        public void GoBack()
        {
            SceneManager.UnloadSceneAsync("Setting");
        }

        /// <summary>
        /// 返回 Splash 选曲界面（卸载 Setting，加载 Splash 场景）
        /// 从编辑器进入设置页时，若想回到选曲界面可使用此方法
        /// </summary>
        public void GoToSplash()
        {
            // 加载 Splash 场景（Single 模式会自动卸载 Editor 和 Setting）
            SceneManager.LoadScene("Splash");
        }

        /// <summary>
        /// 返回地图编辑器（卸载 Settings 场景）
        /// 保留此方法以兼容旧版绑定
        /// </summary>
        public void BackToEditor()
        {
            GoBack();
        }

        /// <summary>
        /// 在设置页左下角自动创建“返回主菜单”按钮并绑定到 GoToSplash
        /// 优先放入左侧菜单滚动列表末尾，作为列表最后一项显示，避免浮在页面之上
        /// </summary>
        private Button CreateBackToSplashButton()
        {
            Transform parent = FindMenuContent();
            bool isInScroll = parent != null;

            if (parent == null)
            {
                // 兜底：挂到设置页 Canvas 左下角
                var canvas = GetCanvasInScene();
                if (canvas == null)
                {
                    Debug.LogWarning($"[{GetType().Name}] 未找到 Canvas 或菜单列表，无法自动创建返回主菜单按钮", this);
                    return null;
                }

                parent = canvas.transform;
            }

            var go = new GameObject(k_backToSplashButtonName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerConstants.Ui;

            var rect = go.GetComponent<RectTransform>();
            if (isInScroll)
            {
                // 菜单列表内：作为最后一项排到列表底部
                // 布局组开启 childControl* 时用 LayoutElement 的优先尺寸计算，
                // 缺少 LayoutElement 会把高度算成 0，因此必须提供
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, k_buttonHeight);

                var layoutElement = go.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = k_buttonHeight;
                layoutElement.preferredWidth = parent.GetComponent<RectTransform>().rect.width;
            }
            else
            {
                // Canvas 兜底：锚定左下角
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(k_buttonMargin, k_buttonMargin);
                rect.sizeDelta = new Vector2(k_buttonWidth, k_buttonHeight);
            }

            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // 按钮文字（铺满按钮背景）
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.layer = LayerConstants.Ui;

            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = k_backToSplashText;
            label.fontSize = 20f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.font = GetChineseFont();

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(GoToSplash);

            return button;
        }

        /// <summary>
        /// 查找左侧菜单滚动列表的 Content（菜单按钮的父节点）
        /// </summary>
        private Transform FindMenuContent()
        {
            var menuController = FindObjectOfType<SettingsMenuController>();
            return menuController != null ? menuController.MenuContent : null;
        }

        /// <summary>
        /// 定位设置页 Canvas：优先自身层级，其次本场景内的 Canvas
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
        /// 加载中文字体资源，失败时回退为 TMP 默认字体（null）
        /// </summary>
        private TMP_FontAsset GetChineseFont()
        {
            if (m_chineseFont != null)
            {
                return m_chineseFont;
            }

            var sourceFont = Resources.Load<Font>("Fonts/black");
            if (sourceFont == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 未找到 Fonts/black 字体，使用 TMP 默认字体", this);
                return null;
            }

            m_chineseFont = TMP_FontAsset.CreateFontAsset(sourceFont);
            m_chineseFont.TryAddCharacters(k_backToSplashText);
            return m_chineseFont;
        }

        private void OnDestroy()
        {
            if (m_backButton != null)
            {
                m_backButton.onClick.RemoveListener(GoBack);
            }

            if (m_backToSplashButton != null)
            {
                m_backToSplashButton.onClick.RemoveListener(GoToSplash);
            }
        }
    }
}
