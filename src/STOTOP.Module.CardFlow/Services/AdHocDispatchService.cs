using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 自由派发服务：在某张卡片完成后，由用户手动触发关联流程。
/// 与编排引擎相互独立，仅写入 CfDispatchRecord（FOrchestrationInstanceId=null）。
/// </summary>
public class AdHocDispatchService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<AdHocDispatchService> _logger;

    public AdHocDispatchService(STOTOPDbContext db, ILogger<AdHocDispatchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 获取某张卡片完成后可触发的流程列表。
    /// </summary>
    public async Task<List<DispatchOption>> GetAvailableTargetsAsync(long cardId)
    {
        var card = await _db.Set<CfCard>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.FID == cardId);
        if (card == null)
        {
            return new List<DispatchOption>();
        }

        var sourceFlow = await _db.Set<CfFlowDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FID == card.FFlowDefinitionId);
        if (sourceFlow == null)
        {
            return new List<DispatchOption>();
        }

        var configs = await _db.Set<CfAdHocDispatchConfig>()
            .AsNoTracking()
            .Where(c => c.FSourceFlowCode == sourceFlow.FFlowCode
                     && c.FOrgId == card.FOrgId
                     && c.FIsEnabled)
            .ToListAsync();

        return configs.Select(c => new DispatchOption
        {
            TargetFlowCode = c.FTargetFlowCode,
            Name = c.FName,
            DataProtocolJson = c.FDataProtocolJson
        }).ToList();
    }

    /// <summary>
    /// 用户手动触发一个关联流程：基于自由派发配置创建目标卡片并写入派发记录。
    /// </summary>
    public async Task<long> DispatchAsync(long sourceCardId, string targetFlowCode,
                                          long operatorId, JsonElement? customData)
    {
        var sourceCard = await _db.Set<CfCard>()
            .FirstOrDefaultAsync(c => c.FID == sourceCardId)
            ?? throw new InvalidOperationException($"源卡片 {sourceCardId} 不存在");

        var sourceFlow = await _db.Set<CfFlowDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FID == sourceCard.FFlowDefinitionId)
            ?? throw new InvalidOperationException($"源卡片对应的流程定义 {sourceCard.FFlowDefinitionId} 不存在");

        var targetFlow = await _db.Set<CfFlowDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FFlowCode == targetFlowCode && f.FOrgId == sourceCard.FOrgId)
            ?? throw new InvalidOperationException($"目标流程 {targetFlowCode}（组织 {sourceCard.FOrgId}）不存在");

        var config = await _db.Set<CfAdHocDispatchConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.FSourceFlowCode == sourceFlow.FFlowCode
                                   && c.FTargetFlowCode == targetFlowCode
                                   && c.FOrgId == sourceCard.FOrgId
                                   && c.FIsEnabled)
            ?? throw new InvalidOperationException(
                $"自由派发配置不存在：{sourceFlow.FFlowCode} → {targetFlowCode} (org={sourceCard.FOrgId})");

        // 优先使用调用方传入的 customData，否则回退到配置的 DataProtocolJson
        string? payloadJson = null;
        if (customData.HasValue && customData.Value.ValueKind != JsonValueKind.Null
            && customData.Value.ValueKind != JsonValueKind.Undefined)
        {
            payloadJson = customData.Value.GetRawText();
        }
        else if (!string.IsNullOrWhiteSpace(config.FDataProtocolJson))
        {
            payloadJson = config.FDataProtocolJson;
        }

        var currentVersion = await _db.Set<CfFlowVersion>()
            .AsNoTracking()
            .Where(v => v.FFlowDefinitionId == targetFlow.FID && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync();

        var card = new CfCard
        {
            FFlowDefinitionId = targetFlow.FID,
            FFlowVersionId = currentVersion?.FID ?? 0,
            FStatus = "draft",
            FInitiatorId = operatorId,
            FInitiatorName = string.Empty,
            FCreatedTime = DateTime.Now,
            FOrgId = sourceCard.FOrgId,
            FDataJson = payloadJson
        };
        _db.Set<CfCard>().Add(card);
        await _db.SaveChangesAsync();

        _db.Set<CfDispatchRecord>().Add(new CfDispatchRecord
        {
            FOrchestrationInstanceId = null,
            FDispatchType = "manual",
            FSourceCardId = sourceCardId,
            FSourceFlowCode = sourceFlow.FFlowCode,
            FTargetCardId = card.FID,
            FTargetFlowCode = targetFlowCode,
            FDataPayloadJson = payloadJson,
            FStatus = "triggered",
            FOperatorId = operatorId,
            FCreatedTime = DateTime.Now,
            FTriggeredTime = DateTime.Now
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("自由派发：源卡片 {SourceCardId}({SourceFlow}) → 目标卡片 {TargetCardId}({TargetFlow}) by {Operator}",
            sourceCardId, sourceFlow.FFlowCode, card.FID, targetFlowCode, operatorId);

        return card.FID;
    }
}

/// <summary>
/// 自由派发可选目标。
/// </summary>
public class DispatchOption
{
    public string TargetFlowCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DataProtocolJson { get; set; }
}
