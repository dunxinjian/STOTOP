using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class VehicleScheduleService : IVehicleScheduleService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<VehicleScheduleService> _logger;

    /// <summary>空驶预估时间(分钟)</summary>
    private const int DeadheadMinutes = 30;
    /// <summary>返程/待命预估时间(分钟)</summary>
    private const int ReturnMinutes = 30;
    /// <summary>接送任务预估时长(分钟)</summary>
    private const int TaskDurationMinutes = 60;
    /// <summary>冲突检测最小间隔(分钟)</summary>
    private const int ConflictThresholdMinutes = 15;

    public VehicleScheduleService(STOTOPDbContext db, ILogger<VehicleScheduleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Query

    public async Task<List<VehicleScheduleDto>> GetVehicleSchedulesAsync(int eventId)
    {
        var schedules = await _db.Set<ConfVehicleSchedule>()
            .Include(s => s.Vehicle)
            .Where(s => s.FEventId == eventId)
            .OrderBy(s => s.FVehicleId).ThenBy(s => s.FDate).ThenBy(s => s.FStartTime)
            .ToListAsync();

        return schedules.Select(MapToDto).ToList();
    }

    public async Task<List<VehicleScheduleDto>> GetByVehicleAsync(int eventId, int vehicleId)
    {
        var schedules = await _db.Set<ConfVehicleSchedule>()
            .Include(s => s.Vehicle)
            .Where(s => s.FEventId == eventId && s.FVehicleId == vehicleId)
            .OrderBy(s => s.FDate).ThenBy(s => s.FStartTime)
            .ToListAsync();

        return schedules.Select(MapToDto).ToList();
    }

    public async Task<List<VehicleScheduleDto>> GetByDateAsync(int eventId, DateTime date)
    {
        var dateOnly = date.Date;
        var schedules = await _db.Set<ConfVehicleSchedule>()
            .Include(s => s.Vehicle)
            .Where(s => s.FEventId == eventId && s.FDate == dateOnly)
            .OrderBy(s => s.FVehicleId).ThenBy(s => s.FStartTime)
            .ToListAsync();

        return schedules.Select(MapToDto).ToList();
    }

    #endregion

    #region Smart Generation (5.1.6)

    /// <summary>
    /// 根据接送任务+车辆分配结果，自动推导每辆车的完整日计划
    /// 返回预览结果，不直接写数据库
    /// </summary>
    public async Task<VehicleScheduleGeneratePreviewDto> GenerateSchedulesAsync(int eventId)
    {
        var preview = new VehicleScheduleGeneratePreviewDto();

        // 获取所有已分配车辆的接送任务
        var tasks = await _db.Set<ConfPickupTask>()
            .Include(t => t.Vehicle)
            .Include(t => t.Passengers)
            .Where(t => t.FEventId == eventId && t.FVehicleId != null)
            .OrderBy(t => t.FDate).ThenBy(t => t.FTime)
            .ToListAsync();

        // 按车辆分组
        var vehicleGroups = tasks.GroupBy(t => t.FVehicleId!.Value);

        foreach (var vehicleGroup in vehicleGroups)
        {
            var vehicleId = vehicleGroup.Key;
            var vehicle = vehicleGroup.First().Vehicle!;

            // 再按日期分组
            var dateGroups = vehicleGroup.GroupBy(t => t.FDate);

            foreach (var dateGroup in dateGroups)
            {
                var date = dateGroup.Key;
                var dayTasks = dateGroup.OrderBy(t => t.FTime).ToList();

                for (int i = 0; i < dayTasks.Count; i++)
                {
                    var task = dayTasks[i];
                    var taskStart = task.FTime;
                    var taskEnd = taskStart.Add(TimeSpan.FromMinutes(TaskDurationMinutes));

                    // 任务前：空驶时段（从待命点到出发地）
                    var deadheadStart = taskStart.Subtract(TimeSpan.FromMinutes(DeadheadMinutes));
                    if (deadheadStart < TimeSpan.Zero) deadheadStart = TimeSpan.Zero;

                    preview.ScheduleItems.Add(new VehicleSchedulePreviewItem
                    {
                        VehicleId = vehicleId,
                        VehiclePlateNumber = vehicle.FPlateNumber,
                        Date = date,
                        StartTime = deadheadStart,
                        EndTime = taskStart,
                        TaskType = "空驶",
                        Origin = "待命点",
                        Destination = task.FOrigin,
                        PassengerCount = 0
                    });

                    // 任务中：接送任务本身
                    preview.ScheduleItems.Add(new VehicleSchedulePreviewItem
                    {
                        VehicleId = vehicleId,
                        VehiclePlateNumber = vehicle.FPlateNumber,
                        Date = date,
                        StartTime = taskStart,
                        EndTime = taskEnd,
                        TaskType = task.FType,
                        Origin = task.FOrigin,
                        Destination = task.FDestination,
                        PassengerCount = task.Passengers.Count
                    });

                    // 任务后：返程/待命
                    var returnEnd = taskEnd.Add(TimeSpan.FromMinutes(ReturnMinutes));
                    if (returnEnd > new TimeSpan(23, 59, 0)) returnEnd = new TimeSpan(23, 59, 0);

                    // 如果不是最后一个任务，返程结束时间不超过下一个任务的空驶开始
                    if (i < dayTasks.Count - 1)
                    {
                        var nextDeadhead = dayTasks[i + 1].FTime.Subtract(TimeSpan.FromMinutes(DeadheadMinutes));
                        if (returnEnd > nextDeadhead) returnEnd = nextDeadhead;
                    }

                    preview.ScheduleItems.Add(new VehicleSchedulePreviewItem
                    {
                        VehicleId = vehicleId,
                        VehiclePlateNumber = vehicle.FPlateNumber,
                        Date = date,
                        StartTime = taskEnd,
                        EndTime = returnEnd,
                        TaskType = i < dayTasks.Count - 1 ? "待命" : "返程",
                        Origin = task.FDestination,
                        Destination = "待命点",
                        PassengerCount = 0
                    });

                    // 冲突检测：与下一个任务间隔不足
                    if (i < dayTasks.Count - 1)
                    {
                        var gap = dayTasks[i + 1].FTime - taskEnd;
                        if (gap < TimeSpan.FromMinutes(ConflictThresholdMinutes))
                        {
                            preview.Conflicts.Add(
                                $"车辆 {vehicle.FPlateNumber} 在 {date:yyyy-MM-dd} " +
                                $"{taskEnd:hh\\:mm}-{dayTasks[i + 1].FTime:hh\\:mm} 间隔仅{gap.TotalMinutes:F0}分钟，不足{ConflictThresholdMinutes}分钟");
                        }
                    }
                }
            }
        }

        _logger.LogInformation("GenerateSchedules for Event {EventId}: {Items} schedule items, {Conflicts} conflicts",
            eventId, preview.ScheduleItems.Count, preview.Conflicts.Count);

        return preview;
    }

    #endregion

    #region Manual CRUD

    public async Task<VehicleScheduleDto> AddVehicleTaskAsync(int eventId, AddVehicleTaskRequest request)
    {
        var entity = new ConfVehicleSchedule
        {
            FEventId = eventId,
            FVehicleId = request.VehicleId,
            FDate = request.Date,
            FStartTime = request.StartTime,
            FEndTime = request.EndTime,
            FTaskType = request.TaskType,
            FOrigin = request.Origin,
            FDestination = request.Destination,
            FPassengerCount = request.PassengerCount,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _db.Set<ConfVehicleSchedule>().Add(entity);
        await _db.SaveChangesAsync();

        return await GetScheduleDtoAsync(entity.FID);
    }

    public async Task<VehicleScheduleDto?> UpdateScheduleAsync(int id, AddVehicleTaskRequest request)
    {
        var entity = await _db.Set<ConfVehicleSchedule>().AsTracking().FirstOrDefaultAsync(s => s.FID == id);
        if (entity == null) return null;

        entity.FVehicleId = request.VehicleId;
        entity.FDate = request.Date;
        entity.FStartTime = request.StartTime;
        entity.FEndTime = request.EndTime;
        entity.FTaskType = request.TaskType;
        entity.FOrigin = request.Origin;
        entity.FDestination = request.Destination;
        entity.FPassengerCount = request.PassengerCount;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return await GetScheduleDtoAsync(id);
    }

    public async Task<bool> DeleteScheduleAsync(int id)
    {
        var entity = await _db.Set<ConfVehicleSchedule>().FirstOrDefaultAsync(s => s.FID == id);
        if (entity == null) return false;

        _db.Set<ConfVehicleSchedule>().Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Export & Driver Cards

    public Task<byte[]> ExportPdfAsync(int eventId)
    {
        // TODO: 使用 QuestPDF 生成车辆/司机日程表PDF（每辆车一页，含当日全部任务）
        throw new NotImplementedException("PDF export not yet implemented");
    }

    /// <summary>
    /// 生成司机任务卡数据（精简版）
    /// 按车辆+日期分组，每辆车每天一张卡
    /// </summary>
    public async Task<List<DriverCardDto>> GetDriverCardsAsync(int eventId)
    {
        var schedules = await _db.Set<ConfVehicleSchedule>()
            .Include(s => s.Vehicle)
            .Include(s => s.PickupTask).ThenInclude(t => t!.Passengers).ThenInclude(p => p.Attendee)
            .Where(s => s.FEventId == eventId)
            .OrderBy(s => s.FVehicleId).ThenBy(s => s.FDate).ThenBy(s => s.FStartTime)
            .ToListAsync();

        var cards = schedules
            .GroupBy(s => new { s.FVehicleId, s.FDate })
            .Select(g =>
            {
                var first = g.First();
                var vehicle = first.Vehicle;
                var taskItems = g.Select(s => new DriverTaskItem
                {
                    StartTime = s.FStartTime,
                    EndTime = s.FEndTime,
                    TaskType = s.FTaskType,
                    Origin = s.FOrigin,
                    Destination = s.FDestination,
                    PassengerNames = s.PickupTask != null
                        ? string.Join("、", s.PickupTask.Passengers.Select(p => p.Attendee?.FName ?? ""))
                        : null,
                    Remark = s.FRemark
                }).ToList();

                var totalMinutes = taskItems.Sum(t => (int)(t.EndTime - t.StartTime).TotalMinutes);
                var trips = taskItems.Count(t => t.TaskType != "空驶" && t.TaskType != "待命" && t.TaskType != "返程" && t.TaskType != "休息");

                return new DriverCardDto
                {
                    VehicleId = first.FVehicleId,
                    PlateNumber = vehicle.FPlateNumber,
                    VehicleType = vehicle.FVehicleType,
                    DriverName = vehicle.FDriverName,
                    DriverPhone = vehicle.FDriverPhone,
                    Date = g.Key.FDate,
                    Tasks = taskItems,
                    TotalWorkMinutes = totalMinutes,
                    TotalTrips = trips
                };
            })
            .ToList();

        return cards;
    }

    #endregion

    #region Private Helpers

    private async Task<VehicleScheduleDto> GetScheduleDtoAsync(long id)
    {
        var s = await _db.Set<ConfVehicleSchedule>()
            .Include(x => x.Vehicle)
            .FirstAsync(x => x.FID == id);
        return MapToDto(s);
    }

    private static VehicleScheduleDto MapToDto(ConfVehicleSchedule s)
    {
        return new VehicleScheduleDto
        {
            Id = s.FID,
            EventId = s.FEventId,
            VehicleId = s.FVehicleId,
            VehiclePlateNumber = s.Vehicle?.FPlateNumber,
            DriverName = s.Vehicle?.FDriverName,
            DriverPhone = s.Vehicle?.FDriverPhone,
            Date = s.FDate,
            StartTime = s.FStartTime,
            EndTime = s.FEndTime,
            TaskType = s.FTaskType,
            PickupTaskId = s.FPickupTaskId,
            Origin = s.FOrigin,
            Destination = s.FDestination,
            PassengerCount = s.FPassengerCount,
            Remark = s.FRemark,
            CreatedTime = s.FCreatedTime,
            UpdatedTime = s.FUpdatedTime
        };
    }

    #endregion

    public async Task<List<DriverNotificationDto>> GetDriverNotificationsAsync(long eventId, DateTime date)
    {
        var dateOnly = date.Date;

        // 获取活动信息
        var evt = await _db.Set<ConfEvent>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.FID == eventId);
        if (evt == null) return new List<DriverNotificationDto>();

        // 查询该活动+日期的所有车辆日程，包含 Vehicle、PickupTask → Passengers → Attendee
        var schedules = await _db.Set<ConfVehicleSchedule>().AsNoTracking()
            .Include(s => s.Vehicle)
            .Include(s => s.PickupTask)
                .ThenInclude(t => t!.Passengers)
                    .ThenInclude(p => p.Attendee)
                        .ThenInclude(a => a.Companions)
            .Where(s => s.FEventId == eventId && s.FDate == dateOnly)
            .OrderBy(s => s.FVehicleId).ThenBy(s => s.FStartTime)
            .ToListAsync();

        // 按车辆（司机）分组
        var vehicleGroups = schedules.GroupBy(s => s.FVehicleId);
        var results = new List<DriverNotificationDto>();

        foreach (var group in vehicleGroups)
        {
            var vehicle = group.First().Vehicle;
            var driverName = vehicle.FDriverName ?? "未知司机";
            var plateNumber = vehicle.FPlateNumber;

            var tasks = group.OrderBy(s => s.FStartTime).ToList();
            var sb = new StringBuilder();

            sb.AppendLine("📋 【接送任务通知】");
            sb.AppendLine("━━━━━━━━━━━━━━━━");
            sb.AppendLine($"活动：{evt.FName}");
            sb.AppendLine($"日期：{dateOnly:yyyy-MM-dd}");
            sb.AppendLine($"司机：{driverName} / 车牌号：{plateNumber}");
            sb.AppendLine("━━━━━━━━━━━━━━━━");
            sb.AppendLine();

            int taskIndex = 0;
            int totalPassengers = 0;

            foreach (var schedule in tasks)
            {
                taskIndex++;
                var taskType = schedule.FTaskType ?? "接送";
                sb.AppendLine($"🚗 任务{taskIndex} - {taskType}");
                sb.AppendLine($"  ⏰ {schedule.FStartTime:hh\\:mm}");
                sb.AppendLine($"  📍 {schedule.FOrigin ?? "未指定"}");
                sb.AppendLine($"  → 送往：{schedule.FDestination ?? "未指定"}");

                // 乘客信息
                var passengers = schedule.PickupTask?.Passengers?.ToList();
                if (passengers != null && passengers.Count > 0)
                {
                    var passengerNames = new List<string>();
                    int taskPassengerCount = 0;
                    bool hasChild = false;
                    int childCount = 0;
                    bool hasVip = false;
                    string? flightTrain = null;
                    DateTime? arrivalTime = null;

                    foreach (var p in passengers)
                    {
                        var attendee = p.Attendee;
                        if (attendee == null) continue;

                        passengerNames.Add(attendee.FName);
                        taskPassengerCount += 1 + attendee.FCompanionCount;

                        // 检查家庭组中的幼儿
                        if (attendee.Companions != null)
                        {
                            foreach (var comp in attendee.Companions)
                            {
                                if (comp.FIsChild)
                                {
                                    hasChild = true;
                                    childCount++;
                                }
                            }
                        }

                        // VIP/领导检测
                        if (attendee.FGuestType == "领导" || attendee.FRole == "VIP" || attendee.FGuestType == "VIP")
                            hasVip = true;

                        // 航班车次信息
                        if (!string.IsNullOrEmpty(attendee.FArrivalFlightTrain))
                            flightTrain = attendee.FArrivalFlightTrain;
                        if (attendee.FArrivalTime.HasValue)
                            arrivalTime = attendee.FArrivalTime;
                    }

                    sb.AppendLine($"  👤 {string.Join("、", passengerNames)} {taskPassengerCount}人");
                    totalPassengers += taskPassengerCount;

                    // 航班/车次信息
                    if (!string.IsNullOrEmpty(flightTrain))
                    {
                        var icon = flightTrain.StartsWith("G") || flightTrain.StartsWith("D") || flightTrain.StartsWith("C") || flightTrain.StartsWith("K") || flightTrain.StartsWith("Z") || flightTrain.StartsWith("T")
                            ? "🚄" : "✈️";
                        var arrTimeStr = arrivalTime?.ToString("HH:mm") ?? "";
                        sb.AppendLine($"  {icon} {flightTrain} {arrTimeStr}");
                    }

                    // 自动标签
                    var tags = new List<string>();
                    if (hasChild) tags.Add($"含{childCount}名幼儿");
                    if (hasVip) tags.Add("VIP");
                    if (schedule.FStartTime < new TimeSpan(7, 0, 0)) tags.Add("早班");

                    var remarkParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(schedule.FRemark)) remarkParts.Add(schedule.FRemark);
                    if (tags.Count > 0) remarkParts.AddRange(tags);

                    if (remarkParts.Count > 0)
                        sb.AppendLine($"  📝 {string.Join(" ", remarkParts)}");
                }
                else
                {
                    sb.AppendLine($"  👤 {schedule.FPassengerCount}人");
                    totalPassengers += schedule.FPassengerCount;

                    // 早班标签
                    if (schedule.FStartTime < new TimeSpan(7, 0, 0))
                        sb.AppendLine($"  📝 {(string.IsNullOrWhiteSpace(schedule.FRemark) ? "" : schedule.FRemark + " ")}早班");
                    else if (!string.IsNullOrWhiteSpace(schedule.FRemark))
                        sb.AppendLine($"  📝 {schedule.FRemark}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("━━━━━━━━━━━━━━━━");
            sb.AppendLine($"共 {taskIndex} 个任务 | {totalPassengers} 位宾客");

            results.Add(new DriverNotificationDto
            {
                DriverVehicleId = group.Key,
                DriverName = driverName,
                PlateNumber = plateNumber,
                DriverPhone = vehicle.FDriverPhone ?? "",
                TaskCount = taskIndex,
                PassengerCount = totalPassengers,
                Message = sb.ToString()
            });
        }

        return results;
    }
}
