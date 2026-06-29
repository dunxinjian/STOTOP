using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IFranchiseAreaService
{
    Task<PagedResult<FranchiseAreaDto>> GetListAsync(FranchiseAreaQueryRequest request);
    Task<FranchiseAreaDto?> GetByIdAsync(string code);
    Task<bool> CheckCodeExistsAsync(string code);
    Task<FranchiseAreaDto> CreateAsync(CreateFranchiseAreaRequest request);
    Task<FranchiseAreaDto?> UpdateAsync(string code, UpdateFranchiseAreaRequest request);
    Task<bool> DeleteAsync(string code);
}
