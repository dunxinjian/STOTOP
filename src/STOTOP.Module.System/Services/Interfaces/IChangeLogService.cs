using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface IChangeLogService
{
    Task LogChangeAsync(string businessType, long businessId, string businessName,
                        string operationType, string changeContent, long? operatorId, string? operatorName);
    Task<(List<ChangeLogDto> Items, int Total)> GetPagedListAsync(ChangeLogQueryRequest request);
    Task<List<ChangeLogDto>> GetByBusinessAsync(string businessType, long businessId);
    string CompareAndSerialize<T>(T oldEntity, T newEntity, params string[] excludeProperties);
}
