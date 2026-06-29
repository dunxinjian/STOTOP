using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmManagerQuota : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FManagerId { get; set; }
    public string FYearMonth { get; set; } = string.Empty;
    public int FAwardQuota { get; set; }
    public int FDeductQuota { get; set; }
    public int FUsedAward { get; set; }
    public int FUsedDeduct { get; set; }
    public int FStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
