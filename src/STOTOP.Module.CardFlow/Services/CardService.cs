using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Models.Schema;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.CardFlow.Services.Redaction;

namespace STOTOP.Module.CardFlow.Services;

public class CardService : ICardService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<CardService> _logger;
    private readonly IStageConfigParser _stageConfigParser;
    private readonly IStageViewProfileResolver _stageViewResolver;
    private readonly ICardFlowSourceContextVerifier _sourceContextVerifier;
    private readonly ICardRedactionService _redactionService;

    public CardService(
        STOTOPDbContext dbContext,
        ILogger<CardService> logger,
        IStageConfigParser stageConfigParser,
        IStageViewProfileResolver stageViewResolver,
        ICardFlowSourceContextVerifier sourceContextVerifier,
        ICardRedactionService redactionService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _stageConfigParser = stageConfigParser;
        _stageViewResolver = stageViewResolver;
        _sourceContextVerifier = sourceContextVerifier;
        _redactionService = redactionService;
    }

    public async Task<List<AvailableFlowDto>> GetAvailableFlowsAsync(long userId, long orgId)
    {
        return await _dbContext.Set<CfFlowDefinition>()
            .Where(x => x.FStatus == "published" && x.FOrgId == orgId)
            .Where(x => x.FTriggerConfigJson == null || !x.FTriggerConfigJson.Contains("fileUpload"))
            .Select(x => new AvailableFlowDto
            {
                Id = x.FID,
                FlowName = x.FFlowName,
                FlowCode = x.FFlowCode,
                Description = x.FDescription
            })
            .ToListAsync();
    }

    public async Task<PagedResult<CardListDto>> GetCardsAsync(CardQueryRequest request)
    {
        var query = _dbContext.Set<CfCard>().AsQueryable();

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(x => x.FStatus == request.Status);
        if (request.FlowId.HasValue)
            query = query.Where(x => x.FFlowDefinitionId == request.FlowId.Value);
        if (request.OrgId.HasValue)
            query = query.Where(x => x.FOrgId == request.OrgId.Value);
        if (request.InitiatorId.HasValue)
            query = query.Where(x => x.FInitiatorId == request.InitiatorId.Value);
        if (!string.IsNullOrWhiteSpace(request.SourceModule))
            query = query.Where(x => x.FSourceModule == NormalizeSourceContext(request.SourceModule));
        if (!string.IsNullOrWhiteSpace(request.SourceType))
            query = query.Where(x => x.FSourceType == NormalizeSourceContext(request.SourceType));
        if (request.SourceId.HasValue)
            query = query.Where(x => x.FSourceId == request.SourceId.Value);
        if (request.StartDate.HasValue)
            query = query.Where(x => x.FCreatedTime >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(x => x.FCreatedTime <= request.EndDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.FCreatedTime)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(_dbContext.Set<CfFlowDefinition>(), c => c.FFlowDefinitionId, f => f.FID, (c, f) => new CardListDto
            {
                Id = c.FID,
                CardNumber = c.FCardNumber,
                Title = c.FTitle,
                Status = c.FStatus,
                FlowName = f.FFlowName,
                InitiatorName = c.FInitiatorName,
                CreatedTime = c.FCreatedTime,
                SubmitTime = c.FSubmitTime,
                CompletedTime = c.FCompletedTime,
                SourceModule = c.FSourceModule,
                SourceType = c.FSourceType,
                SourceId = c.FSourceId,
                ReturnUrl = c.FReturnUrl,
                SourceTitle = c.FSourceTitle
            })
            .ToListAsync();

        return new PagedResult<CardListDto>
        {
            Items = items,
            Total = totalCount,
            PageIndex = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<PagedResult<CardListDto>> GetInitiatedCardsAsync(long userId, CardQueryRequest request)
    {
        request.InitiatorId = userId;
        return await GetCardsAsync(request);
    }

    public async Task<CardDetailDto?> GetByIdAsync(long id, long userId, bool canViewAll = false)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(x => x.FID == id);
        if (card == null) return null;

        var flowDef = await _dbContext.Set<CfFlowDefinition>().FirstOrDefaultAsync(x => x.FID == card.FFlowDefinitionId);

        var detail = new CardDetailDto
        {
            Id = card.FID,
            CardNumber = card.FCardNumber,
            Title = card.FTitle,
            Status = card.FStatus,
            FlowName = flowDef?.FFlowName ?? string.Empty,
            InitiatorName = card.FInitiatorName,
            CreatedTime = card.FCreatedTime,
            SubmitTime = card.FSubmitTime,
            CompletedTime = card.FCompletedTime,
            FlowDefinitionId = card.FFlowDefinitionId,
            FlowVersionId = card.FFlowVersionId,
            InitiatorId = card.FInitiatorId,
            CurrentStageInstanceId = card.FCurrentStageInstanceId,
            DataJson = card.FDataJson,
            CurrentRound = card.FCurrentRound,
            ConcurrencyStamp = Convert.ToBase64String(card.FRowVersion),
            SourceModule = card.FSourceModule,
            SourceType = card.FSourceType,
            SourceId = card.FSourceId,
            ReturnUrl = card.FReturnUrl,
            InitialDataJson = card.FInitialDataJson,
            SourceTitle = card.FSourceTitle
        };

        // Load stage instances with assignees
        var stageInstances = await _dbContext.Set<CfStageInstance>()
            .Where(s => s.FCardId == id)
            .OrderBy(s => s.FRound).ThenBy(s => s.FID)
            .ToListAsync();

        var stageInstanceIds = stageInstances.Select(s => s.FID).ToList();
        var assignees = await _dbContext.Set<CfStageAssignee>()
            .Where(a => stageInstanceIds.Contains(a.FStageInstanceId))
            .ToListAsync();

        // 最小卡片访问门：发起人 / 任意轮次处理人 / 管理员，否则拒绝（不暴露存在性）
        var isInitiator = card.FInitiatorId == userId;
        var isAssignee = assignees.Any(a => a.FUserId == userId);
        if (!canViewAll && !isInitiator && !isAssignee)
        {
            return null;
        }

        detail.StageInstances = stageInstances.Select(s => new StageInstanceDto
        {
            Id = s.FID,
            StageDefinitionId = s.FStageDefinitionId,
            StageName = s.FStageName,
            Type = s.FType,
            Status = s.FStatus,
            Round = s.FRound,
            FinalAction = s.FFinalAction,
            Opinion = s.FOpinion,
            ActivatedTime = s.FActivatedTime,
            CompletedTime = s.FCompletedTime,
            IsTimeout = s.FIsTimeout,
            Assignees = assignees
                .Where(a => a.FStageInstanceId == s.FID)
                .Select(a => new AssigneeDto
                {
                    Id = a.FID,
                    UserId = a.FUserId,
                    UserName = a.FUserName,
                    Status = a.FStatus,
                    Opinion = a.FOpinion,
                    CompletedTime = a.FCompletedTime
                })
                .ToList()
        }).ToList();

        // Load details
        var cardDetails = await _dbContext.Set<CfCardDetail>()
            .Where(d => d.FCardId == id)
            .OrderBy(d => d.FSortOrder)
            .ToListAsync();
        detail.Details = cardDetails.Select(ToCardDetailRowDto).ToList();
        detail.AuditTrail = await LoadRuntimeAuditTrailAsync(card, stageInstances, assignees);

        // 解析查看者处理过的节点配置（粘附授权）+ 当前 active 节点配置（节点视图权威）
        var viewerStageInstanceIds = assignees
            .Where(a => a.FUserId == userId)
            .Select(a => a.FStageInstanceId)
            .ToHashSet();
        var handledStageDefIds = stageInstances
            .Where(s => viewerStageInstanceIds.Contains(s.FID) && s.FStageDefinitionId != null)
            .Select(s => s.FStageDefinitionId!.Value)
            .Distinct()
            .ToList();

        var currentStage = stageInstances.FirstOrDefault(s => s.FID == card.FCurrentStageInstanceId && s.FStatus == "active");
        var viewerIsActiveAssignee = currentStage != null
            && assignees.Any(a => a.FStageInstanceId == currentStage.FID && a.FUserId == userId);

        var stageDefs = await _dbContext.Set<CfStageDefinition>()
            .AsNoTracking()
            .Where(s => handledStageDefIds.Contains(s.FID))
            .ToListAsync();
        var stageConfigs = stageDefs.ToDictionary(s => s.FID, s => _stageConfigParser.Parse(s.FInputFieldsJson));

        var flowVersion = await _dbContext.Set<CfFlowVersion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.FID == card.FFlowVersionId);

        StageConfigEnvelope? activeConfig = null;
        var handledConfigs = new List<StageConfigEnvelope>();
        foreach (var (defId, cfg) in stageConfigs)
        {
            if (viewerIsActiveAssignee && currentStage?.FStageDefinitionId == defId)
            {
                activeConfig = cfg;
            }
            else
            {
                handledConfigs.Add(cfg);
            }
        }

        var redaction = _redactionService.Redact(new CardRedactionRequest
        {
            Card = card,
            CardSchemaJson = flowVersion?.FCardSchemaJson,
            DetailSchemaJson = flowVersion?.FDetailSchemaJson,
            Details = cardDetails,
            ActiveStageConfig = activeConfig,
            HandledStageConfigs = handledConfigs,
            OwnerEditMode = isInitiator && string.Equals(card.FStatus, "draft", StringComparison.OrdinalIgnoreCase)
        });

        detail.DataJson = redaction.RedactedDataJson;
        detail.InitialDataJson = redaction.RedactedInitialDataJson;
        detail.Details = redaction.RedactedDetails.Select(row => new CardDetailRowDto
        {
            Id = row.Id,
            DetailTableKey = string.IsNullOrWhiteSpace(row.DetailTableKey) ? "default" : row.DetailTableKey,
            SortOrder = row.SortOrder,
            DataJson = row.DataJson
        }).ToList();

        // 仅当前 active 处理人查看 v2 节点时，补节点工作视图（sections/components/actions）
        if (viewerIsActiveAssignee && currentStage?.FStageDefinitionId != null)
        {
            var stageDefinition = await _dbContext.Set<CfStageDefinition>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.FID == currentStage.FStageDefinitionId.Value);
            var normalizedConfig = _stageConfigParser.Parse(stageDefinition?.FInputFieldsJson);
            if (stageDefinition != null && normalizedConfig.Version == 2)
            {
                var presentationRelations = await LoadPresentationRelationsAsync(card.FID);
                var presentationSnapshots = await LoadPresentationSnapshotsAsync(card, stageInstances);
                var resolved = _stageViewResolver.Resolve(
                    flowVersion?.FCardSchemaJson,
                    flowVersion?.FDetailSchemaJson,
                    stageDefinition,
                    card,
                    cardDetails,
                    userId,
                    normalizedConfig,
                    presentationRelations,
                    presentationSnapshots);
                detail.CurrentStageWorkView = ToStageWorkViewDto(resolved);
            }
        }

        return detail;
    }

    private async Task<List<CardPresentationRelation>> LoadPresentationRelationsAsync(long cardId)
    {
        var rows = await (
            from relation in _dbContext.Set<CfCardRelation>().AsNoTracking()
            where relation.FSourceCardId == cardId || relation.FTargetCardId == cardId
            join sourceCard in _dbContext.Set<CfCard>().AsNoTracking()
                on relation.FSourceCardId equals sourceCard.FID
            join targetCard in _dbContext.Set<CfCard>().AsNoTracking()
                on relation.FTargetCardId equals targetCard.FID
            orderby relation.FCreatedTime, relation.FID
            select new
            {
                relation.FID,
                relation.FSourceCardId,
                SourceCardNumber = sourceCard.FCardNumber,
                SourceCardTitle = sourceCard.FTitle,
                relation.FTargetCardId,
                TargetCardNumber = targetCard.FCardNumber,
                TargetCardTitle = targetCard.FTitle,
                relation.FRelationType,
                relation.FDescription,
                relation.FOffsetAmount,
                relation.FSnapshotDataJson
            }).ToListAsync();

        return rows.Select(row => new CardPresentationRelation
        {
            Id = row.FID,
            SourceCardId = row.FSourceCardId,
            SourceCardNumber = row.SourceCardNumber,
            SourceCardTitle = row.SourceCardTitle,
            TargetCardId = row.FTargetCardId,
            TargetCardNumber = row.TargetCardNumber,
            TargetCardTitle = row.TargetCardTitle,
            RelationType = row.FRelationType,
            Description = row.FDescription,
            OffsetAmount = row.FOffsetAmount,
            SnapshotData = ParseJsonObject(row.FSnapshotDataJson)
        }).ToList();
    }

    private async Task<List<CardPresentationSnapshot>> LoadPresentationSnapshotsAsync(
        CfCard card,
        IReadOnlyCollection<CfStageInstance> stageInstances)
    {
        var snapshots = new List<CardPresentationSnapshot>();
        var routeSnapshots = await _dbContext.Set<CfRouteDecisionSnapshot>()
            .AsNoTracking()
            .Where(snapshot => snapshot.FCardId == card.FID && snapshot.FRound == card.FCurrentRound)
            .OrderBy(snapshot => snapshot.FDecisionTime)
            .ThenBy(snapshot => snapshot.FID)
            .ToListAsync();

        snapshots.AddRange(routeSnapshots.Select(snapshot =>
        {
            var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceStageInstanceId"] = snapshot.FSourceStageInstanceId,
                ["fromStageDefinitionId"] = snapshot.FFromStageDefinitionId,
                ["fromStageKey"] = snapshot.FFromStageKey,
                ["routeRuleId"] = snapshot.FSelectedRouteRuleId,
                ["edgeKey"] = snapshot.FSelectedEdgeKey,
                ["toStageDefinitionId"] = snapshot.FToStageDefinitionId,
                ["toStageKey"] = snapshot.FToStageKey,
                ["operatorId"] = snapshot.FOperatorId,
                ["decisionTime"] = snapshot.FDecisionTime
            };
            metadata["candidateResults"] = ParseJsonValue(snapshot.FCandidateResultsJson);
            metadata["decisionSnapshot"] = ParseJsonValue(snapshot.FDecisionSnapshotJson);

            return new CardPresentationSnapshot
            {
                SnapshotType = "routeDecision",
                Title = string.IsNullOrWhiteSpace(snapshot.FSelectedEdgeKey) ? "条件流转" : snapshot.FSelectedEdgeKey,
                Reason = string.IsNullOrWhiteSpace(snapshot.FReason) ? "已完成条件流转决策" : snapshot.FReason!,
                Metadata = metadata
            };
        }));

        snapshots.AddRange(stageInstances
            .Where(stage => stage.FIsDynamicInsert && stage.FRound == card.FCurrentRound)
            .OrderBy(stage => stage.FActivatedTime ?? stage.FStartTime ?? DateTime.MinValue)
            .ThenBy(stage => stage.FID)
            .Select(stage =>
            {
                var metadata = ParseJsonObject(stage.FInsertContextJson);
                metadata["stageInstanceId"] = stage.FID;
                metadata["stageDefinitionId"] = stage.FStageDefinitionId;
                metadata["stageName"] = stage.FStageName;
                metadata["approvalMode"] = stage.FApprovalMode;
                metadata["status"] = stage.FStatus;
                metadata["activatedTime"] = stage.FActivatedTime;
                metadata["sourceStageInstanceId"] = metadata.TryGetValue("sourceStageInstanceId", out var sourceStageInstanceId)
                    ? sourceStageInstanceId
                    : stage.FInsertSourceStageId;

                var policyName = ReadString(metadata, "policyName");
                var reason = ReadString(metadata, "reason");
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = string.IsNullOrWhiteSpace(policyName)
                        ? "运行时插入审批节点"
                        : $"命中动态审批策略：{policyName}";
                }

                return new CardPresentationSnapshot
                {
                    SnapshotType = "dynamicApprover",
                    Title = string.IsNullOrWhiteSpace(policyName) ? stage.FStageName : policyName,
                    Reason = reason,
                    Metadata = metadata
                };
            }));

        return snapshots;
    }

    private async Task<List<CardFlowRuntimeAuditDto>> LoadRuntimeAuditTrailAsync(
        CfCard card,
        IReadOnlyCollection<CfStageInstance> stageInstances,
        IReadOnlyCollection<CfStageAssignee> assignees)
    {
        var stageMap = stageInstances.ToDictionary(stage => stage.FID);
        var assigneeMap = assignees
            .GroupBy(assignee => assignee.FStageInstanceId)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.FSortOrder).ThenBy(x => x.FID).ToList());
        var trail = new List<CardFlowRuntimeAuditDto>();

        var routeSnapshots = await _dbContext.Set<CfRouteDecisionSnapshot>()
            .AsNoTracking()
            .Where(snapshot => snapshot.FCardId == card.FID)
            .OrderBy(snapshot => snapshot.FDecisionTime)
            .ThenBy(snapshot => snapshot.FID)
            .ToListAsync();

        foreach (var snapshot in routeSnapshots)
        {
            stageMap.TryGetValue(snapshot.FSourceStageInstanceId, out var stage);
            var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceStageInstanceId"] = snapshot.FSourceStageInstanceId,
                ["fromStageDefinitionId"] = snapshot.FFromStageDefinitionId,
                ["fromStageKey"] = snapshot.FFromStageKey,
                ["routeRuleId"] = snapshot.FSelectedRouteRuleId,
                ["edgeKey"] = snapshot.FSelectedEdgeKey,
                ["toStageDefinitionId"] = snapshot.FToStageDefinitionId,
                ["toStageKey"] = snapshot.FToStageKey
                // 决策快照值映射（decisionSnapshot）与候选结果（candidateResults，含可能内嵌值的自由文本 explanation）
                // 不下发：写入期仅按字段名启发式打码，漏掉 schema 声明的敏感字段（如金额），而 AuditTrail 对任意通过访问门者可见。
                // 前端 CardTimeline 不渲染二者，仅用路由结构元数据（edgeKey 等）。详见脱敏链重构 spec §9.2。
            };

            trail.Add(new CardFlowRuntimeAuditDto
            {
                Id = snapshot.FID,
                SnapshotType = "routeDecision",
                CardId = snapshot.FCardId,
                Round = snapshot.FRound,
                StageInstanceId = snapshot.FSourceStageInstanceId,
                StageName = stage?.FStageName,
                StageKey = snapshot.FFromStageKey,
                EdgeKey = snapshot.FSelectedEdgeKey,
                Title = string.IsNullOrWhiteSpace(snapshot.FSelectedEdgeKey)
                    ? "条件流转"
                    : $"条件流转：{snapshot.FSelectedEdgeKey}",
                Reason = string.IsNullOrWhiteSpace(snapshot.FReason) ? "已完成条件流转决策" : snapshot.FReason!,
                EventTime = snapshot.FDecisionTime,
                Metadata = metadata
            });
        }

        foreach (var stage in stageInstances
            .Where(stage => stage.FIsDynamicInsert)
            .OrderBy(stage => stage.FActivatedTime ?? stage.FStartTime ?? stage.FCompletedTime ?? DateTime.MinValue)
            .ThenBy(stage => stage.FID))
        {
            var context = ParseJsonObject(stage.FInsertContextJson);
            var metadata = SanitizeDynamicInsertContext(context);
            metadata["stageInstanceId"] = stage.FID;
            metadata["stageDefinitionId"] = stage.FStageDefinitionId;
            metadata["stageName"] = stage.FStageName;
            metadata["status"] = stage.FStatus;
            metadata["sourceStageInstanceId"] = metadata.TryGetValue("sourceStageInstanceId", out var sourceStageId)
                ? sourceStageId
                : stage.FInsertSourceStageId;

            if (assigneeMap.TryGetValue(stage.FID, out var stageAssignees))
            {
                metadata["handlerIds"] = stageAssignees.Select(assignee => assignee.FUserId).ToList();
                metadata["handlerNames"] = stageAssignees.Select(assignee => assignee.FUserName).ToList();
            }

            var policyName = ReadString(metadata, "policyName");
            var policyKey = ReadString(metadata, "policyKey");
            var reason = ReadString(metadata, "reason");
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = string.IsNullOrWhiteSpace(policyName)
                    ? "运行时插入审批节点"
                    : $"命中动态审批策略：{policyName}";
            }

            trail.Add(new CardFlowRuntimeAuditDto
            {
                Id = stage.FID,
                SnapshotType = "dynamicApprover",
                CardId = stage.FCardId,
                Round = stage.FRound,
                StageInstanceId = stage.FID,
                StageName = stage.FStageName,
                StageKey = ReadString(metadata, "sourceStageKey"),
                EdgeKey = ReadString(metadata, "selectedRouteEdgeKey"),
                PolicyKey = policyKey,
                Title = string.IsNullOrWhiteSpace(policyName) ? stage.FStageName : policyName,
                Reason = reason!,
                EventTime = stage.FActivatedTime ?? stage.FStartTime ?? stage.FCompletedTime ?? DateTime.MinValue,
                Metadata = metadata
            });
        }

        return trail
            .OrderBy(item => item.EventTime)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private static Dictionary<string, object?> SanitizeDynamicInsertContext(IReadOnlyDictionary<string, object?> context)
    {
        var safe = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[]
        {
            "insertMode",
            "sourceStageInstanceId",
            "sourceWasComplete",
            "policyKey",
            "policyName",
            "sourceStageKey",
            "strategyType",
            "triggerTiming",
            "insertPosition",
            "continuationStageKey",
            "selectedRouteEdgeKey",
            "reason"
        })
        {
            if (context.TryGetValue(key, out var value))
            {
                safe[key] = value;
            }
        }

        var approverIds = ReadApproverIds(context);
        if (approverIds.Count > 0)
        {
            safe["approverIds"] = approverIds;
        }

        var fallbackType = ReadFallbackType(context);
        if (!string.IsNullOrWhiteSpace(fallbackType))
        {
            safe["fallbackType"] = fallbackType;
        }

        return safe;
    }

    private static List<long> ReadApproverIds(IReadOnlyDictionary<string, object?> context)
    {
        var ids = new List<long>();
        if (context.TryGetValue("approverIds", out var directIds) && directIds is IEnumerable<object?> directList)
        {
            ids.AddRange(directList.Select(TryToLong).Where(value => value.HasValue).Select(value => value!.Value));
        }

        if (context.TryGetValue("approvers", out var approvers) && approvers is IEnumerable<object?> approverList)
        {
            foreach (var approver in approverList)
            {
                if (approver is IReadOnlyDictionary<string, object?> data)
                {
                    var id = data.TryGetValue("UserId", out var pascalValue)
                        ? TryToLong(pascalValue)
                        : data.TryGetValue("userId", out var camelValue)
                            ? TryToLong(camelValue)
                            : null;
                    if (id.HasValue)
                    {
                        ids.Add(id.Value);
                    }
                }
            }
        }

        return ids.Distinct().ToList();
    }

    private static string? ReadFallbackType(IReadOnlyDictionary<string, object?> context)
    {
        if (!context.TryGetValue("fallback", out var fallback) || fallback == null)
        {
            return null;
        }

        var fallbackObject = fallback switch
        {
            string text => ParseJsonObject(text),
            IReadOnlyDictionary<string, object?> data => data,
            _ => null
        };

        if (fallbackObject == null)
        {
            return null;
        }

        if (fallbackObject.TryGetValue("type", out var type))
        {
            return type?.ToString();
        }

        if (fallbackObject.TryGetValue("fallback", out var nested) && nested is IReadOnlyDictionary<string, object?> nestedData)
        {
            return nestedData.TryGetValue("type", out var nestedType) ? nestedType?.ToString() : null;
        }

        return null;
    }

    private static long? TryToLong(object? value)
    {
        return value switch
        {
            null => null,
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => (long)decimalValue,
            string text when long.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static Dictionary<string, object?> ParseJsonObject(string? json)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = ConvertJsonElement(property.Value);
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    private static object? ParseJsonValue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ConvertJsonElement(document.RootElement);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> metadata, string key)
        => metadata.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static CardDetailRowDto ToCardDetailRowDto(CfCardDetail detail)
    {
        return new CardDetailRowDto
        {
            Id = detail.FID,
            DetailTableKey = string.IsNullOrWhiteSpace(detail.FDetailTableKey) ? "default" : detail.FDetailTableKey,
            SortOrder = detail.FSortOrder,
            DataJson = detail.FDataJson
        };
    }

    private static StageWorkViewDto ToStageWorkViewDto(StageViewResolutionResult resolved)
    {
        return new StageWorkViewDto
        {
            Sections = resolved.Sections.Select(section => new StageViewSectionDto
            {
                Key = section.Key,
                Title = section.Title,
                Type = section.Type,
                Fields = section.Fields.Select(field => new StageViewFieldDto
                {
                    FieldKey = field.FieldKey,
                    Label = field.Label
                }).ToList()
            }).ToList(),
            FieldAccess = resolved.FieldAccess.ToDictionary(
                pair => pair.Key,
                pair => new StageFieldAccessDto
                {
                    Access = pair.Value.Access,
                    Required = pair.Value.Required,
                    MaskPattern = pair.Value.MaskPattern
                }),
            DetailAccess = resolved.DetailAccess.ToDictionary(
                pair => pair.Key,
                pair => new StageDetailAccessDto
                {
                    Access = pair.Value.Access,
                    Required = pair.Value.Required,
                    MaskPattern = pair.Value.MaskPattern
                }),
            Components = resolved.Presentation.Components,
            DetailSummary = resolved.Presentation.DetailSummary,
            ActionPolicy = new StageActionPolicyDto
            {
                AllowedActions = resolved.AllowedActions
            },
            Summary = resolved.Summary == null
                ? null
                : new StageSummaryProfileDto { Fields = resolved.Summary.Fields }
        };
    }

    public async Task<CardDetailDto> CreateAsync(CreateCardRequest request, long userId)
    {
        var flowDef = await _dbContext.Set<CfFlowDefinition>().FirstOrDefaultAsync(x => x.FID == request.FlowDefinitionId)
            ?? throw new InvalidOperationException("流程定义不存在");

        var currentVersion = await _dbContext.Set<CfFlowVersion>()
            .FirstOrDefaultAsync(x => x.FFlowDefinitionId == request.FlowDefinitionId && x.FIsCurrentVersion)
            ?? throw new InvalidOperationException("没有可用的流程版本");

        var sourceVerification = await _sourceContextVerifier.VerifyAsync(request);
        if (!string.IsNullOrWhiteSpace(sourceVerification.ErrorMessage))
            throw new InvalidOperationException(sourceVerification.ErrorMessage);

        var card = new CfCard
        {
            FFlowDefinitionId = request.FlowDefinitionId,
            FFlowVersionId = currentVersion.FID,
            FStatus = "draft",
            FInitiatorId = userId,
            FInitiatorName = string.Empty,
            FDataJson = MergeTrustedDataJson(request.DataJson, sourceVerification.TrustedDataJson),
            FOrgId = request.OrgId,
            FSourceModule = NormalizeSourceContext(request.SourceModule),
            FSourceType = NormalizeSourceContext(request.SourceType),
            FSourceId = request.SourceId,
            FReturnUrl = NormalizeReturnUrl(request.ReturnUrl),
            FInitialDataJson = sourceVerification.StoredInitialDataJson,
            FSourceTitle = NormalizeSourceTitle(request.SourceTitle),
            FCreatedTime = DateTime.Now,
            FCurrentRound = 1
        };

        _dbContext.Set<CfCard>().Add(card);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Created card {CardId} for flow {FlowId} by user {UserId}", card.FID, request.FlowDefinitionId, userId);

        return (await GetByIdAsync(card.FID, userId))!;
    }

    public async Task<CardDetailDto> UpdateAsync(long id, UpdateCardRequest request, long userId)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(x => x.FID == id)
            ?? throw new InvalidOperationException("卡片不存在");

        if (card.FStatus != "draft")
            throw new InvalidOperationException("只能编辑草稿状态的卡片");

        // 乐观锁检查
        if (!string.IsNullOrEmpty(request.ConcurrencyStamp))
        {
            var expected = Convert.FromBase64String(request.ConcurrencyStamp);
            if (!expected.SequenceEqual(card.FRowVersion))
                throw new DbUpdateConcurrencyException("数据已被修改，请刷新后重试");
        }

        if (request.DataJson != null)
            card.FDataJson = request.DataJson;

        // 明细持久化（全量替换策略：清空旧明细后插入新明细）
        if (request.Details != null)
        {
            var oldDetails = await _dbContext.Set<CfCardDetail>()
                .Where(d => d.FCardId == id)
                .ToListAsync();
            if (oldDetails.Count > 0)
                _dbContext.Set<CfCardDetail>().RemoveRange(oldDetails);

            foreach (var item in request.Details)
            {
                _dbContext.Set<CfCardDetail>().Add(new CfCardDetail
                {
                    FCardId = id,
                    FDetailTableKey = string.IsNullOrWhiteSpace(item.DetailTableKey) ? "default" : item.DetailTableKey,
                    FSortOrder = item.SortOrder,
                    FDataJson = item.DataJson,
                    FCreatedTime = DateTime.Now
                });
            }

            // 自动汇总 amount 字段：累加每行明细 dataJson 中的 amount，写回卡片 dataJson
            decimal totalAmount = 0m;
            foreach (var item in request.Details)
            {
                if (string.IsNullOrWhiteSpace(item.DataJson)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(item.DataJson);
                    if (doc.RootElement.TryGetProperty("amount", out var amtEl))
                    {
                        if (amtEl.ValueKind == JsonValueKind.Number && amtEl.TryGetDecimal(out var d))
                            totalAmount += d;
                        else if (amtEl.ValueKind == JsonValueKind.String && decimal.TryParse(amtEl.GetString(), out var ds))
                            totalAmount += ds;
                    }
                }
                catch { /* 忽略无效 JSON */ }
            }

            // 将 totalAmount 写入 card.FDataJson 的 amount 字段
            JsonNode? rootNode;
            try
            {
                rootNode = string.IsNullOrWhiteSpace(card.FDataJson)
                    ? new JsonObject()
                    : JsonNode.Parse(card.FDataJson) ?? new JsonObject();
            }
            catch { rootNode = new JsonObject(); }

            if (rootNode is JsonObject obj)
            {
                obj["amount"] = totalAmount;
                card.FDataJson = obj.ToJsonString();
            }
        }

        card.FUpdatedTime = DateTime.Now;
        // 显式标记修改，避免在某些跟踪边界场景下未生成 UPDATE
        _dbContext.Entry(card).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();

        return (await GetByIdAsync(id, userId))!;
    }

    public async Task DeleteAsync(long id, long userId)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(x => x.FID == id)
            ?? throw new InvalidOperationException("卡片不存在");

        if (card.FStatus != "draft")
            throw new InvalidOperationException("只能删除草稿状态的卡片");

        _dbContext.Set<CfCard>().Remove(card);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Deleted card {CardId} by user {UserId}", id, userId);
    }

    public async Task<List<CardListDto>> GetAvailablePrerequisitesAsync(long cardId, long userId)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(x => x.FID == cardId)
            ?? throw new InvalidOperationException("卡片不存在");

        // 同组织下已完成的卡片可作为前置
        return await _dbContext.Set<CfCard>()
            .Where(x => x.FOrgId == card.FOrgId && x.FStatus == "completed" && x.FID != cardId)
            .Join(_dbContext.Set<CfFlowDefinition>(), c => c.FFlowDefinitionId, f => f.FID, (c, f) => new CardListDto
            {
                Id = c.FID,
                CardNumber = c.FCardNumber,
                Title = c.FTitle,
                Status = c.FStatus,
                FlowName = f.FFlowName,
                InitiatorName = c.FInitiatorName,
                CreatedTime = c.FCreatedTime,
                SubmitTime = c.FSubmitTime,
                CompletedTime = c.FCompletedTime,
                SourceModule = c.FSourceModule,
                SourceType = c.FSourceType,
                SourceId = c.FSourceId,
                ReturnUrl = c.FReturnUrl,
                SourceTitle = c.FSourceTitle
            })
            .Take(50)
            .ToListAsync();
    }

    private static string? NormalizeSourceContext(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeReturnUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeSourceTitle(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeInitialDataJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            var node = JsonNode.Parse(value);
            return node?.ToJsonString();
        }
        catch
        {
            return null;
        }
    }

    private static string? MergeTrustedDataJson(string? dataJson, string? trustedDataJson)
    {
        if (string.IsNullOrWhiteSpace(trustedDataJson)) return dataJson;
        if (string.IsNullOrWhiteSpace(dataJson)) return trustedDataJson;

        try
        {
            var dataNode = JsonNode.Parse(dataJson) as JsonObject;
            var trustedNode = JsonNode.Parse(trustedDataJson) as JsonObject;
            if (dataNode == null || trustedNode == null)
                return trustedDataJson;

            var merged = new JsonObject();
            foreach (var property in dataNode)
            {
                merged[property.Key] = property.Value == null ? null : JsonNode.Parse(property.Value.ToJsonString());
            }
            foreach (var property in trustedNode)
            {
                merged[property.Key] = property.Value == null ? null : JsonNode.Parse(property.Value.ToJsonString());
            }

            return merged.ToJsonString();
        }
        catch
        {
            return dataJson;
        }
    }

    public async Task<List<CardBalanceDto>> GetAvailableOffsetsAsync(long cardId, long userId)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(x => x.FID == cardId)
            ?? throw new InvalidOperationException("卡片不存在");

        return await _dbContext.Set<CfCardBalance>()
            .Where(b => b.FOrgId == card.FOrgId && b.FRemainingAmount > 0 && b.FStatus == "active" && b.FCardId != cardId)
            .Join(_dbContext.Set<CfCard>(), b => b.FCardId, c => c.FID, (b, c) => new CardBalanceDto
            {
                Id = b.FID,
                CardId = b.FCardId,
                CardNumber = c.FCardNumber,
                CardTitle = c.FTitle,
                OriginalAmount = b.FOriginalAmount,
                OffsetAmount = b.FOffsetAmount,
                RemainingAmount = b.FRemainingAmount,
                Status = b.FStatus
            })
            .ToListAsync();
    }

    public async Task<List<CardBalanceDto>> GetBalanceAsync(long cardId)
    {
        return await _dbContext.Set<CfCardBalance>()
            .Where(b => b.FCardId == cardId)
            .Join(_dbContext.Set<CfCard>(), b => b.FCardId, c => c.FID, (b, c) => new CardBalanceDto
            {
                Id = b.FID,
                CardId = b.FCardId,
                CardNumber = c.FCardNumber,
                CardTitle = c.FTitle,
                OriginalAmount = b.FOriginalAmount,
                OffsetAmount = b.FOffsetAmount,
                RemainingAmount = b.FRemainingAmount,
                Status = b.FStatus
            })
            .ToListAsync();
    }

    public async Task<CardRelationDto> CreateRelationAsync(long cardId, CreateRelationRequest request, long userId)
    {
        var sourceCard = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(x => x.FID == cardId)
            ?? throw new InvalidOperationException("源卡片不存在");
        var targetCard = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(x => x.FID == request.TargetCardId)
            ?? throw new InvalidOperationException("目标卡片不存在");

        var relation = new CfCardRelation
        {
            FSourceCardId = cardId,
            FTargetCardId = request.TargetCardId,
            FRelationType = request.RelationType,
            FDescription = request.Description,
            FOffsetAmount = request.OffsetAmount,
            FOrgId = sourceCard.FOrgId,
            FCreatedTime = DateTime.Now
        };

        _dbContext.Set<CfCardRelation>().Add(relation);
        await _dbContext.SaveChangesAsync();

        return new CardRelationDto
        {
            Id = relation.FID,
            SourceCardId = relation.FSourceCardId,
            SourceCardNumber = sourceCard.FCardNumber,
            TargetCardId = relation.FTargetCardId,
            TargetCardNumber = targetCard.FCardNumber,
            RelationType = relation.FRelationType,
            Description = relation.FDescription,
            OffsetAmount = relation.FOffsetAmount
        };
    }

    public async Task<List<CardRelationDto>> GetRelationsAsync(long cardId)
    {
        return await _dbContext.Set<CfCardRelation>()
            .Where(r => r.FSourceCardId == cardId || r.FTargetCardId == cardId)
            .Select(r => new CardRelationDto
            {
                Id = r.FID,
                SourceCardId = r.FSourceCardId,
                TargetCardId = r.FTargetCardId,
                RelationType = r.FRelationType,
                Description = r.FDescription,
                OffsetAmount = r.FOffsetAmount
            })
            .ToListAsync();
    }

    public async Task<List<ActionLogDto>> GetLogsAsync(long cardId)
    {
        return await _dbContext.Set<CfActionLog>()
            .Where(l => l.FCardId == cardId)
            .OrderByDescending(l => l.FOperationTime)
            .Select(l => new ActionLogDto
            {
                Id = l.FID,
                ActionType = l.FActionType,
                OperatorName = l.FOperatorName,
                OperationTime = l.FOperationTime,
                Opinion = l.FOpinion,
                DetailJson = l.FDetailJson
            })
            .ToListAsync();
    }

    public async Task<PagedResult<AuditLogItemDto>> SearchLogsAsync(AuditLogQueryRequest request)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : request.PageSize;

        var query = _dbContext.Set<CfActionLog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ActionTypes))
        {
            var types = request.ActionTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (types.Count > 0)
                query = query.Where(l => types.Contains(l.FActionType));
        }

        if (!string.IsNullOrWhiteSpace(request.OperatorName))
        {
            var op = request.OperatorName.Trim();
            query = query.Where(l => l.FOperatorName.Contains(op));
        }

        if (request.StartDate.HasValue)
            query = query.Where(l => l.FOperationTime >= request.StartDate.Value);

        if (request.EndDate.HasValue)
        {
            var endExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(l => l.FOperationTime < endExclusive);
        }

        // 按卡片编号过滤（需 join）
        if (!string.IsNullOrWhiteSpace(request.CardNumber))
        {
            var cn = request.CardNumber.Trim();
            var matchedCardIds = await _dbContext.Set<CfCard>()
                .Where(c => c.FCardNumber != null && c.FCardNumber.Contains(cn))
                .Select(c => c.FID)
                .ToListAsync();
            query = query.Where(l => matchedCardIds.Contains(l.FCardId));
        }

        var total = await query.CountAsync();

        var pageData = await query
            .OrderByDescending(l => l.FOperationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_dbContext.Set<CfCard>(), l => l.FCardId, c => c.FID, (l, c) => new { Log = l, Card = c })
            .Join(_dbContext.Set<CfFlowDefinition>(), x => x.Card.FFlowDefinitionId, f => f.FID, (x, f) => new
            {
                x.Log,
                x.Card,
                FlowName = f.FFlowName
            })
            .GroupJoin(_dbContext.Set<CfStageInstance>(), x => x.Log.FStageInstanceId, s => s.FID,
                (x, sg) => new { x.Log, x.Card, x.FlowName, Stages = sg })
            .SelectMany(x => x.Stages.DefaultIfEmpty(), (x, s) => new AuditLogItemDto
            {
                Id = x.Log.FID,
                CardId = x.Log.FCardId,
                CardNumber = x.Card.FCardNumber,
                CardTitle = x.Card.FTitle,
                FlowName = x.FlowName,
                StageName = s != null ? s.FStageName : null,
                ActionType = x.Log.FActionType,
                OperatorName = x.Log.FOperatorName,
                OperationTime = x.Log.FOperationTime,
                Opinion = x.Log.FOpinion,
                DetailJson = x.Log.FDetailJson
            })
            .ToListAsync();

        return new PagedResult<AuditLogItemDto>
        {
            Items = pageData,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<CardFlowRuntimeMonitoringDto> GetRuntimeMonitoringAsync(CardFlowRuntimeMonitoringRequest request)
    {
        var start = request.StartDate;
        var endExclusive = NormalizeEndExclusive(request.EndDate);

        var cardQuery = _dbContext.Set<CfCard>().AsNoTracking().AsQueryable();
        if (request.OrgId.HasValue)
            cardQuery = cardQuery.Where(card => card.FOrgId == request.OrgId.Value);
        if (request.FlowDefinitionId.HasValue)
            cardQuery = cardQuery.Where(card => card.FFlowDefinitionId == request.FlowDefinitionId.Value);
        if (request.FlowVersionId.HasValue)
            cardQuery = cardQuery.Where(card => card.FFlowVersionId == request.FlowVersionId.Value);

        var routeQuery =
            from snapshot in _dbContext.Set<CfRouteDecisionSnapshot>().AsNoTracking()
            join card in cardQuery on snapshot.FCardId equals card.FID
            join flow in _dbContext.Set<CfFlowDefinition>().AsNoTracking() on card.FFlowDefinitionId equals flow.FID
            select new
            {
                Snapshot = snapshot,
                Card = card,
                flow.FFlowCode
            };
        if (start.HasValue)
            routeQuery = routeQuery.Where(row => row.Snapshot.FDecisionTime >= start.Value);
        if (endExclusive.HasValue)
            routeQuery = routeQuery.Where(row => row.Snapshot.FDecisionTime < endExclusive.Value);

        var routeRows = await routeQuery.ToListAsync();
        var routeRuleIds = routeRows
            .Select(row => row.Snapshot.FSelectedRouteRuleId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var routeRules = routeRuleIds.Count == 0
            ? new Dictionary<long, CfStageRouteRule>()
            : await _dbContext.Set<CfStageRouteRule>()
                .AsNoTracking()
                .Where(rule => routeRuleIds.Contains(rule.FID))
                .ToDictionaryAsync(rule => rule.FID);

        var events = new List<RuntimeMonitoringEvent>();
        foreach (var row in routeRows)
        {
            var routeRule = row.Snapshot.FSelectedRouteRuleId.HasValue
                && routeRules.TryGetValue(row.Snapshot.FSelectedRouteRuleId.Value, out var matchedRule)
                    ? matchedRule
                    : null;
            var fallback = routeRule?.FIsDefault == true
                || (!string.IsNullOrWhiteSpace(row.Snapshot.FReason)
                    && row.Snapshot.FReason.Contains("默认分支", StringComparison.Ordinal));
            events.Add(new RuntimeMonitoringEvent
            {
                FlowCode = row.FFlowCode,
                FlowVersionId = row.Card.FFlowVersionId,
                StageKey = row.Snapshot.FFromStageKey,
                EdgeKey = row.Snapshot.FSelectedEdgeKey,
                SourceModule = row.Card.FSourceModule,
                SourceType = row.Card.FSourceType,
                OrgId = row.Card.FOrgId,
                DateBucket = row.Snapshot.FDecisionTime.Date,
                RouteHitCount = 1,
                FallbackCount = fallback ? 1 : 0,
                RuleWarningCount = CountRouteRuleWarnings(row.Snapshot.FCandidateResultsJson)
            });
        }

        var dynamicQuery =
            from stage in _dbContext.Set<CfStageInstance>().AsNoTracking()
            join card in cardQuery on stage.FCardId equals card.FID
            join flow in _dbContext.Set<CfFlowDefinition>().AsNoTracking() on card.FFlowDefinitionId equals flow.FID
            where stage.FIsDynamicInsert
            select new
            {
                Stage = stage,
                Card = card,
                flow.FFlowCode
            };
        if (start.HasValue)
            dynamicQuery = dynamicQuery.Where(row => (row.Stage.FActivatedTime ?? row.Stage.FStartTime ?? row.Stage.FCompletedTime ?? DateTime.MinValue) >= start.Value);
        if (endExclusive.HasValue)
            dynamicQuery = dynamicQuery.Where(row => (row.Stage.FActivatedTime ?? row.Stage.FStartTime ?? row.Stage.FCompletedTime ?? DateTime.MinValue) < endExclusive.Value);

        var dynamicRows = await dynamicQuery.ToListAsync();
        foreach (var row in dynamicRows)
        {
            var context = SanitizeDynamicInsertContext(ParseJsonObject(row.Stage.FInsertContextJson));
            events.Add(new RuntimeMonitoringEvent
            {
                FlowCode = row.FFlowCode,
                FlowVersionId = row.Card.FFlowVersionId,
                StageKey = ReadString(context, "sourceStageKey"),
                EdgeKey = ReadString(context, "selectedRouteEdgeKey"),
                PolicyKey = ReadString(context, "policyKey"),
                SourceModule = row.Card.FSourceModule,
                SourceType = row.Card.FSourceType,
                OrgId = row.Card.FOrgId,
                DateBucket = (row.Stage.FActivatedTime ?? row.Stage.FStartTime ?? row.Stage.FCompletedTime ?? DateTime.Now).Date,
                DynamicInsertCount = 1
            });
        }

        var unresolvedQuery =
            from log in _dbContext.Set<CfActionLog>().AsNoTracking()
            join card in cardQuery on log.FCardId equals card.FID
            join flow in _dbContext.Set<CfFlowDefinition>().AsNoTracking() on card.FFlowDefinitionId equals flow.FID
            join stage in _dbContext.Set<CfStageInstance>().AsNoTracking() on log.FStageInstanceId equals stage.FID into stageGroup
            from stage in stageGroup.DefaultIfEmpty()
            where log.FActionType == "handler-unresolved"
                || log.FActionType == "dynamic-handler-unresolved"
                || ((log.FOpinion ?? string.Empty).Contains("未解析到"))
                || ((log.FDetailJson ?? string.Empty).Contains("未解析到"))
            select new
            {
                Log = log,
                Card = card,
                Stage = stage,
                flow.FFlowCode
            };
        if (start.HasValue)
            unresolvedQuery = unresolvedQuery.Where(row => row.Log.FOperationTime >= start.Value);
        if (endExclusive.HasValue)
            unresolvedQuery = unresolvedQuery.Where(row => row.Log.FOperationTime < endExclusive.Value);

        var unresolvedRows = await unresolvedQuery.ToListAsync();
        foreach (var row in unresolvedRows)
        {
            events.Add(new RuntimeMonitoringEvent
            {
                FlowCode = row.FFlowCode,
                FlowVersionId = row.Card.FFlowVersionId,
                StageKey = row.Stage?.FStageName,
                SourceModule = row.Card.FSourceModule,
                SourceType = row.Card.FSourceType,
                OrgId = row.Card.FOrgId,
                DateBucket = row.Log.FOperationTime.Date,
                HandlerUnresolvedCount = 1
            });
        }

        return BuildMonitoringBuckets(events);
    }

    private static CardFlowRuntimeMonitoringDto BuildMonitoringBuckets(IReadOnlyCollection<RuntimeMonitoringEvent> events)
    {
        var buckets = events
            .GroupBy(row => new
            {
                row.FlowCode,
                row.FlowVersionId,
                StageKey = row.StageKey ?? string.Empty,
                EdgeKey = row.EdgeKey ?? string.Empty,
                PolicyKey = row.PolicyKey ?? string.Empty,
                SourceModule = row.SourceModule ?? string.Empty,
                SourceType = row.SourceType ?? string.Empty,
                row.OrgId,
                row.DateBucket
            })
            .Select(group => new CardFlowRuntimeMonitoringBucketDto
            {
                FlowCode = group.Key.FlowCode,
                FlowVersionId = group.Key.FlowVersionId,
                StageKey = string.IsNullOrWhiteSpace(group.Key.StageKey) ? null : group.Key.StageKey,
                EdgeKey = string.IsNullOrWhiteSpace(group.Key.EdgeKey) ? null : group.Key.EdgeKey,
                PolicyKey = string.IsNullOrWhiteSpace(group.Key.PolicyKey) ? null : group.Key.PolicyKey,
                SourceModule = string.IsNullOrWhiteSpace(group.Key.SourceModule) ? null : group.Key.SourceModule,
                SourceType = string.IsNullOrWhiteSpace(group.Key.SourceType) ? null : group.Key.SourceType,
                OrgId = group.Key.OrgId,
                DateBucket = group.Key.DateBucket.ToString("yyyy-MM-dd"),
                RouteHitCount = group.Sum(row => row.RouteHitCount),
                FallbackCount = group.Sum(row => row.FallbackCount),
                HandlerUnresolvedCount = group.Sum(row => row.HandlerUnresolvedCount),
                DynamicInsertCount = group.Sum(row => row.DynamicInsertCount),
                RuleWarningCount = group.Sum(row => row.RuleWarningCount)
            })
            .OrderByDescending(bucket => bucket.DateBucket)
            .ThenBy(bucket => bucket.FlowCode)
            .ThenBy(bucket => bucket.StageKey)
            .ThenBy(bucket => bucket.EdgeKey)
            .ThenBy(bucket => bucket.PolicyKey)
            .ToList();

        return new CardFlowRuntimeMonitoringDto
        {
            RouteHitCount = buckets.Sum(bucket => bucket.RouteHitCount),
            FallbackCount = buckets.Sum(bucket => bucket.FallbackCount),
            HandlerUnresolvedCount = buckets.Sum(bucket => bucket.HandlerUnresolvedCount),
            DynamicInsertCount = buckets.Sum(bucket => bucket.DynamicInsertCount),
            RuleWarningCount = buckets.Sum(bucket => bucket.RuleWarningCount),
            Buckets = buckets
        };
    }

    private static int CountRouteRuleWarnings(string? candidateResultsJson)
    {
        if (ParseJsonValue(candidateResultsJson) is not IEnumerable<object?> candidates)
        {
            return 0;
        }

        return candidates.Count(candidate =>
        {
            if (candidate is not IReadOnlyDictionary<string, object?> data)
            {
                return false;
            }

            return data.TryGetValue("TypeErrors", out var pascalErrors) && HasAnyItems(pascalErrors)
                || data.TryGetValue("typeErrors", out var camelErrors) && HasAnyItems(camelErrors);
        });
    }

    private static bool HasAnyItems(object? value)
    {
        return value switch
        {
            IEnumerable<object?> items => items.Any(),
            string text => !string.IsNullOrWhiteSpace(text),
            _ => false
        };
    }

    private static DateTime? NormalizeEndExclusive(DateTime? endDate)
    {
        if (!endDate.HasValue)
        {
            return null;
        }

        return endDate.Value.TimeOfDay == TimeSpan.Zero
            ? endDate.Value.Date.AddDays(1)
            : endDate.Value;
    }

    public async Task RetryPushAsync(long cardId, long operatorId)
    {
        var todoItems = await _dbContext.Set<CfTodoItem>()
            .Where(t => t.FCardId == cardId && t.FPushStatus == "failed")
            .ToListAsync();

        foreach (var item in todoItems)
            item.FPushStatus = "pending";

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Retry push for card {CardId} by operator {OperatorId}, {Count} items", cardId, operatorId, todoItems.Count);
    }

    public async Task<string?> GetRelationSnapshotAsync(long cardId, long relationId)
    {
        var relation = await _dbContext.Set<CfCardRelation>()
            .FirstOrDefaultAsync(r => r.FID == relationId && (r.FSourceCardId == cardId || r.FTargetCardId == cardId));
        return relation?.FSnapshotDataJson;
    }

    private sealed class RuntimeMonitoringEvent
    {
        public string FlowCode { get; init; } = string.Empty;
        public long FlowVersionId { get; init; }
        public string? StageKey { get; init; }
        public string? EdgeKey { get; init; }
        public string? PolicyKey { get; init; }
        public string? SourceModule { get; init; }
        public string? SourceType { get; init; }
        public long OrgId { get; init; }
        public DateTime DateBucket { get; init; }
        public int RouteHitCount { get; init; }
        public int FallbackCount { get; init; }
        public int HandlerUnresolvedCount { get; init; }
        public int DynamicInsertCount { get; init; }
        public int RuleWarningCount { get; init; }
    }
}
