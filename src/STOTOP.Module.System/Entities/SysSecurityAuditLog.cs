namespace STOTOP.Module.System.Entities;

public class SysSecurityAuditLog
{
    public long FID { get; set; }
    public long? FUserId { get; set; }
    public string? FAccount { get; set; }
    public string FEventType { get; set; } = string.Empty; // Login/Logout/TokenRefresh/Timeout/Kicked/AnomalyDetected
    public string FEventResult { get; set; } = string.Empty; // Success/Failed
    public string? FIpAddress { get; set; }
    public string? FDeviceFingerprint { get; set; }
    public string? FDeviceInfo { get; set; }
    public string? FFailReason { get; set; }
    public string? FSessionId { get; set; }
    public string? FExtraData { get; set; }
    public DateTime FCreateTime { get; set; }
}
