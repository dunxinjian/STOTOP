using STOTOP.Core.Models;
using STOTOP.Module.PPV.Dtos;

namespace STOTOP.Module.PPV.Services;

/// <summary>
/// PPV 产值模板服务
/// </summary>
public interface IPpvTemplateService
{
    Task<ApiResult<List<PpvTemplateDto>>> GetListAsync(long orgId, int page, int pageSize, long? positionId);
    Task<ApiResult<PpvTemplateDto>> CreateAsync(long orgId, CreatePpvTemplateRequest request);
    Task<ApiResult<PpvTemplateDto>> UpdateAsync(long orgId, long id, UpdatePpvTemplateRequest request);
    Task<ApiResult> EnableAsync(long orgId, long id);
    Task<ApiResult<List<PpvTemplateDto>>> GetByPositionAsync(long orgId, long positionId);
}
