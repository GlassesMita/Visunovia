using System.Text.Json;
using Visunovia.Models.Engine;
using Visunovia.Services.Configuration;

namespace Visunovia.Services;

/// <summary>
/// Blueprint ↔ JSON 双向转换服务。
/// </summary>
public class BlueprintJsonConverter
{
    private readonly EditorService _editorService;
    private static readonly JsonSerializerOptions LorJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public BlueprintJsonConverter(EditorService editorService)
    {
        _editorService = editorService;
    }

    // ==================== Blueprint → JSON ====================

    /// <summary>
    /// 将指定场景的蓝图转换为 JSON 文档。
    /// 为所有节点、资源、连线分配 UUID，并写入数据库。
    /// </summary>
    public async Task<JsonSceneDocument> BlueprintToJsonAsync(
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

        var document = new JsonSceneDocument
        {
            Version = "1.0.0",
            Metadata = new JsonSceneMetadata
            {
                SceneId = sceneId,
                DisplayName = string.IsNullOrEmpty(displayName) ? sceneId : displayName,
                Description = description,
                Author = author,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                UpdatedAt = DateTime.UtcNow.ToString("O")
            }
        };

        var sceneUuid = uuidService?.RegisterScene(sceneId, document.Metadata.DisplayName)
            ?? Guid.NewGuid().ToString();

        document.UuidRegistry.Add(new JsonUuidEntry
        {
            Uuid = sceneUuid,
            EntityType = "Scene",
            Name = sceneId,
            DisplayName = document.Metadata.DisplayName
        });

        if (sceneGraph.Nodes != null)
        {
            foreach (var node in sceneGraph.Nodes)
            {
                var nodeUuid = node.Id;
                var nodeType = node.Type ?? "Unknown";
                var subType = ExtractSubType(node);
                var nodeDisplayName = ExtractDisplayName(node, nodeType);
                var position = NormalizeBlueprintPosition(node.Position?.X ?? 0, node.Position?.Y ?? 0);

                if (uuidService != null)
                {
                    uuidService.RegisterNodeWithUuid(nodeUuid, nodeType, subType, nodeDisplayName,
                    position.X, position.Y, node.Properties);
                }

                document.UuidRegistry.Add(new JsonUuidEntry
                {
                    Uuid = nodeUuid,
                    EntityType = "Node",
                    Name = nodeDisplayName,
                    DisplayName = nodeDisplayName
                });

                var inputs = new List<JsonPortDefinition>();
                var outputs = new List<JsonPortDefinition>();

                if (node.Inputs != null)
                {
                    foreach (var port in node.Inputs)
                    {
                        inputs.Add(new JsonPortDefinition
                        {
                            Name = port.Id ?? string.Empty,
                            Label = port.Label ?? string.Empty,
                            PortType = port.Type ?? string.Empty,
                            DataType = port.DataType ?? string.Empty
                        });
                    }
                }

                if (node.Outputs != null)
                {
                    foreach (var port in node.Outputs)
                    {
                        outputs.Add(new JsonPortDefinition
                        {
                            Name = port.Id ?? string.Empty,
                            Label = port.Label ?? string.Empty,
                            PortType = port.Type ?? string.Empty,
                            DataType = port.DataType ?? string.Empty
                        });
                    }
                }

                document.Nodes.Add(new JsonNodeEntry
                {
                    Uuid = nodeUuid,
                    NodeType = nodeType,
                    SubType = subType,
                    DisplayName = nodeDisplayName,
                    Position = new JsonPosition
                    {
                        X = position.X,
                        Y = position.Y
                    },
                    Properties = node.Properties ?? new Dictionary<string, object>(),
                    Inputs = inputs,
                    Outputs = outputs,
                    NextNodeUuids = node.NextNodeUuids ?? new List<string>()
                });
            }
        }

        if (sceneGraph.Edges != null)
        {
            foreach (var edge in sceneGraph.Edges)
            {
                var edgeUuid = edge.Id;

                if (uuidService != null)
                {
                    uuidService.RegisterEdgeWithUuid(edgeUuid, edge.Source, edge.SourcePort,
                        edge.Target, edge.TargetPort, edge.Type ?? "exec");
                }

                document.UuidRegistry.Add(new JsonUuidEntry
                {
                    Uuid = edgeUuid,
                    EntityType = "Edge",
                    Name = $"{edge.SourcePort}->{edge.TargetPort}",
                    DisplayName = $"{edge.Source}:{edge.SourcePort} → {edge.Target}:{edge.TargetPort}"
                });

                document.Edges.Add(new JsonEdgeEntry
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

        if (uuidService != null)
        {
            var jsonContent = JsonSerializer.Serialize(document, LorJsonOptions);
            uuidService.SaveJsonSnapshot(sceneId, jsonContent, "Blueprint→JSON export");
        }

        return document;
    }

    /// <summary>
    /// 将蓝图转换为 JSON 格式的 Lor 字符串。
    /// </summary>
    public async Task<string> ExportJsonAsync(
        string sceneId,
        UuidRegistryService? uuidService = null,
        string displayName = "",
        string description = "",
        string author = "")
    {
        var document = await BlueprintToJsonAsync(sceneId, uuidService, displayName, description, author);
        return JsonSerializer.Serialize(document, LorJsonOptions);
    }

    // ==================== JSON → Blueprint ====================

    /// <summary>
    /// 从 JSON 文档恢复蓝图到 EditorService。
    /// 解析 UUID 注册表，重建节点和连线关系。
    /// </summary>
    public async Task<SceneGraphData> JsonToBlueprintAsync(
        string jsonContent,
        UuidRegistryService? uuidService = null,
        string? targetSceneId = null)
    {
        var document = DeserializeLorSceneDocument(jsonContent);
        if (document == null)
        {
            throw new InvalidOperationException("JSON 解析失败：文档为空");
        }

        var sceneId = !string.IsNullOrWhiteSpace(targetSceneId)
            ? targetSceneId
            : document.Metadata?.SceneId ?? Guid.NewGuid().ToString();

        if (uuidService != null && document.Metadata != null)
        {
            var sceneUuid = document.UuidRegistry
                .FirstOrDefault(e => e.EntityType == "Scene")?.Uuid
                ?? Guid.NewGuid().ToString();
            uuidService.RegisterSceneWithUuid(sceneUuid, sceneId, document.Metadata.DisplayName);
        }

        var sceneGraph = new SceneGraphData
        {
            Id = sceneId,
            Nodes = new List<NodeData>(),
            Edges = new List<EdgeData>()
        };

        if (uuidService != null && document.UuidRegistry != null)
        {
            foreach (var entry in document.UuidRegistry)
            {
                uuidService.RegisterEntityWithUuid(entry.Uuid, entry.EntityType, entry.Name, entry.DisplayName);
            }
        }

        if (document.Nodes != null)
        {
            foreach (var jsonNode in document.Nodes)
            {
                var position = NormalizeBlueprintPosition(jsonNode.Position?.X ?? 0, jsonNode.Position?.Y ?? 0);

                var nodeData = new NodeData
                {
                    Uuid = jsonNode.Uuid,
                    Type = NormalizeNodeType(jsonNode.NodeType),
                    SubType = jsonNode.SubType,
                    Position = new PositionData
                    {
                        X = position.X,
                        Y = position.Y
                    },
                    Properties = jsonNode.Properties ?? new Dictionary<string, object>(),
                    Inputs = jsonNode.Inputs?.Select(p => new PortData
                    {
                        Id = p.Name,
                        Label = p.Label,
                        Type = p.PortType,
                        DataType = p.DataType
                    }).ToList() ?? new List<PortData>(),
                    Outputs = jsonNode.Outputs?.Select(p => new PortData
                    {
                        Id = p.Name,
                        Label = p.Label,
                        Type = p.PortType,
                        DataType = p.DataType
                    }).ToList() ?? new List<PortData>(),
                    NextNodeUuids = jsonNode.NextNodeUuids ?? new List<string>()
                };

                sceneGraph.Nodes.Add(nodeData);

                if (uuidService != null)
                {
                    uuidService.RegisterNodeWithUuid(
                        jsonNode.Uuid,
                        NormalizeNodeType(jsonNode.NodeType),
                        jsonNode.SubType,
                        jsonNode.DisplayName,
                        position.X,
                        position.Y,
                        jsonNode.Properties);
                }
            }
        }

        if (document.Edges != null)
        {
            foreach (var jsonEdge in document.Edges)
            {
                var edgeData = new EdgeData
                {
                    Uuid = jsonEdge.Uuid,
                    SourceNodeUuid = jsonEdge.SourceNodeUuid,
                    SourcePort = jsonEdge.SourcePort,
                    TargetNodeUuid = jsonEdge.TargetNodeUuid,
                    TargetPort = jsonEdge.TargetPort,
                    Type = jsonEdge.EdgeType
                };

                sceneGraph.Edges.Add(edgeData);

                if (uuidService != null)
                {
                    uuidService.RegisterEdgeWithUuid(
                        jsonEdge.Uuid,
                        jsonEdge.SourceNodeUuid,
                        jsonEdge.SourcePort,
                        jsonEdge.TargetNodeUuid,
                        jsonEdge.TargetPort,
                        jsonEdge.EdgeType);
                }
            }
        }

        var json = JsonSerializer.Serialize(sceneGraph);
        _editorService.SaveSceneGraph(sceneId, json);

        return sceneGraph;
    }

    /// <summary>
    /// 从 JSON 字符串导入蓝图。
    /// </summary>
    public async Task<SceneGraphData> ImportJsonAsync(
        string jsonContent,
        UuidRegistryService? uuidService = null,
        string? targetSceneId = null)
    {
        return await JsonToBlueprintAsync(jsonContent, uuidService, targetSceneId);
    }

    private static JsonSceneDocument? DeserializeLorSceneDocument(string content)
    {
        return JsonSerializer.Deserialize<JsonSceneDocument>(content, LorJsonOptions);
    }

    // ==================== 辅助方法 ====================

    private static string ExtractSubType(NodeData node)
    {
        if (node.Properties != null && node.Properties.TryGetValue("subType", out var subType))
        {
            return subType?.ToString() ?? "";
        }
        return "";
    }

    private static string ExtractDisplayName(NodeData node, string nodeType)
    {
        if (node.Properties != null)
        {
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

    /// <summary>
    /// 蓝图编辑器使用绝对坐标：编辑器最左侧中部为 (0, 0)，不支持 X 小于 0。
    /// Y 允许为负值以表示中线以上的位置。
    /// </summary>
    private static PositionData NormalizeBlueprintPosition(double x, double y)
    {
        return new PositionData
        {
            X = Math.Max(0, x),
            Y = y
        };
    }

    private static string NormalizeNodeType(string? nodeType)
    {
        return nodeType switch
        {
            "Start" => "StartNode",
            "End" => "EndNode",
            "Event" => "EventNode",
            "Dialogue" => "DialogueNode",
            "Branch" => "BranchNode",
            "Logic" => "LogicNode",
            "Resource" => "ResourceNode",
            "Choice" => "ChoiceNode",
            _ => nodeType ?? "UnknownNode"
        };
    }
}