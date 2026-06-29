namespace STOTOP.Module.Quality.Services.Unification;

/// <summary>
/// 网点解析结果。
/// </summary>
/// <param name="Code">命中的网点编号；未匹配为 null。</param>
/// <param name="NameRaw">解析时传入的网点名称原文（回显用）。</param>
/// <param name="Status">0=未匹配 1=已匹配。</param>
public record NetworkMatch(string? Code, string? NameRaw, int Status);

/// <summary>
/// 员工解析结果。
/// </summary>
/// <param name="EmployeeNo">命中的员工工号；未匹配为 null。</param>
/// <param name="EmployeeId">命中的员工ID；未匹配为 null。</param>
/// <param name="NameRaw">解析时传入的员工姓名原文（回显用）。</param>
/// <param name="Status">0=未匹配 1=工号命中 2=别名命中 3=启发式候选(仅建议) 9=不适用。</param>
public record EmployeeMatch(string? EmployeeNo, long? EmployeeId, string? NameRaw, int Status);

/// <summary>
/// 主数据匹配器：把 STG/质量事件中的网点/员工脏名归一到 Express 主数据。
/// 全程按 orgId 显式过滤（不依赖全局组织查询过滤器）。
/// </summary>
public interface IMasterDataMatcher
{
    /// <summary>
    /// 网点解析（4 级回退）：编码 → 简称/全称 → 别名 → 未匹配。
    /// </summary>
    Task<NetworkMatch> ResolveNetworkAsync(string? code, string? name, long orgId, CancellationToken ct = default);

    /// <summary>
    /// 员工解析（4 级回退）：工号(1) → 别名(2) → 启发式网点内唯一命中(3，仅建议) → 未匹配(0)。
    /// </summary>
    Task<EmployeeMatch> ResolveEmployeeAsync(string? employeeNo, string? nameRaw, string? networkCode, long orgId, CancellationToken ct = default);
}
