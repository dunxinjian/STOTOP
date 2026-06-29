using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

#region Models

public enum SchemaChangeType
{
    AddColumn,
    AlterColumn
}

public class SchemaChange
{
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public SchemaChangeType ChangeType { get; set; }
    public string SqlStatement { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime? ExecutedAt { get; set; }
}

public class SchemaWarning
{
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string Message { get; set; } = "";

    /// <summary>
    /// true 表示一条变更 SQL 执行失败（区别于检测类警告），调用方据此 fail-fast，
    /// 不再依赖消息文案匹配。
    /// </summary>
    public bool IsExecutionFailure { get; set; }
}

public class SchemaSyncResult
{
    public List<SchemaChange> ExecutedChanges { get; set; } = new();
    public List<SchemaChange> PendingChanges { get; set; } = new();
    public List<SchemaWarning> Warnings { get; set; } = new();
    public long ElapsedMs { get; set; }
}

#endregion

#region Database Column Info

internal class DbColumnInfo
{
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public int? MaxLength { get; set; }
    public int? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public int? DateTimePrecision { get; set; }
    public bool IsNullable { get; set; }
    public string? ColumnDefault { get; set; }
}

#endregion

/// <summary>
/// Schema Auto-Sync 引擎 — 自动将 EF Core 模型定义同步到数据库（列级差异）
/// </summary>
public static class SchemaAutoSync
{
    private const string LockResource = "STOTOP_SchemaAutoSync";
    private const int LockTimeoutMs = 60000;

    /// <summary>
    /// 排除列表中的表名前缀
    /// </summary>
    private static readonly string[] ExcludedPrefixes = { "STG", "HangFire", "__EF" };

    /// <summary>
    /// 排除的精确表名
    /// </summary>
    private static readonly HashSet<string> ExcludedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS迁移历史",
        "__EFMigrationsHistory",
        "SYS_Schema同步记录",
        "SYS基线数据同步记录"
    };

    /// <summary>
    /// 执行 Schema Auto-Sync
    /// </summary>
    /// <param name="ctx">EF Core DbContext</param>
    /// <param name="autoExecute">是否自动执行变更（开发环境=true, 生产环境=false）</param>
    public static SchemaSyncResult Sync(STOTOPDbContext ctx, bool autoExecute, ILogger? logger = null)
    {
        var sw = Stopwatch.StartNew();
        var result = new SchemaSyncResult();

        // 获取排他锁
        var connectionString = ctx.Database.GetConnectionString()!;
        using var lockConnection = new SqlConnection(connectionString);
        lockConnection.Open();
        using var lockTransaction = lockConnection.BeginTransaction();

        try
        {
            AcquireAppLock(lockConnection, lockTransaction);

            // 确保记录表存在（自举）——放在锁内，避免多实例并发 CREATE TABLE 撞车
            EnsureSyncHistoryTable(ctx);

            // 1. 获取 EF Core 模型期望结构
            var modelColumns = GetModelColumns(ctx);

            // 2. 获取数据库实际结构
            var dbColumns = GetDatabaseColumns(ctx);

            // 3. 对比差异，生成变更
            ComputeDifferences(modelColumns, dbColumns, result);

            // 4. 根据策略执行或暂存
            if (autoExecute && result.PendingChanges.Count > 0)
            {
                // 开发环境：执行所有待处理变更
                foreach (var change in result.PendingChanges.ToList())
                {
                    try
                    {
                        ctx.Database.ExecuteSqlRaw(change.SqlStatement);
                        change.ExecutedAt = DateTime.Now;
                        result.ExecutedChanges.Add(change);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "[SchemaAutoSync] 列同步失败: [{Table}].[{Column}] — {Sql}",
                            change.TableName, change.ColumnName, change.SqlStatement);
                        result.Warnings.Add(new SchemaWarning
                        {
                            TableName = change.TableName,
                            ColumnName = change.ColumnName,
                            Message = $"执行失败: {ex.Message}",
                            IsExecutionFailure = true
                        });
                    }
                }
                // 从 Pending 中移除已执行的
                result.PendingChanges.RemoveAll(c => c.ExecutedAt.HasValue);
            }

            // 5. 将待审核变更存储到记录表（生产环境用）
            if (!autoExecute && result.PendingChanges.Count > 0)
            {
                SavePendingChanges(ctx, result.PendingChanges);
            }
        }
        finally
        {
            try { lockTransaction.Commit(); }
            catch { try { lockTransaction.Rollback(); } catch { } }
        }

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;

        // 输出日志
        PrintSyncReport(result, logger);

