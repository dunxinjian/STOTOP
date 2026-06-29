using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IPositionService
{
    Task<(List<PositionDto> Items, int Total)> GetPagedListAsync(int pageIndex, int pageSize, string? keyword);
    Task<PositionDto?> GetByIdAsync(long id);
    Task<PositionDto> CreateAsync(CreatePositionRequest request);
    Task UpdateAsync(long id, UpdatePositionRequest request);
    Task DeleteAsync(long id);
    Task AssignOrganizationsAsync(long positionId, long[] organizationIds);
    Task AssignUsersAsync(long positionId, long[] userIds);
    Task<List<PositionDto>> GetByOrganizationAsync(long orgId);
    Task<List<PositionDto>> GetByUserAsync(long userId);
}
