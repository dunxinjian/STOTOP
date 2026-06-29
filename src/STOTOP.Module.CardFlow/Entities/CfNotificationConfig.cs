using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// 通知渠道配置（key-value 存储，按组织隔离）
/// </summary>
public class CfNotificationConfig : BaseEntity, IOrgScoped
{
    public string FConfigKey { get; set; } = string.Empty;
    public string? FConfigValue { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    public long FOrgId { get; set; }
}
