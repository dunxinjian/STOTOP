namespace STOTOP.Module.System.Dtos;

// 用户的组织任职信息
public class UserOrganizationDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long OrgId { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string OrgType { get; set; } = string.Empty;
    public long? SwitchableOrgId { get; set; }
    public string? SwitchableOrgName { get; set; }
    public long? DirectSuperiorId { get; set; }
    public string? DirectSuperiorName { get; set; }
    public int IsPrimaryOrg { get; set; }
    public string? Position { get; set; }
    public string? JobNumber { get; set; }
    public DateTime? EntryDate { get; set; }
    public int Status { get; set; }
}

// 组织切换响应
public class SwitchOrganizationResponse
{
    public long OrgId { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string OrgType { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public List<MenuDto> Menus { get; set; } = new();
}

// 切换组织请求
public class SwitchOrganizationRequest
{
    public long OrgId { get; set; }
}

// 切换租户请求（阶段4C·R6）
public class SwitchTenantRequest
{
    public long TenantId { get; set; }
}

// 切换租户响应（阶段4C·R6）：校验成员后返回该租户内可切换组织；有主/单一组织则附带其重算上下文。
public class SwitchTenantResponse
{
    public long TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    /// <summary>用户在该租户内可切换的组织列表（前端据此二级选组织并发 X-Org-Context）。</summary>
    public List<UserOrganizationDto> Organizations { get; set; } = new();
    /// <summary>自动选定组织（主组织/唯一组织）的重算上下文（角色/权限/菜单）；无法自动选时为 null。</summary>
    public SwitchOrganizationResponse? Context { get; set; }
}

// 添加用户到组织请求
public class AddUserToOrganizationRequest
{
    public long UserId { get; set; }
    public long OrgId { get; set; }
    public long? DirectSuperiorId { get; set; }
    public int IsPrimaryOrg { get; set; } = 0;
    public string? Position { get; set; }
    public string? JobNumber { get; set; }
    public DateTime? EntryDate { get; set; }
}

// 更新用户组织任职信息请求
public class UpdateUserOrganizationRequest
{
    public long? DirectSuperiorId { get; set; }
    public int? IsPrimaryOrg { get; set; }
    public string? Position { get; set; }
    public string? JobNumber { get; set; }
    public DateTime? EntryDate { get; set; }
    public int? Status { get; set; }
}
