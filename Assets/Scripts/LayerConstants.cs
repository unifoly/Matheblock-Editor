/// <summary>
/// 场景 Layer 常量集中定义，避免魔法数字散落各处导致难以维护。
/// </summary>
public static class LayerConstants
{
    /// <summary>默认 Layer（编号 0）：普通游戏对象 / 粒子特效等默认渲染层</summary>
    public const int Default = 0;

    /// <summary>UI Layer：编辑器场景内所有 UI 元素专用层（默认编号 5）</summary>
    public const int Ui = 5;

    /// <summary>方体渲染 Layer：CubeCamera 仅渲染此层，主相机剔除该层（默认编号 8）</summary>
    public const int Cube = 8;
}
