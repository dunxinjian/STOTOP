using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IFlowGroupService
{
    Task<List<FlowGroupDto>> GetListAsync(long orgId);
    Task<FlowGroupDto> CreateAsync(CreateFlowGroupRequest request, long operatorId);
    Task<FlowGroupDto> UpdateAsync(long id, UpdateFlowGroupRequest request, long operatorId);
    Task DeleteAsync(long id, long operatorId);
    Task<List<FlowGroupLinkDto>> GetLinksAsync(long groupId);
    Task SaveLinksAsync(long groupId, List<SaveFlowGroupLinkRequest> links, long operatorId);
    Task EvaluateTriggersAsync(long cardId);
}
