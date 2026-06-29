using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTask : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public string FTitle { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public long FOrgId { get; set; }
    public long? FProjectId { get; set; }
    public long? FGoalId { get; set; }
    public long? FKRId { get; set; }
    public long FParentTaskId { get; set; } = 0;
    public int FType { get; set; } = 0;
    public int FPriority { get; set; } = 2;
    public int FStatus { get; set; } = 0;
    public long? FAssigneeId { get; set; }
    public long FCreatorId { get; set; }
    public DateTime? FPlanStart { get; set; }
    public DateTime? FPlanEnd { get; set; }
    public DateTime? FActualStart { get; set; }
    public DateTime? FActualEnd { get; set; }
    public decimal? FEstimatedHours { get; set; }
    public decimal? FActualHours { get; set; }
    public int FProgress { get; set; } = 0;
    public int FVisibility { get; set; } = 0;
    public bool FIsTemplate { get; set; } = false;
    public string? FCode { get; set; }
    public int FSort { get; set; } = 0;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmProject? Project { get; set; }
    public virtual ICollection<TmTask> Children { get; set; } = new List<TmTask>();
    public virtual ICollection<TmTaskMember> Members { get; set; } = new List<TmTaskMember>();
    public virtual ICollection<TmTaskTag> Tags { get; set; } = new List<TmTaskTag>();
    public virtual ICollection<TmTaskComment> Comments { get; set; } = new List<TmTaskComment>();
    public virtual ICollection<TmActivityLog> ActivityLogs { get; set; } = new List<TmActivityLog>();
    public virtual ICollection<TmTaskReminder> Reminders { get; set; } = new List<TmTaskReminder>();
    public virtual ICollection<TmProgressReport> ProgressReports { get; set; } = new List<TmProgressReport>();
    public virtual ICollection<TmDingTalkTodo> DingTalkTodos { get; set; } = new List<TmDingTalkTodo>();
    public virtual ICollection<TmTaskDependency> Dependencies { get; set; } = new List<TmTaskDependency>();
    public virtual ICollection<TmTaskVisibility> VisibilityRules { get; set; } = new List<TmTaskVisibility>();
}
