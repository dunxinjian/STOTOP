using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IAuthService
{
    Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress = null, string? deviceFingerprint = null, string? deviceInfo = null);
    Task<ApiResult<UserInfoDto>> GetUserInfoAsync(long userId);
    Task<ApiResult<List<string>>> GetUserPermissionsAsync(long userId);
    Task<ApiResult<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress = null, string? deviceFingerprint = null);
    Task LogoutAsync(long userId, string? sessionId);
    Task<bool> VerifyPassword(long userId, string password);

    /// <summary>
    /// 签发新的 AccessToken（用于滑动续期），返回 (token, expiresInSeconds)
    /// </summary>
    Task<(string token, int expiresIn)> IssueAccessTokenAsync(long userId, string? sessionId);
}
