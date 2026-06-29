using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Data;
using Hangfire;
using Hangfire.SqlServer;

namespace STOTOP.Module.System.Services;

public class DatabaseService : IDatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseService> _logger;
    private readonly IDatabaseSeeder? _databaseSeeder;
    private readonly IDynamicDbContextFactory _dynamicDbContextFactory;

    public DatabaseService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<DatabaseService> logger,
        IDynamicDbContextFactory dynamicDbContextFactory,
        IDatabaseSeeder? databaseSeeder = null)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _databaseSeeder = databaseSeeder;
        _dynamicDbContextFactory = dynamicDbContextFactory;
    }

    public Task<DatabaseStatusResult> GetDatabaseStatusAsync()
    {
        var provider = DbConnectionsHelper.GetProvider();
        var initialized = DbConnectionsHelper.IsInitialized();

        return Task.FromResult(new DatabaseStatusResult
        {
            Provider = provider,
            Initialized = initialized,
            Connected = initialized
        });
    }

    public async Task<TestConnectionResult> TestConnectionAsync(string provider, string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return new TestConnectionResult { Success = true, Message = "SqlServer 连接成功" };
        }
        catch (SqlException ex) when (ex.Number == 18456 ||
            ex.Message.Contains("登录失败", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "数据库登录失败");
            return new TestConnectionResult { Success = false, Message = "用户名或密码错误" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试数据库连接失败");
            return new TestConnectionResult { Success = false, Message = $"SqlServer 连接失败: {ex.Message}" };
        }
    }

    public async Task<InitializeResult> InitializeDatabaseAsync(string? provider = null, string? connectionString = null)
    {
        try
        {
            var targetProvider = provider ?? "SqlServer";
            var targetConnectionString = connectionString ?? DbConnectionsHelper.GetSystemConnectionString(_configuration.GetValue<string>("Security:EncryptionKey"));

            // 先测试连接
            var testResult = await TestConnectionAsync(targetProvider, targetConnectionString ?? "");
            if (!testResult.Success)
            {
                return new InitializeResult { Success = false, Message = testResult.Message };
            }

            // 使用动态工厂创建目标数据库的 DbContext（绕过 DI 缓存的 Options）
            using var dbContext = _dynamicDbContextFactory.CreateDbContext(targetProvider, targetConnectionString);

            // 先删除旧数据库中的所有表（避免需要 CREATE DATABASE 权限）
            _logger.LogInformation("正在清理旧数据库...");
            await DropAllTablesAsync(dbContext, targetProvider);

            // 再创建完整的数据库结构
            // 注意：不能用 EnsureCreatedAsync()，因为数据库本身已存在时它不会创建表
            _logger.LogInformation("正在使用 {Provider} 提供者初始化数据库...", targetProvider);
            var databaseCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
            await databaseCreator.CreateTablesAsync();
            _logger.LogInformation("数据库表创建完成");

            // 显式重建 Hangfire 表（DropAllTablesAsync 会删除 hangfire schema 下的表）
            await EnsureHangfireTablesAsync(targetConnectionString ?? "");
            
            // 初始化成功后执行种子数据填充
            if (_databaseSeeder != null)
            {
                var report = _databaseSeeder.InitializeNewDatabase(dbContext);
                _logger.LogInformation("数据库 baseline 初始化完成: {Steps}", string.Join(" -> ", report.Steps));
            }

            // 初始化成功，保存连接信息到 db-connections.json
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                await UpdateConfigAsync(targetProvider, targetConnectionString!);
            }

            return new InitializeResult { Success = true, Message = "数据库初始化成功，已完成结构、基础数据和 baseline 校验" };
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "无内部异常";
            _logger.LogError(ex, "数据库初始化失败. 内部异常: {InnerException}", innerMsg);
            return new InitializeResult { Success = false, Message = $"初始化失败: {ex.Message} | 内部异常: {innerMsg}" };
        }
    }

    /// <summary>
    /// 删除数据库中的所有视图、外键约束和表，避免使用 EnsureDeleted 导致需要 CREATE DATABASE 权限
    /// </summary>
    private async Task DropAllTablesAsync(DbContext dbContext, string provider)
    {
        // SQL Server: 先删视图，再删外键，最后删表
        var dropSql = @"
            -- 删除所有视图
            DECLARE @viewSql NVARCHAR(MAX) = N'';
            SELECT @viewSql += N'DROP VIEW ' + QUOTENAME(s.name) + '.' + QUOTENAME(v.name) + ';' + CHAR(13)
            FROM sys.views v JOIN sys.schemas s ON v.schema_id = s.schema_id;
            EXEC sp_executesql @viewSql;

            -- 删除所有外键约束
            DECLARE @fkSql NVARCHAR(MAX) = N'';
            SELECT @fkSql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' + CHAR(13)
            FROM sys.foreign_keys;
            EXEC sp_executesql @fkSql;

            -- 删除所有表
            DECLARE @tblSql NVARCHAR(MAX) = N'';
            SELECT @tblSql += N'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(13)
            FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id;
            EXEC sp_executesql @tblSql;
        ";
        await dbContext.Database.ExecuteSqlRawAsync(dropSql);
    }

    public Task<DatabaseConfigResult> GetConfigAsync()
    {
        var provider = DbConnectionsHelper.GetProvider();
        var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
        var connectionString = DbConnectionsHelper.GetSystemConnectionString(encryptionKey) ?? "";

        // 对密码进行脱敏处理
        var maskedConnectionString = MaskPassword(connectionString);

        return Task.FromResult(new DatabaseConfigResult
        {
            Provider = provider,
            ConnectionString = maskedConnectionString
        });
    }

    public Task<UpdateConfigResult> UpdateConfigAsync(string provider, string connectionString)
    {
        try
        {
            var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
            DbConnectionsHelper.SaveSystemConnectionFromConnectionString(connectionString, provider, encryptionKey);

            return Task.FromResult(new UpdateConfigResult 
            { 
                Success = true, 
                Message = "配置更新成功" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新数据库配置失败");
            return Task.FromResult(new UpdateConfigResult { Success = false, Message = $"更新失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 分析指定连接字符串的数据库表状态
    /// </summary>
    public async Task<DatabaseAnalysisResult> AnalyzeDatabaseAsync(string connectionString)
    {
        var result = new DatabaseAnalysisResult();

        // 1. 用 DynamicDbContextFactory 创建临时 DbContext，获取系统表名
        using var tempContext = _dynamicDbContextFactory.CreateDbContext("SqlServer", connectionString);
        var systemTableNames = tempContext.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(n => n != null)
            .Cast<string>()
            .ToHashSet();

        // 2. 用 ADO.NET 查询目标数据库的实际表和行数（扫描所有 schema）
        var actualTables = new List<(string Schema, string TableName, long RowCount)>();
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            var sql = @"
                SELECT s.name AS SchemaName, t.name AS TableName, ISNULL(SUM(p.rows), 0) AS [RowCount]
                FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
                GROUP BY s.name, t.name";
            using var cmd = new SqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schemaName = reader.GetString(0);
                var tableName = reader.GetString(1);
                var rowCount = reader.GetInt64(2);

                // hangfire / elsa schema 下的表收集到专属字段，不参与 dbo 系统表分类
                if (string.Equals(schemaName, "hangfire", StringComparison.OrdinalIgnoreCase))
                {
                    result.HangfireTables.Add(new TableInfo { TableName = tableName, SchemaName = schemaName, RowCount = rowCount });
                    continue;
                }
                if (string.Equals(schemaName, "elsa", StringComparison.OrdinalIgnoreCase))
                {
                    // Elsa 已移除，跳过残留表
                    continue;
                }

                actualTables.Add((schemaName, tableName, rowCount));
            }
        }

        result.HasExistingTables = actualTables.Count > 0;

        // 3. 分类比对
        // EF 模型的表全部在 dbo schema 下，只用 dbo 的表与 systemTableNames 匹配
        var matchedSystemTableNames = new HashSet<string>();
        foreach (var (schema, tableName, rowCount) in actualTables)
        {
            if (string.Equals(schema, "dbo", StringComparison.OrdinalIgnoreCase) && systemTableNames.Contains(tableName))
            {
                result.ExistingSystemTables.Add(new TableInfo { TableName = tableName, SchemaName = schema, RowCount = rowCount });
                matchedSystemTableNames.Add(tableName);
            }
        }

        // 未在数据库中找到的系统表归入 MissingSystemTables
        foreach (var tableName in systemTableNames)
        {
            if (!matchedSystemTableNames.Contains(tableName))
            {
                result.MissingSystemTables.Add(tableName);
            }
        }

        // 非 dbo 系统表的其余表归入 ForeignTables
        foreach (var (schema, tableName, rowCount) in actualTables)
        {
            var isDboSystemTable = string.Equals(schema, "dbo", StringComparison.OrdinalIgnoreCase) && systemTableNames.Contains(tableName);
            if (!isDboSystemTable)
            {
                result.ForeignTables.Add(new TableInfo { TableName = tableName, SchemaName = schema, RowCount = rowCount });
            }
        }

        return result;
    }

    /// <summary>
    /// 全新初始化指定数据库
    /// </summary>
    public async Task FullInitializeAsync(string connectionString)
    {
        _logger.LogInformation("开始全新初始化数据库...");

        try
        {
        using var tempContext = _dynamicDbContextFactory.CreateDbContext("SqlServer", connectionString);

        // 1. 删除所有表
        _logger.LogInformation("正在清理旧数据库...");
        await DropAllTablesAsync(tempContext, "SqlServer");

        // 2. 创建全部表（不能用 EnsureCreatedAsync，数据库已存在时它不会创建表）
        _logger.LogInformation("正在创建数据库结构...");
        var databaseCreator = tempContext.Database.GetService<IRelationalDatabaseCreator>();
        await databaseCreator.CreateTablesAsync();

        // 显式重建 Hangfire 表（DropAllTablesAsync 会删除 hangfire schema 下的表）
        await EnsureHangfireTablesAsync(connectionString);

        // 3. 填充种子数据
        if (_databaseSeeder != null)
        {
            _logger.LogInformation("正在执行数据库 baseline 初始化...");
            var report = _databaseSeeder.InitializeNewDatabase(tempContext);
            _logger.LogInformation("数据库 baseline 初始化完成: {Steps}", string.Join(" -> ", report.Steps));
        }

        // 4. 更新连接信息到 db-connections.json
        await UpdateConfigAsync("SqlServer", connectionString);

        _logger.LogInformation("全新初始化数据库完成");
        }
        catch (SqlException ex)
        {
            ThrowIfLoginFailed(ex);
            throw;
        }
    }

    /// <summary>
    /// 保留初始化指定数据库
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableActions">表处理方式字典</param>
    /// <param name="backupPath">备份目录（SQL Server 服务器上的路径），为空则跳过备份</param>
    /// <param name="onProgress">进度回调 (stepName, status, currentStep, totalSteps)</param>
    public async Task PreserveInitializeAsync(
        string connectionString,
        Dictionary<string, string> tableActions,
        string? backupPath = null,
        Func<string, string, int, int, Task>? onProgress = null)
    {
        _logger.LogInformation("开始保留初始化数据库...");
    
        // 确定总步数：备份（可选）、分析、禁用约束、删除非系统表、清理外键、清理Hangfire、处理系统表、创建表结构、恢复数据、填充种子数据
        int totalSteps = string.IsNullOrWhiteSpace(backupPath) ? 9 : 10;
        int step = 0;
    
        // 步骤1: 备份（可选）
        if (!string.IsNullOrWhiteSpace(backupPath))
        {
            step++;
            if (onProgress != null) await onProgress("数据库备份", "processing", step, totalSteps);
            try
            {
                using var backupConn = new SqlConnection(connectionString);
                await backupConn.OpenAsync();
                var dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFile = $"{backupPath.TrimEnd('\\', '/')}/{dbName}_preserve_{ts}.bak";
                var backupSql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{backupFile}' WITH FORMAT, INIT, COMPRESSION";
                using var backupCmd = new SqlCommand(backupSql, backupConn) { CommandTimeout = 600 };
                await backupCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("数据库备份完成: {BackupFile}", backupFile);
                if (onProgress != null) await onProgress("数据库备份", "completed", step, totalSteps);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "数据库备份失败，继续执行初始化");
                if (onProgress != null) await onProgress("数据库备份", "warning", step, totalSteps);
            }
        }
    
        // 步骤2: 分析数据库
        step++;
        if (onProgress != null) await onProgress("分析数据库", "processing", step, totalSteps);
        var analysis = await AnalyzeDatabaseAsync(connectionString);
        if (onProgress != null) await onProgress("分析数据库", "completed", step, totalSteps);
    
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
    
        var preservedTables = new List<string>();
        bool creationPhaseReached = false;
    
        try
        {
            // 步骤3: 禁用外键约束
            step++;
            if (onProgress != null) await onProgress("禁用约束", "processing", step, totalSteps);
            _logger.LogInformation("禁用所有外键约束...");
            await DisableForeignKeysAsync(connection);
            if (onProgress != null) await onProgress("禁用约束", "completed", step, totalSteps);
    
            // 步骤4: 删除所有非系统表
            step++;
            if (onProgress != null) await onProgress("删除非系统表", "processing", step, totalSteps);
            _logger.LogInformation("删除非系统表...");
            foreach (var foreignTable in analysis.ForeignTables)
            {
                var fullName = $"{QuoteIdentifier(foreignTable.SchemaName)}.{QuoteIdentifier(foreignTable.TableName)}";
                await ExecuteNonQueryAsync(connection, $"DROP TABLE {fullName}");
            }
    
            // 验证非系统表已删除
            if (analysis.ForeignTables.Count > 0)
            {
                var foreignTableNames = string.Join(",", analysis.ForeignTables
                    .Select(t => $"'{t.TableName.Replace("'", "''")}'"));
                var checkSql = $@"
                    SELECT COUNT(*) FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE t.name IN ({foreignTableNames})";
                var remainingForeignCount = await ExecuteScalarAsync<int>(connection, checkSql);
                if (remainingForeignCount > 0)
                    _logger.LogWarning("删除非系统表后仍有 {Count} 张非系统表残留", remainingForeignCount);
            }
            if (onProgress != null) await onProgress("删除非系统表", "completed", step, totalSteps);
    
            // 步骤5: 删除所有外键约束
            step++;
            if (onProgress != null) await onProgress("清理外键", "processing", step, totalSteps);
            _logger.LogInformation("删除所有外键约束...");
            var fkSql = @"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' + CHAR(13)
                FROM sys.foreign_keys;
                EXEC sp_executesql @sql;";
            await ExecuteNonQueryAsync(connection, fkSql);
            if (onProgress != null) await onProgress("清理外键", "completed", step, totalSteps);
    
            // 步骤6: 删除 hangfire 表
            step++;
            if (onProgress != null) await onProgress("清理Hangfire", "processing", step, totalSteps);
            _logger.LogInformation("删除 hangfire 表...");
            var dropHangfireSql = @"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql += N'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(13)
                FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = 'hangfire';
                IF LEN(@sql) > 0 EXEC sp_executesql @sql;";
            await ExecuteNonQueryAsync(connection, dropHangfireSql);
            if (onProgress != null) await onProgress("清理Hangfire", "completed", step, totalSteps);

            // 步骤7: 处理已存在的系统表
            step++;
            if (onProgress != null) await onProgress("处理系统表", "processing", step, totalSteps);
            _logger.LogInformation("处理已存在的系统表...");
    
            foreach (var table in analysis.ExistingSystemTables)
            {
                var tableFullName = $"{QuoteIdentifier(table.SchemaName)}.{QuoteIdentifier(table.TableName)}";
                if (tableActions.TryGetValue(table.TableName, out var action) && action == "preserve")
                {
                    // 重命名为备份表（sp_rename 需要使用 schema.table 格式）
                    var bakName = $"_BAK_{table.TableName}";
                    await ExecuteNonQueryAsync(connection, $"EXEC sp_rename '{table.SchemaName}.{table.TableName}', '{bakName}'");
                    // sp_rename 只改表名，不改约束名；必须手动重命名约束和索引，防止新建同名表时 PK 冲突
                    await RenameTableConstraintsAsync(connection, table.SchemaName, bakName);
                    preservedTables.Add(table.TableName);
                }
                else
                {
                    // clear 或不在 tableActions 中（无数据的表）→ DROP
                    await ExecuteNonQueryAsync(connection, $"DROP TABLE {tableFullName}");
                }
            }
            if (onProgress != null) await onProgress("处理系统表", "completed", step, totalSteps);
    
            connection.Close();
    
            // 步骤9: CreateTablesAsync 无条件创建全部系统表（不受 _BAK_ 表影响）
            creationPhaseReached = true;
            step++;
            if (onProgress != null) await onProgress("创建表结构", "processing", step, totalSteps);
            _logger.LogInformation("创建全部系统表结构...");
            using var tempContext = _dynamicDbContextFactory.CreateDbContext("SqlServer", connectionString);
            var databaseCreator = tempContext.GetService<IRelationalDatabaseCreator>();
            await databaseCreator.CreateTablesAsync();
    
            // 显式重建 Hangfire 表（删表阶段会删除 hangfire schema 下的表）
            await EnsureHangfireTablesAsync(connectionString);
    
            // 验证系统表已创建
            {
                var systemTableNames = tempContext.Model.GetEntityTypes()
                    .Select(e => e.GetTableName())
                    .Where(n => n != null)
                    .Cast<string>()
                    .ToList();
                var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await connection.OpenAsync();
                var tableCheckSql = @"
                    SELECT t.name FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'dbo'";
                using (var cmd = new SqlCommand(tableCheckSql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        existingTables.Add(reader.GetString(0));
                }
                connection.Close();
                var missingTables = systemTableNames.Where(t => !existingTables.Contains(t)).ToList();
                if (missingTables.Count > 0)
                {
                    var msg = $"以下系统表创建失败: {string.Join(", ", missingTables)}";
                    _logger.LogError(msg);
                    throw new InvalidOperationException(msg);
                }
            }
            if (onProgress != null) await onProgress("创建表结构", "completed", step, totalSteps);
    
            // 步骤10: 恢复备份数据
            step++;
            if (onProgress != null) await onProgress("恢复数据", "processing", step, totalSteps);
            if (preservedTables.Count > 0)
            {
                _logger.LogInformation("恢复备份数据...");
                await connection.OpenAsync();
    
                foreach (var tableName in preservedTables)
                {
                    var bakTableName = $"_BAK_{tableName}";
                    try
                    {
                        _logger.LogInformation("恢复表 {TableName} 的数据...", tableName);
    
                        // a. 查询共同列
                        var commonColumnsSql = $@"
                            SELECT c1.name FROM sys.columns c1
                            INNER JOIN sys.columns c2 ON c1.name = c2.name
                            WHERE c1.object_id = OBJECT_ID('{QuoteIdentifier(tableName)}')
                            AND c2.object_id = OBJECT_ID('{QuoteIdentifier(bakTableName)}')";
                        var commonColumns = new List<string>();
                        using (var cmd = new SqlCommand(commonColumnsSql, connection))
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                commonColumns.Add(reader.GetString(0));
                            }
                        }
    
                        if (commonColumns.Count == 0)
                        {
                            _logger.LogWarning("表 {TableName} 没有共同列，跳过数据恢复", tableName);
                            await ExecuteNonQueryAsync(connection, $"IF OBJECT_ID('{QuoteIdentifier(bakTableName)}', 'U') IS NOT NULL DROP TABLE {QuoteIdentifier(bakTableName)}");
                            continue;
                        }
    
                        // b. 检查新表是否有 IDENTITY 列且该列在共同列中
                        string? identityColumnName = null;
                        var identitySql = $"SELECT name FROM sys.identity_columns WHERE object_id = OBJECT_ID('{QuoteIdentifier(tableName)}')";
                        using (var cmd = new SqlCommand(identitySql, connection))
                        {
                            var result = await cmd.ExecuteScalarAsync();
                            identityColumnName = result as string;
                        }
    
                        bool hasIdentityInCommon = !string.IsNullOrEmpty(identityColumnName) &&
                            commonColumns.Any(c => string.Equals(c, identityColumnName, StringComparison.OrdinalIgnoreCase));
    
                        var columnList = string.Join(", ", commonColumns.Select(c => QuoteIdentifier(c)));
    
                        // c. 使用 try-finally 确保 IDENTITY_INSERT OFF 一定执行
                        try
                        {
                            if (hasIdentityInCommon)
                                await ExecuteNonQueryAsync(connection, $"SET IDENTITY_INSERT {QuoteIdentifier(tableName)} ON");
                        
                            // d. 插入数据
                            var insertSql = $"INSERT INTO {QuoteIdentifier(tableName)} ({columnList}) SELECT {columnList} FROM {QuoteIdentifier(bakTableName)}";
                            await ExecuteNonQueryAsync(connection, insertSql);
                        }
                        finally
                        {
                            if (hasIdentityInCommon)
                            {
                                try { await ExecuteNonQueryAsync(connection, $"SET IDENTITY_INSERT {QuoteIdentifier(tableName)} OFF"); }
                                catch { /* 忽略关闭失败 */ }
                            }
                        }
    
                        // f. 验证恢复行数并删除备份表
                        // 验证恢复的数据行数
                        try
                        {
                            using var countCmd = new SqlCommand(
                                $"SELECT (SELECT COUNT(*) FROM {QuoteIdentifier(bakTableName)}) AS BakCount, (SELECT COUNT(*) FROM {QuoteIdentifier(tableName)}) AS NewCount",
                                connection);
                            using var countReader = await countCmd.ExecuteReaderAsync();
                            if (await countReader.ReadAsync())
                            {
                                var verifyBakCount = countReader.GetInt32(0);
                                var verifyNewCount = countReader.GetInt32(1);
                                if (verifyBakCount != verifyNewCount)
                                {
                                    _logger.LogWarning("表 {TableName} 数据恢复行数不一致: 备份表 {BakCount} 行, 新表 {NewCount} 行",
                                        tableName, verifyBakCount, verifyNewCount);
                                }
                            }
                        }
                        catch (Exception countEx)
                        {
                            _logger.LogWarning(countEx, "表 {TableName} 行数验证失败", tableName);
                        }

                        await ExecuteNonQueryAsync(connection, $"DROP TABLE {QuoteIdentifier(bakTableName)}");
                    }
                    catch (Exception tableEx)
                    {
                        _logger.LogError(tableEx, "恢复表 {TableName} 数据失败，备份表 {BakTable} 已保留", tableName, bakTableName);
                        // 不抛出，继续处理其他表（备份表保留供人工恢复）
                    }
                }
    
                connection.Close();
    
                // 验证 _BAK_ 表残留
                await connection.OpenAsync();
                var bakCount = await ExecuteScalarAsync<int>(connection,
                    "SELECT COUNT(*) FROM sys.tables WHERE name LIKE '_BAK_%'");
                if (bakCount > 0)
                    _logger.LogWarning("数据恢复后仍有 {Count} 张备份表残留，可能有表恢复失败", bakCount);
                connection.Close();
            }
            if (onProgress != null) await onProgress("恢复数据", "completed", step, totalSteps);
    
            // 步骤11: 填充种子数据
            step++;
            if (onProgress != null) await onProgress("填充种子数据", "processing", step, totalSteps);
            if (_databaseSeeder != null)
            {
                _logger.LogInformation("正在执行数据库迁移...");
                _databaseSeeder.MigrateAll(tempContext);
            }
    
            // 更新配置
            await UpdateConfigAsync("SqlServer", connectionString);
            if (onProgress != null) await onProgress("填充种子数据", "completed", step, totalSteps);
    
            _logger.LogInformation("保留初始化数据库完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保留初始化过程中发生错误");
    
            if (!creationPhaseReached && preservedTables.Count > 0)
            {
                // 备份阶段完成但创建阶段未开始：尝试将 _BAK_ 表恢复为原表名
                _logger.LogWarning("尝试回滚备份表...");
                foreach (var tableName in preservedTables)
                {
                    try
                    {
                        var bakName = $"_BAK_{tableName}";
                        if (connection.State != global::System.Data.ConnectionState.Open)
                            await connection.OpenAsync();
    
                        using var checkCmd = new SqlCommand(
                            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{bakName.Replace("'", "''")}'"
                            + $" AND TABLE_SCHEMA = 'dbo'", connection);
                        var bakExists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;
    
                        if (bakExists)
                        {
                            using var origCmd = new SqlCommand(
                                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName.Replace("'", "''")}'"
                                + $" AND TABLE_SCHEMA = 'dbo'", connection);
                            var origExists = (int)(await origCmd.ExecuteScalarAsync() ?? 0) > 0;
    
                            if (!origExists)
                            {
                                await ExecuteNonQueryAsync(connection, $"EXEC sp_rename '{bakName}', '{tableName}'");
                                _logger.LogInformation("已回滚备份表: {TableName}", tableName);
                            }
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "回滚备份表 {TableName} 失败", tableName);
                    }
                }
            }
            else if (creationPhaseReached)
            {
                // 创建阶段后失败：清理残留的 _BAK_ 表
                foreach (var tableName in preservedTables)
                {
                    try
                    {
                        var bakName = $"_BAK_{tableName}";
                        if (connection.State != global::System.Data.ConnectionState.Open)
                            await connection.OpenAsync();
                        await ExecuteNonQueryAsync(connection,
                            $"IF OBJECT_ID('{bakName.Replace("'", "''")}', 'U') IS NOT NULL DROP TABLE [{bakName.Replace("]", "]]")}]");
                    }
                    catch { /* 忽略清理错误 */ }
                }
            }
    
            // 检查是否是登录失败
            if (ex is SqlException sqlEx)
                ThrowIfLoginFailed(sqlEx);
    
            throw;
        }
        finally
        {
            // 确保外键约束恢复启用（WITH CHECK 验证数据完整性）
            try
            {
                if (connection.State != global::System.Data.ConnectionState.Open)
                    await connection.OpenAsync();
                await EnableForeignKeysAsync(connection);
            }
            catch (Exception fkEx)
            {
                _logger.LogWarning(fkEx, "finally 中恢复外键约束时发生错误");
            }
        }
    }

    /// <summary>
    /// 执行非查询 SQL 命令
    /// </summary>
    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        using var cmd = new SqlCommand(sql, connection);
        cmd.CommandTimeout = 120;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 执行标量查询并返回指定类型的结果
    /// </summary>
    private static async Task<T> ExecuteScalarAsync<T>(SqlConnection connection, string sql)
    {
        using var cmd = new SqlCommand(sql, connection);
        cmd.CommandTimeout = 120;
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return default!;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// 禁用数据库所有外键约束（SqlConnection 版本）
    /// </summary>
    private static async Task DisableForeignKeysAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
    }

    /// <summary>
    /// 启用数据库所有外键约束并验证数据完整性（SqlConnection 版本）
    /// </summary>
    private static async Task EnableForeignKeysAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'");
    }

    /// <summary>
    /// 禁用数据库所有外键约束（DbContext 版本，用于种子数据填充阶段）
    /// </summary>
    private static async Task DisableForeignKeysAsync(DbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
    }

    /// <summary>
    /// 启用数据库所有外键约束并验证数据完整性（DbContext 版本）
    /// </summary>
    private static async Task EnableForeignKeysAsync(DbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'");
    }

    /// <summary>
    /// 将指定备份表（_BAK_xxx）上的所有约束（主键、唯一、默认、检查）和普通索引加上 _BAK_ 前缀重命名。
    /// sp_rename 只改表名而不改约束名，若不重命名，后续 CreateTablesAsync 创建同名新表时会因同名 PK 冲突而报错。
    /// </summary>
    private static async Task RenameTableConstraintsAsync(SqlConnection connection, string schemaName, string bakTableName)
    {
        // 构造安全的带引号标识符，用于 OBJECT_ID 参数
        var quotedFullName = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(bakTableName)}";
        var escapedFullName = quotedFullName.Replace("'", "''");

        // 拼接重命名 SQL：
        // 1. 主键和唯一约束 (key_constraints)
        // 2. 普通索引 (sys.indexes, 排除主键和唯一约束索引)
        // 3. 默认值约束 (default_constraints)
        // 4. 检查约束 (check_constraints)
        var renameSql = $@"
DECLARE @sql NVARCHAR(MAX) = N'';

-- 重命名主键和唯一约束
SELECT @sql += N'EXEC sp_rename N''' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(name) + N''', N''_BAK_' + REPLACE(name, N'''', N'''''') + N''', N''OBJECT'';' + CHAR(13)
FROM sys.key_constraints
WHERE parent_object_id = OBJECT_ID(N'{escapedFullName}');

-- 重命名普通索引（排除主键和唯一约束索引）
SELECT @sql += N'EXEC sp_rename N''' + QUOTENAME(OBJECT_SCHEMA_NAME(object_id)) + N'.' + QUOTENAME(OBJECT_NAME(object_id)) + N'.' + QUOTENAME(name) + N''', N''_BAK_' + REPLACE(name, N'''', N'''''') + N''', N''INDEX'';' + CHAR(13)
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'{escapedFullName}')
  AND name IS NOT NULL AND is_primary_key = 0 AND is_unique_constraint = 0 AND type > 0;

-- 重命名默认值约束
SELECT @sql += N'EXEC sp_rename N''' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(name) + N''', N''_BAK_' + REPLACE(name, N'''', N'''''') + N''', N''OBJECT'';' + CHAR(13)
FROM sys.default_constraints
WHERE parent_object_id = OBJECT_ID(N'{escapedFullName}');

-- 重命名检查约束
SELECT @sql += N'EXEC sp_rename N''' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(name) + N''', N''_BAK_' + REPLACE(name, N'''', N'''''') + N''', N''OBJECT'';' + CHAR(13)
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'{escapedFullName}');

