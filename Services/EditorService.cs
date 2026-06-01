using System.Text.Json;
using System.Xml.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Visunovia.Models.Engine;

namespace Visunovia.Services;

/// <summary>
/// 编辑器服务，管理视觉小说项目的创建、加载、保存及场景/对话的 CRUD 操作
/// </summary>
public class EditorService
{
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;
    private readonly UndoRedoManager _undoRedoManager;

    public VNProject? CurrentProject { get; private set; }
    public string? CurrentProjectPath { get; set; }
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
            .IgnoreUnmatchedProperties()
            .Build();

        _undoRedoManager = new UndoRedoManager();
        _undoRedoManager.HistoryChanged += () => ProjectChanged?.Invoke();
    }

    /// <summary>
    /// 创建新项目
    /// </summary>
    /// <param name="name">项目名称</param>
    /// <param name="path">项目保存路径</param>
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
                            Uuid = System.Guid.NewGuid().ToString(),
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

    /// <summary>
    /// 保存项目到指定路径
    /// </summary>
    /// <param name="path">项目文件保存路径</param>
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
        // 保存项目时可能因磁盘空间不足或权限不足导致 IOException，此处捕获并通知调用方
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"保存项目失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 创建项目目录结构
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
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

    /// <summary>
    /// 将项目元数据保存为 XML 格式的 .tlor 文件
    /// </summary>
    /// <param name="path">.tlor 文件保存路径</param>
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

    /// <summary>
    /// 将项目变量保存为 JSON 文件
    /// </summary>
    /// <param name="path">变量文件保存路径</param>
    private async Task SaveVariablesAsync(string path)
    {
        if (CurrentProject == null) return;

        var json = JsonSerializer.Serialize(CurrentProject.Variables, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// 将资源索引保存到项目目录（内部使用，基于当前项目路径扫描资源）
    /// </summary>
    /// <param name="path">资源索引文件保存路径</param>
    private async Task SaveResourceIndexInternalAsync(string path)
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

    /// <summary>
    /// 将资源索引保存到指定项目根目录，按类别生成子索引文件和清单文件
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="resources">按类别分类的资源列表</param>
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

    /// <summary>
    /// 从项目目录加载资源索引清单，读取各子索引文件中的资源列表
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
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
        // 清单文件可能格式损坏或被锁定，忽略解析错误并返回已读取的部分
        catch
        {
        }

        return resources;
    }

    /// <summary>
    /// 从指定路径加载项目
    /// </summary>
    /// <param name="path">项目文件路径或项目目录路径</param>
    /// <param name="projectRoot">可选：指定项目根目录路径（用于上传场景）</param>
    public async Task<bool> LoadProjectAsync(string path, string? projectRoot = null)
    {
        try
        {
            var effectiveProjectRoot = projectRoot ?? Path.GetDirectoryName(path) ?? path;
            if (string.IsNullOrEmpty(effectiveProjectRoot))
            {
                effectiveProjectRoot = path;
            }

            if (!File.Exists(path))
            {
                var tlorPath = Path.Combine(path, "Project.tlor");
                if (File.Exists(tlorPath))
                {
                    path = tlorPath;
                    effectiveProjectRoot = path;
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
                    var scriptPath = Path.Combine(effectiveProjectRoot, "Scripts", "Main", $"{sceneId}.lor");

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

            var variablesPath = Path.Combine(effectiveProjectRoot, "Settings", "Variables.json");
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
        // 加载项目时可能因文件不存在、格式错误或权限不足导致异常
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"加载项目失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 将指定场景导出为 YAML 脚本文件
    /// </summary>
    /// <param name="sceneId">场景 ID</param>
    /// <param name="path">脚本文件保存路径</param>
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

    /// <summary>
    /// 从 YAML 脚本文件导入场景
    /// </summary>
    /// <param name="path">脚本文件路径</param>
    public async Task<VNScene?> ImportScriptAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                ErrorOccurred?.Invoke($"脚本文件不存在: {path}");
                return null;
            }

            var content = await File.ReadAllTextAsync(path);
            var scene = _yamlDeserializer.Deserialize<VNScene>(content);

            if (scene == null)
            {
                ErrorOccurred?.Invoke($"导入脚本失败: 反序列化返回 null");
                return null;
            }

            if (scene.Dialogues == null || scene.Dialogues.Count == 0)
            {
                ErrorOccurred?.Invoke($"警告: 导入脚本 {path} 的对话列表为空，请检查 YAML 格式");
            }

            return scene;
        }
        // 脚本文件可能格式错误或编码不匹配，捕获异常并通知调用方
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"导入脚本失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 扫描项目目录中的资源文件，按类别分类返回
    /// </summary>
    /// <param name="projectPath">项目根目录路径</param>
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

    /// <summary>
    /// 递归扫描指定目录中匹配扩展名的文件
    /// </summary>
    /// <param name="path">扫描目录路径</param>
    /// <param name="results">匹配文件的文件名列表</param>
    /// <param name="extensions">允许的文件扩展名</param>
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

    /// <summary>
    /// 标记项目已修改并通知监听者
    /// </summary>
    public void MarkAsModified()
    {
        HasUnsavedChanges = true;
        ProjectChanged?.Invoke();
    }

    /// <summary>
    /// 标记项目已修改但不触发通知（用于撤销/重做内部操作）
    /// </summary>
    public void MarkAsModifiedWithoutNotify()
    {
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// 根据 ID 获取场景
    /// </summary>
    /// <param name="id">场景 ID</param>
    public VNScene? GetScene(string id)
    {
        return CurrentProject?.Scenes.FirstOrDefault(s => s.Id == id);
    }

    /// <summary>
    /// 添加场景到当前项目（支持撤销/重做）
    /// </summary>
    /// <param name="scene">要添加的场景</param>
    public void AddScene(VNScene scene)
    {
        if (CurrentProject == null) return;
        _undoRedoManager.ExecuteCommand(new AddSceneCommand(this, scene));
    }

    /// <summary>
    /// 从当前项目移除指定场景（支持撤销/重做）
    /// </summary>
    /// <param name="id">要移除的场景 ID</param>
    public void RemoveScene(string id)
    {
        if (CurrentProject == null) return;
        _undoRedoManager.ExecuteCommand(new RemoveSceneCommand(this, id));
    }

    /// <summary>
    /// 向指定场景添加对话（支持撤销/重做）
    /// </summary>
    /// <param name="sceneId">目标场景 ID</param>
    /// <param name="dialogue">要添加的对话</param>
    public void AddDialogue(string sceneId, VNDialogue dialogue)
    {
        var scene = GetScene(sceneId);
        if (scene != null)
        {
            _undoRedoManager.ExecuteCommand(new AddDialogueCommand(this, sceneId, dialogue));
        }
    }

    /// <summary>
    /// 从指定场景移除对话（支持撤销/重做）
    /// </summary>
    /// <param name="sceneId">目标场景 ID</param>
    /// <param name="dialogueIndex">要移除的对话索引</param>
    public void RemoveDialogue(string sceneId, int dialogueIndex)
    {
        var scene = GetScene(sceneId);
        if (scene != null && dialogueIndex >= 0 && dialogueIndex < scene.Dialogues.Count)
        {
            _undoRedoManager.ExecuteCommand(new RemoveDialogueCommand(this, sceneId, dialogueIndex));
        }
    }

    /// <summary>
    /// 撤销上一步操作
    /// </summary>
    public void Undo()
    {
        _undoRedoManager.Undo();
    }

    /// <summary>
    /// 重做上一步撤销的操作
    /// </summary>
    public void Redo()
    {
        _undoRedoManager.Redo();
    }

    /// <summary>
    /// 获取当前项目的资源列表
    /// </summary>
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

    /// <summary>
    /// 将 XML 特殊字符转义为实体引用
    /// </summary>
    /// <param name="text">原始文本</param>
    private static string EscapeXml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// 将当前项目导出为 XML 格式字符串
    /// </summary>
    public async Task<string> ExportProjectToXmlAsync()
    {
        if (CurrentProject == null)
            return "";

        return await Task.Run(() =>
        {
            var project = CurrentProject;
            var scenesElement = new XElement("scenes");

            foreach (var scene in project.Scenes)
            {
                var sceneElement = new XElement("scene",
                    new XAttribute("id", scene.Id)
                );

                if (!string.IsNullOrEmpty(scene.Background))
                {
                    sceneElement.Add(new XElement("background", scene.Background));
                }
                else
                {
                    sceneElement.Add(new XElement("background", ""));
                }

                if (scene.Bgm != null)
                {
                    var bgmElement = new XElement("bgm");
                    if (!string.IsNullOrEmpty(scene.Bgm.Path))
                    {
                        bgmElement.Add(new XElement("path", scene.Bgm.Path));
                    }
                    else
                    {
                        bgmElement.Add(new XElement("path", ""));
                    }
                    bgmElement.Add(new XElement("volume", scene.Bgm.Volume));
                    bgmElement.Add(new XElement("loop", scene.Bgm.Loop.ToString().ToLower()));
                    sceneElement.Add(bgmElement);
                }

                var dialoguesElement = new XElement("dialogues");

                foreach (var dialogue in scene.Dialogues)
                {
                    XElement dialogueElement;

                    switch (dialogue.Type)
                    {
                        case VNDialogueType.Branch:
                            dialogueElement = new XElement("dialogue",
                                new XAttribute("type", "Branch")
                            );
                            if (dialogue.Branch != null)
                            {
                                var branchElement = new XElement("branch");
                                var choicesElement = new XElement("choices");
                                foreach (var choice in dialogue.Branch.Choices)
                                {
                                    choicesElement.Add(new XElement("choice",
                                        new XAttribute("text", choice.Text),
                                        new XAttribute("targetScene", choice.TargetScene),
                                        new XAttribute("condition", choice.Condition)
                                    ));
                                }
                                branchElement.Add(choicesElement);
                                dialogueElement.Add(branchElement);
                            }
                            break;

                        case VNDialogueType.Event:
                            dialogueElement = new XElement("dialogue",
                                new XAttribute("type", "Event")
                            );
                            if (dialogue.Event != null)
                            {
                                var eventElement = new XElement("event",
                                    new XAttribute("eventType", dialogue.Event.EventType.ToString())
                                );
                                if (dialogue.Event.Parameters.Count > 0)
                                {
                                    var parametersElement = new XElement("parameters");
                                    foreach (var param in dialogue.Event.Parameters)
                                    {
                                        parametersElement.Add(new XElement(param.Key, param.Value?.ToString() ?? ""));
                                    }
                                    eventElement.Add(parametersElement);
                                }
                                dialogueElement.Add(eventElement);
                            }
                            if (dialogue.Transition != null)
                            {
                                dialogueElement.Add(new XElement("transition",
                                    new XAttribute("type", dialogue.Transition.Effect.ToString()),
                                    new XAttribute("duration", dialogue.Transition.Duration)
                                ));
                            }
                            break;

                        default:
                            dialogueElement = new XElement("dialogue",
                                new XAttribute("type", "Dialogue")
                            );
                            if (!string.IsNullOrEmpty(dialogue.Speaker))
                            {
                                dialogueElement.Add(new XElement("speaker", dialogue.Speaker));
                            }
                            else
                            {
                                dialogueElement.Add(new XElement("speaker", ""));
                            }
                            if (!string.IsNullOrEmpty(dialogue.Text))
                            {
                                dialogueElement.Add(new XElement("text", dialogue.Text));
                            }
                            else
                            {
                                dialogueElement.Add(new XElement("text", ""));
                            }
                            if (!string.IsNullOrEmpty(scene.Background))
                            {
                                dialogueElement.Add(new XElement("background", scene.Background));
                            }
                            else
                            {
                                dialogueElement.Add(new XElement("background", ""));
                            }
                            var spritesElement = new XElement("sprites");
                            foreach (var sprite in dialogue.Sprites)
                            {
                                var spriteElement = new XElement("sprite");
                                if (!string.IsNullOrEmpty(sprite.Path))
                                {
                                    spriteElement.Add(new XElement("path", sprite.Path));
                                }
                                else
                                {
                                    spriteElement.Add(new XElement("path", ""));
                                }
                                spriteElement.Add(new XElement("position", sprite.Position));
                                spriteElement.Add(new XElement("layer", sprite.Layer));
                                if (sprite.Animation != null)
                                {
                                    spriteElement.Add(new XElement("animation",
                                        new XAttribute("type", sprite.Animation.Type),
                                        new XAttribute("duration", sprite.Animation.Duration)
                                    ));
                                }
                                spritesElement.Add(spriteElement);
                            }
                            dialogueElement.Add(spritesElement);
                            if (!string.IsNullOrEmpty(dialogue.Voice))
                            {
                                dialogueElement.Add(new XElement("voice", dialogue.Voice));
                            }
                            else
                            {
                                dialogueElement.Add(new XElement("voice", ""));
                            }
                            if (dialogue.TextEffect != null)
                            {
                                dialogueElement.Add(new XElement("textEffect",
                                    new XAttribute("type", dialogue.TextEffect.Type.ToString()),
                                    new XAttribute("speed", dialogue.TextEffect.Speed),
                                    new XAttribute("shake", dialogue.TextEffect.Shake.ToString().ToLower()),
                                    new XAttribute("fadeDuration", dialogue.TextEffect.FadeDuration),
                                    new XAttribute("delayBeforeStart", dialogue.TextEffect.DelayBeforeStart)
                                ));
                            }
                            if (dialogue.Animation != null)
                            {
                                dialogueElement.Add(new XElement("animation",
                                    new XAttribute("type", dialogue.Animation.Type),
                                    new XAttribute("duration", dialogue.Animation.Duration)
                                ));
                            }
                            if (dialogue.Transition != null)
                            {
                                dialogueElement.Add(new XElement("transition",
                                    new XAttribute("type", dialogue.Transition.Effect.ToString()),
                                    new XAttribute("duration", dialogue.Transition.Duration)
                                ));
                            }
                            break;
                    }

                    dialoguesElement.Add(dialogueElement);
                }

                sceneElement.Add(dialoguesElement);
                scenesElement.Add(sceneElement);
            }

            var variablesElement = new XElement("variables");

            var xml = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("project",
                    new XAttribute("version", "1.0"),
                    new XElement("metadata",
                        new XElement("title", project.Metadata.Title),
                        new XElement("author", project.Metadata.Author),
                        new XElement("version", project.Metadata.Version)
                    ),
                    scenesElement,
                    variablesElement
                )
            );

            return xml.ToString();
        });
    }

    /// <summary>
    /// 从 XML 字符串导入项目
    /// </summary>
    /// <param name="xml">XML 格式的项目字符串</param>
    public async Task<VNProject?> ImportProjectFromXmlAsync(string xml)
    {
        try
        {
            return await Task.Run(() =>
            {
                var doc = XDocument.Parse(xml);
                var root = doc.Root;
                if (root == null) return null;

                var project = new VNProject();
                project.Metadata = new VNMetadata
                {
                    Title = root.Element("metadata")?.Element("title")?.Value ?? "未命名项目",
                    Author = root.Element("metadata")?.Element("author")?.Value ?? "",
                    Version = root.Element("metadata")?.Element("version")?.Value ?? "1.0"
                };

                project.Scenes = new List<VNScene>();
                var scenesElement = root.Element("scenes");
                if (scenesElement != null)
                {
                    foreach (var sceneElem in scenesElement.Elements("scene"))
                    {
                        var scene = new VNScene
                        {
                            Id = sceneElem.Attribute("id")?.Value ?? "unknown",
                            Background = sceneElem.Element("background")?.Value ?? ""
                        };

                        var bgmElem = sceneElem.Element("bgm");
                        if (bgmElem != null)
                        {
                            scene.Bgm = new VNBgm
                            {
                                Path = bgmElem.Element("path")?.Value ?? "",
                                Volume = int.TryParse(bgmElem.Element("volume")?.Value, out var vol) ? vol : 80,
                                Loop = bool.TryParse(bgmElem.Element("loop")?.Value, out var loop) ? loop : true
                            };
                        }
                        else
                        {
                            scene.Bgm = new VNBgm();
                        }

                        scene.Dialogues = new List<VNDialogue>();
                        var dialoguesElement = sceneElem.Element("dialogues");
                        if (dialoguesElement != null)
                        {
                            foreach (var dialogueElem in dialoguesElement.Elements("dialogue"))
                            {
                                var typeAttr = dialogueElem.Attribute("type")?.Value ?? "Dialogue";
                                VNDialogue dialogue;

                                if (typeAttr == "Branch")
                                {
                                    dialogue = new VNDialogue { Uuid = System.Guid.NewGuid().ToString(), Type = VNDialogueType.Branch };
                                    var branchElem = dialogueElem.Element("branch");
                                    if (branchElem != null)
                                    {
                                        dialogue.Branch = new VNBranch();
                                        var choicesElem = branchElem.Element("choices");
                                        if (choicesElem != null)
                                        {
                                            dialogue.Branch.Choices = new List<VNChoiceOption>();
                                            foreach (var choiceElem in choicesElem.Elements("choice"))
                                            {
                                                dialogue.Branch.Choices.Add(new VNChoiceOption
                                                {
                                                    Text = choiceElem.Attribute("text")?.Value ?? "",
                                                    TargetScene = choiceElem.Attribute("targetScene")?.Value ?? "",
                                                    Condition = choiceElem.Attribute("condition")?.Value ?? ""
                                                });
                                            }
                                        }
                                    }
                                }
                                else if (typeAttr == "Event")
                                {
                                    dialogue = new VNDialogue { Uuid = System.Guid.NewGuid().ToString(), Type = VNDialogueType.Event };
                                    var eventElem = dialogueElem.Element("event");
                                    if (eventElem != null)
                                    {
                                        var eventTypeStr = eventElem.Attribute("eventType")?.Value ?? "Custom";
                                        if (Enum.TryParse<VNEventType>(eventTypeStr, out var eventType))
                                        {
                                            dialogue.Event = new VNEvent { EventType = eventType };
                                        }
                                        else
                                        {
                                            dialogue.Event = new VNEvent { EventType = VNEventType.Custom };
                                        }
                                        var parametersElem = eventElem.Element("parameters");
                                        if (parametersElem != null)
                                        {
                                            dialogue.Event.Parameters = new Dictionary<string, object>();
                                            foreach (var paramElem in parametersElem.Elements())
                                            {
                                                dialogue.Event.Parameters[paramElem.Name.LocalName] = paramElem.Value;
                                            }
                                        }
                                    }
                                    var transitionElem = dialogueElem.Element("transition");
                                    if (transitionElem != null)
                                    {
                                        var effectStr = transitionElem.Attribute("type")?.Value ?? "None";
                                        if (Enum.TryParse<VNTransitionEffect>(effectStr, out var effect))
                                        {
                                            dialogue.Transition = new VNTransition
                                            {
                                                Effect = effect,
                                                Duration = int.TryParse(transitionElem.Attribute("duration")?.Value, out var dur) ? dur : 300
                                            };
                                        }
                                    }
                                }
                                else
                                {
                                    dialogue = new VNDialogue { Uuid = System.Guid.NewGuid().ToString(), Type = VNDialogueType.Dialogue };
                                    dialogue.Speaker = dialogueElem.Element("speaker")?.Value ?? "";
                                    dialogue.Text = dialogueElem.Element("text")?.Value ?? "";
                                    dialogue.Sprites = new List<VNSprite>();
                                    var spritesElem = dialogueElem.Element("sprites");
                                    if (spritesElem != null)
                                    {
                                        foreach (var spriteElem in spritesElem.Elements("sprite"))
                                        {
                                            var sprite = new VNSprite
                                            {
                                                Path = spriteElem.Element("path")?.Value ?? "",
                                                Position = spriteElem.Element("position")?.Value ?? "center",
                                                Layer = int.TryParse(spriteElem.Element("layer")?.Value, out var layer) ? layer : 0
                                            };
                                            var animElem = spriteElem.Element("animation");
                                            if (animElem != null)
                                            {
                                                sprite.Animation = new VNAnimation
                                                {
                                                    Type = animElem.Attribute("type")?.Value ?? "none",
                                                    Duration = int.TryParse(animElem.Attribute("duration")?.Value, out var dur) ? dur : 300
                                                };
                                            }
                                            dialogue.Sprites.Add(sprite);
                                        }
                                    }
                                    dialogue.Voice = dialogueElem.Element("voice")?.Value ?? "";
                                    var textEffectElem = dialogueElem.Element("textEffect");
                                    if (textEffectElem != null)
                                    {
                                        var teTypeStr = textEffectElem.Attribute("type")?.Value ?? "None";
                                        if (Enum.TryParse<VNTextEffectType>(teTypeStr, out var teType))
                                        {
                                            dialogue.TextEffect = new VNTextEffect
                                            {
                                                Type = teType,
                                                Speed = int.TryParse(textEffectElem.Attribute("speed")?.Value, out var speed) ? speed : 50,
                                                Shake = bool.TryParse(textEffectElem.Attribute("shake")?.Value, out var shake) ? shake : false,
                                                FadeDuration = int.TryParse(textEffectElem.Attribute("fadeDuration")?.Value, out var fadeDur) ? fadeDur : 500,
                                                DelayBeforeStart = int.TryParse(textEffectElem.Attribute("delayBeforeStart")?.Value, out var delay) ? delay : 0
                                            };
                                        }
                                    }
                                    var animElem2 = dialogueElem.Element("animation");
                                    if (animElem2 != null)
                                    {
                                        dialogue.Animation = new VNAnimation
                                        {
                                            Type = animElem2.Attribute("type")?.Value ?? "none",
                                            Duration = int.TryParse(animElem2.Attribute("duration")?.Value, out var dur) ? dur : 300
                                        };
                                    }
                                    var transitionElem2 = dialogueElem.Element("transition");
                                    if (transitionElem2 != null)
                                    {
                                        var effectStr = transitionElem2.Attribute("type")?.Value ?? "None";
                                        if (Enum.TryParse<VNTransitionEffect>(effectStr, out var effect))
                                        {
                                            dialogue.Transition = new VNTransition
                                            {
                                                Effect = effect,
                                                Duration = int.TryParse(transitionElem2.Attribute("duration")?.Value, out var dur) ? dur : 300
                                            };
                                        }
                                    }
                                }

                                scene.Dialogues.Add(dialogue);
                            }
                        }

                        project.Scenes.Add(scene);
                    }
                }

                return project;
            });
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"导入 XML 项目失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从 XML 字符串保存项目（保存到当前项目路径）
    /// </summary>
    /// <param name="xml">XML 格式的项目字符串</param>
    public async Task<bool> SaveProjectFromXmlAsync(string xml)
    {
        try
        {
            var project = await ImportProjectFromXmlAsync(xml);
            if (project == null) return false;

            if (string.IsNullOrEmpty(CurrentProjectPath))
            {
                ErrorOccurred?.Invoke("项目路径未设置");
                return false;
            }

            var projectRoot = Path.GetDirectoryName(CurrentProjectPath) ?? "";
            if (!Directory.Exists(projectRoot))
            {
                Directory.CreateDirectory(projectRoot);
            }

            CreateDirectoryStructure(projectRoot);

            var tlorPath = Path.Combine(projectRoot, "Project.tlor");
            var tlorXml = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("project",
                    new XAttribute("version", "1.0"),
                    new XElement("metadata",
                        new XElement("title", project.Metadata.Title),
                        new XElement("author", project.Metadata.Author),
                        new XElement("version", project.Metadata.Version)
                    ),
                    new XElement("scenes",
                        project.Scenes.Select(s => new XElement("scene",
                            new XAttribute("id", s.Id)
                        ))
                    )
                )
            );
            await File.WriteAllTextAsync(tlorPath, tlorXml.ToString());

            foreach (var scene in project.Scenes)
            {
                var scriptPath = Path.Combine(projectRoot, "Scripts", "Main", $"{scene.Id}.lor");
                await ExportScriptAsync(scene.Id, scriptPath);
            }

            if (project.Variables.Count > 0)
            {
                var variablesPath = Path.Combine(projectRoot, "Settings", "Variables.json");
                var json = JsonSerializer.Serialize(project.Variables, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(variablesPath, json);
            }

            CurrentProject = project;
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

    private Dictionary<string, SceneGraphData> _sceneGraphs = new();

    public SceneGraphData? GetSceneGraph(string sceneId)
    {
        if (_sceneGraphs.TryGetValue(sceneId, out var graph))
        {
            return graph;
        }
        return null;
    }

    public void SaveSceneGraph(string sceneId, string jsonData)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SceneGraphData>(jsonData);
            if (data != null)
            {
                _sceneGraphs[sceneId] = data;
                HasUnsavedChanges = true;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"保存场景图失败: {ex.Message}");
        }
    }

    public object CreateNode(string sceneId, NodeCreateRequest request)
    {
        var graph = GetSceneGraph(sceneId);
        if (graph == null)
        {
            graph = new SceneGraphData { Id = sceneId, Nodes = new List<NodeData>() };
            _sceneGraphs[sceneId] = graph;
        }

        var node = new NodeData
        {
            Id = request.Id,
            Type = request.Type,
            Position = new PositionData { X = request.Position.X, Y = request.Position.Y },
            Properties = request.Properties,
            Inputs = request.Inputs.Select(p => new PortData
            {
                Id = p.Id,
                Label = p.Label,
                Type = p.Type,
                DataType = p.DataType
            }).ToList(),
            Outputs = request.Outputs.Select(p => new PortData
            {
                Id = p.Id,
                Label = p.Label,
                Type = p.Type,
                DataType = p.DataType
            }).ToList()
        };

        graph.Nodes.Add(node);
        HasUnsavedChanges = true;
        return node;
    }

    public object UpdateNode(string sceneId, string nodeId, NodeUpdateRequest request)
    {
        var graph = GetSceneGraph(sceneId);
        if (graph == null)
        {
            throw new Exception($"场景图 {sceneId} 不存在");
        }

        var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null)
        {
            throw new Exception($"节点 {nodeId} 不存在");
        }

        if (request.Position != null)
        {
            node.Position = new PositionData { X = request.Position.X, Y = request.Position.Y };
        }
        if (request.Properties != null)
        {
            node.Properties = request.Properties;
        }
        if (request.Inputs != null)
        {
            node.Inputs = request.Inputs.Select(p => new PortData
            {
                Id = p.Id,
                Label = p.Label,
                Type = p.Type,
                DataType = p.DataType
            }).ToList();
        }
        if (request.Outputs != null)
        {
            node.Outputs = request.Outputs.Select(p => new PortData
            {
                Id = p.Id,
                Label = p.Label,
                Type = p.Type,
                DataType = p.DataType
            }).ToList();
        }

        HasUnsavedChanges = true;
        return node;
    }

    public void DeleteNode(string sceneId, string nodeId)
    {
        var graph = GetSceneGraph(sceneId);
        if (graph == null) return;

        graph.Nodes.RemoveAll(n => n.Id == nodeId);
        graph.Edges.RemoveAll(e => e.Source == nodeId || e.Target == nodeId);
        HasUnsavedChanges = true;
    }

    public object CreateEdge(string sceneId, EdgeCreateRequest request)
    {
        var graph = GetSceneGraph(sceneId);
        if (graph == null)
        {
            graph = new SceneGraphData { Id = sceneId, Nodes = new List<NodeData>(), Edges = new List<EdgeData>() };
            _sceneGraphs[sceneId] = graph;
        }

        var edge = new EdgeData
        {
            Id = request.Id,
            Source = request.Source,
            SourcePort = request.SourcePort,
            Target = request.Target,
            TargetPort = request.TargetPort,
            Type = request.Type
        };

        graph.Edges.Add(edge);
        HasUnsavedChanges = true;
        return edge;
    }

    public void DeleteEdge(string sceneId, string edgeId)
    {
        var graph = GetSceneGraph(sceneId);
        if (graph == null) return;

        graph.Edges.RemoveAll(e => e.Id == edgeId);
        HasUnsavedChanges = true;
    }

    public List<string> GetSceneGraphList()
    {
        return _sceneGraphs.Keys.ToList();
    }
}

public class SceneGraphData
{
    public string Id { get; set; } = "";
    public ViewportData Viewport { get; set; } = new();
    public List<NodeData> Nodes { get; set; } = new();
    public List<EdgeData> Edges { get; set; } = new();
    public SceneConfigData? SceneConfig { get; set; }
}

public class ViewportData
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Zoom { get; set; } = 1.0;
}

public class NodeData
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public PositionData Position { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<PortData> Inputs { get; set; } = new();
    public List<PortData> Outputs { get; set; } = new();
}

public class PositionData
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class PortData
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "";
    public string? DataType { get; set; }
    public string? Target { get; set; }
    public string? TargetPort { get; set; }
}

public class EdgeData
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string SourcePort { get; set; } = "";
    public string Target { get; set; } = "";
    public string TargetPort { get; set; } = "";
    public string Type { get; set; } = "exec";
}

public class SceneConfigData
{
    public string? Background { get; set; }
    public BgmData? Bgm { get; set; }
}

public class BgmData
{
    public string? Path { get; set; }
    public int Volume { get; set; } = 80;
    public bool Loop { get; set; } = true;
}

public class NodeCreateRequest
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public PositionData Position { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<PortData> Inputs { get; set; } = new();
    public List<PortData> Outputs { get; set; } = new();
}

public class PositionRequest
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class NodeUpdateRequest
{
    public PositionRequest? Position { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public List<PortData>? Inputs { get; set; }
    public List<PortData>? Outputs { get; set; }
}

public class EdgeCreateRequest
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string SourcePort { get; set; } = "";
    public string Target { get; set; } = "";
    public string TargetPort { get; set; } = "";
    public string Type { get; set; } = "exec";
}
