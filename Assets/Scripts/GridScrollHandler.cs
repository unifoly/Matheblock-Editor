using UnityEngine;
using UnityEngine.EventSystems;

public class GridScrollHandler : MonoBehaviour, IScrollHandler, IEventSystemHandler
{
    private GridManager m_gridManager;

    // Ctrl 键状态缓存：EventSystem 回调里 Input.GetKey 有时返回滞后值，
    // 每帧在 Update 中先缓存，OnScroll 同时读缓存和实时值进行兜底。
    private bool m_ctrlCached;

    private void Start()
    {
        FindGridManager();
    }

    private void FindGridManager()
    {
        m_gridManager = GetComponent<GridManager>();
        if (m_gridManager == null)
        {
            var playScreen = GameObject.Find("PlayScreen");
            if (playScreen != null)
            {
                m_gridManager = playScreen.GetComponent<GridManager>();
            }
        }

        if (m_gridManager == null)
        {
            Debug.LogError("GridScrollHandler: GridManager not found!");
        }
    }

    private void Update()
    {
        if (m_gridManager == null) return;

        // 缓存 Ctrl 状态供 OnScroll 使用
        m_ctrlCached = Input.GetKey(KeyCode.LeftControl)
                       || Input.GetKey(KeyCode.RightControl);

        // 键盘上下方向键滚动（与滚轮缩放/滚动的判定无关）
        if (Input.GetKey(KeyCode.UpArrow))
        {
            m_gridManager.HandleScroll(-0.1f);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            m_gridManager.HandleScroll(0.1f);
        }

        // 键盘缩放兜底：Ctrl+= / Ctrl+-（绕开 Ctrl+滚轮 在 EventSystem 中的不确定性，
        // 让用户可以验证缩放逻辑本身是否生效）
        if (m_ctrlCached)
        {
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                m_gridManager.HandleZoom(1f);
            }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                m_gridManager.HandleZoom(-1f);
            }
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (m_gridManager == null) return;

        // 双重检测：缓存值 + 实时值，任一为真则视为按下 Ctrl
        bool isCtrlHeld = m_ctrlCached
                          || Input.GetKey(KeyCode.LeftControl)
                          || Input.GetKey(KeyCode.RightControl);

        if (isCtrlHeld)
        {
            // Ctrl+滚轮：缩放线间距（实质不变，仅视觉密度）
            m_gridManager.HandleZoom(eventData.scrollDelta.y);
        }
        else
        {
            m_gridManager.HandleScroll(-eventData.scrollDelta.y);
        }
    }
}