namespace STOTOP.Module.Finance.Dtos;

public class AccountSetAuthorizationDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public string UserAccount { get; set; } = "";
    public long AccountSetId { get; set; }
    public long AccountSetRoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public long OrgId { get; set; }
    public long GrantedBy { get; set; }
    public string GrantedByName { get; set; } = "";
    public DateTime CreatedTime { get; set; }
}

public class AccountSetRoleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public List<string> Permissions { get; set; } = new();
}
