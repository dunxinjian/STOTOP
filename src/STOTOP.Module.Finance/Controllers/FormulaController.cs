using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/formulas")]
public class FormulaController : ControllerBase
{
    private readonly IFormulaService _formulaService;

    public FormulaController(IFormulaService formulaService)
    {
        _formulaService = formulaService;
    }

    /// <summary>
    /// 获取公式列表
    /// </summary>
    [HttpGet]
    public async Task<ApiResult<List<FormulaDto>>> GetFormulas(
        [FromQuery] string reportType,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _formulaService.GetByReportTypeAsync(reportType, accountSetId);
        return ApiResult<List<FormulaDto>>.Success(result);
    }

    /// <summary>
    /// 新建公式
    /// </summary>
    [HttpPost]
    public async Task<ApiResult<FormulaDto>> CreateFormula([FromBody] CreateFormulaRequest request)
    {
        var result = await _formulaService.CreateAsync(request);
        return ApiResult<FormulaDto>.Success(result);
    }

    /// <summary>
    /// 修改公式
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiResult<FormulaDto>> UpdateFormula(long id, [FromBody] UpdateFormulaRequest request)
    {
        var result = await _formulaService.UpdateAsync(id, request);
        if (result == null)
            return ApiResult<FormulaDto>.Fail("公式不存在", 404);
        return ApiResult<FormulaDto>.Success(result);
    }

    /// <summary>
    /// 删除公式
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult> DeleteFormula(long id)
    {
        var success = await _formulaService.DeleteAsync(id);
        if (!success)
            return ApiResult.Fail("公式不存在", 404);
        return ApiResult.Ok("删除成功");
    }

    /// <summary>
    /// 测试公式
    /// </summary>
    [HttpPost("test")]
    public async Task<ApiResult<FormulaTestResponse>> TestFormula([FromBody] FormulaTestRequest request)
    {
        var result = await _formulaService.TestFormulaAsync(request);
        return ApiResult<FormulaTestResponse>.Success(result);
    }

    /// <summary>
    /// 初始化默认公式
    /// </summary>
    [HttpPost("init-defaults")]
    public async Task<ApiResult<int>> InitDefaults([FromBody] InitDefaultFormulasRequest request)
    {
        var count = await _formulaService.InitDefaultFormulasAsync(request.ReportType, request.AccountSetId);
        if (count == 0)
            return ApiResult<int>.Success(0, "该报表类型已存在公式，无需初始化");
        return ApiResult<int>.Success(count, $"成功初始化 {count} 条公式");
    }
}
