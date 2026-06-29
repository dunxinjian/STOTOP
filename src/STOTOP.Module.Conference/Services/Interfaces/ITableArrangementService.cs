using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface ITableArrangementService
{
    Task<List<TableDto>> GetTablesAsync(int mealId);
    Task<TableDto> CreateTableAsync(int mealId, CreateTableRequest request);
    Task<TableDto?> UpdateTableAsync(int id, UpdateTableRequest request);
    Task<bool> DeleteTableAsync(int id);
    Task<TableDto?> SetTableSeatsAsync(int id, TableSeatRequest request);
    Task<AutoArrangePreviewDto> AutoArrangeAsync(int mealId, AutoArrangeConfigRequest request);
    Task<byte[]> ExportImageAsync(int mealId);
    Task<byte[]> ExportPdfAsync(int mealId);
    Task<byte[]> ExportExcelAsync(int mealId);
}
