namespace STOTOP.Module.CardFlow.Services.Validation;

/// <summary>
/// 价格解释提供方。
/// 由 Express 模块实现并注册（CardFlow 不引用 Express，依赖方向倒置），
/// 对批次内指定运单按当前报价配置做只读解释计算，供导入计算验证工作台
/// 对比"解释值 vs 系统落库值"，区分配置问题、疑似计算逻辑问题与写入链路问题。
/// 未注册实现时验证工作台跳过解释对比，仅保留落库结果校验。
/// </summary>
public interface IPricingExplainProvider
{
    /// <summary>
    /// 解释批次内指定运单的价格计算过程。
    /// 返回 运单号（忽略大小写）→ 解释快照；批次无价格计算节点或解释失败时返回空字典，不抛异常。
    /// </summary>
    Task<IReadOnlyDictionary<string, PricingExplainSnapshot>> ExplainAsync(
        PricingExplainRequest request,
        CancellationToken cancellationToken = default);
}

public class PricingExplainRequest
{
    public long BatchId { get; set; }
    public long FlowDefinitionId { get; set; }
    public long OrgId { get; set; }
    public List<string> WaybillNos { get; set; } = [];
}

/// <summary>单票价格解释快照（六步业务对象链路）</summary>
public class PricingExplainSnapshot
{
    public string WaybillNo { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>全部业务对象的解释应收合计（含佣金）</summary>
    public decimal TotalChargeAmount { get; set; }
    public List<string> ConfigurationIssues { get; set; } = [];
    public List<PricingExplainStepSnapshot> Steps { get; set; } = [];
}

/// <summary>六步链路中单个业务对象（KH/DL/WD/YW/CB/YZ）的解释结果</summary>
public class PricingExplainStepSnapshot
{
    public string ClientType { get; set; } = string.Empty;
    public bool QuotationMatched { get; set; }
    public string? QuotationCode { get; set; }
    public string? ClientName { get; set; }
    public decimal? BillableWeight { get; set; }
    public bool SegmentMatched { get; set; }
    public int? SegmentIndex { get; set; }
    public bool PriceCellMatched { get; set; }
    public decimal? Freight { get; set; }
    public decimal? Surcharge { get; set; }
    public decimal? InsuranceFee { get; set; }
    public decimal? CommissionAmount { get; set; }
    /// <summary>解释应收金额 = 运费 + 附加费 + 保价费</summary>
    public decimal? ChargeAmount { get; set; }
    /// <summary>首续重公式及参数说明（业务可读）</summary>
    public string? FormulaText { get; set; }
    public List<string> ConfigurationIssues { get; set; } = [];
}
