using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 附加费服务接口
/// </summary>
public interface IPriceSurchargeService
{
    /// <summary>分页查询附加费列表</summary>
    Task<PagedResult<PriceSurchargeListItemDto>> GetListAsync(PriceSurchargeQueryRequest request);
    /// <summary>获取附加费详情（含配置项和目的地）</summary>
    Task<PriceSurchargeDto?> GetByIdAsync(long id);
    /// <summary>创建附加费</summary>
    Task<PriceSurchargeDto> CreateAsync(CreatePriceSurchargeRequest request);
    /// <summary>更新附加费</summary>
    Task<PriceSurchargeDto?> UpdateAsync(long id, UpdatePriceSurchargeRequest request);
    /// <summary>删除附加费</summary>
    Task<bool> DeleteAsync(long id);
    /// <summary>切换启用/停用状态</summary>
    Task<bool> ToggleActiveAsync(long id);
}
