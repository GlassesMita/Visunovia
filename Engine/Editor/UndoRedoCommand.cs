namespace Visunovia.Engine.Editor;

/// <summary>
/// 撤销/重做命令接口
/// </summary>
public interface IUndoRedoCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

/// <summary>
/// 撤销/重做命令基类
/// </summary>
public abstract class UndoRedoCommand : IUndoRedoCommand
{
    public abstract void Execute();
    public abstract void Undo();
    public abstract string Description { get; }
}
