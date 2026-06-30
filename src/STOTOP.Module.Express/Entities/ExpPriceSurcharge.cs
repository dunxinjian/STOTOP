using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 附加费
/// </summary>
public class ExpPriceSurcharge : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>源系统FID（数据溯源）</summary>
    public long? FSourceId { get; set; }
    /// <summary>名称</summary>
    public string FName { get; set; } = string.Empty;
    /// <summary>类型</summary>
    public int FSurchargeType { get; set; }
    /// <summary>生效日期</summary>
    public DateTime FEffectiveDate { get; set; }
    /// <summary>启用</summary>
    public bool FIsActive { get; set; } = true;
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    /// <summary>配置项</summary>
    public List<ExpPriceSurchargeItem> Items { get; set; } = new();

    /// <summary>作用域 0=全局, 1=业务对象级, 2=报价级</summary>
    public int FScope { get; set; }
    /// <summary>网点编码，null=品牌全局</summary>
    public string? FNetworkPointCode { get; set; }

    /// <summary>作用域关联</summary>
    public ICollection<ExpPriceSurchargeScope> Scopes { get; set; } = new List<ExpPriceSurchargeScope>();

    /// <summary>业务对象ID（业务对象编号；作用域关联以 Scopes 为准）</summary>
    public string? FBusinessObjectId { get; set; }
    /// <summary>失效日期</summary>
    public DateTime? FExpiryDate { get; set; }
}
