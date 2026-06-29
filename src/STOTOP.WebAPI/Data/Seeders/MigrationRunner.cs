using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// 迁移步骤声明
/// </summary>
public record MigrationStep(int Version, string Description, Action<STOTOPDbContext> Execute);

/// <summary>
/// 版本化迁移引擎 - 统一管理锁、事务、异常、日志、版本记录
/// 每个步骤独立执行和记录；任一步失败即停止当前模块，避免后续依赖步骤扩大损坏面
/// </summary>
public static class MigrationRunner
{
    private const string LockResource = "STOTOP_Migration";

    /// <summary>
    /// 迁移配置参数（可通过 Configure 注入）
    /// </summary>
    public record MigrationConfig
    {
        public int LockTimeoutMs { get; init; } = 60000;
        public int CommandTimeoutSeconds { get; init; } = 120;
        public int LockRetryCount { get; init; } = 3;
        public int LockRetryBaseDelayMs { get; init; } = 5000;
        public int SeederRetryCount { get; init; } = 2;
        public int SeederRetryDelayMs { get; init; } = 3000;
        public string[] CriticalModules { get; init; } = ["System"];
        public string InstanceId { get; init; } = Environment.MachineName;
    }

    private static MigrationConfig _config = new();

    /// <summary>
    /// 注入迁移配置（应在应用启动时调用一次）
    /// </summary>
    public static void Configure(MigrationConfig config) => _config = config;

    /// <summary>
    /// 获取当前迁移配置
    /// </summary>
    public static MigrationConfig GetConfig() => _config;

    /// <summary>
    /// 确保 SYS迁移历史 表存在，并补充可能缺少的新字段
    /// </summary>
    public static void EnsureMigrationTable(STOTOPDbContext ctx)
    {
        // 创建表（如不存在）
        var createTableSql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SYS迁移历史')
BEGIN
    CREATE TABLE [SYS迁移历史] (
        FID bigint IDENTITY(1,1) PRIMARY KEY,
        F模块 nvarchar(50) NOT NULL,
        F版本号 int NOT NULL,
        F描述 nvarchar(500) NULL,
        F状态 nvarchar(20) NOT NULL DEFAULT 'Success',
        F执行时间 datetime2 NOT NULL DEFAULT GETDATE(),
        F耗时ms bigint NULL,
        CONSTRAINT UQ_SYS迁移历史_模块版本 UNIQUE (F模块, F版本号)
    )
END";
        ctx.Database.ExecuteSqlRaw(createTableSql);

        // 补充可能缺少的新字段（兼容旧表升级）
        var addColumnsSql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'SYS迁移历史' AND COLUMN_NAME = N'F状态')
    ALTER TABLE [SYS迁移历史] ADD F状态 nvarchar(20) NOT NULL DEFAULT 'Success';

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'SYS迁移历史' AND COLUMN_NAME = N'F耗时ms')
    ALTER TABLE [SYS迁移历史] ADD F耗时ms bigint NULL;";
        ctx.Database.ExecuteSqlRaw(addColumnsSql);

        // 创建迁移执行日志表（无 UNIQUE 约束，同一版本可有多条记录）
        var createLogTableSql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SYS迁移执行日志')
