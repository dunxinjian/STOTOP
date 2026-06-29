using System.Data;
using Microsoft.Data.SqlClient;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Import;

public class EfCoreBulkInsertService : IBulkInsertService
{
    private readonly STOTOPDbContext _context;

    public EfCoreBulkInsertService(STOTOPDbContext context) => _context = context;

    public async Task BulkInsertErrorsAsync(List<CfBatchError> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        _context.Set<CfBatchError>().AddRange(records);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> BulkInsertTargetAsync(
        string connectionString,
        string targetTableName,
        List<Dictionary<string, object?>> rows,
        CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        var columns = rows[0].Keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        if (columns.Count == 0)
            throw new InvalidOperationException($"目标表 [{targetTableName}] 没有有效的列数据，请检查字段映射配置");

        // 构建参数化 INSERT 语句
        var colList = string.Join(",", columns.Select(c => $"[{c}]"));
        var paramList = string.Join(",", columns.Select((_, i) => $"@p{i}"));
        var sql = $"INSERT INTO [{targetTableName}] ({colList}) VALUES ({paramList})";

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        int inserted = 0;
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            using var cmd = new SqlCommand(sql, connection);
            for (int i = 0; i < columns.Count; i++)
            {
                var val = row.TryGetValue(columns[i], out var v) ? v : null;
                cmd.Parameters.AddWithValue($"@p{i}", val ?? (object)DBNull.Value);
            }
            await cmd.ExecuteNonQueryAsync(ct);
            inserted++;
        }

        return inserted;
    }
}
