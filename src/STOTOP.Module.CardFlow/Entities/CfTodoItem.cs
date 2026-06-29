using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfTodoItem : BaseEntity, IOrgScoped
{
    public long FCardId { get; set; }
    public long FStageInstanceId { get; set; }
    public long FHandlerId { get; set; }
    public string FHandlerName { get; set; } = string.Empty;
    public string? FTitle { get; set; }
    public string FType { get; set; } = "todo";
    public string FStatus { get; set; } = "pending";
    public int FPriority { get; set; } = 3;
    public long? FDelegateSourceId { get; set; }
    public string? FPushChannel { get; set; }
    public string? FExternalTodoId { get; set; }
    public string FPushStatus { get; set; } = "pending";
    public int FRetryCount { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public long FOrgId { get; set; }
}
