using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IDelegationService
{
    Task<List<DelegationDto>> GetMyDelegationsAsync(long userId);
    Task<DelegationDto> CreateAsync(CreateDelegationRequest request, long userId);
    Task<DelegationDto> UpdateAsync(long id, UpdateDelegationRequest request, long userId);
    Task CancelAsync(long id, long userId);
}
