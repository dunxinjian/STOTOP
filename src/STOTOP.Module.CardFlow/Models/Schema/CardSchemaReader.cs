using System.Text.Json;

namespace STOTOP.Module.CardFlow.Models.Schema;

/// <summary>
/// 卡片 schema JSON 的唯一读取入口：兼容 legacy 裸数组与 V2 信封（{ "fields": [...] }）双形态；
/// 非法 JSON 静默降级为空集合（best-effort，不阻断主流程）。新增读取需求一律走这里，
/// 禁止在各服务里手写 Deserialize，避免 Array/Object 口径漂移复发。
/// </summary>
public static class CardSchemaReader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static CardSchemaV2 ReadSchema(string? schemaJson)
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
            // 解析失败按无 schema 处理
        }

        return new CardSchemaV2();
    }

    public static List<CardFieldDefinitionV2> ReadFields(string? schemaJson)
        => ReadSchema(schemaJson).Fields ?? new List<CardFieldDefinitionV2>();

    /// <summary>
    /// 明细 schema 三形态统一读取：顶层数组 / { fields:[...] } → 单 default 表；{ tables:[...] }（CardDetailSchemaV2）→ 多表。
    /// 非法 JSON 静默降空。呈现（CardPresentationResolver）/脱敏（CardRedactionService）/节点视图（StageViewProfileResolver）
    /// 三处一律走此入口，避免多份读取导致多表口径漂移（曾出现非 default 表敏感列无 baseline 而明文下发）。
    /// </summary>
    public static List<CardDetailTableDefinitionV2> ReadDetailTables(string? detailSchemaJson)
    {
        var tables = new List<CardDetailTableDefinitionV2>();
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
                tables.Add(new CardDetailTableDefinitionV2
                {
                    DetailTableKey = "default",
                    Label = "明细",
                    Columns = JsonSerializer.Deserialize<List<CardFieldDefinitionV2>>(detailSchemaJson, JsonOptions) ?? new()
                });
                return tables;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("tables", out var tablesElement) && tablesElement.ValueKind == JsonValueKind.Array)
                {
                    var schema = JsonSerializer.Deserialize<CardDetailSchemaV2>(detailSchemaJson, JsonOptions);
                    foreach (var table in schema?.Tables ?? new List<CardDetailTableDefinitionV2>())
                    {
                        tables.Add(new CardDetailTableDefinitionV2
                        {
                            DetailTableKey = string.IsNullOrWhiteSpace(table.DetailTableKey) ? "default" : table.DetailTableKey,
                            Label = table.Label,
                            Columns = table.Columns ?? new List<CardFieldDefinitionV2>()
                        });
                    }

                    return tables;
                }

                if (root.TryGetProperty("fields", out var fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Array)
                {
                    var schema = JsonSerializer.Deserialize<CardSchemaV2>(detailSchemaJson, JsonOptions);
                    tables.Add(new CardDetailTableDefinitionV2
                    {
                        DetailTableKey = "default",
                        Label = "明细",
                        Columns = schema?.Fields ?? new List<CardFieldDefinitionV2>()
                    });
                    return tables;
                }
            }
        }
        catch (JsonException)
        {
            // 解析失败 → 无表 → 上层按 fail-closed 处理
        }

        return tables;
    }
}
