using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 缓动显示名称与 DOTween Ease 枚举的映射工具。
/// 过滤掉 Unset / Flash / INTERNAL 等非标准类型，仅保留 31 种常用缓动。
/// </summary>
public static class EaseDisplayNames
{
    /// <summary>可用于下拉菜单的 Ease 值（按展示顺序排列）</summary>
    public static readonly Ease[] UsableEases =
    {
        Ease.Linear,
        Ease.InSine, Ease.OutSine, Ease.InOutSine,
        Ease.InQuad, Ease.OutQuad, Ease.InOutQuad,
        Ease.InCubic, Ease.OutCubic, Ease.InOutCubic,
        Ease.InQuart, Ease.OutQuart, Ease.InOutQuart,
        Ease.InQuint, Ease.OutQuint, Ease.InOutQuint,
        Ease.InExpo, Ease.OutExpo, Ease.InOutExpo,
        Ease.InCirc, Ease.OutCirc, Ease.InOutCirc,
        Ease.InElastic, Ease.OutElastic, Ease.InOutElastic,
        Ease.InBack, Ease.OutBack, Ease.InOutBack,
        Ease.InBounce, Ease.OutBounce, Ease.InOutBounce
    };

    private static readonly string[] k_names =
    {
        "Linear",
        "Ease In Sine", "Ease Out Sine", "Ease InOut Sine",
        "Ease In Quad", "Ease Out Quad", "Ease InOut Quad",
        "Ease In Cubic", "Ease Out Cubic", "Ease InOut Cubic",
        "Ease In Quart", "Ease Out Quart", "Ease InOut Quart",
        "Ease In Quint", "Ease Out Quint", "Ease InOut Quint",
        "Ease In Expo", "Ease Out Expo", "Ease InOut Expo",
        "Ease In Circ", "Ease Out Circ", "Ease InOut Circ",
        "Ease In Elastic", "Ease Out Elastic", "Ease InOut Elastic",
        "Ease In Back", "Ease Out Back", "Ease InOut Back",
        "Ease In Bounce", "Ease Out Bounce", "Ease InOut Bounce"
    };

    /// <summary>所有缓动类型显示名称</summary>
    public static string[] AllNames => k_names;

    /// <summary>根据 Ease 获取显示名称</summary>
    public static string GetName(Ease ease)
    {
        int idx = Array.IndexOf(UsableEases, ease);
        return idx >= 0 ? k_names[idx] : ease.ToString();
    }

    /// <summary>根据下拉菜单索引获取 Ease 值</summary>
    public static Ease GetEaseAt(int index)
    {
        return (index >= 0 && index < UsableEases.Length) ? UsableEases[index] : Ease.Linear;
    }

    /// <summary>根据 Ease 值获取下拉菜单索引</summary>
    public static int GetIndex(Ease ease)
    {
        return Array.IndexOf(UsableEases, ease);
    }
}

/// <summary>
/// 缓动长条：表示一段时间内的数值变化区间。
/// 起始时间到结束时间之间通过缓动函数从起始值过渡到结束值，
/// 长条之外的时间段数值保持不变（延续前一个长条的结束值或默认值）。
/// </summary>
[Serializable]
public class EasingBar
{
    /// <summary>起始时间（秒）</summary>
    public float startTime;

    /// <summary>结束时间（秒）</summary>
    public float endTime;

    /// <summary>起始数值（创建时默认为当前数值）</summary>
    public float startValue;

    /// <summary>结束数值（创建时默认为当前数值）</summary>
    public float endValue;

    /// <summary>长条内的缓动类型（DOTween Ease）</summary>
    public Ease easingType = Ease.Linear;

    /// <summary>缓动权重 (0=线性, 1=完整缓动, 可超过1增强效果)</summary>
    public float weight = 1f;

    /// <summary>是否为瞬时赋值事件（同一格点两次确认，始末值相同）</summary>
    public bool isInstant = false;

    public EasingBar() { }

    public EasingBar(float startTime, float endTime, float startValue, float endValue,
        Ease easingType = Ease.Linear, float weight = 1f, bool isInstant = false)
    {
        this.startTime = startTime;
        this.endTime = endTime;
        this.startValue = startValue;
        this.endValue = endValue;
        this.easingType = easingType;
        this.weight = weight;
        this.isInstant = isInstant;
    }

