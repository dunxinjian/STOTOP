using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Services;

public class RoomService : IRoomService
{
    private readonly IRepository<DorRoom> _roomRepository;
    private readonly IRepository<DorBuilding> _buildingRepository;

    public RoomService(
        IRepository<DorRoom> roomRepository,
        IRepository<DorBuilding> buildingRepository)
    {
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
    }

    #region Room CRUD

    public async Task<PagedResult<RoomListItemDto>> GetRoomsAsync(long buildingId, RoomQueryRequest request)
    {
        var query = _roomRepository.Query()
            .Where(r => r.FBuildingId == buildingId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(r => r.FRoomNumber.Contains(keyword));
        }

        if (request.Floor.HasValue)
        {
            query = query.Where(r => r.FFloor == request.Floor.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .Include(r => r.Building)
            .Include(r => r.Beds)
            .OrderBy(r => r.FFloor)
            .ThenBy(r => r.FRoomNumber)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<RoomListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<RoomListItemDto>> GetAllEnabledRoomsAsync(long buildingId)
    {
        var rooms = await _roomRepository.Query()
            .Where(r => r.FBuildingId == buildingId && r.FStatus == 1)
            .Include(r => r.Building)
            .Include(r => r.Beds)
            .OrderBy(r => r.FFloor)
            .ThenBy(r => r.FRoomNumber)
            .ToListAsync();

        return rooms.Select(MapToListItemDto).ToList();
    }

    public async Task<RoomDto?> GetRoomByIdAsync(long id)
    {
        var room = await _roomRepository.Query()
            .Include(r => r.Building)
            .Include(r => r.Beds)
            .FirstOrDefaultAsync(r => r.FID == id);

        return room == null ? null : MapToDto(room);
    }

    public async Task<RoomDto> CreateRoomAsync(long buildingId, CreateRoomRequest request)
    {
        // 验证楼栋是否存在
        var building = await _buildingRepository.GetByIdAsync(buildingId);
        if (building == null)
        {
            throw new InvalidOperationException("楼栋不存在");
        }

        // 验证房号唯一性
        var exists = await _roomRepository.Query()
            .AnyAsync(r => r.FBuildingId == buildingId && r.FRoomNumber == request.RoomNumber);
        if (exists)
        {
            throw new InvalidOperationException($"该楼栋下房号 {request.RoomNumber} 已存在");
        }

        var room = new DorRoom
        {
            FBuildingId = buildingId,
            FFloor = request.Floor,
            FRoomNumber = request.RoomNumber,
            FBedsCount = request.BedsCount,
            FRoomType = request.RoomType,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _roomRepository.AddAsync(room);

        return (await GetRoomByIdAsync(room.FID))!;
    }

    public async Task<RoomDto?> UpdateRoomAsync(long id, UpdateRoomRequest request)
    {
        var room = await _roomRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (room == null) return null;

        // 验证房号唯一性（排除自身）
        var numberExists = await _roomRepository.Query()
            .AnyAsync(r => r.FBuildingId == room.FBuildingId && r.FRoomNumber == request.RoomNumber && r.FID != id);
        if (numberExists)
        {
            throw new InvalidOperationException($"该楼栋下房号 {request.RoomNumber} 已存在");
        }

        room.FFloor = request.Floor;
        room.FRoomNumber = request.RoomNumber;
        room.FBedsCount = request.BedsCount;
        room.FRoomType = request.RoomType;
        room.FRemark = request.Remark;
        room.FUpdatedTime = DateTime.Now;

        await _roomRepository.UpdateAsync(room);

        return await GetRoomByIdAsync(id);
    }

    public async Task<bool> DeleteRoomAsync(long id)
    {
        var room = await _roomRepository.GetByIdAsync(id);
        if (room == null) return false;

        await _roomRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, int status)
    {
        var room = await _roomRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (room == null) return false;

        room.FStatus = status;
        room.FUpdatedTime = DateTime.Now;
        await _roomRepository.UpdateAsync(room);
        return true;
    }

    public async Task<bool> CheckRoomNumberExistsAsync(long buildingId, string roomNumber, long excludeId)
    {
        return await _roomRepository.Query()
            .AnyAsync(r => r.FBuildingId == buildingId && r.FRoomNumber == roomNumber && r.FID != excludeId);
    }

    #endregion

    #region Mapping

    private static RoomDto MapToDto(DorRoom entity)
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

    private static RoomListItemDto MapToListItemDto(DorRoom entity)
    {
        return new RoomListItemDto
        {
            Id = entity.FID,
            BuildingId = entity.FBuildingId,
            BuildingName = entity.Building?.FName ?? string.Empty,
            Floor = entity.FFloor,
            RoomNumber = entity.FRoomNumber,
            BedsCount = entity.FBedsCount,
            RoomType = entity.FRoomType,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            OccupiedBeds = entity.Beds.Count(b => b.FStatus == DorStatus.Bed.Occupied)
        };
    }

    private static BedDto MapBedToDto(DorBed entity)
    {
        return new BedDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = string.Empty,
            BedNumber = entity.FBedNumber,
            BedType = entity.FBedType,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion
}
