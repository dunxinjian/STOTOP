using STOTOP.Module.Workflow.DTOs;

namespace STOTOP.Module.Workflow.Services.Interfaces;

public interface IWorkItemService
{
    // 创建工作项
    Task<WorkItemDto> CreateAsync(CreateWorkItemRequest request);

    // 获取工作项详情
    Task<WorkItemDto?> GetByIdAsync(long id);
    Task<WorkItemDto?> GetByUidAsync(string uid);

    // 查询我的待办（take 非空时 DB 层封顶取前 N 条，供 WorkHub 聚合避免全量取数）
    Task<List<WorkItemDto>> GetPendingItemsAsync(long userId, string? module = null, int? take = null);

    // 查询我的已办
    Task<List<WorkItemDto>> GetCompletedItemsAsync(long userId, int page = 1, int pageSize = 20);

    // 按链路查询
    Task<List<WorkItemDto>> GetByChainIdAsync(string chainId);

    // 状态流转
    // enforceAssignee=true（默认，用户发起的操作）会校验 operatorId 必须是工作项的指派人，否则抛 UnauthorizedAccessException；
    // 系统/编排内部流程（如批次撤销、问题归集）传 false 以放行。
    Task<WorkItemDto> AssignAsync(long workItemId, long assigneeId, string assigneeName);
    Task<WorkItemDto> StartAsync(long workItemId, long operatorId, bool enforceAssignee = true);
    Task<WorkItemDto> CompleteAsync(long workItemId, long operatorId, string? result = null, string? remark = null, bool enforceAssignee = true);
    Task<WorkItemDto> CancelAsync(long workItemId, long operatorId, string? reason = null, bool enforceAssignee = true);

    // 生命周期管理
    /// <summary>原子认领工作项（防竞态）</summary>
    Task ClaimAsync(long workItemId, long userId);
    /// <summary>WorkHub 手动关闭工作项</summary>
    Task DismissAsync(long workItemId, long userId);
    /// <summary>质量中心解决关闭工作项</summary>
    Task ResolveAsync(long workItemId);
    /// <summary>系统取消工作项（如重新计费时）</summary>
    Task CancelBySystemAsync(long workItemId);
    /// <summary>去重查询：是否存在活跃工作项</summary>
    Task<bool> HasActiveWorkItemAsync(long bizId, string bizType);
    /// <summary>获取指定业务关联的活跃工作项ID列表</summary>
    Task<List<long>> GetActiveWorkItemIdsAsync(long bizId, string bizType);
    /// <summary>设置派发警告</summary>
    Task SetDispatchWarningAsync(long workItemId, string warning);

    // 统计
    Task<WorkItemStatsDto> GetStatsAsync(long userId);

    /// <summary>
    /// 按 F类型 分组统计当前用户的待办工作项数（WorkHub 角标口径：F处理人ID==userId 且 F状态==Pending）。
    /// 返回 {FType: count}，供 WorkHub 按 type→category 映射摊入各角标。
    /// </summary>
    Task<Dictionary<int, int>> GetPendingCountByTypeAsync(long userId);
}
