using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IDatabaseService
{
    /// <summary>
    /// 获取数据库状态
    /// </summary>
    Task<DatabaseStatusResult> GetDatabaseStatusAsync();

    /// <summary>
    /// 测试数据库连接
    /// </summary>
    Task<TestConnectionResult> TestConnectionAsync(string provider, string connectionString);

    /// <summary>
    /// 初始化数据库
    /// </summary>
    Task<InitializeResult> InitializeDatabaseAsync(string? provider = null, string? connectionString = null);

    /// <summary>
    /// 获取当前数据库配置
    /// </summary>
    Task<DatabaseConfigResult> GetConfigAsync();

    /// <summary>
    /// 更新数据库配置
    /// </summary>
    Task<UpdateConfigResult> UpdateConfigAsync(string provider, string connectionString);

    /// <summary>
    /// 分析数据库表状态
    /// </summary>
    Task<DatabaseAnalysisResult> AnalyzeDatabaseAsync(string connectionString);

    /// <summary>
    /// 全新初始化数据库
    /// </summary>
    Task FullInitializeAsync(string connectionString);

    /// <summary>
    /// 保留初始化数据库
    /// </summary>
    Task PreserveInitializeAsync(
        string connectionString,
        Dictionary<string, string> tableActions,
        string? backupPath = null,
        Func<string, string, int, int, Task>? onProgress = null);

    /// <summary>
    /// dry-run 预览保留初始化操作影响
    /// </summary>
    Task<PreserveDryRunResult> DryRunAsync(string connectionString, Dictionary<string, string> tableActions);

    /// <summary>
    /// 执行定时备份任务（由 Hangfire RecurringJob 调用）
    /// </summary>
    Task ExecuteScheduledBackupAsync();

    /// <summary>
    /// 生成 Setup 管理员 JWT Token
    /// </summary>
    string GenerateSetupToken();
}

public class DatabaseStatusResult
{
    public string Provider { get; set; } = "InMemory";
    public bool Initialized { get; set; }
    public bool Connected { get; set; }
}

public class TestConnectionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class InitializeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class DatabaseConfigResult
{
    public string Provider { get; set; } = "";
    public string ConnectionString { get; set; } = "";
}

public class UpdateConfigResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
