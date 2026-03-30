using System.Text.Json;
using System.Xml.Linq;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Visunovia.Engine.Core;

namespace Visunovia.Engine.Editor;

public class EditorService
{
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;
    private readonly UndoRedoManager _undoRedoManager;

    public VNProject? CurrentProject { get; private set; }
    public string? CurrentProjectPath { get; private set; }
    public bool HasUnsavedChanges { get; private set; }

    public UndoRedoManager UndoRedo => _undoRedoManager;
    public bool CanUndo => _undoRedoManager.CanUndo;
    public bool CanRedo => _undoRedoManager.CanRedo;

    public event Action? ProjectChanged;
    public event Action<string>? ErrorOccurred;
    public event Action<string>? StatusChanged;

    public EditorService()
    {
        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _undoRedoManager = new UndoRedoManager();
        _undoRedoManager.HistoryChanged += () => ProjectChanged?.Invoke();
    }

    public void NewProject(string name, string path)
    {
        CurrentProject = new VNProject
        {
            Metadata = new VNMetadata { Title = name },
            Variables = new Dictionary<string, object>(),
            Scenes = new List<VNScene>
            {
                new VNScene
                {
                    Id = "start",
                    Background = "",
                    Bgm = new VNBgm(),
                    Dialogues = new List<VNDialogue>
                    {
                        new VNDialogue
                        {
                            Speaker = "Visunovia",
                            Text = "欢迎使用 Visunovia 视觉小说引擎！"
                        }
                    }
                }
            }
        };
        CurrentProjectPath = path;
        HasUnsavedChanges = true;
        _undoRedoManager.ClearHistory();
        StatusChanged?.Invoke("新项目已创建");
        ProjectChanged?.Invoke();
    }

