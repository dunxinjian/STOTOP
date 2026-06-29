using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Services;

public class HygieneCheckService : IHygieneCheckService
{
    private readonly IRepository<DorHygieneCheck> _hygieneCheckRepository;
    private readonly IRepository<DorRoom> _roomRepository;
    private readonly IRepository<DorBuilding> _buildingRepository;

    public HygieneCheckService(
        IRepository<DorHygieneCheck> hygieneCheckRepository,
        IRepository<DorRoom> roomRepository,
        IRepository<DorBuilding> buildingRepository)
    {
        _hygieneCheckRepository = hygieneCheckRepository;
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
    }

    public async Task<PagedResult<HygieneCheckListItemDto>> GetHygieneChecksAsync(HygieneCheckQueryRequest request)
    {
        var query = from hc in _hygieneCheckRepository.Query()
                    join r in _roomRepository.Query() on hc.FRoomId equals r.FID
                    join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                    select new { HygieneCheck = hc, Room = r, Building = b };

        if (request.BuildingId.HasValue)
        {
            query = query.Where(x => x.Room.FBuildingId == request.BuildingId.Value);
        }

        if (request.RoomId.HasValue)
        {
            query = query.Where(x => x.HygieneCheck.FRoomId == request.RoomId.Value);
        }

        if (request.InspectorId.HasValue)
        {
            query = query.Where(x => x.HygieneCheck.FInspectorId == request.InspectorId.Value);
        }

        if (request.CheckDateStart.HasValue)
        {
            query = query.Where(x => x.HygieneCheck.FCheckDate >= request.CheckDateStart.Value);
        }

        if (request.CheckDateEnd.HasValue)
        {
            query = query.Where(x => x.HygieneCheck.FCheckDate <= request.CheckDateEnd.Value);
        }

        if (request.MinScore.HasValue)
        {
            query = query.Where(x => x.HygieneCheck.FScore >= request.MinScore.Value);
        }

        if (request.MaxScore.HasValue)
        {
            query = query.Where(x => x.HygieneCheck.FScore <= request.MaxScore.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.HygieneCheck.FCheckDate)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<HygieneCheckListItemDto>
        {
            Items = items.Select(x => MapToListItemDto(x.HygieneCheck, x.Room, x.Building)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<HygieneCheckDto?> GetHygieneCheckByIdAsync(long id)
    {
        var result = await (from hc in _hygieneCheckRepository.Query()
                           join r in _roomRepository.Query() on hc.FRoomId equals r.FID
                           join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                           where hc.FID == id
                           select new { HygieneCheck = hc, Room = r, Building = b })
                           .FirstOrDefaultAsync();

        return result == null ? null : MapToDto(result.HygieneCheck, result.Room, result.Building);
    }

    public async Task<HygieneCheckDto> CreateHygieneCheckAsync(CreateHygieneCheckRequest request)
    {
        // 检查房间是否存在
        var room = await _roomRepository.Query()
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.FID == request.RoomId);

        if (room == null)
        {
            throw new InvalidOperationException("房间不存在");
        }

        if (request.Score is < 0 or > 100)
        {
            throw new InvalidOperationException("评分需在 0-100 之间");
        }

        var hygieneCheck = new DorHygieneCheck
        {
            FRoomId = request.RoomId,
            FInspectorId = request.InspectorId,
            FCheckDate = request.CheckDate,
            FScore = request.Score,
            FResult = request.Result,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now
        };

        await _hygieneCheckRepository.AddAsync(hygieneCheck);
        return (await GetHygieneCheckByIdAsync(hygieneCheck.FID))!;
    }

    public async Task<HygieneCheckDto?> UpdateHygieneCheckAsync(long id, UpdateHygieneCheckRequest request)
    {
        var hygieneCheck = await _hygieneCheckRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(hc => hc.FID == id);

        if (hygieneCheck == null) return null;

        hygieneCheck.FInspectorId = request.InspectorId;
        hygieneCheck.FCheckDate = request.CheckDate;
        hygieneCheck.FScore = request.Score;
        hygieneCheck.FResult = request.Result;
        hygieneCheck.FRemark = request.Remark;

        await _hygieneCheckRepository.UpdateAsync(hygieneCheck);
        return await GetHygieneCheckByIdAsync(id);
    }

    public async Task<bool> DeleteHygieneCheckAsync(long id)
    {
        var hygieneCheck = await _hygieneCheckRepository.GetByIdAsync(id);
        if (hygieneCheck == null) return false;

        await _hygieneCheckRepository.DeleteAsync(id);
        return true;
    }

    #region Mapping

    private static HygieneCheckDto MapToDto(DorHygieneCheck entity, DorRoom room, DorBuilding building)
    {
        return new HygieneCheckDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            InspectorId = entity.FInspectorId,
            CheckDate = entity.FCheckDate,
            Score = entity.FScore,
            Result = entity.FResult,
            Remark = entity.FRemark,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static HygieneCheckListItemDto MapToListItemDto(DorHygieneCheck entity, DorRoom room, DorBuilding building)
    {
        return new HygieneCheckListItemDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            InspectorId = entity.FInspectorId,
            CheckDate = entity.FCheckDate,
            Score = entity.FScore,
            Result = entity.FResult,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
