using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IResidenceService
{
    Task<PagedResult<ResidenceListItemDto>> GetResidencesAsync(ResidenceQueryRequest request);
    Task<ResidenceDto?> GetResidenceByIdAsync(long id);
    Task<ResidenceDto> CreateResidenceAsync(CreateResidenceRequest request);
    Task<ResidenceDto?> UpdateResidenceAsync(long id, UpdateResidenceRequest request);
    Task<ResidenceDto?> CheckOutAsync(long id, CheckOutRequest request);
    Task<bool> DeleteResidenceAsync(long id);
    Task<bool> IsBedOccupiedAsync(long bedId);
}
