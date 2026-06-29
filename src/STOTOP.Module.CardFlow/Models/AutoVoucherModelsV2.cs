namespace STOTOP.Module.CardFlow.Models;

/// <summary>
/// AutoVoucher V2 规则配置 - 三层级联匹配引擎
/// </summary>
public class RulesBasedVoucherConfigV2
{
    public string Mode { get; set; } = "rulesBased";
    public int Version { get; set; } = 2;

    // --- 全局配置 ---
    public string VoucherWord { get; set; } = "记";
    public string? DateField { get; set; }
    public string? StagingTable { get; set; }
    public string? GroupBy { get; set; }
    public long? FileTypeId { get; set; }
    /// <summary>[D8] 目标账套ID（必填）</summary>
    public long? AccountSetId { get; set; }
    /// <summary>
    /// [D2] 凭证业务键字段，用于 ComputeBusinessKey 去重。
    /// 不配置将导致所有行被去重为1行。
    /// </summary>
    public List<string>? KeyFields { get; set; }
    /// <summary>
    /// [D9] 未匹配行处理策略：
    /// "skip" - 跳过未匹配行（默认）
    /// "createDraft" - 生成待补录草稿凭证
    /// "error" - 整批标记失败
    /// </summary>
    public string UnmatchedAction { get; set; } = "skip";
    /// <summary>[D10] createDraft 未匹配行的待补录占位科目ID（借方），为空则跳过待补录草稿生成（防 F科目ID FK 冲突）</summary>
    public long? DraftPlaceholderAccountId { get; set; }
    /// <summary>
    /// [E5] 数据行预筛选条件（匹配引擎运行前过滤，如排除金额=0或作废行）。
    /// 为空则不过滤，所有行均进入匹配引擎。
    /// </summary>
    public List<FilterConditionItem>? FilterConditions { get; set; }

    // --- 三层匹配字段配置 ---
    public MatchingLayerConfig MatchingLayers { get; set; } = new();

    // --- 规则组 ---
    public List<RuleGroupV2> RuleGroups { get; set; } = new();
}

/// <summary>
/// 三层匹配字段配置
/// </summary>
public class MatchingLayerConfig
{
    /// <summary>第一层：精确编码匹配字段（如 "F费用编码"）</summary>
    public string? ExactMatchField { get; set; }
    /// <summary>第二层：分类匹配字段（如 "F费用类别"）</summary>
    public string? CategoryField { get; set; }
    /// <summary>第三层：摘要匹配字段（如 "F费用摘要"）</summary>
    public string? SummaryField { get; set; }
}

/// <summary>
/// V2 规则组定义
/// </summary>
public class RuleGroupV2
{
    /// <summary>[D1] 规则组稳定标识符（GUID），Name 变更不影响匹配逻辑</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>规则组显示名称（仅用于 UI 展示，不参与匹配逻辑）</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>[D14] 规则组评估顺序（同长度关键词冲突时按此排序决断）</summary>
    public int Order { get; set; }
    /// <summary>Layer1: 精确匹配编码列表</summary>
    public List<string>? ExactCodes { get; set; }
    /// <summary>[D4] Layer2: 精确分类值列表（与 Name 解耦，支持规则组名≠分类值）</summary>
    public List<string>? ExactCategories { get; set; }
    /// <summary>Layer2: 分类关键词模糊匹配</summary>
    public List<string>? CategoryKeywords { get; set; }
    /// <summary>Layer3: 摘要关键词</summary>
    public List<string>? SummaryKeywords { get; set; }
    /// <summary>[K1] 组内无匹配分录时是否回退到下一层</summary>
    public bool Fallthrough { get; set; } = false;
    /// <summary>
    /// [F1] 聚合方式（规则组级别，确保组内所有分录行一致）
    /// "ROW" - 逐行生成凭证（每行数据产生一张独立凭证，GroupBy无效）
    /// "SUM" - 汇总生成凭证（GroupBy组产生一张凭证，金额求和，摘要/辅助取首行）
    /// </summary>
    public string AmountAggregation { get; set; } = "SUM";
    /// <summary>[F7] SUM模式下"取首行"的排序字段（默认按STG表主键FID升序）</summary>
    public string? SumFirstRowOrderBy { get; set; }
    /// <summary>分录行配置</summary>
    public List<EntryLineV2> Lines { get; set; } = new();
}

