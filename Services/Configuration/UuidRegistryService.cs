using System.Text.Json;
using Dapper;

namespace Visunovia.Services.Configuration;

/// <summary>
/// UUID 注册表服务，负责为所有实体（节点、资源、连线、场景）分配和管理 UUID。
/// 提供 CRUD 操作和查询功能，所有数据持久化到 SQLite 数据库。
/// </summary>
public class UuidRegistryService
{
    private readonly UuidRegistryDbContext _db;

    public UuidRegistryService(UuidRegistryDbContext db)
    {
        _db = db;
    }

    // ==================== UUID 生成与注册 ====================

    /// <summary>
    /// 生成新 UUID 并注册到指定类型的注册表中
    /// </summary>
    public string RegisterEntity(string entityType, string name = "", string displayName = "")
    {
        var uuid = System.Guid.NewGuid().ToString();
        _db.Connection.Execute(@"
            INSERT INTO UuidRegistry (Uuid, EntityType, Name, DisplayName, CreatedAt, UpdatedAt)
            VALUES (@Uuid, @EntityType, @Name, @DisplayName, datetime('now'), datetime('now'))",
            new { Uuid = uuid, EntityType = entityType, Name = name, DisplayName = displayName });
        return uuid;
    }

    /// <summary>
    /// 使用指定 UUID 注册实体（用于从 JSON 导入时恢复原有 UUID）
    /// </summary>
    public void RegisterEntityWithUuid(string uuid, string entityType, string name = "", string displayName = "")
    {
        _db.Connection.Execute(@"
            INSERT OR REPLACE INTO UuidRegistry (Uuid, EntityType, Name, DisplayName, CreatedAt, UpdatedAt)
            VALUES (@Uuid, @EntityType, @Name, @DisplayName, datetime('now'), datetime('now'))",
            new { Uuid = uuid, EntityType = entityType, Name = name, DisplayName = displayName });
    }

    /// <summary>
    /// 获取实体的 UUID 信息
    /// </summary>
    public UuidEntry? GetEntity(string uuid)
    {
        return _db.Connection.QueryFirstOrDefault<UuidEntry>(
            "SELECT * FROM UuidRegistry WHERE Uuid = @Uuid", new { Uuid = uuid });
    }

    /// <summary>
    /// 按类型获取所有实体
    /// </summary>
    public IEnumerable<UuidEntry> GetEntitiesByType(string entityType)
    {
        return _db.Connection.Query<UuidEntry>(
            "SELECT * FROM UuidRegistry WHERE EntityType = @EntityType ORDER BY CreatedAt",
            new { EntityType = entityType });
    }

    /// <summary>
    /// 获取所有已注册的 UUID 条目
    /// </summary>
    public IEnumerable<UuidEntry> GetAllEntities()
    {
        return _db.Connection.Query<UuidEntry>("SELECT * FROM UuidRegistry ORDER BY EntityType, CreatedAt");
    }

    // ==================== 节点操作 ====================

    /// <summary>
    /// 注册节点并分配 UUID
    /// </summary>
    public string RegisterNode(string nodeType, string subType = "", string displayName = "",
        double posX = 0, double posY = 0, Dictionary<string, object>? properties = null)
    {
        var uuid = RegisterEntity("Node", displayName, displayName);
        var propsJson = JsonSerializer.Serialize(properties ?? new Dictionary<string, object>());
        _db.Connection.Execute(@"
            INSERT INTO NodeEntries (Uuid, NodeType, SubType, DisplayName, PositionX, PositionY, PropertiesJson)
            VALUES (@Uuid, @NodeType, @SubType, @DisplayName, @PositionX, @PositionY, @PropertiesJson)",
            new { Uuid = uuid, NodeType = nodeType, SubType = subType, DisplayName = displayName, PositionX = posX, PositionY = posY, PropertiesJson = propsJson });
        return uuid;
    }

