namespace STOTOP.Module.Finance.Dtos;

public class AmoebaPLTemplateDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public long AccountSetId { get; set; }
    public List<AmoebaPLItemDto> Items { get; set; } = new();
}

public class AmoebaPLItemDto
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string NodeRole { get; set; } = "data"; // 节点角色：group/data/formula/indicator
    public string? Formula { get; set; }
    public int Sort { get; set; }
    public long ParentId { get; set; }
    public string? RelatedAccountsJson { get; set; }
    public string? DataSource { get; set; }
    public string? SummaryKeywordsJson { get; set; }
    public string? Unit { get; set; }                 // 单位
    public string? DataSourceRemark { get; set; }     // 数据来源说明
    public string? CalculationLogic { get; set; }     // 计算逻辑说明
    public string? PerUnitMode { get; set; }          // 单票&均计算方式：auto/manual/none
    public int? DecimalPlaces { get; set; }              // 小数位数：null=按单位自动判断, 1~4
    public bool IsManualEntry { get; set; }           // 是否手工填报项
    public string? BillingFilterJson { get; set; }    // 计费过滤条件JSON
    // 正交二维字段（新增）
    public string? ItemCategory { get; set; }        // F项目类别: indicator/revenue/cost/profit/section
    public string? ValueSource { get; set; }         // F值来源: system/formula/manual
    public string? SystemDataSource { get; set; }    // F系统数据源: voucher/billing/estimate/depreciation
    public bool IsIndicatorSection { get; set; }     // F是否指标分区
    public List<AmoebaPLItemDto> Children { get; set; } = new();
}

public class CreateAmoebaPLTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long AccountSetId { get; set; }
}

public class UpdateAmoebaPLTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CloneAmoebaPLTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public long AccountSetId { get; set; }
    public string? Description { get; set; }
}

public class CreateAmoebaPLItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public string NodeRole { get; set; } = "data"; // 节点角色：group/data/formula/indicator
    public string? Formula { get; set; }
    public int Sort { get; set; }
    public long ParentId { get; set; }
    public string? RelatedAccountsJson { get; set; }
    public string? DataSource { get; set; }
    public string? SummaryKeywordsJson { get; set; }
    public string? Unit { get; set; }                 // 单位
    public string? DataSourceRemark { get; set; }     // 数据来源说明
    public string? CalculationLogic { get; set; }     // 计算逻辑说明
    public string? PerUnitMode { get; set; }          // 单票&均计算方式：auto/manual/none
    public int? DecimalPlaces { get; set; }              // 小数位数：null=按单位自动判断, 1~4
    public bool IsManualEntry { get; set; }           // 是否手工填报项
    public string? BillingFilterJson { get; set; }    // 计费过滤条件JSON
    // 正交二维字段（新增）
    public string? ItemCategory { get; set; }        // F项目类别: indicator/revenue/cost/profit/section
    public string? ValueSource { get; set; }         // F值来源: system/formula/manual
    public string? SystemDataSource { get; set; }    // F系统数据源: voucher/billing/estimate/depreciation
    public bool IsIndicatorSection { get; set; }     // F是否指标分区
}

public class UpdateAmoebaPLItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public string? NodeRole { get; set; }            // 允许更新节点角色（可选）：group/data/formula/indicator
    public string? Formula { get; set; }
    public int Sort { get; set; }
    public long? ParentId { get; set; }  // 支持移动操作（拖拽调整父子关系）
    public string? RelatedAccountsJson { get; set; }
    public string? DataSource { get; set; }
    public string? SummaryKeywordsJson { get; set; }
    public string? Unit { get; set; }                 // 单位
    public string? DataSourceRemark { get; set; }     // 数据来源说明
    public string? CalculationLogic { get; set; }     // 计算逻辑说明
    public string? PerUnitMode { get; set; }          // 单票&均计算方式：auto/manual/none
    public int? DecimalPlaces { get; set; }              // 小数位数：null=按单位自动判断, 1~4
    public bool? IsManualEntry { get; set; }          // 是否手工填报项
    public string? BillingFilterJson { get; set; }    // 计费过滤条件JSON
    // 正交二维字段（新增）
    public string? ItemCategory { get; set; }        // F项目类别: indicator/revenue/cost/profit/section
    public string? ValueSource { get; set; }         // F值来源: system/formula/manual
    public string? SystemDataSource { get; set; }    // F系统数据源: voucher/billing/estimate/depreciation
    public bool? IsIndicatorSection { get; set; }    // F是否指标分区
}

public class ReorderAmoebaPLItemRequest
{
    public long ItemId { get; set; }
    public int Sort { get; set; }
    public long ParentId { get; set; }  // 0 表示顶级项
}

public class CloneAmoebaPLItemRequest
{
    public long SourceTemplateId { get; set; }
    public long SourceItemId { get; set; }
    public long? TargetParentId { get; set; }
    public bool CloneChildren { get; set; } = true;
}



/// <summary>模板科目覆盖率诊断报告</summary>
public class AmoebaCoverageReportDto
{
    /// <summary>模板ID</summary>
    public long TemplateId { get; set; }
    /// <summary>诊断期间（YYYYMM）</summary>
    public string Period { get; set; } = string.Empty;
    /// <summary>凭证数据点总数</summary>
    public int TotalDataPoints { get; set; }
    /// <summary>已匹配数据点数</summary>
    public int MatchedDataPoints { get; set; }
    /// <summary>未匹配数据点数</summary>
    public int UnmatchedDataPoints { get; set; }
    /// <summary>未匹配金额合计</summary>
    public decimal UnmatchedAmount { get; set; }
    /// <summary>覆盖率百分比（已匹配/总数 * 100）</summary>
    public decimal CoverageRate { get; set; }
    /// <summary>已配置的科目编码前缀列表（所有损益项的 FRelatedAccountsJson 合集）</summary>
    public List<string> ConfiguredAccountCodes { get; set; } = new();
    /// <summary>未覆盖科目明细（按科目编码聚合）</summary>
    public List<AmoebaUncoveredAccountDto> UncoveredAccounts { get; set; } = new();
}

/// <summary>未覆盖科目明细</summary>
public class AmoebaUncoveredAccountDto
{
    /// <summary>科目编码</summary>
    public string AccountCode { get; set; } = string.Empty;
    /// <summary>科目名称</summary>
    public string AccountName { get; set; } = string.Empty;
    /// <summary>科目类别（revenue/cost/expense等）</summary>
    public string AccountCategory { get; set; } = string.Empty;
    /// <summary>未匹配凭证分录数</summary>
    public int EntryCount { get; set; }
    /// <summary>未匹配金额合计</summary>
    public decimal TotalAmount { get; set; }
    /// <summary>建议归属Tab（根据科目类别推断）</summary>
    public string? SuggestedTab { get; set; }
}
