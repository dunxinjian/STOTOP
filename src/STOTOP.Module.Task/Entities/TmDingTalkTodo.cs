using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmDingTalkTodo : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FTaskId { get; set; }
    public long FUserId { get; set; }
    public string? FDingTalkTodoId { get; set; }
    public int FSyncStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
