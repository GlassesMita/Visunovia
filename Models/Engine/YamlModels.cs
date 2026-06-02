using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Visunovia.Models.Engine;

// ==================== YAML 序列化根模型 ====================

/// <summary>
/// YAML 场景文件根模型。
/// 每个 YAML 文件对应一个场景的完整蓝图定义，包含版本信息、元数据、节点列表、资源列表和连线列表。
/// 所有实体通过 UUID 引用关联。
/// </summary>
public class YamlSceneDocument
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("metadata")]
    public YamlSceneMetadata Metadata { get; set; } = new();

    [JsonPropertyName("uuid_registry")]
    public List<YamlUuidEntry> UuidRegistry { get; set; } = new();

    [JsonPropertyName("resources")]
    public List<YamlResourceEntry> Resources { get; set; } = new();

    [JsonPropertyName("nodes")]
    public List<YamlNodeEntry> Nodes { get; set; } = new();

    [JsonPropertyName("edges")]
    public List<YamlEdgeEntry> Edges { get; set; } = new();
}

/// <summary>
/// 场景元数据
/// </summary>
public class YamlSceneMetadata
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
public class YamlUuidEntry
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
/// YAML 中的资源条目，通过 UUID 标识。
/// </summary>
public class YamlResourceEntry
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
/// YAML 中的节点条目，通过 UUID 标识。
/// 包含节点类型、位置、属性和端口定义。
/// </summary>
public class YamlNodeEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("node_type")]
    public string NodeType { get; set; } = ""; // StartNode, EndNode, DialogueNode, EventNode, BranchNode, ChoiceNode, LogicNode, ResourceNode

    [JsonPropertyName("sub_type")]
    public string SubType { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("position")]
    public YamlPosition Position { get; set; } = new();

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("inputs")]
    public List<YamlPortDefinition> Inputs { get; set; } = new();

    [JsonPropertyName("outputs")]
    public List<YamlPortDefinition> Outputs { get; set; } = new();
}

/// <summary>
/// 端口定义
/// </summary>
public class YamlPortDefinition
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
/// 2D 坐标
/// </summary>
public class YamlPosition
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

// ==================== 连线条目 ====================

/// <summary>
/// YAML 中的连线条目，通过 UUID 标识。
/// 源节点和目标节点通过 UUID 引用节点表中的条目。
/// </summary>
public class YamlEdgeEntry
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

// ==================== YAML 序列化/反序列化辅助 ====================

/// <summary>
/// YAML 序列化器工厂，提供统一的序列化和反序列化配置。
/// </summary>
public static class YamlSerializerFactory
{
    public static ISerializer CreateSerializer()
    {
        return new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    public static IDeserializer CreateDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }
}
