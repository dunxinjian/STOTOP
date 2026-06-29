using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

public interface ISchemaSyncManageService
{
    Task<SchemaSyncStatusDto> GetStatusAsync();
    Task<List<SchemaChangeItemDto>> GetPendingChangesAsync();
    Task ExecuteChangesAsync(List<long> changeIds, string? executedBy);
    Task SkipChangesAsync(List<long> changeIds);
    Task<List<SchemaWarningItemDto>> GetWarningsAsync();
    Task<(List<MigrationHistoryItemDto> Items, int Total)> GetHistoryAsync(int pageIndex, int pageSize);
}
