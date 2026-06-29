using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Supplier.Dtos;
using STOTOP.Module.Supplier.Services.Interfaces;

namespace STOTOP.Module.Supplier.Controllers;

[Authorize]
[ApiController]
[Route("api/supplier/suppliers")]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<SupplierListItemDto>>> GetList([FromQuery] SupplierQueryRequest request)
    {
        var result = await _supplierService.GetSuppliersAsync(request);
        return ApiResult<PagedResult<SupplierListItemDto>>.Success(result);
    }

    [HttpGet("all")]
    public async Task<ApiResult<List<SupplierListItemDto>>> GetAllEnabled()
    {
        var result = await _supplierService.GetAllEnabledSuppliersAsync();
        return ApiResult<List<SupplierListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<SupplierDto>> GetById(long id)
    {
        var result = await _supplierService.GetSupplierByIdAsync(id);
        if (result == null)
        {
            return ApiResult<SupplierDto>.Fail("供应商不存在");
        }
        return ApiResult<SupplierDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request)
    {
        try
        {
            var result = await _supplierService.CreateSupplierAsync(request);
            return ApiResult<SupplierDto>.Success(result, "创建供应商成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<SupplierDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<SupplierDto>> Update(long id, [FromBody] UpdateSupplierRequest request)
    {
        try
        {
            var result = await _supplierService.UpdateSupplierAsync(id, request);
            if (result == null)
            {
                return ApiResult<SupplierDto>.Fail("供应商不存在");
            }
            return ApiResult<SupplierDto>.Success(result, "更新供应商成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<SupplierDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _supplierService.DeleteSupplierAsync(id);
        if (!result)
        {
            return ApiResult.Fail("供应商不存在");
        }
        return ApiResult.Ok("删除供应商成功");
    }

    [HttpPut("{id}/status")]
    public async Task<ApiResult> UpdateStatus(long id, [FromBody] UpdateSupplierStatusRequest request)
    {
        var result = await _supplierService.UpdateStatusAsync(id, request.Status);
        if (!result)
        {
            return ApiResult.Fail("供应商不存在");
        }
        return ApiResult.Ok("更新状态成功");
    }

    [HttpGet("check-code")]
    public async Task<ApiResult<bool>> CheckCode([FromQuery] string code, [FromQuery] long excludeId = 0)
    {
        var exists = await _supplierService.CheckCodeExistsAsync(code, excludeId);
        return ApiResult<bool>.Success(exists);
    }
}
