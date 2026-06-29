using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Import;

public class SqlBulkInsertService : IBulkInsertService
{
    private readonly STOTOPDbContext _context;
    private readonly IOrgContextAccessor? _orgContextAccessor;

    public SqlBulkInsertService(STOTOPDbContext context, IOrgContextAccessor? orgContextAccessor = null)
    {
        _context = context;
        _orgContextAccessor = orgContextAccessor;
    }

    public async Task BulkInsertErrorsAsync(List<CfBatchError> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        var connection = _context.Database.GetDbConnection() as SqlConnection;
        if (connection == null) throw new InvalidOperationException("当前数据库不支持 SqlBulkCopy");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        var dt = new DataTable();
        dt.Columns.Add("F批次ID", typeof(long));
        dt.Columns.Add("F暂存ID", typeof(long));
        dt.Columns.Add("F行号", typeof(int));
        dt.Columns.Add("F错误类型", typeof(string));
        dt.Columns.Add("F严重级别", typeof(string));
        dt.Columns.Add("F错误字段", typeof(string));
        dt.Columns.Add("F错误信息", typeof(string));
        dt.Columns.Add("F建议修复", typeof(string));
        dt.Columns.Add("F原始值", typeof(string));
        dt.Columns.Add("F质量维度", typeof(string));
        dt.Columns.Add("F创建时间", typeof(DateTime));
        dt.Columns.Add("F派发状态", typeof(string));
        dt.Columns.Add("F派发方式", typeof(string));
        dt.Columns.Add("F工作项ID", typeof(long));
        dt.Columns.Add("F问题类型", typeof(string));
        dt.Columns.Add("F处理结果", typeof(int));
        dt.Columns.Add("F处理状态", typeof(string));
        dt.Columns.Add("F处理载荷JSON", typeof(string));
        dt.Columns.Add("F重跑状态", typeof(string));
        dt.Columns.Add("FOrgId", typeof(long));

        var currentOrgId = _orgContextAccessor?.CurrentOrgId ?? 0;

        foreach (var r in records)
        {
            dt.Rows.Add(
                r.FBatchId,
                (object?)r.FStagingId ?? DBNull.Value,
                r.FRowNumber,
                r.FErrorType,
                (object?)r.FSeverityLevel ?? DBNull.Value,
                (object?)r.FErrorField ?? DBNull.Value,
                (object?)r.FErrorMessage ?? DBNull.Value,
                (object?)r.FSuggestedFix ?? DBNull.Value,
                (object?)r.FOriginalValue ?? DBNull.Value,
                (object?)r.FQualityDimension ?? DBNull.Value,
                r.FCreatedTime,
                (object?)r.FDispatchStatus ?? DBNull.Value,
                (object?)r.FDispatchType ?? DBNull.Value,
                (object?)r.FWorkItemId ?? DBNull.Value,
                string.IsNullOrWhiteSpace(r.FIssueType) ? r.FErrorType : r.FIssueType,
                r.FProcessResult,
                string.IsNullOrWhiteSpace(r.FResolutionStatus) ? "Pending" : r.FResolutionStatus,
                (object?)r.FResolutionPayloadJson ?? DBNull.Value,
                string.IsNullOrWhiteSpace(r.FRetryStatus) ? "None" : r.FRetryStatus,
                r.FOrgId != 0 ? r.FOrgId : currentOrgId
            );
        }

        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = "[CF批次错误]",
            BulkCopyTimeout = 120
        };

        bulk.ColumnMappings.Add("F批次ID", "F批次ID");
        bulk.ColumnMappings.Add("F暂存ID", "F暂存ID");
        bulk.ColumnMappings.Add("F行号", "F行号");
        bulk.ColumnMappings.Add("F错误类型", "F错误类型");
        bulk.ColumnMappings.Add("F严重级别", "F严重级别");
        bulk.ColumnMappings.Add("F错误字段", "F错误字段");
        bulk.ColumnMappings.Add("F错误信息", "F错误信息");
        bulk.ColumnMappings.Add("F建议修复", "F建议修复");
        bulk.ColumnMappings.Add("F原始值", "F原始值");
        bulk.ColumnMappings.Add("F质量维度", "F质量维度");
        bulk.ColumnMappings.Add("F创建时间", "F创建时间");
        bulk.ColumnMappings.Add("F派发状态", "F派发状态");
        bulk.ColumnMappings.Add("F派发方式", "F派发方式");
        bulk.ColumnMappings.Add("F工作项ID", "F工作项ID");
        bulk.ColumnMappings.Add("F问题类型", "F问题类型");
        bulk.ColumnMappings.Add("F处理结果", "F处理结果");
        bulk.ColumnMappings.Add("F处理状态", "F处理状态");
        bulk.ColumnMappings.Add("F处理载荷JSON", "F处理载荷JSON");
        bulk.ColumnMappings.Add("F重跑状态", "F重跑状态");
        bulk.ColumnMappings.Add("FOrgId", "FOrgId");

        await bulk.WriteToServerAsync(dt, ct);
    }

    public async Task<int> BulkInsertTargetAsync(
        string connectionString,
        string targetTableName,
        List<Dictionary<string, object?>> rows,
        CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        // 从第一行的 key 集合动态构建列（过滤空列名，全部为 string 类型，让 SQL Server 隐式转换）
        var columns = rows[0].Keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        if (columns.Count == 0)
            throw new InvalidOperationException($"目标表 [{targetTableName}] 没有有效的列数据，请检查字段映射配置");

        var dt = new DataTable();
        foreach (var col in columns)
            dt.Columns.Add(col, typeof(string));

        foreach (var row in rows)
        {
            var dataRow = dt.NewRow();
            foreach (var col in columns)
            {
                dataRow[col] = row.TryGetValue(col, out var val) && val != null
                    ? val.ToString()
                    : (object)DBNull.Value;
            }
            dt.Rows.Add(dataRow);
        }

        // 创建独立连接（目标库可能不是主库）
        using var targetConnection = new SqlConnection(connectionString);
        await targetConnection.OpenAsync(ct);

        using var bulk = new SqlBulkCopy(targetConnection)
        {
            DestinationTableName = $"[{targetTableName}]",
            BulkCopyTimeout = 300
        };

        // key 已经就是目标表列名，直接映射
        foreach (var col in columns)
            bulk.ColumnMappings.Add(col, col);

        await bulk.WriteToServerAsync(dt, ct);
        return rows.Count;
    }
}
