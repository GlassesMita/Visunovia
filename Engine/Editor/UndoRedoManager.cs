using System.Collections.Generic;

namespace Visunovia.Engine.Editor;

/// <summary>
/// 撤销/重做管理器
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

    public void Undo()
    {
        if (!CanUndo) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        HistoryChanged?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        HistoryChanged?.Invoke();
    }

    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke();
    }
}
