using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

public static class BaselineSnapshotExporter
{
    private const int MaxRowsPerTable = 5000;

    private static readonly HashSet<string> BaselineDataTableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS组织类型",
        "SYS功能权限",
        "SYS编码规则",
        "SYS安全配置",
        "FIN科目模板",
        "FIN科目模板_明细",
        "FIN辅助核算类型",
        "EXP品牌",
        "EXP省份",
        "EXP城市",
        "EXP成本项目",
        "WF触发动作",
        "CF自动插件注册"
    };

    private static readonly string[] SensitiveColumnKeywords =
    [
        "password",
        "passwd",
        "pwd",
        "token",
        "secret",
        "refresh",
        "密码",
        "密钥",
        "令牌"
    ];

    public static void Export(STOTOPDbContext ctx, string outputPath)
    {
        Export(ctx.Database.GetDbConnection(), outputPath);
    }

    public static void Export(string connectionString, string outputPath)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 10,
            TrustServerCertificate = true
        };
        using var connection = new SqlConnection(builder.ConnectionString);
        Export(connection, outputPath);
    }

    private static void Export(IDbConnection connection, string outputPath)
    {
        Console.WriteLine("[baseline-export] 读取表清单...");
        var tableNames = GetTables(connection);
        Console.WriteLine($"[baseline-export] 发现 {tableNames.Count} 张 dbo 表");

        var tables = new List<BaselineTableSnapshot>();
        for (var i = 0; i < tableNames.Count; i++)
        {
            var tableName = tableNames[i];
            Console.WriteLine($"[baseline-export] ({i + 1}/{tableNames.Count}) {tableName}");
            tables.Add(ExportTable(connection, tableName));
        }

        var snapshot = new BaselineSnapshot
        {
            GeneratedAt = DateTimeOffset.Now,
            Tables = tables
        };

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"[baseline-export] 已写入 {outputPath}");
    }

    private static BaselineTableSnapshot ExportTable(IDbConnection connection, string tableName)
    {
        var columns = GetColumns(connection, tableName);
        var rowCount = GetRowCount(connection, tableName);
        var exportRows = BaselineDataTableNames.Contains(tableName) && rowCount <= MaxRowsPerTable;

        return new BaselineTableSnapshot
        {
            Name = tableName,
            RowCount = rowCount,
            ExportMode = exportRows ? "schema-and-baseline-data" : "schema-only",
            Columns = columns,
            Rows = exportRows ? GetRows(connection, tableName, columns) : []
        };
    }

    private static List<string> GetTables(IDbConnection connection)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'dbo'
              AND TABLE_TYPE = N'BASE TABLE'
            ORDER BY TABLE_NAME
        """;

        var result = new List<string>();
        using var command = CreateCommand(connection, sql);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static List<BaselineColumnSnapshot> GetColumns(IDbConnection connection, string tableName)
    {
        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo'
              AND TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION
            """;

        var result = new List<BaselineColumnSnapshot>();
        using var command = CreateCommand(connection, sql);
        AddParameter(command, "@tableName", tableName);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            result.Add(new BaselineColumnSnapshot
            {
                Name = name,
                DataType = reader.GetString(1),
                IsNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
                IsIdentity = reader.GetInt32(3) == 1,
                IsSensitive = IsSensitiveColumn(name)
            });
        }

        return result;
    }

    private static int GetRowCount(IDbConnection connection, string tableName)
    {
        const string sql = """
            SELECT COALESCE(SUM(row_count), 0)
            FROM sys.dm_db_partition_stats
            WHERE object_id = OBJECT_ID(@fullTableName)
              AND index_id IN (0, 1)
            """;

        using var command = CreateCommand(connection, sql);
        AddParameter(command, "@fullTableName", $"dbo.{tableName}");
        var value = command.ExecuteScalar();
        return Convert.ToInt32(value);
    }

    private static List<Dictionary<string, object?>> GetRows(
        IDbConnection connection,
        string tableName,
        List<BaselineColumnSnapshot> columns)
    {
        var orderedColumn = columns.FirstOrDefault(c => string.Equals(c.Name, "FID", StringComparison.OrdinalIgnoreCase))?.Name
            ?? columns.First().Name;
        var selectColumns = string.Join(", ", columns.Select(c => $"[{EscapeIdentifier(c.Name)}]"));
        var sql = $"""
            SELECT {selectColumns}
            FROM [dbo].[{EscapeIdentifier(tableName)}]
            ORDER BY [{EscapeIdentifier(orderedColumn)}]
            """;

        var rows = new List<Dictionary<string, object?>>();
        using var command = CreateCommand(connection, sql);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                row[column.Name] = column.IsSensitive
                    ? "<redacted>"
                    : NormalizeValue(reader[column.Name]);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static object? NormalizeValue(object value)
    {
        if (value == DBNull.Value)
        {
            return null;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("O");
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("O");
        }

        if (value is byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        return value;
    }

    private static bool IsSensitiveColumn(string columnName)
    {
        return SensitiveColumnKeywords.Any(keyword =>
            columnName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static IDbCommand CreateCommand(IDbConnection connection, string sql)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        return command;
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string EscapeIdentifier(string value) => value.Replace("]", "]]", StringComparison.Ordinal);
}

public class BaselineSnapshot
{
    public DateTimeOffset GeneratedAt { get; set; }
    public List<BaselineTableSnapshot> Tables { get; set; } = [];
}

public class BaselineTableSnapshot
{
    public string Name { get; set; } = "";
    public int RowCount { get; set; }
    public string ExportMode { get; set; } = "schema-only";
    public List<BaselineColumnSnapshot> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}

public class BaselineColumnSnapshot
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsSensitive { get; set; }
}
