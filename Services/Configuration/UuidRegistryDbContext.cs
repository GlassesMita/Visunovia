using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Visunovia.Services.Configuration;

/// <summary>
/// SQLite 数据库上下文，管理 UUID 注册表和节点/资源的持久化存储。
/// 每个项目对应一个独立的 .vndb 数据库文件，存储在项目根目录下。
/// </summary>
public class UuidRegistryDbContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;
    private bool _disposed;

    public UuidRegistryDbContext(string projectPath)
    {
        _dbPath = Path.Combine(projectPath, "Project.vndb");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeTables();
    }

    public IDbConnection Connection => _connection;

    /// <summary>
    /// 初始化数据库表结构
    /// </summary>
    private void InitializeTables()
    {
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS UuidRegistry (
                Uuid TEXT PRIMARY KEY,
                EntityType TEXT NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                DisplayName TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS NodeEntries (
                Uuid TEXT PRIMARY KEY,
                NodeType TEXT NOT NULL,
                SubType TEXT NOT NULL DEFAULT '',
                DisplayName TEXT NOT NULL DEFAULT '',
                PositionX REAL NOT NULL DEFAULT 0,
                PositionY REAL NOT NULL DEFAULT 0,
                PropertiesJson TEXT NOT NULL DEFAULT '{}',
                FOREIGN KEY (Uuid) REFERENCES UuidRegistry(Uuid) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ResourceEntries (
                Uuid TEXT PRIMARY KEY,
                ResourceType TEXT NOT NULL,
                FilePath TEXT NOT NULL DEFAULT '',
                DisplayName TEXT NOT NULL DEFAULT '',
                MetadataJson TEXT NOT NULL DEFAULT '{}',
                FOREIGN KEY (Uuid) REFERENCES UuidRegistry(Uuid) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS EdgeEntries (
                Uuid TEXT PRIMARY KEY,
                SourceNodeUuid TEXT NOT NULL,
                SourcePort TEXT NOT NULL DEFAULT '',
                TargetNodeUuid TEXT NOT NULL,
                TargetPort TEXT NOT NULL DEFAULT '',
                EdgeType TEXT NOT NULL DEFAULT 'exec',
                FOREIGN KEY (Uuid) REFERENCES UuidRegistry(Uuid) ON DELETE CASCADE,
                FOREIGN KEY (SourceNodeUuid) REFERENCES NodeEntries(Uuid) ON DELETE CASCADE,
                FOREIGN KEY (TargetNodeUuid) REFERENCES NodeEntries(Uuid) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS SceneEntries (
                Uuid TEXT PRIMARY KEY,
                SceneId TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL DEFAULT '',
                BackgroundUuid TEXT,
                BgmUuid TEXT,
                MetadataJson TEXT NOT NULL DEFAULT '{}',
                FOREIGN KEY (Uuid) REFERENCES UuidRegistry(Uuid) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS JsonSnapshots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SceneId TEXT NOT NULL,
                JsonContent TEXT NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                Description TEXT NOT NULL DEFAULT ''
            );

            CREATE INDEX IF NOT EXISTS idx_uuid_registry_type ON UuidRegistry(EntityType);
            CREATE INDEX IF NOT EXISTS idx_node_entries_type ON NodeEntries(NodeType);
            CREATE INDEX IF NOT EXISTS idx_resource_entries_type ON ResourceEntries(ResourceType);
            CREATE INDEX IF NOT EXISTS idx_edge_entries_source ON EdgeEntries(SourceNodeUuid);
            CREATE INDEX IF NOT EXISTS idx_edge_entries_target ON EdgeEntries(TargetNodeUuid);
            CREATE INDEX IF NOT EXISTS idx_scene_entries_sceneid ON SceneEntries(SceneId);
        ");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
