using UnityEngine;
using UnityEngine.EventSystems;

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
        if (Input.GetMouseButtonDown(0))
            Debug.Log("[EventSystemDebug] Mouse click detected!");
    }
}