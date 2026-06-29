using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/theme-settings")]
[ApiController]
public class ThemeSettingController : ControllerBase
{
    private readonly IThemeSettingService _themeSettingService;

    public ThemeSettingController(IThemeSettingService themeSettingService)
    {
        _themeSettingService = themeSettingService;
    }

    /// <summary>
    /// 获取主题配置（无需认证，所有用户需要加载主题）
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ApiResult<string>> Get()
    {
        return await _themeSettingService.GetThemeSettingsAsync();
    }

    /// <summary>
    /// 更新主题配置（需要管理员权限）
    /// </summary>
    [HttpPut]
    [Authorize]
    public async Task<ApiResult<string>> Update([FromBody] ThemeSettingUpdateRequest request)
    {
        var userName = User.FindFirst(ClaimTypes.Name)?.Value;
        return await _themeSettingService.UpdateThemeSettingsAsync(request.ConfigJson, userName);
    }
}

public class ThemeSettingUpdateRequest
{
    public string ConfigJson { get; set; } = "{}";
}
