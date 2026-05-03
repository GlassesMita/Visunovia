using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using Visunovia.Models.Engine;
using Visunovia.Services;

namespace Visunovia.Controllers;

/// <summary>
/// 项目管理 API，处理项目的创建、打开、保存及状态查询
/// </summary>
[ApiController]
[Route("api/project")]
public class ProjectController : ControllerBase
{
    private readonly EditorSessionService _sessionService;

    public ProjectController(EditorSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// 创建新项目
    /// </summary>
    /// <param name="request">包含项目名称和保存路径的请求体</param>
    [HttpPost("new")]
    public async Task<IActionResult> NewProject([FromBody] NewProjectRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "项目名称不能为空" });

            _sessionService.ResetEditor();
            var editor = _sessionService.GetEditor();
            editor.NewProject(request.Name, request.Path);
            var xml = await editor.ExportProjectToXmlAsync();
            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"创建项目失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 从上传的 .tlor 文件打开项目
    /// </summary>
    /// <param name="uploadedFile">上传的 .tlor 项目文件</param>
    /// <param name="projectPath">可选：项目目录路径（用于直接访问项目文件而非临时目录）</param>
    [HttpPost("open")]
    public async Task<IActionResult> OpenProject(IFormFile uploadedFile, [FromQuery] string? projectPath = null)
    {
        try
        {
            if (uploadedFile == null || uploadedFile.Length == 0)
                return BadRequest(new { error = "未提供文件" });

            string tempPath;
            string effectiveProjectRoot;

            if (!string.IsNullOrEmpty(projectPath) && System.IO.Directory.Exists(projectPath))
            {
                var tlorPath = System.IO.Path.Combine(projectPath, "Project.tlor");
                if (System.IO.File.Exists(tlorPath))
                {
                    tempPath = tlorPath;
                    effectiveProjectRoot = projectPath;
                }
                else
                {
                    return BadRequest(new { error = "项目路径中未找到 Project.tlor 文件" });
                }
            }
            else
            {
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Visunovia", Guid.NewGuid().ToString());
                System.IO.Directory.CreateDirectory(tempDir);
                tempPath = System.IO.Path.Combine(tempDir, uploadedFile.FileName);
                effectiveProjectRoot = tempDir;

                using (var stream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                {
                    await uploadedFile.CopyToAsync(stream);
                }
            }

            _sessionService.ResetEditor();
            var editor = _sessionService.GetEditor();
            var success = await editor.LoadProjectAsync(tempPath, effectiveProjectRoot);

            if (!success)
                return BadRequest(new { error = "无法加载项目文件" });

            var xml = await editor.ExportProjectToXmlAsync();
            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"打开项目失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 保存当前项目并返回 .tlor 文件下载
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> SaveProject()
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            using var reader = new StreamReader(Request.Body);
            var xml = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(xml))
            {
                return BadRequest(new { error = "未提供 XML 数据" });
            }

            var success = await editor.SaveProjectFromXmlAsync(xml);
            if (!success)
                return BadRequest(new { error = "保存项目失败" });

            return Ok(new { message = "项目已保存" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"保存项目失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取当前项目状态
    /// </summary>
    /// <param name="activeSceneIndex">当前激活的场景索引（可选）</param>
    /// <param name="selectedDialogueIndex">当前选中的对话索引（可选）</param>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentProject([FromQuery] int? activeSceneIndex, [FromQuery] int? selectedDialogueIndex)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return NotFound(new { error = "没有打开的项目" });

            var xml = await editor.ExportProjectToXmlAsync();
            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"获取项目状态失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 上传 .tlor 项目文件并解析返回项目 JSON
    /// </summary>
    /// <param name="uploadedFile">上传的 .tlor 项目文件</param>
    /// <param name="projectPath">可选：项目目录路径（用于直接访问项目文件而非临时目录）</param>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadProject(IFormFile uploadedFile, [FromQuery] string? projectPath = null)
    {
        try
        {
            if (uploadedFile == null || uploadedFile.Length == 0)
                return BadRequest(new { error = "未提供文件" });

            string tempPath;
            string effectiveProjectRoot;

            if (!string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine($"[DEBUG] UploadProject: projectPath={projectPath}");
                Console.WriteLine($"[DEBUG] Directory.Exists={System.IO.Directory.Exists(projectPath)}");
                Console.WriteLine($"[DEBUG] IsPathRooted={System.IO.Path.IsPathRooted(projectPath)}");

                if (System.IO.Directory.Exists(projectPath))
                {
                    var tlorPath = System.IO.Path.Combine(projectPath, "Project.tlor");
                    if (System.IO.File.Exists(tlorPath))
                    {
                        tempPath = tlorPath;
                        effectiveProjectRoot = projectPath;
                        Console.WriteLine($"[DEBUG] 使用原始项目路径: {projectPath}");
                    }
                    else
                    {
                        return BadRequest(new { error = "项目路径中未找到 Project.tlor 文件" });
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] 项目路径不存在或无效，使用临时目录");
                    var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Visunovia", Guid.NewGuid().ToString());
                    System.IO.Directory.CreateDirectory(tempDir);
                    tempPath = System.IO.Path.Combine(tempDir, uploadedFile.FileName);
                    effectiveProjectRoot = tempDir;

                    using (var stream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                    {
                        await uploadedFile.CopyToAsync(stream);
                    }
                }
            }
            else
            {
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Visunovia", Guid.NewGuid().ToString());
                System.IO.Directory.CreateDirectory(tempDir);
                tempPath = System.IO.Path.Combine(tempDir, uploadedFile.FileName);
                effectiveProjectRoot = tempDir;

                using (var stream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                {
                    await uploadedFile.CopyToAsync(stream);
                }
            }

            _sessionService.ResetEditor();
            var editor = _sessionService.GetEditor();
            var success = await editor.LoadProjectAsync(tempPath, effectiveProjectRoot);

            if (!success)
                return BadRequest(new { error = "无法解析项目文件" });

            var xml = await editor.ExportProjectToXmlAsync();
            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"上传项目失败: {ex.Message}" });
        }
    }
}

/// <summary>
/// 创建新项目的请求体
/// </summary>
/// <param name="Name">项目名称</param>
/// <param name="Path">项目保存路径</param>
public record NewProjectRequest(string Name, string Path);
