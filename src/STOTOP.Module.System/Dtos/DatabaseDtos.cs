using System.Text.Json.Serialization;

namespace STOTOP.Module.System.Dtos;

/// <summary>
/// 数据库分析结果
/// </summary>
public class DatabaseAnalysisResult
{
    public bool HasExistingTables { get; set; }
    public List<TableInfo> ExistingSystemTables { get; set; } = new();
    public List<string> MissingSystemTables { get; set; } = new();

    [JsonPropertyName("nonSystemTables")]
    public List<TableInfo> ForeignTables { get; set; } = new();

    public List<TableInfo> HangfireTables { get; set; } = new();
}

public class TableInfo
{
    [JsonPropertyName("name")]
    public string TableName { get; set; } = string.Empty;

    [JsonPropertyName("schemaName")]
    public string SchemaName { get; set; } = "dbo";

    [JsonPropertyName("rowCount")]
    public long RowCount { get; set; }
}

/// <summary>
/// 保留初始化请求
/// </summary>
public class PreserveInitializeRequest
{
    /// <summary>
    /// 连接字符串
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 数据库连接 ID（优先使用，底层自动解密密码）
    /// </summary>
    public long? ConnectionId { get; set; }

    /// <summary>
    /// 每个有数据的系统表的处理方式：表名 -> "clear"(清空重建) 或 "preserve"(保留数据更新结构)
    /// </summary>
    public Dictionary<string, string> TableActions { get; set; } = new();

    /// <summary>
    /// 备份路径（SQL Server 服务器上的目录），为空则不备份
    /// </summary>
    public string? BackupPath { get; set; }
}

/// <summary>
/// dry-run 预览结果
/// </summary>
public class PreserveDryRunResult
{
    /// <summary>将被删除的非系统表</summary>
    public List<string> TablesToDelete { get; set; } = new();
    /// <summary>将保留数据的系统表</summary>
    public List<string> TablesToPreserve { get; set; } = new();
    /// <summary>将清空重建的系统表</summary>
    public List<string> TablesToRebuild { get; set; } = new();
    /// <summary>将新建的缺失系统表</summary>
    public List<string> TablesToCreate { get; set; } = new();
    /// <summary>Hangfire 表</summary>
    public List<string> HangfireTablesToDelete { get; set; } = new();
    /// <summary>风险提示</summary>
    public List<string> Warnings { get; set; } = new();
    /// <summary>预估数据丢失行数</summary>
    public long EstimatedDataLossRows { get; set; }
}

/// <summary>
/// 自动备份配置
/// </summary>
public class BackupConfig
{
    /// <summary>是否启用定时备份</summary>
    public bool Enabled { get; set; }
    /// <summary>Cron 表达式，默认每天凌晨2点</summary>
    public string CronExpression { get; set; } = "0 2 * * *";
    /// <summary>备份目录（SQL Server 服务器上的路径）</summary>
    public string BackupDirectory { get; set; } = "";
    /// <summary>文件名模式，支持 {dbName} {timestamp} 占位符</summary>
    public string FileNamePattern { get; set; } = "{dbName}_{timestamp}.bak";
    /// <summary>保留最近 N 条备份，超出的旧备份自动删除</summary>
    public int RetentionCount { get; set; } = 7;
}

/// <summary>
/// 连接字符串请求
/// </summary>
public class ConnectionStringRequest
{
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 数据库连接 ID（优先使用，底层自动解密密码）
    /// </summary>
    public long? ConnectionId { get; set; }
}

/// <summary>
/// Setup 口令认证请求
/// </summary>
public class SetupAuthRequest
{
    public string Passphrase { get; set; } = "";
}

/// <summary>
/// Setup 口令认证响应
/// </summary>
public class SetupAuthResponse
{
    public string Token { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}
