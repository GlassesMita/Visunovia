using System.IO;

namespace Visunovia.Engine.Core;

public class VNConfigManager
{
    private static VNConfigManager? _instance;
    public static VNConfigManager Instance => _instance ??= new VNConfigManager();

    public bool IsEditorEnabled { get; private set; }

    private VNConfigManager()
    {
        CheckEditorMode();
    }

    private void CheckEditorMode()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var editorFile = Path.Combine(baseDir, "Editor", "Editor.lorceli");
        IsEditorEnabled = File.Exists(editorFile);
    }
}