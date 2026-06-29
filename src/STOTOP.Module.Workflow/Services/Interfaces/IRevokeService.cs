using STOTOP.Module.Workflow.DTOs;

namespace STOTOP.Module.Workflow.Services.Interfaces;

public interface IRevokeService
{
    // 步骤级撤销（撤销某个 WorkItem 的产出数据）
    Task<RevokeResultDto> RevokeStepAsync(long workItemId, long operatorId, string operatorName);

    // 链路级撤销（撤销整条链路的所有产出）
    Task<RevokeResultDto> RevokeChainAsync(string chainId, long operatorId, string operatorName);

    // 查询撤销日志
    Task<List<RevokeLogDto>> GetRevokeLogsAsync(string? chainId = null, string? dataScopeId = null);

    // 检查是否可撤销
    Task<bool> CanRevokeAsync(long workItemId);
}
