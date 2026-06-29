using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ICardRelationService
{
    Task ValidatePrerequisitesAsync(long cardId);
    Task ExecuteOffsetAsync(long cardId);
    Task RollbackOffsetAsync(long cardId);
    Task<List<CardBalanceDto>> GetAvailableBalancesAsync(long userId, long orgId);
}
