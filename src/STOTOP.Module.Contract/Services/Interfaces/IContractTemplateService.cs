using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;

namespace STOTOP.Module.Contract.Services.Interfaces;

public interface IContractTemplateService
{
    Task<PagedResult<ContractTemplateListItemDto>> GetTemplatesAsync(ContractTemplateQueryRequest request);
    Task<ContractTemplateDto?> GetTemplateByIdAsync(long id);
    Task<ContractTemplateDto> CreateTemplateAsync(CreateContractTemplateRequest request);
    Task<ContractTemplateDto?> UpdateTemplateAsync(long id, UpdateContractTemplateRequest request);
    Task<bool> DeleteTemplateAsync(long id);
    Task<bool> PublishTemplateAsync(long id);
}
