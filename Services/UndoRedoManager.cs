namespace Visunovia.Services;

/// <summary>
/// 撤销/重做管理器，维护操作历史栈并支持命令的执行、撤销与重做
/// </summary>
public class UndoRedoManager
{
    private readonly Stack<IUndoRedoCommand> _undoStack = new Stack<IUndoRedoCommand>();
    private readonly Stack<IUndoRedoCommand> _redoStack = new Stack<IUndoRedoCommand>();
    private const int MaxHistorySize = 100;

    public event Action? HistoryChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? NextUndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
    public string? NextRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    /// <summary>
    /// 执行命令并压入撤销栈，同时清空重做栈
    /// </summary>
    /// <param name="command">要执行的撤销/重做命令</param>
    public void ExecuteCommand(IUndoRedoCommand command)
    {
        command.Execute();
        _undoStack.Push(command);

        if (_undoStack.Count > MaxHistorySize)
        {
            var oldest = new List<IUndoRedoCommand>(_undoStack);
            _undoStack.Clear();
            for (int i = 1; i < oldest.Count; i++)
            {
                _undoStack.Push(oldest[i]);
            }
        }

        _redoStack.Clear();
        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// 撤销最近一次操作
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// 重做最近一次撤销的操作
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// 清空所有撤销/重做历史
    /// </summary>
    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke();
    }
}
