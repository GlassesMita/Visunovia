using Microsoft.AspNetCore.Mvc;
using Visunovia.Models.Engine;
using Visunovia.Services;
using Visunovia.Services.Configuration;

namespace Visunovia.Controllers;

/// <summary>
/// YAML ↔ Blueprint 双向转换 API 控制器。
/// 提供蓝图导出为 YAML、YAML 导入为蓝图、UUID 注册表查询等功能。
/// </summary>
[ApiController]
[Route("api/yaml")]
public class YamlConversionController : ControllerBase
{
    private readonly EditorSessionService _sessionService;
    private readonly ILogger<YamlConversionController> _logger;

    public YamlConversionController(
        EditorSessionService sessionService,
        ILogger<YamlConversionController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    // ==================== 导出：Blueprint → YAML ====================

    /// <summary>
    /// 将指定场景的蓝图导出为 YAML 格式。
    /// 为所有节点、连线分配 UUID，写入数据库，并返回 YAML 内容。
    /// </summary>
    [HttpGet("export/{sceneId}")]
    public async Task<IActionResult> ExportYaml(
        string sceneId,
        [FromQuery] string displayName = "",
        [FromQuery] string description = "",
        [FromQuery] string author = "")
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);
            var converter = new BlueprintYamlConverter(editor);

            var yamlContent = await converter.ExportYamlAsync(
                sceneId, uuidService, displayName, description, author);

            _logger.LogInformation("场景 {SceneId} 已导出为 YAML", sceneId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    sceneId,
                    yamlContent,
                    exportedAt = DateTime.UtcNow.ToString("O")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 YAML 失败: {SceneId}", sceneId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 将指定场景的蓝图导出为 YAML 文件并下载。
    /// </summary>
    [HttpGet("download/{sceneId}")]
    public async Task<IActionResult> DownloadYaml(
        string sceneId,
        [FromQuery] string displayName = "",
        [FromQuery] string description = "",
        [FromQuery] string author = "")
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);
            var converter = new BlueprintYamlConverter(editor);

            var yamlContent = await converter.ExportYamlAsync(
                sceneId, uuidService, displayName, description, author);

