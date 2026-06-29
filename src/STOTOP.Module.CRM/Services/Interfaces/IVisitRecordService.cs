using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;

namespace STOTOP.Module.CRM.Services.Interfaces;

public interface IVisitRecordService
{
    Task<PagedResult<VisitRecordListItemDto>> GetVisitRecordsAsync(VisitRecordQueryRequest request);
    Task<VisitRecordDto?> GetVisitRecordByIdAsync(long id);
    Task<VisitRecordDto> CreateVisitRecordAsync(CreateVisitRecordRequest request);
    Task<VisitRecordDto?> UpdateVisitRecordAsync(long id, UpdateVisitRecordRequest request);
    Task<bool> DeleteVisitRecordAsync(long id);
    Task<List<VisitRecordListItemDto>> GetPendingFollowUpAsync(long? visitorId = null);
    Task<VisitStatisticsDto> GetStatisticsAsync(long? visitorId = null, long? orgId = null);
}
