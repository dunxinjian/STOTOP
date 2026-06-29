using System.Text.Json;

namespace STOTOP.Module.Finance.Services.Auxiliary;

/// <summary>
/// 凭证/估值分录辅助核算校验:分录实带的辅助核算维度是否覆盖科目契约
/// (<c>FinAccount.FAuxiliary</c>,见 <see cref="AccountAuxContract"/>)声明的维度。纯函数。
/// 抽键口径与 <c>AmoebaPLService.ParseAuxiliaryJson/ParseAuxElement</c>(读元素 "type")严格一致,
/// 兼容 FAuxiliaryJson 的 Array(<c>[{type,code,name}]</c>)与 Object 两种历史形态,
/// 避免校验口径与报表过滤口径分叉。
/// </summary>
public static class EntryAuxValidator
{
    /// <summary>返回科目声明但分录未带的辅助核算维度码(空=合规)。</summary>
    public static IReadOnlyList<string> GetMissing(string? fAuxiliary, string? auxiliaryJson)
        => AccountAuxContract.GetMissingAuxTypes(fAuxiliary, ParseAuxTypeKeys(auxiliaryJson));

    /// <summary>从分录 FAuxiliaryJson 抽取已带的辅助核算类型键集合(每个元素的 "type")。</summary>
    public static IReadOnlySet<string> ParseAuxTypeKeys(string? auxiliaryJson)
    {
        var keys = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(auxiliaryJson)) return keys;
        try
        {
            using var doc = JsonDocument.Parse(auxiliaryJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                    AddType(elem, keys);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddType(doc.RootElement, keys);
            }
        }
        catch { /* 格式非法交由上游 JSON 格式校验处理,这里不抛 */ }
        return keys;
    }

    private static void AddType(JsonElement elem, HashSet<string> keys)
    {
        if (elem.ValueKind == JsonValueKind.Object
            && elem.TryGetProperty("type", out var t)
            && t.ValueKind == JsonValueKind.String)
        {
            var type = t.GetString();
            if (!string.IsNullOrEmpty(type)) keys.Add(type);
        }
    }
}
