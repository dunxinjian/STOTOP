namespace STOTOP.Module.Workflow.DTOs;

public class TriggerActionDto
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
}
