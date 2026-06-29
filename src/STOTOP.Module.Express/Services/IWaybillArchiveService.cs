using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IWaybillArchiveService
{
    /// <summary>执行归档</summary>
    Task<ArchiveResultDto> ExecuteArchiveAsync();
    /// <summary>获取归档统计</summary>
    Task<ArchiveStatsDto> GetArchiveStatsAsync();
}
