using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;

namespace STOTOP.Module.Dormitory.Services.Interfaces;

public interface IHygieneCheckService
{
    Task<PagedResult<HygieneCheckListItemDto>> GetHygieneChecksAsync(HygieneCheckQueryRequest request);
    Task<HygieneCheckDto?> GetHygieneCheckByIdAsync(long id);
    Task<HygieneCheckDto> CreateHygieneCheckAsync(CreateHygieneCheckRequest request);
    Task<HygieneCheckDto?> UpdateHygieneCheckAsync(long id, UpdateHygieneCheckRequest request);
    Task<bool> DeleteHygieneCheckAsync(long id);
}
