using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 卡片流程编排引擎主服务：负责编排实例的生命周期管理、节点调度与汇聚评估。
/// </summary>
public class OrchestrationEngineService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<OrchestrationEngineService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrchestrationEngineService(STOTOPDbContext db, ILogger<OrchestrationEngineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // =====================================================================
    // 核心方法
    // =====================================================================

    /// <summary>
    /// 创建编排实例：快照模板、初始化节点、启动 start 节点。
    /// </summary>
    public async Task<long> StartAsync(long templateId, long initiatorId, JsonElement? inputData)
    {
        var template = await _db.Set<CfOrchestrationTemplate>()
            .FirstOrDefaultAsync(t => t.FID == templateId)
            ?? throw new InvalidOperationException($"编排模板 {templateId} 不存在");

        if (template.FStatus != "published")
        {
            throw new InvalidOperationException($"编排模板 {templateId} 状态为 {template.FStatus}，必须为 published 才能启动");
        }

        var nodes = ParseNodes(template.FNodesJson);
        var edges = ParseEdges(template.FEdgesJson);

        // 运行时兜底：拓扑排序校验 DAG 无环
        if (!ValidateDag(template.FNodesJson ?? "{\"nodes\":[]}", template.FEdgesJson ?? "{\"edges\":[]}"))
        {
            throw new InvalidOperationException($"编排模板 {templateId} 包含循环，无法启动");
        }

        var startNode = nodes.FirstOrDefault(n => string.Equals(n.Type, "start", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"编排模板 {templateId} 缺少 start 节点");

        var now = DateTime.Now;
        var instance = new CfOrchestrationInstance
        {
            FTemplateId = templateId,
            FOrgId = template.FOrgId,
            FStatus = "running",
            FSnapshotNodesJson = template.FNodesJson,
            FSnapshotEdgesJson = template.FEdgesJson,
            FContextJson = inputData.HasValue ? inputData.Value.GetRawText() : null,
            FTriggerCount = 0,
            FInitiatorId = initiatorId,
            FInitiatedTime = now
        };
        _db.Set<CfOrchestrationInstance>().Add(instance);
        await _db.SaveChangesAsync();

        // 为每个 DAG 节点创建节点实例
        foreach (var node in nodes)
        {
            var nodeInstance = new CfOrchestrationNodeInstance
            {
                FOrchestrationInstanceId = instance.FID,
                FNodeId = node.Id,
                FStatus = "pending"
            };

            // start 节点直接置为 completed
            if (string.Equals(node.Type, "start", StringComparison.OrdinalIgnoreCase))
            {
                nodeInstance.FStatus = "completed";
                nodeInstance.FStartTime = now;
                nodeInstance.FCompletedTime = now;
                nodeInstance.FResultJson = inputData.HasValue ? inputData.Value.GetRawText() : null;
            }

            _db.Set<CfOrchestrationNodeInstance>().Add(nodeInstance);
        }
        await _db.SaveChangesAsync();

        _logger.LogInformation("编排实例 {InstanceId} 已创建（模板 {TemplateId}），开始评估 start 节点 {StartNodeId}",
            instance.FID, templateId, startNode.Id);

        await EvaluateAndDispatchAsync(instance.FID, startNode.Id);

        return instance.FID;
    }

    /// <summary>
    /// 卡片流程完成时回调入口（由 FlowEngineService 在终态变更后调用）。
    /// </summary>
    public async Task OnFlowCompletedAsync(long orchestrationInstanceId, string nodeId, long cardId,
                                           string endStatus, JsonElement? resultData)
    {
        // 使用 UPDLOCK 行级锁防止并发竞态
        var locked = await _db.Set<CfOrchestrationNodeInstance>()
            .FromSqlRaw(
                "SELECT * FROM [CF编排节点实例] WITH (UPDLOCK, ROWLOCK) WHERE [F编排实例ID] = {0} AND [F节点ID] = {1}",
                orchestrationInstanceId, nodeId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (locked == null)
        {
            _logger.LogWarning("OnFlowCompleted: 找不到节点实例 instance={InstanceId} node={NodeId}",
                orchestrationInstanceId, nodeId);
            return;
        }

        // 幂等：节点已为终态则直接返回
        if (locked.FStatus is "completed" or "skipped" or "failed")
        {
            _logger.LogInformation("节点 {NodeId} 已为终态 {Status}，跳过 OnFlowCompleted", nodeId, locked.FStatus);
            return;
        }

        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == orchestrationInstanceId);
        if (instance == null)
        {
            _logger.LogWarning("OnFlowCompleted: 编排实例 {InstanceId} 不存在", orchestrationInstanceId);
            return;
        }

        if (instance.FStatus is "cancelled" or "failed")
        {
            _logger.LogInformation("编排实例 {InstanceId} 状态为 {Status}，忽略节点 {NodeId} 的完成回调",
                orchestrationInstanceId, instance.FStatus, nodeId);
            return;
        }

        // 取追踪态实体进行更新
        var nodeInstance = await _db.Set<CfOrchestrationNodeInstance>()
            .FirstAsync(n => n.FID == locked.FID);

        nodeInstance.FStatus = "completed";
        nodeInstance.FEndStatusType = endStatus;
        nodeInstance.FResultJson = resultData.HasValue ? resultData.Value.GetRawText() : null;
        nodeInstance.FCompletedTime = DateTime.Now;
        if (nodeInstance.FRelatedCardId == null)
        {
            nodeInstance.FRelatedCardId = cardId;
        }

        // 写入派发记录（源节点完成）
        _db.Set<CfDispatchRecord>().Add(new CfDispatchRecord
        {
            FOrchestrationInstanceId = orchestrationInstanceId,
            FDispatchType = "auto",
            FSourceNodeId = nodeId,
            FSourceCardId = cardId,
            FStatus = "triggered",
            FCreatedTime = DateTime.Now,
            FTriggeredTime = DateTime.Now,
            FDataPayloadJson = resultData.HasValue ? resultData.Value.GetRawText() : null
        });

        await _db.SaveChangesAsync();

        // 暂停态：仅登记节点完成，不触发后续调度
        if (instance.FStatus == "paused")
        {
            _logger.LogInformation("编排实例 {InstanceId} 处于 paused 状态，节点 {NodeId} 完成但不触发后续调度",
                orchestrationInstanceId, nodeId);
            return;
        }

        await EvaluateAndDispatchAsync(orchestrationInstanceId, nodeId);
    }

    /// <summary>
    /// 批次完成时的回调入口（批次模式专用）
    /// </summary>
    public async Task OnBatchCompletedAsync(long orchestrationInstanceId, string nodeId, long batchId, JsonElement? resultData)
    {
        // 使用 UPDLOCK 行级锁防止并发竞态
        var locked = await _db.Set<CfOrchestrationNodeInstance>()
            .FromSqlRaw(
                "SELECT * FROM [CF编排节点实例] WITH (UPDLOCK, ROWLOCK) WHERE [F编排实例ID] = {0} AND [F节点ID] = {1}",
                orchestrationInstanceId, nodeId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (locked == null)
        {
            _logger.LogWarning("OnBatchCompleted: 找不到节点实例 instance={InstanceId} node={NodeId}",
                orchestrationInstanceId, nodeId);
            return;
        }

        // 幂等：节点已为终态则直接返回
        if (locked.FStatus is "completed" or "skipped" or "failed")
        {
            _logger.LogInformation("节点 {NodeId} 已为终态 {Status}，跳过 OnBatchCompleted", nodeId, locked.FStatus);
            return;
        }

        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == orchestrationInstanceId);
        if (instance == null)
        {
            _logger.LogWarning("OnBatchCompleted: 编排实例 {InstanceId} 不存在", orchestrationInstanceId);
            return;
        }

        if (instance.FStatus is "cancelled" or "failed")
        {
            _logger.LogInformation("编排实例 {InstanceId} 状态为 {Status}，忽略节点 {NodeId} 的批次完成回调",
                orchestrationInstanceId, instance.FStatus, nodeId);
            return;
        }

        // 取追踪态实体进行更新
        var nodeInstance = await _db.Set<CfOrchestrationNodeInstance>()
            .FirstAsync(n => n.FID == locked.FID);

        nodeInstance.FStatus = "completed";
        nodeInstance.FEndStatusType = "completed";
        nodeInstance.FResultJson = resultData.HasValue ? resultData.Value.GetRawText() : null;
        nodeInstance.FCompletedTime = DateTime.Now;
        nodeInstance.FRelatedBatchId = batchId;

        // 写入派发记录（源节点完成）
        _db.Set<CfDispatchRecord>().Add(new CfDispatchRecord
        {
            FOrchestrationInstanceId = orchestrationInstanceId,
            FDispatchType = "auto",
            FSourceNodeId = nodeId,
            FStatus = "triggered",
            FCreatedTime = DateTime.Now,
            FTriggeredTime = DateTime.Now,
            FDataPayloadJson = resultData.HasValue ? resultData.Value.GetRawText() : null
        });

        await _db.SaveChangesAsync();

        // 暂停态：仅登记节点完成，不触发后续调度
        if (instance.FStatus == "paused")
        {
            _logger.LogInformation("编排实例 {InstanceId} 处于 paused 状态，节点 {NodeId} 批次完成但不触发后续调度",
                orchestrationInstanceId, nodeId);
            return;
        }

        await EvaluateAndDispatchAsync(orchestrationInstanceId, nodeId);
    }

    /// <summary>
    /// 评估某节点完成后的下游边并触发对应节点。
    /// </summary>
    public async Task EvaluateAndDispatchAsync(long instanceId, string completedNodeId)
    {
        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == instanceId);
        if (instance == null)
        {
            _logger.LogWarning("EvaluateAndDispatch: 编排实例 {InstanceId} 不存在", instanceId);
            return;
        }
        if (instance.FStatus is "completed" or "terminated" or "failed" or "cancelled" or "paused")
        {
            return;
        }

        var nodes = ParseNodes(instance.FSnapshotNodesJson);
        var edges = ParseEdges(instance.FSnapshotEdgesJson);

        var completedNodeInstance = await _db.Set<CfOrchestrationNodeInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.FOrchestrationInstanceId == instanceId && n.FNodeId == completedNodeId);

        var evalCtx = BuildEvalContext(instance, completedNodeInstance);

        var outEdges = edges.Where(e => e.From == completedNodeId).ToList();
        var anyTriggered = false;

        foreach (var edge in outEdges)
        {
            var conditionPassed = EvaluateCondition(edge.Condition, evalCtx);
            if (!conditionPassed)
            {
                _db.Set<CfDispatchRecord>().Add(new CfDispatchRecord
                {
                    FOrchestrationInstanceId = instanceId,
                    FDispatchType = "auto",
                    FSourceNodeId = edge.From,
                    FTargetNodeId = edge.To,
                    FStatus = "skipped",
                    FCreatedTime = DateTime.Now,
                    FFailureReason = "condition_not_met"
                });
                continue;
            }

            var targetNode = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (targetNode == null)
            {
                _logger.LogWarning("边 {From}->{To} 的目标节点不存在", edge.From, edge.To);
                continue;
            }

            var targetType = (targetNode.Type ?? string.Empty).ToLowerInvariant();
            switch (targetType)
            {
                case "cardflow":
                    await TriggerCardFlowNodeAsync(instanceId, targetNode.Id,
                        completedNodeInstance?.FResultJson != null
                            ? JsonDocument.Parse(completedNodeInstance.FResultJson).RootElement
                            : (JsonElement?)null);
                    anyTriggered = true;
                    break;

                case "join":
                    var joinSatisfied = await CheckJoinAsync(instanceId, targetNode.Id);
                    if (joinSatisfied)
                    {
                        var joinInstance = await _db.Set<CfOrchestrationNodeInstance>()
                            .FirstOrDefaultAsync(n => n.FOrchestrationInstanceId == instanceId
                                                  && n.FNodeId == targetNode.Id);
                        if (joinInstance != null && joinInstance.FStatus == "pending")
                        {
                            joinInstance.FStatus = "completed";
                            joinInstance.FStartTime = DateTime.Now;
                            joinInstance.FCompletedTime = DateTime.Now;
                            await _db.SaveChangesAsync();
                            await EvaluateAndDispatchAsync(instanceId, targetNode.Id);
                            anyTriggered = true;
                        }
                    }
                    break;

                case "end":
                    {
                        var endInstance = await _db.Set<CfOrchestrationNodeInstance>()
                            .FirstOrDefaultAsync(n => n.FOrchestrationInstanceId == instanceId
                                                  && n.FNodeId == targetNode.Id);
                        if (endInstance != null && endInstance.FStatus == "pending")
                        {
                            endInstance.FStatus = "completed";
                            endInstance.FStartTime = DateTime.Now;
                            endInstance.FCompletedTime = DateTime.Now;
                            await _db.SaveChangesAsync();
                        }
                        await CompleteAsync(instanceId, "reached_end");
                        anyTriggered = true;
                    }
                    break;

                default:
                    _logger.LogWarning("节点 {NodeId} 类型 {Type} 不支持", targetNode.Id, targetNode.Type);
                    break;
            }
        }

        // 若无触发且无 running 节点，则视为 all_skipped 终止
        if (!anyTriggered)
        {
            var hasRunning = await _db.Set<CfOrchestrationNodeInstance>()
                .AnyAsync(n => n.FOrchestrationInstanceId == instanceId && n.FStatus == "running");
            if (!hasRunning)
            {
                var freshInstance = await _db.Set<CfOrchestrationInstance>()
                    .FirstAsync(i => i.FID == instanceId);
                if (freshInstance.FStatus == "running")
                {
                    freshInstance.FStatus = "terminated";
                    freshInstance.FCompletionReason = "all_skipped";
                    freshInstance.FCompletedTime = DateTime.Now;
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("编排实例 {InstanceId} 因无可触发出边而终止 (all_skipped)", instanceId);
                }
            }
        }
    }

    /// <summary>
    /// 检查 join 节点是否满足汇聚条件（行级锁防并发）。
    /// </summary>
    public async Task<bool> CheckJoinAsync(long instanceId, string joinNodeId)
    {
        var instance = await _db.Set<CfOrchestrationInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.FID == instanceId);
        if (instance == null) return false;

        var nodes = ParseNodes(instance.FSnapshotNodesJson);
        var edges = ParseEdges(instance.FSnapshotEdgesJson);

        var joinNode = nodes.FirstOrDefault(n => n.Id == joinNodeId);
        if (joinNode == null) return false;

        var inboundEdges = edges.Where(e => e.To == joinNodeId).ToList();
        if (inboundEdges.Count == 0) return false;

        var sourceNodeIds = inboundEdges.Select(e => e.From).Distinct().ToList();

        // UPDLOCK 锁定相关节点实例行，防止并发 Evaluate
        var sourceInstances = await _db.Set<CfOrchestrationNodeInstance>()
            .FromSqlRaw(
                "SELECT * FROM [CF编排节点实例] WITH (UPDLOCK, ROWLOCK) WHERE [F编排实例ID] = {0}",
                instanceId)
            .AsNoTracking()
            .ToListAsync();

        var relevant = sourceInstances.Where(s => sourceNodeIds.Contains(s.FNodeId)).ToList();
        if (relevant.Count < sourceNodeIds.Count)
        {
            return false;
        }

        var joinMode = (joinNode.JoinMode ?? "all").ToLowerInvariant();
        return joinMode switch
        {
            "any" => relevant.Any(s => s.FStatus == "completed"),
            _ => relevant.All(s => s.FStatus is "completed" or "skipped")
        };
    }

    /// <summary>
    /// 触发一个 cardflow 节点（创建卡片流程实例的占位实现，Task 3 将集成 FlowEngineService）。
    /// </summary>
    public async Task TriggerCardFlowNodeAsync(long instanceId, string nodeId, JsonElement? inputData)
    {
        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == instanceId);
        if (instance == null)
        {
            throw new InvalidOperationException($"编排实例 {instanceId} 不存在");
        }

        var template = await _db.Set<CfOrchestrationTemplate>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FID == instance.FTemplateId);
        var maxTrigger = template?.FMaxTriggerCount ?? 50;

        if (instance.FTriggerCount >= maxTrigger)
        {
            instance.FStatus = "failed";
            instance.FFailureReason = $"trigger_count_exceeded:{maxTrigger}";
            instance.FCompletedTime = DateTime.Now;
            await _db.SaveChangesAsync();
            _logger.LogWarning("编排实例 {InstanceId} 累计触发数已达上限 {Max}，标记为 failed",
                instanceId, maxTrigger);
            return;
        }

        var nodes = ParseNodes(instance.FSnapshotNodesJson);
        var node = nodes.FirstOrDefault(n => n.Id == nodeId)
            ?? throw new InvalidOperationException($"节点 {nodeId} 不存在于编排实例 {instanceId}");

        if (string.IsNullOrWhiteSpace(node.FlowCode))
        {
            throw new InvalidOperationException($"cardflow 节点 {nodeId} 未配置 flowCode");
        }

        var flowDef = await _db.Set<CfFlowDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FFlowCode == node.FlowCode && f.FOrgId == instance.FOrgId);
        if (flowDef == null)
        {
            throw new InvalidOperationException($"流程定义不存在：flowCode={node.FlowCode} orgId={instance.FOrgId}");
        }

        var currentVersion = await _db.Set<CfFlowVersion>()
            .AsNoTracking()
            .Where(v => v.FFlowDefinitionId == flowDef.FID && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync();

        var nodeInstance = await _db.Set<CfOrchestrationNodeInstance>()
            .FirstOrDefaultAsync(n => n.FOrchestrationInstanceId == instanceId && n.FNodeId == nodeId)
            ?? throw new InvalidOperationException($"节点实例不存在：instance={instanceId} node={nodeId}");

        // 简化实现：直接创建 CfCard 占位记录（draft），Task 3 将替换为 FlowEngineService 调用
        var card = new CfCard
        {
            FFlowDefinitionId = flowDef.FID,
            FFlowVersionId = currentVersion?.FID ?? 0,
            FStatus = "draft",
            FInitiatorId = instance.FInitiatorId,
            FInitiatorName = string.Empty,
            FCreatedTime = DateTime.Now,
            FOrgId = instance.FOrgId,
            FDataJson = inputData.HasValue ? inputData.Value.GetRawText() : null,
            FOrchestrationInstanceId = instanceId,
            FOrchestrationNodeId = nodeId
        };
        _db.Set<CfCard>().Add(card);
        await _db.SaveChangesAsync();

        nodeInstance.FStatus = "running";
        nodeInstance.FStartTime = DateTime.Now;
        nodeInstance.FRelatedCardId = card.FID;

        instance.FTriggerCount += 1;

        _db.Set<CfDispatchRecord>().Add(new CfDispatchRecord
        {
            FOrchestrationInstanceId = instanceId,
            FDispatchType = "auto",
            FTargetNodeId = nodeId,
            FTargetCardId = card.FID,
            FTargetFlowCode = node.FlowCode,
            FStatus = "triggered",
            FCreatedTime = DateTime.Now,
            FTriggeredTime = DateTime.Now,
            FDataPayloadJson = inputData.HasValue ? inputData.Value.GetRawText() : null
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("已触发 cardflow 节点 {NodeId}（cardId={CardId}, flowCode={FlowCode}）",
            nodeId, card.FID, node.FlowCode);
    }

    /// <summary>
    /// 标记编排完成（reached_end / all_skipped 等）。
    /// </summary>
    public async Task CompleteAsync(long instanceId, string reason)
    {
        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == instanceId);
        if (instance == null) return;
        if (instance.FStatus is "completed" or "terminated" or "failed" or "cancelled") return;

        instance.FStatus = reason == "reached_end" ? "completed" : "terminated";
        instance.FCompletionReason = reason;
        instance.FCompletedTime = DateTime.Now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("编排实例 {InstanceId} 已完成（{Status} / {Reason}）",
            instanceId, instance.FStatus, reason);
    }

    /// <summary>
    /// 暂停编排：仅暂停调度，不影响已运行卡片。
    /// </summary>
    public async Task PauseAsync(long instanceId)
    {
        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == instanceId);
        if (instance == null) return;
        if (instance.FStatus != "running")
        {
            throw new InvalidOperationException($"实例 {instanceId} 当前状态 {instance.FStatus}，无法暂停");
        }
        instance.FStatus = "paused";
        await _db.SaveChangesAsync();
        _logger.LogInformation("编排实例 {InstanceId} 已暂停", instanceId);
    }

    /// <summary>
    /// 恢复编排：补偿扫描已完成但未触发 Evaluate 的节点。
    /// </summary>
    public async Task ResumeAsync(long instanceId)
    {
        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == instanceId);
        if (instance == null) return;
        if (instance.FStatus != "paused")
        {
            throw new InvalidOperationException($"实例 {instanceId} 当前状态 {instance.FStatus}，无法恢复");
        }
        instance.FStatus = "running";
        await _db.SaveChangesAsync();

        // 补偿扫描：completed 节点若无对应出向派发记录则补一次 Evaluate
        var completedNodes = await _db.Set<CfOrchestrationNodeInstance>()
            .AsNoTracking()
            .Where(n => n.FOrchestrationInstanceId == instanceId && n.FStatus == "completed")
            .ToListAsync();

        foreach (var node in completedNodes)
        {
            var hasOutbound = await _db.Set<CfDispatchRecord>()
                .AnyAsync(d => d.FOrchestrationInstanceId == instanceId
                            && d.FSourceNodeId == node.FNodeId
                            && d.FStatus != "pending");
            if (!hasOutbound)
            {
                await EvaluateAndDispatchAsync(instanceId, node.FNodeId);
            }
        }
        _logger.LogInformation("编排实例 {InstanceId} 已恢复", instanceId);
    }

    /// <summary>
    /// 取消编排。
    /// </summary>
    public async Task CancelAsync(long instanceId)
    {
        var instance = await _db.Set<CfOrchestrationInstance>()
            .FirstOrDefaultAsync(i => i.FID == instanceId);
        if (instance == null) return;
        if (instance.FStatus is "completed" or "terminated" or "failed" or "cancelled") return;

        instance.FStatus = "cancelled";
        instance.FCompletionReason = "manual_cancel";
        instance.FCompletedTime = DateTime.Now;
        await _db.SaveChangesAsync();
        _logger.LogInformation("编排实例 {InstanceId} 已取消", instanceId);
    }

    // =====================================================================
    // 辅助方法
    // =====================================================================

    /// <summary>
    /// Kahn 算法拓扑排序，校验 DAG 无环。
    /// </summary>
    public bool ValidateDag(string nodesJson, string edgesJson)
    {
        var nodes = ParseNodes(nodesJson);
        var edges = ParseEdges(edgesJson);

        if (nodes.Count == 0) return false;

        var nodeIds = new HashSet<string>(nodes.Select(n => n.Id));

        // 检查所有边引用的节点是否存在
        foreach (var e in edges)
        {
            if (!nodeIds.Contains(e.From) || !nodeIds.Contains(e.To))
                return false;
        }

        var inDegree = nodes.ToDictionary(n => n.Id, _ => 0);
        var adj = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var e in edges)
        {
            inDegree[e.To] += 1;
            adj[e.From].Add(e.To);
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var visited = 0;
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            visited++;
            foreach (var next in adj[n])
            {
                inDegree[next] -= 1;
                if (inDegree[next] == 0) queue.Enqueue(next);
            }
        }
        return visited == nodes.Count;
    }

    /// <summary>
    /// 模板发布前校验：DAG 无环、cardflow 节点 flowCode 有效、start/end/join 结构合法。
    /// </summary>
    public async Task<(bool IsValid, List<string> Errors)> ValidateForPublishAsync(long templateId)
    {
        var errors = new List<string>();
        var template = await _db.Set<CfOrchestrationTemplate>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FID == templateId);
        if (template == null)
        {
            errors.Add($"编排模板 {templateId} 不存在");
            return (false, errors);
        }

        var nodesJson = template.FNodesJson ?? "{\"nodes\":[]}";
        var edgesJson = template.FEdgesJson ?? "{\"edges\":[]}";
        var nodes = ParseNodes(nodesJson);
        var edges = ParseEdges(edgesJson);

        if (!ValidateDag(nodesJson, edgesJson))
        {
            errors.Add("DAG 包含循环或边引用了不存在的节点");
        }

        var startNodes = nodes.Where(n => string.Equals(n.Type, "start", StringComparison.OrdinalIgnoreCase)).ToList();
        if (startNodes.Count != 1)
        {
            errors.Add($"start 节点必须有且仅有一个，当前 {startNodes.Count} 个");
        }

        var endNodes = nodes.Where(n => string.Equals(n.Type, "end", StringComparison.OrdinalIgnoreCase)).ToList();
        if (endNodes.Count == 0)
        {
            errors.Add("至少需要一个 end 节点");
        }

        // join 节点入边数 >= 2
        var joinNodes = nodes.Where(n => string.Equals(n.Type, "join", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var jn in joinNodes)
        {
            var inCount = edges.Count(e => e.To == jn.Id);
            if (inCount < 2)
            {
                errors.Add($"join 节点 {jn.Id} 入边数 {inCount}<2");
            }
        }

        // cardflow 节点 flowCode 校验
        var cardFlowNodes = nodes
            .Where(n => string.Equals(n.Type, "cardflow", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var flowCodes = cardFlowNodes
            .Select(n => n.FlowCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        var existingCodes = await _db.Set<CfFlowDefinition>()
            .AsNoTracking()
            .Where(f => f.FOrgId == template.FOrgId && flowCodes.Contains(f.FFlowCode))
            .Select(f => f.FFlowCode)
            .ToListAsync();
        foreach (var n in cardFlowNodes)
        {
            if (string.IsNullOrWhiteSpace(n.FlowCode))
            {
                errors.Add($"cardflow 节点 {n.Id} 未配置 flowCode");
            }
            else if (!existingCodes.Contains(n.FlowCode!))
            {
                errors.Add($"cardflow 节点 {n.Id} 的 flowCode={n.FlowCode} 在组织 {template.FOrgId} 中不存在");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 简单条件评估：支持 ==, !=, &gt;, &lt;, &gt;=, &lt;=, in, notIn。
    /// </summary>
    private bool EvaluateCondition(JsonElement? condition, Dictionary<string, JsonElement> context)
    {
        if (condition == null || condition.Value.ValueKind == JsonValueKind.Null
            || condition.Value.ValueKind == JsonValueKind.Undefined)
        {
            return true;
        }

        var cond = condition.Value;
        if (cond.ValueKind != JsonValueKind.Object) return true;

        if (!cond.TryGetProperty("field", out var fieldEl)
            || !cond.TryGetProperty("op", out var opEl))
        {
            return true;
        }

        var field = fieldEl.GetString();
        var op = opEl.GetString();
        if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(op)) return true;

        if (!context.TryGetValue(field, out var actualEl))
        {
            return false;
        }

        var hasValue = cond.TryGetProperty("value", out var valueEl);

        switch (op)
        {
            case "==":
                return hasValue && JsonEquals(actualEl, valueEl);
            case "!=":
                return hasValue && !JsonEquals(actualEl, valueEl);
            case ">":
            case "<":
            case ">=":
            case "<=":
                if (!hasValue) return false;
                if (!TryToDouble(actualEl, out var a) || !TryToDouble(valueEl, out var b)) return false;
                return op switch
                {
                    ">" => a > b,
                    "<" => a < b,
                    ">=" => a >= b,
                    "<=" => a <= b,
                    _ => false
                };
            case "in":
                return hasValue && valueEl.ValueKind == JsonValueKind.Array
                    && valueEl.EnumerateArray().Any(v => JsonEquals(actualEl, v));
            case "notIn":
                return hasValue && valueEl.ValueKind == JsonValueKind.Array
                    && !valueEl.EnumerateArray().Any(v => JsonEquals(actualEl, v));
            default:
                _logger.LogWarning("条件评估遇到未知操作符 {Op}", op);
                return false;
        }
    }

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind == JsonValueKind.String && b.ValueKind == JsonValueKind.String)
        {
            return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
        }
        if (a.ValueKind == JsonValueKind.True || a.ValueKind == JsonValueKind.False
            || b.ValueKind == JsonValueKind.True || b.ValueKind == JsonValueKind.False)
        {
            return a.ValueKind == b.ValueKind;
        }
        if (TryToDouble(a, out var da) && TryToDouble(b, out var db))
        {
            return Math.Abs(da - db) < 1e-9;
        }
        return string.Equals(a.GetRawText(), b.GetRawText(), StringComparison.Ordinal);
    }

    private static bool TryToDouble(JsonElement el, out double v)
    {
        v = 0;
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetDouble(out v);
            case JsonValueKind.String:
                return double.TryParse(el.GetString(), out v);
            default:
                return false;
        }
    }

    private Dictionary<string, JsonElement> BuildEvalContext(
        CfOrchestrationInstance instance, CfOrchestrationNodeInstance? completedNode)
    {
        var ctx = new Dictionary<string, JsonElement>();

        if (completedNode != null)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                endStatus = completedNode.FEndStatusType,
                status = completedNode.FStatus,
                nodeId = completedNode.FNodeId
            }));
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                ctx[p.Name] = p.Value.Clone();
            }

            if (!string.IsNullOrEmpty(completedNode.FResultJson))
            {
                try
                {
                    using var resultDoc = JsonDocument.Parse(completedNode.FResultJson);
                    if (resultDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in resultDoc.RootElement.EnumerateObject())
                        {
                            ctx[p.Name] = p.Value.Clone();
                        }
                    }
                    ctx["result"] = resultDoc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "解析节点 {NodeId} 的 FResultJson 失败", completedNode.FNodeId);
                }
            }
        }

        if (!string.IsNullOrEmpty(instance.FContextJson))
        {
            try
            {
                using var ctxDoc = JsonDocument.Parse(instance.FContextJson);
                ctx["context"] = ctxDoc.RootElement.Clone();
            }
            catch (JsonException) { }
        }

        return ctx;
    }

    private static List<DagNode> ParseNodes(string? nodesJson)
    {
        if (string.IsNullOrWhiteSpace(nodesJson)) return new List<DagNode>();
        try
        {
            // 优先尝试直接解析为数组格式（前端保存格式）
            var nodes = JsonSerializer.Deserialize<List<DagNode>>(nodesJson, JsonOpts);
            if (nodes != null && nodes.Count > 0) return nodes;
        }
        catch (JsonException) { }

        try
        {
            // 兼容包装格式 { "nodes": [...] }（Seeder 预置格式）
            var def = JsonSerializer.Deserialize<DagDefinition>(nodesJson, JsonOpts);
            return def?.Nodes ?? new List<DagNode>();
        }
        catch (JsonException)
        {
            return new List<DagNode>();
        }
    }

    private static List<DagEdge> ParseEdges(string? edgesJson)
    {
        if (string.IsNullOrWhiteSpace(edgesJson)) return new List<DagEdge>();
        try
        {
            // 优先尝试直接解析为数组格式
            var edges = JsonSerializer.Deserialize<List<DagEdge>>(edgesJson, JsonOpts);
            if (edges != null && edges.Count > 0) return edges;
        }
        catch (JsonException) { }

        try
        {
            // 兼容包装格式 { "edges": [...] }
            var def = JsonSerializer.Deserialize<EdgeDefinition>(edgesJson, JsonOpts);
            return def?.Edges ?? new List<DagEdge>();
        }
        catch (JsonException)
        {
            return new List<DagEdge>();
        }
    }

    // =====================================================================
    // 内部 DAG 解析模型
    // =====================================================================

    private class DagNode
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FlowCode { get; set; }
        public string CompletionMode { get; set; } = "single";
        public string? JoinMode { get; set; }
    }

    private class DagEdge
    {
        public string Id { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public JsonElement? Condition { get; set; }
        public JsonElement? DataProtocol { get; set; }
    }

    private class DagDefinition
    {
        public List<DagNode> Nodes { get; set; } = new();
    }

    private class EdgeDefinition
    {
        public List<DagEdge> Edges { get; set; } = new();
    }
}
