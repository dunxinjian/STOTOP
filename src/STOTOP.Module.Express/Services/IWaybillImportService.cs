using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IWaybillImportService
{
    Task<WaybillImportResult> ImportAsync(Stream excelStream, string brandCode);
}
