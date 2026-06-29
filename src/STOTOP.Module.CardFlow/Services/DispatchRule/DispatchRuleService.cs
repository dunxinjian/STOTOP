using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.DispatchRule;

public class DispatchRuleService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<DispatchRuleService> _logger;

    public DispatchRuleService(STOTOPDbContext context, ILogger<DispatchRuleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>列表查询，支持按状态/处理器类型/暂存表筛选</summary>
    public async Task<List<DispatchRuleResponseDto>> GetAllAsync(
        int? status = null, string? handlerType = null, string? targetTable = null, CancellationToken ct = default)
    {
        var query = _context.Set<CfDispatchRule>().AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.FStatus == status.Value);

        if (!string.IsNullOrWhiteSpace(handlerType))
            query = query.Where(r => r.FHandlerType == handlerType);

        var rules = await query
            .OrderBy(r => r.FPriority)
            .ThenByDescending(r => r.FCreatedTime)
            .ToListAsync(ct);

        // 如果指定了暂存表，在内存中过滤 FTargetTables JSON 数组
        if (!string.IsNullOrWhiteSpace(targetTable))
        {
            rules = rules.Where(r =>
            {
                if (string.IsNullOrWhiteSpace(r.FTargetTables)) return true; // 全局规则
                try
                {
                    var tables = JsonSerializer.Deserialize<List<string>>(r.FTargetTables);
                    return tables != null && tables.Contains(targetTable, StringComparer.OrdinalIgnoreCase);
                }
                catch { return false; }
            }).ToList();
        }

        return rules.Select(MapToDto).ToList();
    }

    /// <summary>详情</summary>
    public async Task<DispatchRuleResponseDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var entity = await _context.Set<CfDispatchRule>().FirstOrDefaultAsync(r => r.FID == id, ct);
        return entity == null ? null : MapToDto(entity);
    }

    /// <summary>创建</summary>
    public async Task<DispatchRuleResponseDto> CreateAsync(DispatchRuleCreateDto dto, CancellationToken ct = default)
    {
        var entity = new CfDispatchRule
        {
            FRuleName = dto.RuleName,
            FTriggerEvent = dto.TriggerEvent,
            FTargetTables = dto.TargetTables != null ? JsonSerializer.Serialize(dto.TargetTables) : null,
            FRuleType = dto.RuleType,
            FConditionJson = dto.Condition != null ? JsonSerializer.Serialize(dto.Condition) : null,
            FSeverity = dto.Severity,
            FHandlerType = dto.HandlerType,
            FHandlerConfigJson = dto.HandlerConfig != null ? JsonSerializer.Serialize(dto.HandlerConfig) : null,
            FIsAsync = dto.IsAsync,
            FPriority = dto.Priority,
            FStatus = 1,
            FDescription = dto.Description,
            FCreatedTime = DateTime.Now
        };

        _context.Set<CfDispatchRule>().Add(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("创建派发规则: {RuleId} ({RuleName})", entity.FID, entity.FRuleName);
        return MapToDto(entity);
    }

    /// <summary>更新</summary>
    public async Task<DispatchRuleResponseDto> UpdateAsync(long id, DispatchRuleUpdateDto dto, CancellationToken ct = default)
    {
        var entity = await _context.Set<CfDispatchRule>().FirstOrDefaultAsync(r => r.FID == id, ct)
            ?? throw new InvalidOperationException("派发规则不存在");

        if (dto.RuleName != null) entity.FRuleName = dto.RuleName;
        if (dto.TriggerEvent != null) entity.FTriggerEvent = dto.TriggerEvent;
        if (dto.TargetTables != null) entity.FTargetTables = JsonSerializer.Serialize(dto.TargetTables);
        if (dto.RuleType != null) entity.FRuleType = dto.RuleType;
        if (dto.Condition != null) entity.FConditionJson = JsonSerializer.Serialize(dto.Condition);
        if (dto.Severity != null) entity.FSeverity = dto.Severity;
        if (dto.HandlerType != null) entity.FHandlerType = dto.HandlerType;
        if (dto.HandlerConfig != null) entity.FHandlerConfigJson = JsonSerializer.Serialize(dto.HandlerConfig);
        if (dto.IsAsync.HasValue) entity.FIsAsync = dto.IsAsync.Value;
        if (dto.Priority.HasValue) entity.FPriority = dto.Priority.Value;
        if (dto.Status.HasValue) entity.FStatus = dto.Status.Value;
        if (dto.Description != null) entity.FDescription = dto.Description;

        entity.FUpdatedTime = DateTime.Now;

        _context.Set<CfDispatchRule>().Update(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("更新派发规则: {RuleId} ({RuleName})", entity.FID, entity.FRuleName);
        return MapToDto(entity);
    }

    /// <summary>删除</summary>
    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await _context.Set<CfDispatchRule>().FirstOrDefaultAsync(r => r.FID == id, ct)
            ?? throw new InvalidOperationException("派发规则不存在");

        _context.Set<CfDispatchRule>().Remove(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("删除派发规则: {RuleId} ({RuleName})", entity.FID, entity.FRuleName);
    }

    /// <summary>获取处理器类型列表</summary>
    public async Task<List<string>> GetHandlerTypesAsync(CancellationToken ct = default)
    {
        return await _context.Set<CfDispatchRule>()
            .Where(r => r.FStatus == 1)
            .Select(r => r.FHandlerType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);
    }

    private static DispatchRuleResponseDto MapToDto(CfDispatchRule entity)
    {
        return new DispatchRuleResponseDto
        {
            Id = entity.FID,
            RuleName = entity.FRuleName,
            TriggerEvent = entity.FTriggerEvent,
            TargetTables = !string.IsNullOrWhiteSpace(entity.FTargetTables)
                ? JsonSerializer.Deserialize<List<string>>(entity.FTargetTables)
                : null,
            RuleType = entity.FRuleType,
            Condition = !string.IsNullOrWhiteSpace(entity.FConditionJson)
                ? JsonSerializer.Deserialize<object>(entity.FConditionJson)
                : null,
            Severity = entity.FSeverity,
            HandlerType = entity.FHandlerType,
            HandlerConfig = !string.IsNullOrWhiteSpace(entity.FHandlerConfigJson)
                ? JsonSerializer.Deserialize<object>(entity.FHandlerConfigJson)
                : null,
            IsAsync = entity.FIsAsync,
            Priority = entity.FPriority,
            Status = entity.FStatus,
            Description = entity.FDescription,
            CreateTime = entity.FCreatedTime,
            UpdateTime = entity.FUpdatedTime
        };
    }
}
