using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;
using STOTOP.Module.Contract.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Contract.Controllers;

[Authorize]
[ApiController]
[Route("api/contract/templates")]
public class ContractTemplateController : ControllerBase
{
    private readonly IContractTemplateService _service;

    public ContractTemplateController(IContractTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(ContractPermissions.TemplateView)]
    public async Task<ApiResult<PagedResult<ContractTemplateListItemDto>>> GetList([FromQuery] ContractTemplateQueryRequest request)
    {
        var result = await _service.GetTemplatesAsync(request);
        return ApiResult<PagedResult<ContractTemplateListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(ContractPermissions.TemplateView)]
    public async Task<ApiResult<ContractTemplateDto>> GetById(long id)
    {
        var result = await _service.GetTemplateByIdAsync(id);
        if (result == null)
        {
            return ApiResult<ContractTemplateDto>.Fail("合同模板不存在");
        }
        return ApiResult<ContractTemplateDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(ContractPermissions.TemplateManage)]
    public async Task<ApiResult<ContractTemplateDto>> Create([FromBody] CreateContractTemplateRequest request)
    {
        var result = await _service.CreateTemplateAsync(request);
        return ApiResult<ContractTemplateDto>.Success(result, "创建合同模板成功");
    }

    [HttpPut("{id}")]
    [RequirePermission(ContractPermissions.TemplateManage)]
    public async Task<ApiResult<ContractTemplateDto>> Update(long id, [FromBody] UpdateContractTemplateRequest request)
    {
        var result = await _service.UpdateTemplateAsync(id, request);
        if (result == null)
        {
            return ApiResult<ContractTemplateDto>.Fail("合同模板不存在");
        }
        return ApiResult<ContractTemplateDto>.Success(result, "更新合同模板成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(ContractPermissions.TemplateManage)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteTemplateAsync(id);
        if (!result)
        {
            return ApiResult.Fail("合同模板不存在");
        }
        return ApiResult.Ok("删除合同模板成功");
    }

    [HttpPut("{id}/publish")]
    [RequirePermission(ContractPermissions.TemplateManage)]
    public async Task<ApiResult> Publish(long id)
    {
        var result = await _service.PublishTemplateAsync(id);
        if (!result)
        {
            return ApiResult.Fail("合同模板不存在");
        }
        return ApiResult.Ok("发布合同模板成功");
    }
}
