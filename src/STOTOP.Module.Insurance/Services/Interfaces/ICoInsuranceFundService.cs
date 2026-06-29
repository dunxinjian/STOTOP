using STOTOP.Core.Models;
using STOTOP.Module.Insurance.Dtos;

namespace STOTOP.Module.Insurance.Services.Interfaces;

public interface ICoInsuranceFundService
{
    Task<PagedResult<InsCoInsuranceFundListItemDto>> GetListAsync(InsCoInsuranceFundQueryRequest request);
    Task<InsCoInsuranceFundDto?> GetByIdAsync(long id);
    Task<InsCoInsuranceFundDto> CreateAsync(CreateInsCoInsuranceFundRequest request);
    Task<InsCoInsuranceFundDto?> UpdateAsync(long id, UpdateInsCoInsuranceFundRequest request);
    Task<int> GenerateContributionsAsync(long fundId, DateOnly periodStart, DateOnly periodEnd);
    Task ConfirmContributionAsync(long contributionId);
    Task<PagedResult<InsFundContributionListItemDto>> GetContributionsAsync(InsFundContributionQueryRequest request);
}
