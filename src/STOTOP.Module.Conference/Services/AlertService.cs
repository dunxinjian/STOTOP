using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

/// <summary>
/// 异常监测引擎 — 34 条检测规则，按 9 个维度分组扫描
/// </summary>
public class AlertService : IAlertService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<AlertService> _logger;

    public AlertService(STOTOPDbContext dbContext, ILogger<AlertService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<AlertItemDto>> ScanAllAlertsAsync(int eventId)
    {
        var alerts = new List<AlertItemDto>();

        // 预加载核心数据
        var attendees = await _dbContext.Set<ConfAttendee>().AsNoTracking()
            .Where(a => a.FEventId == eventId).ToListAsync();
        var pickupTasks = await _dbContext.Set<ConfPickupTask>().AsNoTracking()
            .Where(p => p.FEventId == eventId)
            .Include(p => p.Passengers)
            .ToListAsync();
        var vehicles = await _dbContext.Set<ConfVehicle>().AsNoTracking()
            .Where(v => v.FEventId == eventId).ToListAsync();
        var hotels = await _dbContext.Set<ConfHotel>().AsNoTracking()
            .Where(h => h.FEventId == eventId)
            .Include(h => h.Rooms).ThenInclude(r => r.RoomGuests).ThenInclude(rg => rg.Attendee)
            .ToListAsync();
        var mealPlans = await _dbContext.Set<ConfMealPlan>().AsNoTracking()
            .Where(m => m.FEventId == eventId)
            .Include(m => m.MealAttendees).ThenInclude(ma => ma.Attendee)
            .ToListAsync();
        var schedules = await _dbContext.Set<ConfSchedule>().AsNoTracking()
            .Where(s => s.FEventId == eventId)
            .Include(s => s.ScheduleAttendees)
            .ToListAsync();
        var materials = await _dbContext.Set<ConfMaterial>().AsNoTracking()
            .Where(m => m.FEventId == eventId).ToListAsync();
        var incomes = await _dbContext.Set<ConfIncome>().AsNoTracking()
            .Where(i => i.FEventId == eventId).ToListAsync();
        var vehicleSchedules = await _dbContext.Set<ConfVehicleSchedule>().AsNoTracking()
            .Where(vs => vs.FEventId == eventId).ToListAsync();
        var ev = await _dbContext.Set<ConfEvent>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.FID == eventId);

        if (ev == null) return alerts;

        // ========== 人员维度 (P-01 ~ P-04) ==========
        alerts.AddRange(ScanPersonnelAlerts(attendees, pickupTasks, hotels));

        // ========== 接送维度 (T-01 ~ T-05) ==========
        alerts.AddRange(ScanTransportAlerts(attendees, pickupTasks, vehicles));

        // ========== 住宿维度 (A-01 ~ A-05) ==========
        alerts.AddRange(ScanAccommodationAlerts(attendees, hotels));

        // ========== 餐食维度 (M-01 ~ M-03) ==========
        alerts.AddRange(ScanMealAlerts(attendees, mealPlans, ev));

        // ========== 日程维度 (S-01 ~ S-03) ==========
        alerts.AddRange(ScanScheduleAlerts(schedules));

        // ========== 资源维度 (R-01 ~ R-04) ==========
        alerts.AddRange(ScanResourceAlerts(attendees, vehicles, hotels, pickupTasks));

        // ========== 物品维度 (MT-01 ~ MT-04) ==========
        alerts.AddRange(ScanMaterialAlerts(materials, ev));

        // ========== 财务维度 (F-01 ~ F-03) ==========
        alerts.AddRange(ScanFinanceAlerts(attendees, incomes));

        // ========== 车辆日程维度 (VS-01 ~ VS-03) ==========
        alerts.AddRange(ScanVehicleScheduleAlerts(vehicleSchedules, vehicles, ev));

        _logger.LogInformation("活动 {EventId} 异常扫描完成，发现 {Count} 条告警", eventId, alerts.Count);
        return alerts;
    }

    #region 人员维度 (4条)

    private static List<AlertItemDto> ScanPersonnelAlerts(
        List<ConfAttendee> attendees, List<ConfPickupTask> pickupTasks, List<ConfHotel> hotels)
    {
        var alerts = new List<AlertItemDto>();

        // P-01: 人员长期未确认（待确认且创建超过3天）
        var pendingDays = 3;
        var longPending = attendees.Where(a => a.FStatus == "待确认" && a.FCreatedTime < DateTime.Now.AddDays(-pendingDays)).ToList();
        foreach (var a in longPending)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Warning",
                Category = "Personnel",
                Title = "人员长期未确认",
                Detail = $"{a.FName} 状态为待确认，已创建超过{pendingDays}天",
                RelatedEntityId = a.FID,
                RelatedEntityType = "Attendee"
            });
        }

        // P-02: 行程信息不完整（需接送但来程/回程信息缺失）
        var incompleteTravel = attendees.Where(a => a.FNeedPickup &&
            (a.FArrivalTime == null || string.IsNullOrWhiteSpace(a.FArrivalStation) ||
             a.FDepartureTime == null || string.IsNullOrWhiteSpace(a.FDepartureStation))).ToList();
        foreach (var a in incompleteTravel)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Warning",
                Category = "Personnel",
                Title = "行程信息不完整",
                Detail = $"{a.FName} 标记需要接送，但来程或回程信息不完整",
                RelatedEntityId = a.FID,
                RelatedEntityType = "Attendee"
            });
        }

        // P-03: VIP安排不完整（嘉宾/领导未全部安排接送+住宿）
        var vipRoles = new[] { "嘉宾", "领导" };
        var vips = attendees.Where(a => vipRoles.Contains(a.FRole) && a.FStatus == "已确认").ToList();
        var allPassengerIds = pickupTasks.SelectMany(p => p.Passengers).Select(pp => pp.FAttendeeId).ToHashSet();
        var allGuestIds = hotels.SelectMany(h => h.Rooms).SelectMany(r => r.RoomGuests).Select(rg => rg.FAttendeeId).ToHashSet();
        foreach (var vip in vips)
        {
            var hasPickup = !vip.FNeedPickup || allPassengerIds.Contains(vip.FID);
            var hasRoom = !vip.FNeedAccommodation || allGuestIds.Contains(vip.FID);
            if (!hasPickup || !hasRoom)
            {
                var missing = new List<string>();
                if (!hasPickup) missing.Add("接送");
                if (!hasRoom) missing.Add("住宿");
                alerts.Add(new AlertItemDto
                {
                    Level = "Error",
                    Category = "Personnel",
                    Title = "VIP安排不完整",
                    Detail = $"{vip.FName}({vip.FRole}) 尚未安排{string.Join("、", missing)}",
                    RelatedEntityId = vip.FID,
                    RelatedEntityType = "Attendee"
                });
            }
        }

        // P-04: 重复人员检测（同姓名+手机号）
        var duplicates = attendees
            .Where(a => !string.IsNullOrWhiteSpace(a.FPhone))
            .GroupBy(a => new { a.FName, a.FPhone })
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var group in duplicates)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Info",
                Category = "Personnel",
                Title = "重复人员检测",
                Detail = $"发现重复人员：{group.Key.FName} ({group.Key.FPhone})，共{group.Count()}条记录",
                RelatedEntityId = group.First().FID,
                RelatedEntityType = "Attendee"
            });
        }

        return alerts;
    }

    #endregion

    #region 接送维度 (5条)

    private static List<AlertItemDto> ScanTransportAlerts(
        List<ConfAttendee> attendees, List<ConfPickupTask> pickupTasks, List<ConfVehicle> vehicles)
    {
        var alerts = new List<AlertItemDto>();
        var allPassengerIds = pickupTasks.SelectMany(p => p.Passengers).Select(pp => pp.FAttendeeId).ToHashSet();

        // T-01: 需接送但未安排
        var needPickup = attendees.Where(a => a.FNeedPickup && a.FStatus != "已取消" && !allPassengerIds.Contains(a.FID)).ToList();
        foreach (var a in needPickup)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Error",
                Category = "Transport",
                Title = "需接送但未安排",
                Detail = $"{a.FName} 标记需要接送，但未出现在任何接送任务中",
                RelatedEntityId = a.FID,
                RelatedEntityType = "Attendee"
            });
        }

        // T-02: 接送任务无车辆（距出发不足24小时）
        var noVehicleTasks = pickupTasks.Where(p => p.FVehicleId == null &&
            p.FDate.Add(p.FTime) < DateTime.Now.AddHours(24)).ToList();
        foreach (var t in noVehicleTasks)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Error",
                Category = "Transport",
                Title = "接送任务无车辆",
                Detail = $"接送任务 {t.FDate:MM-dd} {t.FTime:hh\\:mm} {t.FOrigin}→{t.FDestination} 未分配车辆，距出发不足24小时",
                RelatedEntityId = t.FID,
                RelatedEntityType = "PickupTask"
            });
        }

        // T-03: 乘客超载
        var vehicleDict = vehicles.ToDictionary(v => v.FID, v => v.FSeatCount);
        foreach (var task in pickupTasks.Where(t => t.FVehicleId.HasValue))
        {
            if (vehicleDict.TryGetValue(task.FVehicleId!.Value, out var seatCount))
            {
                if (task.Passengers.Count > seatCount)
                {
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Error",
                        Category = "Transport",
                        Title = "乘客超载",
                        Detail = $"接送任务 {task.FDate:MM-dd} {task.FOrigin}→{task.FDestination} 乘客{task.Passengers.Count}人，超过车辆座位{seatCount}个",
                        RelatedEntityId = task.FID,
                        RelatedEntityType = "PickupTask"
                    });
                }
            }
        }

        // T-04: 接送时间冲突（同车辆时间重叠）
        var vehicleTaskGroups = pickupTasks.Where(t => t.FVehicleId.HasValue)
            .GroupBy(t => t.FVehicleId!.Value).ToList();
        foreach (var group in vehicleTaskGroups)
        {
            var tasks = group.OrderBy(t => t.FDate).ThenBy(t => t.FTime).ToList();
            for (int i = 0; i < tasks.Count - 1; i++)
            {
                var current = tasks[i];
                var next = tasks[i + 1];
                if (current.FDate == next.FDate && current.FTime.Add(TimeSpan.FromHours(1)) > next.FTime)
                {
                    var vehicle = vehicles.FirstOrDefault(v => v.FID == group.Key);
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Warning",
                        Category = "Transport",
                        Title = "接送时间冲突",
                        Detail = $"车辆 {vehicle?.FPlateNumber} 在 {current.FDate:MM-dd} 存在时间重叠的接送任务",
                        RelatedEntityId = current.FID,
                        RelatedEntityType = "PickupTask"
                    });
                }
            }
        }

        // T-05: 可合并的接送任务（同日期+同站点+时间差<=30分钟）
        var mergeable = pickupTasks
            .GroupBy(t => new { t.FDate, t.FOrigin })
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var group in mergeable)
        {
            var sorted = group.OrderBy(t => t.FTime).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i + 1].FTime - sorted[i].FTime <= TimeSpan.FromMinutes(30))
                {
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Info",
                        Category = "Transport",
                        Title = "可合并的接送任务",
                        Detail = $"{group.Key.FDate:MM-dd} {group.Key.FOrigin} 存在时间相近的接送任务，建议合并",
                        RelatedEntityId = sorted[i].FID,
                        RelatedEntityType = "PickupTask"
                    });
                    break; // 每组只报一次
                }
            }
        }

        return alerts;
    }

    #endregion

    #region 住宿维度 (5条)

    private static List<AlertItemDto> ScanAccommodationAlerts(
        List<ConfAttendee> attendees, List<ConfHotel> hotels)
    {
        var alerts = new List<AlertItemDto>();
        var allRooms = hotels.SelectMany(h => h.Rooms).ToList();
        var assignedAttendeeIds = allRooms.SelectMany(r => r.RoomGuests).Select(rg => rg.FAttendeeId).ToHashSet();

        // A-01: 需住宿但未分配
        var needAccommodation = attendees.Where(a => a.FNeedAccommodation && a.FStatus != "已取消" && !assignedAttendeeIds.Contains(a.FID)).ToList();
        foreach (var a in needAccommodation)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Error",
                Category = "Accommodation",
                Title = "需住宿但未分配",
                Detail = $"{a.FName} 标记需要住宿，但未分配房间",
                RelatedEntityId = a.FID,
                RelatedEntityType = "Attendee"
            });
        }

        // A-02: 性别混住
        foreach (var room in allRooms.Where(r => r.RoomGuests.Count > 1))
        {
            var genders = room.RoomGuests.Select(rg => rg.Attendee?.FGender).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().ToList();
            if (genders.Count > 1 && (room.FRoomType == "标间" || room.FRoomType == "大床房"))
            {
                var hotel = hotels.First(h => h.Rooms.Any(r => r.FID == room.FID));
                alerts.Add(new AlertItemDto
                {
                    Level = "Error",
                    Category = "Accommodation",
                    Title = "性别混住",
                    Detail = $"{hotel.FHotelName} {room.FRoomNumber} ({room.FRoomType}) 存在不同性别入住",
                    RelatedEntityId = room.FID,
                    RelatedEntityType = "Room"
                });
            }
        }

        // A-03: 超额入住
        foreach (var room in allRooms)
        {
            var capacity = room.FRoomType switch
            {
                "大床房" => 1,
                "标间" => 2,
                "套房" => 2,
                _ => 2
            };
            if (room.RoomGuests.Count > capacity)
            {
                var hotel = hotels.First(h => h.Rooms.Any(r => r.FID == room.FID));
                alerts.Add(new AlertItemDto
                {
                    Level = "Warning",
                    Category = "Accommodation",
                    Title = "超额入住",
                    Detail = $"{hotel.FHotelName} {room.FRoomNumber} ({room.FRoomType}) 入住{room.RoomGuests.Count}人，超过容量{capacity}",
                    RelatedEntityId = room.FID,
                    RelatedEntityType = "Room"
                });
            }
        }

        // A-04: 入住日期不匹配（简化：检查房间可用日期是否覆盖活动期间）
        // 需要活动日期信息，这里仅检查基础条件
        foreach (var room in allRooms.Where(r => r.RoomGuests.Count > 0))
        {
            if (room.FCheckInDate > room.FCheckOutDate)
            {
                var hotel = hotels.First(h => h.Rooms.Any(r => r.FID == room.FID));
                alerts.Add(new AlertItemDto
                {
                    Level = "Warning",
                    Category = "Accommodation",
                    Title = "入住日期异常",
                    Detail = $"{hotel.FHotelName} {room.FRoomNumber} 入住日期晚于退房日期",
                    RelatedEntityId = room.FID,
                    RelatedEntityType = "Room"
                });
            }
        }

        // A-05: 单人占双人间
        foreach (var room in allRooms.Where(r => r.FRoomType == "标间" && r.RoomGuests.Count == 1))
        {
            var hotel = hotels.First(h => h.Rooms.Any(r => r.FID == room.FID));
            var guestName = room.RoomGuests.First().Attendee?.FName ?? "未知";
            alerts.Add(new AlertItemDto
            {
                Level = "Info",
                Category = "Accommodation",
                Title = "单人占双人间",
                Detail = $"{hotel.FHotelName} {room.FRoomNumber} (标间) 仅入住{guestName}1人，可建议合并",
                RelatedEntityId = room.FID,
                RelatedEntityType = "Room"
            });
        }

        return alerts;
    }

    #endregion

    #region 餐食维度 (3条)

    private static List<AlertItemDto> ScanMealAlerts(
        List<ConfAttendee> attendees, List<ConfMealPlan> mealPlans, ConfEvent ev)
    {
        var alerts = new List<AlertItemDto>();

        // M-01: 特殊饮食未处理
        var specialDietAttendees = attendees.Where(a => a.FDietPreference != null &&
            a.FDietPreference != "无特殊" && a.FStatus != "已取消").ToList();
        foreach (var a in specialDietAttendees)
        {
            var mealAttendees = mealPlans.SelectMany(m => m.MealAttendees)
                .Where(ma => ma.FAttendeeId == a.FID).ToList();
            var hasNote = mealAttendees.Any(ma => !string.IsNullOrWhiteSpace(ma.FDietNote));
            if (mealAttendees.Count > 0 && !hasNote)
            {
                alerts.Add(new AlertItemDto
                {
                    Level = "Warning",
                    Category = "Meal",
                    Title = "特殊饮食未处理",
                    Detail = $"{a.FName} 饮食偏好为{a.FDietPreference}，但用餐安排中未备注处理方式",
                    RelatedEntityId = a.FID,
                    RelatedEntityType = "Attendee"
                });
            }
        }

        // M-02: 用餐人数超容量
        foreach (var meal in mealPlans.Where(m => m.FExpectedCount > 0))
        {
            var actualCount = meal.MealAttendees.Count;
            if (actualCount > meal.FExpectedCount * 1.2)
            {
                alerts.Add(new AlertItemDto
                {
                    Level = "Warning",
                    Category = "Meal",
                    Title = "用餐人数超容量",
                    Detail = $"{meal.FDate:MM-dd} {meal.FMealType} 实际{actualCount}人，超过预计{meal.FExpectedCount}人的120%",
                    RelatedEntityId = meal.FID,
                    RelatedEntityType = "MealPlan"
                });
            }
        }

        // M-03: 无用餐安排（活动期间某天某餐次无计划）
        var mealTypes = new[] { "早餐", "午餐", "晚餐" };
        for (var date = ev.FStartDate.Date; date <= ev.FEndDate.Date; date = date.AddDays(1))
        {
            foreach (var mealType in mealTypes)
            {
                var d = date;
                if (!mealPlans.Any(m => m.FDate.Date == d && m.FMealType == mealType))
                {
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Info",
                        Category = "Meal",
                        Title = "无用餐安排",
                        Detail = $"{date:MM-dd} {mealType} 无餐食计划",
                        RelatedEntityType = "MealPlan"
                    });
                }
            }
        }

        return alerts;
    }

    #endregion

    #region 日程维度 (3条)

    private static List<AlertItemDto> ScanScheduleAlerts(List<ConfSchedule> schedules)
    {
        var alerts = new List<AlertItemDto>();

        // S-01: 日程时间重叠
        var dateGroups = schedules.GroupBy(s => s.FDate.Date).ToList();
        foreach (var group in dateGroups)
        {
            var sorted = group.OrderBy(s => s.FStartTime).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].FEndTime > sorted[i + 1].FStartTime)
                {
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Warning",
                        Category = "Schedule",
                        Title = "日程时间重叠",
                        Detail = $"{sorted[i].FDate:MM-dd} 「{sorted[i].FTitle}」与「{sorted[i + 1].FTitle}」时间存在重叠",
                        RelatedEntityId = sorted[i].FID,
                        RelatedEntityType = "Schedule"
                    });
                }
            }
        }

        // S-02: 人员时间冲突（同一参会人在时间重叠的不同日程中）
        var attendeeSchedules = schedules
            .SelectMany(s => s.ScheduleAttendees.Select(sa => new { sa.FAttendeeId, Schedule = s }))
            .GroupBy(x => x.FAttendeeId)
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var group in attendeeSchedules)
        {
            var sorted = group.OrderBy(x => x.Schedule.FDate).ThenBy(x => x.Schedule.FStartTime).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var curr = sorted[i].Schedule;
                var next = sorted[i + 1].Schedule;
                if (curr.FDate.Date == next.FDate.Date && curr.FEndTime > next.FStartTime)
                {
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Warning",
                        Category = "Schedule",
                        Title = "人员时间冲突",
                        Detail = $"参会人(ID:{group.Key}) 在 {curr.FDate:MM-dd} 被分配到重叠的日程「{curr.FTitle}」和「{next.FTitle}」",
                        RelatedEntityId = group.Key,
                        RelatedEntityType = "Attendee"
                    });
                }
            }
        }

        // S-03: 空日程（无任何参会人关联）
        foreach (var s in schedules.Where(s => s.ScheduleAttendees.Count == 0))
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Info",
                Category = "Schedule",
                Title = "空日程",
                Detail = $"{s.FDate:MM-dd} 「{s.FTitle}」无任何参会人关联",
                RelatedEntityId = s.FID,
                RelatedEntityType = "Schedule"
            });
        }

        return alerts;
    }

    #endregion

    #region 资源维度 (4条)

    private static List<AlertItemDto> ScanResourceAlerts(
        List<ConfAttendee> attendees, List<ConfVehicle> vehicles,
        List<ConfHotel> hotels, List<ConfPickupTask> pickupTasks)
    {
        var alerts = new List<AlertItemDto>();

        var needPickupCount = attendees.Count(a => a.FNeedPickup && a.FStatus != "已取消");
        var totalSeats = vehicles.Sum(v => v.FSeatCount);
        var needAccommodationCount = attendees.Count(a => a.FNeedAccommodation && a.FStatus != "已取消");
        var allRooms = hotels.SelectMany(h => h.Rooms).ToList();

        // R-01: 车辆总座位不足
        if (totalSeats < needPickupCount && vehicles.Count > 0)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Error",
                Category = "Resource",
                Title = "车辆总座位不足",
                Detail = $"所有车辆总座位{totalSeats}个，需接送人员{needPickupCount}人",
                RelatedEntityType = "Vehicle"
            });
        }

        // R-02: 房间总数不足
        var totalRoomCapacity = allRooms.Sum(r => r.FRoomType switch
        {
            "大床房" => 1,
            "标间" => 2,
            "套房" => 2,
            _ => 2
        });
        if (totalRoomCapacity < needAccommodationCount && allRooms.Count > 0)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Error",
                Category = "Resource",
                Title = "房间总数不足",
                Detail = $"可用房间总容量{totalRoomCapacity}人，需住宿人员{needAccommodationCount}人",
                RelatedEntityType = "Room"
            });
        }

        // R-03: 接送任务无车辆池
        var pendingTasks = pickupTasks.Count(t => t.FVehicleId == null);
        var freeVehicles = vehicles.Count; // 简化：有空闲车辆
        if (pendingTasks > 0 && vehicles.Count == 0)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Warning",
                Category = "Resource",
                Title = "接送任务无车辆池",
                Detail = $"有{pendingTasks}个待安排接送任务，但无可用车辆",
                RelatedEntityType = "Vehicle"
            });
        }

        // R-04: 酒店房间紧张
        foreach (var hotel in hotels)
        {
            var freeRoomCount = hotel.Rooms.Count(r => r.RoomGuests.Count == 0);
            var assignedGuestIds = hotel.Rooms.SelectMany(r => r.RoomGuests).Select(rg => rg.FAttendeeId).ToHashSet();
            var unassignedCount = needAccommodationCount; // 简化
            if (hotel.Rooms.Count > 0 && freeRoomCount > 0 && freeRoomCount < hotel.Rooms.Count * 0.2)
            {
                alerts.Add(new AlertItemDto
                {
                    Level = "Warning",
                    Category = "Resource",
                    Title = "酒店房间紧张",
                    Detail = $"{hotel.FHotelName} 空闲房间仅{freeRoomCount}间（总{hotel.Rooms.Count}间），不足20%",
                    RelatedEntityId = hotel.FID,
                    RelatedEntityType = "Hotel"
                });
            }
        }

        return alerts;
    }

    #endregion

    #region 物品维度 (4条)

    private static List<AlertItemDto> ScanMaterialAlerts(List<ConfMaterial> materials, ConfEvent ev)
    {
        var alerts = new List<AlertItemDto>();

        // MT-01: 物品未按时到位
        foreach (var m in materials.Where(m => m.FRequiredDate.HasValue && m.FRequiredDate.Value < DateTime.Now && m.FReceivedQuantity < m.FRequiredQuantity))
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Error",
                Category = "Material",
                Title = "物品未按时到位",
                Detail = $"「{m.FName}」需求日期{m.FRequiredDate:MM-dd}已过，到位{m.FReceivedQuantity}/{m.FRequiredQuantity}",
                RelatedEntityId = m.FID,
                RelatedEntityType = "Material"
            });
        }

        // MT-02: 租赁物品临近归还
        foreach (var m in materials.Where(m => m.FAcquisitionMethod == "租赁" && m.FReturnDate.HasValue &&
            m.FReturnDate.Value <= DateTime.Now.AddDays(3) && m.FReturnDate.Value >= DateTime.Now && m.FStatus == "使用中"))
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Warning",
                Category = "Material",
                Title = "租赁物品临近归还",
                Detail = $"「{m.FName}」归还日期{m.FReturnDate:MM-dd}，仍为使用中",
                RelatedEntityId = m.FID,
                RelatedEntityType = "Material"
            });
        }

        // MT-03: 物品费用超预算
        var totalMaterialCost = materials.Sum(m => m.FTotalPrice);
        if (ev.FBudget > 0 && totalMaterialCost > ev.FBudget * 0.3m) // 物品费用超预算30%
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Warning",
                Category = "Material",
                Title = "物品费用超预算",
                Detail = $"物品总费用{totalMaterialCost:N2}元，超过活动预算{ev.FBudget:N2}元的30%",
                RelatedEntityType = "Material"
            });
        }

        // MT-04: 物品可复用提示（简化：检查同名自有物品）
        var ownedItems = materials.Where(m => m.FAcquisitionMethod == "自有").Select(m => m.FName).ToHashSet();
        foreach (var m in materials.Where(m => m.FAcquisitionMethod == "采买" && ownedItems.Contains(m.FName)))
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Info",
                Category = "Material",
                Title = "物品可复用提示",
                Detail = $"「{m.FName}」标记为采买，但存在同名自有物品，可检查是否可复用",
                RelatedEntityId = m.FID,
                RelatedEntityType = "Material"
            });
        }

        return alerts;
    }

    #endregion

    #region 财务维度 (3条)

    private static List<AlertItemDto> ScanFinanceAlerts(
        List<ConfAttendee> attendees, List<ConfIncome> incomes)
    {
        var alerts = new List<AlertItemDto>();
        var confirmedAttendees = attendees.Where(a => a.FStatus == "已确认").ToList();
        var paidAttendeeIds = incomes.Where(i => i.FAttendeeId.HasValue && i.FType == "会费")
            .Select(i => i.FAttendeeId!.Value).ToHashSet();

        // F-01: 未缴费参会人
        var unpaid = confirmedAttendees.Where(a => !paidAttendeeIds.Contains(a.FID)).ToList();
        if (unpaid.Count > 0)
        {
            alerts.Add(new AlertItemDto
            {
                Level = "Warning",
                Category = "Finance",
                Title = "未缴费参会人",
                Detail = $"有{unpaid.Count}位已确认参会人未登记会费缴纳记录",
                RelatedEntityType = "Income"
            });
        }

        // F-02: 缴费率统计
        if (confirmedAttendees.Count > 0)
        {
            var payRate = (double)paidAttendeeIds.Count / confirmedAttendees.Count;
            if (payRate < 0.8)
            {
                alerts.Add(new AlertItemDto
                {
                    Level = "Info",
                    Category = "Finance",
                    Title = "缴费率偏低",
                    Detail = $"缴费率{payRate:P0}（{paidAttendeeIds.Count}/{confirmedAttendees.Count}），低于80%",
                    RelatedEntityType = "Income"
                });
            }
        }

        // F-03: 收入金额异常（单笔偏离同类型平均值>2倍或<0.5倍）
        var typeGroups = incomes.GroupBy(i => i.FType).ToList();
        foreach (var group in typeGroups.Where(g => g.Count() >= 3))
        {
            var avg = group.Average(i => i.FAmount);
            foreach (var income in group.Where(i => i.FAmount > avg * 2 || i.FAmount < avg * 0.5m))
            {
                alerts.Add(new AlertItemDto
                {
                    Level = "Warning",
                    Category = "Finance",
                    Title = "收入金额异常",
                    Detail = $"{income.FPayerName} {income.FType} {income.FAmount:N2}元，明显偏离同类型平均值{avg:N2}元",
                    RelatedEntityId = income.FID,
                    RelatedEntityType = "Income"
                });
            }
        }

        return alerts;
    }

    #endregion

    #region 车辆日程维度 (3条)

    private static List<AlertItemDto> ScanVehicleScheduleAlerts(
        List<ConfVehicleSchedule> vehicleSchedules, List<ConfVehicle> vehicles, ConfEvent ev)
    {
        var alerts = new List<AlertItemDto>();

        // VS-01: 车辆日程冲突（同车辆同日时间重叠）
        var vehicleGroups = vehicleSchedules.GroupBy(vs => new { vs.FVehicleId, vs.FDate.Date }).ToList();
        foreach (var group in vehicleGroups)
        {
            var sorted = group.OrderBy(vs => vs.FStartTime).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].FEndTime > sorted[i + 1].FStartTime)
                {
                    var vehicle = vehicles.FirstOrDefault(v => v.FID == group.Key.FVehicleId);
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Error",
                        Category = "VehicleSchedule",
                        Title = "车辆日程冲突",
                        Detail = $"车辆 {vehicle?.FPlateNumber} 在 {group.Key.Date:MM-dd} 存在时间重叠的日程项",
                        RelatedEntityId = sorted[i].FID,
                        RelatedEntityType = "VehicleSchedule"
                    });
                }
            }
        }

        // VS-02: 司机连续作业超时（单日>10小时）
        var dailyWork = vehicleSchedules.GroupBy(vs => new { vs.FVehicleId, vs.FDate.Date }).ToList();
        foreach (var group in dailyWork)
        {
            var totalHours = group.Sum(vs => (vs.FEndTime - vs.FStartTime).TotalHours);
            if (totalHours > 10)
            {
                var vehicle = vehicles.FirstOrDefault(v => v.FID == group.Key.FVehicleId);
                alerts.Add(new AlertItemDto
                {
                    Level = "Warning",
                    Category = "VehicleSchedule",
                    Title = "司机连续作业超时",
                    Detail = $"车辆 {vehicle?.FPlateNumber} 司机 {vehicle?.FDriverName} 在 {group.Key.Date:MM-dd} 任务时长{totalHours:F1}小时，超过10小时",
                    RelatedEntityId = vehicle?.FID,
                    RelatedEntityType = "Vehicle"
                });
            }
        }

        // VS-03: 车辆闲置时段（活动期间整天无任务）
        var scheduledVehicleDates = vehicleSchedules.Select(vs => new { vs.FVehicleId, vs.FDate.Date }).ToHashSet();
        foreach (var vehicle in vehicles)
        {
            for (var date = ev.FStartDate.Date; date <= ev.FEndDate.Date; date = date.AddDays(1))
            {
                var d = date;
                if (!vehicleSchedules.Any(vs => vs.FVehicleId == vehicle.FID && vs.FDate.Date == d))
                {
                    alerts.Add(new AlertItemDto
                    {
                        Level = "Info",
                        Category = "VehicleSchedule",
                        Title = "车辆闲置时段",
                        Detail = $"车辆 {vehicle.FPlateNumber} 在 {date:MM-dd} 无任何任务安排",
                        RelatedEntityId = vehicle.FID,
                        RelatedEntityType = "Vehicle"
                    });
                }
            }
        }

        return alerts;
    }

    #endregion
}
