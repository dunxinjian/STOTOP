using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// 流程定义编辑锁（CF定义编辑锁，M7-1 / 设计 E7）：单写者并发编辑锁。
/// 单定义至多一行（F定义ID 唯一键）；持锁端每 30s 心跳续期，超时 120s 未续视为死锁可被抢占。
/// 接管请求内联本行（全局唯一，无独立请求表——避免二次竞态）：他人申请接管时登记申请人段，
/// 持锁端同意/60s 超时则原子移交（改 holder + 清请求段），拒绝则清请求段。
/// </summary>
public class CfDefinitionEditLock : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FTenantId { get; set; }
    public long FOrgId { get; set; }
    /// <summary>锁定的流程定义ID（唯一键）</summary>
    public long FFlowDefinitionId { get; set; }
    /// <summary>当前持锁用户ID</summary>
    public long FHolderId { get; set; }
    public string FHolderName { get; set; } = string.Empty;
    public DateTime FAcquiredTime { get; set; }
    /// <summary>心跳续期戳；now - 此值 > 120s 视为死锁，可被 acquire/takeover 抢占</summary>
    public DateTime FHeartbeatAt { get; set; }
    /// <summary>接管申请人ID（null=无接管请求）</summary>
    public long? FTakeoverRequesterId { get; set; }
    public string? FTakeoverRequesterName { get; set; }
    /// <summary>接管申请时间；now - 此值 > 60s 未响应 = 超时移交</summary>
    public DateTime? FTakeoverRequestedAt { get; set; }
}
