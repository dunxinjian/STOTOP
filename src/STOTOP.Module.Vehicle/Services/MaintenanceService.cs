using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Entities;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IRepository<VehMaintenance> _maintenanceRepository;
    private readonly IRepository<VehVehicle> _vehicleRepository;

    public MaintenanceService(
        IRepository<VehMaintenance> maintenanceRepository,
        IRepository<VehVehicle> vehicleRepository)
    {
        _maintenanceRepository = maintenanceRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<PagedResult<VehicleMaintenanceListItemDto>> GetMaintenancesAsync(MaintenanceQueryRequest request)
    {
        var query = _maintenanceRepository.Query()
            .Include(m => m.Vehicle)
            .AsQueryable();

        if (request.VehicleId.HasValue)
        {
            query = query.Where(m => m.FVehicleId == request.VehicleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.MaintenanceType))
        {
            query = query.Where(m => m.FMaintenanceType != null && m.FMaintenanceType.Contains(request.MaintenanceType));
        }

        if (request.MaintenanceStatus.HasValue)
        {
            query = query.Where(m => m.FMaintenanceStatus == request.MaintenanceStatus.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(m => m.FMaintenanceDate >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(m => m.FMaintenanceDate <= request.EndDate.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<VehicleMaintenanceListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<VehicleMaintenanceDto?> GetMaintenanceByIdAsync(long id)
    {
        var maintenance = await _maintenanceRepository.Query()
            .Include(m => m.Vehicle)
            .FirstOrDefaultAsync(m => m.FID == id);

        return maintenance == null ? null : MapToDto(maintenance);
    }

    public async Task<VehicleMaintenanceDto> CreateMaintenanceAsync(CreateMaintenanceRequest request)
    {
        // 验证车辆存在
        var vehicle = await _vehicleRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == request.VehicleId);

        if (vehicle == null)
        {
            throw new InvalidOperationException($"车辆 ID {request.VehicleId} 不存在");
        }

        // 如果车辆状态不是维修中(3)，则更新为维修中
        if (vehicle.FVehicleStatus != 3)
        {
            vehicle.FVehicleStatus = 3;
            vehicle.FUpdatedTime = DateTime.Now;
            await _vehicleRepository.UpdateAsync(vehicle);
        }

        var maintenance = new VehMaintenance
        {
            FUID = Guid.NewGuid().ToString("N"),
            FVehicleId = request.VehicleId,
            FMaintenanceDate = request.MaintenanceDate,
            FMaintenanceType = request.MaintenanceType,
            FMaintenanceItem = request.MaintenanceItem,
            FMaintenanceUnit = request.MaintenanceUnit,
            FMaintenanceCost = request.MaintenanceCost,
            FCostBearer = request.CostBearer,
            FMaintenanceStatus = 1, // 维修中
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _maintenanceRepository.AddAsync(maintenance);

        return (await GetMaintenanceByIdAsync(maintenance.FID))!;
    }

    public async Task<VehicleMaintenanceDto?> CompleteMaintenanceAsync(long id, CompleteMaintenanceRequest request)
    {
        var maintenance = await _maintenanceRepository.Query()
            .AsTracking()
            .Include(m => m.Vehicle)
            .FirstOrDefaultAsync(m => m.FID == id);

        if (maintenance == null) return null;

        // 更新维修状态为已完成
        maintenance.FMaintenanceStatus = 2; // 已完成
        maintenance.FCompletionDate = request.CompletionDate;
        maintenance.FRemark = request.Remark;
        maintenance.FUpdatedTime = DateTime.Now;

        await _maintenanceRepository.UpdateAsync(maintenance);

        // 检查该车辆是否还有其他未完成的维修
        var hasUnfinishedMaintenance = await _maintenanceRepository.Query()
            .AnyAsync(m => m.FVehicleId == maintenance.FVehicleId &&
                           m.FID != id &&
                           m.FMaintenanceStatus == 1); // 维修中

        // 如果没有其他未完成的维修，恢复车辆状态为闲置(1)
        if (!hasUnfinishedMaintenance)
        {
            var vehicle = await _vehicleRepository.Query()
                .AsTracking()
                .FirstOrDefaultAsync(v => v.FID == maintenance.FVehicleId);

            if (vehicle != null)
            {
                vehicle.FVehicleStatus = 1; // 闲置
                vehicle.FUpdatedTime = DateTime.Now;
                await _vehicleRepository.UpdateAsync(vehicle);
            }
        }

        return await GetMaintenanceByIdAsync(id);
    }

    #region Mapping

    private static VehicleMaintenanceDto MapToDto(VehMaintenance entity)
    {
        return new VehicleMaintenanceDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            VehicleId = entity.FVehicleId,
            MaintenanceDate = entity.FMaintenanceDate,
            MaintenanceType = entity.FMaintenanceType,
            MaintenanceItem = entity.FMaintenanceItem,
            MaintenanceUnit = entity.FMaintenanceUnit,
            MaintenanceCost = entity.FMaintenanceCost,
            CostBearer = entity.FCostBearer,
            CompletionDate = entity.FCompletionDate,
            MaintenanceStatus = entity.FMaintenanceStatus,
            Remark = entity.FRemark,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static VehicleMaintenanceListItemDto MapToListItemDto(VehMaintenance entity)
    {
        return new VehicleMaintenanceListItemDto
        {
            Id = entity.FID,
            VehicleId = entity.FVehicleId,
            VehicleCode = entity.Vehicle?.FCode ?? string.Empty,
            MaintenanceDate = entity.FMaintenanceDate,
            MaintenanceType = entity.FMaintenanceType,
            MaintenanceItem = entity.FMaintenanceItem,
            MaintenanceCost = entity.FMaintenanceCost,
            CostBearer = entity.FCostBearer,
            MaintenanceStatus = entity.FMaintenanceStatus
        };
    }

    #endregion
}
