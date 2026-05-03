using Microsoft.AspNetCore.Mvc;

namespace Visunovia.Controllers;

/// <summary>
/// 文件浏览器 API，提供服务端文件系统浏览功能
/// </summary>
[ApiController]
[Route("api/files")]
public class FileBrowserController : ControllerBase
{
    private readonly Services.EditorSessionService _sessionService;

    public FileBrowserController(Services.EditorSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// 获取用户的文档文件夹路径
    /// </summary>
    [HttpGet("roots")]
    public IActionResult GetRoots()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Ok(new
        {
            documents = documentsPath,
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        });
    }

    /// <summary>
    /// 浏览指定目录
    /// </summary>
    /// <param name="path">要浏览的目录路径</param>
    [HttpGet("browse")]
    public IActionResult BrowseDirectory([FromQuery] string? path = null)
    {
        try
        {
            string targetPath;

            if (string.IsNullOrEmpty(path))
            {
                targetPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                targetPath = Uri.UnescapeDataString(path);
                targetPath = targetPath.Replace("\\", "/");
                if (!System.IO.Directory.Exists(targetPath))
                {
                    if (targetPath.Contains("/"))
                    {
                        var altPath = string.Join("\\", targetPath.Split('/'));
                        if (System.IO.Directory.Exists(altPath))
                        {
                            targetPath = altPath;
                        }
                        else
                        {
                            return BadRequest(new { error = "目录不存在: " + targetPath });
                        }
                    }
                    else
                    {
                        return BadRequest(new { error = "目录不存在: " + targetPath });
                    }
                }
            }

            var entries = new List<object>();
            var dirs = System.IO.Directory.GetDirectories(targetPath);
            foreach (var dir in dirs.OrderBy(d => d))
            {
                var name = System.IO.Path.GetFileName(dir);
                if (name.StartsWith(".")) continue;
                entries.Add(new { type = "directory", name, path = dir });
            }

            var files = System.IO.Directory.GetFiles(targetPath);
            foreach (var file in files.OrderBy(f => f))
            {
                var name = System.IO.Path.GetFileName(file);
                if (name.StartsWith(".")) continue;
                var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                var size = new System.IO.FileInfo(file).Length;
                entries.Add(new { type = "file", name, path = file, extension = ext, size });
            }

            return Ok(new
            {
                path = targetPath,
                parent = System.IO.Directory.GetParent(targetPath)?.FullName,
                entries
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"浏览目录失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 读取文本文件内容
    /// </summary>
    [HttpGet("read")]
    public IActionResult ReadFile([FromQuery] string? path = null)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return BadRequest(new { error = "未指定文件路径" });

            var filePath = Uri.UnescapeDataString(path);
            if (!System.IO.File.Exists(filePath))
                return NotFound(new { error = "文件不存在" });

            var content = System.IO.File.ReadAllText(filePath);
            return Ok(new { path = filePath, content });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"读取文件失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 打开指定路径的项目（tlor 文件或包含 tlor 文件的目录）
    /// </summary>
    [HttpPost("openProject")]
    public async Task<IActionResult> OpenProject([FromBody] OpenProjectRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Path))
                return BadRequest(new { error = "未指定项目路径" });

            var projectPath = Uri.UnescapeDataString(request.Path);
            projectPath = projectPath.Replace("\\", "/");
            if (projectPath.Contains("/"))
            {
                var altPath = string.Join("\\", projectPath.Split('/'));
                if (System.IO.File.Exists(altPath) || System.IO.Directory.Exists(altPath))
                {
                    projectPath = altPath;
                }
            }
            Console.WriteLine($"[FileBrowser] OpenProject: {projectPath}");

            string tlorPath;
            string projectRoot;

            if (System.IO.File.Exists(projectPath) && projectPath.EndsWith(".tlor", StringComparison.OrdinalIgnoreCase))
            {
                tlorPath = projectPath;
                projectRoot = System.IO.Path.GetDirectoryName(projectPath) ?? projectPath;
            }
            else if (System.IO.Directory.Exists(projectPath))
            {
                var tlorFiles = System.IO.Directory.GetFiles(projectPath, "*.tlor", System.IO.SearchOption.TopDirectoryOnly);
                if (tlorFiles.Length == 0)
                    return BadRequest(new { error = "目录中没有找到 .tlor 项目文件" });
                tlorPath = tlorFiles[0];
                projectRoot = projectPath;
            }
            else
            {
                return BadRequest(new { error = "指定的路径不存在: " + projectPath });
            }

            Console.WriteLine($"[FileBrowser] tlorPath: {tlorPath}, projectRoot: {projectRoot}");

            _sessionService.ResetEditor();
            var editor = _sessionService.GetEditor();
            var success = await editor.LoadProjectAsync(tlorPath, projectRoot);

            if (!success)
                return BadRequest(new { error = "无法加载项目文件" });

            editor.CurrentProjectPath = tlorPath;

            var xml = await editor.ExportProjectToXmlAsync();
            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"打开项目失败: {ex.Message}" });
        }
    }

    public class OpenProjectRequest
    {
        public string? Path { get; set; }
    }
}
