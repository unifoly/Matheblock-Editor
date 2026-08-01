using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 调试工具：诊断 EventSystem 状态与鼠标点击事件。
/// 文件名与类名保持一致（SplashDiagnose 更名），方可作为组件挂载。
/// </summary>
public class EventSystemDebug : MonoBehaviour
{
    private void Start()
    {
        var es = EventSystem.current;
        if (es == null)
            Debug.LogError("[EventSystemDebug] EventSystem.current is NULL!");
        else
            Debug.Log($"[EventSystemDebug] EventSystem OK, enabled={es.enabled}, module={es.currentInputModule?.GetType().Name}");
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
            Debug.Log("[EventSystemDebug] Mouse click detected!");
#endif
    }
}
