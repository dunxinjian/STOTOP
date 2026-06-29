using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IBudgetOccupationService
{
    Task<BudgetPreviewResult> PreviewAsync(BudgetPreviewRequest request);
    Task OccupyAsync(BudgetPreviewRequest request, string transitionKey);
    Task LockAsync(string sourceType, long sourceId, string transitionKey);
    Task ConsumeAsync(string sourceType, long sourceId, decimal amount, string transitionKey);
    Task ReleaseAsync(string sourceType, long sourceId, string transitionKey);
}
