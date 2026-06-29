using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(string? brandCode);
}
