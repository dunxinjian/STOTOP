using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class FinanceService : IFinanceService
{
    private readonly STOTOPDbContext _dbContext;

    public FinanceService(STOTOPDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<IncomeListItemDto>> GetIncomesAsync(int eventId, IncomeQueryRequest request)
    {
        var query = _dbContext.Set<ConfIncome>()
            .Where(i => i.FEventId == eventId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(i =>
                (i.FPayerName != null && i.FPayerName.Contains(keyword)) ||
                (i.FPayerOrganization != null && i.FPayerOrganization.Contains(keyword)) ||
                (i.FReceiptNumber != null && i.FReceiptNumber.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(i => i.FType == request.Type);

        if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
            query = query.Where(i => i.FPaymentMethod == request.PaymentMethod);

        if (request.StartDate.HasValue)
            query = query.Where(i => i.FPaymentDate >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(i => i.FPaymentDate <= request.EndDate.Value);

        var total = await query.CountAsync();

        var items = await query
            .Include(i => i.Attendee)
            .OrderByDescending(i => i.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<IncomeListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<IncomeDto?> GetIncomeByIdAsync(int id)
    {
        var income = await _dbContext.Set<ConfIncome>()
            .Include(i => i.Attendee)
            .FirstOrDefaultAsync(i => i.FID == id);

        return income == null ? null : MapToDto(income);
    }

    public async Task<IncomeDto> CreateIncomeAsync(int eventId, CreateIncomeRequest request)
    {
        var income = new ConfIncome
        {
            FEventId = eventId,
            FAttendeeId = request.AttendeeId,
            FType = request.Type,
            FAmount = request.Amount,
            FPaymentMethod = request.PaymentMethod,
            FPayerName = request.PayerName,
            FPayerOrganization = request.PayerOrganization,
            FPaymentDate = request.PaymentDate,
            FReceiptNumber = request.ReceiptNumber,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfIncome>().Add(income);
        await _dbContext.SaveChangesAsync();

        return (await GetIncomeByIdAsync((int)income.FID))!;
    }

    public async Task<IncomeDto?> UpdateIncomeAsync(int id, UpdateIncomeRequest request)
    {
        var income = await _dbContext.Set<ConfIncome>()
            .AsTracking()
            .FirstOrDefaultAsync(i => i.FID == id);

        if (income == null) return null;

        income.FAttendeeId = request.AttendeeId;
        income.FType = request.Type;
        income.FAmount = request.Amount;
        income.FPaymentMethod = request.PaymentMethod;
        income.FPayerName = request.PayerName;
        income.FPayerOrganization = request.PayerOrganization;
        income.FPaymentDate = request.PaymentDate;
        income.FReceiptNumber = request.ReceiptNumber;
        income.FRemark = request.Remark;
        income.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        return await GetIncomeByIdAsync(id);
    }

    public async Task<bool> DeleteIncomeAsync(int id)
    {
        var income = await _dbContext.Set<ConfIncome>()
            .AsTracking()
            .FirstOrDefaultAsync(i => i.FID == id);

        if (income == null) return false;

        _dbContext.Set<ConfIncome>().Remove(income);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<IncomeSummaryDto> GetSummaryAsync(int eventId)
    {
        var incomes = await _dbContext.Set<ConfIncome>()
            .Where(i => i.FEventId == eventId)
            .ToListAsync();

        var summary = new IncomeSummaryDto
        {
            TotalAmount = incomes.Sum(i => i.FAmount),
            TotalCount = incomes.Count,
            TypeSummaries = incomes
                .GroupBy(i => i.FType ?? "未分类")
                .Select(g => new IncomeTypeSummary
                {
                    Type = g.Key,
                    Amount = g.Sum(i => i.FAmount),
                    Count = g.Count()
                }).ToList()
        };

        return summary;
    }

    public Task<byte[]> ExportIncomesAsync(int eventId)
    {
        // TODO: 实现收入导出
        throw new NotImplementedException("收入导出功能待实现");
    }

    public async Task<int> BatchRegisterAsync(int eventId, BatchRegisterIncomeRequest request)
    {
        // 获取参会人信息，用于自动填充姓名和单位
        var attendees = await _dbContext.Set<ConfAttendee>()
            .Where(a => request.AttendeeIds.Contains(a.FID) && a.FEventId == eventId)
            .ToListAsync();

        var count = 0;
        var now = DateTime.Now;

        foreach (var attendee in attendees)
        {
            var income = new ConfIncome
            {
                FEventId = eventId,
                FAttendeeId = attendee.FID,
                FType = request.Type,
                FAmount = request.Amount,
                FPaymentMethod = request.PaymentMethod,
                FPayerName = attendee.FName,
                FPayerOrganization = attendee.FOrganization,
                FPaymentDate = request.PaymentDate,
                FRemark = request.Remark,
                FCreatedTime = now,
                FUpdatedTime = now
            };

            _dbContext.Set<ConfIncome>().Add(income);
            count++;
        }

        if (count > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return count;
    }

    #region Mapping

    private static IncomeDto MapToDto(ConfIncome entity)
    {
        return new IncomeDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            AttendeeId = entity.FAttendeeId,
            AttendeeName = entity.Attendee?.FName,
            Type = entity.FType,
            Amount = entity.FAmount,
            PaymentMethod = entity.FPaymentMethod,
            PayerName = entity.FPayerName,
            PayerOrganization = entity.FPayerOrganization,
            PaymentDate = entity.FPaymentDate,
            ReceiptNumber = entity.FReceiptNumber,
            Remark = entity.FRemark,
            Registrant = entity.FRegistrant,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    private static IncomeListItemDto MapToListItemDto(ConfIncome entity)
    {
        return new IncomeListItemDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            AttendeeId = entity.FAttendeeId,
            AttendeeName = entity.Attendee?.FName,
            Type = entity.FType,
            Amount = entity.FAmount,
            PaymentMethod = entity.FPaymentMethod,
            PayerName = entity.FPayerName,
            PaymentDate = entity.FPaymentDate,
            ReceiptNumber = entity.FReceiptNumber,
            Registrant = entity.FRegistrant
        };
    }

    #endregion
}
