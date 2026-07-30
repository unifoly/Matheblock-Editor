using System;
using System.Collections.Generic;

/// <summary>
/// 单个 Note 数据（方体轨道内的 Note）
/// </summary>
[Serializable]
public class CubeNoteData
{
    /// <summary>Note 类型字符串（对应 NoteType 枚举：Click/Flick/Drag/ReverseFlick）</summary>
    public string type;

    /// <summary>轨道索引（对应 GridManager 的 lane）</summary>
    public int lane;

    /// <summary>时间（秒）</summary>
    public float time;
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