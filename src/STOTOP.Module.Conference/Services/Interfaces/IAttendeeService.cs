using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IAttendeeService
{
    Task<PagedResult<AttendeeListItemDto>> GetAttendeesAsync(int eventId, AttendeeQueryRequest request);
    Task<AttendeeDto?> GetAttendeeByIdAsync(int id);
    Task<AttendeeDto> CreateAttendeeAsync(int eventId, CreateAttendeeRequest request);
    Task<AttendeeDto?> UpdateAttendeeAsync(int id, UpdateAttendeeRequest request);
    Task<bool> DeleteAttendeeAsync(int id);
    Task<AttendeeImpactAnalysisDto> GetImpactAnalysisAsync(int id);
    Task<bool> ApplyChangesAsync(int id);
    Task<List<AttendeeDto>> ImportAttendeesAsync(int eventId, Stream excelStream);
    Task<byte[]> ExportAttendeesAsync(int eventId);
    Task<byte[]> GenerateImportTemplateAsync();
    Task<List<AttendeeDto>> GetCompanionsAsync(long primaryGuestId);
    Task BatchUpdateStatusAsync(BatchUpdateStatusRequest request);
    Task UpdateRoomPreferenceAsync(long attendeeId, string? preferredRoomType);
}
