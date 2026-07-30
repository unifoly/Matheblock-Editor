/// <summary>
/// 正方体的6个面，对应6组note轨道。
/// 面的命名基于 Unity 世界坐标轴方向。
/// </summary>
public enum CubeFace
{
    Up,     // 上 (Y+)
    Down,   // 下 (Y-)
    Left,   // 左 (X-)
    Right,  // 右 (X+)
    Front,  // 前 (Z+)
    Back    // 后 (Z-)
}

/// <summary>
/// 每个面的4个方向。
/// 6面 × 4方向 = 24条note轨道。
/// 方向是相对于面自身的局部坐标系定义的。
/// </summary>
public enum FaceDirection
{
    Up,     // 上
    Down,   // 下
    Left,   // 左
    Right   // 右
}

/// <summary>
/// 方体系统常量定义
/// </summary>
public static class CubeConstants
{
    /// <summary>每个方体的面数</summary>
    public const int FaceCount = 6;

    /// <summary>每个面的方向数</summary>
    public const int DirectionCount = 4;

    /// <summary>每个方体的note轨道总数 (6 × 4 = 24)</summary>
    public const int TotalTracksPerCube = FaceCount * DirectionCount;
}