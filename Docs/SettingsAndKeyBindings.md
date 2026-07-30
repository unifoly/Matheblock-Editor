# 技术文档：设置持久化与组合键支持

## 概述

本次更新为 Matheblock Editor 添加了 JSON 文件持久化系统和完整的组合键（Combo Key）支持。主要变更包括：

1. **设置数据持久化**：从 PlayerPrefs 迁移到 JSON 文件，更健壮、可读
2. **组合键支持**：快捷键系统全面支持 Ctrl/Shift/Alt 组合键
3. **撤回/重做快捷键可自定义**：UndoRedoManager 改为从 KeyBindingsStore 读取快捷键
4. **Setting 场景新增撤回/重做行**：通过 Unity MCP 添加 RebindButton 组件

---

## 变更文件清单

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Assets/Scripts/KeyBindingsStore.cs` | 重写 | 新增 `KeyCombo` 结构体 + JSON 持久化 |
| `Assets/Scripts/SettingsDataManager.cs` | 重写 | JSON 文件持久化替代 PlayerPrefs |
| `Assets/Scripts/RebindButton.cs` | 修改 | 启动时加载已保存的绑定 |
| `Assets/Scripts/NotePlacementManager.cs` | 修改 | 使用 `KeyCombo` 替代单 KeyCode |
| `Assets/Scripts/UndoRedoManager.cs` | 修改 | 从 KeyBindingsStore 读取快捷键 |
| `Assets/Scripts/EditorInit.cs` | 修改 | Setting 场景关闭后重新加载快捷键 |
| `Assets/Scenes/Setting.unity` | 修改 | 新增 Row_Undo / Row_Redo |

---

## 1. KeyCombo 结构体

**文件**：`Assets/Scripts/KeyBindingsStore.cs`
**命名空间**：`HexMap`

### 设计

`KeyCombo` 是一个可序列化结构体，用于表示一个完整的组合键绑定：

```csharp
[Serializable]
public struct KeyCombo
{
    [SerializeField] private bool m_ctrl;     // 是否需要 Ctrl
    [SerializeField] private bool m_shift;    // 是否需要 Shift
    [SerializeField] private bool m_alt;      // 是否需要 Alt
    [SerializeField] private KeyCode m_mainKey; // 主键
}
```

### 核心方法

| 方法 | 说明 |
|------|------|
| `Parse(string)` | 从显示字符串解析（如 `"Ctrl + Shift + A"`） |
| `ToDisplayString()` | 格式化为显示字符串 |
| `IsPressed()` | 检测当前帧是否触发（修饰键精确匹配） |
| `IsModifierKey(KeyCode)` | 判断按键是否为修饰键 |
| `FormatKeyCode(KeyCode)` | 将 KeyCode 格式化为显示名 |

### 检测逻辑

`IsPressed()` 采用**精确匹配**策略：
- 指定的修饰键必须处于按下状态
- 未指定的修饰键不能处于按下状态
- 主键必须在当前帧 `GetKeyDown`
- 当主键本身是修饰键时（如单独绑定 "Shift"），检测 `GetKeyDown`（左右皆可）

### 显示名映射

`FormatKeyCode` 将 KeyCode 转换为用户友好的显示名，与 `RebindButton.BuildCombinedKeyName` 保持一致：

| KeyCode | 显示名 |
|---------|--------|
| `Alpha0`-`Alpha9` | `0`-`9` |
| `Return` | `Enter` |
| `Escape` | `Esc` |
| `LeftShift`/`RightShift` | `Shift` |
| `LeftControl`/`RightControl` | `Ctrl` |
| `LeftAlt`/`RightAlt` | `Alt` |
| `UpArrow`/`DownArrow` | `↑`/`↓` |
| `Mouse0`/`Mouse1`/`Mouse2` | `鼠标左键`/`鼠标右键`/`鼠标中键` |

---

## 2. KeyBindingsStore - JSON 持久化

**文件**：`Assets/Scripts/KeyBindingsStore.cs`
**命名空间**：`HexMap`

### 持久化路径

```
Application.persistentDataPath/KeyBindings.json
```

### JSON 格式

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

### API

| 方法 | 说明 |
|------|------|
| `GetKeyCombo(actionName, defaultCombo)` | 获取组合键绑定 |
| `GetBinding(actionName, defaultKey)` | 获取显示名字符串（向后兼容） |
| `SetBinding(actionName, keyName)` | 设置绑定（解析字符串为 KeyCombo） |
| `SetKeyCombo(actionName, combo)` | 直接设置 KeyCombo |
| `ResetAll()` | 清除所有自定义绑定 |

### 旧版数据迁移

加载时优先读取 JSON 文件。若文件不存在，自动从旧版 PlayerPrefs 数据迁移：
1. 解析旧格式（`actionName|keyName\n...`）
2. 转换为 KeyCombo 并保存到 JSON
3. 清理 PlayerPrefs 中的旧数据

---

## 3. SettingsDataManager - JSON 持久化

**文件**：`Assets/Scripts/SettingsDataManager.cs`
**命名空间**：`HexMap`

### 持久化路径

```
Application.persistentDataPath/Settings.json
```

### JSON 格式

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

### 旧版数据迁移

与 KeyBindingsStore 类似，首次加载时从 PlayerPrefs 迁移并清理旧数据。

### 持久化字段

| 属性 | 类型 | 默认值 | PlayerPrefs Key（旧） |
|------|------|--------|----------------------|
| `MasterVolume` | float | 1.0 | `Settings_MasterVolume` |
| `MusicVolume` | float | 1.0 | `Settings_MusicVolume` |
| `SFXVolume` | float | 1.0 | `Settings_SFXVolume` |
| `IsFullscreen` | bool | true | `Settings_Fullscreen` |
| `QualityLevel` | int | 2 | `Settings_QualityLevel` |
| `ResolutionIndex` | int | 0 | `Settings_ResolutionIndex` |

---

## 4. RebindButton - 启动时加载绑定

**文件**：`Assets/Scripts/RebindButton.cs`

### 变更

新增 `Start()` 方法，在场景加载后自动从 `KeyBindingsStore` 读取已保存的绑定并显示：

```csharp
private void Start()
{
    LoadSavedBinding();
}

