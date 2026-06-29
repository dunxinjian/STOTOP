using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly SessionService _sessionService;
    private readonly SecurityConfigService _securityConfigService;

    public AuthController(IAuthService authService, SessionService sessionService, SecurityConfigService securityConfigService)
    {
        _authService = authService;
        _sessionService = sessionService;
        _securityConfigService = securityConfigService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ApiResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var deviceFingerprint = Request.Headers["X-Device-Fingerprint"].FirstOrDefault();
        var deviceInfo = Request.Headers["User-Agent"].FirstOrDefault();
        return await _authService.LoginAsync(request, ipAddress, deviceFingerprint, deviceInfo);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ApiResult> Logout()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sessionId = User.FindFirst("sessionId")?.Value;

        if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var userId))
        {
            await _authService.LogoutAsync(userId, sessionId);
        }

        return ApiResult.Ok("登出成功");
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ApiResult<RefreshTokenResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var deviceFingerprint = Request.Headers["X-Device-Fingerprint"].FirstOrDefault();
        return await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, deviceFingerprint);
    }

    [HttpPost("verify-password")]
    [Authorize]
    public async Task<ApiResult> VerifyPassword([FromBody] VerifyPasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return ApiResult.Fail("未登录", 401);
        }

        var result = await _authService.VerifyPassword(userId, request.Password);
        return result ? ApiResult.Ok("验证成功") : ApiResult.Fail("密码错误");
    }

    [HttpGet("userinfo")]
    [Authorize]
    public async Task<ApiResult<UserInfoDto>> GetUserInfo()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return ApiResult<UserInfoDto>.Fail("未登录", 401);
        }

        return await _authService.GetUserInfoAsync(userId);
    }

    /// <summary>
    /// 心跳 — 更新最后活跃时间
    /// </summary>
    [HttpPost("heartbeat")]
    [Authorize]
    public async Task<ApiResult> Heartbeat()
    {
        var sessionId = User.FindFirst("sessionId")?.Value;
        if (!string.IsNullOrEmpty(sessionId))
        {
            await _sessionService.UpdateLastActiveTime(sessionId);
        }
        return ApiResult.Ok();
    }

    /// <summary>
    /// 获取当前用户活跃会话列表
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    public async Task<ApiResult<List<SessionDto>>> GetMySessions()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return ApiResult<List<SessionDto>>.Fail("未登录", 401);
        }

        var sessions = await _sessionService.GetActiveSessions(userId);
        var currentSessionId = User.FindFirst("sessionId")?.Value;

        var dtos = sessions.Select(s => new SessionDto
        {
            SessionId = s.FSessionId,
            DeviceInfo = s.FDeviceInfo,
            IpAddress = s.FIpAddress,
            LoginTime = s.FLoginTime,
            LastActiveTime = s.FLastActiveTime,
            IsCurrent = s.FSessionId == currentSessionId
        }).ToList();

        return ApiResult<List<SessionDto>>.Success(dtos);
    }

    /// <summary>
    /// 终止指定会话
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    [Authorize]
    public async Task<ApiResult> TerminateSession(string sessionId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return ApiResult.Fail("未登录", 401);
        }

        // 安全检查：验证该会话属于当前用户
        var sessions = await _sessionService.GetActiveSessions(userId);
        if (!sessions.Any(s => s.FSessionId == sessionId))
        {
            return ApiResult.Fail("无权操作该会话", 403);
        }

        await _sessionService.TerminateSession(sessionId, "用户主动退出");
        return ApiResult.Ok("会话已终止");
    }

    /// <summary>
    /// 终止除当前外所有会话
    /// </summary>
    [HttpDelete("sessions")]
    [Authorize]
    public async Task<ApiResult> TerminateOtherSessions()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return ApiResult.Fail("未登录", 401);
        }

        var currentSessionId = User.FindFirst("sessionId")?.Value;
        await _sessionService.TerminateOtherSessions(userId, currentSessionId);
        return ApiResult.Ok("其他会话已全部终止");
    }

    /// <summary>
    /// 获取客户端所需的安全配置（超时时间等）
    /// </summary>
    [HttpGet("security-config")]
    [Authorize]
    public async Task<ApiResult<object>> GetClientSecurityConfig()
    {
        var config = await _securityConfigService.GetClientConfig();
        return ApiResult<object>.Success(config);
    }

    private string? GetClientIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
