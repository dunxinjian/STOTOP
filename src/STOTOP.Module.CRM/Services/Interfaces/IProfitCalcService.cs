using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface IProfitCalcService
{
    Task<PagedResult<CustomerProfitDto>> GetProfitsAsync(ProfitQueryRequest request);
    Task<CustomerProfitDto?> GetProfitByIdAsync(long id);
    Task<CustomerProfitDto> CreateProfitAsync(CreateProfitRequest request);
    Task<CustomerProfitDto?> UpdateProfitAsync(long id, CreateProfitRequest request);
    Task<bool> DeleteProfitAsync(long id);

    // Summary & Ranking
    Task<List<ProfitSummaryDto>> GetProfitSummaryAsync(long? orgId, string? period);
    Task<List<ProfitRankingDto>> GetProfitRankingAsync(long? orgId, string? period, int top = 20);

    // Reserved for future EXP integration
    Task CalcProfitAsync(string customerId, string period);
}
