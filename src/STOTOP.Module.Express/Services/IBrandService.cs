using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IBrandService
{
    Task<PagedResult<BrandListItemDto>> GetListAsync(BrandQueryRequest request);
    Task<BrandDto?> GetByCodeAsync(string code);
    Task<BrandDto> CreateAsync(CreateBrandRequest request);
    Task<BrandDto?> UpdateAsync(string code, UpdateBrandRequest request);
    Task<bool> DeleteAsync(string code);
}
