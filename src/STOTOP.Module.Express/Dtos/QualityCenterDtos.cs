using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 数据质量中心概览
/// </summary>
public class QualityCenterOverviewDto
{
    /// <summary>待配置店铺总数（FNeedsAssignment=true 或 未建立任何归属）</summary>
    public int PendingShopCount { get; set; }

    /// <summary>自动建档但未配置归属的店铺数</summary>
    public int AutoCreatedPendingCount { get; set; }

    /// <summary>空店铺账号运单总数（按 DcImportError ERR_SHOP_EMPTY 统计，未忽略）</summary>
    public int EmptyShopRowCount { get; set; }

    /// <summary>受影响的批次数量</summary>
    public int AffectedBatchCount { get; set; }

    /// <summary>未识别网点运单数</summary>
    public int UnrecognizedNetworkPointCount { get; set; }

    /// <summary>网点不一致警告数</summary>
    public int NetworkPointMismatchCount { get; set; }

    /// <summary>因质量问题阻塞计费的批次 ID 列表</summary>
    public List<long> BlockedBatchIds { get; set; } = new();
}

/// <summary>
/// 待配置店铺查询请求
/// </summary>
public class PendingShopQueryRequest : PagedRequest
{
    /// <summary>是否仅查自动建档的（默认 false 表示全部待配置）</summary>
    public bool? IsAutoCreated { get; set; }

    /// <summary>关联批次 ID（只看此批次自动创建或关联的店铺）</summary>
    public long? BatchId { get; set; }
}

/// <summary>
/// 待配置店铺列表项
/// </summary>
public class PendingShopItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public bool IsAutoCreated { get; set; }
    public bool NeedsAssignment { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
    /// <summary>备注（通常包含自动建档的批次信息）</summary>
    public string? Remark { get; set; }

    /// <summary>该店铺在所有批次中受影响的运单数</summary>
    public int AffectedWaybillCount { get; set; }

    /// <summary>受影响的批次 ID 列表（最多返回前 5 个）</summary>
    public List<long> AffectedBatchIds { get; set; } = new();
}

/// <summary>
/// 一键完成店铺配置请求
/// </summary>
public class CompleteShopConfigRequest
{
    public string ShopName { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    /// <summary>报价方案ID（必填）</summary>
    public long PricePlanId { get; set; }
    /// <summary>归属生效日期（默认今天）</summary>
    public DateTime? EffectiveDate { get; set; }
    /// <summary>归属失效日期（可选）</summary>
    public DateTime? ExpiryDate { get; set; }
    /// <summary>归属备注</summary>
    public string? AssignmentRemark { get; set; }
    /// <summary>是否跳过报价方案校验（若为 false，当业务对象无生效报价时返回 Warning）</summary>
    public bool SkipPricePlanCheck { get; set; }
}

/// <summary>
/// 一键完成店铺配置结果
/// </summary>
public class CompleteShopConfigResultDto
{
    public string ShopName { get; set; } = string.Empty;
    public long AssignmentId { get; set; }
    /// <summary>是否已关闭 NeedsAssignment</summary>
    public bool Completed { get; set; }
    /// <summary>报价方案缺失提示（若无生效报价）</summary>
    public string? PricePlanWarning { get; set; }
}

/// <summary>
/// 空店铺账号运单查询请求
/// </summary>
public class EmptyShopRowQueryRequest : PagedRequest
{
    /// <summary>关联批次 ID（可选，用于筛选指定批次）</summary>
    public long? BatchId { get; set; }

