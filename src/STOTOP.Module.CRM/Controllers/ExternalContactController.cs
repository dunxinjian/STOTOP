using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/external-contacts")]
public class ExternalContactController : ControllerBase
{
    private readonly IReferralCommissionService _service;

    public ExternalContactController(IReferralCommissionService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.CustomerView)]
    public async Task<ApiResult<PagedResult<ExternalContactDto>>> GetList([FromQuery] ExternalContactQueryRequest request)
    {
        var result = await _service.GetExternalContactsAsync(request);
        return ApiResult<PagedResult<ExternalContactDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.CustomerView)]
    public async Task<ApiResult<ExternalContactDto>> GetById(long id)
    {
        var result = await _service.GetExternalContactByIdAsync(id);
        if (result == null)
            return ApiResult<ExternalContactDto>.Fail("外部联系人不存在");
        return ApiResult<ExternalContactDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.CustomerCreate)]
    public async Task<ApiResult<ExternalContactDto>> Create([FromBody] CreateExternalContactRequest request)
    {
        var result = await _service.CreateExternalContactAsync(request);
        return ApiResult<ExternalContactDto>.Success(result, "创建外部联系人成功");
    }

    [HttpPut("{id}")]
    [RequirePermission(CrmPermissions.CustomerEdit)]
    public async Task<ApiResult<ExternalContactDto>> Update(long id, [FromBody] UpdateExternalContactRequest request)
    {
        var result = await _service.UpdateExternalContactAsync(id, request);
        if (result == null)
            return ApiResult<ExternalContactDto>.Fail("外部联系人不存在");
        return ApiResult<ExternalContactDto>.Success(result, "更新外部联系人成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(CrmPermissions.CustomerDelete)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteExternalContactAsync(id);
        if (!result)
            return ApiResult.Fail("外部联系人不存在");
        return ApiResult.Ok("删除外部联系人成功");
    }
}
