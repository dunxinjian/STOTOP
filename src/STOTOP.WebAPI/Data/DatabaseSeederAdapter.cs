using System.Data;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.WebAPI.Data.Seeders;

namespace STOTOP.WebAPI.Data;

/// <summary>
/// 数据库迁移器 - 统一调度各模块的版本化迁移
/// </summary>
public class DatabaseMigrator : IDatabaseSeeder
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseMigrator> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly string[] DefaultCriticalModules =
    [
        "System",
        "Finance",
        "Express",
        "BasicData",
        "Workflow",
        "CardFlow",
        "OA",
        "Menu"
    ];

    public DatabaseMigrator(
        IConfiguration configuration,
        ILogger<DatabaseMigrator> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    /// <summary>
    /// 排除列表中的表名前缀（与 SchemaAutoSync 保持一致）
    /// </summary>
    private static readonly string[] ExcludedPrefixes = { "STG", "HangFire", "__EF" };

    /// <summary>
    /// 排除的精确表名（与 SchemaAutoSync 保持一致）
    /// </summary>
    private static readonly HashSet<string> ExcludedTableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS迁移历史",
        "__EFMigrationsHistory",
        "SYS_Schema同步记录",
        "SYS基线数据同步记录"
    };

    private record BaselineDataCheck(string Name, string Sql, int MinCount);

    private static readonly BaselineDataCheck[] BaselineDataChecks =
    [
        new("组织类型", "IF OBJECT_ID(N'[dbo].[SYS组织类型]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[SYS组织类型]", 6),
        new("admin用户", "IF OBJECT_ID(N'[dbo].[SYS用户]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[SYS用户] WHERE [F账号] = N'admin'", 1),
        new("系统角色", "IF OBJECT_ID(N'[dbo].[SYS角色]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[SYS角色]", 1),
        new("功能权限", "IF OBJECT_ID(N'[dbo].[SYS功能权限]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[SYS功能权限]", 1),
        new("默认账套", "IF OBJECT_ID(N'[dbo].[FIN账套]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[FIN账套] WHERE [F是否默认] = 1", 1),
        new("快递品牌", "IF OBJECT_ID(N'[dbo].[EXP品牌]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[EXP品牌]", 5),
        new("省份基础数据", "IF OBJECT_ID(N'[dbo].[EXP省份]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[EXP省份]", 34),
        new("CardFlow插件注册", "IF OBJECT_ID(N'[dbo].[CF自动插件注册]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[CF自动插件注册]", 3),
        new("Workflow触发动作", "IF OBJECT_ID(N'[dbo].[WF触发动作]', N'U') IS NULL SELECT 0 AS Value ELSE SELECT COUNT(*) AS Value FROM [dbo].[WF触发动作]", 1),
    ];

    public void MigrateAll(STOTOPDbContext ctx)
    {
        var report = RunInitializationPipeline(ctx, strictRelationalArtifacts: false);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                $"数据库启动校验失败: {string.Join("; ", report.Issues)}");
        }
    }

    public DatabaseInitializationReport InitializeNewDatabase(STOTOPDbContext ctx)
    {
        return RunInitializationPipeline(ctx, strictRelationalArtifacts: true);
    }

    public DatabaseInitializationReport ValidateDatabase(STOTOPDbContext ctx)
    {
        var report = new DatabaseInitializationReport();
        ConfigureMigrationRunner(ctx);
        var modelTables = GetModelTables(ctx);
        ValidateBaseline(ctx, modelTables, report);
        return report;
    }

    private DatabaseInitializationReport RunInitializationPipeline(STOTOPDbContext ctx, bool strictRelationalArtifacts)
    {
        var report = new DatabaseInitializationReport();

        // 全局禁用开关：通过环境变量跳过整个迁移流程
        var skipMigration = Environment.GetEnvironmentVariable("SKIP_DB_MIGRATION");
        if (string.Equals(skipMigration, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skipMigration, "1", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("=== 数据库迁移已跳过 (SKIP_DB_MIGRATION=true) ===");
            report.Steps.Add("跳过数据库初始化/迁移 (SKIP_DB_MIGRATION)");
            return report;
        }

        Console.WriteLine("=== 数据库迁移开始 ===");

        ConfigureMigrationRunner(ctx);

        // 管线级排他锁：建表/SchemaAutoSync/索引补建/baseline 对齐均在 MigrationRunner 的
        // applock 范围之外，多实例同时启动（如 IIS 重叠回收）会在这些 DDL 上撞车。
        // Session 级 applock 随连接关闭自动释放。
        using var pipelineLock = AcquireInitializationLock(ctx);

        MigrationRunner.EnsureMigrationTable(ctx);
        report.Steps.Add("迁移历史表就绪");

        // 创建缺失的新表（EF Core 模型中存在但数据库中不存在的表）
        IReadOnlyList<ITable> modelTables;
        try
        {
            modelTables = CreateMissingTables(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CreateMissingTables] 新表创建失败，应用无法启动");
            throw new InvalidOperationException(
                $"数据库新表创建失败: {ex.Message}。后续 Seeder 可能依赖这些表。", ex);
        }
        report.Steps.Add("模型表结构已校验/补齐");

        // ExpressSchemaMigrationSeeder 已废弃删除，其 DDL 迁移逻辑由 SchemaSeeder + SchemaAutoSync 统一接管

        // Schema Auto-Sync：在 Seeder 之前自动同步模型列级差异
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var autoExecute = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
        var syncResult = SchemaAutoSync.Sync(ctx, autoExecute, _logger);
        report.Steps.Add("列级 SchemaAutoSync 已完成");

        // 开发环境：如果有变更执行失败，fail-fast
        if (autoExecute && syncResult.Warnings.Any(w => w.IsExecutionFailure))
        {
            var failedDetails = string.Join("\n", syncResult.Warnings
                .Where(w => w.IsExecutionFailure)
                .Select(w => $"  [{w.TableName}].[{w.ColumnName}]: {w.Message}"));
            _logger.LogError("[SchemaAutoSync] 以下列同步失败:\n{Details}", failedDetails);
            throw new InvalidOperationException(
                $"Schema Auto-Sync 存在 {syncResult.Warnings.Count(w => w.IsExecutionFailure)} 项列同步失败，应用无法启动。\n{failedDetails}");
        }

        var artifactFailureCount = CreateRelationalArtifacts(ctx, modelTables, _logger, strictRelationalArtifacts, report);
        report.Steps.Add("索引/唯一约束/外键已校验");

        // Tier 0: 基础层 — 失败则立即终止全部
        var tier0 = new (string Name, Action<STOTOPDbContext> Action)[]
        {
            ("System", ctx2 => SystemSeeder.Migrate(ctx2)),
        };

        // 业务模块：CriticalModules 中的模块失败会阻止启动，其余模块失败仅记录警告。
        var businessModules = new (string Name, Action<STOTOPDbContext> Action)[]
        {
            ("Finance", ctx2 => FinanceSeeder.Migrate(ctx2)),
            ("Express", ctx2 => ExpressSeeder.Migrate(ctx2)),
            ("OA", ctx2 => OASeeder.Migrate(ctx2)),
            ("Menu", ctx2 => MenuSeeder.Migrate(ctx2)),
            ("BasicData", ctx2 => BasicDataSeeder.Migrate(ctx2)),
            ("CRM", ctx2 => CrmSeeder.Migrate(ctx2)),
            ("Workflow", ctx2 => WorkflowSeeder.Migrate(ctx2)),
            ("CardFlow", ctx2 => CardFlowSeeder.Migrate(ctx2)),
            ("Points", ctx2 => PointsSeeder.Migrate(ctx2)),
            ("KSF", ctx2 => KsfSeeder.Migrate(ctx2)),
            ("PPV", ctx2 => PpvSeeder.Migrate(ctx2)),
            ("Salary", ctx2 => SalarySeeder.Migrate(ctx2)),
            ("CarrierQuality", ctx2 => CarrierQualitySeeder.Migrate(ctx2)),
        };

        IServiceScope? replacementScope = null;
        try
        {
            // --- 执行 Tier 0（fail-fast）---
            foreach (var (name, action) in tier0)
            {
                ExecuteWithRetry(name, action, ctx);
            }

            var criticalModules = new HashSet<string>(
                MigrationRunner.GetConfig().CriticalModules,
                StringComparer.OrdinalIgnoreCase);

            var criticalFailures = new List<string>();
            var optionalFailures = new List<string>();

            foreach (var (name, action) in businessModules)
            {
                try
                {
                    ExecuteWithRetry(name, action, ctx);
                }
                catch (Exception ex)
                {
                    if (criticalModules.Contains(name))
                    {
                        criticalFailures.Add(name);
                        _logger.LogError(ex, "[{Name}] 关键模块迁移失败", name);
                    }
                    else
                    {
                        optionalFailures.Add(name);
                        _logger.LogWarning(ex, "[{Name}] 可选模块迁移失败（不影响启动）", name);
                    }

                    ctx = RefreshContextIfClosed(ctx, ref replacementScope);
                }
            }

            // --- 判断是否允许启动 ---
            if (criticalFailures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"关键业务模块迁移失败: {string.Join(", ", criticalFailures)}，应用无法启动。" +
                    $"请检查 [SYS迁移执行日志] 表获取失败详情。");
            }

            if (optionalFailures.Count > 0)
            {
                _logger.LogWarning(
                    "以下可选模块迁移失败，应用继续启动: {Modules}",
                    string.Join(", ", optionalFailures));
            }

            // Seeder 可能修复了阻碍索引/约束创建的脏数据（如 CardFlow V19/V20 的重复节点键），
            // 对 Seeder 前补建失败的关系对象再补建一次，使其在同一次启动内自愈，
            // 不必等下一次重启才建上唯一索引。
            if (artifactFailureCount > 0)
            {
                _logger.LogInformation(
                    "[CreateRelationalArtifacts] Seeder 完成后重试补建 {Count} 项失败的关系对象",
                    artifactFailureCount);
                CreateRelationalArtifacts(ctx, modelTables, _logger, strictRelationalArtifacts, report);
                report.Steps.Add("Seeder 后关系对象重试补建已完成");
            }

            Console.WriteLine("=== 数据库迁移完成 ===");
            report.Steps.Add("基础数据 Seeder 已完成");

            // 以下步骤必须留在 try 块内：模块失败后 ctx 可能已被替换为 replacementScope
            // 中的实例，finally 一旦执行（Dispose 该 scope）ctx 即不可用。
            // 严格初始化（--init-database）强制对齐 baseline；常规启动按文件指纹跳过
            BaselineReferenceDataSeeder.Seed(ctx, force: strictRelationalArtifacts);
            OASeeder.PurgeRetiredReferenceData(ctx);
            report.Steps.Add("canonical baseline reference data 已对齐");

            ValidateBaseline(ctx, modelTables, report);
            if (!report.Success)
            {
                throw new InvalidOperationException(
                    $"数据库 baseline 校验失败: {string.Join("; ", report.Issues)}");
            }

            report.Steps.Add("baseline 校验通过");
            return report;
        }
        finally
        {
            replacementScope?.Dispose();
        }
    }

    /// <summary>
    /// 获取数据库初始化管线级排他锁（sp_getapplock, Session 级）。
    /// 返回持锁连接，由调用方 Dispose 释放（连接关闭即释放锁）；非 SQL Server 返回 null。
    /// </summary>
    private static IDisposable? AcquireInitializationLock(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return null;

        var connection = new SqlConnection(ctx.Database.GetConnectionString());
        try
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
DECLARE @result int;
EXEC @result = sp_getapplock
    @Resource = @resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = @timeout;
SELECT @result;";
            cmd.Parameters.AddWithValue("@resource", "STOTOP_InitPipeline");
            cmd.Parameters.AddWithValue("@timeout", MigrationRunner.GetConfig().LockTimeoutMs);
            var result = (int)cmd.ExecuteScalar()!;
            if (result < 0)
            {
                throw new InvalidOperationException(
                    $"获取数据库初始化管线锁失败 (sp_getapplock 返回 {result})，可能有其它实例正在执行迁移");
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void ConfigureMigrationRunner(STOTOPDbContext ctx)
    {
        var migrationSection = _configuration.GetSection("Database:Migration");
        var configuredCriticalModules = migrationSection.GetSection("CriticalModules").Get<string[]>();
        MigrationRunner.Configure(new MigrationRunner.MigrationConfig
        {
            LockTimeoutMs = migrationSection.GetValue("LockTimeoutMs", 60000),
            CommandTimeoutSeconds = migrationSection.GetValue("CommandTimeoutSeconds", 120),
            LockRetryCount = migrationSection.GetValue("LockRetryCount", 3),
            LockRetryBaseDelayMs = migrationSection.GetValue("LockRetryBaseDelayMs", 5000),
            SeederRetryCount = migrationSection.GetValue("SeederRetryCount", 2),
            SeederRetryDelayMs = migrationSection.GetValue("SeederRetryDelayMs", 3000),
            CriticalModules = configuredCriticalModules is { Length: > 0 }
                ? configuredCriticalModules
                : DefaultCriticalModules,
            InstanceId = Environment.MachineName
        });
        ctx.Database.SetCommandTimeout(MigrationRunner.GetConfig().CommandTimeoutSeconds);
    }

    /// <summary>
    /// 单模块迁移执行（带瞬态错误重试）
    /// </summary>
    private void ExecuteWithRetry(string name, Action<STOTOPDbContext> action, STOTOPDbContext primaryCtx)
    {
        var config = MigrationRunner.GetConfig();
        var maxAttempts = config.SeederRetryCount + 1;
        var baseDelay = config.SeederRetryDelayMs;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            IServiceScope? retryScope = null;
            STOTOPDbContext ctx;

            if (attempt == 1)
            {
                ctx = primaryCtx;
            }
            else
            {
                // 重试时创建全新 scope + DbContext，避免上一次失败的状态污染
                retryScope = _scopeFactory.CreateScope();
                ctx = retryScope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            }

            try
            {
                ctx.Database.SetCommandTimeout(config.CommandTimeoutSeconds);
                action(ctx);
                return; // 成功
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientError(ex))
            {
                var jitter = Random.Shared.Next(0, 1000);
                var delay = baseDelay * attempt + jitter;
                _logger.LogWarning(ex,
                    "[{Name}] 迁移遇到瞬态错误（尝试 {Attempt}/{Max}），{Delay}ms 后重试",
                    name, attempt, maxAttempts, delay);
                Thread.Sleep(delay);
            }
            finally
            {
                // Dispose 整个 scope（会同时 dispose 其内的 DbContext 及其他 scoped 服务）
                retryScope?.Dispose();
            }
        }
    }

    /// <summary>
    /// 判断异常是否为 SQL Server 瞬态错误（值得重试）
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        // 沿异常链向内查找：MigrationRunner 会把步骤异常包装成 InvalidOperationException，
        // 只看最外层会导致瞬态错误（死锁/超时）永远不被识别、重试机制失效
        for (var current = ex; current != null; current = current.InnerException)
        {
            // 仅识别 SQL Server 瞬态错误码
            if (current is SqlException sqlEx)
            {
                int[] transientCodes = [-2, 1205, -1, 40613, 40197, 40501, 49918, 49919, 49920];
                if (sqlEx.Errors.Cast<SqlError>().Any(e => transientCodes.Contains(e.Number)))
                    return true;
            }

            if (current is TimeoutException) return true;
        }

        return false;
        // 注意：不包含"锁请求超时"（InvalidOperationException，无 SqlException 内层）！
        // 获锁重试由 MigrationRunner.AcquireAppLock 内部处理，
        // 外层不再重复重试，避免 3*3=9 次叠加导致 10 分钟启动超时
    }

    private STOTOPDbContext RefreshContextIfClosed(STOTOPDbContext ctx, ref IServiceScope? replacementScope)
    {
        if (ctx.Database.GetDbConnection().State == ConnectionState.Open)
        {
            return ctx;
        }

        replacementScope?.Dispose();
        replacementScope = _scopeFactory.CreateScope();
        var freshCtx = replacementScope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        freshCtx.Database.SetCommandTimeout(MigrationRunner.GetConfig().CommandTimeoutSeconds);
        return freshCtx;
    }

    #region 创建缺失表

    private static IReadOnlyList<ITable> GetModelTables(STOTOPDbContext ctx)
    {
        IModel model;
        try
        {
            model = ctx.GetService<IDesignTimeModel>().Model;
        }
        catch
        {
            model = ctx.Model;
        }

        return model.GetRelationalModel()
            .Tables
            .Where(t => !IsExcludedTable(t.Name))
            .ToList();
    }

    private static void ValidateBaseline(
        STOTOPDbContext ctx,
        IReadOnlyList<ITable> modelTables,
        DatabaseInitializationReport report)
    {
        var existingTables = GetExistingTableNames(ctx);
        foreach (var table in modelTables)
        {
            if (!existingTables.Contains(table.Name))
            {
                report.Issues.Add($"缺少模型表 [{table.Name}]");
            }
        }

        foreach (var check in BaselineDataChecks)
        {
            var count = ExecuteScalarInt(ctx, check.Sql);
            if (count < check.MinCount)
            {
                report.Issues.Add($"{check.Name} baseline 数据不足: {count}/{check.MinCount}");
            }
        }
    }

    private static int ExecuteScalarInt(STOTOPDbContext ctx, string sql)
    {
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = MigrationRunner.GetConfig().CommandTimeoutSeconds;
        command.Transaction = ctx.Database.CurrentTransaction?.GetDbTransaction();
        var value = command.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    /// <summary>
    /// 检测并创建 EF Core 模型中存在但数据库中不存在的表
    /// </summary>
    private static IReadOnlyList<ITable> CreateMissingTables(STOTOPDbContext ctx)
    {
        // 1. 从 EF Core 模型获取所有表（排除 STG/HangFire/__EF 前缀及特殊表）
        //    先尝试 IDesignTimeModel（完整元数据），失败时回退到 ctx.Model
        IModel model;
        try
        {
            model = ctx.GetService<IDesignTimeModel>().Model;
            Console.WriteLine("[CreateMissingTables] 使用 IDesignTimeModel 获取模型");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateMissingTables] IDesignTimeModel 获取失败({ex.Message})，回退到 ctx.Model");
            model = ctx.Model;
        }

        var relationalModel = model.GetRelationalModel();
        var modelTables = new Dictionary<string, ITable>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in relationalModel.Tables)
        {
            if (IsExcludedTable(table.Name)) continue;
            modelTables[table.Name] = table;
        }

        Console.WriteLine($"[CreateMissingTables] 关系模型中共检测到 {modelTables.Count} 张非排除表");

        // 1b. 补充检测：通过 ctx.Model.GetEntityTypes() 发现可能遗漏的表
        //     （防止 IRelationalModel 未包含某些表的情况）
        var entityTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entityType in ctx.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (!string.IsNullOrEmpty(tableName) && !IsExcludedTable(tableName))
            {
                entityTableNames.Add(tableName);
                if (!modelTables.ContainsKey(tableName))
                {
                    Console.WriteLine($"[CreateMissingTables] ⚠ 实体 {entityType.ClrType.Name} 的表 [{tableName}] 不在关系模型中，将补充检测");
                }
            }
        }
        Console.WriteLine($"[CreateMissingTables] 实体类型映射到 {entityTableNames.Count} 张非排除表");

        // 2. 查询数据库中已存在的表名
        var existingTables = GetExistingTableNames(ctx);

        // 3. 计算差集：模型中有但数据库中没有的表
        var missingTableNames = modelTables.Keys
            .Where(t => !existingTables.Contains(t))
            .OrderBy(t => t)
            .ToList();

        // 3b. 检查有哪些表只在 entityTableNames 中存在但不在 relationalModel 且不在数据库中
        var orphanMissingTables = entityTableNames
            .Where(t => !modelTables.ContainsKey(t) && !existingTables.Contains(t))
            .OrderBy(t => t)
            .ToList();

        if (orphanMissingTables.Count > 0)
        {
            Console.WriteLine($"[CreateMissingTables] ⚠ 发现 {orphanMissingTables.Count} 张表仅在实体类型中存在但不在关系模型中: {string.Join(", ", orphanMissingTables)}");
            Console.WriteLine("[CreateMissingTables] 尝试通过实体类型元数据生成 CREATE TABLE...");
        }

        var tableList = modelTables.Values.ToList();

        if (missingTableNames.Count == 0 && orphanMissingTables.Count == 0)
        {
            Console.WriteLine("[CreateMissingTables] 所有模型表均已存在，无需创建");
            return tableList;
        }

        // 4. 逐表生成并执行 CREATE TABLE（关系模型中的缺失表）
        var failures = new List<string>();
        var created = 0;
        foreach (var tableName in missingTableNames)
        {
            var table = modelTables[tableName];
            try
            {
                var sql = GenerateCreateTableSql(table);
                ctx.Database.ExecuteSqlRaw(sql);
                Console.WriteLine($"  [Created] {tableName}");
                created++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [Failed] {tableName}: {ex.Message}");
                failures.Add($"{tableName}: {ex.Message}");
            }
        }

        // 5. 对 orphan 缺失表，通过实体类型元数据生成 CREATE TABLE
        foreach (var tableName in orphanMissingTables)
        {
            try
            {
                var entityType = ctx.Model.GetEntityTypes()
                    .First(e => string.Equals(e.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));
                var sql = GenerateCreateTableFromEntityType(entityType, tableName);
                ctx.Database.ExecuteSqlRaw(sql);
                Console.WriteLine($"  [Created via EntityType] {tableName}");
                created++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [Failed via EntityType] {tableName}: {ex.Message}");
                failures.Add($"{tableName}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"CreateMissingTables 有 {failures.Count} 张表创建失败: {string.Join("; ", failures)}");
        }

        Console.WriteLine($"[CreateMissingTables] 完成：成功 {created} 张，失败 0 张");
        return tableList;
    }

    /// <summary>
    /// 查询数据库中 dbo schema 下已存在的所有表名
    /// </summary>
    private static HashSet<string> GetExistingTableNames(STOTOPDbContext ctx)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo'";

        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        var wasOpen = ctx.Database.GetDbConnection().State == System.Data.ConnectionState.Open;
        if (!wasOpen) ctx.Database.GetDbConnection().Open();

        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (!wasOpen) ctx.Database.GetDbConnection().Close();
        }

        return result;
    }

    /// <summary>
    /// 根据 EF Core 关系模型的 ITable 元数据生成 CREATE TABLE SQL
    /// </summary>
    private static string GenerateCreateTableSql(ITable table)
    {
        var sb = new StringBuilder();
        // IF 守卫：调用方的存在性检查与执行之间有时间窗，多实例并发启动时另一实例可能已建表
        sb.AppendLine($"IF OBJECT_ID(N'{SqlStringLiteral(GetFullTableName(table))}', N'U') IS NULL");
        sb.AppendLine("BEGIN");
        sb.AppendLine($"CREATE TABLE [{table.Name}] (");
    
        var columns = table.Columns.ToList();
        var pkColumns = table.PrimaryKey?.Columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var colSql = GenerateColumnDefinition(col, pkColumns);
            sb.Append($"    {colSql}");
            if (i < columns.Count - 1 || pkColumns.Count > 0)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }
    
        // PRIMARY KEY 约束
        if (pkColumns.Count > 0 && table.PrimaryKey != null)
        {
            var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(c => $"[{c.Name}]"));
            sb.AppendLine($"    CONSTRAINT [PK_{table.Name}] PRIMARY KEY ({pkCols})");
        }

        sb.AppendLine(");");
        sb.Append("END");
        return sb.ToString();
    }

    /// <summary>
    /// 为启动时补建的表补齐 EF 关系模型中的唯一约束、外键和索引。
    /// 返回补建失败的对象数（非严格模式下调用方可在 Seeder 修复数据后重试）。
    /// </summary>
    private static int CreateRelationalArtifacts(
        STOTOPDbContext ctx,
        IEnumerable<ITable> tables,
        ILogger logger,
        bool failOnError,
        DatabaseInitializationReport report)
    {
        var tableList = tables.ToList();

        var failures = new List<string>();

        // 先把库中已存在的约束/外键/索引名一次性读进内存，只对缺失对象发 DDL。
        // 稳态启动下模型有上千个关系对象，逐条 IF NOT EXISTS 往返曾占启动耗时的 80% 以上。
        var existingArtifacts = QueryExistingArtifactNames(ctx);
        var skipped = 0;
        var executed = 0;

        foreach (var table in tableList)
        {
            foreach (var uniqueConstraint in table.UniqueConstraints
                .Where(c => !string.Equals(c.Name, table.PrimaryKey?.Name, StringComparison.OrdinalIgnoreCase)))
            {
                if (existingArtifacts.Contains(ArtifactKey(table, uniqueConstraint.Name)))
                {
                    skipped++;
                    continue;
                }

                executed++;
                ExecuteArtifactSql(
                    ctx,
                    GenerateUniqueConstraintSql(table, uniqueConstraint),
                    $"唯一约束 {uniqueConstraint.Name}",
                    failures);
            }
        }

        foreach (var table in tableList)
        {
            foreach (var foreignKey in table.ForeignKeyConstraints)
            {
                if (existingArtifacts.Contains(ArtifactKey(table, foreignKey.Name)))
                {
                    skipped++;
                    continue;
                }

                executed++;
                ExecuteArtifactSql(
                    ctx,
                    GenerateForeignKeySql(table, foreignKey),
                    $"外键 {foreignKey.Name}",
                    failures);
            }
        }

        foreach (var table in tableList)
        {
            foreach (var index in table.Indexes)
            {
                if (existingArtifacts.Contains(ArtifactKey(table, index.Name)))
                {
                    skipped++;
                    continue;
                }

                executed++;
                ExecuteArtifactSql(
                    ctx,
                    GenerateIndexSql(table, index),
                    $"索引 {index.Name}",
                    failures);
            }
        }

        if (failures.Count > 0)
        {
            var message = $"关系对象补建失败 {failures.Count} 项: {string.Join("; ", failures.Take(20))}";
            if (failOnError)
            {
                report.Issues.Add(message);
                throw new InvalidOperationException(message);
            }

            logger.LogWarning(
                "[CreateRelationalArtifacts] {Message}",
                message);
            return failures.Count;
        }

        Console.WriteLine($"[CreateMissingTables] 已校验 {tableList.Count} 张模型表的关系对象（已存在 {skipped} 项，补建 {executed} 项）");
        return 0;
    }

    /// <summary>
    /// 一次性读取库中已存在的键约束、外键和索引名（按 schema.表::对象名 组合键）。
    /// </summary>
    private static HashSet<string> QueryExistingArtifactNames(STOTOPDbContext ctx)
    {
        const string sql = @"
SELECT s.name AS SchemaName, t.name AS TableName, kc.name AS ArtifactName
FROM sys.key_constraints kc
JOIN sys.tables t ON kc.parent_object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
UNION ALL
SELECT s.name, t.name, fk.name
FROM sys.foreign_keys fk
JOIN sys.tables t ON fk.parent_object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
UNION ALL
SELECT s.name, t.name, i.name
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE i.name IS NOT NULL";

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = ctx.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = MigrationRunner.GetConfig().CommandTimeoutSeconds;
            command.Transaction = ctx.Database.CurrentTransaction?.GetDbTransaction();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add($"{reader.GetString(0)}.{reader.GetString(1)}::{reader.GetString(2)}");
            }
        }
        finally
        {
            if (!wasOpen) connection.Close();
        }

        return result;
    }

    private static string ArtifactKey(ITable table, string artifactName)
    {
        var schema = string.IsNullOrWhiteSpace(table.Schema) ? "dbo" : table.Schema;
        return $"{schema}.{table.Name}::{artifactName}";
    }

    private static void ExecuteArtifactSql(STOTOPDbContext ctx, string sql, string description, List<string> failures)
    {
        try
        {
            ctx.Database.ExecuteSqlRaw(sql);
        }
        catch (Exception ex)
        {
            failures.Add($"{description}: {ex.Message}");
        }
    }

    private static string GenerateUniqueConstraintSql(ITable table, IUniqueConstraint uniqueConstraint)
    {
        var fullTableName = GetFullTableName(table);
        var columns = JoinColumnNames(uniqueConstraint.Columns);
        return $@"
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'{SqlStringLiteral(fullTableName)}') AND name = N'{SqlStringLiteral(uniqueConstraint.Name)}')
BEGIN
    ALTER TABLE {fullTableName} ADD CONSTRAINT {QuoteIdentifier(uniqueConstraint.Name)} UNIQUE ({columns});
