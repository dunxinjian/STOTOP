using System.Text.Json;

namespace STOTOP.Module.CardFlow.Models.Schema;

/// <summary>
/// 发布前卡片组件校验：镜像前端 buildCapabilityProps / resolveComponentCapability 的“拒绝面”，
/// 封死直调 API 绕过前端门禁发布 deferred/template/占位组件或非法绑定来源。
/// 能力真源仍在前端能力表，本类只做拒绝，不做“可发布”判定；解析复用 <see cref="CardSchemaReader"/>。
/// </summary>
public static class CardComponentPublishValidator
{
    // 与前端 DEFERRED_PLACEHOLDER_CONTROL_KINDS 对齐：type=placeholderControl 且缺 publishable 的存量占位组件兜底
    private static readonly HashSet<string> DeferredControlKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "formula", "columnLayout", "aiAssist", "serialNumber", "ocrText", "componentSuite",
    };

    // 与前端 componentBindingText 支持的绑定来源对齐
    private static readonly HashSet<string> AllowedBindingSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "cardField", "detailTable", "detailSummary", "relation", "snapshot", "static",
    };

    /// <summary>返回中文错误列表；空列表表示通过。</summary>
    public static List<string> Validate(string? cardSchemaJson)
    {
        var errors = new List<string>();
        var schema = CardSchemaReader.ReadSchema(cardSchemaJson);
        foreach (var comp in schema.Components)
        {
            var title = !string.IsNullOrWhiteSpace(comp.Title) ? comp.Title
                : !string.IsNullOrWhiteSpace(comp.Id) ? comp.Id
                : string.IsNullOrWhiteSpace(comp.Type) ? "未命名组件" : comp.Type;

            var publishable = AsBool(GetProp(comp.Props, "publishable"));
            var status = AsString(GetProp(comp.Props, "componentStatus"));
            var controlKind = AsString(GetProp(comp.Props, "controlKind"));

            if (publishable == false)
            {
                var reason = AsString(GetProp(comp.Props, "unsupportedReason"));
                errors.Add($"组件[{title}]暂未支持发布{(string.IsNullOrWhiteSpace(reason) ? "" : "：" + reason)}");
            }
            else if (string.Equals(status, "deferred", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"组件[{title}]为暂缓组件，请移除或替换后再发布");
            }
            else if (string.Equals(status, "template", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"组件[{title}]为模板占位组件，不可直接发布");
            }
            else if (string.Equals(comp.Type, "placeholderControl", StringComparison.OrdinalIgnoreCase)
                     && controlKind is not null && DeferredControlKinds.Contains(controlKind))
            {
                errors.Add($"组件[{title}]为暂缓占位组件（{controlKind}），不可发布");
            }

            var source = comp.Binding?.Source;
            if (!string.IsNullOrWhiteSpace(source) && !AllowedBindingSources.Contains(source))
            {
                errors.Add($"组件[{title}]绑定来源非法：{source}");
            }
        }
        return errors;
    }

    private static object? GetProp(Dictionary<string, object?>? props, string key)
        => props is not null && props.TryGetValue(key, out var v) ? v : null;

    private static string? AsString(object? o) => o switch
    {
        string s => s,
        JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString(),
        _ => null,
    };

    private static bool? AsBool(object? o) => o switch
    {
        bool b => b,
        JsonElement e when e.ValueKind == JsonValueKind.True => true,
        JsonElement e when e.ValueKind == JsonValueKind.False => false,
        _ => null,
    };
}