CREATE TABLE [SYS迁移执行日志] (
    FID bigint IDENTITY(1,1) PRIMARY KEY,
    F模块 nvarchar(50) NOT NULL,
    F版本号 int NOT NULL,
    F描述 nvarchar(500) NULL,
    F状态 nvarchar(20) NOT NULL,
    F错误消息 nvarchar(max) NULL,
    F执行时间 datetime2 NOT NULL DEFAULT GETDATE(),
    F耗时ms bigint NULL,
    F实例标识 nvarchar(100) NULL
)";
        ctx.Database.ExecuteSqlRaw(createLogTableSql);
    }

    /// <summary>
    /// 执行模块迁移 - 获取数据库级排他锁，按版本顺序执行未完成的迁移步骤
    /// 每个步骤独立执行，任一步失败即停止当前模块
    /// </summary>
    /// <param name="ctx">数据库上下文</param>
    /// <param name="module">模块名称</param>
    /// <param name="steps">按版本号排列的迁移步骤列表</param>
    public static void RunMigrations(STOTOPDbContext ctx, string module, List<MigrationStep> steps)
    {
        // ─── 1. 校验 steps 列表 ───
        ValidateSteps(module, steps);
    
        // ─── 2. 使用独立连接获取 applock（避免干扰 EF Core 的 RetryingExecutionStrategy） ───
        var connectionString = ctx.Database.GetConnectionString();
        using var lockConnection = new SqlConnection(connectionString);
        lockConnection.Open();
        using var lockTransaction = lockConnection.BeginTransaction();
    
        try
        {
            AcquireAppLock(lockConnection, lockTransaction);
    
            // ─── 3. 获取已完成版本集合（步骤级独立判断，不再依赖最大版本号） ───
            var completedVersions = GetCompletedVersions(ctx, module);
    
            // ─── 4. 遍历并执行未完成的步骤 ───
            var pendingSteps = steps.Where(s => !completedVersions.Contains(s.Version)).OrderBy(s => s.Version);

            foreach (var step in pendingSteps)
            {
                Console.WriteLine($"  [{module}] 正在执行 V{step.Version}: {step.Description}...");

                var sw = new Stopwatch();
                try
                {
                    // ─── 原子性保障：Execute + RecordVersion 同一事务 ───
                    // 若 step.Execute 内部已有活跃事务（ctx.Database.CurrentTransaction != null），
                    // 则不再重复开启外层事务，由内部事务自行保证原子性。
                    var hasExternalTransaction = ctx.Database.CurrentTransaction != null;
                    if (hasExternalTransaction)
                    {
                        sw.Restart();
                        step.Execute(ctx);
                        sw.Stop();
                        RecordVersion(ctx, module, step.Version, step.Description, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        // 使用 CreateExecutionStrategy 包裹事务，兼容 SqlServerRetryingExecutionStrategy
                        var strategy = ctx.Database.CreateExecutionStrategy();
                        strategy.Execute(() =>
                        {
                            // ExecutionStrategy 重试会重新进入此 lambda，Restart 保证只记录本次尝试的耗时
                            sw.Restart();
                            using var stepTransaction = ctx.Database.BeginTransaction();
                            try
                            {
                                step.Execute(ctx);
                                sw.Stop();
                                RecordVersion(ctx, module, step.Version, step.Description, sw.ElapsedMilliseconds);
                                stepTransaction.Commit();
                            }
                            catch
                            {
                                stepTransaction.Rollback();
                                throw;
                            }
                        });
                    }

                    // best-effort 写入执行日志：在事务外执行，失败不影响迁移主流程
                    try { AppendExecutionLog(connectionString!, module, step.Version, step.Description, "Success", null, sw.ElapsedMilliseconds); }
                    catch (Exception logEx) { Console.WriteLine($"  [MigrationRunner] 日志写入失败(不影响迁移): {logEx.Message}"); }

                    Console.WriteLine($"  [{module}] V{step.Version} 完成 ({sw.ElapsedMilliseconds}ms)");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    // best-effort 失败日志写入
                    try { AppendExecutionLog(connectionString!, module, step.Version, step.Description, "Failed", ex.ToString(), sw.ElapsedMilliseconds); }
                    catch { /* 静默忽略 */ }

                    Console.WriteLine($"  [{module}] V{step.Version} 失败: {ex.Message}");
                    throw new InvalidOperationException(
                        $"[{module}] V{step.Version}({step.Description}) 迁移失败，已停止后续步骤。", ex);
                }
            }
        }
        finally
        {
            // ─── 6. 释放锁 ─── commit 锁事务即释放 applock
            try
            {
                lockTransaction.Commit();
            }
            catch
            {
                // 如果 commit 失败，rollback 同样会释放 applock
                try { lockTransaction.Rollback(); } catch { /* 忽略 rollback 异常 */ }
            }
        }
    }

    /// <summary>
    /// 获取指定模块已执行的最高版本号，未执行过返回 0（保留以兼容外部引用）
    /// </summary>
    private static int GetCurrentVersion(STOTOPDbContext ctx, string module)
    {
        // 先检查表是否存在（兼容首次调用时表还未建的情况）
        var tableExists = ctx.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SYS迁移历史'"
        ).AsEnumerable().FirstOrDefault();

        if (tableExists == 0) return 0;

        var result = ctx.Database.SqlQueryRaw<int>(
            "SELECT ISNULL(MAX(F版本号), 0) AS Value FROM [SYS迁移历史] WHERE F模块 = {0}", module
        ).AsEnumerable().FirstOrDefault();

        return result;
    }

    /// <summary>
    /// 获取指定模块所有已成功记录的版本号集合，用于步骤级独立过滤
    /// </summary>
    private static HashSet<int> GetCompletedVersions(STOTOPDbContext ctx, string module)
    {
        var tableExists = ctx.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SYS迁移历史'"
        ).AsEnumerable().FirstOrDefault();

        if (tableExists == 0) return new HashSet<int>();

        var versions = ctx.Database.SqlQueryRaw<int>(
            "SELECT F版本号 AS Value FROM [SYS迁移历史] WHERE F模块 = {0}", module
        ).ToList();

        return new HashSet<int>(versions);
    }

    /// <summary>
    /// 记录版本执行成功
    /// </summary>
    private static void RecordVersion(STOTOPDbContext ctx, string module, int version, string description, long elapsedMs)
    {
        ctx.Database.ExecuteSqlRaw(
            "INSERT INTO [SYS迁移历史] (F模块, F版本号, F描述, F状态, F耗时ms) VALUES ({0}, {1}, {2}, {3}, {4})",
            module, version, description, "Success", elapsedMs
        );
    }

    /// <summary>
    /// 校验迁移步骤列表的合法性
    /// </summary>
    private static void ValidateSteps(string module, List<MigrationStep> steps)
    {
        if (steps == null || steps.Count == 0)
            throw new InvalidOperationException($"[{module}] 迁移步骤列表不能为空");

        if (steps[0].Version != 1)
            throw new InvalidOperationException(
                $"[{module}] 迁移版本号必须从 1 开始，当前首个版本号为 {steps[0].Version}");

        for (int i = 0; i < steps.Count; i++)
        {
            var expectedVersion = i + 1;
            if (steps[i].Version != expectedVersion)
                throw new InvalidOperationException(
                    $"[{module}] 迁移版本号必须严格递增（每次+1），期望 V{expectedVersion}，实际为 V{steps[i].Version}");
        }

        var duplicates = steps.GroupBy(s => s.Version).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"[{module}] 存在重复的迁移版本号: {string.Join(", ", duplicates)}");
    }

    /// <summary>
    /// 获取数据库级排他锁 (sp_getapplock)，支持重试 + 随机退避
    /// </summary>
    private static void AcquireAppLock(SqlConnection connection, SqlTransaction transaction)
    {
        var maxRetries = _config.LockRetryCount;
        var baseDelay = _config.LockRetryBaseDelayMs;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            var result = TryGetAppLock(connection, transaction);

            if (result >= 0) return; // 0=正常授予, 1=在等待后授予

            if (result == -3) // 死锁不重试
            {
                throw new InvalidOperationException("获取迁移锁失败: 死锁");
            }

            if (attempt < maxRetries && result == -1) // 超时可重试
            {
                // 随机退避：baseDelay + random(0, baseDelay)
                var jitter = Random.Shared.Next(0, baseDelay);
                var delay = baseDelay + jitter;
                Console.WriteLine($"  [MigrationRunner] 获锁超时，第 {attempt + 1}/{maxRetries} 次重试，等待 {delay}ms...");
                Thread.Sleep(delay);
                continue;
            }

            // 超过重试次数或其他错误
            var message = result switch
            {
                -1 => $"锁请求超时（已重试 {maxRetries} 次）",
                -2 => "锁请求被取消",
                -999 => "参数验证或其他调用错误",
                _ => $"未知错误 (返回值: {result})"
            };
            throw new InvalidOperationException(
                $"获取数据库迁移锁失败: {message}。Resource='{LockResource}', Timeout={_config.LockTimeoutMs}ms");
        }
    }

    /// <summary>
    /// 单次尝试获取 applock，返回 sp_getapplock 的结果码
    /// </summary>
    private static int TryGetAppLock(SqlConnection connection, SqlTransaction transaction)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
DECLARE @result int;
EXEC @result = sp_getapplock 
    @Resource = @resource, 
    @LockMode = 'Exclusive', 
    @LockOwner = 'Transaction', 
    @LockTimeout = @timeout;
SELECT @result;";
        cmd.Parameters.AddWithValue("@resource", LockResource);
        cmd.Parameters.AddWithValue("@timeout", _config.LockTimeoutMs);
        return (int)cmd.ExecuteScalar()!;
    }

    /// <summary>
    /// 写入迁移执行日志（使用独立连接，不依赖主 DbContext）
    /// </summary>
    private static void AppendExecutionLog(string connectionString, string module, int version, string description, string status, string? errorMessage, long elapsedMs)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO [SYS迁移执行日志] (F模块, F版本号, F描述, F状态, F错误消息, F耗时ms, F实例标识)
VALUES (@module, @version, @desc, @status, @error, @elapsed, @instance)";
        cmd.Parameters.AddWithValue("@module", module);
        cmd.Parameters.AddWithValue("@version", version);
        cmd.Parameters.AddWithValue("@desc", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@error", (object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@elapsed", elapsedMs);
        cmd.Parameters.AddWithValue("@instance", _config.InstanceId);
        cmd.ExecuteNonQuery();
    }
}