    /// <summary>
    /// 使用指定 UUID 注册节点（从 JSON 导入）
    /// </summary>
    public void RegisterNodeWithUuid(string uuid, string nodeType, string subType = "", string displayName = "",
        double posX = 0, double posY = 0, Dictionary<string, object>? properties = null)
    {
        RegisterEntityWithUuid(uuid, "Node", displayName, displayName);
        var propsJson = JsonSerializer.Serialize(properties ?? new Dictionary<string, object>());
        _db.Connection.Execute(@"
            INSERT OR REPLACE INTO NodeEntries (Uuid, NodeType, SubType, DisplayName, PositionX, PositionY, PropertiesJson)
            VALUES (@Uuid, @NodeType, @SubType, @DisplayName, @PositionX, @PositionY, @PropertiesJson)",
            new { Uuid = uuid, NodeType = nodeType, SubType = subType, DisplayName = displayName, PositionX = posX, PositionY = posY, PropertiesJson = propsJson });
    }

    /// <summary>
    /// 获取节点完整信息
    /// </summary>
    public NodeEntry? GetNode(string uuid)
    {
        return _db.Connection.QueryFirstOrDefault<NodeEntry>(
            "SELECT * FROM NodeEntries WHERE Uuid = @Uuid", new { Uuid = uuid });
    }

    /// <summary>
    /// 获取所有节点
    /// </summary>
    public IEnumerable<NodeEntry> GetAllNodes()
    {
        return _db.Connection.Query<NodeEntry>("SELECT * FROM NodeEntries ORDER BY NodeType");
    }

    /// <summary>
    /// 更新节点属性
    /// </summary>
    public void UpdateNodeProperties(string uuid, Dictionary<string, object> properties)
    {
        var propsJson = JsonSerializer.Serialize(properties);
        _db.Connection.Execute(@"
            UPDATE NodeEntries SET PropertiesJson = @PropertiesJson WHERE Uuid = @Uuid",
            new { Uuid = uuid, PropertiesJson = propsJson });
        _db.Connection.Execute(@"
            UPDATE UuidRegistry SET UpdatedAt = datetime('now') WHERE Uuid = @Uuid",
            new { Uuid = uuid });
    }

    /// <summary>
    /// 更新节点位置
    /// </summary>
    public void UpdateNodePosition(string uuid, double posX, double posY)
    {
        _db.Connection.Execute(@"
            UPDATE NodeEntries SET PositionX = @PositionX, PositionY = @PositionY WHERE Uuid = @Uuid",
            new { Uuid = uuid, PositionX = posX, PositionY = posY });
    }

    /// <summary>
    /// 删除节点（级联删除相关连线）
    /// </summary>
    public void DeleteNode(string uuid)
    {
        _db.Connection.Execute("DELETE FROM EdgeEntries WHERE SourceNodeUuid = @Uuid OR TargetNodeUuid = @Uuid", new { Uuid = uuid });
        _db.Connection.Execute("DELETE FROM NodeEntries WHERE Uuid = @Uuid", new { Uuid = uuid });
        _db.Connection.Execute("DELETE FROM UuidRegistry WHERE Uuid = @Uuid", new { Uuid = uuid });
    }

    // ==================== 资源操作 ====================

    /// <summary>
    /// 注册资源并分配 UUID
    /// </summary>
    public string RegisterResource(string resourceType, string filePath, string displayName = "",
        Dictionary<string, object>? metadata = null)
    {
        var uuid = RegisterEntity("Resource", displayName, displayName);
        var metaJson = JsonSerializer.Serialize(metadata ?? new Dictionary<string, object>());
        _db.Connection.Execute(@"
            INSERT INTO ResourceEntries (Uuid, ResourceType, FilePath, DisplayName, MetadataJson)
            VALUES (@Uuid, @ResourceType, @FilePath, @DisplayName, @MetadataJson)",
            new { Uuid = uuid, ResourceType = resourceType, FilePath = filePath, DisplayName = displayName, MetadataJson = metaJson });
        return uuid;
    }

    /// <summary>
    /// 使用指定 UUID 注册资源
    /// </summary>
    public void RegisterResourceWithUuid(string uuid, string resourceType, string filePath,
        string displayName = "", Dictionary<string, object>? metadata = null)
    {
        RegisterEntityWithUuid(uuid, "Resource", displayName, displayName);
        var metaJson = JsonSerializer.Serialize(metadata ?? new Dictionary<string, object>());
        _db.Connection.Execute(@"
            INSERT OR REPLACE INTO ResourceEntries (Uuid, ResourceType, FilePath, DisplayName, MetadataJson)
            VALUES (@Uuid, @ResourceType, @FilePath, @DisplayName, @MetadataJson)",
            new { Uuid = uuid, ResourceType = resourceType, FilePath = filePath, DisplayName = displayName, MetadataJson = metaJson });
    }

