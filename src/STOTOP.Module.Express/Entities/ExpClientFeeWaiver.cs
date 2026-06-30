using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 费用减免
/// </summary>
public class ExpClientFeeWaiver : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>减免类型 1-5</summary>
    public int FWaiverType { get; set; }
    /// <summary>减免名称</summary>
    public string? FWaiverName { get; set; }
    /// <summary>是否启用</summary>
    public bool FIsActive { get; set; } = true;
    /// <summary>生效日期</summary>
    public DateTime? FEffectiveDate { get; set; }
    /// <summary>失效日期</summary>
    public DateTime? FExpiryDate { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
