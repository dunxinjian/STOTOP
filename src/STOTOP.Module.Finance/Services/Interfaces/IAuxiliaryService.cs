using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IAuxiliaryService
{
    // 辅助核算类型
    Task<List<AuxiliaryTypeDto>> GetTypesAsync();
    
    // 辅助核算项目（新接口，基于账套+类型）
    Task<List<AuxiliaryItemDto>> GetItemsByAccountSetAsync(AuxiliaryItemQueryRequest request);
    Task<AuxiliaryItemDto> CreateItemByAccountSetAsync(AuxiliaryItemCreateRequest request);
    Task<AuxiliaryItemDto?> UpdateItemByAccountSetAsync(long id, AuxiliaryItemCreateRequest request);
    Task<bool> DeleteItemByIdAsync(long id);
    
    // 检查辅助核算项是否被凭证引用
    Task<bool> CheckItemUsageAsync(long id);
    
    // 检查编码是否已被同账套其他辅助核算项占用
    Task<bool> CheckCodeExistsAsync(long accountSetId, string code, long excludeId);
    
    // 检查编码和名称唯一性（返回是否唯一及冲突字段）
    Task<(bool isUnique, string? conflictField)> CheckUniqueAsync(
        long accountSetId, string auxType, string? code, string? name, long? excludeId = null);

    // 组织架构集成
    Task<List<AvailableDepartmentDto>> GetAvailableDepartmentsAsync(long accountSetId);
    Task<List<AvailableEmployeeDto>> GetAvailableEmployeesAsync(long accountSetId);
    Task<List<AuxiliaryItemDto>> AddFromOrganizationAsync(AddFromOrgRequest request);
    Task<List<AuxiliaryItemDto>> AddFromUserAsync(AddFromUserRequest request);

    // 客户/供应商/快递品牌集成
    Task<List<AvailableCustomerDto>> GetAvailableCustomersAsync(long accountSetId);
    Task<List<AvailableSupplierDto>> GetAvailableSuppliersAsync(long accountSetId);
    Task<List<AvailableBrandDto>> GetAvailableBrandsAsync(long accountSetId);
    Task<List<AuxiliaryItemDto>> AddFromCustomerAsync(AddFromCustomerRequest request);
    Task<List<AuxiliaryItemDto>> AddFromSupplierAsync(AddFromSupplierRequest request);
    Task<List<AuxiliaryItemDto>> AddFromBrandAsync(AddFromBrandRequest request);

    // 网点集成
    Task<List<AvailableNetworkPointDto>> GetAvailableNetworkPointsAsync(long accountSetId);
    Task<List<AuxiliaryItemDto>> AddFromNetworkPointAsync(AddFromNetworkPointRequest request);
}
