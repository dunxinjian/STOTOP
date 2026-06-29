using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Contract.Controllers;

[Authorize]
[ApiController]
[Route("api/contract/reminders")]
public class ContractReminderController : ControllerBase
{
    private readonly IContractReminderService _service;

    public ContractReminderController(IContractReminderService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(ContractPermissions.ReminderView)]
    public async Task<ApiResult<PagedResult<ContractReminderDto>>> GetList([FromQuery] ContractReminderQueryRequest request)
    {
        var result = await _service.GetRemindersAsync(request);
        return ApiResult<PagedResult<ContractReminderDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(ContractPermissions.ReminderView)]
    public async Task<ApiResult<ContractReminderDto>> GetById(long id)
    {
        var result = await _service.GetReminderByIdAsync(id);
        if (result == null)
        {
            return ApiResult<ContractReminderDto>.Fail("提醒不存在");
        }
        return ApiResult<ContractReminderDto>.Success(result);
    }

    [HttpGet("pending/{recipientId}")]
    [RequirePermission(ContractPermissions.ReminderView)]
    public async Task<ApiResult<List<ContractReminderDto>>> GetPending(long recipientId)
    {
        var result = await _service.GetPendingRemindersAsync(recipientId);
        return ApiResult<List<ContractReminderDto>>.Success(result);
    }

    [HttpPost]
    [RequirePermission(ContractPermissions.ReminderManage)]
    public async Task<ApiResult<ContractReminderDto>> Create([FromBody] CreateContractReminderRequest request)
    {
        try
        {
            var result = await _service.CreateReminderAsync(request);
            return ApiResult<ContractReminderDto>.Success(result, "创建提醒成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ContractReminderDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(ContractPermissions.ReminderManage)]
    public async Task<ApiResult<ContractReminderDto>> Update(long id, [FromBody] UpdateContractReminderRequest request)
    {
        var result = await _service.UpdateReminderAsync(id, request);
        if (result == null)
        {
            return ApiResult<ContractReminderDto>.Fail("提醒不存在");
        }
        return ApiResult<ContractReminderDto>.Success(result, "更新提醒成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(ContractPermissions.ReminderManage)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteReminderAsync(id);
        if (!result)
        {
            return ApiResult.Fail("提醒不存在");
        }
        return ApiResult.Ok("删除提醒成功");
    }

    [HttpPut("{id}/handle")]
    [RequirePermission(ContractPermissions.ReminderManage)]
    public async Task<ApiResult> MarkAsHandled(long id)
    {
        var result = await _service.MarkAsHandledAsync(id);
        if (!result)
        {
            return ApiResult.Fail("提醒不存在");
        }
        return ApiResult.Ok("标记已处理成功");
    }
}
