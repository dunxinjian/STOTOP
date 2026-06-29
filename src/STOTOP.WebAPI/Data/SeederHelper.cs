using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STOTOP.Infrastructure.Data;
using STOTOP.WebAPI.Data.Seeders;

namespace STOTOP.WebAPI.Data;

/// <summary>
/// 数据库种子数据初始化公共工具方法
/// </summary>
public static class SeederHelper
{
    public static bool IsSqlServer(STOTOPDbContext context)
    {
        return context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static void SaveWithIdentityInsert(STOTOPDbContext context, string tableName, bool isSqlServer)
    {
        // SaveChanges 会提交 ChangeTracker 中所有被跟踪的变更，而 IDENTITY_INSERT 仅对
        // tableName 一张表生效——若同时跟踪了其它 IDENTITY 表的新增实体，插入会失败。
        // 提前给出明确预警，避免排查无门。
        var foreignTables = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Metadata.GetTableName())
            .Where(t => t != null && !string.Equals(t, tableName, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        if (foreignTables.Count > 0)
        {
            Console.WriteLine(
                $"  [SaveWithIdentityInsert] ⚠ ChangeTracker 中还跟踪了其它表的变更（{string.Join(", ", foreignTables)}），" +
                $"它们会随本次 SaveChanges 一并提交，且 IDENTITY_INSERT 仅对 [{tableName}] 生效");
        }

        if (isSqlServer)
        {
            // 先检查表是否有 IDENTITY 列，没有则直接 SaveChanges（避免 SQL 8106 错误）
            context.Database.OpenConnection();
            try
            {
                var hasIdentity = context.Database.SqlQueryRaw<int>(
                    $"SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID(@p0) AND is_identity = 1",
                    tableName
                ).FirstOrDefault();

                if (hasIdentity > 0)
                {
                    // tableName 为内部硬编码的表名，非用户输入，无法参数化，安全无 SQL 注入风险
#pragma warning disable EF1002
                    context.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [{tableName}] ON;");
                    context.SaveChanges();
                    context.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [{tableName}] OFF;");
#pragma warning restore EF1002
                }
                else
                {
                    context.SaveChanges();
                }
            }
            finally
            {
                context.Database.CloseConnection();
            }
        }
        else
        {
            context.SaveChanges();
        }
    }

    public static void SaveWithIdentityInsert<TEntity>(STOTOPDbContext context, bool isSqlServer) where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        var tableName = entityType?.GetTableName() ?? typeof(TEntity).Name;
        SaveWithIdentityInsert(context, tableName, isSqlServer);
    }

    public static string DecodeGzipBase64(params string[] chunks)
    {
        var base64 = string.Concat(chunks);
        var bytes = Convert.FromBase64String(base64);
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 使用 ADO.NET 直接执行 SQL，不做 String.Format 解析。
    /// 这是 Seeder 中执行无参数 SQL 的统一入口：EF 的 ExecuteSqlRaw 即使不传参数
    /// 也会做 String.Format 处理，SQL 字面量中的 { } （JSON、模板占位符）会触发
    /// FormatException 或需要双写转义。需要参数化的 SQL 才使用 ExecuteSqlRaw({0}, ...)。
    /// </summary>
    public static void ExecuteRawSql(STOTOPDbContext ctx, string sql)
    {
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = MigrationRunner.GetConfig().CommandTimeoutSeconds;
        command.Transaction = ctx.Database.CurrentTransaction?.GetDbTransaction();
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 安全删除列：先删除依赖索引/唯一约束、默认约束（Default Constraint），再删除列。
    /// 每次调用为独立 batch，变量名不冲突。
    /// </summary>
    public static void DropColumnSafe(STOTOPDbContext ctx, string tableName, string columnName)
    {
#pragma warning disable EF1002
        ctx.Database.ExecuteSqlRaw($@"
        IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'{tableName}' AND COLUMN_NAME = N'{columnName}')
        BEGIN
            DECLARE @dropIndexSql NVARCHAR(MAX) = N'';
            SELECT @dropIndexSql = @dropIndexSql +
                CASE
                    WHEN i.is_unique_constraint = 1
                        THEN N'ALTER TABLE [{tableName}] DROP CONSTRAINT ' + QUOTENAME(i.name) + N';'
                    ELSE N'DROP INDEX ' + QUOTENAME(i.name) + N' ON [{tableName}];'
                END
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.object_id = OBJECT_ID(N'{tableName}')
              AND c.name = N'{columnName}'
              AND i.index_id > 0
              AND i.is_primary_key = 0;
            IF @dropIndexSql <> N'' EXEC sp_executesql @dropIndexSql;

            DECLARE @con NVARCHAR(256);
            SELECT @con = d.name FROM sys.default_constraints d
                JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
                WHERE d.parent_object_id = OBJECT_ID(N'{tableName}') AND c.name = N'{columnName}';
            IF @con IS NOT NULL EXEC(N'ALTER TABLE [{tableName}] DROP CONSTRAINT [' + @con + N']');
            ALTER TABLE [{tableName}] DROP COLUMN [{columnName}];
        END
        ");
#pragma warning restore EF1002
    }

    /// <summary>
    /// 将 CLR 默认值转换为 SQL 字面量（建表/加列 DDL 用）。
    /// 数值统一按 InvariantCulture 格式化，避免运行时文化（如小数逗号）破坏 DDL。
    /// DateTime 默认值沿用既有语义退化为 GETDATE()；无法转换时返回 null（调用方跳过 DEFAULT）。
    /// </summary>
    public static string? ConvertDefaultValueToSql(object value)
    {
        if (value is bool b)
            return b ? "1" : "0";
        if (value is int or long or short or byte or decimal or double or float)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (value is string s)
            return $"N'{s.Replace("'", "''")}'";
        if (value is DateTime)
            return "GETDATE()";
        if (value is Guid)
            return "NEWID()";
        return null;
    }

    public static void DropIndexSafe(STOTOPDbContext ctx, string tableName, string indexName)
    {
#pragma warning disable EF1002
        ctx.Database.ExecuteSqlRaw($@"
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'{tableName}') AND name = N'{indexName}')
        BEGIN
            DROP INDEX [{indexName}] ON [{tableName}];
        END
        ");
#pragma warning restore EF1002
    }
}
