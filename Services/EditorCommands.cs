using Visunovia.Models.Engine;

namespace Visunovia.Services;

/// <summary>
/// 撤销/重做命令接口，定义命令的执行、撤销及描述
/// </summary>
public interface IUndoRedoCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

/// <summary>
/// 撤销/重做命令基类，提供统一的命令抽象
/// </summary>
public abstract class UndoRedoCommand : IUndoRedoCommand
{
    public abstract void Execute();
    public abstract void Undo();
    public abstract string Description { get; }
}

/// <summary>
/// 添加场景命令：将新场景添加到项目场景列表末尾
/// </summary>
public class AddSceneCommand : UndoRedoCommand
{
    private readonly EditorService _editor;
    private readonly VNScene _scene;
    private int _insertedIndex;

    public AddSceneCommand(EditorService editor, VNScene scene)
    {
        _editor = editor;
        _scene = scene;
    }

    public override void Execute()
    {
        if (_editor.CurrentProject == null) return;
        _editor.CurrentProject.Scenes.Add(_scene);
        _insertedIndex = _editor.CurrentProject.Scenes.Count - 1;
        _editor.MarkAsModified();
    }

    public override void Undo()
    {
        if (_editor.CurrentProject == null) return;
        _editor.CurrentProject.Scenes.RemoveAt(_insertedIndex);
        _editor.MarkAsModified();
    }

    public override string Description => "添加场景";
}

/// <summary>
/// 删除场景命令：从项目场景列表中移除指定场景，撤销时恢复到原位置
/// </summary>
public class RemoveSceneCommand : UndoRedoCommand
{
    private readonly EditorService _editor;
    private readonly string _sceneId;
    private VNScene? _removedScene;
    private int _removedIndex;

    public RemoveSceneCommand(EditorService editor, string sceneId)
    {
        _editor = editor;
        _sceneId = sceneId;
    }

    public override void Execute()
    {
        if (_editor.CurrentProject == null) return;
        var scene = _editor.CurrentProject.Scenes.FirstOrDefault(s => s.Id == _sceneId);
        if (scene != null)
        {
            _removedScene = scene;
            _removedIndex = _editor.CurrentProject.Scenes.IndexOf(scene);
            _editor.CurrentProject.Scenes.RemoveAt(_removedIndex);
            _editor.MarkAsModified();
        }
    }

    public override void Undo()
    {
        if (_editor.CurrentProject == null || _removedScene == null) return;
        _editor.CurrentProject.Scenes.Insert(_removedIndex, _removedScene);
        _editor.MarkAsModified();
    }

    public override string Description => "删除场景";
}

/// <summary>
/// 添加对话命令：将新对话添加到指定场景的对话列表末尾
/// </summary>
public class AddDialogueCommand : UndoRedoCommand
{
    private readonly EditorService _editor;
    private readonly string _sceneId;
    private readonly VNDialogue _dialogue;
    private int _insertedIndex;

    public AddDialogueCommand(EditorService editor, string sceneId, VNDialogue dialogue)
    {
        _editor = editor;
        _sceneId = sceneId;
        _dialogue = dialogue;
    }

    public override void Execute()
    {
        var scene = _editor.GetScene(_sceneId);
        if (scene != null)
        {
            scene.Dialogues.Add(_dialogue);
            _insertedIndex = scene.Dialogues.Count - 1;
            _editor.MarkAsModified();
        }
    }

    public override void Undo()
    {
        var scene = _editor.GetScene(_sceneId);
        if (scene != null)
        {
            scene.Dialogues.RemoveAt(_insertedIndex);
            _editor.MarkAsModified();
        }
    }

    public override string Description => "添加对话";
}

/// <summary>
/// 删除对话命令：从指定场景的对话列表中移除指定索引的对话，撤销时恢复到原位置
/// </summary>
public class RemoveDialogueCommand : UndoRedoCommand
{
    private readonly EditorService _editor;
    private readonly string _sceneId;
    private readonly int _dialogueIndex;
    private VNDialogue? _removedDialogue;

    public RemoveDialogueCommand(EditorService editor, string sceneId, int dialogueIndex)
    {
        _editor = editor;
        _sceneId = sceneId;
        _dialogueIndex = dialogueIndex;
    }

    public override void Execute()
    {
        var scene = _editor.GetScene(_sceneId);
        if (scene != null && _dialogueIndex >= 0 && _dialogueIndex < scene.Dialogues.Count)
        {
            _removedDialogue = scene.Dialogues[_dialogueIndex];
            scene.Dialogues.RemoveAt(_dialogueIndex);
            _editor.MarkAsModified();
        }
    }

    public override void Undo()
    {
        var scene = _editor.GetScene(_sceneId);
        if (scene != null && _removedDialogue != null)
        {
            scene.Dialogues.Insert(_dialogueIndex, _removedDialogue);
            _editor.MarkAsModified();
        }
    }

    public override string Description => "删除对话";
}
