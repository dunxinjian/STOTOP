using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class AccommodationService : IAccommodationService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<AccommodationService> _logger;
    private readonly Random _random = new();

    // 软约束权重 (5.2.1)
    private const int WeightSameOrg = 30;        // S1 同单位优先
    private const int WeightRankMatch = 25;       // S2 职级匹配
    private const int WeightVipSingle = 20;       // S3 VIP单独住
    private const int WeightFloorGroup = 15;      // S4 楼层集中 (简化：同酒店)
    private const int WeightFillFirst = 10;       // S5 填满优先

    // 房型容量
    private static readonly Dictionary<string, int> RoomCapacity = new()
    {
        ["标单"] = 1,
        ["标双"] = 2,
        ["套房"] = 2,
        ["大床房"] = 1,
        ["行政大床"] = 1,
        ["其他"] = 2
    };

    public AccommodationService(STOTOPDbContext db, ILogger<AccommodationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Hotel CRUD

    public async Task<List<HotelListItemDto>> GetHotelsAsync(int eventId)
    {
        var hotels = await _db.Set<ConfHotel>()
            .Include(h => h.Rooms).ThenInclude(r => r.RoomGuests)
            .Where(h => h.FEventId == eventId)
            .OrderBy(h => h.FHotelName)
            .ToListAsync();

        return hotels.Select(h => new HotelListItemDto
        {
            Id = h.FID,
            EventId = h.FEventId,
            HotelName = h.FHotelName,
            Address = h.FAddress,
            Contact = h.FContact,
            ContactPhone = h.FContactPhone,
            AgreedPrice = h.FAgreedPrice,
            TotalRooms = h.Rooms.Count,
            AssignedRooms = h.Rooms.Count(r => r.RoomGuests.Any())
        }).ToList();
    }

    public async Task<HotelDto> CreateHotelAsync(int eventId, CreateHotelRequest request)
    {
        var entity = new ConfHotel
        {
            FEventId = eventId,
            FHotelName = request.HotelName,
            FAddress = request.Address,
            FContact = request.Contact,
            FContactPhone = request.ContactPhone,
            FAgreedPrice = request.AgreedPrice,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _db.Set<ConfHotel>().Add(entity);
        await _db.SaveChangesAsync();

        return await GetHotelDtoAsync(entity.FID);
    }

    public async Task<HotelDto?> UpdateHotelAsync(int id, UpdateHotelRequest request)
    {
        var entity = await _db.Set<ConfHotel>().AsTracking().FirstOrDefaultAsync(h => h.FID == id);
        if (entity == null) return null;

        entity.FHotelName = request.HotelName;
        entity.FAddress = request.Address;
        entity.FContact = request.Contact;
        entity.FContactPhone = request.ContactPhone;
        entity.FAgreedPrice = request.AgreedPrice;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return await GetHotelDtoAsync(id);
    }

    public async Task<bool> DeleteHotelAsync(int id)
    {
        var entity = await _db.Set<ConfHotel>().FirstOrDefaultAsync(h => h.FID == id);
        if (entity == null) return false;

        _db.Set<ConfHotel>().Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Room CRUD

    public async Task<List<RoomListItemDto>> GetRoomsAsync(int hotelId)
    {
        var rooms = await _db.Set<ConfRoom>()
            .Include(r => r.RoomGuests).ThenInclude(g => g.Attendee)
            .Where(r => r.FHotelId == hotelId)
            .OrderBy(r => r.FRoomNumber)
            .ToListAsync();

        return rooms.Select(r => new RoomListItemDto
        {
            Id = r.FID,
            HotelId = r.FHotelId,
            RoomNumber = r.FRoomNumber,
            RoomType = r.FRoomType,
            CheckInDate = r.FCheckInDate,
            CheckOutDate = r.FCheckOutDate,
            Status = r.FStatus,
            GuestCount = r.RoomGuests.Count,
            GuestNames = r.RoomGuests.Any()
                ? string.Join("、", r.RoomGuests.Select(g => g.Attendee?.FName ?? ""))
                : null
        }).ToList();
    }

    public async Task<List<RoomDto>> BatchAddRoomsAsync(int hotelId, BatchAddRoomRequest request)
    {
        var entities = request.Rooms.Select(r => new ConfRoom
        {
            FHotelId = hotelId,
            FRoomNumber = r.RoomNumber,
            FRoomType = r.RoomType,
            FCheckInDate = r.CheckInDate,
            FCheckOutDate = r.CheckOutDate,
            FRemark = r.Remark,
            FStatus = "空闲",
            FUpdatedTime = DateTime.Now
        }).ToList();

        _db.Set<ConfRoom>().AddRange(entities);
        await _db.SaveChangesAsync();

        return entities.Select(MapRoomDto).ToList();
    }

    public async Task<RoomDto?> UpdateRoomAsync(int id, UpdateRoomRequest request)
    {
        var entity = await _db.Set<ConfRoom>().AsTracking().FirstOrDefaultAsync(r => r.FID == id);
        if (entity == null) return null;

        entity.FRoomNumber = request.RoomNumber;
        entity.FRoomType = request.RoomType;
        entity.FCheckInDate = request.CheckInDate;
        entity.FCheckOutDate = request.CheckOutDate;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return MapRoomDto(entity);
    }

    public async Task<bool> AssignRoomAsync(int id, RoomAssignRequest request)
    {
        var room = await _db.Set<ConfRoom>()
            .Include(r => r.RoomGuests)
            .FirstOrDefaultAsync(r => r.FID == id);
        if (room == null) return false;

        // Remove existing guests
        _db.Set<ConfRoomGuest>().RemoveRange(room.RoomGuests);

        // Add new guests
        foreach (var attendeeId in request.AttendeeIds)
        {
            _db.Set<ConfRoomGuest>().Add(new ConfRoomGuest
            {
                FRoomId = id,
                FAttendeeId = attendeeId
            });
        }

        room.FStatus = request.AttendeeIds.Any() ? "已分配" : "空闲";
        room.FUpdatedTime = DateTime.Now;
        await _db.SaveChangesAsync();
        return true;
    }

    #endregion

    #region Smart CSP Algorithm (5.2)

    /// <summary>
    /// CSP建模智能分房 (设计方案 5.2.1 + 5.2.2)
    /// 第一轮硬约束满足 + 第二轮软约束优化
    /// 返回预览结果，不直接写数据库
    /// </summary>
    public async Task<AutoAssignPreviewDto> AutoAssignAsync(int eventId)
    {
        var preview = new AutoAssignPreviewDto();

        // 1. 获取需要住宿且已确认的参会人
        var attendees = await _db.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FNeedAccommodation && a.FStatus == "已确认")
            .ToListAsync();

        // 排除已分配住宿的人员
        var assignedAttendeeIds = await _db.Set<ConfRoomGuest>()
            .Include(g => g.Room).ThenInclude(r => r.Hotel)
            .Where(g => g.Room.Hotel.FEventId == eventId)
            .Select(g => g.FAttendeeId)
            .ToListAsync();
        var assignedSet = new HashSet<long>(assignedAttendeeIds);

        var needAssign = attendees.Where(a => !assignedSet.Contains(a.FID)).ToList();

        // 2. 获取所有空闲房间
        var rooms = await _db.Set<ConfRoom>()
            .Include(r => r.Hotel)
            .Include(r => r.RoomGuests)
            .Where(r => r.Hotel.FEventId == eventId && !r.RoomGuests.Any())
            .OrderBy(r => r.FRoomNumber)
            .ToListAsync();

        // ======== 第一轮：硬约束满足 ========
        // 按角色优先级排序：嘉宾/领导 → 参会代表 → 工作人员
        var sorted = needAssign.OrderBy(a => GetRolePriority(a.FRole)).ToList();

        // 分配追踪 (roomId -> list of attendees)
        var assignments = new Dictionary<long, List<ConfAttendee>>();
        var roomLookup = rooms.ToDictionary(r => r.FID);
        var availableRooms = new List<ConfRoom>(rooms);
        var unassigned = new List<(ConfAttendee attendee, string reason)>();

        // VIP人员优先分配套房/大床房（单人）
        var vipAttendees = sorted.Where(a => IsVipRole(a.FRole)).ToList();
        var nonVipAttendees = sorted.Where(a => !IsVipRole(a.FRole)).ToList();

        foreach (var vip in vipAttendees)
        {
            var room = availableRooms
                .Where(r => r.FRoomType is "套房" or "大床房")
                .Where(r => !assignments.ContainsKey(r.FID))
                .FirstOrDefault();

            if (room == null)
            {
                // 退而求其次：尝试空标间
                room = availableRooms
                    .Where(r => r.FRoomType == "标间")
                    .Where(r => !assignments.ContainsKey(r.FID))
                    .FirstOrDefault();
            }

            if (room != null)
            {
                assignments[room.FID] = new List<ConfAttendee> { vip };
            }
            else
            {
                unassigned.Add((vip, "无可用房间"));
            }
        }

        // 非VIP人员：按性别分组，在标间中配对
        var maleGroup = nonVipAttendees.Where(a => a.FGender == "男").ToList();
        var femaleGroup = nonVipAttendees.Where(a => a.FGender == "女").ToList();
        var unknownGender = nonVipAttendees.Where(a => a.FGender != "男" && a.FGender != "女").ToList();

        AssignGroupToRooms(maleGroup, availableRooms, assignments, unassigned);
        AssignGroupToRooms(femaleGroup, availableRooms, assignments, unassigned);
        AssignGroupToRooms(unknownGender, availableRooms, assignments, unassigned);

        // ======== 第二轮：软约束优化 ========
        var iterationCount = needAssign.Count * 5;
        var currentScore = CalcSoftScore(assignments, roomLookup);

        for (int i = 0; i < iterationCount; i++)
        {
            // 随机选取两个可交换的分配
            var assignedRoomIds = assignments.Keys.Where(k => assignments[k].Count > 0).ToList();
            if (assignedRoomIds.Count < 2) break;

            var idx1 = _random.Next(assignedRoomIds.Count);
            var idx2 = _random.Next(assignedRoomIds.Count);
            if (idx1 == idx2) continue;

            var room1Id = assignedRoomIds[idx1];
            var room2Id = assignedRoomIds[idx2];
            var room1 = roomLookup.GetValueOrDefault(room1Id);
            var room2 = roomLookup.GetValueOrDefault(room2Id);
            if (room1 == null || room2 == null) continue;

            // 硬约束检查：性别隔离
            var guests1 = assignments[room1Id];
            var guests2 = assignments[room2Id];
            if (guests1.Count == 0 || guests2.Count == 0) continue;

            // 尝试交换第一个人
            var person1 = guests1[0];
            var person2 = guests2[0];

            // H1 性别检查：交换后不能混住
            if (!CanSwap(person1, person2, guests1, guests2, room1, room2)) continue;

            // 执行交换
            guests1[0] = person2;
            guests2[0] = person1;

            var newScore = CalcSoftScore(assignments, roomLookup);
            if (newScore > currentScore)
            {
                currentScore = newScore;
            }
            else
            {
                // 回滚
                guests1[0] = person1;
                guests2[0] = person2;
            }
        }

        // 构建预览结果
        foreach (var kvp in assignments)
        {
            if (kvp.Value.Count == 0) continue;
            var room = roomLookup.GetValueOrDefault(kvp.Key);
            if (room == null) continue;

            preview.Assignments.Add(new RoomAssignmentPreviewItem
            {
                RoomId = kvp.Key,
                RoomNumber = room.FRoomNumber,
                RoomType = room.FRoomType,
                HotelName = room.Hotel?.FHotelName,
                Guests = kvp.Value.Select(a => new AssignedGuestItem
                {
                    AttendeeId = a.FID,
                    Name = a.FName,
                    Gender = a.FGender,
                    Organization = a.FOrganization,
                    Role = a.FRole
                }).ToList()
            });
        }

        foreach (var (att, reason) in unassigned)
        {
            preview.UnassignedAttendees.Add(new UnassignedAttendeeItem
            {
                AttendeeId = att.FID,
                Name = att.FName,
                Reason = reason
            });
        }

        var totalNeed = needAssign.Count;
        var totalAssigned = assignments.Values.Sum(v => v.Count);
        preview.SatisfactionRate = totalNeed > 0 ? (double)totalAssigned / totalNeed * 100 : 100;

        _logger.LogInformation(
            "AutoAssign for Event {EventId}: {Assigned}/{Total} assigned, score={Score}, {Unassigned} unassigned",
            eventId, totalAssigned, totalNeed, currentScore, unassigned.Count);

        return preview;
    }

    public async Task<List<AttendeeListItemDto>> GetUnassignedAsync(int eventId)
    {
        var assignedIds = await _db.Set<ConfRoomGuest>()
            .Include(g => g.Room).ThenInclude(r => r.Hotel)
            .Where(g => g.Room.Hotel.FEventId == eventId)
            .Select(g => g.FAttendeeId)
            .ToListAsync();
        var assignedSet = new HashSet<long>(assignedIds);

        var unassigned = await _db.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FNeedAccommodation && a.FStatus == "已确认")
            .ToListAsync();

        return unassigned
            .Where(a => !assignedSet.Contains(a.FID))
            .Select(a => new AttendeeListItemDto
            {
                Id = a.FID,
                EventId = a.FEventId,
                Name = a.FName,
                Gender = a.FGender,
                Phone = a.FPhone,
                Organization = a.FOrganization,
                Title = a.FTitle,
                Role = a.FRole,
                NeedPickup = a.FNeedPickup,
                NeedAccommodation = a.FNeedAccommodation,
                Status = a.FStatus,
                ArrivalTime = a.FArrivalTime,
                DepartureTime = a.FDepartureTime
            }).ToList();
    }

    #endregion

    #region Statistics

    public async Task<AccommodationDemandStatsDto> GetDemandStatsAsync(int eventId)
    {
        var roomTypes = new[] { "标单", "标双", "套房", "大床房", "行政大床", "其他" };

        // 1. 获取活动日期作为默认到达/离开时间
        var confEvent = await _db.Set<ConfEvent>()
            .Where(e => e.FID == eventId)
            .Select(e => new { e.FStartDate, e.FEndDate })
            .FirstOrDefaultAsync();

       // 2. 获取需要住宿的参会人员（放宽查询，不在DB层过滤状态和时间）
        var attendees = await _db.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FNeedAccommodation)
            .Select(a => new { a.FID, a.FPrimaryGuestId, a.FRelation, a.FStatus, a.FArrivalTime, a.FDepartureTime, PreferredRoomType = a.FPreferredRoomType })
            .ToListAsync();

        // 分离主宾客和随行人员
        var primaryMap = attendees.Where(a => a.FPrimaryGuestId == null).ToDictionary(a => a.FID);
        var companionList = attendees.Where(a => a.FPrimaryGuestId != null).ToList();

        // 随行人员继承主宾客属性后，再做最终过滤
        var validAttendees = new List<(long FID, long? FPrimaryGuestId, string? FRelation, DateTime Arrival, DateTime Departure, string? PreferredRoomType)>();

        // 处理主宾客
        foreach (var guest in primaryMap.Values)
        {
            if (guest.FStatus != "已确认") continue;
            if (!guest.FArrivalTime.HasValue || !guest.FDepartureTime.HasValue) continue;
            var arrival = guest.FArrivalTime.Value.Date;
            var departure = guest.FDepartureTime.Value.Date;
            if (arrival > departure) continue;
            validAttendees.Add((guest.FID, guest.FPrimaryGuestId, guest.FRelation, arrival, departure, guest.PreferredRoomType));
        }

        // 处理随行人员（继承主宾客属性）
        foreach (var comp in companionList)
        {
            if (!primaryMap.TryGetValue(comp.FPrimaryGuestId!.Value, out var primary)) continue;

            // 继承状态：随行人员自身状态为空或非已确认时，使用主宾客状态
            var effectiveStatus = string.IsNullOrEmpty(comp.FStatus) || comp.FStatus != "已确认"
                ? primary.FStatus : comp.FStatus;
            if (effectiveStatus != "已确认") continue;

            // 继承时间：随行人员自身时间为空时，使用主宾客时间
            var arrival = (comp.FArrivalTime ?? primary.FArrivalTime)?.Date;
            var departure = (comp.FDepartureTime ?? primary.FDepartureTime)?.Date;
            if (!arrival.HasValue || !departure.HasValue) continue;
            if (arrival.Value > departure.Value) continue;

            validAttendees.Add((comp.FID, comp.FPrimaryGuestId, comp.FRelation, arrival.Value, departure.Value, comp.PreferredRoomType ?? primary.PreferredRoomType));
        }

        // 构建房间单元：配偶与主宾客共享一个房间，其他关系的随行人员各自独立一间
        var roomUnits = new List<(DateTime Arrival, DateTime Departure, string RoomType)>();

        var primaryGuests = validAttendees.Where(a => a.FPrimaryGuestId == null).ToList();
        var companions = validAttendees.Where(a => a.FPrimaryGuestId != null).ToList();

        foreach (var guest in primaryGuests)
        {
            // 主宾客本身算一个房间单元（配偶随行人员与主宾客同住，不额外计房）
            roomUnits.Add((guest.Arrival, guest.Departure, guest.PreferredRoomType ?? "其他"));

            // 非配偶随行人员，每人一个独立房间单元
            var nonSpouseCompanions = companions.Where(c => c.FPrimaryGuestId == guest.FID && c.FRelation != "配偶").ToList();
            foreach (var comp in nonSpouseCompanions)
            {
                roomUnits.Add((comp.Arrival, comp.Departure, comp.PreferredRoomType ?? guest.PreferredRoomType ?? "其他"));
            }
        }

        // 容错：处理没有对应主宾客记录的"孤立"随行人员
        var accountedCompanionIds = new HashSet<long>(
            companions.Where(c => primaryGuests.Any(g => g.FID == c.FPrimaryGuestId)).Select(c => c.FID));
        var orphanCompanions = companions.Where(c => !accountedCompanionIds.Contains(c.FID)).ToList();
        foreach (var orphan in orphanCompanions)
        {
            roomUnits.Add((orphan.Arrival, orphan.Departure, orphan.PreferredRoomType ?? "其他"));
        }

        // 3. 获取该活动所有房间（含RoomGuests）
        var rooms = await _db.Set<ConfRoom>()
            .Include(r => r.Hotel)
            .Include(r => r.RoomGuests)
            .Where(r => r.Hotel.FEventId == eventId)
            .Select(r => new
            {
                r.FRoomType,
                CheckIn = r.FCheckInDate,
                CheckOut = r.FCheckOutDate,
                HasGuests = r.RoomGuests.Any(),
                GuestCount = r.RoomGuests.Count()
            })
            .ToListAsync();

        // 4. 确定日期范围（基于房间单元）
        var allDates = new List<DateTime>();
        if (roomUnits.Any())
        {
            allDates.Add(roomUnits.Min(r => r.Arrival));
            allDates.Add(roomUnits.Max(r => r.Departure));
        }
        if (rooms.Any())
        {
            allDates.Add(rooms.Min(r => r.CheckIn.Date));
            allDates.Add(rooms.Max(r => r.CheckOut.Date));
        }

        var result = new AccommodationDemandStatsDto
        {
            TotalNeedAccommodation = roomUnits.Count
        };

        if (!allDates.Any())
            return result;

        var startDate = allDates.Min();
        var endDate = allDates.Max();

        // 5. 按日期逐天统计
        var totalByRoomType = new Dictionary<string, int>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var daily = new DailyDemandItem { Date = date };

            // 当日需要住宿的房间数（到达日 ≤ 当日 ≤ 离开日）
            daily.TotalDemand = roomUnits.Count(r => r.Arrival <= date && date < r.Departure);

            // 按房型统计房间
            foreach (var roomType in roomTypes)
            {
                var typeRooms = rooms.Where(r => NormalizeRoomType(r.FRoomType) == roomType).ToList();

                // 当日可用房间（入住日 ≤ 当日 ≤ 退房日）
                var availableRooms = typeRooms.Where(r => r.CheckIn.Date <= date && date <= r.CheckOut.Date).ToList();
                var totalAvailable = availableRooms.Count;
                var allocated = availableRooms.Count(r => r.HasGuests);

                // 当天该房型的需求房间数（基于房间单元的房型偏好）
                var demand = roomUnits
                    .Count(r => r.Arrival <= date && date < r.Departure
                        && NormalizeRoomType(r.RoomType) == roomType);
                daily.RoomTypes[roomType] = new RoomTypeStat
                {
                    Demand = demand,
                    Allocated = allocated,
                    Available = totalAvailable
                };
            }

            // 只在有需求或有可用房间时才添加
            if (daily.TotalDemand > 0 || daily.RoomTypes.Values.Any(rt => rt.Available > 0 || rt.Allocated > 0))
            {
                result.DailyStats.Add(daily);
            }
        }

        // 循环结束后单独计算房间总数（避免在循环内重复累加）
        foreach (var roomType in roomTypes)
        {
            var typeRooms = rooms.Where(r => NormalizeRoomType(r.FRoomType) == roomType).ToList();
            totalByRoomType[roomType] = typeRooms.Count;
        }

        result.TotalByRoomType = totalByRoomType;
        return result;
    }

    /// <summary>将房型标准化到预定义列表</summary>
    private static string NormalizeRoomType(string? roomType)
    {
        return roomType switch
        {
            "标单" => "标单",
            "标双" or "标间" => "标双",
            "套房" => "套房",
            "大床房" => "大床房",
            "行政大床" => "行政大床",
            _ => "其他"
        };
    }

    public async Task<List<RoomTypeGuestDto>> GetRoomTypeGuestsAsync(int eventId, DateTime date, string roomType)
    {
        var normalizedType = NormalizeRoomType(roomType);

        // 1. 查询所有需要住宿的参会人（放宽查询，不在DB层过滤状态和时间）
        var rawAttendees = await _db.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FNeedAccommodation)
            .Select(a => new {
                a.FID, a.FName, a.FGender, a.FOrganization, a.FPhone,
                a.FPreferredRoomType, a.FPrimaryGuestId, a.FRelation,
                a.FStatus, a.FArrivalTime, a.FDepartureTime
            })
            .ToListAsync();

        // 分离主宾客和随行人员
        var primaryMapRaw = rawAttendees.Where(a => a.FPrimaryGuestId == null).ToDictionary(a => a.FID);
        var companionListRaw = rawAttendees.Where(a => a.FPrimaryGuestId != null).ToList();

        // 随行人员继承主宾客属性后，再做最终过滤
        var attendees = new List<(long FID, string? FName, string? FGender, string? FOrganization, string? FPhone, string? FPreferredRoomType, long? FPrimaryGuestId, string? FRelation, DateTime Arrival, DateTime Departure)>();

        // 处理主宾客
        foreach (var guest in primaryMapRaw.Values)
        {
            if (guest.FStatus != "已确认") continue;
            if (!guest.FArrivalTime.HasValue || !guest.FDepartureTime.HasValue) continue;
            var arrival = guest.FArrivalTime.Value.Date;
            var departure = guest.FDepartureTime.Value.Date;
            if (arrival > departure) continue;
            attendees.Add((guest.FID, guest.FName, guest.FGender, guest.FOrganization, guest.FPhone, guest.FPreferredRoomType, guest.FPrimaryGuestId, guest.FRelation, arrival, departure));
        }

        // 处理随行人员（继承主宾客属性）
        foreach (var comp in companionListRaw)
        {
            if (!primaryMapRaw.TryGetValue(comp.FPrimaryGuestId!.Value, out var primary)) continue;

            var effectiveStatus = string.IsNullOrEmpty(comp.FStatus) || comp.FStatus != "已确认"
                ? primary.FStatus : comp.FStatus;
            if (effectiveStatus != "已确认") continue;

            var arrival = (comp.FArrivalTime ?? primary.FArrivalTime)?.Date;
            var departure = (comp.FDepartureTime ?? primary.FDepartureTime)?.Date;
            if (!arrival.HasValue || !departure.HasValue) continue;
            if (arrival.Value > departure.Value) continue;

            attendees.Add((comp.FID, comp.FName, comp.FGender, comp.FOrganization, comp.FPhone, comp.FPreferredRoomType ?? primary.FPreferredRoomType, comp.FPrimaryGuestId, comp.FRelation, arrival.Value, departure.Value));
        }

        var primaryGuests = attendees.Where(a => a.FPrimaryGuestId == null).ToList();
        var companions = attendees.Where(a => a.FPrimaryGuestId != null).ToList();

        var result = new List<RoomTypeGuestDto>();

        // 2. 查询已分配的房间信息（用于填充酒店/房号）
        var allAttendeeIds = attendees.Select(a => a.FID).ToList();
        var roomAssignments = await _db.Set<ConfRoomGuest>()
            .Include(rg => rg.Room)
                .ThenInclude(r => r.Hotel)
            .Where(rg => allAttendeeIds.Contains(rg.FAttendeeId))
            .ToListAsync();

        foreach (var guest in primaryGuests)
        {
            var guestRoomType = NormalizeRoomType(guest.FPreferredRoomType ?? "其他");

            if (guestRoomType == normalizedType
                && guest.Arrival <= date.Date && date.Date < guest.Departure)
            {
                var assignment = roomAssignments.FirstOrDefault(rg => rg.FAttendeeId == guest.FID);
                result.Add(new RoomTypeGuestDto
                {
                    AttendeeId = guest.FID,
                    Name = guest.FName,
                    Gender = guest.FGender ?? "",
                    Organization = guest.FOrganization ?? "",
                    Phone = guest.FPhone ?? "",
                    PreferredRoomType = guest.FPreferredRoomType ?? "其他",
                    HotelName = assignment?.Room?.Hotel?.FHotelName ?? "",
                    RoomNumber = assignment?.Room?.FRoomNumber ?? "",
                    PrimaryGuestName = null,
                    Relation = null
                });
            }

            // 非配偶随行人员，每人独立一间房
            var nonSpouseCompanions = companions
                .Where(c => c.FPrimaryGuestId == guest.FID && c.FRelation != "配偶")
                .ToList();

            foreach (var comp in nonSpouseCompanions)
            {
                var compRoomType = NormalizeRoomType(comp.FPreferredRoomType ?? "其他");

                if (compRoomType == normalizedType
                    && comp.Arrival <= date.Date && date.Date < comp.Departure)
                {
                    var assignment = roomAssignments.FirstOrDefault(rg => rg.FAttendeeId == comp.FID);
                    result.Add(new RoomTypeGuestDto
                    {
                        AttendeeId = comp.FID,
                        Name = comp.FName,
                        Gender = comp.FGender ?? "",
                        Organization = comp.FOrganization ?? "",
                        Phone = comp.FPhone ?? "",
                        PreferredRoomType = comp.FPreferredRoomType ?? "其他",
                        HotelName = assignment?.Room?.Hotel?.FHotelName ?? "",
                        RoomNumber = assignment?.Room?.FRoomNumber ?? "",
                        PrimaryGuestName = guest.FName,
                        Relation = comp.FRelation
                    });
                }
            }
        }

        // 容错：孤立随行人员（没有找到对应主宾客的）
        var accountedCompanionIds = new HashSet<long>(
            companions.Where(c => primaryGuests.Any(g => g.FID == c.FPrimaryGuestId)).Select(c => c.FID));
        var orphanCompanions = companions.Where(c => !accountedCompanionIds.Contains(c.FID)).ToList();
        foreach (var orphan in orphanCompanions)
        {
            var orphanRoomType = NormalizeRoomType(orphan.FPreferredRoomType ?? "其他");
            if (orphanRoomType == normalizedType
                && orphan.Arrival <= date.Date && date.Date < orphan.Departure)
            {
                var assignment = roomAssignments.FirstOrDefault(rg => rg.FAttendeeId == orphan.FID);
                result.Add(new RoomTypeGuestDto
                {
                    AttendeeId = orphan.FID,
                    Name = orphan.FName,
                    Gender = orphan.FGender ?? "",
                    Organization = orphan.FOrganization ?? "",
                    Phone = orphan.FPhone ?? "",
                    PreferredRoomType = orphan.FPreferredRoomType ?? "其他",
                    HotelName = assignment?.Room?.Hotel?.FHotelName ?? "",
                    RoomNumber = assignment?.Room?.FRoomNumber ?? "",
                    PrimaryGuestName = null,
                    Relation = orphan.FRelation
                });
            }
        }

        return result;
    }

    #endregion

    #region Export

    public Task<byte[]> ExportPdfAsync(int eventId)
    {
        // TODO: 使用 QuestPDF 生成住宿安排PDF（按酒店分组 -> 每个酒店列出房间）
        throw new NotImplementedException("PDF export not yet implemented");
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// 将同性别人员分配到标间（同单位优先配对）
    /// </summary>
    private void AssignGroupToRooms(
        List<ConfAttendee> group,
        List<ConfRoom> availableRooms,
        Dictionary<long, List<ConfAttendee>> assignments,
        List<(ConfAttendee, string)> unassigned)
    {
        // 按单位分组，同单位优先配对
        var orgGroups = group.GroupBy(a => a.FOrganization ?? "").ToList();
        var pending = new List<ConfAttendee>();

        foreach (var orgGroup in orgGroups)
        {
            var members = orgGroup.ToList();
            int i = 0;

            // 两两配对
            while (i + 1 < members.Count)
            {
                var room = FindAvailableRoom("标间", availableRooms, assignments);
                if (room != null)
                {
                    assignments[room.FID] = new List<ConfAttendee> { members[i], members[i + 1] };
                    i += 2;
                }
                else
                {
                    // 没有标间，尝试大床房/套房（单人）
                    var altRoom = FindAvailableRoom(null, availableRooms, assignments);
                    if (altRoom != null)
                    {
                        assignments[altRoom.FID] = new List<ConfAttendee> { members[i] };
                        i++;
                    }
                    else
                    {
                        // 全部无房
                        for (; i < members.Count; i++)
                            unassigned.Add((members[i], "无可用房间"));
                        break;
                    }
                }
            }

            // 剩余单人
            if (i < members.Count)
                pending.Add(members[i]);
        }

        // 尝试把跨单位的落单人配对
        int j = 0;
        while (j + 1 < pending.Count)
        {
            var room = FindAvailableRoom("标间", availableRooms, assignments);
            if (room != null)
            {
                assignments[room.FID] = new List<ConfAttendee> { pending[j], pending[j + 1] };
                j += 2;
            }
            else
            {
                break;
            }
        }

        // 剩余单人分配单间
        for (; j < pending.Count; j++)
        {
            var room = FindAvailableRoom(null, availableRooms, assignments);
            if (room != null)
            {
                assignments[room.FID] = new List<ConfAttendee> { pending[j] };
            }
            else
            {
                unassigned.Add((pending[j], "无可用房间"));
            }
        }
    }

    private static ConfRoom? FindAvailableRoom(string? preferredType, List<ConfRoom> rooms, Dictionary<long, List<ConfAttendee>> assignments)
    {
        foreach (var room in rooms)
        {
            if (assignments.ContainsKey(room.FID)) continue;
            if (preferredType != null && room.FRoomType != preferredType) continue;
            return room;
        }

        // 若指定类型无房，且不限制类型
        if (preferredType != null)
        {
            foreach (var room in rooms)
            {
                if (assignments.ContainsKey(room.FID)) continue;
                return room;
            }
        }

        return null;
    }

    /// <summary>计算软约束总得分 (5.2.1)</summary>
    private int CalcSoftScore(Dictionary<long, List<ConfAttendee>> assignments, Dictionary<long, ConfRoom> roomLookup)
    {
        int score = 0;

        foreach (var kvp in assignments)
        {
            var guests = kvp.Value;
            var room = roomLookup.GetValueOrDefault(kvp.Key);
            if (guests.Count == 0 || room == null) continue;

            // S1 同单位优先
            if (guests.Count == 2 && guests[0].FOrganization == guests[1].FOrganization
                && !string.IsNullOrEmpty(guests[0].FOrganization))
            {
                score += WeightSameOrg;
            }

            // S2 职级匹配：嘉宾/领导在套房/大床房
            foreach (var g in guests)
            {
                if (IsVipRole(g.FRole) && room.FRoomType is "套房" or "大床房")
                    score += WeightRankMatch;
            }

            // S3 VIP 单独住
            if (guests.Count == 1 && IsVipRole(guests[0].FRole))
                score += WeightVipSingle;

            // S5 填满优先：标间住满2人
            if (room.FRoomType == "标间" && guests.Count == 2)
                score += WeightFillFirst;
        }

        return score;
    }

    /// <summary>检查两人是否可交换（硬约束不违反）</summary>
    private static bool CanSwap(
        ConfAttendee p1, ConfAttendee p2,
        List<ConfAttendee> room1Guests, List<ConfAttendee> room2Guests,
        ConfRoom room1, ConfRoom room2)
    {
        // 模拟交换后的性别检查
        var room1After = room1Guests.Where(g => g.FID != p1.FID).Append(p2).ToList();
        var room2After = room2Guests.Where(g => g.FID != p2.FID).Append(p1).ToList();

        // H1 性别隔离
        if (room1After.Count > 1 && room1After.Select(g => g.FGender).Distinct().Count() > 1) return false;
        if (room2After.Count > 1 && room2After.Select(g => g.FGender).Distinct().Count() > 1) return false;

        // H2 房型容量
        var cap1 = GetCapacity(room1.FRoomType);
        var cap2 = GetCapacity(room2.FRoomType);
        if (room1After.Count > cap1 || room2After.Count > cap2) return false;

        return true;
    }

    private static int GetCapacity(string? roomType)
    {
        if (roomType != null && RoomCapacity.TryGetValue(roomType, out var cap)) return cap;
        return 2; // 默认
    }

    private static bool IsVipRole(string? role) => role is "嘉宾" or "领导";

    private static int GetRolePriority(string? role) => role switch
    {
        "嘉宾" => 0,
        "领导" => 1,
        "参会代表" => 2,
        "媒体" => 3,
        "工作人员" => 4,
        _ => 5
    };

    private async Task<HotelDto> GetHotelDtoAsync(long id)
    {
        var h = await _db.Set<ConfHotel>()
            .Include(x => x.Rooms).ThenInclude(r => r.RoomGuests).ThenInclude(g => g.Attendee)
            .FirstAsync(x => x.FID == id);

        return new HotelDto
        {
            Id = h.FID,
            EventId = h.FEventId,
            HotelName = h.FHotelName,
            Address = h.FAddress,
            Contact = h.FContact,
            ContactPhone = h.FContactPhone,
            AgreedPrice = h.FAgreedPrice,
            Remark = h.FRemark,
            CreatedTime = h.FCreatedTime,
            UpdatedTime = h.FUpdatedTime,
            Rooms = h.Rooms.Select(MapRoomDto).ToList()
        };
    }

    private static RoomDto MapRoomDto(ConfRoom r)
    {
        return new RoomDto
        {
            Id = r.FID,
            HotelId = r.FHotelId,
            RoomNumber = r.FRoomNumber,
            RoomType = r.FRoomType,
            CheckInDate = r.FCheckInDate,
            CheckOutDate = r.FCheckOutDate,
            Status = r.FStatus,
            Remark = r.FRemark,
            UpdatedTime = r.FUpdatedTime,
            Guests = r.RoomGuests?.Select(g => new AttendeeListItemDto
            {
                Id = g.Attendee?.FID ?? 0,
                EventId = g.Attendee?.FEventId ?? 0,
                Name = g.Attendee?.FName ?? "",
                Gender = g.Attendee?.FGender,
                Phone = g.Attendee?.FPhone,
                Organization = g.Attendee?.FOrganization,
                Title = g.Attendee?.FTitle,
                Role = g.Attendee?.FRole,
                NeedPickup = g.Attendee?.FNeedPickup ?? false,
                NeedAccommodation = g.Attendee?.FNeedAccommodation ?? false,
                Status = g.Attendee?.FStatus ?? ""
            }).ToList() ?? new()
        };
    }

    #endregion

    #region Excel Export

    public async Task<byte[]> ExportDemandStatsExcelAsync(int eventId)
    {
        // 1. 获取活动日期范围
        var confEvent = await _db.Set<ConfEvent>()
            .Where(e => e.FID == eventId)
            .Select(e => new { e.FStartDate, e.FEndDate })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("活动不存在");

        // 2. 查询已确认、需要住宿的宾客
        var attendees = await _db.Set<ConfAttendee>()
            .Where(a => a.FEventId == eventId && a.FNeedAccommodation)
            .Select(a => new
            {
                a.FID, a.FName, a.FPrimaryGuestId, a.FRelation, a.FStatus,
                a.FArrivalTime, a.FDepartureTime, a.FPreferredRoomType
            })
            .ToListAsync();

        var primaryMap = attendees.Where(a => a.FPrimaryGuestId == null).ToDictionary(a => a.FID);
        var companionList = attendees.Where(a => a.FPrimaryGuestId != null).ToList();

        // 构建有效宾客列表（继承主宾客属性）
        var validGuests = new List<(long FID, string Name, DateTime Arrival, DateTime Departure, string? RoomPref)>();

        foreach (var guest in primaryMap.Values)
        {
            if (guest.FStatus != "已确认") continue;
            if (!guest.FArrivalTime.HasValue || !guest.FDepartureTime.HasValue) continue;
            var arrival = guest.FArrivalTime.Value.Date;
            var departure = guest.FDepartureTime.Value.Date;
            if (arrival > departure) continue;
            validGuests.Add((guest.FID, guest.FName, arrival, departure, guest.FPreferredRoomType));
        }

        foreach (var comp in companionList)
        {
            if (!primaryMap.TryGetValue(comp.FPrimaryGuestId!.Value, out var primary)) continue;
            var effectiveStatus = string.IsNullOrEmpty(comp.FStatus) || comp.FStatus != "已确认"
                ? primary.FStatus : comp.FStatus;
            if (effectiveStatus != "已确认") continue;
            var arrival = (comp.FArrivalTime ?? primary.FArrivalTime)?.Date;
            var departure = (comp.FDepartureTime ?? primary.FDepartureTime)?.Date;
            if (!arrival.HasValue || !departure.HasValue) continue;
            if (arrival.Value > departure.Value) continue;
            validGuests.Add((comp.FID, comp.FName, arrival.Value, departure.Value,
                comp.FPreferredRoomType ?? primary.FPreferredRoomType));
        }

        // 3. 查询房间分配信息
        var rooms = await _db.Set<ConfRoom>()
            .Include(r => r.Hotel)
            .Include(r => r.RoomGuests)
            .Where(r => r.Hotel.FEventId == eventId && r.RoomGuests.Any())
            .Select(r => new
            {
                r.FID, r.FRoomType, r.FCheckInDate, r.FCheckOutDate,
                GuestIds = r.RoomGuests.Select(g => g.FAttendeeId).ToList()
            })
            .ToListAsync();

        var validGuestIds = validGuests.Select(g => g.FID).ToHashSet();
        var assignedGuestIds = new HashSet<long>();

        // 4. 按房间分组：同一房间的宾客归为一组（行）
        var rows = new List<(string Header, string RoomType, List<(long GuestId, DateTime Arrival, DateTime Departure)> Guests)>();

        foreach (var room in rooms)
        {
            var roomGuestIds = room.GuestIds.Where(id => validGuestIds.Contains(id)).ToList();
            if (!roomGuestIds.Any()) continue;

            var guestNames = new List<string>();
            var guestDateRanges = new List<(long GuestId, DateTime Arrival, DateTime Departure)>();

            foreach (var gid in roomGuestIds)
            {
                assignedGuestIds.Add(gid);
                var guest = validGuests.First(g => g.FID == gid);
                guestNames.Add(guest.Name);
                guestDateRanges.Add((gid, guest.Arrival, guest.Departure));
            }

            rows.Add((string.Join(",", guestNames), room.FRoomType ?? "其他", guestDateRanges));
        }

        // 5. 未分配房间的宾客：配偶关系归为同一组，其他单独成行
        var unassigned = validGuests.Where(g => !assignedGuestIds.Contains(g.FID)).ToList();
        var spouseGrouped = new HashSet<long>();

        // 查找主宾客及其配偶关系随行人员，归为同一组
        foreach (var guest in unassigned)
        {
            if (spouseGrouped.Contains(guest.FID)) continue;

            // 检查是否为主宾客（无 FPrimaryGuestId）
            var attendee = attendees.First(a => a.FID == guest.FID);
            if (attendee.FPrimaryGuestId != null) continue; // 随行人员稍后处理

            // 找出该主宾客的配偶随行人员（未分配房间的）
            var spouseCompanions = unassigned
                .Where(g => g.FID != guest.FID)
                .Where(g =>
                {
                    var comp = attendees.First(a => a.FID == g.FID);
                    return comp.FPrimaryGuestId == guest.FID && comp.FRelation == "配偶";
                })
                .ToList();

            if (spouseCompanions.Any())
            {
                var groupNames = new List<string> { guest.Name };
                var groupRanges = new List<(long, DateTime, DateTime)> { (guest.FID, guest.Arrival, guest.Departure) };
                spouseGrouped.Add(guest.FID);

                foreach (var sp in spouseCompanions)
                {
                    groupNames.Add(sp.Name);
                    groupRanges.Add((sp.FID, sp.Arrival, sp.Departure));
                    spouseGrouped.Add(sp.FID);
                }

                rows.Add((string.Join(",", groupNames), guest.RoomPref ?? "其他", groupRanges));
            }
        }

        // 剩余未分组的宾客单独成行
        foreach (var guest in unassigned.Where(g => !spouseGrouped.Contains(g.FID)))
        {
            rows.Add((guest.Name, guest.RoomPref ?? "其他",
                new List<(long, DateTime, DateTime)> { (guest.FID, guest.Arrival, guest.Departure) }));
        }

        // 6. 确定日期范围并过滤无住宿需求的日期
        var startDate = confEvent.FStartDate.Date;
        var endDate = confEvent.FEndDate.Date;
        if (validGuests.Any())
        {
            var minArrival = validGuests.Min(g => g.Arrival);
            var maxDeparture = validGuests.Max(g => g.Departure);
            if (minArrival < startDate) startDate = minArrival;
            if (maxDeparture > endDate) endDate = maxDeparture;
        }

        // 只保留至少有1位宾客需要住宿的日期
        var activeDates = new List<DateTime>();
        for (var date = startDate; date < endDate; date = date.AddDays(1))
        {
            var hasGuest = rows.Any(r => r.Guests.Any(g => g.Arrival <= date && date < g.Departure));
            if (hasGuest) activeDates.Add(date);
        }

        // 7. 构建Excel（行=宾客，列=日期）
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("住宿需求统计");

        // 表头样式
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;
        headerStyle.VerticalAlignment = VerticalAlignment.Center;

        // 数据单元格样式（居中）
        var dataStyle = workbook.CreateCellStyle();
        dataStyle.Alignment = HorizontalAlignment.Center;
        dataStyle.VerticalAlignment = VerticalAlignment.Center;

        // 写表头：第一列"宾客"，后续列为日期
        var hRow = sheet.CreateRow(0);
        var guestHeaderCell = hRow.CreateCell(0);
        guestHeaderCell.SetCellValue("宾客");
        guestHeaderCell.CellStyle = headerStyle;

        for (int i = 0; i < activeDates.Count; i++)
        {
            var cell = hRow.CreateCell(i + 1);
            cell.SetCellValue(activeDates[i].ToString("MM-dd"));
            cell.CellStyle = headerStyle;
        }

        // 冻结首列（宾客列）和首行（表头行）
        sheet.CreateFreezePane(1, 1);

        // 写数据行：每行一组宾客
        for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var dataRow = sheet.CreateRow(rowIdx + 1);
            var nameCell = dataRow.CreateCell(0);
            nameCell.SetCellValue(rows[rowIdx].Header);
            nameCell.CellStyle = dataStyle;

            for (int colIdx = 0; colIdx < activeDates.Count; colIdx++)
            {
                var date = activeDates[colIdx];
                var group = rows[rowIdx];
                var isPresent = group.Guests.Any(g => g.Arrival <= date && date < g.Departure);
                var cell = dataRow.CreateCell(colIdx + 1);
                cell.SetCellValue(isPresent ? group.RoomType : "");
                cell.CellStyle = dataStyle;
            }
        }

        // 自动列宽
        for (int i = 0; i <= activeDates.Count; i++)
            sheet.AutoSizeColumn(i);

        using var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        return output.ToArray();
    }

    #endregion
}
