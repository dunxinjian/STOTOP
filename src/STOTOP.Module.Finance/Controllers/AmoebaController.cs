using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/amoeba")]
public class AmoebaController : ControllerBase
{
    private readonly IAmoebaService _amoebaService;
    private readonly AmoebaPLService _amoebaPLService;
    private readonly IOrganizationService _organizationService;

    public AmoebaController(IAmoebaService amoebaService, AmoebaPLService amoebaPLService, IOrganizationService organizationService)
    {
        _amoebaService = amoebaService;
        _amoebaPLService = amoebaPLService;
        _organizationService = organizationService;
    }

    [HttpGet("units/tree")]
    public async Task<ApiResult<List<OrganizationDto>>> GetUnitsTree()
    {
        return await _organizationService.GetTreeAsync();
    }

    #region Templates

    [HttpGet("templates")]
    public async Task<ApiResult<List<AmoebaPLTemplateDto>>> GetTemplates([FromQuery] long? accountSetId = null)
    {
        // 模板按账套隔离：不带账套查询会返回全部账套的模板（跨账套可见）
        if (!accountSetId.HasValue || accountSetId.Value <= 0)
        {
            return ApiResult<List<AmoebaPLTemplateDto>>.Fail("请指定账套ID");
        }
        var result = await _amoebaService.GetTemplatesAsync(accountSetId);
        return ApiResult<List<AmoebaPLTemplateDto>>.Success(result);
    }

    [HttpGet("templates/{id}")]
    public async Task<ApiResult<AmoebaPLTemplateDto>> GetTemplateById(long id)
    {
        var result = await _amoebaService.GetTemplateByIdAsync(id);
        if (result == null)
        {
            return ApiResult<AmoebaPLTemplateDto>.Fail("模板不存在");
        }
        return ApiResult<AmoebaPLTemplateDto>.Success(result);
    }

    [HttpPost("templates")]
    public async Task<ApiResult<AmoebaPLTemplateDto>> CreateTemplate([FromBody] CreateAmoebaPLTemplateRequest request)
    {
        var result = await _amoebaService.CreateTemplateAsync(request);
        return ApiResult<AmoebaPLTemplateDto>.Success(result, "创建模板成功");
    }

    [HttpPut("templates/{id}")]
    public async Task<ApiResult<AmoebaPLTemplateDto>> UpdateTemplate(long id, [FromBody] UpdateAmoebaPLTemplateRequest request)
    {
        var result = await _amoebaService.UpdateTemplateAsync(id, request);
        if (result == null)
        {
            return ApiResult<AmoebaPLTemplateDto>.Fail("模板不存在");
        }
        return ApiResult<AmoebaPLTemplateDto>.Success(result, "更新模板成功");
    }

