using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>外部身份绑定状态（IDP用户身份.FBindStatus）。</summary>
public enum IdpBindStatus
{
    /// <summary>未绑定</summary>
    Unbound = 0,
    /// <summary>已绑定</summary>
    Bound = 1,
}

/// <summary>
/// 外部用户身份（IDP用户身份，M8）。一个 SYS用户 在某外部企业(corp)下的外部账号绑定。
/// 平台层·不实现 <see cref="ITenantScoped"/>：用户身份是全局的（跨租户随用户），非被隔离业务行。无 F组织ID → 漏标门禁不触发。
/// <para>替代 SYS用户 单 corp 的 F钉钉用户ID/F钉钉UnionId（阶段4D 加性引入前向规范存储；SysUser 钉钉字段暂保留兼容,拆列列为后续）。
/// 唯一 (F用户ID, F企业CorpId)——一个用户在一个 corp 下至多一条身份。</para>
/// </summary>
public class IdpUserIdentity : BaseEntity
{
    /// <summary>系统用户ID（→ SYS用户.FID）</summary>
    public long FUserId { get; set; }

    /// <summary>外部企业 CorpId（→ IDP外部企业.FCorpId）</summary>
    public string FExternalCorpId { get; set; } = string.Empty;

    /// <summary>外部用户ID（corp 内的 userId）</summary>
    public string FExternalUserId { get; set; } = string.Empty;

    /// <summary>UnionId（供应商跨应用唯一，可空）</summary>
    public string? FUnionId { get; set; }

    /// <summary>绑定状态，见 <see cref="IdpBindStatus"/></summary>
    public int FBindStatus { get; set; } = (int)IdpBindStatus.Bound;

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
