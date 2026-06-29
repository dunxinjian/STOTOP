using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class GiftService : IGiftService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<GiftService> _logger;

    public GiftService(STOTOPDbContext dbContext, ILogger<GiftService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<GiftDto>> GetGiftsAsync(long eventId)
    {
        var gifts = await _dbContext.Set<ConfGift>().AsNoTracking()
            .Include(g => g.Attendee)
            .Where(g => g.FEventId == eventId)
            .OrderByDescending(g => g.FRegistrationTime)
            .ToListAsync();

        return gifts.Select(MapToDto).ToList();
    }

    public async Task<GiftDto?> GetGiftByIdAsync(long id)
    {
        var entity = await _dbContext.Set<ConfGift>().AsNoTracking()
            .Include(g => g.Attendee)
            .FirstOrDefaultAsync(g => g.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<GiftDto> CreateGiftAsync(long eventId, CreateGiftRequest request)
    {
        // 验证：必须提供 AttendeeId 或 GuestName
        if ((request.AttendeeId == null || request.AttendeeId == 0) && string.IsNullOrWhiteSpace(request.GuestName))
            throw new InvalidOperationException("请选择宾客或输入宾客姓名");

        var entity = new ConfGift
        {
            FEventId = eventId,
            FAttendeeId = request.AttendeeId > 0 ? request.AttendeeId : null,
            FGuestName = request.GuestName,
            FAmount = request.Amount,
            FGiftDescription = request.GiftDescription,
            FRegistrationMethod = request.RegistrationMethod,
            FRemark = request.Remark,
            FRegistrationTime = DateTime.Now,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfGift>().Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("创建礼金记录 {GiftId}: 金额={Amount} (活动 {EventId})", entity.FID, entity.FAmount, eventId);

        // Reload with Attendee
        return (await GetGiftByIdAsync(entity.FID))!;
    }

    public async Task<GiftDto?> UpdateGiftAsync(long id, UpdateGiftRequest request)
    {
        var entity = await _dbContext.Set<ConfGift>()
            .AsTracking()
            .FirstOrDefaultAsync(g => g.FID == id);

        if (entity == null) return null;

        if (request.Amount.HasValue) entity.FAmount = request.Amount.Value;
        if (request.GiftDescription != null) entity.FGiftDescription = request.GiftDescription;
        if (request.RegistrationMethod != null) entity.FRegistrationMethod = request.RegistrationMethod;
        if (request.Remark != null) entity.FRemark = request.Remark;
        if (request.ReturnContent != null) entity.FReturnContent = request.ReturnContent;

        if (request.IsReturned.HasValue)
        {
            entity.FIsReturned = request.IsReturned.Value;
            if (request.IsReturned.Value && entity.FReturnTime == null)
            {
                entity.FReturnTime = DateTime.Now;
            }
        }

        entity.FUpdatedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("更新礼金记录 {GiftId}", id);
        return await GetGiftByIdAsync(id);
    }

    public async Task<bool> DeleteGiftAsync(long id)
    {
        var entity = await _dbContext.Set<ConfGift>()
            .AsTracking()
            .FirstOrDefaultAsync(g => g.FID == id);

        if (entity == null) return false;

        _dbContext.Set<ConfGift>().Remove(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("删除礼金记录 {GiftId}", id);
        return true;
    }

    public async Task<GiftSummaryDto> GetSummaryAsync(long eventId)
    {
        var gifts = await _dbContext.Set<ConfGift>().AsNoTracking()
            .Include(g => g.Attendee)
            .Where(g => g.FEventId == eventId)
            .ToListAsync();

        var summary = new GiftSummaryDto
        {
            TotalAmount = gifts.Sum(g => g.FAmount),
            TotalCount = gifts.Count,
            CashCount = gifts.Count(g => g.FRegistrationMethod == "现金"),
            TransferCount = gifts.Count(g => g.FRegistrationMethod == "转账"),
            GiftCount = gifts.Count(g => g.FRegistrationMethod == "礼物"),
            ReturnedCount = gifts.Count(g => g.FIsReturned),
            PendingReturnCount = gifts.Count(g => !g.FIsReturned),
            CampSummaries = gifts
                .GroupBy(g => g.Attendee?.FCamp ?? "未分组")
                .Select(grp => new GiftCampSummary
                {
                    Camp = grp.Key,
                    Count = grp.Count(),
                    TotalAmount = grp.Sum(g => g.FAmount)
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList()
        };

        return summary;
    }

    public async Task<int> BatchRegisterAsync(long eventId, BatchRegisterGiftRequest request)
    {
        var entities = request.Items.Select(item => new ConfGift
        {
            FEventId = eventId,
            FAttendeeId = item.AttendeeId > 0 ? item.AttendeeId : null,
            FGuestName = item.GuestName,
            FAmount = item.Amount,
            FGiftDescription = item.GiftDescription,
            FRegistrationMethod = item.RegistrationMethod,
            FRemark = item.Remark,
            FRegistrationTime = DateTime.Now,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        }).ToList();

        _dbContext.Set<ConfGift>().AddRange(entities);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("批量登记礼金 {Count} 条 (活动 {EventId})", entities.Count, eventId);
        return entities.Count;
    }

    public async Task<byte[]> ExportGiftsAsync(long eventId)
    {
        _logger.LogInformation("导出礼金 Excel (活动 {EventId})", eventId);

        var gifts = await _dbContext.Set<ConfGift>().AsNoTracking()
            .Include(g => g.Attendee)
            .Where(g => g.FEventId == eventId)
            .OrderByDescending(g => g.FRegistrationTime)
            .ToListAsync();

        var headers = new[] { "序号", "宾客姓名", "阵营", "关系", "金额", "登记方式", "登记时间", "回礼状态", "备注" };

        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("礼金登记");

        // 表头样式
        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);
        headerStyle.Alignment = HorizontalAlignment.Center;

        // 写表头
        var hRow = sheet.CreateRow(0);
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = hRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
        }
        sheet.CreateFreezePane(0, 1);

        // 写数据
        for (int i = 0; i < gifts.Count; i++)
        {
            var g = gifts[i];
            var row = sheet.CreateRow(i + 1);
            row.CreateCell(0).SetCellValue(i + 1);
            row.CreateCell(1).SetCellValue(g.Attendee?.FName ?? g.FGuestName ?? "");
            row.CreateCell(2).SetCellValue(g.Attendee?.FCamp ?? "");
            row.CreateCell(3).SetCellValue(g.Attendee?.FRelation ?? "");
            row.CreateCell(4).SetCellValue((double)g.FAmount);
            row.CreateCell(5).SetCellValue(g.FRegistrationMethod);
            row.CreateCell(6).SetCellValue(g.FRegistrationTime.ToString("yyyy-MM-dd HH:mm"));
            row.CreateCell(7).SetCellValue(g.FIsReturned ? "已回礼" : "未回礼");
            row.CreateCell(8).SetCellValue(g.FRemark ?? "");
        }

        // 自动列宽
        for (int i = 0; i < headers.Length; i++)
            sheet.AutoSizeColumn(i);

        using var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        return output.ToArray();
    }

    private static GiftDto MapToDto(ConfGift entity)
    {
        return new GiftDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            AttendeeId = entity.FAttendeeId,
            AttendeeName = entity.Attendee?.FName ?? entity.FGuestName,
            GuestName = entity.FGuestName,
            Camp = entity.Attendee?.FCamp,
            GuestType = entity.Attendee?.FGuestType,
            Amount = entity.FAmount,
            GiftDescription = entity.FGiftDescription,
            RegistrationTime = entity.FRegistrationTime,
            RegistrationMethod = entity.FRegistrationMethod,
            IsReturned = entity.FIsReturned,
            ReturnContent = entity.FReturnContent,
            ReturnTime = entity.FReturnTime,
            Remark = entity.FRemark
        };
    }
}
