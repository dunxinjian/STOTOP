namespace STOTOP.Core.Services;

/// <summary>
/// per-tenant 后台迭代地基（阶段4 收尾）：遍历所有活跃平台租户（PLT租户 F状态∈{试用,正式,欠费冻结}，跳停用），
/// 逐个经 <see cref="ITenantScopeFactory"/> 固化租户上下文后运行 action。
/// <para>
/// 用途：把后台 Hangfire Job 原来的"设根租户后处理全库"，升级为"逐活跃租户各处理一遍"，实现多客户 per-tenant 迭代。
/// </para>
/// <para>
/// 向后兼容：单客户下 PLT租户 只有 1 行（= <see cref="ITenantResolver.GetRootTenantId"/> 那个），
/// 故只循环 1 次、上下文与现状一致，行为不变。PLT租户 表未建 / 空表时回退单租户，Job 不空转。
/// </para>
/// <para>
/// 隔离：每租户 try/catch 独立，一个租户失败不中断其它（照 ShentongUnificationJob 的 per-org 隔离范式）。
/// </para>
/// <para>
/// 冻结（D7 决策：照跑）：欠费冻结（F状态=4）只在 HTTP 中间件拦用户业务写 + 批量导出；后台 Job 是系统写、不经该中间件，
/// 故迭代仍覆盖冻结租户，保证账目 / 超时等系统正确性不因欠费而中断。
/// </para>
/// </summary>
public interface ITenantIterationService
{
    /// <summary>
    /// 遍历活跃租户，逐个固化租户上下文并运行 <paramref name="action"/>（入参 = 当前租户 FID）。
    /// </summary>
    /// <param name="action">对单个租户执行的业务逻辑（在该租户上下文内，读写自动收敛到该租户）。</param>
    /// <param name="reason">迭代原因（日志 / 作用域标识，建议传 Job id）。</param>
    Task ForEachActiveTenantAsync(Func<long, Task> action, string reason = "tenant-iteration");
}
