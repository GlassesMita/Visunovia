using System.Text.Json;
using Visunovia.Models.Engine;
using Visunovia.Services.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Visunovia.Services;

/// <summary>
/// Blueprint ↔ YAML 双向转换服务。
/// 
/// 转换流程：
/// - Blueprint → YAML：从 EditorService 获取场景图数据，为每个节点/资源/连线分配 UUID，
///   序列化为 YAML 文件，同时写入 SQLite 数据库。
/// - YAML → Blueprint：解析 YAML 文件，从 UUID 注册表恢复实体关系，
///   重建场景图数据并加载到 EditorService。
/// 
/// UUID 机制：
/// - 每个节点、资源、连线、场景都有唯一 UUID
/// - UUID 表集中存储在 SQLite 数据库中
/// - YAML 文件内嵌 uuid_registry 表，实现自包含
/// - 导入时优先使用 YAML 中的 UUID，保持跨文件引用一致性
/// </summary>
public class BlueprintYamlConverter
{
    private readonly EditorService _editorService;
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;

    public BlueprintYamlConverter(EditorService editorService)
    {
        _editorService = editorService;
        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    // ==================== Blueprint → YAML ====================

    /// <summary>
    /// 将指定场景的蓝图转换为 YAML 文档。
    /// 为所有节点、资源、连线分配 UUID，并写入数据库。
    /// </summary>
    public async Task<YamlSceneDocument> BlueprintToYamlAsync(
        string sceneId,
        UuidRegistryService? uuidService = null,
        string displayName = "",
        string description = "",
        string author = "")
    {
        var sceneGraph = _editorService.GetSceneGraph(sceneId);
        if (sceneGraph == null)
        {
            throw new InvalidOperationException($"场景图 {sceneId} 不存在");
        }

        var document = new YamlSceneDocument
        {
            Version = "1.0.0",
            Metadata = new YamlSceneMetadata
            {
                SceneId = sceneId,
                DisplayName = string.IsNullOrEmpty(displayName) ? sceneId : displayName,
                Description = description,
                Author = author,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                UpdatedAt = DateTime.UtcNow.ToString("O")
            }
        };

        // 注册场景 UUID
        var sceneUuid = uuidService?.RegisterScene(sceneId, document.Metadata.DisplayName)
            ?? Guid.NewGuid().ToString();

        document.UuidRegistry.Add(new YamlUuidEntry
        {
            Uuid = sceneUuid,
            EntityType = "Scene",
            Name = sceneId,
            DisplayName = document.Metadata.DisplayName
        });

        // 转换节点
        if (sceneGraph.Nodes != null)
        {
            foreach (var node in sceneGraph.Nodes)
            {
                var nodeUuid = node.Id; // 使用现有 ID 作为 UUID
                var nodeType = node.Type ?? "Unknown";
                var subType = ExtractSubType(node);
                var nodeDisplayName = ExtractDisplayName(node, nodeType);

                // 注册到 UUID 注册表
                if (uuidService != null)
                {
                    uuidService.RegisterNodeWithUuid(nodeUuid, nodeType, subType, nodeDisplayName,
                        node.Position?.X ?? 0, node.Position?.Y ?? 0, node.Properties);
                }

                document.UuidRegistry.Add(new YamlUuidEntry
                {
                    Uuid = nodeUuid,
                    EntityType = "Node",
                    Name = nodeDisplayName,
                    DisplayName = nodeDisplayName
                });

                // 构建端口定义
                var inputs = new List<YamlPortDefinition>();
                var outputs = new List<YamlPortDefinition>();

                if (node.Inputs != null)
                {
                    foreach (var port in node.Inputs)
                    {
                        inputs.Add(new YamlPortDefinition
                        {
                            Name = port.Id,
                            Label = port.Label,
                            PortType = port.Type,
                            DataType = port.DataType
                        });
                    }
                }

                if (node.Outputs != null)
                {
                    foreach (var port in node.Outputs)
                    {
                        outputs.Add(new YamlPortDefinition
                        {
                            Name = port.Id,
                            Label = port.Label,
                            PortType = port.Type,
                            DataType = port.DataType
                        });
                    }
                }

                document.Nodes.Add(new YamlNodeEntry
                {
                    Uuid = nodeUuid,
                    NodeType = nodeType,
                    SubType = subType,
                    DisplayName = nodeDisplayName,
                    Position = new YamlPosition
                    {
                        X = node.Position?.X ?? 0,
                        Y = node.Position?.Y ?? 0
                    },
                    Properties = node.Properties ?? new Dictionary<string, object>(),
                    Inputs = inputs,
                    Outputs = outputs,
                    NextNodeUuids = node.NextNodeUuids ?? new List<string>()
                });
            }
        }

        // 转换连线
        if (sceneGraph.Edges != null)
        {
            foreach (var edge in sceneGraph.Edges)
            {
                var edgeUuid = edge.Id; // 使用现有 ID 作为 UUID

                if (uuidService != null)
                {
                    uuidService.RegisterEdgeWithUuid(edgeUuid, edge.Source, edge.SourcePort,
                        edge.Target, edge.TargetPort, edge.Type ?? "exec");
                }

                document.UuidRegistry.Add(new YamlUuidEntry
                {
                    Uuid = edgeUuid,
                    EntityType = "Edge",
                    Name = $"{edge.SourcePort}->{edge.TargetPort}",
                    DisplayName = $"{edge.Source}:{edge.SourcePort} → {edge.Target}:{edge.TargetPort}"
                });

                document.Edges.Add(new YamlEdgeEntry
                {
                    Uuid = edgeUuid,
                    SourceNodeUuid = edge.Source,
                    SourcePort = edge.SourcePort,
                    TargetNodeUuid = edge.Target,
                    TargetPort = edge.TargetPort,
                    EdgeType = edge.Type ?? "exec"
                });
            }
        }

        // 保存 YAML 快照到数据库
        if (uuidService != null)
        {
            var yamlContent = _yamlSerializer.Serialize(document);
            uuidService.SaveYamlSnapshot(sceneId, yamlContent, "Blueprint→YAML export");
        }

        return document;
    }

    /// <summary>
    /// 将蓝图转换为 YAML 字符串
    /// </summary>
    public async Task<string> ExportYamlAsync(
        string sceneId,
        UuidRegistryService? uuidService = null,
        string displayName = "",
        string description = "",
        string author = "")
    {
        var document = await BlueprintToYamlAsync(sceneId, uuidService, displayName, description, author);
        return _yamlSerializer.Serialize(document);
    }

    // ==================== YAML → Blueprint ====================

    /// <summary>
    /// 从 YAML 文档恢复蓝图到 EditorService。
    /// 解析 UUID 注册表，重建节点和连线关系。
    /// </summary>
    public async Task<SceneGraphData> YamlToBlueprintAsync(
        string yamlContent,
        UuidRegistryService? uuidService = null)
    {
        var document = _yamlDeserializer.Deserialize<YamlSceneDocument>(yamlContent);
        if (document == null)
        {
            throw new InvalidOperationException("YAML 解析失败：文档为空");
        }

        var sceneId = document.Metadata?.SceneId ?? Guid.NewGuid().ToString();

        // 注册场景
        if (uuidService != null && document.Metadata != null)
        {
            var sceneUuid = document.UuidRegistry
                .FirstOrDefault(e => e.EntityType == "Scene")?.Uuid
                ?? Guid.NewGuid().ToString();
            uuidService.RegisterSceneWithUuid(sceneUuid, sceneId, document.Metadata.DisplayName);
        }

        // 构建场景图数据
        var sceneGraph = new SceneGraphData
        {
            Id = sceneId,
            Nodes = new List<NodeData>(),
            Edges = new List<EdgeData>()
        };

        // 注册所有 UUID 条目
        if (uuidService != null && document.UuidRegistry != null)
        {
            foreach (var entry in document.UuidRegistry)
            {
                uuidService.RegisterEntityWithUuid(entry.Uuid, entry.EntityType, entry.Name, entry.DisplayName);
            }
        }

        // 重建节点
        if (document.Nodes != null)
        {
            foreach (var yamlNode in document.Nodes)
            {
                var nodeData = new NodeData
                {
                    Uuid = yamlNode.Uuid,
                    Type = yamlNode.NodeType,
                    SubType = yamlNode.SubType,
                    Position = new PositionData
                    {
                        X = yamlNode.Position?.X ?? 0,
                        Y = yamlNode.Position?.Y ?? 0
                    },
                    Properties = yamlNode.Properties ?? new Dictionary<string, object>(),
                    Inputs = yamlNode.Inputs?.Select(p => new PortData
                    {
                        Id = p.Name,
                        Label = p.Label,
                        Type = p.PortType,
                        DataType = p.DataType
                    }).ToList() ?? new List<PortData>(),
                    Outputs = yamlNode.Outputs?.Select(p => new PortData
                    {
                        Id = p.Name,
                        Label = p.Label,
                        Type = p.PortType,
                        DataType = p.DataType
                    }).ToList() ?? new List<PortData>(),
                    NextNodeUuids = yamlNode.NextNodeUuids ?? new List<string>()
                };

                sceneGraph.Nodes.Add(nodeData);

                // 写入数据库
                if (uuidService != null)
                {
                    uuidService.RegisterNodeWithUuid(
                        yamlNode.Uuid,
                        yamlNode.NodeType,
                        yamlNode.SubType,
                        yamlNode.DisplayName,
                        yamlNode.Position?.X ?? 0,
                        yamlNode.Position?.Y ?? 0,
                        yamlNode.Properties);
                }
            }
        }

        // 重建连线
        if (document.Edges != null)
        {
            foreach (var yamlEdge in document.Edges)
            {
                var edgeData = new EdgeData
                {
                    Uuid = yamlEdge.Uuid,
                    SourceNodeUuid = yamlEdge.SourceNodeUuid,
                    SourcePort = yamlEdge.SourcePort,
                    TargetNodeUuid = yamlEdge.TargetNodeUuid,
                    TargetPort = yamlEdge.TargetPort,
                    Type = yamlEdge.EdgeType
                };

                sceneGraph.Edges.Add(edgeData);

                // 写入数据库
                if (uuidService != null)
                {
                    uuidService.RegisterEdgeWithUuid(
                        yamlEdge.Uuid,
                        yamlEdge.SourceNodeUuid,
                        yamlEdge.SourcePort,
                        yamlEdge.TargetNodeUuid,
                        yamlEdge.TargetPort,
                        yamlEdge.EdgeType);
                }
            }
        }

        // 保存到 EditorService
        var json = JsonSerializer.Serialize(document);
        _editorService.SaveSceneGraph(sceneId, json);

        return sceneGraph;
    }

    /// <summary>
    /// 从 YAML 字符串导入蓝图
    /// </summary>
    public async Task<SceneGraphData> ImportYamlAsync(
        string yamlContent,
        UuidRegistryService? uuidService = null)
    {
        return await YamlToBlueprintAsync(yamlContent, uuidService);
    }

    // ==================== 辅助方法 ====================

    private string ExtractSubType(NodeData node)
    {
        if (node.Properties != null && node.Properties.TryGetValue("subType", out var subType))
        {
            return subType?.ToString() ?? "";
        }
        return "";
    }

    private string ExtractDisplayName(NodeData node, string nodeType)
    {
        if (node.Properties != null)
        {
            // 尝试从属性中获取显示名称
            if (node.Properties.TryGetValue("speaker", out var speaker))
            {
                return $"Dialogue: {speaker}";
            }
            if (node.Properties.TryGetValue("eventType", out var eventType))
            {
                return $"Event: {eventType}";
            }
            if (node.Properties.TryGetValue("condition", out var condition))
            {
                return $"Branch: {condition}";
            }
        }
        return nodeType;
    }
}
