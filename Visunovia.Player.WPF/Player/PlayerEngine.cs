using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using System.Windows.Threading;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Visunovia.Player.WPF.Security;

namespace Visunovia.Player.WPF.Player;

public static class WindowHandleProvider
{
    public static IntPtr CurrentHandle { get; set; } = IntPtr.Zero;
}

public class PlayerEngine
{
    private ZipArchive? _archive;
    private readonly ResourceLoader _resourceLoader;
    private readonly IDeserializer _yamlDeserializer;
    private readonly DispatcherTimer _waitTimer;
    private readonly PluginManager _pluginManager;
    private GameProject? _currentProject;
    private VNScene? _currentScene;
    private string? _currentSceneId;
    private int _currentDialogueIndex;
    private readonly Dictionary<string, VNScene> _scenes = new();

    public event Action<string>? SpeakerChanged;
    public event Action<string>? DialogueTextChanged;
    public event Action<string?>? BackgroundChanged;
    public event Action<List<ChoiceData>>? ChoicesChanged;
    public event Action? DialogueAdvanced;
    public event Action? SceneEnded;
    public event Action<string>? BgmChanged;
    public event Action? BgmStopped;
    public event Action<List<VNSprite>>? SpritesChanged;

    public ResourceLoader Resources => _resourceLoader;
    public PluginManager Plugins => _pluginManager;
    public GameProject? CurrentProject => _currentProject;
    public string? CurrentSceneId => _currentSceneId;
    public bool IsPlaying { get; private set; }

    public PlayerEngine()
    {
        _resourceLoader = new ResourceLoader();
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        _waitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _waitTimer.Tick += OnWaitTimerTick;

        _pluginManager = new PluginManager();
        LoadPlugins();
    }

    private void LoadPlugins()
    {
        var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        _pluginManager.LoadPlugins(pluginsDir);
    }

    private bool _isWaiting = false;
    private int _waitSeconds = 0;
    private int _waitTicks = 0;

    private void OnWaitTimerTick(object? sender, EventArgs e)
    {
        _waitTicks++;
        if (_waitTicks >= _waitSeconds)
        {
            _waitTimer.Stop();
            _isWaiting = false;
            Advance();
        }
    }

