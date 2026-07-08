using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

/// <summary>
/// 流程定义编辑锁服务（M7-1 / 设计 E7）。
/// 单写者并发锁 + 接管协议：心跳 30s、超时 120s、接管等待 60s。
/// </summary>
public interface IDefinitionEditLockService
{
    /// <summary>尝试获取编辑锁（进入编辑页时调用）。无锁/死锁 → 抢占成为 holder；他人活锁 → 返回 holder 信息。</summary>
    Task<LockStateDto> AcquireAsync(long definitionId, long userId, string userName);

    /// <summary>持锁端心跳续期（30s 一次）。顺带返回是否有待响应接管请求。非 holder 调用返回 held=false。</summary>
    Task<LockStateDto> HeartbeatAsync(long definitionId, long userId);

    /// <summary>只读端申请接管。全局唯一：已有未过期请求且非本人 → 拒绝。成功登记后经 SignalR 推送通知 holder。</summary>
    Task<LockStateDto> RequestTakeoverAsync(long definitionId, long requesterId, string requesterName);

    /// <summary>持锁端响应接管请求。accept=true → 原子移交；accept=false → 清请求。</summary>
    Task<LockStateDto> RespondTakeoverAsync(long definitionId, long holderId, bool accept);
}
