using STOTOP.Core.Models;
using STOTOP.Module.PPV.Dtos;

namespace STOTOP.Module.PPV.Services;

/// <summary>
/// PPV 月度汇总服务
/// </summary>
public interface IPpvResultService
{
    Task<ApiResult<List<PpvMonthlyResultDto>>> GetListAsync(long orgId, string? period, long? employeeId, int page, int pageSize);
    Task<ApiResult<PpvMonthlyResultDto>> GetDetailAsync(long orgId, long id);
    Task<ApiResult<List<PpvMonthlyResultDto>>> GetMyResultsAsync(long orgId, long employeeId, string? period);
}
