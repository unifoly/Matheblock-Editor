# Matheblock Editor

基于 Unity 的节奏游戏谱面编辑器，支持变 BPM、六边形网格与完整的设置系统。

## 技术栈

| 项目 | 版本 / 说明 |
|------|-------------|
| Unity | 2022.3.53f1c1 |
| 脚本语言 | C# |
| UI 框架 | UI Toolkit + uGUI (TextMesh Pro) |
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

## 设置系统

| 模块 | 功能 |
|------|------|
| `SettingsMenuController` | 左右分栏菜单，滑块/开关/下拉框自动绑定 |
| `SettingsDataManager` | JSON 文件持久化（音量、全屏、画质），自动从 PlayerPrefs 迁移 |
| `KeyBindingsStore` | 快捷键绑定存储，JSON 持久化，支持组合键（`KeyCombo`） |
| `RebindButton` | 点击 5 秒内捕获按键（支持 Ctrl/Shift/Alt 组合），启动时自动加载已保存绑定 |
| `EditorOpenSettings` | 编辑器内以 Additive 模式叠加设置场景 |

### 持久化

设置数据保存到 `Application.persistentDataPath/` 下的 JSON 文件：

| 文件 | 内容 |
|------|------|
| `Settings.json` | 音量、画质、全屏等设置 |
| `KeyBindings.json` | 快捷键绑定（含组合键） |

首次运行时自动从旧版 PlayerPrefs 数据迁移。

**Settings.json 格式：**

```json
{
    "masterVolume": 1.0,
    "musicVolume": 0.8,
    "sfxVolume": 1.0,
    "isFullscreen": true,
    "qualityLevel": 2,
    "resolutionIndex": 0
}
```

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
    { "type": "Click", "lane": 0, "time": 1.0 }
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
    "notes": [ {"type": "Click", "lane": 0, "time": 1.0} ],
    "cubes": [
        {
            "cubeId": 1,
            "cubeName": "Cube_1",
            "cubeNote": "",
            "tracks": [
                { "face": "Up", "direction": "Up", "notes": [
                    { "type": "Click", "lane": 0, "time": 1.0 }
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
| `CubeData` | 单个方体（cubeId, cubeName, cubeNote, 24条tracks） |

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
| [DOTween](http://dotween.demigiant.com/) | UI 动画补间 |
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
| 滚轮上 | 向上滚动 | ✅ (`Editor_ScrollUp`) |
| 滚轮下 | 向下滚动 | ✅ (`Editor_ScrollDown`) |
| Ctrl + 滚轮上 | 放大 | ✅ (`Editor_ZoomIn`) |
| Ctrl + 滚轮下 | 缩小 | ✅ (`Editor_ZoomOut`) |
| Ctrl + E | 打开设置（Editor 模式） | ❌ |
| Esc | 关闭设置 / 返回 | ❌ |
