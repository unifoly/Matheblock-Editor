# Matheblock Editor 技术文档

> 基于 Unity 的节奏游戏谱面编辑器。本文档基于一次全量代码审查（2026-08-01）与优化整理后的代码状态编写，可作为架构理解、二次开发与维护的参考。

---

## 1. 项目概述

Matheblock Editor 是一款节奏游戏谱面编辑器，围绕「正方体」构建 24 条 Note 轨道（6 面 × 4 方向），支持：

- 变 BPM 的节拍网格编辑（时间轴水平线 + 轨道垂直线）
- Note 放置（Click / Flick / Drag / ReverseFlick）
- 方体属性缓动编辑（15 个数据槽的关键帧 + DOTween 缓动曲线）
- 3D 方体实时预览（CubeCamera + RenderTexture）与放映模式
- 完整设置系统（音量/画质/全屏 + 全量可自定义快捷键）

## 2. 技术栈与环境

| 项目 | 版本 / 说明 |
|------|-------------|
| Unity | 2022.3.53f1c1（Built-in Render Pipeline，旧版 Input Manager） |
| 脚本语言 | C# |
| UI 框架 | UI Toolkit（设置场景）+ uGUI / TextMesh Pro（编辑器场景） |
| 动画引擎 | DOTween 1.2.x（`DOVirtual.EasedValue` 插值） |
| 文件对话框 | StandaloneFileBrowser |
| 命名空间 | `HexMap`（设置/键位相关）+ 全局命名空间（编辑器脚本） |

## 3. 项目结构

```
Assets/
│   ├── Scripts/                      # 核心脚本（35 个 .cs）
│   │   ├── CubeSystem/               # 方体系统：数据模型 / 管理器 / 可视化 / 放映控制
│   │   │   ├── CubeEnums.cs          #   CubeFace / FaceDirection 枚举
│   │   │   ├── CubeDataModels.cs     #   CubeData / CubeNoteTrackData 等序列化模型
│   │   │   ├── CubeManager.cs        #   方体创建/选择/持久化 + RenderTexture 显示
│   │   │   ├── CubeManagerUI.cs      #   方体管理面板 UI + UpperList 快捷选择
│   │   │   ├── CubeVisualizer.cs     #   3D 可视化（12 棱 + 6 面）
│   │   │   └── PlaybackModeController.cs  # 放映模式：淡出/淡入 + 3D Note 下落
│   │   ├── RuntimePlayback/          # 放映驱动（运行时组件）
│   │   │   ├── ChartPlaybackController.cs  # 谱面加载 + 热重载 + 动画驱动
│   │   │   ├── CubeAnimator.cs       #   13 个方体级缓动槽 → Transform 应用
│   │   │   ├── EasingEvaluator.cs    #   DOTween Ease 锚点间插值求值
│   │   │   ├── HitEffectManager.cs   #   打击特效粒子对象池
│   │   │   └── PlaybackDataModels.cs #   放映期数据模型
│   │   ├── Debug/
│   │   │   └── EventSystemDebug.cs   #   事件系统诊断（原 SplashDiagnose.cs 更名）
│   │   ├── Editor/
│   │   │   └── SettingsEditorBridge.cs   # 编辑器侧设置桥接
│   │   ├── LayerConstants.cs         #   Layer 常量集中定义（Ui=5 / Cube=8）
│   │   └── 其余 20+ 脚本              #   编辑器核心 / 设置系统 / 缓动区 / BPM 管理
├── Shaders/                      # GaussianBlur / Aurora / Blur 等
├── Maps/                         # 谱面数据（chart.json / music.mp3 / illustration.png）
├── Scenes/                       # 场景文件
└── Plugins/                      # DOTween / StandaloneFileBrowser / TimerManager
```

## 4. 系统架构与核心流程

### 4.1 谱面数据双文件工作流

| 文件 | 作用 | 生命周期 |
|------|------|----------|
| `chart.json` | 正式谱面文件 | 启动时复制为 tmp，保存时覆写 |
| `chart.tmp` | 编辑期工作副本 | `EditorInit.CopyChartToTemp()` 生成，退出时清理 |

