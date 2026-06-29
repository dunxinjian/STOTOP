using System.Text.Json;
using System.Text.Json.Nodes;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.CardFlow.Services.Redaction;

namespace STOTOP.Module.CardFlow.Services;

public sealed class StageViewProfileResolver : IStageViewProfileResolver
{
    private static readonly List<string> LegacyDefaultActions = new()
    {
        "approve",
        "reject",
        "transfer",
        "addSignAfter",
        "cc"
    };

    private readonly ICardPresentationResolver _presentationResolver;

    public StageViewProfileResolver()
        : this(new CardPresentationResolver())
    {
    }

    public StageViewProfileResolver(ICardPresentationResolver presentationResolver)
    {
        _presentationResolver = presentationResolver;
    }

    public StageViewResolutionResult Resolve(
        string? cardSchemaJson,
        string? detailSchemaJson,
        CfStageDefinition stageDefinition,
        CfCard card,
        IReadOnlyCollection<CfCardDetail> details,
        long operatorId,
        StageConfigEnvelope normalizedConfig,
        IReadOnlyCollection<CardPresentationRelation>? relations = null,
        IReadOnlyCollection<CardPresentationSnapshot>? snapshots = null)
    {
        var fields = ReadFields(cardSchemaJson);
        var fieldKeys = fields.Select(f => f.Key).Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        var fieldAccess = BuildFieldAccess(fields, normalizedConfig);
        var detailAccess = BuildDetailAccess(detailSchemaJson, normalizedConfig);
        var presentation = _presentationResolver.Resolve(new CardPresentationResolveRequest
        {
            CardSchemaJson = cardSchemaJson,
            DetailSchemaJson = detailSchemaJson,
            StageProfile = normalizedConfig.ViewProfile ?? new StageViewProfile(),
            FieldAccess = fieldAccess,
            DetailAccess = detailAccess,
            Card = card,
            Details = details,
            Relations = relations ?? Array.Empty<CardPresentationRelation>(),
            Snapshots = snapshots?.ToList() ?? new List<CardPresentationSnapshot>()
        });

        return new StageViewResolutionResult
        {
            Sections = normalizedConfig.ViewProfile?.Sections ?? BuildLegacySections(fieldKeys, detailAccess.Keys.Any()),
            FieldAccess = fieldAccess,
            DetailAccess = detailAccess,
            Summary = normalizedConfig.ViewProfile?.Summary,
            AllowedActions = ResolveActions(normalizedConfig),
            Presentation = presentation,
            RedactedDataJson = RedactJson(card.FDataJson, fieldAccess),
            RedactedDetails = details
                .OrderBy(d => d.FSortOrder)
                .Select(d => new RedactedDetailRow
                {
                    Id = d.FID,
                    SortOrder = d.FSortOrder,
                    DetailTableKey = string.IsNullOrWhiteSpace(d.FDetailTableKey) ? "default" : d.FDetailTableKey,
                    DataJson = RedactDetailJson(
                        d.FDataJson,
                        string.IsNullOrWhiteSpace(d.FDetailTableKey) ? "default" : d.FDetailTableKey,
                        detailAccess)
                })
                .ToList()
        };
    }

    private static Dictionary<string, StageFieldAccessRule> BuildFieldAccess(
        IReadOnlyCollection<CardFieldDefinitionV2> fields,
        StageConfigEnvelope config)
    {
        var result = new Dictionary<string, StageFieldAccessRule>();
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                continue;
            }

            StageFieldAccessRule baseline;
            if (config.InputFields.Contains(field.Key))
            {
                baseline = new StageFieldAccessRule { Access = "editable" };
            }
            else if (field.Sensitive)
            {
                baseline = new StageFieldAccessRule { Access = "masked", MaskPattern = field.MaskPattern };
            }
            else
            {
                baseline = new StageFieldAccessRule { Access = "readonly" };
            }

