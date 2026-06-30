using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 快递报价（核心聚合根）
/// </summary>
public class ExpQuotation : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>方案编号（业务编号，如 STO-BJ01-KH001-20260501-01）</summary>
    public string? FPlanCode { get; set; }
    /// <summary>方案名称</summary>
    public string FPlanName { get; set; } = string.Empty;
    /// <summary>网点编号</summary>
    public string? FNetworkPointCode { get; set; }
    /// <summary>业务对象类型 KH/DL/WD/YW/CB/YZ</summary>
    public string? FClientType { get; set; }
    /// <summary>业务对象ID（自然编号）</summary>
    public string? FBusinessObjectId { get; set; }
    /// <summary>生效日期</summary>
    public DateOnly? FEffectiveDate { get; set; }
    /// <summary>结算重量环节：1=揽收重量, 2=中转重量, 3=到件重量, 4=集包重量, 5=计泡重量, 6=总部重量, 7=取所有环节最大值</summary>
    public int FSettlementWeightStage { get; set; } = 1;
    /// <summary>状态 0草稿 1生效 2过期 3作废</summary>
    public int FStatus { get; set; } = 0;
    // 商务条款
    /// <summary>付款方式 1预付 2后付 3混合</summary>
    public int FPaymentMode { get; set; } = 2;
    /// <summary>预付比例</summary>
    public decimal? FPrepayRatio { get; set; }
    /// <summary>账单周期</summary>
    public int FBillingCycle { get; set; } = 2;
    /// <summary>出账日</summary>
    public int? FBillingDay { get; set; }
    /// <summary>付款截止日</summary>
    public int? FPaymentDueDay { get; set; }
    /// <summary>抛比</summary>
    public int FThrowRatio { get; set; } = 8000;
    /// <summary>保价费率</summary>
    public decimal? FInsuranceRate { get; set; }
    /// <summary>OA流程ID</summary>
    public long? FOaProcessId { get; set; }
    /// <summary>审批人</summary>
    public string? FApprovedBy { get; set; }
    /// <summary>审批时间</summary>
    public DateTime? FApprovedAt { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    /// <summary>共享店铺开关</summary>
    public bool FSharedShopEnabled { get; set; }
    /// <summary>上一版本ID</summary>
    public long? F上一版本ID { get; set; }
    /// <summary>含税</summary>
    public bool F含税 { get; set; }
    /// <summary>源FID - 数据迁移来源</summary>
    public long? F源FID { get; set; }
    /// <summary>版本</summary>
    public int F版本 { get; set; } = 1;
    /// <summary>税率</summary>
    public decimal? F税率 { get; set; }
    /// <summary>重量进位方式 0=无 5=五入 9=九进</summary>
    public int? F重量进位方式 { get; set; }

    /// <summary>矩阵JSON（报价矩阵完整序列化）</summary>
    public string? FMatrixJson { get; set; }

    // 导航属性
    /// <summary>关联店铺</summary>
    public List<ExpQuotationShop> Shops { get; set; } = new();
    /// <summary>共享别名</summary>
    public List<ExpQuotationAlias> Aliases { get; set; } = new();
    /// <summary>佣金配置</summary>
    public ExpQuotationCommission? Commission { get; set; }

}