private void LoadSavedBinding()
{
    if (m_keyDisplay == null || string.IsNullOrEmpty(m_actionName))
        return;

    string savedKey = KeyBindingsStore.GetBinding(m_actionName, m_defaultKey);
    m_keyDisplay.text = savedKey;
}
```

### 修复的问题

之前打开设置页面时，RebindButton 不会显示已保存的快捷键绑定，用户无法知道当前绑定了什么键。修复后会自动加载并显示。

### 格式化重构

`BuildCombinedKeyName` 和 `FormatKeyName` 改为调用 `KeyCombo.FormatKeyCode()`，避免代码重复。

---

## 5. NotePlacementManager - 组合键检测

**文件**：`Assets/Scripts/NotePlacementManager.cs`

### 变更

| 旧实现 | 新实现 |
|--------|--------|
| `Dictionary<KeyCode, NoteType> m_hotkeyToType` | `List<(KeyCombo combo, NoteType type)> m_hotkeyList` |
| `ParseKeyName(string)` 手动解析 | `KeyCombo.Parse(string)` 统一解析 |
| `Input.GetKeyDown(keyCode)` | `combo.IsPressed()` |

### 加载流程

```csharp
private void LoadHotkeys()
{
    m_hotkeyList = new List<(KeyCombo, NoteType)>();
    TryAddHotkey(k_actionClick, "Q", NoteType.Click);
    TryAddHotkey(k_actionFlick, "R", NoteType.Flick);
    TryAddHotkey(k_actionDrag, "E", NoteType.Drag);
    TryAddHotkey(k_actionReverseFlick, "T", NoteType.ReverseFlick);
}

private void TryAddHotkey(string actionName, string defaultKey, NoteType type)
{
    KeyCombo defaultCombo = KeyCombo.Parse(defaultKey);
    KeyCombo combo = KeyBindingsStore.GetKeyCombo(actionName, defaultCombo);
    m_hotkeyList.Add((combo.IsValid ? combo : defaultCombo, type));
}
```

### 检测流程

```csharp
private void HandlePlacementInput()
{
    if (!m_isHovering || m_hotkeyList == null) return;
    foreach (var (combo, type) in m_hotkeyList)
    {
        if (combo.IsPressed())
            PlaceNote(type, m_hoveredLane, m_hoveredTime);
    }
}
```

### 场景卸载后重载

Setting 场景关闭后自动重新加载快捷键（用户可能修改了绑定）：

```csharp
private void OnSceneUnloaded(Scene scene)
{
    if (scene.name == "Setting")
        LoadHotkeys();
}
```

---

## 6. UndoRedoManager - 可自定义快捷键

**文件**：`Assets/Scripts/UndoRedoManager.cs`

### 变更

| 旧实现 | 新实现 |
|--------|--------|
| 硬编码 `Ctrl+Z` / `Ctrl+Y` | 从 `KeyBindingsStore` 读取 |
| 无法自定义 | 用户可在设置页面重绑 |
| 无重载机制 | `ReloadShortcuts()` 公共方法 |

### Action 名称

| Action | 默认快捷键 | 说明 |
|--------|-----------|------|
| `Editor_Undo` | `Ctrl + Z` | 撤回 |
| `Editor_Redo` | `Ctrl + Y` | 重做 |

### 兼容性

保留 `Ctrl + Shift + Z` 作为重做的兼容快捷键（仅在 Redo 绑定不是 `Ctrl+Shift+Z` 时生效）：

```csharp
KeyCombo ctrlShiftZ = new KeyCombo(true, true, false, KeyCode.Z);
if (ctrlShiftZ.IsPressed() && !s_redoCombo.Equals(ctrlShiftZ))
    Redo();