            result[field.Key] = baseline;
        }

        if (config.ViewProfile?.FieldAccess != null)
        {
            foreach (var (key, rule) in config.ViewProfile.FieldAccess)
            {
                result[key] = rule;
            }
        }

        return result;
    }

    private static Dictionary<string, StageDetailAccessRule> BuildDetailAccess(
        string? detailSchemaJson,
        StageConfigEnvelope config)
    {
        var result = new Dictionary<string, StageDetailAccessRule>();
        foreach (var field in ReadFields(detailSchemaJson))
        {
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                continue;
            }

            var rule = field.Sensitive
                ? new StageDetailAccessRule { Access = "masked", MaskPattern = field.MaskPattern }
                : new StageDetailAccessRule { Access = "readonly" };
            result[$"default.{field.Key}"] = rule;
        }

        if (config.ViewProfile?.DetailAccess != null)
        {
            foreach (var (key, rule) in config.ViewProfile.DetailAccess)
            {
                result[key] = rule;
            }
        }

        return result;
    }

    private static List<string> ResolveActions(StageConfigEnvelope config)
    {
        var allowed = config.ActionPolicy?.AllowedActions is { Count: > 0 }
            ? config.ActionPolicy.AllowedActions
            : LegacyDefaultActions;

        if (config.ViewProfile?.Actions is { Count: > 0 } displayActions)
        {
            var allowedSet = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return displayActions.Where(a => allowedSet.Contains(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return allowed.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<StageViewSection> BuildLegacySections(IReadOnlyCollection<string> fieldKeys, bool hasDetails)
    {
        var sections = new List<StageViewSection>
        {
            new()
            {
                Key = "fields",
                Type = "fields",
                Fields = fieldKeys.Select(key => new StageViewFieldRef { FieldKey = key }).ToList()
            }
        };

        if (hasDetails)
        {
            sections.Add(new StageViewSection { Key = "details", Type = "detailTable" });
        }

        return sections;
    }

    private static string RedactJson(string? dataJson, IReadOnlyDictionary<string, StageFieldAccessRule> access)
    {
        var obj = ParseObject(dataJson);
        foreach (var (key, rule) in access)
        {
            if (rule.Access.Equals("hidden", StringComparison.OrdinalIgnoreCase))
            {
                obj.Remove(key);
            }
            else if (rule.Access.Equals("masked", StringComparison.OrdinalIgnoreCase) && obj.ContainsKey(key))
            {
                obj[key] = MaskValue(obj[key], rule.MaskPattern);
            }
        }

        return obj.ToJsonString();
    }

    private static string RedactDetailJson(
        string? dataJson,
        string detailTableKey,
        IReadOnlyDictionary<string, StageDetailAccessRule> access)
    {
        var obj = ParseObject(dataJson);
        var prefix = $"{detailTableKey}.";
        foreach (var (key, rule) in access)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var columnKey = key[prefix.Length..];
            if (rule.Access.Equals("hidden", StringComparison.OrdinalIgnoreCase))
            {
                obj.Remove(columnKey);
            }
            else if (rule.Access.Equals("masked", StringComparison.OrdinalIgnoreCase) && obj.ContainsKey(columnKey))
            {
                obj[columnKey] = MaskValue(obj[columnKey], rule.MaskPattern);
            }
        }

        return obj.ToJsonString();
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static JsonNode MaskValue(JsonNode? value, string? pattern = null)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var text = value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : value.ToJsonString();

        return FieldMasker.Mask(text, pattern);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static List<CardFieldDefinitionV2> ReadFields(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new List<CardFieldDefinitionV2>();
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<CardFieldDefinitionV2>>(schemaJson, JsonOptions) ?? new();
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("fields", out var fieldsProp)
                && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                var schema = JsonSerializer.Deserialize<CardSchemaV2>(schemaJson, JsonOptions);
                return schema?.Fields ?? new List<CardFieldDefinitionV2>();
            }
        }
        catch (JsonException)
        {
            // ignore，返回空
        }

        return new List<CardFieldDefinitionV2>();
    }

    private static List<string> ReadFieldKeys(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new List<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            var root = document.RootElement;

            // 字段数组：v1 扁平为顶层 Array；v2 信封为 { "fields": [...] }
            JsonElement fieldsArray;
            if (root.ValueKind == JsonValueKind.Array)
            {
                fieldsArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("fields", out var fieldsProp)
                && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                fieldsArray = fieldsProp;
            }
            else
            {
                return new List<string>();
            }

            var result = new List<string>();
            foreach (var field in fieldsArray.EnumerateArray())
            {
                if (field.ValueKind == JsonValueKind.Object
                    && field.TryGetProperty("key", out var keyProperty)
                    && keyProperty.ValueKind == JsonValueKind.String)
                {
                    var key = keyProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        result.Add(key);
                    }
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
