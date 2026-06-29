using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

public class SessionService
{
    private readonly IConfiguration _configuration;
    private readonly SecurityConfigService _securityConfigService;
    private readonly SecurityAuditService _securityAuditService;

    public SessionService(IConfiguration configuration, SecurityConfigService securityConfigService, SecurityAuditService securityAuditService)
    {
        _configuration = configuration;
        _securityConfigService = securityConfigService;
        _securityAuditService = securityAuditService;
    }

    private string GetConnectionString()
    {
        var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
        return DbConnectionsHelper.GetSystemConnectionString(encryptionKey)
            ?? throw new InvalidOperationException("系统数据库连接字符串未配置");
    }

    /// <summary>
    /// 登录时创建会话，生成RefreshToken，返回(sessionId, refreshToken)
    /// </summary>
    public async Task<(string sessionId, string refreshToken)> CreateSession(long userId, string? ipAddress, string? deviceFingerprint, string? deviceInfo)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var refreshTokenDays = await _securityConfigService.GetIntConfig("token.refresh_token_days", 7);
        var expiry = DateTime.Now.AddDays(refreshTokenDays);

        using var conn = new SqlConnection(GetConnectionString());
        await conn.ExecuteAsync(
            @"INSERT INTO [SYS用户会话] (FUserId, FSessionId, FRefreshToken, FRefreshTokenExpiry, 
              FDeviceFingerprint, FDeviceInfo, FIpAddress, FLoginTime, FLastActiveTime, FStatus)
              VALUES (@UserId, @SessionId, @RefreshToken, @RefreshTokenExpiry, 
              @DeviceFingerprint, @DeviceInfo, @IpAddress, GETDATE(), GETDATE(), 1)",
            new
            {
                UserId = userId,
                SessionId = sessionId,
                RefreshToken = refreshToken,
                RefreshTokenExpiry = expiry,
                DeviceFingerprint = deviceFingerprint,
                DeviceInfo = deviceInfo,
                IpAddress = ipAddress
            });

        return (sessionId, refreshToken);
    }

    /// <summary>
    /// 验证RefreshToken，返回session信息供AuthService签发
    /// </summary>
    public async Task<SysUserSession?> ValidateRefreshToken(string refreshToken)
    {
        using var conn = new SqlConnection(GetConnectionString());
        var session = await conn.QueryFirstOrDefaultAsync<SysUserSession>(
            @"SELECT FID, FUserId, FSessionId, FRefreshToken, FRefreshTokenExpiry, 
              FDeviceFingerprint, FDeviceInfo, FIpAddress, FLoginTime, FLastActiveTime, FStatus, FLogoutTime, FLogoutReason
              FROM [SYS用户会话]
              WHERE FRefreshToken = @RefreshToken AND FStatus = 1 AND FRefreshTokenExpiry > GETDATE()",
            new { RefreshToken = refreshToken });

        return session;
    }

    /// <summary>
    /// 刷新后更新RefreshToken（旋转策略）
    /// </summary>
    public async Task<string> RotateRefreshToken(long sessionId)
    {
        var newRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshTokenDays = await _securityConfigService.GetIntConfig("token.refresh_token_days", 7);
        var expiry = DateTime.Now.AddDays(refreshTokenDays);

        using var conn = new SqlConnection(GetConnectionString());
        await conn.ExecuteAsync(
            @"UPDATE [SYS用户会话] SET FRefreshToken = @RefreshToken, FRefreshTokenExpiry = @Expiry, 
              FLastActiveTime = GETDATE() WHERE FID = @Id",
            new { RefreshToken = newRefreshToken, Expiry = expiry, Id = sessionId });

        return newRefreshToken;
    }

    /// <summary>
    /// 终止指定会话
    /// </summary>
    public async Task TerminateSession(string sessionId, string reason)
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.ExecuteAsync(
            @"UPDATE [SYS用户会话] SET FStatus = 0, FLogoutTime = GETDATE(), FLogoutReason = @Reason
              WHERE FSessionId = @SessionId AND FStatus = 1",
            new { SessionId = sessionId, Reason = reason });
    }

    /// <summary>
    /// 终止用户所有会话（除指定会话外）
    /// </summary>
    public async Task TerminateOtherSessions(long userId, string? exceptSessionId)
    {
        using var conn = new SqlConnection(GetConnectionString());
        if (string.IsNullOrEmpty(exceptSessionId))
        {
            await conn.ExecuteAsync(
                @"UPDATE [SYS用户会话] SET FStatus = 2, FLogoutTime = GETDATE(), FLogoutReason = '被其他会话踢出'
                  WHERE FUserId = @UserId AND FStatus = 1",
                new { UserId = userId });
        }
        else
        {
            await conn.ExecuteAsync(
                @"UPDATE [SYS用户会话] SET FStatus = 2, FLogoutTime = GETDATE(), FLogoutReason = '被其他会话踢出'
                  WHERE FUserId = @UserId AND FStatus = 1 AND FSessionId != @ExceptSessionId",
                new { UserId = userId, ExceptSessionId = exceptSessionId });
        }
    }

    /// <summary>
    /// 终止用户全部会话
    /// </summary>
    public async Task TerminateAllSessions(long userId, string reason)
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.ExecuteAsync(
            @"UPDATE [SYS用户会话] SET FStatus = 2, FLogoutTime = GETDATE(), FLogoutReason = @Reason
              WHERE FUserId = @UserId AND FStatus = 1",
            new { UserId = userId, Reason = reason });
    }

    /// <summary>
    /// 获取用户活跃会话列表
    /// </summary>
    public async Task<List<SysUserSession>> GetActiveSessions(long userId)
    {
        using var conn = new SqlConnection(GetConnectionString());
        var result = await conn.QueryAsync<SysUserSession>(
            @"SELECT FID, FUserId, FSessionId, FRefreshToken, FRefreshTokenExpiry,
              FDeviceFingerprint, FDeviceInfo, FIpAddress, FLoginTime, FLastActiveTime, FStatus
              FROM [SYS用户会话]
              WHERE FUserId = @UserId AND FStatus = 1
              ORDER BY FLastActiveTime DESC",
            new { UserId = userId });
        return result.ToList();
    }

    /// <summary>
    /// 心跳更新最后活跃时间
    /// </summary>
    public async Task UpdateLastActiveTime(string sessionId)
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.ExecuteAsync(
            "UPDATE [SYS用户会话] SET FLastActiveTime = GETDATE() WHERE FSessionId = @SessionId AND FStatus = 1",
            new { SessionId = sessionId });
    }

    /// <summary>
    /// 检查并执行最大设备数限制（踢出最早的会话）
    /// </summary>
    public async Task EnforceMaxDevices(long userId, int maxDevices)
    {
        using var conn = new SqlConnection(GetConnectionString());
        // 查询时一并获取会话的设备/IP信息，用于踢出审计日志完整性
        var activeSessions = await conn.QueryAsync<SysUserSession>(
            @"SELECT FID, FSessionId, FLoginTime, FIpAddress, FDeviceInfo, FDeviceFingerprint FROM [SYS用户会话]
              WHERE FUserId = @UserId AND FStatus = 1
              ORDER BY FLoginTime ASC",
            new { UserId = userId });

        var sessionList = activeSessions.ToList();
        if (sessionList.Count > maxDevices)
        {
            // 被踢会话的日志需要 account，从 SYS用户 表取
            var account = await conn.ExecuteScalarAsync<string?>(
                "SELECT TOP 1 [F账号] FROM [SYS用户] WHERE FID = @UserId",
                new { UserId = userId });

            var toKick = sessionList.Take(sessionList.Count - maxDevices);
            foreach (var session in toKick)
            {
                await conn.ExecuteAsync(
                    @"UPDATE [SYS用户会话] SET FStatus = 2, FLogoutTime = GETDATE(), FLogoutReason = '超出最大设备数限制'
                      WHERE FID = @Id",
                    new { Id = session.FID });

                await _securityAuditService.LogEvent(userId, account, "Kicked", "Success",
                    ipAddress: session.FIpAddress,
                    deviceFingerprint: session.FDeviceFingerprint,
                    deviceInfo: session.FDeviceInfo,
                    sessionId: session.FSessionId,
                    failReason: "超出最大设备数限制");
            }
        }
    }

    /// <summary>
    /// 验证会话是否有效
    /// </summary>
    public async Task<SysUserSession?> ValidateSession(string sessionId)
    {
        using var conn = new SqlConnection(GetConnectionString());
        return await conn.QueryFirstOrDefaultAsync<SysUserSession>(
            @"SELECT FID, FUserId, FSessionId, FRefreshToken, FRefreshTokenExpiry,
              FDeviceFingerprint, FDeviceInfo, FIpAddress, FLoginTime, FLastActiveTime, FStatus
              FROM [SYS用户会话]
              WHERE FSessionId = @SessionId AND FStatus = 1",
            new { SessionId = sessionId });
    }

    /// <summary>
    /// 获取会话状态详情（用于区分会话失效原因）
    /// </summary>
    public async Task<(int status, string? logoutReason)> GetSessionStatusInfo(string sessionId)
    {
        using var conn = new SqlConnection(GetConnectionString());
        var row = await conn.QueryFirstOrDefaultAsync(
            @"SELECT FStatus, FLogoutReason FROM [SYS用户会话] WHERE FSessionId = @SessionId",
            new { SessionId = sessionId });

        if (row == null) return (-1, null);
        return ((int)row.FStatus, (string?)row.FLogoutReason);
    }

    /// <summary>
    /// 清理过期会话（定时任务调用）
    /// </summary>
    public async Task CleanExpiredSessions()
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.ExecuteAsync(
            @"UPDATE [SYS用户会话] SET FStatus = 3, FLogoutTime = GETDATE(), FLogoutReason = '会话超时'
              WHERE FStatus = 1 AND FRefreshTokenExpiry < GETDATE()");
    }

    /// <summary>
    /// 获取在线用户列表（管理员用）
    /// </summary>
    public async Task<List<object>> GetOnlineUsers()
    {
        using var conn = new SqlConnection(GetConnectionString());
        var result = await conn.QueryAsync<dynamic>(
            @"SELECT s.FUserId, u.FAccount, u.FName, s.FSessionId, s.FDeviceInfo, 
              s.FIpAddress, s.FLoginTime, s.FLastActiveTime
              FROM [SYS用户会话] s
              INNER JOIN [SYS用户] u ON s.FUserId = u.FID
              WHERE s.FStatus = 1
              ORDER BY s.FLastActiveTime DESC");

        var projected = result.Select(x => new
        {
            userId = (long)x.FUserId,
            account = (string)x.FAccount,
            userName = (string)x.FName,
            sessionId = (string)x.FSessionId,
            deviceInfo = (string)(x.FDeviceInfo ?? ""),
            ipAddress = (string)(x.FIpAddress ?? ""),
            loginTime = ((DateTime)x.FLoginTime).ToString("yyyy-MM-dd HH:mm:ss"),
            lastActiveTime = ((DateTime)x.FLastActiveTime).ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList<object>();

        return projected;
    }
}
