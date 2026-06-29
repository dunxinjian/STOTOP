using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IStatisticsService
{
    Task<DormitoryStatisticsDto> GetStatisticsAsync();
}
