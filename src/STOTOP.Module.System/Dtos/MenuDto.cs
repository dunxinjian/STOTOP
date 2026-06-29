namespace STOTOP.Module.System.Dtos;

public class MenuDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Route { get; set; }
    public string? ComponentPath { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Sort { get; set; }
    public long ParentId { get; set; }
    public int IsVisible { get; set; }
    public List<MenuDto> Children { get; set; } = new();
}
