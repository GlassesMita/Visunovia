using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using Visunovia.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Visunovia.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly ILogger<ProjectController> _logger;
    private readonly EditorSessionService _sessionService;

    public ProjectController(ILogger<ProjectController> logger, EditorSessionService sessionService)
    {
        _logger = logger;
        _sessionService = sessionService;
    }

    /// <summary>
    /// 获取当前已打开项目的信息
    /// </summary>
    [HttpGet("currentProject")]
    public IActionResult GetCurrentProject()
    {
        var editor = _sessionService.GetEditor();

        if (editor.CurrentProject == null || string.IsNullOrEmpty(editor.CurrentProjectPath))
        {
            return Ok(new
            {
                success = true,
                data = (object?)null,
                message = "当前没有打开的项目"
            });
        }

        var project = editor.CurrentProject;
        var projectPath = editor.CurrentProjectPath!;

        // 获取项目根目录（.tlor 文件所在目录）
        var projectRoot = Path.GetDirectoryName(projectPath) ?? projectPath;

        // 获取一级子目录（相对路径）
        var subDirectories = new List<string>();
        try
        {
            if (Directory.Exists(projectRoot))
            {
                foreach (var dir in Directory.GetDirectories(projectRoot).OrderBy(d => d))
                {
                    subDirectories.Add(Path.GetFileName(dir));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取项目子目录失败: {Path}", projectRoot);
        }

        return Ok(new
        {
            success = true,
            data = new CurrentProjectResponse
            {
                ProjectName = project.Metadata.Title,
                Version = project.Metadata.Version,
                VersionCode = project.Metadata.VersionCode,
                CompanyName = project.Metadata.CompanyName,
                ProjectPath = projectRoot,
                SubDirectories = subDirectories
            }
        });
    }

    /// <summary>
    /// 新建项目：参照模板目录结构创建标准项目
    /// </summary>
    /// <param name="request">包含项目名称和目录路径的请求</param>
    /// <returns>创建结果，包含项目文件路径和文件夹结构</returns>
    [HttpPost("new")]
    public async Task<IActionResult> NewProject([FromBody] NewProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, error = "项目名称不能为空" });
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { success = false, error = "项目目录不能为空" });
        }

        try
        {
            var projectDir = Path.Combine(request.Path, request.Name);

            // 如果同名项目目录已存在且不为空，禁止创建
            if (Directory.Exists(projectDir))
            {
                var entries = Directory.EnumerateFileSystemEntries(projectDir);
                if (entries.Any())
                {
                    return Conflict(new { success = false, error = "该目录下已存在同名项目文件夹且不为空" });
                }
                // 目录存在但为空，允许覆盖（删除后重建）
                Directory.Delete(projectDir, recursive: true);
            }

            // 模板目录路径
            var templatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyNewProject");

            if (Directory.Exists(templatePath))
            {
                // 从模板目录复制整个目录结构（排除文件，只复制文件夹）
                CopyDirectoryStructure(templatePath, projectDir);

                // 复制模板文件（排除二进制资源文件，只复制配置/文本文件）
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".tlor", ".lor", ".json", ".yaml", ".yml", ".xml", ".txt", ".md", ".resona", ".po", ".css", ".js", ".ts", ".html"
                };
                CopyTemplateFiles(templatePath, projectDir, allowedExtensions);

                // 更新项目 .tlor 文件中的元数据
                var tlorFiles = Directory.GetFiles(projectDir, "*.tlor");
                foreach (var tlorFile in tlorFiles)
                {
                    var content = await System.IO.File.ReadAllTextAsync(tlorFile);
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"(<title>)(.*?)(</title>)",
                        $"$1{request.Name}$3");
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"(<version>)(.*?)(</version>)",
                        $"$1{request.Version}$3");
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"(<versionCode>)(.*?)(</versionCode>)",
                        $"$1{request.VersionCode}$3");
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"(<companyName>)(.*?)(</companyName>)",
                        $"$1{request.CompanyName}$3");
                    await System.IO.File.WriteAllTextAsync(tlorFile, content);
                }
            }
            else
            {
                // 模板不存在时，使用默认结构创建
                CreateDefaultProjectStructure(projectDir, request.Name, request.CompanyName, request.Version, request.VersionCode);
            }

            _logger.LogInformation("新建项目成功: {Path}", projectDir);

            // 获取创建后的文件夹结构
            var folderTree = BuildFolderTree(projectDir);

            return Ok(new
            {
                success = true,
                data = new
                {
                    projectPath = projectDir,
                    tlorPath = Path.Combine(projectDir, "Project.tlor"),
                    name = request.Name,
                    folderTree = folderTree
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新建项目失败: {Path}\\{Name}", request.Path, request.Name);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 更新当前项目设置
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateProjectSettingsRequest request)
    {
        var editor = _sessionService.GetEditor();

        if (editor.CurrentProject == null || string.IsNullOrEmpty(editor.CurrentProjectPath))
        {
            return BadRequest(new { success = false, error = "当前没有打开的项目" });
        }

        var project = editor.CurrentProject;
        var projectPath = editor.CurrentProjectPath!;
        var projectDir = Path.GetDirectoryName(projectPath)!;

        // 更新内存中的元数据
        if (request.ProjectName != null)
        {
            var oldName = project.Metadata.Title;
            project.Metadata.Title = request.ProjectName;

            // 如果项目名称改变，需要重命名 .tlor 文件
            if (!string.Equals(oldName, request.ProjectName, StringComparison.OrdinalIgnoreCase))
            {
                var oldTlorPath = Path.Combine(projectDir, "Project.tlor");
                if (System.IO.File.Exists(oldTlorPath))
                {
                    var newTlorPath = Path.Combine(projectDir, "Project.tlor");
                    var content = await System.IO.File.ReadAllTextAsync(oldTlorPath);
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        "(<title>)(.*?)(</title>)",
                        $"$1{request.ProjectName}$3");
                    await System.IO.File.WriteAllTextAsync(newTlorPath, content);
                }
            }
        }

        if (request.CompanyName != null)
            project.Metadata.CompanyName = request.CompanyName;

        if (request.Version != null)
            project.Metadata.Version = request.Version;

        if (request.VersionCode != null)
            project.Metadata.VersionCode = request.VersionCode;

        // 持久化到 .tlor 文件
        try
        {
            var tlorPath = Path.Combine(projectDir, "Project.tlor");
            await editor.SaveProjectFileAsync(tlorPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存项目设置失败");
            return StatusCode(500, new { success = false, error = "保存项目文件失败: " + ex.Message });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// 获取项目的文件夹树结构
    /// </summary>
    [HttpGet("folder-tree")]
    public IActionResult GetFolderTree([FromQuery] string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return BadRequest(new { success = false, error = "项目路径不能为空" });
        }

        if (!Directory.Exists(projectPath))
        {
            return NotFound(new { success = false, error = "项目路径不存在" });
        }

        try
        {
            var tree = BuildFolderTree(projectPath);
            return Ok(new { success = true, data = tree });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件夹结构失败: {Path}", projectPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 递归获取文件夹树结构
    /// </summary>
    private FolderNode BuildFolderTree(string path)
    {
        var info = new DirectoryInfo(path);
        var node = new FolderNode
        {
            Name = info.Name,
            Path = info.FullName,
            IsDirectory = true,
            Children = new List<FolderNode>()
        };

        try
        {
            // 添加子目录
            foreach (var dir in info.GetDirectories().OrderBy(d => d.Name))
            {
                node.Children.Add(BuildFolderTree(dir.FullName));
            }

            // 添加文件
            foreach (var file in info.GetFiles().OrderBy(f => f.Name))
            {
                node.Children.Add(new FolderNode
                {
                    Name = file.Name,
                    Path = file.FullName,
                    IsDirectory = false,
                    Extension = file.Extension.ToLowerInvariant(),
                    Size = file.Length,
                    LastModified = file.LastWriteTime.ToString("o"),
                    Children = null
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 权限不足的目录，跳过
        }

        return node;
    }

    /// <summary>
    /// 仅复制目录结构（不复制文件）
    /// </summary>
    private void CopyDirectoryStructure(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, dir);
            var targetPath = Path.Combine(targetDir, relativePath);
            Directory.CreateDirectory(targetPath);
        }
    }

    /// <summary>
    /// 复制模板中允许的文件类型
    /// </summary>
    private void CopyTemplateFiles(string sourceDir, string targetDir, HashSet<string> allowedExtensions)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (!allowedExtensions.Contains(extension))
                continue;

            var relativePath = Path.GetRelativePath(sourceDir, file);
            var targetPath = Path.Combine(targetDir, relativePath);

            var targetFileDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetFileDir) && !Directory.Exists(targetFileDir))
            {
                Directory.CreateDirectory(targetFileDir);
            }

            System.IO.File.Copy(file, targetPath, overwrite: true);
        }
    }

    /// <summary>
    /// 创建默认项目结构（模板不存在时的后备方案）
    /// </summary>
    private void CreateDefaultProjectStructure(string projectDir, string name, string companyName, string version, string versionCode)
    {
        // 创建项目目录结构
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "UI"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Scripts"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Scripts", "Main"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Locales"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Locales", "Engine"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets", "Characters"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets", "Backgrounds"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets", "Musics"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets", "Voices"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets", "Sfx"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets", "Fonts"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Assets", "SFXs"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Saves"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Settings"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Settings", "Editor"));

        // 创建主项目 XML 文件 (.tlor)
        var tlorPath = Path.Combine(projectDir, "Project.tlor");
        var tlorContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<project version=""1.0"">
  <metadata>
    <title>{name}</title>
    <author></author>
    <version>{version}</version>
    <versionCode>{versionCode}</versionCode>
    <companyName>{companyName}</companyName>
  </metadata>
  <scenes>
    <scene id=""start"" />
  </scenes>
</project>";
        System.IO.File.WriteAllText(tlorPath, tlorContent);

        // 创建默认场景文件 (.lor)
        var mainLorPath = Path.Combine(projectDir, "Scripts", "Main", "start.lor");
        var lorContent = @"id: start
background: ''
bgm:
  path: ''
  volume: 80
  loop: true
dialogues: []";
        System.IO.File.WriteAllText(mainLorPath, lorContent);
    }

    /// <summary>
    /// 导入项目：从 .tlor 文件中读取剧本配置
    /// </summary>
    /// <param name="request">包含项目路径的请求</param>
    /// <returns>剧本内容列表</returns>
    [HttpPost("import")]
    public async Task<IActionResult> ImportProject([FromBody] ImportProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { success = false, error = "项目路径不能为空" });
        }

        try
        {
            var result = await ParseProjectAsync(request.ProjectPath);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入项目失败: {Path}", request.ProjectPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 解析项目：读取 .tlor 文件并返回剧本列表
    /// </summary>
    private async Task<ProjectParseResult> ParseProjectAsync(string projectPath)
    {
        var result = new ProjectParseResult();

        // 查找 .tlor 文件
        var tlorFiles = Directory.GetFiles(projectPath, "*.tlor", SearchOption.AllDirectories);
        
        foreach (var tlorFile in tlorFiles)
        {
            var scene = await ParseTlorFileAsync(tlorFile);
            if (scene != null)
            {
                result.Scenes.Add(scene);
            }
        }

        // 如果没找到 .tlor 文件，尝试扫描 Scripts/Main 目录下的 .lor 文件
        if (result.Scenes.Count == 0)
        {
            var scriptsMainPath = Path.Combine(projectPath, "Scripts", "Main");
            if (Directory.Exists(scriptsMainPath))
            {
                var lorFiles = Directory.GetFiles(scriptsMainPath, "*.lor");
                foreach (var lorFile in lorFiles)
                {
                    var scene = new SceneInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(lorFile),
                        LorFilePath = lorFile,
                        Content = await System.IO.File.ReadAllTextAsync(lorFile)
                    };
                    result.Scenes.Add(scene);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 解析单个 .tlor XML 文件
    /// </summary>
    private async Task<SceneInfo?> ParseTlorFileAsync(string tlorFilePath)
    {
        var xml = await System.IO.File.ReadAllTextAsync(tlorFilePath);
        var doc = XDocument.Parse(xml);

        // 尝试多种可能的 XML 命名空间和元素名称
        var projectElement = doc.Root;
        if (projectElement == null) return null;

        // 尝试查找场景列表
        XElement? scenesElement = null;

        // 尝试不同的元素名称
        var possibleNames = new[] { "scenes", "scene", "Scenes", "Scene", "scripts", "Scripts" };
        foreach (var name in possibleNames)
        {
            scenesElement = projectElement.Element(name);
            if (scenesElement != null) break;
        }

        if (scenesElement == null)
        {
            // 如果找不到明确的场景列表，检查根元素是否包含场景信息
            var firstScenePath = projectElement.Element("path")?.Value 
                ?? projectElement.Element("Path")?.Value
                ?? projectElement.Attribute("entry")?.Value;

            if (firstScenePath != null)
            {
                var lorPath = Path.Combine(Path.GetDirectoryName(tlorFilePath)!, firstScenePath);
                if (System.IO.File.Exists(lorPath))
                {
                    return new SceneInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(lorPath),
                        LorFilePath = lorPath,
                        Content = await System.IO.File.ReadAllTextAsync(lorPath)
                    };
                }
            }

            return null;
        }

        // 解析场景列表
        var sceneElements = scenesElement.Elements("scene")
            .Concat(scenesElement.Elements("Scene"))
            .ToList();

        if (sceneElements.Count == 0)
        {
            return null;
        }

        // 返回第一个场景（或配置中指定的默认场景）
        var defaultScene = sceneElements.FirstOrDefault(s => 
            s.Attribute("default")?.Value == "true" ||
            s.Attribute("Default")?.Value == "true");

        var targetScene = defaultScene ?? sceneElements.First();

        var lorPathElement = targetScene.Element("path") ?? targetScene.Element("Path");
        var lorFileName = targetScene.Attribute("id")?.Value ?? targetScene.Attribute("Id")?.Value;

        if (lorPathElement == null && lorFileName == null)
        {
            return null;
        }

        string? lorFilePath = null;
        string? content = null;

        if (lorPathElement != null)
        {
            lorFilePath = Path.Combine(Path.GetDirectoryName(tlorFilePath)!, lorPathElement.Value);
        }
        else if (lorFileName != null)
        {
            // 尝试从 Scripts/Main 目录查找
            var scriptsMainPath = Path.Combine(Path.GetDirectoryName(tlorFilePath)!, "Scripts", "Main");
            var potentialPath = Path.Combine(scriptsMainPath, $"{lorFileName}.lor");
            
            if (System.IO.File.Exists(potentialPath))
            {
                lorFilePath = potentialPath;
            }
            else
            {
                // 尝试在项目根目录查找
                potentialPath = Path.Combine(Path.GetDirectoryName(tlorFilePath)!, $"{lorFileName}.lor");
                if (System.IO.File.Exists(potentialPath))
                {
                    lorFilePath = potentialPath;
                }
            }
        }

        if (lorFilePath != null && System.IO.File.Exists(lorFilePath))
        {
            content = await System.IO.File.ReadAllTextAsync(lorFilePath);
        }

        var sceneId = targetScene.Attribute("id")?.Value 
            ?? targetScene.Attribute("Id")?.Value
            ?? lorFileName
            ?? Path.GetFileNameWithoutExtension(tlorFilePath);

        return new SceneInfo
        {
            Id = sceneId,
            LorFilePath = lorFilePath ?? string.Empty,
            Content = content ?? string.Empty
        };
    }

    /// <summary>
    /// 获取项目的场景列表（不包含完整内容）
    /// </summary>
    [HttpGet("scenes")]
    public async Task<IActionResult> GetScenes([FromQuery] string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return BadRequest(new { success = false, error = "项目路径不能为空" });
        }

        try
        {
            var result = await ParseProjectAsync(projectPath);
            var sceneList = result.Scenes.Select(s => new
            {
                id = s.Id,
                lorFilePath = s.LorFilePath
            }).ToList();

            return Ok(new { success = true, data = sceneList });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取场景列表失败: {Path}", projectPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 读取指定剧本文件的内容
    /// </summary>
    [HttpGet("scene")]
    public async Task<IActionResult> GetScene([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { success = false, error = "文件路径不能为空" });
        }

        try
        {
            if (!System.IO.File.Exists(path))
            {
                return NotFound(new { success = false, error = "文件不存在" });
            }

            var content = await System.IO.File.ReadAllTextAsync(path);
            var sceneId = Path.GetFileNameWithoutExtension(path);

            return Ok(new { success = true, data = new { id = sceneId, content } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取剧本失败: {Path}", path);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

/// <summary>
/// 新建项目请求
/// </summary>
public class NewProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string VersionCode { get; set; } = "1";
}

/// <summary>
/// 更新项目设置请求
/// </summary>
public class UpdateProjectSettingsRequest
{
    public string? ProjectName { get; set; }
    public string? CompanyName { get; set; }
    public string? Version { get; set; }
    public string? VersionCode { get; set; }
}

/// <summary>
/// 导入项目请求
/// </summary>
public class ImportProjectRequest
{
    public string ProjectPath { get; set; } = string.Empty;
}

/// <summary>
/// 项目解析结果
/// </summary>
public class ProjectParseResult
{
    public List<SceneInfo> Scenes { get; set; } = new();
}

/// <summary>
/// 场景信息
/// </summary>
public class SceneInfo
{
    public string Id { get; set; } = string.Empty;
    public string LorFilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 文件夹树节点
/// </summary>
public class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public string LastModified { get; set; } = string.Empty;
    public List<FolderNode>? Children { get; set; }
}

/// <summary>
/// 当前项目信息响应
/// </summary>
public class CurrentProjectResponse
{
    public string ProjectName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string VersionCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public List<string> SubDirectories { get; set; } = new();
}
