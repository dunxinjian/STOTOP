using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class FlowDefinitionService : IFlowDefinitionService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<FlowDefinitionService> _logger;

    public FlowDefinitionService(STOTOPDbContext dbContext, ILogger<FlowDefinitionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedResult<FlowDefinitionDto>> GetListAsync(FlowDefinitionQueryRequest request)
    {
        var query = _dbContext.Set<CfFlowDefinition>().AsQueryable();

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(x => x.FStatus == request.Status);
        if (request.OrgId.HasValue)
            query = query.Where(x => x.FOrgId == request.OrgId.Value);
        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(x => x.FFlowName.Contains(request.Keyword) || x.FFlowCode.Contains(request.Keyword));

        var totalCount = await query.CountAsync();
        var pagedDefinitions = await query
            .OrderByDescending(x => x.FCreatedTime)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var definitionIds = pagedDefinitions.Select(x => x.FID).ToList();
        var versionInfos = await _dbContext.Set<CfFlowVersion>()
            .Where(v => definitionIds.Contains(v.FFlowDefinitionId) && v.FPublishTime != null)
            .GroupBy(v => v.FFlowDefinitionId)
            .Select(g => new
            {
                FlowDefinitionId = g.Key,
                CurrentVersion = g.Max(v => v.FVersionNumber),
                LastPublishedTime = g.Max(v => v.FPublishTime)
            })
            .ToListAsync();
        var versionMap = versionInfos.ToDictionary(v => v.FlowDefinitionId);

        // 查询草稿版本信息
        var draftInfos = await _dbContext.Set<CfFlowVersion>()
            .Where(v => definitionIds.Contains(v.FFlowDefinitionId) && v.FStatus == "draft")
            .Select(v => new { v.FFlowDefinitionId, v.FVersionNumber })
            .ToListAsync();
        var draftMap = draftInfos.ToDictionary(d => d.FFlowDefinitionId);

        var items = pagedDefinitions.Select(x =>
        {
            versionMap.TryGetValue(x.FID, out var ver);
            var hasDraft = draftMap.TryGetValue(x.FID, out var draftInfo);
            return new FlowDefinitionDto
            {
                Id = x.FID,
                FlowName = x.FFlowName,
                FlowCode = x.FFlowCode,
                Description = x.FDescription,
                Status = x.FStatus,
                NumberTemplate = x.FNumberTemplate,
                TitleTemplate = x.FTitleTemplate,
                AllowedRolesJson = x.FAllowedRolesJson,
                FlowGroupId = x.FFlowGroupId,
                OrgId = x.FOrgId,
                CreatedTime = x.FCreatedTime,
                CurrentVersion = ver?.CurrentVersion,
                LastPublishedTime = ver?.LastPublishedTime,
                HasDraft = hasDraft,
                DraftVersion = draftInfo?.FVersionNumber
            };
        }).ToList();

        return new PagedResult<FlowDefinitionDto>
        {
            Items = items,
            Total = totalCount,
            PageIndex = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<FlowDefinitionDto?> GetByIdAsync(long id)
    {
        var entity = await _dbContext.Set<CfFlowDefinition>()
            .FirstOrDefaultAsync(x => x.FID == id);
        if (entity == null) return null;

        // 查询当前已发布版本号
        var publishedVersion = await _dbContext.Set<CfFlowVersion>()
            .Where(v => v.FFlowDefinitionId == id && v.FPublishTime != null)
            .OrderByDescending(v => v.FVersionNumber)
            .Select(v => (int?)v.FVersionNumber)
            .FirstOrDefaultAsync();

        return new FlowDefinitionDto
        {
            Id = entity.FID,
            FlowName = entity.FFlowName,
            FlowCode = entity.FFlowCode,
            Description = entity.FDescription,
            Status = entity.FStatus,
            NumberTemplate = entity.FNumberTemplate,
            TitleTemplate = entity.FTitleTemplate,
            AllowedRolesJson = entity.FAllowedRolesJson,
            FlowGroupId = entity.FFlowGroupId,
            OrgId = entity.FOrgId,
            CreatedTime = entity.FCreatedTime,
            TriggerConfigJson = entity.FTriggerConfigJson,
            AccountSetId = entity.FAccountSetId,
            CurrentVersion = publishedVersion
        };
    }

    public async Task<FlowDefinitionDto> CreateAsync(CreateFlowDefinitionRequest request, long operatorId)
    {
        var entity = new CfFlowDefinition
        {
            FFlowName = request.FlowName,
            FFlowCode = request.FlowCode,
            FDescription = request.Description,
            FStatus = "draft",
            FNumberTemplate = request.NumberTemplate,
            FTitleTemplate = request.TitleTemplate,
            FAllowedRolesJson = request.AllowedRolesJson,
            FFlowGroupId = request.FlowGroupId,
            FOrgId = 0, // 让 DbContext 的 FillOrgIdForNewEntities 自动填充当前组织
            FCreatorId = operatorId,
            FCreatedTime = DateTime.Now
        };

        _dbContext.Set<CfFlowDefinition>().Add(entity);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Created flow definition {Id} by operator {OperatorId}", entity.FID, operatorId);

        return new FlowDefinitionDto
        {
            Id = entity.FID,
            FlowName = entity.FFlowName,
            FlowCode = entity.FFlowCode,
            Description = entity.FDescription,
            Status = entity.FStatus,
            NumberTemplate = entity.FNumberTemplate,
            TitleTemplate = entity.FTitleTemplate,
            AllowedRolesJson = entity.FAllowedRolesJson,
            FlowGroupId = entity.FFlowGroupId,
            OrgId = entity.FOrgId,
            CreatedTime = entity.FCreatedTime
        };
    }

    public async Task<FlowDefinitionDto> UpdateAsync(long id, UpdateFlowDefinitionRequest request, long operatorId)
    {
        var entity = await _dbContext.Set<CfFlowDefinition>().FirstOrDefaultAsync(x => x.FID == id)
            ?? throw new InvalidOperationException("流程定义不存在");

        if (!string.IsNullOrEmpty(request.FlowName))
            entity.FFlowName = request.FlowName;
        if (request.Description != null)
            entity.FDescription = request.Description;
        if (request.NumberTemplate != null)
            entity.FNumberTemplate = request.NumberTemplate;
        if (request.TitleTemplate != null)
            entity.FTitleTemplate = request.TitleTemplate;
        if (request.AllowedRolesJson != null)
            entity.FAllowedRolesJson = request.AllowedRolesJson;
        if (request.FlowGroupId.HasValue)
            entity.FFlowGroupId = request.FlowGroupId;

        entity.FUpdatedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        return new FlowDefinitionDto
        {
            Id = entity.FID,
            FlowName = entity.FFlowName,
            FlowCode = entity.FFlowCode,
            Description = entity.FDescription,
            Status = entity.FStatus,
            NumberTemplate = entity.FNumberTemplate,
            TitleTemplate = entity.FTitleTemplate,
            AllowedRolesJson = entity.FAllowedRolesJson,
            FlowGroupId = entity.FFlowGroupId,
            OrgId = entity.FOrgId,
            CreatedTime = entity.FCreatedTime
        };
    }

    public async Task PublishAsync(long id, long operatorId)
    {
        var entity = await _dbContext.Set<CfFlowDefinition>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.FID == id)
            ?? throw new InvalidOperationException("流程定义不存在");

        // 查找draft版本
        var draftVersion = await _dbContext.Set<CfFlowVersion>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.FFlowDefinitionId == id && x.FStatus == "draft")
            ?? throw new InvalidOperationException("没有可发布的草稿版本");

        // 取消当前版本标记
        var currentVersions = await _dbContext.Set<CfFlowVersion>()
            .AsTracking()
            .Where(x => x.FFlowDefinitionId == id && x.FIsCurrentVersion)
            .ToListAsync();
        foreach (var v in currentVersions)
            v.FIsCurrentVersion = false;

        // ═══ FanOut 末位约束校验 ═══
        var draftStages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == draftVersion.FID)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync();
        await ValidateRouteRulesAsync(draftVersion.FID, draftStages);
        await ValidateDynamicPoliciesAsync(draftVersion.FID, draftStages);

        // 查找 FanOut 类型节点
        var fanOutRegistryId = await _dbContext.Set<CfAutoPluginRegistry>()
            .Where(r => r.F插件编码 == "FanOut" && r.F状态 == 1)
            .Select(r => r.FID)
            .FirstOrDefaultAsync();

        if (fanOutRegistryId > 0)
        {
            var fanOutStages = draftStages
                .Where(s => s.F插件注册ID == fanOutRegistryId)
                .ToList();

            if (fanOutStages.Count > 0)
            {
                // 找到所有批次级节点
                var batchStages = draftStages
                    .Where(s => string.Equals(s.F处理粒度, "batch", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.FSortOrder)
                    .ToList();

                var lastBatchStage = batchStages.LastOrDefault();
                foreach (var fs in fanOutStages)
                {
                    if (lastBatchStage == null || fs.FID != lastBatchStage.FID)
                    {
                        throw new InvalidOperationException("卡片展开节点必须为批次级节点链的最后一个节点");
                    }
                }
            }
        }

        // 发布草稿版本
        draftVersion.FStatus = "published";
        draftVersion.FPublishTime = DateTime.Now;
        draftVersion.FIsCurrentVersion = true;

        // 更新定义状态
        entity.FStatus = "published";
        entity.FUpdatedTime = DateTime.Now;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Published flow definition {Id}, version {VersionId} by operator {OperatorId}", id, draftVersion.FID, operatorId);
    }

    public async Task ArchiveAsync(long id, long operatorId)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var entity = await _dbContext.Set<CfFlowDefinition>().AsTracking().FirstOrDefaultAsync(x => x.FID == id)
            ?? throw new InvalidOperationException("流程定义不存在");

        entity.FStatus = "archived";
        entity.FUpdatedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Archived flow definition {Id} by operator {OperatorId}", id, operatorId);
    }

    public async Task DisableAsync(long id, long operatorId)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var entity = await _dbContext.Set<CfFlowDefinition>().AsTracking().FirstOrDefaultAsync(x => x.FID == id)
            ?? throw new InvalidOperationException("流程定义不存在");

        if (entity.FStatus != "published")
            throw new InvalidOperationException("只有已发布的流程才能停用");

        entity.FStatus = "disabled";
        entity.FUpdatedTime = DateTime.Now;
        var affected = await _dbContext.SaveChangesAsync();
        if (affected == 0)
            throw new InvalidOperationException("停用失败：数据库未更新，可能存在并发冲突");
        _logger.LogInformation("Disabled flow definition {Id}, affected {Affected} rows, by operator {OperatorId}", id, affected, operatorId);
    }

    public async Task EnableAsync(long id, long operatorId)
    {
        // 注意：由于全局配置了 NoTracking，必须使用 AsTracking() 才能正确更新
        var entity = await _dbContext.Set<CfFlowDefinition>().AsTracking().FirstOrDefaultAsync(x => x.FID == id)
            ?? throw new InvalidOperationException("流程定义不存在");

        if (entity.FStatus != "disabled")
            throw new InvalidOperationException("只有已停用的流程才能启用");

        entity.FStatus = "published";
        entity.FUpdatedTime = DateTime.Now;
        var affected = await _dbContext.SaveChangesAsync();
        if (affected == 0)
            throw new InvalidOperationException("启用失败：数据库未更新，可能存在并发冲突");
        _logger.LogInformation("Enabled flow definition {Id}, affected {Affected} rows, by operator {OperatorId}", id, affected, operatorId);
    }

    public async Task<List<FlowVersionDto>> GetVersionsAsync(long definitionId)
    {
        return await _dbContext.Set<CfFlowVersion>()
            .Where(x => x.FFlowDefinitionId == definitionId)
            .OrderByDescending(x => x.FVersionNumber)
            .Select(x => new FlowVersionDto
            {
                Id = x.FID,
                VersionNumber = x.FVersionNumber,
                Status = x.FStatus,
                IsCurrentVersion = x.FIsCurrentVersion,
                CreatedTime = x.FCreatedTime,
                PublishTime = x.FPublishTime
            })
            .ToListAsync();
    }

    public async Task<FlowVersionDetailDto?> GetVersionDetailAsync(long definitionId, long versionId)
    {
        var version = await _dbContext.Set<CfFlowVersion>()
            .Where(x => x.FID == versionId && x.FFlowDefinitionId == definitionId)
            .Select(x => new FlowVersionDetailDto
            {
                Id = x.FID,
                VersionNumber = x.FVersionNumber,
                Status = x.FStatus,
                IsCurrentVersion = x.FIsCurrentVersion,
                CreatedTime = x.FCreatedTime,
                PublishTime = x.FPublishTime,
                CardSchemaJson = x.FCardSchemaJson,
                DetailSchemaJson = x.FDetailSchemaJson,
                FlowSettingsJson = x.FFlowSettingsJson
            })
            .FirstOrDefaultAsync();

        if (version == null) return null;

        version.Stages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == versionId)
            .OrderBy(s => s.FSortOrder)
            .Select(s => new StageDefinitionDto
            {
                Id = s.FID,
                StageKey = s.FStageKey,
                SortOrder = s.FSortOrder,
                StageName = s.FStageName,
                Type = s.FType,
                ApprovalMode = s.FApprovalMode,
                AssigneeStrategy = s.FAssigneeStrategy,
                AssigneeConfigJson = s.FAssigneeConfigJson,
                ConditionJson = s.FConditionJson,
                InputFieldsJson = s.FInputFieldsJson,
                ProcessingGranularity = s.F处理粒度,
                PluginRegistryId = s.F插件注册ID,
                PluginRuleId = s.F插件规则ID,
                FailurePolicyJson = s.FFailurePolicyJson,
                CcConfigJson = s.FCcConfigJson,
                TimeoutHours = s.FTimeoutHours,
                PriorityTemplate = s.FPriorityTemplate
            })
            .ToListAsync();

        version.Routes = await _dbContext.Set<CfStageRouteRule>()
            .Where(r => r.FFlowVersionId == versionId)
            .OrderBy(r => r.FFromStageKey)
            .ThenBy(r => r.FPriority)
            .ThenBy(r => r.FID)
            .Select(r => new StageRouteRuleDto
            {
                Id = r.FID,
                EdgeKey = r.FEdgeKey,
                FromStageKey = r.FFromStageKey,
                ToStageKey = r.FToStageKey,
                RouteName = r.FRouteName,
                ConditionJson = r.FConditionJson,
                Priority = r.FPriority,
                IsDefault = r.FIsDefault,
                Status = r.FStatus,
                FailurePolicyJson = r.FFailurePolicyJson
            })
            .ToListAsync();

        version.DynamicPolicies = await _dbContext.Set<CfDynamicStagePolicy>()
            .Where(p => p.FFlowVersionId == versionId)
            .OrderBy(p => p.FSourceStageKey)
            .ThenBy(p => p.FPriority)
            .ThenBy(p => p.FID)
            .Select(p => new DynamicStagePolicyDto
            {
                Id = p.FID,
                PolicyKey = p.FPolicyKey,
                SourceStageKey = p.FSourceStageKey,
                PolicyName = p.FPolicyName,
                StrategyType = p.FStrategyType,
                StrategyConfigJson = p.FStrategyConfigJson,
                ConditionJson = p.FConditionJson,
                TriggerTiming = p.FTriggerTiming,
                InsertPosition = p.FInsertPosition,
                ContinuationStageKey = p.FContinuationStageKey,
                Priority = p.FPriority,
                MaxInsertCount = p.FMaxInsertCount,
                FallbackJson = p.FFallbackJson,
                Status = p.FStatus
            })
            .ToListAsync();

        return version;
    }

    public async Task<FlowVersionDetailDto> SaveDraftVersionAsync(long definitionId, SaveDraftVersionRequest request, long operatorId)
    {
        var existingDraft = await _dbContext.Set<CfFlowVersion>()
            .FirstOrDefaultAsync(x => x.FFlowDefinitionId == definitionId && x.FStatus == "draft");

        if (existingDraft == null)
        {
            // 计算新版本号
            var maxVersion = await _dbContext.Set<CfFlowVersion>()
                .Where(x => x.FFlowDefinitionId == definitionId)
                .MaxAsync(x => (int?)x.FVersionNumber) ?? 0;

            existingDraft = new CfFlowVersion
            {
                FFlowDefinitionId = definitionId,
                FVersionNumber = maxVersion + 1,
                FStatus = "draft",
                FCardSchemaJson = request.CardSchemaJson,
                FDetailSchemaJson = request.DetailSchemaJson,
                FFlowSettingsJson = request.FlowSettingsJson,
                FCreatorId = operatorId,
                FCreatedTime = DateTime.Now,
                FIsCurrentVersion = false
            };
            _dbContext.Set<CfFlowVersion>().Add(existingDraft);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            existingDraft.FCardSchemaJson = request.CardSchemaJson;
            existingDraft.FDetailSchemaJson = request.DetailSchemaJson;
            existingDraft.FFlowSettingsJson = request.FlowSettingsJson;

            var oldRouteRules = await _dbContext.Set<CfStageRouteRule>()
                .Where(r => r.FFlowVersionId == existingDraft.FID)
                .ToListAsync();
            _dbContext.Set<CfStageRouteRule>().RemoveRange(oldRouteRules);

            var oldDynamicPolicies = await _dbContext.Set<CfDynamicStagePolicy>()
                .Where(p => p.FFlowVersionId == existingDraft.FID)
                .ToListAsync();
            _dbContext.Set<CfDynamicStagePolicy>().RemoveRange(oldDynamicPolicies);

            // 删除旧节点
            var oldStages = await _dbContext.Set<CfStageDefinition>()
                .Where(s => s.FFlowVersionId == existingDraft.FID)
                .ToListAsync();
            _dbContext.Set<CfStageDefinition>().RemoveRange(oldStages);
        }

        // 保存新节点
        var usedStageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stageReq in request.Stages)
        {
            // 自动节点必须指定插件注册，且节点粒度与插件粒度必须一致
            if (stageReq.PluginRegistryId.HasValue)
            {
                var registry = await _dbContext.Set<CfAutoPluginRegistry>()
                    .FirstOrDefaultAsync(r => r.FID == stageReq.PluginRegistryId.Value)
                    ?? throw new InvalidOperationException($"指定的插件注册不存在（FID={stageReq.PluginRegistryId}）");

                if (!string.Equals(registry.F处理粒度, stageReq.ProcessingGranularity, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"节点粒度({stageReq.ProcessingGranularity})与插件粒度({registry.F处理粒度})不一致");
            }

            // 插件规则必须隐含于对应插件下（可选：部分插件可不依赖规则）
            if (stageReq.PluginRuleId.HasValue)
            {
                var ruleExists = await _dbContext.Set<CfPluginRule>()
                    .AnyAsync(r => r.FID == stageReq.PluginRuleId.Value);
                if (!ruleExists)
                    throw new InvalidOperationException($"指定的插件规则不存在（FID={stageReq.PluginRuleId}）");
            }

            var stage = new CfStageDefinition
            {
                FFlowVersionId = existingDraft.FID,
                FStageKey = EnsureStageKey(stageReq.StageKey, stageReq.SortOrder, stageReq.Name, usedStageKeys),
                FSortOrder = stageReq.SortOrder,
                FStageName = stageReq.Name,
                FType = stageReq.Type,
                FApprovalMode = NormalizeApprovalMode(stageReq.ApprovalMode),
                FAssigneeStrategy = NormalizeAssigneeStrategy(stageReq.AssigneeStrategy),
                FAssigneeConfigJson = stageReq.AssigneeConfigJson,
                FConditionJson = stageReq.ConditionJson,
                FInputFieldsJson = stageReq.InputFieldsJson,
                F处理粒度 = string.IsNullOrEmpty(stageReq.ProcessingGranularity) ? "card" : stageReq.ProcessingGranularity,
                F插件注册ID = stageReq.PluginRegistryId,
                F插件规则ID = stageReq.PluginRuleId,
                FFailurePolicyJson = stageReq.FailurePolicyJson,
                FCcConfigJson = stageReq.CcConfigJson,
                FTimeoutHours = stageReq.TimeoutHours,
                FPriorityTemplate = stageReq.PriorityTemplate
            };
            _dbContext.Set<CfStageDefinition>().Add(stage);
        }

        await _dbContext.SaveChangesAsync();
        var stageKeyMap = await BuildStageKeyMapAsync(existingDraft.FID);
        await SaveRouteRulesAsync(existingDraft.FID, request.Routes, stageKeyMap);
        await SaveDynamicPoliciesAsync(existingDraft.FID, request.DynamicPolicies, stageKeyMap);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Saved draft version for definition {DefinitionId}", definitionId);

        return (await GetVersionDetailAsync(definitionId, existingDraft.FID))!;
    }

    private static string NormalizeApprovalMode(string? mode) => (mode ?? "").ToLowerInvariant() switch
    {
        "allapprove" => "countersign",
        "anyapprove" => "single",
        "sequential" => "sequential",
        "countersign" => "countersign",
        "orsign" => "orsign",
        "single" => "single",
        "" => "single",
        _ => mode!.ToLowerInvariant()
    };

    private static string NormalizeAssigneeStrategy(string? strategy) => (strategy ?? "").ToLowerInvariant() switch
    {
        "specified" => "fixed",
        "department" => "initiator", // 暂不支持，降级为发起人
        "role" => "role",
        "initiator" => "initiator",
        "fixed" => "fixed",
        "" => "initiator",
        _ => strategy!.ToLowerInvariant()
    };

    private static string EnsureStageKey(string? stageKey, int sortOrder, string stageName, ISet<string> usedKeys)
    {
        var result = NormalizeKeyToken(stageKey);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException($"节点“{stageName}”(排序 {sortOrder}) 必须携带稳定 StageKey");
        if (!usedKeys.Add(result))
            throw new InvalidOperationException($"节点 StageKey 重复：{result}");
        return result;
    }

    private static string EnsureRuleKey(string? ruleKey, string prefix, int sortOrder, ISet<string> usedKeys)
    {
        var result = NormalizeKeyToken(ruleKey);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException($"{prefix} 规则(优先级 {sortOrder}) 必须携带稳定 Key");
        if (!usedKeys.Add(result))
            throw new InvalidOperationException($"{prefix} 规则 Key 重复：{result}");
        return result;
    }

    private static string? NormalizeKeyToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var chars = value.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();
        var token = new string(chars).Trim('_');
        while (token.Contains("__", StringComparison.Ordinal))
        {
            token = token.Replace("__", "_", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private async Task<Dictionary<string, long>> BuildStageKeyMapAsync(long flowVersionId)
    {
        var stages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == flowVersionId)
            .Select(s => new { s.FID, s.FStageKey })
            .ToListAsync();

        return stages
            .Where(s => !string.IsNullOrWhiteSpace(s.FStageKey))
            .GroupBy(s => s.FStageKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().FID, StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveRouteRulesAsync(
        long flowVersionId,
        List<StageRouteRuleRequest> routes,
        IReadOnlyDictionary<string, long> stageKeyMap)
    {
        var usedRuleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes ?? new List<StageRouteRuleRequest>())
        {
            var fromStageKey = NormalizeStageReference(route.FromStageKey, "条件边来源节点不能为空");
            var toStageKey = NormalizeStageReference(route.ToStageKey, "条件边目标节点不能为空");
            if (!stageKeyMap.TryGetValue(fromStageKey, out var fromStageId))
                throw new InvalidOperationException($"条件边来源节点不存在：{fromStageKey}");
            if (!stageKeyMap.TryGetValue(toStageKey, out var toStageId))
                throw new InvalidOperationException($"条件边目标节点不存在：{toStageKey}");

            _dbContext.Set<CfStageRouteRule>().Add(new CfStageRouteRule
            {
                FFlowVersionId = flowVersionId,
                FEdgeKey = EnsureRuleKey(route.EdgeKey, "edge", route.Priority, usedRuleKeys),
                FFromStageDefinitionId = fromStageId,
                FFromStageKey = fromStageKey,
                FToStageDefinitionId = toStageId,
                FToStageKey = toStageKey,
                FRouteName = string.IsNullOrWhiteSpace(route.RouteName) ? "未命名条件" : route.RouteName.Trim(),
                FConditionJson = route.ConditionJson,
                FPriority = route.Priority,
                FIsDefault = route.IsDefault,
                FStatus = string.IsNullOrWhiteSpace(route.Status) ? "active" : route.Status.Trim(),
                FFailurePolicyJson = route.FailurePolicyJson
            });
        }
    }

    private async Task SaveDynamicPoliciesAsync(
        long flowVersionId,
        List<DynamicStagePolicyRequest> dynamicPolicies,
        IReadOnlyDictionary<string, long> stageKeyMap)
    {
        var usedPolicyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var policy in dynamicPolicies ?? new List<DynamicStagePolicyRequest>())
        {
            var sourceStageKey = NormalizeStageReference(policy.SourceStageKey, "动态节点策略触发节点不能为空");
            if (!stageKeyMap.TryGetValue(sourceStageKey, out var sourceStageId))
                throw new InvalidOperationException($"动态节点策略触发节点不存在：{sourceStageKey}");

            _dbContext.Set<CfDynamicStagePolicy>().Add(new CfDynamicStagePolicy
            {
                FFlowVersionId = flowVersionId,
                FPolicyKey = EnsureRuleKey(policy.PolicyKey, "policy", policy.Priority, usedPolicyKeys),
                FSourceStageDefinitionId = sourceStageId,
                FSourceStageKey = sourceStageKey,
                FPolicyName = string.IsNullOrWhiteSpace(policy.PolicyName) ? "未命名动态节点策略" : policy.PolicyName.Trim(),
                FStrategyType = string.IsNullOrWhiteSpace(policy.StrategyType) ? "fixedUsers" : policy.StrategyType.Trim(),
                FStrategyConfigJson = policy.StrategyConfigJson,
                FConditionJson = policy.ConditionJson,
                FTriggerTiming = NormalizeDynamicPolicyTriggerTiming(policy.TriggerTiming),
                FInsertPosition = string.IsNullOrWhiteSpace(policy.InsertPosition) ? "afterSource" : policy.InsertPosition.Trim(),
                FContinuationStageKey = NormalizeKeyToken(policy.ContinuationStageKey),
                FPriority = policy.Priority,
                FMaxInsertCount = Math.Clamp(policy.MaxInsertCount ?? 20, 1, 20),
                FFallbackJson = policy.FallbackJson,
                FStatus = string.IsNullOrWhiteSpace(policy.Status) ? "active" : policy.Status.Trim()
            });
        }
    }

    private static string NormalizeStageReference(string? stageKey, string errorMessage)
    {
        return NormalizeKeyToken(stageKey) ?? throw new InvalidOperationException(errorMessage);
    }

    private async Task CloneRouteRulesAsync(long sourceVersionId, long targetVersionId)
    {
        var stageKeyMap = await BuildStageKeyMapAsync(targetVersionId);
        var sourceRoutes = await _dbContext.Set<CfStageRouteRule>()
            .Where(r => r.FFlowVersionId == sourceVersionId)
            .OrderBy(r => r.FPriority)
            .ThenBy(r => r.FID)
            .ToListAsync();

        await SaveRouteRulesAsync(
            targetVersionId,
            sourceRoutes.Select(r => new StageRouteRuleRequest
            {
                EdgeKey = r.FEdgeKey,
                FromStageKey = r.FFromStageKey,
                ToStageKey = r.FToStageKey,
                RouteName = r.FRouteName,
                ConditionJson = r.FConditionJson,
                Priority = r.FPriority,
                IsDefault = r.FIsDefault,
                Status = r.FStatus,
                FailurePolicyJson = r.FFailurePolicyJson
            }).ToList(),
            stageKeyMap);
    }

    private async Task CloneDynamicPoliciesAsync(long sourceVersionId, long targetVersionId)
    {
        var stageKeyMap = await BuildStageKeyMapAsync(targetVersionId);
        var sourcePolicies = await _dbContext.Set<CfDynamicStagePolicy>()
            .Where(p => p.FFlowVersionId == sourceVersionId)
            .OrderBy(p => p.FPriority)
            .ThenBy(p => p.FID)
            .ToListAsync();

        await SaveDynamicPoliciesAsync(
            targetVersionId,
            sourcePolicies.Select(p => new DynamicStagePolicyRequest
            {
                PolicyKey = p.FPolicyKey,
                SourceStageKey = p.FSourceStageKey,
                PolicyName = p.FPolicyName,
                StrategyType = p.FStrategyType,
                StrategyConfigJson = p.FStrategyConfigJson,
                ConditionJson = p.FConditionJson,
                TriggerTiming = p.FTriggerTiming,
                InsertPosition = p.FInsertPosition,
                ContinuationStageKey = p.FContinuationStageKey,
                Priority = p.FPriority,
                MaxInsertCount = p.FMaxInsertCount,
                FallbackJson = p.FFallbackJson,
                Status = p.FStatus
            }).ToList(),
            stageKeyMap);
    }

    private async Task ValidateRouteRulesAsync(long flowVersionId, List<CfStageDefinition> stages)
    {
        var stageKeySet = stages
            .Select(stage => stage.FStageKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = await _dbContext.Set<CfStageRouteRule>()
            .Where(rule => rule.FFlowVersionId == flowVersionId && rule.FStatus == "active")
            .ToListAsync();

        foreach (var group in rules.GroupBy(rule => rule.FFromStageKey, StringComparer.OrdinalIgnoreCase))
        {
            if (!stageKeySet.Contains(group.Key))
                throw new InvalidOperationException($"条件流转来源节点不存在：{group.Key}");

            var defaultCount = group.Count(rule => rule.FIsDefault);
            if (defaultCount != 1)
                throw new InvalidOperationException($"节点 {group.Key} 的条件流转必须且只能配置一个默认分支");

            var duplicatedPriority = group
                .GroupBy(rule => rule.FPriority)
                .FirstOrDefault(priorityGroup => priorityGroup.Count() > 1);
            if (duplicatedPriority != null)
                throw new InvalidOperationException($"节点 {group.Key} 的条件流转优先级重复：{duplicatedPriority.Key}");

            foreach (var rule in group)
            {
                if (!stageKeySet.Contains(rule.FToStageKey))
                    throw new InvalidOperationException($"条件流转目标节点不存在：{rule.FToStageKey}");
                if (rule.FIsDefault && !string.IsNullOrWhiteSpace(rule.FConditionJson))
                    throw new InvalidOperationException($"默认分支不能配置条件：{rule.FEdgeKey}");
                if (!rule.FIsDefault && string.IsNullOrWhiteSpace(rule.FConditionJson))
                    throw new InvalidOperationException($"非默认分支必须配置条件：{rule.FEdgeKey}");
            }
        }

        if (rules.Count > 0)
        {
            var nodeKeys = stages
                .Select(s => s.FStageKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToList();
            var entryKey = stages.FirstOrDefault()?.FStageKey;
            var edges = rules
                .Select(rule => (From: rule.FFromStageKey, To: rule.FToStageKey))
                .ToList();

            var graphErrors = RouteGraphValidator.Validate(nodeKeys, entryKey, edges);
            if (graphErrors.Count > 0)
                throw new InvalidOperationException(string.Join("；", graphErrors));
        }
    }

    private async Task ValidateDynamicPoliciesAsync(long flowVersionId, List<CfStageDefinition> stages)
    {
        var stageKeySet = stages
            .Select(stage => stage.FStageKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var policies = await _dbContext.Set<CfDynamicStagePolicy>()
            .Where(policy => policy.FFlowVersionId == flowVersionId && policy.FStatus == "active")
            .OrderBy(policy => policy.FSourceStageKey)
            .ThenBy(policy => policy.FPriority)
            .ToListAsync();
        if (policies.Count == 0)
            return;

        var supportedTimings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "afterSourceBeforeRoute",
            "afterRouteBeforeTarget",
            "afterTarget",
            "replaceTargetHandlers"
        };
        var supportedStrategies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fixedUsers",
            "fieldUsers",
            "role",
            "orgChain",
            "amountMatrix",
            "feeTypeBp"
        };

        foreach (var policy in policies)
        {
            if (string.IsNullOrWhiteSpace(policy.FSourceStageKey) || !stageKeySet.Contains(policy.FSourceStageKey))
                throw new InvalidOperationException($"动态节点策略触发节点不存在：{policy.FSourceStageKey}");
            var triggerTiming = NormalizeDynamicPolicyTriggerTiming(policy.FTriggerTiming);
            if (!supportedTimings.Contains(triggerTiming))
                throw new InvalidOperationException($"动态节点策略触发时机无效：{policy.FPolicyName} / {policy.FTriggerTiming}");
            if (!supportedStrategies.Contains(policy.FStrategyType))
                throw new InvalidOperationException($"动态节点策略处理人策略无效：{policy.FPolicyName} / {policy.FStrategyType}");
            if (policy.FMaxInsertCount is < 1 or > 20)
                throw new InvalidOperationException($"动态节点策略最大插入数量必须在 1 到 20 之间：{policy.FPolicyName}");
            if (string.IsNullOrWhiteSpace(policy.FFallbackJson) || !LooksLikeJsonObject(policy.FFallbackJson))
                throw new InvalidOperationException($"动态节点策略必须配置处理人兜底：{policy.FPolicyName}");
            if (string.Equals(triggerTiming, "afterRouteBeforeTarget", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(policy.FContinuationStageKey))
            {
                throw new InvalidOperationException($"afterRouteBeforeTarget 动态节点策略必须配置续接节点：{policy.FPolicyName}");
            }
            if (!string.IsNullOrWhiteSpace(policy.FContinuationStageKey) && !stageKeySet.Contains(policy.FContinuationStageKey))
                throw new InvalidOperationException($"动态节点策略续接节点不存在：{policy.FContinuationStageKey}");
        }

        static bool LooksLikeJsonObject(string json)
        {
            try
            {
                using var document = global::System.Text.Json.JsonDocument.Parse(json);
                return document.RootElement.ValueKind == global::System.Text.Json.JsonValueKind.Object;
            }
            catch (global::System.Text.Json.JsonException)
            {
                return false;
            }
        }
    }

    private static string NormalizeDynamicPolicyTriggerTiming(string? triggerTiming)
    {
        return triggerTiming?.Trim() switch
        {
            null or "" => "afterSourceBeforeRoute",
            "afterComplete" => "afterSourceBeforeRoute",
            "afterSourceBeforeRoute" => "afterSourceBeforeRoute",
            "afterRouteBeforeTarget" => "afterRouteBeforeTarget",
            "afterTarget" => "afterTarget",
            "replaceTargetHandlers" => "replaceTargetHandlers",
            var value => value
        };
    }

    public async Task<FlowVersionDetailDto?> GetDraftVersionAsync(long definitionId)
    {
        var draft = await _dbContext.Set<CfFlowVersion>()
            .FirstOrDefaultAsync(x => x.FFlowDefinitionId == definitionId && x.FStatus == "draft");

        if (draft == null)
        {
            // 自动从当前已发布版本克隆一份草稿
            var published = await _dbContext.Set<CfFlowVersion>()
                .FirstOrDefaultAsync(x => x.FFlowDefinitionId == definitionId && x.FIsCurrentVersion);
            if (published == null) return null; // 既无草稿也无已发布版本

            // 计算新版本号
            var maxVersion = await _dbContext.Set<CfFlowVersion>()
                .Where(x => x.FFlowDefinitionId == definitionId)
                .MaxAsync(x => (int?)x.FVersionNumber) ?? 0;

            draft = new CfFlowVersion
            {
                FFlowDefinitionId = definitionId,
                FVersionNumber = maxVersion + 1,
                FStatus = "draft",
                FCardSchemaJson = published.FCardSchemaJson,
                FDetailSchemaJson = published.FDetailSchemaJson,
                FFlowSettingsJson = published.FFlowSettingsJson,
                FCreatorId = published.FCreatorId,
                FCreatedTime = DateTime.Now,
                FIsCurrentVersion = false
            };
            _dbContext.Set<CfFlowVersion>().Add(draft);
            await _dbContext.SaveChangesAsync();

            // 克隆节点定义
            var sourceStages = await _dbContext.Set<CfStageDefinition>()
                .Where(s => s.FFlowVersionId == published.FID)
                .OrderBy(s => s.FSortOrder)
                .ToListAsync();

            var usedStageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var src in sourceStages)
            {
                var clone = new CfStageDefinition
                {
                    FFlowVersionId = draft.FID,
                    FStageKey = EnsureStageKey(src.FStageKey, src.FSortOrder, src.FStageName, usedStageKeys),
                    FSortOrder = src.FSortOrder,
                    FStageName = src.FStageName,
                    FType = src.FType,
                    FApprovalMode = src.FApprovalMode,
                    FAssigneeStrategy = src.FAssigneeStrategy,
                    FAssigneeConfigJson = src.FAssigneeConfigJson,
                    FConditionJson = src.FConditionJson,
                    FInputFieldsJson = src.FInputFieldsJson,
                    F处理粒度 = src.F处理粒度,
                    F插件注册ID = src.F插件注册ID,
                    F插件规则ID = src.F插件规则ID,
                    FFailurePolicyJson = src.FFailurePolicyJson,
                    FCcConfigJson = src.FCcConfigJson,
                    FTimeoutHours = src.FTimeoutHours,
                    FPriorityTemplate = src.FPriorityTemplate
                };
                _dbContext.Set<CfStageDefinition>().Add(clone);
            }
            await _dbContext.SaveChangesAsync();
            await CloneRouteRulesAsync(published.FID, draft.FID);
            await CloneDynamicPoliciesAsync(published.FID, draft.FID);
            await _dbContext.SaveChangesAsync();
        }

        return await GetVersionDetailAsync(definitionId, draft.FID);
    }

    public async Task<FlowDefinitionDto> CloneFlowDefinitionAsync(
        long sourceDefinitionId,
        CloneFlowDefinitionRequest request,
        long operatorId)
    {
        // 源读取隔离：先按当前组织过滤读；读不到再回退全局模板(FOrgId=0)。不可读他组织私有流程。
        // request.OrgId 忽略——克隆一律落当前组织，目标组织由服务端 CurrentOrgId(FillOrgId) 决定。
        var sourceDefinition = await _dbContext.Set<CfFlowDefinition>()
                .FirstOrDefaultAsync(x => x.FID == sourceDefinitionId)
            ?? await _dbContext.Set<CfFlowDefinition>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.FID == sourceDefinitionId && x.FOrgId == 0)
            ?? throw new InvalidOperationException("源流程定义不存在");

        var newDefinition = await CloneInternalAsync(
            sourceDefinition, request.FlowName, request.FlowCode, request.Description,
            asGlobalTemplate: false, operatorId);

        _logger.LogInformation("Cloned flow definition {SourceId} -> {NewId} by operator {OperatorId}",
            sourceDefinitionId, newDefinition.FID, operatorId);
        return MapToDto(newDefinition);
    }

    /// <summary>克隆主体：建定义 + 复制当前发布版本/节点/路由/动态策略。
    /// asGlobalTemplate=true 时把定义建为全局(FOrgId=0)并抑制自动组织填充；否则落当前组织(FillOrgId)。</summary>
    private async Task<CfFlowDefinition> CloneInternalAsync(
        CfFlowDefinition sourceDefinition, string flowName, string flowCode, string? description,
        bool asGlobalTemplate, long operatorId)
    {
        var newDefinition = new CfFlowDefinition
        {
            FFlowName = flowName,
            FFlowCode = flowCode,
            FDescription = description ?? sourceDefinition.FDescription,
            FStatus = "draft",
            FNumberTemplate = sourceDefinition.FNumberTemplate,
            FTitleTemplate = sourceDefinition.FTitleTemplate,
            FAllowedRolesJson = sourceDefinition.FAllowedRolesJson,
            FFlowGroupId = null,
            FTriggerConfigJson = sourceDefinition.FTriggerConfigJson,
            FAccountSetId = sourceDefinition.FAccountSetId,
            FOrgId = 0, // 非模板由 FillOrgId 填当前组织；模板在抑制作用域内保持 0
            FCreatorId = operatorId,
            FCreatedTime = DateTime.Now
        };

        _dbContext.Set<CfFlowDefinition>().Add(newDefinition);
        if (asGlobalTemplate)
        {
            using (_dbContext.SuppressOrgIdFill())
            {
                await _dbContext.SaveChangesAsync();
            }
        }
        else
        {
            await _dbContext.SaveChangesAsync();
        }

        var sourceVersion = await _dbContext.Set<CfFlowVersion>()
            .FirstOrDefaultAsync(x => x.FFlowDefinitionId == sourceDefinition.FID && x.FIsCurrentVersion);
        if (sourceVersion != null)
        {
            var newVersion = new CfFlowVersion
            {
                FFlowDefinitionId = newDefinition.FID,
                FVersionNumber = 1,
                FStatus = "draft",
                FCardSchemaJson = sourceVersion.FCardSchemaJson,
                FDetailSchemaJson = sourceVersion.FDetailSchemaJson,
                FFlowSettingsJson = sourceVersion.FFlowSettingsJson,
                FCreatorId = operatorId,
                FCreatedTime = DateTime.Now,
                FIsCurrentVersion = false
            };
            _dbContext.Set<CfFlowVersion>().Add(newVersion);
            await _dbContext.SaveChangesAsync();

            var sourceStages = await _dbContext.Set<CfStageDefinition>()
                .Where(s => s.FFlowVersionId == sourceVersion.FID)
                .OrderBy(s => s.FSortOrder)
                .ToListAsync();
            var usedStageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var src in sourceStages)
            {
                _dbContext.Set<CfStageDefinition>().Add(new CfStageDefinition
                {
                    FFlowVersionId = newVersion.FID,
                    FStageKey = EnsureStageKey(src.FStageKey, src.FSortOrder, src.FStageName, usedStageKeys),
                    FSortOrder = src.FSortOrder,
                    FStageName = src.FStageName,
                    FType = src.FType,
                    FApprovalMode = src.FApprovalMode,
                    FAssigneeStrategy = src.FAssigneeStrategy,
                    FAssigneeConfigJson = src.FAssigneeConfigJson,
                    FConditionJson = src.FConditionJson,
                    FInputFieldsJson = src.FInputFieldsJson,
                    F处理粒度 = src.F处理粒度,
                    F插件注册ID = src.F插件注册ID,
                    F插件规则ID = src.F插件规则ID,
                    FFailurePolicyJson = src.FFailurePolicyJson,
                    FCcConfigJson = src.FCcConfigJson,
                    FTimeoutHours = src.FTimeoutHours,
                    FPriorityTemplate = src.FPriorityTemplate
                });
            }
            await _dbContext.SaveChangesAsync();
            await CloneRouteRulesAsync(sourceVersion.FID, newVersion.FID);
            await CloneDynamicPoliciesAsync(sourceVersion.FID, newVersion.FID);
            await _dbContext.SaveChangesAsync();
        }

        return newDefinition;
    }

    public async Task<List<FlowDefinitionDto>> GetTemplatesAsync()
    {
        var templates = await _dbContext.Set<CfFlowDefinition>()
            .IgnoreQueryFilters()
            .Where(x => x.FIsTemplate && x.FStatus == "published" && x.FOrgId == 0)
            .OrderBy(x => x.FFlowName)
            .Select(x => new FlowDefinitionDto
            {
                Id = x.FID,
                FlowName = x.FFlowName,
                FlowCode = x.FFlowCode,
                Description = x.FDescription,
                Status = x.FStatus,
                NumberTemplate = x.FNumberTemplate,
                TitleTemplate = x.FTitleTemplate,
                AllowedRolesJson = x.FAllowedRolesJson,
                FlowGroupId = x.FFlowGroupId,
                OrgId = x.FOrgId,
                CreatedTime = x.FCreatedTime,
                TriggerConfigJson = x.FTriggerConfigJson,
                AccountSetId = x.FAccountSetId,
                IsTemplate = x.FIsTemplate
            })
            .ToListAsync();
        return templates;
    }

    public async Task<FlowDefinitionDto> SaveAsTemplateAsync(long sourceDefinitionId, long operatorId)
    {
        // 1. 读取源流程（当前组织下，受 IOrgScoped 过滤）
        var source = await _dbContext.Set<CfFlowDefinition>()
            .FirstOrDefaultAsync(x => x.FID == sourceDefinitionId)
            ?? throw new InvalidOperationException("流程定义不存在");

        // 2. 检查是否已存在同编码模板（FOrgId=0）
        var existingTemplate = await _dbContext.Set<CfFlowDefinition>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FFlowCode == source.FFlowCode && x.FOrgId == 0);

        if (existingTemplate != null)
        {
            // 已存在同编码模板 → 更新模板的版本内容
            // 删除旧版本和节点，重新从源复制
            var oldVersions = await _dbContext.Set<CfFlowVersion>()
                .IgnoreQueryFilters()
                .Where(v => v.FFlowDefinitionId == existingTemplate.FID)
                .ToListAsync();
            foreach (var ov in oldVersions)
            {
                var oldRouteRules = await _dbContext.Set<CfStageRouteRule>()
                    .IgnoreQueryFilters()
                    .Where(r => r.FFlowVersionId == ov.FID)
                    .ToListAsync();
                _dbContext.Set<CfStageRouteRule>().RemoveRange(oldRouteRules);

                var oldDynamicPolicies = await _dbContext.Set<CfDynamicStagePolicy>()
                    .IgnoreQueryFilters()
                    .Where(p => p.FFlowVersionId == ov.FID)
                    .ToListAsync();
                _dbContext.Set<CfDynamicStagePolicy>().RemoveRange(oldDynamicPolicies);

                var oldStages = await _dbContext.Set<CfStageDefinition>()
                    .IgnoreQueryFilters()
                    .Where(s => s.FFlowVersionId == ov.FID)
                    .ToListAsync();
                _dbContext.Set<CfStageDefinition>().RemoveRange(oldStages);
            }
            _dbContext.Set<CfFlowVersion>().RemoveRange(oldVersions);

            // 更新定义基本信息
            existingTemplate.FFlowName = source.FFlowName;
            existingTemplate.FDescription = source.FDescription;
            existingTemplate.FNumberTemplate = source.FNumberTemplate;
            existingTemplate.FTitleTemplate = source.FTitleTemplate;
            existingTemplate.FAllowedRolesJson = source.FAllowedRolesJson;
            existingTemplate.FTriggerConfigJson = source.FTriggerConfigJson;
            existingTemplate.FAccountSetId = source.FAccountSetId;
            existingTemplate.FStatus = "published";
            existingTemplate.FIsTemplate = true;
            existingTemplate.FUpdatedTime = DateTime.Now;
            await _dbContext.SaveChangesAsync();

            // 复制源流程当前发布版本+节点到模板（新版本号=1，状态=published，作为当前版本）
            await CopyCurrentVersionToTemplateAsync(sourceDefinitionId, existingTemplate.FID, operatorId);

            _logger.LogInformation("Updated existing template {TemplateId} from source {SourceId} by operator {OperatorId}", existingTemplate.FID, sourceDefinitionId, operatorId);
            return MapToDto(existingTemplate);
        }
        else
        {
            // 新建全局模板：在抑制作用域内以 FOrgId=0 创建（CloneInternalAsync 内部处理）
            var newTemplate = await CloneInternalAsync(
                source, source.FFlowName, source.FFlowCode, source.FDescription,
                asGlobalTemplate: true, operatorId);

            newTemplate.FStatus = "published";
            newTemplate.FIsTemplate = true;

            var templateVersion = await _dbContext.Set<CfFlowVersion>()
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstOrDefaultAsync(x => x.FFlowDefinitionId == newTemplate.FID);
            if (templateVersion != null)
            {
                templateVersion.FStatus = "published";
                templateVersion.FIsCurrentVersion = true;
                templateVersion.FPublishTime = DateTime.Now;
            }
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created new template {TemplateId} from source {SourceId} by operator {OperatorId}", newTemplate.FID, sourceDefinitionId, operatorId);
            return MapToDto(newTemplate);
        }
    }

    /// <summary>
    /// 从源流程的当前发布版本复制版本+节点到目标定义（用于更新已存在模板）。
    /// 目标版本设置为 published 且 IsCurrentVersion=true，版本号=1。
    /// </summary>
    private async Task CopyCurrentVersionToTemplateAsync(long sourceDefinitionId, long targetDefinitionId, long operatorId)
    {
        var sourceVersion = await _dbContext.Set<CfFlowVersion>()
            .FirstOrDefaultAsync(x => x.FFlowDefinitionId == sourceDefinitionId && x.FIsCurrentVersion);
        if (sourceVersion == null) return;

        var newVersion = new CfFlowVersion
        {
            FFlowDefinitionId = targetDefinitionId,
            FVersionNumber = 1,
            FStatus = "published",
            FCardSchemaJson = sourceVersion.FCardSchemaJson,
            FDetailSchemaJson = sourceVersion.FDetailSchemaJson,
            FFlowSettingsJson = sourceVersion.FFlowSettingsJson,
            FCreatorId = operatorId,
            FCreatedTime = DateTime.Now,
            FPublishTime = DateTime.Now,
            FIsCurrentVersion = true
        };
        _dbContext.Set<CfFlowVersion>().Add(newVersion);
        await _dbContext.SaveChangesAsync();

        var sourceStages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == sourceVersion.FID)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync();

        var usedStageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var src in sourceStages)
        {
            var newStage = new CfStageDefinition
            {
                FFlowVersionId = newVersion.FID,
                FStageKey = EnsureStageKey(src.FStageKey, src.FSortOrder, src.FStageName, usedStageKeys),
                FSortOrder = src.FSortOrder,
                FStageName = src.FStageName,
                FType = src.FType,
                FApprovalMode = src.FApprovalMode,
                FAssigneeStrategy = src.FAssigneeStrategy,
                FAssigneeConfigJson = src.FAssigneeConfigJson,
                FConditionJson = src.FConditionJson,
                FInputFieldsJson = src.FInputFieldsJson,
                F处理粒度 = src.F处理粒度,
                F插件注册ID = src.F插件注册ID,
                F插件规则ID = src.F插件规则ID,
                FFailurePolicyJson = src.FFailurePolicyJson,
                FCcConfigJson = src.FCcConfigJson,
                FTimeoutHours = src.FTimeoutHours,
                FPriorityTemplate = src.FPriorityTemplate
            };
            _dbContext.Set<CfStageDefinition>().Add(newStage);
        }
        await _dbContext.SaveChangesAsync();
        await CloneRouteRulesAsync(sourceVersion.FID, newVersion.FID);
        await CloneDynamicPoliciesAsync(sourceVersion.FID, newVersion.FID);
        await _dbContext.SaveChangesAsync();
    }

    private static FlowDefinitionDto MapToDto(CfFlowDefinition entity)
    {
        return new FlowDefinitionDto
        {
            Id = entity.FID,
            FlowName = entity.FFlowName,
            FlowCode = entity.FFlowCode,
            Description = entity.FDescription,
            Status = entity.FStatus,
            NumberTemplate = entity.FNumberTemplate,
            TitleTemplate = entity.FTitleTemplate,
            AllowedRolesJson = entity.FAllowedRolesJson,
            FlowGroupId = entity.FFlowGroupId,
            OrgId = entity.FOrgId,
            CreatedTime = entity.FCreatedTime,
            TriggerConfigJson = entity.FTriggerConfigJson,
            AccountSetId = entity.FAccountSetId,
            IsTemplate = entity.FIsTemplate
        };
    }
}