    public bool LoadGame(string lorePath, string password)
    {
        try
        {
            _archive?.Dispose();
            _archive = SimpleCryptoHelper.OpenEncryptedPackage(lorePath, password);
            if (_archive == null)
            {
                return false;
            }

            _resourceLoader.LoadFromArchive(_archive);

            var projectJson = _resourceLoader.GetText("project.json");
            if (string.IsNullOrEmpty(projectJson))
            {
                var tlorContent = _resourceLoader.GetText("Project.tlor");
                if (!string.IsNullOrEmpty(tlorContent))
                {
                    _currentProject = ParseProjectFromXml(tlorContent);
                }
            }
            else
            {
                _currentProject = JsonSerializer.Deserialize<GameProject>(projectJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            LoadAllScenes();
            return _currentProject != null && _scenes.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public bool LoadGameFromZip(string zipPath)
    {
        try
        {
            _archive?.Dispose();
            _archive = SimpleCryptoHelper.OpenZipPackage(zipPath);
            if (_archive == null)
            {
                return false;
            }

            _resourceLoader.LoadFromArchive(_archive);

            var projectJson = _resourceLoader.GetText("project.json");
            if (string.IsNullOrEmpty(projectJson))
            {
                var tlorContent = _resourceLoader.GetText("Project.tlor");
                if (!string.IsNullOrEmpty(tlorContent))
                {
                    _currentProject = ParseProjectFromXml(tlorContent);
                }
            }
            else
            {
                _currentProject = JsonSerializer.Deserialize<GameProject>(projectJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            LoadAllScenes();
            return _currentProject != null && _scenes.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private void LoadAllScenes()
    {
        _scenes.Clear();
        if (_archive == null) return;

        foreach (var entry in _archive.Entries)
        {
            if (entry.FullName.StartsWith("Scripts/Main/") && entry.FullName.EndsWith(".lor"))
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(content))
                    continue;

                try
                {
                    var scene = _yamlDeserializer.Deserialize<VNScene>(content);
                    if (scene != null)
                    {
                        var sceneId = Path.GetFileNameWithoutExtension(entry.Name);
                        scene.Id = sceneId;
                        _scenes[sceneId] = scene;
                    }
                }
                catch
                {
                }
            }
        }
    }

    private GameProject? ParseProjectFromXml(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root;
            if (root == null) return null;

            var project = new GameProject();
            project.Metadata = new ProjectMetadata
            {
                Title = root.Element("metadata")?.Element("title")?.Value ?? "未命名",
                Author = root.Element("metadata")?.Element("author")?.Value ?? "",
                Version = root.Element("metadata")?.Element("version")?.Value ?? "1.0"
            };

            return project;
        }
        catch
        {
            return null;
        }
    }

    public void StartScene(string sceneId)
    {
        if (!_scenes.ContainsKey(sceneId)) return;

        _currentSceneId = sceneId;
        _currentScene = _scenes[sceneId];
        _currentDialogueIndex = 0;
        IsPlaying = true;

        if (_currentScene?.Bgm != null && !string.IsNullOrEmpty(_currentScene.Bgm.Path))
        {
            BgmChanged?.Invoke(_currentScene.Bgm.Path);
        }

        Advance();
    }

    public void Start()
    {
        if (_currentProject?.Metadata != null)
        {
            StartScene("start");
        }
    }

    public void Advance()
    {
        if (_isWaiting) return;

        if (string.IsNullOrEmpty(_currentSceneId) || _currentScene == null)
            return;

        if (_currentScene.Dialogues == null || _currentDialogueIndex >= _currentScene.Dialogues.Count)
        {
            SceneEnded?.Invoke();
            return;
        }

        var dialogue = _currentScene.Dialogues[_currentDialogueIndex];
        var dialogueType = dialogue.GetDialogueType();

        switch (dialogueType)
        {
            case VNDialogueType.Event:
                ProcessEventNode(dialogue);
                return;

            case VNDialogueType.Branch:
                ProcessBranchNode(dialogue);
                return;

            case VNDialogueType.Dialogue:
            default:
                SpeakerChanged?.Invoke(dialogue.Speaker ?? "");
                DialogueTextChanged?.Invoke(dialogue.Text ?? "");
                ChoicesChanged?.Invoke(new List<ChoiceData>());

                if (dialogue.Sprites != null && dialogue.Sprites.Count > 0)
                {
                    SpritesChanged?.Invoke(dialogue.Sprites);
                }
                else
                {
                    SpritesChanged?.Invoke(new List<VNSprite>());
                }
                break;
        }

        DialogueAdvanced?.Invoke();
        _currentDialogueIndex++;
    }

    private void ProcessEventNode(VNDialogue dialogue)
    {
        if (dialogue.Event == null)
        {
            _currentDialogueIndex++;
            Advance();
            return;
        }

        var eventType = dialogue.Event.GetEventType();

        switch (eventType)
        {
            case VNEventType.ChangeBackground:
                var bgPath = dialogue.Event.Parameters.TryGetValue("BackgroundPath", out var bp) == true ? bp?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(bgPath))
                {
                    BackgroundChanged?.Invoke(bgPath);
                }
                _currentDialogueIndex++;
                Advance();
                break;

            case VNEventType.ChangeBgm:
                var bgmPath = dialogue.Event.Parameters.TryGetValue("BgmPath", out var mp) == true ? mp?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(bgmPath))
                {
                    BgmChanged?.Invoke(bgmPath);
                }
                _currentDialogueIndex++;
                Advance();
                break;

            case VNEventType.BgmStop:
                BgmStopped?.Invoke();
                _currentDialogueIndex++;
                Advance();
                break;

            case VNEventType.ShowCharacter:
                _currentDialogueIndex++;
                Advance();
                break;

            case VNEventType.HideCharacter:
                _currentDialogueIndex++;
                Advance();
                break;

            case VNEventType.WaitSeconds:
                var seconds = 1.0;
                if (dialogue.Event.Parameters.TryGetValue("Seconds", out var secObj))
                {
                    if (secObj is double secD) seconds = secD;
                    else if (secObj is int secI) seconds = secI;
                    else if (secObj is string secS && double.TryParse(secS, out var secParsed)) seconds = secParsed;
                }
                _waitSeconds = (int)(seconds * 10);
                _waitTicks = 0;
                _isWaiting = true;
                _waitTimer.Start();
                break;

            case VNEventType.JumpScene:
                var targetScene = dialogue.Event.Parameters.TryGetValue("TargetScene", out var ts) == true ? ts?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(targetScene))
                {
                    StartScene(targetScene);
                }
                else
                {
                    _currentDialogueIndex++;
                    Advance();
                }
                break;

            case VNEventType.SetVariable:
            case VNEventType.PlaySound:
            case VNEventType.Pause:
            case VNEventType.Custom:
                _currentDialogueIndex++;
                Advance();
                break;

            case VNEventType.InvokePlugin:
                var pluginAssembly = dialogue.Event.Parameters.TryGetValue("PluginAssembly", out var pAsm) == true ? pAsm?.ToString() ?? "" : "";
                var pluginClass = dialogue.Event.Parameters.TryGetValue("PluginClass", out var pCls) == true ? pCls?.ToString() ?? "" : "";
                var pluginMethod = dialogue.Event.Parameters.TryGetValue("PluginMethod", out var pMth) == true ? pMth?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(pluginAssembly) && !string.IsNullOrEmpty(pluginClass) && !string.IsNullOrEmpty(pluginMethod))
                {
                    _pluginManager.InvokeMethod(pluginAssembly, pluginClass, pluginMethod);
                }
                _currentDialogueIndex++;
                Advance();
                break;

            case VNEventType.InvokeCode:
                var code = dialogue.Event.Parameters.TryGetValue("Code", out var codeObj) == true ? codeObj?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(code))
                {
                    ExecuteCode(code);
                }
                _currentDialogueIndex++;
                Advance();
                break;
        }
    }

