using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>暂存表撤销处理器：按 FDataScopeId 批量撤销所有 STG 暂存表记录</summary>
public class StagingRevokeHandler : IDataScopeRevokeHandler
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<StagingRevokeHandler> _logger;

    public StagingRevokeHandler(STOTOPDbContext db, ILogger<StagingRevokeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public string HandlerName => "StagingRevokeHandler";

    /// <summary>IDataScopeRevokeHandler 接口实现，返回影响行数</summary>
    async Task<int> IDataScopeRevokeHandler.RevokeByDataScopeAsync(string dataScopeId, long operatorId)
    {
        var result = await RevokeByDataScopeAsync(dataScopeId, operatorId);
        return result.TotalAffected;
    }

    /// <summary>按 DataScopeId 批量撤销所有 STG 暂存表中的记录</summary>
    /// <param name="dataScopeId">数据作用域ID</param>
    /// <param name="operatorId">操作人ID</param>
    /// <returns>撤销结果</returns>
    public async Task<StagingRevokeResult> RevokeByDataScopeAsync(string dataScopeId, long operatorId)
    {
        if (string.IsNullOrEmpty(dataScopeId))
            throw new ArgumentException("DataScopeId 不能为空", nameof(dataScopeId));

        var result = new StagingRevokeResult();
        var errors = new List<string>();

        // 逐表撤销：覆盖所有实现 IStagingRecord 的 STG 实体
        result.JtCount = await RevokeTableAsync<StgJituHqTx>(dataScopeId, "STG极兔总部交易明细", errors);
        result.StCount = await RevokeTableAsync<StgShentongHqTx>(dataScopeId, "STG申通总部交易明细", errors);
        result.YdCount = await RevokeTableAsync<StgYundaHqTx>(dataScopeId, "STG韵达总部交易明细", errors);
        result.ExpenseCount = await RevokeTableAsync<StgExpenseRecord>(dataScopeId, "STG费用支出记录", errors);
        result.OutboundCount = await RevokeTableAsync<StgShentongOutbound>(dataScopeId, "STG申通出港运单数据", errors);
        result.DynamicCount = await RevokeTableAsync<StgDynamicRecord>(dataScopeId, "STG通用记录", errors);
        result.DeliveryCount = await RevokeTableAsync<StgShentongDeliveryDaily>(
            dataScopeId, "STG申通派件日明细", errors);  // 对派件恒 0 行(FDataScopeId 恒 NULL)；真正撤销走 F批次ID 物理删除

        result.TotalAffected = result.JtCount + result.StCount + result.YdCount
            + result.ExpenseCount + result.OutboundCount + result.DynamicCount + result.DeliveryCount;
        result.Errors = errors;

        // 记录撤销日志
        if (result.TotalAffected > 0 || errors.Count > 0)
        {
            var revokeLog = new WfRevokeLog
            {
                FOrgId = 1, // 系统级操作
                FDataScopeId = dataScopeId,
                FOperatorId = operatorId,
                FRevokeType = "StagingRevoke",
                FTargetTable = "STG暂存表(全部)",
                FAffectedRows = result.TotalAffected,
                FRevokeStrategy = "MarkRevoked",
                FIsSuccess = errors.Count == 0,
                FErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
            };

            _db.Set<WfRevokeLog>().Add(revokeLog);
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation(
            "StagingRevokeHandler: DataScopeId={DataScopeId} 撤销完成，" +
            "总影响{Total}行（极兔{Jt}、申通{St}、韵达{Yd}、费用{Expense}、出港{Outbound}、通用{Dynamic}、派件{Delivery}），失败{Errors}项",
            dataScopeId, result.TotalAffected,
            result.JtCount, result.StCount, result.YdCount, result.ExpenseCount, result.OutboundCount, result.DynamicCount,
            result.DeliveryCount, errors.Count);

        return result;
    }

    /// <summary>对单个 STG 表执行批量撤销</summary>
    private async Task<int> RevokeTableAsync<T>(string dataScopeId, string tableName, List<string> errors)
        where T : class, IStagingRecord
    {
        try
        {
            var affected = await _db.Set<T>()
                .Where(r => r.FDataScopeId == dataScopeId && !r.FIsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.FIsRevoked, true));

            if (affected > 0)
            {
                _logger.LogDebug("StagingRevokeHandler: {Table} 撤销 {Count} 条记录，DataScopeId={DataScopeId}",
                    tableName, affected, dataScopeId);
            }

            return affected;
        }
        catch (Exception ex)
        {
            var msg = $"{tableName} 撤销失败: {ex.Message}";
            _logger.LogWarning(ex, "StagingRevokeHandler: {Message}", msg);
            errors.Add(msg);
            return 0;
        }
    }
}

/// <summary>暂存表撤销结果</summary>
public class StagingRevokeResult
{
    public int TotalAffected { get; set; }
    public int JtCount { get; set; }
    public int StCount { get; set; }
    public int YdCount { get; set; }
    public int ExpenseCount { get; set; }
    public int OutboundCount { get; set; }
    public int DynamicCount { get; set; }
    public int DeliveryCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
