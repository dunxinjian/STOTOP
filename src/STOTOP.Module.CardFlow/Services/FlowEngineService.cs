using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.CardFlow.Services;

public class FlowEngineService : IFlowEngineService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly INumberSequenceService _numberService;
    private readonly ICardSchemaService _schemaService;
    private readonly IApprovalModeHandler _approvalHandler;
    private readonly SequentialApprovalRuntime _sequentialRuntime;
    private readonly ReturnToStageRuntime _returnToStageRuntime;
    private readonly IStageConfigParser _stageConfigParser;
    private readonly IStageFieldAccessService _stageFieldAccess;
    private readonly IStageActionPolicyService _stageActionPolicy;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly IStageRouteResolver _stageRouteResolver;
    private readonly IDynamicStagePolicyResolver _dynamicStagePolicyResolver;
    private readonly IAuditSnapshotPolicyService _auditSnapshotPolicyService;
    private readonly IApproverResolver _approverResolver;
    private readonly IBudgetOccupationService _budgetOccupationService;
    private readonly ITodoService _todoService;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly AutoPluginFactory _pluginFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrchestrationEngineService _orchestrationEngine;
    private readonly IBatchNotifier _batchNotifier;
    private readonly IBatchLifecycleService _batchLifecycle;
    private readonly ILogger<FlowEngineService> _logger;
    private const int MaxErrorMessageLength = 2000;

    public FlowEngineService(
        STOTOPDbContext dbContext,
        INumberSequenceService numberService,
        ICardSchemaService schemaService,
        IApprovalModeHandler approvalHandler,
        SequentialApprovalRuntime sequentialRuntime,
        ReturnToStageRuntime returnToStageRuntime,
        IStageConfigParser stageConfigParser,
        IStageFieldAccessService stageFieldAccess,
        IStageActionPolicyService stageActionPolicy,
        IConditionEvaluator conditionEvaluator,
        IApproverResolver approverResolver,
        IBudgetOccupationService budgetOccupationService,
        ITodoService todoService,
        INotificationDispatcher notificationDispatcher,
        AutoPluginFactory pluginFactory,
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        OrchestrationEngineService orchestrationEngine,
        IBatchNotifier batchNotifier,
        IBatchLifecycleService batchLifecycle,
        ILogger<FlowEngineService> logger,
        IStageRouteResolver? stageRouteResolver = null,
        IDynamicStagePolicyResolver? dynamicStagePolicyResolver = null,
        IAuditSnapshotPolicyService? auditSnapshotPolicyService = null)
    {
        _dbContext = dbContext;
        _numberService = numberService;
        _schemaService = schemaService;
        _approvalHandler = approvalHandler;
        _sequentialRuntime = sequentialRuntime;
        _returnToStageRuntime = returnToStageRuntime;
        _stageConfigParser = stageConfigParser;
        _stageFieldAccess = stageFieldAccess;
        _stageActionPolicy = stageActionPolicy;
        _conditionEvaluator = conditionEvaluator;
        _stageRouteResolver = stageRouteResolver
            ?? new StageRouteResolver(dbContext, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(dbContext));
        _dynamicStagePolicyResolver = dynamicStagePolicyResolver
            ?? new DynamicStagePolicyResolver(dbContext, new ConditionRuleEvaluator(), new ConditionEvaluationContextBuilder(dbContext), approverResolver);
        _auditSnapshotPolicyService = auditSnapshotPolicyService ?? new AuditSnapshotPolicyService();
        _approverResolver = approverResolver;
        _budgetOccupationService = budgetOccupationService;
        _todoService = todoService;
        _notificationDispatcher = notificationDispatcher;
        _pluginFactory = pluginFactory;
        _serviceProvider = serviceProvider;
        _scopeFactory = scopeFactory;
        _orchestrationEngine = orchestrationEngine;
        _batchNotifier = batchNotifier;
        _batchLifecycle = batchLifecycle;
        _logger = logger;
    }

    /// <summary>
    /// 卡片进入终态时通知编排引擎（如该卡片由编排实例驱动产生）。
    /// 编排回调失败不影响主流程。
    /// </summary>
    private async Task NotifyOrchestrationOnCardCompletedAsync(CfCard card)
    {
        if (card.FOrchestrationInstanceId == null || string.IsNullOrEmpty(card.FOrchestrationNodeId))
            return;

        try
        {
            JsonElement? resultData = null;
            if (!string.IsNullOrEmpty(card.FDataJson))
            {
                try
                {
                    resultData = JsonDocument.Parse(card.FDataJson).RootElement.Clone();
                }
                catch (JsonException)
                {
                    resultData = null;
                }
            }

            await _orchestrationEngine.OnFlowCompletedAsync(
                card.FOrchestrationInstanceId.Value,
                card.FOrchestrationNodeId!,
                card.FID,
                card.FStatus,
                resultData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "编排回调失败: 编排实例={InstanceId}, 节点={NodeId}, 卡片={CardId}",
                card.FOrchestrationInstanceId, card.FOrchestrationNodeId, card.FID);
        }
    }

    /// <summary>
    /// 判定节点是否为批次级自动节点（兼容旧版 FType="batchAuto" 与新版 FType="auto" + F处理粒度="batch"）。
    /// </summary>
    private static bool IsBatchAutoStage(CfStageDefinition stage)
    {
        if (string.Equals(stage.FType, "batchAuto", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(stage.FType, "auto", StringComparison.OrdinalIgnoreCase)
               && string.Equals(stage.F处理粒度, "batch", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>将 batch 中重复跟踪的实体 Detach，避免 Reload 时发生 identity conflict。</summary>
    private void ReloadBatchDetachConflicts(CfBatch batch)
    {
        _dbContext.ChangeTracker.Entries<CfBatch>()
            .Where(e => e.Entity.FID == batch.FID && !ReferenceEquals(e.Entity, batch))
            .ToList()
            .ForEach(e => e.State = EntityState.Detached);
    }

    /// <summary>根据插件编码返回执行成功后应转移的批次状态（null 表示不自动转移）。</summary>
    private static int? GetPostPluginBatchStatus(string pluginCode)
    {
        if (pluginCode.Contains("ExcelInput", StringComparison.OrdinalIgnoreCase)) return 1;   // Staged
        if (pluginCode.Contains("QualityAnalysis", StringComparison.OrdinalIgnoreCase)) return 3; // CardCreated
        if (pluginCode.Contains("FanOut", StringComparison.OrdinalIgnoreCase)) return 4;          // Processing
        return null;
    }

    /// <summary>
    /// 处理批次级节点链：按 FSortOrder 顺序执行所有批次级自动节点。
    /// 采用"记录先行+原子推送"模式：预创建所有执行记录，再逐个推进并通过 SignalR 推送状态。
    /// </summary>
    public async Task ProcessBatchStagesAsync(CfBatch batch, CancellationToken ct = default)
    {
        // 0. 设置组织上下文：批次在后台 HostedService 链路执行，没有 HttpContext，
        //    若不显式设置 CurrentOrgId，EF 全局组织过滤器(CurrentOrgId==null 时整体放行)会失效，
        //    导致计价/成本等引擎按 IRepository 读取参考数据时跨组织串数据。此处以批次组织为准。
        var orgAccessor = _serviceProvider.GetService<IOrgContextAccessor>();
        if (orgAccessor != null)
            orgAccessor.CurrentOrgId = batch.FOrgId;

        // 1. 获取流程当前发布版本
        var version = await _dbContext.Set<CfFlowVersion>()
            .Where(v => v.FFlowDefinitionId == batch.FFlowDefinitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version == null)
            throw new InvalidOperationException($"流程未发布版本：FlowDefinitionId={batch.FFlowDefinitionId}");

        // 2. 获取所有节点定义（按排序号升序）
        var stages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == version.FID)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync(ct);
        if (stages.Count == 0) return;

        // 3. 确定起始位置
        var startIndex = 0;
        if (batch.FCurrentBatchStageOrder.HasValue)
        {
            startIndex = stages.FindIndex(s => s.FSortOrder > batch.FCurrentBatchStageOrder.Value);
            if (startIndex < 0) return;
        }

        // 4. 收集本次要执行的批次级节点及插件编码
        var batchStages = new List<(CfStageDefinition Stage, string PluginCode, int OriginalIndex)>();
        for (var i = startIndex; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (!IsBatchAutoStage(stage)) break;

            string pluginCode;
#pragma warning disable CS0618
            if (stage.F插件注册ID.HasValue)
            {
                var registry = await _dbContext.Set<CfAutoPluginRegistry>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.FID == stage.F插件注册ID.Value, ct);
                if (registry == null)
                    throw new InvalidOperationException(
                        $"未找到插件注册 ID={stage.F插件注册ID}, StageId={stage.FID}");
                pluginCode = registry.F插件编码;
            }
            else if (!string.IsNullOrWhiteSpace(stage.FAutoPluginName))
            {
                _logger.LogWarning(
                    "批次级节点[{StageName}](StageId={StageId}) 仍使用废弃字段 FAutoPluginName={PluginName}，请尽快迁移到 F插件注册ID",
                    stage.FStageName, stage.FID, stage.FAutoPluginName);
                pluginCode = stage.FAutoPluginName!;
            }
            else
            {
                throw new InvalidOperationException(
                    $"批次级自动节点未配置插件：StageId={stage.FID}, StageName={stage.FStageName}");
            }
#pragma warning restore CS0618
            batchStages.Add((stage, pluginCode, i));
        }

        // 5. 预创建所有批次级 CfPluginExecution 记录（状态=10 待运行）
        var executions = new List<CfPluginExecution>();
        if (batchStages.Count > 0)
        {
            foreach (var (stageItem, codeItem, origIdxItem) in batchStages)
            {
                var execution = new CfPluginExecution
                {
                    FBatchId = batch.FID,
                    FAutoPluginName = stageItem.FStageName,
                    FAutoPluginIndex = origIdxItem,
                    FOrgId = batch.FOrgId,
                    FStatus = 10, // 10=待运行
                    FCreatedTime = DateTime.Now
                };
                _dbContext.Set<CfPluginExecution>().Add(execution);
                executions.Add(execution);
            }
            await _dbContext.SaveChangesAsync(ct);

            // 6. 推送管道启动事件
            var snapshots = batchStages
                .Select((item, idx) => new PluginSnapshot(item.Stage.FStageName, idx, 10))
                .ToList();
            try { await _batchNotifier.PipelineStartedAsync(batch.FID, snapshots); }
            catch (Exception ex) { _logger.LogWarning(ex, "PipelineStarted 推送失败, BatchId={BatchId}", batch.FID); }
        }

        // 7. 逐个执行批次级节点
        for (var idx = 0; idx < batchStages.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();
            var (stage, pluginCode, _) = batchStages[idx];
            var execution = executions[idx];

            // 7a. 标记执行记录为进行中
            execution.FStatus = 11; // 进行中
            execution.FStartTime = DateTime.Now;
            _dbContext.Entry(execution).State = EntityState.Modified;

            // 7b. QualityAnalysisAgent 开始前推进批次状态为 2(质量检查中)
            if (pluginCode.Contains("QualityAnalysis", StringComparison.OrdinalIgnoreCase))
            {
                ReloadBatchDetachConflicts(batch);
                try { await _dbContext.Entry(batch).ReloadAsync(ct); } catch { /* 忽略 */ }
                await _batchLifecycle.TransitionBatchStatusAsync(batch, 2);
            }
            else
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            try { await _batchNotifier.PluginStatusChangedAsync(batch.FID, idx, stage.FStageName, "Running"); }
            catch (Exception exNotify) { _logger.LogWarning(exNotify, "PluginStatusChanged(Running) 推送失败, BatchId={BatchId}", batch.FID); }

            // 7c. 构建插件上下文
            IAutoPlugin plugin;
            try
            {
                plugin = _pluginFactory.Create(pluginCode, _serviceProvider);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"创建批次级插件失败 [{pluginCode}], StageId={stage.FID}: {ex.Message}", ex);
            }

            var pluginContext = new PluginContext
            {
                BatchId = batch.FID,
                CardId = null,
                StageDefinitionId = stage.FID,
                PluginRuleId = stage.F插件规则ID,
                ConfigJson = stage.FAutoPluginConfigJson,
                OrgId = batch.FOrgId,
                Services = _serviceProvider,
                CancellationToken = ct
            };

            PluginResult result;
            try
            {
                _logger.LogInformation("批次 {BatchId} 开始执行节点[{StageName}] Plugin={PluginCode}",
                    batch.FID, stage.FStageName, pluginCode);
                result = await plugin.ExecuteAsync(pluginContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BatchAuto插件 {PluginCode} 执行异常, BatchId={BatchId}", pluginCode, batch.FID);

                execution.FStatus = 13; // 失败
                execution.FEndTime = DateTime.Now;
                execution.FErrorMessage = Truncate($"执行异常：{ex.Message}", MaxErrorMessageLength);
                _dbContext.Entry(execution).State = EntityState.Modified;

                ReloadBatchDetachConflicts(batch);
                try { await _dbContext.Entry(batch).ReloadAsync(ct); } catch { /* 忽略 */ }
                var errMsg1 = Truncate($"批次级节点[{stage.FStageName}]执行异常：{ex.Message}", MaxErrorMessageLength);
                await _batchLifecycle.TransitionBatchStatusAsync(batch, 6, errMsg1);
                try { await _batchNotifier.PluginStatusChangedAsync(batch.FID, idx, stage.FStageName, "Failed", ex.Message); }
                catch (Exception nex) { _logger.LogWarning(nex, "PluginStatusChanged(Failed) 推送失败, BatchId={BatchId}", batch.FID); }
                throw;
            }

            if (!result.Success)
            {
                _logger.LogError("批次 {BatchId} 节点[{StageName}] 执行失败：{Reason}",
                    batch.FID, stage.FStageName, result.Message);

                execution.FStatus = 13; // 失败
                execution.FEndTime = DateTime.Now;
                execution.FErrorMessage = Truncate(result.Message ?? string.Empty, MaxErrorMessageLength);
                _dbContext.Entry(execution).State = EntityState.Modified;

                ReloadBatchDetachConflicts(batch);
                await _dbContext.Entry(batch).ReloadAsync(ct);
                var errMsg2 = Truncate($"批次级节点[{stage.FStageName}]失败：{result.Message}", MaxErrorMessageLength);
                await _batchLifecycle.TransitionBatchStatusAsync(batch, 6, errMsg2);
                try { await _batchNotifier.PluginStatusChangedAsync(batch.FID, idx, stage.FStageName, "Failed", result.Message); }
                catch (Exception nex) { _logger.LogWarning(nex, "PluginStatusChanged(Failed) 推送失败, BatchId={BatchId}", batch.FID); }
                throw new InvalidOperationException($"批次级节点[{stage.FStageName}] 执行失败：{result.Message}");
            }

            // 7d. 成功：更新执行记录为已完成
            execution.FStatus = 12; // 已完成
            execution.FEndTime = DateTime.Now;
            _dbContext.Entry(execution).State = EntityState.Modified;

            // Reload batch 确保与数据库状态同步
            ReloadBatchDetachConflicts(batch);
            await _dbContext.Entry(batch).ReloadAsync(ct);

            // 更新批次进度字段
            batch.FCurrentBatchStageOrder = stage.FSortOrder;
            batch.FUpdatedTime = DateTime.Now;
            _dbContext.Entry(batch).State = EntityState.Modified;

            // 按 Plugin→Status 映射表推进批次状态（含 SaveChanges + SignalR 推送）
            int? nextBatchStatus = GetPostPluginBatchStatus(pluginCode);
            if (nextBatchStatus.HasValue)
            {
                await _batchLifecycle.TransitionBatchStatusAsync(batch, nextBatchStatus.Value);
            }
            else
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            try { await _batchNotifier.PluginStatusChangedAsync(batch.FID, idx, stage.FStageName, "Completed"); }
            catch (Exception exNotify) { _logger.LogWarning(exNotify, "PluginStatusChanged(Completed) 推送失败, BatchId={BatchId}", batch.FID); }

            _logger.LogInformation("批次 {BatchId} 节点[{StageName}] 执行成功，已处理 {Processed} 行",
                batch.FID, stage.FStageName, result.ProcessedRows);
        }

        // ═══ 批次级链结束检测 ═══
        var hasCards = await _dbContext.Set<CfCard>()
            .AnyAsync(c => c.FBatchId == batch.FID, ct);

        if (hasCards)
        {
            _logger.LogInformation("批次 {BatchId} 批次级链结束，检测到已创建卡片，启动卡片级推进", batch.FID);
            await ProcessCardStagesForBatchAsync(batch, ct);
        }
        else
        {
            // 无 FanOut（场景A）→ 标记批次完成（含 SignalR 推送）
            ReloadBatchDetachConflicts(batch);
            try { await _dbContext.Entry(batch).ReloadAsync(ct); } catch { /* 忽略 */ }
            await _batchLifecycle.TransitionBatchStatusAsync(batch, 5);
            _logger.LogInformation("批次 {BatchId} 批次级链结束，无卡片创建，标记为已完成", batch.FID);
        }
    }
    public async Task<CardOperationResult> SubmitAsync(long cardId, long operatorId)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // 1. 查询卡片
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FStatus != "draft" && card.FStatus != "returned")
                    return CardOperationResult.Fail($"当前状态[{card.FStatus}]不允许提交");
                if (card.FInitiatorId != operatorId)
                    return CardOperationResult.Fail("只有发起人可以提交");

                // 2. 查询流程定义
                var flowDef = await _dbContext.Set<CfFlowDefinition>().FirstOrDefaultAsync(f => f.FID == card.FFlowDefinitionId);
                if (flowDef == null) return CardOperationResult.Fail("流程定义不存在");

                // 3. 查询当前发布版本
                var version = await _dbContext.Set<CfFlowVersion>()
                    .FirstOrDefaultAsync(v => v.FFlowDefinitionId == flowDef.FID && v.FIsCurrentVersion);
                if (version == null) return CardOperationResult.Fail("流程未发布，无法提交");

                // 4. 锁定版本
                card.FFlowVersionId = version.FID;

                // 5. Schema校验
                if (!string.IsNullOrWhiteSpace(version.FCardSchemaJson))
                {
                    var validation = _schemaService.ValidateCardData(version.FCardSchemaJson, card.FDataJson ?? "{}");
                    if (!validation.IsValid)
                        return CardOperationResult.Fail($"数据校验失败: {string.Join("; ", validation.Errors)}");
                }

                var budgetPreview = await PreviewBudgetBeforeSubmitAsync(card, flowDef);
                if (budgetPreview?.Blocked == true)
                {
                    return CardOperationResult.Fail($"超预算，缺口 {budgetPreview.GapAmount:N2}，当前预算策略不允许提交");
                }

                // 6. 生成编号
                if (string.IsNullOrWhiteSpace(card.FCardNumber) && !string.IsNullOrWhiteSpace(flowDef.FNumberTemplate))
                {
                    card.FCardNumber = await _numberService.GenerateNumberAsync(flowDef.FID, card.FOrgId, flowDef.FNumberTemplate);
                }

                // 7. 生成标题
                card.FTitle = _schemaService.GenerateTitle(
                    flowDef.FTitleTemplate ?? "",
                    card.FDataJson ?? "{}",
                    flowDef.FFlowName,
                    card.FCardNumber ?? "");

                // 8. 更新卡片状态
                card.FStatus = "active";
                card.FCurrentRound += 1;
                card.FSubmitTime = DateTime.Now;
                card.FUpdatedTime = DateTime.Now;
                // 显式标记 card 已修改，避免在某些 ChangeTracker 边界场景下漏发 UPDATE
                _dbContext.Entry(card).State = EntityState.Modified;

                // 9. 获取第一个阶段定义
                var stages = await _dbContext.Set<CfStageDefinition>()
                    .Where(s => s.FFlowVersionId == version.FID)
                    .OrderBy(s => s.FSortOrder)
                    .ToListAsync();

                if (stages.Count == 0) return CardOperationResult.Fail("流程未定义任何节点");

                // 10. 创建第一个节点实例
                var firstStage = stages[0];
                var stageInstance = new CfStageInstance
                {
                    FCardId = card.FID,
                    FStageDefinitionId = firstStage.FID,
                    FStageName = firstStage.FStageName,
                    FType = firstStage.FType,
                    FApprovalMode = firstStage.FApprovalMode,
                    FRound = card.FCurrentRound,
                    FStatus = "active",
                    FActivatedTime = DateTime.Now,
                    FStartTime = DateTime.Now
                };
                _dbContext.Set<CfStageInstance>().Add(stageInstance);
                await _dbContext.SaveChangesAsync();

                card.FCurrentStageInstanceId = stageInstance.FID;
                await OccupyBudgetOnSubmitAsync(card, flowDef, $"card:{card.FID}:submit:{card.FStatus}");

                // 11. 分配处理人（auto 节点则直接执行自动插件）
                if (string.Equals(firstStage.FType, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteAutoStageAsync(card, stageInstance, firstStage);
                }
                else
                {
                    await AssignStageHandlersAsync(stageInstance, firstStage, card);
                }

                // 12. 记录ActionLog
                await LogActionAsync(card.FID, stageInstance.FID, "submit", operatorId, card.FInitiatorName, null);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return new CardOperationResult
                {
                    Success = true,
                    CardId = card.FID,
                    CardNumber = card.FCardNumber,
                    NewStatus = card.FStatus
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "提交卡片失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"提交失败: {ex.Message}");
            }
        });
    }

    public async Task<CardOperationResult> ApproveAsync(long cardId, long operatorId, ApproveRequest request)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FStatus != "active") return CardOperationResult.Fail("卡片不在审批中");

                // 验证concurrencyStamp
                if (!string.IsNullOrWhiteSpace(request.ConcurrencyStamp))
                {
                    var currentStamp = Convert.ToBase64String(card.FRowVersion);
                    if (currentStamp != request.ConcurrencyStamp)
                        return CardOperationResult.Fail("数据已被修改，请刷新后重试");
                }

                // 查找当前处理人
                var stageInstance = await _dbContext.Set<CfStageInstance>()
                    .FirstOrDefaultAsync(s => s.FID == card.FCurrentStageInstanceId && s.FStatus == "active");
                if (stageInstance == null) return CardOperationResult.Fail("当前无活跃节点");

                var assignee = await _dbContext.Set<CfStageAssignee>()
                    .FirstOrDefaultAsync(a => a.FStageInstanceId == stageInstance.FID
                        && a.FUserId == operatorId && a.FStatus == "pending");
                if (assignee == null) return CardOperationResult.Fail("您不是当前节点处理人");

                var stageDefForApproval = await _dbContext.Set<CfStageDefinition>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.FID == stageInstance.FStageDefinitionId);
                var normalizedConfig = _stageConfigParser.Parse(stageDefForApproval?.FInputFieldsJson);
                var actionValidation = ValidateStageAction(normalizedConfig, "approve");
                if (!actionValidation.Success)
                {
                    return CardOperationResult.Fail(actionValidation.ErrorMessage!);
                }

                var supplementValidation = _stageFieldAccess.ValidateSupplement(normalizedConfig, request.SupplementData);
                if (!supplementValidation.Success)
                {
                    return CardOperationResult.Fail(supplementValidation.ErrorMessage!);
                }

                var requiredValidation = _stageFieldAccess.ValidateRequiredFields(
                    normalizedConfig,
                    ParseCardData(card.FDataJson),
                    request.SupplementData);
                if (!requiredValidation.Success)
                {
                    return CardOperationResult.Fail(requiredValidation.ErrorMessage!);
                }

                var detailValidation = _stageFieldAccess.ValidateDetailEdits(normalizedConfig, request.DetailEdits);
                if (!detailValidation.Success)
                {
                    return CardOperationResult.Fail(detailValidation.ErrorMessage!);
                }

                if (request.DetailEdits is { Count: > 0 })
                {
                    var detailError = await ApplyDetailEditsAsync(card.FID, request.DetailEdits);
                    if (!string.IsNullOrWhiteSpace(detailError))
                    {
                        return CardOperationResult.Fail(detailError);
                    }
                }

                // 处理supplementData
                if (request.SupplementData != null && request.SupplementData.Count > 0)
                {
                    stageInstance.FSupplementDataJson = JsonSerializer.Serialize(request.SupplementData);

                    // 按 stage 定义的 FInputFieldsJson 白名单，将允许字段合并回 card.FDataJson
                    MergeSupplementIntoCardData(card, normalizedConfig, request.SupplementData);
                }

                // 完成当前Assignee
                assignee.FStatus = "approved";
                assignee.FOpinion = request.Opinion;
                assignee.FCompletedTime = DateTime.Now;
                // 显式标记为 Modified，避免 ChangeTracker 边界场景漏发 UPDATE
                _dbContext.Entry(assignee).State = EntityState.Modified;

                // 判定节点是否完成
                // 先保存 assignee 状态变更、再重查 —— 避免 EF Identity Resolution 不生效、或投影查询读到旧 DB 值
                await _dbContext.SaveChangesAsync();
                var allAssignees = await _dbContext.Set<CfStageAssignee>()
                    .Where(a => a.FStageInstanceId == stageInstance.FID)
                    .Select(a => new AssigneeStatus(a.FUserId, a.FStatus))
                    .ToListAsync();

                if (_approvalHandler.IsStageCompleted(stageInstance.FApprovalMode, allAssignees))
                {
                    // 节点完成，推进到下一节点
                    stageInstance.FStatus = "completed";
                    stageInstance.FFinalAction = "approved";
                    stageInstance.FOpinion = request.Opinion;
                    stageInstance.FCompletedTime = DateTime.Now;
                    // 显式标记为 Modified
                    _dbContext.Entry(stageInstance).State = EntityState.Modified;

                    // 完成相关待办
                    await CompleteStageTodosAsync(stageInstance.FID);

                    if (stageInstance.FIsDynamicInsert && stageInstance.FInsertSourceStageId.HasValue)
                    {
                        await HandleDynamicStageCompletedAsync(card, stageInstance);
                    }
                    else if (await TryStartDynamicStageAsync(card, stageInstance, "afterTarget"))
                    {
                        // afterTarget 策略命中时，动态节点完成后再从原目标节点继续流转。
                    }
                    else
                    {
                        // 推进到下一节点
                        await AdvanceToNextStageAsync(card, stageInstance);
                    }
                }
                else
                {
                    // 会签/顺签等待其他人，仅完成当前待办
                    var todo = await _dbContext.Set<CfTodoItem>()
                        .FirstOrDefaultAsync(t => t.FStageInstanceId == stageInstance.FID
                            && t.FHandlerId == operatorId && t.FStatus == "pending");
                    if (todo != null)
                    {
                        todo.FStatus = "completed";
                        todo.FCompletedTime = DateTime.Now;
                        await _notificationDispatcher.DispatchCompleteTodoAsync(todo.FID);
                    }

                    if (string.Equals(stageInstance.FApprovalMode, "sequential", StringComparison.OrdinalIgnoreCase))
                    {
                        var stageAssignees = await _dbContext.Set<CfStageAssignee>()
                            .Where(a => a.FStageInstanceId == stageInstance.FID)
                            .ToListAsync();
                        var nextAssignee = _sequentialRuntime.PromoteNextWaitingAssignee(stageAssignees);
                        if (nextAssignee != null)
                        {
                            _dbContext.Entry(nextAssignee).State = EntityState.Modified;
                            await _todoService.CreateTodoAsync(card.FID, stageInstance.FID,
                                nextAssignee.FUserId, nextAssignee.FUserName, card.FTitle ?? "待办");
                        }
                    }
                }

                await LogActionAsync(card.FID, stageInstance.FID, "approve", operatorId,
                    assignee.FUserName, request.Opinion);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return CardOperationResult.Ok(card.FID, "审批通过");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "审批失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"审批失败: {ex.Message}");
            }
        });
    }

    public async Task<CardOperationResult> RejectAsync(long cardId, long operatorId, RejectRequest request)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FStatus != "active") return CardOperationResult.Fail("卡片不在审批中");

                // ConcurrencyStamp验证
                if (!string.IsNullOrWhiteSpace(request.ConcurrencyStamp))
                {
                    var currentStamp = Convert.ToBase64String(card.FRowVersion);
                    if (currentStamp != request.ConcurrencyStamp)
                        return CardOperationResult.Fail("数据已被修改，请刷新后重试");
                }

                var stageInstance = await _dbContext.Set<CfStageInstance>()
                    .FirstOrDefaultAsync(s => s.FID == card.FCurrentStageInstanceId && s.FStatus == "active");
                if (stageInstance == null) return CardOperationResult.Fail("当前无活跃节点");

                var normalizedConfig = await LoadStageConfigAsync(stageInstance.FStageDefinitionId);
                var action = IsReturnToStageMode(request.ReturnMode) ? "returnToStage" : "reject";
                var actionValidation = ValidateStageAction(normalizedConfig, action);
                if (!actionValidation.Success)
                {
                    return CardOperationResult.Fail(actionValidation.ErrorMessage!);
                }

                var assignee = await _dbContext.Set<CfStageAssignee>()
                    .FirstOrDefaultAsync(a => a.FStageInstanceId == stageInstance.FID
                        && a.FUserId == operatorId && a.FStatus == "pending");
                if (assignee == null) return CardOperationResult.Fail("您不是当前节点处理人");

                if (IsReturnToStageMode(request.ReturnMode))
                {
                    var returnResult = await ReturnToStageAsync(card, stageInstance, assignee, operatorId, request);
                    if (!returnResult.Success)
                    {
                        return returnResult;
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return returnResult;
                }

                // 标记rejected
                assignee.FStatus = "rejected";
                assignee.FOpinion = request.Opinion;
                assignee.FCompletedTime = DateTime.Now;

                // 标记当前节点返回
                stageInstance.FStatus = "returned";
                stageInstance.FFinalAction = "rejected";
                stageInstance.FOpinion = request.Opinion;
                stageInstance.FCompletedTime = DateTime.Now;

                // 卡片状态→returned
                card.FStatus = "returned";
                card.FUpdatedTime = DateTime.Now;
                card.FCurrentStageInstanceId = null;

                if (string.Equals(stageInstance.FApprovalMode, "sequential", StringComparison.OrdinalIgnoreCase))
                {
                    var assignees = await _dbContext.Set<CfStageAssignee>()
                        .Where(a => a.FStageInstanceId == stageInstance.FID)
                        .ToListAsync();
                    _sequentialRuntime.CancelOpenAssignees(assignees);
                }

                // 取消所有pending待办
                await CancelStageTodosAsync(stageInstance.FID);

                await ReleaseBudgetAsync(card, "reject");

                await LogActionAsync(card.FID, stageInstance.FID, "reject", operatorId,
                    assignee.FUserName, request.Opinion);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                // 卡片状态变更，需重算批次聚合
                TriggerBatchRefreshIfNeeded(card);

                return new CardOperationResult
                {
                    Success = true, CardId = card.FID, NewStatus = "returned", Message = "已退回"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "退回失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"退回失败: {ex.Message}");
            }
        });
    }

    private async Task<CardOperationResult> ReturnToStageAsync(
        CfCard card,
        CfStageInstance currentStage,
        CfStageAssignee operatorAssignee,
        long operatorId,
        RejectRequest request)
    {
        var stages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == card.FFlowVersionId)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync();
        var stageInstances = await _dbContext.Set<CfStageInstance>()
            .Where(s => s.FCardId == card.FID)
            .ToListAsync();
        var routeSnapshots = await _dbContext.Set<CfRouteDecisionSnapshot>()
            .Where(s => s.FCardId == card.FID && s.FRound == card.FCurrentRound)
            .ToListAsync();

        var targetResult = string.Equals(request.ReturnMode, "toSpecified", StringComparison.OrdinalIgnoreCase)
            ? _returnToStageRuntime.ResolveSpecifiedTarget(
                stages,
                stageInstances,
                currentStage.FStageDefinitionId,
                card.FCurrentRound,
                request.TargetStageId,
                routeSnapshots)
            : _returnToStageRuntime.ResolvePreviousTarget(
                stages,
                stageInstances,
                currentStage.FStageDefinitionId,
                card.FCurrentRound,
                routeSnapshots);

        if (!targetResult.Success)
        {
            return CardOperationResult.Fail(targetResult.ErrorMessage ?? "退回目标节点无效");
        }

        var targetDefinition = targetResult.TargetStageDefinition!;
        var now = DateTime.Now;

        operatorAssignee.FStatus = "rejected";
        operatorAssignee.FOpinion = request.Opinion;
        operatorAssignee.FCompletedTime = now;
        _dbContext.Entry(operatorAssignee).State = EntityState.Modified;

        currentStage.FStatus = "returned";
        currentStage.FFinalAction = "returnToStage";
        currentStage.FOpinion = request.Opinion;
        currentStage.FCompletedTime = now;
        _dbContext.Entry(currentStage).State = EntityState.Modified;

        var currentAssignees = await _dbContext.Set<CfStageAssignee>()
            .Where(a => a.FStageInstanceId == currentStage.FID)
            .ToListAsync();
        foreach (var assignee in currentAssignees.Where(a =>
                     a.FID != operatorAssignee.FID &&
                     (a.FStatus == "pending" || a.FStatus == "waiting")))
        {
            assignee.FStatus = "cancelled";
            assignee.FCompletedTime = now;
            _dbContext.Entry(assignee).State = EntityState.Modified;
        }

        await CancelStageTodosAsync(currentStage.FID);

        var supersededInstances = _returnToStageRuntime.SupersedeDownstreamCompletedStages(
            stages,
            stageInstances,
            targetDefinition.FID,
            card.FCurrentRound,
            currentStage.FStageDefinitionId,
            routeSnapshots);
        foreach (var instance in supersededInstances)
        {
            _dbContext.Entry(instance).State = EntityState.Modified;
        }

        var targetInstance = new CfStageInstance
        {
            FCardId = card.FID,
            FStageDefinitionId = targetDefinition.FID,
            FStageName = targetDefinition.FStageName,
            FType = targetDefinition.FType,
            FApprovalMode = targetDefinition.FApprovalMode,
            FRound = card.FCurrentRound,
            FStatus = "active",
            FStartTime = now,
            FActivatedTime = now
        };
        _dbContext.Set<CfStageInstance>().Add(targetInstance);
        await _dbContext.SaveChangesAsync();

        card.FStatus = "active";
        card.FCurrentStageInstanceId = targetInstance.FID;
        card.FUpdatedTime = now;
        _dbContext.Entry(card).State = EntityState.Modified;

        await AssignStageHandlersAsync(targetInstance, targetDefinition, card);

        var detailJson = JsonSerializer.Serialize(new
        {
            returnMode = request.ReturnMode,
            sourceStageInstanceId = currentStage.FID,
            targetStageDefinitionId = targetDefinition.FID,
            targetStageInstanceId = targetInstance.FID,
            visitedStageInstanceId = targetResult.VisitedStageInstance?.FID,
            supersededStageInstanceIds = supersededInstances.Select(s => s.FID).ToArray()
        });
        await LogActionAsync(card.FID, currentStage.FID, "returnToStage", operatorId,
            operatorAssignee.FUserName, request.Opinion, detailJson);

        return new CardOperationResult
        {
            Success = true,
            CardId = card.FID,
            NewStatus = "active",
            Message = $"已退回到{targetDefinition.FStageName}"
        };
    }

    public async Task<CardOperationResult> WithdrawAsync(long cardId, long operatorId)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FInitiatorId != operatorId) return CardOperationResult.Fail("只有发起人可以撤回");
                if (card.FStatus != "active") return CardOperationResult.Fail("当前状态不允许撤回");

                // 验证当前节点所有Assignee状态=pending（即无人审批）
                if (card.FCurrentStageInstanceId.HasValue)
                {
                    var assignees = await _dbContext.Set<CfStageAssignee>()
                        .Where(a => a.FStageInstanceId == card.FCurrentStageInstanceId.Value)
                        .ToListAsync();

                    if (assignees.Any(a => a.FStatus != "pending"))
                        return CardOperationResult.Fail("当前节点已有人审批，无法撤回");

                    // 取消当前节点实例
                    var stageInstance = await _dbContext.Set<CfStageInstance>()
                        .FirstOrDefaultAsync(s => s.FID == card.FCurrentStageInstanceId.Value);
                    if (stageInstance != null)
                    {
                        stageInstance.FStatus = "cancelled";
                        stageInstance.FCompletedTime = DateTime.Now;
                    }

                    // 取消所有待办
                    await CancelStageTodosAsync(card.FCurrentStageInstanceId.Value);

                    // 清理外部推送
                    var todos = await _dbContext.Set<CfTodoItem>()
                        .Where(t => t.FStageInstanceId == card.FCurrentStageInstanceId.Value)
                        .ToListAsync();
                    foreach (var todo in todos.Where(t => !string.IsNullOrWhiteSpace(t.FExternalTodoId)))
                    {
                        await _notificationDispatcher.DispatchDeleteTodoAsync(todo.FID);
                    }
                }

                card.FStatus = "draft";
                card.FCurrentStageInstanceId = null;
                card.FUpdatedTime = DateTime.Now;

                await ReleaseBudgetAsync(card, "withdraw");

                await LogActionAsync(card.FID, null, "withdraw", operatorId, card.FInitiatorName, null);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                // 卡片被撤销，需重算批次聚合
                TriggerBatchRefreshIfNeeded(card);

                return new CardOperationResult
                {
                    Success = true, CardId = card.FID, NewStatus = "draft", Message = "已撤回"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "撤回失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"撤回失败: {ex.Message}");
            }
        });
    }

    public async Task<CardOperationResult> ResubmitAsync(long cardId, long operatorId)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FStatus != "returned") return CardOperationResult.Fail("只有退回状态的卡片可以重新提交");
                if (card.FInitiatorId != operatorId) return CardOperationResult.Fail("只有发起人可以重新提交");

                var flowDef = await _dbContext.Set<CfFlowDefinition>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FID == card.FFlowDefinitionId);
                var budgetPreview = await PreviewBudgetBeforeSubmitAsync(card, flowDef);
                if (budgetPreview?.Blocked == true)
                {
                    return CardOperationResult.Fail($"超预算，缺口 {budgetPreview.GapAmount:N2}，当前预算策略不允许提交");
                }

                // 轮次+1
                card.FCurrentRound += 1;
                card.FStatus = "active";
                card.FUpdatedTime = DateTime.Now;

                // 获取第一个阶段定义（重新提交从第一个节点开始）
                var stages = await _dbContext.Set<CfStageDefinition>()
                    .Where(s => s.FFlowVersionId == card.FFlowVersionId)
                    .OrderBy(s => s.FSortOrder)
                    .ToListAsync();

                if (stages.Count == 0) return CardOperationResult.Fail("流程无节点定义");

                var firstStage = stages[0];
                var stageInstance = new CfStageInstance
                {
                    FCardId = card.FID,
                    FStageDefinitionId = firstStage.FID,
                    FStageName = firstStage.FStageName,
                    FType = firstStage.FType,
                    FApprovalMode = firstStage.FApprovalMode,
                    FRound = card.FCurrentRound,
                    FStatus = "active",
                    FActivatedTime = DateTime.Now,
                    FStartTime = DateTime.Now
                };
                _dbContext.Set<CfStageInstance>().Add(stageInstance);
                await _dbContext.SaveChangesAsync();

                card.FCurrentStageInstanceId = stageInstance.FID;
                await OccupyBudgetOnSubmitAsync(card, flowDef, $"card:{card.FID}:submit:resubmit:{card.FCurrentRound}");

                // 分配处理人+创建待办（auto 节点则直接执行自动插件）
                if (string.Equals(firstStage.FType, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteAutoStageAsync(card, stageInstance, firstStage);
                }
                else
                {
                    await AssignStageHandlersAsync(stageInstance, firstStage, card);
                }

                await LogActionAsync(card.FID, stageInstance.FID, "resubmit", operatorId, card.FInitiatorName, null);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return new CardOperationResult
                {
                    Success = true, CardId = card.FID, CardNumber = card.FCardNumber, NewStatus = "active", Message = "已重新提交"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "重新提交失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"重新提交失败: {ex.Message}");
            }
        });
    }

    public async Task<CardOperationResult> VoidAsync(long cardId, long operatorId, string? opinion = null)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FStatus == "voided") return CardOperationResult.Fail("卡片已作废");

                // 验证权限（发起人或管理员 - 此处简化为发起人）
                if (card.FInitiatorId != operatorId)
                    return CardOperationResult.Fail("无权限作废此卡片");

                // 取消所有pending节点实例
                var pendingStages = await _dbContext.Set<CfStageInstance>()
                    .Where(s => s.FCardId == cardId && (s.FStatus == "pending" || s.FStatus == "active"))
                    .ToListAsync();

                foreach (var stage in pendingStages)
                {
                    stage.FStatus = "cancelled";
                    stage.FCompletedTime = DateTime.Now;
                    await CancelStageTodosAsync(stage.FID);
                }

                // 回滚冲销余额
                var balances = await _dbContext.Set<CfCardBalance>()
                    .Where(b => b.FCardId == cardId && b.FStatus == "active")
                    .ToListAsync();
                foreach (var balance in balances)
                {
                    balance.FStatus = "voided";
                    balance.FUpdatedTime = DateTime.Now;
                }

                // 清理外部推送
                var allTodos = await _dbContext.Set<CfTodoItem>()
                    .Where(t => t.FCardId == cardId && !string.IsNullOrWhiteSpace(t.FExternalTodoId))
                    .ToListAsync();
                foreach (var todo in allTodos.Where(t => t.FStatus == "pending"))
                {
                    await _notificationDispatcher.DispatchDeleteTodoAsync(todo.FID);
                }

                card.FStatus = "voided";
                card.FCurrentStageInstanceId = null;
                card.FUpdatedTime = DateTime.Now;

                await ReleaseBudgetAsync(card, "void");

                await LogActionAsync(card.FID, null, "void", operatorId, card.FInitiatorName, opinion);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                // 卡片作废，需重算批次聚合
                TriggerBatchRefreshIfNeeded(card);

                // 编排回调：通知编排引擎卡片已作废
                await NotifyOrchestrationOnCardCompletedAsync(card);

                return new CardOperationResult
                {
                    Success = true, CardId = card.FID, NewStatus = "voided", Message = "已作废"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "作废失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"作废失败: {ex.Message}");
            }
        });
    }

    public async Task<CardOperationResult> CountersignAsync(long cardId, long operatorId, CountersignRequest request)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FStatus != "active") return CardOperationResult.Fail("卡片不在审批中");

                var currentStage = await _dbContext.Set<CfStageInstance>()
                    .FirstOrDefaultAsync(s => s.FID == card.FCurrentStageInstanceId && s.FStatus == "active");
                if (currentStage == null) return CardOperationResult.Fail("当前无活跃节点");

                var normalizedConfig = await LoadStageConfigAsync(currentStage.FStageDefinitionId);
                var actionName = string.Equals(request.InsertMode, "before", StringComparison.OrdinalIgnoreCase)
                    ? "addSignBefore"
                    : "addSignAfter";
                var actionValidation = ValidateStageAction(normalizedConfig, actionName);
                if (!actionValidation.Success)
                {
                    return CardOperationResult.Fail(actionValidation.ErrorMessage!);
                }

                var requesterAssignee = await _dbContext.Set<CfStageAssignee>()
                    .FirstOrDefaultAsync(a => a.FStageInstanceId == currentStage.FID
                        && a.FUserId == operatorId
                        && a.FStatus == "pending");
                if (requesterAssignee == null) return CardOperationResult.Fail("您不是当前节点处理人");

                var now = DateTime.Now;
                var insertMode = string.Equals(request.InsertMode, "before", StringComparison.OrdinalIgnoreCase)
                    ? "before"
                    : "after";
                var sourceWasComplete = false;
                var suspendedAssigneeIds = new List<long>();

                if (insertMode == "before")
                {
                    currentStage.FStatus = "suspended";
                    _dbContext.Entry(currentStage).State = EntityState.Modified;

                    requesterAssignee.FStatus = "waiting";
                    requesterAssignee.FCompletedTime = null;
                    _dbContext.Entry(requesterAssignee).State = EntityState.Modified;

                    await CancelPendingTodoAsync(currentStage.FID, requesterAssignee.FUserId, now);
                    suspendedAssigneeIds.Add(requesterAssignee.FID);
                }
                else
                {
                    requesterAssignee.FStatus = "approved";
                    requesterAssignee.FOpinion = request.Opinion;
                    requesterAssignee.FCompletedTime = now;
                    _dbContext.Entry(requesterAssignee).State = EntityState.Modified;
                    await CompletePendingTodoAsync(currentStage.FID, requesterAssignee.FUserId, now);
                    await _dbContext.SaveChangesAsync();

                    var allAssignees = await _dbContext.Set<CfStageAssignee>()
                        .Where(a => a.FStageInstanceId == currentStage.FID)
                        .Select(a => new AssigneeStatus(a.FUserId, a.FStatus))
                        .ToListAsync();
                    sourceWasComplete = _approvalHandler.IsStageCompleted(currentStage.FApprovalMode, allAssignees);
                    if (sourceWasComplete)
                    {
                        currentStage.FStatus = "completed";
                        currentStage.FFinalAction = "approved";
                        currentStage.FOpinion = request.Opinion;
                        currentStage.FCompletedTime = now;
                        _dbContext.Entry(currentStage).State = EntityState.Modified;
                    }
                    else
                    {
                        currentStage.FStatus = "suspended";
                        _dbContext.Entry(currentStage).State = EntityState.Modified;

                        var openAssignees = await _dbContext.Set<CfStageAssignee>()
                            .Where(a => a.FStageInstanceId == currentStage.FID && a.FStatus == "pending")
                            .ToListAsync();
                        foreach (var assignee in openAssignees)
                        {
                            assignee.FStatus = "waiting";
                            assignee.FCompletedTime = null;
                            _dbContext.Entry(assignee).State = EntityState.Modified;
                            suspendedAssigneeIds.Add(assignee.FID);
                            await CancelPendingTodoAsync(currentStage.FID, assignee.FUserId, now);
                        }
                    }
                }

                // 创建动态节点实例（加签）
                var dynamicStage = new CfStageInstance
                {
                    FCardId = card.FID,
                    FStageDefinitionId = currentStage.FStageDefinitionId,
                    FStageName = $"{currentStage.FStageName}(加签)",
                    FType = "human",
                    FApprovalMode = "single",
                    FRound = card.FCurrentRound,
                    FStatus = "active",
                    FIsDynamicInsert = true,
                    FInsertSourceStageId = currentStage.FID,
                    FInsertContextJson = JsonSerializer.Serialize(new
                    {
                        insertMode,
                        sourceStageInstanceId = currentStage.FID,
                        requesterAssigneeId = requesterAssignee.FID,
                        sourceWasComplete,
                        suspendedAssigneeIds = suspendedAssigneeIds.ToArray()
                    }),
                    FActivatedTime = now,
                    FStartTime = now
                };
                _dbContext.Set<CfStageInstance>().Add(dynamicStage);
                await _dbContext.SaveChangesAsync();

                card.FCurrentStageInstanceId = dynamicStage.FID;
                card.FUpdatedTime = now;
                _dbContext.Entry(card).State = EntityState.Modified;

                var newUserName = await ResolveUserNameAsync(request.UserId);

                // 分配新处理人
                var newAssignee = new CfStageAssignee
                {
                    FStageInstanceId = dynamicStage.FID,
                    FUserId = request.UserId,
                    FUserName = newUserName,
                    FAssignedTime = now,
                    FStatus = "pending"
                };
                _dbContext.Set<CfStageAssignee>().Add(newAssignee);
                await _dbContext.SaveChangesAsync();

                // 创建待办
                await _todoService.CreateTodoAsync(card.FID, dynamicStage.FID,
                    request.UserId, newAssignee.FUserName, card.FTitle ?? "待办");

                await LogActionAsync(card.FID, dynamicStage.FID, "countersign", operatorId,
                    requesterAssignee.FUserName, request.Opinion, dynamicStage.FInsertContextJson);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return CardOperationResult.Ok(card.FID, "加签成功");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "加签失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"加签失败: {ex.Message}");
            }
        });
    }

    public async Task<CardOperationResult> TransferAsync(long cardId, long operatorId, TransferRequest request)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");
                if (card.FStatus != "active") return CardOperationResult.Fail("卡片不在审批中");

                var stageInstance = await _dbContext.Set<CfStageInstance>()
                    .FirstOrDefaultAsync(s => s.FID == card.FCurrentStageInstanceId && s.FStatus == "active");
                if (stageInstance == null) return CardOperationResult.Fail("当前无活跃节点");

                var normalizedConfig = await LoadStageConfigAsync(stageInstance.FStageDefinitionId);
                var actionValidation = ValidateStageAction(normalizedConfig, "transfer");
                if (!actionValidation.Success)
                {
                    return CardOperationResult.Fail(actionValidation.ErrorMessage!);
                }

                // 标记原Assignee为transferred
                var originalAssignee = await _dbContext.Set<CfStageAssignee>()
                    .FirstOrDefaultAsync(a => a.FStageInstanceId == stageInstance.FID
                        && a.FUserId == operatorId && a.FStatus == "pending");
                if (originalAssignee == null) return CardOperationResult.Fail("您不是当前节点处理人");

                originalAssignee.FStatus = "transferred";
                originalAssignee.FOpinion = request.Opinion;
                originalAssignee.FCompletedTime = DateTime.Now;

                var newUserName = await ResolveUserNameAsync(request.NewUserId);

                // 创建新Assignee
                var newAssignee = new CfStageAssignee
                {
                    FStageInstanceId = stageInstance.FID,
                    FUserId = request.NewUserId,
                    FUserName = newUserName,
                    FAssignedTime = DateTime.Now,
                    FStatus = "pending"
                };
                _dbContext.Set<CfStageAssignee>().Add(newAssignee);

                // 取消原待办，创建新待办
                var oldTodo = await _dbContext.Set<CfTodoItem>()
                    .FirstOrDefaultAsync(t => t.FStageInstanceId == stageInstance.FID
                        && t.FHandlerId == operatorId && t.FStatus == "pending");
                if (oldTodo != null)
                {
                    oldTodo.FStatus = "cancelled";
                    oldTodo.FCompletedTime = DateTime.Now;
                    await _notificationDispatcher.DispatchDeleteTodoAsync(oldTodo.FID);
                }

                await _dbContext.SaveChangesAsync();

                await _todoService.CreateTodoAsync(card.FID, stageInstance.FID,
                    request.NewUserId, newAssignee.FUserName, card.FTitle ?? "待办");

                await LogActionAsync(card.FID, stageInstance.FID, "transfer", operatorId,
                    originalAssignee.FUserName, request.Opinion);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return CardOperationResult.Ok(card.FID, "转办成功");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "转办失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"转办失败: {ex.Message}");
            }
        });
    }

    public async Task<CardOperationResult> CcAsync(long cardId, long operatorId, CcRequest request)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
        if (card == null) return CardOperationResult.Fail("卡片不存在");

        var stageInstanceId = card.FCurrentStageInstanceId ?? 0;
        if (stageInstanceId > 0)
        {
            var stageInstance = await _dbContext.Set<CfStageInstance>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.FID == stageInstanceId);
            var normalizedConfig = await LoadStageConfigAsync(stageInstance?.FStageDefinitionId);
            var actionValidation = ValidateStageAction(normalizedConfig, "cc");
            if (!actionValidation.Success)
            {
                return CardOperationResult.Fail(actionValidation.ErrorMessage!);
            }
        }

        foreach (var userId in request.UserIds)
        {
            await _todoService.CreateTodoAsync(card.FID, stageInstanceId,
                userId, userId.ToString(), card.FTitle ?? "抄送通知", "cc");
        }

        await LogActionAsync(card.FID, stageInstanceId > 0 ? stageInstanceId : null,
            "cc", operatorId, card.FInitiatorName, request.Opinion);

        await _dbContext.SaveChangesAsync();
        return CardOperationResult.Ok(card.FID, "抄送成功");
    }

    public async Task<CardOperationResult> UrgeAsync(long cardId, long operatorId, string? message = null)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
        if (card == null) return CardOperationResult.Fail("卡片不存在");
        if (card.FStatus != "active") return CardOperationResult.Fail("卡片不在审批中");

        // 查找当前节点的待办并触发通知
        if (card.FCurrentStageInstanceId.HasValue)
        {
            var pendingTodos = await _dbContext.Set<CfTodoItem>()
                .Where(t => t.FStageInstanceId == card.FCurrentStageInstanceId.Value && t.FStatus == "pending")
                .ToListAsync();

            foreach (var todo in pendingTodos)
            {
                await _notificationDispatcher.DispatchCreateTodoAsync(todo.FID);
            }
        }

        await LogActionAsync(card.FID, card.FCurrentStageInstanceId, "urge", operatorId,
            card.FInitiatorName, message);
        await _dbContext.SaveChangesAsync();

        return CardOperationResult.Ok(card.FID, "催办成功");
    }

    public async Task<CardOperationResult> ResumeAsync(long cardId, long operatorId)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId);
                if (card == null) return CardOperationResult.Fail("卡片不存在");

                // 查找failed状态节点
                var failedStage = await _dbContext.Set<CfStageInstance>()
                    .FirstOrDefaultAsync(s => s.FCardId == cardId && s.FStatus == "failed");
                if (failedStage == null) return CardOperationResult.Fail("未找到失败的节点");

                // 重新标记为active
                failedStage.FStatus = "active";
                failedStage.FActivatedTime = DateTime.Now;
                card.FCurrentStageInstanceId = failedStage.FID;
                card.FStatus = "active";
                card.FUpdatedTime = DateTime.Now;

                // 如果是自动节点，重新调度自动插件
                if (string.Equals(failedStage.FType, "auto", StringComparison.OrdinalIgnoreCase) && failedStage.FStageDefinitionId.HasValue)
                {
                    var stageDef = await _dbContext.Set<CfStageDefinition>()
                        .FirstOrDefaultAsync(s => s.FID == failedStage.FStageDefinitionId.Value);
                    if (stageDef != null)
                    {
                        await ExecuteAutoStageAsync(card, failedStage, stageDef);
                    }
                }

                await LogActionAsync(card.FID, failedStage.FID, "resume", operatorId, "管理员", null);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return CardOperationResult.Ok(card.FID, "已恢复执行");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "恢复失败，CardId={CardId}", cardId);
                return CardOperationResult.Fail($"恢复失败: {ex.Message}");
            }
        });
    }

    #region 内部方法

    /// <summary>
    /// 批次级链结束后，自动提交所有 FanOut 创建的卡片，启动卡片级节点链。
    /// 逐卡推进到第一个卡片级节点（auto 节点则直接执行插件，human 节点则分配处理人）。
    /// </summary>
    private async Task ProcessCardStagesForBatchAsync(CfBatch batch, CancellationToken ct)
    {
        // 卡片由 BatchSummary/FanOut 等批次级插件在“共享 DbContext”里 Add+SaveChanges 创建，
        // 保存后这些 CfCard 实例仍处于 ChangeTracker 跟踪态(Unchanged)。
        // 生产环境全局启用 NoTrackingWithIdentityResolution(Program.cs)，默认无跟踪重查询会返回
        // 全新未跟踪实例；随后循环里 `_dbContext.Entry(card).State = Modified` 与已跟踪的同主键实例
        // 撞 key，抛 "CfCard cannot be tracked because another instance with the same key value is
        // already being tracked"，批次卡在 CardCreated(3) 无法到终态。
        // 显式 AsTracking 让重查询复用插件已跟踪的同一实例(标识解析)，消除重复跟踪冲突，
        // 同时保证后续 ExecuteAutoStageAsync 内 `Entry(card).ReloadAsync()` 作用于已跟踪实例。
        var cards = await _dbContext.Set<CfCard>()
            .AsTracking()
            .Where(c => c.FBatchId == batch.FID && c.FStatus == "draft")
            .ToListAsync(ct);

        if (cards.Count == 0) return;

        // 获取当前发布版本
        var version = await _dbContext.Set<CfFlowVersion>()
            .Where(v => v.FFlowDefinitionId == batch.FFlowDefinitionId && v.FIsCurrentVersion)
            .FirstOrDefaultAsync(ct);
        if (version == null) return;

        // 获取所有节点定义
        var stages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == version.FID)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync(ct);

        // 找到第一个卡片级节点（非批次级）
        var firstCardStage = stages.FirstOrDefault(s => !IsBatchAutoStage(s));
        if (firstCardStage == null)
        {
            // 无卡片级节点 → 所有卡片直接完成
            foreach (var card in cards)
            {
                card.FFlowVersionId = version.FID;
                await CompleteCardAsync(card);
            }
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        var flowDef = await _dbContext.Set<CfFlowDefinition>()
            .FirstOrDefaultAsync(f => f.FID == batch.FFlowDefinitionId, ct);

        foreach (var card in cards)
        {
            ct.ThrowIfCancellationRequested();

            // 锁定版本
            card.FFlowVersionId = version.FID;

            // 生成编号
            if (string.IsNullOrWhiteSpace(card.FCardNumber) && flowDef != null && !string.IsNullOrWhiteSpace(flowDef.FNumberTemplate))
            {
                card.FCardNumber = await _numberService.GenerateNumberAsync(flowDef.FID, card.FOrgId, flowDef.FNumberTemplate);
            }

            // 生成标题
            if (flowDef != null)
            {
                card.FTitle = _schemaService.GenerateTitle(
                    flowDef.FTitleTemplate ?? "",
                    card.FDataJson ?? "{}",
                    flowDef.FFlowName,
                    card.FCardNumber ?? "");
            }

            var budgetPreview = await PreviewBudgetBeforeSubmitAsync(card, flowDef);
            if (budgetPreview?.Blocked == true)
            {
                card.FStatus = "exception";
                card.FUpdatedTime = DateTime.Now;
                _dbContext.Entry(card).State = EntityState.Modified;
                await LogActionAsync(card.FID, null, "budget-blocked", 0, "system",
                    $"超预算，缺口 {budgetPreview.GapAmount:N2}");
                continue;
            }

            // 更新卡片状态为 active
            card.FStatus = "active";
            card.FCurrentRound += 1;
            card.FSubmitTime = DateTime.Now;
            card.FUpdatedTime = DateTime.Now;
            _dbContext.Entry(card).State = EntityState.Modified;

            // 创建第一个卡片级节点实例
            var stageInstance = new CfStageInstance
            {
                FCardId = card.FID,
                FStageDefinitionId = firstCardStage.FID,
                FStageName = firstCardStage.FStageName,
                FType = firstCardStage.FType,
                FApprovalMode = firstCardStage.FApprovalMode,
                FRound = card.FCurrentRound,
                FStatus = "active",
                FActivatedTime = DateTime.Now,
                FStartTime = DateTime.Now
            };
            _dbContext.Set<CfStageInstance>().Add(stageInstance);
            await _dbContext.SaveChangesAsync(ct);

            card.FCurrentStageInstanceId = stageInstance.FID;
            _dbContext.Entry(card).State = EntityState.Modified;
            await OccupyBudgetOnSubmitAsync(card, flowDef, $"card:{card.FID}:submit:auto:{card.FCurrentRound}");

            // 区分 auto / human 节点类型分发
            if (string.Equals(firstCardStage.FType, "auto", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteAutoStageAsync(card, stageInstance, firstCardStage);
            }
            else
            {
                await AssignStageHandlersAsync(stageInstance, firstCardStage, card);
            }

            // 记录ActionLog
            await LogActionAsync(card.FID, stageInstance.FID, "auto-submit", 0, "system", "批次级链完成，自动提交卡片");
        }

        // 更新批次状态为处理中（含 SignalR 推送）
        ReloadBatchDetachConflicts(batch);
        try { await _dbContext.Entry(batch).ReloadAsync(ct); } catch { /* 忽略 */ }
        await _batchLifecycle.TransitionBatchStatusAsync(batch, 4);
    }

    private async Task AdvanceToNextStageAsync(CfCard card, CfStageInstance currentStage)
    {
        if (await TryStartDynamicStageAsync(card, currentStage, "afterSourceBeforeRoute"))
            return;

        await AdvanceToNextStageCoreAsync(card, currentStage);
    }

    private async Task AdvanceToNextStageCoreAsync(CfCard card, CfStageInstance currentStage)
    {
        var routeResult = await _stageRouteResolver.ResolveNextStageAsync(card, currentStage);
        if (routeResult.RuleMode)
        {
            if (routeResult.NextStage == null)
            {
                await WriteRouteDecisionSnapshotAsync(card, currentStage, routeResult);
                await CompleteCardAsync(card);
                return;
            }

            await StartResolvedNextStageAsync(card, currentStage, routeResult);
            return;
        }

        var stages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == card.FFlowVersionId)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync();

        // 找到当前节点在定义中的位置
        var currentDefIdx = stages.FindIndex(s => s.FID == currentStage.FStageDefinitionId);
        if (currentDefIdx < 0)
        {
            // 动态插入节点，找到原始节点位置
            if (currentStage.FInsertSourceStageId.HasValue)
            {
                var sourceStage = await _dbContext.Set<CfStageInstance>()
                    .FirstOrDefaultAsync(s => s.FID == currentStage.FInsertSourceStageId.Value);
                if (sourceStage != null)
                    currentDefIdx = stages.FindIndex(s => s.FID == sourceStage.FStageDefinitionId);
            }
        }

        // 寻找下一个有效节点
        var dataDict = ParseCardData(card.FDataJson);
        for (int i = currentDefIdx + 1; i < stages.Count; i++)
        {
            var nextDef = stages[i];

            // 评估条件
            if (!string.IsNullOrWhiteSpace(nextDef.FConditionJson))
            {
                var schemaFields = GetSchemaFields(card.FFlowVersionId);
                if (!_conditionEvaluator.Evaluate(nextDef.FConditionJson, dataDict, await schemaFields))
                {
                    // 条件不满足，跳过
                    continue;
                }
            }

            // 创建下一个节点实例
            var nextInstance = new CfStageInstance
            {
                FCardId = card.FID,
                FStageDefinitionId = nextDef.FID,
                FStageName = nextDef.FStageName,
                FType = nextDef.FType,
                FApprovalMode = nextDef.FApprovalMode,
                FRound = card.FCurrentRound,
                FStatus = "active",
                FActivatedTime = DateTime.Now,
                FStartTime = DateTime.Now
            };
            _dbContext.Set<CfStageInstance>().Add(nextInstance);
            await _dbContext.SaveChangesAsync();

            card.FCurrentStageInstanceId = nextInstance.FID;
            card.FUpdatedTime = DateTime.Now;
            // 显式标记 card 为 Modified，避免 ChangeTracker 边界场景漏发 UPDATE
            _dbContext.Entry(card).State = EntityState.Modified;

            // 区分 auto / human 节点类型分发
            if (string.Equals(nextDef.FType, "auto", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteAutoStageAsync(card, nextInstance, nextDef);
                return;
            }

            // 人工节点：分配处理人
            await AssignStageHandlersAsync(nextInstance, nextDef, card);
            return;
        }

        // 所有节点都已完成或跳过，卡片完成
        await CompleteCardAsync(card);
    }

    private async Task StartResolvedNextStageAsync(
        CfCard card,
        CfStageInstance currentStage,
        STOTOP.Module.CardFlow.Models.Rules.StageRouteResolveResult routeResult)
    {
        var nextDef = routeResult.NextStage ?? throw new InvalidOperationException("条件路由未解析到目标节点");
        if (await TryStartDynamicStageAsync(card, currentStage, "afterRouteBeforeTarget", routeResult))
            return;

        var replaceHandlers = await _dynamicStagePolicyResolver.ResolveAsync(
            card,
            currentStage,
            "replaceTargetHandlers",
            routeResult);
        if (!string.IsNullOrWhiteSpace(replaceHandlers.ErrorMessage))
            throw new InvalidOperationException(replaceHandlers.ErrorMessage);
        if (replaceHandlers.ShouldReplaceTargetHandlers
            && string.Equals(nextDef.FType, "auto", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("replaceTargetHandlers 不能用于自动节点");
        }

        var nextInstance = new CfStageInstance
        {
            FCardId = card.FID,
            FStageDefinitionId = nextDef.FID,
            FStageName = nextDef.FStageName,
            FType = nextDef.FType,
            FApprovalMode = nextDef.FApprovalMode,
            FRound = card.FCurrentRound,
            FStatus = "active",
            FActivatedTime = DateTime.Now,
            FStartTime = DateTime.Now
        };
        _dbContext.Set<CfStageInstance>().Add(nextInstance);
        await WriteRouteDecisionSnapshotAsync(card, currentStage, routeResult);
        await _dbContext.SaveChangesAsync();

        card.FCurrentStageInstanceId = nextInstance.FID;
        card.FUpdatedTime = DateTime.Now;
        _dbContext.Entry(card).State = EntityState.Modified;

        if (string.Equals(nextDef.FType, "auto", StringComparison.OrdinalIgnoreCase))
        {
            await ExecuteAutoStageAsync(card, nextInstance, nextDef);
            return;
        }

        if (replaceHandlers.ShouldReplaceTargetHandlers)
        {
            await AssignResolvedApproversAsync(nextInstance, replaceHandlers.ApprovalMode, replaceHandlers.Approvers, card);
            await LogActionAsync(card.FID, nextInstance.FID, "dynamic-policy-replace-handlers", 0, "system", replaceHandlers.InsertContextJson);
            return;
        }

        await AssignStageHandlersAsync(nextInstance, nextDef, card);
    }

    private async Task<bool> TryStartDynamicStageAsync(
        CfCard card,
        CfStageInstance currentStage,
        string triggerTiming,
        STOTOP.Module.CardFlow.Models.Rules.StageRouteResolveResult? routeResult = null)
    {
        var policyResult = await _dynamicStagePolicyResolver.ResolveAsync(card, currentStage, triggerTiming, routeResult);
        if (!string.IsNullOrWhiteSpace(policyResult.ErrorMessage))
            throw new InvalidOperationException(policyResult.ErrorMessage);
        if (!policyResult.ShouldInsert || policyResult.Approvers.Count == 0)
            return false;

        var now = DateTime.Now;
        var dynamicStage = new CfStageInstance
        {
            FCardId = card.FID,
            FStageDefinitionId = currentStage.FStageDefinitionId,
            FStageName = policyResult.StageName,
            FType = "human",
            FApprovalMode = policyResult.ApprovalMode,
            FRound = card.FCurrentRound,
            FStatus = "active",
            FIsDynamicInsert = true,
            FInsertSourceStageId = currentStage.FID,
            FInsertContextJson = policyResult.InsertContextJson,
            FActivatedTime = now,
            FStartTime = now
        };
        _dbContext.Set<CfStageInstance>().Add(dynamicStage);
        if (routeResult?.RuleMode == true)
        {
            await WriteRouteDecisionSnapshotAsync(card, currentStage, routeResult);
        }
        await _dbContext.SaveChangesAsync();

        card.FCurrentStageInstanceId = dynamicStage.FID;
        card.FUpdatedTime = now;
        _dbContext.Entry(card).State = EntityState.Modified;

        var assignments = _sequentialRuntime.BuildInitialAssignments(policyResult.ApprovalMode, policyResult.Approvers);
        foreach (var assignment in assignments)
        {
            _dbContext.Set<CfStageAssignee>().Add(new CfStageAssignee
            {
                FStageInstanceId = dynamicStage.FID,
                FUserId = assignment.UserId,
                FUserName = assignment.UserName,
                FSortOrder = assignment.SortOrder,
                FAssignedTime = now,
                FStatus = assignment.Status
            });
        }
        await _dbContext.SaveChangesAsync();

        foreach (var assignment in assignments.Where(a => a.Status == "pending"))
        {
            await _todoService.CreateTodoAsync(card.FID, dynamicStage.FID,
                assignment.UserId, assignment.UserName, card.FTitle ?? "待办");
        }

        await LogActionAsync(card.FID, dynamicStage.FID, "dynamic-policy-insert", 0, "system", policyResult.InsertContextJson);
        return true;
    }

    private async Task WriteRouteDecisionSnapshotAsync(
        CfCard card,
        CfStageInstance currentStage,
        STOTOP.Module.CardFlow.Models.Rules.StageRouteResolveResult routeResult)
    {
        var context = await new ConditionEvaluationContextBuilder(_dbContext).BuildAsync(card, currentStage);
        var snapshotJson = _auditSnapshotPolicyService.BuildRouteDecisionSnapshotJson(context, routeResult);
        _dbContext.Set<CfRouteDecisionSnapshot>().Add(new CfRouteDecisionSnapshot
        {
            FCardId = card.FID,
            FSourceStageInstanceId = currentStage.FID,
            FFromStageDefinitionId = currentStage.FStageDefinitionId,
            FFromStageKey = routeResult.FromStageKey,
            FSelectedRouteRuleId = routeResult.SelectedRoute?.FID,
            FSelectedEdgeKey = routeResult.SelectedRoute?.FEdgeKey,
            FToStageDefinitionId = routeResult.NextStage?.FID,
            FToStageKey = routeResult.NextStage?.FStageKey,
            FCandidateResultsJson = routeResult.CandidateResultsJson,
            FDecisionSnapshotJson = snapshotJson,
            FReason = routeResult.Reason,
            FOperatorId = 0,
            FDecisionTime = DateTime.Now,
            FRound = card.FCurrentRound
        });
    }

    private async Task HandleDynamicStageCompletedAsync(CfCard card, CfStageInstance dynamicStage)
    {
        var context = ParseDynamicInsertContext(dynamicStage.FInsertContextJson);
        if (string.Equals(context.TriggerTiming, "afterRouteBeforeTarget", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(context.ContinuationStageKey))
        {
            await StartStageByKeyAsync(card, context.ContinuationStageKey);
            return;
        }

        var sourceStageId = context.SourceStageInstanceId ?? dynamicStage.FInsertSourceStageId;
        if (!sourceStageId.HasValue)
        {
            await AdvanceToNextStageAsync(card, dynamicStage);
            return;
        }

        var sourceStage = await _dbContext.Set<CfStageInstance>()
            .FirstOrDefaultAsync(s => s.FID == sourceStageId.Value);
        if (sourceStage == null)
        {
            await AdvanceToNextStageAsync(card, dynamicStage);
            return;
        }

        if (string.Equals(context.TriggerTiming, "afterSourceBeforeRoute", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.TriggerTiming, "afterTarget", StringComparison.OrdinalIgnoreCase))
        {
            await AdvanceToNextStageCoreAsync(card, sourceStage);
            return;
        }

        if (string.Equals(context.InsertMode, "before", StringComparison.OrdinalIgnoreCase))
        {
            sourceStage.FStatus = "active";
            sourceStage.FActivatedTime ??= DateTime.Now;
            _dbContext.Entry(sourceStage).State = EntityState.Modified;

            if (context.RequesterAssigneeId.HasValue)
            {
                await ReactivateAssigneeAsync(card, sourceStage, context.RequesterAssigneeId.Value);
            }

            card.FStatus = "active";
            card.FCurrentStageInstanceId = sourceStage.FID;
            card.FUpdatedTime = DateTime.Now;
            _dbContext.Entry(card).State = EntityState.Modified;
            return;
        }

        if (context.SourceWasComplete)
        {
            await AdvanceToNextStageAsync(card, sourceStage);
            return;
        }

        sourceStage.FStatus = "active";
        sourceStage.FActivatedTime ??= DateTime.Now;
        _dbContext.Entry(sourceStage).State = EntityState.Modified;

        foreach (var assigneeId in context.SuspendedAssigneeIds)
        {
            await ReactivateAssigneeAsync(card, sourceStage, assigneeId);
        }

        card.FStatus = "active";
        card.FCurrentStageInstanceId = sourceStage.FID;
        card.FUpdatedTime = DateTime.Now;
        _dbContext.Entry(card).State = EntityState.Modified;
    }

    private async Task StartStageByKeyAsync(CfCard card, string stageKey)
    {
        var nextDef = await _dbContext.Set<CfStageDefinition>()
            .FirstOrDefaultAsync(stage => stage.FFlowVersionId == card.FFlowVersionId && stage.FStageKey == stageKey)
            ?? throw new InvalidOperationException($"动态审批续接节点不存在：{stageKey}");
        var nextInstance = new CfStageInstance
        {
            FCardId = card.FID,
            FStageDefinitionId = nextDef.FID,
            FStageName = nextDef.FStageName,
            FType = nextDef.FType,
            FApprovalMode = nextDef.FApprovalMode,
            FRound = card.FCurrentRound,
            FStatus = "active",
            FActivatedTime = DateTime.Now,
            FStartTime = DateTime.Now
        };
        _dbContext.Set<CfStageInstance>().Add(nextInstance);
        await _dbContext.SaveChangesAsync();

        card.FStatus = "active";
        card.FCurrentStageInstanceId = nextInstance.FID;
        card.FUpdatedTime = DateTime.Now;
        _dbContext.Entry(card).State = EntityState.Modified;

        if (string.Equals(nextDef.FType, "auto", StringComparison.OrdinalIgnoreCase))
        {
            await ExecuteAutoStageAsync(card, nextInstance, nextDef);
            return;
        }

        await AssignStageHandlersAsync(nextInstance, nextDef, card);
    }

    private async Task ReactivateAssigneeAsync(CfCard card, CfStageInstance sourceStage, long assigneeId)
    {
        var assignee = await _dbContext.Set<CfStageAssignee>()
            .FirstOrDefaultAsync(a => a.FID == assigneeId && a.FStageInstanceId == sourceStage.FID);
        if (assignee == null) return;

        assignee.FStatus = "pending";
        assignee.FCompletedTime = null;
        _dbContext.Entry(assignee).State = EntityState.Modified;

        var hasPendingTodo = await _dbContext.Set<CfTodoItem>()
            .AnyAsync(t => t.FStageInstanceId == sourceStage.FID
                && t.FHandlerId == assignee.FUserId
                && t.FStatus == "pending");
        if (!hasPendingTodo)
        {
            await _todoService.CreateTodoAsync(card.FID, sourceStage.FID,
                assignee.FUserId, assignee.FUserName, card.FTitle ?? "待办");
        }
    }

    private async Task AssignResolvedApproversAsync(
        CfStageInstance stageInstance,
        string approvalMode,
        IReadOnlyCollection<ResolvedApprover> approvers,
        CfCard card)
    {
        if (approvers.Count == 0)
            throw new InvalidOperationException("未解析到有效处理人");

        var normalizedApprovers = approvers
            .OrderBy(approver => approver.SortOrder)
            .Select((approver, index) => new ResolvedApprover
            {
                UserId = approver.UserId,
                UserName = string.IsNullOrWhiteSpace(approver.UserName) ? approver.UserId.ToString() : approver.UserName,
                Source = approver.Source,
                SortOrder = index + 1
            })
            .ToList();
        var assignments = _sequentialRuntime.BuildInitialAssignments(approvalMode, normalizedApprovers);

        foreach (var assignment in assignments)
        {
            _dbContext.Set<CfStageAssignee>().Add(new CfStageAssignee
            {
                FStageInstanceId = stageInstance.FID,
                FUserId = assignment.UserId,
                FUserName = assignment.UserName,
                FSortOrder = assignment.SortOrder,
                FAssignedTime = DateTime.Now,
                FStatus = assignment.Status
            });
        }
        await _dbContext.SaveChangesAsync();

        foreach (var assignment in assignments.Where(a => a.Status == "pending"))
        {
            await _todoService.CreateTodoAsync(card.FID, stageInstance.FID,
                assignment.UserId, assignment.UserName, card.FTitle ?? "待办");
        }
    }

    private static DynamicInsertContext ParseDynamicInsertContext(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
        {
            return new DynamicInsertContext();
        }

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            var root = document.RootElement;
            var suspendedAssigneeIds = new List<long>();
            if (root.TryGetProperty("suspendedAssigneeIds", out var suspendedProperty)
                && suspendedProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in suspendedProperty.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var id))
                    {
                        suspendedAssigneeIds.Add(id);
                    }
                }
            }

            return new DynamicInsertContext
            {
                InsertMode = ReadString(root, "insertMode") ?? "after",
                TriggerTiming = ReadString(root, "triggerTiming"),
                ContinuationStageKey = ReadString(root, "continuationStageKey"),
                SourceStageInstanceId = ReadLong(root, "sourceStageInstanceId"),
                RequesterAssigneeId = ReadLong(root, "requesterAssigneeId"),
                SourceWasComplete = ReadBool(root, "sourceWasComplete"),
                SuspendedAssigneeIds = suspendedAssigneeIds
            };
        }
        catch (JsonException)
        {
            return new DynamicInsertContext();
        }

        static string? ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        static long? ReadLong(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.Number
                && property.TryGetInt64(out var value)
                    ? value
                    : null;
        }

        static bool ReadBool(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.True;
        }
    }

    private async Task CompleteCardAsync(CfCard card)
    {
        card.FStatus = "completed";
        card.FCompletedTime = DateTime.Now;
        card.FCurrentStageInstanceId = null;
        card.FUpdatedTime = DateTime.Now;
        // 显式标记 card 为 Modified
        _dbContext.Entry(card).State = EntityState.Modified;

        // 处理onComplete触发器（简化：标记余额为active）
        var existingBalance = await _dbContext.Set<CfCardBalance>()
            .FirstOrDefaultAsync(b => b.FCardId == card.FID);
        if (existingBalance == null)
        {
            // 尝试从数据中解析金额创建余额记录
            var data = ParseCardData(card.FDataJson);
            if (data.TryGetValue("amount", out var amountObj) && amountObj != null)
            {
                if (decimal.TryParse(amountObj.ToString(), out var amount) && amount > 0)
                {
                    var balance = new CfCardBalance
                    {
                        FCardId = card.FID,
                        FOriginalAmount = amount,
                        FOffsetAmount = 0,
                        FRemainingAmount = amount,
                        FStatus = "active",
                        FOrgId = card.FOrgId,
                        FUpdatedTime = DateTime.Now
                    };
                    _dbContext.Set<CfCardBalance>().Add(balance);
                }
            }
        }

        await LogActionAsync(card.FID, null, "complete", card.FInitiatorId, card.FInitiatorName, null);

        await LockBudgetOnCompletionAsync(card, $"card:{card.FID}:lock:complete");
        await ConsumeBudgetIfConfirmedAsync(card);

        // 批次状态刷新触发点（仅对批量触发生成的卡片）
        TriggerBatchRefreshIfNeeded(card);

        // 编排回调：通知编排引擎卡片已完成
        await NotifyOrchestrationOnCardCompletedAsync(card);
    }

    /// <summary>
    /// 批次状态聚合调用点：在卡片进入终态/异常/撤销时调用。
    /// 通过独立 scope 解析 IBatchLifecycleService（避免与当前事务共享 DbContext），
    /// 以 fire-and-forget 方式异步触发，防止阻塞主流程。
    /// </summary>
    private void TriggerBatchRefreshIfNeeded(CfCard card)
    {
        if (!card.FBatchId.HasValue) return;
        var batchId = card.FBatchId.Value;
        _logger.LogDebug("卡片状态变更触发批次聚合：BatchId={BatchId}, CardId={CardId}", batchId, card.FID);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<IBatchLifecycleService>();
                await lifecycle.RefreshBatchStatusAsync(batchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批次 {BatchId} 状态聚合失败", batchId);
            }
        });
    }

    /// <summary>
    /// 执行 auto 节点：优先按 F插件注册ID 查出插件编码，通过 AutoPluginFactory 创建并执行插件。
    /// 成功则标记节点完成并递归推进；失败则按 FFailurePolicyJson 处理。
    /// </summary>
    private async Task ExecuteAutoStageAsync(CfCard card, CfStageInstance stageInstance, CfStageDefinition stageDef)
    {
        // 解析插件编码：优先使用 F插件注册ID；为空时降级到旧版 FAutoAgentName
        string? pluginCode = null;
#pragma warning disable CS0618
        if (stageDef.F插件注册ID.HasValue)
        {
            var registry = await _dbContext.Set<CfAutoPluginRegistry>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.FID == stageDef.F插件注册ID.Value);
            if (registry == null)
            {
                await HandleAutoStageFailureAsync(card, stageInstance, stageDef,
                    new InvalidOperationException(
                        $"未找到插件注册 ID={stageDef.F插件注册ID}"));
                return;
            }
            pluginCode = registry.F插件编码;
        }
        else if (!string.IsNullOrWhiteSpace(stageDef.FAutoPluginName))
        {
            _logger.LogWarning(
                "节点[{StageName}](StageDefId={StageDefId}) 仍使用废弃字段 FAutoPluginName={PluginName}，请尽快迁移到 F插件注册ID",
                stageDef.FStageName, stageDef.FID, stageDef.FAutoPluginName);
            pluginCode = stageDef.FAutoPluginName;
        }
