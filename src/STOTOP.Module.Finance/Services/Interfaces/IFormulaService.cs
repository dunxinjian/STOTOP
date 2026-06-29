using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

/// <summary>
/// 公式管理服务接口
/// </summary>
public interface IFormulaService
{
    /// <summary>
    /// 按报表类型获取公式列表
    /// </summary>
    Task<List<FormulaDto>> GetByReportTypeAsync(string reportType, long accountSetId);

    /// <summary>
    /// 创建公式
    /// </summary>
    Task<FormulaDto> CreateAsync(CreateFormulaRequest request);

    /// <summary>
    /// 更新公式
    /// </summary>
    Task<FormulaDto?> UpdateAsync(long id, UpdateFormulaRequest request);

    /// <summary>
    /// 删除公式
    /// </summary>
    Task<bool> DeleteAsync(long id);

    /// <summary>
    /// 初始化默认公式（将硬编码规则写入数据库）
    /// </summary>
    Task<int> InitDefaultFormulasAsync(string reportType, long accountSetId);

    /// <summary>
    /// 测试公式
    /// </summary>
    Task<FormulaTestResponse> TestFormulaAsync(FormulaTestRequest request);
}
