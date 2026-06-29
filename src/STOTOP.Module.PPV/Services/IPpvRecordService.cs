using STOTOP.Core.Models;
using STOTOP.Module.PPV.Dtos;

namespace STOTOP.Module.PPV.Services;

/// <summary>
/// PPV 产值记录服务
/// </summary>
public interface IPpvRecordService
{
    Task<ApiResult<List<PpvRecordDto>>> GetListAsync(long orgId, PpvRecordPagedRequest request);
    Task<ApiResult<PpvRecordDto>> CreateAsync(long orgId, long currentUserId, CreatePpvRecordRequest request);
    Task<ApiResult<PpvRecordDto>> UpdateAsync(long orgId, long id, UpdatePpvRecordRequest request);
    Task<ApiResult> ReviewAsync(long orgId, long id, long reviewerId, ReviewPpvRecordRequest request);
    Task<ApiResult<List<PpvRecordDto>>> GetMyRecordsAsync(long orgId, long employeeId, string? period);
}
