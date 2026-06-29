using System.Text.Json;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class ConditionEvaluator : IConditionEvaluator
{
    private static readonly string[] Operators = { ">=", "<=", "!=", "==", ">", "<", "contains", "in" };
    private readonly IConditionRuleEvaluator _conditionRuleEvaluator;

    public ConditionEvaluator()
        : this(new ConditionRuleEvaluator())
    {
    }

    public ConditionEvaluator(IConditionRuleEvaluator conditionRuleEvaluator)
    {
        _conditionRuleEvaluator = conditionRuleEvaluator;
    }

    public bool Evaluate(string condition, Dictionary<string, object?> data, List<SchemaFieldDefinition> schemaFields)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;

        try
        {
            if (condition.TrimStart().StartsWith("{", StringComparison.Ordinal)
                || condition.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                return _conditionRuleEvaluator.Evaluate(condition, new STOTOP.Module.CardFlow.Models.Rules.ConditionEvaluationContext
                {
                    CardData = new Dictionary<string, object?>(data, StringComparer.OrdinalIgnoreCase)
                }).Matched;
            }

            var allowedKeys = schemaFields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var clauses = Tokenize(condition);
            return EvaluateClauses(clauses, data, allowedKeys, schemaFields);
        }
        catch
        {
            return false;
        }
    }

    private List<ConditionClause> Tokenize(string condition)
    {
        var clauses = new List<ConditionClause>();
        // Split by AND/OR while preserving the connector
        var parts = SplitByLogicalOperators(condition);
        return parts;
    }

    private List<ConditionClause> SplitByLogicalOperators(string condition)
    {
        var result = new List<ConditionClause>();
        var remaining = condition.Trim();
        string currentConnector = "AND";

        while (!string.IsNullOrEmpty(remaining))
        {
            // Try to find AND/OR
            int andIdx = FindKeywordIndex(remaining, " AND ");
            int orIdx = FindKeywordIndex(remaining, " OR ");

            string segment;
            if (andIdx < 0 && orIdx < 0)
            {
                segment = remaining;
                remaining = "";
            }
            else if (andIdx >= 0 && (orIdx < 0 || andIdx < orIdx))
            {
                segment = remaining[..andIdx];
                remaining = remaining[(andIdx + 5)..]; // skip " AND "
                var clause = ParseSingleClause(segment.Trim(), currentConnector);
                if (clause != null) result.Add(clause);
                currentConnector = "AND";
                continue;
            }
            else
            {
                segment = remaining[..orIdx];
                remaining = remaining[(orIdx + 4)..]; // skip " OR "
                var clause = ParseSingleClause(segment.Trim(), currentConnector);
                if (clause != null) result.Add(clause);
                currentConnector = "OR";
                continue;
            }

            var lastClause = ParseSingleClause(segment.Trim(), currentConnector);
            if (lastClause != null) result.Add(lastClause);
        }

        return result;
    }

    private static int FindKeywordIndex(string text, string keyword)
    {
        return text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private ConditionClause? ParseSingleClause(string segment, string connector)
    {
        if (string.IsNullOrWhiteSpace(segment)) return null;

        foreach (var op in Operators)
        {
            int idx = segment.IndexOf($" {op} ", StringComparison.OrdinalIgnoreCase);
            if (idx < 0 && segment.Contains(op) && op.Length <= 2)
            {
                // Try without spaces for operators like >=, <= etc.
                idx = segment.IndexOf(op, StringComparison.Ordinal);
                if (idx > 0)
                {
                    var field = segment[..idx].Trim();
                    var value = segment[(idx + op.Length)..].Trim();
                    return new ConditionClause(field, op, value, connector);
                }
            }
            else if (idx >= 0)
            {
                var field = segment[..idx].Trim();
                var value = segment[(idx + op.Length + 2)..].Trim();
                return new ConditionClause(field, op, value, connector);
            }
        }

        return null;
    }

    private bool EvaluateClauses(List<ConditionClause> clauses, Dictionary<string, object?> data,
        HashSet<string> allowedKeys, List<SchemaFieldDefinition> schemaFields)
    {
        if (clauses.Count == 0) return true;

        bool result = EvaluateSingle(clauses[0], data, allowedKeys, schemaFields);

        for (int i = 1; i < clauses.Count; i++)
        {
            var clause = clauses[i];
            bool current = EvaluateSingle(clause, data, allowedKeys, schemaFields);

            if (clause.Connector.Equals("OR", StringComparison.OrdinalIgnoreCase))
                result = result || current;
            else
                result = result && current;
        }

        return result;
    }

    private bool EvaluateSingle(ConditionClause clause, Dictionary<string, object?> data,
        HashSet<string> allowedKeys, List<SchemaFieldDefinition> schemaFields)
    {
        if (!allowedKeys.Contains(clause.Field)) return false;

        data.TryGetValue(clause.Field, out var rawValue);
        var fieldDef = schemaFields.FirstOrDefault(f => f.Key.Equals(clause.Field, StringComparison.OrdinalIgnoreCase));
        var fieldType = fieldDef?.Type ?? "text";

        // NULL handling
        if (rawValue == null || (rawValue is JsonElement je && je.ValueKind == JsonValueKind.Null))
        {
            return clause.Operator switch
            {
                "==" => clause.Value.Equals("null", StringComparison.OrdinalIgnoreCase),
                "!=" => !clause.Value.Equals("null", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        return fieldType.ToLowerInvariant() switch
        {
            "money" or "number" or "decimal" or "integer" => EvaluateNumeric(clause, rawValue),
            "date" or "datetime" => EvaluateDate(clause, rawValue),
            _ => EvaluateString(clause, rawValue)
        };
    }

    private bool EvaluateNumeric(ConditionClause clause, object rawValue)
    {
        if (!TryParseDecimal(rawValue, out var left)) return false;
        if (!decimal.TryParse(clause.Value, out var right)) return false;

        return clause.Operator switch
        {
            ">" => left > right,
            "<" => left < right,
            ">=" => left >= right,
            "<=" => left <= right,
            "==" => left == right,
            "!=" => left != right,
            _ => false
        };
    }

    private bool EvaluateDate(ConditionClause clause, object rawValue)
    {
        if (!TryParseDateTime(rawValue, out var left)) return false;
        if (!DateTime.TryParse(clause.Value, out var right)) return false;

        return clause.Operator switch
        {
            ">" => left > right,
            "<" => left < right,
            ">=" => left >= right,
            "<=" => left <= right,
            "==" => left == right,
            "!=" => left != right,
            _ => false
        };
    }

    private bool EvaluateString(ConditionClause clause, object rawValue)
    {
        var left = ConvertToString(rawValue);
        var right = clause.Value.Trim('\'', '"');

        return clause.Operator switch
        {
            "==" => left.Equals(right, StringComparison.OrdinalIgnoreCase),
            "!=" => !left.Equals(right, StringComparison.OrdinalIgnoreCase),
            "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "in" => right.Split(',').Select(v => v.Trim().Trim('\'', '"'))
                .Any(v => v.Equals(left, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static bool TryParseDecimal(object value, out decimal result)
    {
        if (value is JsonElement je)
            return decimal.TryParse(je.GetRawText(), out result);
        return decimal.TryParse(ConvertToString(value), out result);
    }

    private static bool TryParseDateTime(object value, out DateTime result)
    {
        if (value is JsonElement je)
            return DateTime.TryParse(je.GetString(), out result);
        return DateTime.TryParse(ConvertToString(value), out result);
    }

    private static string ConvertToString(object value)
    {
        if (value is JsonElement je)
        {
            return je.ValueKind == JsonValueKind.String ? je.GetString() ?? "" : je.GetRawText();
        }
        return value?.ToString() ?? "";
    }

    private record ConditionClause(string Field, string Operator, string Value, string Connector);
}