    /// <summary>
    /// 获取资源完整信息
    /// </summary>
    public ResourceEntry? GetResource(string uuid)
    {
        return _db.Connection.QueryFirstOrDefault<ResourceEntry>(
            "SELECT * FROM ResourceEntries WHERE Uuid = @Uuid", new { Uuid = uuid });
    }

    /// <summary>
    /// 获取所有资源
    /// </summary>
    public IEnumerable<ResourceEntry> GetAllResources()
    {
        return _db.Connection.Query<ResourceEntry>("SELECT * FROM ResourceEntries ORDER BY ResourceType");
    }

    /// <summary>
    /// 按类型获取资源
    /// </summary>
    public IEnumerable<ResourceEntry> GetResourcesByType(string resourceType)
    {
        return _db.Connection.Query<ResourceEntry>(
            "SELECT * FROM ResourceEntries WHERE ResourceType = @ResourceType ORDER BY DisplayName",
            new { ResourceType = resourceType });
    }

    // ==================== 连线操作 ====================

    /// <summary>
    /// 注册连线并分配 UUID
    /// </summary>
    public string RegisterEdge(string sourceNodeUuid, string sourcePort, string targetNodeUuid,
        string targetPort, string edgeType = "exec")
    {
        var uuid = RegisterEntity("Edge", $"{sourcePort}->{targetPort}");
        _db.Connection.Execute(@"
            INSERT INTO EdgeEntries (Uuid, SourceNodeUuid, SourcePort, TargetNodeUuid, TargetPort, EdgeType)
            VALUES (@Uuid, @SourceNodeUuid, @SourcePort, @TargetNodeUuid, @TargetPort, @EdgeType)",
            new { Uuid = uuid, SourceNodeUuid = sourceNodeUuid, SourcePort = sourcePort, TargetNodeUuid = targetNodeUuid, TargetPort = targetPort, EdgeType = edgeType });
        return uuid;
    }

    /// <summary>
    /// 使用指定 UUID 注册连线
    /// </summary>
    public void RegisterEdgeWithUuid(string uuid, string sourceNodeUuid, string sourcePort,
        string targetNodeUuid, string targetPort, string edgeType = "exec")
    {
        RegisterEntityWithUuid(uuid, "Edge", $"{sourcePort}->{targetPort}");
        _db.Connection.Execute(@"
            INSERT OR REPLACE INTO EdgeEntries (Uuid, SourceNodeUuid, SourcePort, TargetNodeUuid, TargetPort, EdgeType)
            VALUES (@Uuid, @SourceNodeUuid, @SourcePort, @TargetNodeUuid, @TargetPort, @EdgeType)",
            new { Uuid = uuid, SourceNodeUuid = sourceNodeUuid, SourcePort = sourcePort, TargetNodeUuid = targetNodeUuid, TargetPort = targetPort, EdgeType = edgeType });
    }

    /// <summary>
    /// 获取所有连线
    /// </summary>
    public IEnumerable<EdgeEntry> GetAllEdges()
    {
        return _db.Connection.Query<EdgeEntry>("SELECT * FROM EdgeEntries");
    }

    /// <summary>
    /// 删除连线
    /// </summary>
    public void DeleteEdge(string uuid)
    {
        _db.Connection.Execute("DELETE FROM EdgeEntries WHERE Uuid = @Uuid", new { Uuid = uuid });
        _db.Connection.Execute("DELETE FROM UuidRegistry WHERE Uuid = @Uuid", new { Uuid = uuid });
    }

    // ==================== 场景操作 ====================

