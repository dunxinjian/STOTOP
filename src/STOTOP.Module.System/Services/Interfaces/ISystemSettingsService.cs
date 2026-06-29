using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface ISystemSettingsService
{
    Task<ApiResult<List<SysConfigDto>>> GetAllAsync();
    Task<ApiResult<SysConfigDto?>> GetByKeyAsync(string key);
    Task<ApiResult<SysConfigDto>> UpdateAsync(string key, SysConfigUpdateDto dto);
}
