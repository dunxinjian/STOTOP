using STOTOP.Core.Models;

namespace STOTOP.Infrastructure.Data;

/// <summary>
/// R8 数据范围查询扩展（多租户阶段2D）。<b>刻意不进全局过滤器</b>——可视域随用户/动作变，须逐查询 opt-in。
/// 用法：先由 IScopeGrantService.GetVisibleNodeIdsAsync 取可视节点集，再 <c>query.ApplyVisibilityScope(ids)</c>。
/// <para>
/// 注意：fail-open 风险——列表/报表查询若忘记调用本扩展，将只受"租户硬墙 + 单节点组织过滤器"约束、不受 R8 收窄。
/// 生产查询接入须逐一 opt-in;单租户过渡期可视域退化为整棵租户树(近乎 no-op),接入安全。
/// </para>
/// </summary>
public static class ScopeQueryExtensions
{
    /// <summary>把查询收窄到可视组织节点集（<paramref name="visibleNodeIds"/> 由 R8 引擎算出）。</summary>
    public static IQueryable<T> ApplyVisibilityScope<T>(this IQueryable<T> query, IReadOnlyCollection<long> visibleNodeIds)
        where T : class, IOrgScoped
        => query.Where(e => visibleNodeIds.Contains(e.FOrgId));
}