END";
    }

    private static string GenerateForeignKeySql(ITable table, IForeignKeyConstraint foreignKey)
    {
        var fullTableName = GetFullTableName(table);
        var principalTableName = GetFullTableName(foreignKey.PrincipalTable);
        var columns = JoinColumnNames(foreignKey.Columns);
        var principalColumns = JoinColumnNames(foreignKey.PrincipalColumns);
        var onDelete = foreignKey.OnDeleteAction.ToString() switch
        {
            "Cascade" => " ON DELETE CASCADE",
            "SetNull" => " ON DELETE SET NULL",
            "SetDefault" => " ON DELETE SET DEFAULT",
            _ => ""
        };

        return $@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'{SqlStringLiteral(fullTableName)}') AND name = N'{SqlStringLiteral(foreignKey.Name)}')
BEGIN
    ALTER TABLE {fullTableName} ADD CONSTRAINT {QuoteIdentifier(foreignKey.Name)} FOREIGN KEY ({columns}) REFERENCES {principalTableName} ({principalColumns}){onDelete};
END";
    }

    private static string GenerateIndexSql(ITable table, ITableIndex index)
    {
        var fullTableName = GetFullTableName(table);
        var unique = index.IsUnique ? "UNIQUE " : "";
        var columns = JoinColumnNames(index.Columns);
        var filter = string.IsNullOrWhiteSpace(index.Filter) ? "" : $" WHERE {index.Filter}";
        return $@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'{SqlStringLiteral(fullTableName)}') AND name = N'{SqlStringLiteral(index.Name)}')
