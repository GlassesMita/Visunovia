using Visunovia.Engine.Core;

namespace Visunovia.Engine.Editor;

/// <summary>
/// 添加场景命令
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
/// 删除场景命令
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
/// 添加对话命令
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
/// 删除对话命令
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

/// <summary>
/// 打包项目命令
/// </summary>
public class PackageProjectCommand : UndoRedoCommand
{
    private readonly EditorService _editor;
    private readonly string _playerTemplatePath;
    private readonly string _outputPath;
    private PackagingService? _packagingService;

    public PackageProjectCommand(EditorService editor, string playerTemplatePath, string outputPath)
    {
        _editor = editor;
        _playerTemplatePath = playerTemplatePath;
        _outputPath = outputPath;
    }

    public override void Execute()
    {
        if (_editor.CurrentProject == null) return;
        _packagingService = new PackagingService(_editor, _playerTemplatePath, _outputPath);
    }

    public override void Undo()
    {
    }

    public PackagingService? GetPackagingService() => _packagingService;

    public override string Description => "打包项目";
}
