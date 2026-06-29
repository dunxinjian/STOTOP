using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface ICardService
{
    Task<List<AvailableFlowDto>> GetAvailableFlowsAsync(long userId, long orgId);
    Task<PagedResult<CardListDto>> GetCardsAsync(CardQueryRequest request);
    Task<PagedResult<CardListDto>> GetInitiatedCardsAsync(long userId, CardQueryRequest request);
    Task<CardDetailDto?> GetByIdAsync(long id, long userId, bool canViewAll = false);
    Task<CardDetailDto> CreateAsync(CreateCardRequest request, long userId);
    Task<CardDetailDto> UpdateAsync(long id, UpdateCardRequest request, long userId);
    Task DeleteAsync(long id, long userId);
    Task<List<CardListDto>> GetAvailablePrerequisitesAsync(long cardId, long userId);
    Task<List<CardBalanceDto>> GetAvailableOffsetsAsync(long cardId, long userId);
    Task<List<CardBalanceDto>> GetBalanceAsync(long cardId);
    Task<CardRelationDto> CreateRelationAsync(long cardId, CreateRelationRequest request, long userId);
    Task<List<CardRelationDto>> GetRelationsAsync(long cardId);
    Task<List<ActionLogDto>> GetLogsAsync(long cardId);
    Task<PagedResult<AuditLogItemDto>> SearchLogsAsync(AuditLogQueryRequest request);
    Task<CardFlowRuntimeMonitoringDto> GetRuntimeMonitoringAsync(CardFlowRuntimeMonitoringRequest request);
    Task RetryPushAsync(long cardId, long operatorId);
    Task<string?> GetRelationSnapshotAsync(long cardId, long relationId);
}
