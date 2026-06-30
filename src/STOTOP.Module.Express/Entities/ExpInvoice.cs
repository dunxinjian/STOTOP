using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 账单
/// </summary>
public class ExpInvoice : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>账单号</summary>
    public string? FInvoiceNo { get; set; }
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>报价ID</summary>
    public long? FQuotationId { get; set; }
    /// <summary>业务对象类型 KH/DL/WD/YW/CB/YZ</summary>
    public string? FClientType { get; set; }
    /// <summary>品牌ID</summary>
    public string? FBrandCode { get; set; }
    /// <summary>网点ID（哪个网点出的账单）</summary>
    public long? FNetworkPointId { get; set; }
    /// <summary>账期开始</summary>
    public DateTime FPeriodStart { get; set; }
    /// <summary>账期结束</summary>
    public DateTime FPeriodEnd { get; set; }
    /// <summary>总单量</summary>
    public int? FTotalWaybills { get; set; }
    /// <summary>总重量</summary>
    public decimal? FTotalWeight { get; set; }
    /// <summary>平均重量</summary>
    public decimal? FAvgWeight { get; set; }
    /// <summary>均重上限</summary>
    public decimal? FWeightCap { get; set; }
    /// <summary>超出均重</summary>
    public decimal? FExcessWeight { get; set; }
    /// <summary>均重追补</summary>
    public decimal? FWeightCapSurcharge { get; set; }
    /// <summary>占比追补</summary>
    public decimal? FQuotaSurcharge { get; set; }
    /// <summary>总应收</summary>
    public decimal? FTotalCharge { get; set; }
    /// <summary>含追补应收</summary>
    public decimal? FTotalChargeWithSurcharge { get; set; }
    /// <summary>总成本</summary>
    public decimal? FTotalCost { get; set; }
    /// <summary>总利润</summary>
    public decimal? FTotalProfit { get; set; }
    /// <summary>预付抵扣</summary>
    public decimal? FPrepayDeduction { get; set; }
    /// <summary>应付金额</summary>
    public decimal? FPayableAmount { get; set; }
    /// <summary>审核状态 0待审核 1自动通过 2人工通过 3人工驳回 4反审核</summary>
    public int FReviewStatus { get; set; }
    /// <summary>审核人</summary>
    public string? FReviewer { get; set; }
    /// <summary>审核时间</summary>
    public DateTime? FReviewTime { get; set; }
    /// <summary>审核备注</summary>
    public string? FReviewRemark { get; set; }
    /// <summary>状态 0未确认 1已确认 2已发送 3已收款 4已归档</summary>
    public int FStatus { get; set; }
    /// <summary>已归档</summary>
    public bool FArchived { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>归档时间</summary>
    public DateTime? FArchivedTime { get; set; }
    /// <summary>归档人</summary>
    public string? FArchivedBy { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // 对账字段
    /// <summary>对账状态 0:未对账 1:已对账 2:有异议 3:异议已解决</summary>
    public int FReconciliationStatus { get; set; }
    /// <summary>对账备注</summary>
    public string? FReconciliationRemarks { get; set; }
    /// <summary>对账操作人ID</summary>
    public long? FReconciliationBy { get; set; }
    /// <summary>对账时间</summary>
    public DateTime? FReconciliationTime { get; set; }
    /// <summary>异议原因</summary>
    public string? FDisputeReason { get; set; }
    /// <summary>异议处理人ID</summary>
    public long? FDisputeResolvedBy { get; set; }
    /// <summary>异议处理时间</summary>
    public DateTime? FDisputeResolvedTime { get; set; }
    /// <summary>异议处理说明</summary>
    public string? FDisputeResolution { get; set; }
}
