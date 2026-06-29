using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 报价方案服务接口
/// </summary>
public interface IQuotationService
{
    /// <summary>分页查询报价方案列表</summary>
    Task<PagedResult<QuotationListItemDto>> GetListAsync(QuotationQueryRequest request);
    /// <summary>获取方案详情（含重量段和价格矩阵）</summary>
    Task<QuotationDto?> GetByIdAsync(long id);
    /// <summary>创建报价方案（含重量段和价格矩阵）</summary>
    Task<QuotationDto> CreateAsync(CreateQuotationRequest request);
    /// <summary>更新报价方案（全量替换重量段和价格矩阵）</summary>
    Task<QuotationDto?> UpdateAsync(long id, UpdateQuotationRequest request);
    /// <summary>删除报价方案</summary>
    Task<bool> DeleteAsync(long id);
    /// <summary>复制报价方案</summary>
    Task<QuotationDto> CopyPlanAsync(long sourcePlanId);
    /// <summary>业务对象统一查询聚合（含报价数量统计）</summary>
    Task<PagedResult<ClientQuotationSummaryDto>> GetClientQuotationSummaryAsync(ClientQuotationSummaryQuery query);
    /// <summary>按店铺名称查询关联的报价方案（按业务对象分组）</summary>
    Task<List<QuotationByShopGroupDto>> GetQuotationsByShopAsync(string shopName);
}
