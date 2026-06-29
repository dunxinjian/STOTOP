using System.Text.Json;
using System.Text.RegularExpressions;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class CardSchemaService : ICardSchemaService
{
    private static readonly JsonSerializerOptions SchemaJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static List<SchemaFieldDefinition> ReadFields(string schemaJson)
    {
        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<SchemaFieldDefinition>>(schemaJson, SchemaJsonOptions) ?? new();
            }
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("fields", out var fieldsProp)
                && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<SchemaFieldDefinition>>(fieldsProp.GetRawText(), SchemaJsonOptions) ?? new();
            }
        }
        catch (JsonException)
        {
            // 无法解析 → 视为无字段，best-effort 不阻断提交
        }
        return new List<SchemaFieldDefinition>();
    }

    public ValidationResult ValidateCardData(string schemaJson, string dataJson)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(schemaJson))
            return new ValidationResult(true, errors);

        var fields = ReadFields(schemaJson);

        if (fields.Count == 0)
            return new ValidationResult(true, errors);

        Dictionary<string, JsonElement>? data;
        try
        {
            data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            errors.Add("卡片数据JSON解析失败");
            return new ValidationResult(false, errors);
        }

        data ??= new Dictionary<string, JsonElement>();

        foreach (var field in fields)
        {
            // 必填校验
            if (field.Required)
            {
                if (!data.TryGetValue(field.Key, out var value) ||
                    value.ValueKind == JsonValueKind.Null ||
                    (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
                {
                    errors.Add($"字段[{field.Label}]为必填项");
                    continue;
                }
            }

            // 类型校验
            if (data.TryGetValue(field.Key, out var fieldValue) && fieldValue.ValueKind != JsonValueKind.Null)
            {
                if (!ValidateFieldType(field.Type, fieldValue))
                {
                    errors.Add($"字段[{field.Label}]类型不匹配，期望{field.Type}");
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    public string GenerateTitle(string template, string dataJson, string flowName, string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(template))
            return $"{flowName}-{cardNumber}";

        var result = template
            .Replace("${flowName}", flowName)
            .Replace("${cardNo}", cardNumber);

        // 替换 {fieldKey} 变量
        Dictionary<string, JsonElement>? data = null;
        try
        {
            data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* ignore */ }

        if (data != null)
        {
            result = Regex.Replace(result, @"\{(\w+)\}", match =>
            {
                var key = match.Groups[1].Value;
                if (data.TryGetValue(key, out var val))
                {
                    return val.ValueKind == JsonValueKind.String
                        ? val.GetString() ?? ""
                        : val.GetRawText();
                }
                return match.Value;
            });
        }

        // 截断80字符
        if (result.Length > 80)
            result = result[..80];

        return result;
    }

    private static bool ValidateFieldType(string fieldType, JsonElement value)
    {
        return fieldType.ToLowerInvariant() switch
        {
            "money" or "number" or "decimal" or "integer" =>
                value.ValueKind == JsonValueKind.Number ||
                (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out _)),
            "date" or "datetime" =>
                value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out _),
            "boolean" =>
                value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
            _ => true // text, enum, etc. accept any value
        };
    }
}
