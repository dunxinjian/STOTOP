using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class VisitRecordService : IVisitRecordService
{
    private readonly IRepository<CrmVisitRecord> _visitRepository;
    private readonly IRepository<CrmCustomer> _customerRepository;

    public VisitRecordService(
        IRepository<CrmVisitRecord> visitRepository,
        IRepository<CrmCustomer> customerRepository)
    {
        _visitRepository = visitRepository;
        _customerRepository = customerRepository;
    }

    public async Task<PagedResult<VisitRecordListItemDto>> GetVisitRecordsAsync(VisitRecordQueryRequest request)
    {
        IQueryable<CrmVisitRecord> query = _visitRepository.Query().Include(v => v.Customer);

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            query = query.Where(v => v.FCustomerId == request.CustomerId);

        if (request.VisitorId.HasValue)
            query = query.Where(v => v.FVisitorId == request.VisitorId.Value);

        if (request.VisitMethod.HasValue)
            query = query.Where(v => v.FVisitMethod == request.VisitMethod.Value);

        if (request.StartDate.HasValue)
            query = query.Where(v => v.FVisitDate >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(v => v.FVisitDate <= request.EndDate.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.FVisitDate)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<VisitRecordListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<VisitRecordDto?> GetVisitRecordByIdAsync(long id)
    {
        var entity = await _visitRepository.Query()
            .Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<VisitRecordDto> CreateVisitRecordAsync(CreateVisitRecordRequest request)
    {
        var customer = await _customerRepository.Query()
            .FirstOrDefaultAsync(c => c.FCode == request.CustomerId);
        if (customer == null)
            throw new InvalidOperationException("客户不存在");

        var entity = new CrmVisitRecord
        {
            FCustomerId = request.CustomerId,
            FVisitorId = request.VisitorId,
            FVisitDate = request.VisitDate,
            FVisitMethod = request.VisitMethod,
            FContent = request.Content,
            FNextFollowUpDate = request.NextFollowUpDate,
            FCreatedTime = DateTime.Now
        };

        await _visitRepository.AddAsync(entity);
        return (await GetVisitRecordByIdAsync(entity.FID))!;
    }

    public async Task<VisitRecordDto?> UpdateVisitRecordAsync(long id, UpdateVisitRecordRequest request)
    {
        var entity = await _visitRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(v => v.FID == id);

        if (entity == null) return null;

        entity.FVisitDate = request.VisitDate;
        entity.FVisitMethod = request.VisitMethod;
        entity.FContent = request.Content;
        entity.FNextFollowUpDate = request.NextFollowUpDate;
        entity.FUpdatedTime = DateTime.Now;

        await _visitRepository.UpdateAsync(entity);
        return await GetVisitRecordByIdAsync(id);
    }

    public async Task<bool> DeleteVisitRecordAsync(long id)
    {
        var entity = await _visitRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await _visitRepository.DeleteAsync(id);
        return true;
    }

    public async Task<List<VisitRecordListItemDto>> GetPendingFollowUpAsync(long? visitorId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        IQueryable<CrmVisitRecord> query = _visitRepository.Query()
            .Include(v => v.Customer)
            .Where(v => v.FNextFollowUpDate.HasValue && v.FNextFollowUpDate.Value <= today);

        if (visitorId.HasValue)
            query = query.Where(v => v.FVisitorId == visitorId.Value);

        var items = await query
            .OrderBy(v => v.FNextFollowUpDate)
            .ToListAsync();

        return items.Select(MapToListItemDto).ToList();
    }

    public async Task<VisitStatisticsDto> GetStatisticsAsync(long? visitorId = null, long? orgId = null)
    {
        var query = _visitRepository.Query();

        if (visitorId.HasValue)
            query = query.Where(v => v.FVisitorId == visitorId.Value);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var totalVisits = await query.CountAsync();
        var todayVisits = await query.CountAsync(v => v.FVisitDate == today);
        var weekVisits = await query.CountAsync(v => v.FVisitDate >= weekStart);
        var monthVisits = await query.CountAsync(v => v.FVisitDate >= monthStart);
        var pendingFollowUp = await query.CountAsync(v => v.FNextFollowUpDate.HasValue && v.FNextFollowUpDate.Value <= today);

        return new VisitStatisticsDto
        {
            TotalVisits = totalVisits,
            TodayVisits = todayVisits,
            WeekVisits = weekVisits,
            MonthVisits = monthVisits,
            PendingFollowUp = pendingFollowUp
        };
    }

    #region Mapping

    private static VisitRecordDto MapToDto(CrmVisitRecord entity)
    {
        return new VisitRecordDto
        {
            Id = entity.FID,
            CustomerId = entity.FCustomerId,
            CustomerName = entity.Customer?.FShortName,
            VisitorId = entity.FVisitorId,
            VisitDate = entity.FVisitDate,
            VisitMethod = entity.FVisitMethod,
            Content = entity.FContent,
            NextFollowUpDate = entity.FNextFollowUpDate,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static VisitRecordListItemDto MapToListItemDto(CrmVisitRecord entity)
    {
        return new VisitRecordListItemDto
        {
            Id = entity.FID,
            CustomerId = entity.FCustomerId,
            CustomerName = entity.Customer?.FShortName,
            VisitorId = entity.FVisitorId,
            VisitDate = entity.FVisitDate,
            VisitMethod = entity.FVisitMethod,
            NextFollowUpDate = entity.FNextFollowUpDate,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
