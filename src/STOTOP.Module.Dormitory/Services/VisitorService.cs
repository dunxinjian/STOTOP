using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Constants;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Services;

public class VisitorService : IVisitorService
{
    private readonly IRepository<DorVisitor> _visitorRepository;
    private readonly IRepository<DorRoom> _roomRepository;
    private readonly IRepository<DorBuilding> _buildingRepository;

    public VisitorService(
        IRepository<DorVisitor> visitorRepository,
        IRepository<DorRoom> roomRepository,
        IRepository<DorBuilding> buildingRepository)
    {
        _visitorRepository = visitorRepository;
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
    }

    public async Task<PagedResult<VisitorListItemDto>> GetVisitorsAsync(VisitorQueryRequest request)
    {
        var query = from v in _visitorRepository.Query()
                    join r in _roomRepository.Query() on v.FRoomId equals r.FID
                    join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                    select new { Visitor = v, Room = r, Building = b };

        if (request.BuildingId.HasValue)
        {
            query = query.Where(x => x.Room.FBuildingId == request.BuildingId.Value);
        }

        if (request.RoomId.HasValue)
        {
            query = query.Where(x => x.Visitor.FRoomId == request.RoomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Visitor.FVisitorName.Contains(keyword) ||
                                     (x.Visitor.FVisitorPhone != null && x.Visitor.FVisitorPhone.Contains(keyword)));
        }

        if (request.ArrivalDateStart.HasValue)
        {
            query = query.Where(x => x.Visitor.FArrivalTime >= request.ArrivalDateStart.Value);
        }

        if (request.ArrivalDateEnd.HasValue)
        {
            query = query.Where(x => x.Visitor.FArrivalTime <= request.ArrivalDateEnd.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.Visitor.FArrivalTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<VisitorListItemDto>
        {
            Items = items.Select(x => MapToListItemDto(x.Visitor, x.Room, x.Building)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<VisitorDto?> GetVisitorByIdAsync(long id)
    {
        var result = await (from v in _visitorRepository.Query()
                           join r in _roomRepository.Query() on v.FRoomId equals r.FID
                           join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                           where v.FID == id
                           select new { Visitor = v, Room = r, Building = b })
                           .FirstOrDefaultAsync();

        return result == null ? null : MapToDto(result.Visitor, result.Room, result.Building);
    }

    public async Task<VisitorDto> CreateVisitorAsync(CreateVisitorRequest request)
    {
        // 检查房间是否存在
        var room = await _roomRepository.Query()
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.FID == request.RoomId);

        if (room == null)
        {
            throw new InvalidOperationException("房间不存在");
        }

        var visitor = new DorVisitor
        {
            FRoomId = request.RoomId,
            FVisitorName = request.VisitorName,
            FVisitorPhone = request.VisitorPhone,
            FVisitorIdCard = request.VisitorIdCard,
            FVisitReason = request.VisitReason,
            FVisitedPersonId = request.VisitedPersonId,
            FArrivalTime = request.ArrivalTime,
            FRemark = request.Remark,
            FStatus = DorStatus.Visitor.Visiting,
            FCreatedTime = DateTime.Now
        };

        await _visitorRepository.AddAsync(visitor);
        return (await GetVisitorByIdAsync(visitor.FID))!;
    }

    public async Task<VisitorDto?> UpdateVisitorAsync(long id, UpdateVisitorRequest request)
    {
        var visitor = await _visitorRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == id);

        if (visitor == null) return null;

        visitor.FVisitorName = request.VisitorName;
        visitor.FVisitorPhone = request.VisitorPhone;
        visitor.FVisitorIdCard = request.VisitorIdCard;
        visitor.FVisitReason = request.VisitReason;
        visitor.FVisitedPersonId = request.VisitedPersonId;
        visitor.FRemark = request.Remark;

        await _visitorRepository.UpdateAsync(visitor);
        return await GetVisitorByIdAsync(id);
    }

    public async Task<VisitorDto?> DepartureAsync(long id, DateTime? departureTime = null)
    {
        var visitor = await _visitorRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == id);

        if (visitor == null) return null;

        visitor.FDepartureTime = departureTime ?? DateTime.Now;
        visitor.FStatus = DorStatus.Visitor.Left;

        await _visitorRepository.UpdateAsync(visitor);
        return await GetVisitorByIdAsync(id);
    }

    public async Task<bool> DeleteVisitorAsync(long id)
    {
        var visitor = await _visitorRepository.GetByIdAsync(id);
        if (visitor == null) return false;

        await _visitorRepository.DeleteAsync(id);
        return true;
    }

    #region Mapping

    private static VisitorDto MapToDto(DorVisitor entity, DorRoom room, DorBuilding building)
    {
        return new VisitorDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            VisitorName = entity.FVisitorName,
            VisitorPhone = entity.FVisitorPhone,
            VisitorIdCard = entity.FVisitorIdCard,
            VisitReason = entity.FVisitReason,
            VisitedPersonId = entity.FVisitedPersonId,
            ArrivalTime = entity.FArrivalTime,
            DepartureTime = entity.FDepartureTime,
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static VisitorListItemDto MapToListItemDto(DorVisitor entity, DorRoom room, DorBuilding building)
    {
        return new VisitorListItemDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            VisitorName = entity.FVisitorName,
            VisitorPhone = entity.FVisitorPhone,
            ArrivalTime = entity.FArrivalTime,
            DepartureTime = entity.FDepartureTime,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
