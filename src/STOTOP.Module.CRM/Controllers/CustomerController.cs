using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/customers")]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly STOTOPDbContext _db;

    public CustomerController(ICustomerService customerService, STOTOPDbContext db)
    {
        _customerService = customerService;
        _db = db;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.CustomerView)]
    public async Task<ApiResult<PagedResult<CustomerListItemDto>>> GetList([FromQuery] CustomerQueryRequest request)
    {
        var result = await _customerService.GetCustomersAsync(request);
        return ApiResult<PagedResult<CustomerListItemDto>>.Success(result);
    }

    [HttpGet("statistics")]
    [RequirePermission(CrmPermissions.CustomerView)]
    public async Task<ApiResult<CustomerStatisticsDto>> GetStatistics()
        => ApiResult<CustomerStatisticsDto>.Success(await _customerService.GetStatisticsAsync());

    [HttpGet("{code}")]
    [RequirePermission(CrmPermissions.CustomerView)]
    public async Task<ApiResult<CustomerDto>> GetByCode(string code)
    {
        var result = await _customerService.GetCustomerByCodeAsync(code);
        if (result == null)
            return ApiResult<CustomerDto>.Fail("客户不存在");
        return ApiResult<CustomerDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.CustomerCreate)]
    public async Task<ApiResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request)
    {
        try
        {
            var result = await _customerService.CreateCustomerAsync(request);
            return ApiResult<CustomerDto>.Success(result, "创建客户成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CustomerDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{code}")]
    [RequirePermission(CrmPermissions.CustomerEdit)]
    public async Task<ApiResult<CustomerDto>> Update(string code, [FromBody] UpdateCustomerRequest request)
    {
        try
        {
            var result = await _customerService.UpdateCustomerAsync(code, request);
            if (result == null)
                return ApiResult<CustomerDto>.Fail("客户不存在");
            return ApiResult<CustomerDto>.Success(result, "更新客户成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CustomerDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{code}")]
    [RequirePermission(CrmPermissions.CustomerDelete)]
    public async Task<ApiResult> Delete(string code)
    {
        var result = await _customerService.DeleteCustomerAsync(code);
        if (!result)
            return ApiResult.Fail("客户不存在");
        return ApiResult.Ok("删除客户成功");
    }

    [HttpPut("{code}/status")]
    [RequirePermission(CrmPermissions.CustomerEdit)]
    public async Task<ApiResult> UpdateStatus(string code, [FromBody] UpdateCustomerStatusRequest request)
    {
        var result = await _customerService.UpdateStatusAsync(code, request.Status);
        if (!result)
            return ApiResult.Fail("客户不存在");
        return ApiResult.Ok("更新状态成功");
    }

    [HttpPost("{code}/transfer")]
    [RequirePermission(CrmPermissions.CustomerEdit)]
    public async Task<ApiResult> Transfer(string code, [FromBody] TransferCustomerRequest request)
    {
        try
        {
            var result = await _customerService.TransferCustomerAsync(code, request);
            if (!result)
                return ApiResult.Fail("客户不存在");
            return ApiResult.Ok("客户流转成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("duplicate-check")]
    [RequirePermission(CrmPermissions.CustomerView)]
    public async Task<ApiResult<List<CustomerListItemDto>>> CheckDuplicate([FromBody] CustomerDuplicateCheckRequest request)
    {
        var result = await _customerService.CheckDuplicateAsync(request);
        return ApiResult<List<CustomerListItemDto>>.Success(result);
    }

    [HttpGet("{code}/timeline")]
    [RequirePermission(CrmPermissions.CustomerView)]
    public async Task<ApiResult<List<CustomerTimelineItemDto>>> GetTimeline(string code, [FromQuery] int count = 20)
    {
        var result = await _customerService.GetTimelineAsync(code, count);
        return ApiResult<List<CustomerTimelineItemDto>>.Success(result);
    }

    /// <summary>获取全部启用客户（扁平列表，供辅助核算关联使用）</summary>
    [HttpGet("all-enabled")]
    public async Task<ApiResult<List<object>>> GetAllEnabled()
    {
        var items = await _db.Set<CrmCustomer>()
            .Where(c => c.FStatus == 1)
            .OrderBy(c => c.FCode)
            .Select(c => (object)new { id = c.FCode, code = c.FCode, name = c.FShortName, fullName = c.FFullName, status = c.FStatus })
            .ToListAsync();
        return ApiResult<List<object>>.Success(items);
    }
}
