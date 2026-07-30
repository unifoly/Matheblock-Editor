using System;
using System.Collections.Generic;

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
    /// 生成轨道的唯一标识键 "Face_Direction"（如 "Up_Left"）
    /// </summary>
    public string GetTrackKey()
    {
        return $"{face}_{direction}";
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

    // ---- 方体视觉/物理属性（对应右侧 15 个数据槽）----

    /// <summary>方体长宽高 length_xyz：lx</summary>
    public float lengthX = 1f;
    /// <summary>方体长宽高 length_xyz：ly</summary>
    public float lengthY = 1f;
    /// <summary>方体长宽高 length_xyz：lz</summary>
    public float lengthZ = 1f;

    /// <summary>方体倾斜角 rotation_xyz：rx（度）</summary>
    public float rotationX = 0f;
    /// <summary>方体倾斜角 rotation_xyz：ry（度）</summary>
    public float rotationY = 0f;
    /// <summary>方体倾斜角 rotation_xyz：rz（度）</summary>
    public float rotationZ = 0f;

    /// <summary>方体位置 position_xyz：px</summary>
    public float positionX = 0f;
    /// <summary>方体位置 position_xyz：py</summary>
    public float positionY = 0f;
    /// <summary>方体位置 position_xyz：pz</summary>
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
    public float flowSpeed = 1f;

    /// <summary>
    /// 15 个数据槽的缓动数据，对应右侧缓动区的锚点编辑。
    /// 索引顺序：lx/ly/lz, rx/ry/rz, px/py/pz, R/G/B/A, 棱偏移, 流速
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
                tracks.Add(new CubeNoteTrackData
                {
                    face = face.ToString(),
                    direction = dir.ToString(),
                    notes = new List<CubeNoteData>()
                });
            }
        }
    }

    /// <summary>
    /// 初始化 15 个空缓动数据槽（无锚点，使用默认值）
    /// </summary>
    public void InitializeDefaultEasingSlots()
    {
        easingSlots.Clear();
        for (int i = 0; i < EasingSlotConfigs.SlotCount; i++)
        {
            easingSlots.Add(new EasingSlotData());
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