    /// <summary>派发状态（Pending/Ignored/Resolved）</summary>
    public string? DispatchStatus { get; set; }
}

/// <summary>
/// 空店铺账号运单明细项
/// </summary>
public class EmptyShopRowItemDto
{
    /// <summary>错误明细 ID（DcImportError.FID）</summary>
    public long ErrorId { get; set; }
    public long BatchId { get; set; }
    public long? StagingId { get; set; }
    /// <summary>运单号（从 STG 行取）</summary>
    public string? WaybillNo { get; set; }
    public DateTime? WaybillDate { get; set; }
    /// <summary>错误信息（冗余便于前端显示）</summary>
    public string? ErrorMessage { get; set; }
    public string? DispatchStatus { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 补填空店铺账号请求
/// </summary>
public class FillEmptyShopAccountRequest
{
    /// <summary>错误明细 ID 列表</summary>
    public List<long> ErrorIds { get; set; } = new();
    /// <summary>要补填的店铺账号值</summary>
    public string ShopAccount { get; set; } = string.Empty;
}

/// <summary>
/// 补填/忽略结果
/// </summary>
public class EmptyShopRowBatchResultDto
{
    public int AffectedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 忽略空店铺账号请求
/// </summary>
public class IgnoreEmptyShopRowsRequest
{
    public List<long> ErrorIds { get; set; } = new();
    public string? Reason { get; set; }
}

/// <summary>
/// 重新计费请求
/// </summary>
public class RerunBillingRequest
{
    public long BatchId { get; set; }
}

/// <summary>
/// 重新计费结果
/// </summary>
public class RerunBillingResultDto
{
    public long BatchId { get; set; }
    public bool Enqueued { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ==================== 未识别网点 ====================

/// <summary>
/// 未识别网点查询请求
/// </summary>
public class UnrecognizedNetworkPointQueryRequest : PagedRequest
{
    /// <summary>按批次筛选</summary>
    public long? BatchId { get; set; }
}

/// <summary>
/// 未识别网点列表项
/// </summary>
public class UnrecognizedNetworkPointItemDto
{
    /// <summary>错误记录ID (DcImportError.FID)</summary>
    public long ErrorId { get; set; }
    /// <summary>批次ID</summary>
    public long BatchId { get; set; }
    /// <summary>运单编号</summary>
    public string? WaybillNo { get; set; }
    /// <summary>原始网点名称 (FOriginalValue)</summary>
    public string NetworkPointName { get; set; } = string.Empty;
    /// <summary>派发状态</summary>
    public string? DispatchStatus { get; set; }
    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 关联网点请求
/// </summary>
public class AssociateNetworkPointRequest
{
    /// <summary>原始网点名称（FOriginalValue）</summary>
    public string NetworkPointName { get; set; } = string.Empty;
    /// <summary>目标网点编号</summary>
    public string NetworkPointCode { get; set; } = string.Empty;
    /// <summary>可选，限定批次范围</summary>
    public long? BatchId { get; set; }
}

/// <summary>
/// 关联网点结果
/// </summary>
public class AssociateNetworkPointResultDto
{
    public int ResolvedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 批量忽略网点错误请求
/// </summary>
public class IgnoreNetworkPointErrorsRequest
{
    /// <summary>错误记录ID列表</summary>
    public List<long> ErrorIds { get; set; } = new();
    /// <summary>忽略原因</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// 批量忽略网点错误结果
/// </summary>
public class IgnoreNetworkPointErrorsResultDto
{
    public int AffectedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ==================== 网点不一致 ====================

/// <summary>
/// 网点不一致查询请求
/// </summary>
public class NetworkPointMismatchQueryDto : PagedRequest
{
    /// <summary>按批次筛选</summary>
    public long? BatchId { get; set; }
    /// <summary>按运单号筛选（模糊匹配）</summary>
    public string? WaybillNo { get; set; }
}

/// <summary>
/// 网点不一致列表项
/// </summary>
public class NetworkPointMismatchItemDto
{
    /// <summary>错误记录ID</summary>
    public long ErrorId { get; set; }
    /// <summary>批次ID</summary>
    public long BatchId { get; set; }
    /// <summary>运单号</summary>
    public string WaybillNo { get; set; } = string.Empty;
    /// <summary>映射网点编号</summary>
    public string MappedNpCode { get; set; } = string.Empty;
    /// <summary>报价网点编号</summary>
    public string QuotationNpCode { get; set; } = string.Empty;
    /// <summary>派发状态</summary>
    public string? DispatchStatus { get; set; }
    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 批量忽略网点不一致请求
/// </summary>
public class IgnoreMismatchErrorsRequest
{
    /// <summary>错误记录ID列表</summary>
    public List<long> ErrorIds { get; set; } = new();
}
