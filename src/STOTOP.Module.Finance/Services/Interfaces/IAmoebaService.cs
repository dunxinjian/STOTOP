using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IAmoebaService
{
    // 损益模板
    Task<List<AmoebaPLTemplateDto>> GetTemplatesAsync(long? accountSetId = null);
    Task<AmoebaPLTemplateDto?> GetTemplateByIdAsync(long id);
    Task<AmoebaPLTemplateDto> CreateTemplateAsync(CreateAmoebaPLTemplateRequest request);
    Task<AmoebaPLTemplateDto?> UpdateTemplateAsync(long id, UpdateAmoebaPLTemplateRequest request);
    Task<bool> DeleteTemplateAsync(long id);
    Task<AmoebaPLTemplateDto> CloneTemplateAsync(long sourceId, CloneAmoebaPLTemplateRequest request);
    
    // 损益项
    Task<AmoebaPLItemDto> AddItemAsync(long templateId, CreateAmoebaPLItemRequest request);
    Task<AmoebaPLItemDto?> UpdateItemAsync(long templateId, long itemId, UpdateAmoebaPLItemRequest request);
    Task<bool> DeleteItemAsync(long templateId, long itemId);
    Task<bool> ReorderItemsAsync(long templateId, List<ReorderAmoebaPLItemRequest> items);
    Task<AmoebaPLItemDto> CloneItemFromTemplateAsync(long targetTemplateId, CloneAmoebaPLItemRequest request);
}
