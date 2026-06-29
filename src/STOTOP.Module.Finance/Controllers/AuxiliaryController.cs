using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/auxiliaries")]
public class AuxiliaryController : ControllerBase
{
    private readonly IAuxiliaryService _auxiliaryService;

    public AuxiliaryController(IAuxiliaryService auxiliaryService)
    {
        _auxiliaryService = auxiliaryService;
    }

    /// <summary>
    /// 获取辅助核算类型列表
    /// </summary>
    [HttpGet("types")]
    public async Task<ApiResult<List<AuxiliaryTypeDto>>> GetTypes()
    {
        var result = await _auxiliaryService.GetTypesAsync();
        return ApiResult<List<AuxiliaryTypeDto>>.Success(result);
    }
}

// 账套维度辅助核算项 Controller
[Authorize]
[ApiController]
[Route("api/finance/auxiliary-items")]
public class AuxiliaryItemController : ControllerBase
{
    private readonly IAuxiliaryService _auxiliaryService;

    public AuxiliaryItemController(IAuxiliaryService auxiliaryService)
    {
        _auxiliaryService = auxiliaryService;
    }

    /// <summary>
    /// 按账套+类型查询辅助核算项
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<List<AuxiliaryItemDto>>> GetList(
        [FromQuery] long accountSetId,
        [FromQuery] string? auxType,
        [FromQuery] string? keyword)
    {
        var request = new AuxiliaryItemQueryRequest
        {
            AccountSetId = accountSetId,
            AuxType = auxType,
            Keyword = keyword
        };
        var result = await _auxiliaryService.GetItemsByAccountSetAsync(request);
        return ApiResult<List<AuxiliaryItemDto>>.Success(result);
    }