    private void ProcessBranchNode(VNDialogue dialogue)
    {
        if (dialogue.Branch?.Choices == null || dialogue.Branch.Choices.Count == 0)
        {
            _currentDialogueIndex++;
            Advance();
            return;
        }

        var choices = new List<ChoiceData>();
        foreach (var choice in dialogue.Branch.Choices)
        {
            choices.Add(new ChoiceData
            {
                Text = choice.Text ?? "",
                Target = choice.TargetScene ?? ""
            });
        }

        SpeakerChanged?.Invoke("请选择：");
        DialogueTextChanged?.Invoke("");
        ChoicesChanged?.Invoke(choices);
        _currentDialogueIndex++;
    }

    public void SelectChoice(int index)
    {
        if (_currentScene == null || _currentScene.Dialogues == null)
            return;

        if (_currentDialogueIndex >= _currentScene.Dialogues.Count)
            return;

        var dialogue = _currentScene.Dialogues[_currentDialogueIndex];
        if (dialogue.Branch?.Choices != null && index >= 0 && index < dialogue.Branch.Choices.Count)
        {
            var targetScene = dialogue.Branch.Choices[index].TargetScene;
            if (!string.IsNullOrEmpty(targetScene))
            {
                StartScene(targetScene);
            }
        }
    }

    private ScriptOptions CreateScriptOptions()
    {
        return ScriptOptions.Default
            .WithImports(
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.Threading.Tasks",
                "Visunovia.Player.WPF.Player"
            )
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(WindowHandleProvider).Assembly
            );
    }

    private void ExecuteCode(string code)
    {
        try
        {
            var scriptOptions = CreateScriptOptions();
            var task = CSharpScript.EvaluateAsync<object>(code, scriptOptions);
            task.Wait();
        }
        catch
        {
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        _waitTimer.Stop();
        _isWaiting = false;
    }
}

public class GameProject
{
    public ProjectMetadata? Metadata { get; set; }
}

public class ProjectMetadata
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0";
}

public class ChoiceData
{
    public string Text { get; set; } = "";
    public string Target { get; set; } = "";
}