    [HttpDelete("templates/{id}")]
    public async Task<ApiResult> DeleteTemplate(long id)
    {
        try
        {
            var result = await _amoebaService.DeleteTemplateAsync(id);
            if (!result)
            {
                return ApiResult.Fail("模板不存在");
            }
            return ApiResult.Ok("删除模板成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 从现有模板复制创建新模板（名称、账套使用新值，深拷贝所有损益项）。
    /// </summary>
    [HttpPost("templates/{sourceId}/clone")]
    public async Task<ApiResult<AmoebaPLTemplateDto>> CloneTemplate(long sourceId, [FromBody] CloneAmoebaPLTemplateRequest request)
    {
        try
        {
            var result = await _amoebaService.CloneTemplateAsync(sourceId, request);
            return ApiResult<AmoebaPLTemplateDto>.Success(result, "复制模板成功");
        }
        catch (ArgumentException ex)
        {
            return ApiResult<AmoebaPLTemplateDto>.Fail(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AmoebaPLTemplateDto>.Fail(ex.Message);
        }
    }

    #endregion

    #region Items

    [HttpPost("templates/{id}/items")]
    public async Task<ApiResult<AmoebaPLItemDto>> AddItem(long id, [FromBody] CreateAmoebaPLItemRequest request)
    {
        try
        {
            var result = await _amoebaService.AddItemAsync(id, request);
            return ApiResult<AmoebaPLItemDto>.Success(result, "添加损益项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AmoebaPLItemDto>.Fail(ex.Message);
        }
    }

    [HttpPut("templates/{id}/items/{itemId}")]
    public async Task<ApiResult<AmoebaPLItemDto>> UpdateItem(long id, long itemId, [FromBody] UpdateAmoebaPLItemRequest request)
    {
        try
        {
            var result = await _amoebaService.UpdateItemAsync(id, itemId, request);
            if (result == null)
            {
                return ApiResult<AmoebaPLItemDto>.Fail("损益项不存在");
            }
            return ApiResult<AmoebaPLItemDto>.Success(result, "更新损益项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AmoebaPLItemDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("templates/{id}/items/{itemId}")]
    public async Task<ApiResult> DeleteItem(long id, long itemId)
    {
        try
        {
            var result = await _amoebaService.DeleteItemAsync(id, itemId);
            if (!result)
            {
                return ApiResult.Fail("损益项不存在");
            }
            return ApiResult.Ok("删除损益项成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPut("templates/{id}/items/reorder")]
    public async Task<ApiResult> ReorderItems(long id, [FromBody] List<ReorderAmoebaPLItemRequest> items)
    {
        try
        {
            var result = await _amoebaService.ReorderItemsAsync(id, items);
            if (!result)
                return ApiResult.Fail("模板不存在");
            return ApiResult.Ok("排序更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("templates/{id}/items/clone-from")]
    public async Task<ApiResult<AmoebaPLItemDto>> CloneItemFromTemplate(long id, [FromBody] CloneAmoebaPLItemRequest request)
    {
        try
        {
            var result = await _amoebaService.CloneItemFromTemplateAsync(id, request);
            return ApiResult<AmoebaPLItemDto>.Success(result, "复制损益项成功");
        }
        catch (ArgumentException ex)
        {
            return ApiResult<AmoebaPLItemDto>.Fail(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AmoebaPLItemDto>.Fail(ex.Message);
        }
    }

    #endregion

    #region Mapping Rules

    [HttpGet("mapping-rules")]
    public async Task<ApiResult<List<AmoebaMappingRuleDto>>> GetMappingRules()
    {
        var orgId = GetCurrentOrgId();
        var result = await _amoebaPLService.GetMappingRulesAsync(orgId);
        return ApiResult<List<AmoebaMappingRuleDto>>.Success(result);
    }

    [HttpPost("mapping-rules")]
    public async Task<ApiResult<AmoebaMappingRuleDto>> CreateMappingRule([FromBody] CreateMappingRuleRequest request)
    {
        var orgId = GetCurrentOrgId();
        var result = await _amoebaPLService.CreateMappingRuleAsync(request, orgId);
        return ApiResult<AmoebaMappingRuleDto>.Success(result, "创建映射规则成功");
    }

    [HttpPut("mapping-rules/{id}")]
    public async Task<ApiResult<AmoebaMappingRuleDto>> UpdateMappingRule(long id, [FromBody] CreateMappingRuleRequest request)
    {
        var orgId = GetCurrentOrgId();
        var result = await _amoebaPLService.UpdateMappingRuleAsync(id, request, orgId);
        if (result == null)
        {
            return ApiResult<AmoebaMappingRuleDto>.Fail("映射规则不存在");
        }
        return ApiResult<AmoebaMappingRuleDto>.Success(result, "更新映射规则成功");
    }

    [HttpDelete("mapping-rules/{id}")]
    public async Task<ApiResult> DeleteMappingRule(long id)
    {
        var orgId = GetCurrentOrgId();
        var result = await _amoebaPLService.DeleteMappingRuleAsync(id, orgId);
        if (!result)
        {
            return ApiResult.Fail("映射规则不存在");
        }
        return ApiResult.Ok("删除映射规则成功");
    }

    #endregion

    #region Manual Classification

    [HttpGet("unclassified")]
    public async Task<ApiResult<List<AmoebaUnclassifiedDto>>> GetUnclassified([FromQuery] DateTime startDate, [FromQuery] DateTime endDate, [FromQuery] long accountSetId = 0)
    {
        var orgId = GetCurrentOrgId();
        var result = await _amoebaPLService.GetUnclassifiedAsync(startDate, endDate, orgId, accountSetId);
        return ApiResult<List<AmoebaUnclassifiedDto>>.Success(result);
    }

    [HttpPost("classify")]
    public async Task<ApiResult> BatchClassify([FromBody] AmoebaBatchClassifyRequest request)
    {
        var orgId = GetCurrentOrgId();
        await _amoebaPLService.BatchClassifyAsync(request, orgId);
        return ApiResult.Ok("批量分类成功");
    }

    #endregion

    #region Coverage Check

    /// <summary>
    /// 模板科目覆盖率诊断：检查损益模板的关联科目是否覆盖了当期所有凭证科目
    /// </summary>
    [HttpGet("templates/{id}/coverage")]
    public async Task<ApiResult<AmoebaCoverageReportDto>> GetCoverageReport(long id, [FromQuery] string period, [FromQuery] long accountSetId = 0)
    {
        if (string.IsNullOrEmpty(period))
            return ApiResult<AmoebaCoverageReportDto>.Fail("请指定诊断期间（YYYYMM格式）");
        if (accountSetId <= 0)
            return ApiResult<AmoebaCoverageReportDto>.Fail("请指定账套ID");

        var orgId = GetCurrentOrgId();
        var result = await _amoebaPLService.GetCoverageReportAsync(id, period, accountSetId, orgId);
        return ApiResult<AmoebaCoverageReportDto>.Success(result);
    }

    #endregion

    private long GetCurrentOrgId()
    {
        var orgIdObj = HttpContext.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }
}
