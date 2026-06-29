using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;
using global::System.Security.Claims;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/assets")]
public class AssetController : ControllerBase
{
    private readonly IAssetService _assetService;
    private readonly ITrialBalanceService _trialBalanceService;

    public AssetController(IAssetService assetService, ITrialBalanceService trialBalanceService)
    {
        _assetService = assetService;
        _trialBalanceService = trialBalanceService;
    }

    #region Categories

    [HttpGet("categories")]
    public async Task<ApiResult<List<AssetCategoryDto>>> GetCategories([FromQuery] long accountSetId)
    {
        var result = await _assetService.GetCategoriesAsync(accountSetId);
        return ApiResult<List<AssetCategoryDto>>.Success(result);
    }

    [HttpGet("categories/{id}")]
    public async Task<ApiResult<AssetCategoryDto>> GetCategoryById(long id)
    {
        var result = await _assetService.GetCategoryByIdAsync(id);
        if (result == null)
        {
            return ApiResult<AssetCategoryDto>.Fail("类别不存在");
        }
        return ApiResult<AssetCategoryDto>.Success(result);
    }

    [HttpPost("categories")]
    public async Task<ApiResult<AssetCategoryDto>> CreateCategory([FromBody] CreateAssetCategoryRequest request, [FromQuery] long accountSetId)
    {
        try
        {
            var result = await _assetService.CreateCategoryAsync(request, accountSetId);
            return ApiResult<AssetCategoryDto>.Success(result, "创建类别成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AssetCategoryDto>.Fail(ex.Message);
        }
    }

    [HttpPut("categories/{id}")]
    public async Task<ApiResult<AssetCategoryDto>> UpdateCategory(long id, [FromBody] CreateAssetCategoryRequest request)
    {
        var result = await _assetService.UpdateCategoryAsync(id, request);
        if (result == null)
        {
            return ApiResult<AssetCategoryDto>.Fail("类别不存在");
        }
        return ApiResult<AssetCategoryDto>.Success(result, "更新类别成功");
    }

    [HttpDelete("categories/{id}")]
    public async Task<ApiResult> DeleteCategory(long id)
    {
        try
        {
            var result = await _assetService.DeleteCategoryAsync(id);
            if (!result)
            {
                return ApiResult.Fail("类别不存在");
            }
            return ApiResult.Ok("删除类别成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    #endregion

    #region Cards

    [HttpGet("cards")]
    public async Task<ApiResult<List<AssetCardDto>>> GetCards([FromQuery] long accountSetId, [FromQuery] long? categoryId)
    {
        var result = await _assetService.GetCardsAsync(accountSetId, categoryId);
        return ApiResult<List<AssetCardDto>>.Success(result);
    }

    [HttpGet("cards/{id}")]
    public async Task<ApiResult<AssetCardDto>> GetCardById(long id)
    {
        var result = await _assetService.GetCardByIdAsync(id);
        if (result == null)
        {
            return ApiResult<AssetCardDto>.Fail("卡片不存在");
        }
        return ApiResult<AssetCardDto>.Success(result);
    }

    [HttpPost("cards")]
    public async Task<ApiResult<AssetCardDto>> CreateCard([FromBody] CreateAssetCardRequest request, [FromQuery] long accountSetId)
    {
        try
        {
            var result = await _assetService.CreateCardAsync(request, accountSetId);
            return ApiResult<AssetCardDto>.Success(result, "创建卡片成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AssetCardDto>.Fail(ex.Message);
        }
    }

    [HttpPut("cards/{id}")]
    public async Task<ApiResult<AssetCardDto>> UpdateCard(long id, [FromBody] UpdateAssetCardRequest request)
    {
        var result = await _assetService.UpdateCardAsync(id, request);
        if (result == null)
        {
            return ApiResult<AssetCardDto>.Fail("卡片不存在");
        }
        return ApiResult<AssetCardDto>.Success(result, "更新卡片成功");
    }

    [HttpDelete("cards/{id}")]
    public async Task<ApiResult> DeleteCard(long id)
    {
        var result = await _assetService.DeleteCardAsync(id);
        if (!result)
        {
            return ApiResult.Fail("卡片不存在");
        }
        return ApiResult.Ok("删除卡片成功");
    }

    /// <summary>导入小番财务导出的资产类别 Excel</summary>
    [HttpPost("categories/import/xiaofan")]
    public async Task<ApiResult<AssetImportResult>> ImportCategoriesFromXiaofan(IFormFile? file, [FromQuery] long accountSetId)
    {
        if (file == null || file.Length == 0)
            return ApiResult<AssetImportResult>.Fail("请上传文件");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return ApiResult<AssetImportResult>.Fail("仅支持 .xlsx 或 .xls 格式");

        if (file.Length > 10 * 1024 * 1024)
            return ApiResult<AssetImportResult>.Fail("文件过大，请拆分后再导入");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _assetService.ImportCategoriesFromXiaofanAsync(stream, file.FileName, accountSetId);
            var msg = $"导入完成：成功 {result.ImportedCount} 行，跳过 {result.SkippedCount} 行";
            return ApiResult<AssetImportResult>.Success(result, msg);
        }
        catch (Exception ex)
        {
            return ApiResult<AssetImportResult>.Fail($"导入失败: {ex.Message}");
        }
    }

    /// <summary>导入小番财务导出的资产卡片 Excel</summary>
    [HttpPost("cards/import/xiaofan")]
    public async Task<ApiResult<AssetImportResult>> ImportCardsFromXiaofan(IFormFile? file, [FromQuery] long accountSetId)
    {
        if (file == null || file.Length == 0)
            return ApiResult<AssetImportResult>.Fail("请上传文件");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return ApiResult<AssetImportResult>.Fail("仅支持 .xlsx 或 .xls 格式");

        if (file.Length > 10 * 1024 * 1024)
            return ApiResult<AssetImportResult>.Fail("文件过大，请拆分后再导入");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _assetService.ImportFromXiaofanAsync(stream, file.FileName, accountSetId);
            var msg = $"导入完成：成功 {result.ImportedCount} 行，跳过 {result.SkippedCount} 行";
            return ApiResult<AssetImportResult>.Success(result, msg);
        }
        catch (Exception ex)
        {
            return ApiResult<AssetImportResult>.Fail($"导入失败: {ex.Message}");
        }
    }

    #endregion

    #region Depreciation

    [HttpPost("depreciation/{periodId}")]
    public async Task<ApiResult<DepreciationResultDto>> CalculateDepreciation(long periodId)
    {
        var creator = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        var result = await _assetService.CalculateDepreciationAsync(periodId, creator);
        return ApiResult<DepreciationResultDto>.Success(result, $"成功计提{result.DepreciatedCount}项资产折旧");
    }

    [HttpPost("calculate-depreciation")]
    public async Task<ApiResult<DepreciationPreviewDto>> CalculateDepreciationPreview([FromQuery] long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _assetService.CalculateDepreciationPreviewAsync(periodId, accountSetId);
        return ApiResult<DepreciationPreviewDto>.Success(result);
    }

    [HttpPost("generate-depreciation-vouchers")]
    public async Task<ApiResult<DepreciationResultDto>> GenerateDepreciationVouchers([FromQuery] long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _assetService.GenerateDepreciationVouchersAsync(periodId, accountSetId);
        return ApiResult<DepreciationResultDto>.Success(result, $"成功生成折旧凭证，共{result.DepreciatedCount}项资产");
    }

    [HttpGet("trial-balance")]
    public async Task<ApiResult<TrialBalanceDto>> GetTrialBalance([FromQuery] long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _trialBalanceService.GetLatestTrialBalanceAsync(periodId, accountSetId);
        if (result == null)
        {
            return ApiResult<TrialBalanceDto>.Fail("未找到试算平衡记录");
        }
        return ApiResult<TrialBalanceDto>.Success(result);
    }

    [HttpPost("trial-balance/generate")]
    public async Task<ApiResult<TrialBalanceDto>> GenerateTrialBalance([FromQuery] long periodId, [FromQuery] long accountSetId = 0)
    {
        var result = await _trialBalanceService.GenerateTrialBalanceAsync(periodId, accountSetId);
        return ApiResult<TrialBalanceDto>.Success(result);
    }

    #endregion
}
