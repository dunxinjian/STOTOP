using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos;

namespace STOTOP.Module.Quality.Services.Alert;

public interface IAlertConfigService
{
    /// <summary>预警配置分页列表</summary>
    Task<ApiResult<PagedResult<AlertConfigDto>>> GetPagedAsync(long orgId, AlertConfigPagedRequest request);
    /// <summary>预警配置详情</summary>
    Task<ApiResult<AlertConfigDto>> GetByIdAsync(long orgId, long id);
    /// <summary>创建预警配置</summary>
    Task<ApiResult<AlertConfigDto>> CreateAsync(long orgId, CreateAlertConfigRequest request);
    /// <summary>更新预警配置</summary>
    Task<ApiResult<AlertConfigDto>> UpdateAsync(long orgId, long id, UpdateAlertConfigRequest request);
    /// <summary>删除预警配置</summary>
    Task<ApiResult<bool>> DeleteAsync(long orgId, long id);
    /// <summary>启用/禁用预警配置</summary>
    Task<ApiResult<bool>> ToggleAsync(long orgId, long id);
}
