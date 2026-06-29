using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/waybill-pools")]
public class WaybillPoolController : ControllerBase
{
    private readonly IPrepaymentWaybillService _service;

    public WaybillPoolController(IPrepaymentWaybillService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.WaybillPoolView)]
    public async Task<ApiResult<PagedResult<WaybillPoolDto>>> GetList([FromQuery] WaybillPoolQueryRequest request)
    {
        var result = await _service.GetWaybillPoolsAsync(request);
        return ApiResult<PagedResult<WaybillPoolDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.WaybillPoolView)]
    public async Task<ApiResult<WaybillPoolDto>> GetById(long id)
    {
        var result = await _service.GetWaybillPoolByIdAsync(id);
        if (result == null)
            return ApiResult<WaybillPoolDto>.Fail("号段池不存在");
        return ApiResult<WaybillPoolDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.WaybillPoolManage)]
    public async Task<ApiResult<WaybillPoolDto>> Create([FromBody] CreateWaybillPoolRequest request)
    {
        var result = await _service.CreateWaybillPoolAsync(request);
        return ApiResult<WaybillPoolDto>.Success(result, "创建号段池成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(CrmPermissions.WaybillPoolManage)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteWaybillPoolAsync(id);
        if (!result)
            return ApiResult.Fail("号段池不存在");
        return ApiResult.Ok("删除号段池成功");
    }

    [HttpGet("{id}/allocations")]
    [RequirePermission(CrmPermissions.WaybillPoolView)]
    public async Task<ApiResult<List<WaybillAllocationDto>>> GetAllocations(long id)
    {
        var result = await _service.GetAllocationsByPoolAsync(id);
        return ApiResult<List<WaybillAllocationDto>>.Success(result);
    }

    [HttpPost("allocate")]
    [RequirePermission(CrmPermissions.WaybillPoolManage)]
    public async Task<ApiResult<WaybillAllocationDto>> Allocate([FromBody] AllocateWaybillRequest request)
    {
        try
        {
            var result = await _service.AllocateWaybillAsync(request);
            return ApiResult<WaybillAllocationDto>.Success(result, "运单号分配成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WaybillAllocationDto>.Fail(ex.Message);
        }
    }

    [HttpPost("allocations/{allocationId}/recycle")]
    [RequirePermission(CrmPermissions.WaybillPoolManage)]
    public async Task<ApiResult> Recycle(long allocationId)
    {
        try
        {
            var result = await _service.RecycleWaybillAsync(allocationId);
            if (!result)
                return ApiResult.Fail("分配记录不存在");
            return ApiResult.Ok("运单号回收成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
