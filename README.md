# Matheblock Editor

基于 Unity 的节奏游戏谱面编辑器，支持变 BPM、六边形网格与完整的设置系统。

> 当前版本：**0.1.1a**

## 技术栈

| 项目 | 版本 / 说明 |
|------|-------------|
| Unity | 2022.3.53f1c1 |
| 脚本语言 | C# |
| UI 框架 | uGUI (TextMesh Pro)（所有场景） |
| 渲染管线 | Built-in Render Pipeline |
| 动画引擎 | DOTween 1.2.x |
| 命名空间 | `HexMap` |

## 项目结构

```
Assets/
├── Fonts/              # 字体资源 (black.ttf, consola.ttf)
├── Maps/               # 谱面数据 (chart.json / music.mp3 / illustration.png)
├── Plugins/            # 第三方插件
│   ├── DOTween/        #   动画补间引擎
│   ├── StandaloneFileBrowser/  #   跨平台文件对话框
│   └── TimerManager/   #   轻量定时器管理
├── Resources/          # 动态资源 (Fonts/black.ttf)
├── Scenes/             # 场景文件
├── Scripts/            # 核心脚本
│   ├── CubeSystem/     #   方体管理系统（6面/12棱/24方向）
│   ├── Editor/         #   Editor 工具脚本
│   └── Debug/          #   调试工具
├── Shaders/            # 自定义着色器 (Aurora.shader, Blur.shader)
└── TextMesh Pro/       # 文字渲染方案
```

## 核心模块

### 谱面选择 (`ChartSelect`)
- 扫描 `Maps/` 目录构建谱面列表
- 支持创建新谱面（曲名、作者、音乐家）
- 封面图加载与显示

### 编辑器初始化 (`EditorInit`)
- 加载谱面 JSON 并复制为临时工作副本 `chart.tmp`
- 异步加载音乐（`UnityWebRequest`）
- 动态挂载 `GridManager` 与 `GridScrollHandler`

### 节拍网格 (`GridManager`)
- 基于 BPM 节点的可变节拍时间轴
- 垂直线（轨道数 XLine）/ 水平线（时间线 YLine）
- Ctrl + 滚轮缩放（0.1x - 8x）
- 时间滑块同步滚动偏移

### 滚动处理 (`GridScrollHandler`)
- 滚轮时间轴滚动，Ctrl+滚轮缩放
- 方向键上下滚动
- Ctrl + = / - 键盘缩放

### BPM 管理 (`BpmManagerUI`)
- 运行时动态构建 BPM 管理面板
- 节点增删、升序时间校验
- 读写 `chart.tmp`，供 `GridManager` 实时绘制

### 音频控制 (`MusicTimeStampController` / `ButtonFuctionManager`)
- 音乐播放/暂停/重播
- 滑动条与音频时间同步

### 文件管理 (`FileBrowserManager`)
- 封装 `StandaloneFileBrowser`，支持打开 MP3/WAV/OGG/PNG 等文件

### 缓动系统 (Easing Area)

在编辑器右侧实现了缓动函数区，为方体的 15 个属性提供数值随时间变化的可视化编辑能力。用户可在时间轴上添加锚点（关键帧），为每个锚点设置缓动类型和权重，系统通过 DOTween 在锚点间插值计算，并将函数曲线实时绘制在 UI 上。

#### 架构

```
EasingAreaManager          -- 右侧缓动区核心管理器
├── EasingDataModels        -- 数据模型（AnchorPoint, EasingSlotData, EasingSlotConfig）
├── AnchorPointEditorUI     -- 锚点编辑面板 UI
├── EaseDisplayNames        -- DOTween Ease 枚举的显示名映射
└── CubeDataModels          -- 方体数据中新增 easingSlots 字段
```

#### 15 个数据槽

每个数据槽对应方体的一条属性，以竖线分割：

| 索引 | 标号 | 含义 | 默认值 |
|------|------|------|--------|
| 0-2  | lx/ly/lz | 方体长宽高 | 100（百分比，100=原始大小） |
| 3-5  | rx/ry/rz | 方体倾斜角 (度) | 0 |
| 6-8  | px/py/pz | 方体位置 | 0 |
| 9-12 | R/G/B/A | 方体颜色 RGBA | 0.9/0.9/0.9/1 |
| 13   | 棱偏移 | Note 距中间位置偏移 | 0 |
| 14   | 流速 | 下落速度倍率 | 30 |

#### 数据模型

**`AnchorPoint`** — 关键帧数据：
```csharp
public class AnchorPoint {
    public float time;           // 时间位置 (秒)
    public float value;          // 数值
    public Ease easingType;      // DOTween 缓动类型
    public float weight;         // 权重 (0=线性, 1=完整缓动, 可超过1增强)
}
```

**`EasingSlotData`** — 单个数据槽的完整缓动数据，包含锚点列表和 `EvaluateAt()` 插值求值方法。

