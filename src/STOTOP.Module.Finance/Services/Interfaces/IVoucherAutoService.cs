using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IVoucherAutoService
{
    // 凭证规则 CRUD
    Task<PagedResult<VoucherRuleDto>> GetRulesAsync(VoucherRuleQueryRequest request);
    Task<List<VoucherRuleDto>> GetRulesByPriorityAsync();
    Task<VoucherRuleDto?> GetRuleByIdAsync(long id);
    Task<VoucherRuleDto> CreateRuleAsync(CreateVoucherRuleRequest request, string? operatorName);
    Task<VoucherRuleDto?> UpdateRuleAsync(long id, UpdateVoucherRuleRequest request, string? operatorName);
    Task<bool> DeleteRuleAsync(long id);

    // 自动生成凭证草稿
    Task<VoucherGenerateResult> GenerateVoucherDraftAsync(string? operatorName);

    // 统计接口
    Task<FundStatisticsDto> GetStatisticsAsync();
}
