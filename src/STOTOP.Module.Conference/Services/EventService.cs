using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class EventService : IEventService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IAlertService _alertService;
    private readonly ILogger<EventService> _logger;

    public EventService(
        STOTOPDbContext dbContext,
        IAlertService alertService,
        ILogger<EventService> logger)
    {
        _dbContext = dbContext;
        _alertService = alertService;
        _logger = logger;
    }

    #region Event CRUD

    public async Task<PagedResult<EventListItemDto>> GetEventsAsync(EventQueryRequest request)
    {
        var query = _dbContext.Set<ConfEvent>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e => e.FName.Contains(keyword) || (e.FLocation != null && e.FLocation.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(e => e.FStatus == request.Status);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new EventListItemDto
            {
                Id = e.FID,
                Name = e.FName,
                StartDate = e.FStartDate,
                EndDate = e.FEndDate,
                Location = e.FLocation,
                Status = e.FStatus,
                Manager = e.FManager,
                Budget = e.FBudget,
                Type = e.FType,
                GroomName = e.FGroomName,
                BrideName = e.FBrideName,
                CreatedTime = e.FCreatedTime,
                AttendeeCount = e.Attendees.Count
            })
            .ToListAsync();

        return new PagedResult<EventListItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<EventDto?> GetEventByIdAsync(int id)
    {
        var entity = await _dbContext.Set<ConfEvent>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<EventDto> CreateEventAsync(CreateEventRequest request)
    {
        var entity = new ConfEvent
        {
            FName = request.Name,
            FDescription = request.Description,
            FStartDate = request.StartDate,
            FEndDate = request.EndDate,
            FLocation = request.Location,
            FStatus = "筹备中",
            FManager = request.Manager,
            FManagerPhone = request.ManagerPhone,
            FBudget = request.Budget,
            FRemark = request.Remark,
            FType = request.Type ?? "conference",
            FGroomName = request.GroomName,
            FBrideName = request.BrideName,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfEvent>().Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("创建活动 {EventId}: {EventName}", entity.FID, entity.FName);
        return MapToDto(entity);
    }

    public async Task<EventDto?> UpdateEventAsync(int id, UpdateEventRequest request)
    {
        var entity = await _dbContext.Set<ConfEvent>()
            .AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return null;

        entity.FName = request.Name;
        entity.FDescription = request.Description;
        entity.FStartDate = request.StartDate;
        entity.FEndDate = request.EndDate;
        entity.FLocation = request.Location;
        entity.FStatus = request.Status ?? entity.FStatus;
        entity.FManager = request.Manager;
        entity.FManagerPhone = request.ManagerPhone;
        entity.FBudget = request.Budget;
        entity.FRemark = request.Remark;
        entity.FType = request.Type ?? entity.FType;
        entity.FGroomName = request.GroomName;
        entity.FBrideName = request.BrideName;
        entity.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("更新活动 {EventId}: {EventName}", entity.FID, entity.FName);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteEventAsync(int id)
    {
        var entity = await _dbContext.Set<ConfEvent>()
            .AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return false;

        _dbContext.Set<ConfEvent>().Remove(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("删除活动 {EventId}", id);
        return true;
    }

    #endregion

    #region Dashboard

    public async Task<DashboardDto> GetDashboardAsync(int eventId)
    {
        var dashboard = new DashboardDto();

        // 参会人统计
        var attendees = await _dbContext.Set<ConfAttendee>().AsNoTracking()
            .Where(a => a.FEventId == eventId).ToListAsync();
        dashboard.TotalAttendees = attendees.Count;
        dashboard.ConfirmedAttendees = attendees.Count(a => a.FStatus == "已确认");

        // 日程统计
        dashboard.TotalSchedules = await _dbContext.Set<ConfSchedule>().AsNoTracking()
            .CountAsync(s => s.FEventId == eventId);

        // 车辆统计
        dashboard.TotalVehicles = await _dbContext.Set<ConfVehicle>().AsNoTracking()
            .CountAsync(v => v.FEventId == eventId);

        // 接送任务统计
        var pickupTasks = await _dbContext.Set<ConfPickupTask>().AsNoTracking()
            .Include(p => p.Passengers).ThenInclude(p => p.Attendee)
            .Where(p => p.FEventId == eventId).ToListAsync();
        dashboard.TotalPickupTasks = pickupTasks.Count;
        dashboard.PendingPickupTasks = pickupTasks.Count(p => p.FStatus == "待安排");

        // 已使用车辆数（已分配接送任务的不同车辆）
        dashboard.UsedVehicles = pickupTasks
            .Where(p => p.FVehicleId.HasValue)
            .Select(p => p.FVehicleId!.Value)
            .Distinct()
            .Count();

        // 乘客数统计（含随行人）
        dashboard.TotalPassengers = pickupTasks
            .SelectMany(t => t.Passengers)
            .Sum(p => 1 + (p.Attendee?.FCompanionCount ?? 0));

        dashboard.ArrangedPassengers = pickupTasks
            .Where(t => t.FStatus != "待安排")
            .SelectMany(t => t.Passengers)
            .Sum(p => 1 + (p.Attendee?.FCompanionCount ?? 0));

        // 酒店与房间统计
        var hotels = await _dbContext.Set<ConfHotel>().AsNoTracking()
            .Where(h => h.FEventId == eventId)
            .Include(h => h.Rooms).ThenInclude(r => r.RoomGuests)
            .ToListAsync();
        dashboard.TotalHotels = hotels.Count;
        var allRooms = hotels.SelectMany(h => h.Rooms).ToList();
        dashboard.TotalRooms = allRooms.Count;
        dashboard.AssignedRooms = allRooms.Count(r => r.RoomGuests.Count > 0);

        // 餐食统计
        dashboard.TotalMealPlans = await _dbContext.Set<ConfMealPlan>().AsNoTracking()
            .CountAsync(m => m.FEventId == eventId);

        // 物品统计
        var materials = await _dbContext.Set<ConfMaterial>().AsNoTracking()
            .Where(m => m.FEventId == eventId).ToListAsync();
        dashboard.TotalMaterials = materials.Count;
        dashboard.ReceivedMaterials = materials.Count(m => m.FReceivedQuantity >= m.FRequiredQuantity);

        // 收入统计
        dashboard.TotalIncome = await _dbContext.Set<ConfIncome>().AsNoTracking()
            .Where(i => i.FEventId == eventId)
            .SumAsync(i => (decimal?)i.FAmount) ?? 0;

        // 预算
        var ev = await _dbContext.Set<ConfEvent>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.FID == eventId);
        dashboard.Budget = ev?.FBudget ?? 0;

        // 异常告警数
        var alerts = await _alertService.ScanAllAlertsAsync(eventId);
        dashboard.AlertCount = alerts.Count;

        return dashboard;
    }

    public async Task<List<AlertItemDto>> GetAlertsAsync(int eventId)
    {
        return await _alertService.ScanAllAlertsAsync(eventId);
    }

    #endregion

    #region Mapping

    private static EventDto MapToDto(ConfEvent entity)
    {
        return new EventDto
        {
            Id = entity.FID,
            Name = entity.FName,
            Description = entity.FDescription,
            StartDate = entity.FStartDate,
            EndDate = entity.FEndDate,
            Location = entity.FLocation,
            Status = entity.FStatus,
            Manager = entity.FManager,
            ManagerPhone = entity.FManagerPhone,
            Budget = entity.FBudget,
            Type = entity.FType,
            GroomName = entity.FGroomName,
            BrideName = entity.FBrideName,
            Remark = entity.FRemark,
            Creator = entity.FCreator,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion
}
