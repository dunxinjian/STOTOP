using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class MealService : IMealService
{
    private readonly STOTOPDbContext _dbContext;

    public MealService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MealPlanListItemDto>> GetMealPlansAsync(int eventId)
    {
        var plans = await _dbContext.Set<ConfMealPlan>()
            .Where(p => p.FEventId == eventId)
            .Include(p => p.Tables)
            .Include(p => p.MealAttendees)
                .ThenInclude(ma => ma.Attendee)
            .OrderBy(p => p.FDate)
            .ThenBy(p => p.FMealType)
            .ToListAsync();

        return plans.Select(p => new MealPlanListItemDto
        {
            Id = p.FID,
            EventId = p.FEventId,
            Date = p.FDate,
            MealType = p.FMealType,
            DiningMode = p.FDiningMode,
            Location = p.FLocation,
            ExpectedCount = p.FExpectedCount,
            ActualCount = p.FActualCount,
            TableCount = p.Tables.Count,
            Attendees = p.MealAttendees.Select(ma => new MealAttendeeDto
            {
                AttendeeId = ma.FAttendeeId,
                Name = ma.Attendee?.FName ?? string.Empty,
                Organization = ma.Attendee?.FOrganization,
                DietPreference = ma.Attendee?.FDietPreference,
                DietNote = ma.FDietNote
            }).ToList()
        }).ToList();
    }

    public async Task<MealPlanDto?> GetMealPlanByIdAsync(int id)
    {
        var plan = await _dbContext.Set<ConfMealPlan>()
            .Include(p => p.MealAttendees)
                .ThenInclude(ma => ma.Attendee)
            .FirstOrDefaultAsync(p => p.FID == id);

        return plan == null ? null : MapToDto(plan);
    }

    public async Task<MealPlanDto> CreateMealPlanAsync(int eventId, CreateMealPlanRequest request)
    {
        var plan = new ConfMealPlan
        {
            FEventId = eventId,
            FDate = request.Date,
            FMealType = request.MealType,
            FDiningMode = request.DiningMode,
            FLocation = request.Location,
            FExpectedCount = request.ExpectedCount,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfMealPlan>().Add(plan);
        await _dbContext.SaveChangesAsync();

        return (await GetMealPlanByIdAsync((int)plan.FID))!;
    }

    public async Task<MealPlanDto?> UpdateMealPlanAsync(int id, UpdateMealPlanRequest request)
    {
        var plan = await _dbContext.Set<ConfMealPlan>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);

        if (plan == null) return null;

        plan.FDate = request.Date;
        plan.FMealType = request.MealType;
        plan.FDiningMode = request.DiningMode;
        plan.FLocation = request.Location;
        plan.FExpectedCount = request.ExpectedCount;
        plan.FRemark = request.Remark;
        plan.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        return await GetMealPlanByIdAsync(id);
    }

    public async Task<bool> DeleteMealPlanAsync(int id)
    {
        var plan = await _dbContext.Set<ConfMealPlan>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);

        if (plan == null) return false;

        // 删除关联的用餐人员
        var attendees = await _dbContext.Set<ConfMealAttendee>()
            .Where(ma => ma.FMealPlanId == id)
            .ToListAsync();
        _dbContext.Set<ConfMealAttendee>().RemoveRange(attendees);

        // 删除关联的桌次和座位
        var tables = await _dbContext.Set<ConfTable>()
            .Include(t => t.Seats)
            .Where(t => t.FMealPlanId == id)
            .ToListAsync();
        foreach (var table in tables)
        {
            _dbContext.Set<ConfTableSeat>().RemoveRange(table.Seats);
        }
        _dbContext.Set<ConfTable>().RemoveRange(tables);

        _dbContext.Set<ConfMealPlan>().Remove(plan);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<MealPlanDto?> SetMealAttendeesAsync(int id, MealAttendeeRequest request)
    {
        var plan = await _dbContext.Set<ConfMealPlan>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);

        if (plan == null) return null;

        // 删除现有用餐人员
        var existing = await _dbContext.Set<ConfMealAttendee>()
            .Where(ma => ma.FMealPlanId == id)
            .ToListAsync();

        // 获取前端传入的主宾 ID 列表
        var primaryIds = request.Attendees.Select(a => a.AttendeeId).ToHashSet();

        // 查询这些主宾的所有随行人员（FPrimaryGuestId 指向主宾），只包含占座的
        var companions = await _dbContext.Set<ConfAttendee>()
            .Where(a => a.FPrimaryGuestId.HasValue && primaryIds.Contains(a.FPrimaryGuestId.Value))
            .Where(a => a.FHasSeat)
            .Select(a => a.FID)
            .ToListAsync();

        // 构建完整 ID 集合（主宾 + 随行），避免重复
        var newAttendeeIds = new HashSet<long>(primaryIds);
        foreach (var cid in companions)
        {
            newAttendeeIds.Add(cid);
        }

        // 找出被移除的人员，清理其桌次座位
        var oldAttendeeIds = existing.Select(a => a.FAttendeeId).ToHashSet();
        var removedAttendeeIds = oldAttendeeIds.Except(newAttendeeIds).ToList();

        if (removedAttendeeIds.Any())
        {
            var tableIds = await _dbContext.Set<ConfTable>()
                .Where(t => t.FMealPlanId == id)
                .Select(t => t.FID)
                .ToListAsync();

            if (tableIds.Any())
            {
                var seatsToRemove = await _dbContext.Set<ConfTableSeat>()
                    .Where(s => tableIds.Contains(s.FTableId) && removedAttendeeIds.Contains(s.FAttendeeId))
                    .ToListAsync();
                _dbContext.Set<ConfTableSeat>().RemoveRange(seatsToRemove);
            }
        }

        _dbContext.Set<ConfMealAttendee>().RemoveRange(existing);

        // 添加新的用餐人员（主宾 + 随行人员）
        foreach (var attendeeId in newAttendeeIds)
        {
            var dietNote = request.Attendees.FirstOrDefault(a => a.AttendeeId == attendeeId)?.DietNote;
            var mealAttendee = new ConfMealAttendee
            {
                FMealPlanId = id,
                FAttendeeId = attendeeId,
                FDietNote = dietNote
            };
            _dbContext.Set<ConfMealAttendee>().Add(mealAttendee);
        }

        plan.FActualCount = newAttendeeIds.Count;
        plan.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        return await GetMealPlanByIdAsync(id);
    }

    public async Task<List<MealPlanListItemDto>> AutoGenerateMealPlansAsync(int eventId)
    {
        var evt = await _dbContext.Set<ConfEvent>()
            .FirstOrDefaultAsync(e => e.FID == eventId);

        if (evt == null) return new List<MealPlanListItemDto>();

        // 获取已有计划，用于去重
        var existingPlans = await _dbContext.Set<ConfMealPlan>()
            .Where(p => p.FEventId == eventId)
            .ToListAsync();

        var generated = new List<ConfMealPlan>();
        var mealTypes = new[] { "早餐", "午餐", "晚餐" };
        var defaultModes = new Dictionary<string, string>
        {
            { "早餐", "自助餐" },
            { "午餐", "桌餐" },
            { "晚餐", "桌餐" }
        };

        // 获取该活动有日程安排的日期集合
        var scheduledDates = await _dbContext.Set<ConfSchedule>()
            .Where(s => s.FEventId == eventId)
            .Select(s => s.FDate.Date)
            .Distinct()
            .ToListAsync();

        // 获取有人员到达的日期
        var arrivalDates = await _dbContext.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FArrivalTime.HasValue)
            .Select(a => a.FArrivalTime!.Value.Date)
            .Distinct()
            .ToListAsync();

        // 获取有人员离开的日期
        var departureDates = await _dbContext.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FDepartureTime.HasValue)
            .Select(a => a.FDepartureTime!.Value.Date)
            .Distinct()
            .ToListAsync();

        // 合并所有需要生成餐食计划的日期
        var activeDates = new HashSet<DateTime>(scheduledDates);
        foreach (var d in arrivalDates) activeDates.Add(d);
        foreach (var d in departureDates) activeDates.Add(d);

        // 只遍历活动期间内有日程或人员变动的日期
        var datesToGenerate = activeDates
            .Where(d => d >= evt.FStartDate.Date && d <= evt.FEndDate.Date)
            .OrderBy(d => d);

        foreach (var date in datesToGenerate)
        {
            foreach (var mealType in mealTypes)
            {
                // 跳过已存在的（同日期+同餐次）
                if (existingPlans.Any(p => p.FDate.Date == date && p.FMealType == mealType))
                    continue;

                var plan = new ConfMealPlan
                {
                    FEventId = eventId,
                    FDate = date,
                    FMealType = mealType,
                    FDiningMode = defaultModes[mealType],
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                _dbContext.Set<ConfMealPlan>().Add(plan);
                generated.Add(plan);
            }
        }

        if (generated.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return generated.Select(p => new MealPlanListItemDto
        {
            Id = p.FID,
            EventId = p.FEventId,
            Date = p.FDate,
            MealType = p.FMealType,
            DiningMode = p.FDiningMode,
            Location = p.FLocation,
            ExpectedCount = p.FExpectedCount,
            ActualCount = p.FActualCount,
            TableCount = 0
        }).ToList();
    }

    #region Mapping

    private static MealPlanDto MapToDto(ConfMealPlan entity)
    {
        return new MealPlanDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            Date = entity.FDate,
            MealType = entity.FMealType,
            DiningMode = entity.FDiningMode,
            Location = entity.FLocation,
            ExpectedCount = entity.FExpectedCount,
            ActualCount = entity.FActualCount,
            Remark = entity.FRemark,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            Attendees = entity.MealAttendees.Select(ma => new MealAttendeeDto
            {
                AttendeeId = ma.FAttendeeId,
                Name = ma.Attendee?.FName ?? string.Empty,
                Organization = ma.Attendee?.FOrganization,
                DietPreference = ma.Attendee?.FDietPreference,
                DietNote = ma.FDietNote
            }).ToList()
        };
    }

    #endregion
}
