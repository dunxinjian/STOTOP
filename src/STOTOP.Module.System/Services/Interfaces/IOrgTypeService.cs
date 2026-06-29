using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IOrgTypeService
{
    Task<ApiResult<List<OrgTypeDto>>> GetAllAsync();
    Task<ApiResult<List<OrgTypeDto>>> GetForAccountSetAsync();
    Task<ApiResult<OrgTypeDto?>> GetByIdAsync(long id);
    Task<ApiResult<bool>> UpdateAsync(long id, OrgTypeUpdateDto dto);
}
