using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Controllers;

/// <summary>
/// 车辆维修管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/vehicle/maintenances")]
public class VehicleMaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;

    public VehicleMaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    /// <summary>
    /// 获取维修记录列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(VehiclePermissions.MaintenanceView)]
    public async Task<ApiResult<PagedResult<VehicleMaintenanceListItemDto>>> GetList([FromQuery] MaintenanceQueryRequest request)
    {
        var result = await _maintenanceService.GetMaintenancesAsync(request);
        return ApiResult<PagedResult<VehicleMaintenanceListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取维修记录详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(VehiclePermissions.MaintenanceView)]
    public async Task<ApiResult<VehicleMaintenanceDto>> GetById(long id)
    {
        var result = await _maintenanceService.GetMaintenanceByIdAsync(id);
        if (result == null)
        {
            return ApiResult<VehicleMaintenanceDto>.Fail("维修记录不存在");
        }
        return ApiResult<VehicleMaintenanceDto>.Success(result);
    }

    /// <summary>
    /// 创建维修记录
    /// </summary>
    [HttpPost]
    [RequirePermission(VehiclePermissions.MaintenanceCreate)]
    public async Task<ApiResult<VehicleMaintenanceDto>> Create([FromBody] CreateMaintenanceRequest request)
    {
        try
        {
            var result = await _maintenanceService.CreateMaintenanceAsync(request);
            return ApiResult<VehicleMaintenanceDto>.Success(result, "创建维修记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleMaintenanceDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 完成维修
    /// </summary>
    [HttpPut("{id}/complete")]
    [RequirePermission(VehiclePermissions.MaintenanceEdit)]
    public async Task<ApiResult<VehicleMaintenanceDto>> Complete(long id, [FromBody] CompleteMaintenanceRequest request)
    {
        try
        {
            var result = await _maintenanceService.CompleteMaintenanceAsync(id, request);
            if (result == null)
            {
                return ApiResult<VehicleMaintenanceDto>.Fail("维修记录不存在");
            }
            return ApiResult<VehicleMaintenanceDto>.Success(result, "完成维修成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleMaintenanceDto>.Fail(ex.Message);
        }
    }
}
