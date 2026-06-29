using System.Globalization;

namespace STOTOP.Module.Quality.Services.Unification;

/// <summary>
/// 归一阶段的轻量解析助手：把 STG 表里以字符串落地的业务列（日期/小数/整数/布尔）安全转成强类型。
/// 全部容错（解析失败返回 null/false），不抛异常——脏数据是常态，归一不应因单格解析失败而中断。
/// </summary>
public static class ParseUtil
{
    /// <summary>
    /// 尝试解析日期；失败返回 null。
    /// 先走通用 <see cref="DateTime.TryParse(string?, out DateTime)"/>（覆盖 2026-06-15 / 2026/6/15 等带分隔格式），
    /// 再退化按紧凑 yyyyMMdd（如申通积压监控的「20260615」，通用 TryParse 解不出）精确解析。
    /// </summary>
    public static DateTime? TryDate(string? s)
    {
        if (DateTime.TryParse(s, out var d)) return d;
        if (DateTime.TryParseExact((s ?? "").Trim(), "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2)) return d2;
        return null;
    }

    /// <summary>
    /// 尝试解析小数；失败返回 null（全部容错，不抛异常）。
    /// 率以「百分数数值」口径存（不除以 100），按尾缀折算到与百分号同一标度：
    ///   尾「‱」万分号(U+2031) → 去尾解析后 ×0.01（6.4‱ = 0.064% → 0.064）；
    ///   尾「‰」千分号(U+2030) → 去尾解析后 ×0.1 （5‰   = 0.5%   → 0.5）；
    ///   尾「%」百分号 或 无尾缀的纯数值 → ×1（仅去符号、不除 100；积压倍数/遗失率ppm 等无尾缀走原样解析，不受影响）。
    /// </summary>
    public static decimal? TryDecimal(string? s)
    {
        var t = (s ?? "").Trim();
        if (t.EndsWith('‱'))
            return decimal.TryParse(t[..^1].Trim(), out var w) ? w * 0.01m : (decimal?)null;
        if (t.EndsWith('‰'))
            return decimal.TryParse(t[..^1].Trim(), out var p) ? p * 0.1m : (decimal?)null;
        // 百分号仅去符号、不除 100；无尾缀纯数值原样解析。
        return decimal.TryParse(t.TrimEnd('%'), out var v) ? v : (decimal?)null;
    }

    /// <summary>尝试解析整数；整数失败时退化为小数后截断；再失败返回 null。</summary>
    public static int? TryInt(string? s) =>
        int.TryParse(s, out var v) ? v : (decimal.TryParse(s, out var dv) ? (int)dv : (int?)null);

    /// <summary>真值判定：是/1/true/True 视为真，其余为假。</summary>
    public static bool Truthy(string? s) => s is "是" or "1" or "true" or "True";

    /// <summary>把日期格式化为 yyyyMM 统计月；null 入参返回 null。</summary>
    public static string? Ym(DateTime? d) => d?.ToString("yyyyMM", CultureInfo.InvariantCulture);
}
