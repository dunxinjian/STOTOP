using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IFlowEngineService
{
    Task<CardOperationResult> SubmitAsync(long cardId, long operatorId);
    /// <summary>
    /// 处理批次级节点链：按 FSortOrder 顺序执行所有 batchAuto 节点，
    /// 直到遇到第一个非 batchAuto 节点为止。
    /// </summary>
    Task ProcessBatchStagesAsync(CfBatch batch, CancellationToken ct = default);
    Task<CardOperationResult> ApproveAsync(long cardId, long operatorId, ApproveRequest request);
    Task<CardOperationResult> RejectAsync(long cardId, long operatorId, RejectRequest request);
    Task<CardOperationResult> WithdrawAsync(long cardId, long operatorId);
    Task<CardOperationResult> ResubmitAsync(long cardId, long operatorId);
    Task<CardOperationResult> VoidAsync(long cardId, long operatorId, string? opinion = null);
    Task<CardOperationResult> CountersignAsync(long cardId, long operatorId, CountersignRequest request);
    Task<CardOperationResult> TransferAsync(long cardId, long operatorId, TransferRequest request);
    Task<CardOperationResult> CcAsync(long cardId, long operatorId, CcRequest request);
    Task<CardOperationResult> UrgeAsync(long cardId, long operatorId, string? message = null);
    Task<CardOperationResult> ResumeAsync(long cardId, long operatorId);

    /// <summary>超时升级链-系统自动通过（M8-C，CardFlowTimeoutJob 调用）。timeoutLevel=命中的倍数级别，用于幂等高水位标记。</summary>
    Task<CardOperationResult> SystemAutoApproveStageAsync(long stageInstanceId, int timeoutLevel, string reason);
    /// <summary>超时升级链-系统自动拒绝（M8-C，CardFlowTimeoutJob 调用）。</summary>
    Task<CardOperationResult> SystemAutoRejectStageAsync(long stageInstanceId, int timeoutLevel, string reason);
    /// <summary>超时升级链-升级至上级（M8-C，CardFlowTimeoutJob 调用）：无法解析上级时降级为提醒。</summary>
    Task<CardOperationResult> EscalateStageAsync(long stageInstanceId, int timeoutLevel, string reason);

    /// <summary>执行节点自定义动作（M8-C）：actionCode 命中当前节点 ActionPolicy.CustomActions 后按 Handler 分派
    /// （autoApprove=自动通过/autoReject=自动驳回/notify=触发通知抄送）。</summary>
    Task<CardOperationResult> ExecuteCustomActionAsync(long cardId, long operatorId, string actionCode, string? opinion);
}
