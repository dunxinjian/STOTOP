using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;

namespace STOTOP.Module.Points.Services;

public interface IPointSourceService
{
    Task<ApiResult<List<PointSourceDto>>> GetListAsync(long orgId);
    Task<ApiResult<PointSourceDto>> CreateAsync(long orgId, CreatePointSourceRequest request);
    Task<ApiResult<PointSourceDto>> UpdateAsync(long id, UpdatePointSourceRequest request);
    Task<ApiResult<bool>> ToggleAsync(long id);
}