BEGIN
    CREATE {unique}INDEX {QuoteIdentifier(index.Name)} ON {fullTableName} ({columns}){filter};
END";
    }

    private static string GetFullTableName(ITable table)
    {
        return string.IsNullOrWhiteSpace(table.Schema)
            ? QuoteIdentifier(table.Name)
            : $"{QuoteIdentifier(table.Schema)}.{QuoteIdentifier(table.Name)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"[{identifier.Replace("]", "]]")}]";
    }

    private static string JoinColumnNames(IEnumerable<IColumn> columns)
    {
        return string.Join(", ", columns.Select(c => QuoteIdentifier(c.Name)));
    }

    private static string SqlStringLiteral(string value)
    {
        return value.Replace("'", "''");
    }
    
    /// <summary>
    /// 回退方案：通过 IEntityType 元数据生成 CREATE TABLE SQL
    /// （当实体不在 IRelationalModel.Tables 中时使用）
    /// </summary>
    private static string GenerateCreateTableFromEntityType(IEntityType entityType, string tableName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'{tableName}')");
        sb.AppendLine("BEGIN");
        sb.AppendLine($"    CREATE TABLE [{tableName}] (");
    
        var properties = entityType.GetProperties().ToList();
        var pk = entityType.FindPrimaryKey();
        var pkProperties = pk?.Properties.Select(p => p.GetColumnName()).ToHashSet(StringComparer.OrdinalIgnoreCase)
                           ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            var colName = prop.GetColumnName();
            var storeType = prop.GetColumnType() ?? GetDefaultStoreType(prop);
            var isNullable = prop.IsNullable;
            var isPk = pkProperties.Contains(colName);
    
            sb.Append($"        [{colName}] {storeType}");
    
            // IDENTITY：以 EF 的有效值生成策略为准（与 IsIdentityColumn 同理）
            if (SqlServerPropertyExtensions.GetValueGenerationStrategy(prop) == SqlServerValueGenerationStrategy.IdentityColumn)
            {
                sb.Append(" IDENTITY(1,1)");
            }
    
            sb.Append(!isNullable || isPk ? " NOT NULL" : " NULL");
    
            // DEFAULT
            var defaultSql = prop.GetDefaultValueSql();
            if (defaultSql == null)
            {
                var defaultValue = prop.GetDefaultValue();
                if (defaultValue != null)
                    defaultSql = SeederHelper.ConvertDefaultValueToSql(defaultValue);
            }
            if (!string.IsNullOrEmpty(defaultSql))
                sb.Append($" DEFAULT {defaultSql}");
    
            if (i < properties.Count - 1 || pkProperties.Count > 0)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }
    
        if (pk != null && pkProperties.Count > 0)
        {
            var pkCols = string.Join(", ", pk.Properties.Select(p => $"[{p.GetColumnName()}]"));
            sb.AppendLine($"        CONSTRAINT [PK_{tableName}] PRIMARY KEY ({pkCols})");
        }
    
        sb.AppendLine("    );");
        sb.AppendLine("END");
        return sb.ToString();
    }
    
    /// <summary>
    /// 为属性推断默认的 SQL Server 存储类型
    /// </summary>
    private static string GetDefaultStoreType(IProperty prop)
    {
        var clrType = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
        if (clrType == typeof(long)) return "bigint";
        if (clrType == typeof(int)) return "int";
        if (clrType == typeof(short)) return "smallint";
        if (clrType == typeof(byte)) return "tinyint";
        if (clrType == typeof(bool)) return "bit";
        if (clrType == typeof(decimal)) return "decimal(18,2)";
        if (clrType == typeof(double)) return "float";
        if (clrType == typeof(float)) return "real";
        if (clrType == typeof(DateTime)) return "datetime2";
        if (clrType == typeof(DateTimeOffset)) return "datetimeoffset";
        if (clrType == typeof(Guid)) return "uniqueidentifier";
        if (clrType == typeof(string))
        {
            var maxLen = prop.GetMaxLength();
            return maxLen.HasValue ? $"nvarchar({maxLen.Value})" : "nvarchar(max)";
        }
        if (clrType == typeof(byte[])) return "varbinary(max)";
        return "nvarchar(max)";
    }

    /// <summary>
    /// 生成单列的 DDL 定义
    /// </summary>
    private static string GenerateColumnDefinition(IColumn column, HashSet<string> pkColumns)
    {
        var sb = new StringBuilder();
        sb.Append($"[{column.Name}] {column.StoreType}");

        // 检测 IDENTITY
        if (IsIdentityColumn(column))
        {
            sb.Append(" IDENTITY(1,1)");
        }

        // NULL / NOT NULL
        if (!column.IsNullable || pkColumns.Contains(column.Name))
            sb.Append(" NOT NULL");
        else
            sb.Append(" NULL");

        // DEFAULT 值
        var defaultSql = column.DefaultValueSql;
        if (defaultSql == null && column.DefaultValue != null)
        {
            defaultSql = SeederHelper.ConvertDefaultValueToSql(column.DefaultValue);
        }
        if (!string.IsNullOrEmpty(defaultSql))
        {
            sb.Append($" DEFAULT {defaultSql}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 检测列是否为 IDENTITY 自增列。
    /// 直接询问 EF 的有效值生成策略，而不是"OnAdd + 整数 ≈ IDENTITY"的启发式——
    /// 后者会把应用层生成 ID（雪花等）的列错误建成 IDENTITY，导致显式插入失败。
    /// </summary>
    private static bool IsIdentityColumn(IColumn column)
    {
        var prop = column.PropertyMappings.FirstOrDefault()?.Property;
        // 显式静态调用：项目同时引用 Sqlite/SqlServer provider，扩展方法二义
        return prop != null
            && SqlServerPropertyExtensions.GetValueGenerationStrategy(prop) == SqlServerValueGenerationStrategy.IdentityColumn;
    }

    /// <summary>
    /// 判断表名是否在排除列表中（与 SchemaAutoSync 逻辑一致）
    /// </summary>
    private static bool IsExcludedTable(string tableName)
    {
        if (ExcludedTableNames.Contains(tableName)) return true;

        foreach (var prefix in ExcludedPrefixes)
        {
            if (tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    #endregion
}
