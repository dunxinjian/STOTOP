using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/account-templates")]
public class AccountTemplateController : ControllerBase
{
    private readonly IAccountTemplateService _templateService;

    public AccountTemplateController(IAccountTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<ApiResult<List<AccountTemplateDto>>> GetTemplates()
    {
        var result = await _templateService.GetTemplatesAsync();
        return ApiResult<List<AccountTemplateDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<AccountTemplateDetailDto>> GetTemplateDetail(long id)
    {
        try
        {
            var result = await _templateService.GetTemplateDetailAsync(id);
            return ApiResult<AccountTemplateDetailDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AccountTemplateDetailDto>.Fail(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ApiResult<object>> CreateTemplate([FromBody] CreateAccountTemplateRequest request)
    {
        try
        {
            var id = await _templateService.CreateTemplateAsync(request);
            return ApiResult<object>.Success(new { id }, "创建模板成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult> UpdateTemplate(long id, [FromBody] UpdateAccountTemplateRequest request)
    {
        try
        {
            await _templateService.UpdateTemplateAsync(id, request);
            return ApiResult.Ok("更新模板成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> DeleteTemplate(long id)
    {
        try
        {
            await _templateService.DeleteTemplateAsync(id);
            return ApiResult.Ok("删除模板成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("{id}/items")]
    public async Task<ApiResult<List<AccountTemplateItemTreeDto>>> GetTemplateItems(long id)
    {
        try
        {
            var result = await _templateService.GetTemplateItemsTreeAsync(id);
            return ApiResult<List<AccountTemplateItemTreeDto>>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<List<AccountTemplateItemTreeDto>>.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/items")]
    public async Task<ApiResult<AccountTemplateItemDto>> AddTemplateItem(long id, [FromBody] CreateTemplateItemRequest request)
    {
        try
        {
            var result = await _templateService.AddTemplateItemAsync(id, request);
            return ApiResult<AccountTemplateItemDto>.Success(result, "添加科目项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AccountTemplateItemDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}/items/{itemId}")]
    public async Task<ApiResult> UpdateTemplateItem(long id, long itemId, [FromBody] UpdateTemplateItemRequest request)
    {
        try
        {
            await _templateService.UpdateTemplateItemAsync(id, itemId, request);
            return ApiResult.Ok("更新科目项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}/items/{itemId}")]
    public async Task<ApiResult> DeleteTemplateItem(long id, long itemId)
    {
        try
        {
            await _templateService.DeleteTemplateItemAsync(id, itemId);
            return ApiResult.Ok("删除科目项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("{id}/apply/{accountSetId}")]
    public async Task<ApiResult> ApplyTemplate(long id, long accountSetId)
    {
        try
        {
            await _templateService.ApplyTemplateAsync(id, accountSetId);
            return ApiResult.Ok("模板应用成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
