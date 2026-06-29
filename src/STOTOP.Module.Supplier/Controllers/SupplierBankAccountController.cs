using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Supplier.Dtos;
using STOTOP.Module.Supplier.Services.Interfaces;

namespace STOTOP.Module.Supplier.Controllers;

[Authorize]
[ApiController]
[Route("api/supplier/suppliers/{supplierId}/bank-accounts")]
public class SupplierBankAccountController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierBankAccountController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<ApiResult<List<BankAccountDto>>> GetBankAccounts(long supplierId)
    {
        var result = await _supplierService.GetBankAccountsAsync(supplierId);
        return ApiResult<List<BankAccountDto>>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<BankAccountDto>> CreateBankAccount(long supplierId, [FromBody] CreateBankAccountRequest request)
    {
        try
        {
            var result = await _supplierService.CreateBankAccountAsync(supplierId, request);
            return ApiResult<BankAccountDto>.Success(result, "创建收款账户成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BankAccountDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<BankAccountDto>> UpdateBankAccount(long supplierId, long id, [FromBody] UpdateBankAccountRequest request)
    {
        var result = await _supplierService.UpdateBankAccountAsync(id, request);
        if (result == null)
        {
            return ApiResult<BankAccountDto>.Fail("收款账户不存在");
        }
        return ApiResult<BankAccountDto>.Success(result, "更新收款账户成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> DeleteBankAccount(long supplierId, long id)
    {
        var result = await _supplierService.DeleteBankAccountAsync(id);
        if (!result)
        {
            return ApiResult.Fail("收款账户不存在");
        }
        return ApiResult.Ok("删除收款账户成功");
    }

    [HttpPut("{id}/set-default")]
    public async Task<ApiResult> SetDefault(long supplierId, long id)
    {
        var result = await _supplierService.SetDefaultBankAccountAsync(id);
        if (!result)
        {
            return ApiResult.Fail("收款账户不存在");
        }
        return ApiResult.Ok("设置默认账户成功");
    }
}
