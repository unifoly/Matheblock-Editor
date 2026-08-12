using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace BoxSelection
{
    /// <summary>
    /// 通用框选（多选）单例管理器。
    /// 交互规则（与 Matheblock Editor 缓动区框选一致）：
    ///   - 点击条目   -> 单选（按住追加修饰键则为切换选中）
    ///   - 点击空白   -> 清空选择
    ///   - Shift+拖拽 -> 框选（Shift+追加修饰键拖拽 为追加式框选）
    ///   - 非 Shift 拖拽（超过阈值）：不消费输入，交还宿主处理（滚动 / 移动条目等）
    /// </summary>
    public sealed class BoxSelectionManager : MonoBehaviour
    {
        /// <summary>单例实例；场景内同时只允许存在一个。</summary>
        public static BoxSelectionManager Instance { get; private set; }

        /// <summary>选中集合或主选中发生变化时触发（集合非空）。</summary>
        public event Action SelectionChanged;

        /// <summary>选中集合从非空变为空时触发。</summary>
        public event Action SelectionCleared;

        private const string k_selectionBoxName = "BoxSelectionVisual";

        // ---- 绑定 ----
        private RectTransform m_content;
        private RectTransform m_interactionRect;
        private Camera m_uiCamera;
        private BoxSelectionConfig m_config;

        // ---- 条目注册表 ----
        private readonly List<IBoxSelectable> m_registered = new List<IBoxSelectable>();

        // ---- 选择状态 ----
        private readonly List<IBoxSelectable> m_selection = new List<IBoxSelectable>();
        private IBoxSelectable m_mainSelection;

        // ---- 指针交互状态 ----
        private GameObject m_boxVisual;
        private bool m_isPressed;
        private bool m_potentialClick;
        private bool m_isBoxSelecting;
        private Vector2 m_downScreenPos;
        private Vector2 m_downLocalPos;

        /// <summary>坐标基准节点；条目包围盒与框选矩形都位于其本地坐标系。</summary>
        public RectTransform Content => m_content;

        /// <summary>用于屏幕坐标转换的相机；Screen Space - Overlay 画布传 null。</summary>
        public Camera UiCamera => m_uiCamera;

        /// <summary>当前选中集合（按选中顺序排列，第 0 项即最早选中）。</summary>
        public IReadOnlyList<IBoxSelectable> Selection => m_selection;

        /// <summary>主选中条目（编辑面板的操作对象；多选时对应集合中最早选中的一项）。</summary>
        public IBoxSelectable MainSelection => m_mainSelection;

        /// <summary>当前选中数量。</summary>
        public int SelectionCount => m_selection.Count;

        /// <summary>是否存在任意选中。</summary>
        public bool HasSelection => m_selection.Count > 0;

        /// <summary>当前是否正在拖拽框选矩形。</summary>
        public bool IsBoxSelecting => m_isBoxSelecting;

        /// <summary>已注册的条目（只读）。</summary>
        public IReadOnlyList<IBoxSelectable> RegisteredItems => m_registered;

        /// <summary>
        /// 创建并初始化框选管理器（单例）。
        /// 场景内已存在实例时复用已有实例并重新绑定 Content。
        /// </summary>
        /// <param name="content">坐标基准节点（见 IBoxSelectable 的坐标契约）。</param>
        /// <param name="uiCamera">屏幕坐标转换相机；Screen Space - Overlay 画布传 null。</param>
        /// <param name="config">运行配置；传 null 使用默认配置。</param>
        /// <param name="interactionRect">鼠标交互命中区域；传 null 时使用 Content 本身。</param>
        /// <param name="parent">管理器 GameObject 的父节点。</param>
        public static BoxSelectionManager Create(
            RectTransform content,
            Camera uiCamera = null,
            BoxSelectionConfig config = null,
            RectTransform interactionRect = null,
            Transform parent = null)
        {
            if (Instance != null)
            {
                Instance.Initialize(content, uiCamera, config, interactionRect);
                return Instance;
            }

            var go = new GameObject("BoxSelectionManager");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var manager = go.AddComponent<BoxSelectionManager>();
            manager.Initialize(content, uiCamera, config, interactionRect);
            return manager;
        }

        /// <summary>尝试获取当前单例。</summary>
        public static bool TryGet(out BoxSelectionManager manager)
        {
            manager = Instance;
            return manager != null;
        }

        /// <summary>
        /// 绑定坐标基准与配置，可在运行时任意时刻重新调用以重新绑定。
        /// </summary>
        public void Initialize(
            RectTransform content,
            Camera uiCamera = null,
            BoxSelectionConfig config = null,
            RectTransform interactionRect = null)
        {
            if (m_boxVisual != null)
            {
                Destroy(m_boxVisual);
                m_boxVisual = null;
            }

            m_content = content;
            m_uiCamera = uiCamera;
            m_config = config != null ? config : BoxSelectionConfig.Default;
            m_interactionRect = interactionRect != null ? interactionRect : content;
            ResetPointerState();
        }

        #region Unity 生命周期

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (m_content == null || m_config == null) return;
            HandlePointerInput();
        }

        #endregion

        #region 条目注册

        /// <summary>注册一个可选中条目（重复注册会被忽略）。</summary>
        public void RegisterItem(IBoxSelectable item)
        {
            if (item == null || m_registered.Contains(item)) return;
            m_registered.Add(item);
        }

        /// <summary>注销条目；若该条目当前处于选中状态会同步移除。</summary>
        public void UnregisterItem(IBoxSelectable item)
        {
            if (item == null) return;
            m_registered.Remove(item);

            if (m_selection.Remove(item))
            {
                if (m_mainSelection == item)
                {
                    m_mainSelection = m_selection.Count > 0 ? m_selection[0] : null;
                }

                if (m_selection.Count == 0)
                {
                    SelectionCleared?.Invoke();
                }
                else
                {
                    SelectionChanged?.Invoke();
                }
            }
        }

        #endregion

        #region 选择 API

        /// <summary>清空所有选择；此前有选中时触发 SelectionCleared。</summary>
        public void ClearSelection()
        {
            bool hadSelection = m_selection.Count > 0;
            m_selection.Clear();
            m_mainSelection = null;

            if (hadSelection)
            {
                SelectionCleared?.Invoke();
            }
        }

        /// <summary>选中全部可选条目，最早注册的条目作为主选中。</summary>
        public void SelectAll()
        {
            bool changed = false;
            for (int i = 0; i < m_registered.Count; i++)
            {
                IBoxSelectable item = m_registered[i];
                if (!item.IsSelectable || m_selection.Contains(item)) continue;

                m_selection.Add(item);
                if (m_mainSelection == null)
                {
                    m_mainSelection = item;
                }

                changed = true;
            }

            if (changed)
            {
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>单选：清除其他选择并选中指定条目。</summary>
        public void Select(IBoxSelectable item)
        {
            if (item == null || !item.IsSelectable) return;

            m_selection.Clear();
            m_selection.Add(item);
            m_mainSelection = item;
            SelectionChanged?.Invoke();
        }

        /// <summary>追加到选择集合（已选中则忽略），并设为主选中。</summary>
        public void AddToSelection(IBoxSelectable item)
        {
            if (item == null || !item.IsSelectable || m_selection.Contains(item)) return;

            m_selection.Add(item);
            m_mainSelection = item;
            SelectionChanged?.Invoke();
        }

        /// <summary>切换指定条目的选中状态（Ctrl+点击）。</summary>
        public void ToggleSelect(IBoxSelectable item)
        {
            if (item == null || !item.IsSelectable) return;

            if (m_selection.Remove(item))
            {
                // 取消选中：若移除的是主选中，则退到集合中最早选中的一项
                if (m_mainSelection == item)
                {
                    m_mainSelection = m_selection.Count > 0 ? m_selection[0] : null;
                }

                if (m_selection.Count == 0)
                {
                    SelectionCleared?.Invoke();
                }
                else
                {
                    SelectionChanged?.Invoke();
                }
            }
            else
            {
                m_selection.Add(item);
                m_mainSelection = item;
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>从选择集合中移除指定条目。</summary>
        public void Deselect(IBoxSelectable item)
        {
            if (item == null) return;

            if (m_selection.Remove(item))
            {
                if (m_mainSelection == item)
                {
                    m_mainSelection = m_selection.Count > 0 ? m_selection[0] : null;
                }

                if (m_selection.Count == 0)
                {
                    SelectionCleared?.Invoke();
                }
                else
                {
                    SelectionChanged?.Invoke();
                }
            }
        }

        /// <summary>将指定条目设为主选中（需已在选择集合中）。</summary>
        public void SetMainSelection(IBoxSelectable item)
        {
            if (item == null || m_mainSelection == item || !m_selection.Contains(item)) return;

            m_mainSelection = item;
            SelectionChanged?.Invoke();
        }

        /// <summary>指定条目是否处于选中状态。</summary>
        public bool Contains(IBoxSelectable item)
        {
            return item != null && m_selection.Contains(item);
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理鼠标交互：区分点击、Shift+拖拽框选。
        /// 非框选拖拽（超过阈值）不消费输入，交还宿主处理。
        /// </summary>
        private void HandlePointerInput()
        {
            bool inRect = IsPointerInsideInteractionRect();

            if (IsMouseButtonDown())
            {
                if (!inRect) return;

                m_isPressed = true;
                m_potentialClick = true;
                m_downScreenPos = MouseScreenPos();
                m_downLocalPos = ScreenToContentLocal();
            }

            // 按住：超过阈值后，Shift+拖拽进入框选；其余拖拽放弃点击意图
            if (m_isPressed && !m_isBoxSelecting && m_potentialClick && IsMouseButtonHeld())
            {
                float dragDist = Vector2.Distance(MouseScreenPos(), m_downScreenPos);
                if (dragDist > m_config.DragThresholdPixels)
                {
                    if (m_config.RequireShiftForBoxSelect && IsShiftHeld())
                    {
                        StartBoxSelection();
                    }
                    else
                    {
                        // 非 Shift 拖拽：不消费，交还宿主（滚动 / 移动条目等）
                        m_potentialClick = false;
                    }
                }
            }

            // 框选进行中：实时更新矩形视觉
            if (m_isBoxSelecting)
            {
                UpdateBoxVisual(ScreenToContentLocal());
            }

            // 鼠标抬起：判定点击 / 框选
            if (IsMouseButtonUp())
            {
                if (m_isBoxSelecting)
                {
                    EndBoxSelection();
                }
                else if (m_potentialClick && inRect)
                {
                    HandleClick();
                }

                m_isPressed = false;
                m_potentialClick = false;
            }
        }

        private void StartBoxSelection()
        {
            m_isBoxSelecting = true;
            m_potentialClick = false;
            ShowBoxVisual();
            UpdateBoxVisual(m_downLocalPos);
        }

        private void EndBoxSelection()
        {
            FinalizeBoxSelection(ScreenToContentLocal(), IsAdditiveModifierHeld());
            m_isBoxSelecting = false;

            if (m_boxVisual != null)
            {
                m_boxVisual.SetActive(false);
            }
        }

        /// <summary>
        /// 完成框选：选中矩形范围内的所有条目。
        /// 非追加模式（未按住追加修饰键）先清除已有选择。
        /// </summary>
        private void FinalizeBoxSelection(Vector2 currentLocal, bool additive)
        {
            float minX = Mathf.Min(m_downLocalPos.x, currentLocal.x);
            float maxX = Mathf.Max(m_downLocalPos.x, currentLocal.x);
            float minY = Mathf.Min(m_downLocalPos.y, currentLocal.y);
            float maxY = Mathf.Max(m_downLocalPos.y, currentLocal.y);

            if (!additive)
            {
                ClearSelection();
            }

            bool anySelected = false;
            for (int i = 0; i < m_registered.Count; i++)
            {
                IBoxSelectable item = m_registered[i];
                if (!item.IsSelectable) continue;

                // AABB 相交判定
                if (item.SelectionBounds.xMin > maxX || item.SelectionBounds.xMax < minX
                    || item.SelectionBounds.yMin > maxY || item.SelectionBounds.yMax < minY)
                {
                    continue;
                }

                // 追加模式下可能重复命中已选中的条目，去重
                if (m_selection.Contains(item)) continue;

                m_selection.Add(item);
                if (!anySelected)
                {
                    // 第一个命中的条目作为主选中（编辑面板操作对象）
                    m_mainSelection = item;
                    anySelected = true;
                }
            }

            if (anySelected)
            {
                SelectionChanged?.Invoke();
            }
        }

        private void HandleClick()
        {
            IBoxSelectable hit = HitTest(ScreenToContentLocal());
            bool additive = IsAdditiveModifierHeld();

            if (hit != null)
            {
                if (additive)
                {
                    // Ctrl+点击：切换该条目的选中状态
                    ToggleSelect(hit);
                }
                else
                {
                    // 无修饰键：单选
                    Select(hit);
                }
            }
            else if (!additive)
            {
                // 点击空白：清除所有选择
                ClearSelection();
            }
        }

        /// <summary>
        /// 命中测试：返回包含给定内容本地坐标的条目。
        /// 多个条目重叠时，越靠后注册的条目视为越靠上层，优先命中。
        /// </summary>
        private IBoxSelectable HitTest(Vector2 localPos)
        {
            IBoxSelectable topmost = null;
            for (int i = 0; i < m_registered.Count; i++)
            {
                IBoxSelectable item = m_registered[i];
                if (!item.IsSelectable) continue;

                if (item.SelectionBounds.Contains(localPos))
                {
                    topmost = item;
                }
            }

            return topmost;
        }

        private bool IsPointerInsideInteractionRect()
        {
            RectTransform rect = m_interactionRect != null ? m_interactionRect : m_content;
            if (rect == null) return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, MouseScreenPos(), m_uiCamera, out Vector2 local);
            return rect.rect.Contains(local);
        }

        private Vector2 ScreenToContentLocal()
        {
            if (m_content == null) return Vector2.zero;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_content, MouseScreenPos(), m_uiCamera, out Vector2 local);
            return local;
        }

        private void ResetPointerState()
        {
            m_isPressed = false;
            m_potentialClick = false;
            m_isBoxSelecting = false;

            if (m_boxVisual != null)
            {
                m_boxVisual.SetActive(false);
            }
        }

        private bool IsAdditiveModifierHeld()
        {
            switch (m_config.Modifier)
            {
                case BoxSelectionConfig.AdditiveModifier.Shift:
                    return IsShiftHeld();
                case BoxSelectionConfig.AdditiveModifier.Alt:
                    return IsAltHeld();
                default:
                    return IsCtrlHeld();
            }
        }

        #endregion

        #region 框选视觉

        private void ShowBoxVisual()
        {
            CreateBoxVisual();
            if (m_boxVisual == null) return;

            RectTransform rect = m_boxVisual.GetComponent<RectTransform>();
            // 锚点对齐 Content 的 pivot，使 anchoredPosition 与内容本地坐标同空间
            rect.anchorMin = m_content.pivot;
            rect.anchorMax = m_content.pivot;
            m_boxVisual.SetActive(true);
            m_boxVisual.transform.SetAsLastSibling();
        }

        private void CreateBoxVisual()
        {
            if (m_boxVisual != null || m_content == null) return;

            m_boxVisual = new GameObject(k_selectionBoxName, typeof(RectTransform));
            m_boxVisual.transform.SetParent(m_content, false);
            m_boxVisual.layer = m_content.gameObject.layer;

            RectTransform rect = m_boxVisual.GetComponent<RectTransform>();
            rect.anchorMin = m_content.pivot;
            rect.anchorMax = m_content.pivot;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;

            var img = m_boxVisual.AddComponent<Image>();
            img.color = m_config.SelectionBoxColor;
            img.raycastTarget = false;

            m_boxVisual.transform.SetAsLastSibling();
            m_boxVisual.SetActive(false);
        }

        private void UpdateBoxVisual(Vector2 currentLocal)
        {
            if (m_boxVisual == null) return;

            RectTransform rect = m_boxVisual.GetComponent<RectTransform>();
            float minX = Mathf.Min(m_downLocalPos.x, currentLocal.x);
            float maxX = Mathf.Max(m_downLocalPos.x, currentLocal.x);
            float minY = Mathf.Min(m_downLocalPos.y, currentLocal.y);
            float maxY = Mathf.Max(m_downLocalPos.y, currentLocal.y);

            rect.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            rect.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        }

        #endregion

        #region 输入抽象（旧 Input / 新 Input System 双支持）

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private bool IsMouseButtonDown()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && GetButton(mouse).wasPressedThisFrame;
        }

        private bool IsMouseButtonHeld()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && GetButton(mouse).isPressed;
        }

        private bool IsMouseButtonUp()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && GetButton(mouse).wasReleasedThisFrame;
        }

        private ButtonControl GetButton(Mouse mouse)
        {
            switch (m_config.MouseButton)
            {
                case 1:
                    return mouse.middleButton;
                case 2:
                    return mouse.rightButton;
                default:
                    return mouse.leftButton;
            }
        }

        private Vector2 MouseScreenPos()
        {
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }

        private bool IsShiftHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private bool IsCtrlHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private bool IsAltHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
        }
#else
        private bool IsMouseButtonDown()
        {
            return Input.GetMouseButtonDown(m_config.MouseButton);
        }

        private bool IsMouseButtonHeld()
        {
            return Input.GetMouseButton(m_config.MouseButton);
        }

        private bool IsMouseButtonUp()
        {
            return Input.GetMouseButtonUp(m_config.MouseButton);
        }

        private Vector2 MouseScreenPos()
        {
            return Input.mousePosition;
        }

        private bool IsShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private bool IsCtrlHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private bool IsAltHeld()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
#endif

        #endregion
    }
}
