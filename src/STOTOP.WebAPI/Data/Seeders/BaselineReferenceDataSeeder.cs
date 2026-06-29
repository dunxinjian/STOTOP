using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        if (identityInsert)
        {
            ExecuteNonQuery(ctx, $"SET IDENTITY_INSERT [dbo].[{EscapeIdentifier(table.Name)}] ON");
        }

        try
        {
            foreach (var row in table.Rows)
            {
                UpsertRow(ctx, table.Name, columns, keyColumn, row);
            }
        }
        finally
        {
            if (identityInsert)
            {
                ExecuteNonQuery(ctx, $"SET IDENTITY_INSERT [dbo].[{EscapeIdentifier(table.Name)}] OFF");
            }
        }
    }

    private static void UpsertRow(
        STOTOPDbContext ctx,
        string tableName,
        IReadOnlyList<BaselineColumnSnapshot> columns,
        BaselineColumnSnapshot keyColumn,
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

        var sql = $"""
            IF EXISTS (SELECT 1 FROM [dbo].[{escapedTable}] WHERE [{escapedKey}] = @p_key)
            BEGIN
            {updateSql}
            END
            ELSE
            BEGIN
                INSERT INTO [dbo].[{escapedTable}] ({insertColumnSql})
                VALUES ({insertValueSql});
            END
            """;

        using var command = CreateCommand(ctx, sql);
        AddParameter(command, "@p_key", ConvertRowValue(GetRowValue(row, keyColumn.Name), keyColumn.DataType));

        for (var i = 0; i < updateColumns.Count; i++)
        {
            AddParameter(command, $"@p{i}", ConvertRowValue(GetRowValue(row, updateColumns[i].Name), updateColumns[i].DataType));
        }

        for (var i = 0; i < writableColumns.Count; i++)
        {
            AddParameter(command, $"@i{i}", ConvertRowValue(GetRowValue(row, writableColumns[i].Name), writableColumns[i].DataType));
        }

        command.ExecuteNonQuery();
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
