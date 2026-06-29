using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmPointRanking : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FUserId { get; set; }
    public long? FDepartmentId { get; set; }
    public int FDimension { get; set; }
    public string FPeriod { get; set; } = string.Empty;
    public int FTotalPoints { get; set; }
    public int FAwardPoints { get; set; }
    public int FDeductPoints { get; set; }
    public int FRank { get; set; }
    /// <summary>是否主组织</summary>
    public bool F是否主组织 { get; set; } = true;
    /// <summary>经营单元ID</summary>
    public long? F经营单元ID { get; set; }
    public DateTime FGenerateTime { get; set; } = DateTime.Now;
}
