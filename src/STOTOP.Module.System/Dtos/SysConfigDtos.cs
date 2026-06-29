namespace STOTOP.Module.System.Dtos;

public class SysConfigDto
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
}

public class SysConfigUpdateDto
{
    public string Value { get; set; } = string.Empty;
}
