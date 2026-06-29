using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IManagerQuotaService
{
    Task<ApiResult<PagedResult<ManagerQuotaListDto>>> GetPagedListAsync(long orgId, ManagerQuotaPagedRequest request);
    Task<ApiResult<ManagerQuotaListDto>> SaveAsync(long orgId, SaveManagerQuotaRequest request);
    Task<ApiResult<MyQuotaDto>> GetMyQuotaAsync(long orgId, long managerId);
    /// <summary>
    /// 内部方法：使用配额（奖分或扣分时扣减配额余额）
    /// </summary>
    Task<ApiResult<bool>> UseQuotaAsync(long orgId, long managerId, int points, bool isAward);
}
