using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IBudgetExpenseMappingService
{
    Task<List<BudgetExpenseMappingDto>> GetMappingsAsync(long accountSetId, long? orgId);
    Task<BudgetExpenseMappingDto> SaveMappingAsync(BudgetExpenseMappingDto dto);
    Task<BudgetExpenseMappingDto?> ResolveAsync(long accountSetId, long? orgId, string expenseType);
}
