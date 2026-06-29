using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IFinanceService
{
    Task<PagedResult<IncomeListItemDto>> GetIncomesAsync(int eventId, IncomeQueryRequest request);
    Task<IncomeDto?> GetIncomeByIdAsync(int id);
    Task<IncomeDto> CreateIncomeAsync(int eventId, CreateIncomeRequest request);
    Task<IncomeDto?> UpdateIncomeAsync(int id, UpdateIncomeRequest request);
    Task<bool> DeleteIncomeAsync(int id);
    Task<IncomeSummaryDto> GetSummaryAsync(int eventId);
    Task<byte[]> ExportIncomesAsync(int eventId);
    Task<int> BatchRegisterAsync(int eventId, BatchRegisterIncomeRequest request);
}
