using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmGoal : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public string FTitle { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public long FOrgId { get; set; }
    public long FGoalOrgId { get; set; }
    public long? FResponsibleId { get; set; }
    public long FParentId { get; set; } = 0;
    public string FLevel { get; set; } = string.Empty;
    public DateTime FStartDate { get; set; }
    public DateTime FEndDate { get; set; }
    public int FProgress { get; set; } = 0;
    public int FWeight { get; set; } = 100;
    public int FStatus { get; set; } = 0;
    public long FCreatorId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<TmKeyResult> KeyResults { get; set; } = new List<TmKeyResult>();
    public virtual ICollection<TmGoal> Children { get; set; } = new List<TmGoal>();
}
