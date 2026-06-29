using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Dispatch;

public interface IDispatchService
{
    /// <summary>
    /// 异常数据聚合查询（关联批次信息）
    /// </summary>
    Task<PagedResult<ImportErrorDto>> GetImportErrorsAsync(ImportErrorQueryDto query);

    /// <summary>
    /// 导入总览统计
    /// </summary>
    Task<ImportOverviewDto> GetImportOverviewAsync();

    /// <summary>
    /// 创建派发记录（同时更新错误记录的派发状态）
    /// </summary>
    Task<BusinessDispatchRecordDto> CreateDispatchAsync(CreateDispatchDto dto, string operatorName);

    /// <summary>
    /// 批量创建派发记录
    /// </summary>
    Task<List<BusinessDispatchRecordDto>> CreateBatchDispatchAsync(CreateDispatchDto dto, string operatorName);

    /// <summary>
    /// 查询派发记录（分页+筛选）
    /// </summary>
    Task<PagedResult<BusinessDispatchRecordDto>> GetDispatchRecordsAsync(int page, int pageSize, string? status, long? batchId);

    /// <summary>
    /// 更新派发状态
    /// </summary>
    Task<BusinessDispatchRecordDto?> UpdateDispatchStatusAsync(long id, string status, string? result);

    /// <summary>
    /// 批量忽略错误
    /// </summary>
    Task<int> BatchIgnoreErrorsAsync(long[] errorIds);
}
