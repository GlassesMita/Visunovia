using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Visunovia.Services;

namespace Visunovia.Controllers;

/// <summary>
/// 资源管理 API，处理项目资源的查询和文件服务
/// </summary>
[ApiController]
[Route("api/resources")]
public class ResourceController : ControllerBase
{
    private readonly EditorSessionService _sessionService;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "sprites", "backgrounds", "bgm", "voice", "sfx"
    };

    public ResourceController(EditorSessionService sessionService)
    {
        _sessionService = sessionService;
        _contentTypeProvider = new FileExtensionContentTypeProvider();
    }

    /// <summary>
    /// 获取当前项目的所有资源列表，按类别分组
    /// </summary>
    [HttpGet]
    public IActionResult GetAllResources()
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var resources = editor.GetResources();
            return Ok(new
            {
                sprites = resources.GetValueOrDefault("sprites", new List<string>()),
                backgrounds = resources.GetValueOrDefault("backgrounds", new List<string>()),
                bgm = resources.GetValueOrDefault("bgm", new List<string>()),
                voice = resources.GetValueOrDefault("voice", new List<string>()),
                sfx = resources.GetValueOrDefault("sfx", new List<string>())
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"获取资源列表失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取指定类别的资源列表
    /// </summary>
    /// <param name="category">资源类别：sprites/backgrounds/bgm/voice/sfx</param>
    [HttpGet("{category}")]
    public IActionResult GetResourcesByCategory(string category)
    {
        try
        {
            if (!ValidCategories.Contains(category))
                return BadRequest(new { error = $"无效的资源类别: {category}，有效值为: sprites, backgrounds, bgm, voice, sfx" });

            var editor = _sessionService.GetEditor();
            var resources = editor.GetResources();
            var items = resources.GetValueOrDefault(category, new List<string>());
            return Ok(items);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"获取资源列表失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 提供项目资源文件的访问服务，路径相对于项目根目录
    /// </summary>
    /// <param name="path">相对于项目根目录的文件路径（catch-all 路由参数）</param>
    [HttpGet("file/{**path}")]
    public IActionResult GetResourceFile(string path)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (string.IsNullOrEmpty(editor.CurrentProjectPath))
                return BadRequest(new { error = "没有打开的项目" });

            var projectRoot = Path.GetDirectoryName(editor.CurrentProjectPath) ?? "";
            if (string.IsNullOrEmpty(projectRoot))
                return BadRequest(new { error = "无法确定项目根目录" });

            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)));

            if (!fullPath.StartsWith(Path.GetFullPath(projectRoot), StringComparison.OrdinalIgnoreCase))
                return Forbid("不允许访问项目目录之外的文件");

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { error = "文件不存在" });

            if (!_contentTypeProvider.TryGetContentType(fullPath, out var contentType))
                contentType = "application/octet-stream";

            return PhysicalFile(fullPath, contentType);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"获取资源文件失败: {ex.Message}" });
        }
    }
}
