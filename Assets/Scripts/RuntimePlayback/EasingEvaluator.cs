using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace RuntimePlayback
{
    /// <summary>
    /// 缓动求值器：根据锚点列表在任意时间点插值求值。
    /// 使用 DOTween 的 DOVirtual.EasedValue 进行缓动插值，
    /// 支持权重混合（weight=0 线性, weight=1 完整缓动, 可 >1 增强）。
    /// </summary>
    public static class EasingEvaluator
    {
        /// <summary>
        /// 在指定时间点根据锚点和缓动函数插值求值。
        /// 无锚点时返回默认值；时间在首尾锚点之外时返回边界值。
        /// </summary>
        public static float Evaluate(List<PlaybackAnchorPoint> anchors, float time, float defaultValue)
        {
            if (anchors == null || anchors.Count == 0)
                return defaultValue;

            // 时间在第一个锚点之前：返回第一个锚点的值
            if (time <= anchors[0].time)
                return anchors[0].value;

            // 时间在最后一个锚点之后：返回最后一个锚点的值
            int last = anchors.Count - 1;
            if (time >= anchors[last].time)
                return anchors[last].value;

            // 查找所在区间并插值
            for (int i = 0; i < last; i++)
            {
                var curr = anchors[i];
                var next = anchors[i + 1];

                if (time >= curr.time && time <= next.time)
                {
                    float duration = next.time - curr.time;
                    if (duration <= 0f)
                        return curr.value;

                    float t = (time - curr.time) / duration;

                    // 权重混合：weight=0 时线性，weight=1 时完整缓动
                    float easedT = DOVirtual.EasedValue(0f, 1f, t, curr.easingType);
                    float weightedT = Mathf.Lerp(t, easedT, curr.weight);

                    return Mathf.Lerp(curr.value, next.value, weightedT);
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// 便捷方法：根据缓动槽和槽配置求值
        /// </summary>
        public static float EvaluateSlot(PlaybackEasingSlot slot, float time, PlaybackSlotConfig config)
        {
            float defaultValue = config.defaultValue;
            if (slot == null || slot.anchorPoints == null || slot.anchorPoints.Count == 0)
                return defaultValue;

            return Evaluate(slot.anchorPoints, time, defaultValue);
        }
    }
}
