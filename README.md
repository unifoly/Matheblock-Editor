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
  ]
}
```

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
