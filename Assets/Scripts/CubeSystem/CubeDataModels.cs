using System;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 单个 Note 数据（方体轨道内的 Note）
/// </summary>
[Serializable]
public class CubeNoteData
{
    /// <summary>Note 类型字符串（对应 NoteType 枚举：Click/Flick/Drag/ReverseFlick/Hold）</summary>
    public string type;

    /// <summary>轨道索引（对应 GridManager 的 lane）</summary>
    public int lane;

    /// <summary>时间（秒）</summary>
    public float time;

    /// <summary>Hold 类型专用：结束时间（非 Hold 类型为 0）</summary>
    public float endTime;
}

/// <summary>
/// 单条 note 轨道数据，由面+方向唯一标识。
/// 每个方体包含 24 条轨道（6面 × 4方向）。
/// 每条轨道拥有独立的轨道级缓动槽（棱偏移、流速）。
/// </summary>
[Serializable]
public class CubeNoteTrackData
{
    /// <summary>所属面（CubeFace 枚举字符串）</summary>
    public string face;

    /// <summary>方向（FaceDirection 枚举字符串）</summary>
    public string direction;

    /// <summary>该轨道内的 Note 列表</summary>
    public List<CubeNoteData> notes = new List<CubeNoteData>();

    /// <summary>
    /// 轨道级缓动数据槽（棱偏移、流速），每条轨道各自独立。
    /// 索引顺序对应 EasingSlotConfigs.Slots[CubeSlotCount..]。
    /// </summary>
    public List<EasingSlotData> easingSlots = new List<EasingSlotData>();

    /// <summary>
    /// 初始化轨道级缓动槽（2个），每个槽在 time=0 处放置不可删除的瞬时事件（初始值）。
    /// </summary>
    public void InitializeDefaultTrackEasingSlots()
    {
        easingSlots.Clear();
        for (int i = 0; i < EasingSlotConfigs.TrackSlotCount; i++)
        {
            var slotData = new EasingSlotData();
            var config = EasingSlotConfigs.Slots[EasingSlotConfigs.CubeSlotCount + i];
            slotData.bars.Add(new EasingBar(0f, 0f, config.defaultValue, config.defaultValue,
                Ease.Linear, 1f, true));
            easingSlots.Add(slotData);
        }
    }
}

/// <summary>
/// 单个方体数据，包含 24 条 note 轨道
/// </summary>
[Serializable]
public class CubeData
{
    /// <summary>方体唯一 ID</summary>
    public int cubeId;

    /// <summary>方体显示名称</summary>
    public string cubeName;

    /// <summary>方体备注（用户自定义描述）</summary>
    public string cubeNote = "";

    // ---- 方体视觉/物理属性（前 13 个为方体级，棱偏移/流速为轨道级）----

    /// <summary>方体长宽高 length_xyz：lx（百分比，100=原始大小）</summary>
    public float lengthX = 100f;
    /// <summary>方体长宽高 length_xyz：ly（百分比，100=原始大小）</summary>
    public float lengthY = 100f;
    /// <summary>方体长宽高 length_xyz：lz（百分比，100=原始大小）</summary>
    public float lengthZ = 100f;

    /// <summary>方体倾斜角 rotation_xyz：rx（度）</summary>
    public float rotationX = 0f;
    /// <summary>方体倾斜角 rotation_xyz：ry（度）</summary>
    public float rotationY = 0f;
    /// <summary>方体倾斜角 rotation_xyz：rz（度）</summary>
    public float rotationZ = 0f;

    /// <summary>方体位置 position_xyz：px（0=屏幕中心）</summary>
    public float positionX = 0f;
    /// <summary>方体位置 position_xyz：py（0=屏幕中心）</summary>
    public float positionY = 0f;
    /// <summary>方体位置 position_xyz：pz（0=摄像机平面）</summary>
    public float positionZ = 0f;

    /// <summary>方体颜色 RGBA：R (0-1)</summary>
    public float colorR = 0.9f;
    /// <summary>方体颜色 RGBA：G (0-1)</summary>
    public float colorG = 0.9f;
    /// <summary>方体颜色 RGBA：B (0-1)</summary>
    public float colorB = 0.9f;
    /// <summary>方体颜色 RGBA：A (0-1)</summary>
    public float colorA = 1f;

    /// <summary>棱偏移：中间位置的 note 距离实际中间位置有多远</summary>
    public float edgeOffset = 0f;

    /// <summary>流速：note 下落速度倍率</summary>
    public float flowSpeed = 30f;

    /// <summary>
    /// 方体级缓动数据槽（lx/ly/lz, rx/ry/rz, px/py/pz, R/G/B/A，共13个）。
    /// 棱偏移和流速属于轨道级，存储在各 CubeNoteTrackData.easingSlots 中。
    /// </summary>
    public List<EasingSlotData> easingSlots = new List<EasingSlotData>();

    /// <summary>24 条 note 轨道（6面 × 4方向）</summary>
    public List<CubeNoteTrackData> tracks = new List<CubeNoteTrackData>();

    /// <summary>
    /// 初始化 24 条空轨道（按面→方向顺序）
    /// </summary>
    public void InitializeDefaultTracks()
    {
        tracks.Clear();

        // 遍历6个面，每个面4个方向
        foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
        {
            foreach (FaceDirection dir in Enum.GetValues(typeof(FaceDirection)))
            {
                var track = new CubeNoteTrackData
                {
                    face = face.ToString(),
                    direction = dir.ToString(),
                    notes = new List<CubeNoteData>()
                };
                track.InitializeDefaultTrackEasingSlots();
                tracks.Add(track);
            }
        }
    }

    /// <summary>
    /// 初始化 13 个方体级缓动数据槽，每个槽在 time=0 处放置不可删除的瞬时事件（初始值）。
    /// </summary>
    public void InitializeDefaultEasingSlots()
    {
        easingSlots.Clear();
        for (int i = 0; i < EasingSlotConfigs.CubeSlotCount; i++)
        {
            var slotData = new EasingSlotData();
            var config = EasingSlotConfigs.Slots[i];
            slotData.bars.Add(new EasingBar(0f, 0f, config.defaultValue, config.defaultValue,
                Ease.Linear, 1f, true));
            easingSlots.Add(slotData);
        }
    }

    /// <summary>
    /// 根据面和方向获取对应的轨道数据
    /// </summary>
    public CubeNoteTrackData GetTrack(CubeFace face, FaceDirection direction)
    {
        string faceStr = face.ToString();
        string dirStr = direction.ToString();

        foreach (var track in tracks)
        {
            if (track.face == faceStr && track.direction == dirStr)
            {
                return track;
            }
        }

        return null;
    }
}