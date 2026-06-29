using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 计费结果历史（归档）
/// </summary>
public class ExpBillingResultHistory : BaseEntity, IOrgScoped
{
    /// <summary>批次ID</summary>
    public long FBatchId { get; set; }
    /// <summary>运单编号</summary>
    public string? FWaybillNo { get; set; }
    /// <summary>运单日期</summary>
    public DateTime? FWaybillDate { get; set; }
    /// <summary>参与方ID（业务对象F编号）</summary>
    public string FPartyClientId { get; set; } = string.Empty;
    /// <summary>参与方角色 1应收 2层级应收 3佣金</summary>
    public int? FPartyRole { get; set; }
    /// <summary>层级</summary>
    public int? FChainLevel { get; set; }
    /// <summary>品牌编码</summary>
    public string? FBrandCode { get; set; }
    /// <summary>计费日期</summary>
    public DateTime? FBillingDate { get; set; }
    /// <summary>计费重量</summary>
    public decimal? FBillableWeight { get; set; }
    /// <summary>基础运费</summary>
    public decimal? FFreightCharge { get; set; }
    /// <summary>保价费</summary>
    public decimal? FInsuranceFee { get; set; }
    /// <summary>加收费用</summary>
    public decimal? FSurchargeAmount { get; set; }
    /// <summary>减免金额</summary>
    public decimal? FWaiverAmount { get; set; }
    /// <summary>佣金金额</summary>
    public decimal? FCommissionAmount { get; set; }
    /// <summary>应收金额</summary>
    public decimal? FChargeAmount { get; set; }
    /// <summary>业务对象类型 KH/DL/WD/YW/CB/YZ</summary>
    public string? FClientType { get; set; }
    /// <summary>报价编号</summary>
    public string? FQuotationCode { get; set; }
    /// <summary>报价ID</summary>
    public long? FQuotationId { get; set; }
    /// <summary>佣金规则ID</summary>
    public long? FCommissionRuleId { get; set; }
    /// <summary>计算状态 1正常 2异常</summary>
    public int FCalcStatus { get; set; } = 1;
    /// <summary>异常信息</summary>
    public string? FErrorMessage { get; set; }
    /// <summary>账单ID</summary>
    public long? FInvoiceId { get; set; }
    /// <summary>目的省份ID</summary>
    public int FDestinationProvinceId { get; set; }
    /// <summary>目的省份名称（冗余）</summary>
    public string? FDestProvinceName { get; set; }
    /// <summary>归属网点编号（冗余，加速多网点过滤查询）</summary>
    public string? FNetworkPointCode { get; set; }
    /// <summary>成本合计</summary>
    public decimal FTotalCost { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>归档时间</summary>
    public DateTime FArchivedAt { get; set; } = DateTime.Now;
}
