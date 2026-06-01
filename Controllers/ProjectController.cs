using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Visunovia.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(ILogger<ProjectController> logger)
    {
        _logger = logger;
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
