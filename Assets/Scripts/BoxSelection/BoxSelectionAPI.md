# BoxSelection 通用框选系统 API 文档

通用矩形框选（多选）模块，从 Matheblock Editor 的缓动区框选功能独立而来，可整体拷贝到任意 Unity 项目使用。

- 适用版本：Unity 6.x（对旧版本亦兼容，无特殊 API 依赖）
- 依赖：UGUI（`UnityEngine.UI`）、旧版 Input 或新版 Input System（自动识别，见 [输入系统](#输入系统兼容)）
- 文件：`Assets/Scripts/BoxSelection/` 下的 4 个脚本 + 本文档

## 目录

1. [功能与交互规则](#功能与交互规则)
2. [坐标契约（重要）](#坐标契约重要)
3. [快速上手](#快速上手)
4. [使用 BoxSelectionItem 组件](#使用-boxselectionitem-组件)
5. [API 参考](#api-参考)
6. [事件](#事件)
7. [配置](#配置)
8. [输入系统兼容](#输入系统兼容)
9. [限制与注意事项](#限制与注意事项)

## 功能与交互规则

| 操作 | 行为 |
|---|---|
| 左键点击条目 | 单选（清除其他选择） |
| Ctrl + 点击条目 | 切换该条目的选中状态 |
| 点击空白 | 清空所有选择（按住 Ctrl 时不清空） |
| Shift + 拖拽 | 框选：选中矩形覆盖范围内的所有条目 |
| Shift + Ctrl + 拖拽 | 追加式框选：在现有选择基础上累加 |
| 非 Shift 拖拽（超过阈值） | **不消费输入**，交还宿主自行处理（滚动 / 移动条目等） |

选中语义：

- 选中集合按选中顺序排列，**主选中（MainSelection）** 为最早选中的一项，作为"编辑面板操作对象"。
- Ctrl+点击取消主选中时，主选中退到集合中最早的一项；集合清空时触发 `SelectionCleared`。
- 框选命中采用 AABB 相交判定；点击命中时重叠条目取"最后注册者"（视为最上层）。
- 追加模式（Ctrl）下重复命中的条目会自动去重。

## 坐标契约（重要）

- 所有条目的 `SelectionBounds` 必须位于**内容节点（Content）的本地坐标系**中，即与
  `RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, screenPos, uiCamera, out local)` 返回的坐标同空间。
- 框选矩形视觉对象由管理器自动创建为 Content 的子节点，其锚点对齐 `Content.pivot`，
  因此对 Content 的锚点/枢轴设置无特殊要求（如 Matheblock 中 Content 的 pivot 为 `(0, 0.5)` 亦可正常工作）。
- `uiCamera`：Screen Space - Overlay 画布传 `null`；Screen Space - Camera 传画布的 `worldCamera`。

## 快速上手

```csharp
using BoxSelection;
using UnityEngine;

public class MySelectionPanel : MonoBehaviour
{
    [SerializeField] private RectTransform m_content;      // 坐标基准节点
    [SerializeField] private RectTransform m_interactionRect; // 鼠标命中区域（可空）

    private readonly List<MyItem> m_items = new();

    private void Start()
    {
        // 1. 创建单例管理器（场景内唯一；重复调用复用已有实例）
        BoxSelectionManager manager = BoxSelectionManager.Create(m_content, uiCamera: null);

        // 2. 注册条目（实现 IBoxSelectable 即可）
        foreach (MyItem item in m_items)
        {
            manager.RegisterItem(item);
        }

        // 3. 监听选择变化，刷新视觉 / 启用按钮
        manager.SelectionChanged += OnSelectionChanged;
        manager.SelectionCleared += OnSelectionCleared;
    }

    private void OnSelectionChanged()
    {
        BoxSelectionManager m = BoxSelectionManager.Instance;
        foreach (MyItem item in m_items)
        {
            item.SetHighlighted(m.Contains(item)); // 宿主自行刷新颜色
        }
        Debug.Log($"选中 {m.SelectionCount} 项，主选中: {m.MainSelection?.Data}");
    }

    private void OnSelectionCleared()
    {
        foreach (MyItem item in m_items)
        {
            item.SetHighlighted(false);
        }
    }
}

// 宿主自定义条目实现
public class MyItem : IBoxSelectable
{
    public Rect SelectionBounds { get; set; }   // 内容本地坐标中的包围盒
    public bool IsSelectable => true;
    public object Data { get; set; }            // 业务数据

    public void SetHighlighted(bool selected) { /* 更新颜色等 */ }
}
```

## 使用 BoxSelectionItem 组件

将 `BoxSelectionItem` 挂到任意带 `RectTransform` 的 UI 元素上，无需手写 `IBoxSelectable` 实现：

- 组件在 `OnEnable` 时自动向单例注册，`OnDisable` 时自动注销（并从选中集合移除）。
- 包围盒根据自身世界坐标四角实时换算到 Content 本地坐标系。
- 注意：需在管理器**已创建**之后再启用组件；若管理器后创建，调用一次 `RefreshRegistration()` 补注册。
- 常用成员：`IsSelected`（是否选中）、`SetData(object)`（绑定业务数据）、`SetSelectable(bool)`、`Data`。

```csharp
BoxSelectionItem item = go.GetComponent<BoxSelectionItem>();
item.SetData(slotIndex);
bool isOn = item.IsSelected;
```

## API 参考

### BoxSelectionManager（`MonoBehaviour` 单例）

静态成员：

| 成员 | 说明 |
|---|---|
| `Instance` | 当前单例实例（`null` 表示尚未创建） |
| `Create(content, uiCamera = null, config = null, interactionRect = null, parent = null)` | 创建并初始化管理器；已存在实例时复用并重新绑定 |
| `TryGet(out BoxSelectionManager)` | 安全获取单例 |

实例属性：

| 属性 | 说明 |
|---|---|
| `Content` | 坐标基准节点 |
| `UiCamera` | 屏幕坐标转换相机（Overlay 为 null） |
| `Selection` | 当前选中集合（按选中顺序，只读） |
| `MainSelection` | 主选中条目 |
| `SelectionCount` | 选中数量 |
| `HasSelection` | 是否存在任意选中 |
| `IsBoxSelecting` | 是否正在拖拽框选矩形 |
| `RegisteredItems` | 已注册条目（只读） |

实例方法：

| 方法 | 说明 |
|---|---|
| `Initialize(content, uiCamera = null, config = null, interactionRect = null)` | 绑定 / 重新绑定坐标基准与配置 |
| `RegisterItem(IBoxSelectable)` | 注册条目（重复注册忽略） |
| `UnregisterItem(IBoxSelectable)` | 注销条目（选中中则同步移除） |
| `ClearSelection()` | 清空所有选择 |
| `SelectAll()` | 选中全部可选条目 |
| `Select(item)` | 单选（清除其他选择） |
| `AddToSelection(item)` | 追加选中并设为主选中 |
| `ToggleSelect(item)` | 切换选中状态（Ctrl+点击等价） |
| `Deselect(item)` | 从选中集合移除指定条目 |
| `SetMainSelection(item)` | 设为主选中（须已在集合中） |
| `Contains(item)` | 是否处于选中状态 |

### IBoxSelectable（条目契约）

| 成员 | 说明 |
|---|---|
| `Rect SelectionBounds` | 条目在内容本地坐标中的包围盒（AABB） |
| `bool IsSelectable` | 是否允许被选中 |
| `object Data` | 宿主业务数据，用于回调中识别条目 |

### BoxSelectionItem（`MonoBehaviour` 适配组件）

| 成员 | 说明 |
|---|---|
| `SelectionBounds` / `IsSelectable` / `Data` | 实现 `IBoxSelectable` 的只读属性 |
| `IsSelected` | 当前是否选中 |
| `SetData(object)` / `SetSelectable(bool)` | 写入业务数据 / 开关可选性 |
| `RefreshRegistration()` | 管理器创建后补注册 |

### BoxSelectionConfig（配置）

| 字段 | 默认值 | 说明 |
|---|---|---|
| `SelectionBoxColor` | `(0.3, 0.8, 1, 0.2)` | 框选矩形填充颜色 |
| `DragThresholdPixels` | `5` | 判定拖拽（非点击）的像素阈值 |
| `RequireShiftForBoxSelect` | `true` | 是否需要 Shift 才触发框选 |
| `Modifier` | `Control` | 追加/切换修饰键（Control / Shift / Alt） |
| `MouseButton` | `0` | 交互鼠标键（0=左 1=中 2=右） |

## 事件

| 事件 | 触发时机 |
|---|---|
| `SelectionChanged` | 选中集合非空时，集合或主选中发生变化（单选、追加、框选、切换、主选中退让等） |
| `SelectionCleared` | 选中集合从非空变为空 |

> 约定：集合从非空变为空只触发 `SelectionCleared`；其余变化触发 `SelectionChanged`。

## 输入系统兼容

通过编译宏自动选择输入后端，无需配置：

| 项目设置（Active Input Handling） | 使用的输入 API |
|---|---|
| Input Manager（旧） | `UnityEngine.Input` |
| Both | `UnityEngine.Input`（与本仓库现有代码一致） |
| Input System Package（新） | `UnityEngine.InputSystem`（`Mouse` / `Keyboard`） |

注意：新 Input System 下若发现框选 Y 轴方向颠倒，说明目标项目的鼠标屏幕坐标 Y 轴方向与默认假设相反，将 `MouseScreenPos()` 中的 `ReadValue()` 结果做 `y = Screen.height - y` 换算即可。

## 限制与注意事项

- 本模块只负责**选择状态 + 框选交互**；删除、移动、滚动、颜色刷新等由宿主在 `SelectionChanged` / `SelectionCleared` 中自行处理。
- 非 Shift 拖拽（超过阈值）会被模块主动放弃（`IsPotentialClick = false`），宿主可在自己的 `Update` 中读取输入实现滚动 / 移动，两者互不冲突。
- 框选时不在拖拽过程中实时变更选中集合，而是在鼠标抬起时一次性提交（与 Matheblock 原行为一致）。
- 条目包围盒建议为轴对齐矩形；旋转后的条目包围盒按世界坐标四角取 AABB，会略大于实际区域。
- 点击命中重叠条目时取"最后注册者"，如需自定义优先级请自行控制注册顺序或拆分为不重叠的包围盒。
- 该模块独立于 Matheblock Editor 的 `EasingAreaManager`，未改动原工程任何既有代码。
