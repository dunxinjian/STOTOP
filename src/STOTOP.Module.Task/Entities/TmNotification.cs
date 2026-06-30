using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmNotification : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FReceiverId { get; set; }
    public int FEventType { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public string FContent { get; set; } = string.Empty;
    public int FRelationType { get; set; }
    public long FRelationId { get; set; }
    public bool FIsRead { get; set; } = false;
    public bool FPushedToDingTalk { get; set; } = false;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
