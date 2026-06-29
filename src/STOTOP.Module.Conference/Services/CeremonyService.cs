using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Entities;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference.Services;

public class CeremonyService : ICeremonyService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<CeremonyService> _logger;

    public CeremonyService(STOTOPDbContext dbContext, ILogger<CeremonyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<CeremonyItemDto>> GetItemsAsync(long eventId)
    {
        var items = await _dbContext.Set<ConfCeremonyItem>().AsNoTracking()
            .Where(c => c.FEventId == eventId)
            .OrderBy(c => c.FSort)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<CeremonyItemDto?> GetItemByIdAsync(long id)
    {
        var entity = await _dbContext.Set<ConfCeremonyItem>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<CeremonyItemDto> CreateItemAsync(long eventId, CreateCeremonyItemRequest request)
    {
        // Sort 默认取最大值+1
        var maxSort = await _dbContext.Set<ConfCeremonyItem>()
            .Where(c => c.FEventId == eventId)
            .MaxAsync(c => (int?)c.FSort) ?? 0;

        var entity = new ConfCeremonyItem
        {
            FEventId = eventId,
            FName = request.Name,
            FStartTime = request.StartTime,
            FDuration = request.Duration,
            FResponsible = request.Responsible,
            FMusic = request.Music,
            FLighting = request.Lighting,
            FProps = request.Props,
            FRemark = request.Remark,
            FSort = request.Sort > 0 ? request.Sort : maxSort + 1,
            FPhase = request.Phase,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _dbContext.Set<ConfCeremonyItem>().Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("创建仪式环节 {ItemId}: {Name} (活动 {EventId})", entity.FID, entity.FName, eventId);
        return MapToDto(entity);
    }

    public async Task<CeremonyItemDto?> UpdateItemAsync(long id, UpdateCeremonyItemRequest request)
    {
        var entity = await _dbContext.Set<ConfCeremonyItem>()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return null;

        if (request.Name != null) entity.FName = request.Name;
        if (request.StartTime != null) entity.FStartTime = request.StartTime;
        if (request.Duration.HasValue) entity.FDuration = request.Duration.Value;
        if (request.Responsible != null) entity.FResponsible = request.Responsible;
        if (request.Music != null) entity.FMusic = request.Music;
        if (request.Lighting != null) entity.FLighting = request.Lighting;
        if (request.Props != null) entity.FProps = request.Props;
        if (request.Remark != null) entity.FRemark = request.Remark;
        if (request.Sort.HasValue) entity.FSort = request.Sort.Value;
        if (request.Phase != null) entity.FPhase = request.Phase;

        entity.FUpdatedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("更新仪式环节 {ItemId}: {Name}", id, entity.FName);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteItemAsync(long id)
    {
        var entity = await _dbContext.Set<ConfCeremonyItem>()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.FID == id);

        if (entity == null) return false;

        _dbContext.Set<ConfCeremonyItem>().Remove(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("删除仪式环节 {ItemId}", id);
        return true;
    }

    public async Task<bool> ReorderAsync(long eventId, ReorderCeremonyRequest request)
    {
        var items = await _dbContext.Set<ConfCeremonyItem>()
            .AsTracking()
            .Where(c => c.FEventId == eventId)
            .ToListAsync();

        for (int i = 0; i < request.ItemIds.Count; i++)
        {
            var item = items.FirstOrDefault(c => c.FID == request.ItemIds[i]);
            if (item != null)
            {
                item.FSort = i + 1;
                item.FUpdatedTime = DateTime.Now;
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("重排序仪式环节 (活动 {EventId}), {Count} 项", eventId, request.ItemIds.Count);
        return true;
    }

    public async Task<string> ExportRundownAsync(long eventId)
    {
        var items = await _dbContext.Set<ConfCeremonyItem>().AsNoTracking()
            .Where(c => c.FEventId == eventId)
            .OrderBy(c => c.FSort)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("📋 【婚礼仪式流程】");
        sb.AppendLine("━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        // 按阶段分组
        var phases = items.GroupBy(c => c.FPhase).OrderBy(g =>
        {
            return g.Key switch
            {
                "迎宾" => 0,
                "仪式" => 1,
                "宴席" => 2,
                "送客" => 3,
                _ => 99
            };
        });

        foreach (var phase in phases)
        {
            sb.AppendLine($"── {phase.Key}阶段 ──");

            foreach (var item in phase.OrderBy(c => c.FSort))
            {
                var parts = new List<string>();
                parts.Add($"{item.FStartTime} │ {item.FName}");

                if (!string.IsNullOrWhiteSpace(item.FResponsible))
                    parts.Add($"负责人：{item.FResponsible}");
                if (!string.IsNullOrWhiteSpace(item.FMusic))
                    parts.Add($"🎵 {item.FMusic}");
                if (!string.IsNullOrWhiteSpace(item.FLighting))
                    parts.Add($"💡 {item.FLighting}");

                sb.AppendLine(string.Join(" │ ", parts));
            }

            sb.AppendLine();
        }

        sb.AppendLine($"共 {items.Count} 个环节");

        return sb.ToString();
    }

    private static CeremonyItemDto MapToDto(ConfCeremonyItem entity)
    {
        return new CeremonyItemDto
        {
            Id = entity.FID,
            EventId = entity.FEventId,
            Name = entity.FName,
            StartTime = entity.FStartTime,
            Duration = entity.FDuration,
            Responsible = entity.FResponsible,
            Music = entity.FMusic,
            Lighting = entity.FLighting,
            Props = entity.FProps,
            Remark = entity.FRemark,
            Sort = entity.FSort,
            Phase = entity.FPhase
        };
    }
}
