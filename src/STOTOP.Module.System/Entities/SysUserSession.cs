namespace STOTOP.Module.System.Entities;

public class SysUserSession
{
    public long FID { get; set; }
    public long FUserId { get; set; }
    public string FSessionId { get; set; } = string.Empty;
    public string FRefreshToken { get; set; } = string.Empty;
    public DateTime FRefreshTokenExpiry { get; set; }
    public string? FDeviceFingerprint { get; set; }
    public string? FDeviceInfo { get; set; }
    public string? FIpAddress { get; set; }
    public DateTime FLoginTime { get; set; }
    public DateTime FLastActiveTime { get; set; }
    public int FStatus { get; set; } // 1=活跃, 0=已退出, 2=被踢出, 3=超时退出
    public DateTime? FLogoutTime { get; set; }
    public string? FLogoutReason { get; set; }
}
