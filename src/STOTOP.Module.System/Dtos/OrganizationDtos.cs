namespace STOTOP.Module.System.Dtos;

public class OrgTypeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool CanBindAccountSet { get; set; }
    public bool CanSwitch { get; set; }
    public string? Icon { get; set; }
    public int Sort { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
}

public class OrgTypeUpdateDto
{
    public string? Icon { get; set; }
    public int? Sort { get; set; }
    public string? Description { get; set; }
    public bool? IsEnabled { get; set; }
}

public class OrganizationDto
{
    public long Id { get; set; }
    public string FUID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public long ParentId { get; set; }

    // 类型新字段（来自 SYS组织类型 表）
    public long TypeId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int TypeLevel { get; set; }
    public bool CanBindAccountSet { get; set; }
    public bool CanSwitch { get; set; }

    // 兼容旧字段（请改用 TypeId）
    [Obsolete("Use TypeId instead")]
    public string Type { get; set; } = string.Empty;

    public int Sort { get; set; }
    public int Status { get; set; }
    public string? DingTalkDeptId { get; set; }
    public int DingTalkBindStatus { get; set; }
    public string? DingTalkDeptName { get; set; }
    public long? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public int? Headcount { get; set; }
    public int? ActualCount { get; set; }
    public bool IsSwitchable { get; set; }
    public string? Description { get; set; }
    public DateTime CreateTime { get; set; }
    public List<OrganizationDto> Children { get; set; } = new();
}

public class CreateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public long ParentId { get; set; } = 0;

    /// <summary>组织类型ID，对应 SYS组织类型.FID</summary>
    public long TypeId { get; set; } = 5; // 默认为部门

    /// <summary>兼容旧版本，请改用 TypeId</summary>
    [Obsolete("Use TypeId instead")]
    public string Type { get; set; } = "部门";

    public int Sort { get; set; } = 0;
    public int Status { get; set; } = 1;
    public string? DingTalkDeptId { get; set; }
    public long? ManagerId { get; set; }
    public int? Headcount { get; set; }
    public bool IsSwitchable { get; set; }
    public string? Description { get; set; }
}

public class UpdateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public long ParentId { get; set; }

    /// <summary>组织类型ID，对应 SYS组织类型.FID</summary>
    public long TypeId { get; set; }

    /// <summary>兼容旧版本，请改用 TypeId</summary>
    [Obsolete("Use TypeId instead")]
    public string Type { get; set; } = string.Empty;

    public int Sort { get; set; }
    public int? Status { get; set; }
    public string? DingTalkDeptId { get; set; }
    public long? ManagerId { get; set; }
    public int? Headcount { get; set; }
    public bool IsSwitchable { get; set; }
    public string? Description { get; set; }
}

public class OrgAccountSetDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Status { get; set; }
}
