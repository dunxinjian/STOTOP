using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services;
using global::System.Security.Claims;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/voucher-templates")]
public class VoucherTemplateController : ControllerBase
{
    private readonly VoucherTemplateService _templateService;
    private readonly ILogger<VoucherTemplateController> _logger;

    public VoucherTemplateController(VoucherTemplateService templateService, ILogger<VoucherTemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>获取模板列表</summary>
    [HttpGet]
    public async Task<ApiResult<List<VoucherTemplateListDto>>> GetList([FromQuery] long accountSetId)
    {
        try
        {
            var result = await _templateService.GetListAsync(accountSetId);
            return ApiResult<List<VoucherTemplateListDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模板列表失败");
            return ApiResult<List<VoucherTemplateListDto>>.Fail("获取模板列表失败");
        }
    }

    /// <summary>获取模板详情</summary>
    [HttpGet("{id}")]
    public async Task<ApiResult<VoucherTemplateDto>> GetDetail(long id)
    {
        var result = await _templateService.GetDetailAsync(id);
        if (result == null)
            return ApiResult<VoucherTemplateDto>.Fail("模板不存在");
        return ApiResult<VoucherTemplateDto>.Success(result);
    }

    /// <summary>新增模板</summary>
    [HttpPost]
    public async Task<ApiResult<VoucherTemplateDto>> Create([FromBody] VoucherTemplateCreateRequest request)
    {
        try
        {
            var result = await _templateService.CreateAsync(request);
            return ApiResult<VoucherTemplateDto>.Success(result, "创建成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VoucherTemplateDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建模板失败");
            return ApiResult<VoucherTemplateDto>.Fail("操作失败，请稍后重试");
        }
    }

    /// <summary>更新模板</summary>
    [HttpPut("{id}")]
    public async Task<ApiResult<VoucherTemplateDto>> Update(long id, [FromBody] VoucherTemplateCreateRequest request)
    {
        try
        {
            var result = await _templateService.UpdateAsync(id, request);
            if (result == null)
                return ApiResult<VoucherTemplateDto>.Fail("模板不存在");
            return ApiResult<VoucherTemplateDto>.Success(result, "更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VoucherTemplateDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新模板失败");
            return ApiResult<VoucherTemplateDto>.Fail("操作失败，请稍后重试");
        }
    }

    /// <summary>删除模板</summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        var ok = await _templateService.DeleteAsync(id);
        return ok ? ApiResult<bool>.Success(true, "删除成功") : ApiResult<bool>.Fail("模板不存在");
    }

    /// <summary>从模板生成凭证</summary>
    [HttpPost("{id}/generate")]
    public async Task<ApiResult<long>> Generate(long id, [FromBody] GenerateVoucherFromTemplateRequest request)
    {
        try
        {
            var creator = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
            var voucherId = await _templateService.CreateVoucherFromTemplateAsync(
                id, request.Date, creator, request.AccountSetId);
            return ApiResult<long>.Success(voucherId, "凭证生成成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<long>.Fail(ex.Message);
        }
    }
}

public class GenerateVoucherFromTemplateRequest
{
    public DateTime Date { get; set; } = DateTime.Today;
    public long AccountSetId { get; set; }
}
