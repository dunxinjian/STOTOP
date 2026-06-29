using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/settings")]
[ApiController]
[Authorize]
public class SystemSettingsController : ControllerBase
{
    private readonly ISystemSettingsService _settingsService;

    public SystemSettingsController(ISystemSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ApiResult<List<SysConfigDto>>> GetAll()
    {
        return await _settingsService.GetAllAsync();
    }

    [HttpGet("{key}")]
    public async Task<ApiResult<SysConfigDto?>> GetByKey(string key)
    {
        return await _settingsService.GetByKeyAsync(key);
    }

    [HttpPut("{key}")]
    public async Task<ApiResult<SysConfigDto>> Update(string key, [FromBody] SysConfigUpdateDto dto)
    {
        return await _settingsService.UpdateAsync(key, dto);
    }
}