**`EasingSlotConfig`** — 数据槽配置结构体（默认值 / 最小值 / 最大值）。

#### DOTween 集成与权重机制

取代自实现缓动函数，直接使用 DOTween 的 `Ease` 枚举（共 31 种常用类型），通过 `DOVirtual.EasedValue()` 进行插值。

权重控制缓动强度：
- `weight = 0`：线性插值
- `weight = 1`：完整 DOTween 缓动曲线
- `weight > 1`：增强缓动效果

核心插值公式：
```csharp
float easedT = DOVirtual.EasedValue(0f, 1f, t, curr.easingType);
float weightedT = Mathf.Lerp(t, easedT, curr.weight);
return Mathf.Lerp(curr.value, next.value, weightedT);
```

#### 交互流程

1. **添加锚点**：在缓动区数据槽上点击格点位置
2. **选中锚点**：点击已有锚点标记（圆形），高亮变绿
3. **编辑面板**：选中锚点后在 FunctionChanger 区域弹出面板，可修改数值、缓动类型、权重，并实时预览曲线
4. **删除锚点**：编辑面板中点击「删除锚点」
5. **水平滚动**：拖拽缓动区内容水平滚动，查看所有数据槽

#### 曲线可视化

使用 Image 线段池方案绘制缓动曲线：
- 锚点间的曲线按 24 段采样
- 每段用一条 Image 线段表示，通过 `sizeDelta` 和旋转角度定位
- 线段池按需动态扩容，闲置线段通过 `SetActive(false)` 隐藏

#### 关键文件

| 文件 | 职责 |
|------|------|
| `EasingAreaManager.cs` | 缓动区 UI 构建、鼠标交互、锚点管理、曲线绘制 |
| `EasingDataModels.cs` | `AnchorPoint`, `EasingSlotData`, `EasingSlotConfig`, `EaseDisplayNames` |
| `AnchorPointEditorUI.cs` | 锚点编辑面板：数值输入、缓动下拉、权重滑块、曲线预览 |
| `CubeDataModels.cs` | `CubeData` 中新增 `easingSlots` 字段和 `InitializeDefaultEasingSlots()` |

---

## 设置系统

| 模块 | 功能 |
|------|------|
| `SettingsMenuController` | 左右分栏菜单，滑块/开关/下拉框自动绑定 |
| `SettingsDataManager` | JSON 文件持久化（音量、分辨率、自动保存），自动从 PlayerPrefs 迁移 |
| `KeyBindingsStore` | 快捷键绑定存储，JSON 持久化，支持组合键（`KeyCombo`） |
| `RebindButton` | 点击 5 秒内捕获按键（支持 Ctrl/Shift/Alt 组合），启动时自动加载已保存绑定 |
| `EditorOpenSettings` | 编辑器内以 Additive 模式叠加设置场景 |

### 持久化

设置数据保存到 `Application.persistentDataPath/` 下的 JSON 文件：

| 文件 | 内容 |
|------|------|
| `Settings.json` | 音量、分辨率、自动保存等设置 |
| `KeyBindings.json` | 快捷键绑定（含组合键） |

首次运行时自动从旧版 PlayerPrefs 数据迁移。

**Settings.json 格式：**

```json
{
    "masterVolume": 1.0,
    "musicVolume": 0.8,
    "sfxVolume": 1.0,
    "resolutionIndex": 0,
    "autoSaveMinutes": 10
}
```

> 画质固定为中档（Medium），不提供用户配置项。

**KeyBindings.json 格式：**

```json
{
    "entries": [
        {
            "actionName": "Note_Click",
            "combo": {
                "m_ctrl": false,
                "m_shift": false,
                "m_alt": false,
                "m_mainKey": 113
            }
        },
        {
            "actionName": "Editor_Undo",
            "combo": {
                "m_ctrl": true,
                "m_shift": false,
                "m_alt": false,
                "m_mainKey": 122
            }
        }
    ]
}
```

> `m_mainKey` 的值是 `KeyCode` 枚举的整数值（如 113 = `KeyCode.Q`，122 = `KeyCode.Z`）。

### 组合键支持（KeyCombo）

快捷键系统全面支持组合键。`KeyCombo` 是一个可序列化结构体，位于 `HexMap` 命名空间：

```csharp
[Serializable]
public struct KeyCombo
{
    [SerializeField] private bool m_ctrl;       // 是否需要 Ctrl
    [SerializeField] private bool m_shift;      // 是否需要 Shift
    [SerializeField] private bool m_alt;        // 是否需要 Alt
    [SerializeField] private KeyCode m_mainKey; // 主键
    [SerializeField] private sbyte m_wheelDir;  // 滚轮方向：0=无，1=上，-1=下
}
```

**核心方法：**

