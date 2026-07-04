using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 企业↔租户映射（IDP企业租户映射，M8·R4）。N:N：一 corp 可服务多租户、一租户可接多 corp（钉钉+企微双接 D3）。
/// 实现 <see cref="ITenantScoped"/> 进租户硬墙——防伪造回调把他租户 corp 归到本租户；查询按 corpId 收窄但落库/读受租户约束。
/// 无 F组织ID（FExternalCorpId/FTenantId 为映射键）→ 不实现 IOrgScoped。
/// </summary>
public class IdpTenantCorpMap : BaseEntity, ITenantScoped
{
    /// <summary>租户ID（R9 隔离键）</summary>
    public long FTenantId { get; set; }

    /// <summary>外部企业 CorpId（→ IDP外部企业.FCorpId）</summary>
    public string FExternalCorpId { get; set; } = string.Empty;

    public int FStatus { get; set; } = 1;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
