using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmProjectMember : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FProjectId { get; set; }
    public long FUserId { get; set; }
    public int FRole { get; set; }
    public DateTime FJoinTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmProject? Project { get; set; }
}
