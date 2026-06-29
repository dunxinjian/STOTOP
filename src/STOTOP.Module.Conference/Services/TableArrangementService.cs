using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class TableArrangementService : ITableArrangementService
{
    private readonly STOTOPDbContext _dbContext;

    /// <summary>角色优先级（从高到低）</summary>
    private static readonly string[] RolePriority = { "领导", "嘉宾", "参会代表", "工作人员", "媒体" };

    public TableArrangementService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TableDto>> GetTablesAsync(int mealId)
    {
        var tables = await _dbContext.Set<ConfTable>()
            .Where(t => t.FMealPlanId == mealId)
            .Include(t => t.Seats)
                .ThenInclude(s => s.Attendee)
            .OrderBy(t => t.FTableNumber)
            .ToListAsync();

        return tables.Select(MapToDto).ToList();
    }

    public async Task<TableDto> CreateTableAsync(int mealId, CreateTableRequest request)
    {
        var table = new ConfTable
        {
            FMealPlanId = mealId,
            FTableNumber = request.TableNumber,
            FTableName = request.TableName,
            FSeatCount = request.SeatCount,
            FRemark = request.Remark,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfTable>().Add(table);
        await _dbContext.SaveChangesAsync();

        return MapToDto(table);
    }

    public async Task<TableDto?> UpdateTableAsync(int id, UpdateTableRequest request)
    {
        var table = await _dbContext.Set<ConfTable>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);

        if (table == null) return null;

        table.FTableNumber = request.TableNumber;
        table.FTableName = request.TableName;
        table.FSeatCount = request.SeatCount;
        table.FRemark = request.Remark;
        table.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        // 重新加载含座位
        var updated = await _dbContext.Set<ConfTable>()
            .Include(t => t.Seats)
                .ThenInclude(s => s.Attendee)
            .FirstOrDefaultAsync(t => t.FID == id);

        return updated == null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteTableAsync(int id)
    {
        var table = await _dbContext.Set<ConfTable>()
            .AsTracking()
            .Include(t => t.Seats)
            .FirstOrDefaultAsync(t => t.FID == id);

        if (table == null) return false;

        _dbContext.Set<ConfTableSeat>().RemoveRange(table.Seats);
        _dbContext.Set<ConfTable>().Remove(table);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<TableDto?> SetTableSeatsAsync(int id, TableSeatRequest request)
    {
        var table = await _dbContext.Set<ConfTable>()
            .AsTracking()
            .Include(t => t.Seats)
            .FirstOrDefaultAsync(t => t.FID == id);

        if (table == null) return null;

        // 清除现有座位
        _dbContext.Set<ConfTableSeat>().RemoveRange(table.Seats);

        // 添加新座位
        foreach (var input in request.Seats)
        {
            var seat = new ConfTableSeat
            {
                FTableId = id,
                FAttendeeId = input.AttendeeId,
                FSeatNumber = input.SeatNumber,
                FRemark = input.Remark
            };
            _dbContext.Set<ConfTableSeat>().Add(seat);
        }

        table.FUpdatedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        // 重新加载
        var updated = await _dbContext.Set<ConfTable>()
            .Include(t => t.Seats)
                .ThenInclude(s => s.Attendee)
            .FirstOrDefaultAsync(t => t.FID == id);

        return updated == null ? null : MapToDto(updated);
    }

    public async Task<AutoArrangePreviewDto> AutoArrangeAsync(int mealId, AutoArrangeConfigRequest request)
    {
        try
        {
        // 参数校验：验证餐次存在
        var mealPlanExists = await _dbContext.Set<ConfMealPlan>()
            .AnyAsync(p => p.FID == mealId);
        if (!mealPlanExists)
            throw new Exception($"餐次ID {mealId} 不存在");

        // 清空该餐次现有桌次和座位（智能编桌是重新全量安排）
        var existingTables = await _dbContext.Set<ConfTable>()
            .AsTracking()
            .Where(t => t.FMealPlanId == mealId)
            .Include(t => t.Seats)
            .ToListAsync();
        foreach (var table in existingTables)
        {
            _dbContext.Set<ConfTableSeat>().RemoveRange(table.Seats);
            _dbContext.Set<ConfTable>().Remove(table);
        }
        await _dbContext.SaveChangesAsync();

        var result = new AutoArrangePreviewDto();

        // 获取餐次的用餐人员
        var mealAttendees = await _dbContext.Set<ConfMealAttendee>()
            .Where(ma => ma.FMealPlanId == mealId)
            .Include(ma => ma.Attendee)
            .ToListAsync();

        var attendees = mealAttendees
            .Where(ma => ma.Attendee != null)
            .Select(ma => ma.Attendee)
            .ToList();

        if (attendees.Count == 0)
        {
            // 如果没有设置用餐人员，获取活动全部参会人员
            var mealPlan = await _dbContext.Set<ConfMealPlan>()
                .FirstOrDefaultAsync(p => p.FID == mealId);

            if (mealPlan != null)
            {
                attendees = await _dbContext.Set<ConfAttendee>()
                    .Where(a => a.FEventId == mealPlan.FEventId && a.FStatus != "已取消")
                    .ToListAsync();
            }
        }

        if (attendees.Count == 0) return result;

        // 过滤掉不占座的人员
        attendees = attendees.Where(a => a.FHasSeat).ToList();
        if (attendees.Count == 0) return result;

        var seatsPerTable = request.SeatsPerTable > 0 ? request.SeatsPerTable : 10;
        var tableNumber = 1;
        var assignedIds = new HashSet<long>();

        // 构建主宾-随行关系映射
        var companionMap = new Dictionary<long, List<ConfAttendee>>();
        foreach (var att in attendees.Where(a => a.FPrimaryGuestId.HasValue))
        {
            var primaryId = att.FPrimaryGuestId!.Value;
            if (!companionMap.ContainsKey(primaryId))
                companionMap[primaryId] = new List<ConfAttendee>();
            companionMap[primaryId].Add(att);
        }

        // 主宾列表（不含随行人员，随行人员将跟随主宾一起处理）
        var primaryGuests = attendees.Where(a => !a.FPrimaryGuestId.HasValue).ToList();

        // 辅助方法：将参会人转为分配项
        AssignedGuestItem ToGuestItem(ConfAttendee a) => new AssignedGuestItem
        {
            AttendeeId = a.FID,
            Name = a.FName,
            Gender = a.FGender,
            Organization = a.FOrganization,
            Role = a.FRole
        };

        // 辅助方法：将主宾及其随行人员作为一组添加到 guest 列表
        void AddGroupToList(List<AssignedGuestItem> list, ConfAttendee primary)
        {
            list.Add(ToGuestItem(primary));
            if (companionMap.TryGetValue(primary.FID, out var companions))
            {
                foreach (var c in companions)
                    list.Add(ToGuestItem(c));
            }
        }

        // 辅助方法：获取主宾组的总人数（主宾 + 随行）
        int GetGroupSize(ConfAttendee primary)
        {
            return 1 + (companionMap.TryGetValue(primary.FID, out var companions) ? companions.Count : 0);
        }

        // 辅助方法：标记主宾组所有人已分配
        void MarkGroupAssigned(ConfAttendee primary)
        {
            assignedIds.Add(primary.FID);
            if (companionMap.TryGetValue(primary.FID, out var companions))
            {
                foreach (var c in companions)
                    assignedIds.Add(c.FID);
            }
        }

        // Step 1: 主桌 - 领导 > 嘉宾优先
        if (request.EnableMainTable)
        {
            var mainRoles = new[] { "领导", "嘉宾" };
            var mainCandidates = primaryGuests
                .Where(a => mainRoles.Contains(a.FRole))
                .OrderBy(a => Array.IndexOf(mainRoles, a.FRole))
                .ToList();

            if (mainCandidates.Count > 0)
            {
                var mainGuestList = new List<AssignedGuestItem>();
                var mainAssigned = new List<ConfAttendee>();
                foreach (var candidate in mainCandidates)
                {
                    var groupSize = GetGroupSize(candidate);
                    if (mainGuestList.Count + groupSize <= seatsPerTable)
                    {
                        AddGroupToList(mainGuestList, candidate);
                        mainAssigned.Add(candidate);
                    }
                }

                if (mainGuestList.Count > 0)
                {
                    var mainTable = new TableArrangePreviewItem
                    {
                        TableNumber = tableNumber++,
                        TableName = "主桌",
                        SeatCount = seatsPerTable,
                        Guests = mainGuestList
                    };
                    result.Tables.Add(mainTable);
                    foreach (var g in mainAssigned) MarkGroupAssigned(g);
                }
            }
        }

        // Step 2: 按角色优先级分组编排（领导 > 嘉宾 > 参会代表 > 工作人员 > 媒体 > 其他）
        var remainingPrimary = primaryGuests.Where(a => !assignedIds.Contains(a.FID)).ToList();

        var roleGroups = remainingPrimary
            .GroupBy(a => a.FRole ?? "其他")
            .OrderBy(g =>
            {
                var idx = Array.IndexOf(RolePriority, g.Key);
                return idx >= 0 ? idx : RolePriority.Length;
            })
            .ToList();

        foreach (var roleGroup in roleGroups)
        {
            var groupMembers = roleGroup.ToList();

            // 在同一角色组内，按单位聚集（同单位尽量同桌）
            // 特殊饮食的人排在同单位子组的前面，便于聚集
            var orgSubGroups = groupMembers
                .GroupBy(a => a.FOrganization ?? "")
                .OrderByDescending(g => g.Count())
                .ToList();

            var currentGuests = new List<AssignedGuestItem>();
            var currentAssigned = new List<ConfAttendee>();
            var currentRole = roleGroup.Key;

            foreach (var orgGroup in orgSubGroups)
            {
                // 同单位内：特殊饮食优先排在一起
                var orgMembers = orgGroup
                    .OrderBy(a => (a.FDietPreference != null && a.FDietPreference != "无特殊") ? 0 : 1)
                    .ThenBy(a => a.FDietPreference ?? "")
                    .ToList();

                foreach (var guest in orgMembers)
                {
                    var groupSize = GetGroupSize(guest);
                    if (currentGuests.Count + groupSize > seatsPerTable && currentGuests.Count > 0)
                    {
                        // 当前桌满了，保存并开新桌（紧接当前角色组）
                        var table = new TableArrangePreviewItem
                        {
                            TableNumber = tableNumber++,
                            TableName = $"第{tableNumber - 1}桌",
                            SeatCount = seatsPerTable,
                            Guests = currentGuests
                        };
                        result.Tables.Add(table);
                        foreach (var a in currentAssigned) MarkGroupAssigned(a);
                        currentGuests = new List<AssignedGuestItem>();
                        currentAssigned = new List<ConfAttendee>();
                    }
                    AddGroupToList(currentGuests, guest);
                    currentAssigned.Add(guest);
                }
            }

            // 当前角色组最后一桌
            if (currentGuests.Count > 0)
            {
                var table = new TableArrangePreviewItem
                {
                    TableNumber = tableNumber++,
                    TableName = $"第{tableNumber - 1}桌",
                    SeatCount = seatsPerTable,
                    Guests = currentGuests
                };
                result.Tables.Add(table);
                foreach (var a in currentAssigned) MarkGroupAssigned(a);
            }
        }

        // 统计未安排人员
        var unseated = attendees.Where(a => !assignedIds.Contains(a.FID)).ToList();
        result.UnseatedAttendees = unseated.Select(a => a.FName).ToList();
        result.TotalTables = result.Tables.Count;
        result.TotalPersons = assignedIds.Count;

        // 将预览结果持久化到数据库
        int persistTableNumber = 1;
        foreach (var previewTable in result.Tables)
        {
            var dbTable = new ConfTable
            {
                FMealPlanId = mealId,
                FTableNumber = persistTableNumber++,
                FTableName = previewTable.TableName,
                FSeatCount = previewTable.SeatCount,
                FUpdatedTime = DateTime.Now
            };
            _dbContext.Set<ConfTable>().Add(dbTable);
            await _dbContext.SaveChangesAsync(); // 先保存获取ID

            int seatNumber = 1;
            foreach (var guest in previewTable.Guests)
            {
                var dbSeat = new ConfTableSeat
                {
                    FTableId = dbTable.FID,
                    FAttendeeId = guest.AttendeeId,
                    FSeatNumber = seatNumber++
                };
                _dbContext.Set<ConfTableSeat>().Add(dbSeat);
            }
        }
        await _dbContext.SaveChangesAsync();

        return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"智能编桌失败: {ex.Message}", ex);
        }
    }

    public Task<byte[]> ExportImageAsync(int mealId)
    {
        // TODO: 实现桌次图片导出
        throw new NotImplementedException("桌次图片导出功能待实现");
    }

    public Task<byte[]> ExportPdfAsync(int mealId)
    {
        // TODO: 实现桌次PDF导出
        throw new NotImplementedException("桌次PDF导出功能待实现");
    }

    public async Task<byte[]> ExportExcelAsync(int mealId)
    {
        var tables = await GetTablesAsync(mealId);

        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("桌次编排");

        // 表头样式
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;

        // 写表头（桌号、桌名，宾客姓名列按实际最大人数动态生成）
        var fixedHeaders = new[] { "桌号", "桌名" };
        var hRow = sheet.CreateRow(0);
        for (int i = 0; i < fixedHeaders.Length; i++)
        {
            var cell = hRow.CreateCell(i);
            cell.SetCellValue(fixedHeaders[i]);
            cell.CellStyle = headerStyle;
        }
        sheet.CreateFreezePane(0, 1);

        // 写数据：一行一桌，每个宾客姓名占一个单元格
        int maxColumnCount = fixedHeaders.Length; // 跟踪实际最大列数
        int rowIndex = 1;
        foreach (var table in tables)
        {
            var row = sheet.CreateRow(rowIndex++);
            row.CreateCell(0).SetCellValue(table.TableNumber);
            row.CreateCell(1).SetCellValue(table.TableName ?? "");
            var guestNames = table.Seats
                .Where(s => !string.IsNullOrEmpty(s.AttendeeName))
                .OrderBy(s => s.SeatNumber)
                .Select(s => s.AttendeeName)
                .ToList();
            for (int g = 0; g < guestNames.Count; g++)
            {
                row.CreateCell(fixedHeaders.Length + g).SetCellValue(guestNames[g]);
            }
            var totalCols = fixedHeaders.Length + guestNames.Count;
            if (totalCols > maxColumnCount) maxColumnCount = totalCols;
        }

        // 自动列宽（根据实际最大列数）
        for (int i = 0; i < maxColumnCount; i++)
            sheet.AutoSizeColumn(i);

        using var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        return output.ToArray();
    }

    #region Mapping

    private static TableDto MapToDto(ConfTable entity)
    {
        return new TableDto
        {
            Id = entity.FID,
            MealPlanId = entity.FMealPlanId,
            TableNumber = entity.FTableNumber,
            TableName = entity.FTableName,
            SeatCount = entity.FSeatCount,
            Remark = entity.FRemark,
            UpdatedTime = entity.FUpdatedTime,
            Seats = entity.Seats.Select(s => new TableSeatDto
            {
                Id = s.FID,
                TableId = s.FTableId,
                AttendeeId = s.FAttendeeId,
                AttendeeName = s.Attendee?.FName,
                Organization = s.Attendee?.FOrganization,
                Role = s.Attendee?.FRole,
                SeatNumber = s.FSeatNumber,
                Remark = s.FRemark
            }).OrderBy(s => s.SeatNumber).ToList()
        };
    }

    #endregion
}
