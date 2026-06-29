using STOTOP.Module.CardFlow.Models;

namespace STOTOP.Module.CardFlow.Services.Handlers;

/// <summary>
/// AutoVoucher V2 三层级联匹配引擎
/// <para>层级：Layer1(精确编码 O(1)) → Layer2(分类精确+关键词最长优先) → Layer3(摘要关键词最长优先)</para>
/// <para>设计要点：</para>
/// <list type="bullet">
///   <item>[D1] 返回规则组 Id 而非 Name</item>
///   <item>[E1] 返回候选列表支持 Fallthrough 重试</item>
///   <item>[F4] 候选列表去重：同一 GroupId 仅保留层级最高的条目</item>
///   <item>[G4] 检查规则组内是否存在至少一条可接纳当前行的分录行</item>
///   <item>[H8] 跨方向检查（借/贷任一方有匹配即可），仅用于 Fallthrough 判定</item>
/// </list>
/// </summary>
public class AutoVoucherMatchingEngineV2
{
    /// <summary>精确编码索引：code → groupId</summary>
    private Dictionary<string, string> _exactCodeIndex = new();

    /// <summary>精确分类索引：category → groupId</summary>
    private Dictionary<string, string> _exactCategoryIndex = new();

    /// <summary>按 Order 排序的规则组列表（用于关键词最长优先遍历）</summary>
    private List<RuleGroupV2> _orderedGroups = new();

    /// <summary>规则组 Id → RuleGroupV2 快速查找</summary>
    private Dictionary<string, RuleGroupV2> _groupsById = new();

    /// <summary>当前配置引用</summary>
    private RulesBasedVoucherConfigV2 _config = null!;

    /// <summary>
    /// 初始化引擎（per-execution 构建预索引）
    /// <para>从 config.RuleGroups 构建精确编码索引、精确分类索引、排序组列表及 Id 字典。</para>
    /// <para>每次执行前调用，不跨请求缓存。</para>
    /// </summary>
    /// <param name="config">V2 规则配置</param>
    public void Initialize(RulesBasedVoucherConfigV2 config)
    {
        _config = config;
        _exactCodeIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _exactCategoryIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _orderedGroups = new List<RuleGroupV2>();
        _groupsById = new Dictionary<string, RuleGroupV2>();

        foreach (var group in config.RuleGroups)
        {
            // 构建精确编码索引
            if (group.ExactCodes is { Count: > 0 })
            {
                foreach (var code in group.ExactCodes)
                {
                    var trimmed = code?.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        // 同一编码只映射到第一个声明它的规则组
                        if (!_exactCodeIndex.ContainsKey(trimmed))
                        {
                            _exactCodeIndex[trimmed] = group.Id;
                        }
                    }
                }
            }

            // 构建精确分类索引
            if (group.ExactCategories is { Count: > 0 })
            {
                foreach (var category in group.ExactCategories)
                {
                    var trimmed = category?.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        if (!_exactCategoryIndex.ContainsKey(trimmed))
                        {
                            _exactCategoryIndex[trimmed] = group.Id;
                        }
                    }
                }
            }

