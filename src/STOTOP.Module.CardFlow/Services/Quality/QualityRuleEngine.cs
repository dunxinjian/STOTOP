using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Quality;

/// <summary>可配置质量规则执行引擎</summary>
public class QualityRuleEngine : IQualityRuleEngine
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<QualityRuleEngine> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public QualityRuleEngine(STOTOPDbContext dbContext, ILogger<QualityRuleEngine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<List<QualityRuleDefinition>> LoadRulesAsync(string? targetTable, long orgId)
    {
        var rules = await _dbContext.Set<CfQualityRule>()
            .Where(r => r.FEnabled
                && (r.FTargetTable == targetTable || r.FTargetTable == null)
                && (r.FOrgId == orgId || r.FOrgId == 0))
            .OrderBy(r => r.FSortOrder)
            .ToListAsync();

        return rules.Select(r => new QualityRuleDefinition
        {
            Id = r.FID,
            RuleCode = r.FRuleCode,
            RuleName = r.FRuleName,
            RuleLevel = r.FRuleLevel,
            CheckType = r.FCheckType,
            TargetField = r.FTargetField,
            Parameters = DeserializeParameters(r.FRuleParamsJson),
            ErrorTypeCode = r.FErrorCode,
            Severity = r.FSeverityLevel,
            QualityDimension = r.FQualityDimension,
            MessageTemplate = r.FErrorMessageTemplate,
            SuggestedFix = r.FSuggestedFix,
            IsBlocking = r.FIsBlocking,
            Sort = r.FSortOrder
        }).ToList();
    }

    /// <inheritdoc/>
    public List<QualityViolation> ValidateRow(Dictionary<string, object?> row, int rowIndex, List<QualityRuleDefinition> rules)
    {
        var violations = new List<QualityViolation>();

        foreach (var rule in rules)
        {
            // 仅处理 Field / Row 级别规则
            if (rule.RuleLevel == "Batch") continue;

            if (rule.RuleLevel == "Field")
            {
                if (string.IsNullOrEmpty(rule.TargetField)) continue;
                row.TryGetValue(rule.TargetField, out var value);
                var error = ValidateField(value, rule);
                if (error != null)
                {
                    violations.Add(CreateViolation(rule, rowIndex, value?.ToString(), error));
                }
            }
            else if (rule.RuleLevel == "Row")
            {
                // Row 级规则：Expression 类型，可引用多个字段
                var error = ValidateExpression(row, rule);
                if (error != null)
                {
                    violations.Add(CreateViolation(rule, rowIndex, null, error));
                }
            }
        }

        return violations;
    }

    /// <inheritdoc/>
    public async Task<List<QualityViolation>> ValidateBatchAsync(
        string targetTable, long batchId, List<QualityRuleDefinition> rules, STOTOPDbContext dbContext)
    {
        var violations = new List<QualityViolation>();
        var batchRules = rules.Where(r => r.RuleLevel == "Batch" && r.CheckType == "SqlCondition").ToList();

        foreach (var rule in batchRules)
        {
            var sqlCondition = rule.Parameters?.GetValueOrDefault("sqlCondition")?.ToString();
            if (string.IsNullOrWhiteSpace(sqlCondition)) continue;

            try
            {
                // 查询不满足条件的行（违规行）
                var sql = $"SELECT [FID] FROM [{targetTable}] WHERE [F批次ID] = @batchId AND NOT ({sqlCondition})";
                var batchIdParam = new SqlParameter("@batchId", batchId);

                var violatingIds = new List<long>();
                var conn = dbContext.Database.GetDbConnection();
                await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(batchIdParam);
                    if (dbContext.Database.CurrentTransaction != null)
                        cmd.Transaction = dbContext.Database.CurrentTransaction.GetDbTransaction();

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        violatingIds.Add(reader.GetInt64(0));
                    }
                }
                finally
                {
                    if (conn.State == ConnectionState.Open)
                        await conn.CloseAsync();
                }

                foreach (var id in violatingIds)
                {
                    violations.Add(new QualityViolation
                    {
                        RuleId = rule.Id,
                        RuleCode = rule.RuleCode,
                        ErrorTypeCode = rule.ErrorTypeCode,
                        Severity = rule.Severity,
                        QualityDimension = rule.QualityDimension,
                        TargetField = rule.TargetField,
                        ErrorMessage = RenderMessage(rule, null, null),
                        SuggestedFix = rule.SuggestedFix,
                        IsBlocking = rule.IsBlocking,
                        RowIndex = -1,
                        StagingId = id
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch规则 {RuleCode} 执行SQL异常, 目标表={Table}, 批次={BatchId}",
                    rule.RuleCode, targetTable, batchId);
            }
        }

        return violations;
    }

    #region 私有方法

    private string? ValidateField(object? value, QualityRuleDefinition rule)
    {
        return rule.CheckType switch
        {
            "NotNull" => ValidateNotNull(value, rule),
            "Format" => ValidateFormat(value, rule),
            "Range" => ValidateRange(value, rule),
            "Expression" => null, // Field级的Expression不在此处理
            "SqlCondition" => null, // 仅Batch级
            _ => null
        };
    }

    private string? ValidateNotNull(object? value, QualityRuleDefinition rule)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return RenderMessage(rule, value?.ToString(), rule.TargetField);
        }
        return null;
    }

    private string? ValidateFormat(object? value, QualityRuleDefinition rule)
    {
        // 空值由 NotNull 规则检查，Format 规则仅在有值时校验格式
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return null;

        var strValue = value.ToString()!;
        var format = rule.Parameters?.GetValueOrDefault("format")?.ToString() ?? "regex";

        switch (format.ToLower())
        {
            case "decimal":
                if (!decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    return RenderMessage(rule, strValue, rule.TargetField);
                break;

            case "date":
                var patterns = GetDatePatterns(rule.Parameters);
                if (!DateTime.TryParseExact(strValue, patterns, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return RenderMessage(rule, strValue, rule.TargetField);
                break;

            case "regex":
                var pattern = rule.Parameters?.GetValueOrDefault("pattern")?.ToString();
                if (!string.IsNullOrEmpty(pattern) && !Regex.IsMatch(strValue, pattern))
                    return RenderMessage(rule, strValue, rule.TargetField);
                break;

            default:
                // 未知格式类型，跳过
                break;
        }

        return null;
    }

    private string? ValidateRange(object? value, QualityRuleDefinition rule)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return null;

        if (!decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numValue))
            return RenderMessage(rule, value.ToString(), rule.TargetField);

        var minObj = rule.Parameters?.GetValueOrDefault("min");
        var maxObj = rule.Parameters?.GetValueOrDefault("max");

        if (minObj != null && decimal.TryParse(minObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var min))
        {
            if (numValue < min)
                return RenderMessage(rule, value.ToString(), rule.TargetField);
        }

        if (maxObj != null && decimal.TryParse(maxObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var max))
        {
            if (numValue > max)
                return RenderMessage(rule, value.ToString(), rule.TargetField);
        }

        return null;
    }

    /// <summary>
    /// Row级表达式验证，支持简单的 [字段] op 值 格式，以及 AND/OR 连接
    /// </summary>
    private string? ValidateExpression(Dictionary<string, object?> row, QualityRuleDefinition rule)
    {
        var expression = rule.Parameters?.GetValueOrDefault("expression")?.ToString();
        if (string.IsNullOrWhiteSpace(expression)) return null;

        try
        {
            var result = EvaluateExpression(expression, row);
            if (!result)
                return RenderMessage(rule, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Expression规则 {RuleCode} 解析失败: {Expression}", rule.RuleCode, expression);
            return null; // 解析失败不算违规
        }

        return null;
    }

    /// <summary>
    /// 简单表达式解析器：支持 [字段] op 值 AND/OR [字段] op 值
    /// </summary>
    private static bool EvaluateExpression(string expression, Dictionary<string, object?> row)
    {
        // 先按 OR 拆分
        var orParts = Regex.Split(expression, @"\s+OR\s+", RegexOptions.IgnoreCase);
        foreach (var orPart in orParts)
        {
            // 再按 AND 拆分
            var andParts = Regex.Split(orPart.Trim(), @"\s+AND\s+", RegexOptions.IgnoreCase);
            var allTrue = true;
            foreach (var andPart in andParts)
            {
                if (!EvaluateSingleCondition(andPart.Trim(), row))
                {
                    allTrue = false;
                    break;
                }
            }
            if (allTrue) return true; // OR 中有一个组合为真即返回 true
        }
        return false;
    }

    /// <summary>
    /// 解析单个条件：[字段名] op 值
    /// 支持运算符：>, <, >=, <=, ==, !=
    /// </summary>
    private static bool EvaluateSingleCondition(string condition, Dictionary<string, object?> row)
    {
        // 匹配 [字段名] 运算符 值
        var match = Regex.Match(condition, @"\[(.+?)\]\s*(>=|<=|!=|==|>|<)\s*(.+)");
        if (!match.Success) return true; // 无法解析的条件视为通过

        var fieldName = match.Groups[1].Value;
        var op = match.Groups[2].Value;
        var expectedStr = match.Groups[3].Value.Trim().Trim('\'', '"');

        row.TryGetValue(fieldName, out var actualValue);
        var actualStr = actualValue?.ToString() ?? "";

        // 尝试数值比较
        if (decimal.TryParse(actualStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var actualNum)
            && decimal.TryParse(expectedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedNum))
        {
            return op switch
            {
                ">" => actualNum > expectedNum,
                "<" => actualNum < expectedNum,
                ">=" => actualNum >= expectedNum,
                "<=" => actualNum <= expectedNum,
                "==" => actualNum == expectedNum,
                "!=" => actualNum != expectedNum,
                _ => true
            };
        }

        // 字符串比较（仅支持 == 和 !=）
        return op switch
        {
            "==" => string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static string[] GetDatePatterns(Dictionary<string, object>? parameters)
    {
        var patternsObj = parameters?.GetValueOrDefault("patterns");
        if (patternsObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            return je.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s != "").ToArray();
        }

        var patternStr = patternsObj?.ToString();
        if (!string.IsNullOrEmpty(patternStr))
            return patternStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

        // 默认日期格式
        return new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd", "yyyy-MM-dd HH:mm:ss" };
    }

    private static Dictionary<string, object>? DeserializeParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string RenderMessage(QualityRuleDefinition rule, string? value, string? fieldName)
    {
        var template = rule.MessageTemplate;
        if (string.IsNullOrEmpty(template))
        {
            // 默认消息
            return $"规则[{rule.RuleName}]校验失败" +
                   (fieldName != null ? $", 字段: {fieldName}" : "") +
                   (value != null ? $", 值: {value}" : "");
        }

        var message = template
            .Replace("{FieldName}", fieldName ?? rule.TargetField ?? "")
            .Replace("{Value}", value ?? "")
            .Replace("{RuleName}", rule.RuleName);

        // 替换 Range 参数
        if (rule.Parameters != null)
        {
            if (rule.Parameters.TryGetValue("min", out var min))
                message = message.Replace("{Min}", min?.ToString() ?? "");
            if (rule.Parameters.TryGetValue("max", out var max))
                message = message.Replace("{Max}", max?.ToString() ?? "");
        }

        return message;
    }

    private static QualityViolation CreateViolation(QualityRuleDefinition rule, int rowIndex, string? originalValue, string errorMessage)
    {
        return new QualityViolation
        {
            RuleId = rule.Id,
            RuleCode = rule.RuleCode,
            ErrorTypeCode = rule.ErrorTypeCode,
            Severity = rule.Severity,
            QualityDimension = rule.QualityDimension,
            TargetField = rule.TargetField,
            OriginalValue = originalValue,
            ErrorMessage = errorMessage,
            SuggestedFix = rule.SuggestedFix,
            IsBlocking = rule.IsBlocking,
            RowIndex = rowIndex
        };
    }

    #endregion
}
