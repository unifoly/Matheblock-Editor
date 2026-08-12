using System;
using UnityEngine;

namespace BoxSelection
{
    /// <summary>
    /// 框选系统的运行配置。所有字段均可按项目需求调整。
    /// </summary>
    [Serializable]
    public class BoxSelectionConfig
    {
        /// <summary>追加选中 / 切换选中时使用的修饰键。</summary>
        public enum AdditiveModifier
        {
            Control,
            Shift,
            Alt,
        }

        [Tooltip("框选矩形的填充颜色")]
        public Color SelectionBoxColor = new Color(0.3f, 0.8f, 1f, 0.2f);

        [Tooltip("判定为拖拽（而非点击）的屏幕像素阈值")]
        public float DragThresholdPixels = 5f;

        [Tooltip("是否需要按住 Shift 才会触发框选；关闭后任意空白拖拽都进入框选")]
        public bool RequireShiftForBoxSelect = true;

        [Tooltip("追加选中使用的修饰键（Ctrl+点击 / Ctrl+框选）")]
        public AdditiveModifier Modifier = AdditiveModifier.Control;

        [Tooltip("用于点击与框选的鼠标键：0=左键 1=右键 2=中键")]
        public int MouseButton = 0;

        /// <summary>默认配置。</summary>
        public static BoxSelectionConfig Default => new BoxSelectionConfig();
    }
}
