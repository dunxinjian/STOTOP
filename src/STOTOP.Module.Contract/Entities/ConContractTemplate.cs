using STOTOP.Core.Models;

namespace STOTOP.Module.Contract.Entities;

public class ConContractTemplate : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTypeId { get; set; }
    public string FTemplateName { get; set; } = string.Empty;
    public string? FTemplateContent { get; set; }
    public int FVersion { get; set; } = 1;
    public int FStatus { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public ConContractType Type { get; set; } = null!;
}
