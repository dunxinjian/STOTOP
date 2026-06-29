using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Controllers;

/// <summary>
/// 租赁费用标准管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/vehicle/rental-standards")]
public class RentalStandardController : ControllerBase
{
    private readonly IRentalStandardService _rentalStandardService;

    public RentalStandardController(IRentalStandardService rentalStandardService)
    {
        _rentalStandardService = rentalStandardService;
    }

    /// <summary>
    /// 获取租赁费用标准列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(VehiclePermissions.RentalStandardView)]
    public async Task<ApiResult<PagedResult<RentalStandardListItemDto>>> GetList([FromQuery] RentalStandardQueryRequest request)
    {
        var result = await _rentalStandardService.GetStandardsAsync(request);
        return ApiResult<PagedResult<RentalStandardListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取租赁费用标准详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(VehiclePermissions.RentalStandardView)]
    public async Task<ApiResult<RentalStandardDto>> GetById(long id)
    {
        var result = await _rentalStandardService.GetStandardByIdAsync(id);
        if (result == null)
        {
            return ApiResult<RentalStandardDto>.Fail("租赁费用标准不存在");
        }
        return ApiResult<RentalStandardDto>.Success(result);
    }

    /// <summary>
    /// 创建租赁费用标准
    /// </summary>
    [HttpPost]
    [RequirePermission(VehiclePermissions.RentalStandardCreate)]
    public async Task<ApiResult<RentalStandardDto>> Create([FromBody] CreateRentalStandardRequest request)
    {
        try
        {
            var result = await _rentalStandardService.CreateStandardAsync(request);
            return ApiResult<RentalStandardDto>.Success(result, "创建租赁费用标准成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RentalStandardDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新租赁费用标准
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(VehiclePermissions.RentalStandardEdit)]
    public async Task<ApiResult<RentalStandardDto>> Update(long id, [FromBody] UpdateRentalStandardRequest request)
    {
        try
        {
            var result = await _rentalStandardService.UpdateStandardAsync(id, request);
            if (result == null)
            {
                return ApiResult<RentalStandardDto>.Fail("租赁费用标准不存在");
            }
            return ApiResult<RentalStandardDto>.Success(result, "更新租赁费用标准成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RentalStandardDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新租赁费用标准状态
    /// </summary>
    [HttpPut("{id}/status")]
    [RequirePermission(VehiclePermissions.RentalStandardEdit)]
    public async Task<ApiResult> UpdateStatus(long id, [FromBody] int status)
    {
        try
        {
            var result = await _rentalStandardService.UpdateStatusAsync(id, status);
            if (!result)
            {
                return ApiResult.Fail("租赁费用标准不存在");
            }
            return ApiResult.Ok("更新状态成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取所有启用的租赁费用标准
    /// </summary>
    [HttpGet("enabled")]
    [RequirePermission(VehiclePermissions.RentalStandardView)]
    public async Task<ApiResult<List<RentalStandardListItemDto>>> GetAllEnabled()
    {
        var result = await _rentalStandardService.GetAllEnabledStandardsAsync();
        return ApiResult<List<RentalStandardListItemDto>>.Success(result);
    }
}
