using STOTOP.Core.Models;
using STOTOP.Module.Supplier.Dtos;

namespace STOTOP.Module.Supplier.Services.Interfaces;

public interface ISupplierService
{
    // Supplier CRUD
    Task<PagedResult<SupplierListItemDto>> GetSuppliersAsync(SupplierQueryRequest request);
    Task<List<SupplierListItemDto>> GetAllEnabledSuppliersAsync();
    Task<SupplierDto?> GetSupplierByIdAsync(long id);
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request);
    Task<SupplierDto?> UpdateSupplierAsync(long id, UpdateSupplierRequest request);
    Task<bool> DeleteSupplierAsync(long id);
    Task<bool> UpdateStatusAsync(long id, int status);
    Task<bool> CheckCodeExistsAsync(string code, long excludeId);

    // Bank Account CRUD
    Task<List<BankAccountDto>> GetBankAccountsAsync(long supplierId);
    Task<BankAccountDto> CreateBankAccountAsync(long supplierId, CreateBankAccountRequest request);
    Task<BankAccountDto?> UpdateBankAccountAsync(long id, UpdateBankAccountRequest request);
    Task<bool> DeleteBankAccountAsync(long id);
    Task<bool> SetDefaultBankAccountAsync(long id);
}
