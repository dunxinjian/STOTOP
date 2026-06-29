using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Contract.Controllers;

[Authorize]
[ApiController]
[Route("api/contract/esign")]
public class ESignController : ControllerBase
{
    private readonly IESignService _service;

    public ESignController(IESignService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(ContractPermissions.ESignView)]
    public async Task<ApiResult<PagedResult<ESignRecordDto>>> GetList([FromQuery] ESignRecordQueryRequest request)
    {
        var result = await _service.GetRecordsAsync(request);
        return ApiResult<PagedResult<ESignRecordDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(ContractPermissions.ESignView)]
    public async Task<ApiResult<ESignRecordDto>> GetById(long id)
    {
        var result = await _service.GetRecordByIdAsync(id);
        if (result == null)
        {
            return ApiResult<ESignRecordDto>.Fail("签署记录不存在");
        }
        return ApiResult<ESignRecordDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(ContractPermissions.ESignManage)]
    public async Task<ApiResult<ESignRecordDto>> Create([FromBody] CreateESignRecordRequest request)
    {
        try
        {
            var result = await _service.CreateRecordAsync(request);
            return ApiResult<ESignRecordDto>.Success(result, "创建签署记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ESignRecordDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}/complete")]
    [RequirePermission(ContractPermissions.ESignManage)]
    public async Task<ApiResult<ESignRecordDto>> Complete(long id, [FromBody] ManualSignRequest request)
    {
        var result = await _service.CompleteSignAsync(id, request);
        if (result == null)
        {
            return ApiResult<ESignRecordDto>.Fail("签署记录不存在");
        }
        return ApiResult<ESignRecordDto>.Success(result, "签署完成");
    }

    [HttpPut("{id}/reject")]
    [RequirePermission(ContractPermissions.ESignManage)]
    public async Task<ApiResult> Reject(long id)
    {
        var result = await _service.RejectSignAsync(id);
        if (!result)
        {
            return ApiResult.Fail("签署记录不存在");
        }
        return ApiResult.Ok("拒签成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(ContractPermissions.ESignManage)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteRecordAsync(id);
        if (!result)
        {
            return ApiResult.Fail("签署记录不存在");
        }
        return ApiResult.Ok("删除签署记录成功");
    }
}
