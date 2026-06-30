using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTag : BaseEntity, IOrgScoped, ITenantScoped
{
    public string FName { get; set; } = string.Empty;
    public string FColor { get; set; } = string.Empty;
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public int FSort { get; set; } = 0;

    // 导航属性
    public virtual ICollection<TmTaskTag> TaskTags { get; set; } = new List<TmTaskTag>();
}
