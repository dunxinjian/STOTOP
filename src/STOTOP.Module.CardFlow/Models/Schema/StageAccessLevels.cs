namespace STOTOP.Module.CardFlow.Models.Schema;

/// <summary>
/// 节点视图/呈现层访问级别的单一 fail-closed 真源。原先 CardPresentationResolver（大小写敏感、未知→readonly 放行）、
/// CardRedactionService（fail-closed、未知→masked）、StageViewProfileResolver（散点 OrdinalIgnoreCase 比较）三处语义不一，
/// 统一到此：Trim + 忽略大小写归一，未知/拼错/null/空一律按 masked（不得默认成明文可见）。
/// 保留 required 语义（呈现层需区分必填），可见性判定见 IsHidden/IsMasked/IsEditable。
/// </summary>
public static class StageAccessLevels
{
    public const string Hidden = "hidden";
    public const string Masked = "masked";
    public const string Readonly = "readonly";
    public const string Editable = "editable";
    public const string Required = "required";

    public static string Normalize(string? access) => access?.Trim().ToLowerInvariant() switch
    {
        Hidden => Hidden,
        Masked => Masked,
        Readonly => Readonly,
        Editable => Editable,
        Required => Required,
        // 未知/拼错/null/空 一律按最严处理（fail-closed），不得默认成明文可见
        _ => Masked
    };

    public static bool IsHidden(string? access) => Normalize(access) == Hidden;

    public static bool IsMasked(string? access) => Normalize(access) == Masked;

    /// <summary>可编辑（editable 或 required）。</summary>
    public static bool IsEditable(string? access)
    {
        var normalized = Normalize(access);
        return normalized == Editable || normalized == Required;
    }

    public static bool IsRequired(string? access) => Normalize(access) == Required;
}
