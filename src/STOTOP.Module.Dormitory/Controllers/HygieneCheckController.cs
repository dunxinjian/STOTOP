using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/hygiene-checks")]
public class HygieneCheckController : ControllerBase
{
    private readonly IHygieneCheckService _hygieneCheckService;

    public HygieneCheckController(IHygieneCheckService hygieneCheckService)
    {
        _hygieneCheckService = hygieneCheckService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<HygieneCheckListItemDto>>> GetList([FromQuery] HygieneCheckQueryRequest request)
    {
        var result = await _hygieneCheckService.GetHygieneChecksAsync(request);
        return ApiResult<PagedResult<HygieneCheckListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<HygieneCheckDto>> GetById(long id)
    {
        var result = await _hygieneCheckService.GetHygieneCheckByIdAsync(id);
        if (result == null)
        {
            return ApiResult<HygieneCheckDto>.Fail("卫生检查记录不存在");
        }
        return ApiResult<HygieneCheckDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<HygieneCheckDto>> Create([FromBody] CreateHygieneCheckRequest request)
    {
        try
        {
            var result = await _hygieneCheckService.CreateHygieneCheckAsync(request);
            return ApiResult<HygieneCheckDto>.Success(result, "创建卫生检查记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<HygieneCheckDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<HygieneCheckDto>> Update(long id, [FromBody] UpdateHygieneCheckRequest request)
    {
        var result = await _hygieneCheckService.UpdateHygieneCheckAsync(id, request);
        if (result == null)
        {
            return ApiResult<HygieneCheckDto>.Fail("卫生检查记录不存在");
        }
        return ApiResult<HygieneCheckDto>.Success(result, "更新卫生检查记录成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _hygieneCheckService.DeleteHygieneCheckAsync(id);
        if (!result)
        {
            return ApiResult.Fail("卫生检查记录不存在");
        }
        return ApiResult.Ok("删除卫生检查记录成功");
    }
}
