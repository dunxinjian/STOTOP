using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IGiftService
{
    Task<List<GiftDto>> GetGiftsAsync(long eventId);
    Task<GiftDto?> GetGiftByIdAsync(long id);
    Task<GiftDto> CreateGiftAsync(long eventId, CreateGiftRequest request);
    Task<GiftDto?> UpdateGiftAsync(long id, UpdateGiftRequest request);
    Task<bool> DeleteGiftAsync(long id);
    Task<GiftSummaryDto> GetSummaryAsync(long eventId);
    Task<int> BatchRegisterAsync(long eventId, BatchRegisterGiftRequest request);
    Task<byte[]> ExportGiftsAsync(long eventId);
}
