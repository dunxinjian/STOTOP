using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface ITreasuryPlanService
{
    Task<List<TreasuryAccountBindingDto>> GetBindingsAsync(long accountSetId, long? orgId);
    Task<TreasuryAccountBindingDto> SaveBindingAsync(TreasuryAccountBindingDto dto);
    Task<List<TreasuryPlanLineDto>> GetPlanLinesAsync(long accountSetId, DateTime startDate, DateTime endDate, long? orgId);
    Task<TreasuryPlanLineDto> SavePlanLineAsync(TreasuryPlanLineDto dto);
    Task DeletePlanLineAsync(long id);
    Task<Rolling13WeekTreasuryDto> GetRolling13WeeksAsync(long accountSetId, DateTime startDate, long? orgId, decimal safetyCash);
}
