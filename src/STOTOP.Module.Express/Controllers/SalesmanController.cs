using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 业务员管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/salesmen")]
public class SalesmanController : ControllerBase
{
    private readonly ISalesmanService _service;

    public SalesmanController(ISalesmanService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<SalesmanDto>>> GetList([FromQuery] SalesmanQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<SalesmanDto>>.Success(result);
    }

    [HttpGet("{no}")]
    public async Task<ApiResult<SalesmanDto>> GetByNo(string no)
    {
        var result = await _service.GetByNoAsync(no);
        if (result == null)
            return ApiResult<SalesmanDto>.Fail("业务员不存在");
        return ApiResult<SalesmanDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<SalesmanDto>> Create([FromBody] CreateSalesmanRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<SalesmanDto>.Success(result, "创建业务员成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<SalesmanDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{no}")]
    public async Task<ApiResult<SalesmanDto>> Update(string no, [FromBody] UpdateSalesmanRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(no, request);
            if (result == null)
                return ApiResult<SalesmanDto>.Fail("业务员不存在");
            return ApiResult<SalesmanDto>.Success(result, "更新业务员成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<SalesmanDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{no}")]
    public async Task<ApiResult> Delete(string no)
    {
        var result = await _service.DeleteAsync(no);
        if (!result)
            return ApiResult.Fail("业务员不存在");
        return ApiResult.Ok("删除业务员成功");
    }

    /// <summary>
    /// 获取可选的HR员工候选人列表
    /// </summary>
    [HttpGet("candidates")]
    public async Task<ApiResult<List<HrEmployeeCandidateDto>>> GetCandidates([FromQuery] string networkPointCode)
    {
        var result = await _service.GetCandidatesAsync(networkPointCode);
        return ApiResult<List<HrEmployeeCandidateDto>>.Success(result);
    }
}
