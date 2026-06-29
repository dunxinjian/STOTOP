using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class FlowGroupService : IFlowGroupService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly ILogger<FlowGroupService> _logger;

    public FlowGroupService(STOTOPDbContext dbContext, IConditionEvaluator conditionEvaluator, ILogger<FlowGroupService> logger)
    {
        _dbContext = dbContext;
        _conditionEvaluator = conditionEvaluator;
        _logger = logger;
    }

    public async Task<List<FlowGroupDto>> GetListAsync(long orgId)
    {
        var groups = await _dbContext.Set<CfFlowGroup>()
            .Where(g => g.FOrgId == orgId)
            .OrderBy(g => g.FGroupName)
            .ToListAsync();

        var groupIds = groups.Select(g => g.FID).ToList();

        var flowCounts = await _dbContext.Set<CfFlowDefinition>()
            .Where(f => f.FFlowGroupId != null && groupIds.Contains(f.FFlowGroupId.Value))
            .GroupBy(f => f.FFlowGroupId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync();

        var linkCounts = await _dbContext.Set<CfFlowGroupLink>()
            .Where(l => groupIds.Contains(l.FFlowGroupId))
            .GroupBy(l => l.FFlowGroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync();

        return groups.Select(g => new FlowGroupDto
        {
            Id = g.FID,
            GroupName = g.FGroupName,
            GroupCode = g.FGroupCode,
            Description = g.FDescription,
            Status = g.FStatus,
            FlowCount = flowCounts.FirstOrDefault(x => x.GroupId == g.FID)?.Count ?? 0,
            LinkCount = linkCounts.FirstOrDefault(x => x.GroupId == g.FID)?.Count ?? 0
        }).ToList();
    }

    public async Task<FlowGroupDto> CreateAsync(CreateFlowGroupRequest request, long operatorId)
    {
        var entity = new CfFlowGroup
        {
            FGroupName = request.GroupName,
            FGroupCode = request.GroupCode,
            FDescription = request.Description,
            FStatus = "active",
            FOrgId = request.OrgId,
            FCreatorId = operatorId,
            FCreatedTime = DateTime.Now
        };

        _dbContext.Set<CfFlowGroup>().Add(entity);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Created flow group {Id} by operator {OperatorId}", entity.FID, operatorId);

        return new FlowGroupDto
        {
            Id = entity.FID,
            GroupName = entity.FGroupName,
            GroupCode = entity.FGroupCode,
            Description = entity.FDescription,
            Status = entity.FStatus,
            FlowCount = 0,
            LinkCount = 0
        };
    }

    public async Task<FlowGroupDto> UpdateAsync(long id, UpdateFlowGroupRequest request, long operatorId)
    {
        var entity = await _dbContext.Set<CfFlowGroup>().FirstOrDefaultAsync(g => g.FID == id)
            ?? throw new InvalidOperationException("流程组不存在");

        if (!string.IsNullOrEmpty(request.GroupName))
            entity.FGroupName = request.GroupName;
        if (request.Description != null)
            entity.FDescription = request.Description;
        if (!string.IsNullOrEmpty(request.Status))
            entity.FStatus = request.Status;

        entity.FUpdatedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        var flowCount = await _dbContext.Set<CfFlowDefinition>()
            .CountAsync(f => f.FFlowGroupId == id);
        var linkCount = await _dbContext.Set<CfFlowGroupLink>()
            .CountAsync(l => l.FFlowGroupId == id);

        return new FlowGroupDto
        {
            Id = entity.FID,
            GroupName = entity.FGroupName,
            GroupCode = entity.FGroupCode,
            Description = entity.FDescription,
            Status = entity.FStatus,
            FlowCount = flowCount,
            LinkCount = linkCount
        };
    }

    public async Task DeleteAsync(long id, long operatorId)
    {
        var entity = await _dbContext.Set<CfFlowGroup>().FirstOrDefaultAsync(g => g.FID == id)
            ?? throw new InvalidOperationException("流程组不存在");

        if (entity.FStatus != "archived")
            throw new InvalidOperationException("只能删除已归档的流程组");

        // 删除关联的链接
        var links = await _dbContext.Set<CfFlowGroupLink>()
            .Where(l => l.FFlowGroupId == id)
            .ToListAsync();
        _dbContext.Set<CfFlowGroupLink>().RemoveRange(links);

        _dbContext.Set<CfFlowGroup>().Remove(entity);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Deleted flow group {Id} by operator {OperatorId}", id, operatorId);
    }

    public async Task<List<FlowGroupLinkDto>> GetLinksAsync(long groupId)
    {
        var links = await _dbContext.Set<CfFlowGroupLink>()
            .Where(l => l.FFlowGroupId == groupId)
            .OrderBy(l => l.FSortOrder)
            .ToListAsync();

        var flowIds = links.SelectMany(l => new[] { l.FSourceFlowId, l.FTargetFlowId }).Distinct().ToList();
        var flows = await _dbContext.Set<CfFlowDefinition>()
            .Where(f => flowIds.Contains(f.FID))
            .ToDictionaryAsync(f => f.FID, f => f.FFlowName);

        return links.Select(l => new FlowGroupLinkDto
        {
            Id = l.FID,
            SourceFlowId = l.FSourceFlowId,
            SourceFlowName = flows.GetValueOrDefault(l.FSourceFlowId, string.Empty),
            TargetFlowId = l.FTargetFlowId,
            TargetFlowName = flows.GetValueOrDefault(l.FTargetFlowId, string.Empty),
            TriggerCondition = l.FTriggerCondition,
            FieldMappingJson = l.FFieldMappingJson,
            TriggerMode = l.FTriggerMode,
            SortOrder = l.FSortOrder
        }).ToList();
    }

    public async Task SaveLinksAsync(long groupId, List<SaveFlowGroupLinkRequest> links, long operatorId)
    {
        // 全量覆盖：先删后建
        var existingLinks = await _dbContext.Set<CfFlowGroupLink>()
            .Where(l => l.FFlowGroupId == groupId)
            .ToListAsync();
        _dbContext.Set<CfFlowGroupLink>().RemoveRange(existingLinks);

        foreach (var linkReq in links)
        {
            var link = new CfFlowGroupLink
            {
                FFlowGroupId = groupId,
                FSourceFlowId = linkReq.SourceFlowId,
                FTargetFlowId = linkReq.TargetFlowId,
                FTriggerCondition = linkReq.TriggerCondition,
                FFieldMappingJson = linkReq.FieldMappingJson,
                FTriggerMode = linkReq.TriggerMode,
                FSortOrder = linkReq.SortOrder,
                FCreatedTime = DateTime.Now
            };
            _dbContext.Set<CfFlowGroupLink>().Add(link);
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Saved {Count} links for group {GroupId} by operator {OperatorId}", links.Count, groupId, operatorId);
    }

    public async Task EvaluateTriggersAsync(long cardId)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
        if (card == null) return;

        var flowDef = await _dbContext.Set<CfFlowDefinition>().FirstOrDefaultAsync(f => f.FID == card.FFlowDefinitionId);
        if (flowDef?.FFlowGroupId == null) return;

        var links = await _dbContext.Set<CfFlowGroupLink>()
            .Where(l => l.FFlowGroupId == flowDef.FFlowGroupId.Value && l.FSourceFlowId == card.FFlowDefinitionId)
            .OrderBy(l => l.FSortOrder)
            .ToListAsync();

        if (!links.Any()) return;

        var cardData = !string.IsNullOrEmpty(card.FDataJson)
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(card.FDataJson) ?? new()
            : new Dictionary<string, object?>();

        foreach (var link in links)
        {
            if (string.IsNullOrEmpty(link.FTriggerCondition))
            {
                _logger.LogInformation("Trigger link {LinkId} activated for card {CardId} (no condition)", link.FID, cardId);
                continue;
            }

            var schemaFields = cardData.Keys
                .Select(k => new SchemaFieldDefinition { Key = k, Label = k, Type = "text", Required = false, Readonly = false })
                .ToList();

            var result = _conditionEvaluator.Evaluate(link.FTriggerCondition, cardData, schemaFields);
            if (result)
            {
                _logger.LogInformation("Trigger link {LinkId} condition met for card {CardId}", link.FID, cardId);
            }
        }
    }
}
