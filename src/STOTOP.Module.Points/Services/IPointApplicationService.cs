using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IPointApplicationService
{
    Task<ApiResult<PointApplicationDetailDto>> SubmitAsync(long orgId, long applicantId, SubmitPointApplicationRequest request);
    Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPagedListAsync(long orgId, ApplicationPagedRequest request);
    Task<ApiResult<PagedResult<PointApplicationListDto>>> GetMyApplicationsAsync(long orgId, long userId, MyApplicationPagedRequest request);
    Task<ApiResult<PagedResult<PointApplicationListDto>>> GetPendingAsync(long orgId, PendingApplicationPagedRequest request);
    Task<ApiResult<bool>> ApproveAsync(long id, long approverId, ApprovePointApplicationRequest request);
    Task<ApiResult<bool>> RejectAsync(long id, long approverId, string reason);
}
