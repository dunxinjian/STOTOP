using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class ThemeSettingService : IThemeSettingService
{
    private readonly STOTOPDbContext _context;

    private const string DefaultConfigJson = """
    {
        "colorPrimary": "#E85E00",
        "colorSuccess": "#3E9E6E",
        "colorWarning": "#D49A2E",
        "colorError": "#D6584E",
        "colorInfo": "#5B7290",
        "borderRadius": 6,
        "fontSize": 14,
        "sizeStep": 4,
        "sizeUnit": 4,
        "wireframe": false,
        "compactMode": false,
        "darkMode": false,
        "marginXS": 8,
        "marginSM": 12,
        "margin": 16,
        "marginMD": 20,
        "marginLG": 24
    }
    """;

    public ThemeSettingService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResult<string>> GetThemeSettingsAsync()
    {
        var setting = await _context.Set<SysThemeSetting>()
            .AsTracking()
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            return ApiResult<string>.Success(DefaultConfigJson);
        }

        return ApiResult<string>.Success(setting.FConfigJson);
    }

    public async Task<ApiResult<string>> UpdateThemeSettingsAsync(string configJson, string? updatedBy)
    {
        var setting = await _context.Set<SysThemeSetting>()
            .AsTracking()
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            setting = new SysThemeSetting
            {
                FConfigJson = configJson,
                FUpdateTime = DateTime.Now,
                FUpdateBy = updatedBy
            };
            _context.Set<SysThemeSetting>().Add(setting);
        }
        else
        {
            setting.FConfigJson = configJson;
            setting.FUpdateTime = DateTime.Now;
            setting.FUpdateBy = updatedBy;
        }

        await _context.SaveChangesAsync();
        return ApiResult<string>.Success(setting.FConfigJson);
    }
}
