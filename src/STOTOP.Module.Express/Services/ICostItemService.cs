using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 成本项目服务接口
/// </summary>
public interface ICostItemService
{
    /// <summary>获取全部成本项目（用于下拉选择）</summary>
    Task<List<CostItemDto>> GetAllAsync();
    /// <summary>创建成本项目</summary>
    Task<CostItemDto> CreateAsync(CreateCostItemRequest request);
    /// <summary>更新成本项目</summary>
    Task<CostItemDto?> UpdateAsync(int id, UpdateCostItemRequest request);
}
