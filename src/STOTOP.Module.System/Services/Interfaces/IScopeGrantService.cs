using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services.Interfaces;

/// <summary>R8 数据范围引擎（多租户阶段2D，§7）。</summary>
public interface IScopeGrantService
{
    /// <summary>重算某用户的派生授权（§7.2）：删旧派生 → 从当前可放大任职的物化范围根算 → 集团级归一 → 写 Read 授权。</summary>
    Task RecomputeScopeGrantsAsync(long userId, long tenantId);

    /// <summary>可视节点集（§7.3）：授权过硬墙 → 集团级=整租户树,否则经闭包展开范围节点子树 + 租户二次夹逼；空=fail-closed。</summary>
    Task<IReadOnlyCollection<long>> GetVisibleNodeIdsAsync(long userId, long tenantId, ScopeAction action);

    /// <summary>手工授权（§7.4/D6）：(Write/All,集团) 须挂二人复核审批单 FApprovalId,否则拒。</summary>
    Task AddManualGrantAsync(SysScopeGrant grant);
}
