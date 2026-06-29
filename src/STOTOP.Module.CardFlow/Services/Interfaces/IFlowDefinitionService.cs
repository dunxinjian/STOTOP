using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IFlowDefinitionService
{
    Task<PagedResult<FlowDefinitionDto>> GetListAsync(FlowDefinitionQueryRequest request);
    Task<FlowDefinitionDto?> GetByIdAsync(long id);
    Task<FlowDefinitionDto> CreateAsync(CreateFlowDefinitionRequest request, long operatorId);
    Task<FlowDefinitionDto> UpdateAsync(long id, UpdateFlowDefinitionRequest request, long operatorId);
    Task PublishAsync(long id, long operatorId);
    Task ArchiveAsync(long id, long operatorId);
    Task DisableAsync(long id, long operatorId);
    Task EnableAsync(long id, long operatorId);
    Task<List<FlowVersionDto>> GetVersionsAsync(long definitionId);
    Task<FlowVersionDetailDto?> GetVersionDetailAsync(long definitionId, long versionId);
    Task<FlowVersionDetailDto> SaveDraftVersionAsync(long definitionId, SaveDraftVersionRequest request, long operatorId);
    Task<FlowVersionDetailDto?> GetDraftVersionAsync(long definitionId);
    Task<FlowDefinitionDto> CloneFlowDefinitionAsync(long sourceDefinitionId, CloneFlowDefinitionRequest request, long operatorId);
    Task<List<FlowDefinitionDto>> GetTemplatesAsync();
    Task<FlowDefinitionDto> SaveAsTemplateAsync(long sourceDefinitionId, long operatorId);
}
