using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface ICeremonyService
{
    Task<List<CeremonyItemDto>> GetItemsAsync(long eventId);
    Task<CeremonyItemDto?> GetItemByIdAsync(long id);
    Task<CeremonyItemDto> CreateItemAsync(long eventId, CreateCeremonyItemRequest request);
    Task<CeremonyItemDto?> UpdateItemAsync(long id, UpdateCeremonyItemRequest request);
    Task<bool> DeleteItemAsync(long id);
    Task<bool> ReorderAsync(long eventId, ReorderCeremonyRequest request);
    Task<string> ExportRundownAsync(long eventId);
}
