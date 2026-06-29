using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.System.Controllers;

[ApiController]
[Route("api/system/security")]
[Authorize]
public class SecurityController : ControllerBase
{
    private readonly SecurityConfigService _securityConfigService;
    private readonly SecurityAuditService _securityAuditService;
    private readonly SessionService _sessionService;

    public SecurityController(SecurityConfigService securityConfigService, SecurityAuditService securityAuditService, SessionService sessionService)
    {
        _securityConfigService = securityConfigService;
        _securityAuditService = securityAuditService;
        _sessionService = sessionService;
    }

    /// <summary>
    /// 获取全部安全配置
    /// </summary>
    [HttpGet("config")]
    public async Task<ApiResult<object>> GetConfigs()
    {
        var configs = await _securityConfigService.GetAllConfigs();
        return ApiResult<object>.Success(configs);
    }

    /// <summary>
    /// 批量更新安全配置
    /// </summary>
    [HttpPut("config")]
    public async Task<ApiResult> UpdateConfigs([FromBody] UpdateConfigsRequest request)
    {
        var userName = User.FindFirst("userName")?.Value ?? User.Identity?.Name;
        await _securityConfigService.UpdateConfigs(request.Configs, userName);
        return ApiResult.Ok("配置已更新");
    }

    /// <summary>
    /// 查询审计日志（分页）
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<ApiResult<object>> GetAuditLogs(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] string? eventType,
        [FromQuery] string? eventResult,
        [FromQuery] string? account,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _securityAuditService.GetAuditLogs(startTime, endTime, eventType, eventResult, account, page, pageSize);
        // 投影为前端期望的 camelCase 字段（避免实体 F 前缀导致序列化为 fID/fAccount 等）
        var projected = items.Select(x => new
        {
            id = x.FID,
            userId = x.FUserId,
            account = x.FAccount,
            eventType = x.FEventType,
            eventResult = x.FEventResult,
            ipAddress = x.FIpAddress,
            deviceFingerprint = x.FDeviceFingerprint,
            deviceInfo = x.FDeviceInfo,
            failReason = x.FFailReason,
            sessionId = x.FSessionId,
            extraData = x.FExtraData,
            createTime = x.FCreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();
        return ApiResult<object>.Success(new { items = projected, total });
    }

    /// <summary>
    /// 登录统计
    /// </summary>
    [HttpGet("audit-logs/statistics")]
    public async Task<ApiResult<object>> GetLoginStatistics([FromQuery] int days = 7)
    {
        var stats = await _securityAuditService.GetLoginStatistics(days);
        return ApiResult<object>.Success(stats);
    }

    /// <summary>
    /// 在线用户列表
    /// </summary>
    [HttpGet("online-users")]
    public async Task<ApiResult<object>> GetOnlineUsers()
    {
        var users = await _sessionService.GetOnlineUsers();
        return ApiResult<object>.Success(users);
    }

    /// <summary>
    /// 管理员强制下线
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<ApiResult> AdminTerminateSession(string sessionId)
    {
        await _sessionService.TerminateSession(sessionId, "管理员强制下线");
        return ApiResult.Ok("已强制下线");
    }
}