| 方法 | 说明 |
|------|------|
| `Parse(string)` | 从显示字符串解析（如 `"Ctrl + 滚轮上"`） |
| `ToDisplayString()` | 格式化为显示字符串 |
| `IsPressed()` | 检测当前帧是否触发（修饰键精确匹配） |
| `IsHeld()` | 检测是否持续按住（用于滚动等连续输入） |
| `FormatKeyCode(KeyCode)` | 将 KeyCode 格式化为用户友好的显示名 |

**检测逻辑** — `IsPressed()` 采用精确匹配策略：
- 指定的修饰键必须处于按下状态
- 未指定的修饰键不能处于按下状态
- 主键必须在当前帧 `GetKeyDown`
- 当主键本身是修饰键时（如单独绑定 "Shift"），检测 `GetKeyDown`（左右皆可）

**组合键格式** — 以 `" + "` 分隔，修饰键顺序固定为 `Ctrl -> Shift -> Alt -> 主键`：
- `Ctrl + Z`
- `Ctrl + Shift + Z`
- `Ctrl + Alt + S`
- `Q`（无修饰键）

### KeyBindingsStore API

| 方法 | 说明 |
|------|------|
| `GetKeyCombo(actionName, defaultCombo)` | 获取组合键绑定 |
| `GetBinding(actionName, defaultKey)` | 获取显示名字符串（向后兼容） |
| `SetBinding(actionName, keyName)` | 设置绑定（解析字符串为 KeyCombo） |
| `SetKeyCombo(actionName, combo)` | 直接设置 KeyCombo |
| `ResetAll()` | 清除所有自定义绑定 |

### RebindButton

点击后 5 秒内捕获按键，支持 Ctrl/Shift/Alt 组合键和鼠标滚轮上/下。`Start()` 时自动从 `KeyBindingsStore` 加载已保存的绑定并显示。

### UndoRedoManager

从 `KeyBindingsStore` 读取快捷键（默认 `Ctrl+Z` / `Ctrl+Y`），用户可在设置页面重绑。保留 `Ctrl+Shift+Z` 作为重做兼容快捷键。

Setting 场景关闭后，`EditorInit` 自动调用 `UndoRedoManager.ReloadShortcuts()` 重新加载快捷键。

### GridScrollHandler

从 `KeyBindingsStore` 读取滚动和缩放快捷键，支持鼠标滚轮和键盘。鼠标滚轮上/下可被 RebindButton 捕获并重绑。

### Setting 场景 - 可自定义快捷键行

通过 Unity MCP 在 `Page_ShortcutKey` 页面中新增了所有可自定义的 RebindButton 行：

| 行 | ActionName | DefaultKey | KeyText |
|----|-----------|------------|---------|
| Row_Note_Click | `Note_Click` | `Q` | Click |
| Row_Note_Flick | `Note_Flick` | `R` | Flick |
| Row_Note_Drag | `Note_Drag` | `E` | Drag |
| Row_Note_ReverseFlick | `Note_ReverseFlick` | `T` | ReverseFlick |
| Row_Note_FakeToggle | `Note_FakeToggle` | `Tab` | Fake Note 切换键 |
| Row_Global_Toggle | `Global_Toggle` | `O` | 切换全局事件区模式 |
| Row_Bar_Create | `Bar_Create` | `S` | 创建缓动区长条 |
| Row_Bar_Delete | `Bar_Delete` | `Delete` | 删除选中长条 |
| Row_Undo | `Editor_Undo` | `Ctrl + Z` | 撤回 |
| Row_Redo | `Editor_Redo` | `Ctrl + Y` | 重做 |
| Row_ScrollUp | `Editor_ScrollUp` | `滚轮上` | 向上滚动 |
| Row_ScrollDown | `Editor_ScrollDown` | `滚轮下` | 向下滚动 |
| Row_ZoomIn | `Editor_ZoomIn` | `Ctrl + 滚轮上` | 放大 |
| Row_ZoomOut | `Editor_ZoomOut` | `Ctrl + 滚轮下` | 缩小 |

## 谱面格式 (`chart.json`)

```json
{
  "info": {
    "MusicName": "Chronomia",
    "Charter": "unifoly",
    "Illustrationer": "",
    "Musician": "Lime"
  },
  "bpmNodes": [
    { "time": 0.0, "bpm": 120.0 },
    { "time": 3.0, "bpm": 30.0 }
  ],
  "notes": [
    { "type": "Click", "lane": 0, "time": 1.0, "isFake": false }
  ],
  "cubes": [
    {
      "cubeId": 1,
      "cubeName": "Cube_1",
      "cubeNote": "",
      "tracks": []
    }
  ]
}
```

## 方体系统 (Cube System)

方体系统是谱面编辑器的核心3D可视化模块，所有note轨道围绕正方体的 **6个面、12条棱、24个方向** 组织。

### 设计概念