所有保存路径统一收敛到 `EditorInit.PersistToChartJson()`：先由 `CubeManager.SaveCubesToJson()` 把内存方体/锚点写入 tmp，再 `File.Copy(tmp, json, true)` 覆写正式文件。调用点：Info 面板 Save、BPM 面板 Save、`OnApplicationQuit`（先持久化再删 tmp，避免锚点丢失）。

**关键时序约束**：
- `CubeManager.LoadCubesFromJson()` 必须在 `Start()` 而非 `Awake()` 调用，确保 `EditorInit.Awake` 已设置 ChartPath 并生成 tmp
- `RefreshChartPlayback` 不调用 `SaveCubesToJson`，避免启动时内存默认数据覆盖文件数据
- Info/BPM 面板 Save 监听器在 `OnDisable` 中不移除（锚点编辑面板会禁用 FunctionChanger 触发 OnDisable，移除会导致 Save 失效）

### 4.2 方体系统（Cube System）

```
CubeManager
├── 数据层：CubeData（cubeId / cubeName / 24 条 tracks / 15 个 easingSlots）
├── 可视化层：CubeVisualizer（12 条白色不透明棱 + 6 个 80% 透明面）
└── 显示层：CubeCamera → RenderTexture → RawImage（PlayScreen 首个子物体）
```

- **轨道组织**：6 面 × 4 方向 = 24 条轨道，按 `CubeFace + FaceDirection` 唯一标识；切换面/方向时先 `SaveCurrentNotesToCubeTrack()` 再 `LoadActiveTrackNotes()`
- **渲染隔离**：方体在 Layer 8（`LayerConstants.Cube`），CubeCamera 仅渲染该层，主相机剔除该层；UI 元素统一 Layer 5（`LayerConstants.Ui`）
- **RT 生命周期**：RenderTexture 在 `OnDestroy()` 中 `Release()` + 销毁，避免 AddComponent/重载泄漏

### 4.3 缓动系统（Easing Area）

编辑器右侧为方体 15 个属性提供关键帧可视化编辑：

| 槽位 | 属性 | 默认值 |
|------|------|--------|
| 0-2 | 长宽高 lx/ly/lz | 100（百分比，100=原始大小） |
| 3-5 | 倾斜角 rx/ry/rz | 0 |
| 6-8 | 位置 px/py/pz | 0 |
| 9-12 | 颜色 R/G/B/A | 0.9/0.9/0.9/1 |
| 13 | 棱偏移 | 0 |
| 14 | 流速 | 30 |

插值公式（`EasingSlotData.EvaluateAt`）：

```
easedT    = DOVirtual.EasedValue(0, 1, t, easingType)
weightedT = Lerp(t, easedT, weight)
result    = Lerp(curr.value, next.value, weightedT)
```

曲线可视化使用 Image 线段池（每段 24 等分采样），闲置线段 `SetActive(false)` 复用。

### 4.4 放映模式（PlaybackModeController + RuntimePlayback）

| 组件 | 职责 |
|------|------|
| `PlaybackModeController` | 监听播放状态：淡出网格/Note/缓动区/标定线，切换相机放映模式，3D Note 下落 |
| `ChartPlaybackController` | 从 tmp 加载谱面、发现/注册方体、驱动动画、文件变更热重载（0.5s 限频轮询） |
| `CubeAnimator` | 13 个方体级缓动槽 → scale/rotation/position/color；重复初始化前先 `RestoreOriginal()` |
| `HitEffectManager` | 打击特效粒子对象池（`UnityEngine.Pool.ObjectPool<T>`） |
| `EasingEvaluator` | DOTween Ease 锚点间插值 |

**Note 下落**：Note 作为 3D SpriteRenderer 挂在方体 Transform 下，从相机视野外开始下落（`startDist = viewHalfExtent + noteHalf + 0.05f`），命中棱外边缘时销毁。垂直下落取 `orthoSize`，水平下落取 `orthoSize * aspect`。

### 4.5 设置系统

