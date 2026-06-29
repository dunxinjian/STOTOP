using System.Collections;
using System.Globalization;
using System.Text.Json;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class ConditionRuleEvaluator : IConditionRuleEvaluator
{
    public ConditionRuleEvaluationResult Evaluate(string? conditionJson, ConditionEvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
            return ConditionRuleEvaluationResult.Match("空条件默认匹配");

        try
        {
            using var document = JsonDocument.Parse(conditionJson);
            var result = EvaluateElement(document.RootElement, context);
            result.ConsumedFields = result.ConsumedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return result;
        }
        catch (JsonException ex)
        {
            return new ConditionRuleEvaluationResult
            {
                Matched = false,
                TypeErrors = { $"条件 JSON 无效：{ex.Message}" },
                Explanation = "条件 JSON 无效，按不匹配处理"
            };
        }
    }

    private ConditionRuleEvaluationResult EvaluateElement(JsonElement element, ConditionEvaluationContext context)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new ConditionRuleEvaluationResult
            {
                Matched = false,
                TypeErrors = { "条件节点必须是对象" },
                Explanation = "条件节点类型错误"
            };
        }

        if (element.TryGetProperty("conditions", out var conditionsElement))
        {
            return EvaluateGroup(element, conditionsElement, context);
        }

        return EvaluateItem(element, context);
    }

    private ConditionRuleEvaluationResult EvaluateGroup(
        JsonElement group,
        JsonElement conditionsElement,
        ConditionEvaluationContext context)
    {
        var logic = ReadString(group, "logic")?.ToLowerInvariant() == "or" ? "or" : "and";
        if (conditionsElement.ValueKind != JsonValueKind.Array)
        {
            return new ConditionRuleEvaluationResult
            {
                Matched = false,
                TypeErrors = { "conditions 必须是数组" },
                Explanation = "条件组结构错误"
            };
        }

        var childResults = conditionsElement.EnumerateArray()
            .Select(child => EvaluateElement(child, context))
            .ToList();
        if (childResults.Count == 0)
            return new ConditionRuleEvaluationResult
            {
                Matched = false,
                Explanation = "空条件组，不匹配"
            };

        var matched = logic == "or"
            ? childResults.Any(result => result.Matched)
            : childResults.All(result => result.Matched);
        return new ConditionRuleEvaluationResult
        {
            Matched = matched,
            ConsumedFields = childResults.SelectMany(result => result.ConsumedFields).ToList(),
            TypeErrors = childResults.SelectMany(result => result.TypeErrors).ToList(),
            Explanation = string.Join(logic == "or" ? "；或：" : "；且：", childResults.Select(result => result.Explanation).Where(s => !string.IsNullOrWhiteSpace(s)))
        };
    }

    private ConditionRuleEvaluationResult EvaluateItem(JsonElement item, ConditionEvaluationContext context)
    {
        var field = ReadString(item, "field") ?? string.Empty;
        var op = NormalizeOperator(ReadString(item, "operator"));
        var expected = item.TryGetProperty("value", out var expectedElement) ? ToPlainValue(expectedElement) : null;

        var result = new ConditionRuleEvaluationResult();
        if (string.IsNullOrWhiteSpace(field))
        {
            result.TypeErrors.Add("条件字段不能为空");
            result.Explanation = "字段为空";
            return result;
        }

        result.ConsumedFields.Add(field);
        var resolved = ResolveField(context, field);
        if (op is "exists" or "notExists" or "empty" or "notEmpty")
        {
            var hasValue = resolved.Exists && HasMeaningfulValue(resolved.Value);
            result.Matched = op switch
            {
                "exists" => hasValue,
                "notExists" => !hasValue,
                "empty" => !hasValue,
                "notEmpty" => hasValue,
                _ => false
            };
            result.Explanation = $"{field} {op} => {result.Matched}";
            return result;
        }

        if (!resolved.Exists)
        {
            result.Matched = false;
            result.Explanation = $"{field} 不存在";
            return result;
        }

        result.Matched = op switch
        {
            "eq" => CompareEquality(resolved.Value, expected, true),
            "neq" => !CompareEquality(resolved.Value, expected, true),
            "contains" => ContainsValue(resolved.Value, expected),
            "startswith" => (resolved.Value?.ToString() ?? string.Empty).StartsWith(expected?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "in" => InValue(resolved.Value, expected),
            "inorgchain" => IsInOrgChain(context, resolved.Value, expected),
            "between" => BetweenValue(resolved.Value, expected, result.TypeErrors),
            "gt" or "gte" or "lt" or "lte" => CompareOrdered(resolved.Value, expected, op, result.TypeErrors),
            _ => false
        };
        result.Explanation = $"{field} {op} {FormatValue(expected)} => {result.Matched}";
        return result;
    }

    private static string NormalizeOperator(string? op)
    {
        return op?.Trim() switch
        {
            "==" => "eq",
            "!=" => "neq",
            ">" => "gt",
            ">=" => "gte",
            "<" => "lt",
            "<=" => "lte",
            "startsWith" => "startswith",
            "notExists" => "notExists",
            "notEmpty" => "notEmpty",
            "inOrgChain" => "inorgchain",
            null or "" => "eq",
            var value => value.ToLowerInvariant()
        };
    }

    private static FieldResolution ResolveField(ConditionEvaluationContext context, string path)
    {
        var normalized = path.Trim();
        if (normalized.Equals("orgChain", StringComparison.OrdinalIgnoreCase))
            return new FieldResolution(true, context.OrgChain);
        if (normalized.Equals("roles.code", StringComparison.OrdinalIgnoreCase) || normalized.Equals("roles.codes", StringComparison.OrdinalIgnoreCase))
            return new FieldResolution(true, context.RoleCodes);
        if (normalized.Equals("roles.name", StringComparison.OrdinalIgnoreCase) || normalized.Equals("roles.names", StringComparison.OrdinalIgnoreCase))
            return new FieldResolution(true, context.RoleNames);

        var dot = normalized.IndexOf('.', StringComparison.Ordinal);
        var root = dot < 0 ? "card" : normalized[..dot];
        var key = dot < 0 ? normalized : normalized[(dot + 1)..];
        var source = root.ToLowerInvariant() switch
        {
            "card" or "carddata" => context.CardData,
            "detailsummary" => context.DetailSummary,
            "source" or "sourcecontext" => context.SourceContext,
            "initiator" => context.Initiator,
            "initiatororg" => context.InitiatorOrg,
            "relations" => context.Relations,
            "currentstageresult" => context.CurrentStageResult,
            _ => context.CardData
        };

        return TryReadDictionaryPath(source, key, out var value)
            ? new FieldResolution(true, value)
            : new FieldResolution(false, null);
    }

    private static bool TryReadDictionaryPath(IReadOnlyDictionary<string, object?> source, string path, out object? value)
    {
        var current = (object?)source;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is IReadOnlyDictionary<string, object?> dict)
            {
                if (!TryGetIgnoreCase(dict, part, out current))
                {
                    value = null;
                    return false;
                }
                continue;
            }

            value = null;
            return false;
        }

        value = current;
        return true;
    }

    private static bool TryGetIgnoreCase(IReadOnlyDictionary<string, object?> dict, string key, out object? value)
    {
        if (dict.TryGetValue(key, out value)) return true;
        foreach (var pair in dict)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }
        value = null;
        return false;
    }

    private static bool CompareEquality(object? left, object? right, bool ignoreCase)
    {
        if (left is IEnumerable enumerable && left is not string)
        {
            return enumerable.Cast<object?>().Any(item => CompareEquality(item, right, ignoreCase));
        }

        if (TryReadDecimal(left, out var leftNumber) && TryReadDecimal(right, out var rightNumber))
            return leftNumber == rightNumber;
        if (TryReadDate(left, out var leftDate) && TryReadDate(right, out var rightDate))
            return leftDate == rightDate;

        return string.Equals(
            left?.ToString() ?? string.Empty,
            right?.ToString() ?? string.Empty,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool CompareOrdered(object? left, object? right, string op, List<string> typeErrors)
    {
        if (TryReadDecimal(left, out var leftNumber) && TryReadDecimal(right, out var rightNumber))
        {
            return op switch
            {
                "gt" => leftNumber > rightNumber,
                "gte" => leftNumber >= rightNumber,
                "lt" => leftNumber < rightNumber,
                "lte" => leftNumber <= rightNumber,
                _ => false
            };
        }

        if (TryReadDate(left, out var leftDate) && TryReadDate(right, out var rightDate))
        {
            return op switch
            {
                "gt" => leftDate > rightDate,
                "gte" => leftDate >= rightDate,
                "lt" => leftDate < rightDate,
                "lte" => leftDate <= rightDate,
                _ => false
            };
        }

        typeErrors.Add($"无法对 {FormatValue(left)} 和 {FormatValue(right)} 执行 {op} 比较");
        return false;
    }

    private static bool ContainsValue(object? left, object? right)
    {
        if (left is IEnumerable enumerable && left is not string)
        {
            return enumerable.Cast<object?>().Any(item => CompareEquality(item, right, true));
        }

        return (left?.ToString() ?? string.Empty).Contains(right?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InValue(object? left, object? right)
    {
        if (right is IEnumerable enumerable && right is not string)
        {
            return enumerable.Cast<object?>().Any(item => CompareEquality(left, item, true));
        }

        return (right?.ToString() ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => CompareEquality(left, item, true));
    }

    private static bool IsInOrgChain(ConditionEvaluationContext context, object? left, object? right)
    {
        var chain = context.OrgChain;
        if (left is IEnumerable leftEnumerable && left is not string)
        {
            return leftEnumerable.Cast<object?>().Any(item => chain.Any(org => CompareEquality(org, item, true)));
        }

        if (right != null)
            return chain.Any(org => CompareEquality(org, right, true));

        return chain.Any(org => CompareEquality(org, left, true));
    }

    private static bool BetweenValue(object? left, object? expected, List<string> typeErrors)
    {
        var values = ToList(expected);
        if (values.Count != 2)
        {
            typeErrors.Add("between 需要两个边界值");
            return false;
        }

        if (TryReadDecimal(left, out var leftNumber)
            && TryReadDecimal(values[0], out var minNumber)
            && TryReadDecimal(values[1], out var maxNumber))
        {
            return leftNumber >= minNumber && leftNumber <= maxNumber;
        }

        if (TryReadDate(left, out var leftDate)
            && TryReadDate(values[0], out var minDate)
            && TryReadDate(values[1], out var maxDate))
        {
            return leftDate >= minDate && leftDate <= maxDate;
        }

        typeErrors.Add("between 边界类型不兼容");
        return false;
    }

    private static List<object?> ToList(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
            return enumerable.Cast<object?>().ToList();
        return new List<object?> { value };
    }

    private static bool HasMeaningfulValue(object? value)
    {
        return value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            IEnumerable enumerable when value is not string => enumerable.Cast<object?>().Any(),
            _ => true
        };
    }

    private static bool TryReadDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal d:
                result = d;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                result = (decimal)d;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } element:
                return element.TryGetDecimal(out result);
            default:
                return decimal.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }

    private static bool TryReadDate(object? value, out DateTime result)
    {
        if (value is DateTime date)
        {
            result = date;
            return true;
        }
        if (value is JsonElement { ValueKind: JsonValueKind.String } element)
            return DateTime.TryParse(element.GetString(), out result);
        return DateTime.TryParse(value?.ToString(), out result);
    }

    private static object? ToPlainValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ToPlainValue).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToPlainValue(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string FormatValue(object? value)
    {
        return value is IEnumerable enumerable && value is not string
            ? $"[{string.Join(", ", enumerable.Cast<object?>())}]"
            : value?.ToString() ?? "null";
    }

    private sealed record FieldResolution(bool Exists, object? Value);
}
