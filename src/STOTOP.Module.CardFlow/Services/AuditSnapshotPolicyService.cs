using System.Text.Json;
using STOTOP.Module.CardFlow.Models.Rules;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class AuditSnapshotPolicyService : IAuditSnapshotPolicyService
{
    public string BuildRouteDecisionSnapshotJson(
        ConditionEvaluationContext context,
        StageRouteResolveResult routeResult)
    {
        var consumed = routeResult.Candidates
            .SelectMany(candidate => candidate.ConsumedFields)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = new
        {
            route = routeResult.SelectedRoute?.FEdgeKey,
            routeName = routeResult.SelectedRoute?.FRouteName,
            toStageKey = routeResult.NextStage?.FStageKey,
            reason = routeResult.Reason,
            fields = consumed.ToDictionary(field => field, field => ReadMaskedField(context, field), StringComparer.OrdinalIgnoreCase)
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object? ReadMaskedField(ConditionEvaluationContext context, string field)
    {
        var value = ResolveField(context, field);
        var policy = ResolvePolicy(field);
        if (policy == "neverStore")
            return new { present = value != null, policy };

        return value switch
        {
            null => new { present = false, policy },
            string text when policy == "mask" => new { present = true, policy, masked = MaskText(text) },
            string text when text.Length > 6 && policy != "store" => new { present = true, policy = "mask", masked = MaskText(text) },
            _ => new { present = true, policy, value }
        };
    }

    private static string ResolvePolicy(string field)
    {
        var normalized = field.ToLowerInvariant();
        if (ContainsAny(normalized, "password", "token", "secret", "attachment", "file", "image", "url"))
            return "neverStore";
        if (ContainsAny(normalized, "bank", "account", "phone", "mobile", "idcard", "identity", "email"))
            return "mask";
        return "store";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.Ordinal));
    }

    private static string MaskText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length <= 2) return "***";
        if (text.Length <= 6) return $"{text[0]}***{text[^1]}";
        return $"{text[..2]}***{text[^2..]}";
    }

    private static object? ResolveField(ConditionEvaluationContext context, string field)
    {
        var dot = field.IndexOf('.', StringComparison.Ordinal);
        var root = dot < 0 ? "card" : field[..dot];
        var key = dot < 0 ? field : field[(dot + 1)..];
        var source = root.ToLowerInvariant() switch
        {
            "card" => context.CardData,
            "detailsummary" => context.DetailSummary,
            "source" => context.SourceContext,
            "initiator" => context.Initiator,
            "initiatororg" => context.InitiatorOrg,
            "relations" => context.Relations,
            "currentstageresult" => context.CurrentStageResult,
            _ => context.CardData
        };

        return source.TryGetValue(key, out var value) ? value : null;
    }
}
