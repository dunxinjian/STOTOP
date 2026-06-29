using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IAccountPeriodService
{
    Task<List<AccountPeriodDto>> GetAllAsync(long accountSetId = 0);
    Task<List<AccountPeriodDto>> GetByYearAsync(int year, long accountSetId = 0);
    Task<AccountPeriodDto?> GetCurrentAsync(long accountSetId = 0);
    Task<AccountPeriodDto?> GetByIdAsync(long id);
    Task<List<AccountPeriodDto>> CreateYearPeriodsAsync(int year, long accountSetId = 0);
    Task<(bool success, string message)> CloseAsync(long periodId, long accountSetId = 0);
    Task<(bool success, string message)> ReopenAsync(long periodId, long accountSetId = 0);
    Task<object> PreCloseCheckAsync(long accountSetId, int year, int periodNo);
}
