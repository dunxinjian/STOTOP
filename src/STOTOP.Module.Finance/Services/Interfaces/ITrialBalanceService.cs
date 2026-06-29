using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface ITrialBalanceService
{
    Task<TrialBalanceDto> GenerateTrialBalanceAsync(long periodId, long accountSetId);
    Task<TrialBalanceDto?> GetLatestTrialBalanceAsync(long periodId, long accountSetId);
}
