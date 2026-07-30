# 缓动初步

## 概述

在编辑器右侧实现了缓动函数区（Easing Area），为方体的 15 个属性提供数值随时间变化的可视化编辑能力。用户可在时间轴上添加锚点（关键帧），为每个锚点设置缓动类型和权重，系统通过 DOTween 缓动库在锚点间插值计算，并将函数曲线实时绘制在 UI 上。

---

## 架构

```
EasingAreaManager          -- 右侧缓动区核心管理器
├── EasingDataModels        -- 数据模型（AnchorPoint, EasingSlotData, EasingSlotConfig）
├── AnchorPointEditorUI     -- 锚点编辑面板 UI
├── EaseDisplayNames        -- DOTween Ease 枚举的中文映射
└── CubeDataModels          -- 方体数据中新增 easingSlots 字段
```

---

## 15 个数据槽

每个数据槽对应方体的一条属性，以竖线分割：

| 索引 | 标号 | 含义 | 默认值 | 范围 |
|------|------|------|--------|------|
| 0-2  | lx/ly/lz | 方体长宽高 | 1.0 | 0~10 |
| 3-5  | rx/ry/rz | 方体倾斜角 (度) | 0.0 | -360~360 |
| 6-8  | px/py/pz | 方体位置 | 0.0 | -10~10 |
| 9-12 | R/G/B/A | 方体颜色 RGBA | 0.9/0.9/0.9/1 | 0~1 |
| 13   | 棱偏移 | Note 距中间位置偏移 | 0.0 | -1~1 |
| 14   | 流速 | 下落速度倍率 | 1.0 | 0~10 |

---

## 数据模型

### `AnchorPoint`
关键帧数据，存储时间点、数值、缓动类型及权重：

```csharp
public class AnchorPoint {
    public float time;           // 时间位置 (秒)
    public float value;          // 数值
    public Ease easingType;      // DOTween 缓动类型
    public float weight;         // 权重 (0=线性, 1=完整缓动, 可超过1增强)
}
```

### `EasingSlotData`
单个数据槽的完整缓动数据，包含锚点列表和插值求值方法 `EvaluateAt()`。

### `EasingSlotConfig`
数据槽配置结构体，定义默认值、最小值和最大值。

---

## 缓动系统

### DOTween 集成
取代自实现缓动函数，直接使用 DOTween 的 `Ease` 枚举（共 31 种常用类型），通过 `DOVirtual.EasedValue()` 进行插值。

### 权重机制
- `weight = 0`：线性插值
- `weight = 1`：完整 DOTween 缓动曲线
- `weight > 1`：增强缓动效果

核心插值公式：
```csharp
float easedT = DOVirtual.EasedValue(0f, 1f, t, curr.easingType);
float weightedT = Mathf.Lerp(t, easedT, curr.weight);
return Mathf.Lerp(curr.value, next.value, weightedT);
```

---

## 交互流程

1. **添加锚点**：在缓动区数据槽上点击格点位置
2. **选中锚点**：点击已有锚点标记（圆形），高亮变绿
3. **编辑面板**：
   - 选中锚点后在 FunctionChanger 区域弹出面板
   - 可修改数值、缓动类型、权重滑块
   - 实时预览当前缓动曲线
4. **删除锚点**：编辑面板中点击"删除锚点"
5. **水平滚动**：拖拽缓动区内容水平滚动，查看所有数据槽

---

## 曲线可视化

使用 Image 线段池方案绘制缓动曲线：
- 锚点间的曲线按 `m_curveSamples`（默认 24）段采样
- 每段用一条 Image 线段表示，通过设置 `sizeDelta` 和旋转角度定位
- 线段池按需动态扩容，闲置线段通过 `SetActive(false)` 隐藏

---

## 数据持久化

锚点数据存储在 `CubeData.easingSlots` 中，通过 `CubeManager` 的 JSON 序列化保存和加载。

---

## 关键文件

| 文件 | 职责 |
|------|------|
| `EasingAreaManager.cs` | 缓动区 UI 构建、鼠标交互、锚点管理、曲线绘制 |
| `EasingDataModels.cs` | `AnchorPoint`, `EasingSlotData`, `EasingSlotConfig`, `EaseDisplayNames` |
| `AnchorPointEditorUI.cs` | 锚点编辑面板：数值输入、缓动下拉、权重滑块、曲线预览 |
| `CubeDataModels.cs` | `CubeData` 中新增 `easingSlots` 字段和 `InitializeDefaultEasingSlots()` |
