using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;

namespace STOTOP.Module.Insurance.Services.Interfaces;

public interface IInsCompanyService
{
    Task<PagedResult<InsCompanyListItemDto>> GetListAsync(InsCompanyQueryRequest request);
    Task<InsCompanyDto?> GetByIdAsync(long id);
    Task<InsCompanyDto> CreateAsync(CreateInsCompanyRequest request);
    Task<InsCompanyDto?> UpdateAsync(long id, UpdateInsCompanyRequest request);
    Task<bool> ToggleStatusAsync(long id);
}
