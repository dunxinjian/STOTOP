using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Entities;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Services;

public class AssignmentService : IAssignmentService
{
    private readonly IRepository<VehAssignment> _assignmentRepository;
    private readonly IRepository<VehVehicle> _vehicleRepository;

    public AssignmentService(
        IRepository<VehAssignment> assignmentRepository,
        IRepository<VehVehicle> vehicleRepository)
    {
        _assignmentRepository = assignmentRepository;
        _vehicleRepository = vehicleRepository;
    }

    #region Assignment CRUD

    public async Task<PagedResult<VehicleAssignmentListItemDto>> GetAssignmentsAsync(AssignmentQueryRequest request)
    {
        var baseQuery = _assignmentRepository.Query();

        // 车辆ID筛选
        if (request.VehicleId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.FVehicleId == request.VehicleId.Value);
        }

        // 员工ID筛选
        if (request.EmployeeId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.FEmployeeId == request.EmployeeId.Value);
        }

        // 分配类型筛选
        if (request.AssignmentType.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.FAssignmentType == request.AssignmentType.Value);
        }

        // 分配状态筛选
        if (request.AssignmentStatus.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.FAssignmentStatus == request.AssignmentStatus.Value);
        }

        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .Include(a => a.Vehicle)
            .OrderByDescending(a => a.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<VehicleAssignmentListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<VehicleAssignmentDto?> GetAssignmentByIdAsync(long id)
    {
        var assignment = await _assignmentRepository.Query()
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.FID == id);

        return assignment == null ? null : MapToDto(assignment);
    }

    public async Task<VehicleAssignmentDto> CreateAssignmentAsync(CreateAssignmentRequest request)
    {
        // 验证车辆存在
        var vehicle = await _vehicleRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == request.VehicleId);

        if (vehicle == null)
        {
            throw new InvalidOperationException($"车辆不存在");
        }

        // 验证车辆状态为闲置(1)
        if (vehicle.FVehicleStatus != 1)
        {
            throw new InvalidOperationException($"车辆当前状态不是闲置，无法分配");
        }

        // 创建分配记录
        var assignment = new VehAssignment
        {
            FUID = Guid.NewGuid().ToString("N"),
            FVehicleId = request.VehicleId,
            FEmployeeId = request.EmployeeId,
            FEmployeeName = request.EmployeeName,
            FAssignmentType = request.AssignmentType,
            FStartDate = request.StartDate,
            FAssignmentStatus = 1, // 使用中
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _assignmentRepository.AddAsync(assignment);

        // 更新车辆状态为使用中(2)
        vehicle.FVehicleStatus = 2;
        vehicle.FUpdatedTime = DateTime.Now;
        await _vehicleRepository.UpdateAsync(vehicle);

        return (await GetAssignmentByIdAsync(assignment.FID))!;
    }

    public async Task<VehicleAssignmentDto?> ReturnVehicleAsync(long id, ReturnVehicleRequest request)
    {
        var assignment = await _assignmentRepository.Query()
            .AsTracking()
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.FID == id);

        if (assignment == null) return null;

        // 验证分配状态为使用中
        if (assignment.FAssignmentStatus != 1)
        {
            throw new InvalidOperationException($"该车辆已归还，无法重复归还");
        }

        // 更新分配记录
        assignment.FEndDate = request.EndDate;
        assignment.FAssignmentStatus = 2; // 已归还
        assignment.FRemark = string.IsNullOrEmpty(assignment.FRemark)
            ? request.Remark
            : $"{assignment.FRemark}; {request.Remark}";
        assignment.FUpdatedTime = DateTime.Now;

        await _assignmentRepository.UpdateAsync(assignment);

        // 更新车辆状态为闲置(1)
        var vehicle = await _vehicleRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == assignment.FVehicleId);

        if (vehicle != null)
        {
            vehicle.FVehicleStatus = 1;
            vehicle.FUpdatedTime = DateTime.Now;
            await _vehicleRepository.UpdateAsync(vehicle);
        }

        return await GetAssignmentByIdAsync(id);
    }

    #endregion

    #region Mapping

    private static VehicleAssignmentDto MapToDto(VehAssignment entity)
    {
        return new VehicleAssignmentDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            VehicleId = entity.FVehicleId,
            VehicleCode = entity.Vehicle?.FCode,
            VehiclePlateNumber = entity.Vehicle?.FPlateNumber,
            EmployeeId = entity.FEmployeeId,
            EmployeeName = entity.FEmployeeName,
            AssignmentType = entity.FAssignmentType,
            StartDate = entity.FStartDate,
            EndDate = entity.FEndDate,
            AssignmentStatus = entity.FAssignmentStatus,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static VehicleAssignmentListItemDto MapToListItemDto(VehAssignment entity)
    {
        return new VehicleAssignmentListItemDto
        {
            Id = entity.FID,
            VehicleId = entity.FVehicleId,
            VehicleCode = entity.Vehicle?.FCode,
            VehiclePlateNumber = entity.Vehicle?.FPlateNumber,
            EmployeeId = entity.FEmployeeId,
            EmployeeName = entity.FEmployeeName,
            AssignmentType = entity.FAssignmentType,
            StartDate = entity.FStartDate,
            EndDate = entity.FEndDate,
            AssignmentStatus = entity.FAssignmentStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
