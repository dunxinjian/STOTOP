using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services;

public interface IDbConnectionService
{
    Task<List<DbConnectionDto>> GetListAsync();
    Task<DbConnectionDto> GetByIdAsync(long id);
    Task<DbConnectionDto> CreateAsync(DbConnectionCreateDto dto);
    Task<DbConnectionDto> UpdateAsync(long id, DbConnectionUpdateDto dto);
    Task DeleteAsync(long id);
    Task<bool> TestConnectionAsync(DbConnectionTestDto dto);
    Task<List<DbConnectionOptionDto>> GetOptionsAsync();
    Task<List<DbTableDto>> GetTablesAsync(long connectionId);
    Task<List<DbColumnDto>> GetColumnsAsync(long connectionId, string tableName);
    Task<string> GetConnectionStringAsync(long connectionId);
    Task<bool> HasSystemConnectionAsync();
}
