using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexMap
{
    /// <summary>
    /// Editor 设置场景桥接：提供菜单入口加载/卸载 Setting 场景。
    /// </summary>
    [InitializeOnLoad]
    public static class SettingsEditorBridge
    {
        private static bool s_isSettingLoaded;

        static SettingsEditorBridge()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("HexMap/Open Settings %e", false, 100)]
        public static void ToggleSettings()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SettingsEditorBridge] 请在 Play Mode 中使用此功能。");
                return;
            }

            if (s_isSettingLoaded)
            {
                SceneManager.UnloadSceneAsync("Setting");
                s_isSettingLoaded = false;
            }
            else
            {
                SceneManager.LoadScene("Setting", LoadSceneMode.Additive);
                s_isSettingLoaded = true;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_isSettingLoaded = false;
            }
        }
    }
}
