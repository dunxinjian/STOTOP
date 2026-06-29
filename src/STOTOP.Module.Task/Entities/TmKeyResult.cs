using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmKeyResult : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FGoalId { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public int FMeasureType { get; set; }
    public decimal FTargetValue { get; set; }
    public decimal FCurrentValue { get; set; } = 0;
    public decimal FStartValue { get; set; } = 0;
    public string? FUnit { get; set; }
    public int FWeight { get; set; } = 100;
    public int FProgress { get; set; } = 0;
    public int FStatus { get; set; } = 0;
    public long? FResponsibleId { get; set; }
    public int FSort { get; set; } = 0;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmGoal? Goal { get; set; }
}
