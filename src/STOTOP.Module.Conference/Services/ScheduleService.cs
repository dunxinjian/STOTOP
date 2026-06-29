using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class ScheduleService : IScheduleService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(STOTOPDbContext dbContext, ILogger<ScheduleService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region Schedule CRUD

    public async Task<List<ScheduleDto>> GetSchedulesAsync(int eventId)
    {
        var schedules = await _dbContext.Set<ConfSchedule>().AsNoTracking()
            .Where(s => s.FEventId == eventId)
            .Include(s => s.ScheduleAttendees).ThenInclude(sa => sa.Attendee)
            .Include(s => s.ScheduleItems)
            .OrderBy(s => s.FDate)
            .ThenBy(s => s.FSort)
            .ThenBy(s => s.FStartTime)
            .ToListAsync();

        return schedules.Select(s => new ScheduleDto
        {
            Id = s.FID,
            EventId = s.FEventId,
            Date = s.FDate,
            StartTime = s.FStartTime,
            EndTime = s.FEndTime,
            Title = s.FTitle,
            Location = s.FLocation,
            Type = s.FType,
            Description = s.FDescription,
            Sort = s.FSort,
            CreatedTime = s.FCreatedTime,
            UpdatedTime = s.FUpdatedTime,
            Attendees = s.ScheduleAttendees.Select(sa => new AttendeeListItemDto
            {
                Id = sa.Attendee.FID,
                EventId = sa.Attendee.FEventId,
                Name = sa.Attendee.FName,
                Gender = sa.Attendee.FGender,
                Phone = sa.Attendee.FPhone,
                Organization = sa.Attendee.FOrganization,
                Title = sa.Attendee.FTitle,
                Role = sa.Attendee.FRole,
                NeedPickup = sa.Attendee.FNeedPickup,
                NeedAccommodation = sa.Attendee.FNeedAccommodation,
                Status = sa.Attendee.FStatus,
                ArrivalTime = sa.Attendee.FArrivalTime,
                DepartureTime = sa.Attendee.FDepartureTime
            }).ToList(),
            Items = s.ScheduleItems.Select(si => new ScheduleItemDto
            {
                Id = si.FID,
                ScheduleId = si.FScheduleId,
                ItemName = si.FItemName,
                Quantity = si.FQuantity,
                Unit = si.FUnit,
                Status = si.FStatus,
                Remark = si.FRemark
            }).ToList()
        }).ToList();
    }

    public async Task<ScheduleDto> CreateScheduleAsync(int eventId, CreateScheduleRequest request)
    {
        var entity = new ConfSchedule
        {
            FEventId = eventId,
            FDate = request.Date,
            FStartTime = request.StartTime,
            FEndTime = request.EndTime,
            FTitle = request.Title,
            FLocation = request.Location,
            FType = request.Type,
            FDescription = request.Description,
            FSort = request.Sort,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfSchedule>().Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("创建日程 {ScheduleId}: {Title} (活动 {EventId})", entity.FID, entity.FTitle, eventId);

        return new ScheduleDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            Date = entity.FDate,
            StartTime = entity.FStartTime,
            EndTime = entity.FEndTime,
            Title = entity.FTitle,
            Location = entity.FLocation,
            Type = entity.FType,
            Description = entity.FDescription,
            Sort = entity.FSort,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            Attendees = new List<AttendeeListItemDto>(),
            Items = new List<ScheduleItemDto>()
        };
    }

    public async Task<ScheduleDto?> UpdateScheduleAsync(int id, UpdateScheduleRequest request)
    {
        var entity = await _dbContext.Set<ConfSchedule>()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (entity == null) return null;

        entity.FDate = request.Date;
        entity.FStartTime = request.StartTime;
        entity.FEndTime = request.EndTime;
        entity.FTitle = request.Title;
        entity.FLocation = request.Location;
        entity.FType = request.Type;
        entity.FDescription = request.Description;
        entity.FSort = request.Sort;
        entity.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("更新日程 {ScheduleId}: {Title}", entity.FID, entity.FTitle);

        // 重新查询返回完整数据
        var schedules = await GetSchedulesAsync((int)entity.FEventId);
        return schedules.FirstOrDefault(s => s.Id == id);
    }

    public async Task<bool> DeleteScheduleAsync(int id)
    {
        var entity = await _dbContext.Set<ConfSchedule>()
            .AsTracking()
            .Include(s => s.ScheduleAttendees)
            .Include(s => s.ScheduleItems)
            .FirstOrDefaultAsync(s => s.FID == id);

        if (entity == null) return false;

        // 先删除关联数据
        _dbContext.Set<ConfScheduleAttendee>().RemoveRange(entity.ScheduleAttendees);
        _dbContext.Set<ConfScheduleItem>().RemoveRange(entity.ScheduleItems);
        _dbContext.Set<ConfSchedule>().Remove(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("删除日程 {ScheduleId}", id);
        return true;
    }

    #endregion

    #region Schedule Attendees & Items

    public async Task<bool> SetScheduleAttendeesAsync(int id, ScheduleAttendeeRequest request)
    {
        var schedule = await _dbContext.Set<ConfSchedule>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (schedule == null) return false;

        // 移除现有关联
        var existing = await _dbContext.Set<ConfScheduleAttendee>()
            .AsTracking()
            .Where(sa => sa.FScheduleId == id)
            .ToListAsync();
        _dbContext.Set<ConfScheduleAttendee>().RemoveRange(existing);

        // 创建新关联
        foreach (var attendeeId in request.AttendeeIds)
        {
            _dbContext.Set<ConfScheduleAttendee>().Add(new ConfScheduleAttendee
            {
                FScheduleId = id,
                FAttendeeId = attendeeId
            });
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("设置日程 {ScheduleId} 参会人 {Count} 人", id, request.AttendeeIds.Count);
        return true;
    }

    public async Task<bool> SetScheduleItemsAsync(int id, ScheduleItemRequest request)
    {
        var schedule = await _dbContext.Set<ConfSchedule>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (schedule == null) return false;

        // 移除现有物品
        var existing = await _dbContext.Set<ConfScheduleItem>()
            .AsTracking()
            .Where(si => si.FScheduleId == id)
            .ToListAsync();
        _dbContext.Set<ConfScheduleItem>().RemoveRange(existing);

        // 创建新物品
        foreach (var item in request.Items)
        {
            _dbContext.Set<ConfScheduleItem>().Add(new ConfScheduleItem
            {
                FScheduleId = id,
                FItemName = item.ItemName,
                FQuantity = item.Quantity,
                FUnit = item.Unit,
                FStatus = "待准备",
                FRemark = item.Remark
            });
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("设置日程 {ScheduleId} 物品 {Count} 项", id, request.Items.Count);
        return true;
    }

    #endregion
}
