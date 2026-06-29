using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IShopService
{
    Task<PagedResult<ShopListItemDto>> GetListAsync(ShopQueryRequest request);
    Task<ShopDto?> GetByNameAsync(string name);
    Task<ShopDto> CreateAsync(CreateShopRequest request);
    Task<ShopDto?> UpdateAsync(string name, UpdateShopRequest request);
    Task<bool> DeleteAsync(string name);
    Task<List<QuotationShopDto>> GetShopsByQuotationIdAsync(long quotationId);
    Task<int> AddShopsToQuotationAsync(long quotationId, List<string> shopNames);
    Task<bool> RemoveShopFromQuotationAsync(long quotationId, long shopId);
    Task<ShopAssignmentDto> AddAssignmentAsync(CreateShopAssignmentRequest request);
    Task<bool> RemoveAssignmentAsync(long assignmentId);
    Task<PagedResult<ShopAssignmentListItemDto>> GetAssignmentListAsync(ShopAssignmentQueryRequest request);
    Task<ShopAssignmentDto> CreateAssignmentAsync(CreateShopAssignmentRequest request);
    Task<ShopAssignmentDto?> UpdateAssignmentAsync(long id, UpdateShopAssignmentRequest request);
    Task<bool> DeleteAssignmentAsync(long id);
    Task<List<ShopAssignmentBatchDto>> GetAssignmentBatchesAsync(long clientId);
    Task<List<BatchShopItemDto>> GetBatchShopsAsync(long clientId, long pricePlanId, DateTime effectiveDate);
    Task<int> CreateBatchAssignmentAsync(CreateBatchAssignmentRequest request);
    Task<List<ShopConflictDto>> CheckShopConflictsAsync(long quotationId, List<string> shopNames);
}
