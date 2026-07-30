using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HexMap;

/// <summary>
/// 集中式撤回/重做管理器：维护全局操作历史栈，绑定场景中的 Undo/Redo 按钮。
/// 各管理器（InfoManagerUI、NotePlacementManager、BpmManagerUI）通过 Execute 方法注册操作。
/// </summary>
public static class UndoRedoManager
{
    private static readonly Stack<(Action undo, Action redo)> s_undoStack = new Stack<(Action, Action)>();
    private static readonly Stack<(Action undo, Action redo)> s_redoStack = new Stack<(Action, Action)>();
    private static Button s_undoButton;
    private static Button s_redoButton;

    // 快捷键的 Action 名（与 Settings 页面中一致，用于 KeyBindingsStore 持久化）
    private const string k_actionUndo = "Editor_Undo";
    private const string k_actionRedo = "Editor_Redo";

    // 缓存的组合键（从 KeyBindingsStore 加载）
    private static KeyCombo s_undoCombo;
    private static KeyCombo s_redoCombo;
    private static bool s_combosLoaded;

    /// <summary>
    /// 在场景中查找 Undo/Redo 按钮并绑定点击事件。由 EditorInit 在 Awake 中调用。
    /// 同时从 KeyBindingsStore 加载快捷键绑定。
    /// </summary>
    public static void Initialize()
    {
        // 查找 Undo 按钮
        var undoObj = UnityEngine.GameObject.Find("Undo");
        if (undoObj != null)
        {
            s_undoButton = undoObj.GetComponent<Button>();
            if (s_undoButton != null)
            {
                s_undoButton.onClick.AddListener(Undo);
            }
        }

        // 查找 Redo 按钮
        var redoObj = UnityEngine.GameObject.Find("Redo");
        if (redoObj != null)
        {
            s_redoButton = redoObj.GetComponent<Button>();
            if (s_redoButton != null)
            {
                s_redoButton.onClick.AddListener(Redo);
            }
        }

        // 加载快捷键绑定
        LoadShortcuts();

        UpdateButtons();
    }

    /// <summary>
    /// 从 KeyBindingsStore 加载撤回/重做快捷键。
    /// 默认：Undo = Ctrl+Z，Redo = Ctrl+Y（也支持 Ctrl+Shift+Z）。
    /// </summary>
    private static void LoadShortcuts()
    {
        s_undoCombo = KeyBindingsStore.GetKeyCombo(k_actionUndo, KeyCombo.Parse("Ctrl + Z"));
        s_redoCombo = KeyBindingsStore.GetKeyCombo(k_actionRedo, KeyCombo.Parse("Ctrl + Y"));
        s_combosLoaded = true;
    }

    /// <summary>
    /// 重新加载快捷键绑定。Setting 场景关闭后由 EditorInit 调用。
    /// </summary>
    public static void ReloadShortcuts()
    {
        LoadShortcuts();
    }

    /// <summary>
    /// 注册一个已执行的操作到撤回栈。操作本身已由调用者完成，此方法仅记录历史。
    /// 新操作会清空重做栈。
    /// </summary>
    public static void Execute(Action undo, Action redo)
    {
        if (undo == null || redo == null) return;

        s_undoStack.Push((undo, redo));
        s_redoStack.Clear();
        UpdateButtons();
    }

    /// <summary>
    /// 撤回上一步操作：执行 undo 回调，将操作移入重做栈。
    /// </summary>
    public static void Undo()
    {
        if (s_undoStack.Count == 0) return;

        var (undo, redo) = s_undoStack.Pop();
        undo?.Invoke();
        s_redoStack.Push((undo, redo));
        UpdateButtons();

        UnityEngine.Debug.Log($"[UndoRedoManager] 撤回成功，剩余 {s_undoStack.Count} 步可撤回，{s_redoStack.Count} 步可重做");
    }

    /// <summary>
    /// 重做上一步撤回的操作：执行 redo 回调，将操作移回撤回栈。
    /// </summary>
    public static void Redo()
    {
        if (s_redoStack.Count == 0) return;

        var (undo, redo) = s_redoStack.Pop();
        redo?.Invoke();
        s_undoStack.Push((undo, redo));
        UpdateButtons();

        UnityEngine.Debug.Log($"[UndoRedoManager] 重做成功，剩余 {s_undoStack.Count} 步可撤回，{s_redoStack.Count} 步可重做");
    }

    /// <summary>
    /// 清空所有历史记录并解除按钮引用。切换谱面时由 EditorInit 调用。
    /// </summary>
    public static void Clear()
    {
        s_undoStack.Clear();
        s_redoStack.Clear();
        s_undoButton = null;
        s_redoButton = null;
        s_combosLoaded = false;
    }

    /// <summary>
    /// 每帧检测撤回/重做快捷键。由 EditorInit.Update 调用。
    /// 支持用户自定义组合键（通过 KeyBindingsStore 持久化）。
    /// 当焦点在文本输入框上时跳过，避免与 TMP_InputField 内置文本撤回冲突。
    /// </summary>
    public static void ProcessKeyboardShortcuts()
    {
        // 文本输入框获焦时跳过全局快捷键
        if (IsTextInputFocused())
        {
            return;
        }

        if (!s_combosLoaded)
        {
            LoadShortcuts();
        }

        // 检测 Undo 快捷键
        if (s_undoCombo.IsPressed())
        {
            Undo();
            return;
        }

        // 检测 Redo 快捷键
        if (s_redoCombo.IsPressed())
        {
            Redo();
            return;
        }

        // 兼容默认的 Ctrl+Shift+Z 重做（即使用户未修改绑定）
        // 仅在 Redo 绑定不是 Ctrl+Shift+Z 时生效
        KeyCombo ctrlShiftZ = new KeyCombo(true, true, false, UnityEngine.KeyCode.Z);
        if (ctrlShiftZ.IsPressed() && !s_redoCombo.Equals(ctrlShiftZ))
        {
            Redo();
        }
    }

    /// <summary>
    /// 检测当前是否有 TMP_InputField 获得焦点。
    /// </summary>
    private static bool IsTextInputFocused()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
        {
            return false;
        }

        return eventSystem.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }

    private static void UpdateButtons()
    {
        if (s_undoButton != null)
        {
            s_undoButton.interactable = s_undoStack.Count > 0;
        }

        if (s_redoButton != null)
        {
            s_redoButton.interactable = s_redoStack.Count > 0;
        }
    }
}
