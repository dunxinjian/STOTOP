using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmProgressReport : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTaskId { get; set; }
    public long FReporterId { get; set; }
    public int FProgress { get; set; }
    public string FContent { get; set; } = string.Empty;
    public decimal? FHours { get; set; }
    public bool FPushedToDingTalk { get; set; } = false;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