| 概念 | 数量 | 说明 |
|------|------|------|
| 面 (CubeFace) | 6 | 上/下/左/右/前/后，对应Y+/Y-/X-/X+/Z+/Z- |
| 棱 (Edge) | 12 | 正方体的12条边，100%不透明白色 |
| 方向 (FaceDirection) | 4 | 每个面的上/下/左/右 |
| note轨道总数 | 24 | 6面 × 4方向 = 24条独立轨道 |

用户在 UpperList 区域选择某个面（6方向之一）后，展示该面对应的4条note轨道。

### 文件结构

```
Assets/Scripts/CubeSystem/
├── CubeEnums.cs          # CubeFace / FaceDirection 枚举 + CubeConstants 常量
├── CubeDataModels.cs     # JSON 可序列化数据模型
├── CubeVisualizer.cs     # 3D 可视化（12棱 + 6面）
├── CubeManager.cs        # 方体管理器（创建/选择/持久化）
└── CubeManagerUI.cs      # 方体管理面板 UI + 快捷选择绑定
```

### 数据模型

方体数据存储在 `chart.tmp`（编辑期间）和 `chart.json`（保存后）的 `cubes` 字段中，与 `info`、`bpmNodes`、`notes` 共用同一个 JSON 文件：

```json
{
    "info": { "MusicName": "...", "Charter": "...", "Illustrationer": "...", "Musician": "..." },
    "bpmNodes": [ {"time": 0.0, "bpm": 120.0} ],
    "notes": [ {"type": "Click", "lane": 0, "time": 1.0, "isFake": false} ],
    "cubes": [
        {
            "cubeId": 1,
            "cubeName": "Cube_1",
            "cubeNote": "",
            "tracks": [
                { "face": "Up", "direction": "Up", "notes": [
                    { "type": "Click", "lane": 0, "time": 1.0, "isFake": false }
                ] },
                { "face": "Up", "direction": "Down", "notes": [] }
                // ... 共24条轨道（6面 × 4方向）
            ]
        }
    ]
}
```

| 类 | 说明 |
|----|------|
| `CubeNoteData` | 单个Note数据（type, lane, time），lane 保存原始轨道索引 |
| `CubeNoteTrackData` | 单条轨道（face, direction, notes列表），由面+方向唯一标识 |
| `CubeData` | 单个方体（cubeId, cubeName, cubeNote, 24条tracks, 15个easingSlots） |

### 可视化规格

| 元素 | 渲染方式 | 颜色 | 透明度 |
|------|----------|------|--------|
| 12条棱 | 细长 Cube Primitive | 白色 (1,1,1) | 100% 不透明 |
| 6个面 | Quad Primitive | 白色 (1,1,1) | 80% 半透明 |

- 棱材质：Unlit/CubeUnlit shader，不透明，恒白色
- 面材质：Unlit/CubeUnlit shader，80% 透明，双面渲染（Cull Off）
- 不受光照影响，所有面颜色一致
- 颜色和透明度可通过 `CubeVisualizer.SetEdgeColor()` / `SetFaceColor()` 调整（预留扩展）

### CubeManager API

| 方法 / 属性 | 说明 |
|-------------|------|
| `CreateCube()` | 创建新方体，自动初始化24条空轨道并生成3D可视化 |
| `DeleteCube(int cubeId)` | 删除指定方体（仅剩1个时禁止删除） |
| `SetActiveCube(int cubeId)` | 设置当前选中的方体，触发轨道组切换 |
| `SetActiveTrack(CubeFace, FaceDirection)` | 设置当前选中的面和方向 |
| `GetActiveTrack()` | 获取当前选中轨道数据 |
| `GetCube(int cubeId)` | 根据 ID 获取方体数据 |
| `SaveCubesToJson()` | 保存方体数据到 chart.tmp（保留其他字段） |
| `Cubes` | 只读访问所有方体列表 |
| `ActiveCubeId` / `ActiveFace` / `ActiveDirection` | 当前选择状态 |

> 首次启动时若无方体数据，自动创建默认方体 `Cube_1`（ID=1）。后续创建的方体 ID 从 2 开始递增。

### 事件

| 事件 | 触发时机 |
|------|----------|
| `CubeCreated` | 方体创建后 |
| `CubeDeleted` | 方体删除后 |
| `ActiveCubeChanged` | 选中的方体变化时（切换轨道组） |
| `ActiveTrackChanged` | 选中的面/方向变化时 |

### 场景结构

```
CubeSystem (GameObject + CubeManager)
├── Cube_1 (GameObject + CubeVisualizer)
│   ├── Edges/
│   │   ├── Edge_0 ~ Edge_11  (12条棱)
│   └── Faces/
│       ├── Face_Up / Face_Down / Face_Left / Face_Right / Face_Front / Face_Back
├── Cube_2
└── ...
```

### 方体管理 UI (`CubeManagerUI`)

