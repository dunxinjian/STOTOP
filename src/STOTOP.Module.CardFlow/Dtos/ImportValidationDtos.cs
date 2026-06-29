namespace STOTOP.Module.CardFlow.Dtos;

public enum ValidationDomain
{
    Voucher = 1,
    Pricing = 2,
    Cost = 3
}

public enum ValidationAttribution
{
    None = 0,
    ImportData = 1,
    Configuration = 2,
    CalculationLogic = 3,
    Persistence = 4
}

public enum ValidationSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Blocker = 4
}

public class ImportValidationRunRequest
{
    public List<ValidationDomain> Domains { get; set; } =
    [
        ValidationDomain.Voucher,
        ValidationDomain.Pricing,
        ValidationDomain.Cost
    ];

    /// <summary>验证模式：sample=抽样, errorsOnly=异常优先, allLimited=有限全量（兼容旧值 all）</summary>
    public string Mode { get; set; } = "sample";
    public int SampleSize { get; set; } = 100;
    public bool IncludeEvidence { get; set; } = true;
    public decimal Tolerance { get; set; } = 0.01m;
}

public class ImportValidationSummaryDto
{
    public long BatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? FlowName { get; set; }
    public string? TargetTable { get; set; }
    public int TotalRows { get; set; }
    /// <summary>批次状态机：0=解析中, 1=已暂存, 2=质检中, 3=已创建卡片, 4=处理中, 5=已完成</summary>
    public int BatchStatus { get; set; }
    public string BatchStatusText { get; set; } = string.Empty;
    /// <summary>批次节点链仍在执行中（计费/成本结果可能尚未写入，验证结果不完整）</summary>
    public bool IsBatchRunning { get; set; }
    public bool IsRevoked { get; set; }
    public string? CurrentNodeName { get; set; }
    public int? ProgressPercent { get; set; }
    public string? BatchErrorMessage { get; set; }
    public DateTime? ImportStartTime { get; set; }
    public DateTime? ImportEndTime { get; set; }
    public Dictionary<ValidationDomain, int> ExistingResultCounts { get; set; } = new();
}

