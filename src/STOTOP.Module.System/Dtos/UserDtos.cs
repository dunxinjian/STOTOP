namespace STOTOP.Module.System.Dtos;

public class UserDto
{
    public long Id { get; set; }
    public string FUID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public string? OrgName { get; set; }
    public int Status { get; set; }
    public int DingTalkBindStatus { get; set; }
    public string? DingTalkUserId { get; set; }
    public string? DingTalkUserName { get; set; }
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
    public List<RoleSimpleDto> Roles { get; set; } = new();
    public List<UserOrganizationDto>? Organizations { get; set; }
    public List<PositionDto>? Positions { get; set; }
}

public class UserSimpleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public int Status { get; set; } = 1;
    public List<long> RoleIds { get; set; } = new();
}

public class UpdateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public int Status { get; set; } = 1;
    public List<long> RoleIds { get; set; } = new();
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// 用户分页查询请求（支持按角色/组织过滤）
/// </summary>
public class UserPagedRequest : STOTOP.Core.Models.PagedRequest
{
    /// <summary>按角色ID过滤，只返回该角色下的用户</summary>
    public long? RoleId { get; set; }

    /// <summary>按组织ID过滤</summary>
    public long? OrgId { get; set; }
}
