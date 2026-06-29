using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class ChangeLogService : IChangeLogService
{
    private readonly STOTOPDbContext _context;

    public ChangeLogService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task LogChangeAsync(string businessType, long businessId, string businessName,
                                     string operationType, string changeContent, long? operatorId, string? operatorName)
    {
        var log = new SysChangeLog
        {
            FBusinessType = businessType,
            FBusinessId = businessId,
            FBusinessName = businessName,
            FOperationType = operationType,
            FChangeContent = changeContent,
            FOperatorId = operatorId,
            FOperatorName = operatorName,
            FOperationTime = DateTime.Now
        };

        await _context.Set<SysChangeLog>().AddAsync(log);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<ChangeLogDto> Items, int Total)> GetPagedListAsync(ChangeLogQueryRequest request)
    {
        var query = _context.Set<SysChangeLog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
            query = query.Where(l => l.FBusinessType == request.BusinessType);

        if (request.BusinessId.HasValue)
            query = query.Where(l => l.FBusinessId == request.BusinessId.Value);

        if (!string.IsNullOrWhiteSpace(request.OperationType))
            query = query.Where(l => l.FOperationType == request.OperationType);

        if (request.OperatorId.HasValue)
            query = query.Where(l => l.FOperatorId == request.OperatorId.Value);

        if (request.StartTime.HasValue)
            query = query.Where(l => l.FOperationTime >= request.StartTime.Value);

        if (request.EndTime.HasValue)
            query = query.Where(l => l.FOperationTime <= request.EndTime.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.FOperationTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new ChangeLogDto
            {
                Id = l.FID,
                BusinessType = l.FBusinessType,
                BusinessId = l.FBusinessId,
                BusinessName = l.FBusinessName,
                OperationType = l.FOperationType,
                ChangeContent = l.FChangeContent,
                OperatorId = l.FOperatorId,
                OperatorName = l.FOperatorName,
                OperationTime = l.FOperationTime,
                DingTalkSyncStatus = l.FDingTalkSyncStatus,
                DingTalkSyncTime = l.FDingTalkSyncTime,
                DingTalkSyncResult = l.FDingTalkSyncResult,
                Remark = l.FRemark
            })
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<ChangeLogDto>> GetByBusinessAsync(string businessType, long businessId)
    {
        return await _context.Set<SysChangeLog>()
            .Where(l => l.FBusinessType == businessType && l.FBusinessId == businessId)
            .OrderByDescending(l => l.FOperationTime)
            .Select(l => new ChangeLogDto
            {
                Id = l.FID,
                BusinessType = l.FBusinessType,
                BusinessId = l.FBusinessId,
                BusinessName = l.FBusinessName,
                OperationType = l.FOperationType,
                ChangeContent = l.FChangeContent,
                OperatorId = l.FOperatorId,
                OperatorName = l.FOperatorName,
                OperationTime = l.FOperationTime,
                DingTalkSyncStatus = l.FDingTalkSyncStatus,
                DingTalkSyncTime = l.FDingTalkSyncTime,
                DingTalkSyncResult = l.FDingTalkSyncResult,
                Remark = l.FRemark
            })
            .ToListAsync();
    }

    public string CompareAndSerialize<T>(T oldEntity, T newEntity, params string[] excludeProperties)
    {
        var changes = new Dictionary<string, object>();
        var properties = typeof(T).GetProperties()
            .Where(p => !excludeProperties.Contains(p.Name) && p.CanRead);

        foreach (var prop in properties)
        {
            var oldValue = prop.GetValue(oldEntity);
            var newValue = prop.GetValue(newEntity);

            if (!Equals(oldValue, newValue))
            {
                changes[prop.Name] = new
                {
                    旧值 = oldValue?.ToString() ?? "",
                    新值 = newValue?.ToString() ?? ""
                };
            }
        }

        return changes.Count > 0
            ? global::System.Text.Json.JsonSerializer.Serialize(changes, new global::System.Text.Json.JsonSerializerOptions { WriteIndented = false, Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
            : "{}";
    }
}