- `SettingsDataManager`：JSON 持久化（`Settings.json`），首次运行从 PlayerPrefs 迁移；静态构造函数 try-catch + 加载值范围校验（音量 `Clamp01`、画质 `Clamp`、自动保存 `Max(0)`）
- `KeyBindingsStore`：快捷键绑定（`KeyBindings.json`），`KeyCombo` 支持 Ctrl/Shift/Alt/滚轮组合；`GetModifierVariants()` 使用静态只读数组缓存，避免每帧 `new[]` 分配
- `RebindButton`：点击后 5 秒捕获按键，Esc 视为取消（不保存）
- `SettingsSceneController`：Escape 仅在 `SceneManager.GetActiveScene().name == "Setting"` 时响应，避免 Additive 叠加时拦截编辑器 Esc 操作

### 4.6 网格系统（GridManager + GridScrollHandler）

- 垂直线（轨道）：XLine 输入框控制数量，分布左半 Note 区；水平线（时间轴）：YLine 控制密度，按 BPM 节点动态间距，水平线使用对象池 + Image 引用缓存避免每帧创建/GetComponent
- 滚轮垂直滚动时间轴，Ctrl+滚轮缩放（0.1x~8x，`k_zoomStep=1.1`）；方向键左右不控制滚动
- Display 模式由 `m_followPlayback` 接管：滑块回调跳过全量重绘，`SetScrollOffsetToTime()` 仅轻量重绘水平线
- 缩放通过 `EffectivePixelsPerSecond` 只改变视觉密度，`m_cachedIntervalFactor` 独立于像素密度，节拍位置不受缩放影响

## 5. 本次代码审查与优化记录

审查范围：审查阶段全部 34 个脚本（3 个并行子代理分组深度只读审查；此后新增 `LayerConstants.cs`，现共 35 个）。问题按严重程度分级，修复结果如下。

### 5.1 P0 严重缺陷（已全部修复）

| 文件 | 问题 | 修复 |
|------|------|------|
| `ChartPlaybackController` | `Update()` 音频结束检测在未分配 clip 时 NRE | 增加 `clip != null` 短路 |
| `HitEffectManager` | 方体销毁后粒子引用失效导致 MissingReferenceException 刷屏 | `p.View == null` 防护 + `SpawnOne()` 空父级防护 + `OnDestroy()` 释放纹理 |
| `BpmManagerUI` | 全局 Regex 舍入 2 位破坏 notes/cubes 精度；区域设置差异导致解析异常 | 舍入改 6 位（`"0.######"`），统一 `CultureInfo.InvariantCulture` |
| `ChartSelect` | 单目录缺 chart.json/JSON 损坏中断整个谱面列表 | 逐项 try-catch + Warning 跳过；新建谱面增加路径校验与失败清理 |

### 5.2 P1 潜在风险（已全部修复）

| 文件 | 问题 | 修复 |
|------|------|------|
| `EditorInit` | 文件 IO / 对象查找无容错 | "Time" 判空、`CopyChartToTemp` try-catch、`LoadIllustration`/`LoadAudioClip` 判空链 |
| `SettingsSceneController` | Additive 模式下 Escape 误拦截编辑器 | 场景名限定 |
| `MusicTimeStampController` | 除零风险；Start 时捕获总时长为 0 导致显示错误 | `MusicTime > 0` 防护；实时读取 |
| `RebindButton` | 捕获 Esc 被当作绑定保存 | Esc 视为取消 |
| `EasingAreaManager` | `bars` 判空后未初始化即 Insert | 先 `new List<EasingBar>()` |
| `PlaybackModeController` | 文件变更检测无限频；`GetFlowSpeed()` 死代码 | 0.5s 限频；流速接入 lookAhead 计算 |
| `NotePlacementManager` | 每帧 `GetComponent` 分配（遗留） | 见 §7 遗留建议 |

### 5.3 P2 清理与优化（已完成）

**死代码删除**：`CubeData.GetTrackKey()`、`CubeConstants` 类、`SettingsDataManager.s_initialized`、`EasingAreaManager.m_lastGridScrollOffset`、`GridManager.DrawHorizontalLines` 的 posLog StringBuilder、`CubeManager` 空 Awake、`SettingsMenuController` 死字段、`PlaybackModeController.k_noteRemoveDelay` 未用常量。

**常量提取**：新增 `LayerConstants.cs`（`Ui=5` / `Cube=8`），替换 7 个文件 23 处 `layer = 5` 魔法数字；`CubeManager.k_uiLayer`/`k_cubeLayer` 同步收敛到共享常量；`SetPlaybackCameraMode` 的 `0.8f` 改用 `k_cameraOrthoSize`。

