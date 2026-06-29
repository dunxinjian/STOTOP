using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 成本项目管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/cost-items")]
public class CostItemController : ControllerBase
{
    private readonly ICostItemService _costItemService;

    public CostItemController(ICostItemService costItemService)
    {
        _costItemService = costItemService;
    }

    /// <summary>
    /// 获取全部成本项目列表
    /// </summary>
    [HttpGet]
    [RequirePermission(ExpressPermissions.CostItemView)]
    public async Task<ApiResult<List<CostItemDto>>> GetAll()
    {
        var result = await _costItemService.GetAllAsync();
        return ApiResult<List<CostItemDto>>.Success(result);
    }

    /// <summary>
    /// 创建成本项目
    /// </summary>
    [HttpPost]
    [RequirePermission(ExpressPermissions.CostItemCreate)]
    public async Task<ApiResult<CostItemDto>> Create([FromBody] CreateCostItemRequest request)
    {
        try
        {
            var result = await _costItemService.CreateAsync(request);
            return ApiResult<CostItemDto>.Success(result, "创建成本项目成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostItemDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新成本项目
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(ExpressPermissions.CostItemEdit)]
    public async Task<ApiResult<CostItemDto>> Update(int id, [FromBody] UpdateCostItemRequest request)
    {
        try
        {
            var result = await _costItemService.UpdateAsync(id, request);
            if (result == null)
                return ApiResult<CostItemDto>.Fail("成本项目不存在");
            return ApiResult<CostItemDto>.Success(result, "更新成本项目成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CostItemDto>.Fail(ex.Message);
        }
    }
}