    /// <summary>
    /// 注册场景并分配 UUID
    /// </summary>
    public string RegisterScene(string sceneId, string displayName = "")
    {
        var uuid = RegisterEntity("Scene", sceneId, displayName);
        _db.Connection.Execute(@"
            INSERT INTO SceneEntries (Uuid, SceneId, DisplayName, MetadataJson)
            VALUES (@Uuid, @SceneId, @DisplayName, '{}')",
            new { Uuid = uuid, SceneId = sceneId, DisplayName = displayName });
        return uuid;
    }

    /// <summary>
    /// 使用指定 UUID 注册场景
    /// </summary>
    public void RegisterSceneWithUuid(string uuid, string sceneId, string displayName = "")
    {
        RegisterEntityWithUuid(uuid, "Scene", sceneId, displayName);
        _db.Connection.Execute(@"
            INSERT OR REPLACE INTO SceneEntries (Uuid, SceneId, DisplayName, MetadataJson)
            VALUES (@Uuid, @SceneId, @DisplayName, '{}')",
            new { Uuid = uuid, SceneId = sceneId, DisplayName = displayName });
    }

    /// <summary>
    /// 获取所有场景
    /// </summary>
    public IEnumerable<SceneEntry> GetAllScenes()
    {
        return _db.Connection.Query<SceneEntry>("SELECT * FROM SceneEntries ORDER BY SceneId");
    }

        // ==================== JSON 快照操作 ====================

    /// <summary>
        /// 保存 JSON 快照
    /// </summary>
        public void SaveJsonSnapshot(string sceneId, string jsonContent, string description = "")
    {
        _db.Connection.Execute(@"
            INSERT INTO JsonSnapshots (SceneId, JsonContent, Description, CreatedAt)
            VALUES (@SceneId, @JsonContent, @Description, datetime('now'))",
            new { SceneId = sceneId, JsonContent = jsonContent, Description = description });
    }

    /// <summary>
        /// 获取场景的最新 JSON 快照
    /// </summary>
        public string? GetLatestJsonSnapshot(string sceneId)
    {
        return _db.Connection.QueryFirstOrDefault<string>(@"
            SELECT JsonContent FROM JsonSnapshots
            WHERE SceneId = @SceneId
            ORDER BY CreatedAt DESC LIMIT 1",
            new { SceneId = sceneId });
    }

    // ==================== 批量清除 ====================

    /// <summary>
    /// 清除指定场景的所有数据（用于重新导入）
    /// </summary>
    public void ClearSceneData(string sceneId)
    {
        var scene = _db.Connection.QueryFirstOrDefault<SceneEntry>(
            "SELECT Uuid FROM SceneEntries WHERE SceneId = @SceneId", new { SceneId = sceneId });
        if (scene != null)
        {
            _db.Connection.Execute("DELETE FROM EdgeEntries WHERE SourceNodeUuid IN (SELECT Uuid FROM NodeEntries) OR TargetNodeUuid IN (SELECT Uuid FROM NodeEntries)");
            _db.Connection.Execute("DELETE FROM NodeEntries");
            _db.Connection.Execute("DELETE FROM ResourceEntries");
            _db.Connection.Execute("DELETE FROM SceneEntries WHERE SceneId = @SceneId", new { SceneId = sceneId });
            _db.Connection.Execute("DELETE FROM UuidRegistry WHERE EntityType IN ('Node', 'Resource', 'Edge', 'Scene')");
        }
    }
}

// ==================== 数据模型 ====================

public class UuidEntry
{
    public string Uuid { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class NodeEntry
{
    public string Uuid { get; set; } = "";
    public string NodeType { get; set; } = "";
    public string SubType { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public string PropertiesJson { get; set; } = "{}";
}

public class ResourceEntry
{
    public string Uuid { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string MetadataJson { get; set; } = "{}";
}

public class EdgeEntry
{
    public string Uuid { get; set; } = "";
    public string SourceNodeUuid { get; set; } = "";
    public string SourcePort { get; set; } = "";
    public string TargetNodeUuid { get; set; } = "";
    public string TargetPort { get; set; } = "";
    public string EdgeType { get; set; } = "exec";
}

public class SceneEntry
{
    public string Uuid { get; set; } = "";
    public string SceneId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? BackgroundUuid { get; set; }
    public string? BgmUuid { get; set; }
    public string MetadataJson { get; set; } = "{}";
}
