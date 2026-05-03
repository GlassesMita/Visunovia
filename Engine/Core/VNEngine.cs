using System.ComponentModel;
using System.IO;
using System.Xml.Linq;
using Visunovia.Engine.Script;
using Visunovia.Engine.Editor;

namespace Visunovia.Engine.Core;

public class VNEngine : INotifyPropertyChanged
{
    private static VNEngine? _instance;
    public static VNEngine Instance => _instance ??= new VNEngine();

    private VNState _state;
    private readonly VNResourceManager _resourceManager;
    private readonly VNScriptRunner _scriptRunner;

    public event PropertyChangedEventHandler? PropertyChanged;

    public VNState State
    {
        get => _state;
        private set
        {
            _state = value;
            OnPropertyChanged(nameof(State));
        }
    }

    public VNResourceManager ResourceManager => _resourceManager;
    public VNScriptRunner ScriptRunner => _scriptRunner;

    public VNEngine()
    {
        _state = new VNState();
        _resourceManager = new VNResourceManager();
        _scriptRunner = new VNScriptRunner(this);
    }

    public void Initialize(string projectPath)
    {
        _resourceManager.LoadProject(projectPath);
        _state.CurrentScene = "start";
    }

    public VNProject? LoadProject(string path)
    {
        try
        {
            var projectRoot = Path.GetDirectoryName(path) ?? path;
            if (string.IsNullOrEmpty(projectRoot))
            {
                projectRoot = path;
            }

            if (!File.Exists(path))
            {
                var tlorPath = Path.Combine(path, "Project.tlor");
                if (File.Exists(tlorPath))
                {
                    path = tlorPath;
                    projectRoot = path;
                }
                else
                {
                    return null;
                }
            }

            var content = File.ReadAllText(path);
            var doc = XDocument.Parse(content);
            var root = doc.Root;

            if (root == null)
            {
                return null;
            }

            var project = new VNProject();
            project.Metadata = new VNMetadata
            {
                Title = root.Element("metadata")?.Element("title")?.Value ?? "未命名项目",
                Author = root.Element("metadata")?.Element("author")?.Value ?? "",
                Version = root.Element("metadata")?.Element("version")?.Value ?? "1.0"
            };

            project.Scenes = new List<VNScene>();
            var sceneElements = root.Element("scenes")?.Elements("scene");
            if (sceneElements != null)
            {
                foreach (var sceneElem in sceneElements)
                {
                    var sceneId = sceneElem.Attribute("id")?.Value ?? "unknown";
                    var scriptPath = Path.Combine(projectRoot, "Scripts", "Main", $"{sceneId}.lor");

                    if (File.Exists(scriptPath))
                    {
                        var scene = LoadSceneFromScript(scriptPath);
                        if (scene != null)
                        {
                            scene.Id = sceneId;
                            project.Scenes.Add(scene);
                        }
                    }
                    else
                    {
                        project.Scenes.Add(new VNScene
                        {
                            Id = sceneId,
                            Bgm = new VNBgm(),
                            Dialogues = new List<VNDialogue>()
                        });
                    }
                }
            }

            return project;
        }
        catch
        {
            return null;
        }
    }

    private VNScene? LoadSceneFromScript(string scriptPath)
    {
        try
        {
            var content = File.ReadAllText(scriptPath);
            var scene = new VNScene();
            scene.Dialogues = new List<VNDialogue>();

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.StartsWith("#"))
                {
                    continue;
                }
                else if (trimmed.StartsWith("bg:"))
                {
                    scene.Background = trimmed.Substring(3).Trim();
                }
                else if (trimmed.StartsWith("bgm:"))
                {
                    var bgmPath = trimmed.Substring(4).Trim();
                    var parts = bgmPath.Split('|');
                    scene.Bgm = new VNBgm { Path = parts[0].Trim() };
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var vol))
                    {
                        scene.Bgm.Volume = vol;
                    }
                }
                else if (trimmed.Contains(':'))
                {
                    var colonIndex = trimmed.IndexOf(':');
                    var speaker = trimmed.Substring(0, colonIndex).Trim();
                    var text = trimmed.Substring(colonIndex + 1).Trim();

                    if (speaker.Contains('&'))
                    {
                        var speakers = speaker.Split('&').Select(s => s.Trim()).ToList();
                    }

                    scene.Dialogues.Add(new VNDialogue
                    {
                        Type = VNDialogueType.Dialogue,
                        Speaker = speaker,
                        Text = text
                    });
                }
            }

            return scene;
        }
        catch
        {
            return null;
        }
    }

    public void Start()
    {
        if (string.IsNullOrEmpty(_state.CurrentScene))
        {
            _state.CurrentScene = "start";
        }
        _state.IsPlaying = true;
        _scriptRunner.RunScene(_state.CurrentScene);
    }

    public void Stop()
    {
        _state.IsPlaying = false;
    }

    public void Next()
    {
        _scriptRunner.ExecuteNext();
    }

    public void SelectChoice(int choiceIndex)
    {
        _scriptRunner.HandleChoice(choiceIndex);
    }

    public void SetBackground(string imageName)
    {
        _state.BackgroundImage = _resourceManager.GetImagePath(imageName);
    }

    public void SetBackground(string imageName, VNTransitionEffect effect, int duration)
    {
        _state.BackgroundImage = _resourceManager.GetImagePath(imageName);
        _state.BackgroundTransition = new VNTransition
        {
            Effect = effect,
            Duration = duration
        };
    }

    public void SetCharacter(string characterId, string expression)
    {
        if (!_state.Characters.TryGetValue(characterId, out var character))
        {
            character = new VNCharacter { Id = characterId };
            _state.Characters[characterId] = character;
        }
        character.Expression = expression;
        character.Image = _resourceManager.GetImagePath($"{characterId}_{expression}");
    }

    public void SetCharacter(string characterId, string expression, VNTransitionEffect effect, int duration)
    {
        if (!_state.Characters.TryGetValue(characterId, out var character))
        {
            character = new VNCharacter { Id = characterId };
            _state.Characters[characterId] = character;
        }
        character.Expression = expression;
        character.Image = _resourceManager.GetImagePath($"{characterId}_{expression}");
        character.Transition = new VNTransition
        {
            Effect = effect,
            Duration = duration
        };
    }

    public void PlayBgm(string bgmPath, int volume = 100, int fadeIn = 0)
    {
        _state.CurrentBgmPath = bgmPath;
        _state.CurrentBgmVolume = volume;
        _state.BgmFadeIn = fadeIn;
    }

    public void StopBgm(int fadeOut = 0)
    {
        _state.BgmFadeOut = fadeOut;
        _state.CurrentBgmPath = null;
    }

    public void ShowDialog(string speaker, string text)
    {
        _state.CurrentSpeaker = speaker;
        _state.CurrentText = text;
    }

    public void SetVariable(string name, object value)
    {
        _state.Variables[name] = value;
    }

    public T? GetVariable<T>(string name)
    {
        if (_state.Variables.TryGetValue(name, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}