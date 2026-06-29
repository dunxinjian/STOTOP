namespace STOTOP.Module.CardFlow.Services.Redaction;

/// <summary>卡片字段打码的唯一真源，前后端口径以此为准。</summary>
public static class FieldMasker
{
    public static string Mask(string? value, string? pattern)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var v = value!;
        switch ((pattern ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "phone":
                return v.Length > 7 ? $"{v[..3]}****{v[^4..]}" : Generic(v);
            case "idcard":
                return v.Length > 8 ? $"{v[..4]}**********{v[^4..]}" : Generic(v);
            case "bankcard":
                return v.Length > 4 ? $"**** **** **** {v[^4..]}" : "****";
            case "email":
                var at = v.IndexOf('@');
                return at >= 1 ? $"{v[0]}***{v[at..]}" : Generic(v);
            case "name":
                return v.Length <= 1 ? v : $"{v[..1]}{new string('*', v.Length - 1)}";
            default:
                return Generic(v);
        }
    }

    // 短值整体打码，避免暴露多数字符。
    private static string Generic(string v) => v.Length <= 7 ? "****" : $"{v[..2]}****{v[^2..]}";
}
