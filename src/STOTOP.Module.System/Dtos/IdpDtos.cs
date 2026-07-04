namespace STOTOP.Module.System.Dtos;

/// <summary>免登/登录后租户消歧结果（M8·R4）。</summary>
public class LoginTenantResolution
{
    /// <summary>用户已接受的全部租户成员。</summary>
    public List<TenantMembershipDto> Tenants { get; set; } = new();
    /// <summary>可自动进入的租户（唯一或主租户）；须选时为 null。</summary>
    public long? AutoTenantId { get; set; }
    /// <summary>多租户且无主 → 须强制选择租户（HTTP 428）。</summary>
    public bool MustSelect { get; set; }
}

/// <summary>待确认邀请视图（M8）。</summary>
public class TenantInviteDto
{
    public long TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public long? InvitedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>邀请用户加入租户请求。</summary>
public class InviteMemberRequest
{
    public long TargetUserId { get; set; }
    public long TenantId { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>外部企业视图（M8·平台）。</summary>
public class IdpExternalCorpDto
{
    public long Id { get; set; }
    public int Provider { get; set; }
    public string CorpId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
}

/// <summary>登记/更新外部企业请求。</summary>
public class SaveExternalCorpRequest
{
    public int Provider { get; set; }
    public string CorpId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AccessConfig { get; set; }
}

/// <summary>企业↔租户绑定请求。</summary>
public class LinkCorpTenantRequest
{
    public string CorpId { get; set; } = string.Empty;
    public long TenantId { get; set; }
}

/// <summary>接受/拒绝邀请请求。</summary>
public class TenantMemberActionRequest
{
    public long TenantId { get; set; }
}
