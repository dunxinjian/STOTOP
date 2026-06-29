namespace STOTOP.Module.System.Entities;

public class SysSecurityConfig
{
    public long FID { get; set; }
    public string FConfigKey { get; set; } = string.Empty;
    public string FConfigValue { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public DateTime FUpdateTime { get; set; }
    public string? FUpdatedBy { get; set; }
}
