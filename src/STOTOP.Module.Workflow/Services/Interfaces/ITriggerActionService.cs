using STOTOP.Module.Workflow.DTOs;

namespace STOTOP.Module.Workflow.Services.Interfaces;

public interface ITriggerActionService
{
    /// <summary>获取当前用户可用的触发动作列表</summary>
    Task<List<TriggerActionDto>> GetAvailableActionsAsync(long userId, long orgId);

    /// <summary>获取所有动作（管理用）</summary>
    Task<List<TriggerActionDto>> GetAllActionsAsync();

    /// <summary>切换启用状态</summary>
    Task ToggleAsync(long actionId);
}
