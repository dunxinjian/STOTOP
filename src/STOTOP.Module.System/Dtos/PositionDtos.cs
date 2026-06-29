namespace STOTOP.Module.System.Dtos;

// 岗位返回 DTO
public class PositionDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public string? DingTalkPositionId { get; set; }
    public int DingTalkBindStatus { get; set; }
    public int Sort { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public int OrganizationCount { get; set; }
    public int UserCount { get; set; }
    public List<PositionOrganizationDto>? Organizations { get; set; }
    public List<PositionUserDto>? Users { get; set; }
}

// 岗位关联的组织简要信息
public class PositionOrganizationDto
{
    public long OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
}

// 岗位关联的人员简要信息
public class PositionUserDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int IsPrimary { get; set; }
}

// 创建岗位请求
public class CreatePositionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    public int Sort { get; set; } = 0;
    public long[]? OrganizationIds { get; set; }
}

// 更新岗位请求
public class UpdatePositionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public int Sort { get; set; }
}

// 岗位-组织关系批量设置请求
public class AssignPositionOrganizationsRequest
{
    public long[] OrganizationIds { get; set; } = Array.Empty<long>();
}

// 岗位-人员关系批量设置请求
public class AssignPositionUsersRequest
{
    public long[] UserIds { get; set; } = Array.Empty<long>();
}