    public async Task<bool> SaveProjectAsync(string path)
    {
        if (CurrentProject == null) return false;

        try
        {
            var projectDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(projectDir) && !Directory.Exists(projectDir))
            {
                Directory.CreateDirectory(projectDir);
            }

            var projectRoot = Path.GetDirectoryName(path) ?? "";

            CreateDirectoryStructure(projectRoot);

            var tlorPath = Path.Combine(projectRoot, "Project.tlor");
            await SaveProjectFileAsync(tlorPath);

            foreach (var scene in CurrentProject.Scenes)
            {
                var scriptPath = Path.Combine(projectRoot, "Scripts", "Main", $"{scene.Id}.lor");
                await ExportScriptAsync(scene.Id, scriptPath);
            }

            if (CurrentProject.Variables.Count > 0)
            {
                var variablesPath = Path.Combine(projectRoot, "Settings", "Variables.json");
                await SaveVariablesAsync(variablesPath);
            }

            CurrentProjectPath = path;
            HasUnsavedChanges = false;
            _undoRedoManager.ClearHistory();
            StatusChanged?.Invoke($"项目已保存: {projectRoot}");
            ProjectChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"保存项目失败: {ex.Message}");
            return false;
        }
    }

    private void CreateDirectoryStructure(string projectRoot)
    {
        var directories = new[]
        {
            Path.Combine(projectRoot, "UI"),
            Path.Combine(projectRoot, "Scripts", "Main"),
            Path.Combine(projectRoot, "Locales", "Engine"),
            Path.Combine(projectRoot, "Locales"),
            Path.Combine(projectRoot, "Assets", "Characters"),
            Path.Combine(projectRoot, "Assets", "Backgrounds"),
            Path.Combine(projectRoot, "Assets", "Musics"),
            Path.Combine(projectRoot, "Assets", "Voices"),
            Path.Combine(projectRoot, "Assets", "Sfx"),
            Path.Combine(projectRoot, "Saves"),
            Path.Combine(projectRoot, "Settings", "Editor")
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }

    private async Task SaveProjectFileAsync(string path)
    {
        if (CurrentProject == null) return;

        var xml = new XDocument(
            new XElement("project",
                new XAttribute("version", "1.0"),
                new XElement("metadata",
                    new XElement("title", CurrentProject.Metadata.Title),
                    new XElement("author", CurrentProject.Metadata.Author),
                    new XElement("version", CurrentProject.Metadata.Version)
                ),
                new XElement("scenes",
                    CurrentProject.Scenes.Select(s => new XElement("scene",
                        new XAttribute("id", s.Id)
                    ))
                )
            )
        );

        await File.WriteAllTextAsync(path, xml.ToString());
    }

    private async Task SaveVariablesAsync(string path)
    {
        if (CurrentProject == null) return;

        var json = JsonSerializer.Serialize(CurrentProject.Variables, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private async Task SaveResourceIndexAsync(string path)
    {
        if (CurrentProject == null) return;

        var projectRoot = Path.GetDirectoryName(CurrentProjectPath) ?? "";
        var resources = ScanResources(projectRoot);

        var totalItems = resources["sprites"].Count +
                         resources["backgrounds"].Count +
                         resources["bgm"].Count +
                         resources["voice"].Count +
                         resources["sfx"].Count;

        if (totalItems == 0 && File.Exists(path))
        {
            return;
        }

        var xml = new XDocument(
            new XElement("resources",
                new XElement("sprites",
                    resources["sprites"].Select(r => new XElement("item", r))
                ),
                new XElement("backgrounds",
                    resources["backgrounds"].Select(r => new XElement("item", r))
                ),
                new XElement("bgm",
                    resources["bgm"].Select(r => new XElement("item", r))
                ),
                new XElement("voice",
                    resources["voice"].Select(r => new XElement("item", r))
                ),
                new XElement("sfx",
                    resources["sfx"].Select(r => new XElement("item", r))
                )
            )
        );

        await File.WriteAllTextAsync(path, xml.ToString());
    }

    public async Task SaveResourceIndexAsync(string projectRoot, Dictionary<string, List<string>> resources)
    {
        var assetsPath = Path.Combine(projectRoot, "Assets");
        Directory.CreateDirectory(assetsPath);

        var subIndexEntries = new List<XElement>();

        if (resources.TryGetValue("sprites", out var sprites) && sprites.Count > 0)
        {
            var subPath = Path.Combine(assetsPath, "Characters");
            Directory.CreateDirectory(subPath);
            var subIndexPath = Path.Combine(subPath, "Index.resona");

            var subXml = new XDocument(
                new XElement("resources",
                    sprites.Select(r => new XElement("item", Path.GetFileName(r)))
                )
            );
            await File.WriteAllTextAsync(subIndexPath, subXml.ToString());
            subIndexEntries.Add(new XElement("IndexResona", new XAttribute("Type", "Characters"), new XAttribute("Path", "./Characters")));
        }

        if (resources.TryGetValue("backgrounds", out var backgrounds) && backgrounds.Count > 0)
        {
            var subPath = Path.Combine(assetsPath, "Backgrounds");
            Directory.CreateDirectory(subPath);
            var subIndexPath = Path.Combine(subPath, "Index.resona");

            var subXml = new XDocument(
                new XElement("resources",
                    backgrounds.Select(r => new XElement("item", Path.GetFileName(r)))
                )
            );
            await File.WriteAllTextAsync(subIndexPath, subXml.ToString());
            subIndexEntries.Add(new XElement("IndexResona", new XAttribute("Type", "Backgrounds"), new XAttribute("Path", "./Backgrounds")));
        }

        if (resources.TryGetValue("bgm", out var bgm) && bgm.Count > 0)
        {
            var subPath = Path.Combine(assetsPath, "Musics");
            Directory.CreateDirectory(subPath);
            var subIndexPath = Path.Combine(subPath, "Index.resona");

            var subXml = new XDocument(
                new XElement("resources",
                    bgm.Select(r => new XElement("item", Path.GetFileName(r)))
                )
            );
            await File.WriteAllTextAsync(subIndexPath, subXml.ToString());
            subIndexEntries.Add(new XElement("IndexResona", new XAttribute("Type", "Musics"), new XAttribute("Path", "./Musics")));
        }

        if (resources.TryGetValue("voice", out var voice) && voice.Count > 0)
        {
            var subPath = Path.Combine(assetsPath, "Voices");
            Directory.CreateDirectory(subPath);
            var subIndexPath = Path.Combine(subPath, "Index.resona");

            var subXml = new XDocument(
                new XElement("resources",
                    voice.Select(r => new XElement("item", Path.GetFileName(r)))
                )
            );
            await File.WriteAllTextAsync(subIndexPath, subXml.ToString());
            subIndexEntries.Add(new XElement("IndexResona", new XAttribute("Type", "Voices"), new XAttribute("Path", "./Voices")));
        }

        if (resources.TryGetValue("sfx", out var sfx) && sfx.Count > 0)
        {
            var subPath = Path.Combine(assetsPath, "Sfx");
            Directory.CreateDirectory(subPath);
            var subIndexPath = Path.Combine(subPath, "Index.resona");

            var subXml = new XDocument(
                new XElement("resources",
                    sfx.Select(r => new XElement("item", Path.GetFileName(r)))
                )
            );
            await File.WriteAllTextAsync(subIndexPath, subXml.ToString());
            subIndexEntries.Add(new XElement("IndexResona", new XAttribute("Type", "Sfx"), new XAttribute("Path", "./Sfx")));
        }

        var manifestXml = new XDocument(
            new XElement("Manifest",
                subIndexEntries.ToArray()
            )
        );
        var manifestPath = Path.Combine(assetsPath, "Index.resona");
        await File.WriteAllTextAsync(manifestPath, manifestXml.ToString());
    }

    public Dictionary<string, List<string>> LoadResourceIndex(string projectRoot)
    {
        var resources = new Dictionary<string, List<string>>
        {
            ["sprites"] = new List<string>(),
            ["backgrounds"] = new List<string>(),
            ["bgm"] = new List<string>(),
            ["voice"] = new List<string>(),
            ["sfx"] = new List<string>()
        };

        var assetsPath = Path.Combine(projectRoot, "Assets");
        var manifestPath = Path.Combine(assetsPath, "Index.resona");

        if (!File.Exists(manifestPath))
        {
            return resources;
        }

        try
        {
            var manifestContent = File.ReadAllText(manifestPath);
            var manifestDoc = XDocument.Parse(manifestContent);
            var manifestRoot = manifestDoc.Root;

            if (manifestRoot == null)
            {
                return resources;
            }

            foreach (var indexResona in manifestRoot.Elements("IndexResona"))
            {
                var type = indexResona.Attribute("Type")?.Value;
                var pathAttr = indexResona.Attribute("Path")?.Value;

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(pathAttr))
                {
                    continue;
                }

                var baseDir = assetsPath;
                var subIndexPath = Path.GetFullPath(Path.Combine(baseDir, pathAttr, "Index.resona"));

                if (!File.Exists(subIndexPath))
                {
                    continue;
                }

                var subContent = File.ReadAllText(subIndexPath);
                var subDoc = XDocument.Parse(subContent);
                var subRoot = subDoc.Root;

                if (subRoot == null)
                {
                    continue;
                }

                var items = subRoot.Elements("item").Select(e => e.Value).ToList();

                switch (type)
                {
                    case "Characters":
                        resources["sprites"] = items;
                        break;
                    case "Backgrounds":
                        resources["backgrounds"] = items;
                        break;
                    case "Musics":
                        resources["bgm"] = items;
                        break;
                    case "Voices":
                        resources["voice"] = items;
                        break;
                    case "Sfx":
                        resources["sfx"] = items;
                        break;
                }
            }
        }
        catch
        {
        }

        return resources;
    }

    public async Task<bool> LoadProjectAsync(string path)
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
                    ErrorOccurred?.Invoke("未找到 Project.tlor 文件");
                    return false;
                }
            }

            var content = await File.ReadAllTextAsync(path);
            var doc = XDocument.Parse(content);
            var root = doc.Root;

            if (root == null)
            {
                ErrorOccurred?.Invoke("项目文件格式错误");
                return false;
            }

            CurrentProject = new VNProject();
            CurrentProject.Metadata = new VNMetadata
            {
                Title = root.Element("metadata")?.Element("title")?.Value ?? "未命名项目",
                Author = root.Element("metadata")?.Element("author")?.Value ?? "",
                Version = root.Element("metadata")?.Element("version")?.Value ?? "1.0"
            };

            CurrentProject.Scenes = new List<VNScene>();
            var sceneElements = root.Element("scenes")?.Elements("scene");
            if (sceneElements != null)
            {
                foreach (var sceneElem in sceneElements)
                {
                    var sceneId = sceneElem.Attribute("id")?.Value ?? "unknown";
                    var scriptPath = Path.Combine(projectRoot, "Scripts", "Main", $"{sceneId}.lor");

                    if (File.Exists(scriptPath))
                    {
                        var scene = await ImportScriptAsync(scriptPath);
                        if (scene != null)
                        {
                            scene.Id = sceneId;
                            CurrentProject.Scenes.Add(scene);
                        }
                    }
                    else
                    {
                        CurrentProject.Scenes.Add(new VNScene
                        {
                            Id = sceneId,
                            Bgm = new VNBgm(),
                            Dialogues = new List<VNDialogue>()
                        });
                    }
                }
            }

            var variablesPath = Path.Combine(projectRoot, "Settings", "Variables.json");
            if (File.Exists(variablesPath))
            {
                var varsContent = await File.ReadAllTextAsync(variablesPath);
                CurrentProject.Variables = JsonSerializer.Deserialize<Dictionary<string, object>>(varsContent) ?? new();
            }

            CurrentProjectPath = path;
            HasUnsavedChanges = false;
            _undoRedoManager.ClearHistory();
            StatusChanged?.Invoke($"已加载项目: {CurrentProject.Metadata.Title}");
            ProjectChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"加载项目失败: {ex.Message}");
            return false;
        }
    }

    public async Task ExportScriptAsync(string sceneId, string path)
    {
        if (CurrentProject == null) return;

        var scene = CurrentProject.Scenes.FirstOrDefault(s => s.Id == sceneId);
        if (scene == null) return;

        var scriptDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(scriptDir) && !Directory.Exists(scriptDir))
        {
            Directory.CreateDirectory(scriptDir);
        }

        var yaml = _yamlSerializer.Serialize(scene);
        await File.WriteAllTextAsync(path, yaml);
    }

    public async Task<VNScene?> ImportScriptAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var content = await File.ReadAllTextAsync(path);
            var scene = _yamlDeserializer.Deserialize<VNScene>(content);
            return scene;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"导入脚本失败: {ex.Message}");
            return null;
        }
    }

    public Dictionary<string, List<string>> ScanResources(string projectPath)
    {
        var resources = new Dictionary<string, List<string>>
        {
            { "sprites", new List<string>() },
            { "backgrounds", new List<string>() },
            { "bgm", new List<string>() },
            { "voice", new List<string>() },
            { "sfx", new List<string>() }
        };

        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
        {
            return resources;
        }

        var assetsPath = Path.Combine(projectPath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            return resources;
        }

        var categoryExtensions = new Dictionary<string, string[]>
        {
            { "sprites", new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" } },
            { "backgrounds", new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" } },
            { "bgm", new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a" } },
            { "voice", new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a" } },
            { "sfx", new[] { ".mp3", ".wav", ".ogg", ".flac" } }
        };

        var directoryMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Characters", "sprites" },
            { "chars", "sprites" },
            { "sprites", "sprites" },
            { "sprite", "sprites" },
            { "Backgrounds", "backgrounds" },
            { "bg", "backgrounds" },
            { "background", "backgrounds" },
            { "Musics", "bgm" },
            { "music", "bgm" },
            { "bgm", "bgm" },
            { "Voices", "voice" },
            { "voice", "voice" },
            { "Sfx", "sfx" },
            { "sounds", "sfx" },
            { "sound", "sfx" }
        };

        foreach (var dir in Directory.GetDirectories(assetsPath))
        {
            var dirName = Path.GetFileName(dir);

            if (!directoryMappings.TryGetValue(dirName, out var category))
                continue;

            if (!categoryExtensions.TryGetValue(category, out var extensions))
                continue;

            ScanDirectory(dir, resources[category], extensions);
        }

        return resources;
    }

    private void ScanDirectory(string path, List<string> results, string[] extensions)
    {
        if (!Directory.Exists(path)) return;

        foreach (var file in Directory.GetFiles(path))
        {
            var ext = Path.GetExtension(file).ToLower();
            if (extensions.Contains(ext))
            {
                results.Add(Path.GetFileName(file));
            }
        }
    }

    public void MarkAsModified()
    {
        HasUnsavedChanges = true;
        ProjectChanged?.Invoke();
    }

    public void MarkAsModifiedWithoutNotify()
    {
        HasUnsavedChanges = true;
    }

    public VNScene? GetScene(string id)
    {
        return CurrentProject?.Scenes.FirstOrDefault(s => s.Id == id);
    }

    public void AddScene(VNScene scene)
    {
        if (CurrentProject == null) return;
        _undoRedoManager.ExecuteCommand(new AddSceneCommand(this, scene));
    }

    public void RemoveScene(string id)
    {
        if (CurrentProject == null) return;
        _undoRedoManager.ExecuteCommand(new RemoveSceneCommand(this, id));
    }

    public void AddDialogue(string sceneId, VNDialogue dialogue)
    {
        var scene = GetScene(sceneId);
        if (scene != null)
        {
            _undoRedoManager.ExecuteCommand(new AddDialogueCommand(this, sceneId, dialogue));
        }
    }

    public void RemoveDialogue(string sceneId, int dialogueIndex)
    {
        var scene = GetScene(sceneId);
        if (scene != null && dialogueIndex >= 0 && dialogueIndex < scene.Dialogues.Count)
        {
            _undoRedoManager.ExecuteCommand(new RemoveDialogueCommand(this, sceneId, dialogueIndex));
        }
    }

    public void Undo()
    {
        _undoRedoManager.Undo();
    }

    public void Redo()
    {
        _undoRedoManager.Redo();
    }

    public Dictionary<string, List<string>> GetResources()
    {
        if (string.IsNullOrEmpty(CurrentProjectPath))
        {
            return new Dictionary<string, List<string>>
            {
                { "sprites", new List<string>() },
                { "backgrounds", new List<string>() },
                { "bgm", new List<string>() },
                { "voice", new List<string>() },
                { "sfx", new List<string>() }
            };
        }

        var projectRoot = Path.GetDirectoryName(CurrentProjectPath) ?? "";
        return ScanResources(projectRoot);
    }
}

public class VNProject
{
    public VNMetadata Metadata { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public List<VNScene> Scenes { get; set; } = new();
}

public class VNMetadata
{
    public string Title { get; set; } = "未命名项目";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0";
}

public class VNCustomMethod
{
    public string Name { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? Script { get; set; }
    public string Language { get; set; } = "csharp";
}

public class PackagingService
{
    private readonly EditorService _editor;
    private readonly string _playerTemplatePath;
    private readonly string _outputPath;

    public event Action<string>? StatusChanged;
    public event Action<int>? ProgressChanged;
    public event Action<string>? ErrorOccurred;

    public PackagingService(EditorService editor, string playerTemplatePath, string outputPath)
    {
        _editor = editor;
        _playerTemplatePath = playerTemplatePath;
        _outputPath = outputPath;
    }

    public async Task<bool> PackageProjectAsync(string rawKeyString, IProgress<int>? progress = null)
    {
        if (_editor.CurrentProject == null || string.IsNullOrEmpty(_editor.CurrentProjectPath))
        {
            ErrorOccurred?.Invoke("没有打开的项目");
            return false;
        }

        try
        {
            StatusChanged?.Invoke("正在准备打包...");

            var projectRoot = Path.GetDirectoryName(_editor.CurrentProjectPath) ?? "";
            var tempDir = Path.Combine(Path.GetTempPath(), $"Visunovia_Package_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            StatusChanged?.Invoke("正在复制项目文件...");
            progress?.Report(10);
            await CopyProjectFilesAsync(projectRoot, tempDir);

            StatusChanged?.Invoke("正在创建 ZIP 包...");
            progress?.Report(50);
            var tempZipPath = Path.Combine(tempDir, "Game.zip");
            await CreateZipArchiveAsync(tempDir, tempZipPath);

            StatusChanged?.Invoke("正在加密资源包...");
            progress?.Report(70);
            var outputDir = Path.Combine(_outputPath, _editor.CurrentProject.Metadata.Title);
            Directory.CreateDirectory(outputDir);
            var lorePath = Path.Combine(outputDir, "Game.lore");

            Visunovia.Editor.Security.SimpleCryptoHelper.EncryptPackage(tempZipPath, lorePath, rawKeyString);

            StatusChanged?.Invoke("正在准备播放器...");
            progress?.Report(85);
            await PreparePlayerAsync(outputDir, rawKeyString);

            Directory.Delete(tempDir, true);

            progress?.Report(100);
            StatusChanged?.Invoke($"打包完成: {lorePath}");
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"打包失败: {ex.Message}");
            return false;
        }
    }

    private async Task CopyProjectFilesAsync(string sourceDir, string destDir)
    {
        await Task.Run(() =>
        {
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var destSubDir = dir.Replace(sourceDir, destDir);
                Directory.CreateDirectory(destSubDir);
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                var destFile = file.Replace(sourceDir, destDir);
                var destFileDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destFileDir))
                {
                    Directory.CreateDirectory(destFileDir);
                }
                File.Copy(file, destFile, true);
            }
        });
    }

    private async Task CreateZipArchiveAsync(string sourceDir, string destZipPath)
    {
        await Task.Run(() =>
        {
            if (File.Exists(destZipPath))
            {
                File.Delete(destZipPath);
            }

            System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, destZipPath, System.IO.Compression.CompressionLevel.Optimal, false);
        });
    }

    private async Task PreparePlayerAsync(string outputDir, string rawKeyString)
    {
        await Task.Run(() =>
        {
            var obfuscatedKey = Visunovia.Editor.Security.SimpleCryptoHelper.ObfuscateKeyString(rawKeyString);

            if (Directory.Exists(_playerTemplatePath))
            {
                var playerDir = Path.Combine(outputDir, "Player");
                CopyDirectoryRecursive(_playerTemplatePath, playerDir);

                var cryptoFile = Path.Combine(playerDir, "Security", "simple-crypto.js");
                if (File.Exists(cryptoFile))
                {
                    var content = File.ReadAllText(cryptoFile);
                    content = content.Replace("PLACEHOLDER", obfuscatedKey);
                    File.WriteAllText(cryptoFile, content);
                }
            }
        });
    }

    private void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }
}