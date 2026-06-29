using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IVisitorService
{
    Task<PagedResult<VisitorListItemDto>> GetVisitorsAsync(VisitorQueryRequest request);
    Task<VisitorDto?> GetVisitorByIdAsync(long id);
    Task<VisitorDto> CreateVisitorAsync(CreateVisitorRequest request);
    Task<VisitorDto?> UpdateVisitorAsync(long id, UpdateVisitorRequest request);
    Task<VisitorDto?> DepartureAsync(long id, DateTime? departureTime = null);
    Task<bool> DeleteVisitorAsync(long id);
}
