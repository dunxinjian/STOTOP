namespace STOTOP.Module.CRM.Dtos;

/// <summary>
/// CRM角色映射详情DTO
/// </summary>
public class CrmRoleMappingDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public int Role { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// CRM角色映射列表项DTO
/// </summary>
public class CrmRoleMappingListItemDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public int Role { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建角色映射请求
/// </summary>
public class CreateRoleMappingRequest
{
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    /// <summary>
    /// 角色类型：1=BD, 2=运维
    /// </summary>
    public int Role { get; set; }
}

/// <summary>
/// 更新角色映射请求
/// </summary>
public class UpdateRoleMappingRequest
{
    public int Role { get; set; }
}

/// <summary>
/// 角色映射查询请求
/// </summary>
public class RoleMappingQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? OrgId { get; set; }
    public int? Role { get; set; }
}
