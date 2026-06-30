using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 绩效记录
/// </summary>
public class QlPerformance : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FUserId { get; set; }
    public string FPeriod { get; set; } = string.Empty;
    public int FExceptionCount { get; set; }
    public int FResolvedCount { get; set; }
    public int FOverdueCount { get; set; }
    public decimal FScore { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
