using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmProject : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public long FOrgId { get; set; }
    public long? FGoalId { get; set; }
    public long FManagerId { get; set; }
    public DateTime? FStartDate { get; set; }
    public DateTime? FEndDate { get; set; }
    public int FStatus { get; set; } = 0;
    public long FCreatorId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<TmProjectMember> Members { get; set; } = new List<TmProjectMember>();
    public virtual ICollection<TmTask> Tasks { get; set; } = new List<TmTask>();
}
