using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

public static class BaselineReferenceDataSeeder
{
    private const string BaselineRelativePath = "Data/Seeders/Baseline/baseline-reference-data.json";

    private static readonly string[] PreferredKeyColumns =
    [
        "FID",
        "F编码",
        "F插件编码",
        "F标识",
        "F规则编码",
        "FConfigKey",
        "F参数键",
        "F名称"
    ];

    public static void Seed(STOTOPDbContext ctx, bool force = false)
    {
        if (!SeederHelper.IsSqlServer(ctx))
        {
            return;
        }

        var path = ResolveBaselinePath();
        if (path == null)
        {
            throw new FileNotFoundException($"未找到 canonical baseline 文件: {BaselineRelativePath}");
        }

        var json = File.ReadAllText(path);

        // baseline 文件未变化时跳过整个逐行 upsert（约 4000 行）；
        // --init-database 等严格初始化路径通过 force 强制对齐
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        EnsureFingerprintTable(ctx);
        if (!force && string.Equals(fingerprint, ReadAppliedFingerprint(ctx), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  [BaselineReferenceDataSeeder] baseline 文件未变化，跳过对齐");
            return;
        }

        var snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("canonical baseline 文件反序列化失败");

        var strategy = ctx.Database.CreateExecutionStrategy();
        strategy.Execute(() =>
        {
            using var transaction = ctx.Database.BeginTransaction();
            foreach (var table in snapshot.Tables)
            {
                SeedTable(ctx, table);
            }

            SaveAppliedFingerprint(ctx, fingerprint);
            transaction.Commit();
        });
    }

    private static void EnsureFingerprintTable(STOTOPDbContext ctx)
    {
        ExecuteNonQuery(ctx, """
            IF OBJECT_ID(N'[dbo].[SYS基线数据同步记录]', N'U') IS NULL
            CREATE TABLE [dbo].[SYS基线数据同步记录] (
                [FID] INT NOT NULL CONSTRAINT [PK_SYS基线数据同步记录] PRIMARY KEY,
                [F文件哈希] NVARCHAR(64) NOT NULL,
                [F应用时间] DATETIME2 NOT NULL CONSTRAINT [DF_SYS基线数据同步记录_应用时间] DEFAULT SYSDATETIME()
            );
            """);
    }

    private static string? ReadAppliedFingerprint(STOTOPDbContext ctx)
    {
        using var command = CreateCommand(ctx, "SELECT [F文件哈希] FROM [dbo].[SYS基线数据同步记录] WHERE [FID] = 1");
        return command.ExecuteScalar() as string;
    }

    private static void SaveAppliedFingerprint(STOTOPDbContext ctx, string fingerprint)
    {
        using var command = CreateCommand(ctx, """
            IF EXISTS (SELECT 1 FROM [dbo].[SYS基线数据同步记录] WHERE [FID] = 1)
                UPDATE [dbo].[SYS基线数据同步记录] SET [F文件哈希] = @hash, [F应用时间] = SYSDATETIME() WHERE [FID] = 1;
            ELSE
                INSERT INTO [dbo].[SYS基线数据同步记录] ([FID], [F文件哈希]) VALUES (1, @hash);
            """);
        AddParameter(command, "@hash", fingerprint);
        command.ExecuteNonQuery();
    }

    private static void SeedTable(STOTOPDbContext ctx, BaselineTableSnapshot table)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        var keyColumn = ResolveKeyColumn(table);
        var columns = table.Columns
            .Where(column => !column.IsSensitive && !IsServerGeneratedColumn(column))
            .ToList();

        if (columns.All(column => !string.Equals(column.Name, keyColumn.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"baseline 表 [{table.Name}] 的 key 列 [{keyColumn.Name}] 不可写入");
        }

        EnsureTableExists(ctx, table.Name);

        var identityInsert = columns.Any(column => column.IsIdentity);
        Console.WriteLine($"  [BaselineReferenceDataSeeder] {table.Name}: {table.Rows.Count} rows");

        // 除主键外的唯一业务键（如 FIN科目模板_明细 的 IX(模板ID,编码)）。
        // 供 UpsertRow 在"按主键找不到但业务键已存在"时跳过插入，防 FID 漂移撞唯一索引（2601）。
        // 这是通用兜底：即便某表快照与库 FID 体系再次漂移，也跳过而非崩溃启动。
        var businessKeys = GetBusinessUniqueKeys(ctx, table.Name, keyColumn.Name);

        if (identityInsert)
        {
            ExecuteNonQuery(ctx, $"SET IDENTITY_INSERT [dbo].[{EscapeIdentifier(table.Name)}] ON");
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        try
        {
            foreach (var row in table.Rows)
            {
                switch (UpsertRow(ctx, table.Name, columns, keyColumn, businessKeys, row))
                {
                    case UpsertOutcome.Inserted: inserted++; break;
                    case UpsertOutcome.Updated: updated++; break;
                    case UpsertOutcome.Skipped: skipped++; break;
                }
            }
        }
        finally
        {
            if (identityInsert)
            {
                ExecuteNonQuery(ctx, $"SET IDENTITY_INSERT [dbo].[{EscapeIdentifier(table.Name)}] OFF");
            }
        }

        if (skipped > 0)
        {
            Console.WriteLine(
                $"    ↳ [{table.Name}] 插入 {inserted} / 更新 {updated} / 跳过 {skipped}" +
                $"（业务唯一键已存在但主键漂移，保留库中现有行，快照不覆盖）");
        }
    }

    private enum UpsertOutcome
    {
        Inserted,
        Updated,
        Skipped
    }

    /// <summary>
    /// 读取表上除主键外的唯一键（唯一索引 / 唯一约束），返回每个键的列名有序列表。
    /// 用于在主键漂移场景下按业务键判重，避免快照 INSERT 撞唯一索引。
    /// 仅由单列且等于主键列的"退化唯一键"被剔除（与主键判定重复）。
    /// </summary>
    private static List<List<string>> GetBusinessUniqueKeys(STOTOPDbContext ctx, string tableName, string primaryKeyColumn)
    {
        const string sql = """
            SELECT i.name AS IndexName, c.name AS ColumnName
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.object_id = OBJECT_ID(@fullName)
              AND i.is_unique = 1
              AND i.is_primary_key = 0
              AND ic.is_included_column = 0
            ORDER BY i.name, ic.key_ordinal
            """;

        var byIndex = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using var command = CreateCommand(ctx, sql);
        AddParameter(command, "@fullName", $"dbo.{tableName}");
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var indexName = reader.GetString(0);
                var columnName = reader.GetString(1);
                if (!byIndex.TryGetValue(indexName, out var cols))
                {
                    cols = [];
                    byIndex[indexName] = cols;
                }

                cols.Add(columnName);
            }
        }

        return byIndex.Values
            .Where(cols => !(cols.Count == 1 && string.Equals(cols[0], primaryKeyColumn, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static UpsertOutcome UpsertRow(
        STOTOPDbContext ctx,
        string tableName,
        IReadOnlyList<BaselineColumnSnapshot> columns,
        BaselineColumnSnapshot keyColumn,
        IReadOnlyList<List<string>> businessKeys,
        IReadOnlyDictionary<string, object?> row)
    {
        var writableColumns = columns
            .Where(column => TryGetRowValue(row, column.Name, out var value) && !IsRedacted(value))
            .ToList();

        if (writableColumns.Count == 0 || writableColumns.All(c => !string.Equals(c.Name, keyColumn.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"baseline 表 [{tableName}] 的数据行缺少 key 列 [{keyColumn.Name}]");
        }

        var updateColumns = writableColumns
            .Where(column => !string.Equals(column.Name, keyColumn.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var writableNames = new HashSet<string>(writableColumns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

        // 仅保留本行写入列完整覆盖、且不等于主键判定的业务唯一键。
        // 快照主键(FID)在库与现网间漂移时，用业务唯一键判重，防止 INSERT 撞唯一索引。
        var applicableKeys = businessKeys
            .Where(key => key.Count > 0
                && key.All(col => writableNames.Contains(col))
                && !(key.Count == 1 && string.Equals(key[0], keyColumn.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var escapedTable = EscapeIdentifier(tableName);
        var escapedKey = EscapeIdentifier(keyColumn.Name);
        var updateSql = updateColumns.Count == 0
            ? ""
            : $"""
                  UPDATE [dbo].[{escapedTable}]
                     SET {string.Join(", ", updateColumns.Select((column, index) => $"[{EscapeIdentifier(column.Name)}] = @p{index}"))}
                   WHERE [{escapedKey}] = @p_key;
              """;
        var insertColumnSql = string.Join(", ", writableColumns.Select(column => $"[{EscapeIdentifier(column.Name)}]"));
        var insertValueSql = string.Join(", ", writableColumns.Select((_, index) => $"@i{index}"));

        // 业务唯一键冲突谓词：任一唯一键的全部列都与本行匹配即视为已存在。
        var businessKeyParams = new List<(string Name, object? Value)>();
        var keyPredicates = new List<string>();
        var paramIndex = 0;
        foreach (var key in applicableKeys)
        {
            var terms = new List<string>();
            foreach (var col in key)
            {
                var column = writableColumns.First(c => string.Equals(c.Name, col, StringComparison.OrdinalIgnoreCase));
                var name = $"@bk{paramIndex++}";
                terms.Add($"[{EscapeIdentifier(col)}] = {name}");
                businessKeyParams.Add((name, ConvertRowValue(GetRowValue(row, col), column.DataType)));
            }

            keyPredicates.Add($"({string.Join(" AND ", terms)})");
        }

        // 分支返回码：2=更新（主键命中）/ 0=跳过（业务键已存在但主键漂移）/ 1=插入
        string sql;
        if (keyPredicates.Count == 0)
        {
            sql = $"""
                IF EXISTS (SELECT 1 FROM [dbo].[{escapedTable}] WHERE [{escapedKey}] = @p_key)
                BEGIN
                {updateSql}
                SELECT 2;
                END
                ELSE
                BEGIN
                    INSERT INTO [dbo].[{escapedTable}] ({insertColumnSql})
                    VALUES ({insertValueSql});
                    SELECT 1;
                END
                """;
        }
        else
        {
            sql = $"""
                IF EXISTS (SELECT 1 FROM [dbo].[{escapedTable}] WHERE [{escapedKey}] = @p_key)
                BEGIN
                {updateSql}
                SELECT 2;
                END
                ELSE IF EXISTS (SELECT 1 FROM [dbo].[{escapedTable}] WHERE {string.Join(" OR ", keyPredicates)})
                BEGIN
                    SELECT 0;
                END
                ELSE
                BEGIN
                    INSERT INTO [dbo].[{escapedTable}] ({insertColumnSql})
                    VALUES ({insertValueSql});
                    SELECT 1;
                END
                """;
        }

        using var command = CreateCommand(ctx, sql);
        var keyValue = ConvertRowValue(GetRowValue(row, keyColumn.Name), keyColumn.DataType);
        AddParameter(command, "@p_key", keyValue);

        for (var i = 0; i < updateColumns.Count; i++)
        {
            AddParameter(command, $"@p{i}", ConvertRowValue(GetRowValue(row, updateColumns[i].Name), updateColumns[i].DataType));
        }

        for (var i = 0; i < writableColumns.Count; i++)
        {
            AddParameter(command, $"@i{i}", ConvertRowValue(GetRowValue(row, writableColumns[i].Name), writableColumns[i].DataType));
        }

        foreach (var (name, value) in businessKeyParams)
        {
            AddParameter(command, name, value);
        }

        // 业务键守卫拦不住的残余冲突（如快照 FID 撞到库中不同业务键行的主键）仍包装为中文诊断。
        try
        {
            return Convert.ToInt32(command.ExecuteScalar()) switch
            {
                2 => UpsertOutcome.Updated,
                0 => UpsertOutcome.Skipped,
                _ => UpsertOutcome.Inserted
            };
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                BuildUniqueKeyConflictMessage(tableName, keyColumn.Name, keyValue, ex.Message), ex);
        }
    }

    /// <summary>
    /// 构造唯一键冲突（SqlException 2601/2627）的诊断消息。
    /// internal 供单测直接覆盖：SqlException 无公开构造函数，InMemory 也不执行原生 SQL。
    /// </summary>
    internal static string BuildUniqueKeyConflictMessage(string tableName, string keyColumnName, object? keyValue, string sqlErrorMessage)
    {
        var indexName = TryExtractConstraintName(sqlErrorMessage, tableName) ?? "（未能从 SQL 异常消息中解析索引名）";
        return $"baseline 对齐表 [{tableName}] 时按 key 列 [{keyColumnName}] = {keyValue ?? "NULL"} 写入撞到唯一索引 [{indexName}]：" +
            "baseline 快照的 FID 体系与库中不一致——库中该业务键的行可能已被模块 Seeder 用不同 FID 重建；" +
            "请核对漂移方向后重生成 baseline 快照，而不是改 upsert 匹配键" +
            "（业务键匹配会把旧快照静默盖回并悬空 F父ID 类自引用）。" +
            $"本 seeder 全程单事务，异常后会整体回滚，库中不会留下部分写入的脏数据。原始错误：{sqlErrorMessage}";
    }

    private static string? TryExtractConstraintName(string sqlErrorMessage, string tableName)
    {
        // 2601/2627 的消息里索引/约束名以单引号包裹，但不同语言环境下与对象名的先后顺序不同
        // （英文 2601 先对象后索引，中文 2601 先索引后对象），故取第一个不是表引用的引号项
        foreach (Match match in Regex.Matches(sqlErrorMessage, "'([^']+)'"))
        {
            var candidate = match.Groups[1].Value;
            var isTableReference = string.Equals(candidate, tableName, StringComparison.OrdinalIgnoreCase)
                || candidate.EndsWith("." + tableName, StringComparison.OrdinalIgnoreCase);
            if (!isTableReference)
            {
                return candidate;
            }
        }

        return null;
    }

    private static BaselineColumnSnapshot ResolveKeyColumn(BaselineTableSnapshot table)
    {
        foreach (var candidate in PreferredKeyColumns)
        {
            var column = table.Columns.FirstOrDefault(c => string.Equals(c.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (column != null)
            {
                return column;
            }
        }

        throw new InvalidOperationException($"baseline 表 [{table.Name}] 未找到可用于 upsert 的 key 列");
    }

    private static void EnsureTableExists(STOTOPDbContext ctx, string tableName)
    {
        using var command = CreateCommand(ctx, "SELECT CASE WHEN OBJECT_ID(@tableName, N'U') IS NULL THEN 0 ELSE 1 END");
        AddParameter(command, "@tableName", $"dbo.{tableName}");
        var exists = Convert.ToInt32(command.ExecuteScalar()) == 1;
        if (!exists)
        {
            throw new InvalidOperationException($"canonical baseline 表不存在: [dbo].[{tableName}]");
        }
    }

    private static object? ConvertRowValue(object? value, string dataType)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return ConvertJsonElement(element, dataType);
        }

        if (value is string text)
        {
            return ConvertStringValue(text, dataType);
        }

        return value;
    }

    private static object? ConvertJsonElement(JsonElement element, string dataType)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => ConvertJsonNumber(element, dataType),
            JsonValueKind.String => ConvertStringValue(element.GetString() ?? "", dataType),
            _ => element.GetRawText()
        };
    }

    private static object ConvertJsonNumber(JsonElement element, string dataType)
    {
        var normalizedType = dataType.ToLowerInvariant();

        if (normalizedType is "bigint")
        {
            return element.GetInt64();
        }

        if (normalizedType is "int" or "smallint" or "tinyint")
        {
            return element.GetInt32();
        }

        if (normalizedType is "decimal" or "numeric" or "money" or "smallmoney")
        {
            return element.GetDecimal();
        }

        if (normalizedType is "float" or "real")
        {
            return element.GetDouble();
        }

        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        return element.GetDecimal();
    }

    private static object? ConvertStringValue(string value, string dataType)
    {
        var normalizedType = dataType.ToLowerInvariant();
        if (string.Equals(value, "<redacted>", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if ((normalizedType is "uniqueidentifier") && Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        if ((normalizedType is "date" or "datetime" or "datetime2" or "smalldatetime") && DateTime.TryParse(value, out var dateTime))
        {
            return dateTime;
        }

        if ((normalizedType is "datetimeoffset") && DateTimeOffset.TryParse(value, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        if ((normalizedType is "time") && TimeSpan.TryParse(value, out var timeSpan))
        {
            return timeSpan;
        }

        if ((normalizedType is "bit") && bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        if ((normalizedType is "binary" or "varbinary" or "image") && TryFromBase64(value, out var bytes))
        {
            return bytes;
        }

        return value;
    }

    private static bool TryFromBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static IDbCommand CreateCommand(STOTOPDbContext ctx, string sql)
    {
        var connection = ctx.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = MigrationRunner.GetConfig().CommandTimeoutSeconds;
        command.Transaction = ctx.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    private static void ExecuteNonQuery(STOTOPDbContext ctx, string sql)
    {
        using var command = CreateCommand(ctx, sql);
        command.ExecuteNonQuery();
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static object? GetRowValue(IReadOnlyDictionary<string, object?> row, string columnName)
    {
        if (TryGetRowValue(row, columnName, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"baseline 行缺少列: {columnName}");
    }

    private static bool TryGetRowValue(IReadOnlyDictionary<string, object?> row, string columnName, out object? value)
    {
        if (row.TryGetValue(columnName, out value))
        {
            return true;
        }

        foreach (var pair in row)
        {
            if (string.Equals(pair.Key, columnName, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool IsServerGeneratedColumn(BaselineColumnSnapshot column)
    {
        return column.DataType.Equals("timestamp", StringComparison.OrdinalIgnoreCase)
            || column.DataType.Equals("rowversion", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedacted(object? value)
    {
        if (value is string text)
        {
            return string.Equals(text, "<redacted>", StringComparison.OrdinalIgnoreCase);
        }

        return value is JsonElement { ValueKind: JsonValueKind.String } element
            && string.Equals(element.GetString(), "<redacted>", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveBaselinePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, BaselineRelativePath),
            Path.Combine(Directory.GetCurrentDirectory(), BaselineRelativePath),
            Path.Combine(Directory.GetCurrentDirectory(), "src/STOTOP.WebAPI", BaselineRelativePath)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string EscapeIdentifier(string value) => value.Replace("]", "]]", StringComparison.Ordinal);
}
