namespace STOTOP.Module.System.Dtos;

public class RoleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public List<long> PermissionIds { get; set; } = new();
}

public class RoleSimpleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
}

public class UpdateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
}

public class AssignPermissionsRequest
{
    public List<long> PermissionIds { get; set; } = new();
}
