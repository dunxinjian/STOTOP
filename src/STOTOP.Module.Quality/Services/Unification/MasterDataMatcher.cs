using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Quality.Services.Unification;

/// <summary>
/// 主数据匹配器实现。查询全程 <c>IgnoreQueryFilters()</c> + 显式 <c>FOrgId == orgId</c>，
/// 不依赖全局组织过滤器（统一质控可能在批处理/无组织上下文下运行）。
/// </summary>
public class MasterDataMatcher : IMasterDataMatcher
{
    private readonly STOTOPDbContext _db;

    public MasterDataMatcher(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<NetworkMatch> ResolveNetworkAsync(string? code, string? name, long orgId, CancellationToken ct = default)
    {
        // ① 编码命中（同 orgId）
        if (!string.IsNullOrWhiteSpace(code))
        {
            var byCode = await _db.Set<ExpNetworkPoint>()
                .IgnoreQueryFilters()
                .AnyAsync(np => np.FCode == code && np.FOrgId == orgId, ct);
            if (byCode)
                return new NetworkMatch(code, name, 1);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim();

            // ② 简称/全称精确命中（同 orgId）
            var byName = await _db.Set<ExpNetworkPoint>()
                .IgnoreQueryFilters()
                .Where(np => np.FOrgId == orgId && (np.FShortName == n || np.FFullName == n))
                .Select(np => np.FCode)
                .FirstOrDefaultAsync(ct);
            if (byName != null)
                return new NetworkMatch(byName, name, 1);

            // ③ 别名命中（同 orgId）
            var byAlias = await _db.Set<ExpNetworkPointAlias>()
                .IgnoreQueryFilters()
                .Where(a => a.FName == n && a.FOrgId == orgId)
                .Select(a => a.FNetworkPointCode)
                .FirstOrDefaultAsync(ct);
            if (byAlias != null)
                return new NetworkMatch(byAlias, name, 1);
        }

        // ④ 未匹配
        return new NetworkMatch(null, name, 0);
    }

    public async Task<EmployeeMatch> ResolveEmployeeAsync(string? employeeNo, string? nameRaw, string? networkCode, long orgId, CancellationToken ct = default)
    {
        // ① 工号命中（同 orgId，按所属网点的组织判定）
        if (!string.IsNullOrWhiteSpace(employeeNo))
        {
            var hit = await ResolveByEmployeeNoAsync(employeeNo, orgId, ct);
            if (hit != null)
                return new EmployeeMatch(hit.Value.no, hit.Value.id, nameRaw, 1);
        }

        if (!string.IsNullOrWhiteSpace(nameRaw))
        {
            var raw = nameRaw.Trim();

            // ② 别名命中（同 orgId）→ 取工号 → 查主数据拿 FEmployeeId
            var aliasNo = await _db.Set<ExpSalesmanAlias>()
                .IgnoreQueryFilters()
                .Where(a => a.FName == raw && a.FOrgId == orgId)
                .Select(a => a.FEmployeeNo)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(aliasNo))
            {
                var byAlias = await ResolveByEmployeeNoAsync(aliasNo, orgId, ct);
                if (byAlias != null)
                    return new EmployeeMatch(byAlias.Value.no, byAlias.Value.id, nameRaw, 2);
            }

            // ③ 启发式：清洗 → 网点内唯一命中（候选，仅建议）
            var candidate = CleanEmployeeName(raw);
            if (!string.IsNullOrWhiteSpace(candidate) && !string.IsNullOrWhiteSpace(networkCode))
            {
                var hits = await _db.Set<ExpSalesman>()
                    .IgnoreQueryFilters()
                    .Where(s => s.FNetworkPointCode == networkCode && s.FName == candidate)
                    .Select(s => new { s.FEmployeeNo, s.FEmployeeId })
                    .Take(2)
                    .ToListAsync(ct);
                if (hits.Count == 1)
                    return new EmployeeMatch(hits[0].FEmployeeNo, hits[0].FEmployeeId, nameRaw, 3);
                // 多于一条或无 → 不绑
            }
        }

        // ④ 未匹配
        return new EmployeeMatch(null, null, nameRaw, 0);
    }

    /// <summary>
    /// 按工号查 ExpSalesman 并校验其所属网点归属 orgId（网点为组织扩展实体，按 FOrgId 判定）。
    /// </summary>
    private async Task<(string no, long id)?> ResolveByEmployeeNoAsync(string employeeNo, long orgId, CancellationToken ct)
    {
        var s = await _db.Set<ExpSalesman>()
            .IgnoreQueryFilters()
            .Where(x => x.FEmployeeNo == employeeNo)
            .Select(x => new { x.FEmployeeNo, x.FEmployeeId, x.FNetworkPointCode })
            .FirstOrDefaultAsync(ct);
        if (s == null)
            return null;

        // 校验业务员所属网点属于该 orgId（ExpSalesman 本身不带 FOrgId）
        var orgOk = await _db.Set<ExpNetworkPoint>()
            .IgnoreQueryFilters()
            .AnyAsync(np => np.FCode == s.FNetworkPointCode && np.FOrgId == orgId, ct);
        if (!orgOk)
            return null;

        return (s.FEmployeeNo, s.FEmployeeId);
    }

    // 前缀：城区 / 操作部 / 机动（可重复出现在开头）
    private static readonly Regex LeadingPrefix = new(@"^(城区|操作部|机动)+", RegexOptions.Compiled);
    // 11 位手机号（1 开头）
    private static readonly Regex Mobile = new(@"1\d{10}", RegexOptions.Compiled);
    // 尾部连续数字
    private static readonly Regex TrailingDigits = new(@"\d+$", RegexOptions.Compiled);

    /// <summary>
    /// 清洗员工姓名原文，得到候选姓名：去 11 位手机号 → 去前缀 → 去尾部连续数字。
    /// </summary>
    internal static string CleanEmployeeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        s = Mobile.Replace(s, string.Empty);        // 先去手机号（11 位），避免被尾部数字逻辑切碎
        s = LeadingPrefix.Replace(s, string.Empty); // 去开头 城区/操作部/机动
        s = TrailingDigits.Replace(s, string.Empty); // 去尾部连续数字（工号尾巴等）
        return s.Trim();
    }
}
