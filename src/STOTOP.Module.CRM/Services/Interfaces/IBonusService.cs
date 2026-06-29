using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface IBonusService
{
    // Bonus Plans
    Task<PagedResult<BonusPlanDto>> GetBonusPlansAsync(BonusPlanQueryRequest request);
    Task<BonusPlanDto?> GetBonusPlanByIdAsync(long id);
    Task<BonusPlanDto> CreateBonusPlanAsync(CreateBonusPlanRequest request);
    Task<BonusPlanDto?> UpdateBonusPlanAsync(long id, UpdateBonusPlanRequest request);
    Task<bool> DeleteBonusPlanAsync(long id);
    Task<bool> UpdatePlanStatusAsync(long id, int status);

    // Bonus Details
    Task<PagedResult<BonusDetailDto>> GetBonusDetailsAsync(BonusDetailQueryRequest request);
    Task<BonusDetailDto> AddBonusDetailAsync(long planId, CreateBonusDetailRequest request);
    Task<bool> DeleteBonusDetailAsync(long detailId);
}
