using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IOrganizationService
{
    Task<ApiResult<List<OrganizationDto>>> GetTreeAsync();
    Task<ApiResult<List<OrganizationDto>>> GetOrgChartAsync();
    Task<ApiResult<OrganizationDto>> CreateAsync(CreateOrganizationRequest request);
    Task<ApiResult<OrganizationDto>> UpdateAsync(long id, UpdateOrganizationRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
}
