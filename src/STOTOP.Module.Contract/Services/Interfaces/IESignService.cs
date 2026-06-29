using STOTOP.Core.Models;
using STOTOP.Module.Contract.Dtos;

namespace STOTOP.Module.Contract.Services.Interfaces;

public interface IESignService
{
    Task<PagedResult<ESignRecordDto>> GetRecordsAsync(ESignRecordQueryRequest request);
    Task<ESignRecordDto?> GetRecordByIdAsync(long id);
    Task<ESignRecordDto> CreateRecordAsync(CreateESignRecordRequest request);
    Task<ESignRecordDto?> CompleteSignAsync(long id, ManualSignRequest request);
    Task<bool> RejectSignAsync(long id);
    Task<bool> DeleteRecordAsync(long id);
}
