namespace STOTOP.Module.CardFlow.Models;

public class ImportCompletedEvent
{
    public long BatchId { get; set; }
    public string TargetTable { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime CompletedAt { get; set; }
}