挂在 FunctionChanger 下的「CubeManager」按钮上，点击后弹出全屏面板。

**面板功能：**

| 操作 | 说明 |
|------|------|
| 创建方体 | 点击底部「+ 创建方体」按钮，调用 `CubeManager.CreateCube()` |
| 选中方体 | 点击列表项的「选中」按钮，切换轨道组并高亮 |
| 删除方体 | 点击列表项的「X」按钮（仅剩1个方体时禁用） |
| 编辑备注 | 每行备注输入框可编辑，回车后保存到 chart.tmp |
| 返回 | 点击左上角「<」返回按钮，隐藏面板 |

**快捷选择（UpperList 区域）：**

CubeManagerUI 在 `Start()` 时自动绑定 UpperList 中的现有控件：

| 控件 | 路径 | 功能 |
|------|------|------|
| CubeID 输入框 | `UpperList > TmpDataChanger > CubeID` | 输入方体 ID 回车选中方体 |
| Surface 面按钮 | `UpperList > Surface` | 6个按钮（Up/Down/Left/Right/Front/Back），选中对应面 |
| Side 方向按钮 | `UpperList > Side` | 4个按钮（Up/Down/Left/Right），选中对应方向 |

### 轨道切换机制

24条轨道（6面 × 4方向）独立存储 Note 数据。切换面或方向时：

1. **切换前**：`SaveCurrentNotesToCubeTrack()` 将左侧当前显示的 Note 保存到旧轨道（保留原始 lane 值）
2. **切换后**：`LoadActiveTrackNotes()` 从新轨道读取 Note 并重新加载到左侧显示

按钮高亮采用单选样式：同一按钮组内仅选中项标黄（`#FFEB4B`），其余恢复白色。

| 按钮组 | 高亮颜色 | 默认选中 |
|--------|----------|----------|
| Surface（6面） | `(1, 0.92, 0.3, 1)` 黄色 | Front |
| Side（4方向） | `(1, 0.92, 0.3, 1)` 黄色 | Up |

## 第三方依赖

