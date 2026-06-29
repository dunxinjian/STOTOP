using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class AttendeeService : IAttendeeService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<AttendeeService> _logger;

    public AttendeeService(STOTOPDbContext dbContext, ILogger<AttendeeService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region Attendee CRUD

    public async Task<PagedResult<AttendeeListItemDto>> GetAttendeesAsync(int eventId, AttendeeQueryRequest request)
    {
        var query = _dbContext.Set<ConfAttendee>().AsNoTracking()
            .Include(a => a.Companions)
            .Where(a => a.FEventId == eventId && a.FPrimaryGuestId == null); // 只返回主宾客

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(a => a.FName.Contains(keyword)
                || (a.FPhone != null && a.FPhone.Contains(keyword))
                || (a.FOrganization != null && a.FOrganization.Contains(keyword))
                || a.Companions.Any(c => c.FName.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
            query = query.Where(a => a.FRole == request.Role);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(a => a.FStatus == request.Status);

        if (!string.IsNullOrWhiteSpace(request.Organization))
            query = query.Where(a => a.FOrganization == request.Organization);

        if (request.NeedPickup.HasValue)
            query = query.Where(a => a.FNeedPickup == request.NeedPickup.Value);

        if (request.NeedAccommodation.HasValue)
            query = query.Where(a => a.FNeedAccommodation == request.NeedAccommodation.Value);

        if (!string.IsNullOrEmpty(request.Camp))
            query = query.Where(a => a.FCamp == request.Camp);

        if (!string.IsNullOrWhiteSpace(request.CheckInStatus))
            query = query.Where(a => a.FCheckInStatus == request.CheckInStatus);

        if (request.HasClearTravelDate.HasValue)
        {
            if (request.HasClearTravelDate.Value)
            {
                // 已明确：到达和离开时间都非NULL
                query = query.Where(a => a.FArrivalTime.HasValue && a.FDepartureTime.HasValue);
            }
            else
            {
                // 未明确：到达或离开时间为NULL
                query = query.Where(a => !a.FArrivalTime.HasValue || !a.FDepartureTime.HasValue);
            }
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
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
                PrimaryGuestId = a.FPrimaryGuestId,
                Relation = a.FRelation,
                IsChild = a.FIsChild,
                Age = a.FAge,
                Camp = a.FCamp,
                GuestType = a.FGuestType,
                CompanionCount = a.FCompanionCount,
                HasSeat = a.FHasSeat,
                MealCategory = a.FMealCategory,
                Companions = a.Companions.Select(c => new CompanionDto
                {
                    Id = c.FID,
                    Name = c.FName,
                    Relation = c.FRelation,
                    IsChild = c.FIsChild,
                    Age = c.FAge,
                    HasSeat = c.FHasSeat,
                    MealCategory = c.FMealCategory
                }).ToList(),
                Status = a.FStatus,
                CheckInStatus = a.FCheckInStatus,
                ArrivalTime = a.FArrivalTime,
                DepartureTime = a.FDepartureTime
            })
            .ToListAsync();

        return new PagedResult<AttendeeListItemDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<AttendeeDto?> GetAttendeeByIdAsync(int id)
    {
        var entity = await _dbContext.Set<ConfAttendee>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<AttendeeDto> CreateAttendeeAsync(int eventId, CreateAttendeeRequest request)
    {
        var entity = new ConfAttendee
        {
            FEventId = eventId,
            FName = request.Name,
            FGender = request.Gender,
            FPhone = request.Phone,
            FOrganization = request.Organization,
            FTitle = request.Title,
            FRole = request.Role,
            FDietPreference = request.DietPreference,
            FArrivalMode = request.ArrivalMode,
            FArrivalFlightTrain = request.ArrivalFlightTrain,
            FArrivalTime = request.ArrivalTime,
            FArrivalStation = request.ArrivalStation,
            FDepartureMode = request.DepartureMode,
            FDepartureFlightTrain = request.DepartureFlightTrain,
            FDepartureTime = request.DepartureTime,
            FDepartureStation = request.DepartureStation,
            FNeedPickup = request.NeedPickup,
            FNeedAccommodation = request.NeedAccommodation,
            FPreferredRoomType = request.PreferredRoomType,
            FPrimaryGuestId = request.PrimaryGuestId,
            FRelation = request.Relation,
            FIsChild = request.IsChild ?? false,
            FAge = request.Age,
            FCamp = request.Camp,
            FGuestType = request.GuestType,
            FRemark = request.Remark,
            FStatus = "待确认",
            FCheckInStatus = request.CheckInStatus ?? "未签到",
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfAttendee>().Add(entity);
        await _dbContext.SaveChangesAsync();

        // 如果是随行人员，更新主宾客的 CompanionCount
        if (request.PrimaryGuestId.HasValue)
        {
            var primaryGuest = await _dbContext.Set<ConfAttendee>()
                .AsTracking()
                .FirstOrDefaultAsync(a => a.FID == request.PrimaryGuestId.Value);
            if (primaryGuest != null)
            {
                var companionCount = await _dbContext.Set<ConfAttendee>()
                    .CountAsync(a => a.FPrimaryGuestId == request.PrimaryGuestId.Value);
                primaryGuest.FCompanionCount = companionCount;
                primaryGuest.FUpdatedTime = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }
        }

        _logger.LogInformation("创建参会人 {AttendeeId}: {Name} (活动 {EventId})", entity.FID, entity.FName, eventId);
        return MapToDto(entity);
    }

    public async Task<AttendeeDto?> UpdateAttendeeAsync(int id, UpdateAttendeeRequest request)
    {
        var entity = await _dbContext.Set<ConfAttendee>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (entity == null) return null;

        entity.FName = request.Name;
        entity.FGender = request.Gender;
        entity.FPhone = request.Phone;
        entity.FOrganization = request.Organization;
        entity.FTitle = request.Title;
        entity.FRole = request.Role;
        entity.FDietPreference = request.DietPreference;
        entity.FArrivalMode = request.ArrivalMode;
        entity.FArrivalFlightTrain = request.ArrivalFlightTrain;
        entity.FArrivalTime = request.ArrivalTime;
        entity.FArrivalStation = request.ArrivalStation;
        entity.FDepartureMode = request.DepartureMode;
        entity.FDepartureFlightTrain = request.DepartureFlightTrain;
        entity.FDepartureTime = request.DepartureTime;
        entity.FDepartureStation = request.DepartureStation;
        entity.FNeedPickup = request.NeedPickup;
        entity.FNeedAccommodation = request.NeedAccommodation;
        entity.FPreferredRoomType = request.PreferredRoomType;
        entity.FRemark = request.Remark;
        entity.FCamp = request.Camp;
        entity.FGuestType = request.GuestType;
        entity.FStatus = request.Status ?? entity.FStatus;
        entity.FCheckInStatus = request.CheckInStatus ?? entity.FCheckInStatus;
        entity.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("更新参会人 {AttendeeId}: {Name}", entity.FID, entity.FName);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAttendeeAsync(int id)
    {
        var entity = await _dbContext.Set<ConfAttendee>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (entity == null) return false;

        if (entity.FPrimaryGuestId == null)
        {
            // 主宾客：先处理所有随行人员及其关联记录
            var companions = await _dbContext.Set<ConfAttendee>()
                .AsTracking()
                .Where(a => a.FPrimaryGuestId == id)
                .ToListAsync();

            foreach (var companion in companions)
            {
                await RemoveAttendeeRelatedRecordsAsync(companion.FID);
            }

            if (companions.Count > 0)
            {
                _dbContext.Set<ConfAttendee>().RemoveRange(companions);
                _logger.LogInformation("级联删除主宾客 {AttendeeId} 的 {Count} 名随行人员", id, companions.Count);
            }

            // 清除主宾客自身的关联记录，然后删除主宾客
            await RemoveAttendeeRelatedRecordsAsync(id);
            _dbContext.Set<ConfAttendee>().Remove(entity);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("删除参会人 {AttendeeId}", id);
            return true;
        }
        else
        {
            // 随行人员：清除关联记录后删除，再更新主宾客的 CompanionCount
            var primaryGuestId = entity.FPrimaryGuestId.Value;

            await RemoveAttendeeRelatedRecordsAsync(id);
            _dbContext.Set<ConfAttendee>().Remove(entity);
            await _dbContext.SaveChangesAsync();

            var primaryGuest = await _dbContext.Set<ConfAttendee>()
                .AsTracking()
                .FirstOrDefaultAsync(a => a.FID == primaryGuestId);
            if (primaryGuest != null)
            {
                var companionCount = await _dbContext.Set<ConfAttendee>()
                    .CountAsync(a => a.FPrimaryGuestId == primaryGuestId);
                primaryGuest.FCompanionCount = companionCount;
                primaryGuest.FUpdatedTime = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("删除随行人员 {AttendeeId}，更新主宾客 {PrimaryGuestId} 的随行人数", id, primaryGuestId);
            return true;
        }
    }

    /// <summary>清除宾客的所有关联记录（礼金、接送、住宿、用餐、座位、日程、收入）</summary>
    private async Task RemoveAttendeeRelatedRecordsAsync(long attendeeId)
    {
        var gifts = await _dbContext.Set<ConfGift>().Where(x => x.FAttendeeId == attendeeId).ToListAsync();
        if (gifts.Count > 0) _dbContext.Set<ConfGift>().RemoveRange(gifts);

        var incomes = await _dbContext.Set<ConfIncome>().Where(x => x.FAttendeeId == attendeeId).ToListAsync();
        if (incomes.Count > 0) _dbContext.Set<ConfIncome>().RemoveRange(incomes);

        var pickupPassengers = await _dbContext.Set<ConfPickupPassenger>().Where(x => x.FAttendeeId == attendeeId).ToListAsync();
        if (pickupPassengers.Count > 0) _dbContext.Set<ConfPickupPassenger>().RemoveRange(pickupPassengers);

        var roomGuests = await _dbContext.Set<ConfRoomGuest>().Where(x => x.FAttendeeId == attendeeId).ToListAsync();
        if (roomGuests.Count > 0) _dbContext.Set<ConfRoomGuest>().RemoveRange(roomGuests);

        var mealAttendees = await _dbContext.Set<ConfMealAttendee>().Where(x => x.FAttendeeId == attendeeId).ToListAsync();
        if (mealAttendees.Count > 0) _dbContext.Set<ConfMealAttendee>().RemoveRange(mealAttendees);

        var tableSeats = await _dbContext.Set<ConfTableSeat>().Where(x => x.FAttendeeId == attendeeId).ToListAsync();
        if (tableSeats.Count > 0) _dbContext.Set<ConfTableSeat>().RemoveRange(tableSeats);

        var scheduleAttendees = await _dbContext.Set<ConfScheduleAttendee>().Where(x => x.FAttendeeId == attendeeId).ToListAsync();
        if (scheduleAttendees.Count > 0) _dbContext.Set<ConfScheduleAttendee>().RemoveRange(scheduleAttendees);
    }

    #endregion

    #region Impact Analysis

    public async Task<AttendeeImpactAnalysisDto> GetImpactAnalysisAsync(int id)
    {
        var result = new AttendeeImpactAnalysisDto();

        // 检查接送任务关联
        var pickupPassengers = await _dbContext.Set<ConfPickupPassenger>().AsNoTracking()
            .Include(pp => pp.PickupTask)
            .Where(pp => pp.FAttendeeId == id)
            .ToListAsync();
        result.AffectedPickupTasks = pickupPassengers.Select(pp => new ImpactItem
        {
            Id = pp.FPickupTaskId,
            Description = $"接送任务 [{pp.PickupTask.FType}] {pp.PickupTask.FDate:yyyy-MM-dd} {pp.PickupTask.FOrigin}→{pp.PickupTask.FDestination}",
            ImpactType = "Remove"
        }).ToList();

        // 检查住宿分配
        var roomGuests = await _dbContext.Set<ConfRoomGuest>().AsNoTracking()
            .Include(rg => rg.Room).ThenInclude(r => r.Hotel)
            .Where(rg => rg.FAttendeeId == id)
            .ToListAsync();
        result.AffectedRoomAssignments = roomGuests.Select(rg => new ImpactItem
        {
            Id = rg.FRoomId,
            Description = $"房间 {rg.Room.Hotel.FHotelName} {rg.Room.FRoomNumber}",
            ImpactType = "Remove"
        }).ToList();

        // 检查餐食安排
        var mealAttendees = await _dbContext.Set<ConfMealAttendee>().AsNoTracking()
            .Include(ma => ma.MealPlan)
            .Where(ma => ma.FAttendeeId == id)
            .ToListAsync();
        result.AffectedMealPlans = mealAttendees.Select(ma => new ImpactItem
        {
            Id = ma.FMealPlanId,
            Description = $"餐食 {ma.MealPlan.FDate:yyyy-MM-dd} {ma.MealPlan.FMealType}",
            ImpactType = "Remove"
        }).ToList();

        // 检查桌次座位
        var tableSeats = await _dbContext.Set<ConfTableSeat>().AsNoTracking()
            .Include(ts => ts.Table)
            .Where(ts => ts.FAttendeeId == id)
            .ToListAsync();
        result.AffectedTableSeats = tableSeats.Select(ts => new ImpactItem
        {
            Id = ts.FTableId,
            Description = $"桌次 {ts.Table.FTableName ?? $"第{ts.Table.FTableNumber}桌"} 座位{ts.FSeatNumber}",
            ImpactType = "Remove"
        }).ToList();

        // 检查日程关联
        var scheduleAttendees = await _dbContext.Set<ConfScheduleAttendee>().AsNoTracking()
            .Include(sa => sa.Schedule)
            .Where(sa => sa.FAttendeeId == id)
            .ToListAsync();
        result.AffectedSchedules = scheduleAttendees.Select(sa => new ImpactItem
        {
            Id = sa.FScheduleId,
            Description = $"日程 {sa.Schedule.FDate:yyyy-MM-dd} {sa.Schedule.FTitle}",
            ImpactType = "Remove"
        }).ToList();

        return result;
    }

    public async Task<bool> ApplyChangesAsync(int id)
    {
        var entity = await _dbContext.Set<ConfAttendee>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.FID == id);

        if (entity == null) return false;

        // 移除所有关联记录
        var pickupPassengers = await _dbContext.Set<ConfPickupPassenger>()
            .AsTracking().Where(pp => pp.FAttendeeId == id).ToListAsync();
        _dbContext.Set<ConfPickupPassenger>().RemoveRange(pickupPassengers);

        var roomGuests = await _dbContext.Set<ConfRoomGuest>()
            .AsTracking().Where(rg => rg.FAttendeeId == id).ToListAsync();
        _dbContext.Set<ConfRoomGuest>().RemoveRange(roomGuests);

        var mealAttendees = await _dbContext.Set<ConfMealAttendee>()
            .AsTracking().Where(ma => ma.FAttendeeId == id).ToListAsync();
        _dbContext.Set<ConfMealAttendee>().RemoveRange(mealAttendees);

        var tableSeats = await _dbContext.Set<ConfTableSeat>()
            .AsTracking().Where(ts => ts.FAttendeeId == id).ToListAsync();
        _dbContext.Set<ConfTableSeat>().RemoveRange(tableSeats);

        var scheduleAttendees = await _dbContext.Set<ConfScheduleAttendee>()
            .AsTracking().Where(sa => sa.FAttendeeId == id).ToListAsync();
        _dbContext.Set<ConfScheduleAttendee>().RemoveRange(scheduleAttendees);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("已清除参会人 {AttendeeId} 的所有关联安排", id);
        return true;
    }

    #endregion

    #region Import / Export

    private static readonly string[] ImportHeaders = new[]
    {
        "姓名", "性别", "单位", "职务", "角色", "阵营", "宾客类型", "电话", "邮箱", "身份证号",
        "到达方式", "航班车次", "到达时间", "到达站点",
        "离开方式", "离开航班车次", "离开时间", "离开站点",
        "是否需接送", "是否需住宿", "房型偏好", "饮食禁忌", "备注"
    };

    private static readonly string[] ExportHeaders = new[]
    {
        "序号", "姓名", "性别", "单位", "职务", "角色", "阵营", "宾客类型", "电话", "邮箱",
        "到达方式", "到达时间", "到达站点", "离开方式", "离开时间", "离开站点",
        "是否需住宿", "饮食禁忌", "签到状态", "备注"
    };

    public async Task<List<AttendeeDto>> ImportAttendeesAsync(int eventId, Stream excelStream)
    {
        _logger.LogInformation("导入参会人 Excel (活动 {EventId})", eventId);

        // 复制到 MemoryStream 避免 NPOI 关闭原始流
        using var ms = new MemoryStream();
        await excelStream.CopyToAsync(ms);
        ms.Position = 0;

        IWorkbook workbook = new XSSFWorkbook(ms);
        var sheet = workbook.GetSheetAt(0);
        if (sheet == null || sheet.LastRowNum < 1)
            return new List<AttendeeDto>();

        // 读取表头映射
        var headerRow = sheet.GetRow(0);
        var colMap = new Dictionary<string, int>();
        for (int c = 0; c < headerRow.LastCellNum; c++)
        {
            var val = headerRow.GetCell(c)?.StringCellValue?.Trim();
            if (!string.IsNullOrEmpty(val))
                colMap[val] = c;
        }

        var results = new List<AttendeeDto>();
        for (int r = 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;

            var name = GetCell(row, colMap, "姓名");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var entity = new ConfAttendee
            {
                FEventId = eventId,
                FName = name.Trim(),
                FGender = GetCell(row, colMap, "性别"),
                FOrganization = GetCell(row, colMap, "单位"),
                FTitle = GetCell(row, colMap, "职务"),
                FRole = GetCell(row, colMap, "角色"),
                FCamp = GetCell(row, colMap, "阵营"),
                FGuestType = GetCell(row, colMap, "宾客类型"),
                FPhone = GetCell(row, colMap, "电话"),
                FArrivalMode = GetCell(row, colMap, "到达方式"),
                FArrivalFlightTrain = GetCell(row, colMap, "航班车次"),
                FArrivalStation = GetCell(row, colMap, "到达站点"),
                FDepartureMode = GetCell(row, colMap, "离开方式"),
                FDepartureFlightTrain = GetCell(row, colMap, "离开航班车次"),
                FDepartureStation = GetCell(row, colMap, "离开站点"),
                FNeedPickup = GetCell(row, colMap, "是否需接送") == "是",
                FNeedAccommodation = GetCell(row, colMap, "是否需住宿") == "是",
                FPreferredRoomType = GetCell(row, colMap, "房型偏好"),
                FDietPreference = GetCell(row, colMap, "饮食禁忌"),
                FRemark = GetCell(row, colMap, "备注"),
                FStatus = "待确认",
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now
            };

            // 解析到达时间
            var arrivalTimeStr = GetCell(row, colMap, "到达时间");
            if (DateTime.TryParse(arrivalTimeStr, out var arrivalTime))
                entity.FArrivalTime = arrivalTime;

            // 解析离开时间
            var departureTimeStr = GetCell(row, colMap, "离开时间");
            if (DateTime.TryParse(departureTimeStr, out var departureTime))
                entity.FDepartureTime = departureTime;

            _dbContext.Set<ConfAttendee>().Add(entity);
            await _dbContext.SaveChangesAsync();
            results.Add(MapToDto(entity));
        }

        workbook.Close();
        _logger.LogInformation("导入完成，共导入 {Count} 人 (活动 {EventId})", results.Count, eventId);
        return results;
    }

    public async Task<byte[]> ExportAttendeesAsync(int eventId)
    {
        _logger.LogInformation("导出参会人 Excel (活动 {EventId})", eventId);

        // 查询主宾客及其随行人员
        var primaryGuests = await _dbContext.Set<ConfAttendee>().AsNoTracking()
            .Include(a => a.Companions)
            .Where(a => a.FEventId == eventId && a.FPrimaryGuestId == null)
            .OrderByDescending(a => a.FCreatedTime)
            .ToListAsync();

        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("参会人员名册");

        // 表头样式
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;

        // 随行人员样式（灰色背景）
        var companionStyle = workbook.CreateCellStyle();
        var companionFont = workbook.CreateFont();
        companionFont.Color = NPOI.HSSF.Util.HSSFColor.Grey50Percent.Index;
        companionStyle.SetFont(companionFont);

        // 写表头
        var hRow = sheet.CreateRow(0);
        for (int i = 0; i < ExportHeaders.Length; i++)
        {
            var cell = hRow.CreateCell(i);
            cell.SetCellValue(ExportHeaders[i]);
            cell.CellStyle = headerStyle;
        }
        sheet.CreateFreezePane(0, 1);

        // 写数据
        int rowIndex = 0;
        int seqNo = 0;
        foreach (var a in primaryGuests)
        {
            seqNo++;
            rowIndex++;
            var row = sheet.CreateRow(rowIndex);
            row.CreateCell(0).SetCellValue(seqNo);
            row.CreateCell(1).SetCellValue(a.FName);
            row.CreateCell(2).SetCellValue(a.FGender ?? "");
            row.CreateCell(3).SetCellValue(a.FOrganization ?? "");
            row.CreateCell(4).SetCellValue(a.FTitle ?? "");
            row.CreateCell(5).SetCellValue(a.FRole ?? "");
            row.CreateCell(6).SetCellValue(a.FCamp ?? "");
            row.CreateCell(7).SetCellValue(a.FGuestType ?? "");
            row.CreateCell(8).SetCellValue(a.FPhone ?? "");
            row.CreateCell(9).SetCellValue("");
            row.CreateCell(10).SetCellValue(a.FArrivalMode ?? "");
            row.CreateCell(11).SetCellValue(a.FArrivalTime?.ToString("yyyy-MM-dd HH:mm") ?? "");
            row.CreateCell(12).SetCellValue(a.FArrivalStation ?? "");
            row.CreateCell(13).SetCellValue(a.FDepartureMode ?? "");
            row.CreateCell(14).SetCellValue(a.FDepartureTime?.ToString("yyyy-MM-dd HH:mm") ?? "");
            row.CreateCell(15).SetCellValue(a.FDepartureStation ?? "");
            row.CreateCell(16).SetCellValue(a.FNeedAccommodation ? "是" : "否");
            row.CreateCell(17).SetCellValue(a.FDietPreference ?? "");
            row.CreateCell(18).SetCellValue(a.FCheckInStatus ?? "");
            row.CreateCell(19).SetCellValue(a.FRemark ?? "");

            // 追加随行人员子行
            if (a.Companions != null)
            {
                foreach (var c in a.Companions)
                {
                    rowIndex++;
                    var cRow = sheet.CreateRow(rowIndex);
                    cRow.CreateCell(0).SetCellValue("");
                    var nameCell = cRow.CreateCell(1);
                    nameCell.SetCellValue($"  └ {c.FName} ({c.FRelation ?? "随行"})");
                    nameCell.CellStyle = companionStyle;
                    cRow.CreateCell(2).SetCellValue(c.FGender ?? "");
                    cRow.CreateCell(3).SetCellValue("");
                    cRow.CreateCell(4).SetCellValue("");
                    cRow.CreateCell(5).SetCellValue("");
                    cRow.CreateCell(6).SetCellValue("");
                    cRow.CreateCell(7).SetCellValue("");
                    cRow.CreateCell(8).SetCellValue(c.FPhone ?? "");
                    cRow.CreateCell(9).SetCellValue("");
                    cRow.CreateCell(10).SetCellValue("");
                    cRow.CreateCell(11).SetCellValue("");
                    cRow.CreateCell(12).SetCellValue("");
                    cRow.CreateCell(13).SetCellValue("");
                    cRow.CreateCell(14).SetCellValue("");
                    cRow.CreateCell(15).SetCellValue("");
                    cRow.CreateCell(16).SetCellValue("");
                    cRow.CreateCell(17).SetCellValue(c.FDietPreference ?? "");
                    cRow.CreateCell(18).SetCellValue("");
                    cRow.CreateCell(19).SetCellValue(c.FIsChild ? $"儿童(年龄{c.FAge})" : "");
                }
            }
        }

        // 自动列宽
        for (int i = 0; i < ExportHeaders.Length; i++)
            sheet.AutoSizeColumn(i);

        using var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        return output.ToArray();
    }

    public Task<byte[]> GenerateImportTemplateAsync()
    {
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("导入模板");

        // 表头样式
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;

        // 必填标记样式（红色字体）
        var requiredStyle = workbook.CreateCellStyle();
        var requiredFont = workbook.CreateFont();
        requiredFont.IsBold = true;
        requiredFont.Color = NPOI.HSSF.Util.HSSFColor.Red.Index;
        requiredStyle.SetFont(requiredFont);
        requiredStyle.Alignment = HorizontalAlignment.Center;

        // 写表头
        var hRow = sheet.CreateRow(0);
        for (int i = 0; i < ImportHeaders.Length; i++)
        {
            var cell = hRow.CreateCell(i);
            cell.SetCellValue(ImportHeaders[i]);
            cell.CellStyle = i == 0 ? requiredStyle : headerStyle; // 姓名为必填
        }
        sheet.CreateFreezePane(0, 1);

        // 示例数据行1
        var row1 = sheet.CreateRow(1);
        row1.CreateCell(0).SetCellValue("张三");
        row1.CreateCell(1).SetCellValue("男");
        row1.CreateCell(2).SetCellValue("XX科技有限公司");
        row1.CreateCell(3).SetCellValue("总经理");
        row1.CreateCell(4).SetCellValue("嘉宾");
        row1.CreateCell(5).SetCellValue("男方");
        row1.CreateCell(6).SetCellValue("亲属");
        row1.CreateCell(7).SetCellValue("13800138000");
        row1.CreateCell(8).SetCellValue("zhangsan@example.com");
        row1.CreateCell(9).SetCellValue("110101199001011234");
        row1.CreateCell(10).SetCellValue("飞机");
        row1.CreateCell(11).SetCellValue("CA1234");
        row1.CreateCell(12).SetCellValue("2026-05-01 14:30");
        row1.CreateCell(13).SetCellValue("首都机场T3");
        row1.CreateCell(14).SetCellValue("火车");
        row1.CreateCell(15).SetCellValue("G1234");
        row1.CreateCell(16).SetCellValue("2026-05-03 10:00");
        row1.CreateCell(17).SetCellValue("北京南站");
        row1.CreateCell(18).SetCellValue("是");
        row1.CreateCell(19).SetCellValue("是");
        row1.CreateCell(20).SetCellValue("标单");
        row1.CreateCell(21).SetCellValue("无");
        row1.CreateCell(22).SetCellValue("");

        // 示例数据行2
        var row2 = sheet.CreateRow(2);
        row2.CreateCell(0).SetCellValue("李四");
        row2.CreateCell(1).SetCellValue("女");
        row2.CreateCell(2).SetCellValue("YY集团");
        row2.CreateCell(3).SetCellValue("副总裁");
        row2.CreateCell(4).SetCellValue("领导");
        row2.CreateCell(5).SetCellValue("女方");
        row2.CreateCell(6).SetCellValue("朋友");
        row2.CreateCell(7).SetCellValue("13900139000");
        row2.CreateCell(8).SetCellValue("lisi@example.com");
        row2.CreateCell(9).SetCellValue("310101198505052345");
        row2.CreateCell(10).SetCellValue("火车");
        row2.CreateCell(11).SetCellValue("G5678");
        row2.CreateCell(12).SetCellValue("2026-05-01 09:00");
        row2.CreateCell(13).SetCellValue("北京西站");
        row2.CreateCell(14).SetCellValue("飞机");
        row2.CreateCell(15).SetCellValue("MU5678");
        row2.CreateCell(16).SetCellValue("2026-05-03 18:00");
        row2.CreateCell(17).SetCellValue("大兴机场");
        row2.CreateCell(18).SetCellValue("否");
        row2.CreateCell(19).SetCellValue("是");
        row2.CreateCell(20).SetCellValue("标双");
        row2.CreateCell(21).SetCellValue("素食");
        row2.CreateCell(22).SetCellValue("需要安静房间");

        // 自动列宽
        for (int i = 0; i < ImportHeaders.Length; i++)
            sheet.AutoSizeColumn(i);

        using var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        return Task.FromResult(output.ToArray());
    }

    private static string? GetCell(IRow row, Dictionary<string, int> colMap, string header)
    {
        if (!colMap.TryGetValue(header, out var idx)) return null;
        var cell = row.GetCell(idx);
        if (cell == null) return null;
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue?.Trim(),
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm")
                : cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue ? "是" : "否",
            CellType.Formula => cell.CachedFormulaResultType == CellType.String
                ? cell.StringCellValue?.Trim()
                : cell.NumericCellValue.ToString(),
            _ => null
        };
    }

    #endregion

    #region Mapping

    private static AttendeeDto MapToDto(ConfAttendee entity)
    {
        return new AttendeeDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            Name = entity.FName,
            Gender = entity.FGender,
            Phone = entity.FPhone,
            Organization = entity.FOrganization,
            Title = entity.FTitle,
            Role = entity.FRole,
            DietPreference = entity.FDietPreference,
            ArrivalMode = entity.FArrivalMode,
            ArrivalFlightTrain = entity.FArrivalFlightTrain,
            ArrivalTime = entity.FArrivalTime,
            ArrivalStation = entity.FArrivalStation,
            DepartureMode = entity.FDepartureMode,
            DepartureFlightTrain = entity.FDepartureFlightTrain,
            DepartureTime = entity.FDepartureTime,
            DepartureStation = entity.FDepartureStation,
            NeedPickup = entity.FNeedPickup,
            NeedAccommodation = entity.FNeedAccommodation,
            PreferredRoomType = entity.FPreferredRoomType,
            PrimaryGuestId = entity.FPrimaryGuestId,
            Relation = entity.FRelation,
            IsChild = entity.FIsChild,
            Age = entity.FAge,
            Camp = entity.FCamp,
            GuestType = entity.FGuestType,
            CompanionCount = entity.FCompanionCount,
            HasSeat = entity.FHasSeat,
            MealCategory = entity.FMealCategory,
            Companions = entity.Companions?.Select(c => new CompanionDto
            {
                Id = c.FID,
                Name = c.FName,
                Relation = c.FRelation,
                IsChild = c.FIsChild,
                Age = c.FAge,
                HasSeat = c.FHasSeat,
                MealCategory = c.FMealCategory
            }).ToList(),
            Remark = entity.FRemark,
            Status = entity.FStatus,
            CheckInStatus = entity.FCheckInStatus,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    #endregion

    public async Task<List<AttendeeDto>> GetCompanionsAsync(long primaryGuestId)
    {
        var companions = await _dbContext.Set<ConfAttendee>().AsNoTracking()
            .Where(a => a.FPrimaryGuestId == primaryGuestId)
            .OrderBy(a => a.FCreatedTime)
            .ToListAsync();

        return companions.Select(MapToDto).ToList();
    }

    public async Task BatchUpdateStatusAsync(BatchUpdateStatusRequest request)
    {
        var attendees = await _dbContext.Set<ConfAttendee>()
            .AsTracking()
            .Where(a => request.AttendeeIds.Contains(a.FID))
            .ToListAsync();
        foreach (var attendee in attendees)
        {
            if (request.CheckInStatus != null) attendee.FCheckInStatus = request.CheckInStatus;
            if (request.Status != null) attendee.FStatus = request.Status;
            attendee.FUpdatedTime = DateTime.Now;
        }
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateRoomPreferenceAsync(long attendeeId, string? preferredRoomType)
    {
        var attendee = await _dbContext.Set<ConfAttendee>()
            .AsTracking()
            .FirstOrDefaultAsync(a => a.FID == attendeeId);

        if (attendee == null)
            throw new InvalidOperationException("参会人员不存在");

        attendee.FPreferredRoomType = preferredRoomType;
        attendee.FUpdatedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();
    }
}
