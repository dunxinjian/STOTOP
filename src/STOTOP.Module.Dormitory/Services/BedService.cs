using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Services;

public class BedService : IBedService
{
    private readonly IRepository<DorBed> _bedRepository;
    private readonly IRepository<DorRoom> _roomRepository;

    public BedService(
        IRepository<DorBed> bedRepository,
        IRepository<DorRoom> roomRepository)
    {
        _bedRepository = bedRepository;
        _roomRepository = roomRepository;
    }

    #region Bed CRUD

    public async Task<PagedResult<BedListItemDto>> GetBedsAsync(long roomId, BedQueryRequest request)
    {
        var query = _bedRepository.Query()
            .Where(b => b.FRoomId == roomId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(b => b.FBedNumber.Contains(keyword));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(b => b.FStatus == request.Status.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .Include(b => b.Room)
            .OrderBy(b => b.FBedNumber)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<BedListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<List<BedListItemDto>> GetAllEnabledBedsAsync(long roomId)
    {
        var beds = await _bedRepository.Query()
            .Where(b => b.FRoomId == roomId && b.FStatus == 1)
            .Include(b => b.Room)
            .OrderBy(b => b.FBedNumber)
            .ToListAsync();

        return beds.Select(MapToListItemDto).ToList();
    }

    public async Task<BedDto?> GetBedByIdAsync(long id)
    {
        var bed = await _bedRepository.Query()
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.FID == id);

        return bed == null ? null : MapToDto(bed);
    }

    public async Task<BedDto> CreateBedAsync(long roomId, CreateBedRequest request)
    {
        // 验证房间是否存在
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null)
        {
            throw new InvalidOperationException("房间不存在");
        }

        // 验证床位号唯一性
        var exists = await _bedRepository.Query()
            .AnyAsync(b => b.FRoomId == roomId && b.FBedNumber == request.BedNumber);
        if (exists)
        {
            throw new InvalidOperationException($"该房间下床位号 {request.BedNumber} 已存在");
        }

        var bed = new DorBed
        {
            FRoomId = roomId,
            FBedNumber = request.BedNumber,
            FBedType = request.BedType,
            FRemark = request.Remark,
            FStatus = 1,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _bedRepository.AddAsync(bed);

        return (await GetBedByIdAsync(bed.FID))!;
    }

    public async Task<BedDto?> UpdateBedAsync(long id, UpdateBedRequest request)
    {
        var bed = await _bedRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == id);

        if (bed == null) return null;

        // 验证床位号唯一性（排除自身）
        var numberExists = await _bedRepository.Query()
            .AnyAsync(b => b.FRoomId == bed.FRoomId && b.FBedNumber == request.BedNumber && b.FID != id);
        if (numberExists)
        {
            throw new InvalidOperationException($"该房间下床位号 {request.BedNumber} 已存在");
        }

        bed.FBedNumber = request.BedNumber;
        bed.FBedType = request.BedType;
        bed.FRemark = request.Remark;
        if (request.Status.HasValue) bed.FStatus = request.Status.Value;
        bed.FUpdatedTime = DateTime.Now;

        await _bedRepository.UpdateAsync(bed);

        return await GetBedByIdAsync(id);
    }

    public async Task<bool> DeleteBedAsync(long id)
    {
        var bed = await _bedRepository.GetByIdAsync(id);
        if (bed == null) return false;

        await _bedRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long id, int status)
    {
        var bed = await _bedRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == id);

        if (bed == null) return false;

        bed.FStatus = status;
        bed.FUpdatedTime = DateTime.Now;
        await _bedRepository.UpdateAsync(bed);
        return true;
    }

    public async Task<bool> CheckBedNumberExistsAsync(long roomId, string bedNumber, long excludeId)
    {
        return await _bedRepository.Query()
            .AnyAsync(b => b.FRoomId == roomId && b.FBedNumber == bedNumber && b.FID != excludeId);
    }

    #endregion

    #region Mapping

    private static BedDto MapToDto(DorBed entity)
    {
        return new BedDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = entity.Room?.FRoomNumber ?? string.Empty,
            BedNumber = entity.FBedNumber,
            BedType = entity.FBedType,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static BedListItemDto MapToListItemDto(DorBed entity)
    {
        return new BedListItemDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = entity.Room?.FRoomNumber ?? string.Empty,
            BedNumber = entity.FBedNumber,
            BedType = entity.FBedType,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
