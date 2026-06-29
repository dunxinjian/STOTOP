using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IWaybillService
{
    Task<PagedResult<WaybillListItemDto>> GetListAsync(WaybillQueryRequest request);
    Task<WaybillDto?> GetByIdAsync(long id);
    Task<WaybillDto?> GetByWaybillNoAsync(string waybillNo, string brandCode);
}
