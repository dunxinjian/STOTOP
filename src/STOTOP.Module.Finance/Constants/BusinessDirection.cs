namespace STOTOP.Module.Finance.Constants;

/// <summary>
/// 业务方向编码。阿米巴出港/进港分流的 business_direction 辅助核算取值,
/// 全系统统一用 OUT/IN/CMB,禁用中文(避免与模板 Tab 名/科目名耦合)。
/// </summary>
public static class BusinessDirection
{
    public const string Outbound = "OUT";  // 出港
    public const string Inbound = "IN";    // 进港
    public const string Combined = "CMB";  // 综合/公共(房租水电等不分段的公共费)

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Outbound, Inbound, Combined };

    /// <summary>中文 → 编码(落库/导入翻译用)</summary>
    public static readonly IReadOnlyDictionary<string, string> CnToCode = new Dictionary<string, string>
    {
        ["出港"] = Outbound,
        ["进港"] = Inbound,
        ["综合"] = Combined,
        ["公共"] = Combined,
    };

    /// <summary>编码 → 中文(报表展示用)</summary>
    public static readonly IReadOnlyDictionary<string, string> CodeToCn = new Dictionary<string, string>
    {
        [Outbound] = "出港",
        [Inbound] = "进港",
        [Combined] = "综合",
    };

    public static bool IsValid(string? code) => code != null && All.Contains(code);

    /// <summary>把中文或编码统一规整为编码;无法识别则原样返回。</summary>
    public static string Normalize(string raw)
    {
        var t = raw.Trim();
        return CnToCode.TryGetValue(t, out var code) ? code : t;
    }
}
