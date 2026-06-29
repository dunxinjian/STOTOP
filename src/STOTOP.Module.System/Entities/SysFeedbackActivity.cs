using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysFeedbackActivity : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FFeedbackId { get; set; }
    public long FActorId { get; set; }
    public string FAction { get; set; } = string.Empty;
    public string? FContent { get; set; }
    public int? FFromStatus { get; set; }
    public int? FToStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
