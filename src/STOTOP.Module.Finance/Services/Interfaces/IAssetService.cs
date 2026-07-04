using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IAssetService
{
    // 资产类别
    Task<List<AssetCategoryDto>> GetCategoriesAsync(long accountSetId);
    Task<AssetCategoryDto?> GetCategoryByIdAsync(long id);
    Task<AssetCategoryDto> CreateCategoryAsync(CreateAssetCategoryRequest request, long accountSetId);
    Task<AssetCategoryDto?> UpdateCategoryAsync(long id, CreateAssetCategoryRequest request);
    Task<bool> DeleteCategoryAsync(long id);
    
    // 资产卡片
    Task<List<AssetCardDto>> GetCardsAsync(long accountSetId, long? categoryId = null);
    Task<AssetCardDto?> GetCardByIdAsync(long id);
    Task<AssetCardDto> CreateCardAsync(CreateAssetCardRequest request, long accountSetId);
    Task<AssetCardDto?> UpdateCardAsync(long id, UpdateAssetCardRequest request);
    Task<bool> DeleteCardAsync(long id);
    
    // 小番财务格式导入
    Task<AssetImportResult> ImportFromXiaofanAsync(Stream stream, string fileName, long accountSetId);
    Task<AssetImportResult> ImportCategoriesFromXiaofanAsync(Stream stream, string fileName, long accountSetId);
    
    // 计提折旧（旧 CalculateDepreciationAsync 已废弃删除：跨账套全量选卡/凭证 FAccountSetId=0/FAccountId=0/非事务，
    // 由 preview + GenerateDepreciationVouchersAsync 取代）
    Task<DepreciationPreviewDto> CalculateDepreciationPreviewAsync(long periodId, long accountSetId);
    Task<DepreciationResultDto> GenerateDepreciationVouchersAsync(long periodId, long accountSetId);
}
