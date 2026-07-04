using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IAccountService
{
    Task<List<AccountTreeDto>> GetTreeAsync(string? category = null, long accountSetId = 0);
    Task<AccountDto?> GetByIdAsync(long id, long accountSetId);
    Task<AccountDto> CreateAsync(CreateAccountRequest request, long accountSetId = 0);
    Task<AccountDto?> UpdateAsync(long id, UpdateAccountRequest request, long accountSetId);
    Task<bool> DeleteAsync(long id, long accountSetId);
    Task<bool> ToggleStatusAsync(long id, long accountSetId);
    Task<List<InitialBalanceDto>> GetInitialBalancesAsync(long accountSetId);
    Task<bool> SaveInitialBalancesAsync(SaveInitialBalancesRequest request);
    Task<List<AccountDto>> GetByAuxTypeAsync(string auxType, long accountSetId);
    Task<bool> UpdateAccountAuxiliaryAsync(UpdateAccountAuxiliaryRequest request);
}
