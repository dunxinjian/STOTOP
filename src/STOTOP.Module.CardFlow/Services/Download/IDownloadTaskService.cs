using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Download;

public interface IDownloadTaskService
{
    Task<List<DownloadTaskDto>> GetListAsync();
    Task<DownloadTaskDetailDto?> GetByIdAsync(long id);
    Task<DownloadTaskDetailDto> CreateAsync(DownloadTaskCreateDto dto);
    Task<DownloadTaskDetailDto> UpdateAsync(long id, DownloadTaskUpdateDto dto);
    Task DeleteAsync(long id);
    Task<List<DownloadLogDto>> GetLogsAsync(long? taskId = null);
}
