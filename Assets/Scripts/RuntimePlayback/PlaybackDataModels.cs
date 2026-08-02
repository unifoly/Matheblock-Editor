using System;
using System.Collections.Generic;
using DG.Tweening;

namespace RuntimePlayback
{
    // ---- JSON 数据模型（字段名与编辑器格式完全一致，确保 JsonUtility 兼容）----

    [Serializable]
    public class PlaybackEasingBar
    {
        public float startTime;
        public float endTime;
        public float startValue;
        public float endValue;
        public Ease easingType = Ease.Linear;
        public float weight = 1f;
        public bool isInstant = false;
    }

    [Serializable]
    public class PlaybackEasingSlot
    {
        public List<PlaybackEasingBar> bars = new List<PlaybackEasingBar>();
    }

    [Serializable]
    public class PlaybackNoteData
    {
        public string type;
        public int lane;
        public float time;
        public float endTime;
        public bool isFake;
    }

    [Serializable]
    public class PlaybackTrackData
    {
        public string face;
        public string direction;
        public List<PlaybackNoteData> notes = new List<PlaybackNoteData>();
        public List<PlaybackEasingSlot> easingSlots = new List<PlaybackEasingSlot>();
    }

    [Serializable]
    public class PlaybackCubeData
    {
        public int cubeId;
        public string cubeName;
        public string cubeNote = "";

        // 方体级视觉属性（默认值与编辑器 CubeData / EasingSlotConfigs 保持一致：100 = 原始大小）
        public float lengthX = 100f;
        public float lengthY = 100f;
        public float lengthZ = 100f;
        public float rotationX = 0f;
        public float rotationY = 0f;
        public float rotationZ = 0f;
        public float positionX = 0f;
        public float positionY = 0f;
        public float positionZ = 0f;
        public float colorR = 0.9f;
        public float colorG = 0.9f;
        public float colorB = 0.9f;
        public float colorA = 1f;
        public float edgeOffset = 0f;
        public float flowSpeed = 30f;

        // 方体级缓动槽（13个：lx~A）
        public List<PlaybackEasingSlot> easingSlots = new List<PlaybackEasingSlot>();

        // 24 条 note 轨道（含轨道级缓动槽：棱偏移、流速）
        public List<PlaybackTrackData> tracks = new List<PlaybackTrackData>();
    }

    [Serializable]
    public class PlaybackBpmNode
    {
        public float time;
        public float bpm;
    }

    /// <summary>
    /// 谱面 info 信息（仅读取播放所需的字段，其余字段由 JsonUtility 忽略）
    /// </summary>
    [Serializable]
    public class PlaybackChartInfo
    {
        // 音乐偏移（毫秒）：音乐快了该时长，播放时延后以对齐谱面
        public float offset;
    }

    [Serializable]
    public class PlaybackChartData
    {
        public PlaybackChartInfo info;
        public List<PlaybackBpmNode> bpmNodes;
        public List<PlaybackCubeData> cubes;
    }

    // ---- 槽位配置（镜像编辑器 EasingSlotConfigs）----

    [Serializable]
    public struct PlaybackSlotConfig
    {
        public float defaultValue;
        public float minValue;
        public float maxValue;

        public PlaybackSlotConfig(float def, float min, float max)
        {
            defaultValue = def; minValue = min; maxValue = max;
        }
    }

    /// <summary>
    /// 15 个数据槽配置，与编辑器 EasingSlotConfigs 一致。
    /// 前 13 个为方体级，后 2 个为轨道级。
    /// </summary>
    public static class PlaybackSlotConfigs
    {
        public const int CubeSlotCount = 13;
        public const int TrackSlotCount = 2;
        public const int SlotCount = CubeSlotCount + TrackSlotCount;

        public static readonly PlaybackSlotConfig[] Slots =
        {
            // lx, ly, lz（百分比，100=原始大小）
            new PlaybackSlotConfig(100f, 0f, 200f),
            new PlaybackSlotConfig(100f, 0f, 200f),
            new PlaybackSlotConfig(100f, 0f, 200f),
            // rx, ry, rz
            new PlaybackSlotConfig(0f, -360f, 360f),
            new PlaybackSlotConfig(0f, -360f, 360f),
            new PlaybackSlotConfig(0f, -360f, 360f),
            // px, py（0=屏幕中心）；pz（0=摄像机平面）
            new PlaybackSlotConfig(0f, -10f, 10f),
            new PlaybackSlotConfig(0f, -10f, 10f),
            new PlaybackSlotConfig(0f, -10f, 10f),
            // R, G, B, A
            new PlaybackSlotConfig(0.9f, 0f, 1f),
            new PlaybackSlotConfig(0.9f, 0f, 1f),
            new PlaybackSlotConfig(0.9f, 0f, 1f),
            new PlaybackSlotConfig(1f, 0f, 2f),
            // 棱偏移, 流速
            new PlaybackSlotConfig(0f, -1f, 1f),
            new PlaybackSlotConfig(30f, 0f, 60f)
        };
    }
}
