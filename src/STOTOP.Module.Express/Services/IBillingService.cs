using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 计费服务接口
/// </summary>
public interface IBillingService
{
    /// <summary>执行计费（接收标准化运单数据）</summary>
    Task<BillingExecutionResult> ExecuteBillingAsync(
        List<BillingWaybillData> waybills, string sourceTable, long batchId, string resultTable);
    /// <summary>分页查询计费结果</summary>
    Task<PagedResult<BillingResultListItemDto>> GetResultListAsync(BillingResultQueryRequest request);
    /// <summary>获取计费结果详情</summary>
    Task<BillingResultDto?> GetResultByIdAsync(long id);
    /// <summary>按运单查询所有计费结果</summary>
    Task<List<BillingResultDto>> GetResultsByWaybillAsync(long waybillId);
    /// <summary>查询异常运单统计</summary>
    Task<BillingErrorStatsDto> GetErrorStatsAsync(string? brandCode);
    /// <summary>查询某类异常的运单明细</summary>
    Task<PagedResult<BillingErrorDetailItemDto>> GetErrorDetailAsync(BillingErrorDetailRequest request);
    /// <summary>触发异常运单重算</summary>
    Task<BillingRetryResultDto> RetryBillingAsync(BillingRetryRequest request);
}
