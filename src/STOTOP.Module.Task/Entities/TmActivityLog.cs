using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmActivityLog : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTaskId { get; set; }
    public int FActionType { get; set; }
    public string? FOldValue { get; set; }
    public string? FNewValue { get; set; }
    public long FOperatorId { get; set; }
    public string FRemark { get; set; } = string.Empty;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
