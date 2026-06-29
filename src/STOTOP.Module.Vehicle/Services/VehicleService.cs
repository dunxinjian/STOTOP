using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Entities;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Services;

public class VehicleService : IVehicleService
{
    private readonly IRepository<VehVehicle> _vehicleRepository;

    public VehicleService(IRepository<VehVehicle> vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    #region Vehicle CRUD

    public async Task<PagedResult<VehicleListItemDto>> GetVehiclesAsync(VehicleQueryRequest request)
    {
        var query = _vehicleRepository.Query();

        // 关键字搜索（编码/车牌号模糊搜索）
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(v => v.FCode.Contains(keyword) || (v.FPlateNumber != null && v.FPlateNumber.Contains(keyword)));
        }

        // 权属类型筛选
        if (request.OwnershipType.HasValue)
        {
            query = query.Where(v => v.FOwnershipType == request.OwnershipType.Value);
        }

        // 车辆状态筛选
        if (request.VehicleStatus.HasValue)
        {
            query = query.Where(v => v.FVehicleStatus == request.VehicleStatus.Value);
        }

        // 状态筛选
        if (request.Status.HasValue)
        {
            query = query.Where(v => v.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<VehicleListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<VehicleDto?> GetVehicleByIdAsync(long id)
    {
        var vehicle = await _vehicleRepository.Query()
            .Include(v => v.Assignments)
            .Include(v => v.Maintenances)
            .FirstOrDefaultAsync(v => v.FID == id);

        return vehicle == null ? null : MapToDto(vehicle);
    }

    public async Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request)
    {
        // 验证编码唯一性
        var exists = await _vehicleRepository.Query()
            .AnyAsync(v => v.FCode == request.Code);
        if (exists)
        {
            throw new InvalidOperationException($"车辆编码 {request.Code} 已存在");
        }

        var vehicle = new VehVehicle
        {
            FUID = Guid.NewGuid().ToString("N"),
            FCode = request.Code,
            FPlateNumber = request.PlateNumber,
            FBrand = request.Brand,
            FFrameNumber = request.FrameNumber,
            FOwnershipType = request.OwnershipType,
            FOwnerId = request.OwnerId,
            FOwnerName = request.OwnerName,
            FPurchaseDate = request.PurchaseDate,
            FPurchasePrice = request.PurchasePrice,
            FVehicleStatus = 1, // 默认闲置
            FColor = request.Color,
            FGpsDeviceNo = request.GpsDeviceNo,
            FImage = request.Image,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _vehicleRepository.AddAsync(vehicle);

        return (await GetVehicleByIdAsync(vehicle.FID))!;
    }

    public async Task<VehicleDto?> UpdateVehicleAsync(long id, UpdateVehicleRequest request)
    {
        var vehicle = await _vehicleRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == id);

        if (vehicle == null) return null;

        // 验证编码唯一性（排除自身）
        var codeExists = await _vehicleRepository.Query()
            .AnyAsync(v => v.FCode == request.Code && v.FID != id);
        if (codeExists)
        {
            throw new InvalidOperationException($"车辆编码 {request.Code} 已存在");
        }

        vehicle.FCode = request.Code;
        vehicle.FPlateNumber = request.PlateNumber;
        vehicle.FBrand = request.Brand;
        vehicle.FFrameNumber = request.FrameNumber;
        vehicle.FOwnershipType = request.OwnershipType;
        vehicle.FOwnerId = request.OwnerId;
        vehicle.FOwnerName = request.OwnerName;
        vehicle.FPurchaseDate = request.PurchaseDate;
        vehicle.FPurchasePrice = request.PurchasePrice;
        vehicle.FVehicleStatus = request.VehicleStatus;
        vehicle.FColor = request.Color;
        vehicle.FGpsDeviceNo = request.GpsDeviceNo;
        vehicle.FImage = request.Image;
        vehicle.FRemark = request.Remark;
        vehicle.FUpdatedTime = DateTime.Now;

        await _vehicleRepository.UpdateAsync(vehicle);

        return await GetVehicleByIdAsync(id);
    }

    public async Task<bool> DeleteVehicleAsync(long id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);
        if (vehicle == null) return false;

        await _vehicleRepository.DeleteAsync(id);
        return true;
    }

    public async Task<VehicleStatisticsDto> GetStatisticsAsync()
    {
        var vehicles = await _vehicleRepository.Query()
            .Where(v => v.FStatus == 1)
            .ToListAsync();

        var statistics = new VehicleStatisticsDto
        {
            TotalCount = vehicles.Count,
            IdleCount = vehicles.Count(v => v.FVehicleStatus == 1),
            InUseCount = vehicles.Count(v => v.FVehicleStatus == 2),
            MaintenanceCount = vehicles.Count(v => v.FVehicleStatus == 3),
            ScrapCount = vehicles.Count(v => v.FVehicleStatus == 4),
            CompanyOwnedCount = vehicles.Count(v => v.FOwnershipType == 1),
            PersonalOwnedCount = vehicles.Count(v => v.FOwnershipType == 2)
        };

        // 按状态分组
        statistics.ByStatus = vehicles
            .GroupBy(v => v.FVehicleStatus)
            .Select(g => new VehicleStatusGroupDto
            {
                VehicleStatus = g.Key,
                StatusName = GetVehicleStatusName(g.Key),
                Count = g.Count()
            })
            .ToList();

        // 按权属分组
        statistics.ByOwnership = vehicles
            .GroupBy(v => v.FOwnershipType)
            .Select(g => new VehicleOwnershipGroupDto
            {
                OwnershipType = g.Key,
                OwnershipName = GetOwnershipTypeName(g.Key),
                Count = g.Count()
            })
            .ToList();

        return statistics;
    }

    public async Task<bool> CheckCodeExistsAsync(string code, long excludeId = 0)
    {
        return await _vehicleRepository.Query()
            .AnyAsync(v => v.FCode == code && v.FID != excludeId);
    }

    #endregion

    #region Mapping

    private static VehicleDto MapToDto(VehVehicle entity)
    {
        // 获取当前有效分配（状态为使用中）
        var currentAssignment = entity.Assignments
            .Where(a => a.FAssignmentStatus == 1)
            .OrderByDescending(a => a.FStartDate)
            .FirstOrDefault();

        return new VehicleDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            Code = entity.FCode,
            PlateNumber = entity.FPlateNumber,
            Brand = entity.FBrand,
            FrameNumber = entity.FFrameNumber,
            OwnershipType = entity.FOwnershipType,
            OwnerId = entity.FOwnerId,
            OwnerName = entity.FOwnerName,
            PurchaseDate = entity.FPurchaseDate,
            PurchasePrice = entity.FPurchasePrice,
            VehicleStatus = entity.FVehicleStatus,
            Color = entity.FColor,
            GpsDeviceNo = entity.FGpsDeviceNo,
            Image = entity.FImage,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            AssignmentCount = entity.Assignments.Count,
            MaintenanceCount = entity.Maintenances.Count,
            CurrentAssignment = currentAssignment != null ? MapAssignmentToDto(currentAssignment) : null
        };
    }

    private static VehicleListItemDto MapToListItemDto(VehVehicle entity)
    {
        return new VehicleListItemDto
        {
            Id = entity.FID,
            Code = entity.FCode,
            PlateNumber = entity.FPlateNumber,
            Brand = entity.FBrand,
            OwnershipType = entity.FOwnershipType,
            OwnerName = entity.FOwnerName,
            VehicleStatus = entity.FVehicleStatus,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static VehicleAssignmentDto MapAssignmentToDto(VehAssignment entity)
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

    #endregion

    #region Helper Methods

    private static string GetVehicleStatusName(int status)
    {
        return status switch
        {
            1 => "闲置",
            2 => "使用中",
            3 => "维修中",
            4 => "报废",
            _ => "未知"
        };
    }

    private static string GetOwnershipTypeName(int type)
    {
        return type switch
        {
            1 => "公司",
            2 => "员工个人",
            _ => "未知"
        };
    }

    #endregion
}
