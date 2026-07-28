using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor 场景中的 Settings 按钮控制器（运行时）
/// 将 Setting 场景以 Additive 模式叠加到 Editor 上，保留编辑器全部状态
/// 将此脚本挂到 Editor 场景中的 Settings 按钮所在的 GameObject 上，
/// 并绑定按钮的 onClick 到 OpenSettings()
/// </summary>
public class EditorOpenSettings : MonoBehaviour
{
    /// <summary>
    /// 打开设置页面（Additive 叠加模式，保留编辑器状态）
    /// </summary>
    public void OpenSettings()
    {
        if (!SceneManager.GetSceneByName("Setting").isLoaded)
        {
            SceneManager.LoadScene("Setting", LoadSceneMode.Additive);
        }
    }
}
