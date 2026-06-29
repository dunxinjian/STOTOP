namespace STOTOP.Module.Quality.Services.Unification;

/// <summary>
/// 归一一次的结果计数。
/// </summary>
/// <param name="EventsUpserted">写入/更新的质量事件行数（事件类源贡献；指标类源为 0）。</param>
/// <param name="EmployeeMetricUpserts">写入/更新的员工日指标行数（员工指标源贡献；其它源为 0）。</param>
/// <param name="NetworkMetricUpserts">写入/更新的网点日指标行数（网点指标源贡献；其它源为 0）。</param>
/// <param name="NetworkUnmatched">网点未匹配（Status==0）的事件数。</param>
/// <param name="EmployeeUnmatched">员工未确定绑定（Status∈{0,3}）的事件数。</param>
public record UnifyResult(
    int EventsUpserted,
    int EmployeeMetricUpserts,
    int NetworkMetricUpserts,
    int NetworkUnmatched,
    int EmployeeUnmatched);

/// <summary>
/// 重跑回填一次的结果计数（仅重解析未匹配历史事件的主数据，不重建事件）。
/// </summary>
/// <param name="NetworkRebound">本次由未匹配(0) 重解析为已匹配(1) 并回填网点编码的事件数。</param>
/// <param name="EmployeeRebound">本次由未匹配/启发式(0/3) 重解析为确定绑定(1/2) 并回填工号/ID 的事件数。</param>
/// <param name="Scanned">本次扫描的「待重解析」候选事件数（网点状态0 或 员工状态0/3）。</param>
public record RematchResult(
    int NetworkRebound,
    int EmployeeRebound,
    int Scanned);

/// <summary>
/// 申通归一服务：把各 STG 来源明细归一为统一质控的事实表（质量事件 / 网点·员工日指标）。
/// 全程显式带 orgId，主数据匹配（网点/员工脏名）落地，幂等（按唯一键 upsert）。
/// </summary>
public interface IQualityUnificationService
{
    /// <summary>
    /// 归一指定组织下的申通各源。三类目标（事件 / 网点日指标 / 员工日指标）均已落地，
    /// 覆盖 ShentongSourceMap 全部 29 源（按目标种类 + 表名分发处理）。
    /// </summary>
    global::System.Threading.Tasks.Task<UnifyResult> UnifyShentongAsync(long orgId, CancellationToken ct = default);

    /// <summary>
    /// 重跑回填：<b>仅重解析「未匹配」的历史质量事件主数据，不重建事件、不重导</b>。
    /// 典型用法——用户在「待认领」补了网点/员工别名后，对历史未匹配事件重跑，使其命中。
    /// <para>
    /// 扫描 (FOrgId==orgId &amp;&amp; F承运商=="申通") 且 (F网点匹配状态==0 或 F员工匹配状态∈{0,3}) 的事件，逐条：
    ///   网点未匹配(0)：用事件 F网点名称 原文重 ResolveNetworkAsync(null, 名称, orgId)，命中(1)→回填 F网点编码/F网点匹配状态；
    ///   员工未匹配/启发式(0/3)：用 F员工姓名原文（+ 非空 F员工工号）重 ResolveEmployeeAsync(...)，
    ///     现 Status∈{1,2}→回填 F员工工号/F员工ID/F员工匹配状态；仍 0/3 则不动（启发式候选仍不绑，保持只留姓名原文口径）。
    /// 只 update 变化的行。返回回填计数。幂等：再跑无回填。
    /// </para>
    /// </summary>
    global::System.Threading.Tasks.Task<RematchResult> RematchUnresolvedAsync(long orgId, CancellationToken ct = default);

    /// <summary>
    /// 枚举「有申通数据可归一」的组织ID（去重）：取质量事件表（已归一过的组织）∪ 各申通 STG 源表（已导入待归一的组织）的 distinct FOrgId。
    /// 供 Hangfire 每日 Job 逐组织跑归一用——只跑真正有数据的组织，避免对全部组织空转。
    /// 表名全部来自静态源映射（<see cref="ShentongSourceMap"/>，非用户输入），仅取存在的表（按 INFORMATION_SCHEMA 守卫）。
    /// </summary>
    global::System.Threading.Tasks.Task<IReadOnlyList<long>> ListShentongOrgIdsAsync(CancellationToken ct = default);
}
