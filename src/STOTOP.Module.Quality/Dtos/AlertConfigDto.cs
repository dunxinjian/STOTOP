using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 预警配置列表项
/// </summary>
public class AlertConfigDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ThresholdType { get; set; } = string.Empty;
    public decimal Threshold { get; set; }
    public string NotifyMethod { get; set; } = string.Empty;
    public string? NotifyTargets { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 创建预警配置请求
/// </summary>
public class CreateAlertConfigRequest
{
    public string Name { get; set; } = string.Empty;
    public string ThresholdType { get; set; } = string.Empty;
    public decimal Threshold { get; set; }
    public string NotifyMethod { get; set; } = string.Empty;
    public string? NotifyTargets { get; set; }
}

/// <summary>
/// 更新预警配置请求
/// </summary>
public class UpdateAlertConfigRequest : CreateAlertConfigRequest { }

/// <summary>
/// 预警配置分页请求
/// </summary>
public class AlertConfigPagedRequest : PagedRequest
{
    public string? ThresholdType { get; set; }
    public int? Status { get; set; }
}
