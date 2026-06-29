using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos;

namespace STOTOP.Module.Quality.Services.Exception;

public interface IExceptionService
{
    /// <summary>异常单分页列表</summary>
    Task<ApiResult<PagedResult<ExceptionListDto>>> GetPagedAsync(long orgId, ExceptionPagedRequest request);
    /// <summary>异常单详情</summary>
    Task<ApiResult<ExceptionDetailDto>> GetDetailAsync(long orgId, long id);
    /// <summary>创建异常单</summary>
    Task<ApiResult<ExceptionListDto>> CreateAsync(long orgId, long operatorId, CreateExceptionRequest request);
    /// <summary>更新异常单</summary>
    Task<ApiResult<ExceptionListDto>> UpdateAsync(long orgId, long operatorId, long id, UpdateExceptionRequest request);
    /// <summary>删除异常单</summary>
    Task<ApiResult<bool>> DeleteAsync(long orgId, long operatorId, long id);
    /// <summary>派发异常单</summary>
    Task<ApiResult<bool>> DispatchAsync(long orgId, long operatorId, long id, DispatchExceptionRequest request);
    /// <summary>关闭异常单</summary>
    Task<ApiResult<bool>> CloseAsync(long orgId, long operatorId, long id, CloseExceptionRequest request);
    /// <summary>转派异常单</summary>
    Task<ApiResult<bool>> ReassignAsync(long orgId, long operatorId, long id, ReassignExceptionRequest request);
    /// <summary>各状态数量统计</summary>
    Task<ApiResult<ExceptionCountByStatusDto>> GetCountByStatusAsync(long orgId);
}
