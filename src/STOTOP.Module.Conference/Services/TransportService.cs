using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class TransportService : ITransportService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<TransportService> _logger;

    /// <summary>时间窗口合并阈值(分钟)</summary>
    private const int MergeWindowMinutes = 30;

    public TransportService(STOTOPDbContext db, ILogger<TransportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Vehicle CRUD

    public async Task<List<VehicleDto>> GetVehiclesAsync(int eventId)
    {
        var vehicles = await _db.Set<ConfVehicle>()
            .Where(v => v.FEventId == eventId)
            .OrderBy(v => v.FPlateNumber)
            .ToListAsync();

        return vehicles.Select(MapVehicleDto).ToList();
    }

    public async Task<VehicleDto> CreateVehicleAsync(int eventId, CreateVehicleRequest request)
    {
        var entity = new ConfVehicle
        {
            FEventId = eventId,
            FPlateNumber = request.PlateNumber,
            FVehicleType = request.VehicleType,
            FSeatCount = request.SeatCount,
            FDriverName = request.DriverName,
            FDriverPhone = request.DriverPhone,
            FSource = request.Source,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _db.Set<ConfVehicle>().Add(entity);
        await _db.SaveChangesAsync();

        return MapVehicleDto(entity);
    }

    public async Task<VehicleDto?> UpdateVehicleAsync(int id, UpdateVehicleRequest request)
    {
        var entity = await _db.Set<ConfVehicle>().AsTracking().FirstOrDefaultAsync(v => v.FID == id);
        if (entity == null) return null;

        entity.FPlateNumber = request.PlateNumber;
        entity.FVehicleType = request.VehicleType;
        entity.FSeatCount = request.SeatCount;
        entity.FDriverName = request.DriverName;
        entity.FDriverPhone = request.DriverPhone;
        entity.FSource = request.Source;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return MapVehicleDto(entity);
    }

    public async Task<bool> DeleteVehicleAsync(int id)
    {
        var entity = await _db.Set<ConfVehicle>().FirstOrDefaultAsync(v => v.FID == id);
        if (entity == null) return false;

        _db.Set<ConfVehicle>().Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    #endregion

    #region PickupTask CRUD

    public async Task<List<PickupTaskListItemDto>> GetPickupTasksAsync(int eventId)
    {
        var tasks = await _db.Set<ConfPickupTask>()
            .Include(t => t.Vehicle)
            .Include(t => t.Passengers)
                .ThenInclude(p => p.Attendee)
            .Where(t => t.FEventId == eventId)
            .OrderBy(t => t.FDate).ThenBy(t => t.FTime)
            .ToListAsync();

        return tasks.Select(t => new PickupTaskListItemDto
        {
            Id = t.FID,
            EventId = t.FEventId,
            VehicleId = t.FVehicleId,
            VehiclePlateNumber = t.Vehicle?.FPlateNumber,
            DriverName = t.Vehicle?.FDriverName,
            Type = t.FType,
            Date = t.FDate,
            Time = t.FTime,
            Origin = t.FOrigin,
            Destination = t.FDestination,
            Status = t.FStatus,
            PassengerCount = t.Passengers.Sum(p => 1 + (p.Attendee?.FCompanionCount ?? 0)),
            PassengerNames = string.Join("、", t.Passengers
                .Where(p => p.Attendee != null)
                .Select(p => p.Attendee!.FName))
        }).ToList();
    }

    public async Task<PickupTaskDetailDto?> GetPickupTaskDetailAsync(long taskId)
    {
        var t = await _db.Set<ConfPickupTask>()
            .Include(x => x.Vehicle)
            .Include(x => x.Passengers)
                .ThenInclude(p => p.Attendee)
            .FirstOrDefaultAsync(x => x.FID == taskId);

        if (t == null) return null;

        return new PickupTaskDetailDto
        {
            Id = t.FID,
            EventId = t.FEventId,
            VehicleId = t.FVehicleId,
            VehiclePlateNumber = t.Vehicle?.FPlateNumber,
            DriverName = t.Vehicle?.FDriverName,
            Type = t.FType,
            Date = t.FDate,
            Time = t.FTime,
            Origin = t.FOrigin,
            Destination = t.FDestination,
            Status = t.FStatus,
            Remark = t.FRemark,
            PassengerCount = t.Passengers.Sum(p => 1 + (p.Attendee?.FCompanionCount ?? 0)),
            Passengers = t.Passengers.Select(p => new PickupPassengerDto
            {
                AttendeeId = p.FAttendeeId,
                Name = p.Attendee?.FName ?? string.Empty,
                CompanionCount = p.Attendee?.FCompanionCount ?? 0
            }).ToList()
        };
    }

    public async Task<PickupTaskDto> CreatePickupTaskAsync(int eventId, CreatePickupTaskRequest request)
    {
        var entity = new ConfPickupTask
        {
            FEventId = eventId,
            FVehicleId = request.VehicleId,
            FType = request.Type,
            FDate = request.Date,
            FTime = request.Time,
            FOrigin = request.Origin,
            FDestination = request.Destination,
            FRemark = request.Remark,
            FStatus = "待安排",
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _db.Set<ConfPickupTask>().Add(entity);
        await _db.SaveChangesAsync();

        return await GetPickupTaskDtoAsync(entity.FID);
    }

    public async Task<PickupTaskDto?> UpdatePickupTaskAsync(int id, UpdatePickupTaskRequest request)
    {
        var entity = await _db.Set<ConfPickupTask>().AsTracking().FirstOrDefaultAsync(t => t.FID == id);
        if (entity == null) return null;

        entity.FVehicleId = request.VehicleId;
        entity.FType = request.Type;
        entity.FDate = request.Date;
        entity.FTime = request.Time;
        entity.FOrigin = request.Origin;
        entity.FDestination = request.Destination;
        entity.FStatus = request.Status ?? entity.FStatus;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return await GetPickupTaskDtoAsync(id);
    }

    public async Task<bool> DeletePickupTaskAsync(int id)
    {
        var entity = await _db.Set<ConfPickupTask>()
            .Include(t => t.Passengers)
            .FirstOrDefaultAsync(t => t.FID == id);
        if (entity == null) return false;

        // Remove passengers first
        _db.Set<ConfPickupPassenger>().RemoveRange(entity.Passengers);
        _db.Set<ConfPickupTask>().Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetPassengersAsync(int id, PickupPassengerRequest request)
    {
        var task = await _db.Set<ConfPickupTask>()
            .Include(t => t.Passengers)
            .FirstOrDefaultAsync(t => t.FID == id);
        if (task == null) return false;

        // Remove existing passengers
        _db.Set<ConfPickupPassenger>().RemoveRange(task.Passengers);

        // Add new passengers
        foreach (var attendeeId in request.AttendeeIds)
        {
            _db.Set<ConfPickupPassenger>().Add(new ConfPickupPassenger
            {
                FPickupTaskId = id,
                FAttendeeId = attendeeId
            });
        }

        task.FUpdatedTime = DateTime.Now;
        await _db.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Smart Algorithms

    /// <summary>
    /// 智能自动生成接送任务 (设计方案 5.1.1 + 5.1.2 + 5.1.3)
    /// 返回预览结果，不直接写数据库
    /// </summary>
    public async Task<AutoGeneratePreviewDto> AutoGeneratePickupsAsync(int eventId)
    {
        var preview = new AutoGeneratePreviewDto();

        // 1. 查询所有「需要接送=是」且「已确认」的参会人
        var attendees = await _db.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FNeedPickup && a.FStatus == "已确认")
            .ToListAsync();

        // 获取已有接送任务和乘客关联，用于去重
        var existingPassengerIds = await _db.Set<ConfPickupPassenger>()
            .Include(p => p.PickupTask)
            .Where(p => p.PickupTask.FEventId == eventId)
            .Select(p => p.FAttendeeId)
            .ToListAsync();
        var assignedSet = new HashSet<long>(existingPassengerIds);

        // 获取活动信息（用于目的地/出发地默认值）
        var ev = await _db.Set<ConfEvent>().FirstOrDefaultAsync(e => e.FID == eventId);
        var defaultDestination = ev?.FLocation ?? "会场";

        // 获取可用车辆列表，用于负载均衡分配
        var vehicles = await _db.Set<ConfVehicle>()
            .Where(v => v.FEventId == eventId)
            .OrderBy(v => v.FSeatCount)
            .ToListAsync();

        // 收集原始接送需求
        var rawTasks = new List<RawPickupItem>();

        foreach (var att in attendees)
        {
            // --- 来程处理 ---
            if (att.FArrivalTime.HasValue && !string.IsNullOrEmpty(att.FArrivalStation))
            {
                if (assignedSet.Contains(att.FID))
                {
                    // 已有安排，检查是否来程已分配（简化：跳过）
                    preview.SkippedAttendees.Add($"{att.FName}(来程已安排)");
                }
                else
                {
                    var type = att.FArrivalMode == "飞机" ? "接机" : "接站";
                    rawTasks.Add(new RawPickupItem
                    {
                        AttendeeId = att.FID,
                        AttendeeName = att.FName,
                        Role = att.FRole,
                        Organization = att.FOrganization,
                        Type = type,
                        Date = att.FArrivalTime.Value.Date,
                        Time = att.FArrivalTime.Value.TimeOfDay,
                        Station = att.FArrivalStation,
                        Origin = att.FArrivalStation,
                        Destination = defaultDestination,
                        IsVip = IsVipRole(att.FRole)
                    });
                }
            }
            else if (!att.FArrivalTime.HasValue && att.FNeedPickup)
            {
                preview.UnableToArrange.Add($"{att.FName}(缺少来程信息)");
            }

            // --- 回程处理 ---
            if (att.FDepartureTime.HasValue && !string.IsNullOrEmpty(att.FDepartureStation))
            {
                if (assignedSet.Contains(att.FID))
                {
                    preview.SkippedAttendees.Add($"{att.FName}(回程已安排)");
                }
                else
                {
                    var type = att.FDepartureMode == "飞机" ? "送机" : "送站";
                    // 回程时间倒推：飞机提前2小时，火车/高铁提前1小时
                    var leadHours = att.FDepartureMode == "飞机" ? 2 : 1;
                    var pickupTime = att.FDepartureTime.Value.AddHours(-leadHours);

                    rawTasks.Add(new RawPickupItem
                    {
                        AttendeeId = att.FID,
                        AttendeeName = att.FName,
                        Role = att.FRole,
                        Organization = att.FOrganization,
                        Type = type,
                        Date = pickupTime.Date,
                        Time = pickupTime.TimeOfDay,
                        Station = att.FDepartureStation,
                        Origin = defaultDestination,
                        Destination = att.FDepartureStation,
                        IsVip = IsVipRole(att.FRole)
                    });
                }
            }
            else if (!att.FDepartureTime.HasValue && att.FNeedPickup)
            {
                preview.UnableToArrange.Add($"{att.FName}(缺少回程信息)");
            }
        }

        // 2. VIP专车：独立任务不参与合并 (5.1.2)
        var vipItems = rawTasks.Where(t => t.IsVip).ToList();
        var nonVipItems = rawTasks.Where(t => !t.IsVip).ToList();

        // VIP 每人独立一个任务
        foreach (var vip in vipItems)
        {
            preview.TasksToCreate.Add(new PickupTaskPreviewItem
            {
                Type = vip.Type,
                Date = vip.Date,
                Time = vip.Time,
                Origin = vip.Origin,
                Destination = vip.Destination,
                PassengerNames = new List<string> { vip.AttendeeName }
            });
        }

        // 3. 非VIP：时间窗口聚类 + 方向聚合 (5.1.2)
        var grouped = nonVipItems
            .GroupBy(t => new { t.Date, t.Station, t.Type })
            .ToList();

        foreach (var group in grouped)
        {
            var sorted = group.OrderBy(t => t.Time).ToList();
            var clusters = ClusterByTimeWindow(sorted, TimeSpan.FromMinutes(MergeWindowMinutes));

            foreach (var cluster in clusters)
            {
                preview.TasksToCreate.Add(new PickupTaskPreviewItem
                {
                    Type = cluster[0].Type,
                    Date = cluster[0].Date,
                    Time = cluster[0].Time, // 使用最早时间
                    Origin = cluster[0].Origin,
                    Destination = cluster[0].Destination,
                    PassengerNames = cluster.Select(c => c.AttendeeName).ToList()
                });
            }
        }

        // 4. 车辆负载均衡分配 (5.1.3)
        AssignVehiclesToPreview(preview.TasksToCreate, vehicles, eventId);

        _logger.LogInformation(
            "AutoGenerate for Event {EventId}: {Count} tasks, {Skipped} skipped, {Unable} unable",
            eventId, preview.TasksToCreate.Count, preview.SkippedAttendees.Count, preview.UnableToArrange.Count);

        return preview;
    }

    /// <summary>
    /// 提交自动生成的接送任务到数据库
    /// </summary>
    public async Task<List<PickupTaskDto>> CommitAutoGenerateAsync(int eventId, List<PickupTaskPreviewItem> tasksToCommit)
    {
        try
        {
            var createdIds = new List<long>();

            // 预加载该活动下所有参会人，用于按姓名查找ID
            var attendees = await _db.Set<ConfAttendee>()
                .Where(a => a.FEventId == eventId)
                .ToListAsync();
            var nameToAttendee = attendees
                .GroupBy(a => a.FName)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var item in tasksToCommit)
            {
                var entity = new ConfPickupTask
                {
                    FEventId = eventId,
                    FVehicleId = item.SuggestedVehicleId,
                    FType = item.Type,
                    FDate = item.Date,
                    FTime = item.Time,
                    FOrigin = item.Origin,
                    FDestination = item.Destination,
                    FStatus = "待安排",
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                _db.Set<ConfPickupTask>().Add(entity);
                await _db.SaveChangesAsync();

                // 处理乘客关联
                foreach (var name in item.PassengerNames)
                {
                    if (nameToAttendee.TryGetValue(name, out var att))
                    {
                        _db.Set<ConfPickupPassenger>().Add(new ConfPickupPassenger
                        {
                            FPickupTaskId = entity.FID,
                            FAttendeeId = att.FID
                        });
                    }
                    else
                    {
                        _logger.LogWarning("CommitAutoGenerate: 未找到参会人 '{Name}', 跳过乘客关联", name);
                    }
                }

                await _db.SaveChangesAsync();
                createdIds.Add(entity.FID);
            }

            // 返回创建的任务DTO列表
            var result = new List<PickupTaskDto>();
            foreach (var id in createdIds)
            {
                result.Add(await GetPickupTaskDtoAsync(id));
            }

            _logger.LogInformation("CommitAutoGenerate for Event {EventId}: {Count} tasks created", eventId, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CommitAutoGenerate for Event {EventId} failed", eventId);
            throw;
        }
    }

    /// <summary>
    /// 智能合并优化 (设计方案 5.1.2)
    /// 扫描现有任务，找到可合并的同日期+同站点+时间差<=阈值的任务
    /// </summary>
    public async Task<OptimizePreviewDto> OptimizePickupsAsync(int eventId)
    {
        var tasks = await _db.Set<ConfPickupTask>()
            .Include(t => t.Passengers).ThenInclude(p => p.Attendee)
            .Include(t => t.Vehicle)
            .Where(t => t.FEventId == eventId)
            .OrderBy(t => t.FDate).ThenBy(t => t.FTime)
            .ToListAsync();

        var preview = new OptimizePreviewDto { BeforeCount = tasks.Count };
        var processed = new HashSet<long>();
        var mergeGroups = new List<MergeGroup>();

        // 按日期+站点+类型分组，在组内寻找可合并的
        var groups = tasks
            .GroupBy(t => new { t.FDate, Origin = t.FOrigin ?? "", t.FType })
            .ToList();

        foreach (var group in groups)
        {
            var sorted = group.OrderBy(t => t.FTime).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (processed.Contains(sorted[i].FID)) continue;

                var mergeSet = new List<ConfPickupTask> { sorted[i] };
                var windowStart = sorted[i].FTime;

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (processed.Contains(sorted[j].FID)) continue;

                    // VIP任务不参与合并
                    var hasVip = sorted[j].Passengers.Any(p => IsVipRole(p.Attendee?.FRole));
                    if (hasVip) continue;

                    if (sorted[j].FTime - windowStart <= TimeSpan.FromMinutes(MergeWindowMinutes))
                    {
                        mergeSet.Add(sorted[j]);
                    }
                    else
                    {
                        break; // 已排序，后续不可能在窗口内
                    }
                }

                if (mergeSet.Count > 1)
                {
                    // 检查第一个任务是否包含VIP
                    var firstHasVip = mergeSet[0].Passengers.Any(p => IsVipRole(p.Attendee?.FRole));
                    if (firstHasVip)
                    {
                        processed.Add(mergeSet[0].FID);
                        continue;
                    }

                    foreach (var t in mergeSet) processed.Add(t.FID);

                    var allPassengers = mergeSet.SelectMany(t => t.Passengers.Select(p => p.Attendee?.FName ?? "")).ToList();
                    mergeGroups.Add(new MergeGroup
                    {
                        SourceTaskIds = mergeSet.Select(t => t.FID).ToList(),
                        MergedTask = new PickupTaskPreviewItem
                        {
                            Type = mergeSet[0].FType,
                            Date = mergeSet[0].FDate,
                            Time = mergeSet[0].FTime,
                            Origin = mergeSet[0].FOrigin,
                            Destination = mergeSet[0].FDestination,
                            PassengerNames = allPassengers
                        },
                        Reason = $"同{(mergeSet[0].FType?.Contains("机") == true ? "机场" : "车站")}、时间差≤{MergeWindowMinutes}分钟"
                    });
                }
            }
        }

        preview.MergeGroups = mergeGroups;
        preview.AfterCount = tasks.Count - mergeGroups.Sum(g => g.SourceTaskIds.Count) + mergeGroups.Count;

        _logger.LogInformation("Optimize for Event {EventId}: {Before} -> {After} tasks, {Groups} merge groups",
            eventId, preview.BeforeCount, preview.AfterCount, mergeGroups.Count);

        return preview;
    }

    #endregion

    #region Export

    public Task<byte[]> ExportPickupsPdfAsync(int eventId)
    {
        // TODO: 使用 QuestPDF 生成接送安排PDF
        throw new NotImplementedException("PDF export not yet implemented");
    }

    public Task<byte[]> ExportPickupsImageAsync(int eventId)
    {
        // TODO: 生成接送调度图片
        throw new NotImplementedException("Image export not yet implemented");
    }

    #endregion

    #region Private Helpers

    private async Task<PickupTaskDto> GetPickupTaskDtoAsync(long id)
    {
        var t = await _db.Set<ConfPickupTask>()
            .Include(x => x.Vehicle)
            .Include(x => x.Passengers).ThenInclude(p => p.Attendee)
            .FirstAsync(x => x.FID == id);

        return new PickupTaskDto
        {
            Id = t.FID,
            EventId = t.FEventId,
            VehicleId = t.FVehicleId,
            VehiclePlateNumber = t.Vehicle?.FPlateNumber,
            Type = t.FType,
            Date = t.FDate,
            Time = t.FTime,
            Origin = t.FOrigin,
            Destination = t.FDestination,
            Status = t.FStatus,
            Remark = t.FRemark,
            CreatedTime = t.FCreatedTime,
            UpdatedTime = t.FUpdatedTime,
            Passengers = t.Passengers.Select(p => new AttendeeListItemDto
            {
                Id = p.Attendee.FID,
                EventId = p.Attendee.FEventId,
                Name = p.Attendee.FName,
                Gender = p.Attendee.FGender,
                Phone = p.Attendee.FPhone,
                Organization = p.Attendee.FOrganization,
                Title = p.Attendee.FTitle,
                Role = p.Attendee.FRole,
                NeedPickup = p.Attendee.FNeedPickup,
                NeedAccommodation = p.Attendee.FNeedAccommodation,
                Status = p.Attendee.FStatus,
                ArrivalTime = p.Attendee.FArrivalTime,
                DepartureTime = p.Attendee.FDepartureTime
            }).ToList()
        };
    }

    private static VehicleDto MapVehicleDto(ConfVehicle v)
    {
        return new VehicleDto
        {
            Id = v.FID,
            EventId = v.FEventId,
            PlateNumber = v.FPlateNumber,
            VehicleType = v.FVehicleType,
            SeatCount = v.FSeatCount,
            DriverName = v.FDriverName,
            DriverPhone = v.FDriverPhone,
            Source = v.FSource,
            Remark = v.FRemark,
            CreatedTime = v.FCreatedTime,
            UpdatedTime = v.FUpdatedTime
        };
    }

    /// <summary>判断是否VIP角色（嘉宾/领导）</summary>
    private static bool IsVipRole(string? role)
    {
        return role is "嘉宾" or "领导";
    }

    /// <summary>
    /// 滑动窗口时间聚类 (5.1.2)
    /// 按时间排序后，窗口起点为组内最早时间，超出阈值则新开一组
    /// </summary>
    private static List<List<RawPickupItem>> ClusterByTimeWindow(List<RawPickupItem> sorted, TimeSpan threshold)
    {
        var clusters = new List<List<RawPickupItem>>();
        if (sorted.Count == 0) return clusters;

        var current = new List<RawPickupItem> { sorted[0] };
        var windowStart = sorted[0].Time;

        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Time - windowStart <= threshold)
            {
                current.Add(sorted[i]);
            }
            else
            {
                clusters.Add(current);
                current = new List<RawPickupItem> { sorted[i] };
                windowStart = sorted[i].Time;
            }
        }

        clusters.Add(current);
        return clusters;
    }

    /// <summary>
    /// 车辆负载均衡分配 (5.1.3)
    /// 按乘客数降序排列任务，为每个任务选择座位数>=乘客数的最小可用车辆
    /// </summary>
    private void AssignVehiclesToPreview(List<PickupTaskPreviewItem> tasks, List<ConfVehicle> vehicles, int eventId)
    {
        if (vehicles.Count == 0) return;

        // 按乘客数降序
        var ordered = tasks.OrderByDescending(t => t.PassengerNames.Count).ToList();

        // 跟踪车辆在每个时间段的占用情况 (vehicleId -> list of occupied time ranges)
        var vehicleOccupancy = new Dictionary<long, List<(DateTime date, TimeSpan time)>>();

        foreach (var task in ordered)
        {
            var passengerCount = task.PassengerNames.Count;

            // 选择座位数>=乘客数的最小座位数可用车辆
            ConfVehicle? bestVehicle = null;
            foreach (var v in vehicles.OrderBy(v => v.FSeatCount))
            {
                if (v.FSeatCount < passengerCount) continue;

                // 检查该车辆在该时段是否已被占用
                if (vehicleOccupancy.TryGetValue(v.FID, out var occupied))
                {
                    var conflict = occupied.Any(o => o.date == task.Date &&
                        Math.Abs((o.time - task.Time).TotalMinutes) < 60); // 预估每个任务占用1小时
                    if (conflict) continue;
                }

                bestVehicle = v;
                break;
            }

            if (bestVehicle != null)
            {
                task.SuggestedVehicleId = bestVehicle.FID;
                task.SuggestedVehiclePlate = bestVehicle.FPlateNumber;

                if (!vehicleOccupancy.ContainsKey(bestVehicle.FID))
                    vehicleOccupancy[bestVehicle.FID] = new List<(DateTime, TimeSpan)>();
                vehicleOccupancy[bestVehicle.FID].Add((task.Date, task.Time));
            }
            // else: 无可用车辆，SuggestedVehicleId 保持 null（待安排）
        }
    }

    /// <summary>内部数据结构：原始接送需求项</summary>
    private class RawPickupItem
    {
        public long AttendeeId { get; set; }
        public string AttendeeName { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? Organization { get; set; }
        public string? Type { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public string? Station { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public bool IsVip { get; set; }
    }

    #endregion
}
