using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Staging;

public class StagingService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<StagingService> _logger;

    private static readonly Dictionary<string, string> _tableAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jt"] = "STG极兔总部交易明细",
        ["st"] = "STG申通总部交易明细",
        ["yd"] = "STG韵达总部交易明细",
        ["expense"] = "STG费用支出记录",
        ["outbound"] = "STG申通出港运单数据",
        ["outbound_fee"] = "STG_出港运费",
        ["deliver"] = "STG申通派件日明细",
    };

    public StagingService(STOTOPDbContext context, ILogger<StagingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>按目标暂存表查询数据（分页+筛选+排序）</summary>
    public async Task<PagedResult<Dictionary<string, object?>>> GetStagingDataAsync(
        string targetTable, StagingQueryFilter filter, CancellationToken ct = default)
    {
        targetTable = ResolveTableName(targetTable);

        // 对于内置 EF 实体表，优先使用 EF
        // 但当 FieldName/FieldValue 筛选存在时，EF 的 ApplyFilters 不支持动态字段筛选，
        // 需要走 Raw SQL 路径（BuildWhereClause 已正确实现）
        var efQueryable = GetQueryableByTable(targetTable);
        var needsFieldFilter = !string.IsNullOrWhiteSpace(filter.FieldName);
        if (efQueryable != null && !needsFieldFilter)
        {
            var q = ApplyFilters(efQueryable, filter);
            q = ApplySorting(q, filter);
            var total = await q.CountAsync(ct);
            var items = await q
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(ct);
            return new PagedResult<Dictionary<string, object?>>
            {
                Items = items.Select(EntityToDictionary).ToList(),
                Total = total,
                PageIndex = filter.Page,
                PageSize = filter.PageSize
            };
        }

        // 对于动态暂存表，使用原始 SQL
        if (ValidateTableName(targetTable))
            return await GetStagingDataFromRawTableAsync(targetTable, filter, ct);

        throw new ArgumentException($"不支持的目标表: {targetTable}");
    }

    /// <summary>单条记录详情</summary>
    public async Task<Dictionary<string, object?>?> GetRecordAsync(
        string targetTable, long id, CancellationToken ct = default)
    {
        targetTable = ResolveTableName(targetTable);

        var efQueryable = GetQueryableByTable(targetTable);
        if (efQueryable != null)
        {
            var entity = await FindByIdFromEfAsync(targetTable, id, ct);
            return entity == null ? null : EntityToDictionary(entity);
        }

        if (ValidateTableName(targetTable))
            return await FindByIdFromRawTableAsync(targetTable, id, ct);

        throw new ArgumentException($"不支持的目标表: {targetTable}");
    }

    /// <summary>编辑单条记录</summary>
    public async Task<bool> UpdateRecordAsync(
        string targetTable, long id, Dictionary<string, object?> fields, CancellationToken ct = default)
    {
        targetTable = ResolveTableName(targetTable);

        var efQueryable = GetQueryableByTable(targetTable);
        if (efQueryable != null)
        {
            var entity = await FindByIdFromEfAsync(targetTable, id, ct);
            if (entity == null) return false;

            var type = entity.GetType();
            foreach (var (key, value) in fields)
            {
                var prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite || key == "FID") continue;

                try
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    object? converted = value == null ? null : Convert.ChangeType(value.ToString(), targetType);
                    prop.SetValue(entity, converted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "设置属性 {Prop} 失败", key);
                }
            }

            await _context.SaveChangesAsync(ct);
            return true;
        }

        if (ValidateTableName(targetTable))
            return await UpdateRecordInRawTableAsync(targetTable, id, fields, ct);

        throw new ArgumentException($"不支持的目标表: {targetTable}");
    }

    /// <summary>批量删除</summary>
    public async Task<int> BatchDeleteAsync(
        string targetTable, List<long> ids, CancellationToken ct = default)
    {
        targetTable = ResolveTableName(targetTable);

        if (ids.Count == 0) return 0;

        switch (targetTable)
        {
            case "STG极兔总部交易明细":
                return await _context.Set<StgJituHqTx>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteDeleteAsync(ct);
            case "STG申通总部交易明细":
                return await _context.Set<StgShentongHqTx>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteDeleteAsync(ct);
            case "STG韵达总部交易明细":
                return await _context.Set<StgYundaHqTx>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteDeleteAsync(ct);
            case "STG费用支出记录":
                return await _context.Set<StgExpenseRecord>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteDeleteAsync(ct);
            case "STG申通出港运单数据":
                return await _context.Set<StgShentongOutbound>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteDeleteAsync(ct);
            case "STG申通派件日明细":
                return await _context.Set<StgShentongDeliveryDaily>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteDeleteAsync(ct);
            default:
                if (ValidateTableName(targetTable))
                    return await DeleteFromRawTableAsync(targetTable, ids, ct);
                throw new ArgumentException($"不支持的目标表: {targetTable}");
        }
    }

    /// <summary>批量修改处理状态</summary>
    public async Task<int> BatchUpdateStatusAsync(
        string targetTable, List<long> ids, int newStatus, CancellationToken ct = default)
    {
        targetTable = ResolveTableName(targetTable);

        if (ids.Count == 0) return 0;

        switch (targetTable)
        {
            case "STG极兔总部交易明细":
                return await _context.Set<StgJituHqTx>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.F处理状态, newStatus), ct);
            case "STG申通总部交易明细":
                return await _context.Set<StgShentongHqTx>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.F处理状态, newStatus), ct);
            case "STG韵达总部交易明细":
                return await _context.Set<StgYundaHqTx>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.F处理状态, newStatus), ct);
            case "STG费用支出记录":
                return await _context.Set<StgExpenseRecord>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.F处理状态, newStatus), ct);
            case "STG申通出港运单数据":
                return await _context.Set<StgShentongOutbound>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.F处理状态, newStatus), ct);
            case "STG申通派件日明细":
                return await _context.Set<StgShentongDeliveryDaily>()
                    .Where(e => ids.Contains(e.FID))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.F处理状态, newStatus), ct);
            default:
                if (ValidateTableName(targetTable))
                    return await UpdateStatusInRawTableAsync(targetTable, ids, newStatus, ct);
                throw new ArgumentException($"不支持的目标表: {targetTable}");
        }
    }

    /// <summary>暂存表统计</summary>
    public async Task<StagingStatsDto> GetStatsAsync(
        string targetTable, CancellationToken ct = default)
    {
        targetTable = ResolveTableName(targetTable);

        var stats = new StagingStatsDto { TargetTable = targetTable };

        switch (targetTable)
        {
            case "STG极兔总部交易明细":
            {
                var set = _context.Set<StgJituHqTx>();
                stats.TotalCount = await set.CountAsync(ct);
                stats.UnprocessedCount = await set.CountAsync(e => e.F处理状态 == 0, ct);
                stats.ProcessedCount = await set.CountAsync(e => e.F处理状态 == 1, ct);
                stats.FailedCount = await set.CountAsync(e => e.F处理状态 == 2, ct);
                stats.TotalIncome = await set.SumAsync(e => e.F发生金额 ?? 0, ct);
                stats.TotalExpense = 0;
                break;
            }
            case "STG申通总部交易明细":
            {
                var set = _context.Set<StgShentongHqTx>();
                stats.TotalCount = await set.CountAsync(ct);
                stats.UnprocessedCount = await set.CountAsync(e => e.F处理状态 == 0, ct);
                stats.ProcessedCount = await set.CountAsync(e => e.F处理状态 == 1, ct);
                stats.FailedCount = await set.CountAsync(e => e.F处理状态 == 2, ct);
                stats.TotalIncome = await set.SumAsync(e => e.F发生额收入 ?? 0, ct);
                stats.TotalExpense = await set.SumAsync(e => e.F发生额支出 ?? 0, ct);
                break;
            }
            case "STG韵达总部交易明细":
            {
                var set = _context.Set<StgYundaHqTx>();
                stats.TotalCount = await set.CountAsync(ct);
                stats.UnprocessedCount = await set.CountAsync(e => e.F处理状态 == 0, ct);
                stats.ProcessedCount = await set.CountAsync(e => e.F处理状态 == 1, ct);
                stats.FailedCount = await set.CountAsync(e => e.F处理状态 == 2, ct);
                stats.TotalIncome = await set.SumAsync(e => e.F交易金额, ct);
                stats.TotalExpense = 0;
                break;
            }
            case "STG费用支出记录":
            {
                var set = _context.Set<StgExpenseRecord>();
                stats.TotalCount = await set.CountAsync(ct);
                stats.UnprocessedCount = await set.CountAsync(e => e.F处理状态 == 0, ct);
                stats.ProcessedCount = await set.CountAsync(e => e.F处理状态 == 1, ct);
                stats.FailedCount = await set.CountAsync(e => e.F处理状态 == 2, ct);
                stats.TotalIncome = 0;
                stats.TotalExpense = await set.SumAsync(e => e.F支出金额, ct);
                break;
            }
            case "STG申通出港运单数据":
            {
                var set = _context.Set<StgShentongOutbound>();
                stats.TotalCount = await set.CountAsync(ct);
                stats.UnprocessedCount = await set.CountAsync(e => e.F处理状态 == 0, ct);
                stats.ProcessedCount = await set.CountAsync(e => e.F处理状态 == 1, ct);
                stats.FailedCount = await set.CountAsync(e => e.F处理状态 == 2, ct);
                stats.TotalIncome = 0;
                stats.TotalExpense = 0;
                break;
            }
            case "STG申通派件日明细":
            {
                var set = _context.Set<StgShentongDeliveryDaily>();
                stats.TotalCount = await set.CountAsync(ct);
                stats.UnprocessedCount = await set.CountAsync(e => e.F处理状态 == 0, ct);
                stats.ProcessedCount = await set.CountAsync(e => e.F处理状态 == 1, ct);
                stats.FailedCount = await set.CountAsync(e => e.F处理状态 == 2, ct);
                stats.TotalIncome = 0;
                stats.TotalExpense = 0;
                break;
            }
            default:
                if (ValidateTableName(targetTable))
                    await FillStatsFromRawTableAsync(stats, targetTable, ct);
                break;
        }

        return stats;
    }

    /// <summary>导出数据为字典列表（供导出Excel用）</summary>
    public async Task<List<Dictionary<string, object?>>> ExportDataAsync(
        string targetTable, StagingQueryFilter filter, CancellationToken ct = default)
    {
        targetTable = ResolveTableName(targetTable);

        var efQueryable = GetQueryableByTable(targetTable);
        if (efQueryable != null)
        {
            var query = ApplyFilters(efQueryable, filter);
            query = ApplySorting(query, filter);
            var items = await query.ToListAsync(ct);
            return items.Select(EntityToDictionary).ToList();
        }

        if (ValidateTableName(targetTable))
        {
            var exportFilter = new StagingQueryFilter
            {
                Page = 1,
                PageSize = int.MaxValue,
                BatchId = filter.BatchId,
                Status = filter.Status,
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                Keyword = filter.Keyword,
                SortBy = filter.SortBy,
                SortDesc = filter.SortDesc
            };
            var paged = await GetStagingDataFromRawTableAsync(targetTable, exportFilter, ct);
            return paged.Items;
        }

        throw new ArgumentException($"不支持的目标表: {targetTable}");
    }

    #region Private Helpers

    /// <summary>解析表名别名，如果是别名则返回对应的实际表名，否则原样返回</summary>
    private static string ResolveTableName(string targetTable)
    {
        return _tableAliases.TryGetValue(targetTable, out var resolved) ? resolved : targetTable;
    }

    private IQueryable<object>? GetQueryableByTable(string targetTable) => targetTable switch
    {
        "STG极兔总部交易明细" => _context.Set<StgJituHqTx>(),
        "STG申通总部交易明细" => _context.Set<StgShentongHqTx>(),
        "STG韵达总部交易明细" => _context.Set<StgYundaHqTx>(),
        "STG费用支出记录" => _context.Set<StgExpenseRecord>(),
        "STG申通出港运单数据" => _context.Set<StgShentongOutbound>(),
        "STG申通派件日明细" => _context.Set<StgShentongDeliveryDaily>(),
        _ => null
    };

    private async Task<object?> FindByIdFromEfAsync(string targetTable, long id, CancellationToken ct)
    {
        return targetTable switch
        {
            "STG极兔总部交易明细" => (object?)await _context.Set<StgJituHqTx>().FirstOrDefaultAsync(e => e.FID == id, ct),
            "STG申通总部交易明细" => (object?)await _context.Set<StgShentongHqTx>().FirstOrDefaultAsync(e => e.FID == id, ct),
            "STG韵达总部交易明细" => (object?)await _context.Set<StgYundaHqTx>().FirstOrDefaultAsync(e => e.FID == id, ct),
            "STG费用支出记录" => (object?)await _context.Set<StgExpenseRecord>().FirstOrDefaultAsync(e => e.FID == id, ct),
            "STG申通出港运单数据" => (object?)await _context.Set<StgShentongOutbound>().FirstOrDefaultAsync(e => e.FID == id, ct),
            "STG申通派件日明细" => (object?)await _context.Set<StgShentongDeliveryDaily>().FirstOrDefaultAsync(e => e.FID == id, ct),
            _ => null
        };
    }

    private static IQueryable<object> ApplyFilters(IQueryable<object> query, StagingQueryFilter filter)
    {
        // 使用 IStagingRecord 接口进行通用筛选
        if (filter.BatchId.HasValue)
        {
            var batchId = filter.BatchId.Value;
            query = query.Where(e => ((IStagingRecord)e).F批次ID == batchId);
        }

        if (filter.Status.HasValue)
        {
            var status = filter.Status.Value;
            query = query.Where(e => ((IStagingRecord)e).F处理状态 == status);
        }

        if (filter.StartDate.HasValue)
        {
            var start = filter.StartDate.Value;
            query = query.Where(e => ((IStagingRecord)e).F创建时间 >= start);
        }

        if (filter.EndDate.HasValue)
        {
            var end = filter.EndDate.Value.Date.AddDays(1);
            query = query.Where(e => ((IStagingRecord)e).F创建时间 < end);
        }

        // 关键字搜索 — 对字符串属性进行反射匹配在 EF 中不可行，
        // 改为对每种类型的特定字段过滤，这里用动态方式简化：
        // 由于 EF 不支持对 object 的复杂表达式，关键字搜索在内存中处理不现实，
        // 暂不在 IQueryable 层面实现关键字搜索（需要具体类型才能生成SQL）

        return query;
    }

    private static IQueryable<object> ApplySorting(IQueryable<object> query, StagingQueryFilter filter)
    {
        if (string.IsNullOrEmpty(filter.SortBy))
        {
            // 默认按 FID 降序
            return query.OrderByDescending(e => ((IStagingRecord)e).FID);
        }

        // 使用 IStagingRecord 接口的常用排序字段
        return filter.SortBy switch
        {
            "FID" => filter.SortDesc
                ? query.OrderByDescending(e => ((IStagingRecord)e).FID)
                : query.OrderBy(e => ((IStagingRecord)e).FID),
            "F批次ID" => filter.SortDesc
                ? query.OrderByDescending(e => ((IStagingRecord)e).F批次ID)
                : query.OrderBy(e => ((IStagingRecord)e).F批次ID),
            "F处理状态" => filter.SortDesc
                ? query.OrderByDescending(e => ((IStagingRecord)e).F处理状态)
                : query.OrderBy(e => ((IStagingRecord)e).F处理状态),
            "F创建时间" => filter.SortDesc
                ? query.OrderByDescending(e => ((IStagingRecord)e).F创建时间)
                : query.OrderBy(e => ((IStagingRecord)e).F创建时间),
            _ => query.OrderByDescending(e => ((IStagingRecord)e).FID)
        };
    }

    private static Dictionary<string, object?> EntityToDictionary(object entity)
    {
        var dict = new Dictionary<string, object?>();
        var props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            dict[prop.Name] = prop.GetValue(entity);
        }
        return dict;
    }

    /// <summary>校验动态表名安全性：必须以 STG 开头，仅含中文/字母/数字/下划线</summary>
    private static bool ValidateTableName(string tableName)
    {
        return Regex.IsMatch(tableName, @"^STG[\u4e00-\u9fa5A-Za-z0-9_]+$");
    }

    /// <summary>从指定的动态 STG 表查询分页数据</summary>
    private async Task<PagedResult<Dictionary<string, object?>>> GetStagingDataFromRawTableAsync(
        string tableName, StagingQueryFilter filter, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var (whereClause, parameters) = BuildWhereClause(conn, filter);

        // 查询总数
        var countSql = $"SELECT COUNT(*) FROM [{tableName}] {whereClause}";
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = countSql;
        countCmd.Parameters.AddRange(parameters.ToArray());
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // 排序
        var sortColumn = string.IsNullOrEmpty(filter.SortBy) ? "FID" : SanitizeColumnName(filter.SortBy);
        var sortDir = filter.SortDesc ? "DESC" : "ASC";

        // 分页查询
        var offset = (filter.Page - 1) * filter.PageSize;
        var dataSql = $"SELECT * FROM [{tableName}] {whereClause} ORDER BY [{sortColumn}] {sortDir} OFFSET @_offset ROWS FETCH NEXT @_pageSize ROWS ONLY";
        await using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = dataSql;
        // 重新构建参数（DbCommand 参数不可跨 Command 共享）
        var (_, dataParams) = BuildWhereClause(conn, filter);
        dataCmd.Parameters.AddRange(dataParams.ToArray());
        var offsetParam = dataCmd.CreateParameter();
        offsetParam.ParameterName = "@_offset";
        offsetParam.Value = offset;
        dataCmd.Parameters.Add(offsetParam);
        var pageSizeParam = dataCmd.CreateParameter();
        pageSizeParam.ParameterName = "@_pageSize";
        pageSizeParam.Value = filter.PageSize;
        dataCmd.Parameters.Add(pageSizeParam);

        var items = new List<Dictionary<string, object?>>();
        await using var reader = await dataCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(ReaderToDictionary(reader));
        }

        return new PagedResult<Dictionary<string, object?>>
        {
            Items = items,
            Total = total,
            PageIndex = filter.Page,
            PageSize = filter.PageSize
        };
    }

    /// <summary>从指定的动态 STG 表按 ID 查询单条记录</summary>
    private async Task<Dictionary<string, object?>?> FindByIdFromRawTableAsync(
        string tableName, long id, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var sql = $"SELECT TOP 1 * FROM [{tableName}] WHERE [FID] = @id";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "@id";
        idParam.Value = id;
        cmd.Parameters.Add(idParam);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReaderToDictionary(reader);
        }
        return null;
    }

    /// <summary>从指定的动态 STG 表批量删除</summary>
    private async Task<int> DeleteFromRawTableAsync(
        string tableName, List<long> ids, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        // 构建 IN 参数列表
        var paramNames = ids.Select((_, i) => $"@id{i}").ToList();
        var sql = $"DELETE FROM [{tableName}] WHERE [FID] IN ({string.Join(",", paramNames)})";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        for (var i = 0; i < ids.Count; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@id{i}";
            p.Value = ids[i];
            cmd.Parameters.Add(p);
        }

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>在指定的动态 STG 表更新单条记录</summary>
    private async Task<bool> UpdateRecordInRawTableAsync(
        string tableName, long id, Dictionary<string, object?> fields, CancellationToken ct)
    {
        // 过滤掉 FID 和不合法列名
        var updateFields = fields
            .Where(kv => kv.Key != "FID" && Regex.IsMatch(kv.Key, @"^[\u4e00-\u9fa5A-Za-z0-9_]+$"))
            .ToList();
        if (updateFields.Count == 0) return false;

        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var setClauses = updateFields.Select((kv, i) => $"[{kv.Key}] = @p{i}").ToList();
        var sql = $"UPDATE [{tableName}] SET {string.Join(", ", setClauses)} WHERE [FID] = @id";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        for (var i = 0; i < updateFields.Count; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            p.Value = updateFields[i].Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "@id";
        idParam.Value = id;
        cmd.Parameters.Add(idParam);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    /// <summary>在指定的动态 STG 表批量更新处理状态</summary>
    private async Task<int> UpdateStatusInRawTableAsync(
        string tableName, List<long> ids, int newStatus, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var paramNames = ids.Select((_, i) => $"@id{i}").ToList();
        var sql = $"UPDATE [{tableName}] SET [F处理状态] = @status WHERE [FID] IN ({string.Join(",", paramNames)})";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var statusParam = cmd.CreateParameter();
        statusParam.ParameterName = "@status";
        statusParam.Value = newStatus;
        cmd.Parameters.Add(statusParam);

        for (var i = 0; i < ids.Count; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@id{i}";
            p.Value = ids[i];
            cmd.Parameters.Add(p);
        }

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>从指定的动态 STG 表获取统计信息</summary>
    private async Task FillStatsFromRawTableAsync(
        StagingStatsDto stats, string tableName, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var sql = $@"SELECT
            COUNT(*) AS TotalCount,
            SUM(CASE WHEN [F处理状态] = 0 THEN 1 ELSE 0 END) AS UnprocessedCount,
            SUM(CASE WHEN [F处理状态] = 1 THEN 1 ELSE 0 END) AS ProcessedCount,
            SUM(CASE WHEN [F处理状态] = 2 THEN 1 ELSE 0 END) AS FailedCount
        FROM [{tableName}]";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            stats.TotalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
            stats.UnprocessedCount = reader.GetInt32(reader.GetOrdinal("UnprocessedCount"));
            stats.ProcessedCount = reader.GetInt32(reader.GetOrdinal("ProcessedCount"));
            stats.FailedCount = reader.GetInt32(reader.GetOrdinal("FailedCount"));
        }
        // 动态目标表不做金额汇总
        stats.TotalIncome = 0;
        stats.TotalExpense = 0;
    }

    /// <summary>构建通用 WHERE 子句和参数</summary>
    private static (string whereClause, List<DbParameter> parameters) BuildWhereClause(
        DbConnection conn, StagingQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new List<DbParameter>();

        if (filter.BatchId.HasValue)
        {
            conditions.Add("[F批次ID] = @batchId");
            parameters.Add(CreateParameter(conn, "@batchId", filter.BatchId.Value));
        }
        if (filter.Status.HasValue)
        {
            conditions.Add("[F处理状态] = @status");
            parameters.Add(CreateParameter(conn, "@status", filter.Status.Value));
        }
        if (filter.StartDate.HasValue)
        {
            conditions.Add("[F创建时间] >= @startDate");
            parameters.Add(CreateParameter(conn, "@startDate", filter.StartDate.Value));
        }
        if (filter.EndDate.HasValue)
        {
            conditions.Add("[F创建时间] < @endDate");
            parameters.Add(CreateParameter(conn, "@endDate", filter.EndDate.Value.Date.AddDays(1)));
        }

        // 按字段名+字段值精确过滤（用于查看暂存表记录明细）
        if (!string.IsNullOrWhiteSpace(filter.FieldName))
        {
            // 安全校验：只允许中文字符、英文字母、数字、下划线
            if (!Regex.IsMatch(filter.FieldName, @"^[A-Za-z\u4e00-\u9fa5_][A-Za-z0-9\u4e00-\u9fa5_]*$"))
                throw new ArgumentException($"字段名不合法: {filter.FieldName}");

            var sanitizedFieldName = filter.FieldName;
            if (string.IsNullOrEmpty(filter.FieldValue))
            {
                // 空值：NULL 或空字符串
                conditions.Add($"([{sanitizedFieldName}] IS NULL OR [{sanitizedFieldName}] = '')");
            }
            else
            {
                conditions.Add($"[{sanitizedFieldName}] = @fieldValue");
                parameters.Add(CreateParameter(conn, "@fieldValue", filter.FieldValue));
            }
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        return (whereClause, parameters);
    }

    /// <summary>创建 DbParameter 辅助方法</summary>
    private static DbParameter CreateParameter(DbConnection conn, string name, object value)
    {
        using var tempCmd = conn.CreateCommand();
        var param = tempCmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        return param;
    }

    /// <summary>将 DbDataReader 当前行转为字典</summary>
    private static Dictionary<string, object?> ReaderToDictionary(DbDataReader reader)
    {
        var dict = new Dictionary<string, object?>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }
        return dict;
    }

    /// <summary>清理排序列名：仅保留合法字符</summary>
    private static string SanitizeColumnName(string columnName)
    {
        // 仅允许中文、字母、数字、下划线
        var sanitized = Regex.Replace(columnName, @"[^\u4e00-\u9fa5A-Za-z0-9_]", "");
        return string.IsNullOrEmpty(sanitized) ? "FID" : sanitized;
    }

    #endregion
}
