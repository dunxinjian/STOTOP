using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;
using STOTOP.Module.HR.Entities;

namespace STOTOP.Module.Dormitory.Services;

public class BuildingService : IBuildingService
{
    private readonly IRepository<DorBuilding> _buildingRepository;
    private readonly IRepository<HrEmployee> _employeeRepository;

    public BuildingService(
        IRepository<DorBuilding> buildingRepository,
        IRepository<HrEmployee> employeeRepository)
    {
        _buildingRepository = buildingRepository;
        _employeeRepository = employeeRepository;
    }

    #region Building CRUD

    public async Task<PagedResult<BuildingListItemDto>> GetBuildingsAsync(BuildingQueryRequest request)
    {
        var query = _buildingRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(b => b.FCode.Contains(keyword) || b.FName.Contains(keyword));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(b => b.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .Include(b => b.Rooms)
            .ThenInclude(r => r.Beds)
            .OrderByDescending(b => b.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<BuildingListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<BuildingListItemDto>> GetAllEnabledBuildingsAsync()
    {
        var buildings = await _buildingRepository.Query()
            .Where(b => b.FStatus == 1)
            .OrderBy(b => b.FCode)
            .ToListAsync();

        return buildings.Select(MapToListItemDto).ToList();
    }

    public async Task<BuildingDto?> GetBuildingByIdAsync(long id)
    {
        var building = await _buildingRepository.Query()
            .Include(b => b.Rooms)
            .ThenInclude(r => r.Beds)
            .FirstOrDefaultAsync(b => b.FID == id);

        if (building == null) return null;
        var dto = MapToDto(building);
        if (building.FManagerId.HasValue)
        {
            dto.ManagerName = await _employeeRepository.Query()
                .Where(e => e.FID == building.FManagerId.Value)
                .Select(e => e.FName)
                .FirstOrDefaultAsync();
        }
        return dto;
    }

    public async Task<BuildingDto> CreateBuildingAsync(CreateBuildingRequest request)
    {
        // 验证编码唯一性
        var exists = await _buildingRepository.Query()
            .AnyAsync(b => b.FCode == request.Code);
        if (exists)
        {
            throw new InvalidOperationException($"楼栋编码 {request.Code} 已存在");
        }

        var building = new DorBuilding
        {
            FUID = Guid.NewGuid().ToString("N"),
            FCode = request.Code,
            FName = request.Name,
            FAddress = request.Address,
            FTotalFloors = request.TotalFloors,
            FManagerId = request.ManagerId,
            FDormitoryType = request.DormitoryType,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _buildingRepository.AddAsync(building);

        return (await GetBuildingByIdAsync(building.FID))!;
    }

    public async Task<BuildingDto?> UpdateBuildingAsync(long id, UpdateBuildingRequest request)
    {
        var building = await _buildingRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == id);

        if (building == null) return null;

        // 验证编码唯一性（排除自身）
        var codeExists = await _buildingRepository.Query()
            .AnyAsync(b => b.FCode == request.Code && b.FID != id);
        if (codeExists)
        {
            throw new InvalidOperationException($"楼栋编码 {request.Code} 已存在");
        }

        building.FCode = request.Code;
        building.FName = request.Name;
        building.FAddress = request.Address;
        building.FTotalFloors = request.TotalFloors;
        building.FManagerId = request.ManagerId;
        building.FDormitoryType = request.DormitoryType;
        building.FRemark = request.Remark;
        building.FUpdatedTime = DateTime.Now;

        await _buildingRepository.UpdateAsync(building);

        return await GetBuildingByIdAsync(id);
    }

    public async Task<bool> DeleteBuildingAsync(long id)
    {
        var building = await _buildingRepository.GetByIdAsync(id);
        if (building == null) return false;

        await _buildingRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, int status)
    {
        var building = await _buildingRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == id);

        if (building == null) return false;

        building.FStatus = status;
        building.FUpdatedTime = DateTime.Now;
        await _buildingRepository.UpdateAsync(building);
        return true;
    }

    public async Task<bool> CheckCodeExistsAsync(string code, long excludeId)
    {
        return await _buildingRepository.Query()
            .AnyAsync(b => b.FCode == code && b.FID != excludeId);
    }

    #endregion

    #region Mapping

    private static BuildingDto MapToDto(DorBuilding entity)
    {
        return new BuildingDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            Code = entity.FCode,
            Name = entity.FName,
            Address = entity.FAddress,
            TotalFloors = entity.FTotalFloors,
            ManagerId = entity.FManagerId,
            DormitoryType = entity.FDormitoryType,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatorId = entity.FCreatorId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            Rooms = entity.Rooms.Select(MapRoomToDto).ToList()
        };
    }

    private static BuildingListItemDto MapToListItemDto(DorBuilding entity)
    {
        return new BuildingListItemDto
        {
            Id = entity.FID,
            Code = entity.FCode,
            Name = entity.FName,
            Address = entity.FAddress,
            TotalFloors = entity.FTotalFloors,
            ManagerId = entity.FManagerId,
            DormitoryType = entity.FDormitoryType,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            RoomCount = entity.Rooms.Count,
            BedCount = entity.Rooms.Sum(r => r.Beds.Count),
            // 床位状态 2=已入住（入住/退宿时联动维护），据此统计占用
            OccupiedBeds = entity.Rooms.Sum(r => r.Beds.Count(b => b.FStatus == DorStatus.Bed.Occupied))
        };
    }

    private static RoomDto MapRoomToDto(DorRoom entity)
    {
        return new RoomDto
        {
            Id = entity.FID,
            BuildingId = entity.FBuildingId,
            BuildingName = entity.Building?.FName ?? string.Empty,
            Floor = entity.FFloor,
            RoomNumber = entity.FRoomNumber,
            BedsCount = entity.FBedsCount,
            RoomType = entity.FRoomType,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            Beds = entity.Beds.Select(MapBedToDto).ToList()
        };
    }

    private static BedDto MapBedToDto(DorBed entity)
    {
        return new BedDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = entity.Room?.FRoomNumber ?? string.Empty,
            BedNumber = entity.FBedNumber,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion
}
