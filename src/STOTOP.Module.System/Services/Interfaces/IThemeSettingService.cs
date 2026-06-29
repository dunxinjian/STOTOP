using STOTOP.Core.Models;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IThemeSettingService
{
    Task<ApiResult<string>> GetThemeSettingsAsync();
    Task<ApiResult<string>> UpdateThemeSettingsAsync(string configJson, string? updatedBy);
}
