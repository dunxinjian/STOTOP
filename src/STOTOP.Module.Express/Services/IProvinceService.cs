using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IProvinceService
{
    Task<List<ProvinceListItemDto>> GetAllAsync();
    Task<ProvinceDto?> GetByIdAsync(int id);
    Task<ProvinceDto> CreateAsync(CreateProvinceRequest request);
    Task<ProvinceDto?> UpdateAsync(int id, UpdateProvinceRequest request);
    Task<bool> DeleteAsync(int id);
    /// <summary>获取所有大区名称列表</summary>
    Task<List<string>> GetRegionsAsync();
    /// <summary>重命名大区（批量更新该大区下所有省份）</summary>
    Task<int> RenameRegionAsync(string oldName, string newName);
}
