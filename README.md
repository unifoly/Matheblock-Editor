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
| `SettingsDataManager` | PlayerPrefs 持久化（音量、全屏、画质） |
| `KeyBindingsStore` | 快捷键绑定存储，导出 JSON |
| `RebindButton` | 点击 5 秒内捕获按键（支持 Ctrl/Shift/Alt 组合） |
| `EditorOpenSettings` | 编辑器内以 Additive 模式叠加设置场景 |

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

| 快捷键 | 功能 |
|--------|------|
| Ctrl + E | 打开设置（Editor 模式） |
| Esc | 关闭设置 / 返回 |
| 滚轮 | 时间轴滚动 |
| Ctrl + 滚轮 | 缩放网格 |
| Ctrl + = / - | 缩放网格 |
| ↑↓ 方向键 | 时间轴滚动 |
