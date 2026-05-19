using Microsoft.AspNetCore.Mvc;

namespace Visunovia.Controllers;

/// <summary>
/// 文件浏览器 API 控制器，提供文件系统浏览、驱动器查询和目录管理功能。
/// 用于前端文件浏览器组件的后端数据支撑，
/// 包括驱动器列表、目录内容枚举、文件夹创建和特殊路径获取。
///
/// API 端点概览：
/// - GET /api/file-browser/drives - 获取系统所有可用驱动器列表
/// - GET /api/file-browser/entries?path={path} - 获取指定目录的文件和文件夹列表
/// - POST /api/file-browser/create-folder - 在指定路径创建新文件夹
/// - GET /api/file-browser/special-folders - 获取常用特殊文件夹路径（桌面、文档等）
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class FileBrowserController : ControllerBase
{
    /// <summary>
    /// 获取系统所有可用驱动器列表（Windows 平台）
    /// 使用 System.IO.DriveInfo.GetDrives() 枚举系统中已挂载的逻辑驱动器，
    /// 返回每个驱动器的盘符、卷标、总容量和剩余空间等信息。
    /// 仅返回就绪状态（IsReady == true）的驱动器。
    /// </summary>
    /// <returns>包含驱动器信息数组的标准化 JSON 响应</returns>
    /// <response code="200">成功获取驱动器列表</response>
    [HttpGet("drives")]
    public IActionResult GetDrives()
    {
        try
        {
            var drives = new List<object>();

            if (OperatingSystem.IsWindows())
            {
                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    // 跳过未就绪的驱动器（如空光驱、未插入的 U 盘）
                    if (!drive.IsReady)
                        continue;

                    try
                    {
                        var letter = drive.Name.TrimEnd(System.IO.Path.DirectorySeparatorChar);
                        var totalSpace = drive.TotalSize;
                        var freeSpace = drive.TotalFreeSpace;
                        var volumeLabel = string.IsNullOrEmpty(drive.VolumeLabel)
                            ? "本地磁盘"
                            : drive.VolumeLabel;

                        drives.Add(new
                        {
                            letter,
                            name = volumeLabel,
                            totalSpace,
                            freeSpace
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 异常来源：访问驱动器元数据时权限不足（如受保护的系统分区）
                        // 处理方式：跳过该驱动器，不中断整体枚举流程
                        continue;
                    }
                    catch (Exception)
                    {
                        // 异常来源：读取驱动器信息时发生意外错误（如设备 I/O 故障）
                        // 处理方式：静默跳过，避免单个驱动器异常影响整个列表
                        continue;
                    }
                }
            }

            return Ok(new { success = true, data = drives });
        }
        catch (Exception ex)
        {
            // 异常来源：DriveInfo.GetDrives() 调用失败或结果序列化异常
            // 处理方式：返回 500 错误并附带具体原因
            return StatusCode(500, new { success = false, error = $"获取驱动器列表失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取指定目录下的文件和文件夹列表。
    /// 对目标路径执行安全校验以防止路径遍历攻击，
    /// 返回按类型排序的条目（文件夹优先，然后按名称字母排序）。
    /// 每个条目包含名称、类型标识、大小、修改时间和扩展名等信息。
    /// </summary>
    /// <param name="path">URL 编码的绝对路径（需经过 URL 编码）</param>
    /// <returns>包含当前路径、父路径和条目列表的标准化 JSON 响应</returns>
    /// <response code="200">成功获取目录内容</response>
    /// <response code="400">请求参数无效（路径为空或包含非法字符）</response>
    /// <response code="403">权限不足，无法访问目标目录</response>
    /// <response code="404">指定路径不存在</response>
    [HttpGet("entries")]
    public IActionResult GetEntries([FromQuery] string? path)
    {
        try
        {
            // 参数验证：检查路径是否为空
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest(new { success = false, error = "路径参数不能为空" });

            // URL 解码并规范化路径分隔符
            var targetPath = Uri.UnescapeDataString(path).Trim();

            // 安全校验：防止路径遍历攻击
            // 检测 ".." 序列和非法路径字符，阻止恶意构造的相对路径逃逸
            if (targetPath.Contains("..") || targetPath.Contains("|"))
                return BadRequest(new { success = false, error = "路径包含非法字符" });

            // 规范化路径：统一使用操作系统原生的目录分隔符
            targetPath = System.IO.Path.GetFullPath(targetPath);

            // 验证路径是否存在且为目录
            if (!System.IO.Directory.Exists(targetPath))
                return NotFound(new { success = false, error = $"路径不存在: {targetPath}" });

            // 获取父目录路径（用于前端"返回上一级"导航）
            var parentInfo = System.IO.Directory.GetParent(targetPath);
            var parentPath = parentInfo?.FullName;

            var entries = new List<object>();
            var hasAccessDenied = false;

            // 单独捕获目录枚举的权限异常，避免整个请求因部分条目无权访问而失败
            try
            {
                var directories = System.IO.Directory.GetDirectories(targetPath);
                foreach (var dir in directories.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    var dirInfo = new System.IO.DirectoryInfo(dir);
                    entries.Add(new
                    {
                        name = dirInfo.Name,
                        isDirectory = true,
                        size = 0L,
                        lastModified = dirInfo.LastWriteTime.ToString("o"),
                        extension = ""
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 异常来源：枚举子目录时部分目录权限不足（如系统受保护文件夹）
                // 处理方式：标记权限异常标志，继续尝试枚举文件，最终在响应中告知前端
                hasAccessDenied = true;
            }

            // 单独捕获文件枚举的权限异常
            try
            {
                var files = System.IO.Directory.GetFiles(targetPath);
                foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var fileInfo = new System.IO.FileInfo(file);
                    entries.Add(new
                    {
                        name = fileInfo.Name,
                        isDirectory = false,
                        size = fileInfo.Length,
                        lastModified = fileInfo.LastWriteTime.ToString("o"),
                        extension = fileInfo.Extension.ToLowerInvariant()
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 异常来源：枚举文件时部分文件权限不足（如系统锁定文件）
                // 处理方式：标记权限异常标志，与目录枚举的结果合并后返回
                hasAccessDenied = true;
            }

            // 如果目录和文件的枚举均因权限问题失败，返回 403 错误
            if (hasAccessDenied && entries.Count == 0)
                return StatusCode(403, new { success = false, error = "无权访问该目录" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    currentPath = targetPath,
                    parentPath = (object?)parentPath ?? "",
                    entries
                }
            });
        }
        catch (ArgumentException ex)
        {
            // 异常来源：路径参数包含非法字符（由 Path.GetFullPath 或 Directory.Exists 抛出）
            // 处理方式：返回 400 错误并提示正确的路径格式要求
            return BadRequest(new { success = false, error = $"路径格式无效: {ex.Message}" });
        }
        catch (Exception ex)
        {
            // 异常来源：意外的异常情况（如文件系统 I/O 错误、路径过长等）
            // 处理方式：返回 500 错误并记录详细信息用于排查
            return StatusCode(500, new { success = false, error = $"读取目录内容失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 在指定父目录下创建新文件夹。
    /// 对请求参数进行安全校验（路径遍历检测、非法字符过滤），
    /// 验证父目录存在性后在目标位置创建子目录。
    /// 如果同名文件夹已存在则返回错误提示。
    /// </summary>
    /// <param name="request">创建文件夹请求体，包含父目录路径和新文件夹名称</param>
    /// <returns>创建结果的标准化 JSON 响应，包含新文件夹的完整路径</returns>
    /// <response code="200">文件夹创建成功</response>
    /// <response code="400">请求参数无效（路径为空、名称为空或包含非法字符）</response>
    /// <response code="403">权限不足，无法在目标位置创建文件夹</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("create-folder")]
    public IActionResult CreateFolder([FromBody] CreateFolderRequest request)
    {
        try
        {
            // 参数验证：检查请求体是否有效
            if (request == null || string.IsNullOrWhiteSpace(request.ParentPath))
                return BadRequest(new { success = false, error = "父目录路径不能为空" });

            if (string.IsNullOrWhiteSpace(request.FolderName))
                return BadRequest(new { success = false, error = "文件夹名称不能为空" });

            // 安全校验：检测路径遍历攻击字符和非法文件名字符
            var folderName = request.FolderName.Trim();
            if (folderName.Contains("..") || folderName.Contains('/') || folderName.Contains('\\')
                || folderName.Contains(':') || folderName.Contains('*') || folderName.Contains('?')
                || folderName.Contains('"') || folderName.Contains('<') || folderName.Contains('>')
                || folderName.Contains('|'))
                return BadRequest(new { success = false, error = "文件夹名称包含非法字符" });

            // URL 解码并规范化父目录路径
            var parentPath = Uri.UnescapeDataString(request.ParentPath.Trim());
            parentPath = System.IO.Path.GetFullPath(parentPath);

            // 验证父目录是否存在
            if (!System.IO.Directory.Exists(parentPath))
                return BadRequest(new { success = false, error = $"父目录不存在: {parentPath}" });

            // 组装完整的目标路径
            var fullPath = System.IO.Path.Combine(parentPath, folderName);

            // 检查是否已存在同名文件夹
            if (System.IO.Directory.Exists(fullPath))
                return BadRequest(new { success = false, error = $"文件夹已存在: {folderName}" });

            // 执行目录创建操作
            System.IO.Directory.CreateDirectory(fullPath);

            return Ok(new
            {
                success = true,
                data = new { path = fullPath }
            });
        }
        catch (UnauthorizedAccessException)
        {
            // 异常来源：在目标位置创建目录时权限不足（如系统受保护目录或只读文件系统）
            // 处理方式：返回 403 错误并提示用户选择其他位置
            return StatusCode(403, new { success = false, error = "无权在该位置创建文件夹" });
        }
        catch (NotSupportedException ex)
        {
            // 异常来源：路径格式不受支持（如使用了 UNC 路径但格式不正确）
            // 处理方式：返回 400 错误并附带具体的格式说明
            return BadRequest(new { success = false, error = $"路径格式不受支持: {ex.Message}" });
        }
        catch (Exception ex)
        {
            // 异常来源：意外的异常情况（如磁盘空间不足、文件系统错误等）
            // 处理方式：返回 500 错误并记录详细上下文信息用于排查
            return StatusCode(500, new { success = false, error = $"创建文件夹失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取常用特殊文件夹路径（桌面、文档、图片、用户主目录）。
    /// 使用 Environment.GetFolderPath() 查询 Windows 系统注册的特殊文件夹位置，
    /// 为前端提供快捷入口，方便用户快速导航到常用目录。
    /// </summary>
    /// <returns>包含特殊文件夹信息数组的标准化 JSON 响应</returns>
    /// <response code="200">成功获取特殊文件夹列表</response>
    [HttpGet("special-folders")]
    public IActionResult GetSpecialFolders()
    {
        try
        {
            var specialFolders = new[]
            {
                new
                {
                    id = "desktop",
                    name = "桌面",
                    path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                },
                new
                {
                    id = "documents",
                    name = "文档",
                    path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                },
                new
                {
                    id = "pictures",
                    name = "图片",
                    path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                },
                new
                {
                    id = "userProfile",
                    name = "用户主目录",
                    path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                }
            };

            return Ok(new { success = true, data = specialFolders });
        }
        catch (Exception ex)
        {
            // 异常来源：Environment.GetFolderPath() 调用失败（通常不应发生，除非环境配置严重损坏）
            // 处理方式：返回 500 错误并记录详细信息
            return StatusCode(500, new { success = false, error = $"获取特殊文件夹失败: {ex.Message}" });
        }
    }

    #region 请求模型

    /// <summary>
    /// 创建文件夹请求模型
    /// 包含目标父目录路径和新建文件夹的名称
    /// </summary>
    public class CreateFolderRequest
    {
        /// <summary>
        /// 父目录的绝对路径（需 URL 编码）
        /// </summary>
        public string? ParentPath { get; set; }

        /// <summary>
        /// 新建文件夹的名称（不含路径分隔符）
        /// </summary>
        public string? FolderName { get; set; }
    }

    #endregion
}
