using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmAttachment : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public int FRelationType { get; set; }
    public long FRelationId { get; set; }
    public long FUserId { get; set; }
    public string FOriginalFileName { get; set; } = string.Empty;
    public string FStoragePath { get; set; } = string.Empty;
    public long FFileSize { get; set; }
    public string FFileType { get; set; } = string.Empty;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
