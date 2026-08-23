using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 保存提示弹窗（Toast）：在屏幕右下角弹出提示并自动淡出。
/// 通过静态方法 Show() 调用，内部自动创建独立的高层级 Overlay Canvas，不依赖场景现有 UI。
/// </summary>
public class SaveToastNotifier : MonoBehaviour
{
    // 动画时间轴（秒）：淡入 -> 停留 -> 淡出
    private const float k_fadeInDuration = 0.15f;
    private const float k_holdDuration = 1.2f;
    private const float k_fadeOutDuration = 0.4f;

    // 距屏幕右下角的边距与弹窗尺寸
    private const float k_margin = 24f;
    private const float k_width = 200f;
    private const float k_height = 56f;

    private static SaveToastNotifier s_instance;

    // 中文字体缓存（black.ttf 为动态字体，可按需渲染中文字符）
    private static TMP_FontAsset s_chineseFont;

    private CanvasGroup m_group;
    private RectTransform m_toastRect;
    private TextMeshProUGUI m_label;
    private float m_elapsed;
    private bool m_isShowing;

    /// <summary>
    /// 在屏幕右下角弹出提示。弹窗显示期间重复调用会更新文本并重置动画。
    /// </summary>
    public static void Show(string message)
    {
        if (s_instance == null)
        {
            var go = new GameObject("SaveToastNotifier");
            go.layer = LayerConstants.Ui;
            s_instance = go.AddComponent<SaveToastNotifier>();
        }

        s_instance.ShowInternal(message);
    }

    private void Awake()
    {
        // 独立 Overlay Canvas：排序层级接近最大值，确保提示始终位于所有 UI 之上
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        BuildToast();
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    /// <summary>
    /// 构建弹窗 UI：半透明深色背景 + 居中文本，锚定屏幕右下角
    /// </summary>
    private void BuildToast()
    {
        var toastGo = new GameObject("SaveToast", typeof(RectTransform));
        toastGo.layer = LayerConstants.Ui;
        toastGo.transform.SetParent(transform, false);
        m_toastRect = (RectTransform)toastGo.transform;

        // 锚点与轴心均位于右下角，向屏幕内侧偏移一个边距
        m_toastRect.anchorMin = new Vector2(1f, 0f);
        m_toastRect.anchorMax = new Vector2(1f, 0f);
        m_toastRect.pivot = new Vector2(1f, 0f);
        m_toastRect.anchoredPosition = new Vector2(-k_margin, k_margin);
        m_toastRect.sizeDelta = new Vector2(k_width, k_height);

        var background = toastGo.AddComponent<Image>();
        background.color = new Color(0.13f, 0.16f, 0.13f, 0.92f);
        background.raycastTarget = false;

        // CanvasGroup 控制整体透明度动画，同时禁用射线拦截避免挡住编辑操作
        m_group = toastGo.AddComponent<CanvasGroup>();
        m_group.alpha = 0f;
        m_group.blocksRaycasts = false;
        m_group.interactable = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.layer = LayerConstants.Ui;
        textGo.transform.SetParent(toastGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        m_label = textGo.AddComponent<TextMeshProUGUI>();
        m_label.font = GetChineseFont();
        m_label.fontSize = 26f;
        m_label.alignment = TextAlignmentOptions.Center;
        m_label.color = new Color(0.72f, 0.93f, 0.72f, 1f);
        m_label.raycastTarget = false;
        m_label.text = string.Empty;
    }

    /// <summary>
    /// 显示提示：更新文本并将动画时间归零重新播放
    /// </summary>
    private void ShowInternal(string message)
    {
        m_label.text = message;
        m_elapsed = 0f;
        m_group.alpha = 0f;
        m_toastRect.anchoredPosition = new Vector2(-k_margin, k_margin);
        m_isShowing = true;
    }

    private void Update()
    {
        if (!m_isShowing) return;

        // 使用 unscaledDeltaTime，保证任何 Time.timeScale 下动画时长一致
        m_elapsed += Time.unscaledDeltaTime;

        if (m_elapsed < k_fadeInDuration)
        {
            // 淡入阶段
            m_group.alpha = m_elapsed / k_fadeInDuration;
        }
        else if (m_elapsed < k_fadeInDuration + k_holdDuration)
        {
            // 停留阶段：完全可见
            m_group.alpha = 1f;
        }
        else if (m_elapsed < k_fadeInDuration + k_holdDuration + k_fadeOutDuration)
        {
            // 淡出阶段：透明度下降并轻微上移
            float t = (m_elapsed - k_fadeInDuration - k_holdDuration) / k_fadeOutDuration;
            m_group.alpha = 1f - t;
            m_toastRect.anchoredPosition = new Vector2(-k_margin, k_margin + 12f * t);
        }
        else
        {
            // 动画结束：隐藏并停止更新（保留实例供下次复用）
            m_group.alpha = 0f;
            m_isShowing = false;
        }
    }

    /// <summary>
    /// 获取支持中文的动态 TMP 字体（与 InfoManagerUI 的加载方式一致）
    /// </summary>
    private static TMP_FontAsset GetChineseFont()
    {
        if (s_chineseFont != null) return s_chineseFont;

        var sourceFont = Resources.Load<Font>("Fonts/black");
        if (sourceFont == null) return null;

        s_chineseFont = TMP_FontAsset.CreateFontAsset(sourceFont);
        return s_chineseFont;
    }
}
