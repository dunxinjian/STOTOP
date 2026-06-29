using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmDingTalkTodo : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTaskId { get; set; }
    public long FUserId { get; set; }
    public string? FDingTalkTodoId { get; set; }
    public int FSyncStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
