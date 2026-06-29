using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface ITagService
{
    Task<ApiResult<List<TagListDto>>> GetListAsync(long orgId);
    Task<ApiResult<TagListDto>> CreateAsync(CreateTagRequest request, long orgId);
    Task<ApiResult<TagListDto>> UpdateAsync(long id, UpdateTagRequest request);
    Task<ApiResult<bool>> DeleteAsync(long id);
}
