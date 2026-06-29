using STOTOP.Core.Models;
using STOTOP.Module.KSF.Dtos;

namespace STOTOP.Module.KSF.Services;

/// <summary>
/// KSF 核算结果服务
/// </summary>
public interface IKsfResultService
{
    Task<ApiResult<List<KsfResultDto>>> GetListAsync(long orgId, string? period = null, long? employeeId = null, int? status = null);
    Task<ApiResult<KsfResultDto>> GetDetailAsync(long orgId, long resultId);
    Task<ApiResult<List<KsfResultDto>>> GetMyResultsAsync(long orgId, long userId, int count = 12);
}
