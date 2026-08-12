using UnityEngine;

namespace BoxSelection
{
    /// <summary>
    /// 可被框选系统选中的条目契约。
    /// 坐标契约：<see cref="SelectionBounds"/> 必须位于"内容节点（Content）的本地坐标系"中，
    /// 即与
    /// <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)"/>
    /// 作用于 Content 时返回的坐标同空间。
    /// </summary>
    public interface IBoxSelectable
    {
        /// <summary>
        /// 条目在内容本地坐标中的包围盒（AABB），用于点击命中与框选相交判定。
        /// </summary>
        Rect SelectionBounds { get; }

        /// <summary>
        /// 当前是否允许被选中（false 时点击与框选都会跳过该条目）。
        /// </summary>
        bool IsSelectable { get; }

        /// <summary>
        /// 宿主业务数据（例如 (槽, 索引) 或谱面对象），用于在回调与查询中识别条目。
        /// </summary>
        object Data { get; }
    }
}
