using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class StageFieldAccessService : IStageFieldAccessService
{
    private static readonly HashSet<string> WritableAccess = new(StringComparer.OrdinalIgnoreCase)
    {
        "editable",
        "required"
    };

    private static readonly HashSet<string> ReadOnlyAccess = new(StringComparer.OrdinalIgnoreCase)
    {
        "readonly",
        "hidden",
        "masked"
    };

    public StageFieldAccessValidationResult ValidateSupplement(
        StageConfigEnvelope normalizedConfig,
        IReadOnlyDictionary<string, object>? supplement)
    {
        if (supplement == null || supplement.Count == 0)
        {
            return StageFieldAccessValidationResult.Ok();
        }

        var writable = GetWritableFieldKeys(normalizedConfig);
        foreach (var fieldKey in supplement.Keys)
        {
            if (TryGetBlockingComponentForField(normalizedConfig, fieldKey, out var componentId))
            {
                return DenyComponent(componentId, fieldKey);
            }

            if (TryGetFieldRule(normalizedConfig, fieldKey, out var rule)
                && ReadOnlyAccess.Contains(rule.Access))
            {
                return Deny(fieldKey);
            }

            if (!writable.Contains(fieldKey))
            {
                return Deny(fieldKey);
            }
        }

        return StageFieldAccessValidationResult.Ok();
    }

    public StageFieldAccessValidationResult ValidateRequiredFields(
        StageConfigEnvelope normalizedConfig,
        IReadOnlyDictionary<string, object?> currentData,
        IReadOnlyDictionary<string, object>? supplement)
    {
        if (normalizedConfig.ViewProfile?.FieldAccess == null)
        {
            return StageFieldAccessValidationResult.Ok();
        }

        foreach (var (fieldKey, rule) in normalizedConfig.ViewProfile.FieldAccess)
        {
            if (!IsRequired(rule))
            {
                continue;
            }

            var value = supplement != null && supplement.TryGetValue(fieldKey, out var submitted)
                ? submitted
                : currentData.TryGetValue(fieldKey, out var existing)
                    ? existing
                    : null;
            if (IsMissing(value))
            {
                return StageFieldAccessValidationResult.Fail($"字段权限校验失败: [{fieldKey}] 当前节点必填");
            }
        }

        return StageFieldAccessValidationResult.Ok();
    }

    public StageFieldAccessValidationResult ValidateDetailEdits(
        StageConfigEnvelope normalizedConfig,
        IReadOnlyCollection<DetailRowEditRequest>? detailEdits)
    {
        if (detailEdits == null || detailEdits.Count == 0)
        {
            return StageFieldAccessValidationResult.Ok();
        }

        foreach (var edit in detailEdits)
        {
            var tableKey = string.IsNullOrWhiteSpace(edit.DetailTableKey) ? "default" : edit.DetailTableKey;
            if (TryGetBlockingComponentForDetailTable(normalizedConfig, tableKey, out var componentId))
            {
                return DenyComponent(componentId, tableKey);
            }

            foreach (var columnKey in edit.Values.Keys)
            {
                var accessKey = $"{tableKey}.{columnKey}";
                if (!TryGetDetailRule(normalizedConfig, accessKey, out var rule)
                    || !WritableAccess.Contains(rule.Access))
                {
                    return Deny(accessKey);
                }
            }
        }

        return StageFieldAccessValidationResult.Ok();
    }

    public IReadOnlySet<string> GetWritableFieldKeys(StageConfigEnvelope normalizedConfig)
    {
        var writable = new HashSet<string>(normalizedConfig.InputFields ?? new List<string>(), StringComparer.Ordinal);
        if (normalizedConfig.ViewProfile?.FieldAccess != null)
        {
            foreach (var (fieldKey, rule) in normalizedConfig.ViewProfile.FieldAccess)
            {
                if (WritableAccess.Contains(rule.Access))
                {
                    writable.Add(fieldKey);
                }
            }
        }

        return writable;
    }

    private static bool TryGetFieldRule(
        StageConfigEnvelope normalizedConfig,
        string fieldKey,
        out StageFieldAccessRule rule)
    {
        if (normalizedConfig.ViewProfile?.FieldAccess != null
            && normalizedConfig.ViewProfile.FieldAccess.TryGetValue(fieldKey, out var configuredRule))
        {
            rule = configuredRule;
            return true;
        }

        rule = new StageFieldAccessRule();
        return false;
    }

    private static bool TryGetDetailRule(
        StageConfigEnvelope normalizedConfig,
        string accessKey,
        out StageDetailAccessRule rule)
    {
        if (normalizedConfig.ViewProfile?.DetailAccess != null
            && normalizedConfig.ViewProfile.DetailAccess.TryGetValue(accessKey, out var configuredRule))
        {
            rule = configuredRule;
            return true;
        }

        rule = new StageDetailAccessRule();
        return false;
    }

    private static bool TryGetBlockingComponentForField(
        StageConfigEnvelope normalizedConfig,
        string fieldKey,
        out string componentId)
    {
        componentId = string.Empty;
        if (normalizedConfig.ViewProfile?.Components == null || normalizedConfig.ViewProfile.ComponentAccess == null)
        {
            return false;
        }

        foreach (var component in normalizedConfig.ViewProfile.Components)
        {
            if (!string.Equals(component.Binding.Source, "cardField", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(component.Binding.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalizedConfig.ViewProfile.ComponentAccess.TryGetValue(component.Id, out var rule)
                && !WritableAccess.Contains(rule.Access))
            {
                componentId = component.Id;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBlockingComponentForDetailTable(
        StageConfigEnvelope normalizedConfig,
        string tableKey,
        out string componentId)
    {
        componentId = string.Empty;
        if (normalizedConfig.ViewProfile?.Components == null || normalizedConfig.ViewProfile.ComponentAccess == null)
        {
            return false;
        }

        foreach (var component in normalizedConfig.ViewProfile.Components)
        {
            var componentTableKey = string.IsNullOrWhiteSpace(component.Binding.DetailTableKey)
                ? "default"
                : component.Binding.DetailTableKey;
            if (!string.Equals(component.Binding.Source, "detailTable", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(componentTableKey, tableKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalizedConfig.ViewProfile.ComponentAccess.TryGetValue(component.Id, out var rule)
                && !WritableAccess.Contains(rule.Access))
            {
                componentId = component.Id;
                return true;
            }
        }

        return false;
    }

    private static bool IsRequired(StageFieldAccessRule rule)
    {
        return rule.Required == true || string.Equals(rule.Access, "required", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissing(object? value)
    {
        return value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            _ => false
        };
    }

    private static StageFieldAccessValidationResult Deny(string key)
    {
        return StageFieldAccessValidationResult.Fail($"字段权限校验失败: [{key}] 当前节点不可编辑");
    }

    private static StageFieldAccessValidationResult DenyComponent(string componentId, string key)
    {
        return StageFieldAccessValidationResult.Fail($"组件权限校验失败: [{componentId}] 绑定数据 [{key}] 当前节点不可编辑");
    }
}
