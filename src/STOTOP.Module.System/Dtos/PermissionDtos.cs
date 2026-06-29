namespace STOTOP.Module.System.Dtos;

public class PermissionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public string? Route { get; set; }
    public string? ComponentPath { get; set; }
    public string? Icon { get; set; }
    public int Sort { get; set; }
    public int Status { get; set; }
    public int IsVisible { get; set; }
    public DateTime CreateTime { get; set; }
    public List<PermissionDto> Children { get; set; } = new();
}

public class CreatePermissionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = "菜单";
    public long ParentId { get; set; } = 0;
    public string? Route { get; set; }
    public string? ComponentPath { get; set; }
    public string? Icon { get; set; }
    public int Sort { get; set; } = 0;
    public int Status { get; set; } = 1;
    public int IsVisible { get; set; } = 1;
}

public class UpdatePermissionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? ComponentPath { get; set; }
    public string? Icon { get; set; }
    public int Sort { get; set; }
    public int Status { get; set; }
    public int IsVisible { get; set; }
}

public class MenuTreeResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? ComponentPath { get; set; }
    public string? Icon { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Sort { get; set; }
    public int Badge { get; set; }
    public List<MenuTreeResponse> Children { get; set; } = new();
}
