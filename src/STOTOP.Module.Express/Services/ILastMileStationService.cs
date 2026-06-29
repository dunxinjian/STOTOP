using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface ILastMileStationService
{
    Task<PagedResult<LastMileStationDto>> GetListAsync(LastMileStationQueryRequest request);
    Task<LastMileStationDto?> GetByIdAsync(string code);
    Task<LastMileStationDto> CreateAsync(CreateLastMileStationRequest request);
    Task<LastMileStationDto?> UpdateAsync(string code, UpdateLastMileStationRequest request);
    Task<bool> DeleteAsync(string code);
}
