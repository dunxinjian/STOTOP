using System.Text.Json;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class StageConfigParser : IStageConfigParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StageConfigEnvelope Parse(string? inputFieldsJson)
    {
        if (string.IsNullOrWhiteSpace(inputFieldsJson))
        {
            return new StageConfigEnvelope();
        }

        try
        {
            using var document = JsonDocument.Parse(inputFieldsJson);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return new StageConfigEnvelope
                {
                    Version = 1,
                    InputFields = ReadStringArray(root)
                };
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("version", out var versionProperty)
                && versionProperty.ValueKind == JsonValueKind.Number
                && versionProperty.GetInt32() == 2)
            {
                var envelope = JsonSerializer.Deserialize<StageConfigEnvelope>(inputFieldsJson, JsonOptions)
                    ?? new StageConfigEnvelope();
                envelope.Version = 2;
                envelope.InputFields ??= new List<string>();
                envelope.Warnings ??= new List<string>();
                return envelope;
            }

            return new StageConfigEnvelope
            {
                Warnings = { "Unsupported stage config JSON shape; treated as empty v1 config." }
            };
        }
        catch (JsonException ex)
        {
            return new StageConfigEnvelope
            {
                Warnings = { $"Invalid stage config JSON; treated as empty v1 config. {ex.Message}" }
            };
        }
    }

    private static List<string> ReadStringArray(JsonElement element)
    {
        var result = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }
}
