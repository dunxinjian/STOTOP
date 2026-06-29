using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface IProgressReportService
{
    /// <summary>
    /// 进度上报历史列表（分页）
    /// </summary>
    Task<ApiResult<PagedResult<ProgressReportListDto>>> GetPagedListAsync(long taskId, ProgressReportPagedRequest query);

    /// <summary>
    /// 提交进度上报（更新任务进度+累加工时+记录活动日志）
    /// </summary>
    Task<ApiResult<ProgressReportListDto>> CreateAsync(long taskId, CreateProgressReportRequest request, long operatorId);
}
