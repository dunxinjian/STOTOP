using System.Text.Json;
using System.Text.Json.Nodes;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;

namespace STOTOP.Module.CardFlow.Services.Redaction;

public sealed class CardRedactionService : ICardRedactionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CardRedactionResult Redact(CardRedactionRequest request)
    {
        var fields = ReadFields(request.CardSchemaJson);
        var fieldAccess = ComputeFieldAccess(fields, request);

        var detailAccess = ComputeDetailAccess(request.DetailSchemaJson, request);

        return new CardRedactionResult
        {
            FieldAccess = fieldAccess,
            DetailAccess = detailAccess,
            RedactedDataJson = ApplyAllowlist(request.Card.FDataJson, fieldAccess),
            RedactedInitialDataJson = ApplyAllowlist(request.Card.FInitialDataJson, fieldAccess),
            RedactedDetails = request.Details
                .OrderBy(d => d.FSortOrder)
                .Select(d => new RedactedDetailRowResult
                {
                    Id = d.FID,
                    SortOrder = d.FSortOrder,
                    DetailTableKey = NormalizeTable(d.FDetailTableKey),
                    DataJson = ApplyDetailAllowlist(d.FDataJson, NormalizeTable(d.FDetailTableKey), detailAccess)
                })
                .ToList()
        };
    }

    private static Dictionary<string, ResolvedAccess> ComputeFieldAccess(
        IReadOnlyCollection<CardFieldDefinitionV2> fields,
        CardRedactionRequest request)
    {
        if (request.OwnerEditMode)
        {
            return fields
                .Where(f => !string.IsNullOrWhiteSpace(f.Key))
                .ToDictionary(f => f.Key, f => new ResolvedAccess { Access = "editable", MaskPattern = f.MaskPattern }, StringComparer.Ordinal);
        }

        var result = new Dictionary<string, ResolvedAccess>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var key = field.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            // 设计：查看者正作为当前 active 节点处理人时，该节点视图对其权威（可提权，也可限制到 hidden/masked）。
            // 粘附授权（D4）在其"非 active"时生效——卡片流转走后再查看，下方 baseline+sticky 分支会恢复其历史获授的明文。
            if (request.ActiveStageConfig != null)
            {
                result[key] = ResolveActive(request.ActiveStageConfig, field);
                continue;
            }

            var rule = new ResolvedAccess
            {
                Access = field.Sensitive ? "masked" : "readonly",
                MaskPattern = field.MaskPattern
            };

            if (Rank(rule.Access) < Rank("readonly") && GrantedBySticky(request.HandledStageConfigs, key))
            {
                rule.Access = "readonly";
            }

            result[key] = rule;
        }

        return result;
    }

    private static ResolvedAccess ResolveActive(StageConfigEnvelope config, CardFieldDefinitionV2 field)
    {
        if (config.ViewProfile?.FieldAccess != null
            && config.ViewProfile.FieldAccess.TryGetValue(field.Key, out var r))
        {
            return new ResolvedAccess
            {
                Access = NormalizeAccess(r.Access),
                MaskPattern = r.MaskPattern ?? field.MaskPattern
            };
        }

        if (config.InputFields.Contains(field.Key))
        {
            return new ResolvedAccess { Access = "editable", MaskPattern = field.MaskPattern };
        }

        return new ResolvedAccess
        {
            Access = field.Sensitive ? "masked" : "readonly",
            MaskPattern = field.MaskPattern
        };
    }

    private static bool GrantedBySticky(
        IReadOnlyCollection<StageConfigEnvelope> handled,
        string key)
    {
        foreach (var config in handled)
        {
            if (config.InputFields.Contains(key))
            {
                return true;
            }

            if (config.ViewProfile?.FieldAccess != null
                && config.ViewProfile.FieldAccess.TryGetValue(key, out var r)
                && Rank(NormalizeAccess(r.Access)) >= Rank("readonly"))
            {
                return true;
            }
        }

        return false;
    }

    private static int Rank(string access) => access switch
    {
        "hidden" => 0,
        "masked" => 1,
        "readonly" => 2,
        "editable" => 3,
        "required" => 3,
        _ => 1
    };

    private static string NormalizeAccess(string? access) => access?.Trim().ToLowerInvariant() switch
    {
        "hidden" => "hidden",
        "masked" => "masked",
        "editable" => "editable",
        "required" => "editable",
        "readonly" => "readonly",
        // 未知/拼错/null/空 一律按最严处理（fail-closed），不得默认成明文可见
        _ => "masked"
    };

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

            if (root.ValueKind == JsonValueKind.Object)
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

    private static Dictionary<string, ResolvedAccess> ComputeDetailAccess(
        string? detailSchemaJson,
        CardRedactionRequest request)
    {
        if (request.OwnerEditMode)
        {
            var ownerResult = new Dictionary<string, ResolvedAccess>(StringComparer.Ordinal);
            foreach (var (tableKey, columns) in ReadDetailTables(detailSchemaJson))
            {
                foreach (var col in columns)
                {
                    if (!string.IsNullOrWhiteSpace(col.Key))
                    {
                        ownerResult[$"{tableKey}.{col.Key}"] = new ResolvedAccess { Access = "editable", MaskPattern = col.MaskPattern };
                    }
                }
            }

            return ownerResult;
        }

        var result = new Dictionary<string, ResolvedAccess>(StringComparer.Ordinal);
        foreach (var (tableKey, columns) in ReadDetailTables(detailSchemaJson))
        {
            foreach (var col in columns)
            {
                if (string.IsNullOrWhiteSpace(col.Key))
                {
                    continue;
                }

                var access = new ResolvedAccess
                {
                    Access = col.Sensitive ? "masked" : "readonly",
                    MaskPattern = col.MaskPattern
                };

                var accessKey = $"{tableKey}.{col.Key}";
                if (request.ActiveStageConfig?.ViewProfile?.DetailAccess != null
                    && request.ActiveStageConfig.ViewProfile.DetailAccess.TryGetValue(accessKey, out var r))
                {
                    access.Access = NormalizeAccess(r.Access);
                    access.MaskPattern = r.MaskPattern ?? col.MaskPattern;
                }

                result[accessKey] = access;
            }
        }

        return result;
    }

    // 明细 schema 三形态：顶层数组 / {fields:[]} → 单 default 表；{tables:[...]}（CardDetailSchemaV2）→ 多表
    private static List<(string TableKey, List<CardFieldDefinitionV2> Columns)> ReadDetailTables(string? detailSchemaJson)
    {
        var tables = new List<(string, List<CardFieldDefinitionV2>)>();
        if (string.IsNullOrWhiteSpace(detailSchemaJson))
        {
            return tables;
        }

        try
        {
            using var document = JsonDocument.Parse(detailSchemaJson);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                tables.Add(("default", JsonSerializer.Deserialize<List<CardFieldDefinitionV2>>(detailSchemaJson, JsonOptions) ?? new()));
                return tables;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("tables", out var t) && t.ValueKind == JsonValueKind.Array)
                {
                    var schema = JsonSerializer.Deserialize<CardDetailSchemaV2>(detailSchemaJson, JsonOptions);
                    if (schema?.Tables != null)
                    {
                        foreach (var tbl in schema.Tables)
                        {
                            var key = string.IsNullOrWhiteSpace(tbl.DetailTableKey) ? "default" : tbl.DetailTableKey;
                            tables.Add((key, tbl.Columns ?? new List<CardFieldDefinitionV2>()));
                        }
                    }

                    return tables;
                }

                if (root.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Array)
                {
                    var schema = JsonSerializer.Deserialize<CardSchemaV2>(detailSchemaJson, JsonOptions);
                    tables.Add(("default", schema?.Fields ?? new List<CardFieldDefinitionV2>()));
                    return tables;
                }
            }
        }
        catch (JsonException)
        {
            // 解析失败 → 无表 → 明细全部 fail-closed
        }

        return tables;
    }

    private static string ApplyAllowlist(string? dataJson, Dictionary<string, ResolvedAccess> access)
    {
        var obj = ParseObject(dataJson);
        var output = new JsonObject();
        foreach (var (key, rule) in access)
        {
            if (rule.Access == "hidden" || !obj.TryGetPropertyValue(key, out var val))
            {
                continue;
            }

            output[key] = rule.Access == "masked"
                ? MaskNode(val, rule.MaskPattern)
                : val?.DeepClone();
        }

        return output.ToJsonString();
    }

    private static string ApplyDetailAllowlist(
        string? dataJson, string tableKey, Dictionary<string, ResolvedAccess> detailAccess)
    {
        var obj = ParseObject(dataJson);
        var output = new JsonObject();
        var prefix = $"{tableKey}.";
        foreach (var (accessKey, rule) in detailAccess)
        {
            if (!accessKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var col = accessKey[prefix.Length..];
            if (rule.Access == "hidden" || !obj.TryGetPropertyValue(col, out var val))
            {
                continue;
            }

            output[col] = rule.Access == "masked"
                ? MaskNode(val, rule.MaskPattern)
                : val?.DeepClone();
        }

        return output.ToJsonString();
    }

    private static string MaskNode(JsonNode? value, string? pattern)
    {
        // 复杂值（对象/数组）整体打码，避免按长度模式暴露序列化尾字符
        if (value is JsonObject || value is JsonArray)
        {
            return "****";
        }

        return FieldMasker.Mask(NodeToText(value), pattern);
    }

    private static string NormalizeTable(string? key)
        => string.IsNullOrWhiteSpace(key) ? "default" : key!;

    private static string NodeToText(JsonNode? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : value.ToJsonString();
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
}