    /// <summary>
    /// 新增辅助核算项
    /// </summary>
    [HttpPost]
    public async Task<ApiResult<AuxiliaryItemDto>> Create([FromBody] AuxiliaryItemCreateRequest request)
    {
        try
        {
            var result = await _auxiliaryService.CreateItemByAccountSetAsync(request);
            return ApiResult<AuxiliaryItemDto>.Success(result, "创建成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AuxiliaryItemDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新辅助核算项
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResult<AuxiliaryItemDto>> Update(long id, [FromBody] AuxiliaryItemCreateRequest request)
    {
        var result = await _auxiliaryService.UpdateItemByAccountSetAsync(id, request);
        if (result == null)
        {
            return ApiResult<AuxiliaryItemDto>.Fail("项目不存在");
        }
        return ApiResult<AuxiliaryItemDto>.Success(result, "更新成功");
    }

    /// <summary>
    /// 删除辅助核算项
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            var result = await _auxiliaryService.DeleteItemByIdAsync(id);
            if (!result)
            {
                return ApiResult.Fail("项目不存在");
            }
            return ApiResult.Ok("删除成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 检查辅助核算项是否被凭证引用
    /// </summary>
    [HttpGet("{id:long}/check-usage")]
    public async Task<ApiResult<bool>> CheckUsage(long id)
    {
        var isUsed = await _auxiliaryService.CheckItemUsageAsync(id);
        return ApiResult<bool>.Success(isUsed);
    }

    /// <summary>
    /// 检查编码是否已被同账套其他辅助核算项占用
    /// </summary>
    [HttpGet("check-code")]
    public async Task<ApiResult<bool>> CheckCode(
        [FromQuery] long accountSetId,
        [FromQuery] string code,
        [FromQuery] long excludeId)
    {
        var exists = await _auxiliaryService.CheckCodeExistsAsync(accountSetId, code, excludeId);
        return ApiResult<bool>.Success(exists);
    }

    /// <summary>
    /// 检查编码和名称唯一性
    /// </summary>
    [HttpGet("check-unique")]
    public async Task<ApiResult<object>> CheckUnique(
        [FromQuery] long accountSetId,
        [FromQuery] string auxType,
        [FromQuery] string? code,
        [FromQuery] string? name,
        [FromQuery] long? excludeId)
    {
        var (isUnique, conflictField) = await _auxiliaryService.CheckUniqueAsync(
            accountSetId, auxType, code, name, excludeId);
        return ApiResult<object>.Success(new { isUnique, conflictField });
    }

    /// <summary>
    /// 获取可选部门列表（从组织架构查询，排除已添加的）
    /// </summary>
    [HttpGet("available-departments")]
    public async Task<ApiResult<List<AvailableDepartmentDto>>> GetAvailableDepartments([FromQuery] long accountSetId)
    {
        var result = await _auxiliaryService.GetAvailableDepartmentsAsync(accountSetId);
        return ApiResult<List<AvailableDepartmentDto>>.Success(result);
    }

    /// <summary>
    /// 获取可选员工列表（从用户查询，排除已添加的）
    /// </summary>
    [HttpGet("available-employees")]
    public async Task<ApiResult<List<AvailableEmployeeDto>>> GetAvailableEmployees([FromQuery] long accountSetId)
    {
        var result = await _auxiliaryService.GetAvailableEmployeesAsync(accountSetId);
        return ApiResult<List<AvailableEmployeeDto>>.Success(result);
    }

    /// <summary>
    /// 从组织架构添加辅助核算项目（部门类）
    /// </summary>
    [HttpPost("add-from-org")]
    public async Task<ApiResult<List<AuxiliaryItemDto>>> AddFromOrganization([FromBody] AddFromOrgRequest request)
    {
        try
        {
            var result = await _auxiliaryService.AddFromOrganizationAsync(request);
            return ApiResult<List<AuxiliaryItemDto>>.Success(result, $"成功添加 {result.Count} 个部门辅助核算项");
        }
        catch (Exception ex)
        {
            return ApiResult<List<AuxiliaryItemDto>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 从用户添加辅助核算项目（职员类）
    /// </summary>
    [HttpPost("add-from-user")]
    public async Task<ApiResult<List<AuxiliaryItemDto>>> AddFromUser([FromBody] AddFromUserRequest request)
    {
        try
        {
            var result = await _auxiliaryService.AddFromUserAsync(request);
            return ApiResult<List<AuxiliaryItemDto>>.Success(result, $"成功添加 {result.Count} 个职员辅助核算项");
        }
        catch (Exception ex)
        {
            return ApiResult<List<AuxiliaryItemDto>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取可选客户列表（从CRM查询，排除已添加的）
    /// </summary>
    [HttpGet("available-customers")]
    public async Task<ApiResult<List<AvailableCustomerDto>>> GetAvailableCustomers([FromQuery] long accountSetId)
    {
        var result = await _auxiliaryService.GetAvailableCustomersAsync(accountSetId);
        return ApiResult<List<AvailableCustomerDto>>.Success(result);
    }

    /// <summary>
    /// 获取可选供应商列表（排除已添加的）
    /// </summary>
    [HttpGet("available-suppliers")]
    public async Task<ApiResult<List<AvailableSupplierDto>>> GetAvailableSuppliers([FromQuery] long accountSetId)
    {
        var result = await _auxiliaryService.GetAvailableSuppliersAsync(accountSetId);
        return ApiResult<List<AvailableSupplierDto>>.Success(result);
    }

    /// <summary>
    /// 获取可选快递品牌列表（排除已添加的）
    /// </summary>
    [HttpGet("available-brands")]
    public async Task<ApiResult<List<AvailableBrandDto>>> GetAvailableBrands([FromQuery] long accountSetId)
    {
        var result = await _auxiliaryService.GetAvailableBrandsAsync(accountSetId);
        return ApiResult<List<AvailableBrandDto>>.Success(result);
    }

    /// <summary>
    /// 从CRM客户批量添加辅助核算项目
    /// </summary>
    [HttpPost("add-from-customer")]
    public async Task<ApiResult<List<AuxiliaryItemDto>>> AddFromCustomer([FromBody] AddFromCustomerRequest request)
    {
        try
        {
            var result = await _auxiliaryService.AddFromCustomerAsync(request);
            return ApiResult<List<AuxiliaryItemDto>>.Success(result, $"成功添加 {result.Count} 个客户辅助核算项");
        }
        catch (Exception ex)
        {
            return ApiResult<List<AuxiliaryItemDto>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 从供应商批量添加辅助核算项目
    /// </summary>
    [HttpPost("add-from-supplier")]
    public async Task<ApiResult<List<AuxiliaryItemDto>>> AddFromSupplier([FromBody] AddFromSupplierRequest request)
    {
        try
        {
            var result = await _auxiliaryService.AddFromSupplierAsync(request);
            return ApiResult<List<AuxiliaryItemDto>>.Success(result, $"成功添加 {result.Count} 个供应商辅助核算项");
        }
        catch (Exception ex)
        {
            return ApiResult<List<AuxiliaryItemDto>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 从快递品牌批量添加辅助核算项目
    /// </summary>
    [HttpPost("add-from-brand")]
    public async Task<ApiResult<List<AuxiliaryItemDto>>> AddFromBrand([FromBody] AddFromBrandRequest request)
    {
        try
        {
            var result = await _auxiliaryService.AddFromBrandAsync(request);
            return ApiResult<List<AuxiliaryItemDto>>.Success(result, $"成功添加 {result.Count} 个快递品牌辅助核算项");
        }
        catch (Exception ex)
        {
            return ApiResult<List<AuxiliaryItemDto>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取可选网点列表（从快递网点查询，排除已添加的）
    /// </summary>
    [HttpGet("available-network-points")]
    public async Task<ApiResult<List<AvailableNetworkPointDto>>> GetAvailableNetworkPoints([FromQuery] long accountSetId)
    {
        var result = await _auxiliaryService.GetAvailableNetworkPointsAsync(accountSetId);
        return ApiResult<List<AvailableNetworkPointDto>>.Success(result);
    }

    /// <summary>
    /// 从网点批量添加辅助核算项目
    /// </summary>
    [HttpPost("add-from-network-point")]
    public async Task<ApiResult<List<AuxiliaryItemDto>>> AddFromNetworkPoint([FromBody] AddFromNetworkPointRequest request)
    {
        try
        {
            var result = await _auxiliaryService.AddFromNetworkPointAsync(request);
            return ApiResult<List<AuxiliaryItemDto>>.Success(result, $"成功添加 {result.Count} 个网点辅助核算项");
        }
        catch (Exception ex)
        {
            return ApiResult<List<AuxiliaryItemDto>>.Fail(ex.Message);
        }
    }
}