            // 构建 Id 字典
            _groupsById[group.Id] = group;
        }

        // 按 Order 排序（同长度关键词冲突时按 Order 决断）
        _orderedGroups = config.RuleGroups.OrderBy(g => g.Order).ToList();
    }

    /// <summary>
    /// 对单行数据执行三层匹配，返回候选列表（按层级优先级排序）
    /// <para>
    /// 三层级联逻辑：
    /// <list type="number">
    ///   <item>Layer1: 从 _exactCodeIndex 字典 O(1) 查找精确编码</item>
    ///   <item>Layer2: 先从 _exactCategoryIndex 精确查找；未命中则遍历 CategoryKeywords 做最长优先匹配</item>
    ///   <item>Layer3: 遍历 SummaryKeywords 做最长优先匹配</item>
    /// </list>
    /// </para>
    /// <para>[F4] 候选列表去重：同一 GroupId 仅保留层级最高的条目</para>
    /// <para>所有值统一 .Trim() 处理</para>
    /// </summary>
    /// <param name="row">源数据行（字段名→字段值）</param>
    /// <returns>匹配候选列表，按 Layer 升序排列</returns>
    public List<MatchCandidate> MatchRowToRuleGroup(IDictionary<string, object> row)
    {
        var seen = new HashSet<string>(); // GroupId → 已加入
        var candidates = new List<MatchCandidate>();

        string? exactMatchField = _config.MatchingLayers.ExactMatchField;
        string? categoryField = _config.MatchingLayers.CategoryField;
        string? summaryField = _config.MatchingLayers.SummaryField;

        // ── Layer1: 精确编码匹配 O(1) ──
        if (!string.IsNullOrEmpty(exactMatchField) && TryGetStringValue(row, exactMatchField, out var codeValue))
        {
            var trimmedCode = codeValue.Trim();
            if (_exactCodeIndex.TryGetValue(trimmedCode, out var groupId))
            {
                candidates.Add(new MatchCandidate(groupId, 1));
                seen.Add(groupId);
            }
        }

        // ── Layer2: 分类精确 + 关键词最长优先 ──
        if (!string.IsNullOrEmpty(categoryField) && TryGetStringValue(row, categoryField, out var categoryValue))
        {
            var trimmedCategory = categoryValue.Trim();
            if (!string.IsNullOrEmpty(trimmedCategory))
            {
                // 2a: 精确分类匹配
                if (_exactCategoryIndex.TryGetValue(trimmedCategory, out var exactGroupId) && !seen.Contains(exactGroupId))
                {
                    candidates.Add(new MatchCandidate(exactGroupId, 2));
                    seen.Add(exactGroupId);
                }
                else
                {
                    // 2b: 分类关键词最长优先匹配
                    var keywordHit = FindLongestKeywordMatch(trimmedCategory, _orderedGroups,
                        g => g.CategoryKeywords, 2, seen);
                    if (keywordHit is not null)
                    {
                        candidates.Add(keywordHit);
                        seen.Add(keywordHit.GroupId);
                    }
                }
            }
        }

        // ── Layer3: 摘要关键词最长优先 ──
        if (!string.IsNullOrEmpty(summaryField) && TryGetStringValue(row, summaryField, out var summaryValue))
        {
            var trimmedSummary = summaryValue.Trim();
            if (!string.IsNullOrEmpty(trimmedSummary))
            {
                var keywordHit = FindLongestKeywordMatch(trimmedSummary, _orderedGroups,
                    g => g.SummaryKeywords, 3, seen);
                if (keywordHit is not null)
                {
                    candidates.Add(keywordHit);
                    seen.Add(keywordHit.GroupId);
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// [E1] Fallthrough 处理：从候选列表中选出最终规则组
    /// <para>遍历 candidates（已按 Layer 排序），对每个候选调用 HasMatchingEntryLines：</para>
    /// <list type="bullet">
    ///   <item>有匹配分录行 → 返回该 GroupId</item>
    ///   <item>无匹配 + Fallthrough=false → 返回 (GroupId, routedButNoOutput=true)</item>
    ///   <item>无匹配 + Fallthrough=true → 继续下一个候选</item>
    /// </list>
    /// </summary>
    /// <param name="row">源数据行</param>
    /// <param name="candidates">MatchRowToRuleGroup 返回的候选列表</param>
    /// <returns>(finalGroupId, routedButNoOutput) —— routedButNoOutput=true 表示已路由到组但无可用分录行</returns>
    public (string? GroupId, bool RoutedButNoOutput) ResolveFinalGroup(
        IDictionary<string, object> row, List<MatchCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!_groupsById.TryGetValue(candidate.GroupId, out var group))
                continue;

            if (HasMatchingEntryLines(row, group))
            {
                return (candidate.GroupId, false);
            }

            // 无匹配分录行：检查是否允许 Fallthrough
            if (!group.Fallthrough)
            {
                return (candidate.GroupId, true);
            }
            // Fallthrough=true → 继续下一个候选
        }

        // 所有候选都 Fallthrough 完毕仍无匹配
        return (null, false);
    }

    /// <summary>
    /// [G4] 检查规则组内是否存在至少一条可接纳当前行的分录行
    /// <para>[H8] 跨方向检查（借/贷任一方有匹配即可），仅用于 Fallthrough 判定</para>
    /// <para>检查 Status==1 的分录行中是否有：</para>
    /// <list type="bullet">
    ///   <item>ConditionField==null 的兆底行（始终匹配）</item>
    ///   <item>ConditionValues 包含当前行对应字段值的条件行</item>
    /// </list>
    /// </summary>
    /// <param name="row">源数据行</param>
    /// <param name="group">规则组</param>
    /// <returns>是否存在至少一条匹配的分录行</returns>
    public bool HasMatchingEntryLines(IDictionary<string, object> row, RuleGroupV2 group)
    {
        foreach (var line in group.Lines)
        {
            if (line.Status != 1)
                continue;

            // 兆底行：无条件字段，始终接纳
            if (string.IsNullOrEmpty(line.ConditionField))
                return true;

            // 条件行：检查当前行对应字段值是否在 ConditionValues 中
            if (line.ConditionValues is { Count: > 0 } &&
                TryGetStringValue(row, line.ConditionField, out var fieldValue))
            {
                var trimmed = fieldValue.Trim();
                if (line.ConditionValues.Any(cv =>
                        string.Equals(cv?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// [H8] 核心分录生成逻辑（SUM 模式）—— 按方向独立处理
    /// <para>将数据行分配到各分录行，返回每个分录行的聚合结果。</para>
    /// <para>分配规则：</para>
    /// <list type="number">
    ///   <item>按 Direction 分为借方组/贷方组</item>
    ///   <item>对每个方向独立调用 AssignWithinDirection，借贷各自拥有独立的 consumed 集合（复式记账：同一行同时产生借贷分录）</item>
    ///   <item>AssignWithinDirection 内部：consumed HashSet 记录该方向已分配行索引；先按 DisplayOrder 处理有 ConditionField 的条件行（优先消化）；再处理无 ConditionField 的兆底行（接纳剩余行）</item>
    /// </list>
    /// </summary>
    /// <param name="rows">同一规则组下的所有数据行</param>
    /// <param name="group">规则组</param>
    /// <returns>Dictionary&lt;LineNo, 分配到的数据行集合&gt;</returns>
    public Dictionary<int, List<IDictionary<string, object>>> AssignRowsToEntryLines(
        List<IDictionary<string, object>> rows, RuleGroupV2 group)
    {
        var result = new Dictionary<int, List<IDictionary<string, object>>>();

        // 仅处理启用的分录行
        var activeLines = group.Lines.Where(l => l.Status == 1).ToList();
        if (activeLines.Count == 0 || rows.Count == 0)
            return result;

        // 按 Direction 分组
        var debitLines = activeLines.Where(l => l.Direction == "借").OrderBy(l => l.DisplayOrder).ToList();
        var creditLines = activeLines.Where(l => l.Direction == "贷").OrderBy(l => l.DisplayOrder).ToList();

        // 借方和贷方各自独立追踪已分配行（复式记账：同一行应同时产生借贷分录）
        var debitConsumed = new HashSet<int>();
        var creditConsumed = new HashSet<int>();

        // 借方分配
        AssignWithinDirection(rows, debitLines, debitConsumed, result);

        // 贷方分配
        AssignWithinDirection(rows, creditLines, creditConsumed, result);

        return result;
    }

    /// <summary>
    /// [E5] 应用预筛选条件，返回过滤后的行集合
    /// <para>支持操作符：eq/neq/gt/gte/lt/lte/contains/notEmpty</para>
    /// <para>数值比较时尝试 decimal.TryParse</para>
    /// <para>所有条件为 AND 关系</para>
    /// </summary>
    /// <param name="allRows">待筛选的全部数据行</param>
    /// <param name="conditions">筛选条件列表（null 或空则不过滤）</param>
    /// <returns>通过所有筛选条件的数据行</returns>
    public List<IDictionary<string, object>> ApplyFilterConditions(
        List<IDictionary<string, object>> allRows, List<FilterConditionItem>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
            return allRows;

        var result = new List<IDictionary<string, object>>();

        foreach (var row in allRows)
        {
            bool pass = true;

            foreach (var cond in conditions)
            {
                if (!EvaluateCondition(row, cond))
                {
                    pass = false;
                    break;
                }
            }

            if (pass)
                result.Add(row);
        }

        return result;
    }

    // ────────────────── 私有辅助方法 ──────────────────

    /// <summary>
    /// 从数据行中安全获取字符串值（统一 .Trim() 处理）
    /// </summary>
    private static bool TryGetStringValue(IDictionary<string, object> row, string fieldName, out string value)
    {
        value = string.Empty;

        if (!row.TryGetValue(fieldName, out var raw) || raw is null)
            return false;

        value = raw.ToString()?.Trim() ?? string.Empty;
        return true;
    }

    /// <summary>
    /// 关键词最长优先匹配
    /// <para>遍历 _orderedGroups（已按 Order 排序），对每组提取关键词列表，
    /// 找出当前行字段值中包含的最长关键词，返回对应候选。</para>
    /// <para>同长度关键词按 Order 决断（由于 _orderedGroups 已排序，先遍历到的优先）。</para>
    /// </summary>
    /// <param name="haystack">待匹配的字符串（已 Trim）</param>
    /// <param name="groups">排序后的规则组列表</param>
    /// <param name="keywordSelector">从规则组提取关键词列表的委托</param>
    /// <param name="layer">当前匹配层级</param>
    /// <param name="seen">已加入的 GroupId 集合（用于去重）</param>
    /// <returns>匹配候选，或 null</returns>
    private MatchCandidate? FindLongestKeywordMatch(
        string haystack,
        List<RuleGroupV2> groups,
        Func<RuleGroupV2, List<string>?> keywordSelector,
        int layer,
        HashSet<string> seen)
    {
        int bestLength = 0;
        string? bestGroupId = null;

        foreach (var group in groups)
        {
            if (seen.Contains(group.Id))
                continue;

            var keywords = keywordSelector(group);
            if (keywords is null || keywords.Count == 0)
                continue;

            foreach (var kw in keywords)
            {
                var trimmedKw = kw?.Trim();
                if (string.IsNullOrEmpty(trimmedKw))
                    continue;

                // 关键词长度必须严格大于当前最佳，才替换（同长度保持先遍历到的，即 Order 更小的）
                if (trimmedKw.Length > bestLength &&
                    haystack.IndexOf(trimmedKw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bestLength = trimmedKw.Length;
                    bestGroupId = group.Id;
                }
            }
        }

        if (bestGroupId is not null)
            return new MatchCandidate(bestGroupId, layer);

        return null;
    }

    /// <summary>
    /// 单方向内分配数据行到各分录行
    /// <para>
    /// 分配策略：
    /// <list type="number">
    ///   <item>先按 DisplayOrder 处理有 ConditionField 的条件行（条件行优先消化）</item>
    ///   <item>再处理无 ConditionField 的兆底行（接纳剩余行）</item>
    /// </list>
    /// 已分配行记录在 consumed 中，保证一行不被同一方向的多个分录行重复分配。
    /// </para>
    /// </summary>
    /// <param name="rows">全部数据行</param>
    /// <param name="directionLines">同一方向的分录行列表（已按 DisplayOrder 排序）</param>
    /// <param name="consumed">跨方向共享的已分配行索引集合</param>
    /// <param name="result">累积分录分配结果</param>
    private void AssignWithinDirection(
        List<IDictionary<string, object>> rows,
        List<EntryLineV2> directionLines,
        HashSet<int> consumed,
        Dictionary<int, List<IDictionary<string, object>>> result)
    {
        if (directionLines.Count == 0)
            return;

        // 分为条件行和兆底行
        var conditionalLines = directionLines.Where(l => !string.IsNullOrEmpty(l.ConditionField)).ToList();
        var catchallLines = directionLines.Where(l => string.IsNullOrEmpty(l.ConditionField)).ToList();

        // ── 第一轮：条件行优先消化 ──
        foreach (var line in conditionalLines)
        {
            var assigned = new List<IDictionary<string, object>>();

            for (int i = 0; i < rows.Count; i++)
            {
                if (consumed.Contains(i))
                    continue;

                var row = rows[i];

                // 检查条件是否匹配
                if (line.ConditionValues is { Count: > 0 } &&
                    TryGetStringValue(row, line.ConditionField!, out var fieldValue))
                {
                    var trimmed = fieldValue.Trim();
                    if (line.ConditionValues.Any(cv =>
                            string.Equals(cv?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
                    {
                        assigned.Add(row);
                        consumed.Add(i);
                    }
                }
            }

            result[line.LineNo] = assigned;
        }

        // ── 第二轮：兆底行接纳剩余行 ──
        // 多个兆底行时，按 DisplayOrder 依次分配剩余行
        // 通常一个方向只有一个兆底行，但逻辑上支持多个
        var remainingIndices = Enumerable.Range(0, rows.Count)
            .Where(i => !consumed.Contains(i))
            .ToList();

        foreach (var line in catchallLines)
        {
            var assigned = new List<IDictionary<string, object>>();

            // 每个兆底行接纳当前所有剩余行（兆底行之间不做互斥，通常只有一个）
            foreach (var idx in remainingIndices)
            {
                if (!consumed.Contains(idx))
                {
                    assigned.Add(rows[idx]);
                    consumed.Add(idx);
                }
            }

            result[line.LineNo] = assigned;
        }
    }

    /// <summary>
    /// 评估单条筛选条件
    /// </summary>
    /// <param name="row">数据行</param>
    /// <param name="cond">筛选条件</param>
    /// <returns>是否满足条件</returns>
    private static bool EvaluateCondition(IDictionary<string, object> row, FilterConditionItem cond)
    {
        var field = cond.Field?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(field))
            return true;

        // notEmpty 操作符：字段非空即满足
        if (string.Equals(cond.Operator, "notEmpty", StringComparison.OrdinalIgnoreCase))
        {
            if (!row.TryGetValue(field, out var raw) || raw is null)
                return false;

            var strVal = raw.ToString()?.Trim();
            return !string.IsNullOrEmpty(strVal);
        }

        // 获取字段原始值
        if (!row.TryGetValue(field, out var rawValue) || rawValue is null)
            return false;

        var strValue = rawValue.ToString()?.Trim() ?? string.Empty;
        var compareValue = cond.Value?.Trim() ?? string.Empty;

        // 尝试数值比较
        decimal numCompare = 0m;
        bool bothDecimal = decimal.TryParse(strValue, out var numValue) &&
                           decimal.TryParse(compareValue, out numCompare);

        return cond.Operator?.ToLowerInvariant() switch
        {
            "eq" => string.Equals(strValue, compareValue, StringComparison.OrdinalIgnoreCase) ||
                    (bothDecimal && numValue == numCompare),
            "neq" => !string.Equals(strValue, compareValue, StringComparison.OrdinalIgnoreCase) &&
                     (!bothDecimal || numValue != numCompare),
            "gt" => bothDecimal && numValue > numCompare,
            "gte" => bothDecimal && numValue >= numCompare,
            "lt" => bothDecimal && numValue < numCompare,
            "lte" => bothDecimal && numValue <= numCompare,
            "contains" => strValue.IndexOf(compareValue, StringComparison.OrdinalIgnoreCase) >= 0,
            _ => true // 未知操作符默认通过
        };
    }
}
