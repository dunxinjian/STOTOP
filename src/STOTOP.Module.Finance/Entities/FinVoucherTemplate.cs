using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinVoucherTemplate : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FAccountSetId { get; set; }
    public long FOrgId { get; set; }  // 组织ID
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FSort { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    public List<FinVoucherTemplateEntry> Entries { get; set; } = new();
}
