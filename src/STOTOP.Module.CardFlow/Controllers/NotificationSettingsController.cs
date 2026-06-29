using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/notification-settings")]
public class NotificationSettingsController : ControllerBase
{
    private readonly INotificationSettingsService _service;

    public NotificationSettingsController(INotificationSettingsService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private long GetOrgId()
    {
        var claim = User.FindFirst("OrgId")?.Value
            ?? User.FindFirst("orgId")?.Value;
        return long.TryParse(claim, out var id) ? id : 0;
    }

    [HttpGet]
    public async Task<ApiResult<NotificationSettingsDto>> Get()
    {
        var dto = await _service.GetAsync(GetOrgId());
        // 回写默认回调URL（前端会根据 location.origin 兜底，这里返回相对路径作为提示）
        dto.CallbackUrl = "/api/cardflow/callback/dingtalk";
        return ApiResult<NotificationSettingsDto>.Success(dto);
    }

    [HttpPut]
    public async Task<ApiResult> Save([FromBody] SaveNotificationSettingsRequest request)
    {
        try
        {
            await _service.SaveAsync(request, GetOrgId(), GetUserId());
            return ApiResult.Ok("保存成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("test")]
    public async Task<ApiResult<TestNotificationResult>> Test([FromBody] TestNotificationRequest request)
    {
        var result = await _service.TestAsync(request);
        if (!result.Success)
            return ApiResult<TestNotificationResult>.Fail(result.Message ?? "测试失败");
        return ApiResult<TestNotificationResult>.Success(result, result.Message ?? "测试成功");
    }
}