**健壮性**：`SettingsDataManager` 静态构造 try-catch；加载值范围校验；`ImageTypeManager.SetImageToString` 改用 `MemoryStream + CopyTo` 完整读取，`GetTextureByString` 增加 FormatException 处理；`CubeManager` RenderTexture 尺寸最小 1×1、`OnDestroy()` 释放。

**一致性**：`PlaybackDataModels.PlaybackCubeData.lengthX/Y/Z` 默认值改为 `100f`（与编辑器 `CubeData` 一致）。

**事件/UI**：`CubeManagerUI.OnEnable` 恢复事件订阅（面板已构建时失活重激不丢事件）；`AnchorPointEditorUI` 下拉/滑块改 `SetValueWithoutNotify`（避免打开面板触发回调静默改值写盘）。

**工程问题**：`SplashDiagnose.cs` 文件名与类名不匹配（Unity 无法挂载）→ 更名 `EventSystemDebug.cs`，日志用 `#if UNITY_EDITOR` 包裹。

### 5.4 性能改进

- `KeyBindingsStore.GetModifierVariants()`：每帧 `new[]` 分配 → 静态只读数组缓存
- `GridManager`：播放期滑块全量重绘 → `m_followPlayback` 跳转 + 水平线轻量重绘（既有机制，审查确认保留）
- `PlaybackModeController`：文件变更检测 → 0.5s 限频轮询

## 6. 代码规范执行情况

审查与修改遵循 `.trae/rules/UnityCodeStyleInstructions.md`：

- **字段命名**：私有 `m_` 前缀、常量 `k_` 前缀、静态 `s_` 前缀
- **代码风格**：Allman 大括号、单语句 `if` 带大括号、行宽 ≤120-140
- **组织顺序**：Fields → Properties → Events → MonoBehaviour 生命周期 → Public → Private
- **事件**：过去时动词事件名 + `On` 前缀触发方法，`OnEnable`/`OnDisable` 成对订阅/退订
- **本地化注释**：代码注释保持中文，`[Tooltip]`/`[Header]` 说明序列化字段

本次优化新增的 `LayerConstants.cs` 也遵循「集中定义文本/数值常量」的约定。

## 7. 遗留建议与后续优化方向

| 优先级 | 事项 | 说明 |
|--------|------|------|
| P1 | `NotePlacementManager.GetCurrentNotes()` 每帧分配 | 涉及 `PlaybackModeController` 调用方，改动需回归测试，本次未动 |
| P2 | BPM 默认值 120 常量化 | `GridManager`/`BpmManagerUI` 多处硬编码 120，可提取共享常量 |
| P2 | 中文字体加载统一 | `FindFont`/Resources 加载逻辑多处重复，可提炼公共工具 |
| P2 | `ChartJsonData` 统一数据模型 | notes/bpmNodes/cubes 分散读写，可收敛为单一模型类 |
| 建议 | 竖线绘制池化 | 垂直线仍为 `DestroyImmediate + new GameObject`，高频滚动时可有 GC 压力（`m_followPlayback` 已缓解） |
| 建议 | UI 构建抽公共基类 | `CreateUIObject`/`CreateText`/`PositionElement` 在 5+ 个 UI 管理类中重复，可提炼 `UiFactory` 工具类 |
| 建议 | 设置场景迁移 UI Toolkit | 项目已用 UI Toolkit，编辑器场景 uGUI 为历史遗留 |

## 8. 附录：Layer 与关键常量约定

| 常量 | 值 | 含义 |
|------|-----|------|
| `LayerConstants.Ui` | 5 | 编辑器场景所有 UI 元素 |
| `LayerConstants.Cube` | 8 | 方体专用渲染层（CubeCamera 仅渲染，主相机剔除） |
| `k_cameraOrthoSize` | 0.8 | CubeCamera 正交半高（编辑模式） |
| `k_fixedNoteSize` | 0.18 | 放映 Note 固定世界尺寸 |
| `k_noteZOffset` | -0.55 | Note 相对可见面的 Z 偏移 |
| `k_lookAhead` | 3s | Note 生成提前量 |

> 谱面 JSON 格式、快捷键表、设置文件格式等细节见 [README.md](README.md)。
