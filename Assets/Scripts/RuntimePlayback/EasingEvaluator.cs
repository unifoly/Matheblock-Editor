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
        /// 在指定时间点根据长条和缓动函数插值求值。
        /// 无长条时返回默认值；长条之间返回前一个长条的结束值（数值不变）；
        /// 最后一个长条之后返回其结束值。
        /// </summary>
        public static float Evaluate(List<PlaybackEasingBar> bars, float time, float defaultValue)
        {
            if (bars == null || bars.Count == 0)
                return defaultValue;

            // 时间在第一个长条之前：返回默认值（数值不变）
            if (time < bars[0].startTime)
                return defaultValue;

            int last = bars.Count - 1;

            // 时间在最后一个长条之后：返回最后一个长条的结束值
            if (time > bars[last].endTime)
                return bars[last].endValue;

            // 遍历长条，查找所在区间
            for (int i = 0; i <= last; i++)
            {
                var bar = bars[i];

                // 在长条范围内：使用缓动函数插值
                if (time >= bar.startTime && time <= bar.endTime)
                {
                    float duration = bar.endTime - bar.startTime;
                    if (duration <= 0f)
                        return bar.startValue;

                    float t = (time - bar.startTime) / duration;

                    // 权重混合：weight=0 时线性，weight=1 时完整缓动
                    float easedT = DOVirtual.EasedValue(0f, 1f, t, bar.easingType);
                    float weightedT = Mathf.Lerp(t, easedT, bar.weight);

                    return Mathf.Lerp(bar.startValue, bar.endValue, weightedT);
                }

                // 在当前长条与下一个长条之间的间隙：数值不变（延续当前长条的结束值）
                if (i < last && time > bar.endTime && time < bars[i + 1].startTime)
                {
                    return bar.endValue;
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
            if (slot == null || slot.bars == null || slot.bars.Count == 0)
                return defaultValue;

            return Evaluate(slot.bars, time, defaultValue);
        }
    }
}
