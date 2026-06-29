using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Contract.Controllers;

[Authorize]
[ApiController]
[Route("api/contract/contracts")]
public class ContractController : ControllerBase
{
    private readonly IContractService _service;

    public ContractController(IContractService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(ContractPermissions.ContractView)]
    public async Task<ApiResult<PagedResult<ContractListItemDto>>> GetList([FromQuery] ContractQueryRequest request)
    {
        var result = await _service.GetContractsAsync(request);
        return ApiResult<PagedResult<ContractListItemDto>>.Success(result);
    }

    [HttpGet("statistics")]
    [RequirePermission(ContractPermissions.ContractView)]
    public async Task<ApiResult<ContractStatisticsDto>> GetStatistics()
        => ApiResult<ContractStatisticsDto>.Success(await _service.GetStatisticsAsync());

    [HttpGet("{id}")]
    [RequirePermission(ContractPermissions.ContractView)]
    public async Task<ApiResult<ContractDto>> GetById(long id)
    {
        var result = await _service.GetContractByIdAsync(id);
        if (result == null)
        {
            return ApiResult<ContractDto>.Fail("合同不存在");
        }
        return ApiResult<ContractDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(ContractPermissions.ContractCreate)]
    public async Task<ApiResult<ContractDto>> Create([FromBody] CreateContractRequest request)
    {
        try
        {
            var result = await _service.CreateContractAsync(request);
            return ApiResult<ContractDto>.Success(result, "创建合同成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ContractDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(ContractPermissions.ContractEdit)]
    public async Task<ApiResult<ContractDto>> Update(long id, [FromBody] UpdateContractRequest request)
    {
        var result = await _service.UpdateContractAsync(id, request);
        if (result == null)
        {
            return ApiResult<ContractDto>.Fail("合同不存在");
        }
        return ApiResult<ContractDto>.Success(result, "更新合同成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(ContractPermissions.ContractDelete)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteContractAsync(id);
        if (!result)
        {
            return ApiResult.Fail("合同不存在");
        }
        return ApiResult.Ok("删除合同成功");
    }

    [HttpPut("{id}/status")]
    [RequirePermission(ContractPermissions.ContractApprove)]
    public async Task<ApiResult> UpdateStatus(long id, [FromBody] UpdateContractStatusRequest request)
    {
        var result = await _service.UpdateStatusAsync(id, request.Status);
        if (!result)
        {
            return ApiResult.Fail("合同不存在");
        }
        return ApiResult.Ok("更新状态成功");
    }

    [HttpPost("{id}/renew")]
    [RequirePermission(ContractPermissions.ContractCreate)]
    public async Task<ApiResult<ContractDto>> Renew(long id, [FromBody] CreateContractRequest request)
    {
        try
        {
            var result = await _service.RenewContractAsync(id, request);
            return ApiResult<ContractDto>.Success(result, "续签合同成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ContractDto>.Fail(ex.Message);
        }
    }
}
