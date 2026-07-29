using System;
using System.Collections.Generic;
using UnityEngine.UI;

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

    /// <summary>
    /// 在场景中查找 Undo/Redo 按钮并绑定点击事件。由 EditorInit 在 Awake 中调用。
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

        UpdateButtons();
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
