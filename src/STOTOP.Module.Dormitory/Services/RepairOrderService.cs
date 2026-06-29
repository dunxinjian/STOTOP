using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Entities;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Services;

public class RepairOrderService : IRepairOrderService
{
    private readonly IRepository<DorRepairOrder> _repairOrderRepository;
    private readonly IRepository<DorRoom> _roomRepository;
    private readonly IRepository<DorBuilding> _buildingRepository;

    public RepairOrderService(
        IRepository<DorRepairOrder> repairOrderRepository,
        IRepository<DorRoom> roomRepository,
        IRepository<DorBuilding> buildingRepository)
    {
        _repairOrderRepository = repairOrderRepository;
        _roomRepository = roomRepository;
        _buildingRepository = buildingRepository;
    }

    public async Task<PagedResult<RepairOrderListItemDto>> GetRepairOrdersAsync(RepairOrderQueryRequest request)
    {
        var query = from ro in _repairOrderRepository.Query()
                    join r in _roomRepository.Query() on ro.FRoomId equals r.FID
                    join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                    select new { RepairOrder = ro, Room = r, Building = b };

        if (request.BuildingId.HasValue)
        {
            query = query.Where(x => x.Room.FBuildingId == request.BuildingId.Value);
        }

        if (request.RoomId.HasValue)
        {
            query = query.Where(x => x.RepairOrder.FRoomId == request.RoomId.Value);
        }

        if (request.ReporterId.HasValue)
        {
            query = query.Where(x => x.RepairOrder.FReporterId == request.ReporterId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.RepairOrder.FStatus == request.Status.Value);
        }

        if (request.Priority.HasValue)
        {
            query = query.Where(x => x.RepairOrder.FPriority == request.Priority.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.RepairOrder.FPriority)
            .ThenByDescending(x => x.RepairOrder.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<RepairOrderListItemDto>
        {
            Items = items.Select(x => MapToListItemDto(x.RepairOrder, x.Room, x.Building)).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<RepairOrderDto?> GetRepairOrderByIdAsync(long id)
    {
        var result = await (from ro in _repairOrderRepository.Query()
                           join r in _roomRepository.Query() on ro.FRoomId equals r.FID
                           join b in _buildingRepository.Query() on r.FBuildingId equals b.FID
                           where ro.FID == id
                           select new { RepairOrder = ro, Room = r, Building = b })
                           .FirstOrDefaultAsync();

        return result == null ? null : MapToDto(result.RepairOrder, result.Room, result.Building);
    }

    public async Task<RepairOrderDto> CreateRepairOrderAsync(CreateRepairOrderRequest request)
    {
        // 检查房间是否存在
        var room = await _roomRepository.Query()
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.FID == request.RoomId);

        if (room == null)
        {
            throw new InvalidOperationException("房间不存在");
        }

        var repairOrder = new DorRepairOrder
        {
            FRoomId = request.RoomId,
            FReporterId = request.ReporterId,
            FDescription = request.Description,
            FPriority = request.Priority,
            FStatus = 1, // 1 = 待处理
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _repairOrderRepository.AddAsync(repairOrder);
        return (await GetRepairOrderByIdAsync(repairOrder.FID))!;
    }

    public async Task<RepairOrderDto?> UpdateRepairOrderAsync(long id, UpdateRepairOrderRequest request)
    {
        var repairOrder = await _repairOrderRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(ro => ro.FID == id);

        if (repairOrder == null) return null;

        repairOrder.FDescription = request.Description;
        repairOrder.FPriority = request.Priority;
        repairOrder.FUpdatedTime = DateTime.Now;

        await _repairOrderRepository.UpdateAsync(repairOrder);
        return await GetRepairOrderByIdAsync(id);
    }

    public async Task<RepairOrderDto?> HandleRepairOrderAsync(long id, HandleRepairOrderRequest request)
    {
        var repairOrder = await _repairOrderRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(ro => ro.FID == id);

        if (repairOrder == null) return null;

        repairOrder.FHandlerId = request.HandlerId;
        repairOrder.FResult = request.Result;
        repairOrder.FHandledTime = DateTime.Now;
        repairOrder.FStatus = request.Status;
        repairOrder.FUpdatedTime = DateTime.Now;

        await _repairOrderRepository.UpdateAsync(repairOrder);
        return await GetRepairOrderByIdAsync(id);
    }

    public async Task<bool> DeleteRepairOrderAsync(long id)
    {
        var repairOrder = await _repairOrderRepository.GetByIdAsync(id);
        if (repairOrder == null) return false;

        await _repairOrderRepository.DeleteAsync(id);
        return true;
    }

    #region Mapping

    private static RepairOrderDto MapToDto(DorRepairOrder entity, DorRoom room, DorBuilding building)
    {
        return new RepairOrderDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            ReporterId = entity.FReporterId,
            Description = entity.FDescription,
            Priority = entity.FPriority,
            HandlerId = entity.FHandlerId,
            Result = entity.FResult,
            HandledTime = entity.FHandledTime,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static RepairOrderListItemDto MapToListItemDto(DorRepairOrder entity, DorRoom room, DorBuilding building)
    {
        return new RepairOrderListItemDto
        {
            Id = entity.FID,
            RoomId = entity.FRoomId,
            RoomNumber = room.FRoomNumber,
            BuildingName = building.FName,
            ReporterId = entity.FReporterId,
            Description = entity.FDescription,
            Priority = entity.FPriority,
            Status = entity.FStatus,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
