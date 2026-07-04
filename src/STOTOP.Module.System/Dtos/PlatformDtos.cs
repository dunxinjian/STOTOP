namespace STOTOP.Module.System.Dtos;

/// <summary>平台租户视图（PLT租户，平台超管跨租户查看）。</summary>
public class PlatformTenantDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public long RootOrgId { get; set; }
    public int AccountSetBindMode { get; set; }
    public int DefaultTodoChannel { get; set; }
    public long? PlanId { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpireAt { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

/// <summary>创建平台租户请求（仅登记 PLT租户 实体；新客户完整组织树/角色供给属 R5 开通流程，本阶段不含）。</summary>
public class CreatePlatformTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public long RootOrgId { get; set; }
    public int AccountSetBindMode { get; set; } = 1;
    public int DefaultTodoChannel { get; set; } = 1;
    public long? PlanId { get; set; }
    public DateTime? ExpireAt { get; set; }
}

/// <summary>更新租户状态请求（试用/正式/停用/欠费冻结，见 PltTenantStatus）。</summary>
public class UpdateTenantStatusRequest
{
    public int Status { get; set; }
}

/// <summary>平台套餐视图。</summary>
public class PlatformPlanDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int MaxUsers { get; set; }
    public int MaxOutlets { get; set; }
    public string? ModuleFlags { get; set; }
    public int Status { get; set; }
}

/// <summary>创建/更新套餐请求。</summary>
public class SavePlatformPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int MaxUsers { get; set; }
    public int MaxOutlets { get; set; }
    public string? ModuleFlags { get; set; }
}

/// <summary>平台订阅视图。</summary>
public class PlatformSubscriptionDto
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long PlanId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int Status { get; set; }
}

/// <summary>创建订阅请求（订阅生效同时把租户置正式、写开通/到期时间与套餐）。</summary>
public class CreateSubscriptionRequest
{
    public long TenantId { get; set; }
    public long PlanId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
