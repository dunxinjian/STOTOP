using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 网点公司（SYS网点公司，M5 多租户阶段2）。R1/R2 真实运营公司，与组织树中 FKind=网点公司(Company) 节点 1:1。
/// 是阶段3 FIN经营单元 的 1:1 派生源、EXP网点(ExpNetworkPoint) 的归属公司。
/// 实现 <see cref="ITenantScoped"/> 进租户硬墙(仅请求内被 Express/Finance 消费,不在登录/切换引导路径读,挂墙安全)。
/// </summary>
public class SysOutletCompany : BaseEntity, ITenantScoped
{
    /// <summary>租户ID（R9 隔离键）</summary>
    public long FTenantId { get; set; }

    /// <summary>对应组织节点 FID（↔ SYS组织架构 FKind=网点公司 节点，1:1，事务联动）</summary>
    public long FOrgNodeId { get; set; }

    /// <summary>名称（派生自组织节点）</summary>
    public string FName { get; set; } = string.Empty;

    /// <summary>统一社会信用代码</summary>
    public string? FCreditCode { get; set; }

    public int FStatus { get; set; } = 1;

    // 不设 FOrgId：本表是租户级主数据(1:1 组织节点)，非组织级业务行——只挂 ITenantScoped 硬墙，不参与组织隔离(design/23 D4)。

    /// <summary>并发令牌</summary>
    public byte[]? FRowVersion { get; set; }

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
