using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IBudgetService
{
    Task<List<BudgetVersionDto>> GetVersionsAsync(long accountSetId, int? year);
    Task<BudgetVersionDto> CreateVersionAsync(CreateBudgetVersionRequest request, string? operatorName);
    Task SubmitVersionAsync(long id);
    Task ApproveVersionAsync(long id, string? operatorName);
    Task<List<BudgetLineDto>> GetLinesAsync(long budgetVersionId, string? period, long? orgId);
    Task BatchUpsertLinesAsync(long budgetVersionId, BatchUpsertBudgetLinesRequest request);
}