```

### 生命周期

- `Initialize()`：EditorInit.Awake 时调用，加载快捷键
- `ReloadShortcuts()`：Setting 场景关闭后由 EditorInit 调用
- `Clear()`：切换谱面时调用，重置 `s_combosLoaded` 标志

---

## 7. EditorInit - 场景卸载监听

**文件**：`Assets/Scripts/EditorInit.cs`

### 新增

```csharp
private void OnEnable()
{
    SceneManager.sceneUnloaded += OnSceneUnloaded;
}

private void OnDisable()
{
    SceneManager.sceneUnloaded -= OnSceneUnloaded;
}

private void OnSceneUnloaded(Scene scene)
{
    if (scene.name == "Setting")
        UndoRedoManager.ReloadShortcuts();
}
```

当 Setting 场景以 Additive 模式卸载后，自动重新加载 UndoRedoManager 的快捷键绑定。

---

## 8. Setting 场景 - 新增撤回/重做行

**文件**：`Assets/Scenes/Setting.unity`

通过 Unity MCP 在 `Page_ShortcutKey` 页面中新增了两行 RebindButton：

### Row_Undo（撤回）

| 属性 | 值 |
|------|-----|
| 位置 | `Page_ShortcutKey/Content` 子级 |
| RebindButton.ActionName | `Editor_Undo` |
| RebindButton.DefaultKey | `Ctrl + Z` |
| KeyText 文本 | `撤回` |

### Row_Redo（重做）

| 属性 | 值 |
|------|-----|
| 位置 | `Page_ShortcutKey/Content` 子级 |
| RebindButton.ActionName | `Editor_Redo` |
| RebindButton.DefaultKey | `Ctrl + Y` |
| KeyText 文本 | `重做` |

### 已知问题

原场景中 RebindButton 的 `m_keyDisplay` 引用已损坏（指向已删除的对象）。新增行的 `m_keyDisplay` 继承了此问题。需在 Unity Editor 中手动修复：将 KeyDisplay 的 TextMeshProUGUI 组件拖入 RebindButton 的 `Key Display` 字段。

---

## 快捷键总览

### 编辑器快捷键

| 快捷键 | 功能 | Action 名 | 可自定义 |
|--------|------|-----------|---------|
| `Ctrl + Z` | 撤回 | `Editor_Undo` | ✅ |
| `Ctrl + Y` | 重做 | `Editor_Redo` | ✅ |
| `Ctrl + Shift + Z` | 重做（兼容） | - | 自动兼容 |
| `Q` | 放置 Click | `Note_Click` | ✅ |
| `R` | 放置 Flick | `Note_Flick` | ✅ |
| `E` | 放置 Drag | `Note_Drag` | ✅ |
| `T` | 放置 ReverseFlick | `Note_ReverseFlick` | ✅ |
| `Ctrl + E` | 打开设置 | - | ❌（菜单项） |
| `Esc` | 关闭设置 | - | ❌ |

### 组合键格式

组合键以 `" + "` 分隔，修饰键顺序固定为 `Ctrl → Shift → Alt → 主键`：

- `Ctrl + Z`
- `Ctrl + Shift + Z`
- `Ctrl + Alt + S`
- `Shift + F1`
- `Q`（无修饰键）

---

## 持久化文件位置

| 文件 | 路径 | 内容 |
|------|------|------|
| `Settings.json` | `Application.persistentDataPath/` | 音量、画质、全屏等设置 |
| `KeyBindings.json` | `Application.persistentDataPath/` | 快捷键绑定 |

Windows 默认路径：`C:\Users\<用户名>\AppData\LocalLow\<公司名>\<产品名>\`