/// <summary>
/// V2 分录行定义
/// </summary>
public class EntryLineV2
{
    public int LineNo { get; set; }
    /// <summary>方向：借/贷</summary>
    public string Direction { get; set; } = "借";
    // 科目配置
    /// <summary>固定科目ID</summary>
    public long? AccountId { get; set; }
    /// <summary>固定科目编码（用于跨账套复制规则时通过编码重新映射FID）</summary>
    public string? AccountCode { get; set; }
    /// <summary>动态科目匹配字段名</summary>
    public string? AccountMatchField { get; set; }
    /// <summary>动态科目映射规则列表</summary>
    public List<AccountMatchItem>? AccountMatchRules { get; set; }
    /// <summary>[D6] 动态科目映射未命中时的兜底科目ID（为空则标记该行为"待补录"）</summary>
    public long? DefaultAccountId { get; set; }
    // 金额配置
    public string AmountField { get; set; } = string.Empty;
    // 摘要模板
    /// <summary>摘要模板，支持 {字段名} 变量替换</summary>
    public string? SummaryTemplate { get; set; }
    // 条件过滤（组内进一步筛选）
    /// <summary>条件过滤字段名（同方向内互斥分配）</summary>
    public string? ConditionField { get; set; }
    /// <summary>条件匹配值列表</summary>
    public List<string>? ConditionValues { get; set; }
    // 辅助核算
    /// <summary>辅助核算配置列表（支持多类型）</summary>
    public List<AuxiliaryConfigV2>? AuxiliaryConfigs { get; set; }
    /// <summary>[方案B 源打标] 科目契约(FinAccount.FAuxiliary)声明、但 AuxiliaryConfigs 未覆盖的维度按此补齐
    /// （business_direction 走 fixed=OUT/IN/CMB；project/department 等按需）。为空则不补齐、不改现有行为。</summary>
    public List<AuxiliaryConfigV2>? DefaultAuxiliaryConfigs { get; set; }
    /// <summary>[E7] 分录行在凭证中的显示顺序（借方在前贷方在后的原则下，同方向按此排序）</summary>
    public int DisplayOrder { get; set; }
    /// <summary>[E7] 启用状态：1=启用, 0=禁用（禁用后不参与凭证生成，但保留配置供后续恢复）</summary>
    public int Status { get; set; } = 1;
}

/// <summary>
/// [D3] 动态科目映射条目
/// </summary>
public class AccountMatchItem
{
    /// <summary>匹配值（如"城区"、"沙溪"）</summary>
    public string MatchValue { get; set; } = string.Empty;
    /// <summary>目标科目ID</summary>
    public long AccountId { get; set; }
    /// <summary>目标科目编码（用于跨账套复制时重新映射）</summary>
    public string? AccountCode { get; set; }
}

/// <summary>
/// [E5] 数据行预筛选条件项
/// </summary>
public class FilterConditionItem
{
    /// <summary>源数据字段名</summary>
    public string Field { get; set; } = string.Empty;
    /// <summary>操作符：eq/neq/gt/gte/lt/lte/contains/notEmpty</summary>
    public string Operator { get; set; } = "neq";
    /// <summary>比较值（notEmpty 操作符时可为空）</summary>
    public string? Value { get; set; }
}

/// <summary>
/// 辅助核算配置 V2 - 每条分录行支持配置多个辅助类型
/// </summary>
public class AuxiliaryConfigV2
{
    /// <summary>辅助类型编码：customer/supplier/department/project/employee/business_unit/express_brand 等</summary>
    public string AuxType { get; set; } = string.Empty;
    /// <summary>值来源方式：fixed(固定值) / dynamic(从源数据字段动态取值)</summary>
    public string SourceType { get; set; } = "fixed";
    /// <summary>fixed模式：直接指定辅助核算项目ID</summary>
    public long? FixedItemId { get; set; }
    /// <summary>fixed模式：辅助核算项目编码（与 FixedItemId 双写，跨账套重映射使用）</summary>
    public string? FixedItemCode { get; set; }
    /// <summary>fixed模式：直接指定辅助核算项目编码或名称</summary>
    public string? FixedValue { get; set; }
    /// <summary>dynamic模式：取值的源数据字段名</summary>
    public string? SourceField { get; set; }
    /// <summary>
    /// dynamic模式的匹配策略：
    /// "code" / "exact_code" - 源字段值按FCode精确匹配辅助核算项目
    /// "name" / "exact_name" - 源字段值按FName精确匹配辅助核算项目
    /// "keyword" / "source_contains_name" - 源字段值在FName中做关键词匹配（最长优先）
    /// "contains" / "name_contains_source" - FName包含源字段值（适用于网点名称等模糊场景）
    /// </summary>
    public string MatchBy { get; set; } = "code";
}

/// <summary>
/// [E1] 匹配候选结果
/// </summary>
/// <param name="GroupId">命中的规则组ID</param>
/// <param name="Layer">命中的层级（1/2/3）</param>
public record MatchCandidate(string GroupId, int Layer);