        return result;
    }

    #region 获取 EF Core 模型列信息

    private static Dictionary<string, List<ModelColumnInfo>> GetModelColumns(STOTOPDbContext ctx)
    {
        var result = new Dictionary<string, List<ModelColumnInfo>>(StringComparer.OrdinalIgnoreCase);

        var model = ctx.GetService<IDesignTimeModel>().Model;
        var relationalModel = model.GetRelationalModel();

        foreach (var table in relationalModel.Tables)
        {
            var tableName = table.Name;

            // 排除表
            if (IsExcluded(tableName)) continue;

            var columns = new List<ModelColumnInfo>();
            foreach (var column in table.Columns)
            {
                var storeType = column.StoreType;
                var isNullable = column.IsNullable;
                var defaultValueSql = column.DefaultValueSql;

                // 处理 HasDefaultValue 的情况
                if (defaultValueSql == null)
                {
                    var defaultValue = column.DefaultValue;
                    if (defaultValue != null)
                    {
                        defaultValueSql = SeederHelper.ConvertDefaultValueToSql(defaultValue);
                    }
                }

                columns.Add(new ModelColumnInfo
                {
                    ColumnName = column.Name,
                    StoreType = storeType,
                    IsNullable = isNullable,
                    DefaultValueSql = defaultValueSql
                });
            }

            result[tableName] = columns;
        }

        return result;
    }

    #endregion

    #region 获取数据库实际列信息

    private static Dictionary<string, List<DbColumnInfo>> GetDatabaseColumns(STOTOPDbContext ctx)
    {
        var result = new Dictionary<string, List<DbColumnInfo>>(StringComparer.OrdinalIgnoreCase);

        var sql = @"
SELECT 
    c.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT,
    c.DATETIME_PRECISION
FROM INFORMATION_SCHEMA.COLUMNS c
INNER JOIN INFORMATION_SCHEMA.TABLES t 
    ON c.TABLE_NAME = t.TABLE_NAME AND c.TABLE_SCHEMA = t.TABLE_SCHEMA
WHERE t.TABLE_TYPE = 'BASE TABLE' AND c.TABLE_SCHEMA = 'dbo'
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION";

        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        var wasOpen = ctx.Database.GetDbConnection().State == System.Data.ConnectionState.Open;
        if (!wasOpen) ctx.Database.GetDbConnection().Open();

        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var tableName = reader.GetString(0);

                if (IsExcluded(tableName)) continue;

                if (!result.ContainsKey(tableName))
                    result[tableName] = new List<DbColumnInfo>();

                result[tableName].Add(new DbColumnInfo
                {
                    TableName = tableName,
                    ColumnName = reader.GetString(1),
                    DataType = reader.GetString(2),
                    MaxLength = reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                    NumericPrecision = reader.IsDBNull(4) ? null : (int?)(byte)reader.GetValue(4),
                    NumericScale = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5),
                    IsNullable = reader.GetString(6) == "YES",
                    ColumnDefault = reader.IsDBNull(7) ? null : reader.GetString(7),
                    // datetime2/time 的精度在 DATETIME_PRECISION（smallint），NUMERIC_SCALE 恒为 NULL
                    DateTimePrecision = reader.IsDBNull(8) ? null : (int?)Convert.ToInt32(reader.GetValue(8))
                });
            }
        }
        finally
        {
            if (!wasOpen) ctx.Database.GetDbConnection().Close();
        }

        return result;
    }

    #endregion

    #region 对比差异

    private static void ComputeDifferences(
        Dictionary<string, List<ModelColumnInfo>> modelColumns,
        Dictionary<string, List<DbColumnInfo>> dbColumns,
        SchemaSyncResult result)
    {
        foreach (var (tableName, modelCols) in modelColumns)
        {
            // 新表：跳过（由 CreateTablesAsync 处理）
            if (!dbColumns.ContainsKey(tableName))
                continue;

            var dbCols = dbColumns[tableName];
            var dbColDict = dbCols.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

            foreach (var modelCol in modelCols)
            {
                if (!dbColDict.TryGetValue(modelCol.ColumnName, out var dbCol))
                {
                    // 新增列
                    var addSql = GenerateAddColumnSql(tableName, modelCol);
                    if (addSql != null)
                    {
                        result.PendingChanges.Add(new SchemaChange
                        {
                            TableName = tableName,
                            ColumnName = modelCol.ColumnName,
                            ChangeType = SchemaChangeType.AddColumn,
                            SqlStatement = addSql,
                            Description = $"新增列 {modelCol.ColumnName} ({modelCol.StoreType})"
                        });
                    }
                }
                else
                {
                    // 列已存在，对比类型
                    var alterResult = CompareColumn(tableName, modelCol, dbCol);
                    if (alterResult.HasValue)
                    {
                        var (change, isWarning) = alterResult.Value;
                        if (isWarning)
                        {
                            result.Warnings.Add(new SchemaWarning
                            {
                                TableName = tableName,
                                ColumnName = modelCol.ColumnName,
                                Message = change.Description
                            });
                        }
                        else
                        {
                            result.PendingChanges.Add(change);
                        }
                    }
                }
            }

            // Entity 中不存在但数据库有的列 → 仅警告
            foreach (var dbCol in dbCols)
            {
                if (!modelCols.Any(m => string.Equals(m.ColumnName, dbCol.ColumnName, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Warnings.Add(new SchemaWarning
                    {
                        TableName = tableName,
                        ColumnName = dbCol.ColumnName,
                        Message = $"数据库存在多余列 [{dbCol.ColumnName}]，Entity 中不存在"
                    });
                }
            }
        }
    }

    #endregion

    #region SQL 生成

    private static string? GenerateAddColumnSql(string tableName, ModelColumnInfo col)
    {
        var nullability = col.IsNullable ? "NULL" : "NOT NULL";

        // NOT NULL 列必须有 DEFAULT 值
        if (!col.IsNullable && string.IsNullOrEmpty(col.DefaultValueSql))
        {
            // 自动推断默认值
            var inferredDefault = InferDefaultValue(col.StoreType);
            if (inferredDefault == null)
                return null; // 无法推断，跳过

            return $"ALTER TABLE [{tableName}] ADD [{col.ColumnName}] {col.StoreType} {nullability} CONSTRAINT [DF_{tableName}_{col.ColumnName}] DEFAULT {inferredDefault};";
        }

        if (!string.IsNullOrEmpty(col.DefaultValueSql))
        {
            return $"ALTER TABLE [{tableName}] ADD [{col.ColumnName}] {col.StoreType} {nullability} CONSTRAINT [DF_{tableName}_{col.ColumnName}] DEFAULT {col.DefaultValueSql};";
        }

        return $"ALTER TABLE [{tableName}] ADD [{col.ColumnName}] {col.StoreType} {nullability};";
    }

    private static (SchemaChange change, bool isWarning)? CompareColumn(
        string tableName, ModelColumnInfo modelCol, DbColumnInfo dbCol)
    {
        // 将 EF Core StoreType 解析为可比较的形式
        var modelNormalized = NormalizeStoreType(modelCol.StoreType);
        var dbNormalized = NormalizeDbType(dbCol);

        if (string.Equals(modelNormalized, dbNormalized, StringComparison.OrdinalIgnoreCase))
        {
            // 类型匹配但可空性不一致：改可空性涉及存量 NULL 数据/默认值，不自动执行，仅提示
            if (modelCol.IsNullable != dbCol.IsNullable)
            {
                return (new SchemaChange
                {
                    TableName = tableName,
                    ColumnName = modelCol.ColumnName,
                    ChangeType = SchemaChangeType.AlterColumn,
                    SqlStatement = "",
                    Description = $"列 [{modelCol.ColumnName}] 可空性不一致 db:{(dbCol.IsNullable ? "NULL" : "NOT NULL")} → model:{(modelCol.IsNullable ? "NULL" : "NOT NULL")} [仅提示]"
                }, true);
            }

            return null; // 完全匹配
        }

        // 判断是否为安全扩展（类型收窄为危险）
        var isSafeExpansion = IsSafeTypeExpansion(modelCol.StoreType, dbCol);

        if (isSafeExpansion)
        {
            var alterSql = $"ALTER TABLE [{tableName}] ALTER COLUMN [{modelCol.ColumnName}] {modelCol.StoreType} {(modelCol.IsNullable ? "NULL" : "NOT NULL")};";
            return (new SchemaChange
            {
                TableName = tableName,
                ColumnName = modelCol.ColumnName,
                ChangeType = SchemaChangeType.AlterColumn,
                SqlStatement = alterSql,
                Description = $"列类型扩展 {dbNormalized} → {modelNormalized}"
            }, false);
        }
        else
        {
            // 危险收窄 → 仅警告
            return (new SchemaChange
            {
                TableName = tableName,
                ColumnName = modelCol.ColumnName,
                ChangeType = SchemaChangeType.AlterColumn,
                SqlStatement = "",
                Description = $"列 [{modelCol.ColumnName}] 类型收窄 {dbNormalized} → {modelNormalized} [跳过]"
            }, true);
        }
    }

    #endregion

    #region 类型比较辅助

    /// <summary>
    /// 标准化 EF Core StoreType（如 "nvarchar(200)"）
    /// </summary>
    private static string NormalizeStoreType(string storeType)
    {
        return storeType.ToLowerInvariant().Trim();
    }

    /// <summary>
    /// 从 INFORMATION_SCHEMA 数据重建类型字符串
    /// </summary>
    private static string NormalizeDbType(DbColumnInfo col)
    {
        var type = col.DataType.ToLowerInvariant();

        return type switch
        {
            // INFORMATION_SCHEMA 报旧同义词 timestamp，EF StoreType 为 rowversion，同一类型
            "timestamp" => "rowversion",
            "nvarchar" or "varchar" or "char" or "nchar" =>
                col.MaxLength == -1 ? $"{type}(max)" : $"{type}({col.MaxLength})",
            "varbinary" or "binary" =>
                col.MaxLength == -1 ? $"{type}(max)" : $"{type}({col.MaxLength})",
            "decimal" or "numeric" =>
                $"{type}({col.NumericPrecision},{col.NumericScale})",
            "datetime2" or "time" or "datetimeoffset" =>
                col.DateTimePrecision.HasValue && col.DateTimePrecision.Value != 7
                    ? $"{type}({col.DateTimePrecision})"
                    : type,
            _ => type
        };
    }

    /// <summary>
    /// 判断类型变更是否为安全扩展
    /// </summary>
    private static bool IsSafeTypeExpansion(string modelStoreType, DbColumnInfo dbCol)
    {
        var modelType = modelStoreType.ToLowerInvariant();
        var dbType = dbCol.DataType.ToLowerInvariant();

        // 同类型的长度扩展
        if ((dbType == "nvarchar" || dbType == "varchar" || dbType == "char" || dbType == "nchar" ||
             dbType == "varbinary" || dbType == "binary") &&
            modelType.StartsWith(dbType))
        {
            var modelLength = ExtractLength(modelType);
            var dbLength = dbCol.MaxLength ?? 0;

            // 数据库列已是 (max)（MaxLength = -1）：模型改为有限长度属于收窄，危险
            if (dbLength == -1) return false;
            // 模型为 max 总是安全的扩展
            if (modelLength == -1) return true;
            // 长度增大是安全的
            if (modelLength > dbLength) return true;
            // 长度缩小是危险的
            return false;
        }

        // decimal 精度扩展
        if (dbType == "decimal" || dbType == "numeric")
        {
            var (modelP, modelS) = ExtractPrecisionScale(modelType);
            var dbP = dbCol.NumericPrecision ?? 0;
            var dbS = dbCol.NumericScale ?? 0;

            // 精度和小数位都增大是安全的
            if (modelP >= dbP && modelS >= dbS) return true;
            return false;
        }

        // 不同类型之间的变更默认视为危险
        return false;
    }

    private static int ExtractLength(string type)
    {
        if (type.Contains("(max)")) return -1;
        var start = type.IndexOf('(');
        var end = type.IndexOf(')');
        if (start < 0 || end < 0) return 0;
        var str = type.Substring(start + 1, end - start - 1);
        return int.TryParse(str, out var v) ? v : 0;
    }

    private static (int precision, int scale) ExtractPrecisionScale(string type)
    {
        var start = type.IndexOf('(');
        var end = type.IndexOf(')');
        if (start < 0 || end < 0) return (18, 2);
        var parts = type.Substring(start + 1, end - start - 1).Split(',');
        var p = int.TryParse(parts[0].Trim(), out var pv) ? pv : 18;
        var s = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var sv) ? sv : 0;
        return (p, s);
    }

    /// <summary>
    /// 推断 NOT NULL 列的默认值
    /// </summary>
    private static string? InferDefaultValue(string storeType)
    {
        var type = storeType.ToLowerInvariant();

        if (type.StartsWith("nvarchar") || type.StartsWith("varchar") ||
            type.StartsWith("nchar") || type.StartsWith("char") || type == "ntext" || type == "text")
            return "N''";

        if (type == "int" || type == "bigint" || type == "smallint" || type == "tinyint")
            return "0";

        if (type.StartsWith("decimal") || type.StartsWith("numeric") || type == "float" || type == "real" || type == "money")
            return "0";

        if (type == "bit")
            return "0";

        if (type.StartsWith("datetime") || type == "date")
            return "GETDATE()";

        if (type == "uniqueidentifier")
            return "NEWID()";

        if (type.StartsWith("varbinary") || type.StartsWith("binary"))
            return "0x";

        return null; // 无法推断
    }

    #endregion

    #region 排除判断

    private static bool IsExcluded(string tableName)
    {
        if (ExcludedTables.Contains(tableName)) return true;

        foreach (var prefix in ExcludedPrefixes)
        {
            if (tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    #endregion

    #region 同步记录表（自举）

    private static void EnsureSyncHistoryTable(STOTOPDbContext ctx)
    {
        var sql = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SYS_Schema同步记录')
BEGIN
    CREATE TABLE [SYS_Schema同步记录] (
        FID bigint IDENTITY(1,1) PRIMARY KEY,
        F表名 nvarchar(200) NOT NULL,
        F列名 nvarchar(200) NOT NULL,
        F变更类型 nvarchar(50) NOT NULL,
        FSQL语句 nvarchar(max) NOT NULL,
        F检测时间 datetime2 NOT NULL DEFAULT GETDATE(),
        F执行时间 datetime2 NULL,
        F执行人 nvarchar(100) NULL,
        F状态 nvarchar(20) NOT NULL DEFAULT N'待执行'
    )
END";
        ctx.Database.ExecuteSqlRaw(sql);
    }

    private static void SavePendingChanges(STOTOPDbContext ctx, List<SchemaChange> changes)
    {
        foreach (var change in changes)
        {
            ctx.Database.ExecuteSqlRaw(
                @"IF NOT EXISTS (
                    SELECT 1 FROM [SYS_Schema同步记录] 
                    WHERE F表名 = {0} AND F列名 = {1} AND F变更类型 = {2} AND F状态 = N'待执行'
                )
                INSERT INTO [SYS_Schema同步记录] (F表名, F列名, F变更类型, FSQL语句, F状态)
                VALUES ({0}, {1}, {2}, {3}, N'待执行')",
                change.TableName,
                change.ColumnName,
                change.ChangeType.ToString(),
                change.SqlStatement
            );
        }
    }

    #endregion

    #region 锁机制

    private static void AcquireAppLock(SqlConnection connection, SqlTransaction transaction)
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
        cmd.Parameters.AddWithValue("@timeout", LockTimeoutMs);

        var result = (int)cmd.ExecuteScalar()!;
        if (result < 0)
        {
            var message = result switch
            {
                -1 => "锁请求超时",
                -2 => "锁请求被取消",
                -3 => "锁请求因死锁被选为牺牲者",
                -999 => "参数验证或其他调用错误",
                _ => $"未知错误 (返回值: {result})"
            };
            throw new InvalidOperationException(
                $"获取 SchemaAutoSync 锁失败: {message}。Resource='{LockResource}', Timeout={LockTimeoutMs}ms");
        }
    }

    #endregion

    #region 日志输出

    private static void PrintSyncReport(SchemaSyncResult result, ILogger? logger)
    {
        var totalChanges = result.ExecutedChanges.Count + result.PendingChanges.Count + result.Warnings.Count;
        if (totalChanges == 0)
        {
            logger?.LogInformation("[SchemaAutoSync] 数据库结构与模型完全一致，无需变更 ({ElapsedMs}ms)", result.ElapsedMs);
            return;
        }
    
        logger?.LogInformation("[SchemaAutoSync] 检测到 {TotalChanges} 项结构差异：", totalChanges);
    
        foreach (var change in result.ExecutedChanges)
        {
            logger?.LogInformation("  ✅ [{TableName}] {Description}", change.TableName, change.Description);
        }
    
        foreach (var change in result.PendingChanges)
        {
            logger?.LogInformation("  ⏳ [{TableName}] {Description} [待执行]", change.TableName, change.Description);
        }
    
        foreach (var warning in result.Warnings)
        {
            logger?.LogWarning("  ⚠️ [{TableName}] {Message}", warning.TableName, warning.Message);
        }
    
        logger?.LogInformation("[SchemaAutoSync] 已执行 {ExecutedCount} 项，待执行 {PendingCount} 项，警告 {WarningCount} 项，耗时 {ElapsedMs}ms",
            result.ExecutedChanges.Count, result.PendingChanges.Count, result.Warnings.Count, result.ElapsedMs);
    }

    #endregion
}

#region Internal Models

internal class ModelColumnInfo
{
    public string ColumnName { get; set; } = "";
    public string StoreType { get; set; } = "";
    public bool IsNullable { get; set; }
    public string? DefaultValueSql { get; set; }
}

#endregion
