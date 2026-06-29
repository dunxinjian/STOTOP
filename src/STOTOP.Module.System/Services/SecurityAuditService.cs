using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

public class SecurityAuditService
{
    private readonly IConfiguration _configuration;

    public SecurityAuditService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetConnectionString()
    {
        var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
        return DbConnectionsHelper.GetSystemConnectionString(encryptionKey)
            ?? throw new InvalidOperationException("系统数据库连接字符串未配置");
    }

    /// <summary>
    /// 记录安全审计事件
    /// </summary>
    public async Task LogEvent(long? userId, string? account, string eventType, string eventResult,
        string? ipAddress = null, string? deviceFingerprint = null, string? deviceInfo = null,
        string? failReason = null, string? sessionId = null, string? extraData = null)
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.ExecuteAsync(
            @"INSERT INTO [SYS安全审计日志] (FUserId, FAccount, FEventType, FEventResult, FIpAddress, 
              FDeviceFingerprint, FDeviceInfo, FFailReason, FSessionId, FExtraData, FCreateTime)
              VALUES (@UserId, @Account, @EventType, @EventResult, @IpAddress, 
              @DeviceFingerprint, @DeviceInfo, @FailReason, @SessionId, @ExtraData, GETDATE())",
            new
            {
                UserId = userId,
                Account = account,
                EventType = eventType,
                EventResult = eventResult,
                IpAddress = ipAddress,
                DeviceFingerprint = deviceFingerprint,
                DeviceInfo = deviceInfo,
                FailReason = failReason,
                SessionId = sessionId,
                ExtraData = extraData
            });
    }

    /// <summary>
    /// 分页查询审计日志
    /// </summary>
    public async Task<(List<SysSecurityAuditLog> items, int total)> GetAuditLogs(
        DateTime? startTime, DateTime? endTime, string? eventType, string? eventResult,
        string? account, int page = 1, int pageSize = 20)
    {
        using var conn = new SqlConnection(GetConnectionString());

        var where = "WHERE 1=1";
        if (startTime.HasValue) where += " AND FCreateTime >= @StartTime";
        if (endTime.HasValue) where += " AND FCreateTime <= @EndTime";
        if (!string.IsNullOrEmpty(eventType)) where += " AND FEventType = @EventType";
        if (!string.IsNullOrEmpty(eventResult)) where += " AND FEventResult = @EventResult";
        if (!string.IsNullOrEmpty(account)) where += " AND FAccount LIKE '%' + @Account + '%'";

        var countSql = $"SELECT COUNT(*) FROM [SYS安全审计日志] {where}";
        var querySql = $@"SELECT FID, FUserId, FAccount, FEventType, FEventResult, FIpAddress, 
            FDeviceFingerprint, FDeviceInfo, FFailReason, FSessionId, FExtraData, FCreateTime
            FROM [SYS安全审计日志] {where}
            ORDER BY FCreateTime DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var parameters = new
        {
            StartTime = startTime,
            EndTime = endTime,
            EventType = eventType,
            EventResult = eventResult,
            Account = account,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        };

        var total = await conn.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await conn.QueryAsync<SysSecurityAuditLog>(querySql, parameters)).ToList();

        return (items, total);
    }

    /// <summary>
    /// 获取登录统计（最近N天的登录成功/失败次数统计）
    /// </summary>
    public async Task<object> GetLoginStatistics(int days = 7)
    {
        using var conn = new SqlConnection(GetConnectionString());
        var result = await conn.QueryAsync<dynamic>(
            @"SELECT CONVERT(VARCHAR(10), FCreateTime, 120) AS [Date],
                     SUM(CASE WHEN FEventResult = 'Success' THEN 1 ELSE 0 END) AS SuccessCount,
                     SUM(CASE WHEN FEventResult = 'Failed' THEN 1 ELSE 0 END) AS FailedCount
              FROM [SYS安全审计日志]
              WHERE FEventType = 'Login' AND FCreateTime >= DATEADD(DAY, -@Days, GETDATE())
              GROUP BY CONVERT(VARCHAR(10), FCreateTime, 120)
              ORDER BY [Date]",
            new { Days = days });

        return result.ToList();
    }

    /// <summary>
    /// 检查账号是否被锁定（最近N分钟内失败次数是否超过阈值）
    /// </summary>
    public async Task<(bool isLocked, int remainingMinutes)> CheckAccountLocked(string account)
    {
        using var conn = new SqlConnection(GetConnectionString());

        // 默认：30分钟内失败5次锁定，锁定30分钟
        var lockoutMinutes = 30;
        var maxAttempts = 5;

        // 尝试从配置表读取
        var lockoutConfig = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT FConfigValue FROM [SYS安全配置] WHERE FConfigKey = 'security.lockout_minutes'");
        var maxAttemptsConfig = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT FConfigValue FROM [SYS安全配置] WHERE FConfigKey = 'security.max_login_attempts'");

        if (int.TryParse(lockoutConfig, out var lm)) lockoutMinutes = lm;
        if (int.TryParse(maxAttemptsConfig, out var ma)) maxAttempts = ma;

        var failedCount = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM [SYS安全审计日志]
              WHERE FAccount = @Account AND FEventType = 'Login' AND FEventResult = 'Failed'
              AND FCreateTime >= DATEADD(MINUTE, -@Minutes, GETDATE())",
            new { Account = account, Minutes = lockoutMinutes });

        if (failedCount >= maxAttempts)
        {
            // 计算剩余锁定时间
            var lastFailTime = await conn.ExecuteScalarAsync<DateTime?>(
                @"SELECT MAX(FCreateTime) FROM [SYS安全审计日志]
                  WHERE FAccount = @Account AND FEventType = 'Login' AND FEventResult = 'Failed'",
                new { Account = account });

            if (lastFailTime.HasValue)
            {
                var unlockTime = lastFailTime.Value.AddMinutes(lockoutMinutes);
                var remaining = (int)Math.Ceiling((unlockTime - DateTime.Now).TotalMinutes);
                if (remaining > 0)
                    return (true, remaining);
            }
        }

        return (false, 0);
    }
}