            var fileName = $"{sceneId}.yaml";
            return File(
                System.Text.Encoding.UTF8.GetBytes(yamlContent),
                "application/x-yaml",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载 YAML 失败: {SceneId}", sceneId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ==================== 导入：YAML → Blueprint ====================

    /// <summary>
    /// 从 YAML 内容导入蓝图到指定场景。
    /// 解析 UUID 注册表，重建节点和连线关系。
    /// </summary>
    [HttpPost("import/{sceneId}")]
    public async Task<IActionResult> ImportYaml(
        string sceneId,
        [FromBody] YamlImportRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);
            var converter = new BlueprintYamlConverter(editor);

            // 如果指定了清除现有数据
            if (request.ClearExisting)
            {
                uuidService.ClearSceneData(sceneId);
            }

            var sceneGraph = await converter.ImportYamlAsync(request.YamlContent, uuidService, sceneId);

            _logger.LogInformation("场景 {SceneId} 已从 YAML 导入，节点数: {NodeCount}, 连线数: {EdgeCount}",
                sceneId, sceneGraph.Nodes?.Count ?? 0, sceneGraph.Edges?.Count ?? 0);

            return Ok(new
            {
                success = true,
                data = new
                {
                    sceneId,
                    nodeCount = sceneGraph.Nodes?.Count ?? 0,
                    edgeCount = sceneGraph.Edges?.Count ?? 0,
                    importedAt = DateTime.UtcNow.ToString("O")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入 YAML 失败: {SceneId}", sceneId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 上传 YAML 文件并导入为蓝图。
    /// </summary>
    [HttpPost("upload/{sceneId}")]
    public async Task<IActionResult> UploadYaml(string sceneId, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, error = "未选择文件" });
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var yamlContent = await reader.ReadToEndAsync();

            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);
            var converter = new BlueprintYamlConverter(editor);

            var sceneGraph = await converter.ImportYamlAsync(yamlContent, uuidService, sceneId);

            _logger.LogInformation("场景 {SceneId} 已从文件 {FileName} 导入", sceneId, file.FileName);

            return Ok(new
            {
                success = true,
                data = new
                {
                    sceneId,
                    fileName = file.FileName,
                    nodeCount = sceneGraph.Nodes?.Count ?? 0,
                    edgeCount = sceneGraph.Edges?.Count ?? 0,
                    importedAt = DateTime.UtcNow.ToString("O")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传 YAML 失败: {SceneId}", sceneId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ==================== UUID 注册表查询 ====================

    /// <summary>
    /// 获取项目的完整 UUID 注册表。
    /// </summary>
    [HttpGet("uuid-registry")]
    public IActionResult GetUuidRegistry()
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);

            var entries = uuidService.GetAllEntities().ToList();

            return Ok(new
            {
                success = true,
                data = entries.Select(e => new
                {
                    e.Uuid,
                    e.EntityType,
                    e.Name,
                    e.DisplayName,
                    e.CreatedAt,
                    e.UpdatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 UUID 注册表失败");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 按类型获取 UUID 注册表条目。
    /// </summary>
    [HttpGet("uuid-registry/{entityType}")]
    public IActionResult GetUuidRegistryByType(string entityType)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);

            var entries = uuidService.GetEntitiesByType(entityType).ToList();

            return Ok(new
            {
                success = true,
                data = entries.Select(e => new
                {
                    e.Uuid,
                    e.EntityType,
                    e.Name,
                    e.DisplayName,
                    e.CreatedAt,
                    e.UpdatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 UUID 注册表失败: {EntityType}", entityType);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取指定 UUID 的实体详细信息。
    /// </summary>
    [HttpGet("uuid-registry/detail/{uuid}")]
    public IActionResult GetUuidDetail(string uuid)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);

            var entry = uuidService.GetEntity(uuid);
            if (entry == null)
            {
                return NotFound(new { success = false, error = $"UUID {uuid} 不存在" });
            }

            object? detail = entry.EntityType switch
            {
                "Node" => uuidService.GetNode(uuid),
                "Resource" => uuidService.GetResource(uuid),
                "Edge" => uuidService.GetAllEdges().FirstOrDefault(e => e.Uuid == uuid),
                "Scene" => uuidService.GetAllScenes().FirstOrDefault(s => s.Uuid == uuid),
                _ => null
            };

            return Ok(new
            {
                success = true,
                data = new
                {
                    entry.Uuid,
                    entry.EntityType,
                    entry.Name,
                    entry.DisplayName,
                    entry.CreatedAt,
                    entry.UpdatedAt,
                    detail
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 UUID 详情失败: {Uuid}", uuid);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ==================== YAML 快照 ====================

    /// <summary>
    /// 获取场景的最新 YAML 快照。
    /// </summary>
    [HttpGet("snapshot/{sceneId}")]
    public IActionResult GetYamlSnapshot(string sceneId)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var projectPath = editor.CurrentProjectPath;

            if (string.IsNullOrEmpty(projectPath))
            {
                return BadRequest(new { success = false, error = "未打开项目" });
            }

            using var db = new UuidRegistryDbContext(projectPath);
            var uuidService = new UuidRegistryService(db);

            var snapshot = uuidService.GetLatestYamlSnapshot(sceneId);
            if (snapshot == null)
            {
                return NotFound(new { success = false, error = $"场景 {sceneId} 没有 YAML 快照" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    sceneId,
                    yamlContent = snapshot
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 YAML 快照失败: {SceneId}", sceneId);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ==================== 验证 ====================

    /// <summary>
    /// 验证 YAML 格式是否有效，不实际导入。
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateYaml([FromBody] YamlImportRequest request)
    {
        try
        {
            var deserializer = YamlSerializerFactory.CreateDeserializer();
            var document = deserializer.Deserialize<YamlSceneDocument>(request.YamlContent);

            if (document == null)
            {
                return Ok(new
                {
                    success = false,
                    valid = false,
                    errors = new[] { "YAML 解析失败：文档为空" }
                });
            }

            var errors = new List<string>();
            var warnings = new List<string>();

            // 验证版本
            if (string.IsNullOrEmpty(document.Version))
            {
                warnings.Add("缺少版本信息");
            }

            // 验证元数据
            if (document.Metadata == null || string.IsNullOrEmpty(document.Metadata.SceneId))
            {
                errors.Add("缺少场景 ID (metadata.scene_id)");
            }

            // 验证 UUID 注册表
            if (document.UuidRegistry == null || document.UuidRegistry.Count == 0)
            {
                warnings.Add("UUID 注册表为空");
            }
            else
            {
                // 检查 UUID 唯一性
                var duplicateUuids = document.UuidRegistry
                    .GroupBy(e => e.Uuid)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateUuids.Any())
                {
                    errors.Add($"发现重复 UUID: {string.Join(", ", duplicateUuids)}");
                }
            }

            // 验证节点引用
            if (document.Nodes != null)
            {
                var nodeUuids = document.Nodes.Select(n => n.Uuid).ToHashSet();
                foreach (var edge in document.Edges ?? new List<YamlEdgeEntry>())
                {
                    if (!nodeUuids.Contains(edge.SourceNodeUuid))
                    {
                        errors.Add($"连线 {edge.Uuid} 引用了不存在的源节点: {edge.SourceNodeUuid}");
                    }
                    if (!nodeUuids.Contains(edge.TargetNodeUuid))
                    {
                        errors.Add($"连线 {edge.Uuid} 引用了不存在的目标节点: {edge.TargetNodeUuid}");
                    }
                }
            }

            return Ok(new
            {
                success = true,
                valid = errors.Count == 0,
                errors,
                warnings,
                stats = new
                {
                    nodeCount = document.Nodes?.Count ?? 0,
                    edgeCount = document.Edges?.Count ?? 0,
                    resourceCount = document.Resources?.Count ?? 0,
                    uuidCount = document.UuidRegistry?.Count ?? 0
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                valid = false,
                errors = new[] { $"YAML 格式错误: {ex.Message}" }
            });
        }
    }
}

/// <summary>
/// YAML 导入请求
/// </summary>
public class YamlImportRequest
{
    public string YamlContent { get; set; } = "";
    public bool ClearExisting { get; set; } = true;
}
