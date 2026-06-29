using STOTOP.Core.Models;
using STOTOP.Module.KSF.Dtos;

namespace STOTOP.Module.KSF.Services;

/// <summary>
/// KSF 员工经营单元映射服务
/// </summary>
public interface IKsfMappingService
{
    Task<ApiResult<List<KsfMappingDto>>> GetListAsync(long orgId, long? employeeId = null);
    Task<ApiResult> BatchSaveAsync(long orgId, List<KsfMappingBatchRequest> mappings);
}
