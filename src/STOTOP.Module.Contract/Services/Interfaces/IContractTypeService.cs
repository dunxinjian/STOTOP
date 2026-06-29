using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;

namespace STOTOP.Module.Contract.Services.Interfaces;

public interface IContractTypeService
{
    Task<PagedResult<ContractTypeDto>> GetTypesAsync(ContractTypeQueryRequest request);
    Task<List<ContractTypeDto>> GetAllEnabledTypesAsync();
    Task<ContractTypeDto?> GetTypeByIdAsync(long id);
    Task<ContractTypeDto> CreateTypeAsync(CreateContractTypeRequest request);
    Task<ContractTypeDto?> UpdateTypeAsync(long id, UpdateContractTypeRequest request);
    Task<bool> DeleteTypeAsync(long id);
    Task<bool> UpdateStatusAsync(long id, int status);
}
