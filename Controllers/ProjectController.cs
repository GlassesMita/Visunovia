using Microsoft.AspNetCore.Mvc;
using System.Security;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Visunovia.Services;
using Visunovia.Services.Configuration;

namespace Visunovia.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private const string DefaultSceneId = "start";
    private const string ProjectFileFormatVersion = "1.1";
    private const string CharacterControlSchemaVersion = "2";
    private readonly ILogger<ProjectController> _logger;
    private readonly EditorSessionService _sessionService;
    private readonly SettingsService _settingsService;
    private static DateTime _lastNoProjectLogUtc = DateTime.MinValue;

    public ProjectController(ILogger<ProjectController> logger, EditorSessionService sessionService, SettingsService settingsService)
    {
        _logger = logger;
        _sessionService = sessionService;
        _settingsService = settingsService;
    }

    private static string CreateBlankLorJson(string sceneId)
    {
        return JsonSerializer.Serialize(new
        {
            id = sceneId,
            background = string.Empty,
            bgm = new
            {
                path = string.Empty,
                volume = 80,
                loop = true
            },
            dialogues = Array.Empty<object>()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// 获取最近打开的项目列表。
    /// 列表持久化在应用程序 .exe.config 的 RecentProjects 配置项中。
    /// </summary>
    [HttpGet("recentProjects")]
    public IActionResult GetRecentProjects()
    {
        return Ok(new
        {
            success = true,
            data = LoadRecentProjects()
        });
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
            if (DateTime.UtcNow - _lastNoProjectLogUtc > TimeSpan.FromSeconds(10))
            {
                _lastNoProjectLogUtc = DateTime.UtcNow;
                LogProjectStep("currentProject", "当前没有打开的项目", new Dictionary<string, object?>
                {
                    ["hasProject"] = editor.CurrentProject != null,
                    ["currentProjectPath"] = editor.CurrentProjectPath
                });
            }
            return Ok(new
            {
                success = true,
                data = (object?)null,
                message = "当前没有打开的项目"
            });
        }

        var project = editor.CurrentProject;
        var projectPath = editor.CurrentProjectPath!;
        LogProjectStep("currentProject", "开始获取当前项目状态");
        LogProjectStep("currentProject", "已找到内存中的当前项目", new Dictionary<string, object?>
        {
            ["projectTitle"] = project.Metadata.Title,
            ["currentProjectPath"] = projectPath
        });

        // 获取项目根目录（.tlor 文件所在目录）
        var projectRoot = Path.GetDirectoryName(projectPath) ?? projectPath;
        LogProjectStep("currentProject", "解析项目根目录", new Dictionary<string, object?>
        {
            ["projectRoot"] = projectRoot
        });

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
            LogProjectStep("currentProject", "获取项目子目录失败", new Dictionary<string, object?>
            {
                ["projectRoot"] = projectRoot,
                ["error"] = ex.Message
            });
        }

        LogProjectStep("currentProject", "返回当前项目信息", new Dictionary<string, object?>
        {
            ["projectName"] = project.Metadata.Title,
            ["subDirectoryCount"] = subDirectories.Count
        });

        return Ok(new
        {
            success = true,
            data = new CurrentProjectResponse
            {
                ProjectName = project.Metadata.Title,
                Version = project.Metadata.Version,
                VersionCode = project.Metadata.VersionCode,
                CompanyName = project.Metadata.CompanyName,
                RatingSystem = project.Metadata.RatingSystem,
                RatingValue = project.Metadata.RatingValue,
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
                    ".tlor", ".lor", ".json", ".xml", ".txt", ".md", ".resona", ".po", ".css", ".js", ".ts", ".html"
                };
                CopyTemplateFiles(templatePath, projectDir, allowedExtensions);

                // 更新项目 .tlor 文件中的元数据
                var tlorFiles = Directory.GetFiles(projectDir, "*.tlor");
                foreach (var tlorFile in tlorFiles)
                {
                    var content = await System.IO.File.ReadAllTextAsync(tlorFile);
                    content = ReplaceXmlElementValue(content, "title", request.Name);
                    content = ReplaceXmlElementValue(content, "version", request.Version);
                    content = ReplaceXmlElementValue(content, "versionCode", request.VersionCode);
                    content = ReplaceXmlElementValue(content, "companyName", request.CompanyName);
                    await System.IO.File.WriteAllTextAsync(tlorFile, content);
                }
            }
            else
            {
                // 模板不存在时，使用默认结构创建
                CreateDefaultProjectStructure(projectDir, request.Name, request.CompanyName, request.Version, request.VersionCode);
            }

            _logger.LogInformation("新建项目成功: {Path}", projectDir);

            // 将新项目加载到 EditorService，使后续 API 能获取到当前项目
            var editor = _sessionService.GetEditor();
            var tlorPath = Path.Combine(projectDir, "Project.tlor");
            if (System.IO.File.Exists(tlorPath))
            {
                await editor.LoadProjectAsync(tlorPath, projectDir);
                AddRecentProject(projectDir, request.Name);
            }

            // 获取创建后的文件夹结构
            var folderTree = BuildFolderTree(projectDir);

            return Ok(new
            {
                success = true,
                data = new
                {
                    projectPath = projectDir,
                    tlorPath = tlorPath,
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
                    content = ReplaceXmlElementValue(content, "title", request.ProjectName);
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

        if (request.RatingSystem != null)
            project.Metadata.RatingSystem = NormalizeRatingSystem(request.RatingSystem);

        if (request.RatingValue != null)
            project.Metadata.RatingValue = NormalizeRatingValue(project.Metadata.RatingSystem, request.RatingValue);
        else
            project.Metadata.RatingValue = NormalizeRatingValue(project.Metadata.RatingSystem, project.Metadata.RatingValue);

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
    /// 只读读取当前项目内的文本文件内容，用于资源索引等只读预览。
    /// </summary>
    [HttpGet("file-content")]
    public async Task<IActionResult> GetProjectFileContent([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { success = false, error = "文件路径不能为空" });
        }

        var editor = _sessionService.GetEditor();
        if (string.IsNullOrWhiteSpace(editor.CurrentProjectPath))
        {
            return BadRequest(new { success = false, error = "当前没有打开的项目" });
        }

        var projectRoot = Path.GetFullPath(Path.GetDirectoryName(editor.CurrentProjectPath) ?? editor.CurrentProjectPath);
        var fullPath = Path.GetFullPath(path);

        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid("不允许访问项目目录之外的文件");
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { success = false, error = "文件不存在" });
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".resona", ".json", ".xml", ".txt", ".md", ".po", ".css", ".js", ".ts", ".html"
        };

        var extension = Path.GetExtension(fullPath);
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { success = false, error = "该文件类型不支持文本预览" });
        }

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > 1024 * 1024)
            {
                return BadRequest(new { success = false, error = "文件过大，无法预览" });
            }

            var content = await System.IO.File.ReadAllTextAsync(fullPath);
            return Ok(new
            {
                success = true,
                data = new
                {
                    path = fullPath,
                    name = Path.GetFileName(fullPath),
                    content
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取项目文件内容失败: {Path}", fullPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 读取当前项目 Assets/Characters/Manifest.resona 角色附加配置。
    /// 角色目录本身仍以 Assets/Characters 下的文件夹为准，此配置保存头像、颜色、备注和立绘清单。
    /// </summary>
    [HttpGet("characters/config")]
    public async Task<IActionResult> GetCharacterConfig()
    {
        if (!TryGetCharactersConfigPath(out var configPath, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        try
        {
            if (!System.IO.File.Exists(configPath))
            {
                var legacyPath = Path.Combine(Path.GetDirectoryName(configPath)!, "Characters.json");
                if (!System.IO.File.Exists(legacyPath))
                {
                    return Ok(new { success = true, data = new CharacterConfigResponse() });
                }
                configPath = legacyPath;
            }

            var json = await System.IO.File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<CharacterConfigResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new CharacterConfigResponse();

            return Ok(new { success = true, data = config });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "角色配置文件格式无效: {Path}", configPath);
            return BadRequest(new { success = false, error = "角色配置文件格式无效: " + ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取角色配置失败: {Path}", configPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 写入当前项目 Assets/Characters/Manifest.resona 角色附加配置。
    /// </summary>
    [HttpPut("characters/config")]
    public async Task<IActionResult> SaveCharacterConfig([FromBody] CharacterConfigResponse request)
    {
        if (!TryGetCharactersConfigPath(out var configPath, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        try
        {
            var charactersRoot = Path.GetDirectoryName(configPath)!;
            Directory.CreateDirectory(charactersRoot);

            var sanitized = new CharacterConfigResponse
            {
                Characters = (request?.Characters ?? new List<CharacterConfigEntry>())
                    .Where(character => !string.IsNullOrWhiteSpace(character.Id))
                    .Select(character => new CharacterConfigEntry
                    {
                        Id = Path.GetFileName(character.Id.Trim()),
                        DisplayId = character.DisplayId?.Trim() ?? string.Empty,
                        Affiliation = character.Affiliation?.Trim() ?? string.Empty,
                        Color = character.Color?.Trim() ?? string.Empty,
                        Avatar = character.Avatar?.Trim() ?? string.Empty,
                        Sprites = (character.Sprites ?? new List<string>())
                            .Where(sprite => !string.IsNullOrWhiteSpace(sprite))
                            .Select(sprite => sprite.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        Note = character.Note?.Trim() ?? string.Empty
                    })
                    .Where(character => !string.IsNullOrWhiteSpace(character.Id))
                    .ToList()
            };

            var json = JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(configPath, json);

            return Ok(new { success = true, data = sanitized });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存角色配置失败: {Path}", configPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 导入单个资产文件到当前项目的 Assets 子目录。
    /// </summary>
    [HttpPost("assets/import")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> ImportAsset([FromForm] string targetDirectory, [FromForm] IFormFile file, [FromForm] string? relativePath)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, error = "请选择要导入的资产文件" });
        }

        if (!TryResolveAssetsPath(targetDirectory, mustBeUnderAssets: true, out var targetPath, out _, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        if (!Directory.Exists(targetPath))
        {
            return BadRequest(new { success = false, error = "目标资产目录不存在" });
        }

        var uploadPath = string.IsNullOrWhiteSpace(relativePath) ? file.FileName : relativePath;
        uploadPath = uploadPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(uploadPath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest(new { success = false, error = "文件名无效" });
        }

        var relativeDirectory = Path.GetDirectoryName(uploadPath) ?? string.Empty;
        if (relativeDirectory.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            return BadRequest(new { success = false, error = "目录路径无效" });
        }

        var destinationDirectory = Path.GetFullPath(Path.Combine(targetPath, relativeDirectory));
        if (!IsPathInside(targetPath, destinationDirectory))
        {
            return BadRequest(new { success = false, error = "目录路径无效" });
        }

        var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, fileName));
        if (!IsPathInside(targetPath, destinationPath))
        {
            return BadRequest(new { success = false, error = "文件路径无效" });
        }

        if (System.IO.File.Exists(destinationPath))
        {
            return Conflict(new { success = false, error = "目标目录已存在同名资产" });
        }

        try
        {
            Directory.CreateDirectory(destinationDirectory);
            await using var stream = System.IO.File.Create(destinationPath);
            await file.CopyToAsync(stream);

            var fileInfo = new FileInfo(destinationPath);
            return Ok(new
            {
                success = true,
                data = new FolderNode
                {
                    Name = fileInfo.Name,
                    Path = fileInfo.FullName,
                    IsDirectory = false,
                    Extension = fileInfo.Extension.ToLowerInvariant(),
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime.ToString("o"),
                    Children = null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入资产失败: {Path}", destinationPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 删除当前项目 Assets 目录内的资产文件或空目录。
    /// </summary>
    [HttpDelete("assets")]
    public IActionResult DeleteAsset([FromQuery] string path)
    {
        if (!TryResolveAssetsPath(path, mustBeUnderAssets: true, out var fullPath, out _, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        try
        {
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
                return Ok(new { success = true });
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                return Ok(new { success = true });
            }

            return NotFound(new { success = false, error = "资产不存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除资产失败: {Path}", fullPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 重命名当前项目 Assets 目录内的资产文件或文件夹。
    /// </summary>
    [HttpPut("assets/rename")]
    public IActionResult RenameAsset([FromBody] RenameAssetRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { success = false, error = "资产路径不能为空" });
        }

        if (!TryResolveAssetsPath(request.Path, mustBeUnderAssets: true, out var fullPath, out var assetsRoot, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        var newName = request.NewName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || newName == "." || newName == "..")
        {
            return BadRequest(new { success = false, error = "新名称无效" });
        }

        var parentPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(parentPath) || !IsPathInside(assetsRoot, parentPath))
        {
            return BadRequest(new { success = false, error = "不允许重命名 Assets 根目录" });
        }

        var destinationPath = Path.GetFullPath(Path.Combine(parentPath, newName));
        if (!IsPathInside(assetsRoot, destinationPath))
        {
            return BadRequest(new { success = false, error = "目标路径无效" });
        }

        if (System.IO.File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            return Conflict(new { success = false, error = "目标目录已存在同名资产" });
        }

        try
        {
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Move(fullPath, destinationPath);
                var fileInfo = new FileInfo(destinationPath);
                return Ok(new
                {
                    success = true,
                    data = new FolderNode
                    {
                        Name = fileInfo.Name,
                        Path = fileInfo.FullName,
                        IsDirectory = false,
                        Extension = fileInfo.Extension.ToLowerInvariant(),
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime.ToString("o"),
                        Children = null
                    }
                });
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Move(fullPath, destinationPath);
                return Ok(new { success = true, data = BuildFolderTree(destinationPath) });
            }

            return NotFound(new { success = false, error = "资产不存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重命名资产失败: {Path} -> {NewName}", fullPath, newName);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private bool TryResolveAssetsPath(string path, bool mustBeUnderAssets, out string fullPath, out string assetsRoot, out string error)
    {
        fullPath = string.Empty;
        assetsRoot = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "路径不能为空";
            return false;
        }

        var editor = _sessionService.GetEditor();
        if (string.IsNullOrWhiteSpace(editor.CurrentProjectPath))
        {
            error = "当前没有打开的项目";
            return false;
        }

        var projectRoot = Path.GetFullPath(Path.GetDirectoryName(editor.CurrentProjectPath) ?? editor.CurrentProjectPath);
        assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets"));
        fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(assetsRoot))
        {
            error = "当前项目缺少 Assets 目录";
            return false;
        }

        if (mustBeUnderAssets && !IsPathInside(assetsRoot, fullPath))
        {
            error = "只能管理 Assets 目录内的资产";
            return false;
        }

        return true;
    }

    private bool TryGetCharactersConfigPath(out string configPath, out string error)
    {
        configPath = string.Empty;
        error = string.Empty;

        var editor = _sessionService.GetEditor();
        if (string.IsNullOrWhiteSpace(editor.CurrentProjectPath))
        {
            error = "当前没有打开的项目";
            return false;
        }

        var projectRoot = Path.GetFullPath(Path.GetDirectoryName(editor.CurrentProjectPath) ?? editor.CurrentProjectPath);
        var assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets"));
        var charactersRoot = Path.GetFullPath(Path.Combine(assetsRoot, "Characters"));
        configPath = Path.GetFullPath(Path.Combine(charactersRoot, "Manifest.resona"));

        if (!IsPathInside(assetsRoot, configPath))
        {
            error = "角色配置路径无效";
            return false;
        }

        return true;
    }

    private static bool IsPathInside(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedCandidate.Equals(normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
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
    <project version=""{ProjectFileFormatVersion}"">
  <metadata>
    <title>{name}</title>
    <author></author>
    <version>{version}</version>
    <versionCode>{versionCode}</versionCode>
        <projectFormatVersion>{ProjectFileFormatVersion}</projectFormatVersion>
        <characterControlSchemaVersion>{CharacterControlSchemaVersion}</characterControlSchemaVersion>
    <companyName>{companyName}</companyName>
    <ratingSystem>CADPA</ratingSystem>
    <ratingValue>12+</ratingValue>
  </metadata>
  <scenes>
    <scene id=""start"" />
  </scenes>
</project>";
        System.IO.File.WriteAllText(tlorPath, tlorContent);

            // 创建默认场景文件 (.lor)，内容使用后端内嵌的空白 JSON 结构。
            var mainLorPath = Path.Combine(projectDir, "Scripts", "Main", $"{DefaultSceneId}.lor");
            System.IO.File.WriteAllText(mainLorPath, CreateBlankLorJson(DefaultSceneId));
    }

    /// <summary>
    /// 导入项目：从 .tlor 文件中读取剧本配置
    /// </summary>
    /// <param name="request">包含项目路径的请求</param>
    /// <returns>剧本内容列表</returns>
    [HttpPost("import")]
    public async Task<IActionResult> ImportProject([FromBody] ImportProjectRequest request)
    {
        LogProjectStep("import", "收到导入项目请求", new Dictionary<string, object?>
        {
            ["requestProjectPath"] = request.ProjectPath
        });

        if (string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            LogProjectStep("import", "导入失败：项目路径为空");
            return BadRequest(new { success = false, error = "项目路径不能为空" });
        }

        try
        {
            var (projectRoot, tlorPath) = ResolveProjectPaths(request.ProjectPath);
            LogProjectStep("import", "项目路径已规范化", new Dictionary<string, object?>
            {
                ["projectRoot"] = projectRoot,
                ["tlorPath"] = tlorPath,
                ["projectRootExists"] = Directory.Exists(projectRoot),
                ["tlorExists"] = System.IO.File.Exists(tlorPath)
            });

            if (!Directory.Exists(projectRoot))
            {
                LogProjectStep("import", "导入失败：项目根目录不存在", new Dictionary<string, object?>
                {
                    ["projectRoot"] = projectRoot
                });
                return NotFound(new { success = false, error = "项目路径不存在" });
            }

            if (!System.IO.File.Exists(tlorPath))
            {
                LogProjectStep("import", "导入失败：未找到 Project.tlor", new Dictionary<string, object?>
                {
                    ["tlorPath"] = tlorPath
                });
                return NotFound(new { success = false, error = "未找到 Project.tlor 文件" });
            }

            await EnsureProjectHasScriptFilesAsync(projectRoot, "导入项目前校验");
            await RepairProjectFileIfNeededAsync(tlorPath, projectRoot, "导入项目前校验");

            var result = await ParseProjectAsync(projectRoot);
            LogProjectStep("import", "项目解析完成", new Dictionary<string, object?>
            {
                ["sceneCount"] = result.Scenes.Count
            });

            // 将项目加载到 EditorService，使后续 API 能获取到当前项目
            var editor = _sessionService.GetEditor();
            LogProjectStep("import", "开始加载项目到编辑器会话", new Dictionary<string, object?>
            {
                ["tlorPath"] = tlorPath,
                ["projectRoot"] = projectRoot
            });

            var loaded = await editor.LoadProjectAsync(tlorPath, projectRoot);
            if (!loaded || editor.CurrentProject == null || string.IsNullOrEmpty(editor.CurrentProjectPath))
            {
                LogProjectStep("import", "导入失败：编辑器会话加载项目失败", new Dictionary<string, object?>
                {
                    ["loaded"] = loaded,
                    ["currentProjectPath"] = editor.CurrentProjectPath
                });
                return StatusCode(500, new { success = false, error = "加载项目到编辑器会话失败" });
            }

            LogProjectStep("import", "项目已加载为当前项目", new Dictionary<string, object?>
            {
                ["projectName"] = editor.CurrentProject.Metadata.Title,
                ["currentProjectPath"] = editor.CurrentProjectPath
            });

            AddRecentProject(projectRoot, editor.CurrentProject.Metadata.Title);

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入项目失败: {Path}", request.ProjectPath);
            LogProjectStep("import", "导入项目发生异常", new Dictionary<string, object?>
            {
                ["requestProjectPath"] = request.ProjectPath,
                ["error"] = ex.Message
            });
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private (string ProjectRoot, string TlorPath) ResolveProjectPaths(string inputPath)
    {
        var normalizedPath = Path.GetFullPath(Uri.UnescapeDataString(inputPath.Trim()));
        if (System.IO.File.Exists(normalizedPath) && string.Equals(Path.GetExtension(normalizedPath), ".tlor", StringComparison.OrdinalIgnoreCase))
        {
            var projectRoot = Path.GetDirectoryName(normalizedPath) ?? normalizedPath;
            return (projectRoot, normalizedPath);
        }

        return (normalizedPath, Path.Combine(normalizedPath, "Project.tlor"));
    }

    private List<RecentProjectInfo> LoadRecentProjects()
    {
        try
        {
            var raw = _settingsService.GetRawValue(DefaultSettings.RecentProjectsKey);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            var projects = JsonSerializer.Deserialize<List<RecentProjectInfo>>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            return projects
                .Where(project => !string.IsNullOrWhiteSpace(project.Path))
                .GroupBy(project => NormalizePathKey(project.Path), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(project => project.LastOpened ?? string.Empty)
                .Take(GetRecentProjectsLimit())
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "最近项目配置解析失败，将返回空列表");
            return [];
        }
    }

    private void AddRecentProject(string projectRoot, string? projectName)
    {
        try
        {
            var normalizedRoot = Path.GetFullPath(projectRoot);
            var displayName = string.IsNullOrWhiteSpace(projectName)
                ? new DirectoryInfo(normalizedRoot).Name
                : projectName.Trim();

            var projects = LoadRecentProjects();
            var normalizedKey = NormalizePathKey(normalizedRoot);
            projects.RemoveAll(project => string.Equals(NormalizePathKey(project.Path), normalizedKey, StringComparison.OrdinalIgnoreCase));
            projects.Insert(0, new RecentProjectInfo
            {
                Name = displayName,
                Path = normalizedRoot,
                LastOpened = DateTime.UtcNow.ToString("O")
            });

            var limit = GetRecentProjectsLimit();
            if (projects.Count > limit)
            {
                projects = projects.Take(limit).ToList();
            }

            var json = JsonSerializer.Serialize(projects, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            if (!_settingsService.SetAndSave(DefaultSettings.RecentProjectsKey, json))
            {
                _logger.LogWarning("最近项目保存失败: {Path}", normalizedRoot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新最近项目失败: {Path}", projectRoot);
        }
    }

    private int GetRecentProjectsLimit()
    {
        return Math.Max(1, _settingsService.Get<int>(DefaultSettings.RecentProjectsLimitKey, DefaultSettings.DefaultRecentProjectsLimit));
    }

    private static string NormalizePathKey(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string ReplaceXmlElementValue(string xml, string elementName, string value)
    {
        var escapedValue = SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        var pattern = $"(<{elementName}>)(.*?)(</{elementName}>)";
        var replaced = System.Text.RegularExpressions.Regex.Replace(
            xml,
            pattern,
            match => $"{match.Groups[1].Value}{escapedValue}{match.Groups[3].Value}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        if (!string.Equals(replaced, xml, StringComparison.Ordinal))
        {
            return replaced;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            xml,
            "(</metadata>)",
            $"  <{elementName}>{escapedValue}</{elementName}>{Environment.NewLine}$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private async Task RepairProjectFileIfNeededAsync(string tlorPath, string projectRoot, string? reason = null)
    {
        if (!System.IO.File.Exists(tlorPath))
        {
            return;
        }

        var content = await System.IO.File.ReadAllTextAsync(tlorPath);
        try
        {
            var doc = XDocument.Parse(content);
            var root = doc.Root;
            var metadata = root?.Element("metadata");
            var changed = false;

            if (root == null || !string.Equals(root.Name.LocalName, "project", StringComparison.OrdinalIgnoreCase))
            {
                throw new XmlException("Project.tlor 缺少 project 根元素");
            }

            var versionAttribute = root.Attribute("version");
            if (versionAttribute == null || !string.Equals(versionAttribute.Value, ProjectFileFormatVersion, StringComparison.Ordinal))
            {
                root.SetAttributeValue("version", ProjectFileFormatVersion);
                changed = true;
            }

            if (metadata == null)
            {
                metadata = new XElement("metadata");
                root.AddFirst(metadata);
                changed = true;
            }

            changed |= EnsureMetadataElement(metadata, "title", new DirectoryInfo(projectRoot).Name);
            changed |= EnsureMetadataElement(metadata, "author", string.Empty);
            changed |= EnsureMetadataElement(metadata, "version", "1.0");
            changed |= EnsureMetadataElement(metadata, "versionCode", "1");
            changed |= EnsureMetadataElement(metadata, "projectFormatVersion", ProjectFileFormatVersion);
            changed |= EnsureMetadataElement(metadata, "characterControlSchemaVersion", CharacterControlSchemaVersion);
            changed |= EnsureMetadataElement(metadata, "companyName", string.Empty);
            changed |= EnsureMetadataElement(metadata, "ratingSystem", "CADPA");
            changed |= EnsureMetadataElement(metadata, "ratingValue", "12+");

            var scenes = root.Element("scenes");
            if (scenes == null)
            {
                scenes = new XElement("scenes");
                root.Add(scenes);
                changed = true;
            }

            var sceneElements = scenes.Elements("scene")
                .Concat(scenes.Elements("Scene"))
                .ToList();

            if (sceneElements.Count == 0)
            {
                foreach (var sceneId in GetSceneIdsFromDirectory(projectRoot))
                {
                    scenes.Add(new XElement("scene", new XAttribute("id", sceneId)));
                }
                changed = true;
            }
            else if (!sceneElements.Any(scene => SceneReferenceHasExistingScript(scene, projectRoot, tlorPath)))
            {
                scenes.RemoveNodes();
                foreach (var sceneId in GetSceneIdsFromDirectory(projectRoot))
                {
                    scenes.Add(new XElement("scene", new XAttribute("id", sceneId)));
                }
                changed = true;

                LogProjectStep("repair", "Project.tlor 场景引用全部失效，已改为实际脚本列表", new Dictionary<string, object?>
                {
                    ["projectRoot"] = projectRoot,
                    ["reason"] = reason
                });
            }

            if (changed)
            {
                await WriteRepairedProjectFileAsync(tlorPath, doc, reason ?? "补齐缺失节点");
            }
        }
        catch (XmlException ex)
        {
            LogProjectStep("repair", "Project.tlor XML 损坏，开始重建", new Dictionary<string, object?>
            {
                ["tlorPath"] = tlorPath,
                ["error"] = ex.Message,
                ["reason"] = reason
            });

            var repairedDoc = BuildProjectDocumentFromDirectory(projectRoot, content);
            await WriteRepairedProjectFileAsync(tlorPath, repairedDoc, reason ?? ex.Message);
        }
    }

    private async Task EnsureProjectHasScriptFilesAsync(string projectRoot, string reason)
    {
        var scriptsMainPath = Path.Combine(projectRoot, "Scripts", "Main");
        Directory.CreateDirectory(scriptsMainPath);

        if (Directory.GetFiles(scriptsMainPath, "*.lor").Length > 0)
        {
            return;
        }

        var defaultScriptPath = Path.Combine(scriptsMainPath, $"{DefaultSceneId}.lor");
        await System.IO.File.WriteAllTextAsync(defaultScriptPath, CreateBlankLorJson(DefaultSceneId));

        LogProjectStep("repair", "项目没有任何脚本文件，已创建内嵌空白 Lor JSON", new Dictionary<string, object?>
        {
            ["projectRoot"] = projectRoot,
            ["scriptPath"] = defaultScriptPath,
            ["reason"] = reason
        });
    }

    private static bool SceneReferenceHasExistingScript(XElement sceneElement, string projectRoot, string tlorPath)
    {
        var explicitPath = sceneElement.Element("path")?.Value ?? sceneElement.Element("Path")?.Value;
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var resolvedPath = Path.Combine(Path.GetDirectoryName(tlorPath) ?? projectRoot, explicitPath);
            if (System.IO.File.Exists(resolvedPath))
            {
                return true;
            }
        }

        var sceneId = sceneElement.Attribute("id")?.Value ?? sceneElement.Attribute("Id")?.Value;
        if (string.IsNullOrWhiteSpace(sceneId))
        {
            return false;
        }

        return System.IO.File.Exists(Path.Combine(projectRoot, "Scripts", "Main", $"{sceneId}.lor"))
            || System.IO.File.Exists(Path.Combine(projectRoot, $"{sceneId}.lor"));
    }

    private static bool EnsureMetadataElement(XElement metadata, string name, string defaultValue)
    {
        var element = metadata.Element(name);
        if (element != null)
        {
            return false;
        }

        metadata.Add(new XElement(name, defaultValue));
        return true;
    }

    private static string NormalizeRatingSystem(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized is "CADPA" or "GSRR" or "CERO" or "PEGI" ? normalized : "CADPA";
    }

    private static string NormalizeRatingValue(string ratingSystem, string value)
    {
        var normalizedSystem = NormalizeRatingSystem(ratingSystem);
        var normalizedValue = value.Trim().ToUpperInvariant();
        return normalizedSystem switch
        {
            "CADPA" => normalizedValue is "8+" or "12+" or "16+" ? normalizedValue : "12+",
            "GSRR" => normalizedValue is "G" or "P" or "PG12" or "PG15" or "R18" ? normalizedValue : "PG12",
            "CERO" => normalizedValue is "A" or "B" or "C" or "D" or "Z" ? normalizedValue : "B",
            "PEGI" => normalizedValue is "3" or "4" or "6" or "7" or "12" or "16" or "18" ? normalizedValue : "12",
            _ => "12+"
        };
    }

    private XDocument BuildProjectDocumentFromDirectory(string projectRoot, string oldContent)
    {
        return new XDocument(
            new XElement("project",
                new XAttribute("version", ProjectFileFormatVersion),
                new XElement("metadata",
                    new XElement("title", ExtractXmlLikeTagValue(oldContent, "title") ?? new DirectoryInfo(projectRoot).Name),
                    new XElement("author", ExtractXmlLikeTagValue(oldContent, "author") ?? string.Empty),
                    new XElement("version", ExtractXmlLikeTagValue(oldContent, "version") ?? "1.0"),
                    new XElement("versionCode", ExtractXmlLikeTagValue(oldContent, "versionCode") ?? "1"),
                    new XElement("projectFormatVersion", ProjectFileFormatVersion),
                    new XElement("characterControlSchemaVersion", CharacterControlSchemaVersion),
                    new XElement("companyName", ExtractXmlLikeTagValue(oldContent, "companyName") ?? string.Empty),
                    new XElement("ratingSystem", ExtractXmlLikeTagValue(oldContent, "ratingSystem") ?? "CADPA"),
                    new XElement("ratingValue", ExtractXmlLikeTagValue(oldContent, "ratingValue") ?? "12+")
                ),
                new XElement("scenes",
                    GetSceneIdsFromDirectory(projectRoot).Select(sceneId => new XElement("scene", new XAttribute("id", sceneId)))
                )
            )
        );
    }

    private static IEnumerable<string> GetSceneIdsFromDirectory(string projectRoot)
    {
        var scriptsMainPath = Path.Combine(projectRoot, "Scripts", "Main");
        if (!Directory.Exists(scriptsMainPath))
        {
            return new[] { "start" };
        }

        var sceneIds = Directory.GetFiles(scriptsMainPath, "*.lor")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray();

        return sceneIds.Length > 0 ? sceneIds : new[] { "start" };
    }

    private async Task WriteRepairedProjectFileAsync(string tlorPath, XDocument doc, string reason)
    {
        await System.IO.File.WriteAllTextAsync(tlorPath, doc.ToString());
        LogProjectStep("repair", "Project.tlor 已自动修复", new Dictionary<string, object?>
        {
            ["tlorPath"] = tlorPath,
            ["reason"] = reason
        });
    }

    private static string? ExtractXmlLikeTagValue(string content, string tagName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            content,
            $"<{tagName}>(.*?)</{tagName}>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private void LogProjectStep(string operation, string step, Dictionary<string, object?>? details = null)
    {
        var detailText = details == null || details.Count == 0
            ? string.Empty
            : " | " + string.Join(", ", details.Select(kvp => $"{kvp.Key}={kvp.Value ?? "<null>"}"));
        var message = $"[Project:{operation}] {step}{detailText}";
        Console.WriteLine(message);
        _logger.LogInformation("{Message}", message);
    }

    /// <summary>
    /// 解析项目：读取 .tlor 文件并返回剧本列表
    /// </summary>
    private async Task<ProjectParseResult> ParseProjectAsync(string projectPath)
    {
        var result = new ProjectParseResult();
        LogProjectStep("parse", "开始解析项目", new Dictionary<string, object?>
        {
            ["projectPath"] = projectPath
        });

        // 查找 .tlor 文件
        var tlorFiles = Directory.GetFiles(projectPath, "*.tlor", SearchOption.AllDirectories);
        LogProjectStep("parse", "扫描 .tlor 文件完成", new Dictionary<string, object?>
        {
            ["tlorCount"] = tlorFiles.Length
        });
        
        foreach (var tlorFile in tlorFiles)
        {
            LogProjectStep("parse", "解析 .tlor 文件", new Dictionary<string, object?>
            {
                ["tlorFile"] = tlorFile
            });
            var scenes = await ParseTlorFileScenesAsync(tlorFile);
            foreach (var scene in scenes)
            {
                result.Scenes.Add(scene);
                LogProjectStep("parse", "从 .tlor 解析到场景", new Dictionary<string, object?>
                {
                    ["sceneId"] = scene.Id,
                    ["lorFilePath"] = scene.LorFilePath
                });
            }
        }

        // 如果没找到 .tlor 文件，尝试扫描 Scripts/Main 目录下的 .lor 文件
        if (result.Scenes.Count == 0)
        {
            var scriptsMainPath = Path.Combine(projectPath, "Scripts", "Main");
            if (Directory.Exists(scriptsMainPath))
            {
                var lorFiles = Directory.GetFiles(scriptsMainPath, "*.lor");
                LogProjectStep("parse", "未从 .tlor 找到场景，扫描 Scripts/Main", new Dictionary<string, object?>
                {
                    ["scriptsMainPath"] = scriptsMainPath,
                    ["lorCount"] = lorFiles.Length
                });
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

        LogProjectStep("parse", "项目解析结束", new Dictionary<string, object?>
        {
            ["sceneCount"] = result.Scenes.Count
        });

        return result;
    }

    /// <summary>
    /// 解析单个 .tlor XML 文件
    /// </summary>
    private async Task<List<SceneInfo>> ParseTlorFileScenesAsync(string tlorFilePath)
    {
        var result = new List<SceneInfo>();
        var xml = await System.IO.File.ReadAllTextAsync(tlorFilePath);
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            LogProjectStep("parse", "跳过格式损坏的 .tlor 文件，将回退扫描 .lor 文件", new Dictionary<string, object?>
            {
                ["tlorFile"] = tlorFilePath,
                ["error"] = ex.Message
            });
            return result;
        }

        // 尝试多种可能的 XML 命名空间和元素名称
        var projectElement = doc.Root;
        if (projectElement == null) return result;

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
                    result.Add(new SceneInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(lorPath),
                        LorFilePath = lorPath,
                        Content = await System.IO.File.ReadAllTextAsync(lorPath)
                    });
                }
            }

            return result;
        }

        // 解析场景列表
        var sceneElements = scenesElement.Elements("scene")
            .Concat(scenesElement.Elements("Scene"))
            .ToList();

        if (sceneElements.Count == 0)
        {
            return result;
        }

        foreach (var targetScene in sceneElements)
        {
            var lorPathElement = targetScene.Element("path") ?? targetScene.Element("Path");
            var lorFileName = targetScene.Attribute("id")?.Value ?? targetScene.Attribute("Id")?.Value;

            if (lorPathElement == null && lorFileName == null)
            {
                continue;
            }

            string? lorFilePath = null;

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
                var sceneId = targetScene.Attribute("id")?.Value 
                    ?? targetScene.Attribute("Id")?.Value
                    ?? lorFileName
                    ?? Path.GetFileNameWithoutExtension(lorFilePath);

                result.Add(new SceneInfo
                {
                    Id = sceneId,
                    LorFilePath = lorFilePath,
                    Content = await System.IO.File.ReadAllTextAsync(lorFilePath)
                });
            }
            else
            {
                LogProjectStep("parse", "跳过不存在的场景脚本引用", new Dictionary<string, object?>
                {
                    ["tlorFile"] = tlorFilePath,
                    ["sceneId"] = lorFileName,
                    ["resolvedPath"] = lorFilePath
                });
            }
        }

        return result;
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
    /// 在当前项目中新建场景。
    /// </summary>
    [HttpPost("scenes")]
    public async Task<IActionResult> CreateScene([FromBody] ProjectCreateSceneRequest request)
    {
        if (!TryValidateSceneId(request.SceneId, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        try
        {
            var editor = _sessionService.GetEditor();
            await editor.CreateSceneAsync(request.SceneId);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新建场景失败: {SceneId}", request.SceneId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 重命名当前项目中的场景。
    /// </summary>
    [HttpPut("scenes/{sceneId}/rename")]
    public async Task<IActionResult> RenameScene(string sceneId, [FromBody] ProjectRenameSceneRequest request)
    {
        if (!TryValidateSceneId(sceneId, out var oldError))
        {
            return BadRequest(new { success = false, error = oldError });
        }

        if (!TryValidateSceneId(request.NewSceneId, out var newError))
        {
            return BadRequest(new { success = false, error = newError });
        }

        try
        {
            var editor = _sessionService.GetEditor();
            await editor.RenameSceneAsync(sceneId, request.NewSceneId);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重命名场景失败: {OldSceneId} -> {NewSceneId}", sceneId, request.NewSceneId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 删除当前项目中的场景。
    /// </summary>
    [HttpDelete("scenes/{sceneId}")]
    public async Task<IActionResult> DeleteScene(string sceneId)
    {
        if (!TryValidateSceneId(sceneId, out var error))
        {
            return BadRequest(new { success = false, error });
        }

        try
        {
            var editor = _sessionService.GetEditor();
            await editor.DeleteSceneAsync(sceneId);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除场景失败: {SceneId}", sceneId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private static bool TryValidateSceneId(string? sceneId, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(sceneId))
        {
            error = "场景名称不能为空";
            return false;
        }

        var trimmed = sceneId.Trim();
        if (!string.Equals(trimmed, Path.GetFileName(trimmed), StringComparison.Ordinal) || Path.GetInvalidFileNameChars().Any(trimmed.Contains))
        {
            error = "场景名称不能包含路径分隔符或非法文件名字符";
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[\p{L}\p{N}_\-\s]+$"))
        {
            error = "场景名称只能包含文字、数字、空格、下划线和连字符";
            return false;
        }

        return true;
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
    public string? RatingSystem { get; set; }
    public string? RatingValue { get; set; }
}

/// <summary>
/// 导入项目请求
/// </summary>
public class ImportProjectRequest
{
    public string ProjectPath { get; set; } = string.Empty;
}

/// <summary>
/// 新建场景请求。
/// </summary>
public class ProjectCreateSceneRequest
{
    public string SceneId { get; set; } = string.Empty;
}

/// <summary>
/// 重命名场景请求。
/// </summary>
public class ProjectRenameSceneRequest
{
    public string NewSceneId { get; set; } = string.Empty;
}

/// <summary>
/// 重命名资产请求
/// </summary>
public class RenameAssetRequest
{
    public string Path { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
}

/// <summary>
/// 项目角色附加配置。
/// </summary>
public class CharacterConfigResponse
{
    public List<CharacterConfigEntry> Characters { get; set; } = new();
}

/// <summary>
/// 单个角色的可编辑附加配置。角色 ID/名称由 Assets/Characters 下的文件夹名称决定。
/// </summary>
public class CharacterConfigEntry
{
    public string Id { get; set; } = string.Empty;
    public string DisplayId { get; set; } = string.Empty;
    public string Affiliation { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public List<string> Sprites { get; set; } = new();
    public string Note { get; set; } = string.Empty;
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
    public string RatingSystem { get; set; } = "CADPA";
    public string RatingValue { get; set; } = "12+";
    public string ProjectPath { get; set; } = string.Empty;
    public List<string> SubDirectories { get; set; } = new();
}

/// <summary>
/// 最近打开的项目信息。
/// </summary>
public class RecentProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? LastOpened { get; set; }
}
