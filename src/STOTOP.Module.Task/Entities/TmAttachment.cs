using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmAttachment : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public int FRelationType { get; set; }
    public long FRelationId { get; set; }
    public long FUserId { get; set; }
    public string FOriginalFileName { get; set; } = string.Empty;
    public string FStoragePath { get; set; } = string.Empty;
    public long FFileSize { get; set; }
    public string FFileType { get; set; } = string.Empty;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
