using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Controllers;

/// <summary>
/// 车辆分配管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/vehicle/assignments")]
public class VehicleAssignmentController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public VehicleAssignmentController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    /// <summary>
    /// 获取车辆分配列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(VehiclePermissions.AssignmentView)]
    public async Task<ApiResult<PagedResult<VehicleAssignmentListItemDto>>> GetList([FromQuery] AssignmentQueryRequest request)
    {
        var result = await _assignmentService.GetAssignmentsAsync(request);
        return ApiResult<PagedResult<VehicleAssignmentListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取车辆分配详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(VehiclePermissions.AssignmentView)]
    public async Task<ApiResult<VehicleAssignmentDto>> GetById(long id)
    {
        var result = await _assignmentService.GetAssignmentByIdAsync(id);
        if (result == null)
        {
            return ApiResult<VehicleAssignmentDto>.Fail("车辆分配记录不存在");
        }
        return ApiResult<VehicleAssignmentDto>.Success(result);
    }

    /// <summary>
    /// 创建车辆分配
    /// </summary>
    [HttpPost]
    [RequirePermission(VehiclePermissions.AssignmentCreate)]
    public async Task<ApiResult<VehicleAssignmentDto>> Create([FromBody] CreateAssignmentRequest request)
    {
        try
        {
            var result = await _assignmentService.CreateAssignmentAsync(request);
            return ApiResult<VehicleAssignmentDto>.Success(result, "分配车辆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleAssignmentDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 归还车辆
    /// </summary>
    [HttpPut("{id}/return")]
    [RequirePermission(VehiclePermissions.AssignmentEdit)]
    public async Task<ApiResult<VehicleAssignmentDto>> Return(long id, [FromBody] ReturnVehicleRequest request)
    {
        try
        {
            var result = await _assignmentService.ReturnVehicleAsync(id, request);
            if (result == null)
            {
                return ApiResult<VehicleAssignmentDto>.Fail("车辆分配记录不存在");
            }
            return ApiResult<VehicleAssignmentDto>.Success(result, "归还车辆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleAssignmentDto>.Fail(ex.Message);
        }
    }
}
