using System.Text.Json;
using System.Text.Json.Nodes;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.CardFlow.Services.Redaction;

namespace STOTOP.Module.CardFlow.Services;

public sealed class CardPresentationResolver : ICardPresentationResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> WritableAccess = new(StringComparer.OrdinalIgnoreCase)
    {
        "editable",
        "required"
    };

    public CardPresentationRuntimeView Resolve(CardPresentationResolveRequest request)
    {
        var fields = ReadCardFields(request.CardSchemaJson);
        var detailTables = ReadDetailTables(request.DetailSchemaJson);
        var cardData = ParseObject(request.Card.FDataJson);
        var definitions = ResolveComponentDefinitions(request, fields, detailTables);
        var result = new CardPresentationRuntimeView();

        ComputeDefaultDetailSummary(request.Details, result.DetailSummary, request.DetailAccess);
        foreach (var definition in definitions)
        {
            ApplyAggregations(definition, request.Details, result.DetailSummary, request.DetailAccess);
        }

        foreach (var definition in definitions)
        {
            var component = BuildRuntimeComponent(definition, request, fields, detailTables, cardData);
            if (component.Visible)
            {
                result.Components.Add(component);
            }
        }

        return result;
    }

    private static List<CardComponentDefinition> ResolveComponentDefinitions(
        CardPresentationResolveRequest request,
        IReadOnlyCollection<CardFieldDefinitionV2> fields,
        IReadOnlyCollection<CardDetailTableDefinitionV2> detailTables)
    {
        if (request.StageProfile.Components.Count > 0)
        {
            return request.StageProfile.Components.Select(component => new CardComponentDefinition
            {
                Id = component.Id,
                Type = string.IsNullOrWhiteSpace(component.Type) ? InferType(component.Binding, fields) : component.Type!,
                Title = component.Title ?? component.Id,
                Binding = component.Binding,
                Props = component.Props,
                Validation = component.Validation,
                VisibilityCondition = component.VisibilityCondition,
                Layout = component.Layout,
                Aggregation = component.Aggregation,
                StatisticKey = component.StatisticKey
            }).ToList();
        }

        var schema = ReadCardSchema(request.CardSchemaJson);
        if (schema.Components.Count > 0)
        {
            return schema.Components;
        }

        return BuildLegacyComponents(fields, detailTables);
    }

    private static List<CardComponentDefinition> BuildLegacyComponents(
        IReadOnlyCollection<CardFieldDefinitionV2> fields,
        IReadOnlyCollection<CardDetailTableDefinitionV2> detailTables)
    {
        var components = fields.Select(field => new CardComponentDefinition
        {
            Id = $"field_{field.Key}",
            Type = field.Type,
            Title = string.IsNullOrWhiteSpace(field.Label) ? field.Key : field.Label,
            Binding = new CardComponentBinding
            {
                Source = "cardField",
                FieldKey = field.Key
            }
        }).ToList();

        foreach (var table in detailTables)
        {
            components.Add(new CardComponentDefinition
            {
                Id = $"detail_{table.DetailTableKey}",
                Type = "detailTable",
                Title = string.IsNullOrWhiteSpace(table.Label) ? "明细" : table.Label,
                Binding = new CardComponentBinding
                {
                    Source = "detailTable",
                    DetailTableKey = table.DetailTableKey
                },
                Aggregation = table.Columns.Any(column => string.Equals(column.Key, "amount", StringComparison.OrdinalIgnoreCase))
                    ? new CardComponentAggregationConfig
                    {
                        Sum =
                        {
                            new CardComponentSumAggregation
                            {
                                FieldKey = "amount",
                                TargetKey = "detailSum.amount"
                            }
                        }
                    }
                    : null
            });
        }

        return components;
    }

    private static CardComponentRuntimeDto BuildRuntimeComponent(
        CardComponentDefinition definition,
        CardPresentationResolveRequest request,
        IReadOnlyCollection<CardFieldDefinitionV2> fields,
        IReadOnlyCollection<CardDetailTableDefinitionV2> detailTables,
        JsonObject cardData)
    {
        var access = ResolveAccess(definition, request);
        var component = new CardComponentRuntimeDto
        {
            Id = definition.Id,
            Type = definition.Type,
            Title = definition.Title,
            Access = access,
            Binding = definition.Binding,
            Props = definition.Props,
            StatisticKey = definition.StatisticKey
        };
        ApplyComponentAccess(component, definition, request);

        if (!component.Visible)
        {
            return component;
        }

        switch (definition.Binding.Source)
        {
            case "cardField":
                component.Value = ResolveCardFieldValue(definition, request, cardData);
                break;
            case "detailTable":
                BindDetailTable(component, definition, request, detailTables);
                break;
            case "detailSummary":
                component.Value = ResolveSummaryValue(definition, request);
                break;
            case "relation":
                component.Value = ResolveRelations(definition, request);
                break;
            case "snapshot":
                component.Snapshots = request.Snapshots
                    .Where(snapshot => string.IsNullOrWhiteSpace(definition.Binding.SnapshotType)
                        || string.Equals(snapshot.SnapshotType, definition.Binding.SnapshotType, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                break;
            default:
                component.Value = null;
                break;
        }

        return component;
    }

    private static void ApplyComponentAccess(
        CardComponentRuntimeDto component,
        CardComponentDefinition definition,
        CardPresentationResolveRequest request)
    {
        component.Visible = !string.Equals(component.Access, "hidden", StringComparison.OrdinalIgnoreCase);
        component.Editable = WritableAccess.Contains(component.Access);
        component.Required = string.Equals(component.Access, "required", StringComparison.OrdinalIgnoreCase)
            || (request.StageProfile.ComponentAccess.TryGetValue(definition.Id, out var accessRule) && accessRule.Required == true);
        component.Masked = string.Equals(component.Access, "masked", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveAccess(CardComponentDefinition definition, CardPresentationResolveRequest request)
    {
        if (request.StageProfile.ComponentAccess.TryGetValue(definition.Id, out var componentAccess))
        {
            return NormalizeAccess(componentAccess.Access);
        }

        if (!string.IsNullOrWhiteSpace(definition.Access))
        {
            return NormalizeAccess(definition.Access);
        }

        if (string.Equals(definition.Binding.Source, "cardField", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(definition.Binding.FieldKey)
            && request.FieldAccess.TryGetValue(definition.Binding.FieldKey, out var fieldAccess))
        {
            return NormalizeAccess(fieldAccess.Access);
        }

        if (string.Equals(definition.Binding.Source, "detailTable", StringComparison.OrdinalIgnoreCase))
        {
            var tableKey = string.IsNullOrWhiteSpace(definition.Binding.DetailTableKey) ? "default" : definition.Binding.DetailTableKey;
            var tableRules = request.DetailAccess
                .Where(pair => pair.Key.StartsWith($"{tableKey}.", StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value.Access)
                .ToList();
            if (tableRules.Count == 0)
            {
                return "readonly";
            }
            if (tableRules.All(access => string.Equals(access, "hidden", StringComparison.OrdinalIgnoreCase)))
            {
                return "hidden";
            }
            return tableRules.Any(WritableAccess.Contains) ? "editable" : "readonly";
        }

        return "readonly";
    }

    private static object? ResolveCardFieldValue(
        CardComponentDefinition definition,
        CardPresentationResolveRequest request,
        JsonObject cardData)
    {
        if (string.IsNullOrWhiteSpace(definition.Binding.FieldKey)
            || !cardData.TryGetPropertyValue(definition.Binding.FieldKey, out var value))
        {
            return null;
        }

        var fieldAccess = request.FieldAccess.TryGetValue(definition.Binding.FieldKey, out var access)
            ? access.Access
            : null;
        var componentAccess = request.StageProfile.ComponentAccess.TryGetValue(definition.Id, out var componentRule)
            ? componentRule.Access
            : definition.Access;
        var shouldMask = string.Equals(fieldAccess, "masked", StringComparison.OrdinalIgnoreCase)
            || string.Equals(componentAccess, "masked", StringComparison.OrdinalIgnoreCase);

        return shouldMask ? MaskValue(value) : ConvertNodeValue(value);
    }

    private static object? ResolveSummaryValue(CardComponentDefinition definition, CardPresentationResolveRequest request)
    {
        var summaryKey = definition.Binding.SummaryKey;
        if (string.IsNullOrWhiteSpace(summaryKey))
        {
            return null;
        }

        var summary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ComputeDefaultDetailSummary(request.Details, summary, request.DetailAccess);
        ApplyAggregations(definition, request.Details, summary, request.DetailAccess);
        return summary.TryGetValue(summaryKey, out var value) ? value : null;
    }

    private static List<CardPresentationRelation> ResolveRelations(
        CardComponentDefinition definition,
        CardPresentationResolveRequest request)
    {
        if (string.IsNullOrWhiteSpace(definition.Binding.RelationType))
        {
            return request.Relations.OrderBy(relation => relation.Id).ToList();
        }

        return request.Relations
            .Where(relation => string.Equals(
                relation.RelationType,
                definition.Binding.RelationType,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(relation => relation.Id)
            .ToList();
    }

    private static void BindDetailTable(
        CardComponentRuntimeDto component,
        CardComponentDefinition definition,
        CardPresentationResolveRequest request,
        IReadOnlyCollection<CardDetailTableDefinitionV2> detailTables)
    {
        var tableKey = string.IsNullOrWhiteSpace(definition.Binding.DetailTableKey)
            ? "default"
            : definition.Binding.DetailTableKey!;
        var tableDefinition = detailTables.FirstOrDefault(table =>
            string.Equals(table.DetailTableKey, tableKey, StringComparison.OrdinalIgnoreCase));

        var columns = tableDefinition?.Columns ?? new List<CardFieldDefinitionV2>();
        component.Columns = columns.Select(column =>
        {
            var accessKey = $"{tableKey}.{column.Key}";
            var access = request.DetailAccess.TryGetValue(accessKey, out var rule)
                ? NormalizeAccess(rule.Access)
                : "readonly";
            return new CardComponentColumnRuntimeDto
            {
                Key = column.Key,
                Label = string.IsNullOrWhiteSpace(column.Label) ? column.Key : column.Label,
                Type = column.Type,
                Access = access,
                Editable = WritableAccess.Contains(access) && component.Editable,
                Required = rule?.Required == true || string.Equals(access, "required", StringComparison.OrdinalIgnoreCase),
                Masked = string.Equals(access, "masked", StringComparison.OrdinalIgnoreCase)
            };
        }).ToList();

        var visibleColumns = component.Columns
            .Where(column => !string.Equals(column.Access, "hidden", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var rows = request.Details
            .Where(detail => string.Equals(
                string.IsNullOrWhiteSpace(detail.FDetailTableKey) ? "default" : detail.FDetailTableKey,
                tableKey,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(detail => detail.FSortOrder)
            .ToList();

        foreach (var detail in rows)
        {
            var rowData = ParseObject(detail.FDataJson);
            var runtimeRow = new CardComponentRowRuntimeDto
            {
                Id = detail.FID,
                SortOrder = detail.FSortOrder
            };
            foreach (var column in visibleColumns)
            {
                rowData.TryGetPropertyValue(column.Key, out var value);
                runtimeRow.Values[column.Key] = column.Masked ? MaskValue(value) : ConvertNodeValue(value);
            }
            component.Rows.Add(runtimeRow);
        }

        if (definition.Validation?.MinRows is > 0 && component.Rows.Count < definition.Validation.MinRows.Value)
        {
            component.Warnings.Add($"明细组件 {definition.Title} 至少需要 {definition.Validation.MinRows.Value} 行");
        }
    }

    private static bool IsColumnRestricted(
        IReadOnlyDictionary<string, StageDetailAccessRule> detailAccess,
        string tableKey,
        string fieldKey)
    {
        if (detailAccess.TryGetValue($"{tableKey}.{fieldKey}", out var rule))
        {
            var a = rule.Access?.Trim().ToLowerInvariant();
            return a == "hidden" || a == "masked";
        }

        return false;
    }

    private static void ApplyAggregations(
        CardComponentDefinition definition,
        IReadOnlyCollection<CfCardDetail> details,
        IDictionary<string, object?> detailSummary,
        IReadOnlyDictionary<string, StageDetailAccessRule> detailAccess)
    {
        if (definition.Aggregation?.Sum == null || definition.Aggregation.Sum.Count == 0)
        {
            return;
        }

        var tableKey = string.IsNullOrWhiteSpace(definition.Binding.DetailTableKey)
            ? "default"
            : definition.Binding.DetailTableKey!;
        foreach (var sum in definition.Aggregation.Sum)
        {
            if (string.IsNullOrWhiteSpace(sum.FieldKey) || string.IsNullOrWhiteSpace(sum.TargetKey))
            {
                continue;
            }

            if (IsColumnRestricted(detailAccess, tableKey, sum.FieldKey))
            {
                continue;
            }

            detailSummary[sum.TargetKey] = SumDetailField(details, tableKey, sum.FieldKey);
        }
    }

    private static void ComputeDefaultDetailSummary(
        IReadOnlyCollection<CfCardDetail> details,
        IDictionary<string, object?> detailSummary,
        IReadOnlyDictionary<string, StageDetailAccessRule> detailAccess)
    {
        if (IsColumnRestricted(detailAccess, "default", "amount"))
        {
            return;
        }

        if (!detailSummary.ContainsKey("detailSum.amount"))
        {
            detailSummary["detailSum.amount"] = SumDetailField(details, null, "amount");
        }
    }

    private static decimal SumDetailField(
        IReadOnlyCollection<CfCardDetail> details,
        string? tableKey,
        string fieldKey)
    {
        decimal total = 0;
        foreach (var detail in details)
        {
            var currentTable = string.IsNullOrWhiteSpace(detail.FDetailTableKey) ? "default" : detail.FDetailTableKey;
            if (!string.IsNullOrWhiteSpace(tableKey)
                && !string.Equals(currentTable, tableKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = ParseObject(detail.FDataJson);
            if (data.TryGetPropertyValue(fieldKey, out var value) && TryReadDecimal(value, out var amount))
            {
                total += amount;
            }
        }

        return total;
    }

    private static CardSchemaV2 ReadCardSchema(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new CardSchemaV2();
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return new CardSchemaV2
                {
                    Fields = JsonSerializer.Deserialize<List<CardFieldDefinitionV2>>(schemaJson, JsonOptions) ?? new()
                };
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<CardSchemaV2>(schemaJson, JsonOptions) ?? new CardSchemaV2();
            }
        }
        catch (JsonException)
        {
            return new CardSchemaV2();
        }

        return new CardSchemaV2();
    }

    private static List<CardFieldDefinitionV2> ReadCardFields(string? schemaJson)
        => ReadCardSchema(schemaJson).Fields ?? new List<CardFieldDefinitionV2>();

    private static List<CardDetailTableDefinitionV2> ReadDetailTables(string? detailSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(detailSchemaJson))
        {
            return new List<CardDetailTableDefinitionV2>();
        }

        try
        {
            using var document = JsonDocument.Parse(detailSchemaJson);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return new List<CardDetailTableDefinitionV2>
                {
                    new()
                    {
                        DetailTableKey = "default",
                        Label = "明细",
                        Columns = JsonSerializer.Deserialize<List<CardFieldDefinitionV2>>(detailSchemaJson, JsonOptions) ?? new()
                    }
                };
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var schema = JsonSerializer.Deserialize<CardDetailSchemaV2>(detailSchemaJson, JsonOptions);
                return schema?.Tables ?? new List<CardDetailTableDefinitionV2>();
            }
        }
        catch (JsonException)
        {
            return new List<CardDetailTableDefinitionV2>();
        }

        return new List<CardDetailTableDefinitionV2>();
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

    private static string InferType(CardComponentBinding binding, IReadOnlyCollection<CardFieldDefinitionV2> fields)
    {
        if (string.Equals(binding.Source, "detailTable", StringComparison.OrdinalIgnoreCase))
        {
            return "detailTable";
        }

        if (string.Equals(binding.Source, "snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(binding.SnapshotType, "dynamicApprover", StringComparison.OrdinalIgnoreCase)
                ? "dynamicApprover"
                : "routeDecision";
        }

        if (!string.IsNullOrWhiteSpace(binding.FieldKey))
        {
            return fields.FirstOrDefault(field => string.Equals(field.Key, binding.FieldKey, StringComparison.OrdinalIgnoreCase))?.Type ?? "text";
        }

        return "text";
    }

    private static string NormalizeAccess(string? access)
    {
        return access?.Trim() switch
        {
            "hidden" => "hidden",
            "masked" => "masked",
            "editable" => "editable",
            "required" => "required",
            _ => "readonly"
        };
    }

    private static object? ConvertNodeValue(JsonNode? value)
    {
        if (value == null)
        {
            return null;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number when value.AsValue().TryGetValue<decimal>(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.ToJsonString()
        };
    }

    private static string MaskValue(JsonNode? value, string? pattern = null)
    {
        var text = ConvertNodeValue(value)?.ToString() ?? string.Empty;
        return FieldMasker.Mask(text, pattern);
    }

    private static bool TryReadDecimal(JsonNode? value, out decimal result)
    {
        result = 0;
        if (value == null)
        {
            return false;
        }

        if (value.GetValueKind() == JsonValueKind.Number && value.AsValue().TryGetValue<decimal>(out var decimalValue))
        {
            result = decimalValue;
            return true;
        }

        return decimal.TryParse(ConvertNodeValue(value)?.ToString(), out result);
    }
}
