using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

/// <summary>
/// 账套规则（一账套一行）：账套级会计控制开关与结转科目映射。
/// 无行 = 无配置 = 全部回退当前写死行为（fail-safe，零行为变更）。
/// </summary>
public class FinAccountSetRule : BaseEntity, IAccountSetScoped, ITenantScoped
{
    public long FAccountSetId { get; set; }   // 账套ID（IAccountSetScoped 无自动过滤器，查询须手写 Where）
    public long FTenantId { get; set; }       // 租户ID（区域公司，多租户隔离键，DbContext 自动回填）
    public long FOrgId { get; set; }          // 组织ID（不实现 IOrgScoped，恒 0、无隔离语义，仅列形对齐）

    /// <summary>P0-1 制单审核分离：开则审核时校验制单人≠审核人。默认关=不校验（现状）。</summary>
    public bool FRequireAuditSeparation { get; set; }

    /// <summary>P0-2 本年利润结转目标科目编码；空=回退 "3103"。</summary>
    public string? FProfitAccountCode { get; set; }

    /// <summary>P0-2 未分配利润结转目标科目编码；空=回退 "310405"。</summary>
    public string? FRetainedAccountCode { get; set; }

    /// <summary>P0-3 启用凭证字 JSON 数组（如 ["记","收","付","转"]）；空=回退全集。必含"记"。</summary>
    public string? FEnabledVoucherWords { get; set; }

    public int FStatus { get; set; } = 1;     // 0=禁用, 1=启用
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
