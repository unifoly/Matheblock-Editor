using UnityEngine;
using UnityEngine.EventSystems;
using HexMap;

public class GridScrollHandler : MonoBehaviour, IScrollHandler, IEventSystemHandler
{
    // 可自定义快捷键的 Action 名
    private const string k_actionScrollUp = "Editor_ScrollUp";
    private const string k_actionScrollDown = "Editor_ScrollDown";
    private const string k_actionZoomIn = "Editor_ZoomIn";
    private const string k_actionZoomOut = "Editor_ZoomOut";

    private GridManager m_gridManager;
    private EasingAreaManager m_easingAreaManager;
    private RectTransform m_playScreenRect;

    // 从 KeyBindingsStore 加载的组合键
    private KeyCombo m_scrollUpCombo;
    private KeyCombo m_scrollDownCombo;
    private KeyCombo m_zoomInCombo;
    private KeyCombo m_zoomOutCombo;

    private void Start()
    {
        FindGridManager();
        m_easingAreaManager = GetComponent<EasingAreaManager>();
        m_playScreenRect = GetComponent<RectTransform>();
        LoadShortcuts();
    }

    /// <summary>
    /// 从 KeyBindingsStore 加载快捷键绑定
    /// </summary>
    private void LoadShortcuts()
    {
        m_scrollUpCombo = KeyBindingsStore.GetKeyCombo(k_actionScrollUp, KeyCombo.Parse("滚轮上"));
        m_scrollDownCombo = KeyBindingsStore.GetKeyCombo(k_actionScrollDown, KeyCombo.Parse("滚轮下"));
        m_zoomInCombo = KeyBindingsStore.GetKeyCombo(k_actionZoomIn, KeyCombo.Parse("Ctrl + 滚轮上"));
        m_zoomOutCombo = KeyBindingsStore.GetKeyCombo(k_actionZoomOut, KeyCombo.Parse("Ctrl + 滚轮下"));
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

        // 鼠标在右半缓动区内时，滚轮交给 OnScroll 处理水平滚动，不触发垂直滚动
        bool mouseInEasing = m_scrollUpCombo.IsWheel || m_scrollDownCombo.IsWheel
                              ? IsMouseInEasingArea(Input.mousePosition)
                              : false;

        if (!mouseInEasing)
        {
            if (m_scrollUpCombo.IsHeld())
            {
                m_gridManager.HandleScroll(-0.8f);
            }
            if (m_scrollDownCombo.IsHeld())
            {
                m_gridManager.HandleScroll(0.8f);
            }
        }

        // 键盘缩放 + 滚轮缩放
        if (m_zoomInCombo.IsPressed())
        {
            m_gridManager.HandleZoom(1f);
        }
        if (m_zoomOutCombo.IsPressed())
        {
            m_gridManager.HandleZoom(-1f);
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (m_gridManager == null) return;

        bool isCtrlHeld = Input.GetKey(KeyCode.LeftControl)
                          || Input.GetKey(KeyCode.RightControl);

        // 检测鼠标是否在右半缓动区：若是则将滚轮转为水平滚动（此行为不可自定义）
        if (!isCtrlHeld && m_easingAreaManager != null && m_playScreenRect != null
            && IsMouseInEasingArea(eventData.position))
        {
            m_easingAreaManager.ScrollHorizontal(eventData.scrollDelta.y * 40f);
            return;
        }

        // 如果滚动/缩放绑定的是滚轮，由 Update() 中的 IsHeld/IsPressed 处理（通过 Input.GetAxis）
        // OnScroll 仅处理非滚轮绑定的情况，避免双重触发
        // 实际上滚轮绑定已经在 Update 中通过 Input.GetAxis 处理了
        // 这里仅处理：当滚动绑定不是滚轮时，滚轮事件不做任何事（用户已将滚轮重绑为其他键）
    }

    /// <summary>
    /// 判断鼠标是否在右半缓动区内
    /// </summary>
    private bool IsMouseInEasingArea(Vector2 screenPosition)
    {
        if (m_gridManager == null || m_playScreenRect == null) return false;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_playScreenRect, screenPosition, null, out localPoint);

        return !m_gridManager.IsInNoteArea(localPoint.x)
               && localPoint.x <= m_playScreenRect.rect.width * 0.5f;
    }
}
