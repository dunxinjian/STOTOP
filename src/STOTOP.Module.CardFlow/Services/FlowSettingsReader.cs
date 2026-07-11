using System.Text.Json;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 读 CfFlowVersion.FFlowSettingsJson 里的布尔型定义级开关（如 skipDuplicateApprover / allowInitiatorRevoke）。
/// 缺键 / 非对象 / 非布尔 / 非法 JSON 一律静默返回 defaultValue —— 与 FlowEngineService.GetResubmitStrategy 同款容错。
/// </summary>
public static class FlowSettingsReader
{
    public static bool ReadBool(string? flowSettingsJson, string propertyName, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(flowSettingsJson)) return defaultValue;
        try
        {
            using var doc = JsonDocument.Parse(flowSettingsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(propertyName, out var v)
                && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
            {
                return v.GetBoolean();
            }
        }
        catch (JsonException) { /* 静默降级 */ }
        return defaultValue;
    }
}
