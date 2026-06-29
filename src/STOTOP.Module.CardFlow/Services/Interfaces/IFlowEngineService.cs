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
}
