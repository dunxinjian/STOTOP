using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;

namespace STOTOP.Module.CardFlow.Services.Classification;

public class ClassificationEngine
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<ClassificationEngine> _logger;

    /// <summary>Severity 优先级映射（值越大越严重）</summary>
    private static readonly Dictionary<string, int> SeverityOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Info"] = 0,
        ["Warning"] = 1,
        ["Error"] = 2,
        ["Critical"] = 3
    };

    public ClassificationEngine(STOTOPDbContext context, ILogger<ClassificationEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>对导入完成事件执行分类分析</summary>
    public async Task<ClassificationResult> AnalyzeAsync(ImportCompletedEvent evt, CancellationToken ct = default)
    {
        var result = new ClassificationResult
        {
            BatchId = evt.BatchId,
            TargetTable = evt.TargetTable
        };

        // 1. 查询适用规则：FStatus=1，FTriggerEvent=PipelineCompleted，按 FPriority 排序
        var allRules = await _context.Set<CfDispatchRule>()
            .Where(r => r.FStatus == 1 && r.FTriggerEvent == "PipelineCompleted")
            .OrderBy(r => r.FPriority)
            .ToListAsync(ct);

        var applicableRules = allRules.Where(r =>
        {
            if (string.IsNullOrWhiteSpace(r.FTargetTables)) return true; // 全局规则
            try
            {
                var tables = JsonSerializer.Deserialize<List<string>>(r.FTargetTables);
                return tables != null && tables.Contains(evt.TargetTable, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }).ToList();

        _logger.LogInformation("批次 {BatchId} 表 {Table}: 找到 {Count} 条适用派发规则",
            evt.BatchId, evt.TargetTable, applicableRules.Count);

        // 2. 遍历规则执行
        var items = new List<ClassificationItem>();

        foreach (var rule in applicableRules)
        {
            try
            {
                switch (rule.FRuleType)
                {
                    case "AlwaysMatch":
                        // 满足暂存表匹配就直接命中，无需条件
                        var alwaysRowCount = await GetBatchRowCountAsync(evt.TargetTable, evt.BatchId, ct);
                        items.Add(new ClassificationItem
                        {
                            DispatchRuleId = rule.FID,
                            Type = rule.FRuleName,
                            Severity = rule.FSeverity,
                            AffectedRowIds = new List<long>(),
                            AffectedRowCount = alwaysRowCount,
                            RuleId = rule.FID,
                            RuleName = rule.FRuleName,
                            Context = new Dictionary<string, object>
                            {
                                ["ruleType"] = "AlwaysMatch"
                            }
                        });
                        break;

                    case "RowLevel":
                        var rowIds = await ExecuteRowLevelRuleAsync(rule, evt.TargetTable, evt.BatchId, ct);
                        if (rowIds.Count > 0)
                        {
                            items.Add(new ClassificationItem
                            {
                                DispatchRuleId = rule.FID,
                                Type = rule.FRuleName,
                                Severity = rule.FSeverity,
                                AffectedRowIds = rowIds,
                                AffectedRowCount = rowIds.Count,
                                RuleId = rule.FID,
                                RuleName = rule.FRuleName,
                                Context = new Dictionary<string, object>
                                {
                                    ["ruleType"] = "RowLevel",
                                    ["conditionJson"] = rule.FConditionJson ?? ""
                                }
                            });
                        }
                        break;

                    case "BatchAggregate":
                        var batchItem = await ExecuteBatchAggregateRuleAsync(rule, evt.TargetTable, evt.BatchId, ct);
                        if (batchItem != null)
                            items.Add(batchItem);
                        break;

                    case "HistoryCompare":
                        var historyItem = await ExecuteHistoryCompareRuleAsync(rule, evt.TargetTable, evt.BatchId, ct);
                        if (historyItem != null)
                            items.Add(historyItem);
                        break;

                    default:
                        _logger.LogWarning("规则 {RuleId} ({RuleName}): 未知规则类型 {RuleType}",
                            rule.FID, rule.FRuleName, rule.FRuleType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行分类规则 {RuleId} ({RuleName}) 失败", rule.FID, rule.FRuleName);
            }
        }

        // 3. 汇总结果：按 DispatchRuleId 分组，合并命中行ID（取并集），取最高 Severity
        var grouped = items.GroupBy(i => i.DispatchRuleId).Select(g =>
        {
            var mergedIds = g.SelectMany(i => i.AffectedRowIds).Distinct().OrderBy(id => id).ToList();
            var highestSeverity = g
                .Select(i => i.Severity)
                .OrderByDescending(s => SeverityOrder.GetValueOrDefault(s, 0))
                .First();

            var first = g.First();
            return new ClassificationItem
            {
                DispatchRuleId = g.Key,
                Type = first.Type,
                Severity = highestSeverity,
                AffectedRowIds = mergedIds,
                AffectedRowCount = mergedIds.Count,
                RuleId = first.RuleId,
                RuleName = string.Join(", ", g.Select(i => i.RuleName).Distinct()),
                Context = new Dictionary<string, object>
                {
                    ["mergedFromRules"] = g.Select(i => i.RuleId).Distinct().ToList(),
                    ["originalItemCount"] = g.Count()
                }
            };
        }).ToList();

        result.Items = grouped;

        // 4. 持久化：将每个 ClassificationItem 写入 DC派发结果 表
        foreach (var item in grouped)
        {
            var entity = new CfSystemDispatchResult
            {
                FBatchId = evt.BatchId,
                FStagingTable = evt.TargetTable,
                FDispatchRuleId = item.DispatchRuleId,
                FRuleName = item.RuleName,
                FSeverity = item.Severity,
                FAffectedRowCount = item.AffectedRowCount,
                FAffectedRowIds = JsonSerializer.Serialize(item.AffectedRowIds),
                FProcessingStatus = 0,
                FContext = JsonSerializer.Serialize(item.Context),
                FCreatedTime = DateTime.Now
            };
            _context.Set<CfSystemDispatchResult>().Add(entity);
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("批次 {BatchId}: 分类分析完成，产生 {Count} 条派发结果",
            evt.BatchId, grouped.Count);

        return result;
    }

    /// <summary>执行单条规则的测试运行（不持久化）</summary>
    public async Task<List<ClassificationItem>> TestRunRuleAsync(
        CfDispatchRule rule, long batchId, string targetTable, CancellationToken ct = default)
    {
        var items = new List<ClassificationItem>();

        if (rule.FRuleType == "AlwaysMatch")
        {
            var rowCount = await GetBatchRowCountAsync(targetTable, batchId, ct);
            items.Add(new ClassificationItem
            {
                DispatchRuleId = rule.FID,
                Type = rule.FRuleName,
                Severity = rule.FSeverity,
                AffectedRowIds = new List<long>(),
                AffectedRowCount = rowCount,
                RuleId = rule.FID,
                RuleName = rule.FRuleName
            });
        }
        else if (rule.FRuleType == "RowLevel")
        {
            var rowIds = await ExecuteRowLevelRuleAsync(rule, targetTable, batchId, ct);
            if (rowIds.Count > 0)
            {
                items.Add(new ClassificationItem
                {
                    DispatchRuleId = rule.FID,
                    Type = rule.FRuleName,
                    Severity = rule.FSeverity,
                    AffectedRowIds = rowIds,
                    AffectedRowCount = rowIds.Count,
                    RuleId = rule.FID,
                    RuleName = rule.FRuleName
                });
            }
        }
        else if (rule.FRuleType == "BatchAggregate")
        {
            var batchItem = await ExecuteBatchAggregateRuleAsync(rule, targetTable, batchId, ct);
            if (batchItem != null)
                items.Add(batchItem);
        }
        else if (rule.FRuleType == "HistoryCompare")
        {
            var historyItem = await ExecuteHistoryCompareRuleAsync(rule, targetTable, batchId, ct);
            if (historyItem != null)
                items.Add(historyItem);
        }
        else
        {
            _logger.LogInformation("测试运行: 规则类型 {RuleType} 暂未实现", rule.FRuleType);
        }

        return items;
    }

    /// <summary>执行行级规则：解析条件JSON，构建动态SQL WHERE子句，查询命中行FID列表</summary>
    private async Task<List<long>> ExecuteRowLevelRuleAsync(
        CfDispatchRule rule, string targetTable, long batchId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rule.FConditionJson))
        {
            _logger.LogWarning("规则 {RuleId} 的条件JSON为空，跳过", rule.FID);
            return new List<long>();
        }

        // 表名安全验证
        if (!ValidateTableName(targetTable))
            throw new ArgumentException($"不安全的表名: {targetTable}");

        // 解析条件JSON
        var conditionDoc = JsonSerializer.Deserialize<JsonElement>(rule.FConditionJson);

        var parameters = new List<SqlParameter>();
        var whereClause = BuildWhereClause(conditionDoc, parameters);

        if (string.IsNullOrWhiteSpace(whereClause))
        {
            _logger.LogWarning("规则 {RuleId} 条件解析结果为空，跳过", rule.FID);
            return new List<long>();
        }

        // 构建完整SQL
        var sql = $"SELECT [FID] FROM [{targetTable}] WHERE [F批次ID] = @batchId AND ({whereClause})";
        parameters.Add(new SqlParameter("@batchId", batchId));

        _logger.LogDebug("规则 {RuleId} 生成SQL: {Sql}", rule.FID, sql);

        // 执行查询
        var rowIds = new List<long>();
        var conn = _context.Database.GetDbConnection();
        try
        {
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;

            foreach (var p in parameters)
            {
                var dbParam = cmd.CreateParameter();
                dbParam.ParameterName = p.ParameterName;
                dbParam.Value = p.Value ?? DBNull.Value;
                cmd.Parameters.Add(dbParam);
            }

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rowIds.Add(reader.GetInt64(0));
            }
        }
        finally
        {
            if (_context.Database.GetDbConnection() == conn && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }

        _logger.LogInformation("规则 {RuleId} ({RuleName}) 在表 {Table} 批次 {BatchId} 命中 {Count} 行",
            rule.FID, rule.FRuleName, targetTable, batchId, rowIds.Count);

        return rowIds;
    }

    // ==================== 聚合函数/操作符白名单 ====================

    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUM", "COUNT", "AVG", "MAX", "MIN", "COUNT_DISTINCT"
    };

    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "!=", ">", "<", ">=", "<="
    };

    /// <summary>验证字段名安全性</summary>
    private static bool ValidateFieldName(string field)
    {
        return !string.IsNullOrWhiteSpace(field) &&
               (field == "*" || Regex.IsMatch(field, @"^[\u4e00-\u9fa5A-Za-z0-9_]+$"));
    }

    /// <summary>构建聚合表达式（带白名单验证）</summary>
    private static string BuildAggregateExpression(string function, string field)
    {
        if (!AllowedFunctions.Contains(function))
            throw new ArgumentException($"不支持的聚合函数: {function}");
        if (!ValidateFieldName(field))
            throw new ArgumentException($"不安全的字段名: {field}");

        if (function.Equals("COUNT_DISTINCT", StringComparison.OrdinalIgnoreCase))
            return $"COUNT(DISTINCT [{field}])";
        if (field == "*")
            return $"{function.ToUpper()}(*)";
        return $"{function.ToUpper()}([{field}])";
    }

    /// <summary>执行标量SQL查询，返回 decimal? 值</summary>
    private async Task<decimal?> ExecuteScalarDecimalAsync(string sql, List<SqlParameter> parameters, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        try
        {
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;

            foreach (var p in parameters)
            {
                var dbParam = cmd.CreateParameter();
                dbParam.ParameterName = p.ParameterName;
                dbParam.Value = p.Value ?? DBNull.Value;
                cmd.Parameters.Add(dbParam);
            }

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null || result == DBNull.Value)
                return null;
            return Convert.ToDecimal(result);
        }
        finally
        {
            if (_context.Database.GetDbConnection() == conn && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>获取批次总行数</summary>
    private async Task<int> GetBatchRowCountAsync(string tableName, long batchId, CancellationToken ct)
    {
        var sql = $"SELECT COUNT(*) FROM [{tableName}] WHERE [F批次ID] = @batchId";
        var parameters = new List<SqlParameter> { new("@batchId", batchId) };
        var result = await ExecuteScalarDecimalAsync(sql, parameters, ct);
        return (int)(result ?? 0);
    }

    /// <summary>执行 BatchAggregate 类型规则</summary>
    private async Task<ClassificationItem?> ExecuteBatchAggregateRuleAsync(
        CfDispatchRule rule, string targetTable, long batchId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rule.FConditionJson))
        {
            _logger.LogWarning("规则 {RuleId} 的条件JSON为空，跳过", rule.FID);
            return null;
        }

        if (!ValidateTableName(targetTable))
            throw new ArgumentException($"不安全的表名: {targetTable}");

        var doc = JsonSerializer.Deserialize<JsonElement>(rule.FConditionJson);

        // 解析 aggregate
        if (!doc.TryGetProperty("aggregate", out var aggProp))
            throw new ArgumentException("BatchAggregate 规则缺少 aggregate 配置");

        var aggFunc = aggProp.GetProperty("function").GetString() ?? "";
        var aggField = aggProp.GetProperty("field").GetString() ?? "";
        var aggExpr = BuildAggregateExpression(aggFunc, aggField);

        // 计算当前批次的聚合值
        var aggSql = $"SELECT {aggExpr} FROM [{targetTable}] WHERE [F批次ID] = @batchId";
        var aggParams = new List<SqlParameter> { new("@batchId", batchId) };
        var aggValue = await ExecuteScalarDecimalAsync(aggSql, aggParams, ct);

        _logger.LogDebug("规则 {RuleId} BatchAggregate: {Func}({Field}) = {Value}",
            rule.FID, aggFunc, aggField, aggValue);

        // 解析 compare
        if (!doc.TryGetProperty("compare", out var compareProp))
            throw new ArgumentException("BatchAggregate 规则缺少 compare 配置");

        var compareOp = compareProp.GetProperty("operator").GetString() ?? "";
        if (!AllowedOperators.Contains(compareOp))
            throw new ArgumentException($"不支持的比较操作符: {compareOp}");

        decimal? compareValue;

        if (compareProp.TryGetProperty("target", out var targetProp))
        {
            // 与另一个聚合值比较
            var targetFunc = targetProp.GetProperty("function").GetString() ?? "";
            var targetField = targetProp.GetProperty("field").GetString() ?? "";
            var targetExpr = BuildAggregateExpression(targetFunc, targetField);

            var targetSql = $"SELECT {targetExpr} FROM [{targetTable}] WHERE [F批次ID] = @batchId";
            var targetParams = new List<SqlParameter> { new("@batchId", batchId) };
            compareValue = await ExecuteScalarDecimalAsync(targetSql, targetParams, ct);
        }
        else if (compareProp.TryGetProperty("value", out var valueProp))
        {
            // 与固定值比较
            compareValue = valueProp.ValueKind == JsonValueKind.Number ? valueProp.GetDecimal() : null;
        }
        else
        {
            throw new ArgumentException("BatchAggregate 规则 compare 缺少 target 或 value");
        }

        // 比较
        var conditionMet = EvaluateComparison(aggValue ?? 0, compareOp, compareValue ?? 0);

        _logger.LogInformation("规则 {RuleId} ({RuleName}) BatchAggregate: {AggValue} {Op} {CompareValue} => {Result}",
            rule.FID, rule.FRuleName, aggValue, compareOp, compareValue, conditionMet);

        if (!conditionMet)
            return null;

        var batchRowCount = await GetBatchRowCountAsync(targetTable, batchId, ct);

        return new ClassificationItem
        {
            DispatchRuleId = rule.FID,
            Type = rule.FRuleName,
            Severity = rule.FSeverity,
            AffectedRowIds = new List<long>(),
            AffectedRowCount = batchRowCount,
            RuleId = rule.FID,
            RuleName = rule.FRuleName,
            Context = new Dictionary<string, object>
            {
                ["ruleType"] = "BatchAggregate",
                ["aggregateFunction"] = aggFunc,
                ["aggregateField"] = aggField,
                ["aggregateValue"] = aggValue ?? 0m,
                ["compareOperator"] = compareOp,
                ["compareValue"] = compareValue ?? 0m
            }
        };
    }

    /// <summary>执行 HistoryCompare 类型规则</summary>
    private async Task<ClassificationItem?> ExecuteHistoryCompareRuleAsync(
        CfDispatchRule rule, string targetTable, long batchId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rule.FConditionJson))
        {
            _logger.LogWarning("规则 {RuleId} 的条件JSON为空，跳过", rule.FID);
            return null;
        }

        if (!ValidateTableName(targetTable))
            throw new ArgumentException($"不安全的表名: {targetTable}");

        var doc = JsonSerializer.Deserialize<JsonElement>(rule.FConditionJson);

        // 解析 currentBatch
        if (!doc.TryGetProperty("currentBatch", out var currentProp))
            throw new ArgumentException("HistoryCompare 规则缺少 currentBatch 配置");

        var currentFunc = currentProp.GetProperty("function").GetString() ?? "";
        var currentField = currentProp.GetProperty("field").GetString() ?? "";
        var currentExpr = BuildAggregateExpression(currentFunc, currentField);

        // 计算当前批次聚合值
        var currentSql = $"SELECT {currentExpr} FROM [{targetTable}] WHERE [F批次ID] = @batchId";
        var currentParams = new List<SqlParameter> { new("@batchId", batchId) };
        var currentValue = await ExecuteScalarDecimalAsync(currentSql, currentParams, ct);

        // 解析 history
        if (!doc.TryGetProperty("history", out var historyProp))
            throw new ArgumentException("HistoryCompare 规则缺少 history 配置");

        var historyFunc = historyProp.GetProperty("function").GetString() ?? "";
        var historyField = historyProp.GetProperty("field").GetString() ?? "";
        var historyExpr = BuildAggregateExpression(historyFunc, historyField);
        var scope = historyProp.GetProperty("scope").GetString() ?? "ALL";

        // 构建历史查询SQL
        var historySql = $"SELECT {historyExpr} FROM [{targetTable}] WHERE [F批次ID] != @batchId";
        var historyParams = new List<SqlParameter> { new("@batchId", batchId) };

        var scopeDays = scope.ToUpper() switch
        {
            "LAST_7_DAYS" => 7,
            "LAST_30_DAYS" => 30,
            "LAST_90_DAYS" => 90,
            "ALL" => 0,
            _ => throw new ArgumentException($"不支持的 scope: {scope}")
        };

        if (scopeDays > 0)
        {
            historySql += $" AND [F创建时间] >= DATEADD(DAY, -{scopeDays}, GETDATE())";
        }

        var historyValue = await ExecuteScalarDecimalAsync(historySql, historyParams, ct);

        _logger.LogDebug("规则 {RuleId} HistoryCompare: current={Current}, history={History}, scope={Scope}",
            rule.FID, currentValue, historyValue, scope);

        // 计算偏差百分比
        decimal deviationPercent = 0;
        if (historyValue.HasValue && historyValue.Value != 0 && currentValue.HasValue)
        {
            deviationPercent = Math.Abs(currentValue.Value - historyValue.Value) / Math.Abs(historyValue.Value) * 100;
        }

        // 解析 deviation
        if (!doc.TryGetProperty("deviation", out var deviationProp))
            throw new ArgumentException("HistoryCompare 规则缺少 deviation 配置");

        var devOp = deviationProp.GetProperty("operator").GetString() ?? "";
        if (!AllowedOperators.Contains(devOp))
            throw new ArgumentException($"不支持的比较操作符: {devOp}");

        var devPercent = deviationProp.GetProperty("percent").GetDecimal();

        var conditionMet = EvaluateComparison(deviationPercent, devOp, devPercent);

        _logger.LogInformation(
            "规则 {RuleId} ({RuleName}) HistoryCompare: deviation {DevPct:F2}% {Op} {Threshold}% => {Result}",
            rule.FID, rule.FRuleName, deviationPercent, devOp, devPercent, conditionMet);

        if (!conditionMet)
            return null;

        var batchRowCount = await GetBatchRowCountAsync(targetTable, batchId, ct);

        return new ClassificationItem
        {
            DispatchRuleId = rule.FID,
            Type = rule.FRuleName,
            Severity = rule.FSeverity,
            AffectedRowIds = new List<long>(),
            AffectedRowCount = batchRowCount,
            RuleId = rule.FID,
            RuleName = rule.FRuleName,
            Context = new Dictionary<string, object>
            {
                ["ruleType"] = "HistoryCompare",
                ["currentValue"] = currentValue ?? 0m,
                ["historyValue"] = historyValue ?? 0m,
                ["deviationPercent"] = Math.Round(deviationPercent, 2),
                ["scope"] = scope,
                ["thresholdOperator"] = devOp,
                ["thresholdPercent"] = devPercent
            }
        };
    }

    /// <summary>通用比较运算</summary>
    private static bool EvaluateComparison(decimal left, string op, decimal right)
    {
        return op switch
        {
            "=" => left == right,
            "!=" => left != right,
            ">" => left > right,
            "<" => left < right,
            ">=" => left >= right,
            "<=" => left <= right,
            _ => throw new ArgumentException($"不支持的操作符: {op}")
        };
    }

    /// <summary>验证表名必须以 STG 开头且仅包含安全字符</summary>
    private static bool ValidateTableName(string tableName)
    {
        return !string.IsNullOrWhiteSpace(tableName) &&
               Regex.IsMatch(tableName, @"^STG[\u4e00-\u9fa5A-Za-z0-9_]+$");
    }

    /// <summary>递归构建WHERE子句</summary>
    private string BuildWhereClause(JsonElement element, List<SqlParameter> parameters)
    {
        if (element.TryGetProperty("logic", out var logicProp) &&
            element.TryGetProperty("conditions", out var conditionsProp) &&
            conditionsProp.ValueKind == JsonValueKind.Array)
        {
            var logic = logicProp.GetString()?.ToUpper() == "OR" ? " OR " : " AND ";
            var parts = new List<string>();

            foreach (var cond in conditionsProp.EnumerateArray())
            {
                var part = BuildWhereClause(cond, parameters);
                if (!string.IsNullOrWhiteSpace(part))
                    parts.Add(part);
            }

            return parts.Count > 0 ? $"({string.Join(logic, parts)})" : "";
        }

        // 单个条件
        if (element.TryGetProperty("field", out var fieldProp) &&
            element.TryGetProperty("operator", out var operatorProp))
        {
            var field = fieldProp.GetString();
            var op = operatorProp.GetString();

            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op))
                return "";

            // 字段名安全验证
            if (!Regex.IsMatch(field, @"^[\u4e00-\u9fa5A-Za-z0-9_]+$"))
                throw new ArgumentException($"不安全的字段名: {field}");

            var paramIndex = parameters.Count;
            var paramName = $"@p{paramIndex}";

            return op switch
            {
                "=" => BuildSimpleCondition(field, "=", paramName, element, parameters),
                "!=" => BuildSimpleCondition(field, "<>", paramName, element, parameters),
                ">" => BuildSimpleCondition(field, ">", paramName, element, parameters),
                "<" => BuildSimpleCondition(field, "<", paramName, element, parameters),
                ">=" => BuildSimpleCondition(field, ">=", paramName, element, parameters),
                "<=" => BuildSimpleCondition(field, "<=", paramName, element, parameters),
                "contains" => BuildLikeCondition(field, paramName, "'%' + {0} + '%'", element, parameters),
                "startsWith" => BuildLikeCondition(field, paramName, "{0} + '%'", element, parameters),
                "endsWith" => BuildLikeCondition(field, paramName, "'%' + {0}", element, parameters),
                "isNull" => $"[{field}] IS NULL",
                "isNotNull" => $"[{field}] IS NOT NULL",
                "in" => BuildInCondition(field, paramName, element, parameters),
                "between" => BuildBetweenCondition(field, paramIndex, element, parameters),
                _ => throw new ArgumentException($"不支持的操作符: {op}")
            };
        }

        return "";
    }

    private static string BuildSimpleCondition(string field, string sqlOp, string paramName,
        JsonElement element, List<SqlParameter> parameters)
    {
        var value = GetConditionValue(element);
        parameters.Add(new SqlParameter(paramName, value ?? DBNull.Value));
        return $"[{field}] {sqlOp} {paramName}";
    }

    private static string BuildLikeCondition(string field, string paramName, string pattern,
        JsonElement element, List<SqlParameter> parameters)
    {
        var value = GetConditionValue(element);
        parameters.Add(new SqlParameter(paramName, value ?? DBNull.Value));
        return $"[{field}] LIKE {string.Format(pattern, paramName)}";
    }

    private static string BuildInCondition(string field, string paramName,
        JsonElement element, List<SqlParameter> parameters)
    {
        var value = GetConditionValue(element);
        parameters.Add(new SqlParameter(paramName, value?.ToString() ?? ""));
        return $"[{field}] IN (SELECT value FROM STRING_SPLIT({paramName}, ','))";
    }

    private static string BuildBetweenCondition(string field, int paramIndex,
        JsonElement element, List<SqlParameter> parameters)
    {
        var paramName0 = $"@p{paramIndex}";
        var paramName1 = $"@p{paramIndex + 1}";

        object? value1 = null, value2 = null;

        if (element.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.Array)
        {
            var arr = valProp.EnumerateArray().ToList();
            if (arr.Count >= 2)
            {
                value1 = GetJsonValue(arr[0]);
                value2 = GetJsonValue(arr[1]);
            }
        }

        parameters.Add(new SqlParameter(paramName0, value1 ?? DBNull.Value));
        parameters.Add(new SqlParameter(paramName1, value2 ?? DBNull.Value));
        return $"[{field}] BETWEEN {paramName0} AND {paramName1}";
    }

    private static object? GetConditionValue(JsonElement element)
    {
        if (element.TryGetProperty("value", out var valProp))
            return GetJsonValue(valProp);
        return null;
    }

    private static object? GetJsonValue(JsonElement val)
    {
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString(),
            JsonValueKind.Number => val.TryGetInt64(out var l) ? l : val.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => val.GetRawText()
        };
    }
}
