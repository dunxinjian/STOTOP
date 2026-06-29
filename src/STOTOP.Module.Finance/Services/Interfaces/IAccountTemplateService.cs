using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IAccountTemplateService
{
    Task<List<AccountTemplateDto>> GetTemplatesAsync();
    Task<AccountTemplateDetailDto> GetTemplateDetailAsync(long id);
    Task<long> CreateTemplateAsync(CreateAccountTemplateRequest request);
    Task UpdateTemplateAsync(long id, UpdateAccountTemplateRequest request);
    Task DeleteTemplateAsync(long id);
    Task<List<AccountTemplateItemTreeDto>> GetTemplateItemsTreeAsync(long id);
    Task<AccountTemplateItemDto> AddTemplateItemAsync(long templateId, CreateTemplateItemRequest request);
    Task UpdateTemplateItemAsync(long templateId, long itemId, UpdateTemplateItemRequest request);
    Task DeleteTemplateItemAsync(long templateId, long itemId);
    Task ApplyTemplateAsync(long templateId, long accountSetId);
}