public class ImportValidationReportDto
{
    public long BatchId { get; set; }
    public string BatchStatusText { get; set; } = string.Empty;
    /// <summary>验证执行时批次节点链仍在运行（结果可能不完整）</summary>
    public bool IsBatchRunning { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int CheckedRows { get; set; }
    public List<ImportValidationSampleRowDto> SampleRows { get; set; } = [];
    public List<ImportValidationFindingDto> Findings { get; set; } = [];
    public Dictionary<ValidationAttribution, int> AttributionCounts { get; set; } = new();
    public Dictionary<ValidationSeverity, int> SeverityCounts { get; set; } = new();
}

public class ImportValidationSampleRowDto
{
    public long? SourceRowId { get; set; }
    public string? BusinessKey { get; set; }
    public string? WaybillNo { get; set; }
    public Dictionary<string, object?> SourceFields { get; set; } = new();
    public List<ImportValidationSampleResultDto> Results { get; set; } = [];
    public List<ImportValidationFindingDto> Findings { get; set; } = [];
}

public class ImportValidationSampleResultDto
{
    public ValidationDomain Domain { get; set; }
    public string Label { get; set; } = string.Empty;
    public object? OriginalValue { get; set; }
    public object? SystemValue { get; set; }
    public object? ExpectedValue { get; set; }
    public decimal? Difference { get; set; }
    public string Status { get; set; } = "unknown";
    public string Message { get; set; } = string.Empty;
    public List<ImportValidationSampleCostItemDto> CostItems { get; set; } = [];
    /// <summary>凭证域行级核验明细（原始字段→规则配置→凭证结果三段证据链），仅凭证域且开启证据时返回</summary>
    public ImportValidationVoucherDetailDto? VoucherDetail { get; set; }
    public Dictionary<string, object?> PersistedResult { get; set; } = new();
    public List<CalculationTraceStepDto> TraceSteps { get; set; } = [];
}

/// <summary>凭证行级核验明细：供人工对照 原始数据 / 命中规则配置 / 实际凭证结果</summary>
public class ImportValidationVoucherDetailDto
{
    public bool PassedFilter { get; set; }
    public bool Matched { get; set; }
    public int? MatchedLayer { get; set; }
    /// <summary>命中原因（哪一层、靠哪个字段值/关键词命中）</summary>
    public string? MatchReason { get; set; }
    public bool RoutedButNoOutput { get; set; }
    public string? RuleGroupName { get; set; }
    public string? AmountAggregation { get; set; }
    /// <summary>该行参与凭证生成的关键源字段名值（匹配字段/金额字段/业务键/日期/分组）</summary>
    public Dictionary<string, object?> SourceFieldValues { get; set; } = new();
    /// <summary>命中规则组的分录行配置（含禁用行，便于核对配置全貌）</summary>
    public List<ImportValidationVoucherRuleLineDto> RuleLines { get; set; } = [];
    /// <summary>按当前规则推演的分录草案（ROW 模式=该行凭证；SUM 模式=该行贡献额）</summary>
    public List<ImportValidationVoucherEntryDto> DraftEntries { get; set; } = [];
    public decimal DraftDebitTotal { get; set; }
    public decimal DraftCreditTotal { get; set; }
    /// <summary>仅 ROW 模式有值（单行独立成凭证才能行级判平）</summary>
    public bool? DraftBalanced { get; set; }
    /// <summary>仅 ROW 模式：按 keyFields 重算的业务键（对应 FIN凭证.F数据作用域ID）</summary>
    public string? BusinessKey { get; set; }
    public long? ActualVoucherId { get; set; }
    public string? ActualVoucherNo { get; set; }
    /// <summary>实际生成凭证的分录明细（按业务键反查）</summary>
    public List<ImportValidationVoucherEntryDto> ActualEntries { get; set; } = [];
    public decimal? ActualDebitTotal { get; set; }
    public decimal? ActualCreditTotal { get; set; }
    public bool? ActualBalanced { get; set; }
    public List<string> Issues { get; set; } = [];
}

/// <summary>规则组分录行配置快照（人工核验"规则怎么配的"）</summary>
public class ImportValidationVoucherRuleLineDto
{
    public int LineNo { get; set; }
    public string Direction { get; set; } = "借";
    /// <summary>科目配置描述：固定/动态匹配/兜底</summary>
    public string AccountText { get; set; } = string.Empty;
    public string? AmountField { get; set; }
    public string? SummaryTemplate { get; set; }
    /// <summary>条件行描述：字段∈[值...]；空=兜底行</summary>
    public string? ConditionText { get; set; }
    public string? AuxiliaryText { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>凭证分录（草案或实际）</summary>
public class ImportValidationVoucherEntryDto
{
    public int LineNo { get; set; }
    public string Direction { get; set; } = "借";
    public long? AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public decimal? Amount { get; set; }
    /// <summary>草案分录的金额来源字段</summary>
    public string? AmountField { get; set; }
    public string? Summary { get; set; }
    public string? AuxiliaryText { get; set; }
    public string? Issue { get; set; }
}

public class ImportValidationSampleCostItemDto
{
    public int CostItemId { get; set; }
    public string CostItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ImportValidationFindingDto
{
    public ValidationDomain Domain { get; set; }
    public ValidationAttribution Attribution { get; set; }
    public ValidationSeverity Severity { get; set; }
    public decimal Confidence { get; set; }
    public int AffectedRows { get; set; } = 1;
    public long? SourceRowId { get; set; }
    public string? BusinessKey { get; set; }
    public string? WaybillNo { get; set; }
    public long? VoucherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal? SystemValue { get; set; }
    public decimal? ExpectedValue { get; set; }
    public decimal? Difference { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
    public ImportValidationEvidenceDto Evidence { get; set; } = new();
}

public class ImportValidationEvidenceDto
{
    public Dictionary<string, object?> SourceFields { get; set; } = new();
    public List<string> MatchedConfigurations { get; set; } = [];
    public List<string> ConfigurationIssues { get; set; } = [];
    public List<CalculationTraceStepDto> TraceSteps { get; set; } = [];
    public Dictionary<string, object?> PersistedResult { get; set; } = new();
}

public class CalculationTraceStepDto
{
    public string Step { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? InputValue { get; set; }
    public decimal? OutputValue { get; set; }
    public string? Formula { get; set; }
}
