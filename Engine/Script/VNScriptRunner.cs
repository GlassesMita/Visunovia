using Visunovia.Engine.Core;

namespace Visunovia.Engine.Script;

public class VNScriptRunner
{
    private readonly VNEngine _engine;
    private readonly VNScriptParser _parser;
    private List<VNScriptCommand> _commands = new();
    private int _currentIndex;

    public VNScriptRunner(VNEngine engine)
    {
        _engine = engine;
        _parser = new VNScriptParser();
    }

    public void RunScene(string sceneName)
    {
        var script = _engine.ResourceManager.LoadScript(sceneName);
        _commands = _parser.Parse(script);
        _currentIndex = 0;
        ExecuteNext();
    }

    public void ExecuteNext()
    {
        if (_currentIndex < _commands.Count)
        {
            var command = _commands[_currentIndex];
            _currentIndex++;
            ExecuteCommand(command);
        }
    }

    private void ExecuteCommand(VNScriptCommand command)
    {
        switch (command.Type)
        {
            case "bg":
            case "background":
                var bgImage = command.Parameters.GetValueOrDefault("value", "");
                _engine.SetBackground(bgImage);
                break;

            case "dialog":
                var speaker = command.Parameters.GetValueOrDefault("speaker", "");
                var text = command.Parameters.GetValueOrDefault("text", "");
                _engine.ShowDialog(speaker, text);
                break;

            default:
                if (!string.IsNullOrEmpty(command.Type))
                {
                    _engine.ShowDialog("", command.Type);
                }
                break;
        }
    }

    public void HandleChoice(int choiceIndex)
    {
        // Handle choice logic
    }
}