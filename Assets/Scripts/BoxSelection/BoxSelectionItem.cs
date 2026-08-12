using UnityEngine;

namespace BoxSelection
{
    /// <summary>
    /// 便捷适配组件：挂到任意带 RectTransform 的 UI 元素上即可使其成为框选条目。
    /// 条目包围盒根据 RectTransform 的世界坐标实时换算到内容（Content）本地坐标系。
    /// 注意：应在 <see cref="BoxSelectionManager"/> 创建之后启用该组件（OnEnable 自动注册），
    /// 若管理器后创建，可调用 <see cref="RefreshRegistration"/> 补注册。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoxSelectionItem : MonoBehaviour, IBoxSelectable
    {
        [Tooltip("参与命中与框选的矩形区域（默认取自身 RectTransform）")]
        [SerializeField] private RectTransform m_rectTransform;

        [Tooltip("是否允许被选中")]
        [SerializeField] private bool m_isSelectable = true;

        private BoxSelectionManager m_manager;
        private object m_data;
        private readonly Vector3[] m_corners = new Vector3[4];

        /// <summary>条目在内容本地坐标中的包围盒（实时计算）。</summary>
        public Rect SelectionBounds => ComputeSelectionBounds();

        /// <summary>当前是否允许被选中。</summary>
        public bool IsSelectable => m_isSelectable;

        /// <summary>宿主业务数据（通过 <see cref="SetData"/> 写入）。</summary>
        public object Data => m_data;

        /// <summary>当前是否处于选中状态。</summary>
        public bool IsSelected => m_manager != null && m_manager.Contains(this);

        /// <summary>写入宿主业务数据。</summary>
        public void SetData(object data)
        {
            m_data = data;
        }

        /// <summary>设置是否允许被选中。</summary>
        public void SetSelectable(bool selectable)
        {
            m_isSelectable = selectable;
        }

        #region Unity 生命周期

        private void Awake()
        {
            if (m_rectTransform == null)
            {
                m_rectTransform = GetComponent<RectTransform>();
            }
        }

        private void OnEnable()
        {
            m_manager = BoxSelectionManager.TryGet(out BoxSelectionManager mgr) ? mgr : null;
            m_manager?.RegisterItem(this);
        }

        private void OnDisable()
        {
            if (m_manager != null)
            {
                m_manager.UnregisterItem(this);
                m_manager = null;
            }
        }

        #endregion

        /// <summary>
        /// 管理器创建之后（或条目对象被重新启用后）调用本方法补注册。
        /// </summary>
        public void RefreshRegistration()
        {
            OnDisable();
            OnEnable();
        }

        /// <summary>
        /// 将自身世界坐标四角换算为内容本地坐标，取 AABB 作为包围盒。
        /// </summary>
        private Rect ComputeSelectionBounds()
        {
            RectTransform content = m_manager != null ? m_manager.Content : null;
            if (content == null || m_rectTransform == null) return new Rect();

            Camera camera = m_manager.UiCamera;
            m_rectTransform.GetWorldCorners(m_corners);

            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            for (int i = 0; i < m_corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, m_corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, camera, out Vector2 local))
                {
                    continue;
                }

                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            if (min.x > max.x || min.y > max.y) return new Rect();
            return new Rect(min, max - min);
        }
    }
}