| 插件 | 用途 |
|------|------|
| [DOTween](http://dotween.demigiant.com/) | UI 动画补间 + 缓动函数库 |
| [StandaloneFileBrowser](https://github.com/gkngkc/UnityStandaloneFileBrowser) | 原生文件对话框 |
| TextMesh Pro | 高质量文字渲染 |
| TimerManager | 轻量定时器 |

## 快捷键

| 快捷键 | 功能 | 可自定义 |
|--------|------|---------|
| Ctrl + Z | 撤回 | ✅ (`Editor_Undo`) |
| Ctrl + Y | 重做 | ✅ (`Editor_Redo`) |
| Ctrl + Shift + Z | 重做（兼容） | 自动兼容 |
| Q | 放置 Click Note | ✅ (`Note_Click`) |
| R | 放置 Flick Note | ✅ (`Note_Flick`) |
| E | 放置 Drag Note | ✅ (`Note_Drag`) |
| T | 放置 ReverseFlick Note | ✅ (`Note_ReverseFlick`) |
| W | 放置 Hold Note | ✅ (`Note_Hold`) |
| Tab（切换/按住）+ Q/R/E/T/W | 放置对应类型的 Fake Note | ✅ (`Note_FakeToggle`) |
| O | 切换全局事件区模式 | ✅ (`Global_Toggle`) |
| S | 创建缓动区长条（两次确认） | ✅ (`Bar_Create`) |
| Delete | 删除选中长条（缓动区） | ✅ (`Bar_Delete`) |
| 滚轮上 | 向上滚动 | ✅ (`Editor_ScrollUp`) |
| 滚轮下 | 向下滚动 | ✅ (`Editor_ScrollDown`) |
| Ctrl + 滚轮上 | 放大 | ✅ (`Editor_ZoomIn`) |
| Ctrl + 滚轮下 | 缩小 | ✅ (`Editor_ZoomOut`) |
| Ctrl + E | 打开设置（Editor 模式） | ❌ |
| Esc | 关闭设置 / 返回 | ❌ |

## Fake Note（假音符）

Fake Note 是五种基础 Note（Click / Flick / Drag / ReverseFlick / Hold）的变体，与正常 Note 的唯一区别是**击打（命中）后不生成打击特效**。适合制作"看似可击打但无反馈"的迷惑性谱面段落。

### 放置方式

Fake Note 的放置模式可在「编辑器设置」中选择（默认**切换**模式），两种模式均使用 **Fake Note 切换键**（默认 `Tab`）配合对应类型的 Note 快捷键操作：

| 模式 | 操作方式 |
|------|----------|
| 切换（默认） | 按一次 `Tab` 开启 Fake 模式，再按一次关闭；开启期间悬停指示器变红，放置的所有 Note 均为 fake note |
| 按住 | 按住 `Tab` 的同时按对应类型的 Note 快捷键，放置 fake note |

任意模式下，按住/激活期间以下组合均有效：

| 操作 | 效果 |
|------|------|
| Fake 模式 + `Q` | 放置 Fake Click |
| Fake 模式 + `R` | 放置 Fake Flick |
| Fake 模式 + `E` | 放置 Fake Drag |
| Fake 模式 + `T` | 放置 Fake ReverseFlick |
| Fake 模式 + `W` | 放置 Fake Hold |

切换键默认绑定 `Note_FakeToggle`（默认 `Tab`），可在设置页面的 "Fake Note 切换键" 行重绑。

### 编辑器视觉区分

fake note 在编辑器界面以**半透明**（alpha 0.5）显示，与正常 Note 明显区分：
- 普通 Note：sprite 颜色白色不透明
- Fake Note：sprite 颜色白色 50% 透明（`k_fakeAlpha = 0.5f`）
- Hold 的头 / 中 / 尾三段均半透明，选中 / 取消选中状态同样保持半透明

### 放映表现

- 3D 预览中 fake note 同样以半透明显示（与编辑器一致）
- 命中判定时**跳过打击特效**：普通 Note 的 `SpawnBurst` 与 Hold 的 `EmitHold` 均被跳过
- 其余逻辑（下落、命中、清理、方体动画）与正常 Note 完全一致

### 数据持久化

fake 属性通过 `isFake` 字段贯穿全链路：编辑器放置 → chart.tmp 的 notes 字段（`NoteJsonNode`）→ 方体轨道存储（`CubeNoteData`）→ 运行时播放模型（`PlaybackNoteData`），保存后重新加载仍保持 fake 状态：

```json
{ "type": "Click", "lane": 0, "time": 1.0, "isFake": true }
```

### 关键实现

| 文件 | 改动 |
|------|------|
| `NotePlacementManager.cs` | 新增 `Note_FakeToggle` 动作与 `m_fakeToggleCombo`，放置 / 撤销 / 重做 / 删除时透传 `isFake`，半透明显示 |
| `KeyBindingsStore.cs` | `KeyCombo` 支持纯修饰键组合绑定（如仅 "Alt" / 普通键），`IsHeld()` 适配切换键检测 |
| `PlaybackModeController.cs` | 3D 预览半透明显示；命中时按 `IsFake` 跳过打击特效 |
| `CubeDataModels.cs` / `PlaybackDataModels.cs` | 数据模型新增 `isFake` 字段 |
| `CubeManagerUI.cs` | 轨道存取时透传 `isFake` |
| `Setting.unity` | 新增 `Row_Note_FakeToggle` 行（重绑按钮，默认 `Tab`） |

---

## 3D 方体展示与放映系统

### 渲染管线（CubeCamera + RenderTexture）

方体不再直接渲染在主相机中，而是通过独立的 CubeCamera 渲染到 RenderTexture，再由 RawImage 显示在 PlayScreen 上（曲绘之上、网格之下）：

```
CubeManager.SetupCubeDisplay()
├── 创建 RenderTexture（与 PlayScreen 尺寸一致，保证宽高比）
├── 创建 CubeCamera（正交、SolidColor 透明背景、仅渲染 Layer 8）
│   └── cullingMask = 1 << k_cubeLayer(8)
├── 主相机移除 Layer 8（cullingMask &= ~(1 << 8)）
└── 创建 RawImage "CubeDisplay" 作为 PlayScreen 第一个子物体
```

| 常量 | 值 | 说明 |
|------|-----|------|
| `k_cubeLayer` | 8 | 方体专用渲染层 |
| `k_cameraOrthoSize` | 0.8 | 正交相机半高（编辑模式） |
| `k_cameraYOffset` | 0 | 相机 Y 偏移 |

**相机模式切换（`SetPlaybackCameraMode`）**：

| 模式 | 相机位置 | orthoSize |
|------|----------|-----------|
| 编辑模式 | `(cubeX, 0, 0)`，顶棱对齐标定线 | 0.8 |
| 放映模式 | `(cubeX, 0, 0)`，居中正面 | 0.8 |

### 放映模式控制器（`PlaybackModeController`）

挂在 PlayScreen 上，监听 AudioSource 播放状态，控制编辑层淡出与 3D Note 下落。

**模式进出：**

| 事件 | 动作 |
|------|------|
| 进入放映（`EnterPlaybackMode`） | 淡出网格 / Note 层 / 缓动区 / 标定线（DOFade 0.4s）；曲绘保持变暗（`k_trackDimFactor=0.4`）、展示区恢复全亮（DOColor 白）；切换相机放映模式 |
| 退出放映（`ExitPlaybackMode`） | 反向恢复；曲绘与展示区恢复编辑态变暗（`k_trackDimFactor` / `k_cubeDimFactor` = 0.4） |

**淡出目标缓存（`CacheFadeTargets`）：**
- `GridContainerRect` / `NoteLayerRect` → CanvasGroup（无则动态添加）
- `ReferenceLine` → Graphic
- `EasingViewport` → CanvasGroup（缓动区整体）
- PlayScreen 自身 Graphic（背景曲绘）+ `CubeManager.CubeDisplay`（方体 RawImage）

### 3D Note 下落

Note 作为 3D SpriteRenderer 挂载在方体 Transform 下，由 CubeCamera 渲染。

**方向与轨道轴（`GetDirectionVectors`）：**

| FaceDirection | 下落方向 | 轨道轴 |
|---------------|----------|--------|
| Up | +Y | X（水平） |
| Down | -Y | X（水平） |
| Left | -X | Y（垂直） |
| Right | +X | Y（垂直） |

**下落轨迹：**

```
faceCenter = (0, 0, k_noteZOffset)          // 可见面前方 -0.55
laneOffset = laneAxis * lanePos              // 轨道偏移（-cubeHalf ~ +cubeHalf）
startPos = faceCenter - fallingDir * startDist + laneOffset
endPos   = faceCenter + fallingDir * (cubeHalf - edgeHalf) + laneOffset
```

**离屏起始位置（本次会话新增）：**

Note 从屏幕外开始下落，避免堆积在屏幕边缘。起始距离基于相机视野边界计算：

```
viewHalfExtent = 垂直下落 ? orthoSize : orthoSize * aspect
startDist = viewHalfExtent + noteHalf + 0.05f   // 略超视野边界
```

- 垂直下落（Up/Down）取 `orthoSize`（0.8），水平下落（Left/Right）取 `orthoSize * aspect`（相机 RenderTexture 宽高比）
- `noteHalf` 由 `k_fixedNoteSize`（0.18）派生，保证离屏边距与 Note 实际尺寸联动

**Note 规格：**

| 项 | 值 |
|----|-----|
| 大小 | 固定 0.18 世界单位（`k_fixedNoteSize`），不随轨道数/方体大小变化 |
| Z 偏移 | `k_noteZOffset = -0.55`（可见面前方） |
| 下落预览窗口 | `k_lookAhead = 3s` |
| 命中判定 | 贴图中线到达棱外边缘（`cubeHalf - edgeHalf`）时销毁 |

**生命周期（`UpdatePlaybackNotes`）：**
1. 时间跳变 > 1s 时清空重建
2. 距命中 3s 内生成 Note（`m_spawnedKeys` 去重）
3. 每帧按时间插值 `Lerp(startPos, endPos, progress)`
4. 命中后 `SetActive(false)` + `Destroy` 清理

### 放映动画驱动（RuntimePlayback）

| 组件 | 职责 |
|------|------|
| `ChartPlaybackController` | 从 chart.tmp 加载谱面、发现/注册方体、驱动动画、热重载（文件变更检测） |
| `CubeAnimator` | 将 13 个方体级缓动槽的值应用到方体 Transform（scale/rotation/position/color） |
| `EasingEvaluator` | 基于 DOTween Ease 在锚点间插值求值 |

关键实现：`CubeAnimator.Initialize()` 在重复初始化前先 `RestoreOriginal()`，避免缓存动画后的状态作为基准，确保实时预览与放映均按锚点驱动。

### 编辑模式背景变暗

预处理（编辑）模式下 PlayScreen 背景整体变暗，突出编辑内容。曲绘与展示区使用**独立变量**控制，且曲绘在放映时也保持变暗：

| 区域 | 变量 | 编辑模式 | 放映（非 Display） | Display 放映 |
|------|------|----------|--------------------|--------------|
| 背景曲绘（PlayScreen Graphic） | `k_trackDimFactor = 0.4` | 0.4 暗 | 0.4 暗 | 0.4 暗 |
| 展示区（CubeDisplay RawImage） | `k_cubeDimFactor = 0.4` | 0.4 暗 | 1.0 亮 | 0.4 暗 |

- 曲绘材质在 `CacheFadeTargets` 首次缓存时统一替换为默认 UI 材质（`Canvas.GetDefaultCanvasMaterial()`），保证 `Graphic.color` 着色生效
- 场景中 PlayScreen 曲绘 Image 的序列化颜色基线为白色（1,1,1,1），编辑态经代码置为 0.4 形成明显对比

### Combo 计数显示

在 PlayScreen 竖中线顶部显示 Combo 计数：第一行数字（72px）、第二行 "COMBO" 字母（24px），使用 combo SDF 字体（`Assets/Fonts/combo SDF.asset`，图集为空时回退到 `combo.ttf` 动态生成），黑色描边保证可读性。

**时间驱动计数**：既定播放器默认每个键都被精准击中，combo 不依赖命中判定，而是由当前时间直接决定——`combo = 当前时间之前已到达击打时间的非 Fake Note 数量`。滚动条跳转（seek）时时间变化，combo 自动重算，无需特殊重置。

| 项 | 说明 |
|----|------|
| 计数规则 | `note.time <= 当前时间` 的非 Fake Note 计入；Hold 只按开头击打时间计一次 |
| 播放/编辑 | 播放时用谱面时钟，编辑时用网格时间，两者均驱动 combo |
| 跳转 | 拖动滚动条 / seek 时按新时间重算，不清零 |
| 置顶 | 每帧 `EnsureComboDisplayOnTop()` 确保渲染在最上层 |

关键实现（`PlaybackModeController`）：
- `UpdateComboByTime(float currentTime)`：每帧统计已消失的非 Fake Note 数量，仅变化时刷新显示
- `CreateComboDisplay()` / `GetComboFont()`：运行时构建显示与字体回退
- 数据源复用 `NotePlacementManager.GetCurrentNotes()`（缓存复用，无每帧分配）

---

## 背景高斯模糊

### `GaussianBlur.shader`（Hidden/GaussianBlur）

替代原 GrabPass 模糊方案，采用双 Pass 标准高斯模糊，可与 `Graphics.Blit` 配合使用：

| 项 | 说明 |
|----|------|
| Pass 0 | 水平方向模糊（9 权重高斯核，sigma≈4） |
| Pass 1 | 垂直方向模糊（同核） |
| 参数 | `_BlurSize`（采样偏移倍数，默认 2.0） |

**9 权重高斯核：**

```
weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 }
```

**预模糊流程（`EditorInit.CreateBlurredTexture` / `InfoManagerUI.CreateBlurredTexture`）：**

```
source → Graphics.Blit(rt1, pass 0) → Graphics.Blit(rt2, pass 1)
      → ReadPixels 回 Texture2D → 作为背景图 sprite
```

曲绘在加载时预先完成高斯模糊，运行时不再有 GrabPass 开销，同时避免模糊 shader 误伤方体。

---

## 谱面数据持久化链路

### 双文件工作流

| 文件 | 作用 | 生命周期 |
|------|------|----------|
| `chart.json` | 正式谱面文件 | 启动时复制为 tmp，保存时覆写 |
| `chart.tmp` | 编辑期工作副本 | `EditorInit.Awake` 由 `CopyChartToTemp()` 生成，退出时删除 |

### 统一持久化入口（`EditorInit.PersistToChartJson`）

所有保存路径统一调用：

```csharp
public static void PersistToChartJson()
{
    // 1. CubeManager.SaveCubesToJson() → 内存方体/锚点写入 chart.tmp
    // 2. File.Copy(chart.tmp, chart.json, overwrite) → 持久化
}
```

**调用点：**

| 调用方 | 时机 |
|--------|------|
| `InfoManagerUI.HandleSaveButtonClicked` | Info 面板 Save 按钮 |
| `BpmManagerUI.HandleSaveButtonClicked` | BPM 面板 Save 按钮 |
| `EditorInit.OnApplicationQuit` | 退出前（先持久化，再清理 tmp） |

> 之前 OnApplicationQuit 直接删除 chart.tmp 导致锚点丢失，已改为先持久化再删除。

### 关键时序约束

1. **启动顺序**：`CubeManager.LoadCubesFromJson()` 必须在 `Start()` 中调用（而非 `Awake`）——确保 `EditorInit.Awake` 已设置 `ChartPath` 并生成 chart.tmp
2. **`RefreshChartPlayback` 不调用 `SaveCubesToJson`**：启动时内存数据尚未从文件加载，直接保存会用默认数据覆盖用户锚点
3. **Save 监听器生命周期**：`InfoManagerUI` / `BpmManagerUI` 的 `OnDisable` 不移除 Save 按钮监听器——锚点编辑面板（`AnchorPointEditorUI`）会禁用 FunctionChanger 子物体触发 OnDisable，若移除监听器 Save 按钮将失效；`OnEnable` 中 `RemoveListener + AddListener` 防止重复注册
4. **懒加载空值防护**：`SaveInfoToJson` 在 Info 面板未打开（输入框未创建）时提前返回，避免 NullReferenceException

### 曾修复的问题（排查记录）

| 问题 | 根因 | 修复 |
|------|------|------|
| 锚点保存后丢失 | OnApplicationQuit 删 tmp 未持久化 / 启动时序竞态 | `PersistToChartJson()` 统一入口 + 启动顺序调整 |
| Save 按钮无响应 | 面板切换触发 OnDisable 移除监听器 | OnDisable 保留 Save 监听器 |
| Save 报空引用 | 懒加载输入框未初始化 | 空值防护提前返回 |
| 启动加载失败 | CubeManager.Awake 先于 EditorInit.Awake 执行 | 加载移至 Start |
| Note 堆积屏幕边缘 | 起始距离小于相机视野 | 基于相机视野边界计算离屏起点 |
