using System.Text.Json.Serialization;

namespace Visunovia.Models.Engine;

// ==================== JSON 序列化根模型 ====================

/// <summary>
/// JSON 场景文件根模型。
/// 每个 JSON 文件对应一个场景的完整蓝图定义，包含版本信息、元数据、节点列表、资源列表和连线列表。
/// 所有实体通过 UUID 引用关联。
/// </summary>
public class JsonSceneDocument
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("metadata")]
    public JsonSceneMetadata Metadata { get; set; } = new();

    [JsonPropertyName("uuid_registry")]
    public List<JsonUuidEntry> UuidRegistry { get; set; } = new();

    [JsonPropertyName("resources")]
    public List<JsonResourceEntry> Resources { get; set; } = new();

    [JsonPropertyName("nodes")]
    public List<JsonNodeEntry> Nodes { get; set; } = new();

    [JsonPropertyName("edges")]
    public List<JsonEdgeEntry> Edges { get; set; } = new();
}

/// <summary>
/// 场景元数据。
/// </summary>
public class JsonSceneMetadata
{
    [JsonPropertyName("scene_id")]
    public string SceneId { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("background_ref")]
    public string? BackgroundRef { get; set; }

    [JsonPropertyName("bgm_ref")]
    public string? BgmRef { get; set; }
}

// ==================== UUID 注册表条目 ====================

/// <summary>
/// UUID 注册表条目，集中存储所有实体的 UUID 映射信息。
/// 其他部分通过 UUID 引用此表中的实体。
/// </summary>
public class JsonUuidEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("entity_type")]
    public string EntityType { get; set; } = ""; // Node, Resource, Edge, Scene

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";
}

// ==================== 资源条目 ====================

/// <summary>
/// JSON 中的资源条目，通过 UUID 标识。
/// </summary>
public class JsonResourceEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("resource_type")]
    public string ResourceType { get; set; } = ""; // image, audio, bgm, voice, video, scene, font, data

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// ==================== 节点条目 ====================

/// <summary>
/// JSON 中的节点条目，通过 UUID 标识。
/// 包含节点类型、绝对坐标位置、属性、端口定义以及多目标执行步骤。
/// </summary>
public class JsonNodeEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("node_type")]
    public string NodeType { get; set; } = ""; // StartNode, EndNode, DialogueNode, EventNode, BranchNode, ChoiceNode, LogicNode, ResourceNode

    [JsonPropertyName("sub_type")]
    public string SubType { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// 蓝图视图中的绝对坐标位置，用于从 JSON 正确转换后显示到蓝图视图内。
    /// </summary>
    [JsonPropertyName("position")]
    public JsonPosition Position { get; set; } = new();

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("inputs")]
    public List<JsonPortDefinition> Inputs { get; set; } = new();

    [JsonPropertyName("outputs")]
    public List<JsonPortDefinition> Outputs { get; set; } = new();

    /// <summary>
    /// 下一个执行步骤的节点 UUID 列表。
    /// 一个节点可以连接到多个节点，支持分支、并行等流程。
    /// </summary>
    [JsonPropertyName("next_node_uuids")]
    public List<string> NextNodeUuids { get; set; } = new();
}

/// <summary>
/// 端口定义。
/// </summary>
public class JsonPortDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("port_type")]
    public string PortType { get; set; } = ""; // exec, data, resource

    [JsonPropertyName("data_type")]
    public string DataType { get; set; } = ""; // string, number, boolean, any
}

/// <summary>
/// 2D 坐标。
/// </summary>
public class JsonPosition
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

// ==================== 连线条目 ====================

/// <summary>
/// JSON 中的连线条目，通过 UUID 标识。
/// 源节点和目标节点通过 UUID 引用节点表中的条目。
/// </summary>
public class JsonEdgeEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("source_node")]
    public string SourceNodeUuid { get; set; } = "";

    [JsonPropertyName("source_port")]
    public string SourcePort { get; set; } = "";

    [JsonPropertyName("target_node")]
    public string TargetNodeUuid { get; set; } = "";

    [JsonPropertyName("target_port")]
    public string TargetPort { get; set; } = "";

    [JsonPropertyName("edge_type")]
    public string EdgeType { get; set; } = "exec"; // exec, data, resource
}