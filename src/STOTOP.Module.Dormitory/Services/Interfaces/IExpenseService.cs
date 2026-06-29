using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IExpenseService
{
    Task<PagedResult<ExpenseListItemDto>> GetExpensesAsync(ExpenseQueryRequest request);
    Task<ExpenseDto?> GetExpenseByIdAsync(long id);
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request);
    Task<ExpenseDto?> UpdateExpenseAsync(long id, UpdateExpenseRequest request);
    Task<bool> DeleteExpenseAsync(long id);
    Task<List<MonthlyExpenseSummaryDto>> GetMonthlySummaryAsync(string? month);
    /// <summary>计算某条费用在房间当前在住人之间的分摊明细。费用不存在返回 null。</summary>
    Task<ExpenseAllocationDto?> GetExpenseAllocationAsync(long expenseId);
}
