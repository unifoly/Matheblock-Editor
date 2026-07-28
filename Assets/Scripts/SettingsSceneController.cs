using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexMap
{
    /// <summary>
    /// Settings 场景控制器
    /// 放在 Settings.unity 场景中任意 GameObject 上
    /// 支持从 Splash（选曲界面）和 Editor（编辑器）两个场景进入，
    /// 返回时自动回到来源场景。
    /// Editor 进入时以 Additive 模式叠加，编辑器状态完整保留。
    /// </summary>
    public class SettingsSceneController : MonoBehaviour
    {
        [Header("返回按钮（可选，设计师可拖入）")]
        [SerializeField] private UnityEngine.UI.Button m_backButton;

        [Header("返回选曲界面按钮（可选，从编辑器进入时可用）")]
        [SerializeField] private UnityEngine.UI.Button m_backToSplashButton;

        private bool m_originIsSplash;

        private void Start()
        {
            DetectOriginScene();

            if (m_backButton != null)
            {
                m_backButton.onClick.AddListener(GoBack);
            }

            if (m_backToSplashButton != null)
            {
                // 只有从编辑器进入时，才显示"返回选曲"按钮
                m_backToSplashButton.gameObject.SetActive(!m_originIsSplash);
                m_backToSplashButton.onClick.AddListener(GoToSplash);
            }
        }

        private void Update()
        {
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