IF LEN(@sql) > 0 EXEC sp_executesql @sql;";
        await ExecuteNonQueryAsync(connection, renameSql);
    }

    /// <summary>
    /// 对 SQL 标识符（表名、列名）进行转义，防止 SQL 注入
    /// </summary>
    private static string QuoteIdentifier(string name)
        => $"[{name.Replace("]", "]]")}]";

    /// <summary>
    /// 检测 SqlException 是否为登录失败错误（Error Number 18456），
    /// 如果是则抛出带有明确提示的 InvalidOperationException
    /// </summary>
    private static void ThrowIfLoginFailed(SqlException ex)
    {
        if (ex.Number == 18456 ||
            ex.Message.Contains("登录失败", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("用户名或密码错误", ex);
        }
    }

    /// <summary>
    /// 确保 hangfire schema 存在
    /// </summary>
    private async Task EnsureHangfireSchemaAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var sql = @"
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'hangfire')
            BEGIN
                EXEC('CREATE SCHEMA [hangfire]');
            END";
        await ExecuteNonQueryAsync(connection, sql);
    }

    /// <summary>
    /// 显式重建 Hangfire 的数据库表。
    /// 通过创建一个临时的 SqlServerStorage 实例并设置 PrepareSchemaIfNecessary = true，
    /// 触发 Hangfire.SqlServer 内部的 SqlServerObjectsInstaller.Install 来安装所有表。
    /// 这是显式调用，不依赖应用启动时的隐式行为。
    /// </summary>
    private async Task EnsureHangfireTablesAsync(string connectionString)
    {
        try
        {
            // 先确保 schema 存在
            await EnsureHangfireSchemaAsync(connectionString);

            _logger.LogInformation("正在重建 Hangfire 表...");

            // 显式创建 SqlServerStorage 实例，触发表安装
            // SqlServerStorage 构造函数在 PrepareSchemaIfNecessary = true 时会同步执行建表
            var storage = new SqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true
            });

            _logger.LogInformation("Hangfire 表重建完成");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hangfire 表重建过程中发生错误");
        }
    }

    private static string MaskPassword(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;
    
        // 对 SqlServer 和 MySql 连接字符串中的密码进行脲敏
        var parts = connectionString.Split(';');
        var maskedParts = parts.Select(part =>
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
            {
                var key = trimmed.Split('=')[0];
                return $"{key}=***";
            }
            return part;
        });
    
        return string.Join(";", maskedParts);
    }
    
    /// <summary>
    /// dry-run 预览保留初始化操作影响
    /// </summary>
    public async Task<PreserveDryRunResult> DryRunAsync(string connectionString, Dictionary<string, string> tableActions)
    {
        var analysis = await AnalyzeDatabaseAsync(connectionString);
        var result = new PreserveDryRunResult();
    
        // 1. 非系统表（将被删除）
        result.TablesToDelete = analysis.ForeignTables.Select(t => t.TableName).ToList();
    
        // 2. 根据 tableActions 分类已存在的系统表
        foreach (var table in analysis.ExistingSystemTables)
        {
            var action = tableActions.GetValueOrDefault(table.TableName, "clear");
            if (action == "preserve")
                result.TablesToPreserve.Add(table.TableName);
            else
                result.TablesToRebuild.Add(table.TableName);
        }
    
        // 3. 缺失表
        result.TablesToCreate = analysis.MissingSystemTables.ToList();
    
        // 4. Hangfire 表
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        result.HangfireTablesToDelete = await GetHangfireTablesAsync(conn);
        
        // 6. 预估数据丢失行数（包含非系统表、清空重建的系统表、Hangfire 表）
        foreach (var tableName in result.TablesToDelete.Concat(result.TablesToRebuild))
        {
            try
            {
                var count = await GetTableRowCountAsync(conn, tableName);
                result.EstimatedDataLossRows += count;
            }
            catch { /* 忽略 */ }
        }
        // 统计 Hangfire 表行数
        foreach (var tableName in result.HangfireTablesToDelete)
        {
            try
            {
                var count = await GetTableRowCountAsync(conn, $"hangfire.{tableName}");
                result.EstimatedDataLossRows += count;
            }
            catch { /* 忽略 */ }
        }
        
        // 7. 生成警告
        if (result.TablesToRebuild.Any())
            result.Warnings.Add($"以下 {result.TablesToRebuild.Count} 张系统表将被清空重建，数据将丢失");
        if (result.TablesToDelete.Any())
            result.Warnings.Add($"以下 {result.TablesToDelete.Count} 张非系统表将被删除");
        if (result.EstimatedDataLossRows > 0)
            result.Warnings.Add($"预计将丢失约 {result.EstimatedDataLossRows:N0} 行数据");
    
        return result;
    }
    
    /// <summary>
    /// 获取 hangfire schema 下的所有表名
    /// </summary>
    private static async Task<List<string>> GetHangfireTablesAsync(SqlConnection conn)
    {
        var tables = new List<string>();
        var sql = @"SELECT t.name FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'hangfire'";
        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }
    
    /// <summary>
    /// 获取表行数
    /// </summary>
    private static async Task<long> GetTableRowCountAsync(SqlConnection conn, string tableName)
    {
        var sql = $"SELECT COUNT_BIG(*) FROM {QuoteIdentifier(tableName)}";
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? 0L : (long)Convert.ChangeType(result, typeof(long));
    }
    
    /// <summary>
    /// 执行定时备份任务（由 Hangfire RecurringJob 调用）
    /// </summary>
    public async Task ExecuteScheduledBackupAsync()
    {
        var config = DbConnectionsHelper.LoadBackupConfig();
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.BackupDirectory))
        {
            _logger.LogInformation("定时备份未启用或备份目录未配置，跳过");
            return;
        }
    
        var connStr = DbConnectionsHelper.GetSystemConnectionString(_configuration.GetValue<string>("Security:EncryptionKey"));
        if (string.IsNullOrWhiteSpace(connStr))
        {
            _logger.LogWarning("系统数据库连接字符串为空，跳过定时备份");
            return;
        }
    
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
    
        var dbName = new SqlConnectionStringBuilder(connStr).InitialCatalog;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = config.FileNamePattern
            .Replace("{dbName}", dbName)
            .Replace("{timestamp}", timestamp);
        var backupFile = Path.Combine(config.BackupDirectory, fileName);
    
        try
        {
            // 执行备份
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{backupFile}' WITH FORMAT, INIT, COMPRESSION";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 600 };
            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("定时备份完成: {BackupFile}", backupFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定时备份失败");
            throw;
        }
    
        // 清理旧备份
        await CleanupOldBackupsAsync(conn, config, dbName);
    }
    
    /// <summary>
    /// 清理超出保留数量的旧备份文件
    /// </summary>
    private async Task CleanupOldBackupsAsync(SqlConnection conn, BackupConfig config, string dbName)
    {
        if (config.RetentionCount <= 0) return;
    
        var sql = @"
            SELECT bmf.physical_device_name
            FROM msdb.dbo.backupset bs
            JOIN msdb.dbo.backupmediafamily bmf ON bs.media_set_id = bmf.media_set_id
            WHERE bs.database_name = @dbName AND bs.type = 'D'
            ORDER BY bs.backup_finish_date DESC";
    
        var allBackups = new List<string>();
        using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@dbName", dbName);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                allBackups.Add(reader.GetString(0));
        }
    
        // 跳过最近 N 条，删除其余
        var toDelete = allBackups.Skip(config.RetentionCount).ToList();
        foreach (var filePath in toDelete)
        {
            try
            {
                var deleteSql = $"EXEC master.dbo.xp_delete_file 0, N'{filePath.Replace("'", "''")}', N'bak'";
                using var delCmd = new SqlCommand(deleteSql, conn) { CommandTimeout = 60 };
                await delCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("已删除旧备份: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除旧备份失败: {FilePath}", filePath);
            }
        }
    }

    public string GenerateSetupToken()
    {
        try
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var secretKey = jwtSettings["Secret"]
                ?? throw new InvalidOperationException("缺少 JWT 配置项 Jwt:Secret");
            var issuer = jwtSettings["Issuer"]
                ?? throw new InvalidOperationException("缺少 JWT 配置项 Jwt:Issuer");
            var audience = jwtSettings["Audience"]
                ?? throw new InvalidOperationException("缺少 JWT 配置项 Jwt:Audience");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "0"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim("userName", "系统管理员"),
                new Claim("userId", "0")
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 Setup Token 失败");
            throw;
        }
    }
}
