using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 数据质量中心：待配置店铺 / 空账号运单 / 一键完成 / 重新计费
/// </summary>
public interface IQualityCenterService
{
    /// <summary>获取质量中心总览</summary>
    Task<QualityCenterOverviewDto> GetOverviewAsync();

    /// <summary>分页查询待配置店铺（包含自动建档和人工建档未配置归属的）</summary>
    Task<PagedResult<PendingShopItemDto>> GetPendingShopsAsync(PendingShopQueryRequest request);

    /// <summary>一键完成店铺归属配置（可选跳过报价方案校验）</summary>
    Task<CompleteShopConfigResultDto> CompleteShopConfigAsync(CompleteShopConfigRequest request);

    /// <summary>分页查询空店铺账号运单（基于 DcImportError ERR_SHOP_EMPTY）</summary>
    Task<PagedResult<EmptyShopRowItemDto>> GetEmptyShopRowsAsync(EmptyShopRowQueryRequest request);

    /// <summary>补填空店铺账号到 STG 表对应行，并将错误明细状态置为 Resolved</summary>
    Task<EmptyShopRowBatchResultDto> FillEmptyShopAccountAsync(FillEmptyShopAccountRequest request);

    /// <summary>忽略空店铺账号（标记 FDispatchStatus=Ignored，计费重跑时跳过这些行的阻断）</summary>
    Task<EmptyShopRowBatchResultDto> IgnoreEmptyShopRowsAsync(IgnoreEmptyShopRowsRequest request);

    /// <summary>触发批次重新计费（复用 ImportService.RetryBatchAsync，会重跑整个管道）</summary>
    Task<RerunBillingResultDto> RerunBillingAsync(RerunBillingRequest request);

    // ========== 未识别网点 ==========

    /// <summary>分页查询未识别网点错误记录</summary>
    Task<PagedResult<UnrecognizedNetworkPointItemDto>> GetUnrecognizedNetworkPointsAsync(UnrecognizedNetworkPointQueryRequest request);

    /// <summary>关联网点（名称→编号），自动建映射</summary>
    Task<AssociateNetworkPointResultDto> AssociateNetworkPointAsync(AssociateNetworkPointRequest request);

    /// <summary>批量忽略网点错误</summary>
    Task<IgnoreNetworkPointErrorsResultDto> IgnoreNetworkPointErrorsAsync(IgnoreNetworkPointErrorsRequest request);

    // ========== 网点不一致 ==========

    /// <summary>分页查询网点不一致记录</summary>
    Task<PagedResult<NetworkPointMismatchItemDto>> GetNetworkPointMismatchesAsync(NetworkPointMismatchQueryDto request);

    /// <summary>批量忽略网点不一致错误</summary>
    Task<int> IgnoreMismatchErrorsAsync(List<long> errorIds);
}