    public EasingBar Clone()
    {
        return new EasingBar(startTime, endTime, startValue, endValue, easingType, weight, isInstant);
    }
}

/// <summary>
/// 单个数据槽的缓动数据：包含长条列表及槽位配置（默认值/最小值/最大值）。
/// 长条表示一段时间内的数值变化，长条之外的时间段数值保持不变。
/// </summary>
[Serializable]
public class EasingSlotData
{
    /// <summary>长条列表（按起始时间升序排列）</summary>
    public List<EasingBar> bars = new List<EasingBar>();

    /// <summary>
    /// 在指定时间点根据长条和缓动函数插值求值。
    /// 无长条时返回默认值；长条之间返回前一个长条的结束值（数值不变）；
    /// 最后一个长条之后返回其结束值。
    /// 使用 DOTween 的 DOVirtual.EasedValue 进行缓动插值。
    /// </summary>
    public float EvaluateAt(float time, float defaultValue, EasingSlotConfig config)
    {
        if (bars == null || bars.Count == 0)
        {
            return defaultValue;
        }

        // 时间在第一个长条之前：返回默认值（数值不变）
        if (time < bars[0].startTime)
        {
            return defaultValue;
        }

        int last = bars.Count - 1;

        // 时间在最后一个长条之后：返回最后一个长条的结束值
        if (time > bars[last].endTime)
        {
            return bars[last].endValue;
        }

        // 遍历长条，查找所在区间
        for (int i = 0; i <= last; i++)
        {
            EasingBar bar = bars[i];

            // 在长条范围内：使用缓动函数插值
            if (time >= bar.startTime && time <= bar.endTime)
            {
                float duration = bar.endTime - bar.startTime;
                if (duration <= 0f) return bar.startValue;

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
}

/// <summary>
/// 数据槽配置：默认值、最小值、最大值
/// </summary>
[Serializable]
public struct EasingSlotConfig
{
    public float defaultValue;
    public float minValue;
    public float maxValue;

    public EasingSlotConfig(float defaultValue, float minValue, float maxValue)
    {
        this.defaultValue = defaultValue;
        this.minValue = minValue;
        this.maxValue = maxValue;
    }
}

/// <summary>
/// 15 个数据槽的配置常量，顺序与 k_slotLabels 一致。
/// lx/ly/lz=长宽高, rx/ry/rz=倾斜角, px/py/pz=位置, R/G/B/A=颜色, 棱偏移, 流速
/// </summary>
public static class EasingSlotConfigs
{
    /// <summary>15 个数据槽的配置</summary>
    public static readonly EasingSlotConfig[] Slots =
    {
        // lx, ly, lz - 方体长宽高（百分比，100=原始大小）
        new EasingSlotConfig(100f, 0f, 200f),
        new EasingSlotConfig(100f, 0f, 200f),
        new EasingSlotConfig(100f, 0f, 200f),

        // rx, ry, rz - 方体倾斜角（度）
        new EasingSlotConfig(0f, -360f, 360f),
        new EasingSlotConfig(0f, -360f, 360f),
        new EasingSlotConfig(0f, -360f, 360f),

        // px, py - 方体屏幕位置（0=屏幕中心）；pz - 深度（0=摄像机平面）
        new EasingSlotConfig(0f, -10f, 10f),
        new EasingSlotConfig(0f, -10f, 10f),
        new EasingSlotConfig(0f, -10f, 10f),

        // R, G, B - 方体颜色（0~1，默认 0.9）
        new EasingSlotConfig(0.9f, 0f, 1f),
        new EasingSlotConfig(0.9f, 0f, 1f),
        new EasingSlotConfig(0.9f, 0f, 1f),
        // A - Alpha 修改值（默认 1，可 >1 增强不透明度）
        new EasingSlotConfig(1f, 0f, 2f),

        // 棱偏移
        new EasingSlotConfig(0f, -1f, 1f),

        // 流速（默认 30）
        new EasingSlotConfig(30f, 0f, 60f)
    };

    /// <summary>方体级数据槽数量（lx~A，共13个）</summary>
    public const int CubeSlotCount = 13;

    /// <summary>轨道级数据槽数量（棱偏移、流速，共2个）</summary>
    public const int TrackSlotCount = 2;

    /// <summary>数据槽总数量（方体级 + 轨道级）</summary>
    public const int SlotCount = CubeSlotCount + TrackSlotCount;
}
