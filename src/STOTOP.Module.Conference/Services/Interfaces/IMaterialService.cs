using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IMaterialService
{
    Task<PagedResult<MaterialListItemDto>> GetMaterialsAsync(int eventId, MaterialQueryRequest request);
    Task<MaterialDto?> GetMaterialByIdAsync(int id);
    Task<MaterialDto> CreateMaterialAsync(int eventId, CreateMaterialRequest request);
    Task<MaterialDto?> UpdateMaterialAsync(int id, UpdateMaterialRequest request);
    Task<bool> DeleteMaterialAsync(int id);
    Task<MaterialDto?> ReceiveMaterialAsync(int id, MaterialReceiveRequest request);
    Task<MaterialDto?> ReturnMaterialAsync(int id, MaterialReturnRequest request);
    Task<MaterialSummaryDto> GetSummaryAsync(int eventId);
    Task<byte[]> ExportMaterialsAsync(int eventId);
    Task<List<MaterialListItemDto>> GetChecklistAsync(int eventId);
}
