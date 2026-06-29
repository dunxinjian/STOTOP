using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IRankingService
{
    Task<ApiResult<PagedResult<RankingListDto>>> GetRankingsAsync(long orgId, RankingPagedRequest request);
    Task<ApiResult<List<DepartmentRankingDto>>> GetDepartmentRankingsAsync(long orgId, DepartmentRankingRequest request);
    Task<ApiResult<MyRankingDto>> GetMyRankingAsync(long orgId, long userId, int dimension, string? period);
    /// <summary>
    /// 生成排名快照（供定时Job调用）
    /// </summary>
    Task<ApiResult<bool>> GenerateSnapshotAsync(long orgId, int dimension, string period);
}
