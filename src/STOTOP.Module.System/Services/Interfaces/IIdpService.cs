using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services.Interfaces;

/// <summary>
/// 外部身份（IdP）服务（多租户阶段4D·M8）：外部企业/用户身份绑定、免登多租户消歧、成员邀请流。
/// 通用于多 provider（钉钉=1，企微=2 声明桩）；企微 provider 运行时未实装（需真企微 corp 凭证，留后续）。
/// </summary>
public interface IIdpService
{
    // ---- 外部企业 / 身份（平台层表，无租户过滤器）----
    /// <summary>登记/更新外部企业（幂等，按 CorpId）。</summary>
    Task<long> EnsureExternalCorpAsync(IdpProvider provider, string corpId, string name, string? accessConfig = null);

    Task<List<IdpExternalCorpDto>> GetExternalCorpsAsync();

    /// <summary>绑定/更新一个用户在某 corp 下的外部身份（幂等，唯一键 用户+CorpId）。</summary>
    Task UpsertUserIdentityAsync(long userId, string corpId, string externalUserId, string? unionId);

    /// <summary>由 (corp, 外部用户id) 反查系统用户（免登匹配）；无绑定返回 null。</summary>
    Task<long?> ResolveUserByExternalAsync(string corpId, string externalUserId);

    /// <summary>企业↔租户绑定（ITenantScoped 写，须在平台作用域或目标租户上下文下调用，显式落 FTenantId）。</summary>
    Task LinkCorpToTenantAsync(string corpId, long tenantId);

    // ---- 免登多租户消歧（R4）----
    /// <summary>据用户已接受租户成员算登录进入哪个租户：0→无、1→唯一、多个有主→主、多个无主→强制选(428)。</summary>
    Task<LoginTenantResolution> ResolveLoginTenantAsync(long userId);

    // ---- 成员邀请（加入须确认）----
    /// <summary>邀请用户加入租户（置待确认 SYS租户成员，非自动接受）。</summary>
    Task InviteMemberAsync(long inviterUserId, long targetUserId, long tenantId, bool isPrimary);

    /// <summary>被邀请人接受（→已接受、写加入时间、重算 R8 派生授权）。</summary>
    Task AcceptInviteAsync(long userId, long tenantId);

    /// <summary>被邀请人拒绝（→已拒绝）。</summary>
    Task RejectInviteAsync(long userId, long tenantId);

    /// <summary>用户的待确认邀请列表。</summary>
    Task<List<TenantInviteDto>> GetPendingInvitesAsync(long userId);
}
