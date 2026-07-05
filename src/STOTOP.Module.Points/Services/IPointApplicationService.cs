using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IPointApplicationService
{
    Task<ApiResult<PointApplicationDetailDto>> SubmitAsync(long orgId, long applicantId, SubmitPointApplicationRequest request);
    Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPagedListAsync(long orgId, ApplicationPagedRequest request);
    Task<ApiResult<PagedResult<PointApplicationListDto>>> GetMyApplicationsAsync(long orgId, long userId, MyApplicationPagedRequest request);
    Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPendingAsync(long orgId, PendingApplicationPagedRequest request);
    /// <summary>待审批积分申请数量（WorkHub 角标口径：F组织ID==orgId 且 F状态==0）</summary>
    Task<int> GetPendingCountAsync(long orgId);
    Task<ApiResult<bool>> ApproveAsync(long id, long approverId, ApprovePointApplicationRequest request);
    Task<ApiResult<bool>> RejectAsync(long id, long approverId, string reason);
}
