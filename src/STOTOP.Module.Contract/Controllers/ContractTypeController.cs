using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Contract.Controllers;

[Authorize]
[ApiController]
[Route("api/contract/types")]
public class ContractTypeController : ControllerBase
{
    private readonly IContractTypeService _service;

    public ContractTypeController(IContractTypeService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(ContractPermissions.TypeView)]
    public async Task<ApiResult<PagedResult<ContractTypeDto>>> GetList([FromQuery] ContractTypeQueryRequest request)
    {
        var result = await _service.GetTypesAsync(request);
        return ApiResult<PagedResult<ContractTypeDto>>.Success(result);
    }

    [HttpGet("all")]
    [RequirePermission(ContractPermissions.TypeView)]
    public async Task<ApiResult<List<ContractTypeDto>>> GetAllEnabled()
    {
        var result = await _service.GetAllEnabledTypesAsync();
        return ApiResult<List<ContractTypeDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(ContractPermissions.TypeView)]
    public async Task<ApiResult<ContractTypeDto>> GetById(long id)
    {
        var result = await _service.GetTypeByIdAsync(id);
        if (result == null)
        {
            return ApiResult<ContractTypeDto>.Fail("合同类型不存在");
        }
        return ApiResult<ContractTypeDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(ContractPermissions.TypeManage)]
    public async Task<ApiResult<ContractTypeDto>> Create([FromBody] CreateContractTypeRequest request)
    {
        try
        {
            var result = await _service.CreateTypeAsync(request);
            return ApiResult<ContractTypeDto>.Success(result, "创建合同类型成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ContractTypeDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(ContractPermissions.TypeManage)]
    public async Task<ApiResult<ContractTypeDto>> Update(long id, [FromBody] UpdateContractTypeRequest request)
    {
        try
        {
            var result = await _service.UpdateTypeAsync(id, request);
            if (result == null)
            {
                return ApiResult<ContractTypeDto>.Fail("合同类型不存在");
            }
            return ApiResult<ContractTypeDto>.Success(result, "更新合同类型成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ContractTypeDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(ContractPermissions.TypeManage)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteTypeAsync(id);
        if (!result)
        {
            return ApiResult.Fail("合同类型不存在");
        }
        return ApiResult.Ok("删除合同类型成功");
    }

    [HttpPut("{id}/status")]
    [RequirePermission(ContractPermissions.TypeManage)]
    public async Task<ApiResult> UpdateStatus(long id, [FromBody] UpdateContractStatusRequest request)
    {
        var result = await _service.UpdateStatusAsync(id, request.Status);
        if (!result)
        {
            return ApiResult.Fail("合同类型不存在");
        }
        return ApiResult.Ok("更新状态成功");
    }
}
