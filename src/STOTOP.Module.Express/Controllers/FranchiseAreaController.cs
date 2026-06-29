using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 承包区管理（新版独立实体）
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/franchise-areas")]
public class FranchiseAreaController : ControllerBase
{
    private readonly IFranchiseAreaService _service;

    public FranchiseAreaController(IFranchiseAreaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<FranchiseAreaDto>>> GetList([FromQuery] FranchiseAreaQueryRequest request)
    {
        var result = await _service.GetListAsync(request);
        return ApiResult<PagedResult<FranchiseAreaDto>>.Success(result);
    }

    [HttpGet("{code}")]
    public async Task<ApiResult<FranchiseAreaDto>> GetByCode(string code)
    {
        var result = await _service.GetByIdAsync(code);
        if (result == null)
            return ApiResult<FranchiseAreaDto>.Fail("承包区不存在");
        return ApiResult<FranchiseAreaDto>.Success(result);
    }

    [HttpGet("check-code")]
    public async Task<ApiResult<bool>> CheckCodeExists([FromQuery] string code)
    {
        var exists = await _service.CheckCodeExistsAsync(code);
        return ApiResult<bool>.Success(exists);
    }

    [HttpPost]
    public async Task<ApiResult<FranchiseAreaDto>> Create([FromBody] CreateFranchiseAreaRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return ApiResult<FranchiseAreaDto>.Success(result, "创建承包区成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FranchiseAreaDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{code}")]
    public async Task<ApiResult<FranchiseAreaDto>> Update(string code, [FromBody] UpdateFranchiseAreaRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(code, request);
            if (result == null)
                return ApiResult<FranchiseAreaDto>.Fail("承包区不存在");
            return ApiResult<FranchiseAreaDto>.Success(result, "更新承包区成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<FranchiseAreaDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{code}")]
    public async Task<ApiResult> Delete(string code)
    {
        var result = await _service.DeleteAsync(code);
        if (!result)
            return ApiResult.Fail("承包区不存在");
        return ApiResult.Ok("删除承包区成功");
    }
}