#pragma warning restore CS0618

        if (string.IsNullOrWhiteSpace(pluginCode))
        {
            await HandleAutoStageFailureAsync(card, stageInstance, stageDef,
                new InvalidOperationException("auto 节点未配置插件 (F插件注册ID 为空)"));
            return;
        }

        // 卡片级 auto 节点用独立子作用域解析并执行插件（用后随 using 释放）。
        // 工厂为 Singleton、其根容器解析 Scoped 插件会产生 captive DbContext：应用级近单例、
        // 跨并发卡片级 auto 共享（DbContext 非线程安全）、ChangeTracker 永不释放。
        // 改走双参重载 + 子作用域后，插件获得独立于引擎 _dbContext、也独立于根容器的全新 DbContext，
        // 既消除 captive 与并发共享，又保留下方「插件用独立上下文修改 card → 引擎 ReloadAsync 重读」的既定语义。
        // 批次级链（见 ProcessBatchStagesAsync）共享引擎 scoped DbContext 且随批次作用域释放，不在此列。
        using var pluginScope = _scopeFactory.CreateScope();

        IAutoPlugin plugin;
        try
        {
            plugin = _pluginFactory.Create(pluginCode, pluginScope.ServiceProvider);
        }
        catch (Exception ex)
        {
            await HandleAutoStageFailureAsync(card, stageInstance, stageDef, ex);
            return;
        }

        var pluginContext = new PluginContext
        {
            BatchId = card.FBatchId ?? 0,
            CardId = card.FID,
            StageDefinitionId = stageDef.FID,
            PluginRuleId = stageDef.F插件规则ID,
            Services = pluginScope.ServiceProvider,
            CancellationToken = CancellationToken.None
        };

        try
        {
            var result = await plugin.ExecuteAsync(pluginContext);

            if (!result.Success)
            {
                _logger.LogError("Auto插件 {PluginCode} 执行失败, CardId={CardId}, Reason={Reason}",
                    pluginCode, card.FID, result.Message);
                await HandleAutoStageFailureAsync(card, stageInstance, stageDef,
                    new InvalidOperationException(result.Message ?? "插件执行返回失败"));
                return;
            }

            // 插件可能通过独立 DbContext 修改了 card，重载以保证后续逻辑读取最新状态
            await _dbContext.Entry(card).ReloadAsync();

            // 成功：标记节点完成并推进
            stageInstance.FStatus = "completed";
            stageInstance.FFinalAction = "approved";
            stageInstance.FCompletedTime = DateTime.Now;
            _dbContext.Entry(stageInstance).State = EntityState.Modified;

            await LogActionAsync(card.FID, stageInstance.FID, "auto-execute", 0, "system",
                $"plugin={pluginCode}; processed={result.ProcessedRows}; failed={result.FailedRows}");
            await _dbContext.SaveChangesAsync();

            await AdvanceToNextStageAsync(card, stageInstance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto插件 {PluginCode} 执行异常, CardId={CardId}",
                pluginCode, card.FID);
            await HandleAutoStageFailureAsync(card, stageInstance, stageDef, ex);
        }
    }

    private async Task HandleAutoStageFailureAsync(
        CfCard card, CfStageInstance stageInstance, CfStageDefinition stageDef, Exception ex)
    {
        // 读取 FFailurePolicyJson：stuckWithNotify / maxRetry / notifyRoles
        var policy = ParseFailurePolicy(stageDef.FFailurePolicyJson);

        // 以 CfActionLog 中此节点实例的 auto-retry 记录数作为重试计数依据（避免修改表结构）
        var priorRetries = await _dbContext.Set<CfActionLog>()
            .CountAsync(l => l.FStageInstanceId == stageInstance.FID && l.FActionType == "auto-retry");
        var nextAttempt = priorRetries + 1;

        // 插件标识：优先用 F插件注册ID，降级使用旧 FAutoAgentName 仅用于日志展示
#pragma warning disable CS0618
        var pluginTag = stageDef.F插件注册ID.HasValue
            ? $"pluginRegistryId={stageDef.F插件注册ID}"
            : $"autoPlugin={stageDef.FAutoPluginName}";
#pragma warning restore CS0618

        var maxRetry = policy.MaxRetry > 0 ? policy.MaxRetry : 0;
        if (nextAttempt <= maxRetry)
        {
            // 还有重试额度：节点保持 active，仅记录入日志供后续手工/定时任务重试
            await LogActionAsync(card.FID, stageInstance.FID, "auto-retry", 0, "system",
                $"{pluginTag}; attempt={nextAttempt}; err={ex.Message}");
            await _dbContext.SaveChangesAsync();
            return;
        }

        // 超过重试上限：节点 failed + 卡片 exception
        stageInstance.FStatus = "failed";
        stageInstance.FCompletedTime = DateTime.Now;
        _dbContext.Entry(stageInstance).State = EntityState.Modified;

        card.FStatus = "exception";
        card.FUpdatedTime = DateTime.Now;
        _dbContext.Entry(card).State = EntityState.Modified;

        await LogActionAsync(card.FID, stageInstance.FID, "auto-failed", 0, "system",
            $"{pluginTag}; err={ex.Message}");
        await _dbContext.SaveChangesAsync();

        // 批次状态刷新触发点（异常同样需重算批次状态）
        TriggerBatchRefreshIfNeeded(card);

        // TODO(Task#6): policy.NotifyRoles 不为空时推送通知
    }

    private static FailurePolicy ParseFailurePolicy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new FailurePolicy();
        try
        {
            return JsonSerializer.Deserialize<FailurePolicy>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new FailurePolicy();
        }
        catch
        {
            return new FailurePolicy();
        }
    }

    private async Task<BudgetPreviewRequest?> BuildBudgetPreviewRequestAsync(CfCard card, CfFlowDefinition? flowDef = null)
    {
        var cardData = ParseCardData(card.FDataJson);
        var detailRows = await _dbContext.Set<CfCardDetail>()
            .AsNoTracking()
            .Where(d => d.FCardId == card.FID)
            .OrderBy(d => d.FSortOrder)
            .ToListAsync();

        var detailAmount = 0m;
        string? detailExpenseType = null;
        string? detailAccountCode = null;
        long? detailPLItemId = null;

        foreach (var row in detailRows)
        {
            var detailData = ParseCardData(row.FDataJson);
            detailAmount += ReadDecimal(detailData, "amount", "expenseAmount", "totalAmount", "金额", "报销金额", "申请金额");
            detailExpenseType ??= ReadString(detailData, "expenseType", "feeType", "费用类型");
            detailAccountCode ??= ReadString(detailData, "accountCode", "科目编码");
            detailPLItemId ??= ReadNullableLong(detailData, "plItemId", "PLItemId", "plItemID", "损益项ID");
        }

        var amount = detailAmount > 0
            ? detailAmount
            : ReadDecimal(cardData, "amount", "expenseAmount", "totalAmount", "金额", "报销金额", "申请金额");
        if (amount <= 0)
        {
            return null;
        }

        var accountSetId = ReadNullableLong(cardData, "accountSetId", "accountSetID", "账套ID")
            ?? flowDef?.FAccountSetId
            ?? 0;
        var businessDate = ReadDate(cardData, "businessDate", "expenseDate", "applyDate", "date", "业务日期", "申请日期");

        return new BudgetPreviewRequest
        {
            AccountSetId = accountSetId,
            OrgId = card.FOrgId,
            Period = (businessDate ?? DateTime.Today).ToString("yyyyMM"),
            SourceType = "cardflow_card",
            SourceId = card.FID,
            ExpenseType = ReadString(cardData, "expenseType", "feeType", "费用类型") ?? detailExpenseType,
            AccountCode = ReadString(cardData, "accountCode", "科目编码") ?? detailAccountCode,
            PLItemId = ReadNullableLong(cardData, "plItemId", "PLItemId", "plItemID", "损益项ID") ?? detailPLItemId,
            Amount = amount
        };
    }

    private async Task<BudgetPreviewResult?> PreviewBudgetBeforeSubmitAsync(CfCard card, CfFlowDefinition? flowDef)
    {
        var budgetRequest = await BuildBudgetPreviewRequestAsync(card, flowDef);
        if (budgetRequest == null)
        {
            return null;
        }

        return await _budgetOccupationService.PreviewAsync(budgetRequest);
    }

    private async Task OccupyBudgetOnSubmitAsync(CfCard card, CfFlowDefinition? flowDef, string transitionKey)
    {
        var budgetRequest = await BuildBudgetPreviewRequestAsync(card, flowDef);
        if (budgetRequest == null)
        {
            return;
        }

        await _budgetOccupationService.OccupyAsync(budgetRequest, transitionKey);
    }

    private async Task LockBudgetOnCompletionAsync(CfCard card, string transitionKey)
    {
        await _budgetOccupationService.LockAsync("cardflow_card", card.FID, transitionKey);
    }

    private async Task ReleaseBudgetAsync(CfCard card, string action)
    {
        await _budgetOccupationService.ReleaseAsync("cardflow_card", card.FID, $"card:{card.FID}:release:{action}");
    }

    private async Task ConsumeBudgetIfConfirmedAsync(CfCard card)
    {
        var cardData = ParseCardData(card.FDataJson);
        var voucherId = ReadNullableLong(cardData, "voucherId", "generatedVoucherId", "paymentId", "付款ID", "凭证ID");
        var paymentAmount = ReadDecimal(cardData, "paidAmount", "paymentAmount", "voucherAmount", "付款金额", "凭证金额");
        if (!voucherId.HasValue && paymentAmount <= 0)
        {
            return;
        }

        var amount = paymentAmount > 0
            ? paymentAmount
            : ReadDecimal(cardData, "amount", "expenseAmount", "totalAmount", "金额", "报销金额", "申请金额");
        if (amount <= 0)
        {
            return;
        }

        var consumeKey = voucherId.HasValue ? voucherId.Value.ToString() : "confirmed";
        await _budgetOccupationService.ConsumeAsync("cardflow_card", card.FID, amount, $"card:{card.FID}:consume:{consumeKey}");
    }

    private async Task<StageConfigEnvelope> LoadStageConfigAsync(long? stageDefinitionId)
    {
        if (!stageDefinitionId.HasValue)
        {
            return new StageConfigEnvelope();
        }

        var stageDef = await _dbContext.Set<CfStageDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.FID == stageDefinitionId.Value);
        return _stageConfigParser.Parse(stageDef?.FInputFieldsJson);
    }

    private StageActionPolicyValidationResult ValidateStageAction(
        StageConfigEnvelope normalizedConfig,
        string action)
    {
        return _stageActionPolicy.ValidateAction(normalizedConfig, action);
    }

    private static bool IsReturnToStageMode(string? returnMode)
    {
        return string.Equals(returnMode, "toPrevious", StringComparison.OrdinalIgnoreCase)
            || string.Equals(returnMode, "toSpecified", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按 stage 的 FInputFieldsJson 白名单将审批补充数据合并回 card.FDataJson。
    /// FInputFieldsJson 缺失时不做合并，避免上例干扰卡片主体数据。
    /// </summary>
    private void MergeSupplementIntoCardData(
        CfCard card,
        StageConfigEnvelope normalizedConfig,
        IReadOnlyDictionary<string, object> supplement)
    {
        var dict = ParseCardData(card.FDataJson);
        var allowSet = _stageFieldAccess.GetWritableFieldKeys(normalizedConfig);
        var changed = false;
        foreach (var kv in supplement)
        {
            if (allowSet.Contains(kv.Key))
            {
                dict[kv.Key] = kv.Value;
                changed = true;
            }
        }
        if (!changed) return;

        card.FDataJson = JsonSerializer.Serialize(dict);
        card.FUpdatedTime = DateTime.Now;
        _dbContext.Entry(card).State = EntityState.Modified;
    }

    private async Task<string?> ApplyDetailEditsAsync(
        long cardId,
        IReadOnlyCollection<DetailRowEditRequest> detailEdits)
    {
        foreach (var edit in detailEdits)
        {
            var detailTableKey = string.IsNullOrWhiteSpace(edit.DetailTableKey) ? "default" : edit.DetailTableKey;
            var query = _dbContext.Set<CfCardDetail>()
                .Where(d => d.FCardId == cardId && d.FDetailTableKey == detailTableKey);
            query = edit.DetailId.HasValue
                ? query.Where(d => d.FID == edit.DetailId.Value)
                : query.Where(d => d.FSortOrder == edit.RowIndex);

            var detail = await query.FirstOrDefaultAsync();
            if (detail == null)
            {
                return $"字段权限校验失败: [{detailTableKey}] 明细行不存在或不属于当前卡片";
            }

            var data = ParseCardData(detail.FDataJson);
            foreach (var (key, value) in edit.Values)
            {
                data[key] = value;
            }

            detail.FDataJson = JsonSerializer.Serialize(data);
            _dbContext.Entry(detail).State = EntityState.Modified;
        }

        return null;
    }

    private async Task AssignStageHandlersAsync(CfStageInstance stageInstance, CfStageDefinition stageDef, CfCard card)
    {
        var flowSettingsJson = await _dbContext.Set<CfFlowVersion>()
            .Where(version => version.FID == card.FFlowVersionId)
            .Select(version => version.FFlowSettingsJson)
            .FirstOrDefaultAsync();
        var resolveResult = await _approverResolver.ResolveAsync(
            stageDef,
            card,
            ParseCardData(card.FDataJson),
            card.FOrgId,
            card.FInitiatorId,
            flowSettingsJson);
        if (!resolveResult.Success)
        {
            throw new InvalidOperationException(resolveResult.ErrorMessage ?? "未解析到有效处理人");
        }

        var assignees = resolveResult.Approvers
            .OrderBy(approver => approver.SortOrder)
            .Select((approver, index) => new ResolvedApprover
            {
                UserId = approver.UserId,
                UserName = string.IsNullOrWhiteSpace(approver.UserName) ? approver.UserId.ToString() : approver.UserName,
                Source = approver.Source,
                SortOrder = index + 1
            })
            .ToList();
        var assignments = _sequentialRuntime.BuildInitialAssignments(stageDef.FApprovalMode, assignees);

        foreach (var assignment in assignments)
        {
            var assignee = new CfStageAssignee
            {
                FStageInstanceId = stageInstance.FID,
                FUserId = assignment.UserId,
                FUserName = assignment.UserName,
                FSortOrder = assignment.SortOrder,
                FAssignedTime = DateTime.Now,
                FStatus = assignment.Status
            };
            _dbContext.Set<CfStageAssignee>().Add(assignee);
        }
        await _dbContext.SaveChangesAsync();

        // 创建待办
        foreach (var assignment in assignments.Where(a => a.Status == "pending"))
        {
            await _todoService.CreateTodoAsync(card.FID, stageInstance.FID,
                assignment.UserId, assignment.UserName, card.FTitle ?? "待办");
        }
    }

    private List<(long UserId, string UserName)> ResolveAssignees(CfStageDefinition stageDef, CfCard card)
    {
        var result = new List<(long, string)>();
        var strategy = stageDef.FAssigneeStrategy?.ToLowerInvariant() ?? "initiator";

        switch (strategy)
        {
            case "initiator":
                result.Add((card.FInitiatorId, card.FInitiatorName));
                break;
            case "fixed":
                // 从配置中解析固定处理人
                if (!string.IsNullOrWhiteSpace(stageDef.FAssigneeConfigJson))
                {
                    try
                    {
                        var config = JsonSerializer.Deserialize<AssigneeConfig>(stageDef.FAssigneeConfigJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (config?.Users != null)
                        {
                            result.AddRange(config.Users.Select(u => (u.UserId, u.UserName)));
                        }
                    }
                    catch { /* fallback to initiator */ }
                }
                if (result.Count == 0)
                    result.Add((card.FInitiatorId, card.FInitiatorName));
                break;
            case "role":
                // 角色策略：简化为配置中指定的用户
                if (!string.IsNullOrWhiteSpace(stageDef.FAssigneeConfigJson))
                {
                    try
                    {
                        var config = JsonSerializer.Deserialize<AssigneeConfig>(stageDef.FAssigneeConfigJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (config?.Users != null)
                            result.AddRange(config.Users.Select(u => (u.UserId, u.UserName)));
                    }
                    catch { /* fallback */ }
                }
                if (result.Count == 0)
                    result.Add((card.FInitiatorId, card.FInitiatorName));
                break;
            default:
                result.Add((card.FInitiatorId, card.FInitiatorName));
                break;
        }

        return result;
    }

    private async Task CompleteStageTodosAsync(long stageInstanceId)
    {
        var todos = await _dbContext.Set<CfTodoItem>()
            .Where(t => t.FStageInstanceId == stageInstanceId && t.FStatus == "pending")
            .ToListAsync();

        foreach (var todo in todos)
        {
            todo.FStatus = "completed";
            todo.FCompletedTime = DateTime.Now;
            await _notificationDispatcher.DispatchCompleteTodoAsync(todo.FID);
        }
    }

    private async Task CancelStageTodosAsync(long stageInstanceId)
    {
        var todos = await _dbContext.Set<CfTodoItem>()
            .Where(t => t.FStageInstanceId == stageInstanceId && t.FStatus == "pending")
            .ToListAsync();

        foreach (var todo in todos)
        {
            todo.FStatus = "cancelled";
            todo.FCompletedTime = DateTime.Now;
        }
    }

    private async Task CompletePendingTodoAsync(long stageInstanceId, long handlerId, DateTime completedTime)
    {
        var todo = await _dbContext.Set<CfTodoItem>()
            .FirstOrDefaultAsync(t => t.FStageInstanceId == stageInstanceId
                && t.FHandlerId == handlerId
                && t.FStatus == "pending");
        if (todo == null) return;

        todo.FStatus = "completed";
        todo.FCompletedTime = completedTime;
        await _notificationDispatcher.DispatchCompleteTodoAsync(todo.FID);
    }

    private async Task CancelPendingTodoAsync(long stageInstanceId, long handlerId, DateTime completedTime)
    {
        var todo = await _dbContext.Set<CfTodoItem>()
            .FirstOrDefaultAsync(t => t.FStageInstanceId == stageInstanceId
                && t.FHandlerId == handlerId
                && t.FStatus == "pending");
        if (todo == null) return;

        todo.FStatus = "cancelled";
        todo.FCompletedTime = completedTime;
        await _notificationDispatcher.DispatchDeleteTodoAsync(todo.FID);
    }

    private async Task<string> ResolveUserNameAsync(long userId)
    {
        var userName = await _dbContext.Set<SysUser>()
            .Where(u => u.FID == userId)
            .Select(u => u.FName)
            .FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(userName) ? userId.ToString() : userName;
    }

    private async Task LogActionAsync(long cardId, long? stageInstanceId, string actionType,
        long operatorId, string operatorName, string? opinion, string? detailJson = null)
    {
        var log = new CfActionLog
        {
            FCardId = cardId,
            FStageInstanceId = stageInstanceId,
            FActionType = actionType,
            FOperatorId = operatorId,
            FOperatorName = operatorName,
            FOperationTime = DateTime.Now,
            FOpinion = opinion,
            FDetailJson = detailJson
        };
        _dbContext.Set<CfActionLog>().Add(log);
    }

    private sealed class DynamicInsertContext
    {
        public string InsertMode { get; init; } = "after";
        public string? TriggerTiming { get; init; }
        public string? ContinuationStageKey { get; init; }
        public long? SourceStageInstanceId { get; init; }
        public long? RequesterAssigneeId { get; init; }
        public bool SourceWasComplete { get; init; }
        public IReadOnlyList<long> SuspendedAssigneeIds { get; init; } = Array.Empty<long>();
    }

    private static string? ReadString(Dictionary<string, object?> data, params string[] keys)
    {
        var value = ReadValue(data, keys);
        return value switch
        {
            null => null,
            JsonElement { ValueKind: JsonValueKind.String } element => NormalizeString(element.GetString()),
            JsonElement { ValueKind: JsonValueKind.Number } element => element.ToString(),
            _ => NormalizeString(value.ToString())
        };
    }

    private static decimal ReadDecimal(Dictionary<string, object?> data, params string[] keys)
    {
        var value = ReadValue(data, keys);
        return value switch
        {
            null => 0m,
            decimal d => d,
            int i => i,
            long l => l,
            double d => Convert.ToDecimal(d),
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetDecimal(out var d) => d,
            JsonElement { ValueKind: JsonValueKind.String } element when decimal.TryParse(element.GetString(), out var d) => d,
            _ => decimal.TryParse(value.ToString(), out var d) ? d : 0m
        };
    }

    private static long? ReadNullableLong(Dictionary<string, object?> data, params string[] keys)
    {
        var value = ReadValue(data, keys);
        return value switch
        {
            null => null,
            long l => l,
            int i => i,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var l) => l,
            JsonElement { ValueKind: JsonValueKind.String } element when long.TryParse(element.GetString(), out var l) => l,
            _ => long.TryParse(value.ToString(), out var l) ? l : null
        };
    }

    private static DateTime? ReadDate(Dictionary<string, object?> data, params string[] keys)
    {
        var value = ReadValue(data, keys);
        return value switch
        {
            null => null,
            DateTime dt => dt,
            JsonElement { ValueKind: JsonValueKind.String } element when DateTime.TryParse(element.GetString(), out var dt) => dt,
            _ => DateTime.TryParse(value.ToString(), out var dt) ? dt : null
        };
    }

    private static object? ReadValue(Dictionary<string, object?> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (data.TryGetValue(key, out var value))
            {
                return value;
            }

            var pair = data.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(pair.Key))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string? NormalizeString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Dictionary<string, object?> ParseCardData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(dataJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private async Task<List<SchemaFieldDefinition>> GetSchemaFields(long flowVersionId)
    {
        var version = await _dbContext.Set<CfFlowVersion>().FirstOrDefaultAsync(v => v.FID == flowVersionId);
        if (version == null || string.IsNullOrWhiteSpace(version.FCardSchemaJson))
            return new List<SchemaFieldDefinition>();

        try
        {
            return JsonSerializer.Deserialize<List<SchemaFieldDefinition>>(version.FCardSchemaJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            return new List<SchemaFieldDefinition>();
        }
    }

    #endregion

    #region 内部DTO

    private class AssigneeConfig
    {
        public List<AssigneeUserInfo>? Users { get; set; }
        public string? RoleCode { get; set; }
    }

    private class AssigneeUserInfo
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    private class FailurePolicy
    {
        public bool StuckWithNotify { get; set; }
        public int MaxRetry { get; set; }
        public List<string>? NotifyRoles { get; set; }
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s!.Length > max ? s[..max] : s);

    #endregion
}
